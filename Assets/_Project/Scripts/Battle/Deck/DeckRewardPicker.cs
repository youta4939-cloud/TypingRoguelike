using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TypingRoguelike.Battle.Deck
{
    public static class DeckRewardConfig
    {
        public const int CoinMultiplier = 2;
        public const int CoinPerFloor = 2;
        public const int HistorySize = 2;
        public const int CardChoices = 2;
        public const int MaxCopies = 3;
        public static readonly IReadOnlyDictionary<string, int> RarityWeight = new Dictionary<string, int>
        {
            ["N"] = 1, ["R"] = 1, ["SR"] = 1
        };

        public static int Coins(int baseCoins, int floor) => baseCoins * CoinMultiplier + floor * CoinPerFloor;
    }

    public static class DeckRewardPicker
    {
        static int Weight(DeckCardDefinition card) =>
            DeckRewardConfig.RarityWeight.TryGetValue(card.Rarity, out var weight) ? weight : 0;

        public static List<DeckCardDefinition> Pick(IReadOnlyList<string> deck, IReadOnlyList<string> history)
        {
            var eligible = DeckCardCatalog.All.Values.Where(card => card.Reward && Weight(card) > 0 &&
                deck.Count(id => id == card.Id) < DeckRewardConfig.MaxCopies).ToList();
            var pool = eligible;
            for (int keep = Mathf.Min(history.Count, DeckRewardConfig.HistorySize); keep > 0; keep--)
            {
                var excluded = new HashSet<string>(history.Take(keep).SelectMany(entry => entry.Split(',')));
                var available = eligible.Where(card => !excluded.Contains(card.Id)).ToList();
                if (available.Count >= DeckRewardConfig.CardChoices)
                {
                    pool = available;
                    break;
                }
            }

            var result = new List<DeckCardDefinition>();
            while (pool.Count > 0 && result.Count < DeckRewardConfig.CardChoices)
            {
                var ticket = Random.Range(0, pool.Sum(Weight));
                var index = 0;
                while (ticket >= Weight(pool[index]))
                    ticket -= Weight(pool[index++]);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }
    }
}
