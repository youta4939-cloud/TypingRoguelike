using System;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    [Serializable]
    public sealed class ExNamedEffect
    {
        public string id;
        public string name;
        public float intervalSec;
        public int damagePerStack;
        public int madnessPerBurst;
        public int maxStacks;
        public float critChancePerStack;
    }

    [Serializable]
    public sealed class ExColorEntry
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class ExEnemySpecials
    {
        public bool enabled;
        public float normalIntervalSec;
        public float eliteIntervalSec;
        public float bossIntervalSec;
        public float firstDelayMinSec;
        public float firstDelayMaxSec;
        public float jitter;
        public string[] kinds;
    }

    [Serializable]
    public sealed class ExSpecialRules
    {
        public bool cardDamageFirst = true;
        public bool blockedByParalysis = true;
        public bool countsAsSkillDamage = true;
        public string wordMode = "fixed";
    }

    [Serializable]
    public sealed class ExCardDefinition
    {
        public string id;
        public string nameJa;
        public string kind;
        public string color;
        public string word;
        public bool stacks;
        public bool pushable;
        public float damagePercent;
        public float percentPerCount;
        public float percentPerBlood;
        public bool forceCrit;
        public string consume;
        public int bloodCap;
        public string wordMode;
    }

    [Serializable]
    public sealed class ExAreaConfig
    {
        public int slots = 5;
        public bool exWordCountsAsNormalWord = true;
        public ExNamedEffect colorStock = new ExNamedEffect();
        public ExNamedEffect poison = new ExNamedEffect();
        public ExNamedEffect paralysis = new ExNamedEffect();
        public ExNamedEffect frenzy = new ExNamedEffect();
        public ExNamedEffect focus = new ExNamedEffect();
        public ExColorEntry[] colors = Array.Empty<ExColorEntry>();
        public ExEnemySpecials enemySpecials = new ExEnemySpecials();
        public string version = "";
        public ExSpecialRules specialRules = new ExSpecialRules();
        public ExCardDefinition[] effects = Array.Empty<ExCardDefinition>();

        public const string ResourcePath = "Battle/ExArea/ex_area_v3";

        public static ExAreaConfig Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogWarning("Exエリアの JSON が読めません: " + ResourcePath);
                return new ExAreaConfig();
            }

            var config = JsonUtility.FromJson<ExAreaConfig>(asset.text);
            if (config == null)
            {
                Debug.LogWarning("Exエリアの JSON を解釈できません: " + ResourcePath);
                return new ExAreaConfig();
            }

            config.colorStock ??= new ExNamedEffect();
            config.poison ??= new ExNamedEffect();
            config.paralysis ??= new ExNamedEffect();
            config.frenzy ??= new ExNamedEffect();
            config.focus ??= new ExNamedEffect();
            config.colors ??= Array.Empty<ExColorEntry>();
            config.enemySpecials ??= new ExEnemySpecials();
            config.specialRules ??= new ExSpecialRules();
            config.effects ??= Array.Empty<ExCardDefinition>();
            return config;
        }

        public ExCardDefinition FindCard(string id)
        {
            if (effects == null || string.IsNullOrEmpty(id))
                return null;
            foreach (var card in effects)
            {
                if (card != null && card.id == id)
                    return card;
            }
            return null;
        }

        public string CardWordMode(ExCardDefinition card)
        {
            if (card != null && !string.IsNullOrEmpty(card.wordMode))
                return card.wordMode;
            return specialRules != null && !string.IsNullOrEmpty(specialRules.wordMode)
                ? specialRules.wordMode
                : "fixed";
        }

        public string NameOf(ExKind kind)
        {
            switch (kind)
            {
                case ExKind.Battou: return CardName("battou", "抜刀");
                case ExKind.IssenKiwami: return CardName("issen_kiwami", "一閃・極");
                case ExKind.HyakkiYakou: return CardName("hyakki_yakou", "百鬼夜行");
            }

            var effect = Effect(kind);
            if (effect != null && !string.IsNullOrEmpty(effect.name))
                return effect.name;
            return kind.ToString();
        }

        public ExNamedEffect Effect(ExKind kind)
        {
            switch (kind)
            {
                case ExKind.ColorStock: return colorStock;
                case ExKind.Poison: return poison;
                case ExKind.Paralysis: return paralysis;
                case ExKind.Frenzy: return frenzy;
                case ExKind.Focus: return focus;
                default: return null;
            }
        }

        string CardName(string id, string fallback)
        {
            var card = FindCard(id);
            if (card != null && !string.IsNullOrEmpty(card.nameJa))
                return card.nameJa;
            return fallback;
        }

        public string ColorName(string id)
        {
            if (colors == null || string.IsNullOrEmpty(id))
                return id ?? "";
            foreach (var color in colors)
            {
                if (color != null && color.id == id && !string.IsNullOrEmpty(color.name))
                    return color.name;
            }
            return id;
        }

        public ElementColor ToElement(string id)
        {
            switch (id)
            {
                case "blue": return ElementColor.Blue;
                case "green": return ElementColor.Green;
                case "yellow": return ElementColor.Yellow;
                case "purple": return ElementColor.Purple;
                default: return ElementColor.Red;
            }
        }
    }
}
