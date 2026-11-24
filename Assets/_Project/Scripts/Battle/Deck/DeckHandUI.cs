using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle.Deck
{
    public sealed class DeckHandUIView : MonoBehaviour
    {
        const float CardWidth = 313f;
        const float CardHeight = 150f;
        const string Parts = "UI/HandParts/parts/";
        // layout_v6.json, mock C. Card layers retain their 1x sprite coordinates.
        const float HandLeft = 376f;
        const float HandTop = 880f;
        const float DisplayWidth = 376f;
        const float DisplayHeight = 180f;
        const float HandGap = 20f;
        const float NearLift = 17f;
        const float ReadyLift = 34f;

        static Vector2 CardPosition(int index) => new(HandLeft + index * (DisplayWidth + HandGap), -HandTop);
        static readonly Color Silver = new Color32(236, 242, 252, 255);
        static readonly Color Gold = new Color32(255, 233, 176, 255);
        static readonly Color Rainbow = new Color32(234, 216, 255, 255);
        static readonly Color EffectColor = new Color32(240, 236, 246, 255);
        static readonly Regex EffectNumbers = new(@"[+-]?\d+");
        static readonly Dictionary<string, Sprite> Sprites = new();

        public sealed class WordGuide
        {
            const int MaxLines = 3;
            const float Width = 250f;
            const float TextLeft = 51f;
            const float TextWidth = 191f;
            const float LineHeight = 22f;
            const float FirstLineHeight = 26f;
            const float Gap = 4f;
            static readonly Regex WrapTokens = new(@"(?=[（＋])");
            static readonly Color Border = new Color32(110, 100, 110, 150);
            static readonly Color Background = new Color32(13, 11, 17, 230);
            static Sprite _panel;

            sealed class Chip
            {
                public readonly Image Frame, Icon, Gem;
                public readonly TMP_Text State;
                public readonly TMP_Text[] Body = new TMP_Text[MaxLines];

                public Chip(RectTransform word, int index)
                {
                    Frame = GuidePanel(word, $"WordGuide{index}", Width);
                    var root = Frame.rectTransform;
                    Icon = Picture(root, "Type", 3, 1, 24, 24);
                    Gem = Picture(root, "Color", 30, 5, 16, 16);
                    for (var i = 0; i < Body.Length; i++)
                        Body[i] = GuideLabel(root, $"Line{i}", TextLeft, i * LineHeight, TextWidth);
                    State = GuideLabel(root, "State", TextLeft, 0, TextWidth);
                    root.gameObject.SetActive(false);
                }
            }

            readonly Chip[] _chips = new Chip[MaxLines];
            readonly Image _overflow;
            readonly TMP_Text _overflowLabel;

            public IReadOnlyList<DeckWordPrediction> Predictions { get; private set; }

            public WordGuide(RectTransform word)
            {
                for (var i = 0; i < _chips.Length; i++)
                    _chips[i] = new Chip(word, i);
                _overflow = GuidePanel(word, "MoreGuides", 40);
                _overflowLabel = GuideLabel(_overflow.rectTransform, "Count", 0, 0, 40);
                _overflowLabel.alignment = TextAlignmentOptions.Center;
                _overflow.gameObject.SetActive(false);
            }

            public void Paint(IReadOnlyList<DeckWordPrediction> predictions)
            {
                Predictions = predictions;
                foreach (var chip in _chips)
                    chip.Frame.gameObject.SetActive(false);

                var shown = 0;
                var usedLines = 0;
                var top = 120f;
                while (shown < predictions.Count && usedLines < MaxLines)
                {
                    var chip = _chips[shown];
                    var lineCount = PaintChip(chip, predictions[shown]);
                    if (usedLines + lineCount > MaxLines)
                        break;
                    var height = FirstLineHeight + (lineCount - 1) * LineHeight;
                    chip.Frame.rectTransform.anchoredPosition = new Vector2(0, -top);
                    chip.Frame.rectTransform.sizeDelta = new Vector2(Width, height);
                    chip.Frame.gameObject.SetActive(true);
                    top += height + Gap;
                    usedLines += lineCount;
                    shown++;
                }

                var extra = predictions.Count - shown;
                _overflow.gameObject.SetActive(extra > 0);
                if (extra > 0)
                {
                    _overflow.rectTransform.anchoredPosition = new Vector2(Width - 40, -top);
                    _overflowLabel.text = $"+{extra}";
                }
            }

            static int PaintChip(Chip chip, DeckWordPrediction prediction)
            {
                var ready = prediction.Remaining == 0;
                var state = ready ? "発動" : $"あと{prediction.Remaining}";
                var stateWidth = Measure(chip.State, state) + 1f;
                var tokens = WrapTokens.Split($"{prediction.Effect}（{prediction.Name}）");
                var stateOnFirst = Measure(chip.Body[0], tokens[0]) + stateWidth + Gap <= TextWidth;
                var firstWidth = stateOnFirst ? TextWidth - stateWidth - Gap : TextWidth;

                var lines = new List<string> { tokens[0] };
                for (var i = 1; i < tokens.Length; i++)
                {
                    var combined = lines[^1] + tokens[i];
                    if (Measure(chip.Body[0], combined) <= (lines.Count == 1 ? firstWidth : TextWidth))
                        lines[^1] = combined;
                    else
                        lines.Add(tokens[i]);
                }

                var stateLine = stateOnFirst ? 0 : lines.Count - 1;
                var stateX = TextLeft + TextWidth - stateWidth;
                if (!stateOnFirst)
                {
                    var lastWidth = Measure(chip.Body[0], lines[^1]);
                    if (lastWidth + Gap + stateWidth > TextWidth)
                    {
                        lines.Add(string.Empty);
                        stateLine++;
                        stateX = TextLeft;
                    }
                    else
                        stateX = TextLeft + lastWidth + Gap;
                }

                if (lines.Count > MaxLines)
                    return lines.Count;

                var icon = prediction.Type switch
                {
                    DeckCardType.Shield => "defense",
                    DeckCardType.Buff => "buff",
                    _ => "attack"
                };
                chip.Icon.sprite = Resources.Load<Sprite>($"UI/HandParts/icons/icon_{icon}_24");
                chip.Icon.enabled = chip.Icon.sprite != null;
                Set(chip.Gem, Gem(prediction.Color, true));
                for (var i = 0; i < chip.Body.Length; i++)
                {
                    var body = chip.Body[i];
                    body.text = i < lines.Count ? lines[i] : string.Empty;
                    body.rectTransform.sizeDelta = new Vector2(i == stateLine ? Mathf.Max(0, stateX - TextLeft - Gap) : TextWidth,
                        FirstLineHeight);
                }

                chip.State.text = state;
                chip.State.color = ready ? new Color32(255, 214, 96, 255) : new Color32(161, 156, 171, 255);
                chip.State.rectTransform.anchoredPosition = new Vector2(stateX, -stateLine * LineHeight);
                chip.State.rectTransform.sizeDelta = new Vector2(stateWidth, FirstLineHeight);
                return lines.Count;
            }

            static float Measure(TMP_Text label, string value) => label.GetPreferredValues(value, 1000f, 100f).x;

            static TMP_Text GuideLabel(Transform parent, string name, float x, float y, float width)
            {
                var text = Label(parent, name, x, y, width, FirstLineHeight, 17, Silver);
                text.fontSizeMin = 12;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                return text;
            }

            static Image GuidePanel(Transform parent, string name, float width)
            {
                if (_panel == null)
                    _panel = CreatePanelSprite();
                var image = Picture(parent, name, 0, 0, width, FirstLineHeight);
                image.sprite = _panel;
                image.type = Image.Type.Sliced;
                return image;
            }

            static Sprite CreatePanelSprite()
            {
                const int size = 26;
                const float radius = 12f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius));
                    var dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius));
                    var distance = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    var color = Color.Lerp(Border, Background, Mathf.Clamp01(distance - 0.5f));
                    color.a *= Mathf.Clamp01(distance + 0.5f);
                    pixels[y * size + x] = color;
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, Vector4.one * 12f);
            }
        }

        sealed class SlotView
        {
            public DeckHandSlot Model;
            public int PaintedStack;
            public CanvasGroup Fade;
            public RectTransform DrawBack;
            public RectTransform Root;
            public RectTransform Card;
            public Image Empty, GuideGlow, Glow, Ghost, Art, Frame, Highlight, Backing, Badge, Tag;
            public TMP_Text Name, Effect;
            public readonly Image[] Stars = new Image[3];
            public readonly Image[] Gems = new Image[OrbStock.MaxLength];
        }

        readonly SlotView[] _slots = new SlotView[DeckCombatTuning.HandSize];
        readonly Image[] _stockGems = new Image[OrbStock.MaxLength];
        TMP_Text _draw, _discard, _shield, _hp, _san;
        Image _hpFill, _sanFill;
        TMP_Text _shieldCrack;
        Coroutine _refillPresentation;
        bool _changingHand;
        int _wordGuideHighlightMask;
        Image _shieldIcon, _shieldCrackIcon;
        static Sprite _shieldSprite;

        public static DeckHandUIView Build(RectTransform parent)
        {
            var host = Place(parent, "DeckHand", 0, 0, 1920, 1080);
            var view = host.gameObject.AddComponent<DeckHandUIView>();
            view.BuildHud();
            for (var i = 0; i < view._slots.Length; i++)
            {
                var pos = CardPosition(i);
                var slot = Place(host, $"HandSlot{i}", pos.x, -pos.y, CardWidth, CardHeight);
                slot.localScale = new Vector3(DisplayWidth / CardWidth, DisplayHeight / CardHeight, 1f);
                view._slots[i] = BuildCard(slot);
            }
            return view;
        }

        void BuildHud()
        {
            var portrait = Picture(transform, "Portrait", 52, 790, 132, 132);
            portrait.sprite = Resources.Load<Sprite>("Battle/Portraits/Katana/Sprites/head");
            portrait.enabled = portrait.sprite != null;
            portrait.preserveAspect = true;

            _hp = BuildMeter("HP", 929, new Color32(105, 169, 255, 255), out _hpFill);
            _san = BuildMeter("SAN", 973, new Color32(182, 112, 255, 255), out _sanFill);
            _shieldIcon = BuildShieldIcon(transform, "ShieldIcon");
            _shieldCrackIcon = BuildShieldIcon(transform, "ShieldCrackIcon");
            _shieldCrackIcon.enabled = false;
            _shield = Label(transform, "Shield", 181, 929, 65, 30, 18, Gold);
            _shieldCrack = Label(transform, "ShieldCrack", 181, 929, 65, 30, 18, Gold);
            _shieldCrack.gameObject.SetActive(false);
            Plate(transform, "DeckCounts", 14, 1030, 232, 36);
            _draw = Label(transform, "Draw", 25, 1030, 103, 36, 20, Gold);
            _discard = Label(transform, "Discard", 133, 1030, 110, 36, 20, Silver);

            var stock = Place(transform, "Stock", 1392, 622, 274, 52);
            var stockFrame = Picture(stock, "Frame", 0, 0, 274, 52);
            stockFrame.sprite = Resources.Load<Sprite>("Battle/E5b/parts/hud/stock_capsule_9s_1x");
            stockFrame.type = Image.Type.Sliced;
            Label(stock, "StockLabel", 16, 0, 76, 52, 19, Silver).text = "STOCK";
            for (var i = 0; i < _stockGems.Length; i++)
            {
                var x = 88 + i * 32;
                var empty = Picture(stock, $"StockEmpty{i}", x, 14, 24, 24);
                empty.sprite = Resources.Load<Sprite>("Battle/E5b/parts/gems/gem_empty_slot_54_1x");
                empty.enabled = empty.sprite != null;
                _stockGems[i] = Picture(stock, $"StockGem{i}", x, 14, 24, 24);
            }
        }

        TMP_Text BuildMeter(string name, float y, Color tint, out Image fill)
        {
            Label(transform, name + "Label", 14, y, 54, 30, 19, tint).text = name;
            var text = Label(transform, name + "Value", 67, y, 82, 30, 20, Silver);
            text.alignment = TextAlignmentOptions.MidlineRight;
            Plate(transform, name + "Track", 14, y + 30, 232, 14);
            fill = Picture(transform, name + "Fill", 16, y + 32, 228, 10);
            fill.sprite = BattleUISprites.Solid;
            fill.color = tint;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return text;
        }

        static void Plate(Transform parent, string name, float x, float y, float width, float height)
        {
            var plate = Picture(parent, name, x, y, width, height);
            Set(plate, "card/card_base_9s_48");
            plate.type = Image.Type.Sliced;
        }

        static SlotView BuildCard(RectTransform root)
        {
            var view = new SlotView { Root = root };
            view.Empty = Picture(root, "Empty", 0, 0, CardWidth, CardHeight);
            Set(view.Empty, "frame/frame_N_silver", new Color(1, 1, 1, 0.22f));

            var card = view.Card = Place(root, "Card", 0, 0, CardWidth, CardHeight);
            view.Fade = card.gameObject.AddComponent<CanvasGroup>();
            view.GuideGlow = Picture(card, "WordGuideGlow", -28, -28, 369, 206);
            view.GuideGlow.enabled = false;
            view.Glow = Picture(card, "Glow", -28, -28, 369, 206);
            view.Ghost = Picture(card, "StackGhost", 6, -6, CardWidth, CardHeight);
            Set(Picture(card, "Base", 0, 0, CardWidth, CardHeight), "card/card_base_313x150");
            var mask = Picture(card, "ArtMask", 0, 0, CardWidth, CardHeight);
            Set(mask, "card/art_window_mask_313x150");
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            view.Art = Picture(mask.transform, "Art", 0, 0, CardWidth, CardHeight);
            Set(Picture(card, "Gradient", 0, 0, CardWidth, CardHeight), "card/text_gradient_313x150");

            view.Frame = Picture(card, "RarityFrame", 0, 0, CardWidth, CardHeight);
            view.Name = Label(card, "Name", 20, 16, 270, 40, 28, Silver, BattleHudKit.Mincho);
            view.Name.textWrappingMode = TextWrappingModes.NoWrap;
            var divider = Picture(card, "Divider", 20, 64, 170, 1);
            divider.sprite = BattleUISprites.Solid;
            divider.color = new Color(1, 1, 1, 0.4f);
            view.Effect = Label(card, "Effect", 20, 78, 262, 60, 20, EffectColor, BattleHudKit.Sans);
            view.Effect.fontStyle = FontStyles.Bold;
            view.Effect.alignment = TextAlignmentOptions.TopLeft;
            view.Effect.lineSpacing = 10;
            view.Effect.maxVisibleLines = 2;
            view.Effect.overflowMode = TextOverflowModes.Truncate;
            for (var i = 0; i < view.Stars.Length; i++)
                view.Stars[i] = Picture(card, $"Star{i}", 232 + i * 21, 55, 22, 22);

            view.Highlight = Picture(card, "NearTopEdge", 0, 0, CardWidth, 8);
            view.Backing = Picture(card, "GemBacking", 0, -16, 38, 32);
            for (var i = 0; i < view.Gems.Length; i++)
                view.Gems[i] = Picture(card, $"Gem{i}", 0, -12, 24, 24);
            view.Badge = Picture(card, "StackBadge", 271, -22, 64, 42);
            view.Tag = Picture(card, "StateTag", -2, -54, 121, 48);
            var back = view.DrawBack = Place(root, "DrawBack", CardWidth / 2f, CardHeight / 2f, CardWidth, CardHeight);
            back.pivot = new Vector2(0.5f, 0.5f);
            var backArt = Picture(back, "Art", (CardWidth - CardHeight) / 2f, (CardHeight - CardWidth) / 2f,
                CardHeight, CardWidth);
            backArt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            backArt.rectTransform.anchoredPosition = new Vector2(CardWidth / 2f, -CardHeight / 2f);
            backArt.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            backArt.sprite = Resources.Load<Sprite>("Battle/UI/Arcane_Card_Back");
            backArt.enabled = backArt.sprite != null;
            Set(Picture(back, "Frame", 0, 0, CardWidth, CardHeight), "frame/frame_R_gold");
            back.gameObject.SetActive(false);
            return view;
        }

        public void Refresh(IReadOnlyList<DeckHandSlot> hand, OrbStock stock, ElementColor? previousColor,
            int draw, int discard, int shield, int hp, int maxHp, int san, int maxSan)
        {
            _draw.text = $"山札 {draw}";
            _discard.text = $"捨て札 {discard}";
            _shield.text = shield.ToString();
            _shieldIcon.enabled = shield > 0;
            _shield.gameObject.SetActive(shield > 0);
            SetMeter(_hp, _hpFill, hp, maxHp);
            SetMeter(_san, _sanFill, san, maxSan);
            for (var i = 0; i < _stockGems.Length; i++)
                Set(_stockGems[i], i < stock.Orbs.Count ? Gem(stock.Orbs[i], true) : null);

            if (!_changingHand)
                PaintCards(hand, stock, previousColor);
        }

        public void SetWordGuideHighlights(int mask)
        {
            if (_wordGuideHighlightMask == mask)
                return;
            _wordGuideHighlightMask = mask;
            if (!_changingHand)
                PaintGuideHighlights();
        }

        void PaintGuideHighlights()
        {
            for (var i = 0; i < _slots.Length; i++)
                Set(_slots[i].GuideGlow, (_wordGuideHighlightMask & (1 << i)) != 0 ? "state/glow_near_369x206" : null);
        }

        public void PrepareHandChange(IReadOnlyList<DeckHandSlot> hand, OrbStock stock, ElementColor? previousColor)
        {
            if (_refillPresentation != null)
                StopCoroutine(_refillPresentation);
            // A new input can resolve before a previous draw lands. Settle to the current game state first.
            PaintCards(hand, stock, previousColor);
            _changingHand = true;
            foreach (var view in _slots)
                view.GuideGlow.enabled = false;
        }

        public void PlayHandChange(IReadOnlyList<DeckHandSlot> hand, OrbStock stock,
            ElementColor? previousColor, System.Action refresh)
        {
            _refillPresentation = StartCoroutine(AnimateHandChange(hand, stock, previousColor, refresh));
        }

        IEnumerator AnimateHandChange(IReadOnlyList<DeckHandSlot> hand, OrbStock stock,
            ElementColor? previousColor, System.Action refresh)
        {
            var survivors = new SlotView[_slots.Length];
            var freed = new List<SlotView>(_slots.Length);
            foreach (var view in _slots)
            {
                var nextIndex = IndexOf(hand, view.Model);
                if (nextIndex >= 0)
                    survivors[nextIndex] = view;
                else
                    freed.Add(view);
                view.Empty.enabled = false;
            }

            yield return Animate(0.07f, progress =>
            {
                foreach (var view in freed)
                    view.Fade.alpha = 1f - progress;
            });

            var from = new Vector2[_slots.Length];
            var to = new Vector2[_slots.Length];
            var draws = new bool[_slots.Length];
            var merges = new bool[_slots.Length];
            var freedIndex = 0;
            var hasSlide = false;
            for (var i = 0; i < _slots.Length; i++)
            {
                var view = _slots[i] = survivors[i] ?? freed[freedIndex++];
                var slot = i < hand.Count ? hand[i] : null;
                var oldStack = view.PaintedStack;
                draws[i] = slot != null && survivors[i] == null;
                merges[i] = survivors[i] != null && slot.StackCount > oldStack;
                from[i] = view.Root.anchoredPosition;
                PaintSlot(view, CardPosition(i), slot, stock, previousColor, merges[i] ? oldStack : 0);
                to[i] = view.Root.anchoredPosition;
                if (survivors[i] != null)
                {
                    view.Root.anchoredPosition = from[i];
                    hasSlide |= (from[i] - to[i]).sqrMagnitude > 0.01f;
                }
                else
                {
                    view.Empty.enabled = true;
                    view.Card.gameObject.SetActive(false);
                }
            }

            if (hasSlide)
            {
                yield return Animate(0.13f, progress =>
                {
                    for (var i = 0; i < _slots.Length; i++)
                        if (survivors[i] != null)
                            _slots[i].Root.anchoredPosition = Vector2.LerpUnclamped(from[i], to[i], progress);
                });
            }

            const float revealAt = 0.64f;
            yield return Animate(0.17f, progress =>
            {
                for (var i = 0; i < _slots.Length; i++)
                {
                    var view = _slots[i];
                    if (draws[i])
                    {
                        view.Empty.enabled = false;
                        view.Root.anchoredPosition = to[i] + Vector2.right * (82f * (1f - progress));
                        view.DrawBack.gameObject.SetActive(progress < revealAt);
                        view.DrawBack.localScale = new Vector3(Mathf.Max(0.05f, 1f - progress / revealAt), 1f, 1f);
                        view.Card.gameObject.SetActive(progress >= revealAt);
                        view.Fade.alpha = Mathf.InverseLerp(revealAt, 1f, progress);
                    }
                    else if (merges[i])
                    {
                        view.DrawBack.gameObject.SetActive(progress < 1f);
                        view.DrawBack.anchoredPosition = BackCenter + new Vector2(52f, 20f) * (1f - progress);
                        view.DrawBack.localScale = Vector3.one * (1f - 0.35f * progress);
                        if (progress >= 1f)
                            PaintSlot(view, CardPosition(i), hand[i], stock, previousColor);
                    }
                }
            });

            _refillPresentation = null;
            _changingHand = false;
            refresh();
        }

        static readonly Vector2 BackCenter = new(CardWidth / 2f, -CardHeight / 2f);

        static IEnumerator Animate(float duration, System.Action<float> paint)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                paint(Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            paint(1f);
        }

        static int IndexOf(IReadOnlyList<DeckHandSlot> hand, DeckHandSlot model)
        {
            if (model != null)
                for (var i = 0; i < hand.Count; i++)
                    if (ReferenceEquals(hand[i], model))
                        return i;
            return -1;
        }

        void PaintCards(IReadOnlyList<DeckHandSlot> hand, OrbStock stock, ElementColor? previousColor)
        {
            for (var i = 0; i < _slots.Length; i++)
                PaintSlot(_slots[i], CardPosition(i), i < hand.Count ? hand[i] : null, stock, previousColor);
            PaintGuideHighlights();
        }

        static void SetMeter(TMP_Text text, Image fill, int current, int max)
        {
            text.text = $"{current}/{max}";
            fill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0;
        }

        static void PaintSlot(SlotView view, Vector2 position, DeckHandSlot slot, OrbStock stock,
            ElementColor? previousColor, int stackPreview = 0, bool reward = false)
        {
            view.Model = slot;
            view.PaintedStack = stackPreview > 0 ? stackPreview : slot?.StackCount ?? 0;
            view.Fade.alpha = 1f;
            view.DrawBack.gameObject.SetActive(false);
            view.DrawBack.localScale = Vector3.one;
            view.DrawBack.anchoredPosition = BackCenter;
            var def = slot?.Def;
            var empty = def == null || def.Colors.Length == 0;
            view.Empty.enabled = empty;
            view.Card.gameObject.SetActive(!empty);
            view.Root.anchoredPosition = position;
            if (empty)
                return;

            var colors = def.Colors;
            var count = Mathf.Min(colors.Length, view.Gems.Length);
            var first = colors[0];
            var matched = reward ? 0 : DeckCardResolver.MatchedColorCount(stock, colors);
            var ready = matched == colors.Length;
            var near = !reward && !ready && matched == colors.Length - 1;
            var chain = !ready && !near && previousColor == first;
            view.Root.anchoredPosition += Vector2.up * (ready ? ReadyLift : near ? NearLift : 0f);

            var frame = def.Rarity == "SR" ? "SR_rainbow" : def.Rarity == "R" ? "R_gold" : "N_silver";
            var rarityColor = def.Rarity == "SR" ? Rainbow : def.Rarity == "R" ? Gold : Silver;
            var chainColor = ElementColorUtility.ToUnityColor(first);
            Set(view.Glow, ready ? "state/glow_ready_369x206" : near ? "state/glow_near_369x206" :
                chain ? "state/glow_chain_white_369x206" : null, chain ? chainColor : Color.white);
            Set(view.Ghost, view.PaintedStack > 1 ? "badge/stack_ghost_card" : null, rarityColor);
            Set(view.Art, first == ElementColor.Red ? "art/placeholder_slash_626x300" : "art/placeholder_moon_626x300");
            Set(view.Frame, "frame/frame_" + frame);
            view.Name.text = def.Name;
            view.Name.color = rarityColor;
            view.Effect.text = EffectNumbers.Replace(def.Text ?? "", "<color=#F2D58A>$0</color>");
            for (var i = 0; i < view.Stars.Length; i++)
                Set(view.Stars[i], i < view.PaintedStack ? "badge/star_lit_22" : "badge/star_unlit_22");

            Set(view.Highlight, near ? "state/near_top_edge_313x8" : null);
            var backingWidth = count * 28 + 10;
            view.Backing.rectTransform.sizeDelta = new Vector2(backingWidth, 32);
            var backingLeft = Mathf.Floor((CardWidth - backingWidth) / 2f);
            view.Backing.rectTransform.anchoredPosition = new Vector2(backingLeft, 16);
            Set(view.Backing, $"gem/gem_backing_n{count}", rarityColor);
            for (var i = 0; i < view.Gems.Length; i++)
            {
                view.Gems[i].rectTransform.anchoredPosition = new Vector2(backingLeft + 7 + i * 28, 12);
                Set(view.Gems[i], i < count ? Gem(colors[i], reward || i < matched) : null);
            }
            Set(view.Badge, view.PaintedStack > 1 ? $"badge/stack_x{view.PaintedStack}" : null);
            Set(view.Tag, chain ? "state/tag_chain_Y" : null);
        }

        public static void BuildRewardCard(RectTransform parent, DeckCardDefinition card)
        {
            var face = Place(parent, "CardFace", 0, 0, CardWidth, CardHeight);
            face.localScale = new Vector3(DisplayWidth / CardWidth, DisplayHeight / CardHeight, 1f);
            PaintSlot(BuildCard(face), Vector2.zero, new DeckHandSlot { CardId = card.Id }, null, null, reward: true);
        }

        public static void PaintRewardGem(Image image, ElementColor color) => Set(image, Gem(color, true));

        static Image BuildShieldIcon(Transform parent, string name)
        {
            if (_shieldSprite == null)
            {
                const int size = 24;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (var y = 2; y < size - 2; y++)
                {
                    var halfWidth = Mathf.Min(9, 1 + y * 2 / 3);
                    for (var x = 12 - halfWidth; x < 12 + halfWidth; x++)
                    {
                        var rim = y >= 20 || x <= 13 - halfWidth || x >= 10 + halfWidth;
                        pixels[y * size + x] = rim ? new Color32(242, 213, 138, 255) :
                            new Color32(79, 123, 155, 255);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _shieldSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one / 2);
            }
            var icon = Picture(parent, name, 156, 934, 20, 20);
            icon.sprite = _shieldSprite;
            return icon;
        }

        public void PlayShieldCrack(int points) => StartCoroutine(AnimateShieldCrack(points));

        IEnumerator AnimateShieldCrack(int points)
        {
            _shieldCrack.text = points.ToString();
            _shieldCrackIcon.enabled = true;
            var iconOrigin = _shieldIcon.rectTransform.anchoredPosition;
            _shieldCrack.gameObject.SetActive(true);
            var origin = _shield.rectTransform.anchoredPosition;
            for (var elapsed = 0f; elapsed < 0.28f; elapsed += Time.unscaledDeltaTime)
            {
                var progress = elapsed / 0.28f;
                var offset = new Vector2(Mathf.Sin(progress * 40) * 5, progress * 22);
                _shieldCrack.rectTransform.anchoredPosition = origin + offset;
                _shieldCrackIcon.rectTransform.anchoredPosition = iconOrigin + offset;
                _shieldCrackIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, progress * 35);
                _shieldCrackIcon.color = new Color(1, 1, 1, 1 - progress);
                _shieldCrack.alpha = 1 - progress;
                yield return null;
            }
            _shieldCrack.gameObject.SetActive(false);
            _shieldCrackIcon.enabled = false;
        }

        static string Letter(ElementColor color) => color switch
        {
            ElementColor.Blue => "B", ElementColor.Green => "G", ElementColor.Yellow => "Y",
            ElementColor.Purple => "P", _ => "R"
        };

        static string Gem(ElementColor color, bool on) => $"gem/gem_{Letter(color)}_{(on ? "on" : "dim")}_24";

        static RectTransform Place(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static TMP_Text Label(Transform parent, string name, float x, float y, float width, float height,
            float size, Color color, TMP_FontAsset font = null)
        {
            var rect = Place(parent, name, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font ?? BattleHudKit.Gothic;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = size;
            label.fontSizeMin = 16;
            label.fontSizeMax = size;
            label.enableAutoSizing = true;
            label.raycastTarget = false;
            BattleHudKit.Outline(label, 0.2f, Color.black);
            return label;
        }

        static Image Picture(Transform parent, string name, float x, float y, float width, float height)
        {
            var image = Place(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static void Set(Image image, string path, Color? color = null)
        {
            Sprite sprite = null;
            if (path != null && !Sprites.TryGetValue(path, out sprite))
            {
                sprite = Resources.Load<Sprite>(Parts + path);
                Sprites[path] = sprite;
            }
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.color = color ?? Color.white;
        }
    }
}
