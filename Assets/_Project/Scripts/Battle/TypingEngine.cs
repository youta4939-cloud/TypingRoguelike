using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public readonly struct WordCompletion
    {
        public ElementColor Color { get; }
        public int InputLength { get; }
        public string DisplayText { get; }

        public WordCompletion(ElementColor color, int inputLength, string displayText)
        {
            Color = color;
            InputLength = inputLength;
            DisplayText = displayText ?? string.Empty;
        }
    }

    public readonly struct KeyProcessResult
    {
        public WordCompletion? Completed { get; }
        public bool Advanced { get; }
        public bool Missed { get; }

        public KeyProcessResult(WordCompletion? completed, bool advanced, bool missed)
        {
            Completed = completed;
            Advanced = advanced;
            Missed = missed;
        }
    }

    public sealed class TypingEngine
    {
        static readonly ElementColor[] AllColors =
        {
            ElementColor.Blue,
            ElementColor.Red,
            ElementColor.Green,
            ElementColor.Yellow,
            ElementColor.Purple
        };

        readonly TypingWordEntry[] _wordPool;
        readonly System.Random _random;
        readonly WordSlotState[] _slots = { new(), new(), new() };

        public IReadOnlyList<WordSlotState> Slots => _slots;
        public int RerollCount { get; private set; }

        public TypingEngine(string[] wordPool, int rerollsPerBattle = 3, int? seed = null)
        {
            var source = wordPool is { Length: > 0 } ? wordPool : new[] { "走る|hashiru", "勝利|shouri", "星|hoshi" };
            _wordPool = source
                .Select(ParseWord)
                .GroupBy(word => word.DisplayText)
                .Select(group => new TypingWordEntry(
                    group.Key,
                    group.SelectMany(word => word.InputOptions).Distinct().ToArray()))
                .ToArray();
            RerollCount = rerollsPerBattle;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public void GenerateWords() => GenerateWords(null);

        public void GenerateWords(IReadOnlyCollection<char> avoidFirst)
        {
            var preferred = new List<TypingWordEntry>();
            var rest = new List<TypingWordEntry>();
            foreach (var word in _wordPool)
            {
                if (avoidFirst != null && avoidFirst.Count > 0 && FirstLettersOverlap(word.InputOptions, avoidFirst))
                    rest.Add(word);
                else
                    preferred.Add(word);
            }
            Shuffle(preferred);
            Shuffle(rest);
            var picked = new List<TypingWordEntry>(3);
            foreach (var word in preferred)
            {
                if (picked.Count >= 3)
                    break;
                picked.Add(word);
            }
            foreach (var word in rest)
            {
                if (picked.Count >= 3)
                    break;
                picked.Add(word);
            }
            if (picked.Count == 0)
                picked.Add(new TypingWordEntry("？", new[] { "?" }));

            var colors = AllColors.OrderBy(_ => _random.Next()).Take(3).ToArray();
            for (var i = 0; i < 3; i++)
            {
                var word = picked[i % picked.Count];
                _slots[i].Color = colors[i];
                _slots[i].DisplayText = word.DisplayText;
                _slots[i].InputOptions = word.InputOptions;
                _slots[i].Text = word.InputOptions[0];
                _slots[i].Progress = 0;
                _slots[i].Active = true;
            }
        }

        public bool TryPickWord(IReadOnlyCollection<char> blockedFirst, out string display, out string[] inputs)
        {
            display = null;
            inputs = null;
            var choices = new List<TypingWordEntry>();
            foreach (var word in _wordPool)
            {
                if (FirstLettersOverlap(word.InputOptions, blockedFirst))
                    continue;
                choices.Add(word);
            }
            if (choices.Count == 0)
                return false;
            var pick = choices[_random.Next(choices.Count)];
            display = pick.DisplayText;
            inputs = pick.InputOptions;
            return true;
        }

        public void CollectFirstLetters(ISet<char> into)
        {
            if (into == null)
                return;
            foreach (var slot in _slots)
                AddFirstLetters(slot.InputOptions, into);
        }

        public static bool FirstLettersOverlap(string[] inputs, IReadOnlyCollection<char> blocked)
        {
            if (inputs == null || blocked == null || blocked.Count == 0)
                return false;
            foreach (var input in inputs)
            {
                if (!string.IsNullOrEmpty(input) && blocked.Contains(input[0]))
                    return true;
            }
            return false;
        }

        static void AddFirstLetters(string[] inputs, ISet<char> into)
        {
            if (inputs == null)
                return;
            foreach (var input in inputs)
            {
                if (!string.IsNullOrEmpty(input))
                    into.Add(input[0]);
            }
        }

        void Shuffle(List<TypingWordEntry> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        public bool TryReroll(IReadOnlyCollection<char> avoidFirst = null)
        {
            if (RerollCount <= 0)
                return false;
            RerollCount--;
            GenerateWords(avoidFirst);
            return true;
        }

        public void AddReroll(int amount, int maxRerolls, bool ignoreCap = false)
        {
            if (amount <= 0)
                return;
            RerollCount += amount;
            if (!ignoreCap)
                RerollCount = Mathf.Min(RerollCount, Mathf.Max(0, maxRerolls));
        }

        public void ClearTypingProgress()
        {
            foreach (var slot in _slots)
                slot.Progress = 0;
        }

        public KeyProcessResult ProcessKey(char key) => ProcessKey(key, true);

        /// <param name="resetIfNone">
        /// どの単語も続かなかったとき、全部を入力前へ戻す。
        /// Ex側がまだ続いているときは false にして、外れた単語を入力中のまま戻さない。
        /// </param>
        public KeyProcessResult ProcessKey(char key, bool resetIfNone)
        {
            WordCompletion? completed = null;
            var advanced = false;

            foreach (var slot in _slots)
            {
                if (!slot.Active)
                    continue;

                var currentPrefix = slot.Progress > 0 && slot.Text.Length >= slot.Progress
                    ? slot.Text.Substring(0, slot.Progress)
                    : string.Empty;
                var nextPrefix = currentPrefix + key;
                var matching = slot.InputOptions
                    .Where(input => input.StartsWith(nextPrefix, StringComparison.Ordinal))
                    .ToArray();

                if (matching.Length > 0)
                {
                    advanced = true;
                    if (!slot.Text.StartsWith(nextPrefix, StringComparison.Ordinal))
                        slot.Text = matching[0];
                    slot.Progress = nextPrefix.Length;
                    if (matching.Any(input => input.Length == nextPrefix.Length))
                        completed = new WordCompletion(slot.Color, nextPrefix.Length, slot.DisplayText);
                }
                else
                {
                    slot.Progress = 0;
                    slot.Active = false;
                }
            }

            var anyActive = _slots.Any(s => s.Active && s.Progress > 0);
            if (!anyActive && !completed.HasValue && resetIfNone)
                ResetActivity();

            var missed = !advanced && !completed.HasValue;
            return new KeyProcessResult(completed, advanced, missed);
        }

        public void ResetActivity()
        {
            foreach (var slot in _slots)
            {
                slot.Progress = 0;
                slot.Active = true;
            }
        }

        public void ParkAll()
        {
            foreach (var slot in _slots)
            {
                slot.Progress = 0;
                slot.Active = false;
            }
        }

        static TypingWordEntry ParseWord(string encoded)
        {
            var parts = (encoded ?? string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var display = parts.Length > 0 ? parts[0].Trim() : "?";
            var sourceInputs = parts.Length > 1
                ? parts.Skip(1)
                : new[] { display };

            var inputs = new List<string>();
            foreach (var source in sourceInputs)
            {
                foreach (var variant in ExpandRomajiVariants(source.Trim().ToLowerInvariant()))
                {
                    if (!string.IsNullOrEmpty(variant) && !inputs.Contains(variant))
                        inputs.Add(variant);
                }
            }

            if (inputs.Count == 0)
                inputs.Add("?");
            return new TypingWordEntry(display, inputs.ToArray());
        }

        /// <summary>戦闘のローマ字揺れ（si/shi、ん の nn など）を広げる。</summary>
        public static string[] ExpandInputVariants(string canonical)
        {
            if (string.IsNullOrWhiteSpace(canonical))
                return Array.Empty<string>();
            return ExpandRomajiVariants(canonical.Trim().ToLowerInvariant()).ToArray();
        }

        static IEnumerable<string> ExpandRomajiVariants(string canonical)
        {
            var results = new HashSet<string>();
            var orderedResults = new List<string>();
            var queue = new Queue<string>();
            foreach (var variant in ExpandSyllabicNVariants(canonical))
            {
                if (results.Add(variant))
                {
                    orderedResults.Add(variant);
                    queue.Enqueue(variant);
                }
            }
            var replacements = new[]
            {
                ("shi", "si"),
                ("sh", "sy"),
                ("chi", "ti"),
                ("ch", "ty"),
                ("tsu", "tu"),
                ("ji", "zi"),
                ("fu", "hu")
            };

            while (queue.Count > 0 && results.Count < 32)
            {
                var current = queue.Dequeue();
                foreach (var (from, to) in replacements)
                {
                    if (!current.Contains(from))
                        continue;
                    var variant = current.Replace(from, to);
                    if (results.Add(variant))
                    {
                        orderedResults.Add(variant);
                        queue.Enqueue(variant);
                    }
                }
            }

            return orderedResults;
        }

        static IEnumerable<string> ExpandSyllabicNVariants(string canonical)
        {
            var variants = new List<string> { string.Empty };
            for (var i = 0; i < canonical.Length; i++)
            {
                var currentCount = variants.Count;
                var c = canonical[i];
                for (var j = 0; j < currentCount; j++)
                    variants[j] += c;

                if (c != 'n')
                    continue;

                var isFinalN = i == canonical.Length - 1;
                var next = isFinalN ? '\0' : canonical[i + 1];
                var isSyllabicN = isFinalN || (!IsVowel(next) && next != 'y' && next != 'n');
                if (!isSyllabicN)
                    continue;

                for (var j = 0; j < currentCount; j++)
                    variants.Add(variants[j] + "n");
            }

            return variants;
        }

        static bool IsVowel(char c)
        {
            return c == 'a' || c == 'i' || c == 'u' || c == 'e' || c == 'o';
        }

        sealed class TypingWordEntry
        {
            public string DisplayText { get; }
            public string[] InputOptions { get; }

            public TypingWordEntry(string displayText, string[] inputOptions)
            {
                DisplayText = displayText;
                InputOptions = inputOptions;
            }
        }
    }

    public sealed class JackpotFreezeSystem
    {
        float _baseChance;
        float _chancePerCharacter;
        float _maxFinalChance;
        float _baseAccumulationCap;

        public float BaseAccumulatedChance { get; private set; }
        public float LastBonusJackpotChanceApplied { get; private set; }

        public void Configure(
            float baseChance,
            float chancePerCharacter,
            float maxFinalChance,
            float baseAccumulationCap = 0.1f)
        {
            _baseChance = Mathf.Clamp01(baseChance);
            _chancePerCharacter = Mathf.Max(0f, chancePerCharacter);
            _maxFinalChance = Mathf.Clamp(maxFinalChance, _baseChance, 1f);
            _baseAccumulationCap = Mathf.Clamp(baseAccumulationCap, _baseChance, 1f);
            Reset();
        }

        /// <summary>単語1完了ごとに1回抽選。外れ時は文字数 × 固定値を足す。今の蓄積量では変えない。</summary>
        public bool RegisterWordAndRoll(
            float bonusJackpotChance,
            int completedCharacterCount,
            float? debugRollChanceOverride = null)
        {
            LastBonusJackpotChanceApplied = Mathf.Max(0f, bonusJackpotChance);
            var rollChance = debugRollChanceOverride.HasValue
                ? Mathf.Clamp01(debugRollChanceOverride.Value)
                : ResolveRollChance(LastBonusJackpotChanceApplied);
            var won = UnityEngine.Random.value < rollChance;
            if (!won)
            {
                var chars = Mathf.Max(1, completedCharacterCount);
                BaseAccumulatedChance = Mathf.Min(
                    _baseAccumulationCap,
                    BaseAccumulatedChance + _chancePerCharacter * chars);
            }

            return won;
        }

        public float ResolveRollChance(float bonusJackpotChance) =>
            Mathf.Min(_maxFinalChance, BaseAccumulatedChance + Mathf.Max(0f, bonusJackpotChance));

        public void Reset()
        {
            BaseAccumulatedChance = _baseChance;
            LastBonusJackpotChanceApplied = 0f;
        }

        /// <summary>ラン持ち越しの蓄積率を適用（Configure / Reset の後に呼ぶ）。</summary>
        public void ApplyRunAccumulatedChance(float fromRun)
        {
            BaseAccumulatedChance = Mathf.Clamp(fromRun, _baseChance, _baseAccumulationCap);
        }
    }
}
