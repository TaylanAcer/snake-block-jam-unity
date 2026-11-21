using System.Collections.Generic;
using SnakeBlockJam.Core;
using SnakeBlockJam.Game;
using UnityEditor;
using UnityEngine;

namespace SnakeBlockJam.EditorTools
{
    // Level Design Tool (PDF §1.3, yol haritası §9). EditorWindow + IMGUI; kendi
    // 2D grid'ini çizer (Scene View / 3D picking yok). Düzenlenen şey LevelData
    // asset'idir; oyun aynı asset'i LevelCatalog üzerinden yükler.
    //
    // Pencere sadece çizim ve mouse işi yapar. Veri işlemleri LevelEditing'de,
    // doğrulama LevelValidator'da (Core + Solver). Her değişiklik Undo'ya kayıtlı.
    public sealed class LevelEditorWindow : EditorWindow
    {
        private enum Tool { Select, Snake, Bridge, Burrow, Arrow, Layer, Erase }

        private static readonly string[] ToolNames = { "Seç", "Yılan", "Köprü", "Tünel", "Ok", "Katman", "Sil" };

        // BoardView'daki varsayılan renklerle aynı (SnakeColor sırası).
        private static readonly Color[] SnakeColors =
        {
            new Color32(0xE5, 0x48, 0x4D, 0xFF), new Color32(0x3E, 0x8A, 0xF7, 0xFF),
            new Color32(0xFD, 0xC3, 0x3C, 0xFF), new Color32(0x5D, 0xC7, 0x5B, 0xFF),
            new Color32(0xEE, 0x4F, 0xA1, 0xFF), new Color32(0x9A, 0x5C, 0xE3, 0xFF),
            new Color32(0x3D, 0xCF, 0xB1, 0xFF), new Color32(0xA0, 0x68, 0x3C, 0xFF)
        };

        private static readonly string[] DirectionGlyphs = { "▲", "▶", "▼", "◀" };

        private static readonly Color BoardColor = new Color32(0xE4, 0xF1, 0xFA, 0xFF);
        private static readonly Color GridLineColor = new Color(0f, 0f, 0f, 0.06f);
        private static readonly Color DotColor = new Color32(0xB1, 0xC6, 0xD3, 0xFF);
        // Zemindeki yılanın üstüne çizilir; yarı saydam, alttaki yılan seçilebilsin diye görünür kalır.
        private static readonly Color BridgeColor = new Color32(0x4D, 0x50, 0x57, 0xC8);
        private static readonly Color ArrowColor = new Color32(0x5D, 0x5B, 0x5F, 0xFF);
        private static readonly Color SelectionColor = new Color(1f, 0.85f, 0f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.1f, 0.1f, 1f);

        private const float SidePanelWidth = 280f;

        [SerializeField] private LevelData level;
        [SerializeField] private Tool tool = Tool.Snake;
        [SerializeField] private SnakeColor snakeColor = SnakeColor.Red;
        [SerializeField] private Direction arrowDirection = Direction.Up;

        private SerializedObject _serialized;
        private List<LevelIssue> _issues = new List<LevelIssue>();
        private SnakeData _selectedSnake;
        private BridgeData _selectedBridge;
        private SnakeData _drawingSnake;
        private BridgeData _drawingBridge;
        private int _undoGroup = -1;
        private Vector2 _panelScroll;

        // Grid yerleşimi (son çizimden).
        private Rect _boardRect;
        private float _cellSize;

        [MenuItem("Snake Block Jam/Level Editor")]
        public static void Open()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(760f, 520f);

            if (window.level == null && Selection.activeObject is LevelData selected)
                window.SetLevel(selected);
        }

