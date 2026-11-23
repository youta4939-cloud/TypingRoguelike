using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TypingRoguelike.Run
{
    /// <summary>3列×行数の盤面。行数は Data/ReelBoard.json の rows。</summary>
    public sealed class BoardGrid
    {
        public const int Columns = 3;
        public static int Rows = 6;

        readonly DungeonNodeType[,] _cells;

        public BoardGrid()
        {
            var rows = Rows < 1 ? 1 : Rows;
            _cells = new DungeonNodeType[Columns, rows];
        }

        public int RowCount => _cells.GetLength(1);

        public DungeonNodeType Get(int column, int row) => _cells[column, row];

        public void Set(int column, int row, DungeonNodeType type)
        {
            if (column < 0 || column >= Columns || row < 0 || row >= RowCount)
                return;
            _cells[column, row] = type;
        }

        public static BoardGrid All(DungeonNodeType type)
        {
            var grid = new BoardGrid();
            for (var column = 0; column < Columns; column++)
            {
                for (var row = 0; row < grid.RowCount; row++)
                    grid._cells[column, row] = type;
            }

            return grid;
        }

        public int Count(int column, DungeonNodeType type)
        {
            var count = 0;
            for (var row = 0; row < RowCount; row++)
            {
                if (_cells[column, row] == type)
                    count++;
            }

            return count;
        }

        public IReadOnlyList<DungeonNodeType> Column(int column)
        {
            var cells = new DungeonNodeType[RowCount];
            for (var row = 0; row < RowCount; row++)
                cells[row] = _cells[column, row];
            return cells;
        }

        public int ContentHash()
        {
            unchecked
            {
                var hash = 17;
                for (var column = 0; column < Columns; column++)
                {
                    for (var row = 0; row < RowCount; row++)
                        hash = hash * 31 + (int)_cells[column, row];
                }

                return hash;
            }
        }

        public static BoardGrid Parse(string column1, string column2, string column3)
        {
            var grid = new BoardGrid();
            Fill(grid, 0, column1);
            Fill(grid, 1, column2);
            Fill(grid, 2, column3);
            return grid;
        }

        public static DungeonNodeType ParseSymbol(char symbol)
        {
            switch (char.ToUpperInvariant(symbol))
            {
                case 'B': return DungeonNodeType.Combat;
                case 'R': return DungeonNodeType.Rest;
                case 'S': return DungeonNodeType.Merchant;
                case 'T': return DungeonNodeType.Treasure;
                case 'E': return DungeonNodeType.Event;
                default: return DungeonNodeType.Trap;
            }
        }

        public static string DescribeOdds(BoardGrid grid)
        {
            var types = RoadTypes();
            var text = new StringBuilder();
            for (var column = 0; column < Columns; column++)
            {
                text.Append($"{column + 1}マス目  ");
                foreach (var type in types)
                {
                    var percent = grid.Count(column, type) * 100f / grid.RowCount;
                    text.Append(LayerPathUI.SquareName(type));
                    text.Append(' ');
                    text.Append(percent.ToString("0.0", CultureInfo.InvariantCulture));
                    text.Append("%  ");
                }

                text.AppendLine();
            }

            return text.ToString().TrimEnd();
        }

        public static DungeonNodeType[] RoadTypes() => new[]
        {
            DungeonNodeType.Combat,
            DungeonNodeType.Rest,
            DungeonNodeType.Merchant,
            DungeonNodeType.Treasure,
            DungeonNodeType.Event,
            DungeonNodeType.Trap
        };

        static void Fill(BoardGrid grid, int column, string symbols)
        {
            for (var row = 0; row < grid.RowCount; row++)
            {
                var symbol = symbols != null && row < symbols.Length ? symbols[row] : 'X';
                grid._cells[column, row] = ParseSymbol(symbol);
            }
        }
    }
}
