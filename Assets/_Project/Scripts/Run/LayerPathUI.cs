using System;
using System.Collections.Generic;
using TMPro;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>1層4マスの一本道。次の1マスだけ進める。</summary>
    public sealed partial class LayerPathUI
    {
        public static bool SuppressPrefab;
        GameObject _root;
        GameObject _panel;
        GameObject _modal;
        GameObject _menu;
        TMP_Text _hpValue;
        TMP_Text _hpMax;
        TMP_Text _coinText;
        TMP_Text _madnessValue;
        TMP_Text _madnessMax;
        Image _hpFill;
        Image _madnessFill;
        RectTransform _hpHighlight;
        TrustGaugeView[] _trustGauges;
        System.Action _trustDebug;
        System.Action _jackpotDebug;
        System.Action<string, int> _trustSet;
        readonly List<OpinionGaugeWidget> _opinionGauges = new();
        readonly List<StoryTrustReadout> _storyTrust = new();
        int _trustHoverCard = -1;
        bool _storyChoiceSubmitted;
        StoryMistTransition _storyTransition;
        TrustGaugeView[] _storyParty = Array.Empty<TrustGaugeView>();
        static readonly Dictionary<string, Sprite> ArtCache = new();

        public void Show(
            Transform host,
            DungeonRunState dungeon,
            RunPartyState party,
            RunExplorationInventory exploration,
            RunMadnessState madness,
            Action<int> onSquareSelected,
            Action onAbandonRun,
            Action onOpenLoadout,
            Action onOpenRelics,
            Action onOpenPartyStats,
            TrustGaugeView[] trust = null,
            Action onTrustDebug = null,
            Action onJackpotDebug = null)
        {
            EnsureRoot(host);
            HideModal();
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            _panel = CreateFullScreen("LayerPath", Hex("#151417"), Spec("bg_dungeon_route"));
            _trustGauges = trust;
            _trustDebug = onTrustDebug;
            _jackpotDebug = onJackpotDebug;
            _ = exploration;

            var root = _panel.transform;
            BuildPathHeader(root, dungeon, party, onOpenLoadout, onOpenRelics, onOpenPartyStats, onAbandonRun);
            BuildPathCards(root, dungeon, onSquareSelected);
            BuildPathFooter(root, party, madness, trust);
            Refresh(dungeon, party, exploration, madness);
        }

        // ---------- 進行画面（指示書 v1・1920x1080・左上原点） ----------

        static Color Hex(string html, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(html, out var color))
                color = Color.white;
            color.a = alpha;
            return color;
        }

        static readonly Color Ink = Hex("#F4F1EF");
        static readonly Color InkSub = Hex("#E2DEDC");
        static readonly Color InkTag = Hex("#D9D5D9");
        static readonly Color InkDisabled = Hex("#8A858B");
        static readonly Color InkNumber = Hex("#EAE6E5");
        static readonly Color FrameCurrent = Hex("#D6C5B8");
        static readonly Color FrameHover = Hex("#ECDED2");
        static readonly Color HpValueColor = Hex("#F699BA");
        static readonly Color NameColor = Hex("#DEDEDE");
        static readonly Color TrustColor = Hex("#E9EAEB");

        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            return BattleHudKit.Place(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -y), new Vector2(w, h));
        }

        static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align, float x, float y, float w, float h, float spacing)
        {
            var tmp = BattleHudKit.Text(parent, name, text, font, size, color, align,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -(y + h)), new Vector2(x + w, -y));
            tmp.characterSpacing = spacing;
            return tmp;
        }

        static Image[] BuildFrame(RectTransform card, float thickness, Color color)
        {
            var t = thickness;
            Image Edge(string edge, Vector2 min, Vector2 max, Vector2 omin, Vector2 omax) =>
                BattleHudKit.Img(card, edge, BattleUISprites.Solid, color, min, max, omin, omax);
            return new[]
            {
                Edge("FrameT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -t), Vector2.zero),
                Edge("FrameB", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t)),
                Edge("FrameL", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f)),
                Edge("FrameR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-t, 0f), Vector2.zero)
            };
        }

        void BuildPathHeader(
            Transform root,
            DungeonRunState dungeon,
            RunPartyState party,
            Action onOpenLoadout,
            Action onOpenRelics,
            Action onOpenPartyStats,
            Action onAbandonRun)
        {
            var floor = dungeon != null ? dungeon.Floor : 1;
            Label(root, "FloorTitle", $"第{floor}層", BattleHudKit.SerifMedium, 52f, Ink,
                TextAlignmentOptions.MidlineLeft, 88f, 100f, 320f, 68f, 20f);

            var shard = Box(root, "ShardIcon", 1680f, 16f, 24f, 36f);
            BattleHudKit.AddImage(shard, Spec("icon_wallet_shard"), Color.white).preserveAspect = true;
            _coinText = Label(root, "Coins", party != null ? party.RunCoins.ToString() : "0",
                BattleHudKit.SerifRegular, 28f, Ink, TextAlignmentOptions.MidlineLeft, 1732f, 20f, 68f, 28f, 0f);
            var divider = Box(root, "MenuDivider", 1806f, 20f, 1f, 28f);
            BattleHudKit.AddImage(divider, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.30f));

            var menuIcon = Box(root, "MenuIcon", 1852f, 24f, 26f, 20f);
            BattleHudKit.AddImage(menuIcon, Spec("icon_menu"), Color.white).preserveAspect = true;
            var hitRt = Box(root, "MenuButton", 1836f, 6f, 56f, 56f);
            var hit = BattleHudKit.AddImage(hitRt, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.001f));
            hit.raycastTarget = true;

            var items = new List<(string label, Action action, Color color)>
            {
                ("編成", onOpenLoadout, Ink),
                ("レリック", onOpenRelics, Ink),
                ("ステータス", onOpenPartyStats, Ink)
            };
            if (_trustDebug != null)
                items.Add(("信頼（デバッグ）", _trustDebug, InkSub));
            if (_jackpotDebug != null)
                items.Add(("抽選（デバッグ）", _jackpotDebug, InkSub));
            items.Add(("放棄", () => ConfirmAbandon(onAbandonRun), Hex("#C8352B")));
            BuildPathMenu(root, items);

            var button = hitRt.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (_menu == null)
                    return;
                _menu.transform.SetAsLastSibling();
                _menu.SetActive(!_menu.activeSelf);
            });
        }

        void ConfirmAbandon(Action onAbandonRun)
        {
            if (_menu != null)
                _menu.SetActive(false);
            ShowModal("ランを放棄", "このランを放棄しますか？", new (string label, Action onClick)[]
            {
                ("放棄する", () =>
                {
                    HideModal();
                    onAbandonRun?.Invoke();
                }),
                ("戻る", HideModal)
            });
        }

        void BuildPathMenu(Transform root, List<(string label, Action action, Color color)> items)
        {
            const float rowHeight = 56f;
            var height = items.Count * rowHeight + 16f;
            var blocker = BattleHudKit.Img(root, "MenuBlocker", BattleUISprites.Solid, new Color(0f, 0f, 0f, 0.35f),
                Vector2.zero, Vector2.one);
            blocker.raycastTarget = true;
            _menu = blocker.gameObject;
            var close = _menu.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _menu.SetActive(false));

            var panel = Box(blocker.rectTransform, "Menu", 1600f, 68f, 280f, height);
            var panelBg = BattleHudKit.AddImage(panel, BattleUISprites.Solid, Hex("#121114", 0.95f));
            panelBg.raycastTarget = true;
            BuildFrame(panel, 1f, new Color(1f, 1f, 1f, 0.18f));

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = Box(panel, "Item" + i, 0f, 8f + i * rowHeight, 280f, rowHeight);
                var bg = BattleHudKit.AddImage(row, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0f));
                bg.raycastTarget = true;
                Label(row, "Label", item.label, BattleHudKit.Sans, 24f, item.color,
                    TextAlignmentOptions.MidlineLeft, 24f, 0f, 232f, rowHeight, 0f);
                if (i < items.Count - 1)
                {
                    var rule = Box(row, "Rule", 16f, rowHeight - 1f, 248f, 1f);
                    BattleHudKit.AddImage(rule, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.10f));
                }

                var rowButton = row.gameObject.AddComponent<Button>();
                rowButton.targetGraphic = bg;
                var colors = rowButton.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.14f);
                colors.selectedColor = new Color(1f, 1f, 1f, 0f);
                rowButton.colors = colors;
                rowButton.onClick.AddListener(() =>
                {
                    if (_menu != null)
                        _menu.SetActive(false);
                    item.action?.Invoke();
                });
            }

            _menu.SetActive(false);
        }

        void BuildPathCards(Transform root, DungeonRunState dungeon, Action<int> onSquareSelected)
        {
            var squares = dungeon?.Squares;
            if (squares == null || squares.Length == 0)
                return;

            var open = dungeon.FirstUnclearedIndex();
            var lefts = new float[squares.Length];
            var widths = new float[squares.Length];
            var heights = new float[squares.Length];
            var tops = new float[squares.Length];
            var centers = new float[squares.Length];
            var x = 152f;
            for (var i = 0; i < squares.Length; i++)
            {
                var current = i == open;
                widths[i] = current ? 424f : 380f;
                heights[i] = current ? 632f : 620f;
                tops[i] = current ? 232f : 240f;
                lefts[i] = x;
                centers[i] = x + widths[i] * 0.5f;
                x += widths[i] + 20f;
            }

            BuildCutLine(root, 178f, 88f, 88f + 1756f, centers, 28f, new Color(1f, 1f, 1f, 0.14f));
            var line = Box(root, "TrackLine", centers[0], 207f, centers[centers.Length - 1] - centers[0], 2f);
            BattleHudKit.AddImage(line, BattleUISprites.Solid, Hex("#DDDCDF"));

            for (var i = 0; i < squares.Length; i++)
            {
                var current = i == open;
                var cleared = squares[i].Cleared;
                var numberColor = cleared ? InkDisabled : InkNumber;
                Label(root, "TrackNo" + i, (i + 1).ToString("00"), BattleHudKit.SerifRegular, 24f, numberColor,
                    TextAlignmentOptions.Center, centers[i] - 30f, 164f, 60f, 28f, 4f);
                var dotName = current ? "dot_current" : cleared ? "dot_passed" : "dot_unreached";
                var dot = Box(root, "TrackNode" + i, centers[i] - 18f, 190f, 36f, 36f);
                BattleHudKit.AddImage(dot, Spec(dotName), Color.white).preserveAspect = true;
                BuildSquareCard(root, dungeon, i, lefts[i], tops[i], widths[i], heights[i], current, onSquareSelected);
            }
        }

        static void BuildCutLine(Transform root, float y, float x0, float x1, float[] centers, float halfGap, Color color)
        {
            var cursor = x0;
            for (var i = 0; i < centers.Length; i++)
            {
                var gapStart = centers[i] - halfGap;
                if (gapStart > cursor + 0.5f)
                {
                    var segment = Box(root, "HeaderRule" + i, cursor, y, gapStart - cursor, 1f);
                    BattleHudKit.AddImage(segment, BattleUISprites.Solid, color);
                }

                cursor = Mathf.Max(cursor, centers[i] + halfGap);
            }

            if (x1 > cursor + 0.5f)
            {
                var tail = Box(root, "HeaderRuleEnd", cursor, y, x1 - cursor, 1f);
                BattleHudKit.AddImage(tail, BattleUISprites.Solid, color);
            }
        }

        void BuildSquareCard(Transform root, DungeonRunState dungeon, int index, float left, float top, float width,
            float height, bool current, Action<int> onSquareSelected)
        {
            var square = dungeon.Squares[index];
            var cleared = square.Cleared;
            var card = BattleHudKit.Place(root, "Square_" + index, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(left + width * 0.5f, -(top + height * 0.5f)), new Vector2(width, height));
            var back = BattleHudKit.AddImage(card, BattleUISprites.Solid, Hex("#151417"));
            back.raycastTarget = current;

            var clip = BattleHudKit.Rect(card, "ArtClip", Vector2.zero, Vector2.one);
            clip.gameObject.AddComponent<RectMask2D>();
            var artSprite = SquareIllustration(square, index);
            if (artSprite != null)
            {
                var tint = current ? Color.white
                    : cleared ? new Color(0.62f, 0.62f, 0.62f, 1f)
                    : new Color(0.8f, 0.8f, 0.8f, 1f);
                var art = BattleHudKit.Rect(clip, "Art", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                BattleHudKit.AddImage(art, artSprite, tint);
                art.pivot = new Vector2(0.5f, 0.62f);
                art.anchoredPosition = Vector2.zero;
                var scale = Mathf.Max(width / Mathf.Max(1f, artSprite.rect.width), height / Mathf.Max(1f, artSprite.rect.height));
                art.sizeDelta = new Vector2(artSprite.rect.width * scale, artSprite.rect.height * scale);
            }

            var shadeSprite = Spec("card_shade");
            if (shadeSprite != null)
                BattleHudKit.Img(clip, "Shade", shadeSprite, Color.white, Vector2.zero, Vector2.one);
            if (cleared)
                BattleHudKit.Img(clip, "PassedFilm", BattleUISprites.Solid, Hex("#141416", 0.27f), Vector2.zero, Vector2.one);

            var frameColor = current ? FrameCurrent : new Color(1f, 1f, 1f, cleared ? 0.14f : 0.22f);
            var frames = BuildFrame(card, current ? 2f : 1f, frameColor);
            if (current)
                BuildCorners(card, width, height);

            if (index == dungeon.Squares.Length - 1)
            {
                var inner = Box(card, "FixedLine", 6f, 9f, width - 12f, 1f);
                BattleHudKit.AddImage(inner, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.16f));
                BuildFixedTag(card);
            }

            if (cleared)
                return;

            BuildNameRow(card, square, current ? 136f : 132f);
            if (current)
            {
                Label(card, "Status", "現在地", BattleHudKit.SerifRegular, 24f, InkSub,
                    TextAlignmentOptions.Center, 0f, height - 102f, width, 28f, 5f);
                var buttonImage = BuildAdvanceButton(card, width, height);
                var button = card.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = back;
                button.onClick.AddListener(() => onSquareSelected?.Invoke(index));
                BindCurrentHover(card, frames, buttonImage);
                return;
            }

            var rule = Box(card, "StatusRule", (width - 128f) * 0.5f, height - 55f, 128f, 2f);
            var fade = Spec("divider_fade");
            BattleHudKit.AddImage(rule, fade != null ? fade : BattleUISprites.Solid, new Color(1f, 1f, 1f, fade != null ? 1f : 0.28f));
            Label(card, "Status", "未到達", BattleHudKit.SerifRegular, 24f, Hex("#E2DEDC", 0.85f),
                TextAlignmentOptions.Center, 0f, height - 42f, width, 28f, 5f);
        }

        static void BuildCorners(RectTransform card, float width, float height)
        {
            PlaceCorner(card, 13f, 13f, 1f, 1f);
            PlaceCorner(card, width - 13f, 13f, -1f, 1f);
            PlaceCorner(card, width - 13f, height - 13f, -1f, -1f);
            PlaceCorner(card, 13f, height - 13f, 1f, -1f);
        }

        static void PlaceCorner(RectTransform card, float cx, float cy, float sx, float sy)
        {
            var rt = BattleHudKit.Place(card, "Corner", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(cx, -cy), new Vector2(18f, 18f));
            rt.localScale = new Vector3(sx, sy, 1f);
            var image = BattleHudKit.AddImage(rt, Spec("card_corner_l"), Color.white);
            image.preserveAspect = true;
        }

        static void BuildFixedTag(RectTransform card)
        {
            var tag = BattleHudKit.Place(card, "FixedTag", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(208f, 48f));
            var sprite = Spec("fixed_tag");
            BattleHudKit.AddImage(tag, sprite != null ? sprite : BattleUISprites.Solid,
                sprite != null ? Color.white : Hex("#121114", 0.92f));

            var probe = BattleHudKit.Text(tag, "Probe", "固定", BattleHudKit.SerifMedium, 24f, InkTag,
                TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            probe.characterSpacing = 20f;
            probe.ForceMeshUpdate();
            var textW = Mathf.Max(24f, probe.preferredWidth);
            probe.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(probe.gameObject);

            var groupW = 20f + 14f + textW;
            var start = (208f - groupW) * 0.5f;
            var gem = Box(tag, "Gem", start, 14f, 20f, 20f);
            BattleHudKit.AddImage(gem, Spec("icon_fixed_diamond"), Color.white).preserveAspect = true;
            Label(tag, "Text", "固定", BattleHudKit.SerifMedium, 24f, InkTag,
                TextAlignmentOptions.MidlineLeft, start + 34f, 12f, textW + 4f, 24f, 20f);
        }

        static void BuildNameRow(RectTransform card, LayerSquare square, float fromBottom)
        {
            var title = SquareTitle(square);
            var spacing = title.Length <= 2 ? 35f : 10f;
            var probe = BattleHudKit.Text(card, "NameProbe", title, BattleHudKit.SerifMedium, 32f, Ink,
                TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            probe.characterSpacing = spacing;
            probe.ForceMeshUpdate();
            var textW = Mathf.Max(8f, probe.preferredWidth);
            probe.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(probe.gameObject);

            var icon = Spec(SquareIconName(square));
            var rowW = textW + (icon != null ? 60f : 0f);
            var row = BattleHudKit.Place(card, "NameRow", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, fromBottom), new Vector2(rowW, 40f));
            var textX = 0f;
            if (icon != null)
            {
                var iconRt = BattleHudKit.Place(row, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(20f, 0f), new Vector2(40f, 40f));
                BattleHudKit.AddImage(iconRt, icon, Color.white).preserveAspect = true;
                textX = 60f;
            }

            var name = BattleHudKit.Text(row, "Name", title, BattleHudKit.SerifMedium, 32f, Ink,
                TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(textX, 0f), new Vector2(textX + textW, 0f));
            name.characterSpacing = spacing;
        }

        static Image BuildAdvanceButton(RectTransform card, float cardW, float cardH)
        {
            var width = cardW - 32f;
            var rt = Box(card, "Advance", 16f, cardH - 72f, width, 64f);
            var sprite = Spec("btn_advance");
            var bg = BattleHudKit.AddImage(rt, sprite != null ? sprite : BattleUISprites.Solid,
                sprite != null ? Color.white : Hex("#4E403D", 0.85f));
            bg.type = Image.Type.Simple;
            bg.raycastTarget = false;

            var probe = BattleHudKit.Text(rt, "Probe", "進む", BattleHudKit.SerifMedium, 34f, Ink,
                TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            probe.characterSpacing = 30f;
            probe.ForceMeshUpdate();
            var textW = Mathf.Max(32f, probe.preferredWidth);
            probe.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(probe.gameObject);

            const float chevron = 18f;
            const float gap = 20f;
            var groupW = textW + gap + chevron;
            var start = (width - groupW) * 0.5f;
            Label(rt, "Label", "進む", BattleHudKit.SerifMedium, 34f, Ink,
                TextAlignmentOptions.MidlineLeft, start, 16f, textW + 2f, 32f, 30f);
            var icon = Box(rt, "Chevron", start + textW + gap, 23f, chevron, chevron);
            BattleHudKit.AddImage(icon, Spec("icon_chevron"), Color.white).preserveAspect = true;
            return bg;
        }

        static void BindCurrentHover(RectTransform card, Image[] frames, Image button)
        {
            var trigger = card.gameObject.AddComponent<EventTrigger>();
            void Paint(bool over, bool down)
            {
                card.localScale = Vector3.one * (down ? 0.98f : over ? 1.02f : 1f);
                var frame = down
                    ? new Color(FrameCurrent.r * 0.92f, FrameCurrent.g * 0.92f, FrameCurrent.b * 0.92f, 1f)
                    : over ? FrameHover : FrameCurrent;
                if (frames != null)
                {
                    for (var i = 0; i < frames.Length; i++)
                    {
                        if (frames[i] != null)
                            frames[i].color = frame;
                    }
                }

                if (button != null)
                    button.color = down ? new Color(0.92f, 0.92f, 0.92f, 1f) : over ? new Color(1.12f, 1.12f, 1.12f, 1f) : Color.white;
            }

            AddTrigger(trigger, EventTriggerType.PointerEnter, () => Paint(true, false));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => Paint(false, false));
            AddTrigger(trigger, EventTriggerType.PointerDown, () => Paint(true, true));
            AddTrigger(trigger, EventTriggerType.PointerUp, () => Paint(true, false));
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void BuildPathFooter(Transform root, RunPartyState party, RunMadnessState madness, TrustGaugeView[] trust)
        {
            var rule = Box(root, "FooterRule", 52f, 936f, 1828f, 1f);
            BattleHudKit.AddImage(rule, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.18f));

            Label(root, "HpLabel", "HP", BattleHudKit.SerifRegular, 24f, Ink,
                TextAlignmentOptions.MidlineLeft, 80f, 968f, 48f, 24f, 4f);
            _hpValue = Label(root, "HpValue", "", BattleHudKit.SerifRegular, 36f, HpValueColor,
                TextAlignmentOptions.MidlineLeft, 160f, 962f, 84f, 32f, 0f);
            _hpMax = Label(root, "HpMax", "", BattleHudKit.SerifRegular, 26f, InkSub,
                TextAlignmentOptions.MidlineLeft, 248f, 968f, 120f, 24f, 0f);
            _hpFill = BuildGauge(root, "HpMeter", 80f, 1008f, 496f, 12f, Hex("#F490B4"),
                new Color(1f, 1f, 1f, 0.20f), true, out _hpHighlight);

            var split = Box(root, "FooterDivider", 624f, 972f, 1f, 64f);
            BattleHudKit.AddImage(split, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.22f));

            Label(root, "MadnessLabel", "狂気", BattleHudKit.SerifRegular, 24f, Ink,
                TextAlignmentOptions.MidlineLeft, 676f, 968f, 72f, 24f, 5f);
            _madnessValue = Label(root, "MadnessValue", "", BattleHudKit.SerifRegular, 36f, Ink,
                TextAlignmentOptions.MidlineLeft, 780f, 962f, 120f, 32f, 0f);
            _madnessMax = Label(root, "MadnessMax", "", BattleHudKit.SerifRegular, 26f, InkSub,
                TextAlignmentOptions.MidlineLeft, 812f, 968f, 120f, 24f, 0f);
            _madnessFill = BuildGauge(root, "MadnessMeter", 676f, 1008f, 428f, 12f, Hex("#8A4FD0"),
                Hex("#6E6A66"), false, out _);
            _ = madness;

            var list = trust ?? Array.Empty<TrustGaugeView>();
            var count = Mathf.Min(3, list.Length);
            float[] centers = { 1232f, 1480f, 1728f };
            for (var i = 0; i < count; i++)
            {
                var gauge = list[i];
                if (gauge == null)
                    continue;
                var cx = centers[i];
                var portrait = StoryPortrait(gauge);
                var face = BattleHudKit.CirclePortrait(root, "Portrait" + i, portrait, 96f, new Vector2(0f, 1f),
                    new Vector2(cx, -1000f), new Color(1f, 1f, 1f, 0.25f), 1f);
                if (portrait != null)
                {
                    var faceRt = face.rectTransform;
                    faceRt.anchorMin = faceRt.anchorMax = new Vector2(0.5f, 0.5f);
                    faceRt.pivot = new Vector2(0.5f, 0.5f);
                    faceRt.anchoredPosition = Vector2.zero;
                    faceRt.sizeDelta = new Vector2(96f, 96f);
                    var fitter = face.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    fitter.aspectRatio = portrait.rect.width / Mathf.Max(1f, portrait.rect.height);
                }

                var nameX = cx + 72f;
                Label(root, "Name" + i, gauge.Name ?? "", BattleHudKit.SerifRegular, 20f, NameColor,
                    TextAlignmentOptions.MidlineLeft, nameX, 969f, 150f, 24f, 5f);
                var underline = Box(root, "NameRule" + i, nameX - 4f, 999f, 64f, 1f);
                BattleHudKit.AddImage(underline, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.30f));
                Label(root, "Trust" + i, $"<size=22>信頼</size><space=12><size=24>{gauge.Trust}</size>",
                    BattleHudKit.SerifRegular, 22f, TrustColor, TextAlignmentOptions.MidlineLeft,
                    nameX, 1009f, 160f, 26f, 0f);
            }

            _ = party;
        }

        static Image BuildGauge(Transform root, string name, float x, float y, float w, float h, Color fill, Color border,
            bool bottomHighlight, out RectTransform highlight)
        {
            var meter = Box(root, name, x, y, w, h);
            BattleHudKit.AddImage(meter, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.06f));
            BuildFrame(meter, 1f, border);
            var fillRt = Box(meter, "Fill", 1f, 1f, w - 2f, h - 2f);
            var image = BattleHudKit.AddImage(fillRt, BattleUISprites.Solid, fill);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = 0f;
            highlight = null;
            if (bottomHighlight)
            {
                highlight = Box(meter, "Highlight", 1f, h - 2f, w - 2f, 1f);
                BattleHudKit.AddImage(highlight, BattleUISprites.Solid, Hex("#FFACCD", 0.6f));
            }

            return image;
        }

        static string SquareTitle(LayerSquare square)
        {
            if (square.RoadId == "danger")
                return "強敵";
            return square.Type switch
            {
                DungeonNodeType.Combat => "戦闘",
                DungeonNodeType.Event => "イベント",
                DungeonNodeType.Rest => "休息",
                DungeonNodeType.Treasure => "宝箱",
                DungeonNodeType.Merchant => "店",
                DungeonNodeType.Trap => "罠",
                DungeonNodeType.MidBoss => "中ボス",
                DungeonNodeType.Boss => "ボス",
                DungeonNodeType.Elevator => "エレベーター",
                _ => "？"
            };
        }

        static string SquareIconName(LayerSquare square)
        {
            if (square.RoadId == "danger")
                return "icon_battle";
            return square.Type switch
            {
                DungeonNodeType.Event => "icon_event",
                DungeonNodeType.Rest => "icon_rest",
                DungeonNodeType.Treasure => "icon_treasure",
                DungeonNodeType.Combat or DungeonNodeType.MidBoss or DungeonNodeType.Boss or DungeonNodeType.Trap => "icon_battle",
                _ => "icon_event"
            };
        }

        static Sprite Spec(string name) => LoadSprite("Run/Path/Spec/" + name);

        static Sprite SquareIllustration(LayerSquare square, int index)
        {
            var file = square.RoadId == "danger"
                ? "card_danger_01"
                : square.Type switch
                {
                    DungeonNodeType.Combat => $"card_battle_0{index % 3 + 1}",
                    DungeonNodeType.MidBoss => "card_boss_01",
                    DungeonNodeType.Boss => "card_boss_01",
                    DungeonNodeType.Rest => "card_rest_01",
                    DungeonNodeType.Merchant => "card_shop_01",
                    DungeonNodeType.Treasure => "card_treasure_01",
                    DungeonNodeType.Trap => "card_danger_01",
                    _ => "card_event_01"
                };
            return LoadSprite("RouteLottery/Sprites/Illustrations/" + file);
        }

        static Sprite LoadSprite(string path)
        {
            if (ArtCache.TryGetValue(path, out var cached) && cached != null)
                return cached;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var all = Resources.LoadAll<Sprite>(path);
                if (all != null && all.Length > 0)
                    sprite = all[0];
            }

            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            ArtCache[path] = sprite;
            return sprite;
        }

        public void ReleasePause()
        {
            if (Time.timeScale <= 0f)
                Time.timeScale = 1f;
        }

        public void Refresh(
            DungeonRunState dungeon,
            RunPartyState party,
            RunExplorationInventory exploration = null,
            RunMadnessState madness = null)
        {
            if (party != null)
            {
                var hp = (int)party.CurrentHp;
                var max = (int)party.MaxHp;
                if (_hpValue != null)
                    _hpValue.text = hp.ToString();
                if (_hpMax != null)
                    _hpMax.text = "/ " + max;
                if (_coinText != null)
                    _coinText.text = party.RunCoins.ToString();
                if (_hpFill != null && party.MaxHp > 0f)
                    _hpFill.fillAmount = Mathf.Clamp01(party.CurrentHp / party.MaxHp);
                if (_hpHighlight != null && _hpFill != null)
                    _hpHighlight.sizeDelta = new Vector2(Mathf.Max(0f, 494f * _hpFill.fillAmount), 1f);
            }

            if (madness != null)
            {
                if (_madnessValue != null)
                {
                    _madnessValue.text = madness.CurrentMadness.ToString();
                    _madnessValue.ForceMeshUpdate();
                    if (_madnessMax != null)
                    {
                        var valueWidth = Mathf.Max(20f, _madnessValue.preferredWidth);
                        var maxX = 780f + valueWidth + 12f;
                        var maxRt = _madnessMax.rectTransform;
                        maxRt.offsetMin = new Vector2(maxX, -992f);
                        maxRt.offsetMax = new Vector2(maxX + 120f, -968f);
                        _madnessMax.text = "/ " + madness.MaxMadness;
                    }
                }

                if (_madnessFill != null)
                {
                    var max = madness.MaxMadness;
                    _madnessFill.fillAmount = max > 0 ? madness.CurrentMadness / (float)max : 0f;
                }
            }

            _ = dungeon;
            _ = exploration;
        }

        public void ShowModal(string title, string body, IReadOnlyList<(string label, Action onClick)> actions)
        {
            HideModal();
            EnsureRoot(null);
            _modal = CreateFullScreen("LayerModal", new Color(0.005f, 0.008f, 0.02f, 0.86f));
            var win = BattleUIChrome.CreateLuxuryWindow(
                _modal.transform, "Modal", new Vector2(0.27f, 0.22f), new Vector2(0.73f, 0.78f),
                title, BattleUITheme.Gold);
            var bodyT = win.Find("Body");
            if (bodyT == null)
                return;

            var content = CreateStoryScroll(bodyT, "ModalReading", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.92f));
            AddStoryParagraph(content, "ModalBody", body ?? "", 22, BattleUITheme.TextPrimary);

            var count = actions?.Count ?? 0;
            for (var i = 0; i < count; i++)
            {
                var action = actions[i];
                var yMax = 0.32f - i * 0.12f;
                CreateButton(bodyT, action.label, new Vector2(0.18f, yMax - 0.1f), new Vector2(0.82f, yMax), () =>
                {
                    HideModal();
                    action.onClick?.Invoke();
                });
            }
        }

        public void HideModal()
        {
            if (_modal == null)
                return;
            UnityEngine.Object.Destroy(_modal);
            _modal = null;
        }

        public void Hide()
        {
            HideModal();
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }

            ReleasePause();
        }

        public void ShowStoryEvent(StoryEventView view, Action<int> onPick, StoryEventPrefabView layout = null)
        {
            EnsureRoot(null);
            HideModal();
            if (_storyTransition != null) _storyTransition.Complete();
            var previous = _panel;
            if (!Application.isPlaying && previous != null)
                UnityEngine.Object.DestroyImmediate(previous);

            view ??= new StoryEventView();
            _storyTrust.Clear();
            _trustHoverCard = -1;
            _storyChoiceSubmitted = false;
            _storyParty = view.Party ?? Array.Empty<TrustGaugeView>();
            if (SuppressPrefab)
            {
                _panel = CreateFullScreen("StoryEvent", new Color(0.012f, 0.01f, 0.01f, 1f));
                BuildStoryContent(view, onPick);
            }
            else
            {
                var prefab = layout != null ? layout.gameObject : Resources.Load<GameObject>("UI/StoryEvent");
                if (prefab == null || prefab.GetComponent<StoryEventPrefabView>() == null)
                    throw new InvalidOperationException("イベントUIのPrefabを移行してください: Tools > TypingRoguelike > Migrate Event UI To Prefab");
                _panel = UnityEngine.Object.Instantiate(prefab, _root.transform);
                _panel.name = "StoryEvent";
                BindStoryPrefab(_panel.GetComponent<StoryEventPrefabView>(), view, onPick);
            }
            if (Application.isPlaying)
            {
                _storyTransition = _panel.AddComponent<StoryMistTransition>();
                _storyTransition.Begin(previous, _root.transform);
            }
        }

        // Editor authoring source only. Runtime binds content to the saved prefab.
        void BuildStoryContent(StoryEventView view, Action<int> onPick)
        {
            _storyParty = view.Party ?? Array.Empty<TrustGaugeView>();
            BuildStoryBackdrop(_panel.transform);
            BuildStoryHeader(_panel.transform, view);
            BuildStoryTitle(_panel.transform, view.Title);
            BuildStoryBody(_panel.transform, view.Body, view.Aside);
            var choices = view.Choices ?? Array.Empty<StoryEventChoiceView>();
            for (var i = 0; i < 2; i++)
            {
                var choice = i < choices.Length ? choices[i] : null;
                var xMin = i == 0 ? 0.055f : 0.515f;
                var xMax = i == 0 ? 0.485f : 0.945f;
                var index = i;
                BuildStoryChoice(_panel.transform, choice, view.ShowChance, index, xMin, 0.12f, xMax, 0.575f,
                    () => onPick?.Invoke(index));
            }

            BuildStoryStatus(_panel.transform, view);
        }

        void BuildStoryBackdrop(Transform parent)
        {
            var frame = CreateRect(parent, "Backdrop", Vector2.zero, Vector2.one);
            frame.gameObject.AddComponent<RectMask2D>();
            var art = CreateRect(frame, "Picture", Vector2.zero, Vector2.one);
            var image = art.gameObject.AddComponent<Image>();
            image.sprite = Art("Run/Path/bg_story_corridor") ?? PathSprite("bg_corridor");
            image.color = new Color(0.86f, 0.84f, 0.81f, 1f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            if (image.sprite != null)
            {
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            }
            var shade = CreateRect(parent, "ReadingShade", Vector2.zero, Vector2.one);
            shade.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var fade = shade.gameObject.AddComponent<Image>();
            fade.sprite = BattleUISprites.HorizontalGradient;
            fade.color = new Color(0.005f, 0.004f, 0.003f, 0.85f);
            fade.raycastTarget = false;
        }

        void BuildStoryHeader(Transform parent, StoryEventView view)
        {
            var badge = CreateLabel(parent, "Badge", view.Badge ?? "", 20, TextAnchor.MiddleLeft,
                new Vector2(0.035f, 0.935f), new Vector2(0.52f, 0.985f), StoryInk);
            badge.font = BattleUITheme.Font;
            var coins = CreateLabel(parent, "Coins", "塔片 " + FormatCount(view.Coins), 22, TextAnchor.MiddleRight,
                new Vector2(0.68f, 0.935f), new Vector2(0.83f, 0.985f), StoryInk);
            var materials = CreateLabel(parent, "Materials", "素材 " + FormatCount(view.Materials), 20, TextAnchor.MiddleRight,
                new Vector2(0.85f, 0.935f), new Vector2(0.965f, 0.985f), new Color(0.73f, 0.69f, 0.62f));
            coins.font = materials.font = BattleUITheme.Font;
        }

        static readonly Color StoryInk = new(0.94f, 0.9f, 0.82f, 1f);
        static Font _storySerif;
        static Font StorySerif => _storySerif != null ? _storySerif
            : _storySerif = Resources.Load<Font>("Battle/Fonts/NotoSerifJP-Regular") ?? BattleUITheme.Font;

        void BuildStoryTitle(Transform parent, string title)
        {
            var label = CreateLabel(parent, "EventTitle", string.IsNullOrEmpty(title) ? "出来事" : title,
                64, TextAnchor.MiddleLeft, new Vector2(0.035f, 0.785f), new Vector2(0.9f, 0.925f), StoryInk);
            label.font = StorySerif;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 34;
            label.resizeTextMaxSize = 64;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            BattleUITheme.AddTextShadow(label, new Color(0f, 0f, 0f, 0.8f), new Vector2(1f, -1f));
            var rule = CreateRect(parent, "TitleAccent", new Vector2(0.035f, 0.783f), new Vector2(0.15f, 0.783f));
            rule.sizeDelta = new Vector2(0f, 2f);
            var image = rule.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = new Color(0.66f, 0.53f, 0.36f, 1f);
            image.raycastTarget = false;
        }

        void BuildStoryBody(Transform parent, string body, string aside)
        {
            var plate = CreateRect(parent, "BodyPlate", new Vector2(0.035f, 0.595f), new Vector2(0.935f, 0.755f));
            var content = CreateStoryScroll(plate, "Narrative", Vector2.zero, Vector2.one);
            AddStoryReading(content, "Body", body ?? "", 30, StoryInk);
            if (!string.IsNullOrEmpty(aside))
                AddStoryReading(content, "Aside", aside, 18, new Color(0.72f, 0.67f, 0.59f, 1f));
        }

        void BuildStoryChoice(
            Transform parent, StoryEventChoiceView choice, bool showChance, int cardIndex,
            float xMin, float yMin, float xMax, float yMax, Action onClick)
        {
            choice ??= new StoryEventChoiceView { Label = "……" };
            var card = CreateRect(parent, "Choice" + cardIndex, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            var image = card.gameObject.AddComponent<Image>();
            image.color = new Color(0.012f, 0.009f, 0.008f, 0.9f);
            BattleUITheme.EnsureSprite(image);
            var border = CreateStoryBorder(card);
            var hover = card.gameObject.AddComponent<EventChoiceHover>();
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (_storyChoiceSubmitted || (_storyTransition != null && _storyTransition.IsRunning))
                    return;
                _storyChoiceSubmitted = true;
                foreach (var other in _panel.GetComponentsInChildren<Button>())
                    other.interactable = false;
                onClick?.Invoke();
            });

            var title = CreateLabel(card, "Label", choice.Label ?? "", 36, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.835f), new Vector2(0.96f, 0.975f), new Color(0.98f, 0.94f, 0.84f, 1f));
            title.font = StorySerif;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 20;
            title.resizeTextMaxSize = 36;
            title.verticalOverflow = VerticalWrapMode.Truncate;

            var rule = CreateRect(card, "TitleRule", new Vector2(0.04f, 0.825f), new Vector2(0.96f, 0.825f));
            rule.sizeDelta = new Vector2(0f, 1f);
            var ruleImage = rule.gameObject.AddComponent<Image>();
            ruleImage.color = new Color(0.55f, 0.4f, 0.18f, 0.55f);
            BattleUITheme.EnsureSprite(ruleImage);
            ruleImage.raycastTarget = false;

            var marks = choice.Marks ?? Array.Empty<StoryEventTrustMark>();
            hover.Bind(border, () => PreviewTrust(cardIndex, marks), () => EndTrustPreview(cardIndex));
            var content = CreateStoryScroll(card, "ChoiceDetails", new Vector2(0.04f, 0.345f), new Vector2(0.96f, 0.8f));
            LayoutChoiceEffects(content, choice.Effects, showChance);
            BuildStorySupporters(card, marks);
        }

        static Image[] CreateStoryBorder(Transform parent)
        {
            var root = CreateRect(parent, "Border", Vector2.zero, Vector2.one);
            var images = new Image[4];
            for (var i = 0; i < images.Length; i++)
            {
                var min = i == 0 ? Vector2.zero : i == 1 ? Vector2.up : i == 2 ? Vector2.zero : Vector2.right;
                var max = i == 0 ? Vector2.right : i == 1 ? Vector2.one : i == 2 ? Vector2.up : Vector2.one;
                var edge = CreateRect(root, "Edge" + i, min, max);
                edge.sizeDelta = i < 2 ? new Vector2(0f, 1.25f) : new Vector2(1.25f, 0f);
                var image = edge.gameObject.AddComponent<Image>();
                BattleUITheme.EnsureSprite(image);
                image.raycastTarget = false;
                images[i] = image;
            }
            return images;
        }

        void BuildStorySupporters(Transform card, StoryEventTrustMark[] marks)
        {
            // Fixed three-person lane: no label, separators or visible empty slots.
            // It never competes with the scrollable mechanical effects above it.
            var lane = CreateRect(card, "SupporterLane", new Vector2(0.04f, 0.025f), new Vector2(0.96f, 0.29f));
            var used = new HashSet<string>();
            var filled = 0;
            for (var i = 0; i < 3; i++)
            {
                var slot = CreateRect(lane, "SupporterSlot" + i, new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f));
                StoryEventTrustMark chosen = null;
                TrustGaugeView gauge = null;
                while (filled < marks.Length)
                {
                    var mark = marks[filled++];
                    if (mark == null || !mark.Supporter) continue;
                    gauge = FindStoryMember(mark);
                    if (gauge == null || !used.Add(gauge.CharacterId ?? gauge.Name ?? "")) continue;
                    chosen = mark;
                    break;
                }
                if (chosen == null) continue;
                var face = CreateStoryFace(slot, "Face", gauge, new Vector2(0.01f, 0.36f), new Vector2(0.36f, 0.98f));
                var name = CreateLabel(slot, "SupporterName", gauge.Name ?? "", 17, TextAnchor.MiddleLeft,
                    new Vector2(0.4f, 0.5f), new Vector2(0.97f, 0.98f), StoryInk);
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 13;
                name.resizeTextMaxSize = 17;
                var speech = CreateStoryScroll(slot, "SupporterSpeech", new Vector2(0.01f, 0f), new Vector2(0.99f, 0.32f));
                var line = AddStoryReading(speech, "SupporterLine", chosen.Line ?? "", 17, new Color(0.82f, 0.77f, 0.68f, 1f));
                line.richText = false;
                _ = face;
            }
        }

        TrustGaugeView FindStoryMember(StoryEventTrustMark mark)
        {
            for (var i = 0; i < _storyParty.Length; i++)
            {
                var member = _storyParty[i];
                if (member == null) continue;
                if ((!string.IsNullOrEmpty(mark.CharacterId) && StarterCharacters.Same(mark.CharacterId, member.CharacterId))
                    || (string.IsNullOrEmpty(mark.CharacterId) && mark.Name == member.Name)) return member;
            }
            return null;
        }

        static Image CreateStoryFace(Transform parent, string name, TrustGaugeView gauge, Vector2 min, Vector2 max)
        {
            var bounds = CreateRect(parent, name, min, max);
            var frame = CreateRect(bounds, "Frame", Vector2.zero, Vector2.one);
            var fit = frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            var image = frame.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = new Color(0.77f, 0.71f, 0.6f, 0.9f);
            image.raycastTarget = false;
            var inset = CreateRect(frame, "Portrait", Vector2.zero, Vector2.one);
            inset.offsetMin = new Vector2(2f, 2f);
            inset.offsetMax = new Vector2(-2f, -2f);
            var face = inset.gameObject.AddComponent<Image>();
            ApplyPart(face, StoryPortrait(gauge), new Color(0.16f, 0.14f, 0.12f));
            face.preserveAspect = true;
            face.raycastTarget = false;
            return face;
        }

        void LayoutChoiceEffects(Transform content, StoryEventEffectLine[] effects, bool showChance)
        {
            var list = effects ?? Array.Empty<StoryEventEffectLine>();
            var rewardNote = false;
            for (var i = 0; i < list.Length; i++)
            {
                var line = list[i];
                if (line == null)
                    continue;
                if (line.Kind == "note")
                {
                    rewardNote = true;
                    continue;
                }
                if (line.Kind == "gap")
                {
                    var gap = CreateRect(content, "OutcomeGap", Vector2.zero, Vector2.one);
                    gap.gameObject.AddComponent<LayoutElement>().preferredHeight = 10f;
                    continue;
                }
                if (string.IsNullOrEmpty(line.Text))
                    continue;
                // Probability gets its own column; text wraps in the remaining width.
                // There are no rotated fallback icons or nested ContentSizeFitters.
                var row = CreateRect(content, "EffectRow" + i, Vector2.zero, Vector2.one);
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.spacing = 12f;
                layout.padding.left = rewardNote ? 24 : 0;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                if (showChance)
                {
                    var chance = AddStoryParagraph(row, "Chance", (line.Chance ?? "").TrimEnd(':'), 21,
                        StoryInk);
                    var column = chance.gameObject.AddComponent<LayoutElement>();
                    column.minWidth = 70f;
                    column.preferredWidth = 70f;
                    column.flexibleWidth = 0f;
                }
                var display = EventText.DisplayLine(line);
                if (rewardNote) display = "勝利後：" + display;
                var text = AddStoryParagraph(row, "Effect", display, 26, StoryInk);
                text.font = StorySerif;
                rewardNote = false;
                var element = text.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 0f;
                element.flexibleWidth = 1f;
            }
        }

        // TMP applies Japanese leading/following-character rules, so punctuation
        // cannot become an orphaned line. Natural preferred height feeds the scroll layout.
        static TMP_Text AddStoryReading(Transform parent, string name, string text, int size, Color color)
        {
            var label = BattleHudKit.Text(parent, name, text, BattleHudKit.SerifRegular,
                size, color, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = false;
            label.lineSpacing = 8f;
            return label;
        }

        static Text AddStoryParagraph(Transform parent, string name, string text, int size, Color color)
        {
            var label = CreateLabel(parent, name, text, size, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, color);
            label.font = BattleUITheme.Font;
            label.lineSpacing = 1.15f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        static RectTransform CreateStoryScroll(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = CreateRect(parent, name, min, max);
            var background = rect.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(background);
            background.color = new Color(1f, 1f, 1f, 0.001f);
            var scroll = rect.gameObject.AddComponent<StoryReadingScroll>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            var viewport = CreateRect(rect, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMax = new Vector2(-18f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect(viewport, "Content", new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 7f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var track = CreateRect(rect, "Scrollbar", new Vector2(1f, 0f), Vector2.one);
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(5f, 0f);
            var trackImage = track.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(trackImage);
            trackImage.color = new Color(0.26f, 0.22f, 0.17f, 0.5f);
            var handle = CreateRect(track, "Handle", Vector2.zero, Vector2.one);
            var handleImage = handle.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(handleImage);
            handleImage.color = new Color(0.64f, 0.51f, 0.31f, 1f);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        void BuildStoryStatus(Transform parent, StoryEventView view)
        {
            var bar = CreateRect(parent, "StatusBar", Vector2.zero, new Vector2(1f, 0.095f));
            var background = bar.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(background);
            background.color = new Color(0.035f, 0.028f, 0.025f, 0.95f);
            background.raycastTarget = false;
            var rule = CreateRect(bar, "TopLine", new Vector2(0.02f, 1f), new Vector2(0.98f, 1f));
            rule.sizeDelta = new Vector2(0f, 1f);
            var ruleImage = rule.gameObject.AddComponent<Image>();
            BattleUITheme.EnsureSprite(ruleImage);
            ruleImage.color = new Color(0.58f, 0.49f, 0.37f, 0.7f);
            ruleImage.raycastTarget = false;
            var hp = CreateLabel(bar, "Hp", $"HP {view.Hp}/{Mathf.Max(1, view.HpMax)}", 23, TextAnchor.MiddleLeft,
                new Vector2(0.035f, 0.12f), new Vector2(0.205f, 0.88f), StoryInk);
            var madness = CreateLabel(bar, "Madness", $"狂気 {view.Madness}/{Mathf.Max(1, view.MadnessMax)}", 23, TextAnchor.MiddleLeft,
                new Vector2(0.225f, 0.12f), new Vector2(0.405f, 0.88f), StoryInk);
            hp.resizeTextForBestFit = madness.resizeTextForBestFit = true;
            hp.resizeTextMinSize = madness.resizeTextMinSize = 17;
            hp.resizeTextMaxSize = madness.resizeTextMaxSize = 23;

            var count = Mathf.Min(_storyParty.Length, 3);
            const float width = 0.18f;
            for (var i = 0; i < count; i++)
            {
                var gauge = _storyParty[i];
                if (gauge == null) continue;
                var x = 0.43f + i * width;
                var slot = CreateRect(bar, "AffinitySlot" + i, new Vector2(x, 0.08f), new Vector2(x + width - 0.008f, 0.92f));
                CreateStoryFace(slot, "Face", gauge, new Vector2(0f, 0.05f), new Vector2(0.23f, 0.95f));
                var who = CreateLabel(slot, "Who" + i, gauge.Name ?? "", 19, TextAnchor.MiddleLeft,
                    new Vector2(0.27f, 0.52f), new Vector2(0.98f, 0.95f), StoryInk);
                who.resizeTextForBestFit = true;
                who.resizeTextMinSize = 14;
                who.resizeTextMaxSize = 19;
                var affinity = CreateLabel(slot, "Affinity" + i, "好感度 " + gauge.Trust, 20, TextAnchor.MiddleLeft,
                    new Vector2(0.27f, 0.02f), new Vector2(0.76f, 0.52f), StoryInk);
                affinity.resizeTextForBestFit = true;
                affinity.resizeTextMinSize = 14;
                affinity.resizeTextMaxSize = 20;
                var delta = CreateLabel(slot, "AffinityDelta" + i, "", 20, TextAnchor.MiddleLeft,
                    new Vector2(0.78f, 0.02f), new Vector2(1f, 0.52f), StoryInk);
                delta.horizontalOverflow = HorizontalWrapMode.Overflow;
                _storyTrust.Add(new StoryTrustReadout { Id = gauge.CharacterId, Name = gauge.Name ?? "", Label = delta });
            }
        }

        void PreviewTrust(int cardIndex, StoryEventTrustMark[] marks)
        {
            _trustHoverCard = cardIndex;
            ApplyTrustPreview(marks);
        }

        void EndTrustPreview(int cardIndex)
        {
            if (_trustHoverCard != cardIndex)
                return;
            _trustHoverCard = -1;
            ApplyTrustPreview(null);
        }

        void ApplyTrustPreview(StoryEventTrustMark[] marks)
        {
            for (var i = 0; i < _storyTrust.Count; i++)
            {
                var slot = _storyTrust[i];
                if (slot?.Label == null)
                    continue;
                var delta = 0;
                if (marks != null)
                {
                    for (var m = 0; m < marks.Length; m++)
                    {
                        var mark = marks[m];
                        if (mark != null && ((!string.IsNullOrEmpty(mark.CharacterId) && StarterCharacters.Same(mark.CharacterId, slot.Id))
                            || (string.IsNullOrEmpty(mark.CharacterId) && mark.Name == slot.Name)))
                            delta += mark.Delta;
                    }
                }

                slot.Label.text = TrustDeltaText(delta);
            }
        }

        static string TrustDeltaText(int delta)
        {
            if (delta > 0)
                return $"<color={EventText.BenefitColor}>+{delta}</color>";
            if (delta < 0)
                return $"<color={EventText.RiskColor}>{delta}</color>";
            return "";
        }

        static Sprite StoryPortrait(TrustGaugeView gauge)
        {
            if (gauge == null)
                return null;
            var named = BattlePortraitResolver.LoadCharacter(gauge.Name);
            if (named != null)
                return named;
            return FaceSprite(gauge.CharacterId, gauge.Portrait);
        }

        static void Emphasize(Text label, Font font, int size)
        {
            if (label == null)
                return;
            label.font = font != null ? font : BattleUITheme.FontBold;
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            BattleUITheme.AddTextShadow(label, new Color(0f, 0f, 0f, 0.92f), new Vector2(1.6f, -1.6f));
        }

        static string FormatCount(int value)
        {
            var negative = value < 0;
            var text = Mathf.Abs(value).ToString();
            for (var i = text.Length - 3; i > 0; i -= 3)
                text = text.Insert(i, ",");
            return negative ? "-" + text : text;
        }

        public void ShowOpinion(OpinionScreenView view, Action<int> onPick)
        {
            EnsureRoot(null);
            HideModal();
            _opinionGauges.Clear();
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            var chrome = TypingRoguelike.Data.OpinionChrome.LoadOrFallback();
            _panel = CreateFullScreen("Opinion", new Color(0.012f, 0.014f, 0.035f, 1f), PathSprite("bg_corridor"));
            var window = BattleUIChrome.CreateLuxuryWindow(
                _panel.transform, "OpinionWindow", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                "", BattleUITheme.Gold);
            var body = window.Find("Body");
            if (body == null || view == null)
                return;

            var headerIcon = CreateRect(body, "TileIcon", new Vector2(0.03f, 0.9f), new Vector2(0.09f, 0.99f));
            var headerImage = headerIcon.gameObject.AddComponent<Image>();
            ApplyPart(headerImage, view.TileIcon, new Color(0.35f, 0.55f, 0.72f, 1f));
            headerImage.preserveAspect = view.TileIcon != null;
            var title = CreateLabel(body, "SceneTitle", view.SceneTitle ?? "", 28, TextAnchor.MiddleLeft,
                new Vector2(0.11f, 0.9f), new Vector2(0.97f, 0.99f), BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;

            BuildOpinionPanels(body, view.Gauges, chrome);

            var scenePlate = CreateRect(body, "SceneTextPlate", new Vector2(0.03f, 0.62f), new Vector2(0.97f, 0.73f));
            var sceneImage = scenePlate.gameObject.AddComponent<Image>();
            ApplyPart(sceneImage, OpinionSprite("scene_band"), new Color(0.08f, 0.07f, 0.06f, 0.72f));
            CreateLabel(scenePlate, "SceneText", view.SceneText ?? "", 22, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.86f), BattleUITheme.TextPrimary);

            var bubbles = view.Bubbles ?? Array.Empty<OpinionBubbleView>();
            for (var choice = 0; choice < 2; choice++)
            {
                var column = new List<OpinionBubbleView>();
                foreach (var bubble in bubbles)
                {
                    if (bubble != null && bubble.ChoiceIndex == choice)
                        column.Add(bubble);
                }

                if (column.Count == 0)
                    continue;
                var xMin = choice == 0 ? 0.03f : 0.51f;
                var xMax = choice == 0 ? 0.49f : 0.97f;
                var width = (xMax - xMin) / column.Count;
                for (var i = 0; i < column.Count; i++)
                {
                    var left = xMin + i * width;
                    BuildSpeaker(body, column[i], chrome, left + 0.008f, 0.24f, left + width - 0.008f, 0.60f);
                }
            }

            if (view.Agreed)
            {
                var band = CreateRect(body, "AgreeBand", new Vector2(0.28f, 0.175f), new Vector2(0.72f, 0.225f));
                var bandImage = band.gameObject.AddComponent<Image>();
                ApplyPart(bandImage, OpinionSprite("btn_choice_gold"), new Color(0.86f, 0.68f, 0.22f, 0.96f));
                var bandLabel = CreateLabel(band, "Text", "意見一致", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(0.16f, 0.08f, 0.02f, 1f));
                bandLabel.fontStyle = FontStyle.Bold;
            }

            var buttons = view.Buttons ?? Array.Empty<OpinionButtonView>();
            for (var i = 0; i < buttons.Length && i < 2; i++)
            {
                var button = buttons[i];
                var xMin = i == 0 ? 0.03f : 0.51f;
                var xMax = i == 0 ? 0.49f : 0.97f;
                var plate = CreateOpinionButton(body, button, chrome, new Vector2(xMin, 0.02f), new Vector2(xMax, 0.16f),
                    () => onPick?.Invoke(button.ChoiceIndex));
                var previews = button.Previews;
                BindHover(plate.gameObject, () => PreviewOpinion(previews), ClearOpinionPreview);
            }
        }

        void BuildOpinionPanels(Transform parent, TrustGaugeView[] gauges, TypingRoguelike.Data.OpinionChrome chrome)
        {
            if (parent == null || gauges == null)
                return;
            var count = Mathf.Min(gauges.Length, 3);
            if (count == 0)
                return;
            var slot = 0.94f / count;
            for (var i = 0; i < count; i++)
            {
                var gauge = gauges[i];
                var xMin = 0.03f + i * slot;
                var xMax = xMin + slot - 0.015f;
                var panel = CreateRect(parent, "TrustPanel_" + gauge.CharacterId, new Vector2(xMin, 0.745f), new Vector2(xMax, 0.89f));
                var panelImage = panel.gameObject.AddComponent<Image>();
                var panelSprite = OpinionSprite("trust_panel");
                ApplyPart(panelImage, panelSprite, gauge.Dim ? new Color(0.1f, 0.1f, 0.12f, 0.45f) : new Color(0.14f, 0.11f, 0.09f, 0.88f));
                if (panelSprite != null && gauge.Dim)
                    panelImage.color = new Color(0.62f, 0.62f, 0.62f, 1f);
                if (gauge.Dim)
                {
                    var group = panel.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0.72f;
                }

                var frame = CreateRect(panel, "FaceFrame", new Vector2(0.04f, 0.28f), new Vector2(0.28f, 0.94f));
                var frameImage = frame.gameObject.AddComponent<Image>();
                ApplyPart(frameImage, Part(chrome != null ? chrome.faceFrame : null, "face_frame"), new Color(0.55f, 0.4f, 0.22f, 1f));
                frameImage.preserveAspect = true;
                var face = CreateRect(frame, "Face", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
                var faceImage = face.gameObject.AddComponent<Image>();
                ApplyPart(faceImage, FaceSprite(gauge.CharacterId, gauge.Portrait), FaceColor(gauge.CharacterId));
                faceImage.preserveAspect = true;

                var textColor = gauge.Dim ? BattleUITheme.TextMuted : BattleUITheme.TextPrimary;
                CreateLabel(panel, "Name", $"{gauge.Name}（{gauge.Personality}）", 16, TextAnchor.UpperLeft,
                    new Vector2(0.32f, 0.72f), new Vector2(0.98f, 0.96f), textColor);
                var number = CreateLabel(panel, "Value", gauge.Trust.ToString(), 18, TextAnchor.UpperLeft,
                    new Vector2(0.32f, 0.54f), new Vector2(0.98f, 0.72f), textColor);
                number.fontStyle = FontStyle.Bold;
                var notice = CreateLabel(panel, "TierNotice", gauge.Dim ? "今回は見守り" : "", 12, TextAnchor.UpperLeft,
                    new Vector2(0.32f, 0.38f), new Vector2(0.98f, 0.52f), new Color(1f, 0.86f, 0.45f, 1f));
                CreateLabel(panel, "Passive", gauge.PassiveLabel ?? "未発動",
                    13, TextAnchor.LowerLeft, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.24f), textColor);

                var track = CreateRect(panel, "GaugeTrack", new Vector2(0.32f, 0.26f), new Vector2(0.96f, 0.36f));
                var trackImage = track.gameObject.AddComponent<Image>();
                ApplyPart(trackImage, chrome != null ? chrome.gaugeTrack : null, new Color(0.05f, 0.04f, 0.035f, 0.95f));
                var fillSprite = Part(chrome != null ? chrome.gaugeFill : null, "gauge_fill");
                var fillColor = fillSprite == null
                    ? (gauge.Dim ? new Color(0.45f, 0.32f, 0.18f, 0.7f) : new Color(0.93f, 0.62f, 0.28f, 1f))
                    : (gauge.Dim ? new Color(0.62f, 0.62f, 0.62f, 1f) : Color.white);
                BuildMeter(track, fillSprite, Mathf.Clamp01(gauge.Trust / 100f), fillColor, out var fillImage, out var previewImage);

                _opinionGauges.Add(new OpinionGaugeWidget
                {
                    Id = gauge.CharacterId,
                    Trust = gauge.Trust,
                    Number = number,
                    Notice = notice,
                    Watch = gauge.Dim ? "今回は見守り" : "",
                    CurrentFill = fillImage,
                    PreviewFill = previewImage,
                    FillColor = fillImage.color
                });
            }
        }

        void BuildSpeaker(Transform parent, OpinionBubbleView bubble, TypingRoguelike.Data.OpinionChrome chrome, float xMin, float yMin, float xMax, float yMax)
        {
            var slot = CreateRect(parent, "Speaker_" + bubble.CharacterId, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            var wide = xMax - xMin > 0.3f;
            var frame = CreateRect(slot, "FaceFrame", wide ? new Vector2(0.02f, 0.18f) : new Vector2(0.28f, 0.58f), wide ? new Vector2(0.28f, 0.96f) : new Vector2(0.72f, 0.98f));
            var frameImage = frame.gameObject.AddComponent<Image>();
            ApplyPart(frameImage, Part(chrome != null ? chrome.faceFrame : null, "face_frame"), new Color(0.55f, 0.4f, 0.22f, 1f));
            frameImage.preserveAspect = true;
            var face = CreateRect(frame, "Face", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            var faceImage = face.gameObject.AddComponent<Image>();
            ApplyPart(faceImage, FaceSprite(bubble.CharacterId, bubble.Portrait), FaceColor(bubble.CharacterId));
            faceImage.preserveAspect = true;
            CreateLabel(slot, "Name", bubble.Name ?? "", 14, TextAnchor.UpperCenter,
                wide ? new Vector2(0.02f, 0.02f) : new Vector2(0.05f, 0.42f),
                wide ? new Vector2(0.28f, 0.16f) : new Vector2(0.95f, 0.56f),
                BattleUITheme.TextPrimary);

            var bubbleMin = wide ? new Vector2(0.32f, 0.12f) : new Vector2(0.04f, 0.02f);
            var bubbleMax = wide ? new Vector2(0.98f, 0.9f) : new Vector2(0.96f, 0.4f);
            var plate = CreateRect(slot, "BubbleFrame", bubbleMin, bubbleMax);
            ApplyPart(plate.gameObject.AddComponent<Image>(), Part(chrome != null ? chrome.bubbleFrame : null, "bubble"), new Color(0.96f, 0.93f, 0.86f, 0.96f));
            var tail = CreateRect(plate, "BubbleTail", new Vector2(0.38f, -0.16f), new Vector2(0.62f, 0f));
            ApplyPart(tail.gameObject.AddComponent<Image>(), Part(chrome != null ? chrome.bubbleTail : null, "bubble_tail"), new Color(0.96f, 0.93f, 0.86f, 0.96f));
            CreateLabel(plate, "Line", bubble.Line, wide ? 24 : 18, TextAnchor.MiddleLeft,
                new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.84f), new Color(0.12f, 0.08f, 0.06f, 1f));
        }

        RectTransform CreateOpinionButton(Transform parent, OpinionButtonView button, TypingRoguelike.Data.OpinionChrome chrome, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var rt = CreateRect(parent, "ChoiceButton_" + button.ChoiceIndex, anchorMin, anchorMax);
            var image = rt.gameObject.AddComponent<Image>();
            var sprite = button.Emphasized
                ? Part(chrome != null ? chrome.choiceButtonAgreed : null, "btn_choice_gold")
                : button.Dimmed
                    ? Part(chrome != null ? chrome.choiceButtonDim : null, "btn_choice_dim")
                    : Part(chrome != null ? chrome.choiceButton : null, "btn_choice");
            var color = button.Emphasized
                ? new Color(0.93f, 0.74f, 0.28f, 1f)
                : button.Dimmed
                    ? new Color(0.18f, 0.16f, 0.16f, 1f)
                    : new Color(0.32f, 0.2f, 0.1f, 1f);
            ApplyPart(image, sprite, color);
            image.raycastTarget = true;
            if (sprite != null)
                image.color = Color.white;
            var uiButton = rt.gameObject.AddComponent<Button>();
            uiButton.targetGraphic = image;
            uiButton.onClick.AddListener(() => onClick?.Invoke());
            var textColor = button.Dimmed ? BattleUITheme.TextMuted : (button.Emphasized ? new Color(0.16f, 0.08f, 0.02f, 1f) : Color.white);
            var label = button.Title ?? "";
            if (!string.IsNullOrEmpty(button.Effect))
                label += $"\n{button.Effect}";
            CreateLabel(rt, "Label", label, 20, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.96f), textColor)
                .fontStyle = FontStyle.Bold;
            var faces = button.Faces ?? Array.Empty<OpinionFaceView>();
            for (var i = 0; i < faces.Length; i++)
            {
                var faceView = faces[i];
                var left = 0.08f + i * 0.12f;
                var chip = CreateRect(rt, "FaceFrame", new Vector2(left, 0.06f), new Vector2(left + 0.1f, 0.28f));
                var chipFrame = chip.gameObject.AddComponent<Image>();
                ApplyPart(chipFrame, Part(chrome != null ? chrome.faceFrame : null, "face_frame"), new Color(0.55f, 0.4f, 0.22f, 1f));
                chipFrame.preserveAspect = true;
                var chipFace = CreateRect(chip, "Face", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
                var chipFaceImage = chipFace.gameObject.AddComponent<Image>();
                ApplyPart(chipFaceImage, FaceSprite(faceView.CharacterId, faceView.Portrait), FaceColor(faceView.CharacterId));
                chipFaceImage.preserveAspect = true;
            }

            return rt;
        }

        void PreviewOpinion(OpinionTrustPreview[] previews)
        {
            foreach (var widget in _opinionGauges)
            {
                OpinionTrustPreview hit = null;
                if (previews != null)
                {
                    foreach (var preview in previews)
                    {
                        if (preview != null && preview.CharacterId == widget.Id)
                            hit = preview;
                    }
                }

                if (hit == null || hit.To == hit.From)
                {
                    widget.Number.text = widget.Trust.ToString();
                    widget.Notice.text = widget.Watch;
                    widget.CurrentFill.fillAmount = Mathf.Clamp01(widget.Trust / 100f);
                    widget.CurrentFill.color = widget.FillColor;
                    widget.PreviewFill.fillAmount = 0f;
                    continue;
                }

                widget.Number.text = $"{hit.From}→{hit.To}";
                widget.Notice.text = hit.Notice ?? "";
                if (hit.To > hit.From)
                {
                    widget.PreviewFill.color = new Color(1f, 0.95f, 0.55f, 1f);
                    widget.PreviewFill.fillAmount = Mathf.Clamp01(hit.To / 100f);
                    widget.CurrentFill.fillAmount = Mathf.Clamp01(hit.From / 100f);
                    widget.CurrentFill.color = widget.FillColor;
                }
                else
                {
                    widget.PreviewFill.color = new Color(0.86f, 0.16f, 0.14f, 0.95f);
                    widget.PreviewFill.fillAmount = Mathf.Clamp01(hit.From / 100f);
                    widget.CurrentFill.fillAmount = Mathf.Clamp01(hit.To / 100f);
                    widget.CurrentFill.color = widget.FillColor;
                }
            }
        }

        void ClearOpinionPreview() => PreviewOpinion(null);

        static void BindHover(GameObject target, Action onEnter, Action onExit)
        {
            var trigger = target.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => onEnter?.Invoke());
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => onExit?.Invoke());
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        static void ApplyPart(Image image, Sprite sprite, Color placeholder)
        {
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                return;
            }

            image.color = placeholder;
            BattleUITheme.EnsureSprite(image);
        }

        static Color FaceColor(string characterId) => StarterCharacters.PortraitKey(characterId) switch
        {
            "momo" => new Color(0.42f, 0.7f, 0.52f, 1f),
            "nagi" => new Color(0.42f, 0.46f, 0.82f, 1f),
            _ => new Color(0.78f, 0.32f, 0.28f, 1f)
        };

        sealed class StoryTrustReadout
        {
            public string Name;
            public string Id;
            public Text Label;
        }

        sealed class OpinionGaugeWidget
        {
            public string Id;
            public int Trust;
            public Text Number;
            public Text Notice;
            public string Watch;
            public Image CurrentFill;
            public Image PreviewFill;
            public Color FillColor;
        }

        public void ShowTrustDebug(TrustGaugeView[] gauges, Action<string, int> onSet)
        {
            _trustGauges = gauges;
            _trustSet = onSet;
            HideModal();
            _modal = CreateFullScreen("TrustDebug", new Color(0.005f, 0.008f, 0.02f, 0.92f));
            var window = BattleUIChrome.CreateLuxuryWindow(
                _modal.transform, "TrustDebugWindow", new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.9f),
                "信頼度", BattleUITheme.Arcane);
            var body = window.Find("Body");
            if (body == null)
                return;
            var list = gauges ?? Array.Empty<TrustGaugeView>();
            for (var i = 0; i < list.Length; i++)
            {
                var gauge = list[i];
                var yMax = 0.92f - i * 0.28f;
                CreateLabel(body, "Who" + i, $"{gauge.Name}（{gauge.Personality}） {gauge.Trust}  {gauge.PassiveLabel}",
                    18, TextAnchor.MiddleLeft, new Vector2(0.04f, yMax - 0.08f), new Vector2(0.96f, yMax), BattleUITheme.TextPrimary);
                var steps = new[] { 0, 25, 50, 75, 100 };
                for (var s = 0; s < steps.Length; s++)
                {
                    var value = steps[s];
                    var id = gauge.CharacterId;
                    var xMin = 0.04f + s * 0.18f;
                    CreateButton(body, value.ToString(), new Vector2(xMin, yMax - 0.22f), new Vector2(xMin + 0.16f, yMax - 0.1f),
                        () =>
                        {
                            HideModal();
                            _trustSet?.Invoke(id, value);
                        });
                }
            }

            CreateButton(body, "閉じる", new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.1f), HideModal);
        }

        public GameObject BuildStoryPrefabSource()
        {
            SuppressPrefab = true;
            try
            {
                var view = new StoryEventView
                {
                    Badge = "出来事",
                    Title = "見本の出来事",
                    Body = "鏡の中だけ、三人がこちらに背を向けている。手前には自分と同じ姿が立ち、鏡の縁に指をかけていた。",
                    Aside = "本文と仲間の補足はプレイ中に差し替わります。",
                    ShowChance = true,
                    Category = "event",
                    Coins = 12,
                    Materials = 3,
                    Hp = 1000,
                    HpMax = 1000,
                    Madness = 0,
                    MadnessMax = 100,
                    Choices = new[]
                    {
                        new StoryEventChoiceView { Label = "指を叩いて引き剥がす" },
                        new StoryEventChoiceView { Label = "鏡を壁へ向ける" }
                    },
                    Party = new[]
                    {
                        new TrustGaugeView
                        {
                            CharacterId = StarterCharacters.Chara01,
                            Name = StarterCharacters.Shown(StarterCharacters.Chara01)
                        },
                        new TrustGaugeView
                        {
                            CharacterId = StarterCharacters.Chara02,
                            Name = StarterCharacters.Shown(StarterCharacters.Chara02)
                        },
                        new TrustGaugeView
                        {
                            CharacterId = StarterCharacters.Chara03,
                            Name = StarterCharacters.Shown(StarterCharacters.Chara03)
                        }
                    }
                };
                foreach (var choice in view.Choices)
                {
                    choice.Effects = new[] {
                        new StoryEventEffectLine { Text = "HP +25%", Chance = "60%" },
                        new StoryEventEffectLine { Kind = "gap" } };
                    choice.Marks = Array.ConvertAll(view.Party, member => new StoryEventTrustMark {
                        CharacterId = member.CharacterId, Name = member.Name, Supporter = true, Line = "任せてください。" });
                }
                ShowStoryEvent(view, null);
                _panel.AddComponent<StoryEventPrefabView>().CaptureReferences();
                return _panel;
            }
            finally
            {
                SuppressPrefab = false;
            }
        }

        static Text FindText(Transform root, string name)
        {
            var target = FindNamed(root, name);
            if (target == null)
                return null;
            var text = target.GetComponent<Text>();
            return text != null ? text : target.GetComponentInChildren<Text>(true);
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        void EnsureRoot(Transform host)
        {
            if (_root != null)
                return;

            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("LayerPathCanvas");
            go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            _root = go;
        }

        GameObject CreateFullScreen(string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.type = Image.Type.Simple;
            }
            else
            {
                image.color = color;
                BattleUITheme.EnsureSprite(image);
            }

            return go;
        }

        static void Shade(Transform parent, string name, Vector2 min, Vector2 max, float alpha)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.02f, 0.012f, 0.015f, alpha);
            image.raycastTarget = false;
            BattleUITheme.EnsureSprite(image);
        }

        void CreateQuietButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Action onClick, Color? color = null)
        {
            var rt = CreateRect(parent, "Button", anchorMin, anchorMax);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            BattleUITheme.EnsureSprite(image);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var text = CreateLabel(rt, "Text", label, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, color ?? BattleUITheme.Gold);
            text.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(text, new Color(0f, 0f, 0f, 0.85f), new Vector2(1.5f, -1.5f));
        }

        void BuildMeter(RectTransform track, Sprite fillSprite, float amount, Color fillColor, out Image fill, out Image preview)
        {
            var innerMin = new Vector2(0.012f, 0.18f);
            var innerMax = new Vector2(0.988f, 0.82f);
            var previewRt = CreateRect(track, "GaugePreview", innerMin, innerMax);
            preview = previewRt.gameObject.AddComponent<Image>();
            ApplyPart(preview, fillSprite, new Color(1f, 0.92f, 0.45f, 1f));
            preview.type = Image.Type.Filled;
            preview.fillMethod = Image.FillMethod.Horizontal;
            preview.fillAmount = 0f;
            preview.raycastTarget = false;
            var fillRt = CreateRect(track, "GaugeFill", innerMin, innerMax);
            fill = fillRt.gameObject.AddComponent<Image>();
            ApplyPart(fill, fillSprite, fillColor);
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(amount);
            fill.raycastTarget = false;
            foreach (var mark in new[] { 0.25f, 0.5f, 0.75f })
            {
                var x = Mathf.Lerp(innerMin.x, innerMax.x, mark);
                var tick = CreateRect(track, "GaugeTick", new Vector2(x, 0f), new Vector2(x, 1f));
                tick.offsetMin = new Vector2(-1.5f, 0f);
                tick.offsetMax = new Vector2(1.5f, 0f);
                var tickImage = tick.gameObject.AddComponent<Image>();
                tickImage.color = new Color(1f, 0.96f, 0.86f, 0.95f);
                BattleUITheme.EnsureSprite(tickImage);
                tickImage.raycastTarget = false;
                var outline = tick.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.08f, 0.04f, 0.02f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        static Sprite Art(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (ArtCache.TryGetValue(path, out var sprite))
                return sprite;
            sprite = Resources.Load<Sprite>(path);
            ArtCache[path] = sprite;
            return sprite;
        }

        static Sprite PathSprite(string name) => Art("Run/Path/" + name);

        static Sprite OpinionSprite(string name) => Art("Run/Opinion/" + name);

        static Sprite Part(Sprite assigned, string name) => assigned != null ? assigned : OpinionSprite(name);

        static Sprite FaceSprite(string characterId, Sprite assigned)
        {
            if (assigned != null)
                return assigned;
            var portraitId = StarterCharacters.PortraitKey(characterId);
            return string.IsNullOrEmpty(portraitId) ? null : OpinionSprite("portrait_" + portraitId);
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        void CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Action onClick, Color? background = null)
        {
            var rt = CreateRect(parent, "Button", anchorMin, anchorMax);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = background ?? new Color(0.16f, 0.1f, 0.08f, 0.96f);
            BattleUITheme.EnsureSprite(image);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var text = CreateLabel(rt, "Text", label, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            text.fontStyle = FontStyle.Bold;
        }

        static Text CreateLabel(Transform parent, string name, string text, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = CreateRect(parent, name, anchorMin, anchorMax);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static string SquareName(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Combat => "戦闘",
            DungeonNodeType.Rest => "回復",
            DungeonNodeType.Merchant => "店",
            DungeonNodeType.Treasure => "宝",
            DungeonNodeType.Event => "イベント",
            DungeonNodeType.Trap => "罠",
            DungeonNodeType.MidBoss => "中ボス",
            DungeonNodeType.Boss => "ボス",
            DungeonNodeType.Elevator => "エレベーター",
            _ => "マス"
        };

        static Color SquareColor(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Combat => new Color(0.55f, 0.22f, 0.22f, 1f),
            DungeonNodeType.Rest => new Color(0.16f, 0.42f, 0.32f, 1f),
            DungeonNodeType.Merchant => new Color(0.45f, 0.34f, 0.12f, 1f),
            DungeonNodeType.Treasure => new Color(0.12f, 0.36f, 0.48f, 1f),
            DungeonNodeType.Event => new Color(0.36f, 0.22f, 0.5f, 1f),
            DungeonNodeType.Trap => new Color(0.42f, 0.28f, 0.1f, 1f),
            DungeonNodeType.MidBoss => new Color(0.62f, 0.18f, 0.24f, 1f),
            DungeonNodeType.Boss => new Color(0.72f, 0.12f, 0.16f, 1f),
            DungeonNodeType.Elevator => new Color(0.72f, 0.52f, 0.16f, 1f),
            _ => new Color(0.25f, 0.25f, 0.28f, 1f)
        };
    }

}
