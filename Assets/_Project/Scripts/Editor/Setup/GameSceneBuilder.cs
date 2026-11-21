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
        public const string BackgroundMaterialPath = "Assets/_Project/Art/Materials/BoardBackground.mat";
        public const string BlockShaderName = "SnakeBlockJam/ToonBlock";
        public const string BackgroundShaderName = "SnakeBlockJam/BoardBackground";
        public const string FeelSettingsPath = "Assets/_Project/Data/FeelSettings.asset";
        public const string LevelCatalogPath = "Assets/_Project/Data/LevelCatalog.asset";

        // Kataloğun içeriği (oynanış sırası): referanstaki 20 level (Faz 8).
        // Veri notları (L4/L15 gizli hücreler, L10/L19 kısa köprüler) spec §14.3'te.
        public static readonly int[] CatalogLevels =
            { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

        // Kamera temizleme rengi = zemin degradesinin üst rengi (Level_13.png üstü).
        private static readonly Color Background = new Color32(0xF5, 0xF9, 0xFB, 0xFF);

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
            var backgroundMaterial = EnsureBackgroundMaterial();
            var feel = EnsureFeelSettings();
            var catalog = EnsureLevelCatalog();

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);

            Camera camera = CreateCamera();
            CreateLight();

            var board = new GameObject("Board").AddComponent<BoardView>();
            SetReference(board, "blockMaterial", blockMaterial);
            SetReference(board, "backgroundMaterial", backgroundMaterial);
            SetReference(board, "shovelSprite", InterfaceSprite("Spade"));

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
            Transform canvas = CreateCanvas();
            CreateHud(canvas, controller);
            CreateBoosters(canvas, controller);
            FeedbackView feedback = CreateFeedback(canvas, controller);
            CreateResultWindow(canvas, controller);
            CreateTutorials(canvas, controller);
            SfxPlayer sounds = CreateSfxPlayer(controllerObject, controller, feedback);
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.name != "HintButton" && button.name != "ShovelButton")
                    UnityEventTools.AddPersistentListener(button.onClick, sounds.PlayButton);
            }

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

        // Bloklar: oyuncak görünümlü toon shader (Faz 11b). Eski (URP Lit) Block.mat
        // varsa shader'ı değiştirilir; renkler runtime'da BoardView'da verilir.
        private static Material EnsureBlockMaterial()
        {
            return EnsureMaterial(BlockMaterialPath, BlockShaderName, "Block");
        }

        // Zemin: ekran uzayında degrade + gölge (Faz 11b).
        private static Material EnsureBackgroundMaterial()
        {
            Material material = EnsureMaterial(BackgroundMaterialPath, BackgroundShaderName, "BoardBackground");
            material.SetColor("_ShadowTint", new Color(0.765f, 0.75f, 0.746f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureMaterial(string path, string shaderName, string materialName)
        {
            Shader shader = Shader.Find(shaderName)
                            ?? throw new InvalidOperationException($"Shader '{shaderName}' not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }

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
            // Gölgenin rengi/koyuluğu shader'daki _ShadowTint ile ayarlanır.
            light.shadowStrength = 1f;
            light.transform.rotation = Quaternion.Euler(62f, 145f, 0f);
        }

        // Yeni Input System ile çalışan UI modülü (eski StandaloneInputModule değil).
        // Aksiyon atanmamışsa modül OnEnable'da varsayılan UI aksiyonlarını kendisi atar.
        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // Canvas. Oyun içinde Restart butonu yok (21 Kasım kararı): referansta o köşede
        // ayarlar dişlisi var, bölüm yalnızca HAMLE YOK → Tekrar Dene ile yeniden başlar
        // (v01–v21). Yılan kaçışı yolu sadece açtığı için hiçbir bölümde takılı kalınmaz.
        private static Transform CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject.transform;
        }

        // --- HUD ve sonuç penceresi (Faz 6) -----------------------------------
        // Yerleşim ve renkler Level_07.png ve L7_hamle_yok_v07.png'den yaklaşık
        // (720 × 1600 kare → 1080 × 1920 referans çözünürlük).

        private static readonly Color HudTeal = new Color32(0x45, 0xB4, 0xCB, 0xFF);
        private static readonly Color HudGreen = new Color32(0x55, 0xD4, 0x4B, 0xFF);
        private static readonly Color HudDarkGreen = new Color32(0x2E, 0x7D, 0x1F, 0xFF);
        private static readonly Color TextOutline = new Color32(0x1D, 0x3A, 0x4F, 0xFF);

        private static readonly Color WindowBody = new Color32(0xEA, 0xF4, 0xFB, 0xFF);
        private static readonly Color DarkText = new Color32(0x33, 0x3A, 0x48, 0xFF);

        private static void CreateHud(Transform canvas, GameController controller)
        {
            // HudView kökte; açılıp kapanan parçalar alt objelerde (kapanan objenin
            // OnDisable'ı olay aboneliğini koparırdı).
            RectTransform root = CreateRect("Hud", canvas);
            Stretch(root);

            RectTransform moves = CreateRect("MovesPanel", root);
            moves.anchorMin = moves.anchorMax = new Vector2(0.5f, 1f);
            moves.anchoredPosition = new Vector2(0f, -165f);
            moves.sizeDelta = new Vector2(300f, 96f);

            RectTransform ribbon = CreateRect("LevelRibbon", moves);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 0f);
            ribbon.anchoredPosition = new Vector2(0f, -16f);
            ribbon.sizeDelta = new Vector2(210f, 52f);
            CreateImage(ribbon, HudGreen, "Panel");
            Text levelText = CreateText("Label", ribbon, "Seviye 1", 29, HudDarkGreen);

            // Kutu şeritten sonra: şeridin üstünde çizilir.
            RectTransform box = CreateRect("Box", moves);
            Stretch(box);
            Image frame = CreateImage(box, HudGreen, "Panel");
            RectTransform face = CreateRect("Face", box);
            Stretch(face);
            face.offsetMin = new Vector2(6f, 9f);
            face.offsetMax = new Vector2(-6f, -6f);
            Image background = CreateImage(face, HudTeal, "Panel");
            var skull = Icon("Hard", moves, "Skull", 60f, 0f, 51f);

            Text movesText = CreateText("Label", box, "Hamle: 0", 50, Color.white);
            OutlineText(movesText, 2.5f);

            RectTransform allClear = CreateRect("AllClear", root);
            allClear.anchorMin = allClear.anchorMax = new Vector2(0.5f, 0.565f);
            allClear.sizeDelta = new Vector2(422f, 226f);
            CreateImage(allClear, Color.white, "AllClear").preserveAspect = true;
            allClear.gameObject.SetActive(false);

            RectTransform hard = CreateRect("HardIntro", root);
            Stretch(hard);
            // Bant ekrandayken tahtaya dokunuş geçmez (kullanıcı geri bildirimi, 22 Kasım):
            // karartma ışın yakalar, InputHandler.IsOverUI dokunuşu tahtaya iletmez.
            CreateImage(hard, new Color(0f, 0f, 0f, 0.83f)).raycastTarget = true;
            CanvasGroup hardGroup = hard.gameObject.AddComponent<CanvasGroup>();
            hardGroup.blocksRaycasts = true;
            RectTransform strip = CreateRect("Band", hard);
            strip.anchorMin = new Vector2(0f, 0.5f);
            strip.anchorMax = new Vector2(1f, 0.5f);
            strip.sizeDelta = new Vector2(0f, 280f);
            CreateImage(strip, Color.white, "HardBand");
            Icon("Skull", strip, "Skull", 268f, 0f, 154f);
            RectTransform hardLabel = CreateRect("Text", strip);
            hardLabel.anchoredPosition = new Vector2(0f, -38f);
            hardLabel.sizeDelta = new Vector2(330f, 112f);
            OutlineText(CreateText("Label", hardLabel, "Hard", 72, Color.white), 3f);
            hard.gameObject.SetActive(false);

            var hud = root.gameObject.AddComponent<HudView>();
            SetReference(hud, "hardIntro", hardGroup);
            SetReference(hud, "controller", controller);
            SetReference(hud, "movesPanel", moves.gameObject);
            SetReference(hud, "movesBackground", background);
            SetReference(hud, "movesText", movesText);
            SetReference(hud, "frame", frame);
            SetReference(hud, "ribbon", ribbon.GetComponent<Image>());
            SetReference(hud, "skull", skull.gameObject);
            SetReference(hud, "levelText", levelText);
            SetReference(hud, "allClear", allClear);
        }

        // --- Boosterlar (Faz 9) ------------------------------------------------
        // Level_20.png (720 × 1616): butonlar ~100 × 95 px, merkezleri alttan ~100 px,
        // aralarında ~140 px. Kürek bandı image(20261116-205144): ekranın ~%82'si
        // genişlikte, gri, sol üstte turuncu "Kürek", altında beyaz açıklama, sağda
        // kırmızı X. Ampul ve kürek simgeleri özgün çizimlerdir.

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
            RectTransform hint = CreateBoosterButton(root, "HintButton", "Bulb", out hintCount);
            RectTransform shovel = CreateBoosterButton(root, "ShovelButton", "Spade", out shovelCount);
            UnityEventTools.AddPersistentListener(hint.GetComponent<Button>().onClick, view.OnHint);
            UnityEventTools.AddPersistentListener(shovel.GetComponent<Button>().onClick, view.OnShovel);

            RectTransform banner = CreateRect("ShovelBanner", root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0f);
            banner.anchoredPosition = new Vector2(0f, 131f);
            banner.sizeDelta = new Vector2(816f, 156f);
            CreateImage(banner, BannerBody, "Panel").raycastTarget = true;
            Icon("Symbol", banner, "Spade", 110f, -330f, 0f);

            RectTransform titleRect = CreateRect("Title", banner);
            titleRect.anchoredPosition = new Vector2(0f, 32f);
            titleRect.sizeDelta = new Vector2(700f, 70f);
            Text title = CreateText("Label", titleRect, "Kürek", 54, BannerTitle);
            title.gameObject.AddComponent<Outline>().effectColor = TextOutline;

            RectTransform descriptionRect = CreateRect("Description", banner);
            descriptionRect.anchoredPosition = new Vector2(32f, -18f);
            descriptionRect.sizeDelta = new Vector2(700f, 60f);
            CreateText("Label", descriptionRect, "Seçilen yılanı bölümden kaldırır!", 36, Color.white);

            RectTransform cancel = CreateRect("CancelButton", banner);
            cancel.anchorMin = cancel.anchorMax = new Vector2(1f, 1f);
            cancel.anchoredPosition = new Vector2(-37f, -37f);
            cancel.sizeDelta = new Vector2(72f, 72f);
            Image cancelImage = CreateImage(cancel, CancelRed, "Disc");
            cancelImage.raycastTarget = true;
            var cancelButton = cancel.gameObject.AddComponent<Button>();
            cancelButton.targetGraphic = cancelImage;
            CreateText("Label", cancel, "X", 54, Color.white);
            UnityEventTools.AddPersistentListener(cancelButton.onClick, view.OnCancelShovel);

            banner.gameObject.SetActive(false);

            SetReference(view, "controller", controller);
            SetReference(view, "hintButton", hint);
            var hintFlash = Icon("HintFlash", root, "TapRing", 116f, 0f, 0f).GetComponent<Image>();
            hintFlash.gameObject.SetActive(false);
            SetReference(view, "hintFlash", hintFlash);
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
            button.anchoredPosition = new Vector2(0f, 131f);
            button.sizeDelta = new Vector2(142f, 146f);

            Image image = CreateImage(button, Color.white, "Booster");
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            var component = button.gameObject.AddComponent<Button>();
            component.targetGraphic = image;
            ColorBlock colors = component.colors;
            colors.disabledColor = Color.white;
            component.colors = colors;

            Icon("Symbol", button, label, 117f, 0f, 7f);

            RectTransform badge = CreateRect("Badge", button);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0f);
            badge.anchoredPosition = new Vector2(0f, 5f);
            badge.sizeDelta = new Vector2(60f, 60f);
            CreateImage(badge, BoosterBadge, "Disc");
            count = CreateText("Count", badge, "1", 35, Color.white);
            OutlineText(count, 2f);

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
            glowRect.sizeDelta = new Vector2(420f, 100f);
            var glow = glowRect.gameObject.AddComponent<Image>();
            glow.raycastTarget = false;

            Text label = CreateText("Label", streak, "Great!", 80, Color.white);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(5f, -5f);
            Text multiplier = CreateText("Multiplier", streak, "", 94, Color.white);
            var multiplierOutline = multiplier.gameObject.AddComponent<Outline>();
            multiplierOutline.effectDistance = outline.effectDistance;
            multiplier.gameObject.SetActive(false);
            streak.gameObject.SetActive(false);

            SetReference(view, "controller", controller);
            SetReference(view, "vignette", vignette);
            SetReference(view, "streakRoot", streak);
            SetReference(view, "streakGlow", glow);
            SetReference(view, "streakText", label);
            SetReference(view, "streakOutline", outline);
            SetReference(view, "streakMultiplier", multiplier);
            SetReference(view, "streakMultiplierOutline", multiplierOutline);
            RectTransform praise = CreateRect("Praise", root);
            praise.sizeDelta = Vector2.one * 108f;
            Icon("Thumb", praise, "PraiseThumb", 108f, 0f, 0f);
            praise.gameObject.AddComponent<CanvasGroup>();
            praise.gameObject.SetActive(false);
            SetReference(view, "praiseRoot", praise);
            return view;
        }

        private static SfxPlayer CreateSfxPlayer(GameObject host, GameController controller, FeedbackView feedback)
        {
            var player = host.AddComponent<SfxPlayer>(); // AudioSource RequireComponent ile gelir
            SetReference(player, "controller", controller);
            SetReference(player, "feedback", feedback);
            return player;
        }

        private static void CreateResultWindow(Transform canvas, GameController controller)
        {
            RectTransform root = CreateRect("Result", canvas);
            Stretch(root);

            // Overlay ışın yakalar: pencere açıkken tahtaya dokunuş geçmez.
            RectTransform overlay = CreateRect("Overlay", root);
            Stretch(overlay);
            CreateImage(overlay, new Color(0f, 0f, 0f, 0.982f)).raycastTarget = true;

            RectTransform card = CreateRect("Card", overlay);
            card.sizeDelta = new Vector2(900f, 930f);
            CreateImage(card, Color.white, "Glass");
            var inner = CreateRect("Paper", card);
            Stretch(inner);
            inner.offsetMin = new Vector2(18f, 24f);
            inner.offsetMax = new Vector2(-18f, -24f);
            CreateImage(inner, WindowBody, "Panel");

            RectTransform header = CreateRect("Header", card);
            header.anchorMin = header.anchorMax = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(560f, 110f);
            CreateImage(header, Color.white, "Glass");
            Text title = CreateText("Title", header, "TAMAMLANDI", 60, Color.white);
            title.gameObject.AddComponent<Outline>().effectColor = TextOutline;

            var retrySymbol = Icon("RetrySymbol", card, "Restart", 175f, 0f, 112f);
            var trophy = Icon("Trophy", card, "Trophy", 690f, 0f, 50f);
            trophy.gameObject.SetActive(false);

            RectTransform messageRect = CreateRect("Message", card);
            messageRect.anchoredPosition = new Vector2(0f, 40f);
            messageRect.sizeDelta = new Vector2(720f, 160f);
            Text message = CreateText("Label", messageRect, "", 52, DarkText);

            RectTransform buttonRect = CreateRect("Button", card);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 120f);
            buttonRect.sizeDelta = new Vector2(480f, 130f);
            Image buttonImage = CreateImage(buttonRect, Color.white, "Action");
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
            SetReference(view, "paper", inner.gameObject);
            SetReference(view, "retrySymbol", retrySymbol.gameObject);
            SetReference(view, "trophy", trophy.gameObject);
            SetReference(view, "completedRibbon", InterfaceSprite("Ribbon"));
            UnityEventTools.AddPersistentListener(button.onClick, view.OnButton);
        }

        private static Sprite InterfaceSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Interface/{name}.png")
                   ?? throw new InvalidOperationException($"Missing interface sprite: {name}");
        }

        private static RectTransform Icon(string name, RectTransform parent, string sprite, float size, float x, float y)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = Vector2.one * size;
            rect.anchoredPosition = new Vector2(x, y);
            CreateImage(rect, Color.white, sprite);
            return rect;
        }

        private static void OutlineText(Text text, float width)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = TextOutline;
            outline.effectDistance = new Vector2(width, -width);
        }

        private static void CreateTutorials(Transform canvas, GameController controller)
        {
            RectTransform root = CreateRect("Tutorials", canvas);
            Stretch(root);
            var view = root.gameObject.AddComponent<TutorialView>();
            RectTransform overlay = CreateRect("Overlay", root);
            Stretch(overlay);
            CreateImage(overlay, new Color(0f, 0f, 0f, 0.982f)).raycastTarget = true;
            overlay.gameObject.AddComponent<CanvasGroup>();
            RectTransform card = CreateRect("Card", overlay);
            card.sizeDelta = new Vector2(884f, 1174f);
            CreateImage(card, Color.white, "TutorialCard");
            AddTutorialShadow(card, new Vector2(0f, -9f));
            RectTransform header = CreateRect("Header", card);
            header.anchoredPosition = new Vector2(0f, 568f);
            header.sizeDelta = new Vector2(608f, 154f);
            CreateImage(header, Color.white, "TutorialHeader");
            AddTutorialShadow(header, new Vector2(0f, -7f));
            Text title = CreateText("Title", header, "", 54, Color.white);
            OutlineText(title, 3f);
            var bulbGlow = Icon("BulbGlow", card, "LampGlow", 570f, 32f, 151f).GetComponent<Image>();
            var picture = Icon("Picture", card, "Bridge", 605f, 0f, 120f);
            picture.GetComponent<Image>().preserveAspect = true;
            RectTransform line = CreateRect("Description", card);
            line.sizeDelta = new Vector2(745f, 190f);
            line.anchoredPosition = new Vector2(0f, -205f);
            Text description = CreateText("Label", line, "", 46, Color.white);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            OutlineText(description, 2f);
            RectTransform accept = CreateRect("Accept", card);
            accept.sizeDelta = new Vector2(528f, 166f);
            accept.anchoredPosition = new Vector2(0f, -433f);
            Image face = CreateImage(accept, Color.white, "TutorialAccept");
            AddTutorialShadow(accept, new Vector2(1f, -8f));
            face.raycastTarget = true;
            var button = accept.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            OutlineText(CreateText("Label", accept, "TAMAM", 62, Color.white), 3f);
            UnityEventTools.AddPersistentListener(button.onClick, view.Dismiss);
            RectTransform guide = CreateRect("FreeGuide", root);
            guide.sizeDelta = new Vector2(150f, 175f);
            // Dört panel ortada gerçek buton için açık bir alan bırakır.
            AddGuideShade(guide, new Vector2(0f, 5087.5f), new Vector2(10000f, 10000f));
            AddGuideShade(guide, new Vector2(0f, -5087.5f), new Vector2(10000f, 10000f));
            AddGuideShade(guide, new Vector2(-5075f, 0f), new Vector2(10000f, 175f));
            AddGuideShade(guide, new Vector2(5075f, 0f), new Vector2(10000f, 175f));
            RectTransform free = CreateRect("Free", guide);
            free.anchoredPosition = new Vector2(0f, -62f);
            free.sizeDelta = new Vector2(115f, 47f);
            CreateImage(free, Color.white, "Action");
            OutlineText(CreateText("Label", free, "Free", 33, Color.white), 2f);
            var freeRing = Icon("PressRing", guide, "TapRing", 162f, 0f, 0f).GetComponent<Image>();
            RectTransform freeHand = CreatePressHand(guide, 187f, 195f);
            guide.gameObject.SetActive(false);

            RectTransform banner = CreateRect("TapBanner", root);
            banner.anchorMin = new Vector2(0.08f, 0.23f);
            banner.anchorMax = new Vector2(0.92f, 0.23f);
            banner.sizeDelta = new Vector2(0f, 108f);
            CreateImage(banner, new Color32(255, 222, 68, 255), "Panel").pixelsPerUnitMultiplier = 0.65f;
            RectTransform bannerFill = CreateRect("Fill", banner);
            Stretch(bannerFill);
            bannerFill.offsetMin = new Vector2(5f, 5f);
            bannerFill.offsetMax = new Vector2(-5f, -5f);
            CreateImage(bannerFill, new Color32(55, 69, 89, 255), "Panel").pixelsPerUnitMultiplier = 0.65f;
            Text tapLabel = CreateText("Label", bannerFill, "Dokun ve yılanı çıkar!", 46, Color.white);
            tapLabel.resizeTextForBestFit = true;
            tapLabel.resizeTextMinSize = 22;
            tapLabel.resizeTextMaxSize = 46;
            OutlineText(tapLabel, 2f);
            banner.gameObject.SetActive(false);

            RectTransform tapMarker = CreateRect("TapMarker", root);
            var tapRing = Icon("TapRing", tapMarker, "TapRing", 160f, 0f, 0f).GetComponent<Image>();
            RectTransform tapHand = CreatePressHand(tapMarker, 170f, 65f);
            tapMarker.gameObject.SetActive(false);

            SetReference(view, "guide", guide);
            SetReference(view, "hintButton", canvas.Find("Boosters/HintButton"));
            SetReference(view, "shovelButton", canvas.Find("Boosters/ShovelButton"));
            SetReference(view, "card", card);
            SetReference(view, "bulbGlow", bulbGlow);
            SetReference(view, "freeHand", freeHand);
            SetReference(view, "freeRing", freeRing);
            SetReference(view, "tapBanner", banner.gameObject);
            SetReference(view, "tapMarker", tapMarker);
            SetReference(view, "tapHand", tapHand);
            SetReference(view, "tapRing", tapRing);
            view.Configure(controller, overlay.gameObject, title, description, picture.GetComponent<Image>(),
                new[] { InterfaceSprite("Bridge"), InterfaceSprite("Tunnel"), InterfaceSprite("Bulb"),
                        InterfaceSprite("Spade"), InterfaceSprite("Arrow") });
            overlay.gameObject.SetActive(false);
        }

        private static RectTransform CreatePressHand(RectTransform parent, float size, float angle)
        {
            RectTransform hand = CreateRect("Hand", parent);
            RectTransform pointer = Icon("Pointer", hand, "Pointer", size, 0f, 0f);
            pointer.pivot = new Vector2(0.48f, 0.86f);
            pointer.localRotation = Quaternion.Euler(0f, 0f, angle);
            return hand;
        }

        private static void AddTutorialShadow(RectTransform rect, Vector2 offset)
        {
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(22, 13, 62, 138);
            shadow.effectDistance = offset;
        }

        private static void AddGuideShade(RectTransform parent, Vector2 offset, Vector2 size)
        {
            RectTransform rect = CreateRect("Shade", parent);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            Image shade = rect.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.92f);
            shade.raycastTarget = false;
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

        private static Image CreateImage(RectTransform rect, Color color, string skin = null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = skin == null ? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd") : InterfaceSprite(skin);
            image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
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
            label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/LilitaOne-Regular.ttf")
                         ?? throw new InvalidOperationException("Lilita One font asset is missing.");
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
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
