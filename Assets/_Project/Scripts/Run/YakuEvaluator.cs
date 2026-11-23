using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public sealed class YakuShare
    {
        public string Name;
        public int Hits;
        public int Total;

        public float Percent => Total <= 0 ? 0f : Hits * 100f / Total;
    }

    public static class YakuEvaluator
    {
        public static List<YakuShare> Shares(BoardGrid grid, YakuCatalog catalog)
        {
            var shares = new List<YakuShare>();
            var index = new Dictionary<string, YakuShare>();
            if (catalog?.yaku != null)
            {
                foreach (var hand in catalog.yaku)
                {
                    if (hand == null || string.IsNullOrEmpty(hand.displayName) || index.ContainsKey(hand.displayName))
                        continue;
                    var share = new YakuShare { Name = hand.displayName };
                    shares.Add(share);
                    index[hand.displayName] = share;
                }
            }

            var none = new YakuShare { Name = "一致なし" };
            var rows = grid != null ? grid.RowCount : BoardGrid.Rows;
            if (rows < 1)
                rows = 1;
            var total = rows * rows * rows;
            if (grid != null)
            {
                for (var a = 0; a < rows; a++)
                {
                    for (var b = 0; b < rows; b++)
                    {
                        for (var c = 0; c < rows; c++)
                        {
                            var result = Evaluate(grid.Get(0, a), grid.Get(1, b), grid.Get(2, c), catalog);
                            var name = result != null ? result.displayName : null;
                            if (!string.IsNullOrEmpty(name) && index.TryGetValue(name, out var share))
                                share.Hits++;
                            else
                                none.Hits++;
                        }
                    }
                }
            }
            else
                none.Hits = total;

            foreach (var share in shares)
                share.Total = total;
            none.Total = total;
            shares.Add(none);
            return shares;
        }

        public static YakuDefinition Evaluate(DungeonNodeType first, DungeonNodeType second, DungeonNodeType third, YakuCatalog catalog)
        {
            var list = catalog != null ? catalog.yaku : null;
            if (list == null || list.Length == 0)
                return null;

            var road = new[] { first, second, third };
            if (HasTrap(road))
            {
                foreach (var yaku in list)
                {
                    if (yaku != null && yaku.pattern == YakuPattern.AnyTrap)
                        return yaku;
                }

                return null;
            }

            foreach (var yaku in list)
            {
                if (yaku == null || yaku.pattern == YakuPattern.AnyTrap)
                    continue;
                if (Matches(yaku, road))
                    return yaku;
            }

            return null;
        }

        static bool HasTrap(DungeonNodeType[] road)
        {
            foreach (var type in road)
            {
                if (type == DungeonNodeType.Trap)
                    return true;
            }

            return false;
        }

        static bool Matches(YakuDefinition yaku, DungeonNodeType[] road)
        {
            switch (yaku.pattern)
            {
                case YakuPattern.AllSame:
                    return road[0] == yaku.matchType && road[1] == yaku.matchType && road[2] == yaku.matchType;
                case YakuPattern.AllDifferent:
                    return road[0] != road[1] && road[0] != road[2] && road[1] != road[2];
                case YakuPattern.CountAtLeast:
                    var count = 0;
                    foreach (var type in road)
                    {
                        if (type == yaku.matchType)
                            count++;
                    }

                    return count >= UnityEngine.Mathf.Max(1, yaku.matchCount);
                case YakuPattern.AnyTrap:
                    return HasTrap(road);
                default:
                    return false;
            }
        }
    }
}