        public static void Open(LevelData target)
        {
            Open();
            GetWindow<LevelEditorWindow>().SetLevel(target);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            SetLevel(level);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        // Project penceresinde bir LevelData seçilince onu açar.
        private void OnSelectionChange()
        {
            if (Selection.activeObject is LevelData selected && selected != level)
            {
                SetLevel(selected);
                Repaint();
            }
        }

        private void OnUndoRedo()
        {
            // Undo listeleri yeniden oluşturur; eski SnakeData referansları geçersiz.
            ClearSelection();
            Revalidate();
            Repaint();
        }

        private void SetLevel(LevelData target)
        {
            level = target;
            _serialized = level != null ? new SerializedObject(level) : null;
            ClearSelection();
            Revalidate();
        }

        private void ClearSelection()
        {
            _selectedSnake = null;
            _selectedBridge = null;
            _drawingSnake = null;
            _drawingBridge = null;
        }

        private void Revalidate()
        {
            _issues = level != null ? LevelValidator.Validate(level) : new List<LevelIssue>();
        }

        private void MarkChanged()
        {
            EditorUtility.SetDirty(level);
            Revalidate();
            Repaint();
        }

        // --- Çizim ------------------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();

            if (level == null)
            {
                EditorGUILayout.HelpBox(
                    "Bir LevelData seç (Project penceresinde veya yukarıdaki alandan) ya da 'Yeni' ile oluştur.",
                    MessageType.Info);
                return;
            }

            if (_serialized == null || _serialized.targetObject != level)
                _serialized = new SerializedObject(level);

            EditorGUILayout.BeginHorizontal();

            _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll, GUILayout.Width(SidePanelWidth));
            DrawProperties();
            DrawTools();
            DrawSelection();
            DrawIssues();
            EditorGUILayout.EndScrollView();

            Rect gridArea = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();

            DrawGrid(gridArea);
            HandleGridInput(gridArea);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var picked = (LevelData)EditorGUILayout.ObjectField(level, typeof(LevelData), false, GUILayout.Width(220f));

            if (picked != level)
                SetLevel(picked);

