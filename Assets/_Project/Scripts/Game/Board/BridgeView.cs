using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Köprünün görüntüsü (R-BRG-02): korkuluklu asfalt yol, ortasında kesik
    // çizgi, uçlarında direkler. Kural bilmez; yol hücreleri Core'dan gelir.
    //
    // Yol, yükseltilmiş bir tabla: üst yüzü BoardGeometry.BridgeTopHeight'ta.
    // Zemindeki yılan altından geçer ve üstten bakınca tabla onu örter
    // (R-BRG-03; JSON'da L5/L6'nın "gizli" hücreleri tam köprü hücreleri).
    //
    // Uzunluk (INFERRED, başlangıç karelerinden ölçüldü): tabla uç hücrelerin
    // merkezleri arasında uzanır, uçlardan biraz taşar. L5 7 hücre → ~6,0,
    // L6 5 hücre → ~4,4. 1 hücrelik köprü ~2 hücre görünür (R-BRG-11; L10, L19).
    // Kemerler ve dirseğin yuvarlak dış köşesi henüz yok.
    public sealed class BridgeView : MonoBehaviour
    {
        public const float EndOverhang = 0.15f;
        public const float MinLength = 2f;

        // Genişlik (Level_05/06.png): asfalt ~1,25 hücre, korkuluklarla ~1,6.
        public const float DeckWidth = 1.6f;
        public const float DeckThickness = 0.1f;
        public const float AsphaltWidth = 1.25f;
        public const float AsphaltThickness = 0.02f;
        public const float RailWidth = 0.16f;
        public const float RailHeight = 0.12f;
        public const float PostSize = 0.22f;
        public const float PostHeight = 0.18f;

        // Tablanın iki yanından inen krem etek: köprüyü kalın gösterir (referansta
        // kemerli ön yüz). Zemin yılanının (0,6) üstünde biter; altından geçer.
        public const float SkirtBottom = 0.63f;
        public const float SkirtWidth = 0.12f;
        public const float DashLength = 0.22f;
        public const float DashSpacing = 0.5f;
        public const float DashWidth = 0.07f;

        // Bir düz parça: iki uç (dünya, tabla üstü yüksekliğinde).
        public readonly struct Run
        {
            public readonly Vector3 From;
            public readonly Vector3 To;

            public Run(Vector3 from, Vector3 to)
            {
                From = from;
                To = to;
            }

            public float Length => Vector3.Distance(From, To);
            public Vector3 Direction => (To - From).normalized;
        }

        // Yolu düz parçalara böler (L köprü: dirsekte iki parça, dirsek hücresi
        // ortak). İlk ve son uç dışa doğru uzatılır; toplam kısa kalırsa
        // MinLength'e tamamlanır.
        public static List<Run> Runs(BridgeDefinition bridge, BoardGeometry geometry)
        {
            var path = bridge.Path;
            var centers = new List<Vector3>(path.Count);

            foreach (var cell in path)
                centers.Add(geometry.CellToWorld(cell, BoardLayer.BridgeTop));

            var runs = new List<Run>();

            if (path.Count == 1)
            {
                Vector3 axis = bridge.Axis == BridgeAxis.Vertical ? Vector3.forward : Vector3.right;
                runs.Add(new Run(centers[0] - axis * MinLength * 0.5f, centers[0] + axis * MinLength * 0.5f));
                return runs;
            }

            int start = 0;

            for (int i = 1; i < path.Count; i++)
            {
                bool last = i == path.Count - 1;
                bool bend = !last && centers[i + 1] - centers[i] != centers[i] - centers[i - 1];

                if (bend || last)
                {
                    runs.Add(new Run(centers[start], centers[i]));
                    start = i;
                }
            }

            float total = 0f;

            foreach (var run in runs)
                total += run.Length;

            float extra = Mathf.Max(EndOverhang, (MinLength - total) * 0.5f);
            Run first = runs[0];
            runs[0] = new Run(first.From - first.Direction * extra, first.To);
            Run tail = runs[runs.Count - 1];
            runs[runs.Count - 1] = new Run(tail.From, tail.To + tail.Direction * extra);
            return runs;
        }

        public void Build(BridgeDefinition bridge, BoardGeometry geometry,
                          Material deck, Material asphalt, Material dash)
        {
            name = "Bridge";
            var runs = Runs(bridge, geometry);
            float top = BoardGeometry.BridgeTopHeight;

            for (int r = 0; r < runs.Count; r++)
            {
                Run run = runs[r];
                Vector3 direction = run.Direction;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                Quaternion rotation = Quaternion.LookRotation(direction);
                // Run uçları tabla üstü yüksekliğinde; parçalar yüksekliği aşağıda
                // kendisi ekler, bu yüzden merkez zemine indirilir.
                Vector3 middle = (run.From + run.To) * 0.5f;
                middle.y = 0f;

                // Dirsekte parçalar yarım genişlik uzar ki köşe dolsun.
                bool joinsPrevious = r > 0;
                bool joinsNext = r < runs.Count - 1;
                float startTrim = joinsPrevious ? DeckWidth * 0.5f : 0f;
                float endTrim = joinsNext ? DeckWidth * 0.5f : 0f;
                float length = run.Length + startTrim + endTrim;
                Vector3 center = middle + direction * (endTrim - startTrim) * 0.5f;

                CreatePiece("Deck", center + Vector3.up * (top - AsphaltThickness - DeckThickness * 0.5f),
                    rotation, new Vector3(DeckWidth, DeckThickness, length), deck);
                CreatePiece("Asphalt", center + Vector3.up * (top - AsphaltThickness * 0.5f),
                    rotation, new Vector3(AsphaltWidth, AsphaltThickness, length), asphalt);

                float deckBottom = top - AsphaltThickness - DeckThickness;
                float skirtHeight = deckBottom - SkirtBottom;

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 offset = side * s * (DeckWidth - RailWidth) * 0.5f;
                    CreatePiece("Rail", center + offset + Vector3.up * (top + RailHeight * 0.5f),
                        rotation, new Vector3(RailWidth, RailHeight, length), deck);

                    Vector3 skirtOffset = side * s * (DeckWidth - SkirtWidth) * 0.5f;
                    CreatePiece("Skirt", center + skirtOffset + Vector3.up * (SkirtBottom + skirtHeight * 0.5f),
                        rotation, new Vector3(SkirtWidth, skirtHeight, length), deck);
                }

                CreateDashes(run, rotation, top, dash);

                if (!joinsPrevious)
                    CreatePosts(run.From, side, rotation, top, deck);

                if (!joinsNext)
                    CreatePosts(run.To, side, rotation, top, deck);
            }
        }

        private void CreateDashes(Run run, Quaternion rotation, float top, Material dash)
        {
            int count = Mathf.FloorToInt((run.Length - DashLength) / DashSpacing);
            float used = count * DashSpacing;
            Vector3 first = run.From + run.Direction * ((run.Length - used) * 0.5f);
            first.y = 0f;

            for (int i = 0; i <= count; i++)
            {
                CreatePiece("Dash", first + run.Direction * (i * DashSpacing) + Vector3.up * (top + 0.002f),
                    rotation, new Vector3(DashWidth, 0.004f, DashLength), dash, castShadows: false);
            }
        }

        // Uçlarda iki direk: korkuluk başı + zemine inen ayak.
        private void CreatePosts(Vector3 end, Vector3 side, Quaternion rotation, float top, Material deck)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 at = end + side * s * (DeckWidth - PostSize) * 0.5f;
                at.y = 0f;
                float height = top + PostHeight;
                CreatePiece("Post", at + Vector3.up * (height * 0.5f), rotation,
                    new Vector3(PostSize, height, PostSize), deck);
            }
        }

        private void CreatePiece(string objectName, Vector3 position, Quaternion rotation, Vector3 scale,
                                 Material material, bool castShadows = true)
        {
            var piece = Primitives.Create(PrimitiveType.Cube, objectName);

            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform t = piece.transform;
            t.SetParent(transform, false);
            t.SetPositionAndRotation(position, rotation);
            t.localScale = scale;
        }
    }
}
