using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        static readonly Color ChoiceOrange = new Color(1f, 0.48f, 0.08f, 1f);
        static readonly Color InkDark = new Color(0.16f, 0.11f, 0.08f, 0.96f);
        static readonly Color InkLight = new Color(0.55f, 0.44f, 0.34f, 0.48f);
        static readonly Color InkLightFaint = new Color(0.55f, 0.44f, 0.34f, 0.22f);
        static readonly Color SkippedTint = new Color(0.62f, 0.56f, 0.5f, 1f);
        const float RevealSeconds = 0.3f;
        const float LargeLineThickness = 8f;
        const float LargeLineThicknessChoice = 10.5f;
        const float MiniLineThickness = 3f;
        const float MiniLineThicknessChoice = 4.5f;

        static readonly DungeonNodeType[] LegendOrder =
        {
            DungeonNodeType.Normal,
            DungeonNodeType.Danger,
            DungeonNodeType.Rest,
            DungeonNodeType.Treasure,
            DungeonNodeType.Merchant,
            DungeonNodeType.Event,
            DungeonNodeType.EventV2,
            DungeonNodeType.Mystery,
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
        int _currentLayer;
        Transform _host;
        DungeonRunState _dungeon;
        RectTransform _minimapRoot;
        GameObject _minimapPanel;
        GameObject _overlayPanel;
        bool _pausedByOverlay;
        float _savedTimeScale = 1f;
        bool _animateReveal;
        bool _hasPresentedMap;
        HashSet<int> _reachableNextIds = new HashSet<int>();
        HashSet<int> _revealSnapshotIds = new HashSet<int>();
        HashSet<int> _previousKnownNodeIds = new HashSet<int>();
        readonly List<CanvasGroup> _fadeInks = new List<CanvasGroup>();
        readonly List<CanvasGroup> _fadeStamps = new List<CanvasGroup>();

        public static bool IsOverlayOpen { get; private set; }

        bool IsNodeKnown(DungeonMapNode node) => IsKnownAt(node, _currentLayer);

        bool IsKnownAt(DungeonMapNode node, int layer)
        {
            if (node == null)
                return false;
            if (_revealAllNodes)
                return true;
            if (node.Type is DungeonNodeType.Start or DungeonNodeType.Boss)
                return true;
            if (node.Revealed)
                return true;
            return _reachableNextIds != null && _reachableNextIds.Contains(node.Id);
        }

        bool NewlyRevealedNode(DungeonMapNode node) =>
            node != null && _animateReveal && IsNodeKnown(node) && !_revealSnapshotIds.Contains(node.Id);

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
            bool revealAllNodes = false,
            bool debugRevealMap = false)
        {
            _host = host;
            _dungeon = dungeon;
            _revealAllNodes = revealAllNodes || debugRevealMap;
            _currentLayer = dungeon.Current?.Layer ?? 0;
            ReleasePause();
            HideMinimap();
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
                BuildTowerMap(mapBody, dungeon, topLayer, onNodeSelected, interactive: true);
        }

        /// <summary>道選び以外の画面。右上に前後1段だけのミニマップを出す。</summary>
        public void ShowMinimap(Transform host, DungeonRunState dungeon, bool revealAllNodes)
        {
            if (host == null || dungeon == null)
                return;

            _host = host;
            _dungeon = dungeon;
            _revealAllNodes = revealAllNodes;
            _currentLayer = dungeon.Current?.Layer ?? 0;
            CloseOverlay();
            if (_mapPanel != null)
            {
                UnityEngine.Object.Destroy(_mapPanel);
                _mapPanel = null;
            }

            EnsureMinimapRoot();
            if (_minimapPanel != null)
                UnityEngine.Object.Destroy(_minimapPanel);
            _minimapPanel = BuildMinimapPanel();
        }

        /// <summary>イベント・商人・休憩の文章中。ミニマップは出さず、右上の「Tab：地図」だけ残す。</summary>
        public void ShowMapTabHint(Transform host, DungeonRunState dungeon, bool revealAllNodes)
        {
            if (host == null || dungeon == null)
                return;

            _host = host;
            _dungeon = dungeon;
            _revealAllNodes = revealAllNodes;
            _currentLayer = dungeon.Current?.Layer ?? 0;
            ReleasePause();
            if (_mapPanel != null)
            {
                UnityEngine.Object.Destroy(_mapPanel);
                _mapPanel = null;
            }

            EnsureRoot(host);
            EnsureMinimapRoot();
            if (_minimapPanel != null)
                UnityEngine.Object.Destroy(_minimapPanel);
            _minimapPanel = BuildMapTabHint();
        }

        public void ReleasePause()
        {
            CloseOverlay();
            if (Time.timeScale <= 0f)
                Time.timeScale = 1f;
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
            CloseOverlay();
            HideMinimap();
            if (_mapPanel != null)
                UnityEngine.Object.Destroy(_mapPanel);
            _mapPanel = null;
        }

        void HideMinimap()
        {
            if (_minimapRoot != null)
                UnityEngine.Object.Destroy(_minimapRoot.gameObject);
            _minimapRoot = null;
            _minimapPanel = null;
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
                ("?", "未判明", "今いる段の先は、近づくまで隠れる", BattleUITheme.TextMuted)
            };
            entries.AddRange(LegendOrder.Select(type =>
                (NodeGlyph(type), NodeName(type), NodeDescription(type), NodeAccent(type))));

            var rowHeight = 0.86f / entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var (glyph, name, desc, color) = entries[i];
                var yMax = 0.97f - i * rowHeight;
                var row = CreateRect(body, $"Legend_{i}", new Vector2(0.03f, yMax - rowHeight), new Vector2(0.97f, yMax));
                var legendArt = i == 0
                    ? MapNodeIcons.Catalog != null ? MapNodeIcons.Catalog.stampUnknown : null
                    : MapNodeIcons.Resolve(LegendOrder[i - 1], true).Stamp;

                var icon = CreateRect(row, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                var wide = legendArt != null && legendArt.rect.height > 1f && legendArt.rect.width / legendArt.rect.height > 1.4f;
                icon.sizeDelta = wide ? new Vector2(64, 32) : glyph == "椅" ? new Vector2(52, 32) : new Vector2(44, 44);
                icon.anchoredPosition = new Vector2(28, 0);
                if (legendArt != null)
                {
                    var stamp = CreateImage(icon, "Stamp", legendArt, Color.white, Vector2.zero, Vector2.one);
                    stamp.preserveAspect = true;
                }
                else
                {
                    var legendShape = glyph == "?"
                        ? MapNodeShapes.Get(MapNodeShape.Circle)
                        : MapNodeIcons.Resolve(LegendOrder[i - 1], true).Shape;
                    var legendImage = CreateImage(icon, "Ring", legendShape, color, Vector2.zero, Vector2.one);
                    legendImage.preserveAspect = true;
                    var inner = CreateImage(icon, "Inner", BattleUISprites.Circle, new Color(0.03f, 0.035f, 0.07f, 1f),
                        Vector2.zero, Vector2.one);
                    inner.rectTransform.offsetMin = new Vector2(4, 4);
                    inner.rectTransform.offsetMax = new Vector2(-4, -4);
                    var glyphText = CreateLabel(icon, "Glyph", glyph, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, color);
                    glyphText.fontStyle = FontStyle.Bold;
                }

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

        void BuildTowerMap(Transform parent, DungeonRunState dungeon, int topLayer, Action<int> onNodeSelected, bool interactive)
        {
            _reachableNextIds = new HashSet<int>(dungeon.GetReachableNext().Select(node => node.Id));
            BeginRevealPass();
            var area = CreateRect(parent, "TowerArea", Vector2.zero, Vector2.one);
            var span = Mathf.Max(1, topLayer);
            PlaceFloorPaper(area, topLayer);
            BuildFloorGuides(area, topLayer, MapNodeIcons.Catalog != null && MapNodeIcons.Catalog.paper != null);

            var positions = dungeon.Nodes.Values.ToDictionary(node => node.Id,
                node => NodePosition(node, span));

            var routesRoot = CreateRect(area, "Routes", Vector2.zero, Vector2.one);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(area);

            var reachable = _reachableNextIds;
            var brush = MapNodeIcons.Catalog != null ? MapNodeIcons.Catalog.brushLine : null;
            foreach (var from in dungeon.Nodes.Values)
            {
                foreach (var nextId in from.Next)
                {
                    if (!dungeon.Nodes.TryGetValue(nextId, out var to))
                        continue;

                    var available = from.Id == dungeon.CurrentNodeId && reachable.Contains(nextId);
                    var traveled = from.Cleared && (to.Cleared || to.Id == dungeon.CurrentNodeId);
                    var justWalked = _animateReveal && traveled && to.Id == dungeon.CurrentNodeId && from.Cleared;
                    var fromRadius = NodeRadius(from, mini: false);
                    var toRadius = NodeRadius(to, mini: false);
                    if (brush != null)
                    {
                        var thickness = available ? LargeLineThicknessChoice : traveled ? LargeLineThickness : LargeLineThickness * 0.55f;
                        var color = available ? ChoiceOrange : traveled ? InkDark : InkLightFaint;
                        CreateBrushLine(routesRoot, area, positions[from.Id], positions[nextId], color,
                            thickness, brush, justWalked);
                    }
                    else if (available)
                    {
                        CreateRouteSegment(
                            routesRoot, area, positions[from.Id], positions[nextId],
                            new Color(ChoiceOrange.r, ChoiceOrange.g, ChoiceOrange.b, 0.42f), 13f, fromRadius, toRadius, dashed: false);
                        CreateRouteSegment(
                            routesRoot, area, positions[from.Id], positions[nextId],
                            ChoiceOrange, 6.5f, fromRadius, toRadius, dashed: false);
                    }
                    else
                    {
                        var color = traveled ? InkDark : InkLightFaint;
                        CreateRouteSegment(
                            routesRoot, area, positions[from.Id], positions[nextId],
                            color, traveled ? 4f : 2.2f, fromRadius, toRadius, dashed: !traveled);
                    }
                }
            }

            foreach (var node in dungeon.Nodes.Values.OrderBy(node => node.Layer))
                PlaceNodeInk(area, node, positions[node.Id], mini: false);

            foreach (var node in dungeon.Nodes.Values.OrderBy(node => node.Layer))
            {
                var choice = reachable.Contains(node.Id);
                var canClick = interactive && choice;
                PaintNode(area, node, positions[node.Id], mini: false, choice, canClick,
                    () => onNodeSelected?.Invoke(node.Id));
            }

            if (_animateReveal && (_fadeInks.Count > 0 || _fadeStamps.Count > 0))
            {
                var playback = area.gameObject.AddComponent<MapRevealPlayback>();
                playback.Play(_fadeInks, _fadeStamps, RevealSeconds);
            }

            _animateReveal = false;
        }

        void BeginRevealPass()
        {
            var currentKnown = new HashSet<int>();
            if (_dungeon?.Nodes != null)
            {
                foreach (var node in _dungeon.Nodes.Values)
                {
                    if (IsKnownAt(node, 0))
                        currentKnown.Add(node.Id);
                }
            }

            _revealSnapshotIds = _hasPresentedMap ? new HashSet<int>(_previousKnownNodeIds) : new HashSet<int>();
            _animateReveal = _hasPresentedMap && currentKnown.Except(_revealSnapshotIds).Any();
            _previousKnownNodeIds = currentKnown;
            _hasPresentedMap = true;
            _fadeInks.Clear();
            _fadeStamps.Clear();
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

        void BuildFloorGuides(RectTransform area, int topLayer, bool onPaper)
        {
            for (var layer = 0; layer <= topLayer; layer++)
            {
                var y = LayerY(layer, Mathf.Max(1, topLayer));
                if (!onPaper)
                {
                    var line = CreateImage(area, $"FloorGuide_{layer}", BattleUISprites.HorizontalGradient,
                        new Color(0.4f, 0.44f, 0.7f, 0.08f), new Vector2(0.02f, y), new Vector2(0.98f, y));
                    line.rectTransform.sizeDelta = new Vector2(0, 2);
                }

                var label = layer == 0 ? "入口" : layer == topLayer ? "ボス" : $"{layer}段";
                var text = CreateLabel(area, $"FloorLabel_{layer}", label, 13, TextAnchor.MiddleLeft,
                    new Vector2(0.015f, y), new Vector2(0.12f, y),
                    onPaper ? new Color(0.28f, 0.18f, 0.12f, 0.85f) : new Color(0.6f, 0.64f, 0.82f, 0.7f));
                text.rectTransform.sizeDelta = new Vector2(0, 24);
            }
        }

        void PlaceFloorPaper(RectTransform area, int topLayer)
        {
            var paper = MapNodeIcons.Catalog != null ? MapNodeIcons.Catalog.paper : null;
            if (paper == null)
                return;

            var span = Mathf.Max(1, topLayer);
            var y0 = LayerY(0, span);
            var y1 = LayerY(topLayer, span);
            var padY = Mathf.Max(0.035f, (y1 - y0) / span * 0.55f);
            var image = CreateImage(area, "FloorPaper", paper, Color.white,
                new Vector2(0.03f, Mathf.Max(0.01f, y0 - padY)),
                new Vector2(0.97f, Mathf.Min(0.99f, y1 + padY)));
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.raycastTarget = false;
        }

        void PlaceNodeInk(RectTransform area, DungeonMapNode node, Vector2 position, bool mini)
        {
            if (node == null)
                return;

            var known = IsNodeKnown(node);
            var newlyRevealed = NewlyRevealedNode(node);
            if (known && !newlyRevealed)
                return;

            var catalog = MapNodeIcons.Catalog;
            var sprite = catalog != null ? catalog.Ink(NodeInkIndex(node.Id)) : null;
            if (sprite == null)
                return;

            var radius = NodeRadius(node, mini);
            var size = radius * (mini ? 3.2f : 3.6f);
            var ink = CreateImage(area, mini ? $"InkMini_{node.Id}" : $"Ink_{node.Id}", sprite, Color.white,
                position, position);
            ink.rectTransform.sizeDelta = new Vector2(size, size);
            ink.preserveAspect = true;
            ink.raycastTarget = false;
            var group = ink.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            if (newlyRevealed)
                _fadeInks.Add(group);
        }

        int NodeInkIndex(int nodeId)
        {
            var floor = _dungeon != null ? _dungeon.Floor : 0;
            var hash = unchecked(floor * 7919 + nodeId * 104729);
            return (int)((uint)hash % 4);
        }

        void PaintNode(RectTransform area, DungeonMapNode node, Vector2 position, bool mini, bool choice, bool canClick,
            Action onClick)
        {
            var known = IsNodeKnown(node);
            var visual = MapNodeIcons.Resolve(node.Type, known, mini);
            var isCurrent = node.Id == _dungeon.CurrentNodeId;
            var skipped = node.Layer < _currentLayer && !node.Cleared;
            var fadeIn = !mini && NewlyRevealedNode(node);
            var stamp = visual.Stamp;
            var onPaper = !mini && MapNodeIcons.Catalog != null && MapNodeIcons.Catalog.paper != null;
            var size = stamp != null
                ? StampPixelSize(stamp, mini)
                : mini ? MiniSize(visual.ShapeKind) : NodePixelSize(visual.ShapeKind, node.Type == DungeonNodeType.Boss && known);

            var rootRt = CreateRect(area, mini ? $"Mini_{node.Id}" : $"Node_{node.Id}", position, position);
            rootRt.sizeDelta = size;
            var group = rootRt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = fadeIn ? 0f : skipped ? 0.5f : 1f;
            if (fadeIn)
                _fadeStamps.Add(group);

            var tint = skipped ? SkippedTint : Color.white;
            Image hit = null;
            if (choice)
                PlaceChoiceGlow(rootRt);

            if (stamp != null)
            {
                hit = CreateImage(rootRt, "Stamp", stamp, tint, Vector2.zero, Vector2.one);
                hit.preserveAspect = true;
                hit.raycastTarget = canClick;
            }
            else
            {
                var accent = choice ? ChoiceOrange : visual.Color;
                if (skipped)
                    accent = new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, accent.a);
                hit = CreateImage(rootRt, "Ring", visual.Shape, accent, Vector2.zero, Vector2.one);
                hit.preserveAspect = true;
                hit.raycastTarget = canClick;
                if (!mini)
                {
                    var glyph = CreateLabel(rootRt, "Glyph", string.IsNullOrEmpty(visual.Glyph) ? "?" : visual.Glyph,
                        node.Type == DungeonNodeType.Boss ? 36 : 28,
                        TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                        known ? visual.Color : new Color(0.75f, 0.78f, 0.88f));
                    glyph.fontStyle = FontStyle.Bold;
                    BattleUITheme.AddTextShadow(glyph, new Color(0, 0, 0, 0.9f), new Vector2(2, -2));
                }
            }

            if (known && node.Cleared && node.Type != DungeonNodeType.Start)
                PlaceDoneStamp(rootRt, mini);

            if (!mini)
            {
                var captionColor = onPaper
                    ? known ? new Color(0.24f, 0.15f, 0.1f, 1f) : new Color(0.38f, 0.3f, 0.22f, 1f)
                    : known ? BattleUITheme.TextPrimary : BattleUITheme.TextMuted;
                var caption = CreateLabel(rootRt, "Caption", known ? NodeCaption(node) : "未判明", 13,
                    TextAnchor.UpperCenter, new Vector2(-0.6f, -0.42f), new Vector2(1.6f, -0.04f), captionColor);
                BattleUITheme.AddTextShadow(caption, new Color(1f, 0.96f, 0.9f, onPaper ? 0.65f : 0.95f),
                    new Vector2(1.2f, -1.2f));
            }

            if (isCurrent)
                PlacePin(rootRt, mini);

            if (canClick && hit != null)
            {
                var btn = rootRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.onClick.AddListener(() => onClick());
                var colors = btn.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.75f, 0.75f, 0.82f, 1f);
                btn.colors = colors;
            }

            if (!mini)
                AddHover(rootRt.gameObject, node, canClick);
        }

        void PlaceChoiceGlow(RectTransform rootRt)
        {
            var glowSprite = MapNodeIcons.Catalog != null ? MapNodeIcons.Catalog.glow : null;
            if (glowSprite != null)
            {
                var glow = CreateImage(rootRt, "Glow", glowSprite, Color.white,
                    new Vector2(-0.4f, -0.4f), new Vector2(1.4f, 1.4f));
                glow.preserveAspect = true;
                var pulse = rootRt.gameObject.AddComponent<RunNodePulse>();
                pulse.ConfigureGlow(glow);
                return;
            }

            var fallback = BattleUIChrome.CreateGlow(rootRt, "Glow", new Vector2(-0.55f, -0.55f), new Vector2(1.55f, 1.55f),
                new Color(ChoiceOrange.r, ChoiceOrange.g, ChoiceOrange.b, 0.45f));
            var legacy = rootRt.gameObject.AddComponent<RunNodePulse>();
            legacy.Configure(fallback, null, 1.06f);
        }

        void PlaceDoneStamp(RectTransform rootRt, bool mini)
        {
            var catalog = MapNodeIcons.Catalog;
            var done = catalog == null ? null : mini ? catalog.stampDoneMini : catalog.stampDone;
            if (done != null)
            {
                var overlay = CreateImage(rootRt, "Done", done, Color.white, Vector2.zero, Vector2.one);
                overlay.preserveAspect = true;
                return;
            }

            if (mini)
                return;

            var badge = CreateRect(rootRt, "ClearedBadge", new Vector2(1, 1), new Vector2(1, 1));
            badge.sizeDelta = new Vector2(26, 26);
            badge.anchoredPosition = new Vector2(-4, -4);
            CreateImage(badge, "Bg", BattleUISprites.Circle, BattleUITheme.AccentAlly, Vector2.zero, Vector2.one);
            CreateLabel(badge, "Text", "済", 12, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.black)
                .fontStyle = FontStyle.Bold;
        }

        void PlacePin(RectTransform rootRt, bool mini)
        {
            var pin = MapNodeIcons.Catalog != null ? MapNodeIcons.Catalog.pin : null;
            if (pin != null)
            {
                var rt = CreateRect(rootRt, "Pin", new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f));
                rt.pivot = new Vector2(0.5f, 0.08f);
                rt.sizeDelta = mini ? new Vector2(14f, 30f) : new Vector2(36f, 78f);
                var image = rt.gameObject.AddComponent<Image>();
                image.sprite = pin;
                image.preserveAspect = true;
                image.raycastTarget = false;
                return;
            }

            var markerRt = CreateRect(rootRt, "PartyMarker", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            markerRt.sizeDelta = mini ? new Vector2(10f, 10f) : new Vector2(24f, 24f);
            markerRt.anchoredPosition = new Vector2(0, mini ? 8f : 20f);
            CreateImage(markerRt, "Gem", BattleUISprites.Diamond, BattleUITheme.Gold, Vector2.zero, Vector2.one);
        }

        static Vector2 StampPixelSize(Sprite sprite, bool mini)
        {
            var aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            var height = mini
                ? aspect > 1.4f ? 16f : 22f
                : aspect > 1.4f ? 72f : 84f;
            return new Vector2(height * aspect, height);
        }

        float NodeRadius(DungeonMapNode node, bool mini)
        {
            var visual = MapNodeIcons.Resolve(node.Type, IsNodeKnown(node), mini);
            if (visual.Stamp != null)
            {
                var size = StampPixelSize(visual.Stamp, mini);
                return Mathf.Min(size.x, size.y) * 0.46f;
            }

            return (mini ? 12f : NodeSize) * (node.Type == DungeonNodeType.Boss ? 0.65f : 0.5f);
        }

        void CreateBrushLine(
            RectTransform parent,
            RectTransform area,
            Vector2 fromAnchor,
            Vector2 toAnchor,
            Color color,
            float thickness,
            Sprite brush,
            bool animate)
        {
            if (brush == null)
                return;

            var a = AnchorFractionToLocal(area, fromAnchor);
            var b = AnchorFractionToLocal(area, toAnchor);
            var delta = b - a;
            var length = delta.magnitude;
            if (length < 2f)
                return;

            var rt = CreateRect(parent, "BrushLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(animate ? 0f : length, thickness);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = brush;
            image.type = Image.Type.Tiled;
            image.useSpriteMesh = false;
            image.color = color;
            image.raycastTarget = false;
            var pixelsPerUnit = Mathf.Max(0.01f, image.pixelsPerUnit);
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, brush.rect.height / (thickness * pixelsPerUnit));
            var material = BrushUiMaterial();
            if (material != null)
                image.material = material;
            if (animate)
                rt.gameObject.AddComponent<MapBrushGrow>().Play(length, RevealSeconds);
        }

        static Material _brushUiMaterial;

        static Material BrushUiMaterial()
        {
            if (_brushUiMaterial != null)
                return _brushUiMaterial;
            var loaded = Resources.Load<Material>("Map/MapBrushUi");
            if (loaded != null)
            {
                _brushUiMaterial = new Material(loaded) { hideFlags = HideFlags.HideAndDontSave };
                return _brushUiMaterial;
            }

            var shader = Shader.Find("TypingRoguelike/MapBrushUi");
            if (shader == null)
                return null;
            _brushUiMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _brushUiMaterial;
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
                    : "道がつながった先の1マスだけ種類が分かる。それ以外は未判明のまま。";
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

        static Vector2 NodePixelSize(MapNodeShape shape, bool boss)
        {
            var scale = boss ? 1.3f : 1f;
            return shape switch
            {
                MapNodeShape.Wide => new Vector2(NodeSize * 1.35f * scale, NodeSize * 0.78f * scale),
                MapNodeShape.Document => new Vector2(NodeSize * 0.78f * scale, NodeSize * 1.12f * scale),
                _ => new Vector2(NodeSize * scale, NodeSize * scale)
            };
        }

        static Vector2 NodePosition(DungeonMapNode node, int topLayer)
        {
            var span = Mathf.Max(1, node.LayoutColumns - 1);
            var x = Mathf.Lerp(MapMarginX, 1f - MapMarginX, node.Column / (float)span) + node.DisplayOffsetX;
            var y = LayerY(node.Layer, topLayer) + node.DisplayOffsetY;
            return new Vector2(Mathf.Clamp(x, 0.08f, 0.92f), Mathf.Clamp(y, 0.04f, 0.96f));
        }

        static float LayerY(int layer, int topLayer) =>
            Mathf.Lerp(MapMarginBottom, 1f - MapMarginTop, layer / (float)topLayer);

        static string NodeGlyph(DungeonNodeType type) => MapNodeIcons.Glyph(type);

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
            DungeonNodeType.EventV2 => "イベント",
            DungeonNodeType.Mystery => "謎",
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
                "戦闘にならなければ、狂気・通貨・HPのどれかが変わる。勝利で塔片。",
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
            DungeonNodeType.EventV2 => "その階の出来事が起きる",
            DungeonNodeType.Mystery => "踏むまで中身が分からない",
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
            DungeonNodeType.EventV2 => new Color(0.72f, 0.48f, 0.95f),
            DungeonNodeType.Mystery => new Color(0.55f, 0.45f, 0.78f),
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
                ? "橙のマスを選んで上の段へ進もう。\n先のマスは？で隠れる。"
                : "進行データが不正です。ランを最初からやり直してください。";
        }

        void EnsureMinimapRoot()
        {
            if (_minimapRoot != null || _host == null)
                return;

            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("DungeonMinimapCanvas");
            go.transform.SetParent(_host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _minimapRoot = go.GetComponent<RectTransform>();
            var hotkey = go.AddComponent<MapOverlayHotkey>();
            hotkey.OnToggle = ToggleOverlay;
        }

        GameObject BuildMinimapPanel()
        {
            var panel = CreateRect(_minimapRoot, "Minimap", new Vector2(0.74f, 0.58f), new Vector2(0.985f, 0.90f));
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.025f, 0.05f, 0.92f);
            BattleUITheme.EnsureSprite(background);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(ChoiceOrange.r, ChoiceOrange.g, ChoiceOrange.b, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(ToggleOverlay);
            var label = CreateLabel(panel, "Caption", "地図", 14, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.86f), new Vector2(0.7f, 0.98f), BattleUITheme.Gold);
            label.fontStyle = FontStyle.Bold;

            var area = CreateRect(panel, "Nodes", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.84f));
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            DrawMinimap(area);
            return panel.gameObject;
        }

        GameObject BuildMapTabHint()
        {
            var panel = CreateRect(_minimapRoot, "MapTabHint", new Vector2(0.80f, 0.935f), new Vector2(0.985f, 0.985f));
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.04f, 0.03f, 0.78f);
            BattleUITheme.EnsureSprite(image);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(ToggleOverlay);
            var label = CreateLabel(panel, "Caption", "Tab：地図", 16, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, BattleUITheme.Gold);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            return panel.gameObject;
        }

        void DrawMinimap(RectTransform area)
        {
            if (_dungeon?.Nodes == null)
                return;

            _reachableNextIds = new HashSet<int>(_dungeon.GetReachableNext().Select(node => node.Id));
            var layer = _dungeon.Current?.Layer ?? 0;
            var nodes = _dungeon.Nodes.Values.Where(node => Mathf.Abs(node.Layer - layer) <= 1).ToList();
            var minLayer = nodes.Select(node => node.Layer).DefaultIfEmpty(layer).Min();
            var maxLayer = nodes.Select(node => node.Layer).DefaultIfEmpty(layer).Max();
            var span = Mathf.Max(1, maxLayer - minLayer);
            var reachable = _reachableNextIds;
            var positions = new Dictionary<int, Vector2>();
            foreach (var node in nodes)
            {
                var columns = Mathf.Max(1, node.LayoutColumns - 1);
                var x = Mathf.Lerp(0.12f, 0.88f, node.Column / (float)columns) + node.DisplayOffsetX;
                var y = Mathf.Lerp(0.16f, 0.84f, (node.Layer - minLayer) / (float)span) + node.DisplayOffsetY * 2f;
                positions[node.Id] = new Vector2(Mathf.Clamp(x, 0.08f, 0.92f), Mathf.Clamp(y, 0.1f, 0.9f));
            }

            var brush = MapNodeIcons.Catalog != null ? MapNodeIcons.Catalog.brushLine : null;
            var reveal = _animateReveal;
            _animateReveal = false;
            foreach (var from in nodes)
            {
                foreach (var nextId in from.Next)
                {
                    if (!positions.ContainsKey(nextId) || !positions.ContainsKey(from.Id))
                        continue;
                    if (!_dungeon.Nodes.TryGetValue(nextId, out var to))
                        continue;
                    var choice = from.Id == _dungeon.CurrentNodeId && reachable.Contains(nextId);
                    var traveled = from.Cleared && (to.Cleared || to.Id == _dungeon.CurrentNodeId);
                    var color = choice ? ChoiceOrange : traveled ? InkDark : InkLightFaint;
                    if (brush != null)
                    {
                        var thickness = choice ? MiniLineThicknessChoice : traveled ? MiniLineThickness : MiniLineThickness * 0.65f;
                        CreateBrushLine(area, area, positions[from.Id], positions[nextId], color,
                            thickness, brush, false);
                    }
                    else
                    {
                        CreateRouteSegment(area, area, positions[from.Id], positions[nextId], color,
                            choice ? MiniLineThicknessChoice : traveled ? 2.5f : 1.6f,
                            12f, 12f, dashed: !choice && !traveled);
                    }
                }
            }

            foreach (var node in nodes)
                PlaceNodeInk(area, node, positions[node.Id], mini: true);

            foreach (var node in nodes)
            {
                var choice = reachable.Contains(node.Id);
                PaintNode(area, node, positions[node.Id], mini: true, choice, false, null);
            }

            _animateReveal = reveal;
        }

        static Vector2 MiniSize(MapNodeShape shape) => shape switch
        {
            MapNodeShape.Wide => new Vector2(28f, 14f),
            MapNodeShape.Document => new Vector2(14f, 20f),
            _ => new Vector2(18f, 18f)
        };

        void ToggleOverlay()
        {
            if (_minimapRoot == null || _dungeon == null)
                return;
            if (_overlayPanel != null)
                CloseOverlay();
            else
                OpenOverlay();
        }

        void OpenOverlay()
        {
            if (_host == null || _dungeon == null || _overlayPanel != null)
                return;

            _currentLayer = _dungeon.Current?.Layer ?? 0;
            _pausedByOverlay = true;
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            IsOverlayOpen = true;
            if (_minimapPanel != null)
                _minimapPanel.SetActive(false);

            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("DungeonMapOverlayCanvas");
            go.transform.SetParent(_host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _overlayPanel = go;

            var dim = CreateRect(go.transform, "Dim", Vector2.zero, Vector2.one);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0.01f, 0.012f, 0.03f, 0.92f);
            BattleUITheme.EnsureSprite(dimImage);
            dimImage.raycastTarget = true;

            var window = BattleUIChrome.CreateLuxuryWindow(
                go.transform, "PeekMap", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f),
                "MAP", BattleUITheme.Arcane);
            var body = window.Find("Body");
            var topLayer = _dungeon.Nodes.Values.Select(node => node.Layer).DefaultIfEmpty(0).Max();
            if (body != null)
                BuildTowerMap(body, _dungeon, topLayer, null, interactive: false);
            CreateLabel(go.transform, "Hint", "Tab または右上の地図で閉じる", 16, TextAnchor.LowerCenter,
                new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.05f), BattleUITheme.TextMuted);
        }

        void CloseOverlay()
        {
            if (_overlayPanel != null)
                UnityEngine.Object.Destroy(_overlayPanel);
            _overlayPanel = null;
            if (_pausedByOverlay)
            {
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
                _pausedByOverlay = false;
            }

            IsOverlayOpen = false;
            if (_minimapPanel != null)
                _minimapPanel.SetActive(true);
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

    sealed class MapOverlayHotkey : MonoBehaviour
    {
        public Action OnToggle;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame)
                return;
            OnToggle?.Invoke();
        }
    }

    public sealed class RunNodePulse : MonoBehaviour
    {
        Image _glow;
        Image _gem;
        float _maxScale = 1.03f;
        Vector3 _baseScale;
        float _phase;
        bool _glowOnly;

        public void Configure(Image glow, Image gem, float maxScale)
        {
            _glow = glow;
            _gem = gem;
            _maxScale = maxScale;
            _baseScale = transform.localScale;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            _glowOnly = false;
        }

        public void ConfigureGlow(Image glow)
        {
            _glow = glow;
            _gem = null;
            _glowOnly = true;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            var wave = (Mathf.Sin(Time.unscaledTime * (_glowOnly ? 1.15f : 2.8f) + _phase) + 1f) * 0.5f;
            if (!_glowOnly)
                transform.localScale = _baseScale * Mathf.Lerp(1f, _maxScale, wave);
            if (_glow != null)
            {
                var color = _glow.color;
                color.a = _glowOnly ? Mathf.Lerp(0.28f, 0.92f, wave) : Mathf.Lerp(0.08f, 0.34f, wave);
                _glow.color = color;
            }
            if (!_glowOnly && _gem != null)
                _gem.transform.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * 24f);
        }
    }

    sealed class MapBrushGrow : MonoBehaviour
    {
        RectTransform _rect;
        float _length;
        float _start;
        float _duration = 0.3f;

        public void Play(float length, float duration)
        {
            _rect = transform as RectTransform;
            _length = length;
            _duration = Mathf.Max(0.05f, duration);
            _start = Time.unscaledTime;
        }

        void Update()
        {
            if (_rect == null)
            {
                enabled = false;
                return;
            }

            var t = Mathf.Clamp01((Time.unscaledTime - _start) / _duration);
            var eased = t * t * (3f - 2f * t);
            var size = _rect.sizeDelta;
            size.x = _length * eased;
            _rect.sizeDelta = size;
            if (t >= 1f)
                enabled = false;
        }
    }

    sealed class MapRevealPlayback : MonoBehaviour
    {
        CanvasGroup[] _inks;
        CanvasGroup[] _stamps;
        float _start;
        float _duration = 0.3f;

        public void Play(List<CanvasGroup> inks, List<CanvasGroup> stamps, float duration)
        {
            _inks = inks != null ? inks.ToArray() : Array.Empty<CanvasGroup>();
            _stamps = stamps != null ? stamps.ToArray() : Array.Empty<CanvasGroup>();
            _duration = Mathf.Max(0.05f, duration);
            _start = Time.unscaledTime;
            foreach (var stamp in _stamps)
            {
                if (stamp != null)
                    stamp.alpha = 0f;
            }
        }

        void Update()
        {
            var t = Mathf.Clamp01((Time.unscaledTime - _start) / _duration);
            var eased = t * t * (3f - 2f * t);
            if (_inks != null)
            {
                foreach (var ink in _inks)
                {
                    if (ink == null)
                        continue;
                    ink.alpha = 1f - eased;
                    ink.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, eased);
                }
            }

            if (_stamps != null)
            {
                foreach (var stamp in _stamps)
                {
                    if (stamp != null)
                        stamp.alpha = eased;
                }
            }

            if (t < 1f)
                return;

            if (_inks != null)
            {
                foreach (var ink in _inks)
                {
                    if (ink == null)
                        continue;
                    ink.alpha = 0f;
                    ink.transform.localScale = Vector3.one;
                }
            }

            if (_stamps != null)
            {
                foreach (var stamp in _stamps)
                {
                    if (stamp != null)
                        stamp.alpha = 1f;
                }
            }

            enabled = false;
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
