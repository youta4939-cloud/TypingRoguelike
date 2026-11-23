using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class SkeletonSamuraiSetup
    {
        const string SpriteFolder = "Assets/_Project/Resources/Battle/Art/Enemies/SkeletonSamurai";

        static readonly string[] Names =
        {
            "01_shadow", "02_legs", "03_kimono_lining", "04_kimono", "05_torso",
            "06_scabbards", "07_sash_strip_L", "08_sash_rope", "09_sash_strip_R",
            "10_arm_R", "11_arm_L_katana", "12_skull", "13_hat",
            "14_hat_talisman_L", "15_hat_chain_L_outer", "16_hat_chain_L_inner",
            "17_hat_talisman_R", "18_hat_chain_R", "sway_mask_kimono"
        };

        [MenuItem("Tools/TypingRoguelike/Setup Skeleton Samurai Enemy")]
        public static void SetupFromMenu()
        {
            var pivot = new Vector2(
                515f / 1024f,
                1f - 1440f / 1536f);
            foreach (var name in Names)
            {
                var path = $"{SpriteFolder}/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"骸骨の侍の画像がありません: {path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            Debug.Log(TypingRoguelike.Battle.SkeletonSamuraiAssembler.SpritesReady()
                ? "骸骨の侍のスプライトを読み込めました。"
                : "画像が足りません。Resources/Battle/Art/Enemies/SkeletonSamurai を確認してください。");
        }
    }
}
