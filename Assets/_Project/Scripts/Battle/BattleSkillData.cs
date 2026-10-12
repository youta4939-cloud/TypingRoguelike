namespace TypingRoguelike.Battle
{
    public sealed class BattleSkillData
    {
        public int Id { get; }
        public string CharacterName { get; }
        public string DisplayName { get; }
        public ElementColor[] Pattern { get; }
        public int Damage { get; }
        public int Heal { get; }
        public string EffectText { get; }
        public SkillEffectKind EffectKind { get; }
        public float EffectValue { get; }
        public int EffectValueInt { get; }

        public BattleSkillData(
            int id,
            string characterName,
            string displayName,
            ElementColor[] pattern,
            int damage,
            int heal,
            string effectText,
            SkillEffectKind effectKind = SkillEffectKind.None,
            float effectValue = 0f,
            int effectValueInt = 0)
        {
            Id = id;
            CharacterName = characterName;
            DisplayName = displayName;
            Pattern = pattern;
            Damage = damage;
            Heal = heal;
            EffectText = effectText;
            EffectKind = effectKind;
            EffectValue = effectValue;
            EffectValueInt = effectValueInt;
        }

        public bool DealsDamage =>
            Damage > 0 && EffectKind != SkillEffectKind.HealOnly;
    }
}
