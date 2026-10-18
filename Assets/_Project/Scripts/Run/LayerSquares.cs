using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    /// <summary>1層4マス。1〜3は仮のランダム、4マス目は層番号で固定。</summary>
    public static class LayerSquares
    {
        public const int Count = 4;

        static readonly DungeonNodeType[] RoadTypes =
        {
            DungeonNodeType.Combat,
            DungeonNodeType.Rest,
            DungeonNodeType.Merchant,
            DungeonNodeType.Treasure,
            DungeonNodeType.Event,
            DungeonNodeType.Trap
        };

        public static DungeonNodeType[] DrawSquares(int layer, int runSeed, LayerConfig config)
        {
            var rng = new System.Random(unchecked(runSeed * 397 ^ layer * 7919));
            return DrawSquares(layer, config, rng);
        }

        public static DungeonNodeType[] DrawSquares(int layer, LayerConfig config, System.Random rng)
        {
            config ??= LayerConfig.RuntimeFallback();
            rng ??= new System.Random(layer);
            var squares = new DungeonNodeType[Count];
            for (var i = 0; i < Count - 1; i++)
                squares[i] = RollRoadSquare(rng);
            squares[Count - 1] = FixedSquare(layer, config);
            return squares;
        }

        static DungeonNodeType RollRoadSquare(System.Random rng) =>
            RoadTypes[rng.Next(RoadTypes.Length)];

        public static DungeonNodeType FixedSquare(int layer, LayerConfig config)
        {
            var bossEvery = config != null && config.bossEvery > 0 ? config.bossEvery : 5;
            var elevatorEvery = config != null && config.elevatorEvery > 0 ? config.elevatorEvery : 3;
            if (layer % bossEvery == 0)
                return DungeonNodeType.Boss;
            if (layer % elevatorEvery == 0)
                return DungeonNodeType.Elevator;
            return DungeonNodeType.MidBoss;
        }
    }
}
