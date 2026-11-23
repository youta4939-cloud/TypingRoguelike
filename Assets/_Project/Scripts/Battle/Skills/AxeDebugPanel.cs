using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>開発ビルドだけの斧スキル操作。F4 でほかのデバッグパネルと一緒に開く。</summary>
    public sealed class AxeDebugPanel
    {
        readonly GameObject _root;
        readonly TextMeshProUGUI _status;
        readonly AxeSkillEntry[] _skills;
        TextMeshProUGUI _slotLabel;
        TextMeshProUGUI _levelLabel;
        int _slot;
        int _level = 1;
        bool _open;

        AxeDebugPanel(GameObject root, TextMeshProUGUI status, AxeSkillEntry[] skills)
        {
            _root = root;
            _status = status;
            _skills = skills ?? Array.Empty<AxeSkillEntry>();
        }

        public static AxeDebugPanel Create(
            RectTransform parent,
            AxeSkillConfig config,
            Action<int, int, int> onEquip,
            Action onReset,
            Action<string> onCommand)
        {
            var skills = config != null && config.skills != null ? config.skills : Array.Empty<AxeSkillEntry>();
            var go = new GameObject("AxeDebug", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(1100f, -40f);
            rt.sizeDelta = new Vector2(420f, 780f);
            var bg = go.AddComponent<Image>();
            bg.sprite = BattleUISprites.Solid;
            bg.color = new Color(0.05f, 0.06f, 0.04f, 0.94f);

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(go.transform, false);
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = statusRt.anchorMax = new Vector2(0f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(12f, -8f);
            statusRt.sizeDelta = new Vector2(396f, 150f);
            var status = statusGo.AddComponent<TextMeshProUGUI>();
            status.font = BattleHudKit.Gothic;
            status.fontSize = 15f;
            status.color = BattleUITheme.TextPrimary;
            status.alignment = TextAlignmentOptions.TopLeft;
            status.raycastTarget = false;

            var panel = new AxeDebugPanel(go, status, skills);
            var y = -166f;
            panel._slotLabel = panel.AddButton(go.transform, "枠 最初", new Vector2(12f, y), () =>
            {
                panel._slot = (panel._slot + 1) % 3;
                panel._slotLabel.text = panel._slot == 0 ? "枠 最初" : "枠 " + panel._slot;
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
            panel.AddButton(go.transform, "狂気セット", new Vector2(150f, y), () => onCommand?.Invoke("set:" + input.text), 120f);
            panel.AddButton(go.transform, "0", new Vector2(278f, y), () => onCommand?.Invoke("set:0"), 40f);
            panel.AddButton(go.transform, "500", new Vector2(322f, y), () => onCommand?.Invoke("set:500"), 40f);
            panel.AddButton(go.transform, "1000", new Vector2(366f, y), () => onCommand?.Invoke("set:1000"), 48f);
            y -= 32f;
            panel.AddButton(go.transform, "被ダメ+20%", new Vector2(12f, y), () => onCommand?.Invoke("vuln"), 96f);
            panel.AddButton(go.transform, "攻撃-20%", new Vector2(114f, y), () => onCommand?.Invoke("atk"), 96f);
            panel.AddButton(go.transform, "速度DOWN", new Vector2(216f, y), () => onCommand?.Invoke("slow"), 96f);
            panel.AddButton(go.transform, "バサ+30%", new Vector2(318f, y), () => onCommand?.Invoke("berserk"), 90f);
            y -= 32f;
            panel.AddButton(go.transform, "デバフを消す", new Vector2(12f, y), () => onCommand?.Invoke("clear"), 190f);
            panel.AddButton(go.transform, "1回を戻す", new Vector2(210f, y), () => onCommand?.Invoke("once"), 190f);
            y -= 32f;
            panel.AddButton(go.transform, "斧スキルのJSONを読み直す", new Vector2(12f, y), () => onCommand?.Invoke("reload"), 396f);
            go.SetActive(false);
            return panel;
        }

        public void Toggle()
        {
            _open = !_open;
            if (_root != null)
                _root.SetActive(_open);
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
            image.color = new Color(0.16f, 0.2f, 0.14f, 1f);
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
            var go = new GameObject("MadnessInput", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(130f, 28f);
            var image = go.AddComponent<Image>();
            image.sprite = BattleUISprites.Solid;
            image.color = new Color(0.1f, 0.12f, 0.1f, 1f);
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
            text.color = Color.white;
            input.textComponent = text;
            input.text = "500";
            return input;
        }
    }
}
