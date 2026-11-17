
namespace SnakeBlockJam.Core
{
    public sealed class BurrowDefinition
    {
        public Cell Position { get; }

        public BurrowDefinition(Cell position)
        {
            Position = position;
        }
    }
}
