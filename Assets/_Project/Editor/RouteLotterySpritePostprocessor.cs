using System;
using System.Collections.Generic;
using System.IO;
using TypingRoguelike.Battle;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public sealed class RouteLotterySpritePostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/_Project/Resources/RouteLottery/";

        void OnPreprocessTexture()
        {
            if (assetPath.IndexOf("/RouteLottery/", StringComparison.Ordinal) < 0)
                return;
            var importer = (TextureImporter)assetImporter;
            var file = Path.GetFileName(assetPath);
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (file == "card_reveal_map.png")
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            importer.spritePixelsPerUnit = file.IndexOf("_2x", StringComparison.Ordinal) >= 0 ? 200f : 100f;
            importer.filterMode = FilterMode.Bilinear;
            var border = BorderFor(assetPath);
            if (border.HasValue)
                importer.spriteBorder = border.Value;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            if (border.HasValue)
                settings.spriteBorder = border.Value;
            importer.SetTextureSettings(settings);
        }

        static Vector4? BorderFor(string assetPath)
        {
            var marker = "/Sprites/";
            var index = assetPath.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return null;
            var key = assetPath.Substring(index + marker.Length).Replace('\\', '/');
            var map = Borders();
            if (!map.TryGetValue(key, out var border))
                return null;
            return border;
        }

        static Dictionary<string, Vector4> _borders;

        static Dictionary<string, Vector4> Borders()
        {
            if (_borders != null)
                return _borders;
            _borders = new Dictionary<string, Vector4>();
            var path = Path.Combine(Directory.GetCurrentDirectory(), Root + "Data/sprites_meta.json");
            if (!File.Exists(path))
                return _borders;
            var root = JNode.Parse(File.ReadAllText(path));
            var sprites = root.Get("sprites");
            if (sprites?.Map == null)
                return _borders;
            foreach (var pair in sprites.Map)
            {
                var border = pair.Value.Get("border_LBRT");
                if (border == null || border.Count < 4)
                    continue;
                _borders[pair.Key] = new Vector4(
                    (float)border.At(0).Number,
                    (float)border.At(1).Number,
                    (float)border.At(2).Number,
                    (float)border.At(3).Number);
            }
            return _borders;
        }
    }

    [InitializeOnLoad]
    static class RouteLotteryImportKick
    {
        const string Iron = "Assets/_Project/Resources/RouteLottery/Sprites/Common/iron_panel_9s_2x.png";

        static RouteLotteryImportKick()
        {
            EditorApplication.delayCall += Ensure;
        }

        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!File.Exists(Iron))
                return;
            var importer = AssetImporter.GetAtPath(Iron) as TextureImporter;
            if (importer != null && importer.spritePixelsPerUnit > 150f && importer.spriteBorder.x > 1f)
                return;
            AssetDatabase.ImportAsset("Assets/_Project/Resources/RouteLottery", ImportAssetOptions.ImportRecursive);
            Debug.Log("[RouteLottery] スプライトの PPU と 9スライスを設定しました。");
        }

        [MenuItem("Tools/TypingRoguelike/Route Lottery Probability Check")]
        public static void Check()
        {
            var dir = Path.GetFullPath("Assets/_Project/Resources/RouteLottery/Data");
            var error = TypingRoguelike.RouteLottery.RouteLotterySelfTest.Run(dir);
            if (string.IsNullOrEmpty(error))
                Debug.Log("[RouteLottery] 確率テスト OK");
            else
                Debug.LogError("[RouteLottery]\n" + error);
        }
    }
}
