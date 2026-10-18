using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "YakuCatalog", menuName = "TypingRoguelike/Yaku Catalog")]
    public sealed class YakuCatalog : ScriptableObject
    {
        [Tooltip("上から順に判定する。罠が1つでもあれば、AnyTrap の役をほかより優先する。")]
        public YakuDefinition[] yaku;

        public static YakuCatalog LoadOrFallback()
        {
            var loaded = Resources.Load<YakuCatalog>("Run/YakuCatalog");
            return loaded != null ? loaded : RuntimeFallback();
        }

        public static YakuCatalog RuntimeFallback()
        {
            var catalog = CreateInstance<YakuCatalog>();
            catalog.yaku = new[]
            {
                Make("役なし", YakuPattern.AnyTrap, DungeonNodeType.Trap, 1, YakuBonusKind.None, 0f),
                Make("戦闘×3", YakuPattern.AllSame, DungeonNodeType.Combat, 3, YakuBonusKind.CombatCardChoices, 1f),
                Make("回復×3", YakuPattern.AllSame, DungeonNodeType.Rest, 3, YakuBonusKind.HealBeforeFourth, 0.2f),
                Make("宝×3", YakuPattern.AllSame, DungeonNodeType.Treasure, 3, YakuBonusKind.CoinGainPercent, 0.5f),
                Make("3つとも違う", YakuPattern.AllDifferent, DungeonNodeType.Combat, 3, YakuBonusKind.FlatCoins, 20f)
            };
            return catalog;
        }

        static YakuDefinition Make(string name, YakuPattern pattern, DungeonNodeType type, int count, YakuBonusKind bonus, float value)
        {
            var yaku = CreateInstance<YakuDefinition>();
            yaku.displayName = name;
            yaku.pattern = pattern;
            yaku.matchType = type;
            yaku.matchCount = count;
            yaku.bonus = bonus;
            yaku.bonusValue = value;
            return yaku;
        }
    }
}
