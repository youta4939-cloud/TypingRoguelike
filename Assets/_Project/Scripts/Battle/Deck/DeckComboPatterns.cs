using System;
using System.Collections.Generic;
using System.Linq;

namespace TypingRoguelike.Battle.Deck
{
    // A row is an input sequence. Resolve every input, including inputs between the target skills.
    public sealed class DeckComboPattern
    {
        public readonly ElementColor[] Inputs;
        public readonly DeckCardDefinition[][] Matches;
        public readonly bool CanContinue;

        public DeckComboPattern(ElementColor[] inputs, DeckCardDefinition[][] matches, bool canContinue)
        {
            Inputs = inputs;
            Matches = matches;
            CanContinue = canContinue;
        }
    }

    public static class DeckComboPatterns
    {
        public const int Window = 6;

        // Extend by the missing suffix of another skill. A full match at the same instant
        // belongs in Matches; it never creates a zero-input step or an infinite recursion.
        public static List<DeckComboPattern> Build(IReadOnlyList<DeckCardDefinition> skills,
            IReadOnlyList<ElementColor> prefix, DeckCardDefinition candidate)
        {
            var stock = prefix.Skip(Math.Max(0, prefix.Count - OrbStock.MaxLength)).ToList();
            int start = stock.Count;
            var paths = new List<DeckComboPattern>();
            var visited = new HashSet<string>();
            void Walk()
            {
                var key = string.Concat(stock.Skip(start).Select(c => (char)('a' + (int)c)));
                if (!visited.Add(key)) return;
                bool advanced = false;
                bool canContinue = false;
                bool crossesWindow = false;
                foreach (var skill in skills)
                {
                    var colors = skill.Colors;
                    if (colors.Length == 0 || colors.Length > OrbStock.MaxLength) continue;
                    int overlap = 0;
                    if (stock.Count > 0)
                    {
                        overlap = Math.Min(stock.Count, colors.Length - 1);
                        while (overlap > 0 && !MatchesPrefix(stock, colors, overlap)) overlap--;
                        if (overlap == 0) continue;
                    }
                    canContinue = true;
                    int extra = colors.Length - overlap;
                    if (stock.Count - start + extra > Window)
                    {
                        crossesWindow = true;
                        continue;
                    }
                    int count = stock.Count;
                    for (int i = overlap; i < colors.Length; i++) stock.Add(colors[i]);
                    Walk();
                    stock.RemoveRange(count, extra);
                    advanced = true;
                }
                if ((advanced && !crossesWindow) || stock.Count == start) return;
                var orbs = new OrbStock();
                foreach (var color in stock.Take(start)) orbs.Push(color);
                var inputs = stock.Skip(start).ToArray();
                var matches = new DeckCardDefinition[inputs.Length][];
                for (int i = 0; i < inputs.Length; i++)
                {
                    orbs.Push(inputs[i]);
                    matches[i] = skills.Where(s => s.Colors.Length > 0 &&
                        DeckCardResolver.MatchedColorCount(orbs, s.Colors) == s.Colors.Length)
                        .OrderBy(s => s.ProcessOrder).ToArray();
                }
                paths.Add(new DeckComboPattern(inputs, matches, canContinue));
            }
            Walk();

            // Alternate opening colors on a page so the first rows illustrate different routes.
            var ranked = paths.OrderByDescending(p => p.Matches.Any(step => step.Contains(candidate)))
                .ThenByDescending(p => p.Matches.SelectMany(step => step).Distinct().Count());
            return ranked.GroupBy(p => p.Inputs[0])
                .SelectMany(group => group.Select((pattern, index) => (pattern, index)))
                .OrderBy(entry => entry.index).Select(entry => entry.pattern).ToList();
        }

        static bool MatchesPrefix(List<ElementColor> stock, ElementColor[] colors, int length)
        {
            for (int i = 0; i < length; i++)
                if (stock[stock.Count - length + i] != colors[i]) return false;
            return true;
        }
    }
}
