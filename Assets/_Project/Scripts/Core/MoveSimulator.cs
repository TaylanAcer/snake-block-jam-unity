
using System;
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    // Saf fonksiyon: (BoardState, snakeId) → MoveResult. Durumu değiştirmez.
    //
    // Hamle çözülürken tahta statiktir (Model A', R-CON-04) ve gövde başın
    // yolunu izler (R-MOV-03). Gövdenin girdiği her hücreye baş daha önce
    // boşken girmiştir; bu yüzden yalnızca başın girdiği hücre kontrol edilir.
    // Baş tahtadan çıkınca veya tünele girince kaçış kesinleşir; kuyruğun
    // çıkışı sadece görseldir.
    //
    // Adım sırası: sonraki hücre (köprü üstünde yol takibi) → tahta dışı mı →
    // aynı katmanda başka yılan var mı → yola ekle → tünel mi → ok mu.
    public static class MoveSimulator
    {
        private const int NoBridge = -1;

        public static MoveResult Simulate(BoardState state, int snakeId)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (!state.IsActive(snakeId))
                throw new ArgumentException(
                    $"Snake {snakeId} is not active.", nameof(snakeId));

            LevelDefinition level = state.Level;
            BoardMap map = state.Map;
            SnakeDefinition snake = state.GetSnake(snakeId);
            LayeredCell head = snake.Body[0];
            Direction direction = snake.Direction;
            Cell current = head.Cell;

            // Üst kat takibi: hangi köprü, yolun hangi sırası, hangi yöne (±1).
            int topBridge = NoBridge, topStep = 0, topSign = 0;

            if (head.Layer == BoardLayer.BridgeTop)
                map.TryGetTopTravel(current, direction, out topBridge, out topStep, out topSign);

            var path = new List<LayeredCell>(level.Width + level.Height);

            // Durum (hücre, katman, yön) sayısı kadar adımdan sonra durum tekrar
            // eder: hamle bitmiyordur (ör. kapalı ok döngüsü). Sonsuz döngü yerine
            // hata verilir; böyle bir level geçersizdir.
            int maxSteps = checked(level.Width * level.Height * 2 * 4);

            for (int steps = 0; steps <= maxSteps; steps++)
            {
                Cell nextCell;
                BoardLayer layer;

                if (topBridge != NoBridge &&
                    TryFollowRoad(map, topBridge, ref topStep, topSign, out nextCell, ref direction))
                {
                    // Üst katta köprü yolunu izler, dirsekte döner (R-BRG-08/09).
                    layer = BoardLayer.BridgeTop;
                }
                else
                {
                    // Zeminde düz ilerleme; köprünün ucundan çıkan yılan zemine iner.
                    topBridge = NoBridge;
                    nextCell = current + direction.ToOffset();

                    if (!state.IsInside(nextCell))
                        return MoveResult.EscapedOffBoard(snakeId, path, direction);

                    layer = map.TryEnterTop(current, nextCell, direction,
                                out topBridge, out topStep, out topSign)
                        ? BoardLayer.BridgeTop
                        : BoardLayer.Ground;

                    if (layer == BoardLayer.Ground)
                        topBridge = NoBridge;
                }

                var next = new LayeredCell(nextCell, layer);
                int occupant = state.GetOccupant(next);

                // Yılanın kendi gövdesi engel değildir (R-MOV-09): gövde başın
                // yolunu izler, kuyruk hücreleri boşaltır. Aynı katmandaki
                // başka yılan engeldir (R-COL-01, R-BRG-06).
                if (occupant != 0 && occupant != snakeId)
                    return MoveResult.Blocked(
                        snakeId, path, direction, next, occupant);

                path.Add(next);
                current = nextCell;

                // Tünel portal değil: baş girince yol biter (R-BUR-02, R-BUR-07).
                if (map.IsBurrow(current))
                    return MoveResult.EscapedIntoBurrow(snakeId, path, direction);

                // Ok: baş hücreye girer, sonra okun yönüne döner; ok tükenmez
                // (R-ARW-04, R-ARW-05). Ok köprü hücresinde olamaz (BoardMap).
                if (map.TryGetArrow(current, out Direction turn))
                    direction = turn;
            }

            throw new InvalidOperationException(
                $"Move of snake {snakeId} never ends (closed arrow loop?).");
        }

        private static bool TryFollowRoad(
            BoardMap map, int bridge, ref int step, int sign,
            out Cell nextCell, ref Direction direction)
        {
            var road = map.Bridges[bridge].Path;
            int nextStep = step + sign;

            if (nextStep < 0 || nextStep >= road.Count)
            {
                nextCell = default;
                return false;
            }

            nextCell = road[nextStep];
            direction = DirectionExtensions.Between(road[step], nextCell);
            step = nextStep;
            return true;
        }
    }
}
