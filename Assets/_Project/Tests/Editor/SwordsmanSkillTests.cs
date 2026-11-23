using System.IO;
using NUnit.Framework;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Tests
{
    public sealed class SwordsmanSkillTests
    {
        SwordsmanSkillConfig _config;

        [SetUp]
        public void LoadJson()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/Battle/Skills/swordsman_skills_v7.json");
            _config = SwordsmanSkillConfig.Parse(File.ReadAllText(path));
            SwordsmanSkillConfig.Shared = _config;
        }

        [Test]
        public void JsonHasTwelveSkillsAndTheTableNumbers()
        {
            Assert.AreEqual(12, _config.skills.Length);
            Assert.AreEqual(100, _config.skills[0].skillId);
            Assert.AreEqual(111, _config.skills[11].skillId);
            Assert.AreEqual("red", _config.Find("ichi_no_tachi").pattern[0]);
            Assert.AreEqual("blue", _config.Find("ichi_no_tachi").pattern[1]);
            Assert.AreEqual(0.45f, SwordsmanSkillConfig.At(_config.Find("ichi_no_tachi").damagePercent, 1), 0.0001f);
            Assert.AreEqual(0.30f, SwordsmanSkillConfig.At(_config.Find("ichi_no_tachi").damagePercent, 3), 0.0001f);
            Assert.AreEqual(3, SwordsmanSkillConfig.At(_config.Find("onagi").focusThreshold, 1));
            Assert.AreEqual(1, SwordsmanSkillConfig.At(_config.Find("onagi").focusThreshold, 3));
            Assert.AreEqual(5, _config.Find("kyokuchi_no_kata").kiwamiAddCount);
        }

        [Test]
        public void IchiNoTachiLevelThreeUsesTheJsonPercent()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.logicKey = "ichi_no_tachi";
            skill.displayName = "一ノ太刀";
            skill.characterName = "アリス";
            skill.pattern = new[] { ElementColor.Red, ElementColor.Blue };
            var data = skill.ToRuntimeData(3, 120);
            Assert.AreEqual(0.30f, data.DamagePercent, 0.0001f);
            Assert.AreEqual(0, data.Damage);
            Assert.AreEqual(3, data.Level);
            Assert.AreEqual("攻撃力の30%のダメージ", SkillProgression.Describe(skill, 3));
        }

        [Test]
        public void NormalSixMatchTheTable()
        {
            var battle = new FakeBattle();
            var runner = new SwordsmanSkillRunner(battle, _config);

            var lv1 = Hit(runner, "ichi_no_tachi", 1);
            Assert.AreEqual(54f, lv1.Damage, 0.01f);
            var lv3 = Hit(runner, "ichi_no_tachi", 3);
            Assert.AreEqual(36f, lv3.Damage, 0.01f);

            runner.SetFocus(2);
            var plain = Hit(runner, "onagi", 1);
            Assert.AreEqual(36f, plain.Damage, 0.01f);
            Assert.AreEqual(2, battle.FocusStacks);
            runner.SetFocus(5);
            var boosted = Hit(runner, "onagi", 1);
            Assert.AreEqual(72f, boosted.Damage, 0.01f);
            Assert.AreEqual(2, battle.FocusStacks);
            _config.options.onagiFocusConsume = "all";
            runner.SetFocus(5);
            Hit(runner, "onagi", 1);
            Assert.AreEqual(0, battle.FocusStacks);
            _config.options.onagiFocusConsume = "threshold";
            runner.SetFocus(1);
            var lv3Onagi = Hit(runner, "onagi", 3);
            Assert.AreEqual(120f, lv3Onagi.Damage, 0.01f);
            Assert.AreEqual(0, battle.FocusStacks);

            var gained = runner.State.FocusGainedThisBattle;
            Hit(runner, "meikyo_shisui", 2);
            Assert.AreEqual(4, battle.FocusStacks);
            Assert.AreEqual(gained + 4, runner.State.FocusGainedThisBattle);
            runner.SetFocus(18);
            var before = runner.State.FocusGainedThisBattle;
            Hit(runner, "meikyo_shisui", 2);
            Assert.AreEqual(20, battle.FocusStacks);
            Assert.AreEqual(before + 4, runner.State.FocusGainedThisBattle);
            _config.options.focusGainCountsOverflow = false;
            runner.SetFocus(18);
            before = runner.State.FocusGainedThisBattle;
            Hit(runner, "meikyo_shisui", 2);
            Assert.AreEqual(before + 2, runner.State.FocusGainedThisBattle);

            Hit(runner, "kobu", 1);
            var boostedIchi = battle.Compute(Alice("ichi_no_tachi", 1), 0.15f, 1f, false);
            Assert.AreEqual(20.7f, boostedIchi.Damage, 0.05f);
            var ally = battle.Compute(Ellen(), 0.15f, 1f, false);
            Assert.AreEqual(18f, ally.Damage, 0.01f);
            Assert.AreEqual(30f, CombatMath.WordDamage(300, 0.1f), 0.01f);
            runner.Tick(30f);
            var back = battle.Compute(Alice("ichi_no_tachi", 1), 0.15f, 1f, false);
            Assert.AreEqual(18f, back.Damage, 0.01f);

            battle.Rerolls = 3;
            Hit(runner, "tatenaoshi", 2);
            Assert.AreEqual(5, battle.Rerolls);
            var first = 20f;
            runner.OnEnemyHit(ref first, false);
            Assert.AreEqual(15f, first, 0.01f);
            var second = 20f;
            runner.OnEnemyHit(ref second, false);
            Assert.AreEqual(20f, second, 0.01f);
            _config.options.tateRerollIgnoresCap = false;
            battle.Rerolls = 3;
            Hit(runner, "tatenaoshi", 2);
            Assert.AreEqual(3, battle.Rerolls);

            Hit(runner, "noutou", 3);
            Assert.AreEqual(3, battle.CardCount("red_refill"));
        }

        [Test]
        public void ChainScopeAndOncePerBattle()
        {
            var battle = new FakeBattle();
            var runner = new SwordsmanSkillRunner(battle, _config);
            Hit(runner, "ichi_no_tachi", 1);
            runner.OnAllySkillFired(Ellen());
            Assert.AreEqual("ichi_no_tachi", runner.State.LastFiredKey);
            _config.options.chainScope = "party";
            runner.OnAllySkillFired(Ellen());
            Assert.AreEqual("skill:4", runner.State.LastFiredKey);

            var iai = new SwordsmanSkillRunner(battle, _config);
            Hit(iai, "iai_no_kamae", 1);
            Assert.IsFalse(iai.CanTrigger(Alice("iai_no_kamae", 1)));
            Assert.IsTrue(new SwordsmanSkillRunner(battle, _config).CanTrigger(Alice("iai_no_kamae", 1)));
        }

        [Test]
        public void RareAndSpecialFollowTheChainAndKiwami()
        {
            var battle = new FakeBattle();
            var runner = new SwordsmanSkillRunner(battle, _config);
            Hit(runner, "ichi_no_tachi", 1);
            var chained = Hit(runner, "ni_no_tachi", 1);
            Assert.AreEqual(24f, chained.Damage, 0.01f);
            Assert.AreEqual(1, battle.FocusStacks);
            Hit(runner, "meikyo_shisui", 1);
            var noChain = Hit(runner, "ni_no_tachi", 1);
            Assert.AreEqual(24f, noChain.Damage, 0.01f);
            Assert.AreEqual(4, battle.FocusStacks);

            runner.AddGained(10);
            Hit(runner, "ni_no_tachi", 1);
            Hit(runner, "san_no_tachi", 1);
            Assert.AreEqual(3, battle.CardCount("issen_kiwami"));
            Hit(runner, "ni_no_tachi", 1);
            Hit(runner, "san_no_tachi", 1);
            Assert.AreEqual(6, battle.CardCount("issen_kiwami"));

            var fresh = new FakeBattle();
            var shortGain = new SwordsmanSkillRunner(fresh, _config);
            shortGain.AddGained(9);
            Hit(shortGain, "ni_no_tachi", 1);
            Hit(shortGain, "san_no_tachi", 1);
            Assert.AreEqual(0, fresh.CardCount("issen_kiwami"));

            runner.SetFocus(10);
            var tsui = Hit(runner, "tsui_no_tachi", 1);
            Assert.AreEqual(360f, tsui.Damage, 0.5f);
            Assert.AreEqual(0, battle.FocusStacks);
            runner.SetFocus(20);
            var tsui3 = Hit(runner, "tsui_no_tachi", 3);
            Assert.AreEqual(1188f, tsui3.Damage, 0.5f);

            var kiwami = new SwordsmanSkillRunner(new FakeBattle(), _config);
            kiwami.SetFocus(10);
            kiwami.EnterKiwami();
            var tsuiKiwami = Hit(kiwami, "tsui_no_tachi", 1);
            Assert.AreEqual(480f, tsuiKiwami.Damage, 0.5f);
            var board = new FakeBattle();
            var marked = new SwordsmanSkillRunner(board, _config);
            marked.EnterKiwami();
            var aliceIssen = board.Compute(Alice("issen", 1), 0.5f, 1f, true);
            var ellenIssen = board.Compute(Ellen(), 0.5f, 1f, true);
            Assert.AreEqual(120f, aliceIssen.Damage, 0.5f);
            Assert.AreEqual(90f, ellenIssen.Damage, 0.5f);

            var kata = new SwordsmanSkillRunner(new FakeBattle(), _config);
            Hit(kata, "kyokuchi_no_kata", 1);
            Assert.AreEqual(5, kata.State.FocusGainedThisBattle);
            Assert.IsFalse(kata.State.Kiwami);
            kata.AddGained(15);
            Assert.IsTrue(kata.State.Kiwami);
            Assert.AreEqual(5, ((FakeBattle)GetBattle(kata)).CardCount("issen_kiwami"));
            var cards = ((FakeBattle)GetBattle(kata)).CardCount("issen_kiwami");
            kata.AddGained(10);
            Assert.AreEqual(cards, ((FakeBattle)GetBattle(kata)).CardCount("issen_kiwami"));

            var ready = new SwordsmanSkillRunner(new FakeBattle(), _config);
            ready.AddGained(5);
            Hit(ready, "kyokuchi_no_kata", 3);
            Assert.IsTrue(ready.State.Kiwami);

            var iai = new SwordsmanSkillRunner(new FakeBattle(), _config);
            Hit(iai, "iai_no_kamae", 2);
            Assert.AreEqual(1, ((FakeBattle)GetBattle(iai)).CardCount("battou"));
            iai.OnAllySkillFired(Ellen());
            iai.OnAllySkillFired(Ellen());
            Assert.AreEqual(5, ((FakeBattle)GetBattle(iai)).CardCount("battou"));
            Assert.IsFalse(iai.CanTrigger(Alice("iai_no_kamae", 2)));

            var blood = new FakeBattle();
            Hit(new SwordsmanSkillRunner(blood, _config), "youtou_kaihou", 3);
            Assert.AreEqual(3, blood.LastBlood);
        }

        static SwordsmanCast Hit(SwordsmanSkillRunner runner, string key, int level) =>
            runner.OnSkillFired(Alice(key, level));

        static BattleSkillData Alice(string key, int level)
        {
            var data = new BattleSkillData(100, "アリス", key, System.Array.Empty<ElementColor>(), 0, 0, "", logicKey: key, level: level);
            data.CasterAttack = 120;
            return data;
        }

        static BattleSkillData Ellen()
        {
            var data = new BattleSkillData(4, "エレン", "ヒール", System.Array.Empty<ElementColor>(), 0, 0, "");
            data.CasterAttack = 120;
            return data;
        }

        static ISwordsmanBattle GetBattle(SwordsmanSkillRunner runner)
        {
            var field = typeof(SwordsmanSkillRunner).GetField("_battle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ISwordsmanBattle)field.GetValue(runner);
        }

        sealed class FakeBattle : ISwordsmanBattle
        {
            public readonly ExAreaState Ex = new ExAreaState(5);
            public float Bonus;
            public float CritOverride;
            public int Rerolls = 3;
            public int Max = 3;
            public int LastBlood = 1;

            public int FocusStacks => Ex.Stack(ExKind.Focus);
            public int FocusMax => 20;
            public int RerollCount => Rerolls;

            public int AddFocusStacks(int count) => Ex.AddStacks(ExKind.Focus, count, 20);
            public int ConsumeFocus(int count) => Ex.ConsumeStacks(ExKind.Focus, count);

            public int ConsumeAllFocus()
            {
                var had = FocusStacks;
                Ex.ConsumeAll("focus");
                return had;
            }

            public void SetFocusStacks(int count) => Ex.SetStacks(ExKind.Focus, count, 20);

            public bool AddCard(string cardId, int count, int casterIndex, int bloodOnKill)
            {
                LastBlood = bloodOnKill;
                var ok = true;
                for (var i = 0; i < count; i++)
                {
                    if (cardId == "red_refill")
                        ok &= Ex.TryAddColor("red").Accepted;
                    else
                        ok &= Ex.TryAddSpecial(Kind(cardId), casterIndex, bloodOnKill).Accepted;
                }
                return ok;
            }

            public int CardCount(string cardId)
            {
                if (cardId == "red_refill")
                {
                    var count = 0;
                    foreach (var slot in Ex.Slots)
                    {
                        if (slot.Kind == ExKind.ColorStock && slot.ColorId == "red")
                            count++;
                    }
                    return count;
                }
                return Ex.Stack(Kind(cardId));
            }

            public SkillDamageResult Compute(BattleSkillData skill, float percent, float skillOwnMultiplier, bool forceCrit)
            {
                var alice = StarterCharacters.Same(skill.CharacterName, StarterCharacters.Chara01);
                return CombatMath.ComputeSkillDamage(new SkillDamageRequest
                {
                    CasterAttack = skill.CasterAttack,
                    Percent = percent,
                    SkillOwnMultiplier = skillOwnMultiplier,
                    FinalMultiplier = alice && Bonus > 0f ? 1f + Bonus : 1f,
                    CritChance = 0f,
                    ForceCrit = forceCrit,
                    CritMultiplier = alice && CritOverride > 0f ? CritOverride : 1.5f,
                    Roll = 1f
                });
            }

            public void SetSkillBonus(float bonus) => Bonus = bonus;
            public void SetCritOverride(float multiplier) => CritOverride = multiplier;

            public void AddReroll(int amount, bool ignoreCap)
            {
                Rerolls += amount;
                if (!ignoreCap && Rerolls > Max)
                    Rerolls = Max;
            }

            public void Log(string message) { }

            static ExKind Kind(string cardId)
            {
                switch (cardId)
                {
                    case "battou": return ExKind.Battou;
                    case "issen_kiwami": return ExKind.IssenKiwami;
                    default: return ExKind.HyakkiYakou;
                }
            }
        }
    }
}
