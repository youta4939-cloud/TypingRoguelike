using System;
using System.Collections.Generic;
using System.Linq;

namespace TypingRoguelike.Battle.Deck
{
    public readonly struct DeckComboRoute
    {
        public readonly DeckCardDefinition From, To;
        public readonly int Remaining;
        public bool Simultaneous => Remaining == 0;

        public DeckComboRoute(DeckCardDefinition from, DeckCardDefinition to, int remaining)
        {
            From = from;
            To = to;
            Remaining = remaining;
        }
    }

    public static class DeckComboRoutes
    {
        public static List<DeckComboRoute> Find(DeckCardDefinition candidate, IEnumerable<string> deck)
        {
            var routes = new List<DeckComboRoute>();
            var seen = new HashSet<string> { candidate.Id };
            foreach (var id in deck)
            {
                var other = DeckCardCatalog.Get(id);
                if (other == null || !seen.Add(id))
                    continue;
                AddRoute(candidate, other, routes);
                AddRoute(other, candidate, routes);
            }
            // Stable ordering retains the first occurrence in the run deck for tied routes.
            return routes.OrderBy(route => route.Remaining)
                .ThenByDescending(route => route.To.Type == DeckCardType.Attack ? route.To.Value : 0).ToList();
        }

        static void AddRoute(DeckCardDefinition from, DeckCardDefinition to, List<DeckComboRoute> routes)
        {
            var a = from.Colors;
            var b = to.Colors;
            for (var k = Math.Min(a.Length, b.Length); k > 0; k--)
            {
                var i = 0;
                while (i < k && a[a.Length - k + i] == b[i])
                    i++;
                if (i != k)
                    continue;
                routes.Add(new DeckComboRoute(from, to, b.Length - k));
                return;
            }
        }
    }
}
