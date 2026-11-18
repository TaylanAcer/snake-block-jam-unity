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
            SnakeDefinition snake, MoveResult result, BoardGeometry geometry, int extensionCells)
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

            foreach (var step in result.HeadPath)
                points.Add(geometry.CellToWorld(step.Cell, step.Layer));
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

            return new SnakeTrack(points, snake.Body.Count);
        }

        // Segmentin iz üzerindeki mesafesi.
        public float SegmentDistance(int segmentIndex, float travel)
        {
            return BodyLength - 1 - segmentIndex + travel;
        }

        // İz üzerindeki nokta; uçlarda sabitlenir.
        public Vector3 Sample(float distance)
        {
            if (distance <= 0f)
                return _points[0];

            int last = _points.Count - 1;

            if (distance >= last)
                return _points[last];

            int index = (int)distance;
            return Vector3.Lerp(_points[index], _points[index + 1], distance - index);
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
