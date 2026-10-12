using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>UI をゆっくり回転させる（ポーズ中も回る装飾用）。</summary>
    public sealed class UISpin : MonoBehaviour
    {
        public float degreesPerSecond = 6f;

        void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }

    /// <summary>Graphic のアルファを呼吸させる。</summary>
    public sealed class UIPulse : MonoBehaviour
    {
        public float minAlpha = 0.15f;
        public float maxAlpha = 0.4f;
        public float speed = 1.6f;

        Graphic _graphic;

        void Awake() => _graphic = GetComponent<Graphic>();

        void Update()
        {
            if (_graphic == null)
                return;
            var c = _graphic.color;
            var t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            _graphic.color = c;
        }
    }
}
