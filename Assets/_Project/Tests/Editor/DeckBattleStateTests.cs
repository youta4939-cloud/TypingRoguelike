using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TypingRoguelike.Battle;
using TypingRoguelike.Battle.Deck;

namespace TypingRoguelike.Tests
{
    public sealed class DeckBattleStateTests
    {
        [Test]
        public void FillHand_ReachesHandSize_WithStarterCounts()
        {
            var ids = DeckCardCatalog.BuildStarterDeckIds();
            Assert.AreEqual(18, ids.Count);
            Assert.AreEqual(9, ids.Distinct().Count());
            Assert.AreEqual(3, ids.Count(id => id == "quick_slash_r"));
            Assert.AreEqual(3, ids.Count(id => id == "quick_guard_b"));
            var state = new DeckBattleState(ids, 42);
            Assert.AreEqual(DeckCombatTuning.HandSize, state.Hand.Count,
                $"手札は{DeckCombatTuning.HandSize}枠必要（実際 {state.Hand.Count}）");
        }

        [TestCase("quick_slash_r", ElementColor.Red, DeckCardType.Attack, 3, 4, 6)]
        [TestCase("quick_guard_b", ElementColor.Blue, DeckCardType.Shield, 2, 3, 4)]
        public void SingleColorStarterCard_TriggersAndScalesAtEachLevel(
            string cardId, ElementColor color, DeckCardType type, int levelOne, int levelTwo, int levelThree)
        {
            var state = new DeckBattleState(new[] { cardId }, 42);
            var slot = state.Hand.Single();
            Assert.AreEqual(type, slot.Def.Type);

            var stock = new OrbStock();
            stock.Push(ElementColor.Green);
            Assert.IsEmpty(DeckCardResolver.ResolveTriggered(state.Hand, stock));
            stock.Push(color);
            Assert.AreSame(slot, DeckCardResolver.ResolveTriggered(state.Hand, stock).Single());

            CollectionAssert.AreEqual(new[] { 1, levelOne }, new[] { slot.StackLevel, slot.ScaledValue(slot.Def) });
            state.InsertCardToHand(cardId);
            CollectionAssert.AreEqual(new[] { 2, levelTwo }, new[] { slot.StackLevel, slot.ScaledValue(slot.Def) });
            state.InsertCardToHand(cardId);
            CollectionAssert.AreEqual(new[] { 3, levelThree }, new[] { slot.StackLevel, slot.ScaledValue(slot.Def) });
            Assert.AreSame(slot, state.Hand.Single());
        }

        [Test]
        public void FourthCopy_StartsAnotherStack()
        {
            var state = new DeckBattleState(new[] { "slash_r", "slash_r", "slash_r", "slash_r" }, 42);
            CollectionAssert.AreEqual(new[] { 3, 1 }, state.Hand.Select(slot => slot.StackCount));
            state.RemoveSlot(state.Hand[0], false);
            Assert.AreEqual(3, state.DiscardCount);
            state.FillHandToSize();
            CollectionAssert.AreEqual(new[] { 3, 1 }, state.Hand.Select(slot => slot.StackCount));
        }

        [Test]
        public void ConsumableReplacement_KeepsAllCopiesAccountedFor()
        {
            var state = new DeckBattleState(new[] { "slash_r", "slash_r" }, 42);
            var slot = state.Hand[0];
            state.RemoveSlot(slot, true);
            state.RemoveSlot(slot, false);
            state.InsertCardToHand("iai");
            state.FillHandToSize();
            Assert.AreEqual(0, state.DiscardCount);
            Assert.AreEqual("iai", state.Hand.Single().CardId);
        }

        [Test]
        public void MatchedColors_UsesLongestStockSuffixMatchingPatternPrefix()
        {
            var stock = new OrbStock();
            stock.Push(ElementColor.Green);
            stock.Push(ElementColor.Red);
            stock.Push(ElementColor.Blue);
            var pattern = new[] { ElementColor.Red, ElementColor.Blue, ElementColor.Red };
            Assert.AreEqual(2, DeckCardResolver.MatchedColorCount(stock, pattern));
            stock.Push(ElementColor.Red);
            Assert.AreEqual(3, DeckCardResolver.MatchedColorCount(stock, pattern));
        }

        [Test]
        public void WordGuide_PredictsActivationBeforePartialMatchesWithoutChangingStock()
        {
            var hand = new[]
            {
                new DeckHandSlot { CardId = "iai" },
                new DeckHandSlot { CardId = "slash_b", StackCount = 2 },
                new DeckHandSlot { CardId = "guard_b" }
            };
            var stock = new OrbStock();
            stock.Push(ElementColor.Red);
            var predictions = DeckCardResolver.PredictWord(hand, stock, ElementColor.Blue);
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, predictions.Select(item => item.HandIndex));
            CollectionAssert.AreEqual(new[] { 0, 1, 1 }, predictions.Select(item => item.Remaining));
            Assert.AreEqual("斬撃", predictions[0].Name);
            Assert.AreEqual("9ダメージ", predictions[0].Effect);
            Assert.AreEqual("14ダメージ", predictions[1].Effect);
            Assert.AreEqual("シールド5", predictions[2].Effect);
            Assert.AreEqual(ElementColor.Red, predictions[1].Color);
            CollectionAssert.AreEqual(new[] { ElementColor.Red }, stock.Orbs);
            Assert.IsEmpty(DeckCardResolver.PredictWord(hand, stock, ElementColor.Purple));
        }

        [Test]
        public void Shield_AbsorbsOneHit()
        {
            var state = new DeckBattleState(new List<string> { "guard" }, 1);
            state.AddShield(5);
            var left = state.ApplyShieldToDamage(4);
            Assert.AreEqual(0, left);
            Assert.AreEqual(0, state.Shield);
            state.AddShield(5);
            left = state.ApplyShieldToDamage(8);
            Assert.AreEqual(3, left);
        }

        [Test]
        public void Resolver_MatchesOrbSuffix()
        {
            var hand = new List<DeckHandSlot>
            {
                new() { CardId = "slash_r", HandIndex = 0 }
            };
            var stock = new OrbStock();
            stock.Push(ElementColor.Red);
            var ready = DeckCardResolver.ResolveTriggered(hand, stock);
            Assert.AreEqual(0, ready.Count);
            stock.Push(ElementColor.Red);
            ready = DeckCardResolver.ResolveTriggered(hand, stock);
            Assert.AreEqual(1, ready.Count);
        }
    }
}
