using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
  /// <summary>CastMagic — enemy_visual_preset_spec_v2 §4.5 / §4.6。</summary>
  public static class EnemyCastMotion
  {
    public const float DefaultWindup = 0.95f;
    public const float RiseMax = 0.40f;
    public const float ActiveToImpact = 0.233f;
    public const float Recoil = 0.30f;
    public const float Descend = 0.52f;
    public const float Settle = 0.35f;
    public const float MaxScale = 1.10f;

    // TODO: オーラ／色味脈動／吸い込み粒（§4.5、UI・敵の後ろ）

    public static float TotalAttackDuration(float windupSeconds) =>
      Mathf.Max(0.2f, windupSeconds) + ActiveToImpact + Recoil + Descend + Settle;

    public static EnemySpritePairPose Sample(float t, float windupSeconds, bool strikeAlreadyFired)
    {
      var W = Mathf.Max(0.2f, windupSeconds);
      t = Mathf.Max(0f, t);
      var pose = EnemySpritePairPose.Idle;

      var rise = Mathf.Min(RiseMax, 0.42f * W);
      rise = Mathf.Min(rise, W);
      var charge = Mathf.Max(0f, W - rise);

      if (t < rise)
      {
        var k = EnemyVisualEasing.EaseOut(rise > 0f ? t / rise : 1f, 2f);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(0f, -0.090f * k);
        var sc = Mathf.Lerp(1f, 1.04f, k);
        pose.ScaleX = pose.ScaleY = sc;
        pose.UseAttackSprite = false;
        // tint 近似: 浮上中の弱い紫
        pose.Tint = Color.Lerp(Color.white, new Color(0.85f, 0.75f, 0.95f), 0.15f * k);
        return pose;
      }

      if (t < W)
      {
        var k = t - rise;
        var c = charge > 0f ? Mathf.Clamp01(k / charge) : 1f;
        var tremble = Mathf.Lerp(0.002f, 0.007f, c) * Mathf.Sin(2f * Mathf.PI * 18f * t);
        var bob = 0.012f * Mathf.Sin(2f * Mathf.PI * k / 0.50f);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(tremble, -0.090f + bob);
        var sc = Mathf.Lerp(1.04f, 1.07f, EnemyVisualEasing.SmoothStep(c));
        var pop = 1f + 0.05f * Mathf.Exp(-Mathf.Pow(k / 0.04f, 2f));
        pose.ScaleX = sc;
        pose.ScaleY = sc * pop;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(0.02f * Mathf.Sin(2f * Mathf.PI * k / 0.8f));
        pose.UseAttackSprite = true;
        var pulHz = Mathf.Lerp(3f, 7f, c);
        var pul = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * k * pulHz);
        var tintAmt = Mathf.Lerp(0.15f, 0.35f, c) * (0.6f + 0.4f * pul);
        pose.Tint = Color.Lerp(Color.white, new Color(0.42f, 0.10f, 0.55f), tintAmt);
        return pose;
      }

      var u = t - W;
      if (u <= ActiveToImpact)
      {
        var e = EnemyVisualEasing.EaseOut(ActiveToImpact > 0f ? u / ActiveToImpact : 1f, 3f);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(-0.030f * e, -0.090f);
        var sc = Mathf.Lerp(1.07f, MaxScale, e);
        pose.ScaleX = pose.ScaleY = sc;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(-0.06f * e);
        pose.UseAttackSprite = true;
        pose.Tint = Color.Lerp(Color.white, new Color(0.42f, 0.10f, 0.55f), 0.35f * (1f - 0.45f * e));
        return pose;
      }

      u -= ActiveToImpact;
      if (u <= Recoil)
      {
        var k = u;
        var push = 0.10f * (1f - Mathf.Exp(-22f * k)) - 0.04f * EnemyVisualEasing.SmoothStepRange(k, 0.10f, 0.30f);
        var dySample = -0.090f - 0.020f * EnemyVisualEasing.EaseOut(Mathf.Clamp01(k / 0.12f), 2f);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(-0.030f + push, dySample);
        var sc = Mathf.Lerp(MaxScale, 1.06f, EnemyVisualEasing.SmoothStepRange(k, 0f, 0.30f));
        var sq = 0.08f * Mathf.Exp(-14f * k);
        pose.ScaleX = sc * (1f - sq);
        pose.ScaleY = sc * (1f + 0.5f * sq);
        float rot;
        if (k < 0.08f)
          rot = Mathf.Lerp(-0.06f, 0.14f, EnemyVisualEasing.EaseOut(k / 0.08f, 2f));
        else
          rot = Mathf.Lerp(0.14f, 0.05f, EnemyVisualEasing.SmoothStepRange(k, 0.08f, 0.30f));
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(rot);
        pose.UseAttackSprite = true;
        pose.Tint = Color.Lerp(Color.white, new Color(0.42f, 0.10f, 0.55f), 0.35f * Mathf.Exp(-5f * k));
        if (!strikeAlreadyFired)
        {
          pose.StrikeThisFrame = true;
          pose.ShakeThisFrame = true;
        }
        else if (k >= 0.02f && k < 0.25f)
          pose.ShakeThisFrame = true;
        return pose;
      }

      u -= Recoil;
      if (u <= Descend)
      {
        var m = EnemyVisualEasing.SmoothStep(Descend > 0f ? u / Descend : 1f);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(
          Mathf.Lerp(0.030f, 0f, m),
          Mathf.Lerp(-0.110f, 0f, m));
        var sc = Mathf.Lerp(1.06f, 1f, m);
        pose.ScaleX = pose.ScaleY = sc;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(Mathf.Lerp(0.05f, 0f, m));
        pose.UseAttackSprite = true;
        return pose;
      }

      u -= Descend;
      if (u <= Settle)
      {
        EnemyMotionSampleUtil.ApplySettleSquash(ref pose, Settle > 0f ? u / Settle : 1f, 0.08f);
        return pose;
      }

      return EnemySpritePairPose.Idle;
    }
  }
}
