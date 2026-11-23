using NUnit.Framework;
using TypingRoguelike.Battle;

namespace TypingRoguelike.Tests
{
    public sealed class CombatMathTests
    {
        [Test]
        public void WordDamage_UsesTotalAttackTimesCoefficient()
        {
            Assert.AreEqual(30f, CombatMath.WordDamage(300, 0.1f));
            Assert.AreEqual(1f, CombatMath.WordDamage(0, 0.1f));
        }

        [Test]
        public void SkillDamage_PercentThenOwnAndFinalMultipliers()
        {
            var baseHit = Hit(120, 0.3f, 1f, 1f, 0f, false, 1.5f, 0f);
            Assert.IsFalse(baseHit.IsCrit);
            Assert.AreEqual(36f, baseHit.Damage, 0.001f);

            var owned = Hit(120, 0.3f, 2f, 1f, 0f, false, 1.5f, 0f);
            Assert.AreEqual(72f, owned.Damage, 0.001f);

            var boosted = Hit(120, 0.3f, 1f, 1.15f, 0f, false, 1.5f, 0f);
            Assert.AreEqual(41.4f, boosted.Damage, 0.001f);
        }

        [Test]
        public void SkillDamage_CritOnlyWhenChanceOrForced()
        {
            var none = Hit(120, 0.3f, 1f, 1f, 0f, false, 1.5f, 0f);
            Assert.IsFalse(none.IsCrit);
            Assert.AreEqual(36f, none.Damage, 0.001f);

            var always = Hit(120, 0.3f, 1f, 1f, 1f, false, 1.5f, 0f);
            Assert.IsTrue(always.IsCrit);
            Assert.AreEqual(54f, always.Damage, 0.001f);

            var forced = Hit(120, 0.3f, 1f, 1f, 0f, true, 1.5f, 0.9f);
            Assert.IsTrue(forced.IsCrit);
            Assert.AreEqual(54f, forced.Damage, 0.001f);

            var doubled = Hit(120, 0.3f, 1f, 1f, 0f, true, 2f, 0.9f);
            Assert.IsTrue(doubled.IsCrit);
            Assert.AreEqual(72f, doubled.Damage, 0.001f);
        }

        [Test]
        public void SkillDamage_ZeroPercentUsesFlatDamage()
        {
            var flat = Hit(120, 0f, 1f, 1f, 0f, false, 1.5f, 0f, 30f);
            Assert.IsFalse(flat.IsCrit);
            Assert.AreEqual(30f, flat.Damage, 0.001f);
        }

        static SkillDamageResult Hit(
            int attack,
            float percent,
            float own,
            float finalMultiplier,
            float critChance,
            bool forceCrit,
            float critMultiplier,
            float roll,
            float flat = 0f)
        {
            var request = new SkillDamageRequest
            {
                CasterAttack = attack,
                Percent = percent,
                FlatDamage = flat,
                SkillOwnMultiplier = own,
                FinalMultiplier = finalMultiplier,
                CritChance = critChance,
                ForceCrit = forceCrit,
                CritMultiplier = critMultiplier,
                Roll = roll
            };
            return CombatMath.ComputeSkillDamage(request);
        }
    }
}
