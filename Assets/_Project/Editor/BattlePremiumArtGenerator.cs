#if UNITY_EDITOR
using System.IO;
using TypingRoguelike.Battle;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    /// <summary>戦闘アリーナ用の高解像度 UI スプライトを Resources に出力。</summary>
    public static class BattlePremiumArtGenerator
    {
        const string UiRoot = "Assets/_Project/Resources/Battle/UI";

        [MenuItem("Tools/TypingRoguelike/Generate Premium Battle Arena Art")]
        public static void GenerateFromMenu()
        {
            Generate();
            BattleUISprites.ResetCache();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "TypingRoguelike",
                "Premium 戦闘 UI（単語カード枠・アリーナ床）を生成しました。\n" +
                "Play を止めてから再 Play すると反映されます。",
                "OK");
        }

        public static void Generate()
        {
            Directory.CreateDirectory(UiRoot);
            SaveWordCardPremium($"{UiRoot}/Premium_Word_Card.png");
            SaveArenaFloor($"{UiRoot}/Premium_Arena_Floor.png");
        }

        static void SaveWordCardPremium(string path)
        {
            const int w = 512;
            const int h = 192;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var bg = new Color(0.04f, 0.05f, 0.11f, 0.96f);
            var borderGold = new Color(0.82f, 0.62f, 0.22f, 1f);
            var borderCyan = new Color(0.22f, 0.72f, 0.95f, 0.85f);
            var inner = new Color(0.07f, 0.09f, 0.18f, 0.92f);

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    Color c;
                    if (edge <= 2)
                        c = borderGold;
                    else if (edge <= 5)
                        c = borderCyan;
                    else if (edge <= 10)
                        c = bg;
                    else
                    {
                        var t = y / (float)(h - 1);
                        c = Color.Lerp(inner, inner * 1.15f, t * 0.35f);
                        c.a = inner.a;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            // 上部ハイライト
            for (var x = 12; x < w - 12; x++)
            {
                for (var y = h - 14; y < h - 8; y++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.06f));
            }

            WriteSprite(path, tex, 100f, new Vector4(48, 48, 48, 48));
        }

        static void SaveArenaFloor(string path)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / (center * 0.55f);
                    var r = dx * dx + dy * dy;
                    if (r > 1f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var edge = Mathf.SmoothStep(1f, 0.72f, r);
                    var c = Color.Lerp(new Color(0.05f, 0.1f, 0.22f, 0.85f), new Color(0.12f, 0.35f, 0.65f, 0.35f), edge);
                    tex.SetPixel(x, y, c);
                }
            }

            WriteSprite(path, tex, 100f, Vector4.zero);
        }

        static void WriteSprite(string path, Texture2D tex, float ppu, Vector4 border)
        {
            tex.Apply(false, false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            if (border != Vector4.zero)
                importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }
}
#endif
