#if UNITY_EDITOR
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    /// <summary>
    /// 敵ビジュアルの別窓プレビュー。戦闘アリーナ風の枠の上に既存プレビュー機能を載せる。
    /// </summary>
    public sealed class EnemyVisualPreviewWindow : EditorWindow
    {
        EnemyDefinition _enemy;
        EnemyDefinitionEditorVisual _visual;
        Vector2 _scroll;

        public static void Open(EnemyDefinition enemy)
        {
            var w = GetWindow<EnemyVisualPreviewWindow>(false, "敵プレビュー（戦闘風）", true);
            w.minSize = new Vector2(720f, 560f);
            w.Bind(enemy);
            w.Show();
            w.Focus();
        }

        public static void SetEnemyIfOpen(EnemyDefinition enemy)
        {
            var windows = Resources.FindObjectsOfTypeAll<EnemyVisualPreviewWindow>();
            foreach (var w in windows)
            {
                if (w == null)
                    continue;
                w.Bind(enemy);
                w.Repaint();
            }
        }

        internal static void RepaintAll()
        {
            var windows = Resources.FindObjectsOfTypeAll<EnemyVisualPreviewWindow>();
            foreach (var w in windows)
            {
                if (w != null)
                    w.Repaint();
            }
        }

        void OnEnable()
        {
            _visual ??= new EnemyDefinitionEditorVisual();
            titleContent = new GUIContent("敵プレビュー（戦闘風）");
        }

        void OnDisable() => _visual?.OnDisable();

        public void Bind(EnemyDefinition enemy)
        {
            _enemy = enemy;
            _visual ??= new EnemyDefinitionEditorVisual();
        }

        void OnGUI()
        {
            _visual ??= new EnemyDefinitionEditorVisual();

            DrawHeader();

            if (_enemy == null)
            {
                EditorGUILayout.HelpBox(
                    "敵が選択されていません。敵モンスター編集ウィンドウで敵を選ぶか、そちらから「別窓プレビュー」を開いてください。",
                    MessageType.Info);
                return;
            }

            if (!_visual.ShowsPinnedPreview(_enemy))
            {
                EditorGUILayout.HelpBox(
                    "この敵は「立ち絵2枚」モードではないため、SpritePair プレビューは使えません。",
                    MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {
                _visual.DrawPreviewControls(_enemy);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("戦闘画面イメージ（敵スロット位置）", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "攻撃リング・単語カード位置を模した枠です。足元線・ドラッグ編集・再生ボタンは従来どおり使えます。",
                    MessageType.None);

                var canvasHeight = Mathf.Clamp(position.height - 220f, 360f, 720f);
                var frame = GUILayoutUtility.GetRect(1f, canvasHeight, GUILayout.ExpandWidth(true));
                var stage = DrawBattleChrome(frame);
                _visual.DrawPreviewCanvas(_enemy, stage);
            }
            catch (System.Exception ex)
            {
                EditorGUILayout.HelpBox($"プレビュー描画失敗: {ex.Message}", MessageType.Error);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(_enemy != null ? _enemy.displayName : "（未選択）", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("敵編集へ", EditorStyles.toolbarButton, GUILayout.Width(72)))
                EnemyDefinitionEditorWindow.Open();
            if (GUILayout.Button("選択中の敵を取込", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                var main = Resources.FindObjectsOfTypeAll<EnemyDefinitionEditorWindow>();
                foreach (var w in main)
                {
                    if (w != null && w.TryGetSelected(out var e) && e != null)
                    {
                        Bind(e);
                        break;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>戦闘アリーナ風の装飾を描き、敵ステージ矩形を返す。</summary>
        static Rect DrawBattleChrome(Rect frame)
        {
            EditorGUI.DrawRect(frame, new Color(0.06f, 0.055f, 0.07f, 1f));

            // 参照解像度に近い比率でアリーナを中央やや右に配置
            var arena = Mathf.Min(frame.width * 0.78f, frame.height * 0.88f, 600f);
            var arenaCenter = new Vector2(
                frame.x + frame.width * 0.58f,
                frame.y + frame.height * 0.50f);
            var arenaRect = new Rect(
                arenaCenter.x - arena * 0.5f,
                arenaCenter.y - arena * 0.5f,
                arena,
                arena);

            // 床・リング
            DrawDisc(arenaRect, new Color(0f, 0f, 0f, 0.55f));
            var ringInset = arena * (1f - 560f / 600f) * 0.5f;
            var ring = Inset(arenaRect, ringInset);
            Handles.BeginGUI();
            var prev = Handles.color;
            Handles.color = new Color(0.55f, 0.95f, 1f, 0.22f);
            Handles.DrawWireDisc(ring.center, Vector3.forward, ring.width * 0.5f);
            Handles.color = new Color(0.72f, 0.98f, 1f, 0.55f);
            Handles.DrawWireDisc(ring.center, Vector3.forward, ring.width * 0.48f);
            Handles.color = prev;
            Handles.EndGUI();

            // 単語カード位置（左・右・下）
            var cardW = arena * (252f / 600f);
            var cardH = arena * (106f / 600f);
            DrawFakeCard(new Rect(arenaCenter.x - arena * 0.47f - cardW * 0.15f, arenaCenter.y - cardH * 0.35f, cardW, cardH), "単語");
            DrawFakeCard(new Rect(arenaCenter.x + arena * 0.18f, arenaCenter.y - cardH * 0.35f, cardW, cardH), "単語");
            DrawFakeCard(new Rect(arenaCenter.x - cardW * 0.5f, arenaCenter.y + arena * 0.28f, cardW, cardH), "単語");

            // 敵ステージ（戦闘の 420×420 / AttackRing 560 相当）
            var stageSize = arena * (420f / 560f);
            var stage = new Rect(
                arenaCenter.x - stageSize * 0.5f,
                arenaCenter.y - stageSize * 0.5f + arena * 0.02f,
                stageSize,
                stageSize);
            EditorGUI.DrawRect(stage, new Color(0.12f, 0.10f, 0.12f, 0.55f));

            // HUD 風ラベル
            var hud = new Rect(frame.x + 10f, frame.y + 8f, 220f, 22f);
            EditorGUI.DrawRect(hud, new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(hud, "  戦闘フォーカス（敵）", EditorStyles.whiteMiniLabel);

            var namePlate = new Rect(stage.x, stage.yMax + 4f, stage.width, 18f);
            GUI.Label(namePlate, "EnemyStage 相当", EditorStyles.centeredGreyMiniLabel);

            return stage;
        }

        static Rect Inset(Rect r, float pad) =>
            new(r.x + pad, r.y + pad, Mathf.Max(4f, r.width - pad * 2f), Mathf.Max(4f, r.height - pad * 2f));

        static void DrawDisc(Rect r, Color fill)
        {
            // 粗い円近似（塗り）
            EditorGUI.DrawRect(new Rect(r.x + r.width * 0.15f, r.y, r.width * 0.7f, r.height), fill);
            EditorGUI.DrawRect(new Rect(r.x, r.y + r.height * 0.15f, r.width, r.height * 0.7f), fill);
        }

        static void DrawFakeCard(Rect r, string label)
        {
            EditorGUI.DrawRect(r, new Color(0.14f, 0.13f, 0.16f, 0.92f));
            var border = new Color(0.35f, 0.4f, 0.48f, 0.8f);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), border);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), border);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), border);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), border);
            GUI.Label(r, label, EditorStyles.centeredGreyMiniLabel);
        }
    }
}
#endif
