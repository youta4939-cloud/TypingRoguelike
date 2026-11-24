using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TypingRoguelike.Battle.Deck
{
    public sealed class DeckBattleState
    {
        readonly List<string> _drawPile = new();
        readonly List<string> _discardPile = new();
        readonly List<DeckHandSlot> _hand = new();
        readonly Random _rng;
        readonly int _startingCount;
        int _addedCount;
        int _consumedCount;

        public int Shield { get; private set; }
        public IReadOnlyList<DeckHandSlot> Hand => _hand;
        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        public DeckBattleState(IReadOnlyList<string> deckIds, int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _startingCount = deckIds.Count;
            _drawPile.AddRange(deckIds);
            Shuffle(_drawPile);
            CheckCardCount();
            FillHandToSize();
        }

        public void FillHandToSize()
        {
            while (_hand.Count < DeckCombatTuning.HandSize && DrawOneToHand())
            {
            }
        }

        DeckHandSlot FindOpenStack(string id) =>
            _hand.Find(slot => slot.CardId == id && slot.StackCount < 3);

        bool DrawOneToHand()
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0)
                    return false;
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile);
                CheckCardCount();
            }

            var id = _drawPile[^1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            PutInHand(id, FindOpenStack(id));
            CheckCardCount();
            return true;
        }

        void PutInHand(string id, DeckHandSlot stack)
        {
            if (stack != null)
                stack.StackCount++;
            else
                _hand.Add(new DeckHandSlot { CardId = id, StackCount = 1, HandIndex = _hand.Count });
        }

        void CheckCardCount()
        {
            var actual = DrawCount + DiscardCount + _consumedCount;
            foreach (var slot in _hand)
                actual += slot.StackCount;
            var expected = _startingCount + _addedCount;
            if (actual != expected)
                Debug.LogError($"Deck card count mismatch: expected {expected}, found {actual}.");
        }

        void Shuffle(List<string> pile)
        {
            for (var i = pile.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }

        public void AddShield(int amount)
        {
            if (amount > 0)
                Shield += amount;
        }

        public int ApplyShieldToDamage(int incoming)
        {
            if (Shield <= 0 || incoming <= 0)
                return incoming;
            var shield = Shield;
            Shield = 0;
            return Math.Max(0, incoming - shield);
        }

        public void RemoveSlot(DeckHandSlot slot, bool consumable)
        {
            if (!_hand.Remove(slot))
                return;
            if (consumable)
                _consumedCount += slot.StackCount;
            else
                for (var i = 0; i < slot.StackCount; i++)
                    _discardPile.Add(slot.CardId);

            for (var i = 0; i < _hand.Count; i++)
                _hand[i].HandIndex = i;
            CheckCardCount();
        }

        public void InsertCardToHand(string cardId)
        {
            if (DeckCardCatalog.Get(cardId) == null)
                return;
            var stack = FindOpenStack(cardId);
            if (stack != null || _hand.Count < DeckCombatTuning.HandSize)
                PutInHand(cardId, stack);
            else
                _drawPile.Add(cardId);
            _addedCount++;
            CheckCardCount();
        }
    }
}
