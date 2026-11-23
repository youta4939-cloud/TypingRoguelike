using System;
using System.Globalization;

namespace TypingRoguelike.Run
{
    /// <summary>入力した確率でジャックポットを独立に1000回引き、ランの乱数は使わない。</summary>
    public static class JackpotRollSimulation
    {
        public const int Trials = 1000;

        public static bool TryParseChance(string raw, out float chance)
        {
            chance = 0f;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var text = raw.Trim().Replace('％', '%');
            var asPercent = text.EndsWith("%", StringComparison.Ordinal);
            if (asPercent)
                text = text.Substring(0, text.Length - 1).Trim();
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return false;
            if (asPercent || value > 1f)
                value /= 100f;
            if (value < 0f || value > 1f)
                return false;

            chance = value;
            return true;
        }

        public static string Run(float chance, int trials = Trials)
        {
            trials = Math.Max(1, trials);
            var rng = new Random();
            var hits = 0;
            var missStreak = 0;
            var longestMiss = 0;
            long gapSum = 0;
            var gapCount = 0;
            for (var i = 0; i < trials; i++)
            {
                if (rng.NextDouble() < chance)
                {
                    hits++;
                    if (hits > 1)
                    {
                        gapSum += missStreak;
                        gapCount++;
                    }

                    if (missStreak > longestMiss)
                        longestMiss = missStreak;
                    missStreak = 0;
                }
                else
                {
                    missStreak++;
                }
            }

            if (missStreak > longestMiss)
                longestMiss = missStreak;

            var expected = trials * chance;
            var actualPercent = hits * 100f / trials;
            var text =
                $"{chance * 100f:0.####}% で {trials} 回引いた\n" +
                $"発動 {hits} 回（実測 {actualPercent:0.##}%）\n" +
                $"期待 {expected:0.##} 回\n" +
                $"いちばん長く外れた連続 {longestMiss} 回";
            if (gapCount > 0)
                text += $"\n発動と発動のあいだに外れた回数の平均 {gapSum / (float)gapCount:0.#} 回";
            return text;
        }
    }

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