            if (GUILayout.Button("Yeni", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                CreateLevel(null);

            using (new EditorGUI.DisabledScope(level == null))
            {
                if (GUILayout.Button("Kopyala", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    CreateLevel(level);

                if (GUILayout.Button("Kaydet", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                    AssetDatabase.SaveAssetIfDirty(level);

                LevelCatalog catalog = LoadCatalog();
                bool inCatalog = catalog != null && level != null && catalog.levels.Contains(level);

                using (new EditorGUI.DisabledScope(catalog == null || inCatalog))
                {
                    if (GUILayout.Button(inCatalog ? "Katalogda ✓" : "Kataloğa ekle", EditorStyles.toolbarButton,
                            GUILayout.Width(95f)))
                    {
                        Undo.RecordObject(catalog, "Add level to catalog");
                        LevelEditing.AddToCatalog(catalog, level);
                        EditorUtility.SetDirty(catalog);
                        AssetDatabase.SaveAssetIfDirty(catalog);
                    }
                }

                using (new EditorGUI.DisabledScope(level == null || LevelValidator.HasErrors(_issues)))
                {
                    if (GUILayout.Button("▶ Oyna", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                        LevelEditorPlay.Play(level);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProperties()
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            _serialized.Update();

            EditorGUILayout.PropertyField(_serialized.FindProperty("levelNumber"), new GUIContent("Numara"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("width"), new GUIContent("Genişlik"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("height"), new GUIContent("Yükseklik"));

            SerializedProperty hasLimit = _serialized.FindProperty("hasMoveLimit");
            EditorGUILayout.PropertyField(hasLimit, new GUIContent("Hamle limiti var", "L1–L5'te yok (R-MV-05)."));

            if (hasLimit.boolValue)
                EditorGUILayout.PropertyField(_serialized.FindProperty("moveLimit"), new GUIContent("Hamle limiti"));

            EditorGUILayout.PropertyField(_serialized.FindProperty("isHard"), new GUIContent("Hard"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("isRectangularBoard"),
                new GUIContent("Dikdörtgen tahta", "Grid noktaları bütün W×H'de."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("hintUnlocked"),
                new GUIContent("İpucu açık", "İpucu butonu bu bölümde görünür."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("hintCount"), new GUIContent("İpucu"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("shovelUnlocked"),
                new GUIContent("Kürek açık", "Kürek butonu bu bölümde görünür."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("shovelCount"), new GUIContent("Kürek"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("showCompletedScreen"),
                new GUIContent("TAMAMLANDI ekranı", "Kapalıysa kazanınca sonraki bölüm doğrudan açılır (L1–L4)."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("tutorial"),
                new GUIContent("Öğretici", "Bölüm başındaki kart veya dokunma öğreticisi."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("showIdleNudge"),
                new GUIContent("Dokunma dürtmesi", "İlk dokunuşa kadar kaçabilen yılanı ileri geri oynatır."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("showPraise"),
                new GUIContent("Başparmak", "Köprü üstü veya son kaçışta başparmak + konfeti."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("cameraScale"),
                new GUIContent("Kamera ölçeği", "Hücre boyutu çarpanı (1 = genel fit)."));
            EditorGUILayout.PropertyField(_serialized.FindProperty("cameraDownPixels"),
                new GUIContent("Kamera kayması (px)", "720 px genişlikli karede tahtanın aşağı kayması."));

            if (_serialized.ApplyModifiedProperties())
                Revalidate();

            EditorGUILayout.Space();
        }

        private void DrawTools()
        {
            EditorGUILayout.LabelField("Araç", EditorStyles.boldLabel);
            tool = (Tool)GUILayout.SelectionGrid((int)tool, ToolNames, 4);

            switch (tool)
            {
                case Tool.Snake:
                    snakeColor = (SnakeColor)EditorGUILayout.EnumPopup("Renk", snakeColor);
                    EditorGUILayout.HelpBox(
                        "Baştan başla, kuyruğa doğru sürükle. Geri sürüklemek son hücreyi siler. " +
                        "Kuyruğa tıklayıp sürüklemek yılanı uzatır. Yön boyun → baş.", MessageType.None);
                    break;
                case Tool.Bridge:
                    EditorGUILayout.HelpBox("Köprü yolunu sürükleyerek çiz (düz veya L).", MessageType.None);
                    break;
                case Tool.Arrow:
                    arrowDirection = (Direction)EditorGUILayout.EnumPopup("Yön", arrowDirection);
                    break;
                case Tool.Layer:
                    EditorGUILayout.HelpBox("Köprü hücresindeki segmenti Zemin ↔ Üst yapar.", MessageType.None);
                    break;
                case Tool.Erase:
                    EditorGUILayout.HelpBox("Sırayla: yılan → ok → tünel → köprü.", MessageType.None);
                    break;
            }

            EditorGUILayout.Space();
        }

        private void DrawSelection()
        {
            if (_selectedSnake != null && level.snakes.Contains(_selectedSnake))
            {
                EditorGUILayout.LabelField($"Yılan {_selectedSnake.id} ({_selectedSnake.body.Count} hücre)",
                    EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                var color = (SnakeColor)EditorGUILayout.EnumPopup("Renk", _selectedSnake.color);

                Direction direction;
                using (new EditorGUI.DisabledScope(_selectedSnake.body.Count > 1))
                    direction = (Direction)EditorGUILayout.EnumPopup(
                        new GUIContent("Yön", "Tek hücreli yılanda elle; diğerlerinde boyun → baş."),
                        _selectedSnake.direction);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(level, "Edit snake");
                    _selectedSnake.color = color;
                    _selectedSnake.direction = direction;
                    MarkChanged();
                }

                if (GUILayout.Button("Yılanı sil"))
                {
                    Undo.RecordObject(level, "Delete snake");
                    level.snakes.Remove(_selectedSnake);
                    _selectedSnake = null;
                    MarkChanged();
                }

                EditorGUILayout.Space();
            }

            if (_selectedBridge != null && level.bridges.Contains(_selectedBridge))
            {
                EditorGUILayout.LabelField($"Köprü ({_selectedBridge.path.Count} hücre)", EditorStyles.boldLabel);

                // Ekseni sessizce varsaymamak için açıkça seçilir (validator uyarır).
                if (_selectedBridge.path.Count == 1)
                {
                    EditorGUI.BeginChangeCheck();
                    bool hasAxis = EditorGUILayout.Toggle(
                        new GUIContent("Eksen seçili", "1 hücrelik köprüde zorunlu (R-BRG-11)."), _selectedBridge.hasAxis);
                    BridgeAxis axis = _selectedBridge.axis;

                    using (new EditorGUI.DisabledScope(!hasAxis))
                        axis = (BridgeAxis)EditorGUILayout.EnumPopup("Eksen", axis);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(level, "Edit bridge axis");
                        _selectedBridge.hasAxis = hasAxis;
                        _selectedBridge.axis = axis;
                        MarkChanged();
                    }
                }

                if (GUILayout.Button("Köprüyü sil"))
                {
                    Undo.RecordObject(level, "Delete bridge");
                    LevelEditing.EraseAt(level, _selectedBridge.path[0]);
                    _selectedBridge = null;
                    MarkChanged();
                }

                EditorGUILayout.Space();
            }
        }

        private void DrawIssues()
        {
            EditorGUILayout.LabelField("Doğrulama", EditorStyles.boldLabel);

            foreach (var issue in _issues)
            {
                MessageType type = issue.Severity == IssueSeverity.Error ? MessageType.Error
                    : issue.Severity == IssueSeverity.Warning ? MessageType.Warning
                    : MessageType.Info;
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }

        private void DrawGrid(Rect area)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            int width = Mathf.Max(1, level.width);
            int height = Mathf.Max(1, level.height);
            _cellSize = Mathf.Max(4f, Mathf.Floor(Mathf.Min((area.width - 16f) / width, (area.height - 16f) / height)));
            float boardWidth = _cellSize * width;
            float boardHeight = _cellSize * height;
            _boardRect = new Rect(area.x + (area.width - boardWidth) * 0.5f, area.y + (area.height - boardHeight) * 0.5f,
                boardWidth, boardHeight);

            EditorGUI.DrawRect(_boardRect, BoardColor);

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Rect cell = CellRect(x, y);
                EditorGUI.DrawRect(new Rect(cell.x, cell.y, cell.width, 1f), GridLineColor);
                EditorGUI.DrawRect(new Rect(cell.x, cell.y, 1f, cell.height), GridLineColor);

                if (x % 2 == 0 && y % 2 == 0)
                    EditorGUI.DrawRect(Inset(cell, 0.4f), DotColor);
            }

            foreach (var cell in level.burrows)
            {
                Rect rect = CellRect(cell);
                Handles.color = Color.gray;
                Handles.DrawSolidDisc(rect.center, Vector3.forward, _cellSize * 0.5f);
                Handles.color = Color.black;
                Handles.DrawSolidDisc(rect.center, Vector3.forward, _cellSize * 0.36f);
            }

            var glyph = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp((int)(_cellSize * 0.7f), 6, 40)
            };

            foreach (var arrow in level.arrows)
            {
                glyph.normal.textColor = ArrowColor;
                GUI.Label(CellRect(arrow.cell), DirectionGlyphs[(int)arrow.direction], glyph);
            }

            // Oyundaki sıra (R-BRG-03): zemin segmentleri → köprü → köprü üstü segmentler.
            foreach (var snake in level.snakes)
                DrawSnakeLayer(snake, BoardLayer.Ground);

            foreach (var bridge in level.bridges)
            {
                foreach (var cell in bridge.path)
                    EditorGUI.DrawRect(CellRect(cell), BridgeColor);

                if (bridge == _selectedBridge)
                    foreach (var cell in bridge.path)
                        Outline(CellRect(cell), SelectionColor, 2f);
            }

            foreach (var snake in level.snakes)
                DrawSnakeLayer(snake, BoardLayer.BridgeTop);

            foreach (var snake in level.snakes)
                DrawHeadAndSelection(snake, glyph);

            foreach (var issue in _issues)
            {
                if (issue.Cell.HasValue)
                    Outline(CellRect(issue.Cell.Value), ErrorColor, 2f);
            }
        }

        private const float SegmentInset = 0.14f;

        // Bir katmandaki segmentler: hücreden biraz küçük, komşular arasında
        // bağlantı parçası (yan yana iki yılan ayırt edilir). Zemin ↔ üst geçişindeki
        // bağlantı üst katmanla çizilir. Üst katman segmentleri beyaz çerçeveli.
        private void DrawSnakeLayer(SnakeData snake, BoardLayer layer)
        {
            Color color = SnakeColors[Mathf.Clamp((int)snake.color, 0, SnakeColors.Length - 1)];

            for (int i = 0; i < snake.body.Count; i++)
            {
                SegmentData segment = snake.body[i];

                if (i > 0 && LevelEditing.IsNeighbour(snake.body[i - 1].cell, segment.cell))
                {
                    bool linkOnTop = segment.layer == BoardLayer.BridgeTop ||
                                     snake.body[i - 1].layer == BoardLayer.BridgeTop;

                    if (linkOnTop == (layer == BoardLayer.BridgeTop))
                        EditorGUI.DrawRect(Connector(snake.body[i - 1].cell, segment.cell, SegmentInset), color);
                }

                if (segment.layer != layer)
                    continue;

                Rect rect = Inset(CellRect(segment.cell), SegmentInset);
                EditorGUI.DrawRect(rect, i == 0 ? color * 0.8f + new Color(0, 0, 0, 0.2f) : color);

                if (layer == BoardLayer.BridgeTop)
                    Outline(rect, Color.white, 2f);
            }
        }

        private void DrawHeadAndSelection(SnakeData snake, GUIStyle glyph)
        {
            glyph.normal.textColor = Color.white;
            GUI.Label(CellRect(snake.body[0].cell), DirectionGlyphs[(int)snake.direction], glyph);

            if (snake == _selectedSnake)
                foreach (var segment in snake.body)
                    Outline(CellRect(segment.cell), SelectionColor, 2f);
        }

        // --- Mouse ------------------------------------------------------------------

        private void HandleGridInput(Rect area)
        {
            Event e = Event.current;

            if (e.button != 0 || !area.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseUp)
                    EndStroke();
                return;
            }

            bool onBoard = TryMouseToCell(e.mousePosition, out Vector2Int cell);

            switch (e.type)
            {
                case EventType.MouseDown when onBoard:
                    _undoGroup = Undo.GetCurrentGroup();
                    Undo.RecordObject(level, $"Level Editor: {ToolNames[(int)tool]}");
                    ApplyDown(cell);
                    MarkChanged();
                    e.Use();
                    break;

                case EventType.MouseDrag when onBoard:
                    Undo.RecordObject(level, $"Level Editor: {ToolNames[(int)tool]}");

                    if (ApplyDrag(cell))
                        MarkChanged();

                    e.Use();
                    break;

                case EventType.MouseUp:
                    EndStroke();
                    e.Use();
                    break;
            }
        }

        private void ApplyDown(Vector2Int cell)
        {
            switch (tool)
            {
                case Tool.Select:
                    _selectedSnake = LevelEditing.SnakeAt(level, cell);
                    _selectedBridge = _selectedSnake == null ? LevelEditing.BridgeAt(level, cell) : null;
                    break;

                case Tool.Snake:
                    SnakeData existing = LevelEditing.SnakeAt(level, cell);

                    if (existing == null)
                        _drawingSnake = LevelEditing.StartSnake(level, cell, snakeColor);
                    else if (existing.body[existing.body.Count - 1].cell == cell)
                        _drawingSnake = existing; // kuyruktan devam: uzatma

                    _selectedSnake = _drawingSnake;
                    break;

                case Tool.Bridge:
                    BridgeData bridge = LevelEditing.BridgeAt(level, cell);

                    if (bridge == null)
                        _drawingBridge = LevelEditing.StartBridge(level, cell);
                    else if (bridge.path[bridge.path.Count - 1] == cell)
                        _drawingBridge = bridge;

                    _selectedBridge = _drawingBridge;
                    break;

                case Tool.Burrow:
                    LevelEditing.ToggleBurrow(level, cell);
                    break;

                case Tool.Arrow:
                    LevelEditing.SetArrow(level, cell, arrowDirection);
                    break;

                case Tool.Layer:
                    LevelEditing.ToggleLayer(level, cell);
                    break;

                case Tool.Erase:
                    EraseAndClearSelection(cell);
                    break;
            }
        }

        private bool ApplyDrag(Vector2Int cell)
        {
            if (_drawingSnake != null)
                return LevelEditing.ExtendSnake(_drawingSnake, cell);

            if (_drawingBridge != null)
                return LevelEditing.ExtendBridge(_drawingBridge, cell);

            if (tool == Tool.Erase)
                return EraseAndClearSelection(cell);

            return false;
        }

        private bool EraseAndClearSelection(Vector2Int cell)
        {
            if (!LevelEditing.EraseAt(level, cell))
                return false;

            _selectedSnake = null;
            _selectedBridge = null;
            return true;
        }

        // Bir sürükleme tek Undo adımı olur.
        private void EndStroke()
        {
            _drawingSnake = null;
            _drawingBridge = null;

            if (_undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_undoGroup);
                _undoGroup = -1;
            }
        }

        // --- Grid geometrisi ----------------------------------------------------------

        // Satır 0 en altta (Core koordinatı: y yukarı).
        private Rect CellRect(int x, int y)
        {
            return new Rect(_boardRect.x + x * _cellSize, _boardRect.y + (level.height - 1 - y) * _cellSize,
                _cellSize, _cellSize);
        }

        private Rect CellRect(Vector2Int cell)
        {
            return CellRect(cell.x, cell.y);
        }

        private bool TryMouseToCell(Vector2 mouse, out Vector2Int cell)
        {
            int x = Mathf.FloorToInt((mouse.x - _boardRect.x) / _cellSize);
            int row = Mathf.FloorToInt((mouse.y - _boardRect.y) / _cellSize);
            cell = new Vector2Int(x, level.height - 1 - row);
            return _cellSize > 0f && x >= 0 && x < level.width && row >= 0 && row < level.height;
        }

        private static Rect Inset(Rect rect, float fraction)
        {
            float dx = rect.width * fraction;
            float dy = rect.height * fraction;
            return new Rect(rect.x + dx, rect.y + dy, rect.width - 2f * dx, rect.height - 2f * dy);
        }

        // İki komşu segment arasındaki boşluğu kapatan parça.
        private Rect Connector(Vector2Int a, Vector2Int b, float inset)
        {
            Rect ra = Inset(CellRect(a), inset);
            Rect rb = Inset(CellRect(b), inset);
            float xMin = Mathf.Min(ra.xMin, rb.xMin);
            float yMin = Mathf.Min(ra.yMin, rb.yMin);
            float xMax = Mathf.Max(ra.xMax, rb.xMax);
            float yMax = Mathf.Max(ra.yMax, rb.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void Outline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        // --- Asset işlemleri ----------------------------------------------------------

        private static LevelCatalog LoadCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>(GameSceneBuilder.LevelCatalogPath);
        }

        // 'source' null ise boş 9×11 level, değilse kopyası. Numara: ilk boş
        // Level_NN.asset (21'den itibaren; 1–20 referans level'ları).
        private void CreateLevel(LevelData source)
        {
            int number = 21;

            while (AssetDatabase.LoadAssetAtPath<LevelData>(LevelJsonImporter.AssetPath(number)) != null)
                number++;

            LevelData created;

            if (source != null)
            {
                created = LevelEditing.CloneAs(source, number);
            }
            else
            {
                created = CreateInstance<LevelData>();
                created.levelNumber = number;
                created.width = 9;
                created.height = 11;
                created.name = $"Level_{number:00}";

                // İlerleme varsayılanları referans kurallarıyla aynı (LevelJsonImporter);
                // Inspector'dan değiştirilebilir.
                created.hintUnlocked = number >= LevelJsonImporter.HintUnlockLevel;
                created.shovelUnlocked = number >= LevelJsonImporter.ShovelUnlockLevel;
                created.showCompletedScreen = number >= LevelJsonImporter.FirstCompletedScreenLevel;
            }

            string path = LevelJsonImporter.AssetPath(number);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();

            SetLevel(created);
            Selection.activeObject = created;
        }
    }
}
