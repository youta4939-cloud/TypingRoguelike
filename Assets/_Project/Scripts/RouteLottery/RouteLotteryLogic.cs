using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TypingRoguelike.Battle;

namespace TypingRoguelike.RouteLottery
{
    public sealed class SquareTypeDef
    {
        public string Id;
        public string Name;
        public string Short;
        public int R, G, B;
        public string Icon;
        public string Tile;
    }

    public sealed class PieceCell
    {
        public int Dr;
        public int Dc;
        public string Type;
    }

    public sealed class PieceDef
    {
        public string Id;
        public string Name;
        public int Count;
        public bool Locked;
        public string Unlock;
        public List<PieceCell> Cells = new List<PieceCell>();
        public int Turns { get; private set; }

        public PieceDef Clockwise()
        {
            var result = (PieceDef)MemberwiseClone();
            result.Turns = (Turns + 1) % 4;
            result.Cells = new List<PieceCell>(Cells.Count);
            int bottom = Cells.Max(cell => cell.Dr);
            foreach (var cell in Cells)
                result.Cells.Add(new PieceCell { Dr = cell.Dc, Dc = bottom - cell.Dr, Type = cell.Type });
            return result;
        }

        public PieceDef AtRotation(int turns)
        {
            var result = this;
            for (int i = 0; i < turns; i++)
                result = result.Clockwise();
            return result;
        }
    }

    public sealed class ComboDef
    {
        public string Id;
        public int Priority;
        public string Name;
        public string Effect;
        public string[] Pattern = { "*", "*", "*" };
        public bool Fallback;
    }

    public sealed class Placement
    {
        public string PieceId;
        public int Turns;
        public int Row;
        public int Col;
        public List<PieceCell> Cells = new List<PieceCell>();

        public Placement Copy()
        {
            var copy = new Placement { PieceId = PieceId, Turns = Turns, Row = Row, Col = Col };
            foreach (var cell in Cells)
                copy.Cells.Add(new PieceCell { Dr = cell.Dr, Dc = cell.Dc, Type = cell.Type });
            return copy;
        }
    }

    public sealed class RateDiff
    {
        public string Id;
        public int Before;
        public int After;
    }

    public sealed class MonologueLine
    {
        public string Id;
        public string Context;
        public string Line;
        public List<MonologueCond> When = new List<MonologueCond>();
    }

    public sealed class MonologueCond
    {
        public string Stat;
        public string Op;
        public JNode Value;
    }

    /// <summary>6行×3列。空きセルは danger。確率は 6^3=216 の整数。</summary>
    public sealed class MemoryBoard
    {
        public const int Rows = 6;
        public const int Lanes = 3;
        public const string Danger = "danger";

        public readonly string[,] Cells = new string[Rows, Lanes];
        public readonly List<Placement> Placements = new List<Placement>();
        readonly int[,] _owner = new int[Rows, Lanes];

        public static MemoryBoard Empty()
        {
            var board = new MemoryBoard();
            board.Rebuild();
            return board;
        }

        public static MemoryBoard FromRows(string[][] rows)
        {
            var board = Empty();
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Lanes; c++)
                board.Cells[r, c] = rows[r][c];
            return board;
        }

        public MemoryBoard Clone()
        {
            var copy = Empty();
            foreach (var placement in Placements)
                copy.Placements.Add(placement.Copy());
            copy.Rebuild();
            return copy;
        }

        public int PlacedCount(string pieceId)
        {
            var n = 0;
            foreach (var placement in Placements)
            {
                if (placement.PieceId == pieceId)
                    n++;
            }
            return n;
        }

        public int PlacementAt(int row, int col)
        {
            if (row < 0 || col < 0 || row >= Rows || col >= Lanes)
                return -1;
            return _owner[row, col];
        }

        public bool TryPlace(PieceDef piece, int row, int col)
        {
            if (piece == null || piece.Locked || piece.Cells == null || piece.Cells.Count == 0)
                return false;
            if (piece.Count <= 0 || PlacedCount(piece.Id) >= piece.Count)
                return false;
            if (!Fits(piece, row, col, -1))
                return false;
            var placement = new Placement { PieceId = piece.Id, Turns = piece.Turns, Row = row, Col = col };
            foreach (var cell in piece.Cells)
                placement.Cells.Add(new PieceCell { Dr = cell.Dr, Dc = cell.Dc, Type = cell.Type });
            Placements.Add(placement);
            Rebuild();
            return true;
        }

