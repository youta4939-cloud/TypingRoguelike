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
        /// <summary>v1.2 廃止。セーブ互換のみ。</summary>
        Combat,
        /// <summary>v1.2 廃止。セーブ互換のみ。</summary>
        Elite,
        /// <summary>v1.2 廃止。セーブ互換のみ。</summary>
        Event,
        /// <summary>JSON の出来事。旧 Event の値は変えない。</summary>
        EventV2,
    }
}
