using System;
using System.Collections.Generic;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>パーツを3列×9行に置いてリールを作る。マイセットの枠だけ空けておく。</summary>
    public sealed class ReelBuildUI
    {
        static readonly Color Page = new(0.05f, 0.035f, 0.03f, 1f);
        static readonly Color Panel = new(0.1f, 0.075f, 0.05f, 0.96f);
        static readonly Color Ink = new(0.93f, 0.9f, 0.82f, 1f);
        static readonly Color Muted = new(0.62f, 0.56f, 0.48f, 1f);
        static readonly Color Valid = new(0.28f, 0.78f, 0.38f, 0.9f);
        static readonly Color Invalid = new(0.82f, 0.18f, 0.16f, 0.9f);
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
            Action<ReelLayout, bool> onDepart,
            Action<BoardGrid> onDebugRolls)
        {
            _ = totalLayers;
            EnsureRoot(host);
            _catalog = catalog ?? ReelPartCatalog.Fallback();
            _layout = layout ?? ReelLayout.BalanceStarter(_catalog);
            _debugUnlocked = debugUnlocked;
            _layer = layer;
            _fourth = fourth;
            _config = config;
            _boards = boards;
            _onDepart = onDepart;
            _onDebugRolls = onDebugRolls;
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
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            _panel = FullScreen("ReelBuild", Page);
            var title = Label(_panel.transform, "Title", $"リール編成 — 第{_layer}層へ", 32, TextAnchor.MiddleCenter,
                new Vector2(0.16f, 0.93f), new Vector2(0.84f, 0.995f), BattleUITheme.Gold);
            title.fontStyle = FontStyle.Bold;
            Label(_panel.transform, "Sub", "パーツを並べて、1〜3マス目に出るマスを決める", 16, TextAnchor.MiddleCenter,
                new Vector2(0.2f, 0.905f), new Vector2(0.8f, 0.94f), Muted);

            BuildList(new Vector2(0.015f, 0.025f), new Vector2(0.275f, 0.89f));
            BuildBoard(new Vector2(0.285f, 0.055f), new Vector2(0.52f, 0.89f));
            BuildFourth(new Vector2(0.528f, 0.58f), new Vector2(0.635f, 0.89f));
            BuildReadout(new Vector2(0.645f, 0.025f), new Vector2(0.985f, 0.89f));
        }

        void BuildList(Vector2 min, Vector2 max)
        {
            var panel = PanelOf(_panel.transform, "PartList", min, max);
            _list = panel;
            Label(panel, "Heading", "パーツ一覧", 22, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.93f), new Vector2(0.7f, 0.99f), BattleUITheme.Gold).fontStyle = FontStyle.Bold;
            Label(panel, "Hint", "ドラッグで盤面へ", 13, TextAnchor.MiddleRight,
                new Vector2(0.4f, 0.93f), new Vector2(0.96f, 0.99f), Muted);

            var parts = _catalog.Parts ?? Array.Empty<ReelPart>();
            var rows = Mathf.Max(1, Mathf.CeilToInt(parts.Length / 2f));
            var top = 0.91f;
            var bottom = 0.2f;
            var rowH = (top - bottom) / rows;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var col = i % 2;
                var row = i / 2;
                var xMin = 0.03f + col * 0.485f;
                var yMax = top - row * rowH;
                var card = PanelOf(panel, "Part_" + part.Id, new Vector2(xMin, yMax - rowH + 0.008f), new Vector2(xMin + 0.46f, yMax - 0.004f));
                if (part.HasPenalty)
                    Frame(card.gameObject, BattleUITheme.Gold, 2.5f);
                var owned = _catalog.Owned(part.Id, _debugUnlocked);
                var left = owned - _layout.CountOf(part.Id);
                var locked = part.Locked && !_debugUnlocked;
                if (locked)
                {
                    var group = card.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0.45f;
                    group.blocksRaycasts = true;
                }

                DrawMini(card, part);
                Label(card, "Id", part.Id, 14, TextAnchor.UpperLeft, new Vector2(0.4f, 0.62f), new Vector2(0.98f, 0.95f), BattleUITheme.Gold);
                Label(card, "Name", part.Name, 13, TextAnchor.UpperLeft, new Vector2(0.4f, 0.32f), new Vector2(0.98f, 0.68f), Ink);
                var hold = Label(card, "Hold", locked ? "鍵" : $"残り{Mathf.Max(0, left)}", 13, TextAnchor.LowerLeft,
                    new Vector2(0.4f, 0.04f), new Vector2(0.98f, 0.34f), locked ? Muted : Ink);
                if (!locked && left > 0)
                    BindDrag(card, part.Id, -1, 0, 0, hold);
            }

            Label(panel, "SetTitle", "マイセット（準備中）", 16, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.145f), new Vector2(0.96f, 0.19f), Muted);
            for (var i = 0; i < 3; i++)
            {
                var xMin = 0.04f + i * 0.32f;
                var slot = PanelOf(panel, "SetSlot" + (i + 1), new Vector2(xMin, 0.03f), new Vector2(xMin + 0.28f, 0.13f));
                Label(slot, "Name", $"枠{i + 1}", 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Muted);
            }
        }

        void BuildBoard(Vector2 min, Vector2 max)
        {
            var panel = PanelOf(_panel.transform, "Board", min, max);
            for (var column = 0; column < BoardGrid.Columns; column++)
            {
                var xMin = 0.04f + column * 0.31f;
                var head = PanelOf(panel, "ColHead" + column, new Vector2(xMin, 0.93f), new Vector2(xMin + 0.29f, 0.99f));
                Label(head, "Text", $"{column + 1}マス目", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, BattleUITheme.Gold);
            }

            var grid = PanelOf(panel, "Cells", new Vector2(0.04f, 0.08f), new Vector2(0.97f, 0.92f));
            _board = grid;
            _highlights = new Image[BoardGrid.Columns, BoardGrid.Rows];
            for (var row = 0; row < BoardGrid.Rows; row++)
            {
                for (var column = 0; column < BoardGrid.Columns; column++)
                    BuildCell(grid, column, row);
            }

            Label(panel, "EmptyHint", "空き＝罠", 14, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.01f), new Vector2(0.5f, 0.07f), Muted);
            Label(panel, "EmptyHint2", "空きマスは罠として出る", 14, TextAnchor.MiddleRight,
                new Vector2(0.4f, 0.01f), new Vector2(0.97f, 0.07f), Muted);
        }

        void BuildCell(RectTransform grid, int column, int row)
        {
            var xMin = column / (float)BoardGrid.Columns;
            var xMax = (column + 1) / (float)BoardGrid.Columns;
            var yMax = 1f - row / (float)BoardGrid.Rows;
            var yMin = 1f - (row + 1) / (float)BoardGrid.Rows;
            var cell = Rect(grid, "Cell", new Vector2(xMin + 0.012f, yMin + 0.012f), new Vector2(xMax - 0.012f, yMax - 0.012f));
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
            image.color = empty ? new Color(0.08f, 0.06f, 0.05f, 1f) : TypeColor(type);
            BattleUITheme.EnsureSprite(image);
            if (!empty && part.HasPenalty && type != DungeonNodeType.Trap)
                Frame(cell.gameObject, BattleUITheme.Gold, 2.2f);
            if (!empty && type == DungeonNodeType.Trap)
                Frame(cell.gameObject, Penalty, 3f);
            Label(cell, "Mark", empty ? "罠" : Mark(type), empty ? 14 : 20, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, empty ? new Color(0.35f, 0.28f, 0.24f, 1f) : Color.white);
            var highlight = Rect(cell, "Preview", Vector2.zero, Vector2.one);
            var hi = highlight.gameObject.AddComponent<Image>();
            hi.color = new Color(1f, 1f, 1f, 0f);
            hi.raycastTarget = false;
            BattleUITheme.EnsureSprite(hi);
            _highlights[column, row] = hi;
            if (placement != null && shape != null)
                BindDrag(cell, placement.PartId, index, shape.X, shape.Y, null);
        }

        void BuildFourth(Vector2 min, Vector2 max)
        {
            var panel = PanelOf(_panel.transform, "Fourth", min, max);
            Label(panel, "Head", "4マス目", 16, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.98f), BattleUITheme.Gold);
            var icon = PanelOf(panel, "Icon", new Vector2(0.22f, 0.38f), new Vector2(0.78f, 0.8f));
            icon.GetComponent<Image>().color = TypeColor(_fourth);
            Label(icon, "Mark", Mark(_fourth), 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            Label(panel, "Name", LayerPathUI.SquareName(_fourth), 20, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.38f), Ink).fontStyle = FontStyle.Bold;
            Label(panel, "Fixed", "固定", 14, TextAnchor.MiddleCenter,
                new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.18f), Muted);
        }

        void BuildReadout(Vector2 min, Vector2 max)
        {
            var panel = PanelOf(_panel.transform, "Readout", min, max);
            var grid = _layout.ToGrid(_catalog);
            Label(panel, "OddsTitle", "出現割合", 22, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.93f), new Vector2(0.6f, 0.99f), BattleUITheme.Gold).fontStyle = FontStyle.Bold;
            Label(panel, "OddsHint", "リール1本＝9マス中の数", 13, TextAnchor.MiddleRight,
                new Vector2(0.4f, 0.93f), new Vector2(0.96f, 0.99f), Muted);
            Label(panel, "H0", "", 12, TextAnchor.MiddleCenter, new Vector2(0.28f, 0.88f), new Vector2(0.48f, 0.93f), Muted).text = "1マス目";
            Label(panel, "H1", "2マス目", 12, TextAnchor.MiddleCenter, new Vector2(0.48f, 0.88f), new Vector2(0.7f, 0.93f), Muted);
            Label(panel, "H2", "3マス目", 12, TextAnchor.MiddleCenter, new Vector2(0.7f, 0.88f), new Vector2(0.96f, 0.93f), Muted);
            var types = BoardGrid.RoadTypes();
            for (var i = 0; i < types.Length; i++)
            {
                var yMax = 0.88f - i * 0.055f;
                var yMin = yMax - 0.05f;
                Label(panel, "Type" + i, LayerPathUI.SquareName(types[i]), 14, TextAnchor.MiddleLeft,
                    new Vector2(0.04f, yMin), new Vector2(0.26f, yMax), Ink);
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    var xMin = 0.28f + column * 0.22f;
                    Label(panel, "Odds" + i + "_" + column, OddsText(grid.Count(column, types[i])), 13, TextAnchor.MiddleCenter,
                        new Vector2(xMin, yMin), new Vector2(xMin + 0.2f, yMax), Ink);
                }
            }

            Label(panel, "LegendTitle", "見かけ", 16, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.5f), new Vector2(0.4f, 0.55f), BattleUITheme.Gold);
            Label(panel, "Legend",
                "金のフチ　強いパーツ（罠つき）\nふつうのフチ　ふつうのパーツ\n赤のフチと骸　デメリットのマス\n暗い罠　空きマス（罠として出る）\n緑　置ける　　赤　置けない",
                13, TextAnchor.UpperLeft, new Vector2(0.04f, 0.32f), new Vector2(0.48f, 0.5f), Muted);

            Label(panel, "MinTitle", "最低マス数チェック", 16, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(0.96f, 0.55f), BattleUITheme.Gold);
            var shortages = LayerSquares.ShortageLines(grid, _config, _layer);
            var rules = _config != null ? _config.MinimumsFor(_layer) : new List<LayerTypeMinimum>();
            for (var i = 0; i < rules.Count && i < 4; i++)
            {
                var rule = rules[i];
                var yMax = 0.48f - i * 0.07f;
                var worst = false;
                var bits = new List<string>();
                for (var column = 0; column < BoardGrid.Columns; column++)
                {
                    var have = grid.Count(column, rule.type);
                    bits.Add($"{have}/{rule.minimumPerColumn}");
                    if (have < rule.minimumPerColumn)
                        worst = true;
                }

                Label(panel, "Min" + i,
                    $"{LayerPathUI.SquareName(rule.type)}  {string.Join("  ", bits)}  {(worst ? "不足" : "OK")}",
                    14, TextAnchor.MiddleLeft, new Vector2(0.5f, yMax - 0.06f), new Vector2(0.96f, yMax),
                    worst ? new Color(1f, 0.45f, 0.4f, 1f) : new Color(0.55f, 0.85f, 0.55f, 1f));
            }

            var note = shortages.Count == 0 ? "最低マス数を満たしています。" : string.Join("\n", shortages);
            var empty = _layout.EmptyCells(_catalog);
            if (empty > 0)
                note += $"\n空きマスが{empty}つあります（空きは罠として出ます）";
            Label(panel, "MinNote", note, 14, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.32f), shortages.Count == 0 ? Muted : new Color(1f, 0.55f, 0.45f, 1f));

            Button(panel, _debugUnlocked ? "解放済み" : "全パーツ解放", new Vector2(0.04f, 0.145f), new Vector2(0.28f, 0.19f), () =>
            {
                if (_debugUnlocked)
                    return;
                _debugUnlocked = true;
                Rebuild();
            });
            DebugPreset(panel, "戦闘寄り", "combat", 0.29f);
            DebugPreset(panel, "回復寄り", "rest", 0.46f);
            DebugPreset(panel, "宝寄り", "treasure", 0.63f);
            Button(panel, "1000回抽選", new Vector2(0.8f, 0.145f), new Vector2(0.96f, 0.19f), () =>
                _onDebugRolls?.Invoke(_layout.ToGrid(_catalog)));

            Button(panel, "全部外す", new Vector2(0.04f, 0.03f), new Vector2(0.28f, 0.12f), () =>
            {
                _layout.Clear();
                Rebuild();
            });
            var depart = shortages.Count == 0 ? "出発" : "出発できません：" + shortages[0];
            Button(panel, depart, new Vector2(0.32f, 0.02f), new Vector2(0.96f, 0.13f), TryDepart,
                shortages.Count == 0 ? new Color(0.62f, 0.16f, 0.12f, 1f) : new Color(0.22f, 0.12f, 0.1f, 1f));
        }

        void DebugPreset(RectTransform panel, string label, string id, float xMin)
        {
            Button(panel, label, new Vector2(xMin, 0.145f), new Vector2(xMin + 0.16f, 0.19f), () =>
            {
                var preset = _boards != null ? _boards.Find(id) : null;
                if (preset == null)
                    return;
                _layout.LoadPreset(preset.ToGrid(), _catalog);
                Rebuild();
            });
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
            if (placementIndex < 0)
            {
                var left = _catalog.Owned(partId, _debugUnlocked) - _layout.CountOf(partId);
                if (left <= 0)
                    return;
            }

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
            var ok = _layout.CanPlace(_catalog, part, originX, originY, _dragIndex);
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
                Rebuild();
                return;
            }

            if (part != null && TryCell(eventData, out var column, out var row))
            {
                var originX = column - localX;
                var originY = row - localY;
                if (_layout.CanPlace(_catalog, part, originX, originY, index))
                {
                    if (index >= 0)
                        _layout.RemoveAt(index);
                    _layout.Place(partId, originX, originY);
                    Rebuild();
                    return;
                }
            }

            if (_dragHold != null && index < 0)
            {
                var left = _catalog.Owned(partId, _debugUnlocked) - _layout.CountOf(partId);
                _dragHold.text = $"残り{Mathf.Max(0, left)}";
            }
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

        void DrawMini(RectTransform card, ReelPart part)
        {
            var box = Rect(card, "Mini", new Vector2(0.04f, 0.12f), new Vector2(0.36f, 0.88f));
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
                image.color = TypeColor(cell.Type);
                image.raycastTarget = false;
                BattleUITheme.EnsureSprite(image);
                if (cell.Type == DungeonNodeType.Trap)
                    Frame(piece.gameObject, Penalty, 1.5f);
                Label(piece, "M", Mark(cell.Type), 11, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            }
        }

        void ShowModal(string title, string body)
        {
            HideModal();
            _modal = FullScreen("ReelModal", new Color(0f, 0f, 0f, 0.72f));
            var window = PanelOf(_modal.transform, "Box", new Vector2(0.3f, 0.28f), new Vector2(0.7f, 0.72f));
            Label(window, "Title", title, 24, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.96f), BattleUITheme.Gold);
            Label(window, "Body", body, 18, TextAnchor.UpperLeft, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.76f), Ink);
            Button(window, "閉じる", new Vector2(0.3f, 0.06f), new Vector2(0.7f, 0.18f), HideModal);
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

        GameObject FullScreen(string name, Color color)
        {
            var rect = Rect(_root.transform, name, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            BattleUITheme.EnsureSprite(image);
            return rect.gameObject;
        }

        static RectTransform PanelOf(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Panel;
            BattleUITheme.EnsureSprite(image);
            Frame(rect.gameObject, new Color(0.62f, 0.46f, 0.22f, 0.9f), 1.4f);
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

        static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(parent, name, min, max);
            var label = rect.gameObject.AddComponent<Text>();
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

        static void Button(Transform parent, string text, Vector2 min, Vector2 max, Action onClick, Color? color = null)
        {
            var rect = Rect(parent, "Button", min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color ?? new Color(0.18f, 0.12f, 0.09f, 1f);
            BattleUITheme.EnsureSprite(image);
            Frame(rect.gameObject, new Color(0.72f, 0.54f, 0.24f, 0.8f), 1.2f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var label = Label(rect, "Text", text, 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Ink);
            label.fontStyle = FontStyle.Bold;
        }

        static string OddsText(int count)
        {
            if (count <= 0)
                return "—";
            var percent = Mathf.RoundToInt(count * 100f / BoardGrid.Rows);
            return $"{percent}%  {count}/{BoardGrid.Rows}";
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
}
