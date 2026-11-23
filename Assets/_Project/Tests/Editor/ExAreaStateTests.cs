using NUnit.Framework;
using TypingRoguelike.Battle;

namespace TypingRoguelike.Tests
{
    public sealed class ExAreaStateTests
    {
        [Test]
        public void PushRules_Stacks_AndFrenzyTimer()
        {
            var error = ExAreaState.SelfCheck();
            Assert.IsNull(error, error);
        }

        [Test]
        public void FirstLetterOverlap_UsesEveryVariant()
        {
            Assert.IsTrue(TypingEngine.FirstLettersOverlap(new[] { "shi", "si" }, new[] { 's' }));
            Assert.IsFalse(TypingEngine.FirstLettersOverlap(new[] { "aka" }, new[] { 's' }));
        }

        [Test]
        public void DivergingLetter_LeavesTheOtherWord()
        {
            var engine = new TypingEngine(new[] { "人間|ningen" }, 0, 1);
            engine.Slots[0].DisplayText = "人間";
            engine.Slots[0].Text = "ningen";
            engine.Slots[0].InputOptions = new[] { "ningen" };
            engine.Slots[0].Active = true;
            engine.Slots[1].DisplayText = "星";
            engine.Slots[1].Text = "hoshi";
            engine.Slots[1].InputOptions = new[] { "hoshi" };
            engine.Slots[1].Active = true;
            engine.Slots[2].DisplayText = "雷";
            engine.Slots[2].Text = "kaminari";
            engine.Slots[2].InputOptions = new[] { "kaminari" };

            engine.ProcessKey('n');
            engine.ProcessKey('i');
            engine.ProcessKey('n');
            Assert.AreEqual(3, engine.Slots[0].Progress);
            Assert.IsTrue(engine.Slots[0].Active);

            var branch = engine.ProcessKey('j', false);
            Assert.IsFalse(branch.Advanced);
            Assert.AreEqual(0, engine.Slots[0].Progress);
            Assert.IsFalse(engine.Slots[0].Active);
            Assert.IsFalse(engine.Slots[1].Active);
        }
        [Test]
        public void OldestColorStockIsPushedOut()
        {
            var board = new ExAreaState(5);
            board.TryAddStatus(ExKind.Poison, 0, 3f, false);
            board.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            board.TryAddStatus(ExKind.Paralysis, 0, 4f, false);
            Assert.IsTrue(board.TryAddColor("red").Accepted);
            Assert.IsTrue(board.TryAddColor("red").Accepted);

            var blue = board.TryAddColor("blue");
            Assert.IsTrue(blue.Accepted);
            Assert.IsTrue(blue.Ejected);
            Assert.AreEqual("red", blue.EjectedColorId);
            Assert.AreEqual("red", board.Slots[3].ColorId);
            Assert.AreEqual("blue", board.Slots[4].ColorId);
        }

        [Test]
        public void FullBuffsRejectNewEntriesButStackTheSameKind()
        {
            var full = new ExAreaState(4);
            full.TryAddStatus(ExKind.Poison, 0, 3f, false);
            full.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            full.TryAddStatus(ExKind.Paralysis, 0, 4f, false);
            full.TryAddStatus(ExKind.Focus, 20, 0f, false);

            Assert.IsFalse(full.TryAddColor("red").Accepted);
            Assert.IsFalse(full.CanAccept(ExKind.ColorStock));
            Assert.IsTrue(full.TryAddStatus(ExKind.Poison, 0, 3f, false).Accepted);
            Assert.AreEqual(2, full.Stack(ExKind.Poison));
            Assert.AreEqual(4, full.Count);
        }

