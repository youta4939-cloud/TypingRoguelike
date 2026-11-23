using System.IO;
using NUnit.Framework;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Tests
{
    public sealed class AxeSkillTests
    {
        AxeSkillConfig _config;
        FakeAxeBattle _battle;
        AxeSkillRunner _runner;

        [SetUp]
        public void LoadJson()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Resources/Battle/Skills/axe_skills_v2.json");
            _config = AxeSkillConfig.Parse(File.ReadAllText(path));
            AxeSkillConfig.Shared = _config;
            _battle = new FakeAxeBattle();
            _runner = new AxeSkillRunner(_battle, _config);
        }

        [TearDown]
        public void ClearShared() => AxeSkillConfig.Shared = null;

        [Test]
        public void JsonHasTwelveSkillsAndTheTablePatterns()
        {
            Assert.AreEqual(12, _config.skills.Length);
            Assert.AreEqual(200, _config.skills[0].skillId);
            Assert.AreEqual(211, _config.skills[11].skillId);
            Assert.IsFalse(_config.options.useOptionalPatternSwap);
            Expect("yoroi_kudaki", "blue", "purple");
            Expect("ken_tachi", "blue", "green");
            Expect("jinarashi", "yellow", "green");
            Expect("kabuto_wari", "blue", "red");
            Expect("tsuige", "blue", "blue", "red");
            Expect("bougyo_taisei", "blue", "green", "blue");
            Expect("kyogeki", "blue", "purple", "purple", "red");
            Expect("goufu_ichigeki", "red", "blue", "purple");
            Expect("kishu", "blue", "red", "purple");
            Expect("juso", "purple", "purple", "blue");
            Expect("danzai", "blue", "red", "red", "red", "yellow");
            Expect("berserk", "blue", "purple", "purple", "purple", "blue");
            Assert.AreEqual("blue", _config.PatternOf(_config.Find("kyogeki"))[0]);
            Assert.AreEqual(0.15f, AxeSkillConfig.At(_config.Find("yoroi_kudaki").damagePercent, 1), 0.0001f);
            Assert.AreEqual(0.30f, AxeSkillConfig.At(_config.Find("yoroi_kudaki").damagePercent, 3), 0.0001f);
            Assert.AreEqual(0.20f, AxeSkillConfig.At(_config.Find("yoroi_kudaki").vuln, 3), 0.0001f);
            Assert.AreEqual(20f, AxeSkillConfig.At(_config.Find("yoroi_kudaki").vulnDurationSec, 1), 0.0001f);
            Assert.AreEqual(30f, AxeSkillConfig.At(_config.Find("juso").vulnDurationSec, 3), 0.0001f);
            Assert.AreEqual(15f, AxeSkillConfig.At(_config.Find("ken_tachi").durationSec, 1), 0.0001f);
            Assert.AreEqual(20f, AxeSkillConfig.At(_config.Find("jinarashi").durationSec, 3), 0.0001f);
        }

        [Test]
        public void YoroiLevelThreeUsesTheJsonPercent()
        {
            var data = Skill("yoroi_kudaki", 3);
            Assert.AreEqual(0.30f, data.DamagePercent, 0.0001f);
            Assert.AreEqual(0, data.Damage);
            Assert.AreEqual("攻撃力の30%のダメージ。敵の被ダメージ+20%（20秒）", data.EffectText);
        }

        [Test]
        public void VulnerabilityStacksBySourceAndBerserkIsSeparate()
        {
            Hit("yoroi_kudaki", 3);
            Assert.AreEqual(36f, 30f * _runner.Status.IncomingMultiplier, 0.01f);
            Hit("juso", 3);
            Assert.AreEqual(42f, 30f * _runner.Status.IncomingMultiplier, 0.01f);
            Hit("berserk", 3);
            Assert.AreEqual(51f, 30f * _runner.Status.IncomingMultiplier, 0.01f);
        }

        [Test]
        public void RecastingYoroiOverwritesItsOwnPercentAndKeepsTheLongerTime()
        {
            Hit("yoroi_kudaki", 3);
            _runner.Tick(5f);
            Assert.AreEqual(15f, _runner.Status.SourceRemaining("yoroi_kudaki"), 0.01f);
            Hit("yoroi_kudaki", 1);
            Assert.AreEqual(0.10f, _runner.Status.SourcePercent("yoroi_kudaki"), 0.0001f);
            Assert.AreEqual(20f, _runner.Status.SourceRemaining("yoroi_kudaki"), 0.01f);
        }

        [Test]
        public void KabutoAndGoufuUseDebuffKindsSeparatelyFromVulnerability()
        {
            var plain = Hit("kabuto_wari", 3);
            Assert.AreEqual(50f, plain.Damage, 0.01f);
            Hit("yoroi_kudaki", 3);
            var boosted = Hit("kabuto_wari", 3);
            Assert.AreEqual(85f, boosted.Damage, 0.01f);
            Assert.AreEqual(102f, boosted.Damage * boosted.IncomingAtHit, 0.01f);

            Hit("jinarashi", 1);
            var goufu = Hit("goufu_ichigeki", 3);
            Assert.AreEqual(2, _runner.Status.DebuffKindCount);
            Assert.AreEqual(72f, goufu.Damage, 0.01f);
        }

        [Test]
        public void KishuuChecksCurrentHpAndKyogekiAddsFrenzy()
        {
            _battle.HpRatio = 1f;
            Assert.AreEqual(204f, Hit("kishu", 3).Damage, 0.01f);
            _battle.HpRatio = 0.5f;
            Assert.AreEqual(120f, Hit("kishu", 3).Damage, 0.01f);
            var rage = Hit("kyogeki", 3);
            Assert.AreEqual(120f, rage.Damage, 0.01f);
            Assert.AreEqual(1, _battle.Frenzy);
        }

        [Test]
        public void DanzaiUsesMadnessAndReducesItOnlyOnTheKillingBlow()
        {
            _battle.Madness = 0;
            var zero = Hit("danzai", 3);
            Assert.AreEqual(0f, zero.Damage, 0.01f);
            Assert.AreEqual(0, _battle.Madness);

            _battle.Madness = 500;
            _battle.Hp = 10000f;
            Assert.AreEqual(100f, Hit("danzai", 1).Damage, 0.01f);
            Assert.AreEqual(200f, Hit("danzai", 3).Damage, 0.01f);
            Assert.AreEqual(500, _battle.Madness);

            _battle.Hp = 200f;
            Hit("danzai", 3);
            Assert.AreEqual(400, _battle.Madness);

            _config.options.danzaiReducesMadness = false;
            _battle.Madness = 500;
            _battle.Hp = 200f;
            Hit("danzai", 3);
            Assert.AreEqual(500, _battle.Madness);
        }

        [Test]
        public void AttackDownSlowAndGuardMatchTheTable()
        {
            Hit("ken_tachi", 3);
            var attack = 25f;
            _runner.ApplyAttackDown(ref attack);
            Assert.AreEqual(17.5f, attack, 0.01f);

            Hit("jinarashi", 1);
            Assert.AreEqual(0.5f, _runner.Status.SpeedMultiplier, 0.001f);

            Hit("bougyo_taisei", 3);
            _runner.Status.Clear();
            var first = 100f;
            _runner.ApplyOwnHit(ref first);
            Assert.AreEqual(30f, first, 0.01f);
            var second = 100f;
            _runner.ApplyOwnHit(ref second);
            Assert.AreEqual(100f, second, 0.01f);
        }

        [Test]
        public void BerserkIsOncePerBattleUntilTheNextBattle()
        {
            var skill = Skill("berserk", 1);
            Assert.IsTrue(_runner.CanTrigger(skill));
            Hit("berserk", 1);
            Assert.IsFalse(_runner.CanTrigger(skill));
            var next = new AxeSkillRunner(_battle, _config);
            Assert.IsTrue(next.CanTrigger(skill));
        }

        [Test]
        public void ReplaceFlagHidesEitherTheOldLunaCardsOrTheAxeCards()
        {
            Assert.IsTrue(_config.HidesReward(7, "ルナ"));
            Assert.IsFalse(_config.HidesReward(200, "ルナ"));
            Assert.IsFalse(_config.HidesReward(4, "エレン"));
            _config.options.replaceTargetSkills = false;
            Assert.IsTrue(_config.HidesReward(200, "ルナ"));
            Assert.IsFalse(_config.HidesReward(7, "ルナ"));
        }

        void Expect(string key, params string[] colors)
        {
            var pattern = _config.Find(key).pattern;
            Assert.AreEqual(colors.Length, pattern.Length, key);
            for (var i = 0; i < colors.Length; i++)
                Assert.AreEqual(colors[i], pattern[i], key);
        }

        AxeCast Hit(string key, int level) => _runner.OnSkillFired(Skill(key, level));

        BattleSkillData Skill(string key, int level)
        {
            var entry = _config.Find(key);
            var asset = ScriptableObject.CreateInstance<SkillDefinition>();
            asset.logicKey = key;
            asset.skillId = entry.skillId;
            asset.displayName = entry.nameJa;
            asset.characterName = "ルナ";
            asset.pattern = SwordsmanSkillConfig.ToPattern(entry.pattern);
            return asset.ToRuntimeData(level, 100);
        }

        sealed class FakeAxeBattle : IAxeBattle
        {
            public int Madness { get; set; }
            public float Hp = 10000f;
            public float HpRatio = 1f;
            public int Frenzy;

            public float EnemyHp => Hp;
            public float EnemyHpRatio => HpRatio;
            public float PlayerDamageMultiplier => 1f;
            public int AxeBaseAttack => 100;

            public void ChangeMadness(int delta) => Madness = Mathf.Max(0, Madness + delta);

            public bool AddFrenzy(int count)
            {
                Frenzy += count;
                return true;
            }
        }
    }
}
