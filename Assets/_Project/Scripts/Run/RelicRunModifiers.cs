using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public readonly struct RelicRunModifiers
    {
        public int BackpackSlotBonus { get; }
        public int PlayerDamageBonusPercent { get; }
        public int MadnessMaxBonus { get; }
        public int JackpotMadnessThreshold { get; }
        public float JackpotChanceBonus { get; }
        public float JackpotDamageMultiplier { get; }
        public bool ExclusiveEquipped { get; }
        public float OpeningEnemyStopSeconds { get; }

        public RelicRunModifiers(
            int backpackBonus,
            int damageBonusPercent,
            int madnessMaxBonus,
            int jackpotMadnessThreshold,
            float jackpotChanceBonus,
            float jackpotDamageMultiplier,
            bool exclusiveEquipped,
            float openingEnemyStopSeconds)
        {
            BackpackSlotBonus = backpackBonus;
            PlayerDamageBonusPercent = damageBonusPercent;
            MadnessMaxBonus = madnessMaxBonus;
            JackpotMadnessThreshold = jackpotMadnessThreshold;
            JackpotChanceBonus = jackpotChanceBonus;
            JackpotDamageMultiplier = jackpotDamageMultiplier <= 0f ? 1f : jackpotDamageMultiplier;
            ExclusiveEquipped = exclusiveEquipped;
            OpeningEnemyStopSeconds = openingEnemyStopSeconds;
        }

        public static RelicRunModifiers Compute(RunExplorationInventory loot, RelicCatalogDefinition catalog)
        {
            if (loot == null || catalog == null)
                return default;

            var equipped = new RelicDefinition[RunExplorationInventory.RelicSlotCount];
            var exclusive = false;
            for (var i = 0; i < equipped.Length; i++)
            {
                var id = loot.GetEquippedRelicId(i);
                if (id < 0)
                    continue;
                var def = catalog.FindById(id);
                equipped[i] = def;
                if (def != null && def.exclusiveEquip)
                    exclusive = true;
            }

            var backpack = 0;
            var damage = 0;
            var madness = 0;
            var threshold = 0;
            var chance = 0f;
            var jackpotDamage = 1f;
            var stop = 0f;
            for (var i = 0; i < equipped.Length; i++)
            {
                var def = equipped[i];
                if (def == null)
                    continue;
                if (exclusive && !def.exclusiveEquip)
                    continue;

                backpack += def.backpackSlotBonus;
                damage += def.playerDamageBonusPercent;
                madness += def.madnessMaxBonus;
                if (def.battleStartEnemyStopSeconds > 0f)
                    stop += def.battleStartEnemyStopSeconds;
                if (def.jackpotMadnessThreshold <= 0)
                    continue;

                threshold = threshold == 0
                    ? def.jackpotMadnessThreshold
                    : System.Math.Min(threshold, def.jackpotMadnessThreshold);
                chance += def.jackpotChanceBonus;
                var mul = def.jackpotDamageMultiplier <= 0f ? 1f : def.jackpotDamageMultiplier;
                jackpotDamage *= mul;
            }

            return new RelicRunModifiers(
                backpack, damage, madness, threshold, chance, jackpotDamage, exclusive, stop);
        }
    }
}
