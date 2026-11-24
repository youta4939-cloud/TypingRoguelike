using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TypingRoguelike.Battle.Deck;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TypingRoguelike.Tests
{
    public sealed class DeckRewardTests
    {
        [TestCase(8, 1, 18)]
        [TestCase(8, 10, 36)]
        [TestCase(15, 10, 50)]
        public void CoinOfferIncludesFloor(int baseCoins, int floor, int expected) =>
            Assert.AreEqual(expected, DeckRewardConfig.Coins(baseCoins, floor));

        [Test]
        public void TenOffersExcludeTwoScreensAndCappedCards()
        {
            var deck = DeckCardCatalog.BuildStarterDeckIds();
            var history = new List<string>();
            for (var screen = 0; screen < 10; screen++)
            {
                var ids = DeckRewardPicker.Pick(deck, history).Select(c => c.Id).ToArray();
                Assert.AreEqual(2, ids.Distinct().Count());
                Assert.False(history.SelectMany(h => h.Split(',')).Intersect(ids).Any());
                Assert.True(ids.All(id => deck.Count(copy => copy == id) < 3));
                history.Insert(0, string.Join(",", ids));
                if (history.Count > 2) history.RemoveAt(2);
            }
        }

        [Test]
        public void ShortPoolRelaxesOldestFirstAndAlwaysHonorsCap()
        {
            var deck = DeckCardCatalog.All.Keys.SelectMany(id => Enumerable.Repeat(id, 3)).ToList();
            var history = new List<string> { "thrust", "iai,stance" };
            Assert.IsEmpty(DeckRewardPicker.Pick(deck, history));
            deck.Remove("thrust");
            Assert.AreEqual("thrust", DeckRewardPicker.Pick(deck, history).Single().Id);
            deck.Remove("iai");
            deck.Remove("stance");
            CollectionAssert.AreEquivalent(new[] { "iai", "stance" }, DeckRewardPicker.Pick(deck, history).Select(c => c.Id));
        }

        [TestCase("thrust", "iai:thrust:0 thrust:slash_r:1 thrust:slash_b:1", 5)]
        [TestCase("iai", "iai:thrust:0 slash_b:iai:1 iai:slash_r:1", 7)]
        [TestCase("guard_b", "slash_b:guard_b:1 guard_b:guard_g:1", 2)]
        [TestCase("stance", "stance:slash_r:1 stance:slash_b:1 stance:iai:2", 3)]
        public void SpecDeckRoutesMatchPreview(string candidate, string preview, int count)
        {
            var deck = new[] { "slash_r", "slash_r", "slash_b", "slash_b", "thrust", "guard_g", "guard_g",
                "guard_b", "stance", "iai" };
            var routes = DeckComboRoutes.Find(DeckCardCatalog.Get(candidate), deck);
            Assert.AreEqual(count, routes.Count);
            CollectionAssert.AreEqual(preview.Split(' '), routes.Take(3).Select(r => $"{r.From.Id}:{r.To.Id}:{r.Remaining}"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PanelPreviewsThenConfirmsExactlyOnce(bool takeCard)
        {
            var root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = root.AddComponent<DeckRewardView>();
                var card = DeckCardCatalog.Get("thrust");
                var calls = 0;
                DeckCardDefinition picked = null;
                view.Build(new[] { card }, new RunPartyState(), 18, "", reward => { calls++; picked = reward; });
                var layout = root.transform.Find("Choices");
                var first = layout.Find("Choice1").GetComponent<EventTrigger>();
                var routes = layout.Find("ComboRoutes").gameObject;
                var confirm = layout.Find("Confirm").GetComponent<Button>();
                Assert.False(routes.activeSelf);
                Assert.False(confirm.interactable);
                first.OnPointerEnter(null);
                Assert.True(routes.activeSelf);
                first.OnPointerClick(null);
                first.OnPointerExit(null);
                Assert.True(routes.activeSelf);
                Assert.True(confirm.interactable);
                Assert.AreEqual(0, calls);
                if (takeCard)
                    first.OnPointerClick(null);
                else
                {
                    layout.Find("Choice2").GetComponent<EventTrigger>().OnPointerClick(null);
                    Assert.False(routes.activeSelf);
                    confirm.onClick.Invoke();
                }
                confirm.onClick.Invoke();
                Assert.AreEqual(1, calls);
                Assert.AreSame(takeCard ? card : null, picked);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ComboWindowCoversEveryDirectedPairAndInspectsWithoutGrantingReward()
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var party = new RunPartyState();
                var card = DeckCardCatalog.Get("iai");
                int grants = 0;
                root.AddComponent<DeckRewardView>().Build(new[] { card }, party, 18, "", _ => grants++);
                var choices = root.transform.Find("Choices");
                Click(choices.Find("Choice1"));
                Click(choices.Find("Inspect_iai"));
                var window = root.transform.Find("ComboWindow");
                Assert.False(choices.GetComponent<CanvasGroup>().blocksRaycasts);
                choices.Find("Confirm").GetComponent<Button>().onClick.Invoke();
                var rows = window.Find("Window/Page/RouteList/Rows");
                var source = rows.Find("Origin_iai/Source");
                Assert.IsNotNull(source.Find("Skill_iai"));
                Assert.IsNotNull(source.Find("Skill_thrust"));
                Assert.IsNotNull(source.Find("Skill_quick_slash_r"));
                var next = rows.Find("Origin_iai/Result_slash_b");
                Assert.IsNotNull(next.Find("Skill_slash_b"));
                Assert.IsNotNull(next.Find("Skill_quick_guard_b"));
                Assert.IsNull(next.Find("Skill_quick_slash_r"));
                Assert.IsNotNull(rows.Find("Origin_slash_b/Result_iai/Skill_thrust"));
                Assert.AreEqual(6, rows.childCount);
                Click(window.Find("Window/Page/全スキル"));
                var page = window.Find("Window/Page");
                var ids = party.DeckCardIds.Append(card.Id).Distinct().ToArray();
                foreach (var from in ids)
                {
                    var routes = DeckComboRoutes.Find(DeckCardCatalog.Get(from), ids)
                        .Where(r => r.From.Id == from).ToDictionary(r => r.To.Id);
                    foreach (var to in ids)
                    {
                        var cell = page.Find($"Pair_{from}_{to}");
                        Assert.IsNotNull(cell);
                        Assert.AreEqual(routes.ContainsKey(to), cell.GetComponent<EventTrigger>() != null);
                    }
                }
                var link = page.Find("Pair_iai_thrust");
                link.GetComponent<EventTrigger>().OnPointerEnter(null);
                Assert.True(page.GetComponentsInChildren<Text>().Any(t => t.text.Contains("5ダメージ")));
                Assert.True(page.GetComponentsInChildren<Text>().Any(t => t.text.Contains("同時発動")));
                Click(link);
                Assert.IsNotNull(window.Find("Window/Page/RouteList/Rows/Origin_thrust/Source/Skill_thrust"));
                Click(window.Find("Window/Page/‹  前のスキル"));
                Assert.IsNotNull(window.Find("Window/Page/RouteList/Rows/Origin_iai/Source/Skill_iai"));
                Click(window.Find("Window/Page/閉じる  ×"));
                Assert.True(choices.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.True(choices.Find("Confirm").GetComponent<Button>().interactable);
                Assert.AreEqual(0, grants);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PatternsShowIntermediateAndSimultaneousSkillsAcrossContinuations()
        {
            var skills = DeckCardCatalog.All.Values.ToList();
            var candidate = DeckCardCatalog.Get("iai");
            var prefix = new List<TypingRoguelike.Battle.ElementColor>();
            var hand = skills.Select((s, i) => new DeckHandSlot { CardId = s.Id, HandIndex = i }).ToList();
            for (int window = 0; window < 2; window++)
            {
                var lines = DeckComboPatterns.Build(skills, prefix, candidate);
                Assert.Greater(lines.Count, 2);
                foreach (var line in lines)
                {
                    var stock = new TypingRoguelike.Battle.OrbStock();
                    foreach (var c in prefix) stock.Push(c);
                    Assert.LessOrEqual(line.Inputs.Length, DeckComboPatterns.Window);
                    for (int i = 0; i < line.Inputs.Length; i++)
                    {
                        stock.Push(line.Inputs[i]);
                        CollectionAssert.AreEqual(DeckCardResolver.ResolveTriggered(hand, stock).Select(s => s.CardId),
                            line.Matches[i].Select(s => s.Id));
                    }
                }
                Assert.True(lines.Any(line => line.Matches.Any(step =>
                    step.Contains(candidate) && step.Any(s => s.Id == "thrust") && step.Any(s => s.Id == "quick_slash_r"))));
                prefix.AddRange(lines.First(line => line.CanContinue && line.Matches.Last().Any(s => s.Id == "iai")).Inputs);
            }
        }

        static void Click(Transform item) => item.GetComponent<EventTrigger>().OnPointerClick(null);

        [Test]
        public void RestoredRewardEntersBattleAndNewRunResetsHistory()
        {
            var party = new RunPartyState();
            var ids = DeckCardCatalog.BuildStarterDeckIds();
            var startCount = ids.Count;
            ids.Add("thrust");
            party.RestoreDeck(ids, new List<string> { "iai,thrust" });
            var battle = new DeckBattleState(party.DeckCardIds, 42);
            Assert.AreEqual(startCount + 1, battle.DrawCount + battle.DiscardCount + battle.Hand.Sum(slot => slot.StackCount));
            Assert.AreEqual("iai,thrust", party.RewardHistory.Single());
            party.RestoreDeck(null, null);
            Assert.AreEqual(startCount, party.DeckCardIds.Count);
            Assert.IsEmpty(party.RewardHistory);
        }
    }
}
