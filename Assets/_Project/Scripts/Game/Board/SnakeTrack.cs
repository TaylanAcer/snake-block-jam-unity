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
            var points = BodyPoints(snake, geometry, 1);
            LayeredCell head = snake.Body[0];
            points.Add(geometry.CellToWorld(head.Cell + snake.Direction.ToOffset(), head.Layer));
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

            var points = BodyPoints(
                snake,
                geometry,
                result.HeadPath.Count
                    + (offBoard ? extensionCells : 0)
                    + (blocked ? 1 : 0)
            );

            var track = new SnakeTrack(points, snake.Body.Count);
            for (int i = 0; i < result.HeadPath.Count; i++)
            {
                LayeredCell step = result.HeadPath[i];
                points.Add(geometry.CellToWorld(step.Cell, step.Layer));
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
                }
            }

            return track;
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

        private static List<Vector3> BodyPoints(SnakeDefinition snake, BoardGeometry geometry, int extraCapacity)
        {
            var points = new List<Vector3>(snake.Body.Count + extraCapacity);

            for (int i = snake.Body.Count - 1; i >= 0; i--)
                points.Add(geometry.CellToWorld(snake.Body[i].Cell, snake.Body[i].Layer));

            return points;
        }
    }
}
