using System;
using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Bir yılanın görsel olarak izlediği yol (saf hesap, MonoBehaviour değil).
    //
    // Noktalar sırayla: kuyruk … baş (gövde tersten) + HeadPath + (tahtadan
    // kaçışta) FinalDirection yönünde ekran dışı uzatma. Komşu noktalar arası
    // 1 hücre. Segment i (0 = baş) iz üzerinde (N - 1 - i) + travel
    // mesafesindedir; böylece gövde başın izini hücre hücre takip eder (R-MOV-03)
    // ve ok/L köprü köşelerinde kıvrılır. Sözleşme: MoveResult yorumu.
    public sealed class SnakeTrack
    {
        private readonly List<Vector3> _points;

        public int BodyLength { get; }

        private List<int> _arrowSteps;
        public bool HasReachedArrow(int index, float travel) =>
            _arrowSteps != null && index < _arrowSteps.Count && travel >= _arrowSteps[index];

        // İzin toplam uzunluğu (hücre).
        public float Length => _points.Count - 1;

        private SnakeTrack(List<Vector3> points, int bodyLength)
        {
            _points = points;
            BodyLength = bodyLength;
        }

        // Duran yılan: gövde + baştan bir hücre ileri (başın yönünü vermek için).
        public static SnakeTrack ForBody(SnakeDefinition snake, BoardGeometry geometry)
        {
            var cells = new List<Cell>(snake.Body.Count + 1);
            var points = BodyPoints(snake, geometry, 1, cells);
            LayeredCell head = snake.Body[0];
            Cell ahead = head.Cell + snake.Direction.ToOffset();
            points.Add(geometry.CellToWorld(ahead, head.Layer));
            cells.Add(ahead);
            StepShoulders(points, cells, geometry);
            return new SnakeTrack(points, snake.Body.Count);
        }

        // Hamle izi. 'extensionCells' sadece tahtadan kaçışta eklenir; yılanın
        // ekrandan tamamen çıkmasına yetecek kadar uzun olmalıdır (R-MOV-07).
        public static SnakeTrack ForMove(
            SnakeDefinition snake, MoveResult result, BoardGeometry geometry, int extensionCells,
            IReadOnlyList<ArrowDefinition> arrows = null)
        {
            if (result.SnakeId != snake.Id)
                throw new ArgumentException(
                    $"Result belongs to snake {result.SnakeId}, not {snake.Id}.", nameof(result));

            bool offBoard = result.Outcome == MoveOutcome.EscapedOffBoard;
            bool blocked = result.Outcome == MoveOutcome.Blocked;

            int capacity = snake.Body.Count + result.HeadPath.Count
                           + (offBoard ? extensionCells : 0) + (blocked ? 1 : 0);
            var cells = new List<Cell>(capacity);
            var points = BodyPoints(snake, geometry, capacity - snake.Body.Count, cells);

            var track = new SnakeTrack(points, snake.Body.Count);
            for (int i = 0; i < result.HeadPath.Count; i++)
            {
                LayeredCell step = result.HeadPath[i];
                points.Add(geometry.CellToWorld(step.Cell, step.Layer));
                cells.Add(step.Cell);
                for (int j = 0; j < (arrows?.Count ?? 0); j++)
                {
                    if (arrows[j].Position != step.Cell)
                        continue;
                    (track._arrowSteps ??= new List<int>()).Add(i + 1);
                    break;
                }
            }
            if (blocked)
            {
                LayeredCell blockedCell = result.BlockedAt.Value;

                points.Add(
                    geometry.CellToWorld(
                        blockedCell.Cell,
                        blockedCell.Layer
                    )
                );
                cells.Add(blockedCell.Cell);
            }

            if (offBoard)
            {
                Cell cell = result.HeadPath.Count > 0
                    ? result.HeadPath[result.HeadPath.Count - 1].Cell
                    : snake.Body[0].Cell;
                Cell offset = result.FinalDirection.ToOffset();

                for (int i = 0; i < extensionCells; i++)
                {
                    cell += offset;
                    points.Add(geometry.CellToWorld(cell));
                    cells.Add(cell);
                }
            }

            StepShoulders(points, cells, geometry);
            return track;
        }

        // 1 hücrelik köprünün omzundaki zemin noktası, iz üzerinde köprü üstü bir
        // noktaya komşuysa (yılan oradan köprüye çıkıyor/iniyor) ara yüksekliğe alınır.
        // Komşuluk orijinal yüksekliklerle kontrol edilir; yalnız zemin noktaları değişir.
        private static void StepShoulders(List<Vector3> points, List<Cell> cells, BoardGeometry geometry)
        {
            if (!geometry.HasBridgeShoulders)
                return;

            const float epsilon = 1e-3f;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].y > epsilon || !geometry.IsBridgeShoulder(cells[i]))
                    continue;

                bool nextOnTop = i + 1 < points.Count && points[i + 1].y >= BoardGeometry.BridgeTopHeight - epsilon;
                bool previousOnTop = i > 0 && points[i - 1].y >= BoardGeometry.BridgeTopHeight - epsilon;

                if (!nextOnTop && !previousOnTop)
                    continue;

                Vector3 point = points[i];
                point.y = BoardGeometry.BridgeShoulderHeight;
                points[i] = point;
            }
        }

        // Segmentin iz üzerindeki mesafesi.
        public float SegmentDistance(int segmentIndex, float travel)
        {
            return BodyLength - 1 - segmentIndex + travel;
        }

        // Köprüye çıkış/iniş: yükseklik değişimi hücrenin bu kadarında biter.
        // Doğrusal olsaydı başın ön yüzü tablanın ön kenarına girerdi (tabla uç
        // hücre merkezinden 0,15 önce başlıyor, baş yarı genişliği 0,44).
        public const float RampPortion = 0.4f;

        // İz üzerindeki nokta; uçlarda sabitlenir. Yatayda doğrusal; katman
        // değişen parçada yükseklik çıkışta başta, inişte sonda değişir.
        public Vector3 Sample(float distance)
        {
            if (distance <= 0f)
                return _points[0];

            int last = _points.Count - 1;

            if (distance >= last)
                return _points[last];

            int index = (int)distance;
            float t = distance - index;
            Vector3 from = _points[index];
            Vector3 to = _points[index + 1];
            Vector3 point = Vector3.Lerp(from, to, t);

            if (to.y > from.y)
                point.y = Mathf.Lerp(from.y, to.y, Mathf.Clamp01(t / RampPortion));
            else if (to.y < from.y)
                point.y = Mathf.Lerp(from.y, to.y, Mathf.Clamp01((t - (1f - RampPortion)) / RampPortion));

            return point;
        }

        // O noktadaki yatay ilerleme yönü. Tam köşe noktasında ileri parça.
        public Vector3 Direction(float distance)
        {
            if (_points.Count < 2)
                return Vector3.forward;

            int index = Mathf.Clamp(Mathf.FloorToInt(distance), 0, _points.Count - 2);
            Vector3 direction = _points[index + 1] - _points[index];
            direction.y = 0f;
            return direction.normalized;
        }

        private static List<Vector3> BodyPoints(
            SnakeDefinition snake, BoardGeometry geometry, int extraCapacity, List<Cell> cells)
        {
            var points = new List<Vector3>(snake.Body.Count + extraCapacity);

            for (int i = snake.Body.Count - 1; i >= 0; i--)
            {
                points.Add(geometry.CellToWorld(snake.Body[i].Cell, snake.Body[i].Layer));
                cells.Add(snake.Body[i].Cell);
            }

            return points;
        }
    }
}