        public void RemoveAtIndex(int index)
        {
            if (index < 0 || index >= Placements.Count)
                return;
            Placements.RemoveAt(index);
            Rebuild();
        }

        public string Serialize()
        {
            if (Placements.Count == 0)
                return "";
            var parts = new string[Placements.Count];
            for (var i = 0; i < Placements.Count; i++)
            {
                var placement = Placements[i];
                parts[i] = placement.PieceId
                    + "@" + placement.Row.ToString(CultureInfo.InvariantCulture)
                    + "," + placement.Col.ToString(CultureInfo.InvariantCulture);
                if (placement.Turns != 0)
                    parts[i] += "," + placement.Turns.ToString(CultureInfo.InvariantCulture);
            }
            return string.Join("|", parts);
        }

        public void Restore(RouteLotteryCatalog catalog, string text)
        {
            Placements.Clear();
            Rebuild();
            if (catalog == null || string.IsNullOrEmpty(text))
                return;
            var parts = text.Split('|');
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                var at = part.IndexOf('@');
                if (at <= 0 || at >= part.Length - 1)
                    continue;
                var rc = part.Substring(at + 1).Split(',');
                if (rc.Length != 2 && rc.Length != 3)
                    continue;
                int turns = 0;
                if (rc.Length == 3 && (!int.TryParse(rc[2], out turns) || turns < 0 || turns > 3))
                    continue;
                if (!int.TryParse(rc[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
                    continue;
                if (!int.TryParse(rc[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var col))
                    continue;
                var piece = catalog.FindPiece(part.Substring(0, at));
                if (piece != null)
                    TryPlace(piece.AtRotation(turns), row, col);
            }
        }

        public bool Fits(PieceDef piece, int row, int col, int ignorePlacement)
        {
            if (piece?.Cells == null)
                return false;
            foreach (var cell in piece.Cells)
            {
                var rr = row + cell.Dr;
                var cc = col + cell.Dc;
                if (rr < 0 || cc < 0 || rr >= Rows || cc >= Lanes)
                    return false;
                var owner = _owner[rr, cc];
                if (owner >= 0 && owner != ignorePlacement)
                    return false;
            }
            return true;
        }

        public int Count(string type)
        {
            var n = 0;
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Lanes; c++)
            {
                if (Cells[r, c] == type)
                    n++;
            }
            return n;
        }

        public int LaneCount(int lane, string type)
        {
            var n = 0;
            for (var r = 0; r < Rows; r++)
            {
                if (Cells[r, lane] == type)
                    n++;
            }
            return n;
        }

        public bool LaneValid(int lane, int minBattle) => LaneCount(lane, "battle") >= minBattle;

        public bool AllLanesValid(int minBattle)
        {
            for (var lane = 0; lane < Lanes; lane++)
            {
                if (!LaneValid(lane, minBattle))
                    return false;
            }
            return true;
        }

        public int ContentHash()
        {
            var hash = 17;
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Lanes; c++)
                hash = unchecked(hash * 31 + (Cells[r, c] ?? "").GetHashCode());
            return hash;
        }

        public void Rebuild()
        {
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Lanes; c++)
            {
                Cells[r, c] = Danger;
                _owner[r, c] = -1;
            }

            for (var i = 0; i < Placements.Count; i++)
            {
                var placement = Placements[i];
                foreach (var cell in placement.Cells)
                {
                    var rr = placement.Row + cell.Dr;
                    var cc = placement.Col + cell.Dc;
                    if (rr < 0 || cc < 0 || rr >= Rows || cc >= Lanes)
                        continue;
                    Cells[rr, cc] = string.IsNullOrEmpty(cell.Type) ? Danger : cell.Type;
                    _owner[rr, cc] = i;
                }
            }
        }
    }

    public static class ProbabilityEngine
    {
        public const int Outcomes = 216;

        public static Dictionary<string, int> CountCombos(
            MemoryBoard board,
            IReadOnlyList<ComboDef> combos,
            IReadOnlyList<string> typeIds,
            string fallbackId)
        {
            var types = new List<string>();
            if (typeIds != null)
            {
                foreach (var id in typeIds)
                {
                    if (!string.IsNullOrEmpty(id) && !types.Contains(id))
                        types.Add(id);
                }
            }
            for (var r = 0; r < MemoryBoard.Rows; r++)
            for (var c = 0; c < MemoryBoard.Lanes; c++)
            {
                var id = board.Cells[r, c];
                if (!string.IsNullOrEmpty(id) && !types.Contains(id))
                    types.Add(id);
            }

            var lane = new int[MemoryBoard.Lanes][];
            for (var i = 0; i < MemoryBoard.Lanes; i++)
                lane[i] = new int[types.Count];
            for (var c = 0; c < MemoryBoard.Lanes; c++)
            for (var r = 0; r < MemoryBoard.Rows; r++)
            {
                var index = types.IndexOf(board.Cells[r, c]);
                if (index >= 0)
                    lane[c][index]++;
            }

            var ordered = new List<ComboDef>();
            if (combos != null)
            {
                foreach (var combo in combos)
                {
                    if (combo != null && !combo.Fallback)
                        ordered.Add(combo);
                }
            }
            ordered.Sort((a, b) =>
            {
                var byPriority = a.Priority.CompareTo(b.Priority);
                return byPriority != 0 ? byPriority : string.CompareOrdinal(a.Id, b.Id);
            });

            var result = new Dictionary<string, int>();
            if (combos != null)
            {
                foreach (var combo in combos)
                {
                    if (combo != null && !string.IsNullOrEmpty(combo.Id))
                        result[combo.Id] = 0;
                }
            }
            if (!string.IsNullOrEmpty(fallbackId))
                result[fallbackId] = 0;

            var n = types.Count;
            for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
            for (var k = 0; k < n; k++)
            {
                var weight = lane[0][i] * lane[1][j] * lane[2][k];
                if (weight == 0)
                    continue;
                var id = fallbackId;
                foreach (var combo in ordered)
                {
                    if (Matches(combo, types[i], types[j], types[k]))
                    {
                        id = combo.Id;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(id))
                    continue;
                result.TryGetValue(id, out var have);
                result[id] = have + weight;
            }
            return result;
        }

        public static Dictionary<string, int> ReachCounts(
            MemoryBoard board,
            IReadOnlyList<ComboDef> combos,
            IReadOnlyList<string> typeIds,
            string fallbackId,
            string first,
            string second)
        {
            var result = new Dictionary<string, int>();
            if (combos != null)
            {
                foreach (var combo in combos)
                {
                    if (combo != null)
                        result[combo.Id] = 0;
                }
            }
            if (!string.IsNullOrEmpty(fallbackId))
                result[fallbackId] = 0;

            for (var row = 0; row < MemoryBoard.Rows; row++)
            {
                var third = board.Cells[row, 2];
                var id = WinningId(combos, fallbackId, first, second, third);
                result.TryGetValue(id, out var have);
                result[id] = have + 1;
            }
            return result;
        }

        public static string WinningId(
            IReadOnlyList<ComboDef> combos,
            string fallbackId,
            string a,
            string b,
            string c)
        {
            ComboDef best = null;
            if (combos != null)
            {
                foreach (var combo in combos)
                {
                    if (combo == null || combo.Fallback || !Matches(combo, a, b, c))
                        continue;
                    if (best == null || combo.Priority < best.Priority)
                        best = combo;
                }
            }
            return best != null ? best.Id : fallbackId;
        }

        public static bool Matches(ComboDef combo, string a, string b, string c)
        {
            if (combo?.Pattern == null || combo.Pattern.Length < 3)
                return false;
            return Token(combo.Pattern[0], a) && Token(combo.Pattern[1], b) && Token(combo.Pattern[2], c);
        }

        public static bool Token(string pattern, string value) => pattern == "*" || pattern == value;

        public static List<RateDiff> Diff(Dictionary<string, int> before, Dictionary<string, int> after)
        {
            var keys = new List<string>();
            if (before != null)
            {
                foreach (var key in before.Keys)
                    keys.Add(key);
            }
            if (after != null)
            {
                foreach (var key in after.Keys)
                {
                    if (!keys.Contains(key))
                        keys.Add(key);
                }
            }
            var list = new List<RateDiff>();
            foreach (var key in keys)
            {
                var left = Value(before, key);
                var right = Value(after, key);
                if (left == right)
                    continue;
                list.Add(new RateDiff { Id = key, Before = left, After = right });
            }
            return list;
        }

        public static int Value(Dictionary<string, int> map, string key)
        {
            if (map != null && map.TryGetValue(key, out var value))
                return value;
            return 0;
        }

        /// <summary>count/denom を百分率にし、decimals 桁で四捨五入する。</summary>
        public static string Percent(int count, int denom, int decimals)
        {
            if (denom <= 0)
                denom = 1;
            var scale = 1;
            for (var i = 0; i < decimals; i++)
                scale *= 10;
            var rounded = ((long)count * 100L * scale + denom / 2) / denom;
            var whole = rounded / scale;
            var frac = (int)(rounded % scale);
            var fracText = frac.ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0');
            return whole.ToString(CultureInfo.InvariantCulture) + "." + fracText;
        }

        public static string LanePercent(int count) => Percent(count, MemoryBoard.Rows, 1);

        public static string ComboPercent(int count) => Percent(count, Outcomes, 2);
    }

    public static class MonologueSelector
    {
        public static string Pick(
            IReadOnlyList<MonologueLine> lines,
            string context,
            MemoryBoard board,
            int minBattle,
            string reachType,
            string hitCombo)
        {
            if (lines == null)
                return "";
            foreach (var line in lines)
            {
                if (line == null || line.Context != context)
                    continue;
                var ok = true;
                foreach (var cond in line.When)
                {
                    if (!Match(cond, board, minBattle, reachType, hitCombo))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                    return line.Line ?? "";
            }
            return "";
        }

        static bool Match(MonologueCond cond, MemoryBoard board, int minBattle, string reachType, string hitCombo)
        {
            if (cond == null)
                return false;
            var actual = Stat(cond.Stat, board, minBattle, reachType, hitCombo);
            var value = cond.Value;
            if (value == null || value.Type == JNode.Kind.Null)
                return false;
            if (cond.Op == "==" && value.Type == JNode.Kind.Bool)
                return actual is bool flag && flag == value.Bool;
            if (cond.Op == "==" && value.Type == JNode.Kind.String)
                return actual is string text && text == value.Text();
            if (actual is int number && value.Type == JNode.Kind.Number)
            {
                var target = (int)value.Number;
                if (cond.Op == ">=") return number >= target;
                if (cond.Op == "<=") return number <= target;
                if (cond.Op == ">") return number > target;
                if (cond.Op == "<") return number < target;
                if (cond.Op == "==") return number == target;
            }
            return false;
        }

        static object Stat(string stat, MemoryBoard board, int minBattle, string reachType, string hitCombo)
        {
            if (stat == "validation_failed")
                return board == null || !board.AllLanesValid(minBattle);
            if (stat == "reach_type")
                return reachType ?? "";
            if (stat == "hit_combo")
                return hitCombo ?? "";
            if (!string.IsNullOrEmpty(stat) && stat.EndsWith("_total", StringComparison.Ordinal))
            {
                var type = stat.Substring(0, stat.Length - "_total".Length);
                return board != null ? board.Count(type) : 0;
            }
            if (!string.IsNullOrEmpty(stat) && stat.StartsWith("lane_min_", StringComparison.Ordinal))
            {
                var type = stat.Substring("lane_min_".Length);
                var min = int.MaxValue;
                for (var lane = 0; lane < MemoryBoard.Lanes; lane++)
                    min = Math.Min(min, board != null ? board.LaneCount(lane, type) : 0);
                return min == int.MaxValue ? 0 : min;
            }
            return 0;
        }
    }

    public sealed class CardArtDef
    {
        public string Type;
        public string Title;
        public List<string> Illustrations = new List<string>();
    }

    public sealed class RouteLotteryCatalog
    {
        public List<SquareTypeDef> Types = new List<SquareTypeDef>();
        public List<PieceDef> Pieces = new List<PieceDef>();
        public List<ComboDef> Combos = new List<ComboDef>();
        public string FallbackId = "c99";
        public int MinBattlePerLane = 2;
        public int DangerMadness = 30;
        public List<MonologueLine> Monologues = new List<MonologueLine>();
        public List<CardArtDef> Cards = new List<CardArtDef>();
        public JNode Strings;
        public JNode SampleBoards;

        public IReadOnlyList<string> TypeIds()
        {
            var ids = new List<string>();
            foreach (var type in Types)
                ids.Add(type.Id);
            return ids;
        }

        public PieceDef FindPiece(string id)
        {
            foreach (var piece in Pieces)
            {
                if (piece.Id == id)
                    return piece;
            }
            return null;
        }

        public ComboDef FindCombo(string id)
        {
            foreach (var combo in Combos)
            {
                if (combo.Id == id)
                    return combo;
            }
            return null;
        }

        public SquareTypeDef FindType(string id)
        {
            foreach (var type in Types)
            {
                if (type.Id == id)
                    return type;
            }
            return null;
        }

        public string ShortOf(string id)
        {
            var type = FindType(id);
            return type != null && !string.IsNullOrEmpty(type.Short) ? type.Short : "?";
        }

        public string Text(string key)
        {
            var node = Strings != null ? Strings.Get(key) : null;
            var text = node != null ? node.Get("text") : null;
            var value = text != null ? text.Text() : "";
            return string.IsNullOrEmpty(value) ? key : value;
        }

        public string Format(string key, params (string Name, string Value)[] tokens)
        {
            var text = Text(key);
            if (tokens == null)
                return text;
            foreach (var token in tokens)
                text = text.Replace("{" + token.Name + "}", token.Value ?? "");
            return text;
        }

        public CardArtDef FindCard(string type)
        {
            foreach (var card in Cards)
            {
                if (card.Type == type)
                    return card;
            }
            return null;
        }

        public MemoryBoard BuildSample(string name, List<string> errors)
        {
            var board = MemoryBoard.Empty();
            var list = SampleBoards != null ? SampleBoards.Get(name) : null;
            if (list == null || list.Items == null)
            {
                errors?.Add("sample board missing: " + name);
                return null;
            }
            for (var i = 0; i < list.Count; i++)
            {
                var item = list.At(i);
                var piece = FindPiece(item.Get("piece")?.Text());
                var row = (int)(item.Get("row") != null ? item.Get("row").Number : 0);
                var col = (int)(item.Get("col") != null ? item.Get("col").Number : 0);
                if (piece == null || !board.TryPlace(piece, row, col))
                {
                    errors?.Add(name + " place failed " + item.Get("piece")?.Text() + " @" + row + "," + col);
                    return null;
                }
            }
            return board;
        }

        public static RouteLotteryCatalog Load(string directory)
        {
            string Read(string name) => File.ReadAllText(Path.Combine(directory, name + ".json"));
            return Parse(
                Read("square_types"),
                Read("pieces"),
                Read("combos"),
                Read("monologues"),
                Read("strings_ja"),
                Read("layer_cards"));
        }

        public static RouteLotteryCatalog Parse(
            string squareTypes,
            string pieces,
            string combos,
            string monologues,
            string strings,
            string layerCards)
        {
            var catalog = new RouteLotteryCatalog();
            var typeRoot = JNode.Parse(squareTypes);
            var typeList = typeRoot.Get("types");
            if (typeList?.Items != null)
            {
                foreach (var item in typeList.Items)
                {
                    var tint = item.Get("tint");
                    var effect = item.Get("effect_values");
                    var type = new SquareTypeDef
                    {
                        Id = item.Get("id")?.Text(),
                        Name = item.Get("name")?.Text(),
                        Short = item.Get("short")?.Text(),
                        Icon = item.Get("icon")?.Text(),
                        Tile = item.Get("tile")?.Text(),
                        R = tint != null && tint.At(0) != null ? (int)tint.At(0).Number : 255,
                        G = tint != null && tint.At(1) != null ? (int)tint.At(1).Number : 255,
                        B = tint != null && tint.At(2) != null ? (int)tint.At(2).Number : 255
                    };
                    catalog.Types.Add(type);
                    if (type.Id == MemoryBoard.Danger && effect != null && effect.Get("madness") != null)
                        catalog.DangerMadness = (int)effect.Get("madness").Number;
                }
            }

            var pieceRoot = JNode.Parse(pieces);
            catalog.SampleBoards = pieceRoot.Get("sample_boards");
            var pieceList = pieceRoot.Get("pieces");
            if (pieceList?.Items != null)
            {
                foreach (var item in pieceList.Items)
                {
                    var piece = new PieceDef
                    {
                        Id = item.Get("id")?.Text(),
                        Name = item.Get("name")?.Text(),
                        Count = item.Get("count") != null ? (int)item.Get("count").Number : 0,
                        Locked = item.Get("locked") != null && item.Get("locked").Type == JNode.Kind.Bool && item.Get("locked").Bool,
                        Unlock = item.Get("unlock") != null && item.Get("unlock").Type == JNode.Kind.String ? item.Get("unlock").Text() : null
                    };
                    var cells = item.Get("cells");
                    if (cells?.Items != null)
                    {
                        foreach (var cell in cells.Items)
                        {
                            piece.Cells.Add(new PieceCell
                            {
                                Dr = cell.Get("dr") != null ? (int)cell.Get("dr").Number : 0,
                                Dc = cell.Get("dc") != null ? (int)cell.Get("dc").Number : 0,
                                Type = cell.Get("type")?.Text()
                            });
                        }
                    }
                    catalog.Pieces.Add(piece);
                }
            }

            var comboRoot = JNode.Parse(combos);
            if (comboRoot.Get("min_battle_per_lane") != null)
                catalog.MinBattlePerLane = (int)comboRoot.Get("min_battle_per_lane").Number;
            var comboList = comboRoot.Get("combos");
            if (comboList?.Items != null)
            {
                foreach (var item in comboList.Items)
                    catalog.Combos.Add(ReadCombo(item, false));
            }
            var fallback = comboRoot.Get("fallback");
            if (fallback != null)
            {
                var combo = ReadCombo(fallback, true);
                catalog.FallbackId = combo.Id;
                catalog.Combos.Add(combo);
            }

            var lines = JNode.Parse(monologues).Get("lines");
            if (lines?.Items != null)
            {
                foreach (var item in lines.Items)
                {
                    var line = new MonologueLine
                    {
                        Id = item.Get("id")?.Text(),
                        Context = item.Get("context")?.Text(),
                        Line = item.Get("line")?.Text()
                    };
                    var when = item.Get("when");
                    if (when?.Items != null)
                    {
                        foreach (var cond in when.Items)
                        {
                            line.When.Add(new MonologueCond
                            {
                                Stat = cond.Get("stat")?.Text(),
                                Op = cond.Get("op")?.Text(),
                                Value = cond.Get("value")
                            });
                        }
                    }
                    catalog.Monologues.Add(line);
                }
            }

            var stringRoot = JNode.Parse(strings);
            catalog.Strings = stringRoot.Get("strings") ?? stringRoot;

            var cardList = JNode.Parse(layerCards).Get("cards");
            if (cardList?.Items != null)
            {
                foreach (var item in cardList.Items)
                {
                    var card = new CardArtDef
                    {
                        Type = item.Get("type")?.Text(),
                        Title = item.Get("title")?.Text()
                    };
                    var arts = item.Get("illustrations");
                    if (arts?.Items != null)
                    {
                        foreach (var art in arts.Items)
                            card.Illustrations.Add(art.Text());
                    }
                    catalog.Cards.Add(card);
                }
            }
            return catalog;
        }

        static ComboDef ReadCombo(JNode item, bool fallback)
        {
            var combo = new ComboDef
            {
                Id = item.Get("id")?.Text(),
                Priority = item.Get("priority") != null ? (int)item.Get("priority").Number : 99,
                Name = item.Get("name")?.Text(),
                Effect = item.Get("effect")?.Text(),
                Fallback = fallback
            };
            var pattern = item.Get("pattern");
            if (pattern != null && pattern.Count >= 3)
            {
                combo.Pattern = new[]
                {
                    pattern.At(0).Text(),
                    pattern.At(1).Text(),
                    pattern.At(2).Text()
                };
            }
            return combo;
        }
    }

    public static class RouteLotterySelfTest
    {
        public static string Run(string dataDir)
        {
            var errors = new List<string>();
            RouteLotteryCatalog catalog;
            try
            {
                catalog = RouteLotteryCatalog.Load(dataDir);
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }

            ExpectPercent(errors, 8, 216, 2, "3.70");
            ExpectPercent(errors, 200, 216, 2, "92.59");
            ExpectPercent(errors, 1, 216, 2, "0.46");
            ExpectPercent(errors, 2, 6, 1, "33.3");
            ExpectPercent(errors, 1, 6, 1, "16.7");
            ExpectPercent(errors, 206, 216, 2, "95.37");

            var cases = JNode.Parse(File.ReadAllText(Path.Combine(dataDir, "test_cases_probability.json"))).Get("cases");
            var names = new[] { "A_normal", "B_before_drop", "C_validation_error" };
            var boards = new Dictionary<string, MemoryBoard>();
            foreach (var name in names)
            {
                var node = cases.Get(name);
                var fromRows = ReadRows(node.Get("board_rows_top_to_bottom"));
                boards[name] = fromRows;
                var fromPieces = catalog.BuildSample(name, errors);
                if (fromPieces != null)
                    ExpectSame(fromPieces, fromRows, name, errors);
                ExpectLaneCounts(fromRows, node.Get("lane_counts"), name, errors);
                ExpectCombos(fromRows, catalog, node.Get("combo_216"), name, errors);
                ExpectValid(fromRows, node.Get("valid_lanes"), catalog.MinBattlePerLane, name, errors);
            }

            ExpectLine(catalog, boards["A_normal"], "……灯が見える。", "A", errors);
            ExpectLine(catalog, boards["B_before_drop"], "……何も、思い出せない。", "B", errors);
            ExpectLine(catalog, boards["C_validation_error"], "……血の匂いが、薄い。", "C", errors);
            ExpectSymmetric(catalog, boards["A_normal"], boards["B_before_drop"], errors);
            ExpectWildcard(catalog, cases.Get("W_wildcard_priority_on_A"), boards["A_normal"], errors);
            return errors.Count == 0 ? null : string.Join("\n", errors);
        }

        static void ExpectPercent(List<string> errors, int count, int denom, int decimals, string expected)
        {
            var got = ProbabilityEngine.Percent(count, denom, decimals);
            if (got != expected)
                errors.Add("percent " + count + "/" + denom + " = " + got + " expected " + expected);
        }

        static MemoryBoard ReadRows(JNode rows)
        {
            var table = new string[MemoryBoard.Rows][];
            for (var r = 0; r < MemoryBoard.Rows; r++)
            {
                table[r] = new string[MemoryBoard.Lanes];
                for (var c = 0; c < MemoryBoard.Lanes; c++)
                    table[r][c] = rows.At(r).At(c).Text();
            }
            return MemoryBoard.FromRows(table);
        }

        static void ExpectSame(MemoryBoard placed, MemoryBoard rows, string name, List<string> errors)
        {
            for (var r = 0; r < MemoryBoard.Rows; r++)
            for (var c = 0; c < MemoryBoard.Lanes; c++)
            {
                if (placed.Cells[r, c] != rows.Cells[r, c])
                    errors.Add(name + " cell " + r + "," + c + " " + placed.Cells[r, c] + " != " + rows.Cells[r, c]);
            }
        }

        static void ExpectLaneCounts(MemoryBoard board, JNode expected, string name, List<string> errors)
        {
            for (var lane = 0; lane < MemoryBoard.Lanes; lane++)
            {
                var row = expected.At(lane);
                if (row?.Map == null)
                    continue;
                foreach (var pair in row.Map)
                {
                    var got = board.LaneCount(lane, pair.Key);
                    var want = (int)pair.Value.Number;
                    if (got != want)
                        errors.Add(name + " lane " + lane + " " + pair.Key + " " + got + " != " + want);
                }
            }
        }

        static void ExpectCombos(MemoryBoard board, RouteLotteryCatalog catalog, JNode expected, string name, List<string> errors)
        {
            var counts = ProbabilityEngine.CountCombos(board, catalog.Combos, catalog.TypeIds(), catalog.FallbackId);
            var sum = 0;
            foreach (var value in counts.Values)
                sum += value;
            if (sum != ProbabilityEngine.Outcomes)
                errors.Add(name + " sum " + sum);
            if (expected?.Map == null)
                return;
            foreach (var pair in expected.Map)
            {
                var got = ProbabilityEngine.Value(counts, pair.Key);
                var want = (int)pair.Value.Number;
                if (got != want)
                    errors.Add(name + " " + pair.Key + " " + got + " != " + want);
            }
        }

        static void ExpectValid(MemoryBoard board, JNode expected, int minBattle, string name, List<string> errors)
        {
            for (var lane = 0; lane < MemoryBoard.Lanes; lane++)
            {
                var want = expected.At(lane) != null && expected.At(lane).Bool;
                var got = board.LaneValid(lane, minBattle);
                if (got != want)
                    errors.Add(name + " valid " + lane + " " + got + " != " + want);
            }
        }

        static void ExpectLine(RouteLotteryCatalog catalog, MemoryBoard board, string expected, string label, List<string> errors)
        {
            var line = MonologueSelector.Pick(catalog.Monologues, "build", board, catalog.MinBattlePerLane, null, null);
            if (line != expected)
                errors.Add(label + " monologue [" + line + "] != [" + expected + "]");
        }

        static void ExpectSymmetric(RouteLotteryCatalog catalog, MemoryBoard a, MemoryBoard b, List<string> errors)
        {
            var countsA = ProbabilityEngine.CountCombos(a, catalog.Combos, catalog.TypeIds(), catalog.FallbackId);
            var countsB = ProbabilityEngine.CountCombos(b, catalog.Combos, catalog.TypeIds(), catalog.FallbackId);
            var ab = ProbabilityEngine.Diff(countsA, countsB);
            var ba = ProbabilityEngine.Diff(countsB, countsA);
            if (ProbabilityEngine.Diff(countsA, countsA).Count != 0)
                errors.Add("diff A to A is not empty");
            if (ab.Count != ba.Count)
                errors.Add("diff count " + ab.Count + " != " + ba.Count);
            foreach (var change in ab)
            {
                RateDiff back = null;
                foreach (var item in ba)
                {
                    if (item.Id == change.Id)
                        back = item;
                }
                if (back == null || back.Before != change.After || back.After != change.Before)
                    errors.Add("diff not symmetric " + change.Id);
            }
        }

        static void ExpectWildcard(RouteLotteryCatalog catalog, JNode node, MemoryBoard board, List<string> errors)
        {
            var combos = new List<ComboDef>();
            var list = node.Get("combos");
            for (var i = 0; i < list.Count; i++)
            {
                var item = list.At(i);
                var pattern = item.Get("pattern");
                combos.Add(new ComboDef
                {
                    Id = item.Get("id")?.Text(),
                    Priority = (int)item.Get("priority").Number,
                    Pattern = new[] { pattern.At(0).Text(), pattern.At(1).Text(), pattern.At(2).Text() }
                });
            }
            var counts = ProbabilityEngine.CountCombos(board, combos, catalog.TypeIds(), "fallback");
            var sum = 0;
            foreach (var value in counts.Values)
                sum += value;
            if (sum != ProbabilityEngine.Outcomes)
                errors.Add("W sum " + sum);
            var expected = node.Get("expected_216");
            foreach (var pair in expected.Map)
            {
                var got = ProbabilityEngine.Value(counts, pair.Key);
                var want = (int)pair.Value.Number;
                if (got != want)
                    errors.Add("W " + pair.Key + " " + got + " != " + want);
            }
        }
    }
}
