using UnityEngine;

namespace TypingRoguelike.Battle.Deck
{
    public sealed class DeckHandSlot
    {
        public string CardId;
        public int StackCount = 1;
        public int HandIndex;

        public DeckCardDefinition Def => DeckCardCatalog.Get(CardId);

        public int StackLevel => StackCount <= 1 ? 1 : StackCount >= 3 ? 3 : StackCount;

        public float ValueMultiplier
        {
            get
            {
                var idx = StackLevel - 1;
                if (idx < 0 || idx >= DeckCombatTuning.StackValueMultiplier.Length)
                    return 1f;
                return DeckCombatTuning.StackValueMultiplier[idx];
            }
        }

        public int ScaledValue(DeckCardDefinition def) =>
            def == null ? 0 : ScaledRound(def.Value * ValueMultiplier);

        static int ScaledRound(float v) => Mathf.RoundToInt(v);
    }
}