using System;
using SnakeBlockJam.Game;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SnakeBlockJam.EditorTools
{
    // Game.unity sahnesini koddan kurar. Sahne elle de düzenlenebilir; bu araç
    // sahnenin ne içerdiğini belgeler ve gerektiğinde aynı sahneyi yeniden üretir.
    // Çalıştırmak mevcut Game.unity'nin üzerine yazar.
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        public const string BlockMaterialPath = "Assets/_Project/Art/Materials/Block.mat";
        public const string FeelSettingsPath = "Assets/_Project/Data/FeelSettings.asset";

        private static readonly Color Background = new Color32(0xE4, 0xF1, 0xFA, 0xFF);

        [MenuItem("Snake Block Jam/Setup/Build Game Scene")]
        public static void BuildGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // NewScene(Single) kullanılmayan asset'leri boşaltır; asset referansları
            // sahne açıldıktan sonra yüklenir, yoksa boş (null) kaydedilirler.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelJsonImporter.AssetPath(1));

            if (level == null)
                level = LevelJsonImporter.ImportToAsset(1);

            var blockMaterial = EnsureBlockMaterial();
            var feel = EnsureFeelSettings();

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);

            Camera camera = CreateCamera();
            CreateLight();

            var board = new GameObject("Board").AddComponent<BoardView>();
            SetReference(board, "blockMaterial", blockMaterial);

            var controllerObject = new GameObject("GameController");
            var controller = controllerObject.AddComponent<GameController>();
            SetReference(controller, "level", level);
            SetReference(controller, "feel", feel);
            SetReference(controller, "boardView", board);
            SetReference(controller, "gameCamera", camera);

            var input = controllerObject.AddComponent<InputHandler>();
            SetReference(input, "controller", controller);

            CreateEventSystem();
            CreateCanvas(controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Portre ekran ve build sahnesi. Unity açıkken ProjectSettings dosyası
        // dışarıdan değiştirilirse Unity kendi bellekteki değeriyle ezebilir;
        // bu menü ayarı Unity'nin içinden uygular.
        [MenuItem("Snake Block Jam/Setup/Apply Project Settings")]
        public static void ApplyProjectSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        // Komut satırı: Unity.exe -batchmode -projectPath ... -executeMethod
        // SnakeBlockJam.EditorTools.GameSceneBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            try
            {
                LevelJsonImporter.ImportToAsset(1);
                BuildGameScene();
                ApplyProjectSettings();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        // Varsa dokunulmaz: ince ayar yapılmış değerler korunur.
        private static FeelSettings EnsureFeelSettings()
        {
            var feel = AssetDatabase.LoadAssetAtPath<FeelSettings>(FeelSettingsPath);

            if (feel != null)
                return feel;

            feel = ScriptableObject.CreateInstance<FeelSettings>();
            AssetDatabase.CreateAsset(feel, FeelSettingsPath);
            return feel;
        }

        private static Material EnsureBlockMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BlockMaterialPath);

            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? throw new InvalidOperationException("URP Lit shader not found.");

            material = new Material(shader) { name = "Block" };
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, BlockMaterialPath);
            return material;
        }

        // Perspektif kamera; konumu ve açısını runtime'da BoardView.FitCamera verir.
        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 70f, -19f), Quaternion.Euler(75f, 0f, 0f));
            return camera;
        }

        // Gölgeler referansta sağ-aşağı düşüyor (Level_01.png); ışık sol-üstten.
        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.75f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.35f;
            light.transform.rotation = Quaternion.Euler(60f, 150f, 0f);
        }

        // Yeni Input System ile çalışan UI modülü (eski StandaloneInputModule değil).
        // Aksiyon atanmamışsa modül OnEnable'da varsayılan UI aksiyonlarını kendisi atar.
        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // Faz 3 UI: sadece Restart. HUD (hamle, seviye, sonuç ekranları) sonraki faz.
        private static void CreateCanvas(GameController controller)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.SetParent(canvasObject.transform, false);
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-40f, -60f);
            buttonRect.sizeDelta = new Vector2(240f, 100f);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;

            var button = buttonObject.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, controller.Restart);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.text = "Restart";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 42;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color32(0x33, 0x3A, 0x48, 0xFF);
            label.raycastTarget = false;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field)
                                          ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (value == null || property.objectReferenceValue == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{field} could not be assigned.");
        }
    }
}
