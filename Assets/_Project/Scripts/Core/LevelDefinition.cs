
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class LevelDefinition
    {
        public int Width { get; }
        public int Height { get; }
        public int? MoveLimit { get; }
        public IReadOnlyList<SnakeDefinition> Snakes { get; }
        public IReadOnlyList<BridgeDefinition> Bridges { get; }
        public IReadOnlyList<BurrowDefinition> Burrows { get; }
        public IReadOnlyList<ArrowDefinition> Arrows { get; }

        public LevelDefinition(
            int width,
            int height,
            int? moveLimit = null,
            IReadOnlyList<SnakeDefinition> snakes = null,
            IReadOnlyList<BridgeDefinition> bridges = null,
            IReadOnlyList<BurrowDefinition> burrows = null,
            IReadOnlyList<ArrowDefinition> arrows = null)
        {
            Width = width;
            Height = height;
            MoveLimit = moveLimit;

            var snakeCopy = new List<SnakeDefinition>();

            if (snakes != null)
            {
                snakeCopy.AddRange(snakes);
            }

            Snakes = snakeCopy.AsReadOnly();
            Bridges = new List<BridgeDefinition>(
                bridges ?? new BridgeDefinition[0]
            ).AsReadOnly();

            Burrows = new List<BurrowDefinition>(
                burrows ?? new BurrowDefinition[0]
            ).AsReadOnly();

            Arrows = new List<ArrowDefinition>(
                arrows ?? new ArrowDefinition[0]
            ).AsReadOnly();
        }
    }
}
