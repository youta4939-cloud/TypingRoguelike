using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public sealed class ReelPlacement
    {
        public string PartId;
        public int X;
        public int Y;
    }

    /// <summary>置いたパーツ。空いたマスは盤面にすると罠になる。</summary>
    public sealed class ReelLayout
    {
        public const string Prefix = "v1|";
        public List<ReelPlacement> Placements = new();

        public string Serialize()
        {
            var text = new StringBuilder(Prefix);
            for (var i = 0; i < Placements.Count; i++)
            {
                var placement = Placements[i];
                if (placement == null || string.IsNullOrEmpty(placement.PartId))
                    continue;
                if (text.Length > Prefix.Length)
                    text.Append(';');
                text.Append(placement.PartId);
                text.Append('@');
                text.Append(placement.X);
                text.Append(',');
                text.Append(placement.Y);
            }

            return text.ToString();
        }

        public static ReelLayout Parse(string text, ReelPartCatalog catalog, bool debugUnlocked = false)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(Prefix))
                return null;
            var layout = new ReelLayout();
            var body = text.Substring(Prefix.Length);
            if (body.Length == 0)
                return layout;
            foreach (var token in body.Split(';'))
            {
                var at = token.IndexOf('@');
                if (at <= 0)
                    continue;
                var id = token.Substring(0, at);
                var comma = token.IndexOf(',', at + 1);
                if (comma < 0)
                    continue;
                if (!int.TryParse(token.Substring(at + 1, comma - at - 1), out var x))
                    continue;
                if (!int.TryParse(token.Substring(comma + 1), out var y))
                    continue;
                if (catalog != null && !catalog.IsPlaceable(id))
                {
                    Debug.LogWarning($"パーツ {id} は無いので、そのマスは罠として読み込みました。");
                    continue;
                }
                layout.Placements.Add(new ReelPlacement { PartId = id, X = x, Y = y });
            }

            layout.KeepWithinOwned(catalog, debugUnlocked);
            return layout;
        }

        public static ReelLayout BalanceStarter(ReelPartCatalog catalog)
        {
            var layout = new ReelLayout();
            layout.Placements.Add(new ReelPlacement { PartId = "P12", X = 0, Y = 0 });
            layout.Placements.Add(new ReelPlacement { PartId = "P14", X = 1, Y = 0 });
            layout.Placements.Add(new ReelPlacement { PartId = "P15", X = 0, Y = 2 });
            layout.Placements.Add(new ReelPlacement { PartId = "P16", X = 0, Y = 3 });
            layout.Placements.Add(new ReelPlacement { PartId = "P17", X = 0, Y = 4 });
            layout.Placements.Add(new ReelPlacement { PartId = "P18", X = 0, Y = 5 });
            layout.KeepWithinOwned(catalog, false);
            return layout;
        }

        public void Clear() => Placements.Clear();

        public void KeepWithinOwned(ReelPartCatalog catalog, bool debugUnlocked)
        {
            if (catalog == null)
                return;
            var kept = new List<ReelPlacement>();
            var overStock = new Dictionary<string, int>();
            var unfit = new Dictionary<string, int>();
            foreach (var placement in Placements)
            {
                if (placement == null || string.IsNullOrEmpty(placement.PartId))
                    continue;
                var part = catalog.Find(placement.PartId);
                var probe = new ReelLayout { Placements = kept };
                if (part == null || !probe.Fits(catalog, part, placement.X, placement.Y, -1))
                {
                    AddCount(unfit, placement.PartId);
                    continue;
                }

                if (probe.Remaining(catalog, placement.PartId, debugUnlocked) <= 0)
                {
                    AddCount(overStock, placement.PartId);
                    continue;
                }

                kept.Add(placement);
            }

            Placements = kept;
            foreach (var pair in overStock)
                Debug.LogWarning($"{pair.Key} は1つしか置けないので、余分は罠にしました。");
            foreach (var pair in unfit)
                Debug.LogWarning($"{pair.Key} は盤面に置けないので、そのマスは罠にしました。");
        }

        /// <summary>
        /// プリセットのマスを、持っているパーツで組み直す。
        /// 足りなければ盤面は変えず、足りないパーツを返す。
        /// </summary>
        public bool TryLoadPreset(BoardGrid target, ReelPartCatalog catalog, bool debugUnlocked, List<string> errors)
        {
            errors?.Clear();
            if (!TryCover(target, catalog, debugUnlocked, out var covered, out var needed))
            {
                AddError(errors, "この並べ方は、持っているパーツの形では組めません。");
                return false;
            }

            var shortLines = new List<string>();
            if (catalog?.Parts != null)
            {
                foreach (var part in catalog.Parts)
                {
                    if (part == null || !needed.TryGetValue(part.Id, out var need))
                        continue;
                    var owned = catalog.Owned(part.Id, debugUnlocked);
                    if (need > owned)
                        shortLines.Add($"{part.Id}が{need - owned}個足りないため読み込めません");
                }
            }

            if (shortLines.Count > 0)
            {
                foreach (var line in shortLines)
                    AddError(errors, line);
                return false;
            }

            Placements = covered;
            return true;
        }

        public BoardGrid ToGrid(ReelPartCatalog catalog)
        {
            var grid = BoardGrid.All(DungeonNodeType.Trap);
            foreach (var placement in Placements)
                Paint(grid, catalog, placement);
            return grid;
        }

        public int CountOf(string partId)
        {
            var count = 0;
            foreach (var placement in Placements)
            {
                if (placement != null && placement.PartId == partId)
                    count++;
            }

            return count;
        }

        public int EmptyCells(ReelPartCatalog catalog)
        {
            var filled = new bool[BoardGrid.Columns, BoardGrid.Rows];
            var count = 0;
            foreach (var placement in Placements)
            {
                var part = catalog != null ? catalog.Find(placement?.PartId) : null;
                if (part == null)
                    continue;
                foreach (var cell in part.Cells)
                {
                    if (cell == null)
                        continue;
                    var x = placement.X + cell.X;
                    var y = placement.Y + cell.Y;
                    if (x < 0 || x >= BoardGrid.Columns || y < 0 || y >= BoardGrid.Rows || filled[x, y])
                        continue;
                    filled[x, y] = true;
                    count++;
                }
            }

            return BoardGrid.Columns * BoardGrid.Rows - count;
        }

        public int FindIndexAt(ReelPartCatalog catalog, int column, int row)
        {
            for (var i = 0; i < Placements.Count; i++)
            {
                var part = catalog != null ? catalog.Find(Placements[i]?.PartId) : null;
                if (part == null)
                    continue;
                foreach (var cell in part.Cells)
                {
                    if (cell != null && Placements[i].X + cell.X == column && Placements[i].Y + cell.Y == row)
                        return i;
                }
            }

            return -1;
        }

        public int Remaining(ReelPartCatalog catalog, string partId, bool debugUnlocked, int ignoreIndex = -1)
        {
            if (catalog == null)
                return 0;
            var placed = CountOf(partId);
            if (ignoreIndex >= 0 && ignoreIndex < Placements.Count)
            {
                var ignored = Placements[ignoreIndex];
                if (ignored != null && ignored.PartId == partId)
                    placed--;
            }

            return catalog.Owned(partId, debugUnlocked) - placed;
        }

        public bool CanPlace(ReelPartCatalog catalog, ReelPart part, int originX, int originY, int ignoreIndex, bool debugUnlocked)
        {
            if (!Fits(catalog, part, originX, originY, ignoreIndex))
                return false;
            return Remaining(catalog, part.Id, debugUnlocked, ignoreIndex) > 0;
        }

        public bool CanDrop(ReelPartCatalog catalog, ReelPart part, int originX, int originY, int ignoreIndex, bool debugUnlocked)
        {
            if (!WithinBoard(part, originX, originY))
                return false;
            var same = 0;
            foreach (var index in Overlaps(catalog, part, originX, originY, ignoreIndex))
            {
                var placement = Placements[index];
                if (placement != null && placement.PartId == part.Id)
                    same++;
            }

            return Remaining(catalog, part.Id, debugUnlocked, ignoreIndex) + same > 0;
        }

        public bool TryDrop(ReelPartCatalog catalog, ReelPart part, int originX, int originY, int ignoreIndex, bool debugUnlocked)
        {
            if (!CanDrop(catalog, part, originX, originY, ignoreIndex, debugUnlocked))
                return false;
            var remove = Overlaps(catalog, part, originX, originY, ignoreIndex);
            if (ignoreIndex >= 0)
                remove.Add(ignoreIndex);
            remove.Sort((a, b) => b.CompareTo(a));
            for (var i = 0; i < remove.Count; i++)
                RemoveAt(remove[i]);
            Place(part.Id, originX, originY);
            return true;
        }

        bool WithinBoard(ReelPart part, int originX, int originY)
        {
            if (part?.Cells == null || part.Cells.Length == 0)
                return false;
            foreach (var cell in part.Cells)
            {
                if (cell == null)
                    return false;
                var x = originX + cell.X;
                var y = originY + cell.Y;
                if (x < 0 || x >= BoardGrid.Columns || y < 0 || y >= BoardGrid.Rows)
                    return false;
            }

            return true;
        }

        List<int> Overlaps(ReelPartCatalog catalog, ReelPart part, int originX, int originY, int ignoreIndex)
        {
            var found = new List<int>();
            foreach (var cell in part.Cells)
            {
                if (cell == null)
                    continue;
                var index = FindIndexAt(catalog, originX + cell.X, originY + cell.Y);
                if (index < 0 || index == ignoreIndex || found.Contains(index))
                    continue;
                found.Add(index);
            }

            return found;
        }

        bool Fits(ReelPartCatalog catalog, ReelPart part, int originX, int originY, int ignoreIndex)
        {
            if (catalog == null || part == null)
                return false;
            foreach (var cell in part.Cells)
            {
                if (cell == null)
                    return false;
                var x = originX + cell.X;
                var y = originY + cell.Y;
                if (x < 0 || x >= BoardGrid.Columns || y < 0 || y >= BoardGrid.Rows)
                    return false;
                var occupied = FindIndexAt(catalog, x, y);
                if (occupied >= 0 && occupied != ignoreIndex)
                    return false;
            }

            return true;
        }

        public void Place(string partId, int x, int y)
        {
            Placements.Add(new ReelPlacement { PartId = partId, X = x, Y = y });
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Placements.Count)
                return;
            Placements.RemoveAt(index);
        }

        static void Paint(BoardGrid grid, ReelPartCatalog catalog, ReelPlacement placement)
        {
            var part = catalog != null ? catalog.Find(placement?.PartId) : null;
            if (part == null)
                return;
            foreach (var cell in part.Cells)
            {
                if (cell == null)
                    continue;
                grid.Set(placement.X + cell.X, placement.Y + cell.Y, cell.Type);
            }
        }

        static void AddCount(Dictionary<string, int> counts, string id)
        {
            counts.TryGetValue(id, out var count);
            counts[id] = count + 1;
        }

        static void AddError(List<string> errors, string line)
        {
            errors?.Add(line);
            Debug.Log(line);
        }

        bool TryCover(BoardGrid target, ReelPartCatalog catalog, bool debugUnlocked, out List<ReelPlacement> covered, out Dictionary<string, int> needed)
        {
            covered = null;
            needed = null;
            if (target == null || catalog?.Parts == null)
                return false;

            var parts = new List<ReelPart>();
            foreach (var part in catalog.Parts)
            {
                if (part != null && !part.Synthetic && part.Cells != null && part.Cells.Length > 0)
                    parts.Add(part);
            }

            parts.Sort((a, b) =>
            {
                var size = b.Cells.Length.CompareTo(a.Cells.Length);
                return size != 0 ? size : string.CompareOrdinal(a.Id, b.Id);
            });
            if (parts.Count == 0)
                return false;

            var search = new CoverSearch(target, parts, catalog, debugUnlocked);
            search.Run();
            if (search.Best == null)
                return false;
            if (!Matches(search.Best, catalog, target))
                return false;
            covered = search.Best;
            needed = search.BestNeeded;
            return true;
        }

        static bool Matches(List<ReelPlacement> placements, ReelPartCatalog catalog, BoardGrid target)
        {
            var painted = new ReelLayout { Placements = placements }.ToGrid(catalog);
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                for (var row = 0; row < BoardGrid.Rows; row++)
                {
                    if (painted.Get(column, row) != target.Get(column, row))
                        return false;
                }
            }

            return true;
        }

        sealed class CoverSearch
        {
            const int NodeLimit = 100000;
            const int PieceCap = 1;

            readonly DungeonNodeType[,] _target = new DungeonNodeType[BoardGrid.Columns, BoardGrid.Rows];
            readonly bool[,] _filled = new bool[BoardGrid.Columns, BoardGrid.Rows];
            readonly ReelPart[] _parts;
            readonly int[] _used;
            readonly int[] _owned;
            readonly List<ReelPlacement> _current = new();
            int _nodes;
            int _bestShortage = int.MaxValue;

            public List<ReelPlacement> Best;
            public Dictionary<string, int> BestNeeded;

            public CoverSearch(BoardGrid target, List<ReelPart> parts, ReelPartCatalog catalog, bool debugUnlocked)
            {
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    for (var row = 0; row < BoardGrid.Rows; row++)
                        _target[column, row] = target.Get(column, row);
                }

                _parts = parts.ToArray();
                _used = new int[_parts.Length];
                _owned = new int[_parts.Length];
                for (var i = 0; i < _parts.Length; i++)
                    _owned[i] = catalog.Owned(_parts[i].Id, debugUnlocked);
            }

            public void Run() => Visit();

            void Visit()
            {
                if (++_nodes > NodeLimit)
                    return;
                if (CommittedShortage() >= _bestShortage)
                    return;
                if (!NextHole(out var column, out var row))
                {
                    Remember();
                    return;
                }

                for (var i = 0; i < _parts.Length; i++)
                {
                    if (_used[i] >= PieceCap)
                        continue;
                    var part = _parts[i];
                    foreach (var anchor in part.Cells)
                    {
                        if (anchor == null || anchor.Type != _target[column, row])
                            continue;
                        var originX = column - anchor.X;
                        var originY = row - anchor.Y;
                        if (!FitsAt(part, originX, originY))
                            continue;
                        Paint(part, originX, originY, true);
                        _used[i]++;
                        _current.Add(new ReelPlacement { PartId = part.Id, X = originX, Y = originY });
                        Visit();
                        _current.RemoveAt(_current.Count - 1);
                        _used[i]--;
                        Paint(part, originX, originY, false);
                        if (_bestShortage == 0 || _nodes > NodeLimit)
                            return;
                    }
                }
            }

            void Remember()
            {
                var shortage = CommittedShortage();
                if (shortage >= _bestShortage)
                    return;
                _bestShortage = shortage;
                Best = new List<ReelPlacement>(_current);
                BestNeeded = new Dictionary<string, int>();
                for (var i = 0; i < _parts.Length; i++)
                {
                    if (_used[i] > 0)
                        BestNeeded[_parts[i].Id] = _used[i];
                }
            }

            int CommittedShortage()
            {
                var shortage = 0;
                for (var i = 0; i < _used.Length; i++)
                {
                    if (_used[i] > _owned[i])
                        shortage += _used[i] - _owned[i];
                }

                return shortage;
            }

            bool NextHole(out int column, out int row)
            {
                for (var y = 0; y < BoardGrid.Rows; y++)
                {
                    for (var x = 0; x < BoardGrid.Columns; x++)
                    {
                        if (_filled[x, y] || _target[x, y] == DungeonNodeType.Trap)
                            continue;
                        column = x;
                        row = y;
                        return true;
                    }
                }

                column = -1;
                row = -1;
                return false;
            }

            bool FitsAt(ReelPart part, int originX, int originY)
            {
                foreach (var cell in part.Cells)
                {
                    if (cell == null)
                        return false;
                    var x = originX + cell.X;
                    var y = originY + cell.Y;
                    if (x < 0 || x >= BoardGrid.Columns || y < 0 || y >= BoardGrid.Rows)
                        return false;
                    if (_filled[x, y] || _target[x, y] != cell.Type)
                        return false;
                }

                return true;
            }

            void Paint(ReelPart part, int originX, int originY, bool filled)
            {
                foreach (var cell in part.Cells)
                {
                    if (cell == null)
                        continue;
                    _filled[originX + cell.X, originY + cell.Y] = filled;
                }
            }
        }
    }
}
