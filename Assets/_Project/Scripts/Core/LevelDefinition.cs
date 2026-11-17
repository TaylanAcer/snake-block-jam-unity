
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class LevelDefinition
    {
        public int Width { get; }
        public int Height { get; }
        public int? MoveLimit { get; }
        public IReadOnlyList<SnakeDefinition> Snakes { get; }

        public LevelDefinition(
            int width,
            int height,
            int? moveLimit = null,
            IReadOnlyList<SnakeDefinition> snakes = null)
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
        }
    }
}
