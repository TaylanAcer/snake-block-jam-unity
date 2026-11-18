using System;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Grid ↔ dünya dönüşümü (saf hesap, MonoBehaviour değil).
    // Core: orijin sol-alt, x sağa, y yukarı (spec §0.2).
    // Dünya: tahta XZ düzleminde, merkezi (0,0,0); grid x → dünya x,
    // grid y → dünya z. Bir hücre = 1 dünya birimi.
    public sealed class BoardGeometry
    {
        // Köprü üstündeki segmentlerin yükseltisi (R-BRG-03). Köprü görseli
        // geldiğinde ayarlanacak; L1'de köprü yok.
        public const float BridgeTopHeight = 0.6f;

        public int Width { get; }
        public int Height { get; }

        public BoardGeometry(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Invalid board size.");

            Width = width;
            Height = height;
        }

        // Hücrenin merkezi, katmanın taban yüksekliğinde.
        public Vector3 CellToWorld(Cell cell, BoardLayer layer = BoardLayer.Ground)
        {
            return new Vector3(
                cell.X - (Width - 1) * 0.5f,
                layer == BoardLayer.BridgeTop ? BridgeTopHeight : 0f,
                cell.Y - (Height - 1) * 0.5f);
        }

        // Grid noktası olan hücre (OBSERVED): x ve y'si çift olan hücreler, orijin
        // sol-alt. Noktalar yılanların altında da vardır; yılan kaçınca görünür
        // (R-MOV-07). L1'de bu hücrelerin hepsi yılan altında olduğu için
        // başlangıçta hiç nokta görünmez (Level_01.png); L15'te boş olanlar
        // 2 hücre arayla görünür (Level_15.png). Çift boyutlu tahtada (L4,
        // 23×28) eşliğin hangi kenardan sayıldığı UNKNOWN.
        public static bool HasGridDot(Cell cell)
        {
            return cell.X % 2 == 0 && cell.Y % 2 == 0;
        }

        public static Vector3 DirectionToWorld(Direction direction)
        {
            Cell offset = direction.ToOffset();
            return new Vector3(offset.X, 0f, offset.Y);
        }

        // Yükseklik yok sayılır: noktanın üstünde/altında olduğu hücre.
        public bool TryWorldToCell(Vector3 world, out Cell cell)
        {
            int x = Mathf.FloorToInt(world.x + Width * 0.5f);
            int y = Mathf.FloorToInt(world.z + Height * 0.5f);
            cell = new Cell(x, y);
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        // Ekrandan gelen ışını 'planeHeight' yüksekliğindeki yatay düzlemle
        // keser. Eğik kamerada dokunulan şey blokların üst yüzü olduğu için
        // düzlem blok üstü yüksekliğinde verilir; böylece paralaks kayması olmaz.
        public bool TryRayToCell(Ray ray, float planeHeight, out Cell cell)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));

            if (!plane.Raycast(ray, out float distance))
            {
                cell = default;
                return false;
            }

            return TryWorldToCell(ray.GetPoint(distance), out cell);
        }
    }
}
