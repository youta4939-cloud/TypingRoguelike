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

        public WordCompletion(ElementColor color, int inputLength)
        {
            Color = color;
            InputLength = inputLength;
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

        public void GenerateWords()
        {
            var words = _wordPool.OrderBy(_ => _random.Next()).Take(3).ToArray();
            var colors = AllColors.OrderBy(_ => _random.Next()).Take(3).ToArray();

            for (var i = 0; i < 3; i++)
            {
                var word = words[i % words.Length];
                _slots[i].Color = colors[i];
                _slots[i].DisplayText = word.DisplayText;
                _slots[i].InputOptions = word.InputOptions;
                _slots[i].Text = word.InputOptions[0];
                _slots[i].Progress = 0;
                _slots[i].Active = true;
            }
        }

        public bool TryReroll()
        {
            if (RerollCount <= 0)
                return false;
            RerollCount--;
            GenerateWords();
            return true;
        }

        public void AddReroll(int amount, int maxRerolls)
        {
            if (amount <= 0)
                return;
            RerollCount = Mathf.Min(RerollCount + amount, Mathf.Max(0, maxRerolls));
        }

        public KeyProcessResult ProcessKey(char key)
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
                        completed = new WordCompletion(slot.Color, nextPrefix.Length);
                }
                else
                {
                    slot.Progress = 0;
                    slot.Active = false;
                }
            }

            var anyActive = _slots.Any(s => s.Active && s.Progress > 0);
            if (!anyActive && !completed.HasValue)
            {
                foreach (var slot in _slots)
                {
                    slot.Progress = 0;
                    slot.Active = true;
                }
            }

            var missed = !advanced && !completed.HasValue;
            return new KeyProcessResult(completed, advanced, missed);
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

        /// <summary>単語1完了ごとに1回抽選。外れ時は完了文字数 × 1文字あたり加算。</summary>
        public bool RegisterWordAndRoll(float bonusJackpotChance, int completedCharacterCount)
        {
            LastBonusJackpotChanceApplied = Mathf.Max(0f, bonusJackpotChance);
            var rollChance = ResolveRollChance(LastBonusJackpotChanceApplied);
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
