using System;
using UnityEngine;

namespace TypingRoguelike.Data
{
    public enum TrustPersonality
    {
        Brave = 0,
        Timid = 1,
        Curious = 2,
    }

    public enum TrustPassiveKind
    {
        None = 0,
        AttackPercent = 1,
        OpeningFreezeSeconds = 2,
        DamageTakenPercent = 3,
        PostBattleHealPercent = 4,
        NegateMissHit = 5,
        CardRewardChoices = 6,
        ShowEnemyAttackTime = 7,
    }

    public enum OpinionStance
    {
        Aggressive = 0,
        Defensive = 1,
    }

    public enum OpinionKnowledge
    {
        Unknown = 0,
        Known = 1,
    }

    public enum OpinionEffectKind
    {
        None = 0,
        Coins = 1,
        CardChoices = 2,
        HealFraction = 3,
        EnemyHpFraction = 4,
        OpeningAttackSeconds = 5,
        BattleCoinFraction = 6,
    }

    public enum OpinionScene
    {
        Aggressive = 0,
        Defensive = 1,
        Agree = 2,
        Followed = 3,
        Ignored = 4,
    }

    [Serializable]
    public sealed class TrustPassiveEffect
    {
        public TrustPassiveKind kind;
        public float value = 0.1f;
    }

    [Serializable]
    public sealed class TrustPassiveTier
    {
        public int requiredTrust = 25;
        public string label = "パッシブ";
        public TrustPassiveEffect[] effects = Array.Empty<TrustPassiveEffect>();
    }

    [Serializable]
    public sealed class TrustLayerMultiplier
    {
        public int fromLayer = 1;
        public int toLayer = 99;
        public float multiplier = 1f;
    }

    [Serializable]
    public sealed class OpinionChoice
    {
        [TextArea] public string body = "進む";
        public OpinionStance stance = OpinionStance.Aggressive;
        public OpinionKnowledge knowledge = OpinionKnowledge.Known;
        public OpinionEffectKind effect = OpinionEffectKind.Coins;
        public float effectValue = 30f;
        public OpinionEffectKind effect2 = OpinionEffectKind.None;
        public float effectValue2;

        public string DescribeEffect()
        {
            var first = DescribeOne(effect, effectValue);
            var second = DescribeOne(effect2, effectValue2);
            if (string.IsNullOrEmpty(first))
                return string.IsNullOrEmpty(second) ? "効果なし" : second;
            if (string.IsNullOrEmpty(second))
                return first;
            return $"{first}。{second}";
        }

        static string DescribeOne(OpinionEffectKind kind, float value) => kind switch
        {
            OpinionEffectKind.Coins => $"塔片 +{Mathf.Max(0, Mathf.RoundToInt(value))}",
            OpinionEffectKind.CardChoices => $"次の刻印の選択肢 +{Mathf.Max(1, Mathf.RoundToInt(value))}",
            OpinionEffectKind.HealFraction => $"HP {Mathf.RoundToInt(Mathf.Max(0f, value) * 100f)}% 回復",
            OpinionEffectKind.EnemyHpFraction => $"敵HP {SignedPercent(value)}",
            OpinionEffectKind.OpeningAttackSeconds => $"最初の攻撃まで {SignedNumber(value)}秒",
            OpinionEffectKind.BattleCoinFraction => $"戦闘報酬のお金 {SignedPercent(value)}",
            _ => ""
        };

        static string SignedPercent(float fraction)
        {
            var percent = Mathf.RoundToInt(fraction * 100f);
            return percent > 0 ? $"+{percent}%" : $"{percent}%";
        }

        static string SignedNumber(float value)
        {
            var rounded = Mathf.RoundToInt(value);
            return rounded > 0 ? $"+{rounded}" : rounded.ToString();
        }
    }

    [Serializable]
    public sealed class OpinionLine
    {
        public string characterId = "chara01";
        public OpinionScene scene = OpinionScene.Aggressive;
        [TextArea] public string line = "";
        [Tooltip("あとで同じ名前の顔画像に差し替える")]
        public string expression = "";
    }
}
