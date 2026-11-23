using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
  /// <summary>SwingInPlace — enemy_visual_preset_spec_v2 §4.4 / §4.6。</summary>
  public static class EnemySwingMotion
  {
    public const float DefaultWindup = 0.55f;
    public const float PoseInMax = 0.42f;
    public const float ActiveToImpact = 0.12f;
    public const float HitStopMedium = 0.08f;
    public const float Follow = 0.25f;
    public const float Recover = 0.45f;
    public const float Settle = 0.40f;
    public const float MaxScale = 1.18f;

    // TODO: 残像3枚（ヒットストップ中は止める）/ 土煙（§4.4）

    public static float HitStopSeconds(EnemyEffectIntensity intensity) => intensity switch
    {
      EnemyEffectIntensity.Small => 0.05f,
      EnemyEffectIntensity.Large => 0.11f,
      _ => HitStopMedium
    };

    public static float TotalAttackDuration(float windupSeconds, float hitStop = HitStopMedium) =>
      Mathf.Max(0.2f, windupSeconds) + ActiveToImpact + hitStop + Follow + Recover + Settle;

    public static EnemySpritePairPose Sample(
      float t,
      float windupSeconds,
      bool strikeAlreadyFired,
      float hitStopSeconds = HitStopMedium)
    {
      var W = Mathf.Max(0.2f, windupSeconds);
      t = Mathf.Max(0f, t);
      var pose = EnemySpritePairPose.Idle;
      hitStopSeconds = Mathf.Max(0.02f, hitStopSeconds);

      var poseIn = W >= PoseInMax + 0.06f
        ? PoseInMax
        : Mathf.Max(0.14f, W - 0.06f);
      poseIn = Mathf.Min(poseIn, W);
      var switchAt = Mathf.Min(0.10f, poseIn * 0.5f);

      if (t < W)
      {
        var k = poseIn > 0f ? Mathf.Clamp01(t / poseIn) : 1f;
        k = EnemyVisualEasing.EaseOut(k, 2.5f);
        var hold = EnemyVisualEasing.SmoothStepRange(t, poseIn, Mathf.Min(W, poseIn + 0.04f));
        var dx = 0.035f * k + 0.003f * EnemyMotionSampleUtil.Tremor(t) * hold;
        var sc = Mathf.Lerp(1f, 1.04f, k);
        var pop = t >= switchAt
          ? 1f + 0.05f * Mathf.Exp(-Mathf.Pow((t - switchAt) / 0.04f, 2f))
          : 1f;
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(dx, 0f);
        pose.ScaleX = sc * Mathf.Lerp(1f, 0.95f, k);
        pose.ScaleY = sc * Mathf.Lerp(1f, 1.07f, k) * pop;
        var rot = 0.22f * k + 0.012f * Mathf.Sin(2f * Mathf.PI * t / 0.09f) * hold;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(rot);
        pose.SkewX = 0.08f * k;
        pose.UseAttackSprite = t >= switchAt;
        return pose;
      }

      var u = t - W;
      if (u <= ActiveToImpact)
      {
        var uu = ActiveToImpact > 0f ? u / ActiveToImpact : 1f;
        var e = EnemyVisualEasing.EaseIn(uu, 2.6f);
        var st = Mathf.Sin(Mathf.PI * uu);
        var sc = Mathf.Lerp(1.04f, MaxScale, e);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(
          Mathf.Lerp(0.035f, -0.060f, e),
          0.020f * e);
        pose.ScaleX = sc * Mathf.Lerp(0.95f, 1.06f, e) * (1f + 0.10f * st);
        pose.ScaleY = sc * Mathf.Lerp(1.07f, 0.94f, e);
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(Mathf.Lerp(0.22f, -0.30f, e));
        pose.SkewX = Mathf.Lerp(0.08f, -0.16f, e);
        pose.UseAttackSprite = true;
        return pose;
      }

      u -= ActiveToImpact;
      if (u <= hitStopSeconds)
      {
        // 着弾ポーズで完全停止。ダメージは StrikeThisFrame を先頭フレームで出す
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(-0.060f, 0.020f);
        pose.ScaleX = MaxScale * 1.06f;
        pose.ScaleY = MaxScale * 0.94f;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(-0.30f);
        pose.SkewX = -0.16f;
        pose.UseAttackSprite = true;
        pose.FreezeStrikeFx = true;
        if (!strikeAlreadyFired)
        {
          pose.StrikeThisFrame = true;
          pose.ShakeThisFrame = true;
        }
        else
          pose.ShakeThisFrame = true; // ヒットストップ中も画面揺れは継続
        return pose;
      }

      u -= hitStopSeconds;
      if (u <= Follow)
      {
        var k = u;
        var e = EnemyVisualEasing.EaseOut(Follow > 0f ? k / Follow : 1f, 3f);
        var sc = Mathf.Lerp(MaxScale, 1.12f, e);
        var sq = 0.06f * Mathf.Exp(-12f * k);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(
          Mathf.Lerp(-0.060f, -0.075f, e),
          Mathf.Lerp(0.020f, 0.025f, e));
        pose.ScaleX = sc * Mathf.Lerp(1.06f, 1f, e) * (1f + sq);
        pose.ScaleY = sc * Mathf.Lerp(0.94f, 1f, e) * (1f - sq);
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(Mathf.Lerp(-0.30f, -0.35f, e));
        pose.SkewX = Mathf.Lerp(-0.16f, -0.06f, e);
        pose.UseAttackSprite = true;
        return pose;
      }

      u -= Follow;
      if (u <= Recover)
      {
        var m = EnemyVisualEasing.SmoothStep(Recover > 0f ? u / Recover : 1f);
        pose.OffsetEh = EnemyMotionSampleUtil.ToOffsetEh(
          Mathf.Lerp(-0.075f, 0f, m),
          Mathf.Lerp(0.025f, 0f, m));
        var sc = Mathf.Lerp(1.12f, 1f, m);
        pose.ScaleX = pose.ScaleY = sc;
        pose.RotationDeg = EnemyMotionSampleUtil.ToDeg(Mathf.Lerp(-0.35f, 0f, m));
        pose.SkewX = Mathf.Lerp(-0.06f, 0f, m);
        pose.UseAttackSprite = true;
        return pose;
      }

      u -= Recover;
      if (u <= Settle)
      {
        EnemyMotionSampleUtil.ApplySettleSquash(ref pose, Settle > 0f ? u / Settle : 1f, 0.07f);
        return pose;
      }

      return EnemySpritePairPose.Idle;
    }
  }
}
