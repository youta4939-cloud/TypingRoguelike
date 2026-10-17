using System.IO;
using TypingRoguelike.Battle;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class BigMouthSetup
    {
        const string SpriteFolder = "Assets/_Project/Resources/Battle/Art/Enemies/BigMouth";
        const string PrefabPath = "Assets/_Project/Resources/Battle/Enemies/BigMouth.prefab";

        [MenuItem("Tools/TypingRoguelike/Setup Big Mouth Enemy")]
        public static void SetupFromMenu()
        {
            ApplyPivots();
            BuildPrefab();
        }

        public static void ApplyPivots()
        {
            foreach (var spec in BigMouthParts.All)
            {
                var path = $"{SpriteFolder}/{spec.Name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"大口の敵の画像がありません: {path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = BigMouthParts.Ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = BigMouthParts.NormalizedPivot(spec.PivotX, spec.PivotY);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        public static void BuildPrefab()
        {
            Directory.CreateDirectory("Assets/_Project/Resources/Battle/Enemies");
            var idle = BigMouthAssembler.CreateWorld(null);
            if (idle == null)
                return;

            PrefabUtility.SaveAsPrefabAsset(idle.gameObject, PrefabPath);
            Object.DestroyImmediate(idle.gameObject);
            Debug.Log($"大口の敵プレハブを更新しました: {PrefabPath}");
        }
    }
}
