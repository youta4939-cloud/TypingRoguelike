#if UNITY_EDITOR
using System.IO;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    /// <summary>RPG風 9-slice UI テンプレート PNG と DefaultBattleUITemplate.asset を生成。</summary>
    public static class BattleUITemplateGenerator
    {
        const string UiRoot = "Assets/_Project/Resources/Battle/UI";
        const string TemplateAssetPath = UiRoot + "/DefaultBattleUITemplate.asset";
        const int Border = 24;
        const int CommercialBorder = 104;
        const string CommercialWindowPath = UiRoot + "/Commercial_Window_Frame.png";
        const string CommercialWordPath = UiRoot + "/Commercial_Word_Frame.png";
        const string CommercialButtonPath = UiRoot + "/Commercial_Button_Frame.png";

        [MenuItem("Tools/TypingRoguelike/Generate Battle UI Template")]
        public static void GenerateFromMenu()
        {
            Generate();
            EditorUtility.DisplayDialog(
                "TypingRoguelike",
                "戦闘 UI テンプレート（9-slice）を生成しました。\n" +
                $"{UiRoot} の PNG を差し替えてから同メニューを再実行すると反映されます。",
                "OK");
        }

        public static void Generate()
        {
            EnsureFolder(UiRoot);

            var windowPath = $"{UiRoot}/window_panel.png";
            var partyPath = $"{UiRoot}/party_card.png";
            var portraitPath = $"{UiRoot}/portrait_frame.png";
            var wordPath = $"{UiRoot}/word_box.png";
            var bossPath = $"{UiRoot}/boss_frame.png";
            var buttonPath = $"{UiRoot}/button.png";
            var barTrackPath = $"{UiRoot}/bar_track.png";
            var barFillPath = $"{UiRoot}/bar_fill.png";

            SaveFrame(windowPath, 128, 128, new Color(0.12f, 0.05f, 0.08f), new Color(0.85f, 0.55f, 0.12f));
            SaveFrame(partyPath, 128, 128, new Color(0.14f, 0.06f, 0.1f), new Color(0.75f, 0.2f, 0.22f));
            SaveFrame(portraitPath, 128, 160, new Color(0.08f, 0.04f, 0.06f), new Color(0.9f, 0.7f, 0.25f));
            SaveFrame(wordPath, 128, 64, new Color(0.06f, 0.03f, 0.05f), new Color(0.35f, 0.45f, 0.9f));
            SaveFrame(bossPath, 256, 160, new Color(0.1f, 0.02f, 0.04f), new Color(0.95f, 0.25f, 0.2f));
            SaveFrame(buttonPath, 128, 48, new Color(0.18f, 0.08f, 0.12f), new Color(0.8f, 0.5f, 0.15f));
            SaveBar(barTrackPath, 128, 24, new Color(0.08f, 0.04f, 0.06f));
            SaveBar(barFillPath, 128, 24, new Color(0.06f, 0.65f, 0.45f));

            AssetDatabase.Refresh();
            foreach (var path in new[] { windowPath, partyPath, portraitPath, wordPath, bossPath, buttonPath, barTrackPath, barFillPath })
                ConfigureSlicedSprite(path, Border);
            foreach (var path in new[] { CommercialWindowPath, CommercialWordPath, CommercialButtonPath })
            {
                if (File.Exists(path))
                    ConfigureSlicedSprite(path, CommercialBorder);
            }

            var template = AssetDatabase.LoadAssetAtPath<BattleUITemplateDefinition>(TemplateAssetPath);
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<BattleUITemplateDefinition>();
                AssetDatabase.CreateAsset(template, TemplateAssetPath);
            }

            template.windowPanel = LoadSprite(CommercialWindowPath) ?? LoadSprite(windowPath);
            template.partyCard = LoadSprite(partyPath);
            template.portraitFrame = LoadSprite(portraitPath);
            template.wordBox = LoadSprite(CommercialWordPath) ?? LoadSprite(wordPath);
            template.bossFrame = LoadSprite(bossPath);
            template.button = LoadSprite(CommercialButtonPath) ?? LoadSprite(buttonPath);
            template.barTrack = LoadSprite(barTrackPath);
            template.barFill = LoadSprite(barFillPath);
            template.panelBorder = Border;
            EditorUtility.SetDirty(template);

            AssetDatabase.SaveAssets();
            BattleUITemplateLoader.ClearCache();
            Debug.Log("[TypingRoguelike] Battle UI template generated.");
        }

        static void SaveFrame(string path, int w, int h, Color fill, Color borderColor)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var edge = x < Border || y < Border || x >= w - Border || y >= h - Border;
                    var corner = (x < Border && y < Border) || (x >= w - Border && y < Border)
                                 || (x < Border && y >= h - Border) || (x >= w - Border && y >= h - Border);
                    var c = fill;
                    if (edge)
                        c = Color.Lerp(borderColor, fill, corner ? 0.35f : 0.55f);
                    var inner = new Vector2(x - w * 0.5f, y - h * 0.5f);
                    if (inner.magnitude < w * 0.08f)
                        c = Color.Lerp(c, Color.white, 0.04f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void SaveBar(string path, int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var v = y / (float)(h - 1);
                tex.SetPixel(x, y, Color.Lerp(color * 0.7f, color, v));
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void ConfigureSlicedSprite(string assetPath, int border)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent!, name);
        }
    }

    [InitializeOnLoad]
    static class CommercialUiFrameAutoImporter
    {
        static CommercialUiFrameAutoImporter()
        {
            EditorApplication.delayCall += () =>
            {
                const string commercialWindow = "Assets/_Project/Resources/Battle/UI/Commercial_Window_Frame.png";
                const string templatePath = "Assets/_Project/Resources/Battle/UI/DefaultBattleUITemplate.asset";
                if (!File.Exists(commercialWindow))
                    return;

                var template = AssetDatabase.LoadAssetAtPath<BattleUITemplateDefinition>(templatePath);
                var assignedPath = template?.windowPanel != null
                    ? AssetDatabase.GetAssetPath(template.windowPanel)
                    : string.Empty;
                var importer = AssetImporter.GetAtPath(commercialWindow) as TextureImporter;
                var needsImport = importer == null
                                  || importer.textureType != TextureImporterType.Sprite
                                  || importer.spriteBorder.x < 80f;
                if (needsImport || assignedPath != commercialWindow)
                    BattleUITemplateGenerator.Generate();
            };
        }
    }
}
#endif
