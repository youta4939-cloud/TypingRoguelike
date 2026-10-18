using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public static class YakuEvaluator
    {
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
