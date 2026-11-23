using System;
using System.Collections.Generic;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public sealed class ReelCell
    {
        public int X;
        public int Y;
        public DungeonNodeType Type;
    }

    public sealed class ReelPart
    {
        public string Id;
        public string Name;
        public bool Locked;
        public bool Synthetic;
        public ReelCell[] Cells = Array.Empty<ReelCell>();

        public bool HasPenalty
        {
            get
            {
                foreach (var cell in Cells)
                {
                    if (cell != null && cell.Type == DungeonNodeType.Trap)
                        return true;
                }

                return false;
            }
        }

        public int Width
        {
            get
            {
                var width = 1;
                foreach (var cell in Cells)
                {
                    if (cell != null && cell.X + 1 > width)
                        width = cell.X + 1;
                }

                return width;
            }
        }

        public int Height
        {
            get
            {
                var height = 1;
                foreach (var cell in Cells)
                {
                    if (cell != null && cell.Y + 1 > height)
                        height = cell.Y + 1;
                }

                return height;
            }
        }
    }

    /// <summary>Resources/Data/ReelParts.json を読む。同じパーツは1つだけ持てる。</summary>
    public sealed class ReelPartCatalog
    {
        public ReelPart[] Parts = Array.Empty<ReelPart>();

        public ReelPart Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (id.Length == 2 && id[0] == '*')
                return Stamp(BoardGrid.ParseSymbol(id[1]));
            foreach (var part in Parts)
            {
                if (part != null && part.Id == id)
                    return part;
            }

            return null;
        }

        public bool IsPlaceable(string id)
        {
            var part = Find(id);
            return part != null && !part.Synthetic && part.Cells != null && part.Cells.Length >= 2;
        }

        public int Owned(string id, bool debugUnlocked)
        {
            var part = Find(id);
            if (part == null || part.Synthetic || part.Cells == null || part.Cells.Length < 2)
                return 0;
            if (part.Locked && !debugUnlocked)
                return 0;
            return 1;
        }

        public static ReelPart Stamp(DungeonNodeType type) => new()
        {
            Id = "*" + Symbol(type),
            Name = "印",
            Synthetic = true,
            Cells = new[] { new ReelCell { X = 0, Y = 0, Type = type } }
        };

        public static char Symbol(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Combat => 'B',
            DungeonNodeType.Rest => 'R',
            DungeonNodeType.Merchant => 'S',
            DungeonNodeType.Treasure => 'T',
            DungeonNodeType.Event => 'E',
            _ => 'X'
        };

        public static ReelPartCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Data/ReelParts");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return Fallback();
            try
            {
                var file = JsonUtility.FromJson<ReelPartsFile>(asset.text);
                var catalog = FromFile(file);
                if (catalog.Parts.Length == 0)
                    return Fallback();
                return catalog;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"リールパーツを読めませんでした: {ex.Message}");
                return Fallback();
            }
        }

        public static ReelPartCatalog Fallback()
        {
            return FromFile(new ReelPartsFile
            {
                parts = new[]
                {
                    Rec("P01", "戦闘・縦3", true, "B", 0, 0, "B", 0, 1, "B", 0, 2),
                    Rec("P02", "戦闘・L字", true, "B", 0, 0, "B", 0, 1, "B", 1, 1),
                    Rec("P03", "戦闘・横2", true, "B", 0, 0, "B", 1, 0),
                    Rec("P05", "回復・縦2", true, "R", 0, 0, "R", 0, 1),
                    Rec("P07", "宝・横2", true, "T", 0, 0, "T", 1, 0),
                    Rec("P08", "宝・T字", true, "T", 0, 0, "T", 1, 0, "T", 2, 0, "X", 1, 1),
                    Rec("P09", "イベント・縦2", true, "E", 0, 0, "E", 0, 1),
                    Rec("P10", "イベント・コ字", true, "E", 0, 0, "E", 1, 0, "E", 0, 1, "E", 0, 2, "X", 1, 2),
                    Rec("P12", "戦闘・縦2", false, "B", 0, 0, "B", 0, 1),
                    Rec("P14", "戦闘・四角", false, "B", 0, 0, "B", 1, 0, "B", 0, 1, "B", 1, 1),
                    Rec("P15", "回復・横3", false, "R", 0, 0, "R", 1, 0, "R", 2, 0),
                    Rec("P16", "店・横3", false, "S", 0, 0, "S", 1, 0, "S", 2, 0),
                    Rec("P17", "宝・横3", false, "T", 0, 0, "T", 1, 0, "T", 2, 0),
                    Rec("P18", "イベント・横3", false, "E", 0, 0, "E", 1, 0, "E", 2, 0),
                    Rec("P19", "戦闘・横3", true, "B", 0, 0, "B", 1, 0, "B", 2, 0)
                }
            });
        }

        static ReelPartCatalog FromFile(ReelPartsFile file)
        {
            var list = new List<ReelPart>();
            if (file?.parts == null)
                return new ReelPartCatalog();
            foreach (var record in file.parts)
            {
                if (record == null || string.IsNullOrEmpty(record.id) || record.cells == null)
                    continue;
                if (record.cells.Length < 2)
                {
                    Debug.LogWarning($"パーツ {record.id} は1マスなので読み込みません。");
                    continue;
                }
                var cells = new ReelCell[record.cells.Length];
                for (var i = 0; i < record.cells.Length; i++)
                {
                    var cell = record.cells[i];
                    cells[i] = new ReelCell
                    {
                        X = cell != null ? cell.x : 0,
                        Y = cell != null ? cell.y : 0,
                        Type = BoardGrid.ParseSymbol(cell != null && !string.IsNullOrEmpty(cell.type) ? cell.type[0] : 'X')
                    };
                }

                list.Add(new ReelPart
                {
                    Id = record.id,
                    Name = string.IsNullOrEmpty(record.name) ? record.id : record.name,
                    Locked = record.locked,
                    Cells = cells
                });
            }

            return new ReelPartCatalog { Parts = list.ToArray() };
        }

        static ReelPartRecord Rec(string id, string name, bool locked, params object[] triples)
        {
            var cells = new ReelCellRecord[triples.Length / 3];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new ReelCellRecord
                {
                    type = (string)triples[i * 3],
                    x = (int)triples[i * 3 + 1],
                    y = (int)triples[i * 3 + 2]
                };
            }

            return new ReelPartRecord
            {
                id = id,
                name = name,
                locked = locked,
                cells = cells
            };
        }

        [Serializable]
        sealed class ReelPartsFile
        {
            public ReelPartRecord[] parts;
        }

        [Serializable]
        sealed class ReelPartRecord
        {
            public string id;
            public string name;
            public bool locked;
            public ReelCellRecord[] cells;
        }

        [Serializable]
        sealed class ReelCellRecord
        {
            public int x;
            public int y;
            public string type;
        }
    }
}
