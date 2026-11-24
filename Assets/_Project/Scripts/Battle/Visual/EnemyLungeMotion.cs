using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
    /// <summary>飛びかかり。時刻はため開始＝0。</summary>
    public static class EnemyLungeMotion
    {
        public const float DefaultWindup = 0.65f;
        const float WindupHold = 0.23f;
        const float CrouchTilt = -6f;
        static readonly Vector2 CrouchOffset = new(0.052f, -0.020f);
        public const float Leap = 0.13f;
        public const float Hang = 0.07f;
        public const float Dive = 0.09f;
        public const float ActiveToImpact = Leap + Hang + Dive;
        public const float ImpactTime = DefaultWindup + ActiveToImpact;
        public const float ImpactHold = 0.38f;
        public const float HopBack = 0.40f;
        public const float LandBounce = 0.50f;
        public const float MaxScale = 1.30f;

        public static EnemySpritePairPose Sample(float t, float windupSeconds, bool strikeAlreadyFired)
        {
            windupSeconds = Mathf.Max(0.2f, windupSeconds);
            t = Mathf.Max(0f, t);
            var pose = EnemySpritePairPose.Idle;
            pose.UseAttackSprite = t >= windupSeconds;

            if (t < windupSeconds)
            {
                var hold = Mathf.Min(WindupHold, windupSeconds * 0.36f);
                var crouchDuration = windupSeconds - hold;
                var k = EnemyVisualEasing.EaseOut(t / crouchDuration, 2.6f);
                pose.OffsetEh = CrouchOffset * k;
                pose.RotationDeg = Mathf.Lerp(0f, CrouchTilt, k);
                return pose;
            }

            var u = t - windupSeconds;
            if (u <= Leap)
            {
                var k = Leap > 0f ? u / Leap : 1f;
                k = EnemyVisualEasing.EaseOut(k, 4f);
                pose.OffsetEh = Vector2.LerpUnclamped(CrouchOffset, new Vector2(0.034f, 0.077f), k);
                pose.RotationDeg = Mathf.Lerp(CrouchTilt, 0f, k) + 3f * Mathf.Sin(Mathf.PI * k);
                return pose;
            }

            u -= Leap;
            if (u <= Hang)
            {
                var k = Hang > 0f ? u / Hang : 1f;
                pose.OffsetEh = new Vector2(0.034f, 0.077f + 0.012f * Mathf.Sin(Mathf.PI * k));
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
                pose.RotationDeg = Mathf.Lerp(0f, -3.4f, k);
                return pose;
            }

            u -= Dive;
            var impactEnd = ImpactHold;
            if (u < impactEnd)
            {
                var k = impactEnd > 0f ? u / impactEnd : 1f;
                var smooth = EnemyVisualEasing.SmoothStep(k);
                pose.OffsetEh = new Vector2(-0.034f, -0.010f);
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
                pose.OffsetEh.y += 0.095f * Mathf.Sin(Mathf.PI * k);
                pose.UseAttackSprite = true;
                return pose;
            }

            u -= HopBack;
            if (u <= LandBounce)
            {
                var settle = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u / 0.17f));
                pose.UseAttackSprite = false;
                pose.OffsetEh = new Vector2(0f, -0.008f * settle);
                pose.RotationDeg = -2f * settle;
                return pose;
            }

            pose.UseAttackSprite = false;
            return pose;
        }

        public static float TotalAttackDuration(float windupSeconds) =>
            windupSeconds + ActiveToImpact + ImpactHold + HopBack + LandBounce;
    }
}