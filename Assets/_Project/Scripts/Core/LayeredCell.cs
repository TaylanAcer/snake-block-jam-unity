
using System;

namespace SnakeBlockJam.Core
{
    public readonly struct LayeredCell : IEquatable<LayeredCell>
    {
        public Cell Cell { get; }
        public BoardLayer Layer { get; }

        public LayeredCell(Cell cell, BoardLayer layer)
        {
            Cell = cell;
            Layer = layer;
        }

        public bool Equals(LayeredCell other)
        {
            return Cell.Equals(other.Cell)
                && Layer == other.Layer;
        }

        public override bool Equals(object obj)
        {
            return obj is LayeredCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Cell.GetHashCode() * 397) ^ (int)Layer;
            }
        }

        public static bool operator ==(LayeredCell a, LayeredCell b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(LayeredCell a, LayeredCell b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return $"{Cell} [{Layer}]";
        }
    }
}
