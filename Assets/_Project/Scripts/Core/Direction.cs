
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
