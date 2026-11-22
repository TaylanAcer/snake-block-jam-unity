using System.Collections.Generic;
using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Tahtanın yılan dışındaki sabit görüntüsü: zemin, grid noktaları, köprüler,
    // tüneller, oklar ve renkli küp (kıymık) parçacık sistemleri. BoardView her
    // Build'de çağırır; ok mesh'i bölümler arası paylaşılır.
    internal sealed class BoardDecorBuilder
    {
        // Level_15.png: ~9 px çap / ~25 px hücre.
        private const float DotSize = 0.35f;
        private const float GroundSize = 400f;

        // Zemindeki düz işaretlerin yükseklikleri (z-fighting olmasın diye kademeli):
        // nokta 0–0,004, halka 0,001–0,005, disk 0,003–0,007, ok 0,008.
        private const float MarkerThickness = 0.002f;
        private const float RimHeight = 0.003f;
        private const float HoleHeight = 0.005f;
        private const float ArrowHeight = 0.008f;

        private Mesh _arrowMesh;

        public void CreateGround(Transform content, Material material)
        {
            var ground = Primitives.Create(PrimitiveType.Quad, "Ground");
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;

            Transform t = ground.transform;
            t.SetParent(content, false);
            t.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            t.localScale = new Vector3(GroundSize, GroundSize, 1f);
        }

        // Bu hücrede grid noktası var mı? 'initialState' level'ın başlangıç durumu
        // olmalıdır. Nokta = çift (x, y) hücre (BoardGeometry.HasGridDot) VE
        // tahtanın hücresi. Tahta: dikdörtgen level'da bütün W × H, diğerlerinde
        // başlangıçta yılanların kapladığı hücreler. Yılan altındaki nokta yılan
        // kaçınca görünür (R-MOV-07).
        //
        // Kanıt (20 başlangıç karesi, piksel → hücre eşlemesiyle kontrol edildi):
        // 18 karede hiç nokta yok; L16'daki 6 nokta açılış animasyonunda henüz
        // çizilmemiş yılan başlarının altında, hepsi çift (x, y); L15'te yılansız
        // 24 çift hücrenin 24'ünde de nokta var (dikdörtgen tahta). L2 videosunda
        // boş köşelerde nokta yok. Tahta şekli JSON'da yok → INFERRED.
        public static bool HasDot(BoardState initialState, Cell cell, bool rectangularBoard = false)
        {
            if (!BoardGeometry.HasGridDot(cell))
                return false;

            return rectangularBoard
                   || initialState.GetOccupant(new LayeredCell(cell, BoardLayer.Ground)) != 0
                   || initialState.GetOccupant(new LayeredCell(cell, BoardLayer.BridgeTop)) != 0;
        }

        // Not: büyük level'larda yüzlerce obje olabilir; gerekirse tek mesh'e çevrilir.
        public void CreateDots(Transform content, BoardGeometry geometry, BoardState initialState,
                               bool rectangularBoard, Material material, List<Transform> dotsOut)
        {
            var dots = new GameObject("Dots").transform;
            dots.SetParent(content, false);

            for (int y = 0; y < geometry.Height; y++)
            {
                for (int x = 0; x < geometry.Width; x++)
                {
                    if (!HasDot(initialState, new Cell(x, y), rectangularBoard))
                        continue;

                    var dot = Primitives.Create(PrimitiveType.Cylinder, $"Dot_{x}_{y}");

                    var renderer = dot.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    Transform t = dot.transform;
                    t.SetParent(dots, false);
                    t.position = geometry.CellToWorld(new Cell(x, y)) + Vector3.up * 0.002f;
                    t.localScale = new Vector3(DotSize, 0.002f, DotSize);
                    dotsOut.Add(t);
                }
            }
        }

        // Köprüler; yol hücreleri 'bridgeCells'e eklenir (dokunuş önce köprü üstünde aranır).
        public int CreateBridges(Transform content, BoardGeometry geometry, IReadOnlyList<BridgeDefinition> bridges,
                                 BoardMaterials materials, List<Transform> markers, HashSet<Cell> bridgeCells)
        {
            foreach (var bridge in bridges)
            {
                var view = new GameObject().AddComponent<BridgeView>();
                view.transform.SetParent(content, false);
                view.Build(bridge, geometry, materials.BridgeDeck, materials.BridgeAsphalt, materials.BridgeDash,
                    materials.BridgeArchTrim);
                markers.Add(view.transform);

                foreach (var cell in bridge.Path)
                    bridgeCells.Add(cell);

                if (bridge.Path.Count == 1 && bridge.Axis.HasValue)
                    geometry.AddBridgeShoulders(bridge.Path[0], bridge.Axis.Value);
            }

            return bridges.Count;
        }

        // Tünel: zemine yatık siyah disk ve çevresinde gri halka (R-BUR-03).
        // Tünele giren yılanın segmentleri delik merkezinde kaybolur (SnakeView).
        public int CreateBurrows(Transform content, BoardGeometry geometry, IReadOnlyList<BurrowDefinition> burrows,
                                 BoardMaterials materials, List<Transform> markers)
        {
            foreach (var burrow in burrows)
            {
                var root = new GameObject($"Burrow_{burrow.Position.X}_{burrow.Position.Y}").transform;
                root.SetParent(content, false);
                root.position = geometry.CellToWorld(burrow.Position);

                CreateFlatDisk(root, "Rim", BoardView.BurrowRimSize, RimHeight, materials.BurrowRim);
                CreateFlatDisk(root, "Hole", BoardView.BurrowSize, HoleHeight, materials.Burrow);
                markers.Add(root);
            }

            return burrows.Count;
        }

        // Ok: zemine yatık koyu gri ikon, okun yönüne bakar (R-ARW-02). Yılan
        // üstünden geçer; ok tükenmez (R-ARW-05), bu yüzden hep yerinde kalır.
        public int CreateArrows(Transform content, BoardGeometry geometry, IReadOnlyList<ArrowDefinition> arrows,
                                Material material, List<Transform> markers)
        {
            if (arrows.Count > 0 && _arrowMesh == null)
                _arrowMesh = ArrowMesh.Create();

            foreach (var arrow in arrows)
            {
                var piece = new GameObject(
                    $"Arrow_{arrow.Position.X}_{arrow.Position.Y}", typeof(MeshFilter), typeof(MeshRenderer));
                piece.GetComponent<MeshFilter>().sharedMesh = _arrowMesh;

                var renderer = piece.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Transform t = piece.transform;
                t.SetParent(content, false);
                t.SetPositionAndRotation(
                    geometry.CellToWorld(arrow.Position) + Vector3.up * ArrowHeight,
                    Quaternion.LookRotation(BoardGeometry.DirectionToWorld(arrow.Direction)));
                markers.Add(t);
            }

            return arrows.Count;
        }

        // Yassı silindir (New-Cylinder: çap 1, boy 2): 'height' ortasında, kalınlık 2 × MarkerThickness.
        private static void CreateFlatDisk(Transform parent, string objectName, float diameter, float height,
                                           Material material)
        {
            var disk = Primitives.Create(PrimitiveType.Cylinder, objectName);

            var renderer = disk.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform t = disk.transform;
            t.SetParent(parent, false);
            t.localPosition = Vector3.up * height;
            t.localScale = new Vector3(diameter, MarkerThickness, diameter);
        }

        // Kürek ve tünel kıymıkları: renk başına bir sistem, yayım elle (Emit).
        public static ParticleSystem CreateChips(Transform content, Material material)
        {
            var host = new GameObject("Chips_" + material.name);
            host.transform.SetParent(content, false);
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.32f;
            main.startSize = 0.14f;
            main.gravityModifier = 1.8f;
            main.maxParticles = 48;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = BlockMesh.Get(Vector3.one, 0f);
            renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        public void DestroyMeshes()
        {
            if (_arrowMesh != null)
                BoardView.DestroyOwned(_arrowMesh);
        }
    }
}
