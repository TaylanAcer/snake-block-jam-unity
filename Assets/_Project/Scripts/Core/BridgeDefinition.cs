
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class BridgeDefinition
    {
        public IReadOnlyList<Cell> Path { get; }

        public BridgeDefinition(IReadOnlyList<Cell> path)
        {
            Path = new List<Cell>(path).AsReadOnly();
        }
    }
}
