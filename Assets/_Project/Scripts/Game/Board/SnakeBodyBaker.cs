using System.Collections.Generic;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Dururken tek mesh (çizim çağrısı bütçesi).
    // Galaxy A13 ölçümü (L7, ~940 renderer): render thread bir çekirdeğin %75'i,
    // GPU %69–77; grafik ayarı ne olursa olsun ~26 FPS. Darboğaz çizim çağrısı
    // sayısı (ana geçiş + gölge geçişi ~1500). Yılan dururken gövde blokları,
    // ekler ve kuyruk ucu iki mesh'te birleşir (gövde + kuyruk materyali); gözler
    // ve dil animasyonlu olduğu için ayrı kalır. Hareket, göçük veya açılış
    // başlarken parçalara dönülür (SnakeView). Parçalar forceRenderingOff ile
    // gizlenir: seçim (PickRenderers) onların sınırlarını kullanmaya devam eder.
    internal sealed class SnakeBodyBaker
    {
        private readonly Transform _root;
        private readonly List<MeshRenderer> _bodyRenderers;
        private MeshRenderer _bakedBody;
        private MeshRenderer _bakedTail;
        private Mesh _bakedBodyMesh;
        private Mesh _bakedTailMesh;

        public bool IsBaked { get; private set; }

        public SnakeBodyBaker(Transform root, List<MeshRenderer> bodyRenderers)
        {
            _root = root;
            _bodyRenderers = bodyRenderers;
        }

        public void Bake(MeshRenderer tailTip)
        {
            Matrix4x4 toLocal = _root.worldToLocalMatrix;
            var body = new List<CombineInstance>(_bodyRenderers.Count);

            foreach (MeshRenderer piece in _bodyRenderers)
            {
                if (!piece.gameObject.activeInHierarchy)
                    continue;
                body.Add(new CombineInstance
                {
                    mesh = piece.GetComponent<MeshFilter>().sharedMesh,
                    transform = toLocal * piece.transform.localToWorldMatrix
                });
            }

            _bakedBody = EnsureBaked(_bakedBody, ref _bakedBodyMesh, "BakedBody", body,
                _bodyRenderers[0].sharedMaterial);

            if (tailTip != null && tailTip.gameObject.activeInHierarchy)
            {
                var tail = new List<CombineInstance>(1)
                {
                    new CombineInstance
                    {
                        mesh = tailTip.GetComponent<MeshFilter>().sharedMesh,
                        transform = toLocal * tailTip.transform.localToWorldMatrix
                    }
                };
                _bakedTail = EnsureBaked(_bakedTail, ref _bakedTailMesh, "BakedTail", tail, tailTip.sharedMaterial);
            }

            SetPiecesHidden(tailTip, true);
            IsBaked = true;
        }

        public void Unbake(MeshRenderer tailTip)
        {
            if (!IsBaked)
                return;

            SetPiecesHidden(tailTip, false);
            if (_bakedBody != null)
                _bakedBody.enabled = false;
            if (_bakedTail != null)
                _bakedTail.enabled = false;
            IsBaked = false;
        }

        // İpucu vurgusu birleşik mesh'lere de uygulanır.
        public void ApplyMaterials(Material body, MeshRenderer tailTip)
        {
            if (_bakedBody != null)
                _bakedBody.sharedMaterial = body;
            if (_bakedTail != null && tailTip != null)
                _bakedTail.sharedMaterial = tailTip.sharedMaterial;
        }

        public void DestroyMeshes()
        {
            if (_bakedBodyMesh != null)
                Object.Destroy(_bakedBodyMesh);
            if (_bakedTailMesh != null)
                Object.Destroy(_bakedTailMesh);
        }

        private MeshRenderer EnsureBaked(MeshRenderer target, ref Mesh mesh, string objectName,
                                         List<CombineInstance> parts, Material material)
        {
            if (target == null)
            {
                var host = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
                host.transform.SetParent(_root, false);
                target = host.GetComponent<MeshRenderer>();
                target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            if (mesh == null)
                mesh = new Mesh { name = objectName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

            mesh.Clear();
            mesh.CombineMeshes(parts.ToArray(), true, true);
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
            target.sharedMaterial = material;
            target.enabled = true;
            return target;
        }

        private void SetPiecesHidden(MeshRenderer tailTip, bool hidden)
        {
            foreach (MeshRenderer piece in _bodyRenderers)
                piece.forceRenderingOff = hidden;
            if (tailTip != null)
                tailTip.forceRenderingOff = hidden;
        }
    }
}
