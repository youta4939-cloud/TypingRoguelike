using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
  /// <summary>RushForward — enemy_visual_preset_spec_v2 §4.3 / §4.6。</summary>
  public static class EnemyRushMotion
  {
    public const float DefaultWindup = 0.50f;
    public const float PoseInMax = 0.42f;
    public const float ActiveToImpact = 0.14f;
    public const float ImpactHold = 0.30f;
    public const float Slide = 0.46f;
    public const float Settle = 0.40f;
    public const float MaxScale = 1.22f;
    public const float HitDx = -0.280f;

    // TODO: 残像4枚 / 土煙 / 集中線（§4.3）

    public static float TotalAttackDuration(float windupSeconds) =>
      Mathf.Max(0.2f, windupSeconds) + ActiveToImpact + ImpactHold + Slide + Settle;

    public static EnemySpritePairPose Sample(float t, float windupSeconds, bool strikeAlreadyFired)
    {
      var W = Mathf.Max(0.2f, windupSeconds);
      t = Mathf.Max(0f, t);
      var pose = EnemySpritePairPose.Idle;

      var poseIn = W >= PoseInMax + 0.06f
        ? PoseInMax
        : Mathf.Max(0.14f, W - 0.06f);
      poseIn = Mathf.Min(poseIn, W);

      if (t < W)
      {
        var k = poseIn > 0f ? Mathf.Clamp01(t / poseIn) : 1f;
        k = EnemyVisualEasing.EaseIn(k, 2.2f);
        var hold = EnemyVisualEasing.SmoothStepRange(t, poseIn, Mathf.Min(W, poseIn + 0.04f));
        var dx = 0.050f * k + 0.004f * EnemyMotionSampleUtil.Tremor(t) * hold;
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(dx, 0f);
        pose.ScaleX = 1f + 0.12f * k;
        pose.ScaleY = 1f - 0.20f * k;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(0.10f * k);
        pose.SkewX = 0.05f * k;
        pose.UseAttackSprite = false;
        return pose;
      }

      var u = t - W;
      if (u <= ActiveToImpact)
      {
        var uu = ActiveToImpact > 0f ? u / ActiveToImpact : 1f;
        var p = Mathf.Pow(uu, 1.35f);
        var e = EnemyVisualEasing.EaseOut(Mathf.Clamp01(uu / 0.35f), 2f);
        var st = Mathf.Pow(Mathf.Sin(Mathf.PI * uu), 0.5f);
        var sc = Mathf.Lerp(1f, MaxScale, p);
        var sx0 = Mathf.Lerp(1.12f, 1f, e);
        var sy0 = Mathf.Lerp(0.80f, 1f, e);
        var dx = Mathf.Lerp(0.050f, -0.280f, p);
        var dy = 0.025f * p;
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(dx, dy);
        pose.ScaleX = sc * sx0 * (1f + 0.16f * st);
        pose.ScaleY = sc * sy0 * (1f - 0.08f * st);
        var rotK = EnemyVisualEasing.EaseOut(uu, 3f);
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(Mathf.Lerp(0.10f, -0.16f, rotK));
        pose.SkewX = Mathf.Lerp(0.05f, -0.12f, rotK);
        pose.UseAttackSprite = true;
        return pose;
      }

      u -= ActiveToImpact;
      if (u <= ImpactHold)
      {
        var k = u;
        var spring = Mathf.Exp(-14f * k) * Mathf.Sin(28f * k);
        var dx = -0.280f - 0.050f * spring;
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(dx, 0.025f);
        var sc = Mathf.Lerp(MaxScale, 1.18f, EnemyVisualEasing.SmoothStepRange(k, 0f, 0.30f));
        var sq = 0.12f * Mathf.Exp(-14f * k);
        pose.ScaleX = sc * (1f - sq);
        pose.ScaleY = sc * (1f + 0.5f * sq);
        var rotBase = Mathf.Lerp(-0.16f, -0.04f, EnemyVisualEasing.SmoothStepRange(k, 0.06f, 0.30f));
        rotBase -= 0.10f * Mathf.Exp(-12f * k) * Mathf.Sin(22f * k);
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(rotBase);
        pose.SkewX = -0.12f * (1f - EnemyVisualEasing.SmoothStepRange(k, 0f, 0.20f));
        pose.UseAttackSprite = true;
        if (!strikeAlreadyFired)
        {
          pose.StrikeThisFrame = true;
          pose.ShakeThisFrame = true;
        }
        else if (k >= 0.02f && k < 0.25f)
          pose.ShakeThisFrame = true;
        return pose;
      }

      u -= ImpactHold;
      if (u <= Slide)
      {
        var m = EnemyVisualEasing.SmoothStep(Slide > 0f ? u / Slide : 1f);
        var dx = Mathf.Lerp(-0.280f, 0f, m);
        var dy = Mathf.Lerp(0.025f, 0f, m);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(dx, dy);
        var sc = Mathf.Lerp(1.18f, 1f, m);
        pose.ScaleX = pose.ScaleY = sc;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(-0.04f * (1f - m) - 0.05f * Mathf.Sin(Mathf.PI * m));
        pose.SkewX = 0f;
        pose.UseAttackSprite = true;
        return pose;
      }

      u -= Slide;
      if (u <= Settle)
      {
        EnemyMotionSampleUtil.ApplySettleSquash(ref pose, Settle > 0f ? u / Settle : 1f, 0.08f);
        return pose;
      }

      return EnemySpritePairPose.Idle;
    }
  }
}
