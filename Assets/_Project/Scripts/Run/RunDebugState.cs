namespace TypingRoguelike.Run
{
    /// <summary>エディタ／開発ビルド専用のラン全体デバッグフラグ。</summary>
    public static class RunDebugState
    {
        public const float JackpotForcedRollChance = 0.1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool JackpotRollForce10Percent { get; set; }

        public static float? JackpotRollChanceOverride =>
            JackpotRollForce10Percent ? JackpotForcedRollChance : (float?)null;
#else
        public static float? JackpotRollChanceOverride => null;
#endif
    }
}
