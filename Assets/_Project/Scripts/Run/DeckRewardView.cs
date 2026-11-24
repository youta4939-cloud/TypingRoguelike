using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Battle.Deck;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    public sealed class DeckRewardView : MonoBehaviour
    {
        static readonly Color Gold = new Color32(247, 211, 139, 255);
        static readonly Color Surface = new Color32(29, 36, 53, 255);
        readonly List<Outline> _frames = new();
        readonly List<GameObject> _routes = new();
        IReadOnlyList<DeckCardDefinition> _cards;
        Action<DeckCardDefinition> _onPicked;
        Button _confirm;
        RunPartyState _party;
        CanvasGroup _choices;
        DeckComboWindow _window;
        int _closedFrame = -1;
        int _selected = -1;
        int _hover = -1;

        public void Build(IReadOnlyList<DeckCardDefinition> cards, RunPartyState party, int coins, string notice,
            Action<DeckCardDefinition> onPicked)
        {
            _cards = cards;
            _party = party;
            _onPicked = onPicked;
            var layout = Box(transform, "Choices", 0, 0, 1280, 700);
            _choices = layout.gameObject.AddComponent<CanvasGroup>();
            var canvas = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            layout.localScale = Vector3.one * Mathf.Min(1f, canvas.rect.width / 1300f, canvas.rect.height / 720f);
            Caption(layout, "Title", "報酬をひとつ選ぶ", 0, 286, 750, 80, 48, Gold);
            Caption(layout, "Notice", notice, 0, 215, 1100, 68, 24, Color.white);
            Caption(layout, "DeckCount", $"デッキ {party.DeckCardIds.Count}枚", 518, 285, 230, 48, 24, Color.white);

            for (var i = 0; i <= cards.Count; i++)
            {
                var index = i;
                var x = (i - cards.Count * 0.5f) * 416;
                var panel = Box(layout, $"Choice{i + 1}", x, 45, 376, 180);
                var background = panel.gameObject.AddComponent<Image>();
                background.color = Surface;
                var frame = panel.gameObject.AddComponent<Outline>();
                frame.effectColor = Gold;
                frame.effectDistance = new Vector2(4, -4);
                frame.useGraphicAlpha = false;
                _frames.Add(frame);
                Caption(layout, "Shortcut", (i + 1).ToString(), x, 164, 40, 32, 20, Gold);
                if (i < cards.Count)
                {
                    var card = cards[i];
                    DeckHandUIView.BuildRewardCard(panel, card);
                    var owned = party.DeckCardIds.Count(id => id == card.Id);
                    if (owned > 0)
                    {
                        var badge = Box(panel, "Owned", 140, 89, 84, 32);
                        badge.gameObject.AddComponent<Image>().color = Surface;
                        Caption(badge, "Count", $"所持 {owned}", 0, 0, 84, 32, 18, Gold);
                    }
                    _routes.Add(BuildRoutes(layout, x, DeckComboRoutes.Find(card, party.DeckCardIds)));
                    var inspect = Box(layout, "Inspect_" + card.Id, x, -211, 376, 50);
                    inspect.gameObject.AddComponent<Image>().color = Surface;
                    Caption(inspect, "Label", "コンボ表を開く　›", 0, 0, 350, 44, 22, Gold);
                    Listen(inspect.gameObject.AddComponent<EventTrigger>(), EventTriggerType.PointerClick,
                        () => OpenCombos(index));
                }
                else
                {
                    Caption(panel, "Coins", $"塔片 +{coins}", 0, 16, 350, 75, 44, Gold);
                    Caption(panel, "Balance", $"今の所持 {party.RunCoins}", 0, -48, 350, 43, 24, Color.white);
                }

                var trigger = panel.gameObject.AddComponent<EventTrigger>();
                Listen(trigger, EventTriggerType.PointerEnter, () => { _hover = index; Refresh(); });
                Listen(trigger, EventTriggerType.PointerExit, () => { if (_hover == index) _hover = -1; Refresh(); });
                Listen(trigger, EventTriggerType.PointerClick, () =>
                {
                    if (_selected == index) Confirm();
                    else Select(index);
                });
            }

            var button = Box(layout, "Confirm", 0, -292, 240, 60);
            button.gameObject.AddComponent<Image>().color = Surface;
            _confirm = button.gameObject.AddComponent<Button>();
            _confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            _confirm.onClick.AddListener(Confirm);
            Caption(button, "Label", "決定", 0, 0, 220, 60, 30, Gold);
            Caption(layout, "Controls", $"1～{cards.Count + 1} で選択　 B でコンボ表　 Enter または再クリックで決定",
                0, -353, 1100, 40, 20, Color.white);
            Refresh();
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || _onPicked == null || _window != null || _closedFrame == Time.frameCount)
                return;
            if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) Select(0);
            if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) Select(1);
            if (keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame) Select(2);
            if (keys.bKey.wasPressedThisFrame && _selected >= 0 && _selected < _cards.Count)
            {
                OpenCombos(_selected);
                return;
            }
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) Confirm();
        }

        void OpenCombos(int index)
        {
            if (_window != null || _onPicked == null) return;
            _choices.interactable = _choices.blocksRaycasts = false;
            EventSystem.current?.SetSelectedGameObject(null);
            var shade = Box(transform, "ComboWindow", 0, 0, 0, 0);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.015f, 0.035f, 0.87f);
            _window = shade.gameObject.AddComponent<DeckComboWindow>();
            _window.Open(_cards[index], _party.DeckCardIds, () =>
            {
                _closedFrame = Time.frameCount;
                _window = null;
                _choices.interactable = _choices.blocksRaycasts = true;
                _hover = -1;
                Refresh();
            });
        }

        void Select(int index)
        {
            if (index >= _frames.Count) return;
            _selected = index;
            Refresh();
        }

        void Refresh()
        {
            for (var i = 0; i < _frames.Count; i++)
                _frames[i].enabled = i == _selected;
            for (var i = 0; i < _routes.Count; i++)
                _routes[i].SetActive(i == _selected || i == _hover);
            _confirm.interactable = _selected >= 0;
        }

        void Confirm()
        {
            if (_selected < 0 || _onPicked == null || _window != null || _closedFrame == Time.frameCount) return;
            var callback = _onPicked;
            _onPicked = null;
            callback(_selected < _cards.Count ? _cards[_selected] : null);
        }

        static GameObject BuildRoutes(RectTransform layout, float x, List<DeckComboRoute> routes)
        {
            var panel = Box(layout, "ComboRoutes", x, -116, 376, 120);
            panel.gameObject.AddComponent<Image>().color = Surface;
            if (routes.Count == 0)
                Caption(panel, "Empty", "コンボなし", 0, 0, 376, 64, 22, Color.gray);
            for (var i = 0; i < Mathf.Min(3, routes.Count); i++)
            {
                var r = routes[i];
                var row = Box(panel, "Route", 0, 40 - i * 40, 366, 38);
                var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                group.childAlignment = TextAnchor.MiddleCenter;
                group.childControlWidth = true;
                group.childControlHeight = false;
                group.childForceExpandWidth = false;
                var ink = r.Simultaneous ? Gold : Color.white;
                AddCard(r.From);
                AddText(r.Simultaneous ? "＋" : "→");
                AddCard(r.To);
                AddText(r.Simultaneous ? "同時発動" : $"あと{r.Remaining}色");
                if (i == 2 && routes.Count > 3)
                    AddText($"+{routes.Count - 3}");

                void AddCard(DeckCardDefinition card)
                {
                    foreach (var color in card.Colors)
                    {
                        var gem = Box(row, "Gem", 0, 0, 24, 24);
                        DeckHandUIView.PaintRewardGem(gem.gameObject.AddComponent<Image>(), color);
                        var element = gem.gameObject.AddComponent<LayoutElement>();
                        element.minWidth = 14;
                        element.preferredWidth = 24;
                    }
                    AddText(card.Name.Split('・')[0]);
                }

                void AddText(string value)
                {
                    Caption(row, "Text", value, 0, 0, 56, 34, 17, ink);
                }
            }
            return panel.gameObject;
        }

        internal static void Listen(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        internal static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        internal static Text Caption(Transform parent, string name, string value, float x, float y, float w, float h,
            int size, Color ink)
        {
            var label = Box(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.text = value;
            label.fontSize = size;
            label.color = ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            return label;
        }
    }
}
