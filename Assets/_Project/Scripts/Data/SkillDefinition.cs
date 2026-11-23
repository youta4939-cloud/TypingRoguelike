using TypingRoguelike.Battle;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "Skill", menuName = "TypingRoguelike/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Tooltip("戦闘内クールダウン識別用（プロトタイプは 0〜25）")]
        public int skillId;

        public string characterName;
        public string displayName;

        [Tooltip("オーブパターン（末尾一致で発動）")]
        public ElementColor[] pattern = { ElementColor.Red, ElementColor.Blue };

        [Tooltip("所有者ATKに対する割合。0.15 = 15%。0 なら damage の固定値。")]
        public float damagePercent;
        public int damage = 10;
        public int heal;
        public string effectText = "10ダメ";

        public SkillEffectKind effectKind;
        [Tooltip("EnemyDelay: 効果時間（秒）")]
        public float effectValue;
        [Tooltip("EnemyDelay: 攻撃速度低下％（50=半分の速さ）。RerollRestore: 加算回数")]
        public int effectValueInt;

        [TextArea] public string description;

        [Tooltip("空なら今までのスキル。剣士スキルは JSON の key。")]
        public string logicKey;

        [Tooltip("N / R / SR。出やすさにはまだ使わない。")]
        public string rarity;

        public BattleSkillData ToRuntimeData(int level = 1, int ownerAttack = 0)
        {
            level = SkillProgression.ClampLevel(level);
            var entry = string.IsNullOrEmpty(logicKey) ? null : SwordsmanSkillConfig.Load()?.Find(logicKey);
            var axeConfig = entry == null && !string.IsNullOrEmpty(logicKey) ? AxeSkillConfig.Load() : null;
            var axe = axeConfig?.Find(logicKey);
            var usedPattern = pattern;
            var damage = SkillProgression.ScaleDamage(this, level, 0);
            var percent = 0f;
            if (entry != null)
            {
                usedPattern = SwordsmanSkillConfig.ToPattern(entry.pattern);
                if (!SamePattern(pattern, usedPattern))
                    Debug.LogWarning($"{displayName} のオーブが JSON と違います。JSON を使います。");
                damage = 0;
                percent = SwordsmanSkillConfig.At(entry.damagePercent, level);
            }
            else if (axe != null)
            {
                usedPattern = SwordsmanSkillConfig.ToPattern(axeConfig.PatternOf(axe));
                if (!SamePattern(pattern, usedPattern))
                    Debug.LogWarning($"{displayName} のオーブが JSON と違います。JSON を使います。");
                damage = 0;
                percent = AxeSkillConfig.At(axe.damagePercent, level);
            }

            return new BattleSkillData(
                skillId,
                characterName,
                SkillProgression.FormatName(this, level),
                usedPattern,
                damage,
                entry != null || axe != null ? 0 : SkillProgression.ScaleAmount(heal, level),
                SkillProgression.Describe(this, level, 0),
                entry != null || axe != null ? SkillEffectKind.None : effectKind,
                entry != null || axe != null ? 0f : SkillProgression.ScaleDuration(this, level),
                entry != null || axe != null ? 0 : SkillProgression.ScaleEffectInt(this, level),
                ownerAttack,
                damagePercent: percent,
                logicKey: logicKey,
                level: level);
        }

        static bool SamePattern(ElementColor[] left, ElementColor[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }
    }
}
