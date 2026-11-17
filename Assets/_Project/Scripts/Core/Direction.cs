
using System;

namespace SnakeBlockJam.Core
{
    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    public static class DirectionExtensions
    {
        public static Cell ToOffset(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return new Cell(0, 1);

                case Direction.Right:
                    return new Cell(1, 0);

                case Direction.Down:
                    return new Cell(0, -1);

                case Direction.Left:
                    return new Cell(-1, 0);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(direction), direction, null);
            }
        }

        // Komşu iki hücre arasındaki adım yönü (köprü yolu, view izi).
        public static Direction Between(Cell from, Cell to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (dx == 0 && dy == 1) return Direction.Up;
            if (dx == 1 && dy == 0) return Direction.Right;
            if (dx == 0 && dy == -1) return Direction.Down;
            if (dx == -1 && dy == 0) return Direction.Left;

            throw new ArgumentException($"Cells {from} and {to} are not adjacent.");
        }

        public static BridgeAxis ToAxis(this Direction direction)
        {
            return direction == Direction.Up || direction == Direction.Down
                ? BridgeAxis.Vertical
                : BridgeAxis.Horizontal;
        }

        public static Direction Opposite(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Direction.Down;

                case Direction.Right:
                    return Direction.Left;

                case Direction.Down:
                    return Direction.Up;

                case Direction.Left:
                    return Direction.Right;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(direction), direction, null);
            }
        }
    }
}
