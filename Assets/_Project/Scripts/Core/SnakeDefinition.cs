using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class SnakeDefinition
    {
        public int Id { get; }
        public Direction Direction { get; }
        public IReadOnlyList<Cell> Segments { get; }

        public SnakeDefinition(
            int id,
            Direction direction,
            IReadOnlyList<Cell> segments)
        {
            Id = id;
            Direction = direction;
            Segments = new List<Cell>(segments).AsReadOnly();
        }
    }
}