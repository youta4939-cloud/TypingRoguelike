using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>6 時から時計回り。Track → Glow → Chip → Fill。</summary>
    public sealed class RingGauge : MonoBehaviour
    {
        Image _track;
        Image _glow;
        Image _chip;
        Image _fill;
        Color _color = Color.white;
        public float Flash;

        public float Amount
        {
            get => _fill != null ? _fill.fillAmount : 0f;
            set
            {
                float amount = Mathf.Clamp01(value);
                if (_fill != null)
                    _fill.fillAmount = amount;
                if (_glow != null)
                    _glow.fillAmount = amount;
            }
        }

        public void Build(RectTransform parent, Vector2 centre, Sprite track, Sprite fill, Sprite glow, Color color)
        {
            _color = color;
            _track = Ring(parent, "Track", centre, track, Color.white, false);
            _glow = Ring(parent, "Glow", centre, glow, color, true);
            _chip = Ring(parent, "Chip", centre, fill, GemPalette.Hex("FFECE2"), true);
            _fill = Ring(parent, "Fill", centre, fill, color, true);
            Amount = 0f;
            SetChip(0f, false);
            ApplyFlash();
        }

        public void SetChip(float amount, bool visible)
        {
            if (_chip == null)
                return;
            _chip.fillAmount = Mathf.Clamp01(amount);
            _chip.enabled = visible && amount > 0.001f;
        }

        public void Tick()
        {
            ApplyFlash();
        }

        void ApplyFlash()
        {
            if (_glow == null)
                return;
            var color = _color;
            color.a = 0.55f + 0.8f * Mathf.Clamp01(Flash);
            _glow.color = color;
        }

        static Image Ring(RectTransform parent, string name, Vector2 centre, Sprite sprite, Color color, bool filled)
        {
            var size = sprite != null ? new Vector2(sprite.rect.width, sprite.rect.height) * 0.5f : new Vector2(65f, 65f);
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centre;
            rt.sizeDelta = size;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            if (filled && sprite != null)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Bottom;
                image.fillClockwise = true;
                image.fillAmount = 0f;
            }
            return image;
        }
    }
}
