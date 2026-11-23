using System;
using System.Text.RegularExpressions;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    [Serializable]
    public sealed class AxeSkillOptions
    {
        public bool replaceTargetSkills = true;
        public string innateSkillKey = "yoroi_kudaki";
        public bool innateIsProvisional = true;
        public string passiveLabel = "型";
        public bool vulnAppliesToWord = true;
        public bool vulnAppliesToDot = true;
        public bool vulnAppliesToExCards = true;
        public string atkDownStacking = "add";
        public string aslowStacking = "max";
        public string goufuVulnCountsAs = "separate";
        public bool tsuigeExtendsBerserk;
        public bool danzaiReducesMadness = true;
        public string berserkAtkCheck = "continuous";
        public bool berserkAtkAppliesToWord = true;
        public bool berserkAtkAppliesToDanzai;
        public string nextHitWithTatenaoshi = "multiply";
        public bool useOptionalPatternSwap;
    }

    [Serializable]
    public sealed class AxeSwapPatterns
    {
        public string[] kyogeki;
        public string[] tsuige;
        public string[] berserk;
    }

    [Serializable]
    public sealed class AxeOptionalPatternSwap
    {
        public bool enabled;
        public string note;
        public AxeSwapPatterns patterns;
    }

    [Serializable]
    public sealed class AxeSkillEntry
    {
        public int skillId;
        public string key;
        public string nameJa;
        public string rarity;
        public string type;
        public bool oncePerBattle;
        public string[] pattern;
        public float[] damagePercent;
        public float[] vuln;
        public float[] vulnDurationSec;
        public float[] enemyAtkDown;
        public float[] durationSec;
        public float[] atkDownDurationSec;
        public float enemyAtkSpeedDown;
        public float[] aslowDurationSec;
        public float[] debuffBonus;
        public float[] extendDebuffSec;
        public float[] nextHitReduction;
        public int addDenpa;
        public float[] percentPerDebuff;
        public float hpThreshold;
        public float[] hpBonus;
        public float[] madnessDamagePercent;
        public int[] madnessOnKill;
        public float[] atkUp;
        public int atkUpMadnessNeed;
        public string[] text;
    }

    /// <summary>
    /// 斧スキル。割り当て先は JSON の targetCharacterName だけを見る（仮はルナ）。
    /// ルナにした理由：仮ATKが100、最初のスキルが鎧砕きと同じ青紫、エレンを外すと回復が無くなる。
    /// </summary>
    [Serializable]
    public sealed class AxeSkillConfig
    {
        public string version;
        public string targetCharacterName = "ルナ";
        public string targetCharacterNote;
        public AxeSkillOptions options = new AxeSkillOptions();
        public AxeOptionalPatternSwap optionalPatternSwap;
        public AxeSkillEntry[] skills = Array.Empty<AxeSkillEntry>();

        public const string ResourcePath = "Battle/Skills/axe_skills_v2";

        public static AxeSkillConfig Shared { get; set; }

        public static AxeSkillConfig Load()
        {
            if (Shared != null)
                return Shared;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            Shared = Parse(asset != null ? asset.text : null);
            if (asset == null)
                Debug.LogWarning("斧スキルの JSON が読めません: " + ResourcePath);
            return Shared;
        }

        public static AxeSkillConfig Reload()
        {
            Shared = null;
            return Load();
        }

        public static AxeSkillConfig Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new AxeSkillConfig();
            var normalized = Regex.Replace(json, "\"vulnDurationSec\"\\s*:\\s*([0-9.]+)", "\"vulnDurationSec\":[$1]");
            normalized = Regex.Replace(normalized, "\"durationSec\"\\s*:\\s*([0-9.]+)", "\"durationSec\":[$1]");
            var config = JsonUtility.FromJson<AxeSkillConfig>(normalized) ?? new AxeSkillConfig();
            config.options ??= new AxeSkillOptions();
            config.skills ??= Array.Empty<AxeSkillEntry>();
            if (string.IsNullOrEmpty(config.targetCharacterName))
                config.targetCharacterName = "ルナ";
            return config;
        }

        public AxeSkillEntry Find(string key)
        {
            if (skills == null || string.IsNullOrEmpty(key))
                return null;
            foreach (var skill in skills)
            {
                if (skill != null && skill.key == key)
                    return skill;
            }

            return null;
        }

        public AxeSkillEntry FindById(int skillId)
        {
            if (skills == null)
                return null;
            foreach (var skill in skills)
            {
                if (skill != null && skill.skillId == skillId)
                    return skill;
            }

            return null;
        }

        public int InnateSkillId
        {
            get
            {
                var key = options != null && !string.IsNullOrEmpty(options.innateSkillKey)
                    ? options.innateSkillKey
                    : "yoroi_kudaki";
                var entry = Find(key);
                return entry != null ? entry.skillId : 200;
            }
        }

        public string TextOf(string key, int level)
        {
            var entry = Find(key);
            if (entry?.text == null || entry.text.Length == 0)
                return "";
            return At(entry.text, level) ?? "";
        }

        public string[] PatternOf(AxeSkillEntry entry)
        {
            if (entry == null)
                return Array.Empty<string>();
            if (options != null && options.useOptionalPatternSwap && optionalPatternSwap?.patterns != null)
            {
                string[] swapped = null;
                switch (entry.key)
                {
                    case "kyogeki": swapped = optionalPatternSwap.patterns.kyogeki; break;
                    case "tsuige": swapped = optionalPatternSwap.patterns.tsuige; break;
                    case "berserk": swapped = optionalPatternSwap.patterns.berserk; break;
                }

                if (swapped != null && swapped.Length > 0)
                    return swapped;
            }

            return entry.pattern ?? Array.Empty<string>();
        }

        public bool HidesReward(SkillDefinition card) =>
            card != null && HidesReward(card.skillId, card.characterName);

        public bool HidesReward(int skillId, string characterName)
        {
            var replace = options == null || options.replaceTargetSkills;
            var axe = skillId >= 200 && skillId <= 211;
            if (!replace)
                return axe;
            if (axe)
                return false;
            return !string.IsNullOrEmpty(targetCharacterName) &&
                   StarterCharacters.Same(characterName, targetCharacterName);
        }

        public string RewardOwner(string characterName, int skillId)
        {
            if (skillId >= 200 && skillId <= 211 && !string.IsNullOrEmpty(targetCharacterName))
                return targetCharacterName;
            return characterName;
        }

        public static int At(int[] values, int level)
        {
            if (values == null || values.Length == 0)
                return 0;
            var index = Math.Max(0, Math.Min(values.Length - 1, level - 1));
            return values[index];
        }

        public static float At(float[] values, int level)
        {
            if (values == null || values.Length == 0)
                return 0f;
            var index = Math.Max(0, Math.Min(values.Length - 1, level - 1));
            return values[index];
        }

        public static string At(string[] values, int level)
        {
            if (values == null || values.Length == 0)
                return "";
            var index = Math.Max(0, Math.Min(values.Length - 1, level - 1));
            return values[index] ?? "";
        }
    }
}
