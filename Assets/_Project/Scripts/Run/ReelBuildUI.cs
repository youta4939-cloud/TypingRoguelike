using System;
using System.Collections.Generic;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>パーツを3列×6行に置いてリールを作る。同じパーツは1つだけ。マイセットの枠だけ空けておく。</summary>
    public sealed class ReelBuildUI
    {
        static readonly Color Page = new(0.05f, 0.035f, 0.03f, 1f);
        static readonly Color Panel = new(0.1f, 0.075f, 0.05f, 0.96f);
        static readonly Color Ink = new(0.93f, 0.9f, 0.82f, 1f);
        static readonly Color Muted = new(0.62f, 0.56f, 0.48f, 1f);
        static readonly Color Valid = new(0.45f, 0.95f, 0.5f, 0.42f);
        static readonly Color Invalid = new(0.95f, 0.22f, 0.18f, 0.48f);
        static readonly Color Penalty = new(0.55f, 0.08f, 0.1f, 1f);

        GameObject _root;
        GameObject _panel;
        GameObject _modal;
        RectTransform _board;
        RectTransform _list;
        Image[,] _highlights;
        ReelPartCatalog _catalog;
        ReelLayout _layout;
        bool _debugUnlocked;
        int _layer;
        DungeonNodeType _fourth;
        LayerConfig _config;
        BoardCatalog _boards;
        Action<ReelLayout, bool> _onDepart;
        Action<BoardGrid> _onDebugRolls;
        bool _dragging;
        string _dragPartId;
        int _dragIndex = -1;
        int _dragLocalX;
        int _dragLocalY;
        Text _dragHold;
        Text _tip;
        int _filter;
        int[,] _counts;
        YakuCatalog _yaku;
        readonly List<OddsFlash> _flashes = new();
        Sheet _sheet;

        enum Sheet { None, Detail, Tools }

        public void Show(
            Transform host,
            ReelPartCatalog catalog,
            ReelLayout layout,
            bool debugUnlocked,
            int layer,
            int totalLayers,
            DungeonNodeType fourth,
            LayerConfig config,
            BoardCatalog boards,
            YakuCatalog yaku,
            Action<ReelLayout, bool> onDepart,
            Action<BoardGrid> onDebugRolls)
        {
            _ = totalLayers;
            ReelBoardSettings.Apply();
            EnsureRoot(host);
            _catalog = catalog ?? ReelPartCatalog.Fallback();
            _layout = layout ?? ReelLayout.BalanceStarter(_catalog);
            _debugUnlocked = debugUnlocked;
            _layout.KeepWithinOwned(_catalog, _debugUnlocked);
            _layer = layer;
            _fourth = fourth;
            _config = config;
            _boards = boards;
            _yaku = yaku;
            _onDepart = onDepart;
            _onDebugRolls = onDebugRolls;
            _counts = CaptureCounts();
            _flashes.Clear();
            _sheet = Sheet.None;
            BindClock();
            Rebuild();
        }

        public void Hide()
        {
            HideModal();
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
            }

            _dragging = false;
        }

        void Rebuild()
        {
            HideModal();
            _dragging = false;
            _dragHold = null;
            foreach (var flash in _flashes)
                flash.Lines.Clear();
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            _panel = FullScreen("ReelBuild", Color.white, "bg_chamber");
            var title = Label(_panel.transform, "Title", $"第{_layer}層　リール編成", 28, TextAnchor.MiddleCenter,
                new Vector2(0.24f, 0.905f), new Vector2(0.76f, 0.985f), BattleUITheme.Gold, true);
            Emboss(title, new Color(0f, 0f, 0f, 0.85f));

            BuildList(new Vector2(0.012f, 0.135f), new Vector2(0.232f, 0.89f));
            BuildBoard(new Vector2(0.242f, 0.135f), new Vector2(0.735f, 0.89f));
            BuildRail(new Vector2(0.745f, 0.135f), new Vector2(0.988f, 0.89f));
            if (_sheet == Sheet.Detail)
                BuildDetail();
            BuildDock();
            if (_sheet == Sheet.Tools)
                BuildTools();
        }

        void OpenSheet(Sheet sheet)
        {
            _sheet = _sheet == sheet ? Sheet.None : sheet;
            Rebuild();
        }

        void BuildList(Vector2 min, Vector2 max)
        {
            var panel = PanelOf(_panel.transform, "PartList", min, max, "plate_list");
            _list = panel;
            var heading = Label(panel, "Heading", "パーツ", 18, TextAnchor.MiddleLeft,
                new Vector2(0.09f, 0.875f), new Vector2(0.42f, 0.935f), BattleUITheme.Gold, true);
            Emboss(heading, new Color(0f, 0f, 0f, 0.7f));
            Label(panel, "Hint", "重ねると入れ替わる", 11, TextAnchor.MiddleRight,
                new Vector2(0.40f, 0.875f), new Vector2(0.92f, 0.935f), Muted);
            BuildTabs(panel);

            var parts = VisibleParts();
            var columns = 3;
            var rows = Mathf.Max(1, Mathf.CeilToInt(parts.Count / (float)columns));
            var top = 0.73f;
            var bottom = 0.16f;
            var rowH = (top - bottom) / rows;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var col = i % columns;
                var row = i / columns;
                var xMin = 0.08f + col * 0.30f;
                var yMax = top - row * rowH;
                var card = Rect(panel, "Part_" + part.Id, new Vector2(xMin, yMax - rowH + 0.008f), new Vector2(xMin + 0.28f, yMax - 0.006f));
                var plate = card.gameObject.AddComponent<Image>();
                Paint(plate, part.HasPenalty ? "card_gold" : "card_relic");
                var placed = _layout.Remaining(_catalog, part.Id, _debugUnlocked) <= 0;
                if (placed)
                {
                    var group = card.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0.45f;
                    group.blocksRaycasts = true;
                }

                DrawMini(card, part, true);
                if (placed)
                {
                    var mark = Label(card, "Used", "使用中", 11, TextAnchor.LowerCenter,
                        new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.34f), Muted);
                    mark.fontStyle = FontStyle.Bold;
                }

                var tip = card.gameObject.AddComponent<ReelPartTip>();
                tip.Owner = this;
                tip.Text = part.Name + "\n" + CellList(part);
                if (!placed)
                    BindDrag(card, part.Id, -1, 0, 0, null);
            }

            _tip = Label(panel, "Tip", "カーソルを合わせると、名前と中身が出る", 12, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.075f), new Vector2(0.92f, 0.15f), Muted);
            Label(panel, "Locked", $"未解放 {LockedCount()}個", 13, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.075f), Muted);
        }

        void BuildBoard(Vector2 min, Vector2 max)
        {
            var panel = PanelOf(_panel.transform, "Board", min, max, "plate_side");
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                var xMin = 0.09f + column * 0.28f;
                var headText = Label(panel, "Col" + column, $"{column + 1}マス目", 16, TextAnchor.MiddleCenter,
                    new Vector2(xMin, 0.875f), new Vector2(xMin + 0.26f, 0.935f), BattleUITheme.Gold, true);
                Emboss(headText, new Color(0f, 0f, 0f, 0.75f));
            }

            var grid = Rect(panel, "Cells", new Vector2(0.08f, 0.09f), new Vector2(0.92f, 0.86f));
            _board = grid;
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                var well = Rect(grid, "Well" + column,
                    new Vector2(column / (float)BoardGrid.Columns, 0f),
                    new Vector2((column + 1) / (float)BoardGrid.Columns, 1f));
                var wellImage = well.gameObject.AddComponent<Image>();
                wellImage.raycastTarget = false;
                Paint(wellImage, "column_well");
            }

            _highlights = new Image[BoardGrid.Columns, BoardGrid.Rows];
            for (var row = 0; row < BoardGrid.Rows; row++)
            {
                for (var column = 0; column < BoardGrid.Columns; column++)
                    BuildCell(grid, column, row);
            }

            Label(panel, "EmptyHint", "空いたマスは、罠として出る", 13, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.045f), new Vector2(0.9f, 0.1f), Muted);
        }

        void BuildCell(RectTransform grid, int column, int row)
        {
            var xMin = column / (float)BoardGrid.Columns;
            var xMax = (column + 1) / (float)BoardGrid.Columns;
            var yMax = 1f - row / (float)BoardGrid.Rows;
            var yMin = 1f - (row + 1) / (float)BoardGrid.Rows;
            var inset = 0.16f;
            var cellMin = new Vector2(Mathf.Lerp(xMin, xMax, inset), Mathf.Lerp(yMin, yMax, inset));
            var cellMax = new Vector2(Mathf.Lerp(xMin, xMax, 1f - inset), Mathf.Lerp(yMin, yMax, 1f - inset));
            var shade = Rect(grid, "Shade", new Vector2(cellMin.x + 0.012f, cellMin.y - 0.018f), new Vector2(cellMax.x + 0.012f, cellMax.y - 0.012f));
            var shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.raycastTarget = false;
            shadeImage.color = new Color(0f, 0f, 0f, 0.55f);
            BattleUITheme.EnsureSprite(shadeImage);
            var cell = Rect(grid, "Cell", cellMin, cellMax);
            var image = cell.gameObject.AddComponent<Image>();
            var index = _layout.FindIndexAt(_catalog, column, row);
            var placement = index >= 0 ? _layout.Placements[index] : null;
            var part = placement != null ? _catalog.Find(placement.PartId) : null;
            ReelCell shape = null;
            if (part != null)
            {
                foreach (var piece in part.Cells)
                {
                    if (piece != null && placement.X + piece.X == column && placement.Y + piece.Y == row)
                        shape = piece;
                }
            }

            var empty = shape == null;
            var type = empty ? DungeonNodeType.Trap : shape.Type;
            var art = empty ? ReelBuildArt.Get("socket_empty") : ReelBuildArt.Tile(type);
            if (art != null)
            {
                image.sprite = art;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = empty ? new Color(0.08f, 0.06f, 0.05f, 1f) : TypeColor(type);
                BattleUITheme.EnsureSprite(image);
                Label(cell, "Mark", empty ? "罠" : Mark(type), empty ? 14 : 20, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, empty ? new Color(0.35f, 0.28f, 0.24f, 1f) : Color.white);
            }

            if (!empty && part.HasPenalty)
                Frame(cell.gameObject, type == DungeonNodeType.Trap ? Penalty : BattleUITheme.Gold, 2.2f);
            var highlight = Rect(cell, "Preview", Vector2.zero, Vector2.one);
            var hi = highlight.gameObject.AddComponent<Image>();
            hi.color = new Color(1f, 1f, 1f, 0f);
            hi.raycastTarget = false;
            BattleUITheme.EnsureSprite(hi);
            _highlights[column, row] = hi;
            if (placement != null && shape != null)
                BindDrag(cell, placement.PartId, index, shape.X, shape.Y, null);
        }

        void BuildRail(Vector2 min, Vector2 max)
        {
            var rail = Rect(_panel.transform, "Rail", min, max);
            var icon = Rect(rail, "FourthIcon", new Vector2(0.04f, 0.78f), new Vector2(0.38f, 0.98f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            var tile = ReelBuildArt.Tile(_fourth);
            if (tile != null)
            {
                iconImage.sprite = tile;
                iconImage.preserveAspect = true;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.color = TypeColor(_fourth);
                BattleUITheme.EnsureSprite(iconImage);
            }

            Label(rail, "FourthHead", "4マス目", 13, TextAnchor.MiddleLeft,
                new Vector2(0.42f, 0.90f), new Vector2(0.98f, 0.98f), Muted);
            var fourthName = Label(rail, "FourthName", LayerPathUI.SquareName(_fourth), 20, TextAnchor.MiddleLeft,
                new Vector2(0.42f, 0.82f), new Vector2(0.98f, 0.91f), Ink, true);
            Emboss(fourthName, new Color(0f, 0f, 0f, 0.75f));
            Label(rail, "Advice", ReelBoardSettings.AdviceFor(_fourth), 13, TextAnchor.UpperLeft,
                new Vector2(0.42f, 0.70f), new Vector2(0.98f, 0.82f), BattleUITheme.Gold);

            var grid = _layout.ToGrid(_catalog);
            var shortages = LayerSquares.ShortageLines(grid, _config, _layer);
            Label(rail, "MinLine", MinimumLine(grid), 14, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.62f), new Vector2(0.98f, 0.69f),
                shortages.Count == 0 ? new Color(0.55f, 0.9f, 0.55f, 1f) : new Color(1f, 0.45f, 0.4f, 1f), true);
            var empty = _layout.EmptyCells(_catalog);
            if (empty > 0)
            {
                Label(rail, "EmptyLine", $"空き {empty}（罠として出る）", 12, TextAnchor.MiddleLeft,
                    new Vector2(0.04f, 0.57f), new Vector2(0.98f, 0.62f), Muted);
            }

            Label(rail, "OddsHead", "出現", 13, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.50f), new Vector2(0.98f, 0.56f), Muted);
            BuildColumnOdds(rail, grid, 0.48f);
        }

        void BuildColumnOdds(RectTransform parent, BoardGrid grid, float top)
        {
            var types = BoardGrid.RoadTypes();
            var rowH = 0.062f;
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                var xMin = 0.02f + column * 0.33f;
                var xMax = xMin + 0.31f;
                Label(parent, "ColOdds" + column, $"{column + 1}", 12, TextAnchor.MiddleCenter,
                    new Vector2(xMin, top - rowH), new Vector2(xMax, top), BattleUITheme.Gold, true);
                for (var i = 0; i < types.Length; i++)
                {
                    var yMax = top - rowH * (i + 1);
                    var count = grid.Count(column, types[i]);
                    var label = Label(parent, "Odds" + column + "_" + i, $"{Mark(types[i])} {OddsText(count, true)}", 11,
                        TextAnchor.MiddleCenter, new Vector2(xMin, yMax - rowH), new Vector2(xMax, yMax), Ink);
                    StyleOdds(label, column, i, count);
                }
            }
        }

        void BuildDock()
        {
            var dock = Rect(_panel.transform, "Dock", Vector2.zero, Vector2.one);
            TextLink(dock, "全部外す", new Vector2(0.02f, 0.025f), new Vector2(0.12f, 0.11f), () =>
            {
                _layout.Clear();
                CommitLayout();
            });
            TextLink(dock, "くわしく", new Vector2(0.13f, 0.025f), new Vector2(0.22f, 0.11f), () => OpenSheet(Sheet.Detail), _sheet == Sheet.Detail);
            TextLink(dock, "調整", new Vector2(0.23f, 0.025f), new Vector2(0.30f, 0.11f), () => OpenSheet(Sheet.Tools));
            TextLink(dock, "マイセット", new Vector2(0.31f, 0.025f), new Vector2(0.42f, 0.11f), () => OpenSheet(Sheet.Tools));
            var shortages = LayerSquares.ShortageLines(_layout.ToGrid(_catalog), _config, _layer);
            var ready = shortages.Count == 0;
            if (ready)
            {
                Button(dock, "出発", new Vector2(0.74f, 0.02f), new Vector2(0.97f, 0.115f), TryDepart, "btn_gold", 22, true, true);
            }
            else
            {
                TextLink(dock, "出発できない", new Vector2(0.74f, 0.02f), new Vector2(0.97f, 0.115f), TryDepart, false, 18,
                    new Color(1f, 0.45f, 0.4f, 1f));
            }
        }

        void BuildDetail()
        {
            var window = PanelOf(_panel.transform, "Detail", new Vector2(0.18f, 0.15f), new Vector2(0.82f, 0.88f), "plate_board");
            var heading = Label(window, "Title", "くわしく", 22, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.88f), new Vector2(0.4f, 0.96f), BattleUITheme.Gold, true);
            Emboss(heading, new Color(0f, 0f, 0f, 0.75f));
            TextLink(window, "閉じる", new Vector2(0.78f, 0.88f), new Vector2(0.94f, 0.96f), () => OpenSheet(Sheet.Detail));

            var grid = _layout.ToGrid(_catalog);
            var types = BoardGrid.RoadTypes();
            Label(window, "H0", "1マス目", 12, TextAnchor.MiddleCenter, new Vector2(0.28f, 0.82f), new Vector2(0.48f, 0.87f), Muted);
            Label(window, "H1", "2マス目", 12, TextAnchor.MiddleCenter, new Vector2(0.48f, 0.82f), new Vector2(0.70f, 0.87f), Muted);
            Label(window, "H2", "3マス目", 12, TextAnchor.MiddleCenter, new Vector2(0.70f, 0.82f), new Vector2(0.94f, 0.87f), Muted);
            for (var i = 0; i < types.Length; i++)
            {
                var yMax = 0.81f - i * 0.045f;
                var yMin = yMax - 0.042f;
                Label(window, "Type" + i, LayerPathUI.SquareName(types[i]), 14, TextAnchor.MiddleLeft,
                    new Vector2(0.08f, yMin), new Vector2(0.26f, yMax), Ink);
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    var count = grid.Count(column, types[i]);
                    var xMin = 0.28f + column * 0.22f;
                    var label = Label(window, "DetailOdds" + i + "_" + column, OddsText(count, false), 14, TextAnchor.MiddleCenter,
                        new Vector2(xMin, yMin), new Vector2(xMin + 0.2f, yMax), Ink, true);
                    StyleOdds(label, column, i, count);
                }
            }

            var shares = YakuEvaluator.Shares(grid, _yaku);
            var yakuTitle = Label(window, "YakuTitle", "役の確率", 16, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.52f), new Vector2(0.5f, 0.57f), BattleUITheme.Gold, true);
            Emboss(yakuTitle, new Color(0f, 0f, 0f, 0.7f));
            Label(window, "YakuHint", $"{BoardGrid.Rows * BoardGrid.Rows * BoardGrid.Rows}通り", 12, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0.52f), new Vector2(0.94f, 0.57f), Muted);
            for (var i = 0; i < shares.Count && i < 8; i++)
            {
                var yMax = 0.51f - i * 0.032f;
                Label(window, "Yaku" + i, $"{shares[i].Name}    {shares[i].Percent:0.0}%", 15, TextAnchor.MiddleLeft,
                    new Vector2(0.08f, yMax - 0.033f), new Vector2(0.94f, yMax), Ink);
            }

            var minTitle = Label(window, "MinTitle", "最低マス数", 16, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.24f), new Vector2(0.5f, 0.29f), BattleUITheme.Gold, true);
            Emboss(minTitle, new Color(0f, 0f, 0f, 0.7f));
            var shortages = LayerSquares.ShortageLines(grid, _config, _layer);
            var rules = _config != null ? _config.MinimumsFor(_layer) : new List<LayerTypeMinimum>();
            for (var i = 0; i < rules.Count && i < 4; i++)
            {
                var rule = rules[i];
                var yMax = 0.23f - i * 0.04f;
                var worst = false;
                var bits = new List<string>();
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    var have = grid.Count(column, rule.type);
                    bits.Add($"{have}/{rule.minimumPerColumn}");
                    if (have < rule.minimumPerColumn)
                        worst = true;
                }

                Label(window, "Min" + i,
                    $"{LayerPathUI.SquareName(rule.type)}  {string.Join("   ", bits)}  {(worst ? "不足" : "足りている")}",
                    15, TextAnchor.MiddleLeft, new Vector2(0.08f, yMax - 0.038f), new Vector2(0.94f, yMax),
                    worst ? new Color(1f, 0.45f, 0.4f, 1f) : new Color(0.55f, 0.9f, 0.55f, 1f));
            }

            var note = shortages.Count == 0 ? "最低マス数を満たしています。" : string.Join("\n", shortages);
            var empty = _layout.EmptyCells(_catalog);
            if (empty > 0)
                note += $"\n空きマスが{empty}つあります（空きは罠として出ます）";
            Label(window, "MinNote", note, 13, TextAnchor.UpperLeft,
                new Vector2(0.1f, 0.11f), new Vector2(0.9f, 0.20f), shortages.Count == 0 ? Muted : new Color(1f, 0.55f, 0.45f, 1f));
            Label(window, "Legend", "金枠は強いパーツ。緑は置ける場所、赤は置けない場所。", 12, TextAnchor.MiddleLeft,
                new Vector2(0.1f, 0.07f), new Vector2(0.9f, 0.11f), Muted);
        }

        void BuildTools()
        {
            var dim = FullScreen("ToolsDim", new Color(0f, 0f, 0f, 0.62f));
            dim.transform.SetParent(_panel.transform, false);
            var window = PanelOf(_panel.transform, "Tools", new Vector2(0.28f, 0.16f), new Vector2(0.72f, 0.86f), "plate_list");
            var heading = Label(window, "Title", "調整", 24, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.88f), new Vector2(0.7f, 0.96f), BattleUITheme.Gold, true);
            Emboss(heading, new Color(0f, 0f, 0f, 0.75f));
            TextLink(window, "閉じる", new Vector2(0.72f, 0.88f), new Vector2(0.94f, 0.96f), () => OpenSheet(Sheet.Tools));

            Label(window, "DebugHead", "デバッグ", 14, TextAnchor.MiddleLeft,
                new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.85f), Muted);
            TextLink(window, _debugUnlocked ? "全パーツ解放済み" : "全パーツ解放", new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.77f), () =>
            {
                if (_debugUnlocked)
                    return;
                _debugUnlocked = true;
                _sheet = Sheet.Tools;
                Rebuild();
            }, _debugUnlocked, 18);
            TextLink(window, "戦闘寄り", new Vector2(0.1f, 0.58f), new Vector2(0.4f, 0.67f), () => LoadDebugPreset("combat"));
            TextLink(window, "回復寄り", new Vector2(0.4f, 0.58f), new Vector2(0.65f, 0.67f), () => LoadDebugPreset("rest"));
            TextLink(window, "宝寄り", new Vector2(0.65f, 0.58f), new Vector2(0.9f, 0.67f), () => LoadDebugPreset("treasure"));
            TextLink(window, "1000回抽選", new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.57f), () =>
                _onDebugRolls?.Invoke(_layout.ToGrid(_catalog)), false, 18);

            Label(window, "SetTitle", "マイセット（準備中）", 14, TextAnchor.MiddleLeft,
                new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.44f), Muted);
            for (var i = 0; i < 3; i++)
            {
                var xMin = 0.1f + i * 0.28f;
                var slot = Rect(window, "SetSlot" + (i + 1), new Vector2(xMin, 0.16f), new Vector2(xMin + 0.25f, 0.34f));
                var slotImage = slot.gameObject.AddComponent<Image>();
                slotImage.raycastTarget = false;
                slotImage.color = new Color(0f, 0f, 0f, 0.35f);
                BattleUITheme.EnsureSprite(slotImage);
                Label(slot, "Name", $"枠{i + 1}", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Muted);
            }
        }

        void LoadDebugPreset(string id)
        {
            var preset = _boards != null ? _boards.Find(id) : null;
            if (preset == null)
                return;
            var errors = new List<string>();
            if (!_layout.TryLoadPreset(preset.ToGrid(), _catalog, _debugUnlocked, errors))
            {
                ShowModal("読み込めません", errors.Count > 0 ? string.Join("\n", errors) : "この並べ方は読み込めません。");
                return;
            }

            _sheet = Sheet.None;
            CommitLayout();
        }

        string MinimumLine(BoardGrid grid)
        {
            var rules = _config != null ? _config.MinimumsFor(_layer) : new List<LayerTypeMinimum>();
            if (rules.Count == 0)
                return "最低マス数の指定はありません";
            var rule = rules[0];
            var bits = new List<string>();
            for (var column = 0; column < BoardGrid.Columns; column++)
                bits.Add($"{grid.Count(column, rule.type)}/{rule.minimumPerColumn}");
            return $"{LayerPathUI.SquareName(rule.type)}  {string.Join("   ", bits)}";
        }

        void TryDepart()
        {
            var grid = _layout.ToGrid(_catalog);
            var shortages = LayerSquares.ShortageLines(grid, _config, _layer);
            if (shortages.Count > 0)
            {
                var empty = _layout.EmptyCells(_catalog);
                var body = string.Join("\n", shortages);
                if (empty > 0)
                    body += $"\n\n空きマスが{empty}つあります（空きは罠として出ます）";
                ShowModal("出発できません", body);
                return;
            }

            var layout = _layout;
            var unlocked = _debugUnlocked;
            Hide();
            _onDepart?.Invoke(layout, unlocked);
        }

        void BeginDrag(string partId, int placementIndex, int localX, int localY, Text hold)
        {
            if (placementIndex < 0 && _layout.Remaining(_catalog, partId, _debugUnlocked) <= 0)
                return;

            _dragging = true;
            _dragPartId = partId;
            _dragIndex = placementIndex;
            _dragLocalX = localX;
            _dragLocalY = localY;
            _dragHold = hold;
            if (hold != null)
                hold.text = "つかみ中";
        }

        void Drag(PointerEventData eventData)
        {
            if (!_dragging)
                return;
            ClearPreview();
            if (!TryCell(eventData, out var column, out var row))
                return;
            var originX = column - _dragLocalX;
            var originY = row - _dragLocalY;
            var part = _catalog.Find(_dragPartId);
            if (part == null)
                return;
            var ok = _layout.CanDrop(_catalog, part, originX, originY, _dragIndex, _debugUnlocked);
            foreach (var cell in part.Cells)
            {
                if (cell == null)
                    continue;
                var x = originX + cell.X;
                var y = originY + cell.Y;
                if (x < 0 || x >= BoardGrid.Columns || y < 0 || y >= BoardGrid.Rows)
                    continue;
                var image = _highlights[x, y];
                if (image == null)
                    continue;
                image.color = ok ? Valid : Invalid;
            }
        }

        void EndDrag(PointerEventData eventData)
        {
            if (!_dragging)
                return;
            var partId = _dragPartId;
            var index = _dragIndex;
            var localX = _dragLocalX;
            var localY = _dragLocalY;
            _dragging = false;
            ClearPreview();
            if (_dragHold != null)
                _dragHold.text = "";
            var part = _catalog.Find(partId);
            var overList = Contains(_list, eventData);
            if (index >= 0 && overList)
            {
                _layout.RemoveAt(index);
                CommitLayout();
                return;
            }

            if (part != null && TryCell(eventData, out var column, out var row))
            {
                var originX = column - localX;
                var originY = row - localY;
                if (_layout.TryDrop(_catalog, part, originX, originY, index, _debugUnlocked))
                {
                    CommitLayout();
                    return;
                }
            }

            if (_dragHold != null && index < 0)
                _dragHold.text = HoldText(partId);
        }

        string HoldText(string partId)
        {
            var left = _layout != null ? _layout.Remaining(_catalog, partId, _debugUnlocked) : 0;
            return left <= 0 ? "使用中" : "";
        }

        void ClearPreview()
        {
            if (_highlights == null)
                return;
            foreach (var image in _highlights)
            {
                if (image != null)
                    image.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        bool TryCell(PointerEventData eventData, out int column, out int row)
        {
            column = -1;
            row = -1;
            if (_board == null)
                return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_board, eventData.position, eventData.pressEventCamera))
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_board, eventData.position, eventData.pressEventCamera, out var local))
                return false;
            var rect = _board.rect;
            var x = local.x + rect.width * 0.5f;
            var y = rect.height * 0.5f - local.y;
            if (rect.width <= 0f || rect.height <= 0f)
                return false;
            column = Mathf.Clamp(Mathf.FloorToInt(x / (rect.width / BoardGrid.Columns)), 0, BoardGrid.Columns - 1);
            row = Mathf.Clamp(Mathf.FloorToInt(y / (rect.height / BoardGrid.Rows)), 0, BoardGrid.Rows - 1);
            return true;
        }

        static bool Contains(RectTransform rect, PointerEventData eventData) =>
            rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera);

        void BindDrag(RectTransform target, string partId, int placementIndex, int localX, int localY, Text hold)
        {
            var drag = target.gameObject.AddComponent<ReelPieceDrag>();
            drag.Owner = this;
            drag.PartId = partId;
            drag.PlacementIndex = placementIndex;
            drag.LocalX = localX;
            drag.LocalY = localY;
            drag.Hold = hold;
        }

        void DrawMini(RectTransform card, ReelPart part, bool icon = false)
        {
            var box = Rect(card, "Mini", icon ? new Vector2(0.16f, 0.28f) : new Vector2(0.08f, 0.16f),
                icon ? new Vector2(0.84f, 0.90f) : new Vector2(0.40f, 0.84f));
            var width = Mathf.Max(1, part.Width);
            var height = Mathf.Max(1, part.Height);
            foreach (var cell in part.Cells)
            {
                if (cell == null)
                    continue;
                var piece = Rect(box, "C",
                    new Vector2(cell.X / (float)width, 1f - (cell.Y + 1) / (float)height),
                    new Vector2((cell.X + 1) / (float)width, 1f - cell.Y / (float)height));
                var image = piece.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var art = ReelBuildArt.Tile(cell.Type);
                if (art != null)
                {
                    image.sprite = art;
                    image.preserveAspect = true;
                    image.color = Color.white;
                }
                else
                {
                    image.color = TypeColor(cell.Type);
                    BattleUITheme.EnsureSprite(image);
                    Label(piece, "M", Mark(cell.Type), 11, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
                }
            }
        }

        void ShowModal(string title, string body)
        {
            HideModal();
            _modal = FullScreen("ReelModal", new Color(0f, 0f, 0f, 0.72f));
            var window = PanelOf(_modal.transform, "Box", new Vector2(0.30f, 0.26f), new Vector2(0.70f, 0.74f), "plate_board");
            var modalTitle = Label(window, "Title", title, 24, TextAnchor.MiddleCenter,
                new Vector2(0.14f, 0.76f), new Vector2(0.86f, 0.90f), BattleUITheme.Gold, true);
            Emboss(modalTitle, new Color(0f, 0f, 0f, 0.8f));
            Label(window, "Body", body, 18, TextAnchor.UpperLeft, new Vector2(0.14f, 0.28f), new Vector2(0.86f, 0.74f), Ink);
            TextLink(window, "閉じる", new Vector2(0.32f, 0.08f), new Vector2(0.68f, 0.20f), HideModal, false, 18);
        }

        void HideModal()
        {
            if (_modal == null)
                return;
            UnityEngine.Object.Destroy(_modal);
            _modal = null;
        }

        void EnsureRoot(Transform host)
        {
            if (_root != null)
                return;
            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("ReelBuildCanvas");
            go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = go;
        }

        GameObject FullScreen(string name, Color color, string art = null)
        {
            var rect = Rect(_root.transform, name, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            if (!string.IsNullOrEmpty(art) && ReelBuildArt.Get(art) != null)
                Paint(image, art);
            else
            {
                image.color = color;
                BattleUITheme.EnsureSprite(image);
            }

            return rect.gameObject;
        }

        static RectTransform PanelOf(Transform parent, string name, Vector2 min, Vector2 max, string plate = null)
        {
            var shadow = Rect(parent, name + "Shadow",
                new Vector2(min.x + 0.004f, min.y - 0.012f),
                new Vector2(max.x + 0.007f, max.y - 0.002f));
            var shade = shadow.gameObject.AddComponent<Image>();
            shade.raycastTarget = false;
            if (!string.IsNullOrEmpty(plate) && ReelBuildArt.Get(plate) != null)
                Paint(shade, plate, new Color(0f, 0f, 0f, 0.55f));
            else
            {
                shade.color = new Color(0f, 0f, 0f, 0.45f);
                BattleUITheme.EnsureSprite(shade);
            }

            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            if (!string.IsNullOrEmpty(plate) && ReelBuildArt.Get(plate) != null)
                Paint(image, plate);
            else
            {
                image.color = Panel;
                BattleUITheme.EnsureSprite(image);
                Frame(rect.gameObject, new Color(0.62f, 0.46f, 0.22f, 0.9f), 1.4f);
            }

            return rect;
        }

        static void Frame(GameObject target, Color color, float distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color color, bool bold = false)
        {
            var rect = Rect(parent, name, min, max);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = bold ? BattleUITheme.FontBold : BattleUITheme.Font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = anchor;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static void Emboss(Text label, Color color)
        {
            if (label == null)
                return;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(1.4f, -1.4f);
        }

        static void Paint(Image image, string spriteName, Color? tint = null, bool sliced = false, bool aspect = false)
        {
            var sprite = ReelBuildArt.Get(spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                image.preserveAspect = aspect;
                image.color = tint ?? Color.white;
                return;
            }

            image.color = tint ?? Panel;
            BattleUITheme.EnsureSprite(image);
        }

        static void Button(Transform parent, string text, Vector2 min, Vector2 max, Action onClick, string skin = "btn_iron", int fontSize = 16, bool pulse = false, bool darkLabel = false)
        {
            var shadow = Rect(parent, "ButtonShadow", new Vector2(min.x + 0.004f, min.y - 0.008f), new Vector2(max.x + 0.004f, max.y - 0.004f));
            var shade = shadow.gameObject.AddComponent<Image>();
            shade.raycastTarget = false;
            shade.color = new Color(0f, 0f, 0f, 0.45f);
            BattleUITheme.EnsureSprite(shade);
            var rect = Rect(parent, "Button", min, max);
            var image = rect.gameObject.AddComponent<Image>();
            var gold = skin == "btn_gold";
            Paint(image, skin);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.05f, 0.92f, 1f);
            colors.pressedColor = new Color(0.75f, 0.68f, 0.5f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());
            var ink = darkLabel || gold ? new Color(0.18f, 0.09f, 0.04f, 1f) : Ink;
            var label = Label(rect, "Text", text, fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), ink, true);
            Emboss(label, new Color(0f, 0f, 0f, gold ? 0.28f : 0.7f));
            if (pulse)
                rect.gameObject.AddComponent<ReelBuildPulse>().Play = true;
        }

        void TextLink(Transform parent, string text, Vector2 min, Vector2 max, Action onClick, bool selected = false, int fontSize = 16, Color? color = null)
        {
            var rect = Rect(parent, "Link", min, max);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            BattleUITheme.EnsureSprite(hit);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.16f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());
            var ink = color ?? (selected ? BattleUITheme.Gold : Ink);
            Label(rect, "Text", text, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, ink, selected || color != null);
            if (!selected)
                return;
            var bar = Rect(rect, "Mark", new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.16f));
            var line = bar.gameObject.AddComponent<Image>();
            line.raycastTarget = false;
            line.color = BattleUITheme.Gold;
            BattleUITheme.EnsureSprite(line);
        }

        void BuildTabs(RectTransform panel)
        {
            string[] names = { "全部", "戦闘", "回復", "店", "宝", "イベント", "強いパーツ" };
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var row = i < 4 ? 0 : 1;
                var column = i < 4 ? i : i - 4;
                var count = row == 0 ? 4 : 3;
                var xMin = 0.08f + column * (0.84f / count);
                var yMax = row == 0 ? 0.86f : 0.81f;
                var yMin = yMax - 0.048f;
                var selected = _filter == index;
                TextLink(panel, names[i], new Vector2(xMin, yMin), new Vector2(xMin + 0.84f / count - 0.01f, yMax), () =>
                {
                    _filter = index;
                    Rebuild();
                }, selected, 13);
            }
        }

        List<ReelPart> VisibleParts()
        {
            var list = new List<ReelPart>();
            var parts = _catalog?.Parts ?? Array.Empty<ReelPart>();
            foreach (var part in parts)
            {
                if (part == null || part.Synthetic)
                    continue;
                if (part.Locked && !_debugUnlocked)
                    continue;
                if (!MatchesFilter(part))
                    continue;
                list.Add(part);
            }

            list.Sort((a, b) =>
            {
                var aEmpty = _layout.Remaining(_catalog, a.Id, _debugUnlocked) <= 0;
                var bEmpty = _layout.Remaining(_catalog, b.Id, _debugUnlocked) <= 0;
                if (aEmpty != bEmpty)
                    return aEmpty ? 1 : -1;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        bool MatchesFilter(ReelPart part)
        {
            if (_filter <= 0)
                return true;
            if (_filter == 6)
                return part.HasPenalty;
            var type = _filter switch
            {
                1 => DungeonNodeType.Combat,
                2 => DungeonNodeType.Rest,
                3 => DungeonNodeType.Merchant,
                4 => DungeonNodeType.Treasure,
                _ => DungeonNodeType.Event
            };
            foreach (var cell in part.Cells)
            {
                if (cell != null && cell.Type == type)
                    return true;
            }

            return false;
        }

        int LockedCount()
        {
            var count = 0;
            var parts = _catalog?.Parts ?? Array.Empty<ReelPart>();
            foreach (var part in parts)
            {
                if (part != null && !part.Synthetic && part.Locked && !_debugUnlocked)
                    count++;
            }

            return count;
        }

        static string CellList(ReelPart part)
        {
            var names = new List<string>();
            foreach (var cell in part.Cells)
                names.Add(cell == null ? "?" : LayerPathUI.SquareName(cell.Type));
            return string.Join("・", names);
        }

        void CommitLayout()
        {
            var next = CaptureCounts();
            RememberFlashes(next);
            _counts = next;
            Rebuild();
        }

        int[,] CaptureCounts()
        {
            var types = BoardGrid.RoadTypes();
            var grid = _layout.ToGrid(_catalog);
            var counts = new int[BoardGrid.Columns, types.Length];
            for (var type = 0; type < types.Length; type++)
            {
                for (var column = 0; column < BoardGrid.Columns; column++)
                    counts[column, type] = grid.Count(column, types[type]);
            }

            return counts;
        }

        void RememberFlashes(int[,] next)
        {
            if (_counts == null || next == null)
                return;
            var now = Time.unscaledTime;
            var types = BoardGrid.RoadTypes().Length;
            for (var type = 0; type < types; type++)
            {
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    if (Percent(_counts[column, type]) == Percent(next[column, type]))
                        continue;
                    _flashes.RemoveAll(flash => flash.Column == column && flash.TypeIndex == type);
                    _flashes.Add(new OddsFlash
                    {
                        Column = column,
                        TypeIndex = type,
                        From = _counts[column, type],
                        To = next[column, type],
                        Count = next[column, type],
                        Until = now + 1f
                    });
                }
            }
        }

        void StyleOdds(Text label, int column, int typeIndex, int count)
        {
            var now = Time.unscaledTime;
            foreach (var flash in _flashes)
            {
                if (flash.Column != column || flash.TypeIndex != typeIndex || flash.Until <= now)
                    continue;
                flash.Count = count;
                flash.Lines.Add(new FlashLine { Label = label, Rest = label.text });
                label.text = $"{Percent(flash.From)}%→{Percent(flash.To)}%";
                label.color = flash.To > flash.From ? BattleUITheme.Gold : new Color(1f, 0.4f, 0.35f, 1f);
                return;
            }
        }

        void RefreshFlashes()
        {
            var now = Time.unscaledTime;
            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var flash = _flashes[i];
                if (flash.Until > now)
                    continue;
                foreach (var line in flash.Lines)
                {
                    if (line.Label == null)
                        continue;
                    line.Label.text = line.Rest;
                    line.Label.color = Ink;
                }

                _flashes.RemoveAt(i);
            }
        }

        void BindClock()
        {
            if (_root == null)
                return;
            var clock = _root.GetComponent<ReelBuildClock>();
            if (clock == null)
                clock = _root.AddComponent<ReelBuildClock>();
            clock.Tick = RefreshFlashes;
        }

        internal void ShowTip(string text)
        {
            if (_tip == null)
                return;
            _tip.text = text ?? "";
            _tip.color = Ink;
        }

        internal void HideTip()
        {
            if (_tip == null)
                return;
            _tip.text = "カーソルを合わせると、名前と中身が出る";
            _tip.color = Muted;
        }

        static int Percent(int count) => Mathf.RoundToInt(count * 100f / Mathf.Max(1, BoardGrid.Rows));

        static string OddsText(int count, bool compact)
        {
            if (count <= 0)
                return compact ? "—" : "—";
            var percent = Mathf.RoundToInt(count * 100f / BoardGrid.Rows);
            return compact ? $"{percent}%" : $"{percent}%  {count}/{BoardGrid.Rows}";
        }

        static string Mark(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Combat => "戦",
            DungeonNodeType.Rest => "癒",
            DungeonNodeType.Merchant => "店",
            DungeonNodeType.Treasure => "宝",
            DungeonNodeType.Event => "事",
            DungeonNodeType.Trap => "骸",
            DungeonNodeType.MidBoss => "中",
            DungeonNodeType.Boss => "王",
            DungeonNodeType.Elevator => "昇",
            _ => "？"
        };

        static Color TypeColor(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Combat => new Color(0.55f, 0.16f, 0.16f, 1f),
            DungeonNodeType.Rest => new Color(0.12f, 0.38f, 0.32f, 1f),
            DungeonNodeType.Merchant => new Color(0.42f, 0.3f, 0.12f, 1f),
            DungeonNodeType.Treasure => new Color(0.16f, 0.32f, 0.48f, 1f),
            DungeonNodeType.Event => new Color(0.34f, 0.2f, 0.48f, 1f),
            DungeonNodeType.Trap => new Color(0.28f, 0.1f, 0.1f, 1f),
            DungeonNodeType.MidBoss => new Color(0.62f, 0.14f, 0.18f, 1f),
            DungeonNodeType.Boss => new Color(0.72f, 0.1f, 0.12f, 1f),
            DungeonNodeType.Elevator => new Color(0.62f, 0.46f, 0.14f, 1f),
            _ => new Color(0.2f, 0.2f, 0.2f, 1f)
        };

        internal void NotifyBegin(string partId, int placementIndex, int localX, int localY, Text hold) =>
            BeginDrag(partId, placementIndex, localX, localY, hold);

        internal void NotifyDrag(PointerEventData eventData) => Drag(eventData);

        internal void NotifyEnd(PointerEventData eventData) => EndDrag(eventData);
    }

    sealed class ReelPieceDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ReelBuildUI Owner;
        public string PartId;
        public int PlacementIndex = -1;
        public int LocalX;
        public int LocalY;
        public Text Hold;

        public void OnBeginDrag(PointerEventData eventData) =>
            Owner?.NotifyBegin(PartId, PlacementIndex, LocalX, LocalY, Hold);

        public void OnDrag(PointerEventData eventData) => Owner?.NotifyDrag(eventData);

        public void OnEndDrag(PointerEventData eventData) => Owner?.NotifyEnd(eventData);
    }

    sealed class OddsFlash
    {
        public int Column;
        public int TypeIndex;
        public int From;
        public int To;
        public int Count;
        public float Until;
        public readonly List<FlashLine> Lines = new();
    }

    sealed class FlashLine
    {
        public Text Label;
        public string Rest;
    }

    sealed class ReelPartTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ReelBuildUI Owner;
        public string Text;

        public void OnPointerEnter(PointerEventData eventData) => Owner?.ShowTip(Text);

        public void OnPointerExit(PointerEventData eventData) => Owner?.HideTip();
    }

    sealed class ReelBuildClock : MonoBehaviour
    {
        public Action Tick;

        void Update() => Tick?.Invoke();
    }

    sealed class ReelBuildPulse : MonoBehaviour
    {
        public bool Play;

        void Update()
        {
            var scale = Play ? 1f + Mathf.Sin(Time.unscaledTime * 2.6f) * 0.018f : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    static class ReelBuildArt
    {
        const string Root = "Run/ReelBuild/";
        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (Cache.TryGetValue(name, out var sprite))
                return sprite;
            sprite = Resources.Load<Sprite>(Root + name);
            Cache[name] = sprite;
            return sprite;
        }

        public static Sprite Tile(DungeonNodeType type)
        {
            var name = type switch
            {
                DungeonNodeType.Combat => "tile_combat",
                DungeonNodeType.Rest => "tile_rest",
                DungeonNodeType.Merchant => "tile_shop",
                DungeonNodeType.Treasure => "tile_treasure",
                DungeonNodeType.Event => "tile_event",
                DungeonNodeType.Trap => "tile_trap",
                DungeonNodeType.MidBoss => "tile_midboss",
                DungeonNodeType.Boss => "tile_boss",
                DungeonNodeType.Elevator => "tile_elevator",
                _ => "socket_empty"
            };
            return Get(name);
        }
    }
}
