namespace TypingRoguelike.Run
{
    public enum DungeonNodeType
    {
        Start,
        Normal,
        Danger,
        Rest,
        Merchant,
        Treasure,
        Elevator,
        Boss,
        /// <summary>踏むと必ず戦闘。値は v1.2 以前のまま（セーブ互換）。</summary>
        Combat,
        /// <summary>v1.2 廃止。セーブ互換のみ。</summary>
        Elite,
        /// <summary>v1.2 廃止。セーブ互換のみ。</summary>
        Event,
        /// <summary>JSON の出来事。旧 Event の値は変えない。</summary>
        EventV2,
        /// <summary>踏むまで中身が分からない。末尾追加で既存の値は変えない。</summary>
        Mystery,
    }
}
