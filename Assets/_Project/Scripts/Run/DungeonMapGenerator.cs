using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>マス種: Danger / EventV2 / Merchant / Treasure / Mystery / Rest / Elevator / Boss。Normal は抽選しない。</summary>
    public static class DungeonMapGenerator
    {
        public sealed class Result
        {
            public List<DungeonMapNode> Nodes = new();
            public int StartNodeId;
            public int BossNodeId;
        }

        public const int GridColumns = 5;
        const int MaxGenerateAttempts = 50;

        public static Result Generate(
            DungeonRunDefinition config,
            int? seed = null,
            int idBase = 0,
            bool placeElevator = false)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            for (var attempt = 0; attempt < MaxGenerateAttempts; attempt++)
            {
                var varied = TryGenerateVaried(config, rng, idBase, placeElevator);
                if (varied == null)
                    continue;
                LogNodeTypeCounts(varied);
                Debug.Log($"マップ生成: 行数ランダム（試行 {attempt + 1}）");
                return varied;
            }

            Debug.LogWarning("マップ生成: 50回試して条件を満たせなかった。従来の等間隔生成に戻す。");
            return GenerateLegacy(config, seed ?? rng.Next(), idBase, placeElevator);
        }

        static Result GenerateLegacy(
            DungeonRunDefinition config,
            int? seed = null,
            int idBase = 0,
            bool placeElevator = false)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var pathLayers = config != null ? Mathf.Clamp(config.pathLayers, 3, 8) : 5;
            var width = config != null ? Mathf.Clamp(config.branchWidth, 2, 5) : 3;
            var bossLayer = pathLayers + 1;
            var elevatorLayer = Mathf.Clamp((pathLayers + 1) / 2, 2, pathLayers);
            var elevatorColumn = rng.Next(0, width);

            var result = new Result();
            var byLayer = new List<List<DungeonMapNode>>();

            var id = idBase;
            var start = new DungeonMapNode(id++, 0, width / 2, DungeonNodeType.Start) { Cleared = true, LayoutColumns = width };
            result.Nodes.Add(start);
            result.StartNodeId = start.Id;
            byLayer.Add(new List<DungeonMapNode> { start });

            for (var layer = 1; layer <= pathLayers; layer++)
            {
                var row = new List<DungeonMapNode>();
                for (var col = 0; col < width; col++)
                {
                    var type = placeElevator && layer == elevatorLayer && col == elevatorColumn
                        ? DungeonNodeType.Elevator
                        : RollNodeType(rng, config);
                    var node = new DungeonMapNode(id++, layer, col, type) { LayoutColumns = width };
                    if (type == DungeonNodeType.Danger)
                        node.DangerEncounterPercent = RollDangerEncounterPercent(rng, layer, pathLayers);
                    row.Add(node);
                    result.Nodes.Add(node);
                }

                byLayer.Add(row);
            }

            var boss = new DungeonMapNode(id, bossLayer, width / 2, DungeonNodeType.Boss) { LayoutColumns = width };
            result.Nodes.Add(boss);
            result.BossNodeId = boss.Id;
            byLayer.Add(new List<DungeonMapNode> { boss });

            WireEdges(byLayer, rng, width);
            BalanceRestNodes(result, config, rng, pathLayers);
            LogNodeTypeCounts(result);
            return result;
        }

        static Result TryGenerateVaried(DungeonRunDefinition config, System.Random rng, int idBase, bool placeElevator)
        {
            var pathLayers = config != null ? Mathf.Clamp(config.pathLayers, 3, 8) : 5;
            var minCount = config != null ? config.rowNodeCountMin : 2;
            var maxCount = config != null ? config.rowNodeCountMax : 5;
            minCount = Mathf.Clamp(minCount, 2, GridColumns);
            maxCount = Mathf.Clamp(maxCount, minCount, GridColumns);
            // ボス直前は、全員が2方向へ分かれてから最後の3マスに入る形でないと、一本道が2行続く。
            if (minCount > 2 || maxCount < 3)
                return null;

            var elevatorLayer = Mathf.Clamp((pathLayers + 1) / 2, 2, pathLayers);
            var homeColumn = GridColumns / 2;
            var columns = new int[pathLayers + 1][];
            columns[0] = new[] { homeColumn };
            columns[pathLayers] = new[] { 1, 2, 3 };
            var approaches = new[] { new[] { 1, 2 }, new[] { 1, 3 }, new[] { 2, 3 } };
            columns[pathLayers - 1] = approaches[rng.Next(approaches.Length)];

            var degree = new Dictionary<int, int>[pathLayers + 1];
            degree[pathLayers] = DegreeMap(columns[pathLayers], 1);
            var edges = new List<(int from, int to)>[pathLayers + 1];
            edges[pathLayers - 1] = FindEdges(columns[pathLayers - 1], columns[pathLayers], degree[pathLayers], 2);
            if (edges[pathLayers - 1] == null)
                return null;
            degree[pathLayers - 1] = DegreeFromEdges(columns[pathLayers - 1], edges[pathLayers - 1]);

            for (var layer = pathLayers - 2; layer >= 1; layer--)
            {
                var options = new List<int[]>();
                var optionEdges = new List<List<(int from, int to)>>();
                foreach (var candidate in ColumnSetsBetween(minCount, maxCount))
                {
                    var found = FindEdges(candidate, columns[layer + 1], degree[layer + 1], 1);
                    if (found == null)
                        continue;
                    options.Add(candidate);
                    optionEdges.Add(found);
                }

                if (options.Count == 0)
                    return null;
                var pick = rng.Next(options.Count);
                columns[layer] = options[pick];
                edges[layer] = optionEdges[pick];
                degree[layer] = DegreeFromEdges(columns[layer], edges[layer]);
            }

            edges[0] = FindEdges(columns[0], columns[1], degree[1], 1);
            if (edges[0] == null)
                return null;
            edges[pathLayers] = FindEdges(columns[pathLayers], new[] { homeColumn }, DegreeMap(new[] { homeColumn }, 0), 1);
            if (edges[pathLayers] == null)
                return null;

            var result = new Result();
            var byLayer = new List<List<DungeonMapNode>>();
            var id = idBase;
            var lookup = new Dictionary<(int layer, int column), DungeonMapNode>();
            for (var layer = 0; layer <= pathLayers; layer++)
            {
                var row = new List<DungeonMapNode>();
                foreach (var column in columns[layer].OrderBy(value => value))
                {
                    var type = layer == 0
                        ? DungeonNodeType.Start
                        : RollNodeType(rng, config);
                    var node = new DungeonMapNode(id++, layer, column, type)
                    {
                        LayoutColumns = GridColumns,
                        Cleared = layer == 0
                    };
                    if (type == DungeonNodeType.Danger)
                        node.DangerEncounterPercent = RollDangerEncounterPercent(rng, layer, pathLayers);
                    row.Add(node);
                    result.Nodes.Add(node);
                    lookup[(layer, column)] = node;
                }

                if (placeElevator && layer == elevatorLayer)
                {
                    var slot = row[rng.Next(row.Count)];
                    slot.Type = DungeonNodeType.Elevator;
                    slot.DangerEncounterPercent = 0;
                }

                byLayer.Add(row);
            }

            var boss = new DungeonMapNode(id, pathLayers + 1, homeColumn, DungeonNodeType.Boss)
            {
                LayoutColumns = GridColumns
            };
            result.Nodes.Add(boss);
            result.BossNodeId = boss.Id;
            result.StartNodeId = lookup[(0, homeColumn)].Id;
            lookup[(pathLayers + 1, homeColumn)] = boss;
            byLayer.Add(new List<DungeonMapNode> { boss });

            for (var layer = 0; layer <= pathLayers; layer++)
            {
                foreach (var edge in edges[layer])
                {
                    var from = lookup[(layer, edge.from)];
                    var to = lookup[(layer + 1, edge.to)];
                    if (!from.Next.Contains(to.Id))
                        from.Next.Add(to.Id);
                }
            }

            BalanceRestNodes(result, config, rng, pathLayers);
            if (!IsValidLayout(result, byLayer, placeElevator, elevatorLayer))
                return null;

            const float jitterX = 0.02f;
            const float jitterY = 0.008f;
            foreach (var node in result.Nodes)
            {
                node.DisplayOffsetX = ((float)rng.NextDouble() * 2f - 1f) * jitterX;
                node.DisplayOffsetY = ((float)rng.NextDouble() * 2f - 1f) * jitterY;
            }

            return result;
        }

        static IEnumerable<int[]> ColumnSetsBetween(int minCount, int maxCount)
        {
            for (var count = minCount; count <= maxCount; count++)
            {
                foreach (var columns in ColumnSets(count))
                    yield return columns;
            }
        }

        static List<int[]> ColumnSets(int count)
        {
            var sets = new List<int[]>();
            var pick = new int[count];
            void Walk(int start, int filled)
            {
                if (filled == count)
                {
                    sets.Add((int[])pick.Clone());
                    return;
                }

                for (var column = start; column < GridColumns; column++)
                {
                    pick[filled] = column;
                    Walk(column + 1, filled + 1);
                }
            }

            Walk(0, 0);
            return sets;
        }

        static Dictionary<int, int> DegreeMap(IEnumerable<int> columns, int degree)
        {
            var map = new Dictionary<int, int>();
            foreach (var column in columns)
                map[column] = degree;
            return map;
        }

        static Dictionary<int, int> DegreeFromEdges(int[] sources, List<(int from, int to)> edges)
        {
            var map = DegreeMap(sources, 0);
            foreach (var edge in edges)
                map[edge.from] = map[edge.from] + 1;
            return map;
        }

        static List<(int from, int to)> FindEdges(int[] sources, int[] targets, Dictionary<int, int> childDegree, int minOut)
        {
            var sourceColumns = sources.OrderBy(column => column).ToArray();
            var targetColumns = targets.OrderBy(column => column).ToArray();
            var chosen = new List<int>[sourceColumns.Length];
            var uncovered = new HashSet<int>(targetColumns);
            List<(int from, int to)> result = null;

            bool Walk(int index, int floor)
            {
                if (result != null)
                    return true;
                if (index == sourceColumns.Length)
                {
                    if (uncovered.Count != 0)
                        return false;
                    result = new List<(int from, int to)>();
                    for (var i = 0; i < sourceColumns.Length; i++)
                    {
                        foreach (var target in chosen[i])
                            result.Add((sourceColumns[i], target));
                    }

                    return true;
                }

                var options = new List<int>();
                foreach (var target in targetColumns)
                {
                    if (target >= floor && Math.Abs(sourceColumns[index] - target) <= 1)
                        options.Add(target);
                }

                var limit = 1 << options.Count;
                for (var mask = 1; mask < limit; mask++)
                {
                    var subset = new List<int>();
                    for (var bit = 0; bit < options.Count; bit++)
                    {
                        if ((mask & (1 << bit)) != 0)
                            subset.Add(options[bit]);
                    }

                    if (subset.Count < minOut)
                        continue;
                    if (subset.Count == 1 && childDegree.TryGetValue(subset[0], out var childOut) && childOut == 1)
                        continue;

                    chosen[index] = subset;
                    var removed = new List<int>();
                    foreach (var target in subset)
                    {
                        if (uncovered.Remove(target))
                            removed.Add(target);
                    }

                    if (Walk(index + 1, subset[subset.Count - 1]))
                        return true;
                    foreach (var target in removed)
                        uncovered.Add(target);
                }

                chosen[index] = null;
                return false;
            }

            Walk(0, int.MinValue);
            return result;
        }

        static bool IsValidLayout(Result result, List<List<DungeonMapNode>> byLayer, bool placeElevator, int elevatorLayer)
        {
            if (result?.Nodes == null || result.Nodes.Count == 0)
                return false;

            var byId = result.Nodes.ToDictionary(node => node.Id);
            if (!ReachesEveryNode(result, byId))
                return false;

            foreach (var node in result.Nodes)
            {
                if (node.Id == result.BossNodeId)
                {
                    if (node.Next.Count != 0)
                        return false;
                    continue;
                }

                if (node.Next.Count < 1)
                    return false;
            }

            if (HasSingleChoiceChain(byLayer, byId))
                return false;

            for (var layer = 0; layer < byLayer.Count - 1; layer++)
            {
                if (HasIllegalEdges(byLayer[layer], byId))
                    return false;
                foreach (var node in byLayer[layer])
                {
                    foreach (var nextId in node.Next)
                    {
                        if (!byId.TryGetValue(nextId, out var next) || next.Layer != node.Layer + 1)
                            return false;
                    }
                }
            }

            var elevatorCount = result.Nodes.Count(node => node.Type == DungeonNodeType.Elevator);
            if (placeElevator)
            {
                if (elevatorCount != 1)
                    return false;
                if (!result.Nodes.Any(node => node.Type == DungeonNodeType.Elevator && node.Layer == elevatorLayer))
                    return false;
            }
            else if (elevatorCount != 0)
            {
                return false;
            }

            return true;
        }

        static bool ReachesEveryNode(Result result, Dictionary<int, DungeonMapNode> byId)
        {
            var forward = Walk(result.StartNodeId, id => byId[id].Next);
            if (forward.Count != result.Nodes.Count)
                return false;

            var incoming = new Dictionary<int, List<int>>();
            foreach (var node in result.Nodes)
                incoming[node.Id] = new List<int>();
            foreach (var node in result.Nodes)
            {
                foreach (var nextId in node.Next)
                    incoming[nextId].Add(node.Id);
            }

            var backward = Walk(result.BossNodeId, id => incoming[id]);
            return backward.Count == result.Nodes.Count;
        }

        static HashSet<int> Walk(int origin, Func<int, IEnumerable<int>> nexts)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id))
                    continue;
                foreach (var next in nexts(id))
                    queue.Enqueue(next);
            }

            return seen;
        }

        static bool HasSingleChoiceChain(List<List<DungeonMapNode>> byLayer, Dictionary<int, DungeonMapNode> byId)
        {
            foreach (var row in byLayer)
            {
                foreach (var node in row)
                {
                    if (node.Next.Count != 1 || !byId.TryGetValue(node.Next[0], out var child))
                        continue;
                    if (child.Next.Count == 1)
                        return true;
                }
            }

            return false;
        }

        static bool HasIllegalEdges(List<DungeonMapNode> sources, Dictionary<int, DungeonMapNode> byId)
        {
            var edges = new List<(int sourceColumn, int targetColumn)>();
            foreach (var source in sources)
            {
                foreach (var nextId in source.Next)
                {
                    if (!byId.TryGetValue(nextId, out var target))
                        return true;
                    if (Math.Abs(source.Column - target.Column) > 1)
                        return true;
                    edges.Add((source.Column, target.Column));
                }
            }

            for (var i = 0; i < edges.Count; i++)
            {
                for (var j = i + 1; j < edges.Count; j++)
                {
                    var a = edges[i];
                    var b = edges[j];
                    if ((a.sourceColumn - b.sourceColumn) * (a.targetColumn - b.targetColumn) < 0)
                        return true;
                }
            }

            return false;
        }

        static Dictionary<int, DungeonMapNode> IndexNodes(List<List<DungeonMapNode>> byLayer)
        {
            var byId = new Dictionary<int, DungeonMapNode>();
            foreach (var row in byLayer)
            {
                foreach (var node in row)
                    byId[node.Id] = node;
            }

            return byId;
        }

        /// <summary>生成結果のマス種類ごとの数。Console で EventV2 の有無を見る。</summary>
        static void LogNodeTypeCounts(Result result)
        {
            var counts = new Dictionary<DungeonNodeType, int>();
            if (result?.Nodes != null)
            {
                foreach (var node in result.Nodes)
                {
                    counts.TryGetValue(node.Type, out var count);
                    counts[node.Type] = count + 1;
                }
            }

            var parts = new List<string>();
            foreach (DungeonNodeType type in Enum.GetValues(typeof(DungeonNodeType)))
            {
                counts.TryGetValue(type, out var count);
                parts.Add($"{type}={count}");
            }

            Debug.Log("マップ生成 マス種類: " + string.Join(", ", parts));
        }

        static void BalanceRestNodes(Result result, DungeonRunDefinition config, System.Random rng, int pathLayers)
        {
            if (result?.Nodes == null || result.Nodes.Count == 0)
                return;

            var min = config != null ? config.restNodesPerFloorMin : 1;
            var max = config != null ? config.restNodesPerFloorMax : 2;
            min = Mathf.Clamp(min, 0, 3);
            max = Mathf.Clamp(max, Mathf.Max(min, 1), 3);
            var target = min >= max ? min : rng.Next(min, max + 1);

            var rests = result.Nodes.Where(n => n.Type == DungeonNodeType.Rest).ToList();
            while (rests.Count > target)
            {
                var pick = rests[rng.Next(rests.Count)];
                pick.Type = DungeonNodeType.Danger;
                pick.DangerEncounterPercent = RollDangerEncounterPercent(rng, pick.Layer, pathLayers);
                rests.Remove(pick);
            }

            if (rests.Count >= target)
                return;

            var candidates = result.Nodes
                .Where(n => n.Type == DungeonNodeType.Danger && n.Layer > 0 && n.Id != result.BossNodeId)
                .ToList();
            while (rests.Count < target && candidates.Count > 0)
            {
                var index = rng.Next(candidates.Count);
                var node = candidates[index];
                candidates.RemoveAt(index);
                node.Type = DungeonNodeType.Rest;
                rests.Add(node);
            }
        }

        public static DungeonNodeType RollMysteryContents(DungeonRunDefinition config, System.Random rng)
        {
            return PickWeighted(rng, new[]
            {
                (DungeonNodeType.Danger, config != null ? config.mysteryWeightDanger : 50f),
                (DungeonNodeType.EventV2, config != null ? config.mysteryWeightEvent : 35f),
                (DungeonNodeType.Merchant, config != null ? config.mysteryWeightMerchant : 15f)
            });
        }

        static DungeonNodeType RollNodeType(System.Random rng, DungeonRunDefinition config)
        {
            return PickWeighted(rng, new[]
            {
                (DungeonNodeType.Danger, config != null ? config.nodeWeightDanger : 40f),
                (DungeonNodeType.EventV2, config != null ? config.nodeWeightEvent : 22f),
                (DungeonNodeType.Merchant, config != null ? config.nodeWeightMerchant : 14f),
                (DungeonNodeType.Treasure, config != null ? config.nodeWeightTreasure : 12f),
                (DungeonNodeType.Mystery, config != null ? config.nodeWeightMystery : 12f)
            });
        }

        static DungeonNodeType PickWeighted(System.Random rng, (DungeonNodeType type, float weight)[] table)
        {
            var total = 0f;
            foreach (var item in table)
                total += Mathf.Max(0f, item.weight);
            if (total <= 0f)
                return DungeonNodeType.Danger;

            var roll = (float)rng.NextDouble() * total;
            var cursor = 0f;
            foreach (var item in table)
            {
                cursor += Mathf.Max(0f, item.weight);
                if (roll < cursor)
                    return item.type;
            }

            return table[table.Length - 1].type;
        }

        /// <summary>10%刻み。深い段ほどやや高めになりやすい。</summary>
        public static int RollDangerEncounterPercent(System.Random rng, int layer, int pathLayers)
        {
            var tiers = new[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            var depth = pathLayers <= 0 ? 0f : layer / (float)pathLayers;
            var baseIndex = Mathf.Clamp(Mathf.RoundToInt(depth * (tiers.Length - 1)), 0, tiers.Length - 1);
            var index = Mathf.Clamp(baseIndex + rng.Next(-1, 2), 0, tiers.Length - 1);
            return tiers[index];
        }

        static void WireEdges(List<List<DungeonMapNode>> byLayer, System.Random rng, int width)
        {
            for (var li = 0; li < byLayer.Count - 1; li++)
            {
                var current = byLayer[li];
                var next = byLayer[li + 1];
                if (current.Count == 0 || next.Count == 0)
                    continue;

                var incomingCount = new int[next.Count];

                foreach (var node in current)
                {
                    var minCol = Mathf.Max(0, node.Column - 1);
                    var maxCol = Mathf.Min(width - 1, node.Column + 1);
                    var firstCol = rng.Next(minCol, maxCol + 1);
                    Connect(node, next, firstCol, incomingCount);

                    // 一部のマスだけ二股にする。Next に追加した実際の遷移先だけが UI で結ばれる。
                    if (next.Count > 1 && rng.NextDouble() < 0.42)
                    {
                        var secondCol = firstCol + (rng.Next(0, 2) == 0 ? -1 : 1);
                        secondCol = Mathf.Clamp(secondCol, minCol, maxCol);
                        if (secondCol != firstCol)
                            Connect(node, next, secondCol, incomingCount);
                    }
                }

                for (var i = 0; i < next.Count; i++)
                {
                    if (incomingCount[i] > 0)
                        continue;

                    var preferredFromCol = next[i].Column;
                    var from = PickSourceNode(current, preferredFromCol, rng);
                    if (from == null)
                        continue;

                    if (!from.Next.Contains(next[i].Id))
                        from.Next.Add(next[i].Id);
                }
            }
        }

        static void Connect(
            DungeonMapNode from,
            List<DungeonMapNode> nextLayer,
            int targetColumn,
            int[] incomingCount)
        {
            var (target, targetIndex) = PickTargetNode(nextLayer, targetColumn);
            if (target == null || from.Next.Contains(target.Id))
                return;

            from.Next.Add(target.Id);
            incomingCount[targetIndex]++;
        }

        /// <summary>次層は列＝リスト index とは限らない（ボス層は1ノードのみ）。</summary>
        static (DungeonMapNode node, int index) PickTargetNode(List<DungeonMapNode> layer, int preferredColumn)
        {
            if (layer.Count == 0)
                return (null, -1);
            if (layer.Count == 1)
                return (layer[0], 0);

            var bestIndex = 0;
            var bestDist = int.MaxValue;
            for (var i = 0; i < layer.Count; i++)
            {
                var dist = Mathf.Abs(layer[i].Column - preferredColumn);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return (layer[bestIndex], bestIndex);
        }

        static DungeonMapNode PickSourceNode(List<DungeonMapNode> layer, int preferredColumn, System.Random rng)
        {
            if (layer.Count == 0)
                return null;
            if (layer.Count == 1)
                return layer[0];

            var jitter = rng.Next(-1, 2);
            var targetCol = Mathf.Clamp(preferredColumn + jitter, 0, layer[layer.Count - 1].Column);
            var (node, _) = PickTargetNode(layer, targetCol);
            return node;
        }
    }
}
