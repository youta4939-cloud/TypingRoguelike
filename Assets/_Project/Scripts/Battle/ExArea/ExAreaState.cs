using System;
using System.Collections.Generic;

namespace TypingRoguelike.Battle
{
    public enum ExKind
    {
        None = 0,
        ColorStock = 1,
        Poison = 2,
        Paralysis = 3,
        Frenzy = 4,
        Focus = 5,
        Battou = 6,
        IssenKiwami = 7,
        HyakkiYakou = 8
    }

    public enum SpecialCardKind
    {
        RedRefill,
        Battou,
        IssenKiwami,
        HyakkiYakou
    }

    public static class SpecialCardMath
    {
        public static float Percent(float basePercent, float extraPer, int count) =>
            basePercent + extraPer * MathfMax(0, count);

        public static int AddBlood(int blood, int amount, int cap)
        {
            var next = blood + amount;
            if (next < 0)
                next = 0;
            if (cap > 0 && next > cap)
                next = cap;
            return next;
        }

        static int MathfMax(int a, int b) => a > b ? a : b;
    }

    public static class ExIds
    {
        public const string ColorStock = "color_stock";
        public const string Poison = "poison";
        public const string Paralysis = "paralysis";
        public const string Frenzy = "frenzy";
        public const string Focus = "focus";

        public static ExKind Parse(string id)
        {
            if (id == Poison) return ExKind.Poison;
            if (id == Paralysis) return ExKind.Paralysis;
            if (id == Frenzy) return ExKind.Frenzy;
            if (id == Focus) return ExKind.Focus;
            if (id == ColorStock) return ExKind.ColorStock;
            return ExKind.None;
        }
    }

    public sealed class ExSlot
    {
        public ExKind Kind;
        public int Count = 1;
        public string ColorId = "";
        public string DisplayText = "";
        public string[] Inputs = Array.Empty<string>();
        public string Text = "";
        public int Progress;
        public float Timer;
        public int CasterIndex = -1;
        public int BloodOnKill = 1;
        public bool ConsumeAllOnType;
        public bool InputLocked;

        public bool HasWord => Inputs != null && Inputs.Length > 0;

        public void SetWord(string display, string[] inputs)
        {
            DisplayText = display ?? "";
            Inputs = inputs ?? Array.Empty<string>();
            Text = Inputs.Length > 0 ? Inputs[0] : "";
            Progress = 0;
        }

        public void ClearWord()
        {
            DisplayText = "";
            Inputs = Array.Empty<string>();
            Text = "";
            Progress = 0;
        }
    }

    public readonly struct ExAddResult
    {
        public bool Accepted { get; }
        public bool Ejected { get; }
        public string EjectedColorId { get; }

        public ExAddResult(bool accepted, bool ejected, string ejectedColorId)
        {
            Accepted = accepted;
            Ejected = ejected;
            EjectedColorId = ejectedColorId ?? "";
        }
    }

    public readonly struct ExTickHit
    {
        public ExKind Kind { get; }
        public int Amount { get; }

        public ExTickHit(ExKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }
    }

    public readonly struct ExKeyResult
    {
        public bool Handled { get; }
        public bool Missed { get; }
        public bool Advanced { get; }
        public bool Completed { get; }
        public bool NeedsNewWord { get; }
        public ExKind Kind { get; }
        public string ColorId { get; }
        public string DisplayText { get; }
        public int InputLength { get; }
        public int StackCount { get; }
        public int CasterIndex { get; }
        public int BloodOnKill { get; }

        public ExKeyResult(
            bool handled,
            bool missed,
            bool advanced,
            bool completed,
            bool needsNewWord,
            ExKind kind,
            string colorId,
            string displayText,
            int inputLength,
            int stackCount = 0,
            int casterIndex = -1,
            int bloodOnKill = 0)
        {
            Handled = handled;
            Missed = missed;
            Advanced = advanced;
            Completed = completed;
            NeedsNewWord = needsNewWord;
            Kind = kind;
            ColorId = colorId ?? "";
            DisplayText = displayText ?? "";
            InputLength = inputLength;
            StackCount = stackCount;
            CasterIndex = casterIndex;
            BloodOnKill = bloodOnKill;
        }

        public static ExKeyResult None => new ExKeyResult(false, false, false, false, false, ExKind.None, "", "", 0);
    }

    /// <summary>Exエリアの枠。Unity には依存しない。</summary>
    public sealed class ExAreaState
    {
        readonly List<ExSlot> _slots = new List<ExSlot>();
        readonly int _capacity;

