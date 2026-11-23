using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
  /// <summary>
  /// motion_v2 共通。座標・回転は EnemyLungeMotion と同じ変換
  /// （サンプル dy 正＝画面下 → Unity OffsetEh.y は符号反転、rot は sampleRad→度で同符号）。
  /// </summary>
  public static class EnemyMotionSampleUtil
  {
    public static float ToDeg(float sampleRad) => sampleRad * Mathf.Rad2Deg;

    public static Vector2 ToOffsetEh(float sampleDx, float sampleDy) =>
      new(sampleDx, -sampleDy);

    public static float Tremor(float tau) =>
      0.7f * Mathf.Sin(2f * Mathf.PI * tau / 0.07f) +
      0.3f * Mathf.Sin(2f * Mathf.PI * tau / 0.113f + 1.1f);

    public static void ApplySettleSquash(ref EnemySpritePairPose pose, float k, float amp)
    {
      var sq = amp * Mathf.Exp(-9f * k) * Mathf.Cos(22f * k);
      pose.OffsetEh = Vector2.zero;
      pose.ScaleX = 1f + 0.5f * sq;
      pose.ScaleY = 1f - sq;
      pose.RotationDeg = 0f;
      pose.SkewX = 0f;
      pose.UseAttackSprite = false;
    }
  }
}
