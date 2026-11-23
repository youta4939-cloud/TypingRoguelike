using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
    /// <summary>imp v2 確定値（enemy_visual_preset_spec §4.1）。時刻はため開始＝0。</summary>
    public static class EnemyLungeMotion
    {
        public const float DefaultWindup = 0.65f;
        public const float EaseWindup = 0.57f;
        public const float WindupHold = 0.08f;
        public const float Leap = 0.13f;
        public const float Hang = 0.07f;
        public const float Dive = 0.09f;
        public const float ActiveToImpact = Leap + Hang + Dive;
        public const float ImpactTime = DefaultWindup + ActiveToImpact;
        public const float ImpactHold = 0.38f;
        public const float HopBack = 0.40f;
        public const float LandBounce = 0.50f;
        public const float MaxScale = 1.30f;
        public const float ApexScale = 1.12f;

        public static EnemySpritePairPose Sample(float t, float windupSeconds, bool strikeAlreadyFired)
        {
            windupSeconds = Mathf.Max(0.2f, windupSeconds);
            t = Mathf.Max(0f, t);
            var pose = EnemySpritePairPose.Idle;
            pose.UseAttackSprite = t >= windupSeconds;

            if (t < windupSeconds)
            {
                var easeDur = Mathf.Min(EaseWindup, windupSeconds);
                var k = easeDur > 0f ? Mathf.Clamp01(t / easeDur) : 1f;
                k = EnemyVisualEasing.EaseIn(k, 2.4f);
                pose.OffsetEh = new Vector2(0.028f * k, -0.006f * k);
                pose.ScaleX = Mathf.Lerp(1f, 1.10f, k);
                pose.ScaleY = Mathf.Lerp(1f, 0.81f, k);
                pose.RotationDeg = Mathf.Lerp(0f, -4f, k);
                return pose;
            }

            var u = t - windupSeconds;
            if (u <= Leap)
            {
                var k = Leap > 0f ? u / Leap : 1f;
                k = EnemyVisualEasing.EaseOut(k, 4f);
                pose.OffsetEh = Vector2.LerpUnclamped(Vector2.zero, new Vector2(0.034f, 0.077f), k);
                var syStretch = 1f + 0.10f * Mathf.Sin(Mathf.PI * k);
                var baseScale = Mathf.Lerp(1f, ApexScale, k);
                pose.ScaleY = baseScale * syStretch;
                pose.ScaleX = baseScale / Mathf.Sqrt(Mathf.Max(0.01f, syStretch));
                pose.RotationDeg = Mathf.Lerp(6.9f, 0f, k);
                return pose;
            }

            u -= Leap;
            if (u <= Hang)
            {
                var k = Hang > 0f ? u / Hang : 1f;
                pose.OffsetEh = new Vector2(0.034f, 0.077f + 0.012f * Mathf.Sin(Mathf.PI * k));
                pose.ScaleX = pose.ScaleY = ApexScale;
                return pose;
            }

            u -= Hang;
            if (u <= Dive)
            {
                var k = Dive > 0f ? u / Dive : 1f;
                k = EnemyVisualEasing.EaseIn(k, 2.2f);
                var apex = new Vector2(0.034f, 0.077f);
                var strike = new Vector2(-0.034f, -0.010f);
                pose.OffsetEh = Vector2.LerpUnclamped(apex, strike, k);
                var scale = Mathf.Lerp(ApexScale, MaxScale, k) * (1f + 0.06f * k);
                pose.ScaleY = scale;
                pose.ScaleX = scale;
                pose.RotationDeg = Mathf.Lerp(0f, -3.4f, k);
                return pose;
            }

            u -= Dive;
            var impactStart = 0f;
            var impactEnd = ImpactHold;
            if (u < impactEnd)
            {
                var k = impactEnd > 0f ? u / impactEnd : 1f;
                var smooth = EnemyVisualEasing.SmoothStep(k);
                var sq = 0.08f * Mathf.Exp(-14f * k);
                pose.OffsetEh = new Vector2(-0.034f, -0.010f);
                var baseS = Mathf.Lerp(MaxScale, 1.26f, smooth);
                pose.ScaleX = baseS * (1f + sq);
                pose.ScaleY = baseS * (1f - sq);
                pose.RotationDeg = Mathf.Lerp(-3.4f, 0f, smooth);
                if (!strikeAlreadyFired)
                {
                    pose.StrikeThisFrame = true;
                    pose.ShakeThisFrame = true;
                }
                else if (u >= 0.02f && u < 0.25f)
                    pose.ShakeThisFrame = true;
                return pose;
            }

            u -= impactEnd;
            if (u <= HopBack)
            {
                var k = HopBack > 0f ? u / HopBack : 1f;
                var io = EnemyVisualEasing.SmoothStep(k);
                var strike = new Vector2(-0.034f, -0.010f);
                pose.OffsetEh = Vector2.LerpUnclamped(strike, Vector2.zero, io);
                pose.OffsetEh.y += 0.141f * Mathf.Sin(Mathf.PI * k);
                pose.ScaleX = pose.ScaleY = Mathf.Lerp(1.26f, 1f, io);
                pose.UseAttackSprite = true;
                return pose;
            }

            u -= HopBack;
            if (u <= LandBounce)
            {
                var k = LandBounce > 0f ? u / LandBounce : 1f;
                var sq = 0.10f * Mathf.Exp(-9f * k) * Mathf.Cos(22f * k);
                pose.UseAttackSprite = false;
                pose.OffsetEh = Vector2.zero;
                pose.ScaleX = 1f + 0.5f * sq;
                pose.ScaleY = 1f - sq;
                return pose;
            }

            pose.UseAttackSprite = false;
            return pose;
        }

        public static float TotalAttackDuration(float windupSeconds) =>
            windupSeconds + ActiveToImpact + ImpactHold + HopBack + LandBounce;
    }
}