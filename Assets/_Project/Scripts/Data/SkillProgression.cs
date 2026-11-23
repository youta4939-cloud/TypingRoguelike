using TypingRoguelike.Battle;
using UnityEngine;

namespace TypingRoguelike.Data
{
    /// <summary>装備中スキルのラン内レベル。アセット本体は書き換えない。</summary>
    public static class SkillProgression
    {
        public const int MaxLevel = 3;
        public const int XpForLevel2 = 100;
        public const int XpForLevel3 = 300;
        public const int XpPerActivation = 1;
        public const int XpPerSkippedReward = 10;

        public static int ClampLevel(int level) => Mathf.Clamp(level <= 0 ? 1 : level, 1, MaxLevel);

        public static int LevelFromXp(int xp)
        {
            if (xp >= XpForLevel3)
                return 3;
            if (xp >= XpForLevel2)
                return 2;
            return 1;
        }

        /// <summary>そのレベルに到達した時点の累計経験値。Lv.1 は 0。</summary>
        public static int XpForLevel(int level) => ClampLevel(level) switch
        {
            >= 3 => XpForLevel3,
            2 => XpForLevel2,
            _ => 0
        };

        public static int NextLevelXp(int xp) =>
            LevelFromXp(xp) >= MaxLevel ? XpForLevel3 : LevelFromXp(xp) == 1 ? XpForLevel2 : XpForLevel3;

        public static string FormatXp(int xp)
        {
            var amount = Mathf.Max(0, xp);
            if (LevelFromXp(amount) >= MaxLevel)
                return $"経験値 {amount}（最大）";
            return $"経験値 {amount} / {NextLevelXp(amount)}";
        }

        public static int ScaleAmount(int baseAmount, int level)
        {
            if (baseAmount <= 0)
                return 0;
            var lv = ClampLevel(level);
            return Mathf.Max(1, Mathf.RoundToInt(baseAmount * LevelDamageScale(lv)));
        }

        /// <summary>ダメージ = 所有者ATK × 割合 × レベル補正。割合が無いときは damage の固定値。</summary>
        public static int ScaleDamage(SkillDefinition skill, int level, int ownerAttack = 0)
        {
            if (skill == null)
                return 0;
            var lv = ClampLevel(level);
            if (skill.damagePercent > 0f && ownerAttack > 0)
                return Mathf.Max(1, Mathf.RoundToInt(ownerAttack * skill.damagePercent * LevelDamageScale(lv)));
            return ScaleAmount(skill.damage, lv);
        }

        public static float LevelDamageScale(int level) => 1f + 0.3f * (ClampLevel(level) - 1);

        public static float ScaleDuration(SkillDefinition skill, int level)
        {
            if (skill == null || skill.effectKind != SkillEffectKind.EnemyDelay)
                return skill != null ? skill.effectValue : 0f;
            var lv = ClampLevel(level);
            return skill.effectValue * (1f + 0.25f * (lv - 1));
        }

        public static int ScaleEffectInt(SkillDefinition skill, int level)
        {
            if (skill == null)
                return 0;
            var lv = ClampLevel(level);
            return skill.effectKind switch
            {
                SkillEffectKind.EnemyDelay => Mathf.Min(90, skill.effectValueInt + 10 * (lv - 1)),
                SkillEffectKind.RerollRestore => skill.effectValueInt + (lv - 1),
                _ => skill.effectValueInt
            };
        }

        public static string FormatName(SkillDefinition skill, int level)
        {
            if (skill == null)
                return string.Empty;
            var lv = ClampLevel(level);
            var name = skill.displayName ?? "";
            if (!string.IsNullOrEmpty(skill.logicKey))
            {
                var config = SwordsmanSkillConfig.Load();
                var entry = config?.Find(skill.logicKey);
                if (entry != null && entry.type == "passive")
                {
                    var label = config.options != null && !string.IsNullOrEmpty(config.options.passiveLabel)
                        ? config.options.passiveLabel
                        : "パッシブ";
                    name = $"［{label}］{name}";
                }
            }

            return lv <= 1 ? name : $"{name}  Lv.{lv}";
        }

        public static string Describe(SkillDefinition skill, int level) => Describe(skill, level, 0);

        public static string Describe(SkillDefinition skill, int level, int ownerAttack)
        {
            if (skill == null)
                return string.Empty;

            var lv = ClampLevel(level);
            if (!string.IsNullOrEmpty(skill.logicKey))
            {
                var text = SwordsmanSkillConfig.Load()?.TextOf(skill.logicKey, lv);
                if (string.IsNullOrEmpty(text))
                    text = AxeSkillConfig.Load()?.TextOf(skill.logicKey, lv);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            var damage = ScaleDamage(skill, lv, ownerAttack);
            var heal = ScaleAmount(skill.heal, lv);
            switch (skill.effectKind)
            {
                case SkillEffectKind.HealOnly:
                    return $"{heal}回復";
                case SkillEffectKind.EnemyDelay:
                    var slow = ScaleEffectInt(skill, lv);
                    var seconds = ScaleDuration(skill, lv);
                    return damage > 0
                        ? $"{damage}ダメ + 敵攻撃{slow}%減・{seconds:0.#}秒"
                        : $"敵攻撃{slow}%減・{seconds:0.#}秒";
                case SkillEffectKind.RerollRestore:
                    var rerolls = ScaleEffectInt(skill, lv);
                    return damage > 0
                        ? $"{damage}ダメ + リロール+{rerolls}"
                        : $"リロール+{rerolls}";
                default:
                    if (damage > 0 && heal > 0)
                        return $"{damage}ダメ + {heal}回復";
                    if (heal > 0)
                        return $"{heal}回復";
                    if (damage > 0)
                        return damage >= 80 ? $"{damage}大ダメ" : $"{damage}ダメ";
                    return skill.effectText;
            }
        }
    }
}
