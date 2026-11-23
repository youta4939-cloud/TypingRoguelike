#if UNITY_EDITOR
using System.Collections.Generic;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public sealed class EnemyDefinitionEditorWindow : EditorWindow
    {
        const string EnemyFolder = "Assets/_Project/Data/Enemies";
        const float ListWidthMin = 320f;
        const float ListWidthMax = 720f;

        static readonly string[] SpawnCategoryLabels = { "通常", "エリート（強敵戦）", "ボス" };

        readonly List<EnemyDefinition> _enemies = new();
        Vector2 _listScroll;
        Vector2 _detailScroll;
        EnemyDefinition _selected;
        EnemyDefinitionEditorVisual _visual;
        string _filter = string.Empty;
        [SerializeField] float _listPaneWidth = 420f;
        [SerializeField] float _previewPaneHeight = 140f;
        [SerializeField] bool _previewExpanded;
        bool _splitterDrag;
        bool _previewSplitterDrag;
        const float PreviewPaneMin = 120f;
        const float PreviewPaneMax = 360f;

        internal static void RepaintAllEnemyWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<EnemyDefinitionEditorWindow>();
            foreach (var w in windows)
                w.Repaint();
        }

        public bool TryGetSelected(out EnemyDefinition enemy)
        {
            enemy = _selected;
            return enemy != null;
        }

        [MenuItem("Tools/TypingRoguelike/敵モンスター編集")]
        public static void Open()
        {
            var w = GetWindow<EnemyDefinitionEditorWindow>(false, "敵モンスター", true);
            ApplyWindowSizeLimits(w);
            w.EnsureEditorState();
            w.RefreshList();
        }

        [MenuItem("Tools/TypingRoguelike/敵プレビュー（戦闘風）")]
        public static void OpenPreviewWindowMenu()
        {
            EnemyDefinition selected = null;
            var mains = Resources.FindObjectsOfTypeAll<EnemyDefinitionEditorWindow>();
            foreach (var m in mains)
            {
                if (m != null && m.TryGetSelected(out var e))
                {
                    selected = e;
                    break;
                }
            }
            EnemyVisualPreviewWindow.Open(selected);
        }

        void OnEnable()
        {
            EnsureEditorState();
            ApplyWindowSizeLimits(this);
            RefreshList();
        }

        void EnsureEditorState()
        {
            _visual ??= new EnemyDefinitionEditorVisual();
        }

        static void ApplyWindowSizeLimits(EditorWindow w)
        {
            w.minSize = new Vector2(680, 420);
            w.maxSize = new Vector2(12000, 12000);
        }

        void OnDisable() => _visual?.OnDisable();

        void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawListPane();
            DrawPaneSplitter();
            DrawDetailPane();
            EditorGUILayout.EndHorizontal();
        }

        void DrawPaneSplitter()
        {
            var rect = GUILayoutUtility.GetRect(6f, 10000f, GUILayout.Width(6f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            var center = new Rect(rect.x + 1f, rect.y, 4f, rect.height);
            EditorGUIUtility.AddCursorRect(center, MouseCursor.ResizeHorizontal);

            var ev = Event.current;
            if (ev.type == EventType.MouseDown && center.Contains(ev.mousePosition))
            {
                _splitterDrag = true;
                ev.Use();
            }

            if (ev.type == EventType.MouseUp)
                _splitterDrag = false;

            if (_splitterDrag && ev.type == EventType.MouseDrag)
            {
                _listPaneWidth = Mathf.Clamp(_listPaneWidth + ev.delta.x, ListWidthMin, ListWidthMax);
                ev.Use();
                Repaint();
            }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(48)))
                RefreshList();
            if (GUILayout.Button("新規", EditorStyles.toolbarButton, GUILayout.Width(48)))
                CreateNewEnemy();
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(48)))
                SaveAll();
            if (GUILayout.Button("カタログ同期", EditorStyles.toolbarButton, GUILayout.Width(96)))
            {
                EnemyCatalogSync.SyncFromAllEnemies();
                EditorUtility.DisplayDialog("TypingRoguelike", "EnemyCatalog と遭遇プールを更新しました。", "OK");
            }
            EditorGUI.BeginDisabledGroup(_selected == null);
            if (GUILayout.Button("削除", EditorStyles.toolbarButton, GUILayout.Width(48)))
                DeleteSelected();
            EditorGUI.EndDisabledGroup();
            EnsureEditorState();
            if (_selected != null && _visual.ShowsPinnedPreview(_selected))
            {
                if (GUILayout.Button("別窓プレビュー", EditorStyles.toolbarButton, GUILayout.Width(96)))
                    EnemyVisualPreviewWindow.Open(_selected);
                if (GUILayout.Button(
                        _previewExpanded ? "下部プレビュー閉" : "下部プレビュー",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(100)))
                    _previewExpanded = !_previewExpanded;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("検索", GUILayout.Width(36));
            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
        }

        void DrawListPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_listPaneWidth));
            DrawListHeader();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var enemy in _enemies)
            {
                if (!PassesFilter(enemy))
                    continue;
                DrawListRow(enemy);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawListHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("", GUILayout.Width(32));
            GUILayout.Label("名前", GUILayout.Width(100));
            GUILayout.Label("種類", GUILayout.Width(44));
            GUILayout.Label("層", GUILayout.Width(70));
            GUILayout.Label("HP", GUILayout.Width(52));
            GUILayout.Label("ブレイク", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }

        void DrawListRow(EnemyDefinition enemy)
        {
            if (enemy == null)
                return;

            var selected = enemy == _selected;
            var style = selected ? EditorStyles.helpBox : EditorStyles.textField;
            EditorGUILayout.BeginHorizontal(style);
            EnemyDefinitionEditorVisual.DrawListThumbnail(enemy, 28f);
            if (GUILayout.Button(enemy.displayName, EditorStyles.label, GUILayout.Width(100)))
            {
                _selected = enemy;
                GUI.FocusControl(null);
                EnemyVisualPreviewWindow.SetEnemyIfOpen(enemy);
            }
            GUILayout.Label(SpawnLabel(enemy.spawnCategory), GUILayout.Width(44));
            GUILayout.Label($"{enemy.floorMin}-{enemy.floorMax}", GUILayout.Width(70));
            GUILayout.Label($"{enemy.maxHp:0}", GUILayout.Width(52));
            DrawBreakChipRow(enemy, 10f);
            EditorGUILayout.EndHorizontal();
        }

        void DrawDetailPane()
        {
            EnsureEditorState();
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            var canPreview = _selected != null && _visual.ShowsPinnedPreview(_selected);
            // スクロールが下を食い潰さないよう、フッター分の高さを先に確保する
            var footerReserve = canPreview
                ? (_previewExpanded ? _previewPaneHeight + 54f : 28f)
                : 0f;
            var scrollHeight = Mathf.Max(120f, position.height - 28f - footerReserve);

            _detailScroll = EditorGUILayout.BeginScrollView(
                _detailScroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(scrollHeight));
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("左の一覧から敵を選択するか「新規」で作成してください。", MessageType.Info);
            }
            else
            {
                DrawDetail(_selected);
            }
            EditorGUILayout.EndScrollView();

            if (canPreview)
                DrawPinnedPreviewFooter();

            EditorGUILayout.EndVertical();
        }

        void DrawPinnedPreviewFooter()
        {
            EnsureEditorState();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(28f));
            if (GUILayout.Button("別窓でプレビュー", GUILayout.Width(120), GUILayout.Height(24f)))
                EnemyVisualPreviewWindow.Open(_selected);
            var prev = _previewExpanded;
            _previewExpanded = GUILayout.Toggle(
                _previewExpanded,
                _previewExpanded ? "▼ 下部簡易プレビューを閉じる" : "▶ 下部にも簡易プレビュー",
                "Button",
                GUILayout.Height(24f),
                GUILayout.ExpandWidth(true));
            if (_previewExpanded != prev)
                Repaint();
            EditorGUILayout.EndHorizontal();

            if (!_previewExpanded)
                return;

            var split = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(split, new Color(0.35f, 0.55f, 0.85f, 1f));
            EditorGUIUtility.AddCursorRect(split, MouseCursor.ResizeVertical);
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && split.Contains(ev.mousePosition))
            {
                _previewSplitterDrag = true;
                ev.Use();
            }
            if (ev.type == EventType.MouseUp)
                _previewSplitterDrag = false;
            if (_previewSplitterDrag && ev.type == EventType.MouseDrag)
            {
                _previewPaneHeight = Mathf.Clamp(_previewPaneHeight - ev.delta.y, PreviewPaneMin, PreviewPaneMax);
                ev.Use();
                Repaint();
            }

            try
            {
                _visual.DrawPinnedPreview(_selected, _previewPaneHeight);
            }
            catch (System.Exception ex)
            {
                EditorGUILayout.HelpBox($"プレビュー描画失敗: {ex.Message}", MessageType.Error);
            }
        }

        void DrawDetail(EnemyDefinition e)
        {
            if (EnemyDefinitionAttackUtil.TryMigrateLegacyIfNeeded(e))
            {
                EditorUtility.SetDirty(e);
                AssetDatabase.SaveAssets();
            }

            DrawSectionJumpBar();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("① 基本", EditorStyles.boldLabel);
            e.enemyId = EditorGUILayout.TextField("内部ID", e.enemyId);
            e.displayName = EditorGUILayout.TextField("表示名", e.displayName);
            e.spawnCategory = (EnemySpawnCategory)EditorGUILayout.Popup(
                "種類",
                (int)e.spawnCategory,
                SpawnCategoryLabels);
            EditorGUILayout.BeginHorizontal();
            e.floorMin = EditorGUILayout.IntField("出現開始層", e.floorMin);
            e.floorMax = EditorGUILayout.IntField("出現終了層", e.floorMax);
            EditorGUILayout.EndHorizontal();
            e.pickWeight = Mathf.Max(1, EditorGUILayout.IntField("出現抽選の重み", e.pickWeight));
            e.maxHp = EditorGUILayout.FloatField("最大HP", e.maxHp);

            EditorGUILayout.Space(10);
            DrawAttackSection(e);

            EditorGUILayout.Space(10);
            DrawBreakSection(e);

            EditorGUILayout.Space(10);
            DrawVisualSection(e);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⑤ その他", EditorStyles.boldLabel);
            e.enrageHpFraction = EditorGUILayout.Slider("狂暴HP割合", e.enrageHpFraction, 0.05f, 1f);
            e.enrageIntervalMultiplier = EditorGUILayout.FloatField("狂暴 間隔倍率", e.enrageIntervalMultiplier);
            e.designerNotes = EditorGUILayout.TextArea(e.designerNotes, GUILayout.MinHeight(40));

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("旧攻撃→3枠に取り込み", GUILayout.Width(160)))
            {
                EnemyDefinitionAttackUtil.MigrateLegacyToAttackSlots(e);
                EditorUtility.SetDirty(e);
            }
            if (GUILayout.Button("この敵を削除…", GUILayout.Width(120)))
                DeleteSelected();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(40f);

            if (EditorGUI.EndChangeCheck())
            {
                EnemyDefinitionAttackUtil.EnsureAttackArray(e);
                EditorUtility.SetDirty(e);
            }
        }

        void DrawSectionJumpBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("移動", EditorStyles.miniLabel, GUILayout.Width(28));
            if (GUILayout.Button("①基本", EditorStyles.toolbarButton))
                _detailScroll.y = 0f;
            if (GUILayout.Button("②攻撃", EditorStyles.toolbarButton))
                _detailScroll.y = 180f;
            if (GUILayout.Button("③ブレイク", EditorStyles.toolbarButton))
                _detailScroll.y = 320f;
            if (GUILayout.Button("④画像", EditorStyles.toolbarButton))
                _detailScroll.y = 420f;
            if (GUILayout.Button("⑤その他", EditorStyles.toolbarButton))
                _detailScroll.y = 700f;
            EditorGUILayout.EndHorizontal();
        }

        void DrawVisualSection(EnemyDefinition e)
        {
            EnsureEditorState();
            try
            {
                if (_visual.DrawForm(e, ref _previewExpanded) && _previewExpanded)
                    Repaint();
            }
            catch (System.Exception ex)
            {
                EditorGUILayout.HelpBox(
                    $"画像・演出UIの描画に失敗しました（Console も確認）:\n{ex.Message}",
                    MessageType.Error);
            }
        }

        void DrawAttackSection(EnemyDefinition e)
        {
            EditorGUILayout.LabelField("② 攻撃（3枠・ローテ）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "枠1→2→3のローテ。ダメージと間隔秒が両方0より大きい枠だけ有効。初回待ち0なら間隔秒を使う。",
                EditorStyles.wordWrappedMiniLabel);
            EnemyDefinitionAttackUtil.EnsureAttackArray(e);
            e.attackPickMode = EnemyAttackPickMode.Rotate;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("枠", GUILayout.Width(24));
            GUILayout.Label("ダメージ", GUILayout.Width(64));
            GUILayout.Label("効果", GUILayout.Width(88));
            GUILayout.Label("間隔秒", GUILayout.Width(52));
            GUILayout.Label("効果値", GUILayout.Width(52));
            GUILayout.Label("初回待ち", GUILayout.Width(52));
            GUILayout.Label("演出強", GUILayout.Width(44));
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < EnemyDefinitionAttackUtil.AttackSlotCount; i++)
            {
                ref var slot = ref e.attacks[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), GUILayout.Width(24));
                slot.damage = EditorGUILayout.FloatField(slot.damage, GUILayout.Width(64));
                slot.effect = (EnemyAttackEffectKind)EditorGUILayout.EnumPopup(slot.effect, GUILayout.Width(88));
                slot.intervalSeconds = EditorGUILayout.FloatField(slot.intervalSeconds, GUILayout.Width(52));
                slot.effectValue = EditorGUILayout.FloatField(slot.effectValue, GUILayout.Width(52));
                slot.firstDelaySeconds = EditorGUILayout.FloatField(slot.firstDelaySeconds, GUILayout.Width(52));
                slot.visualIntensity = (EnemyAttackVisualIntensity)EditorGUILayout.EnumPopup(
                    slot.visualIntensity, GUILayout.Width(44));
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawBreakSection(EnemyDefinition e)
        {
            EditorGUILayout.LabelField("③ ブレイク", EditorStyles.boldLabel);
            e.breakEnabled = EditorGUILayout.Toggle("有効", e.breakEnabled);
            if (!e.breakEnabled)
                return;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("3赤3青3黄", GUILayout.Width(88)))
                e.breakRequirements = PresetBreak333();
            if (GUILayout.Button("赤1", GUILayout.Width(48)))
                e.breakRequirements = new[] { ElementColor.Red };
            if (GUILayout.Button("クリア", GUILayout.Width(56)))
                e.breakRequirements = System.Array.Empty<ElementColor>();
            if (GUILayout.Button("+", GUILayout.Width(28)))
                AddBreakSlot(e, ElementColor.Red);
            EditorGUILayout.EndHorizontal();

            if (e.breakRequirements == null)
                e.breakRequirements = System.Array.Empty<ElementColor>();

            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < e.breakRequirements.Length; i++)
            {
                DrawBreakColorChip(ref e.breakRequirements[i], 36f, 28f);
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    RemoveBreakAt(e, i);
                    break;
                }
            }
            EditorGUILayout.EndHorizontal();

            e.breakDuration = EditorGUILayout.FloatField("ブレイク時間(秒)", e.breakDuration);
            e.breakDamageMultiplier = EditorGUILayout.FloatField("与ダメ倍率", e.breakDamageMultiplier);
        }

        static ElementColor[] PresetBreak333()
        {
            return new[]
            {
                ElementColor.Red, ElementColor.Red, ElementColor.Red,
                ElementColor.Blue, ElementColor.Blue, ElementColor.Blue,
                ElementColor.Yellow, ElementColor.Yellow, ElementColor.Yellow
            };
        }

        static void AddBreakSlot(EnemyDefinition e, ElementColor color)
        {
            var list = e.breakRequirements != null
                ? new List<ElementColor>(e.breakRequirements)
                : new List<ElementColor>();
            list.Add(color);
            e.breakRequirements = list.ToArray();
        }

        static void RemoveBreakAt(EnemyDefinition e, int index)
        {
            if (e.breakRequirements == null || index < 0 || index >= e.breakRequirements.Length)
                return;
            var list = new List<ElementColor>(e.breakRequirements);
            list.RemoveAt(index);
            e.breakRequirements = list.ToArray();
        }

        void DrawBreakChipRow(EnemyDefinition enemy, float size)
        {
            if (!enemy.breakEnabled || enemy.breakRequirements == null || enemy.breakRequirements.Length == 0)
            {
                GUILayout.Label("—", GUILayout.Width(40));
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.Width(120));
            foreach (var color in enemy.breakRequirements)
                DrawBreakColorChipReadOnly(color, size);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawBreakColorChipReadOnly(ElementColor color, float size)
        {
            var face = EnemyEditorBreakColors.FaceColor(color);
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, face);
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, EnemyEditorBreakColors.Abbrev(color), labelStyle);
        }

        static void DrawBreakColorChip(ref ElementColor color, float width, float height)
        {
            var face = EnemyEditorBreakColors.FaceColor(color);
            var content = new GUIContent(EnemyEditorBreakColors.Abbrev(color), EnemyEditorBreakColors.JapaneseName(color));
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = face;
            if (GUILayout.Button(content, GUILayout.Width(width), GUILayout.Height(height)))
                color = EnemyEditorBreakColors.Cycle(color);
            GUI.backgroundColor = prev;
        }

        static string SpawnLabel(EnemySpawnCategory cat) => cat switch
        {
            EnemySpawnCategory.Elite => "強敵",
            EnemySpawnCategory.Boss => "ボス",
            _ => "通常"
        };

        bool PassesFilter(EnemyDefinition e)
        {
            if (string.IsNullOrWhiteSpace(_filter))
                return true;
            var f = _filter.Trim();
            return (e.displayName?.Contains(f) ?? false)
                   || (e.enemyId?.Contains(f) ?? false)
                   || (e.name?.Contains(f) ?? false);
        }

        void RefreshList()
        {
            _enemies.Clear();
            _enemies.AddRange(EnemyCatalogSync.LoadAllEnemies());
            var migrated = 0;
            foreach (var e in _enemies)
            {
                if (e == null)
                    continue;
                if (EnemyDefinitionAttackUtil.TryMigrateLegacyIfNeeded(e))
                {
                    EditorUtility.SetDirty(e);
                    migrated++;
                }
            }

            if (migrated > 0)
                AssetDatabase.SaveAssets();
            if (_selected != null && !_enemies.Contains(_selected))
                _selected = _enemies.Count > 0 ? _enemies[0] : null;
        }

        void CreateNewEnemy()
        {
            PrototypeDataGenerator.EnsureFolderPublic(EnemyFolder);
            var id = $"enemy_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            var path = $"{EnemyFolder}/Enemy_{id}.asset";
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.enemyId = id;
            e.displayName = "新規敵";
            e.spawnCategory = EnemySpawnCategory.Normal;
            e.visual = EnemyVisualRegistration.Default;
            e.battleVisual = EnemyBattleVisualKind.StaticIllustration;
            EnemyDefinitionAttackUtil.EnsureAttackArray(e);
            AssetDatabase.CreateAsset(e, path);
            AssetDatabase.SaveAssets();
            EnemyCatalogSync.SyncFromAllEnemies();
            RefreshList();
            _selected = e;
            Selection.activeObject = e;
        }

        void DeleteSelected()
        {
            if (_selected == null)
                return;
            if (!EditorUtility.DisplayDialog("削除", $"{_selected.displayName} を削除しますか？", "削除", "キャンセル"))
                return;

            var path = AssetDatabase.GetAssetPath(_selected);
            _selected = null;
            AssetDatabase.DeleteAsset(path);
            RefreshList();
        }

        void SaveAll()
        {
            foreach (var e in _enemies)
            {
                if (e == null)
                    continue;
                EnemyDefinitionAttackUtil.EnsureAttackArray(e);
                EditorUtility.SetDirty(e);
            }

            AssetDatabase.SaveAssets();
            EnemyCatalogSync.SyncFromAllEnemies();
        }
    }
}
#endif