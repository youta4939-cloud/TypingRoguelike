#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using TypingRoguelike.Battle;
using TypingRoguelike.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TypingRoguelike.EditorTools
{
    public static class RunResultPrefabBuilder
    {
        const string Folder = "Assets/_Project/Resources/UI";
        const string PrefabPath = Folder + "/RunResult.prefab";
        const float Left = 530f;
        const float TableWidth = 860f;

        static readonly Color Paper = new(0.965f, 0.945f, 0.925f, 1f);
        static readonly Color Muted = new(0.62f, 0.56f, 0.54f, 1f);
        static readonly Color Line = new(0.82f, 0.74f, 0.72f, 0.45f);
        static readonly Color Death = new(0.86f, 0.58f, 0.62f, 1f);
        static readonly Color Bg = new(0.04f, 0.03f, 0.034f, 1f);

        static Sprite _fade;
        static Sprite _diamond;
        static Sprite _white;
        static TMP_FontAsset _display;
        static TMP_FontAsset _serif;
        static TMP_FontAsset _body;
        static TMP_FontAsset _condensed;

        [MenuItem("Tools/TypingRoguelike/Build Run Result Prefab")]
        public static void BuildFromMenu()
        {
            Build();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("TypingRoguelike", "リザルトのプレハブを作りました。\n" + PrefabPath, "OK");
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            _fade = Resources.Load<Sprite>("RouteLottery/Sprites/Common/center_fade_line_2x");
            _diamond = Resources.Load<Sprite>("RouteLottery/Sprites/Common/ornament_diamond_2x");
            _serif = BattleHudKit.SerifMedium;
            _body = BattleHudKit.SerifRegular;
            _condensed = BattleHudKit.Condensed;
            _display = DisplayFont();

            var canvasGo = new GameObject("RunResultCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("RunResult", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            Stretch(panel.GetComponent<RectTransform>());

            var background = Image(panel.transform, "Background", 0f, 0f, 1920f, 1080f, Bg, null);
            Stretch(background.rectTransform);
            var glow = Image(panel.transform, "Glow", 260f, 40f, 1400f, 820f,
                new Color(0.55f, 0.4f, 0.44f, 0.16f),
                Resources.Load<Sprite>("RouteLottery/Sprites/Common/soft_radial"));
            var vignette = Image(panel.transform, "Vignette", 0f, 0f, 1920f, 1080f, Color.white,
                Resources.Load<Sprite>("Battle/E5b/parts/fx/edge_vignette_1920"));
            Stretch(vignette.rectTransform);

            var title = Text(panel.transform, "Title", "RESULT", 560f, 118f, 800f, 78f, 60f, _serif, Paper,
                TextAlignmentOptions.Center, 22f);
            var subtitleRow = Box(panel.transform, "SubtitleRow", Left, 208f, TableWidth, 42f);
            Rule(subtitleRow, "LineLeft", 0f, 20f, 250f);
            var subtitle = Text(subtitleRow, "Subtitle", "エレベーター帰還", 250f, 0f, 360f, 42f, 22f, _body, Paper,
                TextAlignmentOptions.Center, 8f);
            Rule(subtitleRow, "LineRight", 610f, 20f, 250f);

            var table = Box(panel.transform, "Table", Left, 292f, TableWidth, 430f);
            Text(table, "ColRecord", "記録", 0f, 0f, 250f, 32f, 16f, _body, Muted, TextAlignmentOptions.MidlineLeft, 4f);
            Text(table, "ColActual", "実績", 250f, 0f, 320f, 32f, 16f, _body, Muted, TextAlignmentOptions.Center, 4f);
            Text(table, "ColPoints", "点数", 560f, 0f, 300f, 32f, 16f, _body, Muted, TextAlignmentOptions.MidlineRight, 4f);
            Rule(table, "HeaderLine", 0f, 44f, TableWidth);

            var samples = new[]
            {
                ("RowTyped", "入力文字数", "2,400文字", "+960"),
                ("RowTrash", "雑魚撃破", "12体", "+300"),
                ("RowSquares", "踏んだマス", "16マス", "+240"),
                ("RowBoss", "ボス撃破", "1体", "+20")
            };
            var rowViews = new RunResultRow[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                var y = 64f + i * 52f;
                var row = Box(table, samples[i].Item1, 0f, y, TableWidth, 44f);
                rowViews[i] = new RunResultRow
                {
                    label = Text(row, "Label", samples[i].Item2, 0f, 0f, 250f, 44f, 24f, _body, Paper,
                        TextAlignmentOptions.MidlineLeft),
                    actual = Text(row, "Actual", samples[i].Item3, 250f, 0f, 320f, 44f, 24f, _body, Paper,
                        TextAlignmentOptions.Center),
                    points = Text(row, "Points", samples[i].Item4, 560f, 0f, 300f, 44f, 24f, _body, Paper,
                        TextAlignmentOptions.MidlineRight)
                };
            }

            Rule(table, "SubtotalLine", 0f, 280f, TableWidth);
            Text(table, "SubtotalLabel", "小計", 0f, 296f, 250f, 44f, 24f, _body, Paper, TextAlignmentOptions.MidlineLeft);
            var subtotal = Text(table, "SubtotalPoints", "1,520", 560f, 296f, 300f, 44f, 24f, _body, Paper,
                TextAlignmentOptions.MidlineRight);

            var death = Box(table, "DeathPenalty", 0f, 352f, TableWidth, 44f);
            var deathLabel = Text(death, "Label", "死亡ペナルティ", 0f, 0f, 280f, 44f, 24f, _body, Death,
                TextAlignmentOptions.MidlineLeft);
            var deathRate = Text(death, "Rate", "−50%", 250f, 0f, 320f, 44f, 24f, _body, Death,
                TextAlignmentOptions.Center);
            var deathPoints = Text(death, "Points", "−760", 560f, 0f, 300f, 44f, 24f, _body, Death,
                TextAlignmentOptions.MidlineRight);
            death.gameObject.SetActive(false);

            Rule(panel.transform, "TotalLine", Left, 748f, TableWidth);
            Text(panel.transform, "TotalCaption", "合計点数 / 獲得経験値", Left, 768f, TableWidth, 36f, 18f, _body, Muted,
                TextAlignmentOptions.Center, 3f);
            var total = Text(panel.transform, "TotalValue", "1,520", 620f, 812f, 520f, 110f, 92f, _display, Paper,
                TextAlignmentOptions.MidlineRight);
            Text(panel.transform, "ExpLabel", "EXP", 1150f, 868f, 120f, 40f, 22f, _condensed, Muted,
                TextAlignmentOptions.MidlineLeft, 6f);

            var hub = Box(panel.transform, "HubButton", 820f, 968f, 280f, 52f);
            var hit = hub.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var button = hub.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            Image(hub, "Diamond", 28f, 14f, 20f, 20f, Paper, _diamond);
            Text(hub, "Label", "拠点へ  >", 56f, 4f, 196f, 44f, 22f, _body, Paper, TextAlignmentOptions.MidlineLeft, 4f);
            Rule(panel.transform, "ButtonLineLeft", Left, 992f, 270f);
            Rule(panel.transform, "ButtonLineRight", 1120f, 992f, 270f);

            var view = panel.AddComponent<RunResultView>();
            view.subtitle = subtitle;
            view.rows = rowViews;
            view.subtotalPoints = subtotal;
            view.deathPenalty = death.gameObject;
            view.deathLabel = deathLabel;
            view.deathRate = deathRate;
            view.deathPoints = deathPoints;
            view.totalValue = total;
            view.hubButton = button;

            BakeGlyphs(panel);
            PersistFonts(panel);
            BakeGlyphs(panel);
            PrefabUtility.SaveAsPrefabAsset(panel, PrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Run result prefab saved: " + PrefabPath);
        }

        static TMP_FontAsset DisplayFont()
        {
            var source = Resources.Load<Font>("Battle/Fonts/ShipporiMinchoB1-ExtraBold");
            if (source == null)
                return BattleHudKit.SerifMedium;
            var asset = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
                return BattleHudKit.SerifMedium;
            asset.name = "ShipporiMinchoB1-ExtraBold (Runtime)";
            return asset;
        }

        static void BakeGlyphs(GameObject panel)
        {
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == null || string.IsNullOrEmpty(text.text))
                    continue;
                if (text.font.TryAddCharacters(text.text, out string _))
                    EditorUtility.SetDirty(text.font);
            }
        }

        static void PersistFonts(GameObject panel)
        {
            var saved = new Dictionary<int, TMP_FontAsset>();
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                var font = text.font;
                if (font == null || EditorUtility.IsPersistent(font))
                    continue;
                if (!saved.TryGetValue(font.GetInstanceID(), out var asset))
                {
                    var safe = font.name.Replace(" (Runtime)", "").Replace(" ", "");
                    if (string.IsNullOrEmpty(safe))
                        safe = "RunResult";
                    var path = "Assets/_Project/Resources/Battle/Fonts/" + safe + "_SDF.asset";
                    var full = Path.Combine(Directory.GetCurrentDirectory(), path.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full) && new FileInfo(full).Length < 20000)
                        path = "Assets/_Project/Resources/Battle/Fonts/" + safe + "_RunResult_SDF.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(font, path);
                        if (font.material != null && !EditorUtility.IsPersistent(font.material))
                            AssetDatabase.AddObjectToAsset(font.material, path);
                        if (font.atlasTextures != null)
                        {
                            foreach (var tex in font.atlasTextures)
                            {
                                if (tex != null && !EditorUtility.IsPersistent(tex))
                                    AssetDatabase.AddObjectToAsset(tex, path);
                            }
                        }

                        AssetDatabase.SaveAssets();
                        existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    }
                    asset = existing;
                    saved[font.GetInstanceID()] = asset;
                }

                if (asset != null)
                    text.font = asset;
            }
        }

        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Image Image(Transform parent, string name, float x, float y, float w, float h, Color color, Sprite sprite)
        {
            var rt = Box(parent, name, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (image.sprite == null)
                image.sprite = White();
            return image;
        }

        static Sprite White()
        {
            if (_white != null)
                return _white;
            _white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            return _white;
        }

        static Image Rule(Transform parent, string name, float x, float y, float w)
        {
            var image = Image(parent, name, x, y, w, 2f, Line, _fade);
            return image;
        }

        static TextMeshProUGUI Text(
            Transform parent, string name, string value, float x, float y, float w, float h, float size,
            TMP_FontAsset font, Color color, TextAlignmentOptions align, float spacing = 0f)
        {
            var rt = Box(parent, name, x, y, w, h);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = value;
            tmp.font = font != null ? font : BattleHudKit.SerifRegular;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.characterSpacing = spacing;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }
    }
}
#endif
