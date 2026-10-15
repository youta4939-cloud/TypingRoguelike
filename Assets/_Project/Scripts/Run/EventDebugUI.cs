using System;
using System.Collections.Generic;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    public sealed class EventDebugHooks
    {
        public Func<IReadOnlyList<EventDefinition>> All;
        public Func<EventDefinition, bool> ConditionMet;
        public Action<EventDefinition> Start;
        public Action<int> AddMadness;
        public Action<int> AddHp;
        public Action<int> AddCoins;
        public Func<bool> ShowWholeMap;
        public Action ToggleWholeMap;
    }

    /// <summary>エディタと開発ビルドだけで、全イベントの開始と数値の増減を出す。</summary>
    public static class EventDebugUI
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static RectTransform _root;
        static GameObject _list;
        static EventDebugHooks _hooks;
#endif

        public static void Ensure(Transform host, EventDebugHooks hooks)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _hooks = hooks;
            if (_root != null)
                return;

            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("EventDebugCanvas");
            go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            _root = go.GetComponent<RectTransform>();

            var button = CreateButton(_root, "イベント", new Vector2(0.86f, 0.93f), new Vector2(0.98f, 0.99f), ToggleList);
            button.GetComponentInChildren<Text>().fontSize = 18;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void ToggleList()
        {
            if (_list != null)
            {
                UnityEngine.Object.Destroy(_list);
                _list = null;
                return;
            }

            _list = CreatePanel(_root, "EventList", new Vector2(0.62f, 0.08f), new Vector2(0.98f, 0.92f),
                new Color(0.03f, 0.04f, 0.08f, 0.96f));
            var y = 0.90f;
            Text wholeLabel = null;
            var wholeButton = CreateButton(_list.transform, WholeMapLabel(), new Vector2(0.04f, y - 0.045f), new Vector2(0.96f, y), () =>
            {
                _hooks.ToggleWholeMap?.Invoke();
                if (wholeLabel != null)
                    wholeLabel.text = WholeMapLabel();
            }, BattleUITheme.TextPrimary);
            wholeLabel = wholeButton.GetComponentInChildren<Text>();
            y -= 0.055f;
            Text jackpotLabel = null;
            var jackpotButton = CreateButton(_list.transform, JackpotDebugLabel(), new Vector2(0.04f, y - 0.045f), new Vector2(0.96f, y), () =>
            {
                RunDebugState.JackpotRollForce10Percent = !RunDebugState.JackpotRollForce10Percent;
                if (jackpotLabel != null)
                    jackpotLabel.text = JackpotDebugLabel();
            }, BattleUITheme.TextPrimary);
            jackpotLabel = jackpotButton.GetComponentInChildren<Text>();
            y -= 0.055f;
            y = AddStatRow(y, "狂気", 100, _hooks.AddMadness);
            y = AddStatRow(y, "HP", 30, _hooks.AddHp);
            y = AddStatRow(y, "通貨", 50, _hooks.AddCoins);

            var events = _hooks.All?.Invoke();
            if (events == null || events.Count == 0)
            {
                AddLabel(_list.transform, "Empty", "events_layer1.json がありません", 0.2f, 0.4f, BattleUITheme.TextMuted);
                return;
            }

            foreach (var ev in events)
            {
                var definition = ev;
                var met = _hooks.ConditionMet == null || _hooks.ConditionMet(definition);
                var title = string.IsNullOrEmpty(definition.title) ? definition.id : definition.title;
                CreateButton(_list.transform, met ? title : $"{title}（条件外）",
                    new Vector2(0.04f, y - 0.045f), new Vector2(0.96f, y),
                    () =>
                    {
                        UnityEngine.Object.Destroy(_list);
                        _list = null;
                        _hooks.Start?.Invoke(definition);
                    },
                    met ? BattleUITheme.TextPrimary : new Color(0.45f, 0.45f, 0.48f));
                y -= 0.05f;
            }
        }

        static string WholeMapLabel() =>
            _hooks.ShowWholeMap != null && _hooks.ShowWholeMap() ? "全体表示をやめる" : "マップ全体を表示";

        static string JackpotDebugLabel() =>
            RunDebugState.JackpotRollForce10Percent
                ? "JACKPOT 当選率: 10% 固定（ON）"
                : "JACKPOT 当選率: 10% 固定（OFF）";

        static Text AddLabeledValue(float y, string text)
        {
            var rt = CreateRect(_list.transform, "VisibleRows", new Vector2(0.04f, y - 0.04f), new Vector2(0.60f, y));
            var label = rt.gameObject.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = BattleUITheme.TextPrimary;
            label.text = text;
            return label;
        }

        static float AddStatRow(float y, string label, int step, Action<int> apply)
        {
            AddLabel(_list.transform, label, label, y - 0.04f, y, BattleUITheme.TextMuted);
            CreateButton(_list.transform, "-", new Vector2(0.28f, y - 0.04f), new Vector2(0.42f, y),
                () => apply?.Invoke(-step), BattleUITheme.TextPrimary);
            CreateButton(_list.transform, "+", new Vector2(0.44f, y - 0.04f), new Vector2(0.58f, y),
                () => apply?.Invoke(step), BattleUITheme.TextPrimary);
            return y - 0.05f;
        }

        static void AddLabel(Transform parent, string name, string text, float yMin, float yMax, Color color)
        {
            var rt = CreateRect(parent, name, new Vector2(0.04f, yMin), new Vector2(0.26f, yMax));
            var label = rt.gameObject.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = color;
            label.text = text;
        }

        static GameObject CreateButton(Transform parent, string label, Vector2 min, Vector2 max, Action onClick, Color? textColor = null)
        {
            var rt = CreateRect(parent, label, min, max);
            var image = rt.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = new Color(0.14f, 0.16f, 0.22f, 0.96f);
            var button = rt.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());
            var textRt = CreateRect(rt, "Label", Vector2.zero, Vector2.one);
            var text = textRt.gameObject.AddComponent<Text>();
            text.font = BattleUITheme.Font;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor ?? Color.white;
            text.text = label;
            return rt.gameObject;
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = rt.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = color;
            return rt.gameObject;
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
#endif
    }
}
