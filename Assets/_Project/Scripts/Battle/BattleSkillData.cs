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
        public int CasterAttack { get; set; }
        public int CasterIndex { get; set; } = -1;
        public float DamagePercent { get; set; }
        public bool ForceCrit { get; set; }
        public string LogicKey { get; set; }
        public int Level { get; set; } = 1;

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
            int effectValueInt = 0,
            int casterAttack = 0,
            int casterIndex = -1,
            float damagePercent = 0f,
            bool forceCrit = false,
            string logicKey = null,
            int level = 1)
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
            CasterAttack = casterAttack;
            CasterIndex = casterIndex;
            DamagePercent = damagePercent;
            ForceCrit = forceCrit;
            LogicKey = logicKey ?? "";
            Level = level <= 0 ? 1 : level;
        }

        public bool DealsDamage =>
            (Damage > 0 || DamagePercent > 0f) && EffectKind != SkillEffectKind.HealOnly;
    }
}
