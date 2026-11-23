using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    /// <summary>1層4マス。1〜3は選んだ盤面から引き、4マス目は層番号で固定。</summary>
    public static class LayerSquares
    {
        public const int Count = 4;

        public static DungeonNodeType[] DrawSquares(int layer, int runSeed, LayerConfig config, BoardGrid board)
        {
            config ??= LayerConfig.RuntimeFallback();
            board ??= BoardGrid.Parse("BBRSTE", "BBRSTE", "BBRSTE");
            // ラン・層・盤面でシードが変わる。同じ組み合わせは同じ結果になり、再開してもマスが変わらない。
            var rng = new System.Random(unchecked(runSeed * 397 ^ layer * 7919 ^ board.ContentHash()));
            var squares = new DungeonNodeType[Count];
            var road = ReelDraw.DrawRoad(rng, board);
            for (var i = 0; i < road.Length && i < Count - 1; i++)
                squares[i] = road[i];
            squares[Count - 1] = FixedSquare(layer, config);
            return squares;
        }

        public static bool MeetsMinimums(BoardGrid grid, LayerConfig config, int layer, out string reason)
        {
            var lines = ShortageLines(grid, config, layer);
            if (grid == null)
            {
                reason = "盤面が無い";
                return false;
            }

            if (lines.Count == 0)
            {
                reason = null;
                return true;
            }

            reason = string.Join("\n", lines);
            return false;
        }

        public static System.Collections.Generic.List<string> ShortageLines(BoardGrid grid, LayerConfig config, int layer)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (grid == null)
                return lines;
            var rules = config != null ? config.MinimumsFor(layer) : null;
            if (rules == null)
                return lines;
            foreach (var rule in rules)
            {
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    var have = grid.Count(column, rule.type);
                    var need = rule.minimumPerColumn;
                    if (have >= need)
                        continue;
                    lines.Add($"{LayerPathUI.SquareName(rule.type)}が{column + 1}列目にあと{need - have}マス足りない");
                }
            }

            return lines;
        }

        public static string DescribeMinimums(LayerConfig config, int layer)
        {
            var rules = config != null ? config.MinimumsFor(layer) : null;
            if (rules == null || rules.Count == 0)
                return "最低必要：なし";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var rule in rules)
                parts.Add($"{LayerPathUI.SquareName(rule.type)} 各列{rule.minimumPerColumn}以上");
            return "最低必要：" + string.Join("、", parts);
        }

        public static DungeonNodeType FromRoadId(string id)
        {
            switch (id)
            {
                case "battle": return DungeonNodeType.Combat;
                case "rest": return DungeonNodeType.Rest;
                case "treasure": return DungeonNodeType.Treasure;
                case "shop": return DungeonNodeType.Merchant;
                case "event": return DungeonNodeType.Event;
                case "danger": return DungeonNodeType.Trap;
                default: return DungeonNodeType.Combat;
            }
        }

        public static DungeonNodeType FixedSquare(int layer, LayerConfig config)
        {
            var bossEvery = config != null && config.bossEvery > 0 ? config.bossEvery : 10;
            var elevatorEvery = config != null && config.elevatorEvery > 0 ? config.elevatorEvery : 3;
            if (layer % bossEvery == 0)
                return DungeonNodeType.Boss;
            if (layer % elevatorEvery == 0)
                return DungeonNodeType.Elevator;
            return DungeonNodeType.MidBoss;
        }
    }
}
