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

        public static Result Generate(
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
            var start = new DungeonMapNode(id++, 0, width / 2, DungeonNodeType.Start) { Cleared = true };
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
                    var node = new DungeonMapNode(id++, layer, col, type);
                    if (type == DungeonNodeType.Danger)
                        node.DangerEncounterPercent = RollDangerEncounterPercent(rng, layer, pathLayers);
                    row.Add(node);
                    result.Nodes.Add(node);
                }

                byLayer.Add(row);
            }

            var boss = new DungeonMapNode(id, bossLayer, width / 2, DungeonNodeType.Boss);
            result.Nodes.Add(boss);
            result.BossNodeId = boss.Id;
            byLayer.Add(new List<DungeonMapNode> { boss });

            WireEdges(byLayer, rng, width);
            BalanceRestNodes(result, config, rng, pathLayers);
            LogNodeTypeCounts(result);
            return result;
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
