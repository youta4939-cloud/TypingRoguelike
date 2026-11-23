using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>攻撃リングの残量と色を、外側のにじみに写す。</summary>
    [RequireComponent(typeof(Image))]
    public sealed class RingBloomFollow : MonoBehaviour
    {
        public Image Bloom;
        public float AlphaScale = 0.38f;

        Image _ring;

        void Awake() => _ring = GetComponent<Image>();

        void LateUpdate()
        {
            if (_ring == null || Bloom == null)
                return;
            Bloom.fillAmount = _ring.fillAmount;
            Bloom.fillClockwise = _ring.fillClockwise;
            var c = _ring.color;
            Bloom.color = new Color(c.r, c.g, c.b, c.a * AlphaScale);
        }
    }
}
