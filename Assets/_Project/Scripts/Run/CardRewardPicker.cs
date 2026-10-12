using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public static class CardRewardPicker
    {
        public static List<SkillDefinition> PickChoices(
            SkillCardPoolDefinition pool,
            int choiceCount,
            int? seed = null) =>
            PickChoices(pool, choiceCount, null, seed);

        public static List<SkillDefinition> PickChoices(
            SkillCardPoolDefinition pool,
            int choiceCount,
            IReadOnlyCollection<int> unlockedSkillIds,
            int? seed = null)
            => PickChoices(pool, choiceCount, unlockedSkillIds, null, seed);

        public static List<SkillDefinition> PickChoices(
            SkillCardPoolDefinition pool,
            int choiceCount,
            IReadOnlyCollection<int> unlockedSkillIds,
            IReadOnlyCollection<int> excludedSkillIds,
            int? seed = null)
            => PickChoices(pool, choiceCount, unlockedSkillIds, excludedSkillIds, null, seed);

        public static List<SkillDefinition> PickChoices(
            SkillCardPoolDefinition pool,
            int choiceCount,
            IReadOnlyCollection<int> unlockedSkillIds,
            IReadOnlyCollection<int> excludedSkillIds,
            IReadOnlyCollection<string> characterNames,
            int? seed = null)
        {
            if (pool?.cards == null || pool.cards.Length == 0 || choiceCount <= 0)
                return new List<SkillDefinition>();

            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var eligible = pool.cards
                .Where(c => c != null && (unlockedSkillIds == null || unlockedSkillIds.Contains(c.skillId)))
                .Where(c => excludedSkillIds == null || !excludedSkillIds.Contains(c.skillId))
                .Where(c => characterNames == null || characterNames.Count == 0 || characterNames.Contains(c.characterName))
                .Distinct()
                .ToList();

            if (eligible.Count == 0)
                return new List<SkillDefinition>();

            Shuffle(eligible, rng);
            return eligible.Take(Mathf.Min(choiceCount, eligible.Count)).ToList();
        }

        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
