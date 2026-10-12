using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;

namespace TypingRoguelike.Run
{
    /// <summary>選択肢の名前を、戦闘と同じローマ字揺れで1文字ずつ進める。</summary>
    public sealed class EventKanaReading
    {
        public readonly struct Mora
        {
            public string Display { get; }
            public string[] Romaji { get; }

            public Mora(string display, string[] romaji)
            {
                Display = display ?? string.Empty;
                Romaji = romaji ?? System.Array.Empty<string>();
            }
        }

        readonly List<Mora> _morae;
        int _moraIndex;
        string _prefix = string.Empty;

        EventKanaReading(List<Mora> morae) => _morae = morae;

        public bool IsComplete => _moraIndex >= _morae.Count;

        public string TypedText
        {
            get
            {
                var text = string.Empty;
                var count = UnityEngine.Mathf.Min(_moraIndex, _morae.Count);
                for (var i = 0; i < count; i++)
                    text += _morae[i].Display;
                return text;
            }
        }

        public string RestText
        {
            get
            {
                var text = string.Empty;
                for (var i = _moraIndex; i < _morae.Count; i++)
                    text += _morae[i].Display;
                return text;
            }
        }

        public void Reset()
        {
            _moraIndex = 0;
            _prefix = string.Empty;
        }

        public bool TryAdvance(char key)
        {
            if (IsComplete)
                return false;

            var next = _prefix + key;
            var romaji = _morae[_moraIndex].Romaji;
            if (!romaji.Any(variant => variant.StartsWith(next, System.StringComparison.Ordinal)))
                return false;

            _prefix = next;
            if (romaji.Any(variant => variant.Length == next.Length))
            {
                _moraIndex++;
                _prefix = string.Empty;
            }

            return true;
        }

        public static EventKanaReading FromLabel(string label)
        {
            var morae = new List<Mora>();
            var text = label ?? string.Empty;
            var i = 0;
            while (i < text.Length)
            {
                if (!TryReadMora(text, i, out var mora, out var next))
                    break;
                morae.Add(mora);
                i = next;
            }

            return new EventKanaReading(morae);
        }

        static bool TryReadMora(string text, int index, out Mora mora, out int next)
        {
            mora = default;
            next = index;
            if (index >= text.Length)
                return false;

            var c = text[index];
            if (c is 'っ' or 'ッ')
            {
                if (!TryReadMora(text, index + 1, out var rest, out var restNext) || rest.Romaji.Length == 0)
                {
                    mora = new Mora(c.ToString(), System.Array.Empty<string>());
                    next = index + 1;
                    return true;
                }

                mora = new Mora(c + rest.Display, DoubleConsonant(rest.Romaji));
                next = restNext;
                return true;
            }

            if (index + 1 < text.Length && TryYoon(c, text[index + 1], out var yoon))
            {
                mora = new Mora(c.ToString() + text[index + 1], TypingEngine.ExpandInputVariants(yoon));
                next = index + 2;
                return true;
            }

            if (BaseRomaji.TryGetValue(c, out var romaji))
            {
                mora = new Mora(c.ToString(), TypingEngine.ExpandInputVariants(romaji));
                next = index + 1;
                return true;
            }

            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                mora = new Mora(char.ToLowerInvariant(c).ToString(), TypingEngine.ExpandInputVariants(char.ToLowerInvariant(c).ToString()));
                next = index + 1;
                return true;
            }

            mora = new Mora(c.ToString(), System.Array.Empty<string>());
            next = index + 1;
            return true;
        }

        static string[] DoubleConsonant(string[] variants)
        {
            var doubled = new List<string>();
            foreach (var variant in variants)
            {
                if (string.IsNullOrEmpty(variant))
                    continue;
                var head = variant[0];
                if (head is 'a' or 'i' or 'u' or 'e' or 'o' or 'n')
                    doubled.Add(variant);
                else
                    doubled.Add(head + variant);
            }

            return doubled.SelectMany(TypingEngine.ExpandInputVariants).Distinct().ToArray();
        }

        static bool TryYoon(char kana, char small, out string romaji)
        {
            romaji = null;
            var vowel = small switch
            {
                'ゃ' or 'ャ' => "ya",
                'ゅ' or 'ュ' => "yu",
                'ょ' or 'ョ' => "yo",
                _ => null
            };
            if (vowel == null || !YoonStem.TryGetValue(kana, out var stem))
                return false;
            if (stem is "sh" or "ch" or "j")
            {
                var tail = vowel switch
                {
                    "ya" => "a",
                    "yu" => "u",
                    "yo" => "o",
                    _ => vowel
                };
                romaji = stem + tail;
            }
            else
                romaji = stem + vowel;
            return true;
        }

