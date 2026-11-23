using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
    [CreateAssetMenu(fileName = "EnemyVisualPresetLibrary", menuName = "TypingRoguelike/Enemy Visual Preset Library")]
    public sealed class EnemyVisualPresetLibrary : ScriptableObject
    {
        const string ResourcePath = "Battle/EnemyVisualPresetLibrary";
        static EnemyVisualPresetLibrary _fallback;

        [Header("待機呼吸（確定）")]
        public float breathPeriod = 1.60f;
        public float breathScaleY = 0.016f;
        public float breathScaleX = 0.008f;

        [Header("狂暴（仮）")]
        public float enrageBreathPeriod = 1.00f;
        public float enrageBreathAmplitudeMul = 1.25f;
        public float enrageTintStrength = 0.35f;

        [Header("ブレイク（仮）")]
        public float breakTiltDeg = 8f;
        public float breakDarken = 0.72f;
        public float breakBreathPeriod = 2.20f;

        [Header("強度 Medium = 1（§6）")]
        public float intensitySmall = 0.67f;
        public float intensityLarge = 1.44f;
        public float shakeAmpMediumPx = 9f;
        public float shakeAmpSmallPx = 6f;
        public float shakeAmpLargePx = 13f;
        public float localFlashPeakMedium = 0.55f;

        public static EnemyVisualPresetLibrary Instance
        {
            get
            {
                var loaded = Resources.Load<EnemyVisualPresetLibrary>(ResourcePath);
                if (loaded != null)
                    return loaded;
                if (_fallback == null)
                {
                    _fallback = CreateInstance<EnemyVisualPresetLibrary>();
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }

                return _fallback;
            }
        }

        public struct ResolvedAttackTiming
        {
            public float WindupSeconds;
            public float ActiveSeconds;
            public float TimeToImpact;
            public float MaxScale;
            public EnemyEffectIntensity Intensity;
        }

        public EnemyEffectIntensity ResolveIntensity(EnemyDefinition enemy, EnemyAttackPattern pattern)
        {
            if (pattern.visualIntensity != EnemyAttackVisualIntensity.Inherit)
            {
                return pattern.visualIntensity switch
                {
                    EnemyAttackVisualIntensity.Small => EnemyEffectIntensity.Small,
                    EnemyAttackVisualIntensity.Large => EnemyEffectIntensity.Large,
                    _ => EnemyEffectIntensity.Medium
                };
            }

            return enemy != null ? enemy.visual.effectIntensity : EnemyEffectIntensity.Medium;
        }

        public float IntensityMultiplier(EnemyEffectIntensity intensity) => intensity switch
        {
            EnemyEffectIntensity.Small => intensitySmall,
            EnemyEffectIntensity.Large => intensityLarge,
            _ => 1f
        };

        public ResolvedAttackTiming ResolveAttackTiming(EnemyDefinition enemy, EnemyAttackPattern pattern)
        {
            var reg = enemy != null ? enemy.visual : EnemyVisualRegistration.Default;
            var motion = reg.motionPreset;
            var windup = ResolveWindupSeconds(enemy, pattern, motion);
            var active = ResolveActiveSeconds(reg, motion);
            var timing = new ResolvedAttackTiming
            {
                WindupSeconds = windup,
                ActiveSeconds = active,
                TimeToImpact = windup + active,
                MaxScale = ResolveMaxScale(reg, motion),
                Intensity = ResolveIntensity(enemy, pattern)
            };
            timing.MaxScale = Mathf.Min(timing.MaxScale, 1.30f);
            return timing;
        }

        public float ResolveWindupSeconds(EnemyDefinition enemy, EnemyAttackPattern pattern, EnemyMotionPreset motion)
        {
            if (pattern.effect == EnemyAttackEffectKind.Telegraph && pattern.effectValue > 0f)
                return Mathf.Max(0.2f, pattern.effectValue);

            var reg = enemy != null ? enemy.visual : EnemyVisualRegistration.Default;
            if (reg.windupSecondsOverride > 0f)
                return reg.windupSecondsOverride;

            return DefaultWindup(motion);
        }

        public float ResolveActiveSeconds(EnemyVisualRegistration reg, EnemyMotionPreset motion)
        {
            if (reg.activeSecondsOverride > 0f)
                return reg.activeSecondsOverride;

            return motion switch
            {
                EnemyMotionPreset.Lunge => EnemyLungeMotion.ActiveToImpact,
                EnemyMotionPreset.RushForward => EnemyRushMotion.ActiveToImpact,
                EnemyMotionPreset.SwingInPlace => EnemySwingMotion.ActiveToImpact,
                EnemyMotionPreset.CastMagic => EnemyCastMotion.ActiveToImpact,
                EnemyMotionPreset.Idle => 0.15f,
                _ => 0.05f
            };
        }

        public float DefaultWindup(EnemyMotionPreset motion) => motion switch
        {
            EnemyMotionPreset.Lunge => EnemyLungeMotion.DefaultWindup,
            EnemyMotionPreset.RushForward => EnemyRushMotion.DefaultWindup,
            EnemyMotionPreset.SwingInPlace => EnemySwingMotion.DefaultWindup,
            EnemyMotionPreset.CastMagic => EnemyCastMotion.DefaultWindup,
            EnemyMotionPreset.Idle => 0.20f,
            _ => 0f
        };

        float ResolveMaxScale(EnemyVisualRegistration reg, EnemyMotionPreset motion)
        {
            var scale = reg.strikeScaleOverride > 0f ? reg.strikeScaleOverride : motion switch
            {
                EnemyMotionPreset.Lunge => EnemyLungeMotion.MaxScale,
                EnemyMotionPreset.RushForward => EnemyRushMotion.MaxScale,
                EnemyMotionPreset.SwingInPlace => EnemySwingMotion.MaxScale,
                EnemyMotionPreset.CastMagic => EnemyCastMotion.MaxScale,
                EnemyMotionPreset.Idle => 1.06f,
                _ => 1.05f
            };
            return Mathf.Min(scale, 1.30f);
        }

        public float ResolveTotalAttackDuration(EnemyDefinition enemy, EnemyAttackPattern pattern)
        {
            var reg = enemy != null ? enemy.visual : EnemyVisualRegistration.Default;
            var windup = ResolveWindupSeconds(enemy, pattern, reg.motionPreset);
            return reg.motionPreset switch
            {
                EnemyMotionPreset.Lunge => EnemyLungeMotion.TotalAttackDuration(windup),
                EnemyMotionPreset.RushForward => EnemyRushMotion.TotalAttackDuration(windup),
                EnemyMotionPreset.SwingInPlace => EnemySwingMotion.TotalAttackDuration(
                    windup, EnemySwingMotion.HitStopSeconds(ResolveIntensity(enemy, pattern))),
                EnemyMotionPreset.CastMagic => EnemyCastMotion.TotalAttackDuration(windup),
                _ => windup + 0.75f
            };
        }

        public EnemySpritePairPose SampleAttackPose(
            EnemyDefinition enemy,
            EnemyAttackPattern pattern,
            float attackTime,
            bool strikeAlreadyFired)
        {
            var reg = enemy != null ? enemy.visual : EnemyVisualRegistration.Default;
            var windup = ResolveWindupSeconds(enemy, pattern, reg.motionPreset);
            var intensity = ResolveIntensity(enemy, pattern);
            var pose = reg.motionPreset switch
            {
                EnemyMotionPreset.Lunge => EnemyLungeMotion.Sample(attackTime, windup, strikeAlreadyFired),
                EnemyMotionPreset.RushForward => EnemyRushMotion.Sample(attackTime, windup, strikeAlreadyFired),
                EnemyMotionPreset.SwingInPlace => EnemySwingMotion.Sample(
                    attackTime, windup, strikeAlreadyFired, EnemySwingMotion.HitStopSeconds(intensity)),
                EnemyMotionPreset.CastMagic => EnemyCastMotion.Sample(attackTime, windup, strikeAlreadyFired),
                EnemyMotionPreset.Idle => SampleIdleAttack(attackTime, windup),
                EnemyMotionPreset.None => EnemySpritePairPose.Idle,
                _ => EnemySpritePairPose.Idle
            };
            ClampScale(ref pose, ResolveMaxScale(reg, reg.motionPreset));
            return pose;
        }

        static void ClampScale(ref EnemySpritePairPose pose, float maxScale)
        {
            // 等方上限は maxScale。伸びで一時的に超える分はハードキャップ 1.30
            var hard = 1.30f;
            pose.ScaleX = Mathf.Min(pose.ScaleX, hard);
            pose.ScaleY = Mathf.Min(pose.ScaleY, hard);
        }

        static EnemySpritePairPose SampleIdleAttack(float t, float windup)
        {
            var pose = EnemySpritePairPose.Idle;
            if (t < windup)
                return pose;
            pose.UseAttackSprite = t < windup + 0.50f;
            return pose;
        }

        public EnemySpritePairPose SampleBreath(float time, bool enraged)
        {
            var period = enraged ? enrageBreathPeriod : breathPeriod;
            var ampMul = enraged ? enrageBreathAmplitudeMul : 1f;
            var w = Mathf.Sin(2f * Mathf.PI * time / period);
            return new EnemySpritePairPose
            {
                ScaleY = 1f + breathScaleY * ampMul * w,
                ScaleX = 1f - breathScaleX * ampMul * w,
                Tint = enraged
                    ? Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), enrageTintStrength)
                    : Color.white
            };
        }

        public EnemySpritePairPose SamplePlayerHit(float time, Color flashColor, EnemyEffectIntensity intensity)
        {
            var mul = IntensityMultiplier(intensity);
            var amp = shakeAmpMediumPx * mul / 496f;
            var pose = EnemySpritePairPose.Idle;
            pose.OffsetEh = new Vector2(
                0.028f * mul * Mathf.Exp(-10f * time) * Mathf.Cos(55f * time),
                0.006f * mul * Mathf.Exp(-10f * time));
            pose.RotationDeg = 2.9f * mul * Mathf.Exp(-9f * time);
            var flash = 0.65f * Mathf.Exp(-16f * time);
            pose.Tint = Color.Lerp(Color.white, flashColor, flash);
            if (time < 0.25f)
                pose.ShakeThisFrame = true;
            return pose;
        }

        public EnemySpritePairPose SampleDefeat(EnemyDefeatPreset preset, float time)
        {
            var pose = EnemySpritePairPose.Idle;
            if (preset == EnemyDefeatPreset.TurnToAsh && time < 0.30f)
            {
                var k = EnemyVisualEasing.SmoothStep(time / 0.30f);
                pose.Tint = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.25f), k * 0.75f);
                pose.ScaleY = 1f - 0.04f * k;
                pose.ScaleX = 1f + 0.02f * k;
            }
            else if (time > 0f)
            {
                pose.Alpha = Mathf.Lerp(1f, 0.12f, EnemyVisualEasing.SmoothStep(Mathf.Clamp01(time / 0.70f)));
            }

            return pose;
        }
    }
}