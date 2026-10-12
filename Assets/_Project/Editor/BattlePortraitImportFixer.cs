#if UNITY_EDITOR
using System.IO;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class BattlePortraitImportFixer
    {
        const string PortraitDir = "Assets/_Project/Resources/Battle/Art/Portraits";
        const string BossPath = "Assets/_Project/Resources/Battle/Art/Boss_Dragon.png";
        const string BattleBackgroundPath = "Assets/_Project/Resources/Battle/Art/Backgrounds/Battle_Background.png";
        const string DungeonBackgroundPath = "Assets/_Project/Resources/Battle/Art/Backgrounds/Dungeon_Map.png";
        const string CardBackPath = "Assets/_Project/Resources/Battle/UI/Arcane_Card_Back.png";
        const string CardFramePath = "Assets/_Project/Resources/Battle/UI/Commercial_Card_Frame.png";
        const string CharRoot = "Assets/_Project/Data/Prototype/Characters";

        [MenuItem("Tools/TypingRoguelike/Fix Portrait Import & Reconnect")]
        public static void FixFromMenu()
        {
            Fix();
            EditorUtility.DisplayDialog(
                "TypingRoguelike",
                "立ち絵の Import 設定を Single Sprite に直し、\n" +
                "Character / Boss データへ再リンクしました。\n" +
                "Play を止めてから再度 Play してください。",
                "OK");
        }

        public static void Fix()
        {
            if (Directory.Exists(PortraitDir))
            {
                foreach (var file in Directory.GetFiles(PortraitDir, "*.png"))
                    FixTextureImport(ToAssetPath(file));
            }

            if (File.Exists(BossPath))
                FixTextureImport(BossPath);
            if (File.Exists(BattleBackgroundPath))
                FixTextureImport(BattleBackgroundPath);
            if (File.Exists(DungeonBackgroundPath))
                FixTextureImport(DungeonBackgroundPath);
            if (File.Exists(CardBackPath))
                FixTextureImport(CardBackPath);
            if (File.Exists(CardFramePath))
                FixTextureImport(CardFramePath);

            AssetDatabase.Refresh();

            AssignPortrait($"{CharRoot}/Char_Alice.asset", "Alice");
            AssignPortrait($"{CharRoot}/Char_Ellen.asset", "Ellen");
            AssignPortrait($"{CharRoot}/Char_Luna.asset", "Luna");

            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/_Project/Data/Prototype/Enemy_PrototypeBoss.asset");
            if (enemy != null)
            {
                enemy.battleIllustration = LoadFirstSprite(BossPath);
                EditorUtility.SetDirty(enemy);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[TypingRoguelike] Portrait import fixed and ScriptableObjects reconnected.");
        }

        static void FixTextureImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void AssignPortrait(string charAssetPath, string resourceName)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(charAssetPath);
            if (character == null)
                return;

            var pngPath = $"{PortraitDir}/{resourceName}.png";
            character.battlePortrait = LoadFirstSprite(pngPath);
            EditorUtility.SetDirty(character);
        }

        static Sprite LoadFirstSprite(string assetPath)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in sprites)
            {
                if (obj is Sprite sprite)
                    return sprite;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static string ToAssetPath(string fullPath)
        {
            fullPath = fullPath.Replace('\\', '/');
            var idx = fullPath.IndexOf("Assets/", System.StringComparison.Ordinal);
            return idx >= 0 ? fullPath[idx..] : fullPath;
        }
    }

    [InitializeOnLoad]
    static class GeneratedBattleArtAutoImporter
    {
        static GeneratedBattleArtAutoImporter()
        {
            EditorApplication.delayCall += () =>
            {
                const string cardPath = "Assets/_Project/Resources/Battle/UI/Arcane_Card_Back.png";
                const string cardFramePath = "Assets/_Project/Resources/Battle/UI/Commercial_Card_Frame.png";
                const string mapPath = "Assets/_Project/Resources/Battle/Art/Backgrounds/Dungeon_Map.png";
                var cardImporter = AssetImporter.GetAtPath(cardPath) as TextureImporter;
                var cardFrameImporter = AssetImporter.GetAtPath(cardFramePath) as TextureImporter;
                var mapImporter = AssetImporter.GetAtPath(mapPath) as TextureImporter;
                if ((cardImporter != null && cardImporter.textureType != TextureImporterType.Sprite)
                    || (cardFrameImporter != null && cardFrameImporter.textureType != TextureImporterType.Sprite)
                    || (mapImporter != null && mapImporter.textureType != TextureImporterType.Sprite))
                {
                    BattlePortraitImportFixer.Fix();
                }
            };
        }
    }
}
#endif
