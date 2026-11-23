using System;
using System.Collections.Generic;

namespace TypingRoguelike.Battle
{
    /// <summary>順不同。消化位置は同色の左端。ブレイク中は次周へ蓄積しない。</summary>
    public sealed class EnemyBreakState
    {
        readonly ElementColor[] _requirements;
        readonly bool[] _consumed;
        readonly float _duration;
        readonly float _multiplier;
        public IReadOnlyList<ElementColor> Requirements => _requirements;
        public IReadOnlyList<bool> Consumed => _consumed;
        public float Remaining { get; private set; }
        public float Duration => _duration;
        public bool IsBroken => Remaining > 0f;
        public float DamageMultiplier => IsBroken ? _multiplier : 1f;

        public EnemyBreakState(ElementColor[] requirements, float duration, float multiplier)
        {
            _requirements = requirements == null ? Array.Empty<ElementColor>() : (ElementColor[])requirements.Clone();
            _consumed = new bool[_requirements.Length];
            _duration = Math.Max(0.1f, duration);
            _multiplier = Math.Max(1f, multiplier);
        }

        public int CompleteWord(ElementColor color)
        {
            if (IsBroken) return -1;
            for (var i = 0; i < _requirements.Length; i++)
            {
                if (_consumed[i] || _requirements[i] != color) continue;
                _consumed[i] = true;
                var complete = true;
                for (var j = 0; j < _consumed.Length; j++) complete &= _consumed[j];
                if (complete) Remaining = _duration;
                return i;
            }
            return -1;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!IsBroken || deltaSeconds <= 0f) return false;
            Remaining = Math.Max(0f, Remaining - deltaSeconds);
            if (IsBroken) return false;
            Array.Clear(_consumed, 0, _consumed.Length);
            return true;
        }
    }
}
