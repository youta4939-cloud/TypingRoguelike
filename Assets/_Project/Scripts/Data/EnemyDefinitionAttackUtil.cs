using UnityEngine;

namespace TypingRoguelike.Data
{
    public static class EnemyDefinitionAttackUtil
    {
        public const int AttackSlotCount = 3;

        public static bool HasConfiguredAttackSlots(EnemyDefinition enemy)
        {
            if (enemy?.attacks == null)
                return false;
            foreach (var pattern in enemy.attacks)
            {
                if (pattern.IsActive)
                    return true;
            }

            return false;
        }

        public static bool HasLegacyAttackData(EnemyDefinition enemy)
        {
            if (enemy == null)
                return false;
            var light = enemy.lightAttackInterval > 0f && enemy.lightAttackDamage > 0f;
            var heavy = enemy.heavyAttackInterval > 0f && enemy.heavyAttackDamage > 0f;
            return light || heavy;
        }

        public static bool TryMigrateLegacyIfNeeded(EnemyDefinition enemy)
        {
            if (enemy == null || HasConfiguredAttackSlots(enemy) || !HasLegacyAttackData(enemy))
                return false;
            MigrateLegacyToAttackSlots(enemy);
            return true;
        }

        public static void EnsureAttackArray(EnemyDefinition enemy)
        {
            if (enemy == null)
                return;
            if (enemy.attacks == null || enemy.attacks.Length != AttackSlotCount)
                enemy.attacks = new EnemyAttackPattern[AttackSlotCount];
        }

        public static EnemyAttackPattern GetResolvedSlot(EnemyDefinition enemy, int index)
        {
            if (enemy == null || index < 0 || index >= AttackSlotCount)
                return default;

            EnsureAttackArray(enemy);
            var fromSlots = enemy.attacks[index];
            if (fromSlots.IsActive)
                return fromSlots;

            if (HasConfiguredAttackSlots(enemy))
                return fromSlots;

            return LegacySlot(enemy, index);
        }

        public static int[] GetActiveSlotIndices(EnemyDefinition enemy)
        {
            if (enemy == null)
                return System.Array.Empty<int>();

            EnsureAttackArray(enemy);
            if (HasConfiguredAttackSlots(enemy))
            {
                var list = new System.Collections.Generic.List<int>(AttackSlotCount);
                for (var i = 0; i < AttackSlotCount; i++)
                {
                    if (enemy.attacks[i].IsActive)
                        list.Add(i);
                }

                return list.ToArray();
            }

            var legacy = new System.Collections.Generic.List<int>(AttackSlotCount);
            for (var i = 0; i < AttackSlotCount; i++)
            {
                if (LegacySlot(enemy, i).IsActive)
                    legacy.Add(i);
            }

            return legacy.ToArray();
        }

        public static int FirstRotateSlot(EnemyDefinition enemy)
        {
            var active = GetActiveSlotIndices(enemy);
            return active.Length > 0 ? active[0] : -1;
        }

        public static int AdvanceRotateSlot(EnemyDefinition enemy, int currentSlot)
        {
            var active = GetActiveSlotIndices(enemy);
            if (active.Length == 0)
                return -1;
            if (active.Length == 1)
                return active[0];

            for (var i = 0; i < active.Length; i++)
            {
                if (active[i] != currentSlot)
                    continue;
                return active[(i + 1) % active.Length];
            }

            return active[0];
        }

        public static void MigrateLegacyToAttackSlots(EnemyDefinition enemy)
        {
            if (enemy == null || HasConfiguredAttackSlots(enemy))
                return;

            EnsureAttackArray(enemy);
            enemy.attacks[0] = new EnemyAttackPattern
            {
                damage = enemy.lightAttackDamage,
                intervalSeconds = enemy.lightAttackInterval,
                effect = EnemyAttackEffectKind.None,
                firstDelaySeconds = enemy.openingAttackDelay > 0f
                    ? enemy.openingAttackDelay
                    : 0f
            };
            enemy.attacks[1] = new EnemyAttackPattern
            {
                damage = enemy.heavyAttackDamage,
                intervalSeconds = enemy.heavyAttackInterval,
                effect = enemy.heavyAttackTelegraphSeconds > 0f
                    ? EnemyAttackEffectKind.Telegraph
                    : EnemyAttackEffectKind.None,
                effectValue = enemy.heavyAttackTelegraphSeconds,
                firstDelaySeconds = enemy.heavyAttackFirstSeconds
            };
            enemy.attacks[2] = default;
        }

        static EnemyAttackPattern LegacySlot(EnemyDefinition enemy, int index)
        {
            return index switch
            {
                0 => new EnemyAttackPattern
                {
                    damage = enemy.lightAttackDamage,
                    intervalSeconds = enemy.lightAttackInterval,
                    effect = EnemyAttackEffectKind.None
                },
                1 => new EnemyAttackPattern
                {
                    damage = enemy.heavyAttackDamage,
                    intervalSeconds = enemy.heavyAttackInterval,
                    effect = enemy.heavyAttackTelegraphSeconds > 0f
                        ? EnemyAttackEffectKind.Telegraph
                        : EnemyAttackEffectKind.None,
                    effectValue = enemy.heavyAttackTelegraphSeconds,
                    firstDelaySeconds = enemy.heavyAttackFirstSeconds
                },
                _ => default
            };
        }
    }
}