using System;
using System.Text;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>盤面の各列から、等確率で1マスを引く。</summary>
    public static class ReelDraw
    {
        public static DungeonNodeType DrawColumn(System.Random rng, BoardGrid grid, int column)
        {
            var row = rng.Next(BoardGrid.Rows);
            return grid.Get(column, row);
        }

        public static DungeonNodeType[] DrawRoad(System.Random rng, BoardGrid grid)
        {
            var road = new DungeonNodeType[BoardGrid.Columns];
            for (var column = 0; column < BoardGrid.Columns; column++)
                road[column] = DrawColumn(rng, grid, column);
            return road;
        }

        public static void LogThousandRolls(BoardCatalog catalog, YakuCatalog yaku, int trials = 1000)
        {
            if (catalog?.presets == null)
            {
                Debug.LogWarning("盤面カタログが無いので抽選ログを出せない。");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"[BoardRoll] 各盤面 {trials} 回（デバッグ用の乱数。ランの抽選とは別）");
            foreach (var preset in catalog.presets)
            {
                if (preset == null)
                    continue;
                var grid = preset.ToGrid();
                var counts = new int[BoardGrid.Columns, 6];
                var types = BoardGrid.RoadTypes();
                var handNames = new System.Collections.Generic.List<string>();
                var handCounts = new System.Collections.Generic.List<int>();
                if (yaku?.yaku != null)
                {
                    foreach (var hand in yaku.yaku)
                    {
                        var name = HandName(hand);
                        if (name == null || handNames.Contains(name))
                            continue;
                        handNames.Add(name);
                        handCounts.Add(0);
                    }
                }

                var unmatched = 0;
                var rng = new System.Random(12345);
                for (var n = 0; n < trials; n++)
                {
                    var road = DrawRoad(rng, grid);
                    for (var column = 0; column < road.Length; column++)
                    {
                        var index = System.Array.IndexOf(types, road[column]);
                        if (index >= 0)
                            counts[column, index]++;
                    }

                    var result = YakuEvaluator.Evaluate(road[0], road[1], road[2], yaku);
                    var resultName = HandName(result);
                    if (resultName == null)
                    {
                        unmatched++;
                        continue;
                    }

                    var handIndex = handNames.IndexOf(resultName);
                    if (handIndex < 0)
                    {
                        handNames.Add(resultName);
                        handCounts.Add(1);
                    }
                    else
                        handCounts[handIndex]++;
                }

                text.AppendLine(preset.displayName);
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    text.Append($"  {column + 1}列");
                    for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                    {
                        text.Append(' ');
                        text.Append(LayerPathUI.SquareName(types[typeIndex]));
                        text.Append(counts[column, typeIndex]);
                    }

                    text.AppendLine();
                }

                for (var i = 0; i < handNames.Count; i++)
                {
                    text.Append("  役 ");
                    text.Append(handNames[i]);
                    text.Append(' ');
                    text.Append(handCounts[i]);
                    text.AppendLine();
                }

                if (unmatched > 0)
                {
                    text.Append("  役 一致なし ");
                    text.Append(unmatched);
                    text.AppendLine();
                }
            }

            Debug.Log(text.ToString().TrimEnd());
        }

        static string HandName(YakuDefinition hand)
        {
            if (hand == null)
                return null;
            return string.IsNullOrEmpty(hand.displayName) ? "役" : hand.displayName;
        }
    }
}
