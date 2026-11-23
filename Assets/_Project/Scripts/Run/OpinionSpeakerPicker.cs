using System;
using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    /// <summary>意見を出す2人の選び方。差し替えるときはこの関数だけを見る。</summary>
    public static class OpinionSpeakerPicker
    {
        public static CharacterConfig[] PickSpeakers(
            IReadOnlyList<CharacterConfig> formation,
            string lastAbsentId,
            bool preferLastAbsent,
            Random rng)
        {
            var pool = new List<CharacterConfig>();
            if (formation != null)
            {
                foreach (var character in formation)
                {
                    if (character != null)
                        pool.Add(character);
                }
            }

            if (pool.Count <= 2)
                return pool.ToArray();

            CharacterConfig forced = null;
            if (preferLastAbsent && !string.IsNullOrEmpty(lastAbsentId))
            {
                foreach (var character in pool)
                {
                    if (StarterCharacters.Same(character.characterId, lastAbsentId))
                    {
                        forced = character;
                        break;
                    }
                }
            }

            if (forced != null)
            {
                pool.Remove(forced);
                var other = pool[rng.Next(pool.Count)];
                return rng.Next(2) == 0
                    ? new[] { forced, other }
                    : new[] { other, forced };
            }

            var firstIndex = rng.Next(pool.Count);
            var first = pool[firstIndex];
            pool.RemoveAt(firstIndex);
            var second = pool[rng.Next(pool.Count)];
            return new[] { first, second };
        }
    }
}
