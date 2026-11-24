using System.Collections.Generic;
using System.Linq;

namespace TypingRoguelike.Battle.Deck
{
    public sealed class DeckCardCandidate
    {
        public DeckHandSlot Slot;
        public bool IsPatternComplete;
        public bool IsNear;
        public ElementColor NeededColor;
    }

    public readonly struct DeckWordPrediction
    {
        public readonly string Name;
        public readonly string Effect;
        public readonly int Remaining;
        public readonly DeckCardType Type;
        public readonly int HandIndex;
        public readonly ElementColor Color;
        internal readonly int ProcessOrder;

        public DeckWordPrediction(DeckCardDefinition card, int value, int remaining, int handIndex, ElementColor color)
        {
            var separator = card.Name.IndexOf('・');
            Name = separator < 0 ? card.Name : card.Name.Substring(0, separator);
            Remaining = remaining;
            Effect = card.Type switch
            {
                DeckCardType.Attack => $"{value}ダメージ",
                DeckCardType.Shield => $"シールド{value}",
                DeckCardType.Buff => $"次の攻撃+{value}",
                _ => card.Text
            };
            Type = card.Type;
            HandIndex = handIndex;
            Color = color;
            ProcessOrder = card.ProcessOrder;
        }
    }

    public static class DeckCardResolver
    {
        public static List<DeckWordPrediction> PredictWord(IReadOnlyList<DeckHandSlot> hand, OrbStock stock, ElementColor word)
        {
            var predictions = new List<DeckWordPrediction>();
            if (hand == null)
                return predictions;

            for (var index = 0; index < hand.Count; index++)
            {
                var slot = hand[index];
                var card = slot?.Def;
                if (card == null)
                    continue;
                var pattern = card.Colors;
                var maxLength = System.Math.Min(pattern.Length, System.Math.Min(stock.Orbs.Count + 1, OrbStock.MaxLength));
                for (var length = maxLength; length > 0; length--)
                {
                    if (pattern[length - 1] != word || !TrySuffixMatch(stock, pattern, length - 1))
                        continue;
                    var remaining = pattern.Length - length;
                    var color = remaining == 0 ? word : pattern[length];
                    predictions.Add(new DeckWordPrediction(card, slot.ScaledValue(card), remaining, index, color));
                    break;
                }
            }

            predictions.Sort((a, b) =>
            {
                var order = a.Remaining.CompareTo(b.Remaining);
                if (order == 0 && a.Remaining == 0)
                    order = a.ProcessOrder.CompareTo(b.ProcessOrder);
                return order != 0 ? order : a.HandIndex.CompareTo(b.HandIndex);
            });
            return predictions;
        }

        public static int MatchedColorCount(OrbStock stock, ElementColor[] pattern)
        {
            for (var length = System.Math.Min(pattern.Length, stock.Orbs.Count); length > 0; length--)
                if (TrySuffixMatch(stock, pattern, length))
                    return length;
            return 0;
        }

        public static List<DeckCardCandidate> GetCandidates(IReadOnlyList<DeckHandSlot> hand, OrbStock stock)
        {
            var list = new List<DeckCardCandidate>();
            if (hand == null)
                return list;
            foreach (var slot in hand)
            {
                var def = slot?.Def;
                if (def == null || def.Colors.Length == 0)
                    continue;
                var length = def.Colors.Length;
                var matched = MatchedColorCount(stock, def.Colors);
                if (matched < length - 1)
                    continue;
                list.Add(new DeckCardCandidate
                {
                    Slot = slot,
                    IsPatternComplete = matched == length,
                    IsNear = matched == length - 1,
                    NeededColor = def.Colors[length - 1]
                });
            }

            return list;
        }

        public static List<DeckHandSlot> ResolveTriggered(IReadOnlyList<DeckHandSlot> hand, OrbStock stock)
        {
            if (hand == null)
                return new List<DeckHandSlot>();
            var ready = new List<DeckHandSlot>();
            foreach (var slot in hand)
            {
                var def = slot?.Def;
                if (def == null || def.Colors.Length == 0)
                    continue;
                if (TrySuffixMatch(stock, def.Colors, def.Colors.Length))
                    ready.Add(slot);
            }

            return ready
                .OrderBy(s => s.Def.ProcessOrder)
                .ThenBy(s => s.HandIndex)
                .ToList();
        }

        static bool TrySuffixMatch(OrbStock stock, ElementColor[] pattern, int patternLen)
        {
            var start = stock.Orbs.Count - patternLen;
            if (start < 0)
                return false;
            for (var i = 0; i < patternLen; i++)
            {
                if (stock.Orbs[start + i] != pattern[i])
                    return false;
            }

            return true;
        }
    }
}