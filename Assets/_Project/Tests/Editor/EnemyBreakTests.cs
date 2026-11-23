using NUnit.Framework;
using TypingRoguelike.Battle;

namespace TypingRoguelike.Tests
{
    public sealed class EnemyBreakTests
    {
        [Test]
        public void Unordered_ConsumesLeftmostOfMatchingColor()
        {
            var state = new EnemyBreakState(new[] { ElementColor.Red, ElementColor.Blue, ElementColor.Red },4,1.5f);
            Assert.AreEqual(1,state.CompleteWord(ElementColor.Blue));
            Assert.AreEqual(-1,state.CompleteWord(ElementColor.Yellow));
            Assert.AreEqual(0,state.CompleteWord(ElementColor.Red));
            Assert.IsFalse(state.IsBroken);
            Assert.AreEqual(2,state.CompleteWord(ElementColor.Red));
            Assert.IsTrue(state.IsBroken);
            Assert.AreEqual(1.5f,state.DamageMultiplier);
            Assert.AreEqual(-1,state.CompleteWord(ElementColor.Blue));
        }
        [Test]
        public void DuringBreak_NoPreload_RecoveryRestoresIdenticalRequirements()
        {
            var state = new EnemyBreakState(new[]{ElementColor.Red},4,1.5f);
            state.CompleteWord(ElementColor.Red);
            Assert.AreEqual(-1,state.CompleteWord(ElementColor.Red));
            Assert.IsFalse(state.Tick(0));
            Assert.AreEqual(4,state.Remaining);
            Assert.IsFalse(state.Tick(3));
            Assert.IsTrue(state.Tick(1));
            Assert.IsFalse(state.Consumed[0]);
            Assert.AreEqual(1,state.DamageMultiplier);
            Assert.AreEqual(0,state.CompleteWord(ElementColor.Red));
            Assert.IsTrue(state.IsBroken);
            Assert.IsTrue(state.Tick(30));
            Assert.IsFalse(state.Tick(1));
        }
        [Test]
        public void DisabledAndCallerArrayMutation_DoNotCauseFalseBreak()
        {
            var colors=new[]{ElementColor.Red};
            var state=new EnemyBreakState(colors,4,1.5f);
            colors[0]=ElementColor.Blue;
            Assert.AreEqual(-1,state.CompleteWord(ElementColor.Blue));
            var disabled=new EnemyBreakState(null,4,1.5f);
            Assert.AreEqual(-1,disabled.CompleteWord(ElementColor.Red));
            Assert.IsFalse(disabled.IsBroken);
        }
    }
}
