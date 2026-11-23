using System;

namespace TypingRoguelike.Battle
{
    public struct SkillDamageRequest
    {
        public int CasterAttack;
        public float Percent;
        public float FlatDamage;
        public float SkillOwnMultiplier;
        public float FinalMultiplier;
        public float CritChance;
        public bool ForceCrit;
        public float CritMultiplier;
        public float Roll;
    }

    public readonly struct SkillDamageResult
    {
        public float Damage { get; }
        public bool IsCrit { get; }

        public SkillDamageResult(float damage, bool isCrit)
        {
            Damage = damage;
            IsCrit = isCrit;
        }
    }

    /// <summary>単語ダメとスキルダメ。Unity には依存しない。</summary>
    public static class CombatMath
    {
        public static float WordDamage(int totalAttack, float coefficient)
        {
            var raw = totalAttack * (double)coefficient;
            var rounded = Math.Round(raw, MidpointRounding.AwayFromZero);
            return (float)Math.Max(1d, rounded);
        }

        public static SkillDamageResult ComputeSkillDamage(in SkillDamageRequest request)
        {
            var own = request.SkillOwnMultiplier > 0f ? request.SkillOwnMultiplier : 1f;
            var finalMultiplier = request.FinalMultiplier > 0f ? request.FinalMultiplier : 1f;
            var critMultiplier = request.CritMultiplier > 0f ? request.CritMultiplier : 1f;
            double baseDamage = request.Percent > 0f
                ? request.CasterAttack * (double)request.Percent
                : request.FlatDamage;
            var damage = baseDamage * own * finalMultiplier;
            var chance = Math.Max(0d, Math.Min(1d, request.CritChance));
            var crit = request.ForceCrit || request.Roll < chance;
            if (crit)
                damage *= critMultiplier;
            return new SkillDamageResult((float)damage, crit);
        }
    }
}
