using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TypingRoguelike.Run
{
    /// <summary>3列×9行の盤面。抽選とは分けて、あとからパーツ組み立てでも作れるようにする。</summary>
    public sealed class BoardGrid
    {
        public const int Columns = 3;
        public const int Rows = 9;

        readonly DungeonNodeType[,] _cells = new DungeonNodeType[Columns, Rows];

        public DungeonNodeType Get(int column, int row) => _cells[column, row];

        public int Count(int column, DungeonNodeType type)
        {
            var count = 0;
            for (var row = 0; row < Rows; row++)
            {
                if (_cells[column, row] == type)
                    count++;
            }

            return count;
        }

        public IReadOnlyList<DungeonNodeType> Column(int column)
        {
            var cells = new DungeonNodeType[Rows];
            for (var row = 0; row < Rows; row++)
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
                    for (var row = 0; row < Rows; row++)
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
                    var percent = grid.Count(column, type) * 100f / Rows;
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
            for (var row = 0; row < Rows; row++)
            {
                var symbol = symbols != null && row < symbols.Length ? symbols[row] : 'X';
                grid._cells[column, row] = ParseSymbol(symbol);
            }
        }
    }
}
