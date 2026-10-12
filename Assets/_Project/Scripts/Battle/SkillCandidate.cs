namespace TypingRoguelike.Battle
{
    public sealed class SkillCandidate
    {
        public BattleSkillData Skill { get; }
        public ElementColor NeededColor { get; }
        public bool IsPatternComplete { get; }
        public bool IsOnCooldown { get; }

        public SkillCandidate(
            BattleSkillData skill,
            ElementColor neededColor,
            bool isPatternComplete = false,
            bool isOnCooldown = false)
        {
            Skill = skill;
            NeededColor = neededColor;
            IsPatternComplete = isPatternComplete;
            IsOnCooldown = isOnCooldown;
        }
    }
}
