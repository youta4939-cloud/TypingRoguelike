using System.Collections.Generic;

namespace TypingRoguelike.Run
{
    public sealed class LayerSquare
    {
        public int Index { get; }
        public DungeonNodeType Type { get; }
        public bool Cleared { get; set; }
        public int Id => Index;

        public LayerSquare(int index, DungeonNodeType type)
        {
            Index = index;
            Type = type;
        }
    }

    /// <summary>1層ぶんの一本道。マスは常に4つで、若い番号から順に入る。</summary>
    public sealed class DungeonRunState
    {
        public int Floor { get; private set; } = 1;
        public int TotalFloors { get; private set; } = 1;
        public int CurrentIndex { get; private set; }
        public LayerSquare[] Squares { get; private set; } = System.Array.Empty<LayerSquare>();
        public List<string> SeenEventIds { get; } = new();

        public LayerSquare Current =>
            Squares != null && CurrentIndex >= 0 && CurrentIndex < Squares.Length
                ? Squares[CurrentIndex]
                : null;

        public int CurrentNodeId => CurrentIndex;

        public bool LayerComplete
        {
            get
            {
                if (Squares == null || Squares.Length == 0)
                    return false;
                foreach (var square in Squares)
                {
                    if (!square.Cleared)
                        return false;
                }

                return true;
            }
        }

        public void SetFloor(
            int floor,
            int totalFloors,
            int runSeed,
            Data.LayerConfig config,
            int restoreIndex,
            IReadOnlyCollection<int> clearedSquareIndices,
            bool startAtElevator)
        {
            Floor = System.Math.Max(1, floor);
            TotalFloors = System.Math.Max(1, totalFloors);
            var types = LayerSquares.DrawSquares(Floor, runSeed, config);
            Squares = new LayerSquare[types.Length];
            for (var i = 0; i < types.Length; i++)
                Squares[i] = new LayerSquare(i, types[i]);

            if (startAtElevator)
            {
                for (var i = 0; i < Squares.Length - 1; i++)
                    Squares[i].Cleared = true;
                CurrentIndex = Squares.Length - 1;
                return;
            }

            if (clearedSquareIndices != null)
            {
                foreach (var index in clearedSquareIndices)
                {
                    if (index >= 0 && index < Squares.Length)
                        Squares[index].Cleared = true;
                }
            }

            if (restoreIndex >= 0 && restoreIndex < Squares.Length)
                CurrentIndex = restoreIndex;
            else
                CurrentIndex = 0;
        }

        public int FirstUnclearedIndex()
        {
            if (Squares == null)
                return -1;
            for (var i = 0; i < Squares.Length; i++)
            {
                if (!Squares[i].Cleared)
                    return i;
            }

            return -1;
        }

        public bool CanEnter(int index) => index == FirstUnclearedIndex();

        public void MoveTo(int index)
        {
            if (!CanEnter(index))
                return;
            CurrentIndex = index;
        }

        public void MarkCurrentCleared()
        {
            if (Current != null)
                Current.Cleared = true;
        }

        public bool HasRowAfterNext()
        {
            var open = FirstUnclearedIndex();
            if (open < 0)
                return Floor < TotalFloors;
            return open < Squares.Length - 1;
        }

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
    }
}