        [Test]
        public void FrenzyTimerRestartsOnStackBurstAndTypedWord()
        {
            var frenzy = new ExAreaState(5);
            frenzy.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            frenzy.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            Assert.AreEqual(2, frenzy.Stack(ExKind.Frenzy));
            Assert.AreEqual(1, frenzy.Count);
            Assert.AreEqual(15f, frenzy.Timer(ExKind.Frenzy), 0.001f);

            var hits = new System.Collections.Generic.List<ExTickHit>();
            frenzy.Tick(15f, 3f, 3, 4f, 2, 15f, 100, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(100, hits[0].Amount);
            Assert.AreEqual(1, frenzy.Stack(ExKind.Frenzy));
            Assert.AreEqual(15f, frenzy.Timer(ExKind.Frenzy), 0.001f);

            frenzy.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            frenzy.Slots[0].SetWord("炎", new[] { "honoo" });
            Assert.IsTrue(frenzy.ProcessKey('h', 15f).Advanced);
            frenzy.ProcessKey('o', 15f);
            frenzy.ProcessKey('n', 15f);
            frenzy.ProcessKey('o', 15f);
            var done = frenzy.ProcessKey('o', 15f);
            Assert.IsTrue(done.Completed);
            Assert.IsTrue(done.NeedsNewWord);
            Assert.AreEqual(1, frenzy.Stack(ExKind.Frenzy));
            Assert.AreEqual(15f, frenzy.Timer(ExKind.Frenzy), 0.001f);
        }

        [Test]
        public void PoisonDamageUsesStackAndFocusCanBeConsumed()
        {
            var poison = new ExAreaState(5);
            poison.TryAddStatus(ExKind.Poison, 0, 3f, false);
            poison.TryAddStatus(ExKind.Poison, 0, 3f, false);
            var hits = new System.Collections.Generic.List<ExTickHit>();
            poison.Tick(3f, 3f, 3, 4f, 2, 15f, 100, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(ExKind.Poison, hits[0].Kind);
            Assert.AreEqual(6, hits[0].Amount);

            var focus = new ExAreaState(5);
            for (var i = 0; i < 22; i++)
                focus.TryAddStatus(ExKind.Focus, 20, 0f, false);
            Assert.AreEqual(20, focus.Stack(ExKind.Focus));
            Assert.IsTrue(focus.ConsumeAll(ExIds.Focus));
            Assert.AreEqual(0, focus.Stack(ExKind.Focus));
            Assert.AreEqual(0, focus.Count);
        }
        [Test]
        public void SpecialCardsStackAndOnlyColorStockIsPushed()
        {
            var board = new ExAreaState(5);
            for (var i = 0; i < 5; i++)
                Assert.IsTrue(board.TryAddSpecial(ExKind.Battou, 0, 1).Accepted);
            Assert.AreEqual(1, board.Count);
            Assert.AreEqual(5, board.Stack(ExKind.Battou));

            var blocked = new ExAreaState(5);
            blocked.TryAddSpecial(ExKind.Battou, 0, 1);
            blocked.TryAddSpecial(ExKind.IssenKiwami, 1, 1);
            blocked.TryAddSpecial(ExKind.HyakkiYakou, 2, 1);
            blocked.TryAddStatus(ExKind.Focus, 20, 0f, false);
            blocked.TryAddStatus(ExKind.Poison, 0, 3f, false);
            Assert.IsFalse(blocked.TryAddColor("red").Accepted);
            Assert.IsTrue(blocked.TryAddSpecial(ExKind.Battou, 0, 1).Accepted);
            Assert.AreEqual(2, blocked.Stack(ExKind.Battou));
            Assert.AreEqual(5, blocked.Count);

            var withColor = new ExAreaState(5);
            withColor.TryAddColor("red");
            withColor.TryAddSpecial(ExKind.Battou, 0, 1);
            withColor.TryAddSpecial(ExKind.IssenKiwami, 0, 1);
            withColor.TryAddSpecial(ExKind.HyakkiYakou, 0, 1);
            withColor.TryAddStatus(ExKind.Focus, 20, 0f, false);
            var pushed = withColor.TryAddStatus(ExKind.Poison, 0, 3f, false);
            Assert.IsTrue(pushed.Accepted);
            Assert.IsTrue(pushed.Ejected);
            Assert.AreEqual("red", pushed.EjectedColorId);
            Assert.AreEqual(1, withColor.Stack(ExKind.Battou));
        }

        [Test]
        public void RedRefillPushesTheOldestRedAndNotSpecials()
        {
            var board = new ExAreaState(5);
            ExAddResult last = default;
            for (var i = 0; i < 6; i++)
                last = board.TryAddColor("red");
            Assert.IsTrue(last.Accepted);
            Assert.IsTrue(last.Ejected);
            Assert.AreEqual(5, board.Count);

            var mixed = new ExAreaState(2);
            mixed.TryAddSpecial(ExKind.Battou, 0, 1);
            mixed.TryAddColor("red");
            var next = mixed.TryAddColor("blue");
            Assert.IsTrue(next.Ejected);
            Assert.AreEqual("red", next.EjectedColorId);
            Assert.AreEqual(1, mixed.Stack(ExKind.Battou));
        }

        [Test]
        public void BattouConsumeAllClearsTheSlotAndOneLeavesTheRest()
        {
            var all = new ExAreaState(3);
            for (var i = 0; i < 4; i++)
                all.TryAddSpecial(ExKind.Battou, 0, 1);
            all.Slots[0].ConsumeAllOnType = true;
            all.Slots[0].SetWord("抜刀", new[] { "aa" });
            Assert.IsTrue(all.ProcessKey('a', 0f).Advanced);
            var done = all.ProcessKey('a', 0f);
            Assert.IsTrue(done.Completed);
            Assert.AreEqual(4, done.StackCount);
            Assert.AreEqual(0, all.Stack(ExKind.Battou));
            Assert.AreEqual(0.7f, SpecialCardMath.Percent(0.3f, 0.1f, done.StackCount), 0.001f);

            var one = new ExAreaState(3);
            for (var i = 0; i < 4; i++)
                one.TryAddSpecial(ExKind.Battou, 0, 1);
            one.Slots[0].SetWord("抜刀", new[] { "aa" });
            one.ProcessKey('a', 0f);
            var left = one.ProcessKey('a', 0f);
            Assert.IsTrue(left.Completed);
            Assert.AreEqual(4, left.StackCount);
            Assert.AreEqual(3, one.Stack(ExKind.Battou));
        }

        [Test]
        public void BloodCapAndIssenCritUseTheSharedFormulas()
        {
            Assert.AreEqual(11, SpecialCardMath.AddBlood(10, 1, 0));
            Assert.AreEqual(20, SpecialCardMath.AddBlood(20, 1, 20));
            Assert.AreEqual(0.6f, SpecialCardMath.Percent(0.2f, 0.04f, 10), 0.001f);

            var request = new SkillDamageRequest
            {
                CasterAttack = 120,
                Percent = 0.5f,
                SkillOwnMultiplier = 1f,
                FinalMultiplier = 1f,
                CritChance = 0f,
                ForceCrit = true,
                CritMultiplier = 1.5f,
                Roll = 0.9f
            };
            var hit = CombatMath.ComputeSkillDamage(request);
            Assert.IsTrue(hit.IsCrit);
            Assert.AreEqual(90f, hit.Damage, 0.001f);
        }
    }
}
