namespace TypingRoguelike.Data
{
    /// <summary>戦闘演出プリセット ID。数値は後から EnemyVisualPresetLibrary で解釈する。</summary>
    public enum EnemyGraphicMode
    {
        PartRig = 0,
        SpritePair = 1,
        EmojiOnly = 2
    }

    public enum EnemyMotionPreset
    {
        None = 0,
        Idle = 1,
        Lunge = 2,
        RushForward = 3,
        SwingInPlace = 4,
        CastMagic = 5
    }

    public enum EnemyStrikeEffectKind
    {
        None = 0,
        Slash = 1,
        Thrust = 2,
        Bite = 3,
        Claw = 4,
        Magic = 5
    }

    public enum EnemyEffectPalette
    {
        Neutral = 0,
        BloodRed = 1,
        PoisonGreen = 2,
        CursePurple = 3,
        IceBlue = 4,
        HolyGold = 5
    }

    public enum EnemyEffectIntensity
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    public enum EnemyDefeatPreset
    {
        FadeOut = 0,
        TurnToAsh = 1,
        Crumble = 2,
        MistDissolve = 3
    }
}