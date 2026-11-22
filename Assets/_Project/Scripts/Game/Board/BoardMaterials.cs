using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Tahtanın runtime materyalleri: hepsi blok materyalinin renkli kopyaları
    // (yılan gövdesi ve kuyruğu, zemin, nokta, göz, dil, köprü, tünel, ok, İpucu
    // vurgusu). BoardView ilk Build'de bir kez oluşturur, yok olurken siler.
    internal sealed class BoardMaterials
    {
        // BoardView'un Inspector alanları.
        public struct Palette
        {
            public Material Background;
            public Color[] Snakes;
            public Color Ground;
            public Color Dot;
            public Color EyeWhite;
            public Color Pupil;
            public Color Tongue;
            public Color BridgeDeck;
            public Color BridgeAsphalt;
            public Color BridgeDash;
            public Color Burrow;
            public Color BurrowRim;
            public Color Arrow;
        }

        private readonly Material _blockMaterial;
        private readonly List<Material> _owned = new List<Material>();

        public Material[] Snakes { get; }
        public Material[] Tails { get; }
        public Material Ground { get; }
        public Material Dot { get; }
        public Material EyeWhite { get; }
        public Material Pupil { get; }
        public Material Tongue { get; }
        public Material BridgeDeck { get; }
        public Material BridgeArchTrim { get; }
        public Material BridgeAsphalt { get; }
        public Material BridgeDash { get; }
        public Material Burrow { get; }
        public Material BurrowRim { get; }
        public Material Arrow { get; }

        public BoardMaterials(Material blockMaterial, Palette palette)
        {
            _blockMaterial = blockMaterial;
            Color[] snakeColors = palette.Snakes;

            Snakes = new Material[snakeColors.Length];
            Tails = new Material[snakeColors.Length];

            for (int i = 0; i < snakeColors.Length; i++)
            {
                Material skin = Create($"Snake_{(SnakeColor)i}", snakeColors[i]);
                if (skin.HasProperty("_SideScale"))
                {
                    skin.SetFloat("_SideScale", 0.39f);
                    skin.SetFloat("_SideGamma", 1.27f);
                }
                Snakes[i] = skin;
                Color tip = snakeColors[i] * 1.055f;
                tip.a = 1f;
                Tails[i] = new Material(skin) { color = tip, name = $"Tail_{(SnakeColor)i}" };
                _owned.Add(Tails[i]);
            }

            Ground = palette.Background != null ? palette.Background : Create("Ground", palette.Ground);
            Dot = Create("Dot", palette.Dot);
            EyeWhite = Create("EyeWhite", palette.EyeWhite);
            Pupil = Create("Pupil", palette.Pupil);
            Tongue = Create("Tongue", palette.Tongue);
            BridgeArchTrim = Create("BridgeArchTrim", new Color32(210, 149, 94, 255));
            BridgeDeck = Create("BridgeDeck", palette.BridgeDeck);
            BridgeAsphalt = Create("BridgeAsphalt", palette.BridgeAsphalt);
            if (BridgeAsphalt.HasProperty("_RoadShading"))
                BridgeAsphalt.SetFloat("_RoadShading", 1f);
            BridgeDash = Create("BridgeDash", palette.BridgeDash);
            Burrow = Create("Burrow", palette.Burrow);

            // Referansta delik tam siyah; blok materyalinin parlaklığı (0,15)
            // siyahı ~RGB(33, 35, 39) griye çekiyordu.
            Burrow.SetFloat("_Smoothness", 0f);
            BurrowRim = Create("BurrowRim", palette.BurrowRim);
            BurrowRim.EnableKeyword("_BURROW_RIM");
            Arrow = Create("Arrow", palette.Arrow);
        }

        // Blok materyalinin adlandırılmış renkli kopyası; yok olurken silinir.
        public Material Create(string materialName, Color color)
        {
            var material = new Material(_blockMaterial) { name = materialName, color = color };
            _owned.Add(material);
            return material;
        }

        public void DestroyAll()
        {
            foreach (var material in _owned)
                BoardView.DestroyOwned(material);
        }
    }
}
