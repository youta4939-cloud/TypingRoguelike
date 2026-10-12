using System.Collections.Generic;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public readonly struct ExplorationPassiveSet
    {
        public int CautiousReductionPercent { get; }
        public bool Pathfinder { get; }
        public bool DangerSense { get; }
        public bool TreasureHunter { get; }
        public bool ElevatorSense { get; }

        public ExplorationPassiveSet(
            int cautiousReductionPercent,
            bool pathfinder,
            bool dangerSense,
            bool treasureHunter,
            bool elevatorSense)
        {
            CautiousReductionPercent = cautiousReductionPercent;
            Pathfinder = pathfinder;
            DangerSense = dangerSense;
            TreasureHunter = treasureHunter;
            ElevatorSense = elevatorSense;
        }
    }

    /// <summary>v1.2 / v1.1: キャラ固有探索パッシブ（同種は編成内1つだけ）。</summary>
    public static class ExplorationPartyPassives
    {
        public static ExplorationPassiveSet Compute(RunPartyState party)
        {
            if (party?.Members == null)
                return default;

            var seen = new HashSet<ExplorationPassiveKind>();
            var cautious = 0;
            var pathfinder = false;
            var dangerSense = false;
            var treasure = false;
            var elevator = false;

            foreach (var member in party.Members)
            {
                var ch = member?.character;
                if (ch == null || ch.explorationPassive == ExplorationPassiveKind.None)
                    continue;
                if (!seen.Add(ch.explorationPassive))
                    continue;

                switch (ch.explorationPassive)
                {
                    case ExplorationPassiveKind.Cautious:
                        cautious = Mathf.Max(cautious, Mathf.Max(0, ch.explorationPassiveValue));
                        break;
                    case ExplorationPassiveKind.Pathfinder:
                        pathfinder = true;
                        break;
                    case ExplorationPassiveKind.DangerSense:
                        dangerSense = true;
                        break;
                    case ExplorationPassiveKind.TreasureHunter:
                        treasure = true;
                        break;
                    case ExplorationPassiveKind.ElevatorSense:
                        elevator = true;
                        break;
                }
            }

            return new ExplorationPassiveSet(cautious, pathfinder, dangerSense, treasure, elevator);
        }

        public static void ApplyMapHints(
            RunPartyState party,
            DungeonRunState dungeon,
            int runSeed)
        {
            if (dungeon == null)
                return;

            var passives = Compute(party);

            if (passives.Pathfinder)
            {
                foreach (var node in dungeon.GetReachableNext())
                    node.Revealed = true;
            }

            if (passives.ElevatorSense)
            {
                foreach (var node in dungeon.Nodes.Values)
                {
                    if (node.Type == DungeonNodeType.Elevator)
                        node.Revealed = true;
                }
            }

            if (passives.TreasureHunter)
                RevealNearbyTreasures(dungeon, maxHops: 2);

            if (passives.DangerSense)
            {
                foreach (var node in dungeon.Nodes.Values)
                {
                    if (node.Type != DungeonNodeType.Danger || !node.Revealed)
                        continue;
                    if (!node.DangerElitePreview.HasValue)
                        node.DangerElitePreview = RollDangerElitePreview(runSeed, dungeon.Floor, node.Id);
                }
            }
        }

        static void RevealNearbyTreasures(DungeonRunState dungeon, int maxHops)
        {
            var currentId = dungeon.CurrentNodeId;
            var visited = new HashSet<int> { currentId };
            var frontier = new List<int> { currentId };

            for (var hop = 0; hop < maxHops && frontier.Count > 0; hop++)
            {
                var nextFrontier = new List<int>();
                foreach (var id in frontier)
                {
                    if (!dungeon.Nodes.TryGetValue(id, out var node))
                        continue;

                    foreach (var nextId in node.Next)
                    {
                        if (!visited.Add(nextId))
                            continue;

                        if (dungeon.Nodes.TryGetValue(nextId, out var next) &&
                            next.Type == DungeonNodeType.Treasure)
                            next.Revealed = true;

                        nextFrontier.Add(nextId);
                    }
                }

                frontier = nextFrontier;
            }
        }

        public static bool RollDangerElitePreview(int runSeed, int floor, int nodeId)
        {
            var hash = unchecked(runSeed + floor * 7919 + nodeId * 104729);
            var rng = new System.Random(hash);
            return rng.NextDouble() < 0.38;
        }
    }
}
