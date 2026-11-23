using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>開発ビルドだけの Exエリア操作。</summary>
    public sealed class ExAreaDebugPanel
    {
        readonly GameObject _root;
        readonly TextMeshProUGUI _status;
        bool _open;

        ExAreaDebugPanel(GameObject root, TextMeshProUGUI status)
        {
            _root = root;
            _status = status;
            _open = false;
            _root.SetActive(false);
        }

        public bool IsOpen => _open;

        public static ExAreaDebugPanel Create(
            RectTransform parent,
            Action<string> onColor,
            Action<ExKind> onStatus,
            Action onFocusFive,
            Action onConsumeFocus,
            Action onClear,
            Action onToggleSpecials,
            Action onSpecialNow,
            Action<string> onCommand = null)
        {
            var go = new GameObject("ExDebug", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(660f, -40f);
            rt.sizeDelta = new Vector2(420f, 900f);
            var bg = go.AddComponent<Image>();
            bg.sprite = BattleUISprites.Solid;
            bg.color = new Color(0.05f, 0.04f, 0.06f, 0.94f);

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(go.transform, false);
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = statusRt.anchorMax = new Vector2(0f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(12f, -12f);
            statusRt.sizeDelta = new Vector2(396f, 150f);
            var status = statusGo.AddComponent<TextMeshProUGUI>();
            status.font = BattleHudKit.Gothic;
            status.fontSize = 14f;
            status.color = BattleUITheme.TextPrimary;
            status.alignment = TextAlignmentOptions.TopLeft;
            status.raycastTarget = false;

            var buttons = new (string label, Action action)[]
            {
                ("色ストック 赤", () => onColor?.Invoke("red")),
                ("色ストック 青", () => onColor?.Invoke("blue")),
                ("色ストック 黄", () => onColor?.Invoke("yellow")),
                ("色ストック 緑", () => onColor?.Invoke("green")),
                ("色ストック 紫", () => onColor?.Invoke("purple")),
                ("毒+1", () => onStatus?.Invoke(ExKind.Poison)),
                ("麻痺+1", () => onStatus?.Invoke(ExKind.Paralysis)),
                ("伝播+1", () => onStatus?.Invoke(ExKind.Frenzy)),
                ("集中+1", () => onStatus?.Invoke(ExKind.Focus)),
                ("集中+5", () => onFocusFive?.Invoke()),
                ("集中をすべて消費", () => onConsumeFocus?.Invoke()),
                ("Exエリアを空にする", () => onClear?.Invoke()),
                ("敵の特殊攻撃 オン/オフ", () => onToggleSpecials?.Invoke()),
                ("今すぐ特殊攻撃", () => onSpecialNow?.Invoke()),
                ("赤補充+1", () => onCommand?.Invoke("red")),
                ("抜刀+1", () => onCommand?.Invoke("battou")),
                ("抜刀+5", () => onCommand?.Invoke("battou5")),
                ("一閃・極+1", () => onCommand?.Invoke("issen")),
                ("一閃・極+5", () => onCommand?.Invoke("issen5")),
                ("百鬼夜行+1", () => onCommand?.Invoke("hyakki")),
                ("怨血+5", () => onCommand?.Invoke("blood5")),
                ("怨血を0に", () => onCommand?.Invoke("blood0")),
                ("抜刀の使い方：全部／1つ", () => onCommand?.Invoke("consume"))
            };

            for (var i = 0; i < buttons.Length; i++)
            {
                var entry = buttons[i];
                var buttonGo = new GameObject(entry.label, typeof(RectTransform));
                buttonGo.transform.SetParent(go.transform, false);
                var brt = buttonGo.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(12f, -170f - i * 30f);
                brt.sizeDelta = new Vector2(396f, 26f);
                var image = buttonGo.AddComponent<Image>();
                image.sprite = BattleUISprites.Solid;
                image.color = new Color(0.22f, 0.16f, 0.18f, 1f);
                var button = buttonGo.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => entry.action?.Invoke());
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(buttonGo.transform, false);
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.font = BattleHudKit.Gothic;
                label.fontSize = 16f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = BattleUITheme.TextPrimary;
                label.text = entry.label;
                label.raycastTarget = false;
            }

            return new ExAreaDebugPanel(go, status);
        }

        public void Toggle()
        {
            _open = !_open;
            if (_root != null)
            {
                _root.SetActive(_open);
                if (_open)
                    _root.transform.SetAsLastSibling();
            }
        }

        public void SetStatus(string text)
        {
            if (_status != null)
                _status.text = text ?? "";
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
