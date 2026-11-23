using TMPro;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    /// <summary>ラベル・数字・/max。Auto Size は使わない。</summary>
    public sealed class StatReadout : MonoBehaviour
    {
        public const float LabelSize = 14f;
        public const float NumberSize = 34f;
        public const float MaxSize = 16f;

        TMP_Text _label;
        TMP_Text _number;
        TMP_Text _max;
        StockLayout.PlateAnchor _plate;
        Color _labelColor;
        Color _numberColor;
        Color _lowColor;
        Color _maxColor;
        Color _punchColor;
        string _labelText = "";
        string _numberText = "0";
        string _maxText = "/0";
        bool _low;
        float _fit = 1f;
        float _punch;
        float _punchAmp;
        int _punchMode;
        float _punchAge;

        public void Build(RectTransform parent, TMP_FontAsset sans, TMP_FontAsset serif,
            StockLayout.PlateAnchor plate, string label, Color labelColor, Color numberColor, Color lowColor, Color maxColor, Color punchColor)
        {
            _plate = plate;
            _labelText = label;
            _labelColor = labelColor;
            _numberColor = numberColor;
            _lowColor = lowColor;
            _maxColor = maxColor;
            _punchColor = punchColor;
            _label = Text(parent, "Label", sans, LabelSize, TextAlignmentOptions.MidlineLeft);
            _number = Text(parent, "Number", serif, NumberSize, TextAlignmentOptions.BottomRight);
            _max = Text(parent, "Max", serif, MaxSize, TextAlignmentOptions.BottomRight);
            _number.fontWeight = FontWeight.Black;
            _max.fontWeight = FontWeight.Bold;
            Style(_label, 0.16f);
            Style(_number, 0.22f);
            Style(_max, 0.16f);
        }

        public float Measure(string number, string max)
        {
            return 6f + Width(_label, _labelText, LabelSize)
                + 6f + Width(_number, number, NumberSize)
                + 3f + Width(_max, max, MaxSize)
                + 6f;
        }

        public void Apply(string number, string max, float fit, bool low)
        {
            _numberText = number;
            _maxText = max;
            _fit = Mathf.Clamp(fit, 0.4f, 1f);
            _low = low;
            Place();
        }

        public void PlayPunch(float amplitude, bool triangle)
        {
            _punchAmp = amplitude;
            _punchMode = triangle ? 2 : 1;
            _punchAge = 0f;
            _punch = 1f;
        }

        public void Tick(float dt)
        {
            if (_punchMode == 0)
                return;
            _punchAge += dt;
            if (_punchMode == 1)
            {
                _punch = Mathf.Clamp01(1f - _punchAge / 0.3f);
                if (_punchAge >= 0.3f)
                    _punchMode = 0;
            }
            else
            {
                if (_punchAge < 0.25f)
                    _punch = _punchAge / 0.25f;
                else if (_punchAge < 0.5f)
                    _punch = 1f - (_punchAge - 0.25f) / 0.25f;
                else
                {
                    _punch = 0f;
                    _punchMode = 0;
                }
            }
            Place();
        }

        void Place()
        {
            float labelSize = LabelSize * _fit;
            float numberSize = NumberSize * _fit;
            float maxSize = MaxSize * _fit;
            _label.fontSize = labelSize;
            _number.fontSize = numberSize;
            _max.fontSize = maxSize;
            _label.text = _labelText;
            _number.text = _numberText;
            _max.text = _maxText;

            float maxWidth = _max.GetPreferredValues(_maxText).x;
            float numberWidth = _number.GetPreferredValues(_numberText).x;
            float labelMid = _plate.CenterY - 1f;
            float maxBase = _plate.CenterY - 11f;
            float numberBase = _plate.CenterY - 12f;
            float maxRight = _plate.Right - 6f;

            Set(_label.rectTransform, new Vector2(0f, 0.5f), new Vector2(_plate.X + 6f, labelMid),
                new Vector2(Mathf.Max(8f, _label.GetPreferredValues(_labelText).x + 2f), labelSize + 4f));
            Set(_max.rectTransform, new Vector2(1f, 0f), new Vector2(maxRight, maxBase),
                new Vector2(Mathf.Max(8f, maxWidth), maxSize + 2f));
            Set(_number.rectTransform, new Vector2(1f, 0f), new Vector2(maxRight - maxWidth - 3f, numberBase),
                new Vector2(Mathf.Max(8f, numberWidth), numberSize + 2f));

            var baseColor = _low ? _lowColor : _numberColor;
            _label.color = _labelColor;
            _max.color = _maxColor;
            _number.color = Color.Lerp(baseColor, _punchColor, _punch);
            float scale = 1f + _punchAmp * _punch;
            _number.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        static void Set(RectTransform rt, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static float Width(TMP_Text text, string value, float size)
        {
            if (text == null)
                return size;
            text.fontSize = size;
            return text.GetPreferredValues(value ?? string.Empty).x;
        }

        static TMP_Text Text(RectTransform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = false;
            return text;
        }

        static void Style(TMP_Text text, float outline)
        {
            if (text.font == null)
                return;
            text.fontMaterial = new Material(text.fontMaterial);
            var material = text.fontMaterial;
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor("_OutlineColor", GemPalette.Hex("0A0508"));
            material.SetFloat("_OutlineWidth", outline);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.86f));
            material.SetFloat("_UnderlayOffsetX", 0.05f);
            material.SetFloat("_UnderlayOffsetY", -0.1f);
            material.SetFloat("_UnderlayDilate", 0.08f);
            material.SetFloat("_UnderlaySoftness", 0.2f);
        }
    }
}
