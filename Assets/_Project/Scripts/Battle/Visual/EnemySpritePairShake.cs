using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
  public sealed class EnemySpritePairShake : MonoBehaviour
  {
    float _shakeAmp;
    float _shakeTime;
    Vector2 _basePos;

    RectTransform _rt;
    RectTransform _arena;

    public void Configure(RectTransform body, RectTransform arena)
    {
      _rt = body;
      _arena = arena;
      if (_rt != null)
        _basePos = _rt.anchoredPosition;
    }

    public void Trigger(float amplitudePx)
    {
      _shakeAmp = amplitudePx;
      _shakeTime = 0f;
    }

    void LateUpdate()
    {
      if (_shakeAmp <= 0f || _rt == null)
        return;
      _shakeTime += Time.unscaledDeltaTime;
      var amp = _shakeAmp * Mathf.Exp(-11f * _shakeTime);
      if (amp < 0.5f)
      {
        _shakeAmp = 0f;
        _rt.anchoredPosition = _basePos;
        if (_arena != null)
          _arena.anchoredPosition = Vector2.zero;
        return;
      }

      var phi = _shakeTime * 30f;
      var dx = amp * Mathf.Sin(phi * 2.3f);
      var dy = amp * 0.7f * Mathf.Cos(phi * 3.1f + 1.3f);
      _rt.anchoredPosition = _basePos + new Vector2(dx, dy);
      if (_arena != null)
        _arena.anchoredPosition = new Vector2(dx * 0.35f, dy * 0.35f);
    }
  }
}