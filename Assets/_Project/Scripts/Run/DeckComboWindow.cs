using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Battle.Deck;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static TypingRoguelike.Run.DeckRewardView;

namespace TypingRoguelike.Run
{
    public sealed class DeckComboWindow : MonoBehaviour
    {
        static readonly Color Gold = new Color32(249, 212, 138, 255);
        static readonly Color Silver = new Color32(197, 211, 230, 255);
        static readonly Color Panel = new Color32(17, 22, 38, 255);
        static readonly Color Tile = new Color32(39, 48, 68, 255);
        static readonly Color Warm = new Color32(70, 53, 37, 255);
        static readonly Color Link = new Color32(118, 139, 167, 255);
        const int MatrixPage = 9;

        readonly Dictionary<string, List<DeckComboRoute>> _routes = new();
        readonly Stack<DeckCardDefinition> _trail = new();
        List<DeckCardDefinition> _skills;
        IReadOnlyList<string> _deck;
        DeckCardDefinition _candidate, _focus;
        RectTransform _frame, _page;
        Text _detailName, _detailBody, _detailRoute;
        Action _close;
        bool _matrix;
        int _rows, _cols;

        public void Open(DeckCardDefinition candidate, IReadOnlyList<string> deck, Action close)
        {
            _candidate = _focus = candidate;
            _deck = deck;
            _close = close;
            var ids = deck.Concat(new[] { candidate.Id }).Distinct().ToList();
            _skills = ids.Select(DeckCardCatalog.Get).Where(c => c != null).ToList();
            foreach (var skill in _skills)
                _routes[skill.Id] = DeckComboRoutes.Find(skill, ids);

            _frame = Plate(transform, "Window", 0, 0, 1520, 920, Panel);
            Border(_frame, Link);
            Fit();
            Draw();
        }

        void Fit()
        {
            if (_frame == null) return;
            var area = ((RectTransform)transform).rect;
            _frame.localScale = Vector3.one * Mathf.Clamp(Mathf.Min(area.width / 1560, area.height / 960), 0.01f, 1f);
        }

        void OnRectTransformDimensionsChange() => Fit();

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        void Close()
        {
            var callback = _close;
            _close = null;
            gameObject.SetActive(false);
            callback?.Invoke();
            Dispose(gameObject);
        }

        static void Dispose(GameObject node)
        {
            if (Application.isPlaying) Destroy(node);
            else DestroyImmediate(node);
        }

        void Draw()
        {
            if (_page != null)
            {
                _page.gameObject.SetActive(false);
                Dispose(_page.gameObject);
            }
            _page = Box(_frame, "Page", 0, 0, 1520, 920);
            Label("コンボ表", -566, 398, 310, 74, 46, Gold, TextAnchor.MiddleLeft);
            var owned = _deck.Count(id => id == _candidate.Id);
            Label($"獲得予定　{_candidate.Name}　{owned} → {owned + 1}枚", 235, 404, 600, 46, 24, Gold, TextAnchor.MiddleRight);
            Control("閉じる  ×", 642, 403, 158, 46, Close);
            Control("候補の前後", -595, 313, 228, 54, () =>
            {
                _focus = _candidate;
                _trail.Clear();
                _matrix = false;
                Draw();
            }, !_matrix);
            Control("全スキル", -361, 313, 198, 54, () => { _matrix = true; Draw(); }, _matrix);
            if (!_matrix && _trail.Count > 0)
                Control("‹  前のスキル", -114, 313, 220, 54, () => { _focus = _trail.Pop(); Draw(); });
            var edges = _routes.Sum(pair => pair.Value.Count(r => r.From.Id == pair.Key));
            Label($"{_skills.Count} 種のスキル　／　{edges} 本のつながり", 435, 312, 540, 48, 22, Silver, TextAnchor.MiddleRight);
            Plate(_page, "Divider", 0, 265, 1420, 2, Link);
            if (_matrix) DrawMatrix();
            else DrawBranches();
            Plate(_page, "DetailBand", 0, -356, 1420, 144, Tile);
            _detailName = Label("", -385, -318, 590, 46, 28, Gold, TextAnchor.MiddleLeft);
            _detailBody = Label("", -290, -377, 780, 56, 23, Color.white, TextAnchor.MiddleLeft);
            _detailRoute = Label("", 395, -352, 560, 104, 22, Silver, TextAnchor.MiddleRight);
            Inspect(_focus, null);
        }

