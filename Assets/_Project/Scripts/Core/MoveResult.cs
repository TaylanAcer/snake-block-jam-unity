
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    // Bir dokunuşun sonucu. Sadece veri; durumu değiştirmez.
    // View tam izi şöyle kurar: Body (tersten) + HeadPath (+ ekran dışı uzatma).
    // Blocked'da geri dönüş aynı izin tersten oynatılmasıdır (R-COL-05).
    public sealed class MoveResult
    {
        public int SnakeId { get; }
        public MoveOutcome Outcome { get; }
        public bool IsEscaped => Outcome != MoveOutcome.Blocked;

        // Başın girdiği hücreler, katmanlarıyla; başlangıç hücresi hariç.
        // EscapedOffBoard: tahtadaki son hücrede biter.
        // Blocked: temastan önceki son boş hücrede biter (boş olabilir).
        public IReadOnlyList<LayeredCell> HeadPath { get; }

        // Son adımdaki baş yönü: kaçışta ekran dışı uzatma, çarpmada temas yönü.
        public Direction FinalDirection { get; }

        // Sadece Blocked: çarpılan slot ve sahibi (R-COL-06 göçüğü için).
        public LayeredCell? BlockedAt { get; }
        public int BlockerSnakeId { get; }

        private MoveResult(
            int snakeId,
            MoveOutcome outcome,
            List<LayeredCell> headPath,
            Direction finalDirection,
            LayeredCell? blockedAt,
            int blockerSnakeId)
        {
            SnakeId = snakeId;
            Outcome = outcome;
            HeadPath = headPath.AsReadOnly();
            FinalDirection = finalDirection;
            BlockedAt = blockedAt;
            BlockerSnakeId = blockerSnakeId;
        }

        // Liste sahipliği sonuca geçer; çağıran listeyi sonra değiştirmemelidir.
        internal static MoveResult EscapedOffBoard(
            int snakeId,
            List<LayeredCell> headPath,
            Direction finalDirection)
        {
            return new MoveResult(
                snakeId, MoveOutcome.EscapedOffBoard, headPath,
                finalDirection, null, 0);
        }

        internal static MoveResult EscapedIntoBurrow(
            int snakeId,
            List<LayeredCell> headPath,
            Direction finalDirection)
        {
            return new MoveResult(
                snakeId, MoveOutcome.EscapedIntoBurrow, headPath,
                finalDirection, null, 0);
        }

        internal static MoveResult Blocked(
            int snakeId,
            List<LayeredCell> headPath,
            Direction finalDirection,
            LayeredCell blockedAt,
            int blockerSnakeId)
        {
            return new MoveResult(
                snakeId, MoveOutcome.Blocked, headPath,
                finalDirection, blockedAt, blockerSnakeId);
        }
    }
}
