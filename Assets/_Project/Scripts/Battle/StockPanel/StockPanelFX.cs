using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>被弾フラッシュ、低 HP の呼吸、狂気グロー。</summary>
    public sealed class StockPanelFX : MonoBehaviour
    {
        Image _hit;
        Image _low;
        Image _madness;
        Image _vignette;
        float _hitAge = 10f;
        float _madAge = 10f;

        public float HitFlash => _hitAge < 0.5f ? Mathf.Pow(1f - _hitAge / 0.5f, 1.5f) : 0f;
        public float MadnessFlash => _madAge < 0.9f ? 0.8f * Mathf.Sin(Mathf.PI * _madAge / 0.9f) : 0f;

        public void Bind(Image hit, Image low, Image madness, Image vignette)
        {
            _hit = hit;
            _low = low;
            _madness = madness;
            _vignette = vignette;
            Set(_hit, 0f);
            Set(_low, 0f);
            Set(_madness, 0f);
            Set(_vignette, 0f);
        }

        public void PlayHit()
        {
            _hitAge = 0f;
        }

        public void PlayMadness()
        {
            _madAge = 0f;
        }

        public void Tick(float time, bool lowHp)
        {
            _hitAge += Time.unscaledDeltaTime;
            _madAge += Time.unscaledDeltaTime;
            float hit = HitFlash;
            Set(_hit, hit);
            float breath = 0.22f + 0.16f * Mathf.Sin(2f * Mathf.PI * time / 1.2f);
            float low = lowHp ? breath : 0f;
            if (lowHp && hit > 0f)
                low = Mathf.Max(low, hit);
            Set(_low, low);
            Set(_madness, MadnessFlash);
            float vignette = _hitAge < 0.35f ? 0.45f * (1f - _hitAge / 0.35f) : 0f;
            Set(_vignette, vignette);
        }

        static void Set(Image image, float alpha)
        {
            if (image == null)
                return;
            var color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
            image.enabled = color.a > 0.004f;
        }
    }
}