        void Follow(DeckCardDefinition card)
        {
            if (card != _focus) _trail.Push(_focus);
            _focus = card;
            _matrix = false;
            Draw();
        }

        // Each band has exactly one origin. Alternate next inputs share that origin cell.
        void DrawBranches()
        {
            Label("起点スキル", -494, 225, 420, 42, 25, Silver, TextAnchor.MiddleLeft);
            Label("次に打つ色", -95, 225, 230, 42, 23, Silver);
            Label("発動するスキル", 285, 225, 480, 42, 25, Silver, TextAnchor.MiddleLeft);

            var viewport = Plate(_page, "RouteList", 0, -11, 1420, 414, Panel);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = Box(viewport, "Rows", 0, 0, 1392, 0);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;

            var links = _routes[_focus.Id];
            var origins = links.Where(r => r.To == _focus).Select(r => r.From).Prepend(_focus).Distinct();
            float used = 0;
            foreach (var origin in origins)
            {
                var relevant = links.Where(r => r.From == origin && (origin == _focus || r.To == _focus)).ToList();
                var branches = relevant.Where(r => !r.Simultaneous)
                    .GroupBy(r => Colors(Additional(r))).Select(group => group.First()).ToList();
                if (branches.Count == 0 && relevant.Count > 0)
                    branches.Add(relevant[0]);
                if (branches.Count == 0) continue;
                var sourceGroup = ActiveTogether(origin, Array.Empty<ElementColor>());
                float rowHeight = 0;
                var band = Plate(content, "Origin_" + origin.Id, 0, 0, 1386, 1,
                    new Color32(24, 31, 47, 255));
                band.anchorMin = band.anchorMax = band.pivot = new Vector2(0.5f, 1);
                band.anchoredPosition = new Vector2(0, -used);
                foreach (var branch in branches)
                {
                    var extra = Additional(branch);
                    var targets = ActiveTogether(origin, extra);
                    var height = Mathf.Max(GroupHeight(targets.Count), GroupHeight(sourceGroup.Count));
                    var y = -rowHeight - height / 2;
                    var transition = Box(band, "Input", -97, y, 240, 68);
                    Caption(transition, "Arrow", "→", 93, 0, 42, 46, 27, Silver);
                    if (extra.Length == 0)
                        Caption(transition, "Same", "同じ入力", -16, 0, 165, 50, 23, Gold);
                    else
                    {
                        Gems(transition, extra, -18 - (extra.Length - 1) * 15, 14);
                        Caption(transition, "Colors", Colors(extra), -18, -19, 180, 27, 19, Silver);
                    }
                    DrawGroup(band, "Result_" + branch.To.Id, 339, y, 638, branch.To, targets, branch);
                    rowHeight += height;
                }
                band.sizeDelta = new Vector2(1386, rowHeight);
                // Children are positioned from the band's top rather than its variable center.
                DrawGroup(band, "Source", -454, -rowHeight / 2, 443, origin,
                    branches[0].Simultaneous ? new List<DeckCardDefinition> { origin } : sourceGroup, null);
                foreach (RectTransform child in band)
                    child.anchorMin = child.anchorMax = new Vector2(0.5f, 1);
                used += rowHeight + 10;
            }
            content.sizeDelta = new Vector2(1392, Mathf.Max(0, used - 10));
            if (used == 0)
                Label("このスキルにつながる技はありません", 0, 0, 1100, 65, 28, Silver);
            if (content.sizeDelta.y > viewport.rect.height)
            {
                var track = Plate(_page, "Scrollbar", 717, -11, 12, 414, Tile);
                var thumb = Plate(track, "Thumb", 0, 0, 0, 0, Silver);
                var bar = track.gameObject.AddComponent<Scrollbar>();
                bar.direction = Scrollbar.Direction.BottomToTop;
                bar.handleRect = thumb;
                bar.targetGraphic = thumb.GetComponent<Image>();
                bar.value = 1;
                scroll.verticalScrollbar = bar;
            }
            scroll.verticalNormalizedPosition = 1;
            Label("同じ枠の技は手札にあれば同時発動　　技にカーソルを合わせると効果を表示",
                0, -248, 1400, 38, 20, Silver);
        }

        static ElementColor[] Additional(DeckComboRoute route) =>
            route.To.Colors.Skip(route.To.Colors.Length - route.Remaining).ToArray();

