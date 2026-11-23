using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
    public static class EnemyVisualEasing
    {
        public static float Clamp01(float t) => Mathf.Clamp01(t);

        public static float EaseIn(float t, float power = 2.4f)
        {
            t = Clamp01(t);
            return Mathf.Pow(t, power);
        }

        public static float EaseOut(float t, float power = 4f)
        {
            t = Clamp01(t);
            return 1f - Mathf.Pow(1f - t, power);
        }

        public static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float SmoothStepRange(float t, float start, float end)
        {
            if (end <= start)
                return t >= end ? 1f : 0f;
            return SmoothStep((t - start) / (end - start));
        }

        public static float LerpEaseIn(float start, float end, float t, float power = 2.4f) =>
            Mathf.LerpUnclamped(start, end, EaseIn(t, power));
    }
}