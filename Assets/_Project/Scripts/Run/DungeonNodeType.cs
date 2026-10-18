namespace TypingRoguelike.Run
{
    public enum DungeonNodeType
    {
        Start,
        Normal,
        /// <summary>廃止。番号は残して、古いセーブのマス種類がずれないようにする。</summary>
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
        /// <summary>廃止。番号は残して、古いセーブのマス種類がずれないようにする。</summary>
        Mystery,
    }
}
