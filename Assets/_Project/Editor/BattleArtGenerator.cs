#if UNITY_EDITOR
using System.IO;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    /// <summary>戦闘用プレースホルダー立ち絵を生成し、キャラ / ボス SO に紐付け。</summary>
    public static class BattleArtGenerator
    {
        const string ArtRoot = "Assets/_Project/Resources/Battle/Art";
        const string PortraitDir = ArtRoot + "/Portraits";
        const string CharRoot = "Assets/_Project/Data/Prototype/Characters";
        const string EnemyPath = "Assets/_Project/Data/Prototype/Enemy_PrototypeBoss.asset";

        [MenuItem("Tools/TypingRoguelike/Generate Battle Art (Portraits)")]
        public static void GenerateFromMenu()
        {
            Generate();
            EditorUtility.DisplayDialog(
                "TypingRoguelike",
                "戦闘用立ち絵プレースホルダーを生成しました。\n" +
                "本番アートは PNG を同フォルダに差し替え、または Character / Enemy の Sprite を指定してください。",
                "OK");
        }

        public static void Generate()
        {
            EnsureFolder(PortraitDir);

            var alicePath = $"{PortraitDir}/Alice.png";
            var ellenPath = $"{PortraitDir}/Ellen.png";
            var lunaPath = $"{PortraitDir}/Luna.png";
            var bossPath = $"{ArtRoot}/Boss_Dragon.png";

            // 差し替え済みの本番アートをプロトタイプ再生成で上書きしない。
            if (!File.Exists(alicePath))
                SavePortrait(alicePath, 512, 640, new Color(0.75f, 0.15f, 0.2f), new Color(0.15f, 0.05f, 0.08f), "ALICE");
            if (!File.Exists(ellenPath))
                SavePortrait(ellenPath, 512, 640, new Color(0.1f, 0.65f, 0.45f), new Color(0.05f, 0.12f, 0.1f), "ELLEN");
            if (!File.Exists(lunaPath))
                SavePortrait(lunaPath, 512, 640, new Color(0.35f, 0.35f, 0.95f), new Color(0.08f, 0.06f, 0.18f), "LUNA");
            if (!File.Exists(bossPath))
                SaveBoss(bossPath, 1024, 576);

            AssetDatabase.Refresh();
            SetSpriteImport(alicePath);
            SetSpriteImport(ellenPath);
            SetSpriteImport(lunaPath);
            SetSpriteImport(bossPath);

            AssignPortrait($"{CharRoot}/Char_Alice.asset", LoadSprite(alicePath));
            AssignPortrait($"{CharRoot}/Char_Ellen.asset", LoadSprite(ellenPath));
            AssignPortrait($"{CharRoot}/Char_Luna.asset", LoadSprite(lunaPath));

            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyPath);
            if (enemy != null)
            {
                enemy.battleIllustration = LoadSprite(bossPath);
                EditorUtility.SetDirty(enemy);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[TypingRoguelike] Battle art generated under Resources/Battle/Art/");
        }

        static void SavePortrait(string path, int w, int h, Color top, Color bottom, string label)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var cx = w * 0.5f;
            var cy = h * 0.42f;
            for (var y = 0; y < h; y++)
            {
                var t = y / (float)(h - 1);
                var bg = Color.Lerp(bottom, top, t);
                for (var x = 0; x < w; x++)
                {
                    var dx = (x - cx) / (w * 0.32f);
                    var dy = (y - cy) / (h * 0.38f);
                    var bust = dx * dx + dy * dy < 1f ? 0.22f : 0f;
                    var vig = Mathf.Clamp01(1.2f - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy * 0.9f) * (y - cy * 0.9f)) / (w * 0.55f));
                    var c = Color.Lerp(bg, Color.white, bust) * vig;
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }

            DrawLabel(tex, label, w / 2, (int)(h * 0.12f), Color.white);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void SaveBoss(string path, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var t = y / (float)(h - 1);
                    var baseC = Color.Lerp(new Color(0.12f, 0.02f, 0.04f), new Color(0.45f, 0.08f, 0.06f), t);
                    var eye = Mathf.Exp(-Vector2.SqrMagnitude(new Vector2(x - w * 0.38f, y - h * 0.55f) / (w * 0.08f)))
                              + Mathf.Exp(-Vector2.SqrMagnitude(new Vector2(x - w * 0.62f, y - h * 0.55f) / (w * 0.08f)));
                    var c = Color.Lerp(baseC, new Color(1f, 0.35f, 0.2f), eye * 0.85f);
                    tex.SetPixel(x, y, c);
                }
            }

            DrawLabel(tex, "BOSS", w / 2, (int)(h * 0.08f), new Color(1f, 0.5f, 0.45f));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void DrawLabel(Texture2D tex, string label, int cx, int cy, Color color)
        {
            var size = 28;
            for (var i = 0; i < label.Length; i++)
            {
                var ox = cx - label.Length * size / 4 + i * size / 2;
                DrawBlock(tex, ox, cy, size / 2, size, color * 0.85f);
            }
        }

        static void DrawBlock(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            for (var py = y; py < y + h && py < tex.height; py++)
            {
                for (var px = x; px < x + w && px < tex.width; px++)
                {
                    if (px < 0 || py < 0)
                        continue;
                    tex.SetPixel(px, py, color);
                }
            }
        }

        static void SetSpriteImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void AssignPortrait(string charAssetPath, Sprite sprite)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(charAssetPath);
            if (character == null)
                return;
            character.battlePortrait = sprite;
            EditorUtility.SetDirty(character);
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                var name = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent!, name);
            }
        }
    }
}
#endif
