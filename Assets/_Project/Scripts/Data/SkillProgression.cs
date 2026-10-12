using TypingRoguelike.Battle;
using UnityEngine;

namespace TypingRoguelike.Data
{
    /// <summary>装備中スキルのラン内レベル。アセット本体は書き換えない。</summary>
    public static class SkillProgression
    {
        public const int MaxLevel = 3;

        public static int ClampLevel(int level) => Mathf.Clamp(level <= 0 ? 1 : level, 1, MaxLevel);

        public static int ScaleAmount(int baseAmount, int level)
        {
            if (baseAmount <= 0)
                return 0;
            var lv = ClampLevel(level);
            return Mathf.Max(1, Mathf.RoundToInt(baseAmount * (1f + 0.3f * (lv - 1))));
        }

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
            return lv <= 1 ? skill.displayName : $"{skill.displayName}  Lv.{lv}";
        }

        public static string Describe(SkillDefinition skill, int level)
        {
            if (skill == null)
                return string.Empty;

            var lv = ClampLevel(level);
            var damage = ScaleAmount(skill.damage, lv);
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
