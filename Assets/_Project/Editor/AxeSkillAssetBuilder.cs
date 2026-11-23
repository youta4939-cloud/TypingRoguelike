#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class AxeSkillAssetBuilder
    {
        const string JsonPath = "Assets/_Project/Resources/Battle/Skills/axe_skills_v2.json";
        const string SkillFolder = "Assets/_Project/Data/Prototype/Skills";

        [MenuItem("Tools/TypingRoguelike/斧スキルをJSONから作る")]
        public static void CreateFromMenu()
        {
            var made = Sync();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("TypingRoguelike", $"斧スキルを {made.Length} 件更新しました。", "OK");
        }

        public static SkillDefinition[] Sync()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogWarning("斧スキルの JSON がありません: " + JsonPath);
                return System.Array.Empty<SkillDefinition>();
            }

            var config = AxeSkillConfig.Parse(File.ReadAllText(JsonPath));
            var list = new List<SkillDefinition>();
            if (config.skills == null)
                return System.Array.Empty<SkillDefinition>();
            var owner = string.IsNullOrEmpty(config.targetCharacterName) ? "ルナ" : config.targetCharacterName;
            foreach (var entry in config.skills)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;
                var asset = PrototypeDataGenerator.LoadOrCreatePublic<SkillDefinition>(
                    $"{SkillFolder}/Skill_{entry.skillId}_{entry.nameJa}.asset");
                asset.skillId = entry.skillId;
                asset.characterName = owner;
                asset.displayName = entry.nameJa;
                asset.pattern = SwordsmanSkillConfig.ToPattern(config.PatternOf(entry));
                asset.logicKey = entry.key;
                asset.rarity = entry.rarity;
                asset.damage = 0;
                asset.damagePercent = 0f;
                asset.heal = 0;
                asset.effectKind = SkillEffectKind.None;
                asset.effectText = AxeSkillConfig.At(entry.text, 1);
                asset.description = asset.effectText;
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }

            return list.ToArray();
        }
    }
}
#endif
