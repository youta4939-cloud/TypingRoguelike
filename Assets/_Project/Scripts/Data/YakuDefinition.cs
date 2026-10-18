using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Data
{
    public enum YakuPattern
    {
        AllSame = 0,
        AllDifferent = 1,
        CountAtLeast = 2,
        AnyTrap = 3,
    }

    public enum YakuBonusKind
    {
        None = 0,
        CombatCardChoices = 1,
        HealBeforeFourth = 2,
        CoinGainPercent = 3,
        FlatCoins = 4,
    }

    [CreateAssetMenu(fileName = "Yaku", menuName = "TypingRoguelike/Yaku")]
    public sealed class YakuDefinition : ScriptableObject
    {
        public string displayName = "役";
        public YakuPattern pattern = YakuPattern.AllSame;
        public DungeonNodeType matchType = DungeonNodeType.Combat;
        public int matchCount = 3;
        public YakuBonusKind bonus = YakuBonusKind.None;
        public float bonusValue;

        public string DescribeEffect() => bonus switch
        {
            YakuBonusKind.CombatCardChoices => $"この層の戦闘は、刻印の選択肢+{Mathf.Max(1, Mathf.RoundToInt(bonusValue))}",
            YakuBonusKind.HealBeforeFourth => $"4マス目の前にHP {Mathf.RoundToInt((bonusValue <= 0f ? 0.2f : bonusValue) * 100f)}% 回復",
            YakuBonusKind.CoinGainPercent => $"この層で得る塔片 +{Mathf.RoundToInt((bonusValue <= 0f ? 0.5f : bonusValue) * 100f)}%",
            YakuBonusKind.FlatCoins => $"塔片 +{Mathf.Max(0, Mathf.RoundToInt(bonusValue))}",
            _ => "ボーナスなし。罠の効果だけ。"
        };
    }
}
