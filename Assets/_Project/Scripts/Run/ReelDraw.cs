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

        public static void LogThousandRolls(BoardCatalog catalog, int trials = 1000)
        {
            if (catalog?.presets == null)
            {
                Debug.LogWarning("盤面カタログが無いので抽選ログを出せない。");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"[BoardRoll] 各盤面 {trials} 回");
            foreach (var preset in catalog.presets)
            {
                if (preset == null)
                    continue;
                var grid = preset.ToGrid();
                var counts = new int[BoardGrid.Columns, 6];
                var types = BoardGrid.RoadTypes();
                var rng = new System.Random(12345);
                for (var n = 0; n < trials; n++)
                {
                    for (var column = 0; column < BoardGrid.Columns; column++)
                    {
                        var drawn = DrawColumn(rng, grid, column);
                        var index = System.Array.IndexOf(types, drawn);
                        if (index >= 0)
                            counts[column, index]++;
                    }
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
            }

            Debug.Log(text.ToString().TrimEnd());
        }
    }
}
