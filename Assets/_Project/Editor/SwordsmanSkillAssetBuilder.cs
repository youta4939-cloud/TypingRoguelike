#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class SwordsmanSkillAssetBuilder
    {
        const string JsonPath = "Assets/_Project/Resources/Battle/Skills/swordsman_skills_v9.json";
        const string SkillFolder = "Assets/_Project/Data/Prototype/Skills";

        [MenuItem("Tools/TypingRoguelike/剣士スキルをJSONから作る")]
        public static void CreateFromMenu()
        {
            var made = Sync();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("TypingRoguelike", $"剣士スキルを {made.Length} 件更新しました。", "OK");
        }

        public static SkillDefinition[] Sync()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogWarning("剣士スキルの JSON がありません: " + JsonPath);
                return System.Array.Empty<SkillDefinition>();
            }

            var config = SwordsmanSkillConfig.Parse(File.ReadAllText(JsonPath));
            var list = new List<SkillDefinition>();
            if (config.skills == null)
                return System.Array.Empty<SkillDefinition>();

            foreach (var entry in config.skills)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;
                var asset = PrototypeDataGenerator.LoadOrCreatePublic<SkillDefinition>(
                    $"{SkillFolder}/Skill_{entry.skillId}_{entry.nameJa}.asset");
                asset.skillId = entry.skillId;
                asset.characterName = string.IsNullOrEmpty(config.character) ? "アリス" : config.character;
                asset.displayName = entry.nameJa;
                asset.pattern = SwordsmanSkillConfig.ToPattern(entry.pattern);
                asset.logicKey = entry.key;
                asset.rarity = entry.rarity;
                asset.damage = 0;
                asset.damagePercent = 0f;
                asset.heal = 0;
                asset.effectKind = SkillEffectKind.None;
                asset.effectText = SwordsmanSkillConfig.At(entry.text, 1);
                asset.description = asset.effectText;
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }

            return list.ToArray();
        }
    }
}
#endif