        List<DeckCardDefinition> ActiveTogether(DeckCardDefinition source, IEnumerable<ElementColor> extra)
        {
            var stock = new OrbStock();
            foreach (var color in source.Colors.Concat(extra)) stock.Push(color);
            return _skills.Where(card => card.Colors.Length > 0 &&
                DeckCardResolver.MatchedColorCount(stock, card.Colors) == card.Colors.Length).ToList();
        }

        static float GroupHeight(int count) => 61 + 33 * (int)Math.Ceiling(Math.Max(0, count - 1) / 2.0);

        static void Gems(RectTransform parent, ElementColor[] colors, float x, float y)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var gem = Box(parent, "Gem", x + 30 * i, y, 26, 26).gameObject.AddComponent<Image>();
                DeckHandUIView.PaintRewardGem(gem, colors[i]);
                gem.raycastTarget = false;
            }
        }

        void DrawGroup(RectTransform band, string name, float x, float y, float width,
            DeckCardDefinition lead, List<DeckCardDefinition> members, DeckComboRoute? route)
        {
            var peers = members.Where(c => c != lead).ToList();
            float height = GroupHeight(members.Count) - 12;
            var group = Plate(band, name, x, y, width, height, lead == _candidate ? Warm : Tile);
            if (lead == _candidate) Border(group, Gold);
            float top = height / 2;
            var title = Plate(group, "Skill_" + lead.Id, 0, top - 23, width - 10, 39, Color.clear);
            var label = Caption(title, "Name", lead.Name, -lead.Colors.Length * 15, 0, width - 40 - lead.Colors.Length * 30, 39, 27,
                lead == _candidate ? Gold : Color.white);
            label.alignment = TextAnchor.MiddleLeft;
            BindSkill(title, lead, route, lead != _focus);
            Gems(group, lead.Colors, width / 2 - 23 - 30 * (lead.Colors.Length - 1), top - 23);
            if (peers.Count == 0) return;
            Caption(group, "Together", "同時発動", -width / 2 + 66, top - 63, 112, 29, 18, Silver);
            float peerWidth = (width - 135) / Math.Min(2, peers.Count);
            for (int i = 0; i < peers.Count; i++)
            {
                var peer = peers[i];
                var chip = Plate(group, "Skill_" + peer.Id, -width / 2 + 122 + peerWidth * (0.5f + i % 2),
                    top - 63 - i / 2 * 33, peerWidth - 6, 29, Panel);
                Caption(chip, "Name", peer.Name, 0, 0, peerWidth - 10, 29, 19, peer == _candidate ? Gold : Color.white);
                BindSkill(chip, peer, null, peer != _focus);
            }
        }

        void DrawMatrix()
        {
            Label("縦が起点、横が派生先　　同時 ＝ 同時発動　　＋数字 ＝ あと何色で発動するか",
                0, 230, 1420, 44, 21, Silver);
            var columns = _skills.Skip(_cols * MatrixPage).Take(MatrixPage).ToList();
            var rows = _skills.Skip(_rows * MatrixPage).Take(MatrixPage).ToList();
            const int step = 127;
            float left = -440;
            for (int j = 0; j < columns.Count; j++)
            {
                var card = columns[j];
                var header = Plate(_page, "To_" + card.Id, left + j * step, 169, 121, 60, card == _candidate ? Warm : Tile);
                Caption(header, "Name", card.Name.Replace("・", "\n"), 0, 0, 116, 58, 19, card == _candidate ? Gold : Silver);
                BindSkill(header, card, null, true);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var from = rows[i];
                float y = 113 - 38 * i;
                var header = Plate(_page, "From_" + from.Id, -614, y, 200, 35, from == _candidate ? Warm : Tile);
                Caption(header, "Name", from.Name, 0, 0, 194, 35, 19, from == _candidate ? Gold : Silver);
                BindSkill(header, from, null, true);
                var outgoing = _routes[from.Id].Where(r => r.From == from).ToDictionary(r => r.To.Id);
                for (int j = 0; j < columns.Count; j++)
                {
                    var to = columns[j];
                    bool connected = outgoing.TryGetValue(to.Id, out var r);
                    var cell = Plate(_page, "Pair_" + from.Id + "_" + to.Id, left + step * j, y, 121, 35,
                        connected ? (from == _candidate || to == _candidate ? Warm : Tile) : Panel);
                    Caption(cell, "Value", connected ? (r.Simultaneous ? "同時" : $"＋{r.Remaining}") : "·",
                        0, 0, 116, 34, 19, connected && r.Simultaneous ? Gold : Silver);
                    if (connected) BindSkill(cell, to, r, true);
                }
            }
            Pager(-376, -241, _rows, _skills.Count, MatrixPage, p => { _rows = p; Draw(); }, "起点");
            Pager(372, -241, _cols, _skills.Count, MatrixPage, p => { _cols = p; Draw(); }, "派生先");
        }

        void BindSkill(RectTransform tile, DeckCardDefinition card, DeckComboRoute? route, bool follow)
        {
            var trigger = tile.gameObject.AddComponent<EventTrigger>();
            var scroll = tile.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                Forward(EventTriggerType.Scroll, scroll.OnScroll);
                Forward(EventTriggerType.InitializePotentialDrag, scroll.OnInitializePotentialDrag);
                Forward(EventTriggerType.BeginDrag, scroll.OnBeginDrag);
                Forward(EventTriggerType.Drag, scroll.OnDrag);
                Forward(EventTriggerType.EndDrag, scroll.OnEndDrag);
            }
            void Forward(EventTriggerType type, Action<PointerEventData> handler)
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(data => handler((PointerEventData)data));
                trigger.triggers.Add(entry);
            }
            var surface = tile.GetComponent<Image>();
            var baseColor = surface.color;
            Listen(trigger, EventTriggerType.PointerEnter, () =>
            {
                surface.color = Color.Lerp(baseColor, Silver, 0.18f);
                Inspect(card, route);
            });
            Listen(trigger, EventTriggerType.PointerExit, () =>
            {
                surface.color = baseColor;
                Inspect(_focus, null);
            });
            if (follow) Listen(trigger, EventTriggerType.PointerClick, () => Follow(card));
        }

        void Inspect(DeckCardDefinition card, DeckComboRoute? route)
        {
            var type = card.Type == DeckCardType.Attack ? "攻撃" : card.Type == DeckCardType.Shield ? "防御" : "強化";
            _detailName.text = card.Name + (card == _candidate ? "　〈獲得予定〉" : "");
            _detailBody.text = $"{type}　／　{card.Rarity}　／　所持 {_deck.Count(id => id == card.Id)}枚\n{card.Text}　｜　発動色：{Colors(card.Colors)}";
            _detailRoute.text = route.HasValue
                ? $"{route.Value.From.Name} → {route.Value.To.Name}\n{Step(route.Value)}"
                : "手札のスキルが、オーブ列の末尾で発動";
        }

        static string Step(DeckComboRoute r) => r.Simultaneous ? "同時発動" :
            "あと " + Colors(r.To.Colors.Skip(r.To.Colors.Length - r.Remaining));

        static string Colors(IEnumerable<ElementColor> colors) => string.Join("・", colors.Select(c => c switch
        {
            ElementColor.Red => "赤", ElementColor.Blue => "青", ElementColor.Green => "緑",
            ElementColor.Yellow => "黄", ElementColor.Purple => "紫", _ => c.ToString()
        }));

        void Pager(float x, float y, int page, int count, int size, Action<int> show, string label = "")
        {
            int pages = (count + size - 1) / size;
            if (pages <= 1) return;
            Label($"{label}　{page + 1} / {pages}", x, y, 190, 36, 19, Silver);
            if (page > 0) Control("‹", x - 120, y, 60, 38, () => show(page - 1));
            if (page + 1 < pages) Control("›", x + 120, y, 60, 38, () => show(page + 1));
        }

        void Control(string title, float x, float y, int w, int h, Action action, bool active = false)
        {
            var button = Plate(_page, title, x, y, w, h, active ? Warm : Tile);
            Border(button, active ? Gold : Link);
            Caption(button, "Label", title, 0, 0, w - 6, h - 4, 22, active ? Gold : Color.white);
            Listen(button.gameObject.AddComponent<EventTrigger>(), EventTriggerType.PointerClick, action);
        }

        Text Label(string value, float x, float y, int w, int h, int size, Color ink,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = Caption(_page, "Text", value, x, y, w, h, size, ink);
            label.alignment = align;
            return label;
        }

        static RectTransform Plate(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rect = Box(parent, name, x, y, w, h);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        static void Border(RectTransform rect, Color color)
        {
            var line = rect.gameObject.AddComponent<Outline>();
            line.effectColor = color;
            line.effectDistance = new Vector2(1, -1);
        }
    }
}
