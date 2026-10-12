using System.Collections.Generic;

namespace TypingRoguelike.Battle
{
    public sealed class OrbStock
    {
        public const int MaxLength = 5;

        readonly List<ElementColor> _orbs = new();

        public IReadOnlyList<ElementColor> Orbs => _orbs;

        public void Push(ElementColor color)
        {
            _orbs.Add(color);
            while (_orbs.Count > MaxLength)
                _orbs.RemoveAt(0);
        }

        public bool TryGetSuffix(int length, out ElementColor[] suffix)
        {
            suffix = null;
            if (_orbs.Count < length)
                return false;

            suffix = new ElementColor[length];
            for (var i = 0; i < length; i++)
                suffix[i] = _orbs[_orbs.Count - length + i];
            return true;
        }

        public void ConsumeSuffix(int length)
        {
            if (length <= 0 || _orbs.Count == 0)
                return;

            var remove = length;
            if (remove > _orbs.Count)
                remove = _orbs.Count;
            _orbs.RemoveRange(_orbs.Count - remove, remove);
        }
    }
}
