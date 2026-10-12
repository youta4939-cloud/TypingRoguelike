using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "SkillCardPool", menuName = "TypingRoguelike/Skill Card Pool")]
    public sealed class SkillCardPoolDefinition : ScriptableObject
    {
        [Tooltip("報酬3選1などで抽選するスキルカード")]
        public SkillDefinition[] cards;
    }
}
