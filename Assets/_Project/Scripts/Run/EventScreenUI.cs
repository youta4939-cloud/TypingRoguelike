using System;
using System.Collections.Generic;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    public readonly struct EventCardChoice
    {
        public int MemberIndex { get; }
        public int SlotIndex { get; }
        public string Label { get; }

        public EventCardChoice(int memberIndex, int slotIndex, string label)
        {
            MemberIndex = memberIndex;
            SlotIndex = slotIndex;
            Label = label;
        }
    }

    /// <summary>イベントの本文、選択肢、結果、カード選択。</summary>
    public sealed class EventScreenUI
    {
        RectTransform _root;
        GameObject _panel;
        Action<int> _onOption;
        Action _onConfirmKey;
        readonly List<OptionTyping> _typing = new();
        EventDefinition _event;
        EventQuery _query;

        sealed class OptionTyping
        {
            public int Index;
            public bool Enabled;
            public EventKanaReading Reading;
            public Text Label;
        }

        public void Hide()
        {
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);
            _panel = null;
            _onOption = null;
            _onConfirmKey = null;
            _event = null;
            _typing.Clear();
        }

        public void Show(
            Transform host,
            EventDefinition ev,
            EventQuery query,
            IReadOnlyCollection<string> partyNames,
            Action<int> onOption,
            Action onBack)
        {
            EnsureRoot(host);
            Hide();
            _event = ev;
            _query = query;
            _onOption = onOption;
            var line = EventRules.PickLine(ev, partyNames);
            var body = BuildShell(ev?.title, $"（{Mathf.Max(1, query.Floor)} 層）");
            var bodyBottom = line == null ? 0.50f : 0.58f;
            AddText(body, "Body", ev?.body ?? string.Empty, 22, bodyBottom, 0.78f, BattleUITheme.TextPrimary);
            if (line != null)
            {
                var speaker = EventCharacterIds.ToCharacterName(line.character);
                AddText(body, "Line", $"{speaker}：{line.text}", 20, 0.46f, 0.57f, BattleUITheme.Gold);
            }

            var options = ev?.options;
            var shown = 0;
            if (options != null)
            {
                var y = line == null ? 0.44f : 0.42f;
                for (var i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    if (!EventRules.OptionShown(option, query))
                        continue;
                    shown++;
                    var available = EventRules.OptionAvailable(option, query);
                    var detail = EventRules.DescribeOption(option, query);
                    var lines = string.IsNullOrEmpty(detail) ? 1 : detail.Split('\n').Length;
                    var height = Mathf.Max(0.11f, 0.02f + lines * 0.055f);
                    y = AddOptionRow(body, i, option, detail, y, height, available);
                }
            }

            if (shown == 0)
            {
                AddButton(body, "戻る", 0.18f, () => onBack?.Invoke());
                return;
            }

            var catcher = _panel.AddComponent<EventTypingCatcher>();
            catcher.OnKey = OnType;
            catcher.OnBackspace = ResetTyping;
        }

        public void ShowResult(Transform host, string resultText, string effectLine, string confirmLabel, Action onConfirm)
        {
            EnsureRoot(host);
            Hide();
            var body = BuildShell("結果", string.Empty);
            AddText(body, "Result", resultText ?? string.Empty, 26, 0.48f, 0.78f, BattleUITheme.TextPrimary);
            AddText(body, "Effect", effectLine ?? string.Empty, 22, 0.32f, 0.48f, BattleUITheme.Gold);
            AddButton(body, string.IsNullOrEmpty(confirmLabel) ? "マップへ戻る" : confirmLabel, 0.16f, onConfirm);
            _onConfirmKey = onConfirm;
            var catcher = _panel.AddComponent<EventTypingCatcher>();
            catcher.OnKey = _ => ConfirmFromKey();
            catcher.OnConfirmKey = ConfirmFromKey;
        }

        public void ShowCardPick(Transform host, string title, IReadOnlyList<EventCardChoice> cards, Action<EventCardChoice> onPick)
        {
            EnsureRoot(host);
            Hide();
            var body = BuildShell(title, "同じカードは選べない");
            if (cards == null || cards.Count == 0)
            {
                AddText(body, "Empty", "選べる刻印がない。", 22, 0.4f, 0.7f, BattleUITheme.TextMuted);
                AddButton(body, "続ける", 0.16f, () => onPick?.Invoke(new EventCardChoice(-1, -1, string.Empty)));
                return;
            }

            var y = 0.62f;
            foreach (var card in cards)
            {
                var choice = card;
                AddButton(body, card.Label, y, () => onPick?.Invoke(choice));
                y -= 0.08f;
            }
        }

        void Choose(int index)
        {
            if (_event?.options == null || index < 0 || index >= _event.options.Count)
                return;
            if (!EventRules.OptionAvailable(_event.options[index], _query))
                return;
            var chosen = _onOption;
            Hide();
            chosen?.Invoke(index);
        }

        float AddOptionRow(
            Transform parent,
            int index,
            EventOption option,
            string detail,
            float y,
            float height,
            bool interactable)
        {
            var bottom = y - height;
            var row = CreateRect(parent, "Option", new Vector2(0.06f, bottom), new Vector2(0.94f, y));
            var image = row.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = interactable
                ? new Color(0.12f, 0.16f, 0.24f, 0.98f)
                : new Color(0.08f, 0.08f, 0.1f, 0.7f);
            var button = row.gameObject.AddComponent<Button>();
            button.interactable = interactable;
            var captured = index;
            button.onClick.AddListener(() => Choose(captured));

            var word = CreateRect(row, "Word", new Vector2(0.02f, 0.08f), new Vector2(0.28f, 0.92f));
            var wordText = word.gameObject.AddComponent<Text>();
            wordText.font = BattleUITheme.Font;
            wordText.fontSize = 34;
            wordText.alignment = TextAnchor.MiddleCenter;
            wordText.supportRichText = true;
            wordText.horizontalOverflow = HorizontalWrapMode.Wrap;
            wordText.verticalOverflow = VerticalWrapMode.Overflow;

            var body = CreateRect(row, "Detail", new Vector2(0.30f, 0.06f), new Vector2(0.98f, 0.94f));
            var bodyText = body.gameObject.AddComponent<Text>();
            bodyText.font = BattleUITheme.Font;
            bodyText.fontSize = 18;
            bodyText.alignment = TextAnchor.MiddleLeft;
            bodyText.color = interactable ? BattleUITheme.TextPrimary : BattleUITheme.TextDisabled;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.text = detail ?? string.Empty;

            var reading = EventKanaReading.FromLabel(string.IsNullOrEmpty(option?.label) ? "選択" : option.label);
            var slot = new OptionTyping
            {
                Index = index,
                Enabled = interactable,
                Reading = reading,
                Label = wordText
            };
            _typing.Add(slot);
            PaintTyping(slot);
            return bottom - 0.015f;
        }

        void PaintTyping(OptionTyping slot)
        {
            if (slot?.Label == null || slot.Reading == null)
                return;
            slot.Label.text = BattleUITheme.RichTypedWord(slot.Reading.TypedText, slot.Reading.RestText, slot.Enabled);
        }

        void ResetTyping()
        {
            foreach (var slot in _typing)
            {
                slot.Reading?.Reset();
                PaintTyping(slot);
            }
        }

        void ConfirmFromKey()
        {
            var confirm = _onConfirmKey;
            Hide();
            confirm?.Invoke();
        }

        void OnType(char key)
        {
            var completed = -1;
            foreach (var slot in _typing)
            {
                if (!slot.Enabled || slot.Reading == null)
                    continue;
                if (!slot.Reading.TryAdvance(key))
                    continue;
                PaintTyping(slot);
                if (slot.Reading.IsComplete && completed < 0)
                    completed = slot.Index;
            }

            if (completed >= 0)
                Choose(completed);
        }

        RectTransform BuildShell(string title, string sub)
        {
            _panel = CreatePanel(_root, "EventScreen", Vector2.zero, Vector2.one, new Color(0.02f, 0.03f, 0.06f, 0.94f));
            var window = CreatePanel(_panel.transform, "Window", new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f),
                new Color(0.05f, 0.07f, 0.12f, 0.96f)).GetComponent<RectTransform>();
            AddText(window, "Title", title ?? "出来事", 34, 0.84f, 0.96f, BattleUITheme.Gold);
            AddText(window, "Sub", sub ?? string.Empty, 16, 0.78f, 0.84f, BattleUITheme.TextMuted);
            return window;
        }

        void EnsureRoot(Transform host)
        {
            if (_root != null)
                return;
            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("EventCanvas");
            go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = go.GetComponent<RectTransform>();
        }

        static void AddText(Transform parent, string name, string text, int size, float yMin, float yMax, Color color)
        {
            var rt = CreateRect(parent, name, new Vector2(0.06f, yMin), new Vector2(0.94f, yMax));
            var label = rt.gameObject.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.fontSize = size;
            label.alignment = TextAnchor.UpperCenter;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text ?? string.Empty;
        }

        static void AddButton(Transform parent, string label, float y, Action onClick, bool interactable = true)
        {
            var rt = CreateRect(parent, "Button", new Vector2(0.1f, y), new Vector2(0.9f, y + 0.07f));
            var image = rt.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = interactable
                ? new Color(0.12f, 0.16f, 0.24f, 0.98f)
                : new Color(0.08f, 0.08f, 0.1f, 0.7f);
            var button = rt.gameObject.AddComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(() => onClick?.Invoke());
            var textRt = CreateRect(rt, "Label", Vector2.zero, Vector2.one);
            var text = textRt.gameObject.AddComponent<Text>();
            text.font = BattleUITheme.Font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = interactable ? BattleUITheme.TextPrimary : BattleUITheme.TextDisabled;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = label ?? string.Empty;
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

        sealed class EventTypingCatcher : MonoBehaviour
        {
            public Action<char> OnKey;
            public Action OnBackspace;
            public Action OnConfirmKey;

            void Update()
            {
                if (DungeonMapUI.IsOverlayOpen)
                    return;
                var keyboard = Keyboard.current;
                if (keyboard == null)
                    return;
                if (keyboard.backspaceKey.wasPressedThisFrame)
                {
                    OnBackspace?.Invoke();
                    return;
                }

                if (keyboard.enterKey.wasPressedThisFrame ||
                    keyboard.numpadEnterKey.wasPressedThisFrame ||
                    keyboard.spaceKey.wasPressedThisFrame)
                {
                    OnConfirmKey?.Invoke();
                    return;
                }

                if (OnKey == null)
                    return;
                for (var c = 'a'; c <= 'z'; c++)
                {
                    if (!keyboard[Key.A + (c - 'a')].wasPressedThisFrame)
                        continue;
                    OnKey(c);
                    return;
                }
            }
        }
    }
}
