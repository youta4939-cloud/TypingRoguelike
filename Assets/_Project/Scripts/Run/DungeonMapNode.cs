using System.Collections.Generic;

namespace TypingRoguelike.Run
{
    public sealed class DungeonMapNode
    {
        public int Id { get; }
        public int Layer { get; }
        public int Column { get; }
        public DungeonNodeType Type { get; set; }
        public List<int> Next { get; } = new();
        public bool Cleared { get; set; }
        public bool Revealed { get; set; }
        /// <summary><see cref="DungeonNodeType.Danger"/> のみ。1〜99 の遭遇率（%）。</summary>
        public int DangerEncounterPercent { get; set; }
        /// <summary>危険察知パッシブで判明した強敵予兆（null=未判明）。</summary>
        public bool? DangerElitePreview { get; set; }

        public DungeonMapNode(int id, int layer, int column, DungeonNodeType type)
        {
            Id = id;
            Layer = layer;
            Column = column;
            Type = type;
        }
    }
}
