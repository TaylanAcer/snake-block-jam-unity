
using System;
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    // Level'ın statik mekaniklerine hücre başına O(1) erişim: tünel, ok,
    // köprü (köprü index'i + yol üzerindeki sıra). Değişmez; BoardState
    // kurulurken bir kez oluşturulur ve yerleşimi doğrular.
    //
    // Reddedilen yerleşimler (spec §17, proje kararı; L1–L20'de görülmedi):
    // ok/tünelin köprü hücresinde, başka bir mekanikle aynı hücrede veya
    // yılan altında olması.
    internal sealed class BoardMap
    {
        private const int None = -1;

        private readonly int _width;
        private readonly bool[] _burrow;
        private readonly int[] _arrow;
        private readonly int[] _bridge;
        private readonly int[] _bridgeStep;

        public IReadOnlyList<BridgeDefinition> Bridges { get; }

        public BoardMap(LevelDefinition level)
        {
            _width = level.Width;
            int size = checked(level.Width * level.Height);

            _burrow = new bool[size];
            _arrow = Filled(size);
            _bridge = Filled(size);
            _bridgeStep = Filled(size);
            Bridges = level.Bridges;

            for (int b = 0; b < level.Bridges.Count; b++)
                AddBridge(level, b);

            foreach (var arrow in level.Arrows)
            {
                if (arrow == null)
                    throw new ArgumentException("Invalid arrow.");

                int index = RequireFreeCell(level, arrow.Position, "Arrow");
                _arrow[index] = (int)arrow.Direction;
            }

            foreach (var burrow in level.Burrows)
            {
                if (burrow == null)
                    throw new ArgumentException("Invalid burrow.");

                int index = RequireFreeCell(level, burrow.Position, "Burrow");
                _burrow[index] = true;
            }

            // Yılan hücrelerinin tahta içinde olduğu BoardState'te kontrol edildi.
            foreach (var snake in level.Snakes)
            {
                foreach (var segment in snake.Body)
                {
                    int index = Index(segment.Cell);

                    if (_arrow[index] != None || _burrow[index])
                        throw new ArgumentException(
                            $"Snake {snake.Id} lies on an arrow or burrow at {segment.Cell}.");

                    if (segment.Layer == BoardLayer.BridgeTop && _bridge[index] == None)
                        throw new ArgumentException(
                            $"Snake {snake.Id} has a BridgeTop segment off any bridge at {segment.Cell}.");
                }

                // Üst katta başlayan baş yol boyunca bakmalı (R-BRG-08).
                if (snake.Body.Count > 0 &&
                    snake.Body[0].Layer == BoardLayer.BridgeTop &&
                    !TryGetTopTravel(snake.Body[0].Cell, snake.Direction, out _, out _, out _))
                    throw new ArgumentException(
                        $"Snake {snake.Id} is on a bridge but faces across its road.");
            }
        }

        // Köprünün dışından 'to' hücresine 'direction' yönünde giren baş üst
        // kata çıkar mı? Sadece yolun bir ucundan, yol doğrultusunda içeri
        // girerken (R-BRG-05). 1 hücrelik köprüde yön köprü ekseniyle aynı
        // olmalıdır (R-BRG-11). Dirsekten veya yolun ortasından gelen zemindeki
        // yılan üst kata çıkmaz (R-BRG-12); aynı köprünün altından gelen yılan
        // zeminde kalır (R-BRG-13).
        // sign: yol üzerindeki ilerleme yönü (+1: Path sırası, -1: ters).
        public bool TryEnterTop(
            Cell from, Cell to, Direction direction,
            out int bridge, out int step, out int sign)
        {
            sign = 0;

            if (!TryGetBridge(to, out bridge, out step))
                return false;

            if (TryGetBridge(from, out int fromBridge, out _) && fromBridge == bridge)
                return false;

            var path = Bridges[bridge].Path;
            int last = path.Count - 1;
            Cell ahead = to + direction.ToOffset();

            if (last == 0)
            {
                if (direction.ToAxis() != Bridges[bridge].Axis)
                    return false;

                sign = 1;
            }
            else if (step == 0 && path[1] == ahead)
                sign = 1;
            else if (step == last && path[last - 1] == ahead)
                sign = -1;
            else
                return false;

            return true;
        }

        // Üst katta duran başın yol üzerindeki ilerleme yönü: önce geldiği
        // taraftan (gövdenin bıraktığı iz), sonra baktığı taraftan çıkarılır.
        // Yolun ucunda dışarı bakan baş bir sonraki adımda zemine iner.
        public bool TryGetTopTravel(
            Cell head, Direction direction,
            out int bridge, out int step, out int sign)
        {
            sign = 0;

            if (!TryGetBridge(head, out bridge, out step))
                return false;

            var path = Bridges[bridge].Path;
            int last = path.Count - 1;
            Cell offset = direction.ToOffset();
            var behind = new Cell(head.X - offset.X, head.Y - offset.Y);
            Cell ahead = head + offset;

            if (step > 0 && path[step - 1] == behind)
                sign = 1;
            else if (step < last && path[step + 1] == behind)
                sign = -1;
            else if (step < last && path[step + 1] == ahead)
                sign = 1;
            else if (step > 0 && path[step - 1] == ahead)
                sign = -1;
            else if (last == 0 && direction.ToAxis() == Bridges[bridge].Axis)
                sign = 1;
            else
                return false;

            return true;
        }

        public bool IsBurrow(Cell cell)
        {
            return _burrow[Index(cell)];
        }

        public bool TryGetArrow(Cell cell, out Direction direction)
        {
            int value = _arrow[Index(cell)];
            direction = value == None ? default : (Direction)value;
            return value != None;
        }

        // step: hücrenin köprü yolundaki sırası (Path[step] == cell).
        public bool TryGetBridge(Cell cell, out int bridge, out int step)
        {
            int index = Index(cell);
            bridge = _bridge[index];
            step = _bridgeStep[index];
            return bridge != None;
        }

        private void AddBridge(LevelDefinition level, int b)
        {
            var bridge = level.Bridges[b];

            if (bridge == null)
                throw new ArgumentException("Invalid bridge.");

            for (int i = 0; i < bridge.Path.Count; i++)
            {
                Cell cell = bridge.Path[i];
                RequireInside(level, cell, "Bridge");

                if (i > 0 && !AreAdjacent(bridge.Path[i - 1], cell))
                    throw new ArgumentException(
                        $"Bridge {b} path is not contiguous at {cell}.");

                int index = Index(cell);

                if (_bridge[index] != None)
                    throw new ArgumentException(
                        $"Bridge cell {cell} is used more than once.");

                _bridge[index] = b;
                _bridgeStep[index] = i;
            }
        }

        private int RequireFreeCell(LevelDefinition level, Cell cell, string kind)
        {
            RequireInside(level, cell, kind);
            int index = Index(cell);

            if (_bridge[index] != None || _arrow[index] != None || _burrow[index])
                throw new ArgumentException(
                    $"{kind} at {cell} shares its cell with a bridge, arrow or burrow.");

            return index;
        }

        private static void RequireInside(LevelDefinition level, Cell cell, string kind)
        {
            if (cell.X < 0 || cell.X >= level.Width || cell.Y < 0 || cell.Y >= level.Height)
                throw new ArgumentException($"{kind} cell {cell} is outside the board.");
        }

        private static bool AreAdjacent(Cell a, Cell b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
        }

        private int Index(Cell cell)
        {
            return cell.Y * _width + cell.X;
        }

        private static int[] Filled(int size)
        {
            var array = new int[size];

            for (int i = 0; i < size; i++)
                array[i] = None;

            return array;
        }
    }
}
