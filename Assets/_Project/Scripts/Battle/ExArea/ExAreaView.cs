using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>右側のExtra Area。見出しは常時、状態は存在する項目だけ表示する。</summary>
    public sealed class ExAreaView
    {
        const float OriginX = 1568f;
        const float OriginY = 240f;
        const float Width = 268f;
        const float ContentY = 48f;
        const float SlotH = 86f;
        const float Gap = 6f;

        readonly RectTransform _root;
        readonly Image[] _frames;
        readonly TextMeshProUGUI[] _labels;
        readonly TextMeshProUGUI _blood;
        readonly int _capacity;

        ExAreaView(RectTransform root, Image[] frames, TextMeshProUGUI[] labels, TextMeshProUGUI blood)
        {
            _root = root;
            _frames = frames;
            _labels = labels;
            _blood = blood;
            _capacity = labels.Length;
        }

        public static ExAreaView Create(RectTransform parent, int capacity)
        {
            if (capacity < 1)
                capacity = 1;
            var rootGo = new GameObject("ExArea", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.GetComponent<RectTransform>();
            TopLeft(root, OriginX, OriginY, Width, ContentY + capacity * (SlotH + Gap));

            var ivory = new Color(0.95f, 0.91f, 0.81f, 1f);
            var title = Text(root, "Title", "Extra Area", 0f, 0f, 170f, 32f, 26f, BattleHudKit.Condensed, ivory);
            title.fontStyle = FontStyles.Italic;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var underline = new GameObject("TitleUnderline", typeof(RectTransform));
            underline.transform.SetParent(root, false);
            TopLeft(underline.GetComponent<RectTransform>(), 0f, 36f, Width, 1.5f);
            var line = underline.AddComponent<Image>();
            line.sprite = BattleUISprites.Solid;
            line.color = ivory;
            line.raycastTarget = false;
            var blood = Text(root, "Blood", "", 176f, 10f, Width - 176f, 22f, 14f, BattleHudKit.Gothic, new Color(0.85f, 0.25f, 0.3f, 1f));
            blood.alignment = TextAlignmentOptions.TopRight;

            var frames = new Image[capacity];
            var labels = new TextMeshProUGUI[capacity];
            for (var i = 0; i < capacity; i++)
            {
                var y = ContentY + i * (SlotH + Gap);
                var frame = new GameObject("Slot" + i, typeof(RectTransform));
                frame.transform.SetParent(root, false);
                var rt = frame.GetComponent<RectTransform>();
                TopLeft(rt, 0f, y, Width, SlotH);
                var image = frame.AddComponent<Image>();
                image.sprite = BattleUISprites.Solid;
                image.color = BattleUITheme.BgPanel;
                image.raycastTarget = false;
                frames[i] = image;
                labels[i] = Text(rt, "Label", "", 8f, 6f, Width - 16f, SlotH - 12f, 16f, BattleHudKit.Gothic, BattleUITheme.TextMuted);
                frame.SetActive(false);
            }

            return new ExAreaView(root, frames, labels, blood);
        }

        public void SetBlood(int blood)
        {
            if (_blood == null)
                return;
            _blood.text = blood > 0 ? "怨血 " + blood : "";
        }

        public void Refresh(ExAreaState state, ExAreaConfig config)
        {
            if (_labels == null)
                return;
            var filled = state?.Count ?? 0;
            var visible = 0;
            for (var i = 0; i < filled && visible < _capacity; i++)
            {
                var slot = state.Slots[i];
                if (slot == null || slot.Kind == ExKind.None || slot.Count <= 0)
                    continue;
                _frames[visible].gameObject.SetActive(true);
                _labels[visible].text = Describe(slot, config);
                _labels[visible].color = slot.InputLocked
                    ? BattleUITheme.TextMuted
                    : BattleUITheme.TextPrimary;
                _frames[visible].color = FrameColor(slot.Kind);
                visible++;
            }
            for (var i = visible; i < _capacity; i++)
            {
                _frames[i].gameObject.SetActive(false);
                _labels[i].text = "";
            }
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        static string Describe(ExSlot slot, ExAreaConfig config)
        {
            if (slot == null || slot.Kind == ExKind.None)
                return "";
            var name = config != null ? config.NameOf(slot.Kind) : slot.Kind.ToString();
            if (slot.Kind == ExKind.ColorStock)
                name = config != null ? config.ColorName(slot.ColorId) : slot.ColorId;
            var text = new StringBuilder();
            if (slot.Kind == ExKind.ColorStock)
                text.Append(name);
            else
                text.Append(name).Append("×").Append(slot.Count);
            if (slot.Kind == ExKind.Frenzy)
                text.Append("  ").Append(Mathf.Max(0f, slot.Timer).ToString("0.0")).Append("秒");
            if (slot.Kind == ExKind.Focus)
            {
                var swordsman = SwordsmanSkillConfig.Load();
                var decay = swordsman?.options != null ? swordsman.options.focusCarryDecay : 0;
                if (decay > 0)
                    text.Append('\n').Append("戦闘終了後、集中-").Append(decay);
            }
            if (slot.HasWord)
            {
                if (!string.IsNullOrEmpty(slot.DisplayText))
                    text.Append('\n').Append(slot.DisplayText);
                text.Append('\n').Append(Ruby(slot));
            }
            return text.ToString();
        }

        static string Ruby(ExSlot slot)
        {
            var source = slot.Text ?? "";
            if (slot.InputLocked)
                return "<color=#8A847C>" + source + "</color>";
            var typedCount = Mathf.Clamp(slot.Progress, 0, source.Length);
            var typed = source.Substring(0, typedCount);
            var rest = source.Substring(typedCount);
            return "<color=#FFD660>" + typed + "</color><color=#FFFFFF>" + rest + "</color>";
        }

        static Color FrameColor(ExKind kind)
        {
            switch (kind)
            {
                case ExKind.Poison: return new Color(0.18f, 0.28f, 0.12f, 0.95f);
                case ExKind.Paralysis: return new Color(0.16f, 0.18f, 0.32f, 0.95f);
                case ExKind.Frenzy: return new Color(0.32f, 0.1f, 0.14f, 0.95f);
                case ExKind.Focus: return new Color(0.28f, 0.22f, 0.08f, 0.95f);
                default: return new Color(0.2f, 0.1f, 0.12f, 0.95f);
            }
        }

        static TextMeshProUGUI Text(Transform parent, string name, string text, float x, float y, float w, float h, float size, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TopLeft(go.GetComponent<RectTransform>(), x, y, w, h);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font != null ? font : BattleHudKit.Gothic;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.text = text;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