        public ExAreaState(int capacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public int Capacity => _capacity;
        public IReadOnlyList<ExSlot> Slots => _slots;
        public int Count => _slots.Count;

        public int Stack(ExKind kind)
        {
            var slot = Find(kind);
            return slot?.Count ?? 0;
        }

        public float Timer(ExKind kind)
        {
            var slot = Find(kind);
            return slot?.Timer ?? 0f;
        }

        public bool HasProgress
        {
            get
            {
                foreach (var slot in _slots)
                {
                    if (slot.Progress > 0)
                        return true;
                }
                return false;
            }
        }

        public void Clear() => _slots.Clear();

        public void ClearTypingProgress()
        {
            foreach (var slot in _slots)
            {
                slot.Progress = 0;
                if (slot.Inputs != null && slot.Inputs.Length > 0)
                    slot.Text = slot.Inputs[0];
            }
        }

        public bool CanAccept(ExKind kind)
        {
            if (kind == ExKind.None || kind == ExKind.ColorStock)
                return HasRoomForNewSlot();
            if (Find(kind) != null)
                return true;
            return HasRoomForNewSlot();
        }

        public ExAddResult TryAddColor(string colorId)
        {
            if (string.IsNullOrEmpty(colorId))
                return new ExAddResult(false, false, "");
            return AddNew(ExKind.ColorStock, colorId, 0, 0f, false);
        }

        public ExAddResult TryAddStatus(ExKind kind, int maxStacks, float initialTimer, bool resetTimerOnStack)
        {
            if (kind == ExKind.None || kind == ExKind.ColorStock)
                return new ExAddResult(false, false, "");
            var existing = Find(kind);
            if (existing != null)
            {
                var cap = maxStacks > 0 ? maxStacks : int.MaxValue;
                if (existing.Count < cap)
                    existing.Count++;
                if (resetTimerOnStack)
                    existing.Timer = initialTimer;
                return new ExAddResult(true, false, "");
            }

            return AddNew(kind, "", maxStacks, initialTimer, false);
        }

        public ExAddResult TryAddSpecial(ExKind kind, int casterIndex, int bloodOnKill)
        {
            if (kind != ExKind.Battou && kind != ExKind.IssenKiwami && kind != ExKind.HyakkiYakou)
                return new ExAddResult(false, false, "");
            var result = TryAddStatus(kind, 0, 0f, false);
            if (!result.Accepted)
                return result;
            var slot = Find(kind);
            if (slot == null)
                return result;
            slot.CasterIndex = casterIndex;
            if (bloodOnKill > 0)
                slot.BloodOnKill = bloodOnKill;
            return result;
        }

        public bool ConsumeAll(string kindId)
        {
            var kind = ExIds.Parse(kindId);
            if (kind == ExKind.None || kind == ExKind.ColorStock)
                return false;
            var index = IndexOf(kind);
            if (index < 0)
                return false;
            _slots.RemoveAt(index);
            return true;
        }

        public int AddStacks(ExKind kind, int amount, int maxStacks)
        {
            if (amount <= 0)
                return 0;
            var before = Stack(kind);
            for (var i = 0; i < amount; i++)
            {
                var added = TryAddStatus(kind, maxStacks, 0f, false);
                if (!added.Accepted || Stack(kind) == before + i)
                    break;
            }

            var after = Stack(kind);
            return after > before ? after - before : 0;
        }

        public int ConsumeStacks(ExKind kind, int amount)
        {
            var slot = Find(kind);
            if (slot == null || amount <= 0)
                return 0;
            var used = amount < slot.Count ? amount : slot.Count;
            slot.Count -= used;
            if (slot.Count <= 0)
                _slots.Remove(slot);
            return used;
        }

        public void SetStacks(ExKind kind, int count, int maxStacks)
        {
            var cap = maxStacks > 0 ? maxStacks : int.MaxValue;
            if (count < 0)
                count = 0;
            if (count > cap)
                count = cap;
            var slot = Find(kind);
            if (count <= 0)
            {
                if (slot != null)
                    _slots.Remove(slot);
                return;
            }

            if (slot == null)
            {
                if (!TryAddStatus(kind, maxStacks, 0f, false).Accepted)
                    return;
                slot = Find(kind);
            }

            if (slot != null)
                slot.Count = count;
        }

        public void Tick(float dt, float poisonInterval, int poisonDamagePerStack, float paralysisInterval, int paralysisDamagePerStack, float frenzyInterval, int frenzyMadness, List<ExTickHit> hits)
        {
            if (dt <= 0f || hits == null)
                return;
            TickDamage(ExKind.Poison, poisonInterval, poisonDamagePerStack, dt, hits);
            TickDamage(ExKind.Paralysis, paralysisInterval, paralysisDamagePerStack, dt, hits);
            TickFrenzy(frenzyInterval, frenzyMadness, dt, hits);
        }

        public bool MatchesFirst(char key)
        {
            if (HasProgress)
                return false;
            foreach (var slot in _slots)
            {
                if (slot.InputLocked)
                    continue;
                if (StartsWith(slot, key.ToString()))
                    return true;
            }
            return false;
        }

        public ExKeyResult ProcessKey(char key, float frenzyResetSeconds)
        {
            var continuing = HasProgress;
            var advanced = false;
            var done = ExKeyResult.None;
            var slots = _slots.ToArray();
            var colorClaimed = false;
            foreach (var slot in slots)
            {
                if (!slot.HasWord || slot.InputLocked)
                    continue;
                if (slot.Kind == ExKind.ColorStock && colorClaimed)
                    continue;
                if (continuing)
                {
                    if (slot.Progress <= 0)
                        continue;
                }
                else if (!StartsWith(slot, key.ToString()))
                {
                    continue;
                }

                var result = Advance(slot, key, frenzyResetSeconds, continuing);
                if (slot.Kind == ExKind.ColorStock && (result.Completed || (result.Advanced && !result.Missed)))
                    colorClaimed = true;
                if (result.Completed && !done.Completed)
                    done = result;
                else if (result.Advanced && !result.Missed)
                    advanced = true;
            }

            if (done.Completed)
                return done;
            if (advanced)
                return new ExKeyResult(true, false, true, false, false, ExKind.None, "", "", 0);
            if (continuing)
                return new ExKeyResult(true, true, false, false, false, ExKind.None, "", "", 0);
            return ExKeyResult.None;
        }

        public void CollectFirstLetters(ISet<char> into, ExSlot except)
        {
            if (into == null)
                return;
            foreach (var slot in _slots)
            {
                if (slot == except || slot.Inputs == null)
                    continue;
                foreach (var input in slot.Inputs)
                {
                    if (!string.IsNullOrEmpty(input))
                        into.Add(input[0]);
                }
            }
        }

        public bool ClashesWith(IReadOnlyCollection<char> screenFirst)
        {
            var seen = new HashSet<char>();
            if (screenFirst != null)
            {
                foreach (var c in screenFirst)
                    seen.Add(c);
            }
            foreach (var slot in _slots)
            {
                var mine = FirstLetters(slot);
                foreach (var c in mine)
                {
                    if (seen.Contains(c))
                        return true;
                }
                foreach (var c in mine)
                    seen.Add(c);
            }
            return false;
        }

        public ExSlot FirstClashingSlot(IReadOnlyCollection<char> screenFirst)
        {
            var seen = new HashSet<char>();
            if (screenFirst != null)
            {
                foreach (var c in screenFirst)
                    seen.Add(c);
            }
            foreach (var slot in _slots)
            {
                var mine = FirstLetters(slot);
                if (mine.Count == 0)
                    continue;
                var hit = false;
                foreach (var c in mine)
                {
                    if (seen.Contains(c))
                        hit = true;
                }
                if (hit)
                    return slot;
                foreach (var c in mine)
                    seen.Add(c);
            }
            return null;
        }

        static HashSet<char> FirstLetters(ExSlot slot)
        {
            var mine = new HashSet<char>();
            if (slot?.Inputs == null)
                return mine;
            foreach (var input in slot.Inputs)
            {
                if (!string.IsNullOrEmpty(input))
                    mine.Add(input[0]);
            }
            return mine;
        }

        ExAddResult AddNew(ExKind kind, string colorId, int maxStacks, float initialTimer, bool unused)
        {
            _ = unused;
            _ = maxStacks;
            string ejected = null;
            if (_slots.Count >= _capacity)
            {
                var index = OldestColorIndex();
                if (index < 0)
                    return new ExAddResult(false, false, "");
                ejected = _slots[index].ColorId;
                _slots.RemoveAt(index);
            }

            var slot = new ExSlot
            {
                Kind = kind,
                Count = 1,
                ColorId = colorId ?? "",
                Timer = initialTimer
            };
            _slots.Add(slot);
            return new ExAddResult(true, ejected != null, ejected);
        }

        bool HasRoomForNewSlot() => _slots.Count < _capacity || OldestColorIndex() >= 0;

        int OldestColorIndex()
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Kind == ExKind.ColorStock)
                    return i;
            }
            return -1;
        }

        ExSlot Find(ExKind kind)
        {
            foreach (var slot in _slots)
            {
                if (slot.Kind == kind)
                    return slot;
            }
            return null;
        }

        int IndexOf(ExKind kind)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Kind == kind)
                    return i;
            }
            return -1;
        }

        static bool StartsWith(ExSlot slot, string prefix)
        {
            if (slot?.Inputs == null || string.IsNullOrEmpty(prefix))
                return false;
            foreach (var input in slot.Inputs)
            {
                if (!string.IsNullOrEmpty(input) && input.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        ExKeyResult Advance(ExSlot slot, char key, float frenzyResetSeconds, bool committed)
        {
            var current = slot.Progress > 0 && slot.Text.Length >= slot.Progress
                ? slot.Text.Substring(0, slot.Progress)
                : "";
            var next = current + key;
            string matched = null;
            var finished = false;
            if (slot.Inputs != null)
            {
                foreach (var input in slot.Inputs)
                {
                    if (string.IsNullOrEmpty(input) || !input.StartsWith(next, StringComparison.Ordinal))
                        continue;
                    if (matched == null)
                        matched = input;
                    if (input.Length == next.Length)
                        finished = true;
                }
            }

            if (matched == null)
            {
                slot.Progress = 0;
                if (!string.IsNullOrEmpty(slot.Text) && slot.Inputs != null && slot.Inputs.Length > 0)
                    slot.Text = slot.Inputs[0];
                return committed
                    ? new ExKeyResult(true, true, false, false, false, slot.Kind, slot.ColorId, slot.DisplayText, 0)
                    : ExKeyResult.None;
            }

            slot.Text = matched;
            slot.Progress = next.Length;
            if (!finished)
                return new ExKeyResult(true, false, true, false, false, slot.Kind, slot.ColorId, slot.DisplayText, 0);

            var kind = slot.Kind;
            var colorId = slot.ColorId;
            var display = slot.DisplayText;
            var length = next.Length;
            var used = slot.Count;
            var casterIndex = slot.CasterIndex;
            var bloodOnKill = slot.BloodOnKill;
            var needsWord = false;
            if (kind == ExKind.ColorStock || slot.ConsumeAllOnType)
            {
                _slots.Remove(slot);
            }
            else
            {
                slot.Count--;
                slot.ClearWord();
                if (kind == ExKind.Frenzy)
                    slot.Timer = frenzyResetSeconds;
                if (slot.Count <= 0)
                    _slots.Remove(slot);
                else
                    needsWord = true;
            }

            return new ExKeyResult(
                true, false, true, true, needsWord, kind, colorId, display, length, used, casterIndex, bloodOnKill);
        }

        void TickDamage(ExKind kind, float interval, int damagePerStack, float dt, List<ExTickHit> hits)
        {
            if (interval <= 0f || damagePerStack == 0)
                return;
            var slot = Find(kind);
            if (slot == null || slot.Count <= 0)
                return;
            slot.Timer -= dt;
            var guard = 0;
            while (slot.Timer <= 0f && guard++ < 32)
            {
                hits.Add(new ExTickHit(kind, damagePerStack * slot.Count));
                slot.Timer += interval;
            }
        }

        void TickFrenzy(float interval, int madness, float dt, List<ExTickHit> hits)
        {
            if (interval <= 0f)
                return;
            var slot = Find(ExKind.Frenzy);
            if (slot == null || slot.Count <= 0)
                return;
            slot.Timer -= dt;
            var guard = 0;
            while (slot.Timer <= 0f && slot.Count > 0 && guard++ < 32)
            {
                hits.Add(new ExTickHit(ExKind.Frenzy, madness));
                slot.Count--;
                slot.Timer += interval;
                if (slot.Count <= 0)
                {
                    _slots.Remove(slot);
                    break;
                }
            }
        }

        public static string SelfCheck()
        {
            var board = new ExAreaState(5);
            board.TryAddStatus(ExKind.Poison, 0, 3f, false);
            board.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            board.TryAddStatus(ExKind.Paralysis, 0, 4f, false);
            var red1 = board.TryAddColor("red");
            var red2 = board.TryAddColor("red");
            if (!red1.Accepted || !red2.Accepted || board.Count != 5)
                return "赤2枚が入らない";
            var blue = board.TryAddColor("blue");
            if (!blue.Accepted || !blue.Ejected || blue.EjectedColorId != "red")
                return "いちばん古い赤が押し出されていない";
            if (board.Slots[3].ColorId != "red" || board.Slots[4].ColorId != "blue")
                return "残った色ストックの順が違う";

            var full = new ExAreaState(4);
            full.TryAddStatus(ExKind.Poison, 0, 3f, false);
            full.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            full.TryAddStatus(ExKind.Paralysis, 0, 4f, false);
            full.TryAddStatus(ExKind.Focus, 20, 0f, false);
            if (full.TryAddColor("red").Accepted)
                return "バフだけで埋まった枠に色ストックが入った";
            if (full.TryAddStatus(ExKind.Poison, 0, 3f, false).Accepted == false || full.Stack(ExKind.Poison) != 2)
                return "同じ毒が×2にならない";
            if (full.Count != 4)
                return "毒の加算で枠が増えた";

            var frenzy = new ExAreaState(5);
            frenzy.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            frenzy.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            if (frenzy.Stack(ExKind.Frenzy) != 2 || frenzy.Count != 1)
                return "伝播が1枠にまとまらない";
            if (Math.Abs(frenzy.Timer(ExKind.Frenzy) - 15f) > 0.001f)
                return "伝播を足したときタイマーが戻らない";
            var hits = new List<ExTickHit>();
            frenzy.Tick(15f, 3f, 3, 4f, 2, 15f, 100, hits);
            if (hits.Count != 1 || hits[0].Amount != 100 || frenzy.Stack(ExKind.Frenzy) != 1)
                return "伝播の爆発が1つ減らない";
            if (Math.Abs(frenzy.Timer(ExKind.Frenzy) - 15f) > 0.001f)
                return "爆発後に15秒へ戻らない";
            frenzy.TryAddStatus(ExKind.Frenzy, 0, 15f, true);
            frenzy.Slots[0].SetWord("炎", new[] { "honoo" });
            var typed = frenzy.ProcessKey('h', 15f);
            if (!typed.Advanced || typed.Completed)
                return "伝播の単語の1文字目が進まない";
            typed = frenzy.ProcessKey('o', 15f);
            typed = frenzy.ProcessKey('n', 15f);
            typed = frenzy.ProcessKey('o', 15f);
            typed = frenzy.ProcessKey('o', 15f);
            if (!typed.Completed || !typed.NeedsNewWord || frenzy.Stack(ExKind.Frenzy) != 1)
                return "伝播の単語で1つ消えない";
            if (Math.Abs(frenzy.Timer(ExKind.Frenzy) - 15f) > 0.001f)
                return "単語のあとタイマーが戻らない";

            var poison = new ExAreaState(5);
            poison.TryAddStatus(ExKind.Poison, 0, 3f, false);
            poison.TryAddStatus(ExKind.Poison, 0, 3f, false);
            hits.Clear();
            poison.Tick(3f, 3f, 3, 4f, 2, 15f, 100, hits);
            if (hits.Count != 1 || hits[0].Kind != ExKind.Poison || hits[0].Amount != 6)
                return "毒×2のダメージが6ではない";

            var focus = new ExAreaState(5);
            for (var i = 0; i < 22; i++)
                focus.TryAddStatus(ExKind.Focus, 20, 0f, false);
            if (focus.Stack(ExKind.Focus) != 20 || !focus.ConsumeAll(ExIds.Focus) || focus.Stack(ExKind.Focus) != 0)
                return "集中の上限か全消費が違う";

            var shared = new ExAreaState(5);
            shared.TryAddStatus(ExKind.Poison, 0, 3f, false);
            shared.TryAddStatus(ExKind.Paralysis, 0, 4f, false);
            shared.Slots[0].SetWord("人間", new[] { "ningen" });
            shared.Slots[1].SetWord("人参", new[] { "ninjin" });
            var sharedKey = shared.ProcessKey('n', 15f);
            if (!sharedKey.Advanced || sharedKey.Completed || shared.Slots[0].Progress != 1 || shared.Slots[1].Progress != 1)
                return "同じ頭文字でExの両方が進まない";
            shared.ProcessKey('i', 15f);
            shared.ProcessKey('n', 15f);
            if (shared.Slots[0].Progress != 3 || shared.Slots[1].Progress != 3)
                return "共有のninで片方が外れる";
            var branch = shared.ProcessKey('j', 15f);
            if (!branch.Advanced || branch.Missed || shared.Slots[0].Progress != 0 || shared.Slots[1].Progress != 4)
                return "分かれた文字で一致しない側が残る";
            return null;
        }
    }
}
