namespace TypingRoguelike.Battle.Deck
{
    public static class DeckCombatTuning
    {
        public const int HandSize = 3;
        public const int MaxStackPerIdInDeck = 3;
        public const float WordDamagePerChar = 1f;

        public static readonly float[] StackValueMultiplier = { 1f, 1.5f, 2f };
    }
}