        static readonly Dictionary<char, string> YoonStem = new()
        {
            ['き'] = "k", ['ぎ'] = "g", ['し'] = "sh", ['じ'] = "j", ['ち'] = "ch", ['ぢ'] = "j",
            ['に'] = "n", ['ひ'] = "h", ['び'] = "b", ['ぴ'] = "p", ['み'] = "m", ['り'] = "r",
            ['キ'] = "k", ['ギ'] = "g", ['シ'] = "sh", ['ジ'] = "j", ['チ'] = "ch", ['ヂ'] = "j",
            ['ニ'] = "n", ['ヒ'] = "h", ['ビ'] = "b", ['ピ'] = "p", ['ミ'] = "m", ['リ'] = "r",
        };

        static readonly Dictionary<char, string> BaseRomaji = new()
        {
            ['あ'] = "a", ['い'] = "i", ['う'] = "u", ['え'] = "e", ['お'] = "o",
            ['か'] = "ka", ['き'] = "ki", ['く'] = "ku", ['け'] = "ke", ['こ'] = "ko",
            ['が'] = "ga", ['ぎ'] = "gi", ['ぐ'] = "gu", ['げ'] = "ge", ['ご'] = "go",
            ['さ'] = "sa", ['し'] = "shi", ['す'] = "su", ['せ'] = "se", ['そ'] = "so",
            ['ざ'] = "za", ['じ'] = "ji", ['ず'] = "zu", ['ぜ'] = "ze", ['ぞ'] = "zo",
            ['た'] = "ta", ['ち'] = "chi", ['つ'] = "tsu", ['て'] = "te", ['と'] = "to",
            ['だ'] = "da", ['ぢ'] = "di", ['づ'] = "du", ['で'] = "de", ['ど'] = "do",
            ['な'] = "na", ['に'] = "ni", ['ぬ'] = "nu", ['ね'] = "ne", ['の'] = "no",
            ['は'] = "ha", ['ひ'] = "hi", ['ふ'] = "fu", ['へ'] = "he", ['ほ'] = "ho",
            ['ば'] = "ba", ['び'] = "bi", ['ぶ'] = "bu", ['べ'] = "be", ['ぼ'] = "bo",
            ['ぱ'] = "pa", ['ぴ'] = "pi", ['ぷ'] = "pu", ['ぺ'] = "pe", ['ぽ'] = "po",
            ['ま'] = "ma", ['み'] = "mi", ['む'] = "mu", ['め'] = "me", ['も'] = "mo",
            ['や'] = "ya", ['ゆ'] = "yu", ['よ'] = "yo",
            ['ら'] = "ra", ['り'] = "ri", ['る'] = "ru", ['れ'] = "re", ['ろ'] = "ro",
            ['わ'] = "wa", ['を'] = "wo", ['ん'] = "n",
            ['ア'] = "a", ['イ'] = "i", ['ウ'] = "u", ['エ'] = "e", ['オ'] = "o",
            ['カ'] = "ka", ['キ'] = "ki", ['ク'] = "ku", ['ケ'] = "ke", ['コ'] = "ko",
            ['ガ'] = "ga", ['ギ'] = "gi", ['グ'] = "gu", ['ゲ'] = "ge", ['ゴ'] = "go",
            ['サ'] = "sa", ['シ'] = "shi", ['ス'] = "su", ['セ'] = "se", ['ソ'] = "so",
            ['ザ'] = "za", ['ジ'] = "ji", ['ズ'] = "zu", ['ゼ'] = "ze", ['ゾ'] = "zo",
            ['タ'] = "ta", ['チ'] = "chi", ['ツ'] = "tsu", ['テ'] = "te", ['ト'] = "to",
            ['ダ'] = "da", ['ヂ'] = "di", ['ヅ'] = "du", ['デ'] = "de", ['ド'] = "do",
            ['ナ'] = "na", ['ニ'] = "ni", ['ヌ'] = "nu", ['ネ'] = "ne", ['ノ'] = "no",
            ['ハ'] = "ha", ['ヒ'] = "hi", ['フ'] = "fu", ['ヘ'] = "he", ['ホ'] = "ho",
            ['バ'] = "ba", ['ビ'] = "bi", ['ブ'] = "bu", ['ベ'] = "be", ['ボ'] = "bo",
            ['パ'] = "pa", ['ピ'] = "pi", ['プ'] = "pu", ['ペ'] = "pe", ['ポ'] = "po",
            ['マ'] = "ma", ['ミ'] = "mi", ['ム'] = "mu", ['メ'] = "me", ['モ'] = "mo",
            ['ヤ'] = "ya", ['ユ'] = "yu", ['ヨ'] = "yo",
            ['ラ'] = "ra", ['リ'] = "ri", ['ル'] = "ru", ['レ'] = "re", ['ロ'] = "ro",
            ['ワ'] = "wa", ['ヲ'] = "wo", ['ン'] = "n",
        };
    }
}
