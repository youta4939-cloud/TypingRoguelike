using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypingRoguelike.Battle
{
    /// <summary>敵に付く斧のデバフ。1戦闘ごとに作り直す。Unity には依存しない。</summary>
    public sealed class EnemyStatusState
    {
        sealed class Timed
        {
            public float Pct;
            public float Remaining;
        }

        readonly Dictionary<string, Timed> _vuln = new Dictionary<string, Timed>();
        readonly Dictionary<string, Timed> _atk = new Dictionary<string, Timed>();
        readonly Dictionary<string, Timed> _slow = new Dictionary<string, Timed>();
        float _berserkVuln;

        public string AtkDownMode = "add";
        public string SlowMode = "max";
        public string GoufuMode = "separate";

        public float IncomingMultiplier
        {
            get
            {
                var sum = Sum(_vuln);
                return 1f + sum + _berserkVuln;
            }
        }

        public float BerserkVuln => _berserkVuln;

        public float AttackMultiplier => Math.Max(0f, 1f - AttackDown);

        public float AttackDown => Combine(_atk, AtkDownMode);

        public float SpeedMultiplier
        {
            get
            {
                var down = Combine(_slow, SlowMode);
                if (down < 0f)
                    down = 0f;
                if (down > 1f)
                    down = 1f;
                return 1f - down;
            }
        }

        public bool HasAnyDebuff => DebuffKindCount > 0;

        public int DebuffKindCount
        {
            get
            {
                var vuln = Sum(_vuln) > 0f;
                var berserk = _berserkVuln > 0f;
                var atk = AttackDown > 0f;
                var slow = Combine(_slow, "max") > 0f;
                if (GoufuMode == "mergeVuln")
                    return (vuln || berserk ? 1 : 0) + (atk ? 1 : 0) + (slow ? 1 : 0);
                return (vuln ? 1 : 0) + (berserk ? 1 : 0) + (atk ? 1 : 0) + (slow ? 1 : 0);
            }
        }

        public void AddVuln(string source, float pct, float sec) => Set(_vuln, source, pct, sec);

        public void AddBerserkVuln(float pct) => _berserkVuln = pct > 0f ? pct : 0f;

        public void AddAtkDown(string source, float pct, float sec) => Set(_atk, source, pct, sec);

        public void AddAtkSlow(string source, float pct, float sec) => Set(_slow, source, pct, sec);

        public void ExtendTimed(float sec, bool includeBerserk)
        {
            if (sec == 0f)
                return;
            AddTime(_vuln, sec);
            AddTime(_atk, sec);
            AddTime(_slow, sec);
            _ = includeBerserk;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;
            Tick(_vuln, dt);
            Tick(_atk, dt);
            Tick(_slow, dt);
        }

        public void Clear()
        {
            _vuln.Clear();
            _atk.Clear();
            _slow.Clear();
            _berserkVuln = 0f;
        }

        public float SourcePercent(string source)
        {
            if (string.IsNullOrEmpty(source))
                return 0f;
            if (_vuln.TryGetValue(source, out var timed) && timed.Remaining > 0f)
                return timed.Pct;
            return 0f;
        }

        public float SourceRemaining(string source)
        {
            if (!string.IsNullOrEmpty(source) && _vuln.TryGetValue(source, out var timed))
                return timed.Remaining;
            return 0f;
        }

        public string DisplayText()
        {
            var parts = new List<string>();
            var vuln = Sum(_vuln);
            if (vuln > 0f)
                parts.Add($"被ダメ+{Percent(vuln)}%（{Seconds(Longest(_vuln))}秒）");
            var atk = AttackDown;
            if (atk > 0f)
                parts.Add($"攻撃-{Percent(atk)}%（{Seconds(Longest(_atk))}秒）");
            if (Combine(_slow, "max") > 0f)
                parts.Add($"速度↓（{Seconds(Longest(_slow))}秒）");
            if (_berserkVuln > 0f)
                parts.Add($"バーサーク+{Percent(_berserkVuln)}%");
            return string.Join("　", parts);
        }

        static void Set(Dictionary<string, Timed> table, string source, float pct, float sec)
        {
            if (string.IsNullOrEmpty(source) || pct <= 0f)
                return;
            if (!table.TryGetValue(source, out var timed))
            {
                timed = new Timed();
                table[source] = timed;
            }

            timed.Pct = pct;
            if (sec > timed.Remaining)
                timed.Remaining = sec;
        }

        static void AddTime(Dictionary<string, Timed> table, float sec)
        {
            foreach (var timed in table.Values)
            {
                if (timed.Remaining > 0f)
                    timed.Remaining += sec;
            }
        }

        static void Tick(Dictionary<string, Timed> table, float dt)
        {
            var dead = new List<string>();
            foreach (var pair in table)
            {
                pair.Value.Remaining -= dt;
                if (pair.Value.Remaining <= 0f)
                    dead.Add(pair.Key);
            }

            foreach (var key in dead)
                table.Remove(key);
        }

        static float Sum(Dictionary<string, Timed> table)
        {
            var sum = 0f;
            foreach (var timed in table.Values)
            {
                if (timed.Remaining > 0f)
                    sum += timed.Pct;
            }

            return sum;
        }

        static float Combine(Dictionary<string, Timed> table, string mode)
        {
            var sum = 0f;
            var max = 0f;
            foreach (var timed in table.Values)
            {
                if (timed.Remaining <= 0f)
                    continue;
                sum += timed.Pct;
                if (timed.Pct > max)
                    max = timed.Pct;
            }

            return mode == "max" ? max : sum;
        }

        static float Longest(Dictionary<string, Timed> table)
        {
            var longest = 0f;
            foreach (var timed in table.Values)
            {
                if (timed.Remaining > longest)
                    longest = timed.Remaining;
            }

            return longest;
        }

        static int Percent(float ratio) =>
            (int)Math.Round(ratio * 100d, MidpointRounding.AwayFromZero);

        static int Seconds(float remaining) =>
            remaining <= 0f ? 0 : (int)Math.Ceiling(remaining - 0.0001f);

        public string Breakdown()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "被ダメ×{0:0.00}　攻撃×{1:0.00}　速度×{2:0.00}　バーサーク{3:0}%　種類{4}",
                IncomingMultiplier,
                AttackMultiplier,
                SpeedMultiplier,
                _berserkVuln * 100f,
                DebuffKindCount);
        }
    }
}
