using System;
using System.Collections.Generic;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEngine;

namespace SnakeBlockJam.EditorTools
{
    public enum IssueSeverity { Error, Warning, Info }

    public readonly struct LevelIssue
    {
        public IssueSeverity Severity { get; }
        public string Message { get; }

        // Hatanın gösterileceği hücre (yoksa null): editor kırmızıya boyar.
        public Vector2Int? Cell { get; }

        public LevelIssue(IssueSeverity severity, string message, Vector2Int? cell = null)
        {
            Severity = severity;
            Message = message;
            Cell = cell;
        }

        public override string ToString()
        {
            return Cell.HasValue ? $"{Severity}: {Message} {Cell.Value}" : $"{Severity}: {Message}";
        }
    }

    // Level Editor doğrulaması (yol haritası §9.1). Kuralları yeniden yazmaz:
    // yerleşim kurallarını Core'un kendisi (BoardState / BoardMap), çözülebilirliği
    // Solver kontrol eder. Burada sadece Core'un bilmediği veri yapısı kontrolleri
    // (gövde bağlılığı, yön tutarlılığı) ve hücreyi işaretleme var.
    public static class LevelValidator
    {
        public static List<LevelIssue> Validate(LevelData level)
        {
            var issues = new List<LevelIssue>();

            if (level.width <= 0 || level.height <= 0)
            {
                issues.Add(new LevelIssue(IssueSeverity.Error, $"Tahta boyutu geçersiz: {level.width}×{level.height}."));
                return issues;
            }

            if (level.snakes.Count == 0)
                issues.Add(new LevelIssue(IssueSeverity.Error, "Level'da yılan yok."));

            if (level.hasMoveLimit && level.moveLimit <= 0)
                issues.Add(new LevelIssue(IssueSeverity.Error, "Hamle limiti açık ama 0 veya negatif."));

            CheckSnakes(level, issues);
            CheckBridges(level, issues);
            CheckCells(level, issues);

            if (HasErrors(issues))
                return issues;

            // Yerleşim kuralları: çakışma, köprü katmanı, ok/tünel üstünde yılan…
            LevelDefinition definition;

            try
            {
                definition = level.ToDefinition();
                new BoardState(definition);
            }
            catch (ArgumentException e)
            {
                issues.Add(new LevelIssue(IssueSeverity.Error, $"Core reddetti: {e.Message}"));
                return issues;
            }

            CheckSolvable(level, definition, issues);
            return issues;
        }

        public static bool HasErrors(List<LevelIssue> issues)
        {
            return issues.Exists(issue => issue.Severity == IssueSeverity.Error);
        }

        // Gövde 4-komşu, aynı yılanda aynı (hücre, katman) iki kez yok, yön boyun → baş.
        private static void CheckSnakes(LevelData level, List<LevelIssue> issues)
        {
            var ids = new HashSet<int>();

            foreach (var snake in level.snakes)
            {
                string name = $"Yılan {snake.id}";

                if (!ids.Add(snake.id))
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"{name}: aynı id iki yılanda."));

                if (snake.body.Count == 0)
                {
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"{name}: hücresi yok."));
                    continue;
                }

                for (int i = 1; i < snake.body.Count; i++)
                {
                    Vector2Int a = snake.body[i - 1].cell;
                    Vector2Int b = snake.body[i].cell;

                    if (!LevelEditing.IsNeighbour(a, b))
                        issues.Add(new LevelIssue(IssueSeverity.Error, $"{name}: gövde kopuk ({a} – {b}).", b));
                }

                var seen = new HashSet<(Vector2Int, BoardLayer)>();

                foreach (var segment in snake.body)
                {
                    if (!seen.Add((segment.cell, segment.layer)))
                        issues.Add(new LevelIssue(IssueSeverity.Error,
                            $"{name}: aynı hücreyi aynı katmanda iki kez kullanıyor.", segment.cell));
                }

                if (snake.body.Count >= 2 && LevelEditing.IsNeighbour(snake.body[1].cell, snake.body[0].cell))
                {
                    Direction expected = LevelEditing.DirectionBetween(snake.body[1].cell, snake.body[0].cell);

                    if (snake.direction != expected)
                        issues.Add(new LevelIssue(IssueSeverity.Error,
                            $"{name}: yön {snake.direction}, boyun → baş {expected}.", snake.body[0].cell));
                }
            }
        }

        private static void CheckBridges(LevelData level, List<LevelIssue> issues)
        {
            for (int b = 0; b < level.bridges.Count; b++)
            {
                List<Vector2Int> path = level.bridges[b].path;

                if (path.Count == 0)
                {
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Köprü {b + 1}: yolu boş."));
                    continue;
                }

                for (int i = 1; i < path.Count; i++)
                {
                    if (!LevelEditing.IsNeighbour(path[i - 1], path[i]))
                        issues.Add(new LevelIssue(IssueSeverity.Error,
                            $"Köprü {b + 1}: yol kopuk ({path[i - 1]} – {path[i]}).", path[i]));
                }

                if (path.Count == 1 && !level.bridges[b].hasAxis)
                    issues.Add(new LevelIssue(IssueSeverity.Error,
                        $"Köprü {b + 1}: 1 hücrelik köprüye eksen seçilmeli (R-BRG-11).", path[0]));
            }
        }

        // Tahta dışı hücreler: Core da yakalar ama hangi hücre olduğunu göstermek için.
        private static void CheckCells(LevelData level, List<LevelIssue> issues)
        {
            foreach (var snake in level.snakes)
            foreach (var segment in snake.body)
                CheckInside(level, segment.cell, $"Yılan {snake.id}", issues);

            foreach (var bridge in level.bridges)
            foreach (var cell in bridge.path)
                CheckInside(level, cell, "Köprü", issues);

            foreach (var cell in level.burrows)
                CheckInside(level, cell, "Tünel", issues);

            foreach (var arrow in level.arrows)
                CheckInside(level, arrow.cell, "Ok", issues);
        }

        private static void CheckInside(LevelData level, Vector2Int cell, string what, List<LevelIssue> issues)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= level.width || cell.y >= level.height)
                issues.Add(new LevelIssue(IssueSeverity.Error, $"{what}: hücre tahta dışında.", cell));
        }

        // Greedy (yol haritası §7.4): takılırsa hiçbir sırayla çözülemez.
        private static void CheckSolvable(LevelData level, LevelDefinition definition, List<LevelIssue> issues)
        {
            SolveResult result = Solver.SolveGreedy(definition);

            if (result.Solved)
            {
                string limit = level.hasMoveLimit ? $" (limit {level.moveLimit})" : "";
                issues.Add(new LevelIssue(IssueSeverity.Info,
                    $"Çözülebilir: {result.Order.Count} hamle{limit}. Sıra: {string.Join(", ", result.Order)}"));
                return;
            }

            int snakes = level.snakes.Count;

            if (level.hasMoveLimit && level.moveLimit < snakes)
                issues.Add(new LevelIssue(IssueSeverity.Error,
                    $"Çözülemez: {snakes} yılan var, hamle limiti {level.moveLimit}."));
            else
                issues.Add(new LevelIssue(IssueSeverity.Error,
                    $"Çözülemez: {result.RemainingSnakes} yılan hiçbir sırayla kaçamıyor."));
        }
    }
}
