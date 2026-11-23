using System;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    [Serializable]
    public sealed class SwordsmanOptions
    {
        public string chainScope = "self";
        public bool chainCountsExCards;
        public string onagiFocusConsume = "threshold";
        public string focusGainMode = "battleTotal";
        public bool focusGainCountsOverflow = true;
        public string kobuStacking = "refresh";
        public bool tateRerollIgnoresCap = true;
        public bool tateStacks;
        public bool tateAppliesToDot;
        public bool tsuiFiresWithZeroFocus = true;
        public string passiveLabel = "パッシブ";
        public bool replaceAliceSkills = true;
        public int aliceInnateSkillId = 100;
        public int focusCarryDecay = 5;
        public int focusCarryCap = 15;
        public bool kyokuRequiresCast = true;
        public int kyokuNeedsHeld;
    }

    [Serializable]
    public sealed class SwordsmanSkillEntry
    {
        public int skillId;
        public string key;
        public string nameJa;
        public string rarity;
        public string type;
        public bool oncePerBattle;
        public string[] pattern;
        public float[] damagePercent;
        public float[] nextHitReduction;
        public int[] reroll;
        public float[] finalDamageBonus;
        public float durationSec;
        public string target;
        public int[] focusAdd;
        public int[] focusThreshold;
        public float multiplierWhenConsumed;
        public string addCard;
        public int[] addCount;
        public int[] allySkillBattou;
        public string chainPrev;
        public int[] chainFocusAdd;
        public int focusGainRequired;
        public int[] bloodOnKill;
        public float[] percentPerFocus;
        public bool consumeAllFocus;
        public bool forceCrit;
        public int kiwamiFocusGainRequired;
        public float kiwamiCritMultiplier;
        public string kiwamiAddCard;
        public int kiwamiAddCount;
        public string[] text;
    }

    [Serializable]
    public sealed class SwordsmanSkillConfig
    {
        public string version;
        public string character;
        public SwordsmanOptions options = new SwordsmanOptions();
        public SwordsmanSkillEntry[] skills = Array.Empty<SwordsmanSkillEntry>();

        public const string ResourcePath = "Battle/Skills/swordsman_skills_v9";

        public static SwordsmanSkillConfig Shared { get; set; }

        public static SwordsmanSkillConfig Load()
        {
            if (Shared != null)
                return Shared;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            Shared = Parse(asset != null ? asset.text : null);
            if (asset == null)
                Debug.LogWarning("剣士スキルの JSON が読めません: " + ResourcePath);
            return Shared;
        }

        public static SwordsmanSkillConfig Reload()
        {
            Shared = null;
            return Load();
        }

        public static SwordsmanSkillConfig Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new SwordsmanSkillConfig();
            var config = JsonUtility.FromJson<SwordsmanSkillConfig>(json) ?? new SwordsmanSkillConfig();
            config.options ??= new SwordsmanOptions();
            config.skills ??= Array.Empty<SwordsmanSkillEntry>();
            return config;
        }

        public SwordsmanSkillEntry Find(string key)
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

        public SwordsmanSkillEntry FindById(int skillId)
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

        public string TextOf(string key, int level)
        {
            var entry = Find(key);
            if (entry?.text == null || entry.text.Length == 0)
                return "";
            return At(entry.text, level) ?? "";
        }

        public bool HidesReward(int skillId)
        {
            var replace = options == null || options.replaceAliceSkills;
            if (replace)
            {
                switch (skillId)
                {
                    case 1:
                    case 2:
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                    case 13:
                    case 23:
                    case 26:
                    case 27:
                    case 28:
                        return true;
                }
                return false;
            }

            return skillId >= 100 && skillId <= 111;
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

        public static ElementColor[] ToPattern(string[] names)
        {
            if (names == null || names.Length == 0)
                return Array.Empty<ElementColor>();
            var pattern = new ElementColor[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                switch (names[i])
                {
                    case "blue": pattern[i] = ElementColor.Blue; break;
                    case "green": pattern[i] = ElementColor.Green; break;
                    case "yellow": pattern[i] = ElementColor.Yellow; break;
                    case "purple": pattern[i] = ElementColor.Purple; break;
                    default: pattern[i] = ElementColor.Red; break;
                }
            }
            return pattern;
        }
    }
}
