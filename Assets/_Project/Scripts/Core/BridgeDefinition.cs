
using System;
using System.Collections.Generic;

namespace SnakeBlockJam.Core
{
    public sealed class BridgeDefinition
    {
        public IReadOnlyList<Cell> Path { get; }

        // Düz köprünün yol ekseni (R-BRG-05). L köprüde null: eksen
        // hücreden hücreye değişir ve path'ten okunur (R-BRG-09).
        public BridgeAxis? Axis { get; }

        // 1 hücrelik köprüde eksen path'ten çıkarılamaz, açıkça verilmelidir
        // (L10, L19). Çok hücreli düz köprüde eksen path'ten hesaplanır;
        // verilmişse path ile tutarlı olmalıdır. L köprüye eksen verilemez.
        // Bağlılık ve şekil (tek dirsek) doğrulaması burada yapılmaz.
        public BridgeDefinition(IReadOnlyList<Cell> path, BridgeAxis? axis = null)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Count == 0)
                throw new ArgumentException("Bridge path is empty.", nameof(path));

            Path = new List<Cell>(path).AsReadOnly();
            Axis = ResolveAxis(Path, axis);
        }

        private static BridgeAxis? ResolveAxis(IReadOnlyList<Cell> path, BridgeAxis? axis)
        {
            if (path.Count == 1)
            {
                if (axis == null)
                    throw new ArgumentException(
                        "Single-cell bridge requires an explicit axis.",
                        nameof(axis)
                    );

                return axis;
            }

            BridgeAxis? derived = DeriveAxis(path);

            if (axis != null && axis != derived)
                throw new ArgumentException(
                    "Explicit axis does not match bridge path.",
                    nameof(axis)
                );

            return derived;
        }

        // Bütün hücreler aynı satırdaysa yatay, aynı sütundaysa dikey;
        // değilse (L) tek bir eksen yok.
        private static BridgeAxis? DeriveAxis(IReadOnlyList<Cell> path)
        {
            bool sameRow = true;
            bool sameColumn = true;

            for (int i = 1; i < path.Count; i++)
            {
                sameRow &= path[i].Y == path[0].Y;
                sameColumn &= path[i].X == path[0].X;
            }

            if (sameRow)
                return BridgeAxis.Horizontal;

            if (sameColumn)
                return BridgeAxis.Vertical;

            return null;
        }
    }
}
