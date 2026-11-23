using System.IO;
using System.Reflection;
using NUnit.Framework;
using TypingRoguelike.Battle;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Tests
{
    public sealed class FocusCarryTests
    {
        SwordsmanSkillConfig _config;

        [SetUp]
        public void LoadV9()
        {
            _config = Read("swordsman_skills_v9.json");
            SwordsmanSkillConfig.Shared = _config;
        }

        [TearDown]
        public void ClearShared()
        {
            SwordsmanSkillConfig.Shared = null;
        }

        [Test]
        public void V9KeepsNiNoTachiAndAddsTheCarryOptions()
        {
            var v7 = Read("swordsman_skills_v7.json");
            var oldNi = v7.Find("ni_no_tachi");
            var ni = _config.Find("ni_no_tachi");
            CollectionAssert.AreEqual(oldNi.pattern, ni.pattern);
            CollectionAssert.AreEqual(oldNi.damagePercent, ni.damagePercent);
            CollectionAssert.AreEqual(oldNi.chainFocusAdd, ni.chainFocusAdd);
            Assert.AreEqual("blue", ni.pattern[0]);
            Assert.AreEqual("red", ni.pattern[1]);
            Assert.AreEqual("purple", ni.pattern[2]);

            Assert.AreEqual("startPlusGain", _config.options.focusGainMode);
            Assert.AreEqual(5, _config.options.focusCarryDecay);
            Assert.AreEqual(15, _config.options.focusCarryCap);
            Assert.IsTrue(_config.options.kyokuRequiresCast);
            Assert.AreEqual(20, _config.options.kyokuNeedsHeld);
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, _config.Find("ichi_no_tachi").focusAdd);
        }

        [Test]
        public void IchiNoTachiAddsOneFocusAtEveryLevel()
        {
            var battle = new FakeBattle();
            var runner = new SwordsmanSkillRunner(battle, _config);
            runner.BeginBattle(0);
            var lv1 = Hit(runner, "ichi_no_tachi", 1);
            Assert.AreEqual(54f, lv1.Damage, 0.01f);
            Assert.AreEqual(1, battle.FocusStacks);
            Assert.AreEqual(1, runner.State.FocusGainedThisBattle);

            var lv3Battle = new FakeBattle();
            var lv3 = new SwordsmanSkillRunner(lv3Battle, _config);
            lv3.BeginBattle(0);
            var hit = Hit(lv3, "ichi_no_tachi", 3);
            Assert.AreEqual(36f, hit.Damage, 0.01f);
            Assert.AreEqual(1, lv3Battle.FocusStacks);
            Assert.AreEqual(1, lv3.State.FocusGainedThisBattle);
        }

        [Test]
        public void BattleEndKeepsHeldMinusDecayUpToTheCap()
        {
            var runner = new SwordsmanSkillRunner(new FakeBattle(), _config);
            Assert.AreEqual(3, runner.ComputeCarry(8));
            Assert.AreEqual(0, runner.ComputeCarry(3));
            Assert.AreEqual(15, runner.ComputeCarry(20));
            Assert.AreEqual(0, runner.ComputeCarry(0));
            Assert.AreEqual("集中を3残した（-5、上限15）", runner.CarryMessage(3));
        }

        [Test]
        public void NextBattleStartsWithCarriedFocusAndDoesNotCountItAsGain()
        {
            var battle = new FakeBattle();
            var runner = new SwordsmanSkillRunner(battle, _config);
            runner.BeginBattle(10);
            Assert.AreEqual(10, battle.FocusStacks);
            Assert.AreEqual(10, runner.State.StartFocus);
            Assert.AreEqual(0, runner.State.FocusGainedThisBattle);
            Assert.IsTrue(runner.HasGainedFocus(10));

            Hit(runner, "ichi_no_tachi", 1);
            Assert.AreEqual(11, battle.FocusStacks);
            Assert.AreEqual(1, runner.State.FocusGainedThisBattle);
        }

        [Test]
        public void BattleTotalModeIgnoresTheStartingFocus()
        {
            _config.options.focusGainMode = "battleTotal";
            var runner = new SwordsmanSkillRunner(new FakeBattle(), _config);
            runner.BeginBattle(10);
            Assert.IsFalse(runner.HasGainedFocus(10));
            runner.AddFocus(10, true, false);
            Assert.IsTrue(runner.HasGainedFocus(10));
        }

        [Test]
        public void SanNoTachiUsesStartPlusGainEvenAfterFocusIsSpent()
        {
            var ready = new SwordsmanSkillRunner(new FakeBattle(), _config);
            ready.BeginBattle(10);
            Hit(ready, "ni_no_tachi", 1);
            Hit(ready, "san_no_tachi", 1);
            Assert.AreEqual(3, ((FakeBattle)BattleOf(ready)).CardCount("issen_kiwami"));

            var shortStart = new SwordsmanSkillRunner(new FakeBattle(), _config);
            shortStart.BeginBattle(9);
            Hit(shortStart, "ni_no_tachi", 1);
            Hit(shortStart, "san_no_tachi", 1);
            Assert.AreEqual(0, ((FakeBattle)BattleOf(shortStart)).CardCount("issen_kiwami"));

            var afterIchi = new SwordsmanSkillRunner(new FakeBattle(), _config);
            afterIchi.BeginBattle(9);
            Hit(afterIchi, "ichi_no_tachi", 1);
            Assert.AreEqual(1, afterIchi.State.FocusGainedThisBattle);
            Assert.IsTrue(afterIchi.HasGainedFocus(10));
            Hit(afterIchi, "ni_no_tachi", 1);
            Hit(afterIchi, "san_no_tachi", 1);
            Assert.AreEqual(3, ((FakeBattle)BattleOf(afterIchi)).CardCount("issen_kiwami"));

            var spent = new SwordsmanSkillRunner(new FakeBattle(), _config);
            spent.BeginBattle(10);
            Hit(spent, "ni_no_tachi", 1);
            Hit(spent, "tsui_no_tachi", 1);
            Assert.AreEqual(0, ((FakeBattle)BattleOf(spent)).FocusStacks);
            Hit(spent, "ni_no_tachi", 1);
            Hit(spent, "san_no_tachi", 1);
            Assert.AreEqual(3, ((FakeBattle)BattleOf(spent)).CardCount("issen_kiwami"));
        }

        [Test]
        public void KiwamiNeedsTheKataCastAndTwentyHeldFocus()
        {
            var opened = new SwordsmanSkillRunner(new FakeBattle(), _config);
            opened.BeginBattle(15);
            Hit(opened, "kyokuchi_no_kata", 1);
            Assert.AreEqual(20, ((FakeBattle)BattleOf(opened)).FocusStacks);
            Assert.IsTrue(opened.State.Kiwami);
            Assert.AreEqual(5, ((FakeBattle)BattleOf(opened)).CardCount("issen_kiwami"));

            var mirrorOnly = new SwordsmanSkillRunner(new FakeBattle(), _config);
            mirrorOnly.BeginBattle(15);
            Hit(mirrorOnly, "meikyo_shisui", 3);
            Assert.AreEqual(20, ((FakeBattle)BattleOf(mirrorOnly)).FocusStacks);
            Assert.IsFalse(mirrorOnly.State.Kiwami);

            var later = new SwordsmanSkillRunner(new FakeBattle(), _config);
            later.BeginBattle(10);
            Hit(later, "kyokuchi_no_kata", 1);
            Assert.IsFalse(later.State.Kiwami);
            Hit(later, "meikyo_shisui", 3);
            Assert.AreEqual(20, ((FakeBattle)BattleOf(later)).FocusStacks);
            Assert.IsTrue(later.State.Kiwami);

            _config.options.kyokuRequiresCast = false;
            var heldOnly = new SwordsmanSkillRunner(new FakeBattle(), _config);
            heldOnly.BeginBattle(0);
            heldOnly.AddFocus(20, true, false);
            Assert.IsFalse(heldOnly.State.KataActive);
            Assert.IsTrue(heldOnly.State.Kiwami);
        }

        [Test]
        public void NewRunAndWipeClearCarryWhileTheSaveFieldKeepsIt()
        {
            var party = new RunPartyState();
            party.SetCarriedFocus(12);
            var saved = new MetaProgressData { towerRunFocus = party.CarriedFocus, towerRunBlood = 4 };
            var resumed = new RunPartyState();
            resumed.SetCarriedFocus(saved.towerRunFocus);
            Assert.AreEqual(12, resumed.CarriedFocus);
            Assert.AreEqual(4, saved.towerRunBlood);

            party.InitializeForNewDungeonRun(null);
            Assert.AreEqual(0, party.CarriedFocus);
            resumed.ResetCarriedFocus();
            Assert.AreEqual(0, resumed.CarriedFocus);
        }

        [Test]
        public void FocusSlotMentionsTheDecayFromJson()
        {
            var text = Describe(new ExSlot { Kind = ExKind.Focus, Count = 3 });
            Assert.IsTrue(text.Contains("戦闘終了後、集中-5"));

            _config.options.focusCarryDecay = 0;
            var quiet = Describe(new ExSlot { Kind = ExKind.Focus, Count = 3 });
            Assert.IsFalse(quiet.Contains("戦闘終了後"));
        }

        static string Describe(ExSlot slot)
        {
            var method = typeof(ExAreaView).GetMethod("Describe", BindingFlags.NonPublic | BindingFlags.Static);
            return (string)method.Invoke(null, new object[] { slot, null });
        }

        static SwordsmanSkillConfig Read(string file)
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/Battle/Skills", file);
            return SwordsmanSkillConfig.Parse(File.ReadAllText(path));
        }

        static SwordsmanCast Hit(SwordsmanSkillRunner runner, string key, int level)
        {
            var data = new BattleSkillData(100, "アリス", key, System.Array.Empty<ElementColor>(), 0, 0, "", logicKey: key, level: level);
            data.CasterAttack = 120;
            return runner.OnSkillFired(data);
        }

        static ISwordsmanBattle BattleOf(SwordsmanSkillRunner runner)
        {
            var field = typeof(SwordsmanSkillRunner).GetField("_battle", BindingFlags.NonPublic | BindingFlags.Instance);
            return (ISwordsmanBattle)field.GetValue(runner);
        }

        sealed class FakeBattle : ISwordsmanBattle
        {
            readonly ExAreaState _ex = new ExAreaState(5);
            public int FocusStacks => _ex.Stack(ExKind.Focus);
            public int FocusMax => 20;
            public int RerollCount => 0;
            public int AddFocusStacks(int count) => _ex.AddStacks(ExKind.Focus, count, 20);
            public int ConsumeFocus(int count) => _ex.ConsumeStacks(ExKind.Focus, count);
            public int ConsumeAllFocus()
            {
                var had = FocusStacks;
                _ex.ConsumeAll("focus");
                return had;
            }
            public void SetFocusStacks(int count) => _ex.SetStacks(ExKind.Focus, count, 20);
            public bool AddCard(string cardId, int count, int casterIndex, int bloodOnKill)
            {
                var ok = true;
                for (var i = 0; i < count; i++)
                    ok &= _ex.TryAddSpecial(Kind(cardId), casterIndex, bloodOnKill).Accepted;
                return ok;
            }
            public int CardCount(string cardId) => _ex.Stack(Kind(cardId));
            public SkillDamageResult Compute(BattleSkillData skill, float percent, float skillOwnMultiplier, bool forceCrit)
            {
                return CombatMath.ComputeSkillDamage(new SkillDamageRequest
                {
                    CasterAttack = skill.CasterAttack,
                    Percent = percent,
                    SkillOwnMultiplier = skillOwnMultiplier,
                    FinalMultiplier = 1f,
                    CritChance = 0f,
                    ForceCrit = forceCrit,
                    CritMultiplier = 1.5f,
                    Roll = 1f
                });
            }
            public void SetSkillBonus(float bonus) { }
            public void SetCritOverride(float multiplier) { }
            public void AddReroll(int amount, bool ignoreCap) { }
            public void Log(string message) { }

            static ExKind Kind(string cardId)
            {
                if (cardId == "battou")
                    return ExKind.Battou;
                if (cardId == "issen_kiwami")
                    return ExKind.IssenKiwami;
                return ExKind.HyakkiYakou;
            }
        }
    }
}
