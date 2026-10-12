using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>塔を下から上へ登る分岐マップ UI。</summary>
    public sealed class DungeonMapUI
    {
        const float NodeSize = 74f;
        const float MapMarginX = 0.14f;
        const float MapMarginBottom = 0.07f;
        const float MapMarginTop = 0.08f;

        static readonly DungeonNodeType[] LegendOrder =
        {
            DungeonNodeType.Normal,
            DungeonNodeType.Danger,
            DungeonNodeType.Rest,
            DungeonNodeType.Treasure,
            DungeonNodeType.Merchant,
            DungeonNodeType.Event,
            DungeonNodeType.Elevator,
            DungeonNodeType.Boss
        };

        Font _font;
        RectTransform _root;
        GameObject _mapPanel;
        GameObject _modalPanel;
        Text _hpText;
        Text _coinText;
        Image _hpFill;
        Text _madnessText;
        Image _madnessFill;
        Text _hintText;
        Text _detailTitle;
        Text _detailBody;
        string _defaultHint;
        bool _revealAllNodes;

        bool IsNodeKnown(DungeonMapNode node) =>
            node != null && (node.Revealed || _revealAllNodes);

        Font UiFont => _font ??= BattleUITheme.Font;

        public void Show(
            Transform host,
            DungeonRunState dungeon,
            RunPartyState party,
            RunExplorationInventory exploration,
            RunMadnessState madness,
            Action<int> onNodeSelected,
            Action onAbandonRun = null,
            Action onOpenLoadout = null,
            Action onOpenRelics = null,
            Action onOpenPartyStats = null,
            bool revealAllNodes = false)
        {
            _revealAllNodes = revealAllNodes;
            EnsureRoot(host);
            HideModal();
            if (_mapPanel != null)
                UnityEngine.Object.Destroy(_mapPanel);

            _mapPanel = CreateFullScreenPanel("DungeonMap", new Color(0.012f, 0.014f, 0.035f, 1f));
            _mapPanel.AddComponent<CanvasGroup>();
            _mapPanel.AddComponent<RunPanelEntrance>();
            BuildDungeonBackdrop(_mapPanel.transform);

            var topLayer = dungeon.Nodes.Values.Select(node => node.Layer).DefaultIfEmpty(0).Max();
            var currentLayer = dungeon.Current?.Layer ?? 0;
            BuildTitleBar(
                _mapPanel.transform,
                dungeon.Floor,
                dungeon.TotalFloors,
                currentLayer,
                topLayer);
            BuildInfoPanel(_mapPanel.transform, dungeon, party, exploration, madness, onOpenLoadout, onOpenRelics,
                onOpenPartyStats, onAbandonRun);
            BuildLegendPanel(_mapPanel.transform, dungeon);

            var mapWindow = BattleUIChrome.CreateLuxuryWindow(
                _mapPanel.transform, "MapWindow", new Vector2(0.245f, 0.04f), new Vector2(0.755f, 0.885f),
                "TOWER OF SIGILS", BattleUITheme.Arcane);
            var mapBody = mapWindow.Find("Body");
            if (mapBody != null)
                BuildTowerMap(mapBody, dungeon, topLayer, onNodeSelected);
        }

        public void Refresh(
            DungeonRunState dungeon,
            RunPartyState party,
            RunExplorationInventory exploration = null,
            RunMadnessState madness = null)
        {
            if (_hpText != null)
                _hpText.text = FormatHp(party);
            if (_coinText != null)
                _coinText.text = FormatCoins(party);
            if (_hpFill != null)
                _hpFill.fillAmount = HpFraction(party);
            RefreshMadness(madness);
            _defaultHint = BuildHint(dungeon);
            if (_hintText != null)
                _hintText.text = _defaultHint;
            _ = exploration;
        }

        void RefreshMadness(RunMadnessState madness)
        {
            if (madness == null)
                return;
            if (_madnessText != null)
                _madnessText.text = $"{madness.CurrentMadness} / {madness.MaxMadness}";
            if (_madnessFill != null)
            {
                var max = madness.MaxMadness;
                _madnessFill.fillAmount = max > 0 ? madness.CurrentMadness / (float)max : 0f;
                _madnessFill.color = RunMadnessState.StageColor(madness.Stage);
            }
        }

        public void ShowModal(string title, string body, IReadOnlyList<(string label, Action onClick)> actions)
        {
            HideModal();
            _modalPanel = CreateFullScreenPanel("DungeonModal", new Color(0.005f, 0.008f, 0.02f, 0.86f));
            _modalPanel.AddComponent<CanvasGroup>();
            _modalPanel.AddComponent<RunPanelEntrance>();
            BattleUIChrome.CreateGlow(_modalPanel.transform, "ModalGlow", new Vector2(0.2f, 0.12f),
                new Vector2(0.8f, 0.88f), new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.13f));
            var win = BattleUIChrome.CreateLuxuryWindow(
                _modalPanel.transform, "Modal", new Vector2(0.27f, 0.28f), new Vector2(0.73f, 0.72f),
                title, BattleUITheme.Gold);
            var bodyT = win.Find("Body");
            if (bodyT != null)
            {
                CreateLabel(bodyT, "Body", body, 18, TextAnchor.UpperCenter,
                    new Vector2(0.07f, 0.35f), new Vector2(0.93f, 0.9f), BattleUITheme.TextPrimary);
            }

            var count = actions?.Count ?? 0;
            var btnH = count >= 5 ? 0.075f : 0.09f;
            var gap = 0.012f;
            var buttonsTop = 0.05f + Mathf.Max(1, count) * (btnH + gap);
            if (bodyT != null)
            {
                var bodyLabel = bodyT.Find("Body")?.GetComponent<Text>();
                if (bodyLabel != null)
                {
                    var rt = bodyLabel.rectTransform;
                    rt.anchorMin = new Vector2(0.07f, Mathf.Clamp(buttonsTop + 0.03f, 0.28f, 0.55f));
                    rt.anchorMax = new Vector2(0.93f, 0.9f);
                }
            }

            var y = buttonsTop - btnH;
            foreach (var (label, onClick) in actions)
            {
                CreateButton(win, label, new Vector2(0.12f, y), new Vector2(0.88f, y + btnH), () =>
                {
                    HideModal();
                    onClick?.Invoke();
                });
                y -= btnH + gap;
            }
        }

        public void HideModal()
        {
            if (_modalPanel != null)
                UnityEngine.Object.Destroy(_modalPanel);
            _modalPanel = null;
        }

        public void Hide()
        {
            HideModal();
            if (_mapPanel != null)
                UnityEngine.Object.Destroy(_mapPanel);
            _mapPanel = null;
        }

        void BuildTitleBar(
            Transform parent,
            int floor,
            int totalFloors,
            int currentLayer,
            int topLayer)
        {
            var bar = BattleUIChrome.CreateLuxuryWindow(parent, "TitleBar",
                new Vector2(0.245f, 0.9f), new Vector2(0.755f, 0.975f), "ASCENT", BattleUITheme.Gold);
            var body = bar.Find("Body") ?? bar;
            var title = CreateLabel(body, "Title", "刻印の小塔", 24, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0), new Vector2(0.5f, 1), BattleUITheme.Gold);
            title.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(title, new Color(0, 0, 0, 0.9f), new Vector2(2, -2));
            var mapProgress = currentLayer >= topLayer
                ? "ボス"
                : currentLayer == 0 ? "入口" : $"{currentLayer} / {topLayer - 1}段";
            CreateLabel(
                body,
                "Floor",
                $"塔 {floor} / {totalFloors} 階層　｜　マップ {mapProgress}",
                18,
                TextAnchor.MiddleRight,
                new Vector2(0.5f, 0), new Vector2(0.96f, 1), BattleUITheme.TextPrimary);
        }

        void BuildInfoPanel(
            Transform parent,
            DungeonRunState dungeon,
            RunPartyState party,
            RunExplorationInventory exploration,
            RunMadnessState madness,
            Action onOpenLoadout,
            Action onOpenRelics,
            Action onOpenPartyStats,
            Action onAbandonRun)
        {
            var window = BattleUIChrome.CreateLuxuryWindow(parent, "InfoPanel",
                new Vector2(0.02f, 0.04f), new Vector2(0.23f, 0.975f), "PARTY", BattleUITheme.AccentAlly);
            var body = window.Find("Body") ?? window;

            CreateLabel(body, "HpHeader", "パーティ HP", 15, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.97f), BattleUITheme.TextMuted);
            var track = CreateImage(body, "HpTrack", BattleUISprites.Solid, BattleUITheme.BarTrack,
                new Vector2(0.06f, 0.865f), new Vector2(0.94f, 0.905f));
            _hpFill = CreateImage(track.rectTransform, "HpFill", BattleUISprites.HorizontalGradient, BattleUITheme.AccentAlly,
                Vector2.zero, Vector2.one);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillAmount = HpFraction(party);
            _hpText = CreateLabel(body, "Hp", FormatHp(party), 18, TextAnchor.MiddleRight,
                new Vector2(0.06f, 0.81f), new Vector2(0.94f, 0.86f), BattleUITheme.TextPrimary);
            _hpText.fontStyle = FontStyle.Bold;

            _coinText = CreateLabel(body, "Coins", FormatCoins(party), 17, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.84f), BattleUITheme.Gold);
            _coinText.fontStyle = FontStyle.Bold;

            CreateLabel(body, "MadnessHeader", "狂気", 14, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.735f), new Vector2(0.94f, 0.775f), BattleUITheme.TextMuted);
            var madnessTrack = CreateImage(body, "MadnessTrack", BattleUISprites.Solid, BattleUITheme.BarTrack,
                new Vector2(0.06f, 0.695f), new Vector2(0.94f, 0.73f));
            _madnessFill = CreateImage(madnessTrack.rectTransform, "MadnessFill", BattleUISprites.HorizontalGradient,
                RunMadnessState.StageColor(RunMadnessState.MadnessStage.Calm), Vector2.zero, Vector2.one);
            _madnessFill.type = Image.Type.Filled;
            _madnessFill.fillMethod = Image.FillMethod.Horizontal;
            _madnessFill.fillAmount = 0f;
            _madnessText = CreateLabel(body, "Madness", "0 / 1000", 15, TextAnchor.MiddleRight,
                new Vector2(0.06f, 0.655f), new Vector2(0.94f, 0.69f), BattleUITheme.TextPrimary);
            _madnessText.fontStyle = FontStyle.Bold;
            RefreshMadness(madness);

            var extra = party?.EquippedSkillCardCount ?? 0;
            var mat = exploration?.MaterialCount ?? 0;
            var matKinds = exploration?.Materials.DistinctKindCount ?? 0;
            var relicBp = exploration?.BackpackRelicIds.Count ?? 0;
            var relicEq = 0;
            if (exploration != null)
            {
                for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
                {
                    if (exploration.GetEquippedRelicId(i) >= 0)
                        relicEq++;
                }
            }

            CreateLabel(body, "Capacity",
                $"追加スキル {extra} 枚　｜　素材 {mat}個/{matKinds}種",
                14, TextAnchor.UpperLeft, new Vector2(0.06f, 0.605f), new Vector2(0.94f, 0.65f), BattleUITheme.Cyan);

            var revealed = dungeon.Nodes.Values.Count(node => IsNodeKnown(node) && node.Type != DungeonNodeType.Start);
            var total = dungeon.Nodes.Values.Count(node => node.Type != DungeonNodeType.Start);
            CreateLabel(body, "Survey", $"踏査記録　{revealed} / {total} マス", 14, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.6f), BattleUITheme.Gold);

            var relicLabel = CreateLabel(body, "Relics", FormatEquippedRelics(exploration, relicEq, relicBp), 13,
                TextAnchor.UpperLeft, new Vector2(0.06f, 0.4f), new Vector2(0.94f, 0.555f), BattleUITheme.TextPrimary);
            relicLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            var detail = CreatePanel(body, "Detail", new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.39f),
                new Color(0.02f, 0.025f, 0.06f, 0.9f));
            _detailTitle = CreateLabel(detail, "DetailTitle", "ルート選択", 17, TextAnchor.UpperLeft,
                new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.95f), BattleUITheme.Gold);
            _detailTitle.fontStyle = FontStyle.Bold;
            _detailBody = CreateLabel(detail, "DetailBody", string.Empty, 14, TextAnchor.UpperLeft,
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.76f), BattleUITheme.TextPrimary);
            _defaultHint = BuildHint(dungeon);
            _hintText = _detailBody;
            _detailBody.text = _defaultHint;

            if (onOpenLoadout != null)
                CreateButton(body, "刻印", new Vector2(0.06f, 0.17f), new Vector2(0.32f, 0.23f), onOpenLoadout);
            if (onOpenPartyStats != null)
                CreateButton(body, "ステータス", new Vector2(0.34f, 0.17f), new Vector2(0.64f, 0.23f), onOpenPartyStats);
            if (onOpenRelics != null)
                CreateButton(body, "レリック", new Vector2(0.66f, 0.17f), new Vector2(0.94f, 0.23f), onOpenRelics);
            if (onAbandonRun != null)
                CreateButton(body, "探索を放棄（カード消失）", new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.11f),
                    onAbandonRun, new Color(0.22f, 0.04f, 0.06f, 0.97f));
        }

        static string FormatEquippedRelics(RunExplorationInventory exploration, int equippedCount, int backpackCount)
        {
            if (exploration == null || equippedCount <= 0)
                return "レリックなし";

            var catalog = Resources.Load<RelicCatalogDefinition>("Battle/PrototypeRelicCatalog");
            var lines = new List<string> { $"レリック {equippedCount}/3" };
            if (backpackCount > 0)
                lines[0] += $"（所持 {backpackCount}）";
            for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
            {
                var def = catalog?.FindById(exploration.GetEquippedRelicId(i));
                if (def == null)
                    continue;
                lines.Add($"{def.displayName}：{def.effectText}");
            }

            return string.Join("\n", lines);
        }

        void BuildLegendPanel(Transform parent, DungeonRunState dungeon)
        {
            var window = BattleUIChrome.CreateLuxuryWindow(parent, "LegendPanel",
                new Vector2(0.77f, 0.04f), new Vector2(0.98f, 0.975f), "LEGEND", BattleUITheme.Arcane);
            var body = window.Find("Body") ?? window;

            var entries = new List<(string glyph, string name, string desc, Color color)>
            {
                ("?", "未判明", "一度踏むと次回以降も内容が表示される", BattleUITheme.TextMuted)
            };
            entries.AddRange(LegendOrder.Select(type =>
                (NodeGlyph(type), NodeName(type), NodeDescription(type), NodeAccent(type))));

            var rowHeight = 0.86f / entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var (glyph, name, desc, color) = entries[i];
                var yMax = 0.97f - i * rowHeight;
                var row = CreateRect(body, $"Legend_{i}", new Vector2(0.03f, yMax - rowHeight), new Vector2(0.97f, yMax));

                var icon = CreateRect(row, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                icon.sizeDelta = new Vector2(44, 44);
                icon.anchoredPosition = new Vector2(28, 0);
                CreateImage(icon, "Ring", BattleUISprites.Circle, color, Vector2.zero, Vector2.one);
                var inner = CreateImage(icon, "Inner", BattleUISprites.Circle, new Color(0.03f, 0.035f, 0.07f, 1f),
                    Vector2.zero, Vector2.one);
                inner.rectTransform.offsetMin = new Vector2(4, 4);
                inner.rectTransform.offsetMax = new Vector2(-4, -4);
                var glyphText = CreateLabel(icon, "Glyph", glyph, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, color);
                glyphText.fontStyle = FontStyle.Bold;

                var nameText = CreateLabel(row, "Name", name, 15, TextAnchor.LowerLeft,
                    new Vector2(0.3f, 0.5f), new Vector2(1, 0.95f), BattleUITheme.TextPrimary);
                nameText.fontStyle = FontStyle.Bold;
                var descText = CreateLabel(row, "Desc", desc, 11, TextAnchor.UpperLeft,
                    new Vector2(0.3f, 0.02f), new Vector2(1, 0.5f), BattleUITheme.TextMuted);
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            var hasElevatorRecord = dungeon.Nodes.Values.Any(node => node.Type == DungeonNodeType.Elevator && node.Revealed);
            CreateLabel(body, "ElevatorHint",
                hasElevatorRecord ? "エレベーターの位置は記録済み" : "エレベーターは未発見",
                13, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.08f),
                hasElevatorRecord ? BattleUITheme.Gold : BattleUITheme.TextMuted);
        }

        void BuildTowerMap(Transform parent, DungeonRunState dungeon, int topLayer, Action<int> onNodeSelected)
        {
            var area = CreateRect(parent, "TowerArea", Vector2.zero, Vector2.one);
            var maxColumn = dungeon.Nodes.Values.Select(node => node.Column).DefaultIfEmpty(0).Max();
            var positions = dungeon.Nodes.Values.ToDictionary(node => node.Id,
                node => NodePosition(node, maxColumn, Mathf.Max(1, topLayer)));

            var routesRoot = CreateRect(area, "Routes", Vector2.zero, Vector2.one);
            routesRoot.SetAsFirstSibling();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(area);

            var reachable = new HashSet<int>(dungeon.GetReachableNext().Select(node => node.Id));
            foreach (var from in dungeon.Nodes.Values)
            {
                foreach (var nextId in from.Next)
                {
                    if (!dungeon.Nodes.TryGetValue(nextId, out var to))
                        continue;

                    var available = from.Id == dungeon.CurrentNodeId && reachable.Contains(nextId);
                    var traveled = from.Cleared && to.Cleared;
                    var fromRadius = NodeSize * (from.Type == DungeonNodeType.Boss ? 0.65f : 0.5f);
                    var toRadius = NodeSize * (to.Type == DungeonNodeType.Boss ? 0.65f : 0.5f);

                    if (available)
                    {
                        CreateRouteSegment(
                            routesRoot, area, positions[from.Id], positions[nextId],
                            new Color(1f, 0.82f, 0.4f, 0.98f), 5f, fromRadius, toRadius, dashed: false);
                    }
                    else if (traveled)
                    {
                        CreateRouteSegment(
                            routesRoot, area, positions[from.Id], positions[nextId],
                            new Color(0.35f, 0.92f, 0.65f, 0.88f), 4f, fromRadius, toRadius, dashed: false);
                    }
                    else
                    {
                        var alpha = IsNodeKnown(from) || IsNodeKnown(to) || from.Cleared ? 0.62f : 0.38f;
                        CreateRouteSegment(
                            routesRoot, area, positions[from.Id], positions[nextId],
                            new Color(0.58f, 0.62f, 0.82f, alpha), 3.2f, fromRadius, toRadius, dashed: true);
                    }
                }
            }

            BuildFloorGuides(area, topLayer);

            foreach (var node in dungeon.Nodes.Values.OrderBy(node => node.Layer))
            {
                var isCurrent = node.Id == dungeon.CurrentNodeId;
                var canClick = reachable.Contains(node.Id);
                CreateNode(area, node, positions[node.Id], isCurrent, canClick, () => onNodeSelected?.Invoke(node.Id));
            }
        }

        static Vector2 AnchorFractionToLocal(RectTransform area, Vector2 anchorFraction)
        {
            var rect = area.rect;
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, anchorFraction.x),
                Mathf.Lerp(rect.yMin, rect.yMax, anchorFraction.y));
        }

        static void CreateRouteSegment(
            RectTransform routesRoot,
            RectTransform area,
            Vector2 fromAnchor,
            Vector2 toAnchor,
            Color color,
            float thickness,
            float insetFromPixels,
            float insetToPixels,
            bool dashed)
        {
            var a = AnchorFractionToLocal(area, fromAnchor);
            var b = AnchorFractionToLocal(area, toAnchor);
            var delta = b - a;
            var length = delta.magnitude;
            if (length < 6f)
                return;

            var direction = delta / length;
            var startInset = Mathf.Min(insetFromPixels, length * 0.42f);
            var endInset = Mathf.Min(insetToPixels, length * 0.42f);
            if (startInset + endInset >= length - 2f)
            {
                startInset = length * 0.22f;
                endInset = length * 0.22f;
            }

            a += direction * startInset;
            b -= direction * endInset;
            length = (b - a).magnitude;
            if (length < 4f)
                return;

            if (!dashed)
            {
                CreateRouteBar(routesRoot, a, b, color, thickness);
                CreateRouteArrow(routesRoot, b, direction, color, thickness);
                return;
            }

            const float dashLen = 12f;
            const float gapLen = 9f;
            for (var t = 0f; t < length; t += dashLen + gapLen)
            {
                var segStart = a + direction * t;
                var segEnd = a + direction * Mathf.Min(length, t + dashLen);
                CreateRouteBar(routesRoot, segStart, segEnd, color, thickness * 0.85f);
            }
        }

        static void CreateRouteBar(RectTransform routesRoot, Vector2 fromLocal, Vector2 toLocal, Color color, float thickness)
        {
            var delta = toLocal - fromLocal;
            var length = delta.magnitude;
            if (length < 1f)
                return;

            var direction = delta / length;
            var mid = (fromLocal + toLocal) * 0.5f;
            var rt = CreateRect(routesRoot, "RouteBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(length, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = BattleUISprites.Solid;
            img.color = color;
            img.raycastTarget = false;
            BattleUITheme.EnsureSprite(img);
        }

        static void CreateRouteArrow(RectTransform routesRoot, Vector2 tipLocal, Vector2 direction, Color color, float thickness)
        {
            var rt = CreateRect(routesRoot, "RouteArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var size = Mathf.Max(10f, thickness * 2.4f);
            rt.anchoredPosition = tipLocal - direction * (size * 0.35f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = BattleUISprites.Solid;
            img.color = color;
            img.raycastTarget = false;
            BattleUITheme.EnsureSprite(img);
        }

        void BuildFloorGuides(RectTransform area, int topLayer)
        {
            for (var layer = 0; layer <= topLayer; layer++)
            {
                var y = LayerY(layer, Mathf.Max(1, topLayer));
                var line = CreateImage(area, $"FloorGuide_{layer}", BattleUISprites.HorizontalGradient,
                    new Color(0.4f, 0.44f, 0.7f, 0.08f), new Vector2(0.02f, y), new Vector2(0.98f, y));
                line.rectTransform.sizeDelta = new Vector2(0, 2);

                var label = layer == 0 ? "入口" : layer == topLayer ? "ボス" : $"{layer}段";
                var text = CreateLabel(area, $"FloorLabel_{layer}", label, 13, TextAnchor.MiddleLeft,
                    new Vector2(0.015f, y), new Vector2(0.12f, y), new Color(0.6f, 0.64f, 0.82f, 0.7f));
                text.rectTransform.sizeDelta = new Vector2(0, 24);
            }
        }

        void CreateNode(RectTransform area, DungeonMapNode node, Vector2 position, bool isCurrent, bool canClick,
            Action onClick)
        {
            var revealed = IsNodeKnown(node);
            var accent = revealed ? NodeAccent(node.Type) : BattleUITheme.TextMuted;
            var isBoss = node.Type == DungeonNodeType.Boss && revealed;
            var size = isBoss ? NodeSize * 1.3f : NodeSize;
            var dimmed = !isCurrent && !canClick;

            var rootRt = CreateRect(area, $"Node_{node.Id}", position, position);
            rootRt.sizeDelta = new Vector2(size, size);
            var group = rootRt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = dimmed ? (node.Cleared ? 0.75f : 0.55f) : 1f;

            var glow = BattleUIChrome.CreateGlow(rootRt, "Glow", new Vector2(-0.55f, -0.55f), new Vector2(1.55f, 1.55f),
                new Color(accent.r, accent.g, accent.b, canClick || isCurrent ? 0.28f : 0.06f));

            var ring = CreateImage(rootRt, "Ring", BattleUISprites.Circle,
                canClick ? Color.Lerp(accent, Color.white, 0.25f) : accent, Vector2.zero, Vector2.one);
            ring.raycastTarget = true;
            var innerColor = node.Cleared
                ? new Color(0.06f, 0.14f, 0.11f, 1f)
                : new Color(0.025f, 0.03f, 0.065f, 1f);
            var inner = CreateImage(rootRt, "Inner", BattleUISprites.Circle, innerColor, Vector2.zero, Vector2.one);
            var ringWidth = isCurrent ? 6f : canClick ? 5f : 3f;
            inner.rectTransform.offsetMin = new Vector2(ringWidth, ringWidth);
            inner.rectTransform.offsetMax = new Vector2(-ringWidth, -ringWidth);

            var glyph = CreateLabel(rootRt, "Glyph", revealed ? NodeGlyph(node.Type) : "?", isBoss ? 36 : 28,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, revealed ? accent : new Color(0.75f, 0.78f, 0.88f));
            glyph.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(glyph, new Color(0, 0, 0, 0.9f), new Vector2(2, -2));

            var caption = CreateLabel(rootRt, "Caption", revealed ? NodeCaption(node) : "未判明", 13,
                TextAnchor.UpperCenter, new Vector2(-0.6f, -0.42f), new Vector2(1.6f, -0.04f),
                revealed ? BattleUITheme.TextPrimary : BattleUITheme.TextMuted);
            BattleUITheme.AddTextShadow(caption, new Color(0, 0, 0, 0.95f), new Vector2(1.5f, -1.5f));

            if (node.Cleared && !isCurrent && node.Type != DungeonNodeType.Start)
            {
                var badge = CreateRect(rootRt, "ClearedBadge", new Vector2(1, 1), new Vector2(1, 1));
                badge.sizeDelta = new Vector2(26, 26);
                badge.anchoredPosition = new Vector2(-4, -4);
                CreateImage(badge, "Bg", BattleUISprites.Circle, BattleUITheme.AccentAlly, Vector2.zero, Vector2.one);
                CreateLabel(badge, "Text", "済", 12, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.black)
                    .fontStyle = FontStyle.Bold;
            }

            Image marker = null;
            if (isCurrent)
            {
                var markerRt = CreateRect(rootRt, "PartyMarker", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                markerRt.sizeDelta = new Vector2(24, 24);
                markerRt.anchoredPosition = new Vector2(0, 20);
                marker = CreateImage(markerRt, "Gem", BattleUISprites.Diamond, BattleUITheme.Gold, Vector2.zero, Vector2.one);
            }

            if (canClick)
            {
                var btn = rootRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = ring;
                btn.onClick.AddListener(() => onClick());
                var colors = btn.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.75f, 0.75f, 0.82f, 1f);
                btn.colors = colors;
            }

            AddHover(rootRt.gameObject, node, canClick);

            if (canClick || isCurrent)
            {
                var pulse = rootRt.gameObject.AddComponent<RunNodePulse>();
                pulse.Configure(glow, marker, isCurrent ? 1.05f : 1.08f);
            }
        }

        void AddHover(GameObject target, DungeonMapNode node, bool canClick)
        {
            var trigger = target.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                if (_detailTitle == null || _detailBody == null)
                    return;
                var known = IsNodeKnown(node);
                _detailTitle.text = known ? NodeHoverTitle(node) : "未判明のマス";
                var body = known
                    ? NodeHoverDescription(node)
                    : "何が待つかは踏むまで分からない。\n一度踏めば次回以降の探索で表示される。";
                if (node.Cleared && node.Type != DungeonNodeType.Start)
                    body += "\n\n（今回の探索で通過済み）";
                else if (canClick)
                    body += "\n\nクリックで進む";
                _detailBody.text = body;
            });
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                if (_detailTitle != null)
                    _detailTitle.text = "ルート選択";
                if (_detailBody != null)
                    _detailBody.text = _defaultHint;
            });
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        static Vector2 NodePosition(DungeonMapNode node, int maxColumn, int topLayer)
        {
            var x = maxColumn <= 0
                ? 0.5f
                : Mathf.Lerp(MapMarginX, 1f - MapMarginX, node.Column / (float)maxColumn);
            return new Vector2(x, LayerY(node.Layer, topLayer));
        }

        static float LayerY(int layer, int topLayer) =>
            Mathf.Lerp(MapMarginBottom, 1f - MapMarginTop, layer / (float)topLayer);

        static string NodeGlyph(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Start => "門",
            DungeonNodeType.Normal => "道",
            DungeonNodeType.Danger => "危",
            DungeonNodeType.Rest => "休",
            DungeonNodeType.Merchant => "商",
            DungeonNodeType.Treasure => "宝",
            DungeonNodeType.Elevator => "昇",
            DungeonNodeType.Boss => "王",
            DungeonNodeType.Combat => "戦",
            DungeonNodeType.Elite => "強",
            DungeonNodeType.Event => "事",
            _ => "?"
        };

        static string NodeName(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Start => "入口",
            DungeonNodeType.Normal => "通常",
            DungeonNodeType.Danger => "危険",
            DungeonNodeType.Rest => "焚き火",
            DungeonNodeType.Merchant => "行商人",
            DungeonNodeType.Treasure => "宝箱",
            DungeonNodeType.Elevator => "エレベーター",
            DungeonNodeType.Boss => "塔の主",
            DungeonNodeType.Combat => "戦闘",
            DungeonNodeType.Elite => "強敵",
            DungeonNodeType.Event => "出来事",
            _ => "未判明"
        };

        static string NodeCaption(DungeonMapNode node) => node.Type switch
        {
            DungeonNodeType.Danger => FormatDangerCaption(node),
            DungeonNodeType.Normal => "通常",
            _ => NodeName(node.Type)
        };

        static string FormatDangerCaption(DungeonMapNode node)
        {
            var line = $"危 {Mathf.Clamp(node.DangerEncounterPercent, 1, 99)}%";
            if (node.DangerElitePreview.HasValue)
                line += node.DangerElitePreview.Value ? " 強" : " 弱";
            return line;
        }

        static string NodeHoverTitle(DungeonMapNode node) => node.Type switch
        {
            DungeonNodeType.Danger => $"危険 {Mathf.Clamp(node.DangerEncounterPercent, 1, 99)}%",
            _ => NodeName(node.Type)
        };

        static string NodeHoverDescription(DungeonMapNode node) => node.Type switch
        {
            DungeonNodeType.Danger =>
                $"進入時 {Mathf.Clamp(node.DangerEncounterPercent, 1, 99)}% で敵と遭遇。\n" +
                (node.DangerElitePreview.HasValue
                    ? node.DangerElitePreview.Value
                        ? "危険察知: 強敵の気配がする。\n"
                        : "危険察知: 通常程度の敵のようだ。\n"
                    : string.Empty) +
                "戦闘しなければ報酬なし。勝利で塔片。",
            _ => NodeDescription(node.Type)
        };

        static string NodeDescription(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Start => "探索の出発地点。",
            DungeonNodeType.Normal => "特にイベントは起きない。",
            DungeonNodeType.Danger => "敵遭遇率付き。判定成功で戦闘。",
            DungeonNodeType.Rest => "HP を少し回復する",
            DungeonNodeType.Merchant => "塔片で回復や刻印。半分くらいの店が高価なレリックを出す",
            DungeonNodeType.Treasure => "塔片と刻印（選択）を獲得",
            DungeonNodeType.Elevator => "拠点へ帰還（探索は中断・再開可）",
            DungeonNodeType.Boss => "撃破で踏破。カードを持ち帰れる",
            DungeonNodeType.Combat => "魔物と戦う。勝利で塔片を獲得",
            DungeonNodeType.Elite => "手強い敵。塔片は多め",
            DungeonNodeType.Event => "まれに現れる。レリックを1つ拾える",
            _ => string.Empty
        };

        static Color NodeAccent(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Start => BattleUITheme.Gold,
            DungeonNodeType.Normal => new Color(0.55f, 0.58f, 0.68f),
            DungeonNodeType.Danger => new Color(0.96f, 0.42f, 0.32f),
            DungeonNodeType.Rest => BattleUITheme.AccentAlly,
            DungeonNodeType.Merchant => BattleUITheme.Gold,
            DungeonNodeType.Treasure => BattleUITheme.Cyan,
            DungeonNodeType.Elevator => BattleUITheme.Gold,
            DungeonNodeType.Boss => BattleUITheme.AccentBoss,
            DungeonNodeType.Combat => new Color(0.66f, 0.7f, 0.86f),
            DungeonNodeType.Elite => new Color(0.96f, 0.28f, 0.36f),
            DungeonNodeType.Event => BattleUITheme.Arcane,
            _ => BattleUITheme.TextMuted
        };

        void BuildDungeonBackdrop(Transform parent)
        {
            var art = Resources.Load<Sprite>("Battle/Art/Backgrounds/Dungeon_Map");
            if (art != null)
            {
                var image = CreateImage(parent, "MapBackgroundArt", art, new Color(0.62f, 0.68f, 0.86f, 0.45f),
                    Vector2.zero, Vector2.one);
                image.preserveAspect = false;
            }

            BattleUIChrome.CreateGlow(parent, "MapArcaneGlow", new Vector2(0.16f, 0.02f),
                new Vector2(0.84f, 0.98f), new Color(0.3f, 0.18f, 0.86f, 0.18f));
            BattleUIChrome.CreateGlow(parent, "MapCrimsonGlow", new Vector2(0.52f, 0.4f),
                new Vector2(1.05f, 1.08f), new Color(0.82f, 0.06f, 0.13f, 0.11f));

            for (var i = 0; i < 28; i++)
            {
                var x = Mathf.Repeat(0.08f + i * 0.137f, 0.84f) + 0.08f;
                var y = Mathf.Repeat(0.05f + i * 0.223f, 0.86f) + 0.05f;
                var rt = CreateRect(parent, $"MapMote_{i}", new Vector2(x, y), new Vector2(x, y));
                var size = 2f + i % 5;
                rt.sizeDelta = new Vector2(size, size);
                CreateImage(rt, "Mote", i % 2 == 0 ? BattleUISprites.Diamond : BattleUISprites.Circle,
                    i % 3 == 0 ? new Color(0.95f, 0.65f, 0.28f, 0.22f) : new Color(0.4f, 0.48f, 0.95f, 0.18f),
                    Vector2.zero, Vector2.one);
            }
        }

        static float HpFraction(RunPartyState party) =>
            party != null && party.RunHpInitialized && party.MaxHp > 0f ? party.CurrentHp / party.MaxHp : 0f;

        static string FormatHp(RunPartyState party) =>
            party != null && party.RunHpInitialized
                ? $"{(int)party.CurrentHp} / {(int)party.MaxHp}"
                : "—";

        static string FormatCoins(RunPartyState party) =>
            party != null ? $"塔片　{party.RunCoins}" : "塔片　0";

        static string BuildHint(DungeonRunState dungeon)
        {
            if (dungeon.IsRunComplete)
                return "塔の主を撃破した！";

            return dungeon.GetReachableNext().Any()
                ? "光っているマスを選んで上の層へ進もう。\nマスにカーソルを合わせると詳細が表示される。"
                : "進行データが不正です。ランを最初からやり直してください。";
        }

        void EnsureRoot(Transform host)
        {
            if (_root != null)
                return;

            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("DungeonMapCanvas");
            go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = go.GetComponent<RectTransform>();
        }

        GameObject CreateFullScreenPanel(string name, Color bg)
        {
            var rt = CreateRect(_root, name, Vector2.zero, Vector2.one);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            BattleUITheme.EnsureSprite(img);
            return rt.gameObject;
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var rt = CreateRect(parent, name, anchorMin, anchorMax);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(parent, name, null, color, anchorMin, anchorMax);
            BattleUIImage.ApplyPanel(image, color, BattleUITemplateLoader.Default?.partyCard);
            image.raycastTarget = false;
            return image.rectTransform;
        }

        void CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Action onClick,
            Color? background = null)
        {
            var rt = CreateRect(parent, "Button", anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            BattleUIImage.ApplyPanel(img, background ?? new Color(0.12f, 0.055f, 0.09f, 0.97f),
                BattleUITemplateLoader.Default?.button);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.22f, 1.16f, 1.08f, 1f);
            colors.pressedColor = new Color(0.75f, 0.7f, 0.72f, 1f);
            btn.colors = colors;
            var text = CreateLabel(rt, "Text", label, 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            text.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(text, new Color(0, 0, 0, 0.9f), new Vector2(2, -2));
        }

        Text CreateLabel(Transform parent, string name, string text, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = CreateRect(parent, name, anchorMin, anchorMax);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = UiFont;
            label.fontSize = size;
            label.alignment = anchor;
            label.text = text;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
    }

    public sealed class RunNodePulse : MonoBehaviour
    {
        Image _glow;
        Image _gem;
        float _maxScale = 1.03f;
        Vector3 _baseScale;
        float _phase;

        public void Configure(Image glow, Image gem, float maxScale)
        {
            _glow = glow;
            _gem = gem;
            _maxScale = maxScale;
            _baseScale = transform.localScale;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            var wave = (Mathf.Sin(Time.unscaledTime * 2.8f + _phase) + 1f) * 0.5f;
            transform.localScale = _baseScale * Mathf.Lerp(1f, _maxScale, wave);
            if (_glow != null)
            {
                var color = _glow.color;
                color.a = Mathf.Lerp(0.08f, 0.34f, wave);
                _glow.color = color;
            }
            if (_gem != null)
                _gem.transform.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * 24f);
        }
    }

    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RunPanelEntrance : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _rect;
        Vector3 _targetScale;
        float _elapsed;
        const float Duration = 0.32f;

        void Awake()
        {
            if (!TryGetComponent(out _group))
            {
                enabled = false;
                return;
            }
            _rect = transform as RectTransform;
            _targetScale = transform.localScale;
            _group.alpha = 1f;
            if (_rect != null)
                _rect.localScale = _targetScale;
            enabled = false;
        }

        void Update()
        {
            if (_group == null)
            {
                enabled = false;
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= Duration)
            {
                FinishEntrance();
                enabled = false;
                return;
            }

            var t = Mathf.Clamp01(_elapsed / Duration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            _group.alpha = eased;
            if (_rect != null)
                _rect.localScale = Vector3.Lerp(_targetScale * 0.985f, _targetScale, eased);
        }

        void FinishEntrance()
        {
            if (_group == null)
                return;
            _group.alpha = 1f;
            if (_rect != null)
                _rect.localScale = _targetScale;
        }

        void OnDisable()
        {
            if (_elapsed > 0f && _elapsed < Duration)
                FinishEntrance();
        }
    }
}
