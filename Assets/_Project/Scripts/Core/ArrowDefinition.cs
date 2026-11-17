
namespace SnakeBlockJam.Core
{
    // Statik ok: baş bu hücreye girince yılan Direction yönüne döner (R-ARW-04).
    // Katmanı yok: L1–20'de köprü ve ok aynı level'da bulunmuyor (spec §7).
    public sealed class ArrowDefinition
    {
        public Cell Position { get; }
        public Direction Direction { get; }

        public ArrowDefinition(Cell position, Direction direction)
        {
            Position = position;
            Direction = direction;
        }
    }
}
