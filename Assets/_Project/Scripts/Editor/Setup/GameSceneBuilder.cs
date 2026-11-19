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
        public const string LevelCatalogPath = "Assets/_Project/Data/LevelCatalog.asset";

        // Kataloğun içeriği (oynanış sırası): referanstaki 20 level (Faz 8).
        // Veri notları (L4/L15 gizli hücreler, L10/L19 kısa köprüler) spec §14.3'te.
        public static readonly int[] CatalogLevels =
            { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

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
            var catalog = EnsureLevelCatalog();

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
            SetReference(controller, "catalog", catalog);
            SetReference(controller, "feel", feel);
            SetReference(controller, "boardView", board);
            SetReference(controller, "gameCamera", camera);

            var input = controllerObject.AddComponent<InputHandler>();
            SetReference(input, "controller", controller);

            CreateEventSystem();
            Transform canvas = CreateCanvas(controller);
            CreateHud(canvas, controller);
            CreateBoosters(canvas, controller);
            FeedbackView feedback = CreateFeedback(canvas, controller);
            CreateResultWindow(canvas, controller);
            CreateSfxPlayer(controllerObject, controller, feedback);

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
                LevelJsonImporter.ImportAllToAssets();
                RebuildLevelCatalog();
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

        // Varsa dokunulmaz: Inspector'da düzenlenmiş sıra korunur.
        private static LevelCatalog EnsureLevelCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            return catalog != null ? catalog : RebuildLevelCatalog();
        }

        // Kataloğu CatalogLevels sırasıyla baştan yazar (asset ve GUID'i korunur).
        // Eksik level asset'i JSON'dan içe aktarılır.
        [MenuItem("Snake Block Jam/Levels/Rebuild Level Catalog (1–20)")]
        public static LevelCatalog RebuildLevelCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            bool isNew = catalog == null;

            if (isNew)
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            else
                Undo.RecordObject(catalog, "Rebuild Level Catalog");

            catalog.levels.Clear();

            foreach (int levelNumber in CatalogLevels)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelJsonImporter.AssetPath(levelNumber));

                if (level == null)
                    level = LevelJsonImporter.ImportToAsset(levelNumber);

                catalog.levels.Add(level);
            }

            if (isNew)
                AssetDatabase.CreateAsset(catalog, LevelCatalogPath);
            else
                EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            return catalog;
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

        // Canvas ve Restart butonu (referanstaki ayarlar dişlisinin yeri).
        private static Transform CreateCanvas(GameController controller)
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

            return canvasObject.transform;
        }

        // --- HUD ve sonuç penceresi (Faz 6) -----------------------------------
        // Yerleşim ve renkler Level_07.png ve L7_hamle_yok_v07.png'den yaklaşık
        // (720 × 1600 kare → 1080 × 1920 referans çözünürlük).

        private static readonly Color HudTeal = new Color32(0x45, 0xB4, 0xCB, 0xFF);
        private static readonly Color HudGreen = new Color32(0x55, 0xD4, 0x4B, 0xFF);
        private static readonly Color HudDarkGreen = new Color32(0x2E, 0x7D, 0x1F, 0xFF);
        private static readonly Color TextOutline = new Color32(0x1D, 0x3A, 0x4F, 0xFF);
        private static readonly Color AllClearYellow = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        private static readonly Color AllClearOutline = new Color32(0x2B, 0x5F, 0xB8, 0xFF);
        private static readonly Color WindowBody = new Color32(0xEA, 0xF4, 0xFB, 0xFF);
        private static readonly Color WindowHeader = new Color32(0x2F, 0x7B, 0xE0, 0xFF);
        private static readonly Color DarkText = new Color32(0x33, 0x3A, 0x48, 0xFF);

        private static void CreateHud(Transform canvas, GameController controller)
        {
            // HudView kökte; açılıp kapanan parçalar alt objelerde (kapanan objenin
            // OnDisable'ı olay aboneliğini koparırdı).
            RectTransform root = CreateRect("Hud", canvas);
            Stretch(root);

            RectTransform moves = CreateRect("MovesPanel", root);
            moves.anchorMin = moves.anchorMax = new Vector2(0.5f, 1f);
            moves.anchoredPosition = new Vector2(0f, -146f);
            moves.sizeDelta = new Vector2(330f, 100f);

            RectTransform ribbon = CreateRect("LevelRibbon", moves);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 0f);
            ribbon.anchoredPosition = new Vector2(0f, -22f);
            ribbon.sizeDelta = new Vector2(230f, 52f);
            CreateImage(ribbon, HudGreen);
            Text levelText = CreateText("Label", ribbon, "Seviye 1", 34, HudDarkGreen);

            // Kutu şeritten sonra: şeridin üstünde çizilir.
            RectTransform box = CreateRect("Box", moves);
            Stretch(box);
            Image background = CreateImage(box, HudTeal);
            Text movesText = CreateText("Label", box, "Hamle: 0", 54, Color.white);
            movesText.gameObject.AddComponent<Outline>().effectColor = TextOutline;

            RectTransform allClear = CreateRect("AllClear", root);
            allClear.anchorMin = allClear.anchorMax = new Vector2(0.5f, 0.62f);
            // Referansta iki satır ("ALL" / "CLEAR!"), ekran genişliğinin ~%40'ı.
            allClear.sizeDelta = new Vector2(520f, 300f);
            Text allClearText = CreateText("Label", allClear, "ALL\nCLEAR!", 120, AllClearYellow);
            allClearText.lineSpacing = 0.85f;
            allClearText.verticalOverflow = VerticalWrapMode.Overflow;
            var allClearOutline = allClearText.gameObject.AddComponent<Outline>();
            allClearOutline.effectColor = AllClearOutline;
            allClearOutline.effectDistance = new Vector2(5f, -5f);
            allClear.gameObject.SetActive(false);

            var hud = root.gameObject.AddComponent<HudView>();
            SetReference(hud, "controller", controller);
            SetReference(hud, "movesPanel", moves.gameObject);
            SetReference(hud, "movesBackground", background);
            SetReference(hud, "movesText", movesText);
            SetReference(hud, "levelText", levelText);
            SetReference(hud, "allClear", allClear);
        }

        // --- Boosterlar (Faz 9) ------------------------------------------------
        // Level_20.png (720 × 1616): butonlar ~100 × 95 px, merkezleri alttan ~100 px,
        // aralarında ~140 px. Kürek bandı image(20261116-205144): ekranın ~%82'si
        // genişlikte, gri, sol üstte turuncu "Kürek", altında beyaz açıklama, sağda
        // kırmızı X. İkonlar (ampul, kürek) yok: görsel tur.

        private static readonly Color BoosterButton = new Color32(0x3B, 0xC0, 0xEB, 0xFF);
        private static readonly Color BoosterBadge = new Color32(0x2F, 0x6F, 0xE0, 0xFF);
        private static readonly Color BannerBody = new Color32(0x6E, 0x7C, 0x86, 0xF0);
        private static readonly Color BannerTitle = new Color32(0xFF, 0xC2, 0x3A, 0xFF);
        private static readonly Color CancelRed = new Color32(0xE0, 0x30, 0x3C, 0xFF);

        private static void CreateBoosters(Transform canvas, GameController controller)
        {
            RectTransform root = CreateRect("Boosters", canvas);
            Stretch(root);
            var view = root.gameObject.AddComponent<BoosterView>();

            Text hintCount;
            Text shovelCount;
            RectTransform hint = CreateBoosterButton(root, "HintButton", "İpucu", out hintCount);
            RectTransform shovel = CreateBoosterButton(root, "ShovelButton", "Kürek", out shovelCount);
            UnityEventTools.AddPersistentListener(hint.GetComponent<Button>().onClick, view.OnHint);
            UnityEventTools.AddPersistentListener(shovel.GetComponent<Button>().onClick, view.OnShovel);

            RectTransform banner = CreateRect("ShovelBanner", root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0f);
            banner.anchoredPosition = new Vector2(0f, 150f);
            banner.sizeDelta = new Vector2(890f, 170f);
            CreateImage(banner, BannerBody).raycastTarget = true;

            RectTransform titleRect = CreateRect("Title", banner);
            titleRect.anchoredPosition = new Vector2(0f, 32f);
            titleRect.sizeDelta = new Vector2(700f, 70f);
            Text title = CreateText("Label", titleRect, "Kürek", 54, BannerTitle);
            title.gameObject.AddComponent<Outline>().effectColor = TextOutline;

            RectTransform descriptionRect = CreateRect("Description", banner);
            descriptionRect.anchoredPosition = new Vector2(0f, -32f);
            descriptionRect.sizeDelta = new Vector2(700f, 60f);
            CreateText("Label", descriptionRect, "Seçilen yılanı bölümden kaldırır!", 38, Color.white);

            RectTransform cancel = CreateRect("CancelButton", banner);
            cancel.anchorMin = cancel.anchorMax = new Vector2(1f, 1f);
            cancel.anchoredPosition = new Vector2(-30f, -30f);
            cancel.sizeDelta = new Vector2(90f, 90f);
            Image cancelImage = CreateImage(cancel, CancelRed);
            cancelImage.raycastTarget = true;
            var cancelButton = cancel.gameObject.AddComponent<Button>();
            cancelButton.targetGraphic = cancelImage;
            CreateText("Label", cancel, "X", 54, Color.white);
            UnityEventTools.AddPersistentListener(cancelButton.onClick, view.OnCancelShovel);

            banner.gameObject.SetActive(false);

            SetReference(view, "controller", controller);
            SetReference(view, "hintButton", hint);
            SetReference(view, "hintCount", hintCount);
            SetReference(view, "shovelButton", shovel);
            SetReference(view, "shovelCount", shovelCount);
            SetReference(view, "shovelBanner", banner.gameObject);
        }

        private static RectTransform CreateBoosterButton(RectTransform parent, string objectName, string label,
                                                         out Text count)
        {
            RectTransform button = CreateRect(objectName, parent);
            button.anchorMin = button.anchorMax = new Vector2(0.5f, 0f);
            button.anchoredPosition = new Vector2(0f, 150f);
            button.sizeDelta = new Vector2(150f, 140f);

            Image image = CreateImage(button, BoosterButton);
            image.raycastTarget = true;
            var component = button.gameObject.AddComponent<Button>();
            component.targetGraphic = image;

            Text text = CreateText("Label", button, label, 34, Color.white);
            text.gameObject.AddComponent<Outline>().effectColor = TextOutline;

            RectTransform badge = CreateRect("Badge", button);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0f);
            badge.anchoredPosition = new Vector2(0f, -8f);
            badge.sizeDelta = new Vector2(56f, 56f);
            CreateImage(badge, BoosterBadge);
            count = CreateText("Count", badge, "1", 36, Color.white);

            return button;
        }

        // --- Geri bildirim ve ses (Faz 11) --------------------------------------
        // Vinyet tam ekran (R-COL-03); seri yazısı HUD şeridinin altında, ortada
        // (banners_group4: 720 × 1616 karede yazı merkezi y ≈ 233, şeritten ~45 px
        // aşağıda; harf yüksekliği ~50 px). Canvas ölçeği 720 × 1616'da ~0,75.

        private static FeedbackView CreateFeedback(Transform canvas, GameController controller)
        {
            RectTransform root = CreateRect("Feedback", canvas);
            Stretch(root);
            var view = root.gameObject.AddComponent<FeedbackView>();

            RectTransform vignetteRect = CreateRect("Vignette", root);
            Stretch(vignetteRect);
            var vignette = vignetteRect.gameObject.AddComponent<Image>();
            vignette.raycastTarget = false;

            RectTransform streak = CreateRect("Streak", root);
            streak.anchorMin = streak.anchorMax = new Vector2(0.5f, 1f);
            streak.anchoredPosition = new Vector2(0f, -315f);
            streak.sizeDelta = new Vector2(760f, 110f);

            RectTransform glowRect = CreateRect("Glow", streak);
            glowRect.sizeDelta = new Vector2(820f, 120f);
            var glow = glowRect.gameObject.AddComponent<Image>();
            glow.raycastTarget = false;

            Text label = CreateText("Label", streak, "Great!", 80, Color.white);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(5f, -5f);
            streak.gameObject.SetActive(false);

            SetReference(view, "controller", controller);
            SetReference(view, "vignette", vignette);
            SetReference(view, "streakRoot", streak);
            SetReference(view, "streakGlow", glow);
            SetReference(view, "streakText", label);
            SetReference(view, "streakOutline", outline);
            return view;
        }

        private static void CreateSfxPlayer(GameObject host, GameController controller, FeedbackView feedback)
        {
            var player = host.AddComponent<SfxPlayer>(); // AudioSource RequireComponent ile gelir
            SetReference(player, "controller", controller);
            SetReference(player, "feedback", feedback);
        }

        private static void CreateResultWindow(Transform canvas, GameController controller)
        {
            RectTransform root = CreateRect("Result", canvas);
            Stretch(root);

            // Overlay ışın yakalar: pencere açıkken tahtaya dokunuş geçmez.
            RectTransform overlay = CreateRect("Overlay", root);
            Stretch(overlay);
            CreateImage(overlay, new Color(0f, 0f, 0f, 0.65f)).raycastTarget = true;

            RectTransform card = CreateRect("Card", overlay);
            card.sizeDelta = new Vector2(820f, 600f);
            CreateImage(card, WindowBody);

            RectTransform header = CreateRect("Header", card);
            header.anchorMin = header.anchorMax = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(560f, 110f);
            CreateImage(header, WindowHeader);
            Text title = CreateText("Title", header, "TAMAMLANDI", 60, Color.white);
            title.gameObject.AddComponent<Outline>().effectColor = TextOutline;

            RectTransform messageRect = CreateRect("Message", card);
            messageRect.anchoredPosition = new Vector2(0f, 40f);
            messageRect.sizeDelta = new Vector2(720f, 160f);
            Text message = CreateText("Label", messageRect, "", 52, DarkText);

            RectTransform buttonRect = CreateRect("Button", card);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 120f);
            buttonRect.sizeDelta = new Vector2(480f, 130f);
            Image buttonImage = CreateImage(buttonRect, HudGreen);
            buttonImage.raycastTarget = true;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            Text buttonLabel = CreateText("Label", buttonRect, "Devam", 58, Color.white);
            buttonLabel.gameObject.AddComponent<Outline>().effectColor = HudDarkGreen;

            overlay.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<ResultView>();
            SetReference(view, "controller", controller);
            SetReference(view, "panel", overlay.gameObject);
            SetReference(view, "titleText", title);
            SetReference(view, "messageText", message);
            SetReference(view, "buttonText", buttonLabel);
            UnityEventTools.AddPersistentListener(button.onClick, view.OnButton);
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string objectName, RectTransform parent, string text, int size, Color color)
        {
            RectTransform rect = CreateRect(objectName, parent);
            Stretch(rect);

            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.color = color;
            label.raycastTarget = false;
            return label;
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
