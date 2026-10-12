namespace TypingRoguelike.Data
{
    /// <summary>キャラ固有探索パッシブ（編成内で同種は1人分のみ有効）。</summary>
    public enum ExplorationPassiveKind
    {
        None = 0,
        /// <summary>Danger 遭遇率を value% 低下。</summary>
        Cautious = 1,
        /// <summary>次に進めるマスの種類を事前表示。</summary>
        Pathfinder = 2,
        /// <summary>Danger マスで強敵かどうかを事前表示。</summary>
        DangerSense = 3,
        /// <summary>近くの宝箱マスを察知。</summary>
        TreasureHunter = 4,
        /// <summary>当階のエレベーター位置を表示。</summary>
        ElevatorSense = 5,
    }
}
