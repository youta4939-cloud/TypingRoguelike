namespace TypingRoguelike.Battle.Deck
{
    public sealed class DeckCardDefinition
    {
        public string Id;
        public string Name;
        public ElementColor[] Colors = System.Array.Empty<ElementColor>();
        public DeckCardType Type;
        public int Value;
        public bool Consumable;
        public string AddCardId;
        public string Rarity;
        public string Text;
        public bool Reward = true;

        public int ProcessOrder => Type switch
        {
            DeckCardType.Buff => 0,
            DeckCardType.Shield => 1,
            DeckCardType.Attack => 2,
            _ => 3
        };
    }
}