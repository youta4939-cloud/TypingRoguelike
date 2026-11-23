using TypingRoguelike.Battle;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class PaperAngelSetup
    {
        const string SpriteFolder = "Assets/_Project/Resources/Battle/Art/Enemies/PaperAngel";

        [MenuItem("Tools/TypingRoguelike/Setup Paper Angel Enemy")]
        public static void SetupFromMenu()
        {
            ApplyPivots();
            Debug.Log(PaperAngelAssembler.SpritesReady()
                ? "紙の天使のスプライトを読み込めました。"
                : "画像が足りません。Resources/Battle/Art/Enemies/PaperAngel を確認してください。");
        }

        public static void ApplyPivots()
        {
            foreach (var spec in PaperAngelParts.All)
            {
                var path = $"{SpriteFolder}/{spec.Name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"紙の天使の画像がありません: {path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PaperAngelParts.Ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = PaperAngelParts.NormalizedPivot(spec.PivotX, spec.PivotY);
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
