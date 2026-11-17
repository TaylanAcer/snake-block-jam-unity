using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class SnakeDefinition
    {
        public int Id { get; }
        public Direction Direction { get; }

        // Asıl veri: baştan kuyruğa, her segmentin hücresi ve katmanı (R-BRG-10).
        public IReadOnlyList<LayeredCell> Body { get; }

        // Geriye dönük uyumluluk: Body'deki hücreler, aynı sırada.
        public IReadOnlyList<Cell> Segments { get; }

        // Eski constructor: bütün segmentler zeminde.
        public SnakeDefinition(
            int id,
            Direction direction,
            IReadOnlyList<Cell> segments)
            : this(id, direction, ToGroundBody(segments))
        {
        }

        public SnakeDefinition(
            int id,
            Direction direction,
            IReadOnlyList<LayeredCell> body)
        {
            Id = id;
            Direction = direction;

            var bodyCopy = new List<LayeredCell>(body);
            var cells = new List<Cell>(bodyCopy.Count);

            foreach (var segment in bodyCopy)
            {
                cells.Add(segment.Cell);
            }

            Body = bodyCopy.AsReadOnly();
            Segments = cells.AsReadOnly();
        }

        private static List<LayeredCell> ToGroundBody(IReadOnlyList<Cell> segments)
        {
            var body = new List<LayeredCell>(segments.Count);

            foreach (var cell in segments)
            {
                body.Add(new LayeredCell(cell, BoardLayer.Ground));
            }

            return body;
        }
    }
}
