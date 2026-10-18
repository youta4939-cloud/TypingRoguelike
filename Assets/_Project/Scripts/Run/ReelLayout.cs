using System.Collections.Generic;
using System.Text;

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

        public static ReelLayout Parse(string text, ReelPartCatalog catalog)
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
                if (catalog != null && catalog.Find(id) == null)
                    continue;
                layout.Placements.Add(new ReelPlacement { PartId = id, X = x, Y = y });
            }

            return layout;
        }

        public static ReelLayout BalanceStarter(ReelPartCatalog catalog)
        {
            var layout = new ReelLayout();
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                layout.Placements.Add(new ReelPlacement { PartId = "P01", X = column, Y = 0 });
                layout.Placements.Add(new ReelPlacement { PartId = "P05", X = column, Y = 3 });
                layout.Placements.Add(new ReelPlacement { PartId = "P06", X = column, Y = 5 });
                layout.Placements.Add(new ReelPlacement { PartId = "P09", X = column, Y = 7 });
            }

            layout.Placements.Add(new ReelPlacement { PartId = "P07", X = 0, Y = 6 });
            layout.Placements.Add(new ReelPlacement { PartId = "P11", X = 2, Y = 6 });
            if (catalog == null)
                return layout;
            for (var i = layout.Placements.Count - 1; i >= 0; i--)
            {
                if (catalog.Find(layout.Placements[i].PartId) == null)
                    layout.Placements.RemoveAt(i);
            }

            return layout;
        }

        public void Clear() => Placements.Clear();

        public void LoadPreset(BoardGrid grid, ReelPartCatalog catalog)
        {
            Placements.Clear();
            if (grid == null)
                return;
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                for (var row = 0; row < BoardGrid.Rows; row++)
                {
                    var type = grid.Get(column, row);
                    var stamp = ReelPartCatalog.Stamp(type);
                    if (catalog != null && catalog.Find(stamp.Id) == null && stamp.Id[0] != '*')
                        continue;
                    Placements.Add(new ReelPlacement { PartId = stamp.Id, X = column, Y = row });
                }
            }
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

        public bool CanPlace(ReelPartCatalog catalog, ReelPart part, int originX, int originY, int ignoreIndex)
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
    }
}
