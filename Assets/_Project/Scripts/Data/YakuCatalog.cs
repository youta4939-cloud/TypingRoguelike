using System;
using System.Collections.Generic;
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
            var json = TryLoadJson();
            if (json != null)
                return json;
            var loaded = Resources.Load<YakuCatalog>("Run/YakuCatalog");
            return loaded != null ? loaded : RuntimeFallback();
        }

        static YakuCatalog TryLoadJson()
        {
            var asset = Resources.Load<TextAsset>("Data/Yaku");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return null;
            try
            {
                var file = JsonUtility.FromJson<YakuFile>(asset.text);
                if (file?.yaku == null || file.yaku.Length == 0)
                    return null;
                var list = new List<YakuDefinition>();
                foreach (var record in file.yaku)
                {
                    var yaku = FromRecord(record);
                    if (yaku != null)
                        list.Add(yaku);
                }

                if (list.Count == 0)
                    return null;
                var catalog = CreateInstance<YakuCatalog>();
                catalog.yaku = list.ToArray();
                return catalog;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"役の表を読めませんでした: {ex.Message}");
                return null;
            }
        }

        static YakuDefinition FromRecord(YakuRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.name))
                return null;
            if (!Enum.TryParse(record.pattern, true, out YakuPattern pattern))
                pattern = YakuPattern.AllSame;
            if (!Enum.TryParse(record.bonus, true, out YakuBonusKind bonus))
                bonus = YakuBonusKind.None;
            var symbol = string.IsNullOrEmpty(record.type) ? 'B' : record.type[0];
            var yaku = CreateInstance<YakuDefinition>();
            yaku.displayName = record.name;
            yaku.pattern = pattern;
            yaku.matchType = BoardGrid.ParseSymbol(symbol);
            yaku.matchCount = record.count > 0 ? record.count : 3;
            yaku.bonus = bonus;
            yaku.bonusValue = record.bonusValue;
            yaku.effectText = record.effect;
            return yaku;
        }

#pragma warning disable CS0649
        [Serializable]
        sealed class YakuFile
        {
            public YakuRecord[] yaku;
        }

        [Serializable]
        sealed class YakuRecord
        {
            public string name;
            public string pattern;
            public string type;
            public int count;
            public string bonus;
            public float bonusValue;
            public string effect;
        }
#pragma warning restore CS0649

        public static YakuCatalog RuntimeFallback()
        {
            var catalog = CreateInstance<YakuCatalog>();
            catalog.yaku = new[]
            {
                Make("罠で不成立", YakuPattern.AnyTrap, DungeonNodeType.Trap, 1, YakuBonusKind.None, 0f),
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
