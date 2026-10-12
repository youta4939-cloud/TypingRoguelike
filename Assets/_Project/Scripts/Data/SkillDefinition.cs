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

        public int damage = 10;
        public int heal;
        public string effectText = "10ダメ";

        public SkillEffectKind effectKind;
        [Tooltip("EnemyDelay: 効果時間（秒）")]
        public float effectValue;
        [Tooltip("EnemyDelay: 攻撃速度低下％（50=半分の速さ）。RerollRestore: 加算回数")]
        public int effectValueInt;

        [TextArea] public string description;

        public BattleSkillData ToRuntimeData(int level = 1)
        {
            level = SkillProgression.ClampLevel(level);
            return new BattleSkillData(
                skillId,
                characterName,
                SkillProgression.FormatName(this, level),
                pattern,
                SkillProgression.ScaleAmount(damage, level),
                SkillProgression.ScaleAmount(heal, level),
                SkillProgression.Describe(this, level),
                effectKind,
                SkillProgression.ScaleDuration(this, level),
                SkillProgression.ScaleEffectInt(this, level));
        }
    }
}
