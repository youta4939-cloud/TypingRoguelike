using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// 立ち絵 JSON 用の最小パーサ。辞書を含むため JsonUtility では読めない。
    /// </summary>
    public sealed class JNode
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type;
        public bool Bool;
        public double Number;
        public string String;
        public List<JNode> Items;
        public Dictionary<string, JNode> Map;

        public static JNode Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new JNode { Type = Kind.Null };
            var parser = new Parser(json);
            var value = parser.ReadValue();
            parser.Skip();
            if (parser.More)
                throw new FormatException("JSON の末尾に余分な文字があります (" + parser.Index + ")");
            return value;
        }

        public JNode Get(string key)
        {
            if (Map != null && Map.TryGetValue(key, out var value))
                return value;
            return null;
        }

        public JNode At(int index)
        {
            if (Items == null || index < 0 || index >= Items.Count)
                return null;
            return Items[index];
        }

        public float Float(float fallback = 0f)
        {
            return Type == Kind.Number ? (float)Number : fallback;
        }

        public string Text()
        {
            return String ?? string.Empty;
        }

        public int Count => Items != null ? Items.Count : 0;

        sealed class Parser
        {
            readonly string _s;
            readonly int _n;
            int _i;

            public Parser(string text)
            {
                _s = text;
                _n = text.Length;
                if (_n > 0 && _s[0] == '\uFEFF')
                    _i = 1;
            }

            public int Index => _i;
            public bool More => _i < _n;

            public JNode ReadValue()
            {
                Skip();
                if (_i >= _n)
                    throw new FormatException("JSON が途中で終わっています");
                char c = _s[_i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return new JNode { Type = Kind.String, String = ReadString() };
                if (c == 't' || c == 'f') return ReadBool();
                if (c == 'n') return ReadNull();
                return ReadNumber();
            }

            public void Skip()
            {
                while (_i < _n)
                {
                    char c = _s[_i];
                    if (c != ' ' && c != '\n' && c != '\r' && c != '\t')
                        return;
                    _i++;
                }
            }

            JNode ReadObject()
            {
                _i++;
                var map = new Dictionary<string, JNode>();
                Skip();
                if (Take('}'))
                    return new JNode { Type = Kind.Object, Map = map };
                while (true)
                {
                    Skip();
                    var key = ReadString();
                    Skip();
                    if (!Take(':'))
                        throw new FormatException("':' がありません (" + _i + ")");
                    map[key] = ReadValue();
                    Skip();
                    if (Take('}'))
                        break;
                    if (!Take(','))
                        throw new FormatException("',' がありません (" + _i + ")");
                }

                return new JNode { Type = Kind.Object, Map = map };
            }

            JNode ReadArray()
            {
                _i++;
                var items = new List<JNode>();
                Skip();
                if (Take(']'))
                    return new JNode { Type = Kind.Array, Items = items };
                while (true)
                {
                    items.Add(ReadValue());
                    Skip();
                    if (Take(']'))
                        break;
                    if (!Take(','))
                        throw new FormatException("',' がありません (" + _i + ")");
                }

                return new JNode { Type = Kind.Array, Items = items };
            }

            string ReadString()
            {
                if (!Take('"'))
                    throw new FormatException("文字列でありません (" + _i + ")");
                var sb = new StringBuilder();
                while (_i < _n)
                {
                    char c = _s[_i++];
                    if (c == '"')
                        return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (_i >= _n)
                        break;
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _n)
                                throw new FormatException("\\u が切れています");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default:
                            sb.Append(e);
                            break;
                    }
                }

                throw new FormatException("文字列が閉じていません");
            }

            JNode ReadBool()
            {
                if (Match("true"))
                    return new JNode { Type = Kind.Bool, Bool = true };
                if (Match("false"))
                    return new JNode { Type = Kind.Bool, Bool = false };
                throw new FormatException("真偽値でありません (" + _i + ")");
            }

            JNode ReadNull()
            {
                if (!Match("null"))
                    throw new FormatException("null でありません (" + _i + ")");
                return new JNode { Type = Kind.Null };
            }

            JNode ReadNumber()
            {
                int start = _i;
                if (_i < _n && (_s[_i] == '-' || _s[_i] == '+'))
                    _i++;
                while (_i < _n && _s[_i] >= '0' && _s[_i] <= '9')
                    _i++;
                if (_i < _n && _s[_i] == '.')
                {
                    _i++;
                    while (_i < _n && _s[_i] >= '0' && _s[_i] <= '9')
                        _i++;
                }

                if (_i < _n && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    _i++;
                    if (_i < _n && (_s[_i] == '-' || _s[_i] == '+'))
                        _i++;
                    while (_i < _n && _s[_i] >= '0' && _s[_i] <= '9')
                        _i++;
                }

                var slice = _s.Substring(start, _i - start);
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    throw new FormatException("数値でありません: " + slice);
                return new JNode { Type = Kind.Number, Number = number };
            }

            bool Take(char c)
            {
                if (_i < _n && _s[_i] == c)
                {
                    _i++;
                    return true;
                }

                return false;
            }

            bool Match(string word)
            {
                if (_i + word.Length > _n)
                    return false;
                for (int i = 0; i < word.Length; i++)
                {
                    if (_s[_i + i] != word[i])
                        return false;
                }

                _i += word.Length;
                return true;
            }
        }
    }
}
