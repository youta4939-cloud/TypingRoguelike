using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>開発ビルドだけの剣士スキル操作。F4 で Ex パネルと一緒に開く。</summary>
    public sealed class SwordsmanDebugPanel
    {
        readonly GameObject _root;
        readonly TextMeshProUGUI _status;
        readonly SwordsmanSkillEntry[] _skills;
        TextMeshProUGUI _slotLabel;
        TextMeshProUGUI _levelLabel;
        int _slot;
        int _level = 1;
        bool _open;

        SwordsmanDebugPanel(GameObject root, TextMeshProUGUI status, SwordsmanSkillEntry[] skills)
        {
            _root = root;
            _status = status;
            _skills = skills ?? Array.Empty<SwordsmanSkillEntry>();
            _open = false;
            _root.SetActive(false);
        }

        public bool IsOpen => _open;

        public static SwordsmanDebugPanel Create(
            RectTransform parent,
            SwordsmanSkillConfig config,
            Action<int, int, int> onEquip,
            Action onReset,
            Action<string> onCommand)
        {
            var skills = config != null && config.skills != null ? config.skills : Array.Empty<SwordsmanSkillEntry>();
            var go = new GameObject("SwordsmanDebug", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(220f, -40f);
            rt.sizeDelta = new Vector2(420f, 860f);
            var bg = go.AddComponent<Image>();
            bg.sprite = BattleUISprites.Solid;
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.94f);

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(go.transform, false);
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = statusRt.anchorMax = new Vector2(0f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(12f, -8f);
            statusRt.sizeDelta = new Vector2(396f, 230f);
            var status = statusGo.AddComponent<TextMeshProUGUI>();
            status.font = BattleHudKit.Gothic;
            status.fontSize = 14f;
            status.color = BattleUITheme.TextPrimary;
            status.alignment = TextAlignmentOptions.TopLeft;
            status.raycastTarget = false;

            var panel = new SwordsmanDebugPanel(go, status, skills);
            var y = -242f;
            panel._slotLabel = panel.AddButton(go.transform, "枠：最初のスキル", new Vector2(12f, y), () =>
            {
                panel._slot = (panel._slot + 1) % 3;
                panel._slotLabel.text = panel._slot == 0 ? "枠：最初のスキル" : "枠：" + panel._slot;
            }, 190f);
            panel._levelLabel = panel.AddButton(go.transform, "Lv 1", new Vector2(210f, y), () =>
            {
                panel._level = panel._level >= 3 ? 1 : panel._level + 1;
                panel._levelLabel.text = "Lv " + panel._level;
            }, 190f);
            y -= 32f;

            for (var i = 0; i < skills.Length; i++)
            {
                var entry = skills[i];
                if (entry == null)
                    continue;
                var column = i % 2;
                if (column == 0 && i > 0)
                    y -= 30f;
                var captured = entry;
                panel.AddButton(
                    go.transform,
                    entry.nameJa,
                    new Vector2(12f + column * 200f, y),
                    () => onEquip?.Invoke(panel._slot, captured.skillId, panel._level),
                    190f);
            }

            y -= 36f;
            panel.AddButton(go.transform, "元に戻す", new Vector2(12f, y), () => onReset?.Invoke(), 396f);
            y -= 34f;
            var input = panel.AddInput(go.transform, new Vector2(12f, y));
            panel.AddButton(go.transform, "集中セット", new Vector2(150f, y), () =>
            {
                var text = input.text;
                onCommand?.Invoke("set:" + text);
            }, 120f);
            panel.AddButton(go.transform, "0", new Vector2(278f, y), () => onCommand?.Invoke("set:0"), 40f);
            panel.AddButton(go.transform, "10", new Vector2(322f, y), () => onCommand?.Invoke("set:10"), 40f);
            panel.AddButton(go.transform, "20", new Vector2(366f, y), () => onCommand?.Invoke("set:20"), 40f);
            y -= 32f;
            panel.AddButton(go.transform, "極状態にする", new Vector2(12f, y), () => onCommand?.Invoke("kiwami"), 190f);
            panel.AddButton(go.transform, "極状態を解除", new Vector2(210f, y), () => onCommand?.Invoke("clearkiwami"), 190f);
            y -= 32f;
            panel.AddButton(go.transform, "集中獲得 +10", new Vector2(12f, y), () => onCommand?.Invoke("gain"), 190f);
            panel.AddButton(go.transform, "1回スキルを戻す", new Vector2(210f, y), () => onCommand?.Invoke("once"), 190f);
            y -= 32f;
            panel.AddButton(go.transform, "持ち越し 0", new Vector2(12f, y), () => onCommand?.Invoke("carry:0"), 120f);
            panel.AddButton(go.transform, "持ち越し 10", new Vector2(140f, y), () => onCommand?.Invoke("carry:10"), 120f);
            panel.AddButton(go.transform, "持ち越し 15", new Vector2(268f, y), () => onCommand?.Invoke("carry:15"), 120f);
            y -= 32f;
            panel.AddButton(go.transform, "JSONを読み直す", new Vector2(12f, y), () => onCommand?.Invoke("reload"), 396f);
            return panel;
        }

        public void Toggle()
        {
            _open = !_open;
            _root.SetActive(_open);
        }

        public void SetStatus(string text)
        {
            if (_status == null)
                return;
            _status.text = text ?? "";
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }

        TextMeshProUGUI AddButton(Transform parent, string label, Vector2 position, Action action, float width)
        {
            var buttonGo = new GameObject(label, typeof(RectTransform));
            buttonGo.transform.SetParent(parent, false);
            var brt = buttonGo.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = position;
            brt.sizeDelta = new Vector2(width, 28f);
            var image = buttonGo.AddComponent<Image>();
            image.sprite = BattleUISprites.Solid;
            image.color = new Color(0.18f, 0.16f, 0.22f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.font = BattleHudKit.Gothic;
            text.fontSize = 14f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = BattleUITheme.TextPrimary;
            text.text = label;
            text.raycastTarget = false;
            return text;
        }

        TMP_InputField AddInput(Transform parent, Vector2 position)
        {
            var go = new GameObject("FocusInput", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(130f, 28f);
            var image = go.AddComponent<Image>();
            image.sprite = BattleUISprites.Solid;
            image.color = new Color(0.12f, 0.1f, 0.12f, 1f);
            var input = go.AddComponent<TMP_InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 2f);
            trt.offsetMax = new Vector2(-6f, -2f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = BattleHudKit.Gothic;
            text.fontSize = 16f;
            text.color = BattleUITheme.TextPrimary;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            input.textComponent = text;
            input.text = "0";
            return input;
        }
    }
}
