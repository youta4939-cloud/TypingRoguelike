using System;

namespace TypingRoguelike.Battle.Deck
{
    static class DeckColorParser
    {
        public static ElementColor[] ParsePattern(string colors)
        {
            if (string.IsNullOrWhiteSpace(colors))
                return Array.Empty<ElementColor>();
            var parts = colors.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var pattern = new ElementColor[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                pattern[i] = ParseOne(parts[i].Trim());
            return pattern;
        }

        static ElementColor ParseOne(string token) => token.ToUpperInvariant() switch
        {
            "R" => ElementColor.Red,
            "B" => ElementColor.Blue,
            "G" => ElementColor.Green,
            "Y" => ElementColor.Yellow,
            "P" => ElementColor.Purple,
            _ => ElementColor.Red
        };
    }
}