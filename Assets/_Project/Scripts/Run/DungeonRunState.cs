using System.Collections.Generic;
using System.Linq;

namespace TypingRoguelike.Run
{
    public sealed class DungeonRunState
    {
        readonly Dictionary<int, DungeonMapNode> _nodes = new();
        int _currentNodeId;
        int _seed;

        public int Floor { get; private set; } = 1;
        public int TotalFloors { get; private set; } = 1;
        public int BossNodeId { get; private set; }
        public int CurrentNodeId => _currentNodeId;
        public IReadOnlyDictionary<int, DungeonMapNode> Nodes => _nodes;

        public void BeginTower(
            DungeonMapGenerator.Result map,
            int seed,
            int totalFloors,
            IReadOnlyCollection<int> revealedNodeIds = null,
            IReadOnlyCollection<int> clearedNodeIds = null)
        {
            Floor = 1;
            TotalFloors = System.Math.Max(1, totalFloors);
            LoadMap(map, seed, revealedNodeIds, clearedNodeIds);
        }

        public void LoadMap(
            DungeonMapGenerator.Result map,
            int seed,
            IReadOnlyCollection<int> revealedNodeIds = null,
            IReadOnlyCollection<int> clearedNodeIds = null)
        {
            _nodes.Clear();
            _seed = seed;
            BossNodeId = map.BossNodeId;
            foreach (var n in map.Nodes)
            {
                n.Revealed = n.Type is DungeonNodeType.Start or DungeonNodeType.Boss ||
                             (revealedNodeIds != null && revealedNodeIds.Contains(n.Id));
                n.Cleared = n.Type == DungeonNodeType.Start ||
                            (clearedNodeIds != null && clearedNodeIds.Contains(n.Id));
                _nodes[n.Id] = n;
            }

            _currentNodeId = map.StartNodeId;
            if (_nodes.TryGetValue(_currentNodeId, out var start))
            {
                start.Cleared = true;
                start.Revealed = true;
            }
        }

        public DungeonMapNode Current => _nodes.GetValueOrDefault(_currentNodeId);

        public IEnumerable<DungeonMapNode> GetReachableNext()
        {
            if (!_nodes.TryGetValue(_currentNodeId, out var cur))
                yield break;

            foreach (var nextId in cur.Next)
            {
                if (_nodes.TryGetValue(nextId, out var next))
                    yield return next;
            }
        }

        public bool CanEnter(int nodeId)
        {
            if (!_nodes.TryGetValue(_currentNodeId, out var cur))
                return false;

            return cur.Next.Contains(nodeId);
        }

        public void MoveTo(int nodeId)
        {
            if (!CanEnter(nodeId))
                return;

            _currentNodeId = nodeId;
            if (_nodes.TryGetValue(nodeId, out var node))
                node.Revealed = true;
        }

        public void MarkCurrentCleared()
        {
            if (Current != null)
                Current.Cleared = true;
        }

        public bool IsRunComplete =>
            Current != null &&
            Current.Id == BossNodeId &&
            Current.Type == DungeonNodeType.Boss &&
            Current.Cleared;

        public void AdvanceFloor(
            DungeonMapGenerator.Result map,
            int seed,
            IReadOnlyCollection<int> revealedNodeIds = null,
            IReadOnlyCollection<int> clearedNodeIds = null)
        {
            Floor++;
            LoadMap(map, seed, revealedNodeIds, clearedNodeIds);
        }

        public void SetFloorAndMap(
            int floor,
            int totalFloors,
            DungeonMapGenerator.Result map,
            int seed,
            IReadOnlyCollection<int> revealedNodeIds = null,
            IReadOnlyCollection<int> clearedNodeIds = null)
        {
            Floor = System.Math.Max(1, floor);
            TotalFloors = System.Math.Max(1, totalFloors);
            LoadMap(map, seed, revealedNodeIds, clearedNodeIds);
        }

        public bool TryRestoreCurrentNode(int nodeId)
        {
            if (!_nodes.ContainsKey(nodeId))
                return false;

            _currentNodeId = nodeId;
            if (_nodes.TryGetValue(nodeId, out var node))
                node.Revealed = true;
            return true;
        }

        /// <summary>死亡後のエレベ再入: 当該エレベのみ表示し、そこから探索開始。</summary>
        public List<string> SeenEventIds { get; } = new();

        public void ClearSeenEvents() => SeenEventIds.Clear();

        public void ReplaceSeenEvents(IReadOnlyList<string> ids)
        {
            SeenEventIds.Clear();
            if (ids == null)
                return;
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id) && !SeenEventIds.Contains(id))
                    SeenEventIds.Add(id);
            }
        }

        public void RememberEvent(string id)
        {
            if (!string.IsNullOrEmpty(id) && !SeenEventIds.Contains(id))
                SeenEventIds.Add(id);
        }

        public bool HasRowAfterNext()
        {
            var cur = Current;
            if (cur == null)
                return false;
            foreach (var nextId in cur.Next)
            {
                if (_nodes.TryGetValue(nextId, out var next) && next.Next.Count > 0)
                    return true;
            }

            return false;
        }

        /// <summary>次の段を飛ばし、その次の段へ進める。飛ばした段は踏破にしない。二重には飛ばさない。</summary>
        public bool TrySkipNextRow(DungeonMapNode from)
        {
            if (from == null || from.Next.Count == 0)
                return false;

            var alreadySkipped = true;
            foreach (var nextId in from.Next)
            {
                if (!_nodes.TryGetValue(nextId, out var next) || next.Layer < from.Layer + 2)
                {
                    alreadySkipped = false;
                    break;
                }
            }

            if (alreadySkipped)
                return false;

            var far = new List<int>();
            foreach (var nextId in from.Next)
            {
                if (!_nodes.TryGetValue(nextId, out var mid))
                    continue;
                foreach (var farId in mid.Next)
                {
                    if (!far.Contains(farId))
                        far.Add(farId);
                }
            }

            if (far.Count == 0)
                return false;

            from.Next.Clear();
            from.Next.AddRange(far);
            return true;
        }

        public bool TrySpawnAtElevator(int elevatorNodeId)
        {
            if (!_nodes.TryGetValue(elevatorNodeId, out var elevator) ||
                elevator.Type != DungeonNodeType.Elevator)
                return false;

            foreach (var node in _nodes.Values)
                node.Revealed = node.Id == elevatorNodeId;

            _currentNodeId = elevatorNodeId;
            return true;
        }
    }
}
