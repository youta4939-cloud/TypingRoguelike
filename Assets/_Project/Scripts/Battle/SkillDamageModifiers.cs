using System;
using System.Collections.Generic;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// スキルダメに外から足す入口。単語ダメ・ジャックポットには使わない。
    /// 会心率は加算、最終ダメ倍率は乗算、会心倍率の上書きは一番大きい値。
    /// </summary>
    public sealed class SkillDamageModifiers
    {
        readonly List<Func<BattleSkillData, float>> _critChance = new();
        readonly List<Func<BattleSkillData, float>> _final = new();
        readonly List<Func<BattleSkillData, float?>> _critMultiplier = new();

        public void Clear()
        {
            _critChance.Clear();
            _final.Clear();
            _critMultiplier.Clear();
        }

        public void AddCritChanceSource(Func<BattleSkillData, float> source)
        {
            if (source != null)
                _critChance.Add(source);
        }

        public void RemoveCritChanceSource(Func<BattleSkillData, float> source) =>
            _critChance.Remove(source);

        public void AddFinalMultiplierSource(Func<BattleSkillData, float> source)
        {
            if (source != null)
                _final.Add(source);
        }

        public void RemoveFinalMultiplierSource(Func<BattleSkillData, float> source) =>
            _final.Remove(source);

        public void AddCritMultiplierOverrideSource(Func<BattleSkillData, float?> source)
        {
            if (source != null)
                _critMultiplier.Add(source);
        }

        public void RemoveCritMultiplierOverrideSource(Func<BattleSkillData, float?> source) =>
            _critMultiplier.Remove(source);

        public float SumCritChance(BattleSkillData skill)
        {
            var sum = 0f;
            for (var i = 0; i < _critChance.Count; i++)
                sum += _critChance[i](skill);
            return sum;
        }

        public float FinalMultiplier(BattleSkillData skill)
        {
            var product = 1f;
            for (var i = 0; i < _final.Count; i++)
            {
                var value = _final[i](skill);
                if (value > 0f)
                    product *= value;
            }

            return product;
        }

        public float? CritMultiplierOverride(BattleSkillData skill)
        {
            float? best = null;
            for (var i = 0; i < _critMultiplier.Count; i++)
            {
                var value = _critMultiplier[i](skill);
                if (!value.HasValue)
                    continue;
                if (!best.HasValue || value.Value > best.Value)
                    best = value;
            }

            return best;
        }
    }
}
