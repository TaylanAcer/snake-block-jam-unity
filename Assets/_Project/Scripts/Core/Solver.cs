using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    // Greedy çözücü (yol haritası §7.4). Oyun monotoniktir: başarısız hamle
    // geometriyi değiştirmez (R-COL-02), kaçış sadece hücre boşaltır (R-CON-04),
    // ok ve tünel tükenmez (R-ARW-05, R-BUR-05). Bu yüzden şu an kaçabilen bir
    // yılan ileride de kaçabilir ve onu kaçırmak hiçbir çözümü bozmaz:
    // "kaçabilen herhangi bir yılanı kaçır" tekrarı tahtayı boşaltmıyorsa
    // level çözülemez. Her yılan tam bir hamle harcar; bu en az hamledir.
    //
    // Kullanımlar: İpucu (R-HNT-01), level regression testi, ileride editor
    // validation'ı. Durumu değiştirmez; SolveGreedy kendi kopyasında çalışır.
    public static class Solver
    {
        // Şu an kaçabilen aktif yılanlar, level'daki sırayla.
        public static List<int> EscapingSnakes(BoardState state)
        {
            var escaping = new List<int>();

            foreach (var snake in state.Level.Snakes)
            {
                if (state.IsActive(snake.Id) && MoveSimulator.Simulate(state, snake.Id).IsEscaped)
                    escaping.Add(snake.Id);
            }

            return escaping;
        }

        // İpucu: kaçabilen yılanlardan, 'previousSnakeId'den sonraki (sırayla,
        // sona gelince başa sarar). Böylece art arda iki kullanım farklı yılan
        // gösterebilir (R-HNT-06). Referansın seçim kuralı UNKNOWN (R-HNT-07);
        // bu kural proje kararıdır (ASSUMED). Kaçabilen yılan yoksa 0.
        public static int PickHint(BoardState state, int previousSnakeId = 0)
        {
            List<int> escaping = EscapingSnakes(state);

            if (escaping.Count == 0)
                return 0;

            int index = escaping.IndexOf(previousSnakeId);
            return escaping[(index + 1) % escaping.Count];
        }

        // Level'ı baştan greedy çözer. Hamle limiti dahil: limit yetmezse
        // durum Lost olur ve Solved false döner.
        public static SolveResult SolveGreedy(LevelDefinition level)
        {
            var state = new BoardState(level);
            var order = new List<int>();

            while (state.Status == GameStatus.Playing)
            {
                List<int> escaping = EscapingSnakes(state);

                if (escaping.Count == 0)
                    break;

                int snakeId = escaping[0];
                state.Apply(MoveSimulator.Simulate(state, snakeId));
                order.Add(snakeId);
            }

            return new SolveResult(state.Status == GameStatus.Won, order, state.ActiveSnakeCount);
        }
    }

    public sealed class SolveResult
    {
        public bool Solved { get; }

        // Kaçış sırası (yılan id'leri). Her biri 1 hamle.
        public IReadOnlyList<int> Order { get; }

        // Çözülemediyse tahtada kalan yılan sayısı.
        public int RemainingSnakes { get; }

        public SolveResult(bool solved, List<int> order, int remainingSnakes)
        {
            Solved = solved;
            Order = order.AsReadOnly();
            RemainingSnakes = remainingSnakes;
        }
    }
}
