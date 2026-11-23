using System.Collections.Generic;
using System.Linq;

namespace TypingRoguelike.Run
{
    /// <summary>Choosing a destination is separate from closing the doors and dispatching.</summary>
    public sealed class HubElevatorSelection
    {
        readonly HashSet<int> _unlocked;
        public int? SelectedFloor { get; private set; }
        public bool Dispatched { get; private set; }
        public int HighestFloor => _unlocked.Count == 0 ? 1 : _unlocked.Max();

        public HubElevatorSelection(IEnumerable<int> unlocked)
        {
            _unlocked = new HashSet<int>((unlocked ?? Enumerable.Empty<int>()).Where(f => f > 0));
        }

        public bool IsUnlocked(int floor) => _unlocked.Contains(floor);

        public bool Select(int floor)
        {
            if (Dispatched || !IsUnlocked(floor)) return false;
            SelectedFloor = floor;
            return true;
        }

        public bool TryDispatch(out int floor)
        {
            floor = SelectedFloor ?? 0;
            if (Dispatched || !SelectedFloor.HasValue) return false;
            Dispatched = true;
            return true;
        }

        public void OpenDoors()
        {
            if (Dispatched) return;
            SelectedFloor = null;
        }
    }
}
