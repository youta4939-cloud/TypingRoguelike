using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using TypingRoguelike.Battle;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TypingRoguelike.RouteLottery
{
    public static class RouteLotteryAssets
    {
        static RouteLotteryCatalog _cache;

        public static RouteLotteryCatalog Get()
        {
            if (_cache == null)
                _cache = Load();
            return _cache;
        }

        public static RouteLotteryCatalog Load()
        {
            string Read(string name)
            {
                var asset = Resources.Load<TextAsset>("RouteLottery/Data/" + name);
                if (asset == null)
                    throw new InvalidOperationException("RouteLottery の " + name + " が読めません");
                return asset.text;
            }

            return RouteLotteryCatalog.Parse(
                Read("square_types"),
                Read("pieces"),
                Read("combos"),
                Read("monologues"),
                Read("strings_ja"),
                Read("layer_cards"));
        }
    }

    /// <summary>
    /// 記憶を編む → リール抽選 → 4枚の層ルート。座標は rlb_v1 の layout JSON（左上原点・y下）。
    /// </summary>
    public sealed class RouteLotteryPresenter : MonoBehaviour
    {
        const float Cell = 96f;
        const float Gap = 6f;
        const float Pitch = Cell + Gap;
        const float BoardX = 608f;
        const float BoardY = 278f;
        const float ReelPitch = 160f;
        const int ReelFaces = 5;
        const float PaylineTop = 165f;
        const float KickDuration = 0.16f;
        const float QuickStopDelay = 0.6f;

        static readonly float[] ReelCenterX = { 574f, 960f, 1346f };
        static readonly float[] ReelStart = { 0f, 0.08f, 0.16f };
        static readonly float[] ReelStop = { 2.60f, 4.20f, 7.80f };
        static readonly Color Paper = new Color(240f / 255f, 228f / 255f, 210f / 255f, 1f);
        static readonly Color Up = new Color(255f / 255f, 168f / 255f, 72f / 255f, 1f);
        static readonly Color Down = new Color(122f / 255f, 150f / 255f, 196f / 255f, 1f);
        static readonly Color DangerText = new Color(232f / 255f, 72f / 255f, 60f / 255f, 1f);

        public static void Hide(Transform host)
        {
            if (host == null)
                return;
            var previous = host.Find("RouteLottery");
            if (previous != null)
                Destroy(previous.gameObject);
        }

        /// <summary>層の進行画面。左の未到達カードから順にイベントへ入る。</summary>
        public static void ShowLayer(
            Transform host,
            int layer,
            DungeonNodeType fourth,
            string[] typeIds,
            bool[] cleared,
            int currentIndex,
            Action<int> onEnter,
            Action onLoadout,
            Action onRelics,
            Action onStats,
            Action onAbandon,
            RunPartyState party = null,
            RunMadnessState madness = null,
            TrustGaugeView[] trust = null)
        {
            Hide(host);
            RunOverlayUI.EnsureGlobalEventSystem();
            var go = new GameObject("RouteLottery");
            if (host != null)
                go.transform.SetParent(host, false);
            var view = go.AddComponent<RouteLotteryPresenter>();
            try
            {
                view.OpenLayer(layer, fourth, typeIds, cleared, currentIndex, onEnter, onLoadout, onRelics, onStats, onAbandon, party, madness, trust);
            }
            catch
            {
                Destroy(go);
                throw;
            }
        }

        public static void Show(
            Transform host,
            int layer,
            int seed,
            DungeonNodeType fourth,
            bool startAtElevator,
            string savedBoard,
            Action<string> onBoardChanged,
            Action<string[]> onDone,
            RunPartyState party = null,
            RunMadnessState madness = null,
            TrustGaugeView[] trust = null)
        {
            RunOverlayUI.EnsureGlobalEventSystem();
            var previous = host != null ? host.Find("RouteLottery") : null;
            if (previous != null)
                Destroy(previous.gameObject);

            var go = new GameObject("RouteLottery");
            if (host != null)
                go.transform.SetParent(host, false);
            var view = go.AddComponent<RouteLotteryPresenter>();
            try
            {
                view.Open(layer, seed, fourth, startAtElevator, savedBoard, onBoardChanged, onDone, party, madness, trust);
            }
            catch
            {
                Destroy(go);
                throw;
            }
        }

        RouteLotteryCatalog _catalog;
        MemoryBoard _board;
        int _layer;
        int _seed;
        DungeonNodeType _fourth;
        RunPartyState _hudParty;
        RunMadnessState _hudMadness;
        TrustGaugeView[] _hudTrust;
        bool _startAtElevator;
        Action<string[]> _onDone;
        Action<string> _onBoardChanged;

        GameObject _buildRoot;
        GameObject _lotteryRoot;
        GameObject _routeRoot;
        readonly Image[] _cells = new Image[MemoryBoard.Rows * MemoryBoard.Lanes];
        readonly TextMeshProUGUI[] _laneTexts = new TextMeshProUGUI[3];
        TextMeshProUGUI _comboText;
        TextMeshProUGUI _monoText;
        TextMeshProUGUI _goSub;
        Button _goButton;
        Image _goImage;
        readonly List<TextMeshProUGUI> _remainLabels = new List<TextMeshProUGUI>();
        readonly List<string> _remainIds = new List<string>();

        readonly Image[,] _reelFaces = new Image[3, ReelFaces];
        readonly Dictionary<string, Sprite> _reelArt = new Dictionary<string, Sprite>();
        RectTransform _machineLayout;
        GameObject _lotteryDetails;
        TextMeshProUGUI _reachWord;
        TextMeshProUGUI _reachSymbols;
        GameObject _hitRoot;
        TextMeshProUGUI _hitBanner;
        TextMeshProUGUI _lotteryList;
        TextMeshProUGUI _lotteryNote;
        Button _rebuildButton;
        Button _startButton;
        Coroutine _spin;
        bool _spinning;
        bool _rolled;
        readonly int[] _stop = new int[3];
        bool _skip;
        bool _reachOn;
        bool _routeBuilt;
        Drag _drag;
        PieceDef _selection;
        int _selectedPlacement = -1;
        Image _rotationButton;
        Image _removeButton;
        RectTransform _rotationPreview;
        TextMeshProUGUI _placementHint;
        RectTransform _ghost;
        int _hoverRow = -1;
        int _hoverCol = -1;

        sealed class Drag
        {
            public PieceDef Piece;
            public int Placement = -1;
            public int OriginRow;
            public int OriginCol;
        }

        void Open(
            int layer,
            int seed,
            DungeonNodeType fourth,
            bool startAtElevator,
            string savedBoard,
            Action<string> onBoardChanged,
            Action<string[]> onDone,
            RunPartyState party,
            RunMadnessState madness,
            TrustGaugeView[] trust)
        {
            _layer = layer;
            _seed = seed;
            _fourth = fourth;
            _startAtElevator = startAtElevator;
            _onDone = onDone;
            _onBoardChanged = onBoardChanged;
            _hudParty = party;
            _hudMadness = madness;
            _hudTrust = trust;
            _catalog = RouteLotteryAssets.Load();
            _board = MemoryBoard.Empty();
            _board.Restore(_catalog, savedBoard);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var bg = MakeImage(transform, "Bg", "Route/bg_hell_town_1920x1080", false, 1f);
            Stretch(bg.rectTransform);
            bg.color = Color.white;
            bg.raycastTarget = true;

            _buildRoot = NewRoot("Build");
            _lotteryRoot = NewRoot("Lottery");
            _routeRoot = NewRoot("Route");
            if (!TryInstallBoardPrefabs())
            {
                BuildBuildScreen();
            }
            BuildRotationControls();
            BuildLotteryScreen();
            ShowBuild();
        }

        public void ExportPrefabRoots()
        {
            _buildingPrefab = true;
            _catalog = RouteLotteryAssets.Load();
            _layer = 1;
            _board = MemoryBoard.Empty();
            _board.Restore(_catalog, null);
            _fourth = DungeonNodeType.Boss;
            _buildRoot = NewRoot("Build");
            _lotteryRoot = NewRoot("Lottery");
            BuildBuildScreen();
            BuildLotteryScreen();
        }

        bool TryInstallBoardPrefabs()
        {
            if (_buildingPrefab)
                return false;
            var buildPrefab = Resources.Load<GameObject>("UI/BoardBuild");
            if (buildPrefab == null)
                return false;
            if (_buildRoot != null)
                DestroyImmediate(_buildRoot);
            _buildRoot = Instantiate(buildPrefab, transform);
            _buildRoot.name = "Build";
            Stretch(_buildRoot.GetComponent<RectTransform>());
            try
            {
                BindBuild(_buildRoot.transform);
                return true;
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("盤面プレハブを使えなかったので、コードで画面を作ります: " + error.Message);
                if (_buildRoot != null)
                    DestroyImmediate(_buildRoot);
                if (_lotteryRoot != null)
                    DestroyImmediate(_lotteryRoot);
                _buildRoot = NewRoot("Build");
                _lotteryRoot = NewRoot("Lottery");
                return false;
            }
        }

        void BindBuild(Transform root)
        {
            RepairBoardFonts(root);
            RepairBoardSprites(root);
            _remainLabels.Clear();
            _remainIds.Clear();
            if (_catalog?.Pieces != null)
            {
                foreach (var piece in _catalog.Pieces)
                {
                    var card = root.Find("Piece_" + piece.Id);
                    if (card == null)
                        continue;
                    _remainLabels.Add(card.Find("Remain")?.GetComponent<TextMeshProUGUI>());
                    _remainIds.Add(piece.Id);
                }
            }

            for (var r = 0; r < MemoryBoard.Rows; r++)
            for (var c = 0; c < MemoryBoard.Lanes; c++)
            {
                var cell = root.Find("Cell_" + r + "_" + c);
                _cells[r * MemoryBoard.Lanes + c] = cell != null ? cell.GetComponent<Image>() : null;
            }

            foreach (var handle in root.GetComponentsInChildren<PieceDrag>(true))
                handle.Owner = this;
            for (var lane = 0; lane < 3; lane++)
                _laneTexts[lane] = root.Find("Lane" + lane)?.GetComponent<TextMeshProUGUI>();
            _comboText = root.Find("Combos")?.GetComponent<TextMeshProUGUI>();
            _monoText = root.Find("Mono")?.GetComponent<TextMeshProUGUI>();
            _goSub = root.Find("GoSub")?.GetComponent<TextMeshProUGUI>();
            var go = root.Find("Go");
            _goImage = go != null ? go.GetComponent<Image>() : null;
            _goButton = go != null ? go.GetComponent<Button>() : null;
            if (_goButton != null)
            {
                _goButton.onClick.RemoveAllListeners();
                _goButton.onClick.AddListener(ShowLottery);
            }
        }

        void RepairBoardFonts(Transform root)
        {
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == "Reach")
                    tmp.font = Brush() ?? Serif() ?? BattleHudKit.SerifRegular;
                else if (tmp.fontSize >= 26f)
                    tmp.font = Serif() ?? BattleHudKit.SerifRegular;
                else
                    tmp.font = Sans() ?? BattleHudKit.Sans;
                if (tmp.font == null)
                    tmp.font = BattleHudKit.Sans;
            }
        }

        static void RepairBoardSprites(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null)
                    continue;
                image.sprite = image.name.IndexOf("Ring", StringComparison.Ordinal) >= 0
                    ? BattleUISprites.Circle
                    : BattleUISprites.Solid;
            }
        }

        GameObject NewRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        void ShowBuild()
        {
            ClearSelection();
            if (_spin != null)
                StopCoroutine(_spin);
            _spin = null;
            _spinning = false;
            _rolled = false;
            _skip = false;
            _reachOn = false;
            EndDragVisual();
            _buildRoot.SetActive(true);
            _lotteryRoot.SetActive(false);
            _routeRoot.SetActive(false);
            Refresh(false, -1, -1);
        }

        void BuildBuildScreen()
        {
            var root = _buildRoot.transform;
            Panel(root, "Palette", 36f, 160f, 464f, 778f, "Common/iron_panel_9s", 2.5f);
            Panel(root, "BoardFrame", 524f, 160f, 436f, 778f, "Common/iron_panel_9s", 2.5f);
            Panel(root, "Rates", 984f, 160f, 900f, 778f, "Common/iron_panel_9s", 2.5f);
            Panel(root, "Bottom", 36f, 952f, 924f, 100f, "Common/iron_panel_9s", 1.67f);
            Panel(root, "Monologue", 984f, 952f, 476f, 100f, "Common/iron_panel_9s", 1.67f);

            Label(root, "Title", _catalog.Text("build.title"), 56f, 48f, 520f, 70f, 58f, true, TextAlignmentOptions.MidlineLeft, Paper);
            Label(root, "Badge", _catalog.Format("build.layer_badge", ("layer", _layer.ToString())), 430f, 58f, 220f, 48f, 28f, true, TextAlignmentOptions.MidlineLeft, Paper);
            Label(root, "Sub", _catalog.Text("build.subtitle"), 58f, 118f, 900f, 36f, 22f, false, TextAlignmentOptions.MidlineLeft, Paper);

            Label(root, "PalTitle", _catalog.Text("build.palette.title"), 36f, 168f, 464f, 44f, 32f, true, TextAlignmentOptions.Center, Paper);
            Label(root, "PalSub", _catalog.Text("build.palette.sub"), 48f, 214f, 440f, 28f, 16f, false, TextAlignmentOptions.Center, new Color(0.75f, 0.7f, 0.62f, 1f));

            var y = 250f;
            foreach (var piece in _catalog.Pieces)
            {
                var card = MakeImage(root, "Piece_" + piece.Id, piece.Locked ? "Build/palette_card_locked_9s" : "Build/palette_card_normal_9s", true, 2f);
                TopLeft(card.rectTransform, 52f, y, 432f, 78f);
                card.raycastTarget = true;
                var handle = card.gameObject.AddComponent<PieceDrag>();
                handle.Owner = this;
                handle.PieceId = piece.Id;
                var name = piece.Locked && !string.IsNullOrEmpty(piece.Unlock) ? piece.Unlock : piece.Name;
                Label(card.transform, "Name", name, 12f, 8f, 250f, 36f, 22f, true, TextAlignmentOptions.MidlineLeft, Paper);
                var remain = Label(card.transform, "Remain", "", 12f, 40f, 250f, 28f, 16f, false, TextAlignmentOptions.MidlineLeft, Paper);
                _remainLabels.Add(remain);
                _remainIds.Add(piece.Id);
                DrawMini(card.transform, piece);
                y += 84f;
            }

            for (var r = 0; r < MemoryBoard.Rows; r++)
            for (var c = 0; c < MemoryBoard.Lanes; c++)
            {
                var image = MakeImage(root, "Cell_" + r + "_" + c, "Build/board_slot_9s", true, 2f);
                TopLeft(image.rectTransform, BoardX + c * Pitch, BoardY + r * Pitch, Cell, Cell);
                image.raycastTarget = true;
                var handle = image.gameObject.AddComponent<PieceDrag>();
                handle.Owner = this;
                handle.Row = r;
                handle.Col = c;
                _cells[r * MemoryBoard.Lanes + c] = image;
            }

            Label(root, "RateTitle", _catalog.Text("build.rates.title"), 1008f, 176f, 520f, 40f, 28f, true, TextAlignmentOptions.MidlineLeft, Paper);
            Label(root, "Formula", _catalog.Text("build.rates.formula"), 1280f, 180f, 560f, 36f, 18f, false, TextAlignmentOptions.MidlineRight, new Color(0.75f, 0.7f, 0.62f, 1f));
            for (var lane = 0; lane < 3; lane++)
            {
                _laneTexts[lane] = Label(root, "Lane" + lane, "", 1008f + lane * 280f, 230f, 270f, 280f, 20f, false, TextAlignmentOptions.TopLeft, Paper);
            }
            Label(root, "ComboTitle", _catalog.Text("build.combos.title"), 1008f, 520f, 400f, 36f, 26f, true, TextAlignmentOptions.MidlineLeft, Paper);
            _comboText = Label(root, "Combos", "", 1008f, 560f, 840f, 320f, 20f, false, TextAlignmentOptions.TopLeft, Paper);

            var respin = MakeImage(root, "Respin", "Build/icon_power_respin", false, 1f);
            TopLeft(respin.rectTransform, 250f, 968f, 48f, 48f);
            respin.raycastTarget = false;
            Label(root, "RespinText", _catalog.Text("build.powers.respin") + " " + _catalog.Format("build.powers.remaining", ("n", "2")), 304f, 968f, 180f, 48f, 18f, false, TextAlignmentOptions.MidlineLeft, Paper);
            var nudge = MakeImage(root, "Nudge", "Build/icon_power_nudge", false, 1f);
            TopLeft(nudge.rectTransform, 500f, 968f, 48f, 48f);
            nudge.raycastTarget = false;
            Label(root, "NudgeText", _catalog.Text("build.powers.nudge") + " " + _catalog.Format("build.powers.remaining", ("n", "1")), 554f, 968f, 180f, 48f, 18f, false, TextAlignmentOptions.MidlineLeft, Paper);
            Label(root, "PowerNote", _catalog.Text("build.powers.note"), 70f, 1004f, 360f, 28f, 14f, false, TextAlignmentOptions.MidlineLeft, new Color(0.7f, 0.66f, 0.58f, 1f));
            for (var i = 0; i < 3; i++)
            {
                var slot = MakeImage(root, "Save" + i, "Build/save_slot_empty_9s", true, 2f);
                TopLeft(slot.rectTransform, 712f + i * 72f, 970f, 62f, 64f);
                slot.raycastTarget = false;
            }

            _monoText = Label(root, "Mono", "", 1010f, 968f, 430f, 70f, 26f, true, TextAlignmentOptions.MidlineLeft, Paper);
            _goImage = MakeImage(root, "Go", "Common/button_normal_9s", true, 2f);
            TopLeft(_goImage.rectTransform, 1490f, 950f, 394f, 70f);
            _goButton = _goImage.gameObject.AddComponent<Button>();
            _goButton.targetGraphic = _goImage;
            _goButton.onClick.AddListener(ShowLottery);
            Label(_goImage.transform, "GoLabel", _catalog.Text("build.go"), 0f, 0f, 394f, 70f, 32f, true, TextAlignmentOptions.Center, Paper);
            _goSub = Label(root, "GoSub", "", 1490f, 1022f, 394f, 28f, 16f, false, TextAlignmentOptions.Center, Paper);
        }

        void DrawMini(Transform card, PieceDef piece)
        {
            if (piece.Cells == null)
                return;
            foreach (var cell in piece.Cells)
            {
                var chip = MakeImage(card, "Mini", "Icons/tile_" + cell.Type, false, 1f);
                TopLeft(chip.rectTransform, 280f + cell.Dc * 28f, 14f + cell.Dr * 28f, 26f, 26f);
                chip.raycastTarget = false;
                if (chip.sprite == null)
                    chip.color = TypeColor(cell.Type);
            }
        }

        void BuildRotationControls()
        {
            var root = _buildRoot.transform;
            var hint = root.Find("PalSub")?.GetComponent<TextMeshProUGUI>();
            if (hint != null)
                hint.text = "クリックで選択・回転 ／ ドラッグで配置";
            _rotationPreview = new GameObject("RotationPreview", typeof(RectTransform)).GetComponent<RectTransform>();
            _rotationPreview.SetParent(root, false);
            TopLeft(_rotationPreview, 556, 174, 82, 82);
            _rotationButton = MakeImage(root, "Rotate", "Common/button_normal_9s", true, 2f);
            TopLeft(_rotationButton.rectTransform, 644, 174, 152, 50);
            _rotationButton.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                _selection = _selection.Clockwise();
                ShowSelection();
            });
            Label(_rotationButton.transform, "Label", "右に90°回転", 0, 0, 152, 50, 21, true, TextAlignmentOptions.Center, Paper);
            _removeButton = MakeImage(root, "RemovePiece", "Common/iron_panel_9s", true, 2f);
            TopLeft(_removeButton.rectTransform, 803, 174, 140, 50);
            _removeButton.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                _board.RemoveAtIndex(_selectedPlacement);
                ClearSelection();
                Refresh(false, -1, -1);
                _onBoardChanged?.Invoke(_board.Serialize());
            });
            Label(_removeButton.transform, "Label", "取り外す", 0, 0, 140, 50, 21, true, TextAlignmentOptions.Center, Paper);
            _placementHint = Label(root, "PlaceHint", "", 644, 225, 304, 45, 17, false, TextAlignmentOptions.Center, Paper);
        }

        void ClearSelection()
        {
            _selection = null;
            _selectedPlacement = -1;
            _rotationButton.gameObject.SetActive(false);
            _removeButton.gameObject.SetActive(false);
            _rotationPreview.gameObject.SetActive(false);
            _placementHint.text = "";
        }

        void ShowSelection()
        {
            _rotationButton.gameObject.SetActive(_selection.Cells.Count > 1);
            _removeButton.gameObject.SetActive(_selectedPlacement >= 0);
            _rotationPreview.gameObject.SetActive(true);
            foreach (Transform child in _rotationPreview)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            foreach (var cell in _selection.Cells)
            {
                var icon = MakeImage(_rotationPreview, "Cell", "Icons/tile_" + cell.Type, false, 1f);
                TopLeft(icon.rectTransform, cell.Dc * 24, cell.Dr * 24, 23, 23);
                icon.raycastTarget = false;
            }
            _placementHint.text = "置く位置（左上）をクリック";
            Refresh(false, -1, -1);
        }

        public void ClickPiece(string pieceId, int row, int col)
        {
            int index = _board.PlacementAt(row, col);
            if (row >= 0 && _selection != null && (index < 0 || index == _selectedPlacement))
            {
                if (!_board.Fits(_selection, row, col, _selectedPlacement))
                {
                    _placementHint.text = "ここには置けません";
                    return;
                }
                if (_selectedPlacement >= 0)
                    _board.RemoveAtIndex(_selectedPlacement);
                _board.TryPlace(_selection, row, col);
                ClearSelection();
                Refresh(false, -1, -1);
                _onBoardChanged?.Invoke(_board.Serialize());
                return;
            }
            var placement = index >= 0 ? _board.Placements[index] : null;
            var piece = _catalog.FindPiece(placement?.PieceId ?? pieceId);
            if (piece == null || piece.Locked || (placement == null && _board.PlacedCount(piece.Id) >= piece.Count))
                return;
            _selectedPlacement = index;
            _selection = piece.AtRotation(placement?.Turns ?? 0);
            ShowSelection();
        }

        public void BeginPieceDrag(string pieceId, PointerEventData eventData)
        {
            if (_spinning || _buildRoot == null || !_buildRoot.activeSelf)
                return;
            var piece = _catalog.FindPiece(pieceId);
            if (piece == null || piece.Locked || _board.PlacedCount(piece.Id) >= piece.Count)
                return;
            if (_selectedPlacement < 0 && _selection?.Id == pieceId)
                piece = _selection;
            ClearSelection();
            _drag = new Drag { Piece = piece, Placement = -1 };
            StartGhost(piece, eventData);
        }

        public void BeginCellDrag(int row, int col, PointerEventData eventData)
        {
            if (_spinning || _buildRoot == null || !_buildRoot.activeSelf)
                return;
            var index = _board.PlacementAt(row, col);
            if (index < 0)
                return;
            var placement = _board.Placements[index];
            _drag = new Drag
            {
                Piece = _selectedPlacement == index ? _selection : _catalog.FindPiece(placement.PieceId).AtRotation(placement.Turns),
                Placement = index,
                OriginRow = placement.Row,
                OriginCol = placement.Col
            };
            ClearSelection();
            StartGhost(_drag.Piece, eventData);
            _hoverRow = -1;
            _hoverCol = -1;
            Refresh(false, -1, -1);
        }

        public void DragMove(PointerEventData eventData)
        {
            if (_drag == null)
                return;
            var row = -1;
            var col = -1;
            var over = TryCell(eventData.position, out row, out col);
            var piece = _drag.Piece;
            var valid = over && PreviewFits(piece, row, col);
            if (_ghost != null)
            {
                if (valid)
                    TopLeft(_ghost, BoardX + col * Pitch, BoardY + row * Pitch, Cell, Cell);
                else
                    Follow(_ghost, eventData.position);
            }
            if (row != _hoverRow || col != _hoverCol)
            {
                _hoverRow = row;
                _hoverCol = col;
                Refresh(valid, row, col);
            }
        }

        public void EndPieceDrag(PointerEventData eventData)
        {
            if (_drag == null)
                return;
            var piece = _drag.Piece;
            var before = _board.Serialize();
            var row = -1;
            var col = -1;
            var onBoard = ScreenToBoard(eventData.position, out var boardX, out var boardY) && InsideBoard(boardX, boardY);
            if (piece != null && TryCell(eventData.position, out row, out col) && PreviewFits(piece, row, col))
            {
                if (_drag.Placement >= 0)
                {
                    var originRow = _drag.OriginRow;
                    var originCol = _drag.OriginCol;
                    var original = _board.Placements[_drag.Placement];
                    _board.RemoveAtIndex(_drag.Placement);
                    if (!_board.TryPlace(piece, row, col))
                        _board.TryPlace(_catalog.FindPiece(original.PieceId).AtRotation(original.Turns), originRow, originCol);
                }
                else
                {
                    _board.TryPlace(piece, row, col);
                }
            }
            else if (_drag.Placement >= 0 && !onBoard)
            {
                _board.RemoveAtIndex(_drag.Placement);
            }
            _drag = null;
            _hoverRow = -1;
            _hoverCol = -1;
            EndDragVisual();
            Refresh(false, -1, -1);
            var after = _board.Serialize();
            if (after != before)
                _onBoardChanged?.Invoke(after);
        }

        bool PreviewFits(PieceDef piece, int row, int col)
        {
            if (piece == null)
                return false;
            var copy = _board.Clone();
            if (_drag != null && _drag.Placement >= 0)
                copy.RemoveAtIndex(_drag.Placement);
            return copy.TryPlace(piece, row, col);
        }

        void StartGhost(PieceDef piece, PointerEventData eventData)
        {
            EndDragVisual();
            if (piece == null)
                return;
            var go = new GameObject("Ghost", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _ghost = go.GetComponent<RectTransform>();
            TopLeft(_ghost, 0f, 0f, Cell, Cell);
            foreach (var cell in piece.Cells)
            {
                var image = MakeImage(_ghost, "G", "Icons/tile_" + cell.Type, false, 1f);
                TopLeft(image.rectTransform, cell.Dc * Pitch, cell.Dr * Pitch, Cell, Cell);
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0.85f);
            }
            go.transform.SetAsLastSibling();
            DragMove(eventData);
        }

        void EndDragVisual()
        {
            if (_ghost != null)
                Destroy(_ghost.gameObject);
            _ghost = null;
        }

        void Follow(RectTransform rt, Vector2 screen)
        {
            var canvasRt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local))
                return;
            var size = canvasRt.rect.size;
            var x = local.x + size.x * canvasRt.pivot.x;
            var y = size.y * (1f - canvasRt.pivot.y) - local.y;
            TopLeft(rt, x, y, Cell, Cell);
        }

        bool ScreenToBoard(Vector2 screen, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var canvasRt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local))
                return false;
            var size = canvasRt.rect.size;
            x = local.x + size.x * canvasRt.pivot.x;
            y = size.y * (1f - canvasRt.pivot.y) - local.y;
            return true;
        }

        bool InsideBoard(float x, float y)
        {
            var w = MemoryBoard.Lanes * Pitch - Gap;
            var h = MemoryBoard.Rows * Pitch - Gap;
            return x >= BoardX && y >= BoardY && x < BoardX + w && y < BoardY + h;
        }

        bool TryCell(Vector2 screen, out int row, out int col)
        {
            row = -1;
            col = -1;
            if (!ScreenToBoard(screen, out var x, out var y))
                return false;
            var fx = (x - BoardX) / Pitch;
            var fy = (y - BoardY) / Pitch;
            col = Mathf.FloorToInt(fx);
            row = Mathf.FloorToInt(fy);
            if (row < 0 || col < 0 || row >= MemoryBoard.Rows || col >= MemoryBoard.Lanes)
                return false;
            if ((fx - col) * Pitch > Cell || (fy - row) * Pitch > Cell)
                return false;
            return true;
        }

        void Refresh(bool preview, int row, int col)
        {
            var shown = _board;
            var showDiff = false;
            if (_drag != null && _drag.Placement >= 0)
            {
                shown = _board.Clone();
                shown.RemoveAtIndex(_drag.Placement);
            }
            if (preview)
            {
                var piece = _drag?.Piece;
                var copy = shown.Clone();
                if (piece != null && copy.TryPlace(piece, row, col))
                {
                    shown = copy;
                    showDiff = true;
                }
            }

            for (var r = 0; r < MemoryBoard.Rows; r++)
            for (var c = 0; c < MemoryBoard.Lanes; c++)
            {
                var image = _cells[r * MemoryBoard.Lanes + c];
                if (image == null)
                    continue;
                var type = shown.Cells[r, c];
                var sprite = SpriteOf("Icons/tile_" + type);
                image.sprite = sprite;
                image.color = sprite != null ? Color.white : TypeColor(type);
                if (_selectedPlacement >= 0 && _board.PlacementAt(r, c) == _selectedPlacement)
                    image.color = Paper;
                image.type = Image.Type.Simple;
            }

            for (var i = 0; i < _remainLabels.Count; i++)
            {
                var piece = _catalog.FindPiece(_remainIds[i]);
                if (piece == null || _remainLabels[i] == null)
                    continue;
                var placed = _board.PlacedCount(piece.Id);
                if (_drag != null && _drag.Piece.Id == piece.Id)
                    placed = Mathf.Max(0, placed - 1);
                if (piece.Locked)
                    _remainLabels[i].text = _catalog.Text("build.palette.locked");
                else if (placed >= piece.Count)
                    _remainLabels[i].text = _catalog.Text("build.palette.used");
                else
                    _remainLabels[i].text = _catalog.Format("build.palette.remaining", ("n", (piece.Count - placed).ToString()), ("max", piece.Count.ToString()));
            }

            var beforeLanes = LaneMap(_board);
            var afterLanes = LaneMap(shown);
            var laneDiff = showDiff ? ProbabilityEngine.Diff(beforeLanes, afterLanes) : new List<RateDiff>();
            for (var lane = 0; lane < 3; lane++)
            {
                if (_laneTexts[lane] == null)
                    continue;
                var text = new StringBuilder();
                text.Append(_catalog.Text("build.lane." + (lane + 1))).Append('\n');
                var battles = shown.LaneCount(lane, "battle");
                var check = _catalog.Format("build.lane.battle_check", ("n", battles.ToString()), ("min", _catalog.MinBattlePerLane.ToString()));
                if (battles < _catalog.MinBattlePerLane)
                    text.Append("<color=#E8483C>").Append(check).Append("</color>\n");
                else
                    text.Append(check).Append('\n');
                foreach (var type in _catalog.Types)
                {
                    var count = shown.LaneCount(lane, type.Id);
                    var line = type.Short + "  " + ProbabilityEngine.LanePercent(count) + "%";
                    var change = Find(laneDiff, "L" + lane + ":" + type.Id);
                    if (change != null)
                    {
                        var color = change.After > change.Before ? "#FFA848" : "#7A96C4";
                        line = type.Short + "  <color=" + color + ">" + ProbabilityEngine.LanePercent(change.Before) + "% → " + ProbabilityEngine.LanePercent(change.After) + "%</color>";
                    }
                    else if (count == 0)
                    {
                        continue;
                    }
                    text.Append(line).Append('\n');
                }
                _laneTexts[lane].text = text.ToString();
            }

            var beforeCombos = ProbabilityEngine.CountCombos(_board, _catalog.Combos, _catalog.TypeIds(), _catalog.FallbackId);
            var afterCombos = ProbabilityEngine.CountCombos(shown, _catalog.Combos, _catalog.TypeIds(), _catalog.FallbackId);
            var comboDiff = showDiff ? ProbabilityEngine.Diff(beforeCombos, afterCombos) : new List<RateDiff>();
            var combos = new StringBuilder();
            foreach (var combo in _catalog.Combos)
            {
                var count = ProbabilityEngine.Value(afterCombos, combo.Id);
                var change = Find(comboDiff, combo.Id);
                combos.Append(combo.Name).Append("  ");
                if (change != null)
                {
                    var color = change.After > change.Before ? "#FFA848" : "#7A96C4";
                    combos.Append("<color=").Append(color).Append(">")
                        .Append(ProbabilityEngine.ComboPercent(change.Before)).Append("% → ")
                        .Append(ProbabilityEngine.ComboPercent(change.After)).Append("%</color>");
                }
                else
                {
                    combos.Append(ProbabilityEngine.ComboPercent(count)).Append('%');
                }
                combos.Append('\n');
            }
            combos.Append(_catalog.Format("build.combos.total", ("total", "100.00%")));
            if (_comboText != null)
                _comboText.text = combos.ToString();

            if (_monoText != null)
                _monoText.text = MonologueSelector.Pick(_catalog.Monologues, "build", shown, _catalog.MinBattlePerLane, null, null);

            var valid = _board.AllLanesValid(_catalog.MinBattlePerLane);
            if (_goButton != null)
                _goButton.interactable = valid;
            if (_goImage != null)
            {
                var key = _goButton != null && _goButton.interactable ? "Common/button_normal_9s" : "Common/button_disabled_9s";
                var sprite = SpriteOf(key);
                if (sprite != null)
                    _goImage.sprite = sprite;
            }
            if (_goSub != null)
            {
                if (valid)
                {
                    _goSub.color = Paper;
                    _goSub.text = _catalog.Text("build.go.sub");
                }
                else
                {
                    _goSub.color = DangerText;
                    _goSub.text = _catalog.Format(
                        "build.go.blocked",
                        ("lanes", ShortLanes(shown)),
                        ("n", LowestBattle(shown).ToString()),
                        ("min", _catalog.MinBattlePerLane.ToString()));
                }
            }
        }

        string ShortLanes(MemoryBoard board)
        {
            var names = new List<string>();
            for (var lane = 0; lane < 3; lane++)
            {
                if (!board.LaneValid(lane, _catalog.MinBattlePerLane))
                    names.Add(_catalog.Text("build.lane." + (lane + 1)));
            }
            return string.Join("、", names);
        }

        int LowestBattle(MemoryBoard board)
        {
            var min = MemoryBoard.Rows;
            for (var lane = 0; lane < 3; lane++)
            {
                if (!board.LaneValid(lane, _catalog.MinBattlePerLane))
                    min = Mathf.Min(min, board.LaneCount(lane, "battle"));
            }
            return min;
        }

        Dictionary<string, int> LaneMap(MemoryBoard board)
        {
            var map = new Dictionary<string, int>();
            for (var lane = 0; lane < 3; lane++)
            {
                foreach (var type in _catalog.Types)
                    map["L" + lane + ":" + type.Id] = board.LaneCount(lane, type.Id);
            }
            return map;
        }

        static RateDiff Find(List<RateDiff> list, string id)
        {
            if (list == null)
                return null;
            foreach (var item in list)
            {
                if (item.Id == id)
                    return item;
            }
            return null;
        }

        void ShowLottery()
        {
            if (!_board.AllLanesValid(_catalog.MinBattlePerLane))
                return;
            _onBoardChanged?.Invoke(_board.Serialize());
            EndDragVisual();
            _buildRoot.SetActive(false);
            _routeRoot.SetActive(false);
            _lotteryRoot.SetActive(true);
            _lotteryDetails.SetActive(false);
            _spinning = false;
            _rolled = false;
            _skip = false;
            _reachOn = false;
            if (_startButton != null)
                _startButton.interactable = true;
            if (_rebuildButton != null)
                _rebuildButton.interactable = true;
            if (_reachWord != null)
                _reachWord.gameObject.SetActive(false);
            if (_reachSymbols != null)
                _reachSymbols.gameObject.SetActive(false);
            if (_hitRoot != null)
                _hitRoot.SetActive(false);
            RefreshStrips();
            WriteLotteryList(false, null, null);
            if (_lotteryNote != null)
                _lotteryNote.text = "";
        }

        void BuildLotteryScreen()
        {
            _machineLayout = new GameObject("MachineLayout", typeof(RectTransform)).GetComponent<RectTransform>();
            _machineLayout.SetParent(_lotteryRoot.transform, false);
            _machineLayout.anchorMin = _machineLayout.anchorMax = Vector2.one * 0.5f;
            _machineLayout.sizeDelta = new Vector2(1920f, 1080f);
            var layout = _machineLayout;
            var machine = MakeImage(layout, "Machine", "Lottery/reel_machine", false, 1f);
            Stretch(machine.rectTransform);
            machine.raycastTarget = false;
            foreach (var type in _catalog.TypeIds())
                _reelArt[type] = SpriteOf("Lottery/reel_" + type) ?? SpriteOf("Icons/tile_" + type);
            for (var lane = 0; lane < 3; lane++)
            for (var slot = 0; slot < ReelFaces; slot++)
            {
                var image = MakeImage(layout, "Face" + lane + "_" + slot, null, false, 1f);
                image.raycastTarget = false;
                _reelFaces[lane, slot] = image;
            }

            // Put the two brass rails in front of the moving print.
            foreach (var span in new[] { new Vector2(301f, 322f), new Vector2(536f, 553f) })
            {
                var rail = new GameObject("Rail", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                rail.transform.SetParent(layout, false);
                rail.texture = machine.sprite.texture;
                rail.uvRect = new Rect(0, 1f - span.y / 941f, 1, (span.y - span.x) / 941f);
                TopLeft(rail.rectTransform, 0, 1080f * span.x / 941f, 1920f, 1080f * (span.y - span.x) / 941f);
                rail.raycastTarget = false;
            }
            Label(layout, "Title", _catalog.Text("lottery.title"), 46, 22, 610, 80, 58, true, TextAlignmentOptions.MidlineLeft, Paper);
            _lotteryDetails = new GameObject("Details", typeof(RectTransform));
            _lotteryDetails.transform.SetParent(layout, false);
            Stretch((RectTransform)_lotteryDetails.transform);
            var root = _lotteryDetails.transform;
            Panel(root, "List", 36f, 160f, 576f, 884f, "Common/iron_panel_9s", 1f);
            Label(root, "ListTitle", _catalog.Text("lottery.list.title"), 36f, 172f, 576f, 48f, 36f, true, TextAlignmentOptions.Center, Paper);
            Label(root, "ListSub", _catalog.Text("lottery.list.sub"), 48f, 220f, 552f, 28f, 18f, false, TextAlignmentOptions.Center, Paper);
            _lotteryList = Label(root, "ListBody", "", 64f, 270f, 520f, 700f, 24f, false, TextAlignmentOptions.TopLeft, Paper);

            Panel(root, "Flow", 1410f, 160f, 474f, 646f, "Common/iron_panel_9s", 1f);
            Label(root, "FlowTitle", _catalog.Text("lottery.flow.title"), 1410f, 172f, 474f, 48f, 32f, true, TextAlignmentOptions.Center, Paper);
            var flow = _catalog.Text("lottery.flow.1.title") + "\n" + _catalog.Text("lottery.flow.1.body") + "\n\n"
                + _catalog.Text("lottery.flow.2.title") + "\n" + _catalog.Text("lottery.flow.2.body") + "\n\n"
                + _catalog.Text("lottery.flow.3.title") + "\n" + _catalog.Text("lottery.flow.3.body");
            Label(root, "FlowBody", flow, 1434f, 240f, 430f, 400f, 22f, false, TextAlignmentOptions.TopLeft, Paper);
            _lotteryNote = Label(root, "Note", "", 1434f, 660f, 430f, 120f, 24f, true, TextAlignmentOptions.TopLeft, Paper);

            root = layout;
            _reachSymbols = Label(root, "ReachSymbols", "", 750f, 790f, 380f, 80f, 60f, true, TextAlignmentOptions.Center, Paper);
            _reachWord = Label(root, "Reach", _catalog.Text("lottery.reach"), 750f, 846f, 380f, 120f, 110f, true, TextAlignmentOptions.Center, Paper);
            _reachWord.font = Brush() ?? _reachWord.font;
            _reachWord.fontStyle = FontStyles.Italic;
            _reachWord.enableVertexGradient = true;
            _reachWord.colorGradient = new VertexGradient(
                new Color(1f, 232f / 255f, 170f / 255f),
                new Color(1f, 232f / 255f, 170f / 255f),
                new Color(214f / 255f, 120f / 255f, 40f / 255f),
                new Color(214f / 255f, 120f / 255f, 40f / 255f));
            _reachSymbols.gameObject.SetActive(false);
            _reachWord.gameObject.SetActive(false);

            var banner = MakeImage(root, "Hit", null, false, 1f);
            TopLeft(banner.rectTransform, 640f, 76f, 640f, 52f);
            banner.color = new Color(40f / 255f, 14f / 255f, 10f / 255f, 230f / 255f);
            banner.raycastTarget = false;
            BattleUITheme.EnsureSprite(banner);
            _hitRoot = banner.gameObject;
            _hitBanner = Label(banner.transform, "HitText", "", 0f, 0f, 640f, 52f, 28f, true, TextAlignmentOptions.Center, Paper);
            _hitRoot.SetActive(false);

            var start = MakeImage(root, "Start", "Common/button_normal_9s", true, 2f);
            TopLeft(start.rectTransform, 785f, 943f, 350f, 74f);
            _startButton = start.gameObject.AddComponent<Button>();
            _startButton.targetGraphic = start;
            _startButton.onClick.AddListener(PressStart);
            Label(start.transform, "Label", _catalog.Text("lottery.button.start"), 0f, 0f, 350f, 74f, 40f, true, TextAlignmentOptions.Center, Paper);

            var rebuild = MakeImage(root, "Rebuild", "Common/iron_panel_9s", true, 2.5f);
            TopLeft(rebuild.rectTransform, 250f, 943f, 446f, 68f);
            _rebuildButton = rebuild.gameObject.AddComponent<Button>();
            _rebuildButton.targetGraphic = rebuild;
            _rebuildButton.onClick.AddListener(ShowBuild);
            Label(rebuild.transform, "Label", _catalog.Text("lottery.button.rebuild"), 0f, 0f, 446f, 68f, 26f, true, TextAlignmentOptions.Center, Paper);

            var skip = MakeImage(root, "Skip", "Common/button_normal_9s", true, 2.5f);
            TopLeft(skip.rectTransform, 1210f, 950f, 294f, 56f);
            var skipButton = skip.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = skip;
            skipButton.onClick.AddListener(PressSkip);
            Label(skip.transform, "Label", _catalog.Text("lottery.button.skip"), 0f, 0f, 294f, 56f, 28f, true, TextAlignmentOptions.Center, Paper);
            var details = MakeImage(layout, "DetailsToggle", "Common/iron_panel_9s", true, 2f);
            TopLeft(details.rectTransform, 1620, 950, 160, 56);
            details.gameObject.AddComponent<Button>().onClick.AddListener(() => _lotteryDetails.SetActive(!_lotteryDetails.activeSelf));
            Label(details.transform, "Label", "詳細", 0, 0, 160, 56, 28, true, TextAlignmentOptions.Center, Paper);
            _lotteryDetails.SetActive(false);
            RefreshStrips();
        }

        void PressStart()
        {
            if (_spinning)
                return;
            _spinning = true;
            if (_startButton != null)
                _startButton.interactable = false;
            if (_rebuildButton != null)
                _rebuildButton.interactable = false;
            EnsureRolled();
            _spin = StartCoroutine(Spin());
        }

        void PressSkip()
        {
            _skip = true;
            if (!_spinning)
            {
                EnsureRolled();
                ShowRoute();
            }
        }

        void EnsureRolled()
        {
            if (_rolled)
                return;
            _rolled = true;
            var rng = new System.Random(unchecked(_seed * 397 ^ _layer * 7919 ^ _board.ContentHash()));
            for (var lane = 0; lane < 3; lane++)
                _stop[lane] = rng.Next(MemoryBoard.Rows);
        }

        IEnumerator Spin()
        {
            float elapsed = 0f;
            float finish = ReelStop[2];
            bool secondStopped = false;
            while (elapsed < finish && !_skip)
            {
                if (!secondStopped && elapsed >= ReelStop[1])
                {
                    secondStopped = true;
                    UpdateReach(true);
                    if (!_reachOn)
                        finish = ReelStop[1] + QuickStopDelay;
                }

                float reachAge = elapsed - ReelStop[1];
                float lift = _reachOn
                    ? Mathf.SmoothStep(0f, 1f, reachAge / 0.24f)
                      * (1f + 0.08f * Mathf.Sin(reachAge * 5f))
                      * Mathf.SmoothStep(0f, 1f, (finish - elapsed) / 0.22f)
                    : 0f;
                for (int lane = 0; lane < 3; lane++)
                    SetStrip(lane, SpinScroll(lane, elapsed, finish), lane < 2 ? lift : 0f);

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            for (var lane = 0; lane < 3; lane++)
                SetStrip(lane, LandingScroll(_stop[lane]));
            UpdateReach(false);
            ShowHit();
            if (!_skip)
                yield return new WaitForSecondsRealtime(1.1f);
            ShowRoute();
        }

        static float LandingScroll(int stopRow)
        {
            return (stopRow - 4 * MemoryBoard.Rows) * ReelPitch - PaylineTop;
        }

        float SpinScroll(int lane, float elapsed, float finish)
        {
            float age = Mathf.Max(0f, elapsed - ReelStart[lane]);
            const float kick = 0.075f * ReelPitch;
            if (age <= KickDuration)
                return kick * Mathf.SmoothStep(0f, 1f, age / KickDuration);

            float start = ReelStart[lane] + KickDuration;
            float landing = LandingScroll(_stop[lane]);
            if (lane < 2)
            {
                float t = Mathf.InverseLerp(start, ReelStop[lane], elapsed);
                return Mathf.Lerp(kick, landing, Mathf.SmoothStep(0f, 1f, t));
            }

            // A reach adds one complete revolution. Both braking paths start
            // at the same position and speed and end on the rolled symbol.
            float extra = MemoryBoard.Rows * ReelPitch;
            float speed = 2f * extra / (ReelStop[2] - ReelStop[1] - QuickStopDelay);
            float brakeStart = landing + speed * QuickStopDelay * 0.5f;
            if (elapsed < ReelStop[1])
            {
                float duration = ReelStop[1] - start;
                float t = Mathf.InverseLerp(start, ReelStop[1], elapsed);
                return Mathf.Lerp(kick, brakeStart, Mathf.SmoothStep(0f, 1f, t))
                    + speed * duration * t * t * (1f - t);
            }
            float brakeTime = finish - ReelStop[1];
            float u = Mathf.InverseLerp(ReelStop[1], finish, elapsed);
            return brakeStart - speed * brakeTime * u * (1f - 0.5f * u);
        }

        void LateUpdate()
        {
            if (_machineLayout == null || !_lotteryRoot.activeInHierarchy)
                return;
            var viewport = ((RectTransform)_lotteryRoot.transform).rect.size;
            _machineLayout.localScale = Vector3.one * Mathf.Min(viewport.x / 1920f, viewport.y / 1080f);
        }

        void RefreshStrips()
        {
            for (var lane = 0; lane < 3; lane++)
                SetStrip(lane, 0f);
        }

        void SetStrip(int lane, float anchoredY, float centerLift = 0f)
        {
            float position = (anchoredY + PaylineTop) / ReelPitch;
            int nearest = Mathf.RoundToInt(position);
            for (int slot = 0; slot < ReelFaces; slot++)
            {
                int row = nearest + slot - ReelFaces / 2;
                float angle = (row - position) * 0.71f;
                var image = _reelFaces[lane, slot];
                float depth = Mathf.Cos(angle);
                image.enabled = depth > 0.08f;
                if (!image.enabled)
                    continue;
                var type = _board.Cells[(row % MemoryBoard.Rows + MemoryBoard.Rows) % MemoryBoard.Rows, lane];
                image.sprite = _reelArt[type];
                float y = 492f + 375f * Mathf.Sin(angle);
                float lift = slot == ReelFaces / 2 ? centerLift : 0f;
                float size = 224f * (1f + 0.16f * lift);
                Center(image.rectTransform, ReelCenterX[lane], y - 12f * lift, size, size * depth);
                float light = Mathf.Lerp(0.14f, 1f, depth * depth);
                image.color = new Color(light, light, light, Mathf.Clamp01(depth * 3f));
            }
        }

        void UpdateReach(bool reaching)
        {
            var first = _board.Cells[_stop[0], 0];
            var second = _board.Cells[_stop[1], 1];
            ComboDef best = null;
            if (reaching)
            {
                var counts = ProbabilityEngine.ReachCounts(_board, _catalog.Combos, _catalog.TypeIds(), _catalog.FallbackId, first, second);
                foreach (var combo in _catalog.Combos)
                {
                    if (combo.Fallback || ProbabilityEngine.Value(counts, combo.Id) <= 0)
                        continue;
                    if (best == null || combo.Priority < best.Priority)
                        best = combo;
                }
            }
            _reachOn = best != null;
            if (_reachWord != null)
                _reachWord.gameObject.SetActive(_reachOn);
            if (_reachSymbols != null)
            {
                _reachSymbols.gameObject.SetActive(_reachOn);
                if (_reachOn)
                {
                    var s1 = best.Pattern[0] == "*" ? "？" : _catalog.ShortOf(best.Pattern[0]);
                    var s2 = best.Pattern[1] == "*" ? "？" : _catalog.ShortOf(best.Pattern[1]);
                    var s3 = best.Pattern[2] == "*" ? "？" : _catalog.ShortOf(best.Pattern[2]);
                    _reachSymbols.text = _catalog.Format("lottery.reach.symbols", ("s1", s1), ("s2", s2), ("s3", s3));
                }
            }
            if (_reachOn && _lotteryNote != null)
            {
                var reachType = ReachType(best, first);
                _lotteryNote.text = MonologueSelector.Pick(_catalog.Monologues, "lottery_reach", _board, _catalog.MinBattlePerLane, reachType, null);
            }
            WriteLotteryList(reaching && best != null, first, second);
        }

        static string ReachType(ComboDef combo, string first)
        {
            if (combo?.Pattern == null)
                return first ?? "";
            foreach (var token in combo.Pattern)
            {
                if (!string.IsNullOrEmpty(token) && token != "*")
                    return token;
            }
            return first ?? "";
        }

        void WriteLotteryList(bool reach, string first, string second)
        {
            if (_lotteryList == null)
                return;
            Dictionary<string, int> counts;
            var denom = ProbabilityEngine.Outcomes;
            if (reach)
            {
                counts = ProbabilityEngine.ReachCounts(_board, _catalog.Combos, _catalog.TypeIds(), _catalog.FallbackId, first, second);
                denom = MemoryBoard.Rows;
            }
            else
            {
                counts = ProbabilityEngine.CountCombos(_board, _catalog.Combos, _catalog.TypeIds(), _catalog.FallbackId);
            }
            var text = new StringBuilder();
            foreach (var combo in _catalog.Combos)
            {
                var count = ProbabilityEngine.Value(counts, combo.Id);
                text.Append(combo.Name).Append("  ").Append(ProbabilityEngine.Percent(count, denom, 2)).Append("%\n");
            }
            text.Append(_catalog.Format("build.combos.total", ("total", "100.00%")));
            _lotteryList.text = text.ToString();
        }

        void ShowHit()
        {
            var a = _board.Cells[_stop[0], 0];
            var b = _board.Cells[_stop[1], 1];
            var c = _board.Cells[_stop[2], 2];
            var id = ProbabilityEngine.WinningId(_catalog.Combos, _catalog.FallbackId, a, b, c);
            var combo = _catalog.FindCombo(id);
            var name = combo != null ? combo.Name : id;
            if (_hitRoot != null)
                _hitRoot.SetActive(true);
            if (_hitBanner != null)
                _hitBanner.text = _catalog.Format("lottery.hit.banner", ("combo", name));
            if (_reachWord != null)
                _reachWord.gameObject.SetActive(false);
            if (_reachSymbols != null)
                _reachSymbols.gameObject.SetActive(false);
            _reachOn = false;
            if (_lotteryNote != null)
                _lotteryNote.text = MonologueSelector.Pick(_catalog.Monologues, "lottery_hit", _board, _catalog.MinBattlePerLane, null, id);
        }

        void ShowRoute()
        {
            if (_spin != null)
                StopCoroutine(_spin);
            _spin = null;
            _spinning = false;
            _reachOn = false;
            EnsureRolled();
            _buildRoot.SetActive(false);
            _lotteryRoot.SetActive(false);
            _routeRoot.SetActive(true);
            if (_routeBuilt)
                return;
            _routeBuilt = true;
            BuildRoute();
        }

        void BuildRoute()
        {
            var results = new string[4];
            results[0] = _board.Cells[_stop[0], 0];
            results[1] = _board.Cells[_stop[1], 1];
            results[2] = _board.Cells[_stop[2], 2];
            results[3] = FourthId();
            var cleared = new bool[4];
            var current = 0;
            if (_startAtElevator)
            {
                current = 3;
                cleared[0] = cleared[1] = cleared[2] = true;
            }

            PaintDungeonRoute(_routeRoot.transform, _layer, results, cleared, current, _ => EnterLayer(), null, null, null, null);
        }

        string FourthId()
        {
            if (_fourth == DungeonNodeType.Elevator)
                return "elevator";
            return "boss";
        }

        void OpenLayer(
            int layer,
            DungeonNodeType fourth,
            string[] typeIds,
            bool[] cleared,
            int currentIndex,
            Action<int> onEnter,
            Action onLoadout,
            Action onRelics,
            Action onStats,
            Action onAbandon,
            RunPartyState party,
            RunMadnessState madness,
            TrustGaugeView[] trust)
        {
            _catalog = RouteLotteryAssets.Get();
            _layer = layer;
            _fourth = fourth;
            _hudParty = party;
            _hudMadness = madness;
            _hudTrust = trust;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            PaintDungeonRoute(transform, layer, typeIds, cleared, currentIndex, onEnter, onLoadout, onRelics, onStats, onAbandon);
        }

        /// <summary>エディタが進行画面プレハブを作るときの見本。</summary>
        public void BuildPrefabSource()
        {
            _buildingPrefab = true;
            try
            {
                _catalog = RouteLotteryAssets.Get();
                _hudTrust = new[]
                {
                    new TrustGaugeView { Name = "chara01", Trust = 0 },
                    new TrustGaugeView { Name = "chara02", Trust = 0 },
                    new TrustGaugeView { Name = "chara03", Trust = 0 }
                };
                PaintDungeonRoute(transform, 1, new[] { "battle", "event", "rest", "boss" }, new bool[4], 0,
                    _ => { }, () => { }, () => { }, () => { }, () => { });
            }
            finally
            {
                _buildingPrefab = false;
            }
        }

        bool _buildingPrefab;

        void PaintDungeonRoute(
            Transform root,
            int layer,
            string[] typeIds,
            bool[] cleared,
            int currentIndex,
            Action<int> onEnter,
            Action onLoadout,
            Action onRelics,
            Action onStats,
            Action onAbandon)
        {
            if (TryBindDungeonRoute(root, layer, typeIds, cleared, currentIndex, onEnter, onLoadout, onRelics, onStats, onAbandon))
                return;

            var bg = SpecImage(root, "RouteBg", "bg_dungeon_route");
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            if (bg.sprite == null)
                bg.color = new Color(0.082f, 0.078f, 0.09f, 1f);

            var title = RouteText(root, "FloorTitle", $"第{layer}層", BattleHudKit.SerifMedium, 52f, SpecInk, TextAlignmentOptions.MidlineLeft, 88f, 100f, 320f, 68f, 20f);
            _ = title;

            var count = typeIds != null ? Mathf.Min(4, typeIds.Length) : 0;
            var lefts = new float[count];
            var widths = new float[count];
            var heights = new float[count];
            var tops = new float[count];
            var centers = new float[count];
            var x = 152f;
            for (var i = 0; i < count; i++)
            {
                var done = cleared != null && i < cleared.Length && cleared[i];
                var current = i == currentIndex && !done;
                widths[i] = current ? 424f : 380f;
                heights[i] = current ? 632f : 620f;
                tops[i] = current ? 232f : 240f;
                lefts[i] = x;
                centers[i] = x + widths[i] * 0.5f;
                x += widths[i] + 20f;
            }

            if (count > 0)
            {
                CutRule(root, 178f, 88f, 1844f, centers, 28f, new Color(1f, 1f, 1f, 0.14f));
                var line = TopBox(root, "TrackLine", centers[0], 207f, Mathf.Max(2f, centers[count - 1] - centers[0]), 2f);
                BattleHudKit.AddImage(line, BattleUISprites.Solid, HexColor("#DDDCDF"));
                for (var i = 0; i < count; i++)
                {
                    var done = cleared != null && i < cleared.Length && cleared[i];
                    var current = i == currentIndex && !done;
                    RouteText(root, "TrackNo" + i, (i + 1).ToString("00"), BattleHudKit.SerifRegular, 24f,
                        done ? HexColor("#8A858B") : HexColor("#EAE6E5"), TextAlignmentOptions.Center,
                        centers[i] - 30f, 164f, 60f, 28f, 4f);
                    var dot = SpecImage(root, "Dot" + i, current ? "dot_current" : done ? "dot_passed" : "dot_unreached");
                    TopLeft(dot.rectTransform, centers[i] - 18f, 190f, 36f, 36f);
                    dot.preserveAspect = true;
                }
            }

            var used = new Dictionary<string, int>();
            for (var i = 0; i < count; i++)
            {
                var done = cleared != null && i < cleared.Length && cleared[i];
                var current = i == currentIndex && !done;
                var index = i;
                BuildSpecCard(root, i, typeIds[i], lefts[i], tops[i], widths[i], heights[i], current, done, i == count - 1, used,
                    current ? () => onEnter?.Invoke(index) : null);
            }

            if (onLoadout != null || onAbandon != null)
                BuildRouteChrome(root, onLoadout, onRelics, onStats, onAbandon);
            else
                BuildWallet(root);
            BuildRouteFooter(root);
        }

        void BuildRouteChrome(Transform root, Action onLoadout, Action onRelics, Action onStats, Action onAbandon)
        {
            BuildWallet(root);
            var icon = SpecImage(root, "MenuIcon", "icon_menu");
            TopLeft(icon.rectTransform, 1852f, 24f, 26f, 20f);
            icon.preserveAspect = true;
            var hit = TopBox(root, "MenuHit", 1836f, 6f, 56f, 56f);
            var hitImage = BattleHudKit.AddImage(hit, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.001f));
            hitImage.raycastTarget = true;
            var items = new List<(string label, Action action, Color color)>
            {
                ("編成", onLoadout, SpecInk),
                ("レリック", onRelics, SpecInk),
                ("ステータス", onStats, SpecInk),
                ("放棄", () => ConfirmAbandon(onAbandon), HexColor("#C8352B"))
            };
            var menu = BuildRouteMenu(root, items);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                menu.transform.SetAsLastSibling();
                menu.SetActive(!menu.activeSelf);
            });
        }

        void BuildWallet(Transform root)
        {
            var shard = SpecImage(root, "Shard", "icon_wallet_shard");
            TopLeft(shard.rectTransform, 1680f, 16f, 24f, 36f);
            shard.preserveAspect = true;
            var coins = _hudParty != null ? _hudParty.RunCoins.ToString() : "0";
            RouteText(root, "Coins", coins, BattleHudKit.SerifRegular, 28f, SpecInk, TextAlignmentOptions.MidlineLeft, 1732f, 20f, 68f, 28f, 0f);
            var divider = TopBox(root, "WalletRule", 1806f, 20f, 1f, 28f);
            BattleHudKit.AddImage(divider, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.3f));
        }

        GameObject BuildRouteMenu(Transform root, List<(string label, Action action, Color color)> items)
        {
            const float rowH = 56f;
            var blocker = BattleHudKit.Img(root, "MenuBlocker", BattleUISprites.Solid, new Color(0f, 0f, 0f, 0.35f), Vector2.zero, Vector2.one);
            blocker.raycastTarget = true;
            var menu = blocker.gameObject;
            var close = menu.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => menu.SetActive(false));
            var panel = TopBox(blocker.rectTransform, "Menu", 1600f, 68f, 280f, items.Count * rowH + 16f);
            var bg = BattleHudKit.AddImage(panel, BattleUISprites.Solid, HexColor("#121114", 0.95f));
            bg.raycastTarget = true;
            FrameEdges(panel, 1f, new Color(1f, 1f, 1f, 0.18f));
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = TopBox(panel, "Item" + i, 0f, 8f + i * rowH, 280f, rowH);
                var rowBg = BattleHudKit.AddImage(row, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0f));
                rowBg.raycastTarget = true;
                RouteText(row, "Label", item.label, BattleHudKit.Sans, 24f, item.color, TextAlignmentOptions.MidlineLeft, 24f, 0f, 232f, rowH, 0f);
                if (i < items.Count - 1)
                {
                    var rule = TopBox(row, "Rule", 16f, rowH - 1f, 248f, 1f);
                    BattleHudKit.AddImage(rule, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.1f));
                }

                var rowButton = row.gameObject.AddComponent<Button>();
                rowButton.targetGraphic = rowBg;
                rowButton.onClick.AddListener(() =>
                {
                    menu.SetActive(false);
                    item.action?.Invoke();
                });
            }

            menu.SetActive(false);
            return menu;
        }

        void ConfirmAbandon(Action onAbandon)
        {
            var blocker = BattleHudKit.Img(transform, "AbandonConfirm", BattleUISprites.Solid, new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.one);
            blocker.raycastTarget = true;
            var panel = BattleHudKit.Place(blocker.rectTransform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 220f));
            BattleHudKit.AddImage(panel, BattleUISprites.Solid, HexColor("#121114", 0.98f));
            RouteText(panel, "Ask", "このランを放棄しますか？", BattleHudKit.SerifRegular, 28f, SpecInk, TextAlignmentOptions.Center, 24f, 36f, 512f, 48f, 0f);
            var yes = TopBox(panel, "Yes", 48f, 120f, 220f, 64f);
            var yesImage = BattleHudKit.AddImage(yes, BattleUISprites.Solid, HexColor("#C8352B"));
            yesImage.raycastTarget = true;
            RouteText(yes, "Label", "放棄する", BattleHudKit.SerifMedium, 24f, Color.white, TextAlignmentOptions.Center, 0f, 0f, 220f, 64f, 4f);
            var yesButton = yes.gameObject.AddComponent<Button>();
            yesButton.targetGraphic = yesImage;
            yesButton.onClick.AddListener(() => onAbandon?.Invoke());
            var no = TopBox(panel, "No", 292f, 120f, 220f, 64f);
            var noImage = BattleHudKit.AddImage(no, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.08f));
            noImage.raycastTarget = true;
            RouteText(no, "Label", "戻る", BattleHudKit.SerifRegular, 24f, SpecInk, TextAlignmentOptions.Center, 0f, 0f, 220f, 64f, 0f);
            var noButton = no.gameObject.AddComponent<Button>();
            noButton.targetGraphic = noImage;
            noButton.onClick.AddListener(() => Destroy(blocker.gameObject));
        }

        void BuildSpecCard(Transform root, int index, string typeId, float left, float top, float width, float height, bool current, bool done, bool fixedSquare, Dictionary<string, int> used, Action onClick, RectTransform host = null)
        {
            GameObject card;
            RectTransform rt;
            if (host != null)
            {
                while (host.childCount > 0)
                    DestroyImmediate(host.GetChild(0).gameObject);
                card = new GameObject("Body", typeof(RectTransform));
                card.transform.SetParent(host, false);
                rt = card.GetComponent<RectTransform>();
                Stretch(rt);
                width = host.sizeDelta.x;
                height = host.sizeDelta.y;
            }
            else
            {
                card = new GameObject("Card" + index, typeof(RectTransform));
                card.transform.SetParent(root, false);
                rt = card.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(left + width * 0.5f, -(top + height * 0.5f));
                rt.sizeDelta = new Vector2(width, height);
            }
            var back = card.AddComponent<Image>();
            BattleUITheme.EnsureSprite(back);
            back.color = new Color(0.082f, 0.078f, 0.09f, 1f);
            back.raycastTarget = onClick != null;

            var clip = new GameObject("Clip", typeof(RectTransform));
            clip.transform.SetParent(card.transform, false);
            Stretch(clip.GetComponent<RectTransform>());
            clip.AddComponent<RectMask2D>();
            var artName = NextArt(typeId == "elevator" ? null : typeId, used);
            if (!string.IsNullOrEmpty(artName))
            {
                var art = MakeImage(clip.transform, "Art", "Illustrations/" + artName, false, 1f);
                var artRt = art.rectTransform;
                artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 0.5f);
                artRt.pivot = new Vector2(0.5f, 0.62f);
                artRt.anchoredPosition = Vector2.zero;
                if (art.sprite != null)
                {
                    var rect = art.sprite.rect;
                    var scale = Mathf.Max(width / Mathf.Max(1f, rect.width), height / Mathf.Max(1f, rect.height));
                    artRt.sizeDelta = new Vector2(rect.width * scale, rect.height * scale);
                }

                art.color = current ? Color.white : done ? new Color(0.62f, 0.62f, 0.62f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
                art.raycastTarget = false;
            }

            var shade = SpecImage(clip.transform, "Shade", "card_shade");
            Stretch(shade.rectTransform);
            if (done)
            {
                var film = new GameObject("Film", typeof(RectTransform));
                film.transform.SetParent(clip.transform, false);
                Stretch(film.GetComponent<RectTransform>());
                var filmImage = film.AddComponent<Image>();
                BattleUITheme.EnsureSprite(filmImage);
                filmImage.color = HexColor("#141416", 0.27f);
                filmImage.raycastTarget = false;
            }

            var frame = current ? HexColor("#D6C5B8") : new Color(1f, 1f, 1f, done ? 0.14f : 0.22f);
            var edges = FrameEdges(rt, current ? 2f : 1f, frame);
            if (current)
            {
                PlaceCorner(rt, 13f, 13f, 1f, 1f);
                PlaceCorner(rt, width - 13f, 13f, -1f, 1f);
                PlaceCorner(rt, width - 13f, height - 13f, -1f, -1f);
                PlaceCorner(rt, 13f, height - 13f, 1f, -1f);
            }

            if (fixedSquare)
            {
                var line = TopBox(rt, "FixedLine", 6f, 9f, width - 12f, 1f);
                BattleHudKit.AddImage(line, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.16f));
                BuildFixedTag(rt);
            }

            if (!done)
            {
                var label = RouteLabel(typeId);
                BuildRouteName(rt, label, RouteIcon(typeId), current ? 136f : 132f);
                if (current)
                {
                    RouteText(rt, "Status", "現在地", BattleHudKit.SerifRegular, 24f, SpecSub, TextAlignmentOptions.Center, 0f, height - 102f, width, 28f, 5f);
                    BuildAdvance(rt, width, height);
                }
                else
                {
                    var rule = SpecImage(rt, "Rule", "divider_fade");
                    TopLeft(rule.rectTransform, (width - 128f) * 0.5f, height - 55f, 128f, 2f);
                    RouteText(rt, "Status", "未到達", BattleHudKit.SerifRegular, 24f, HexColor("#E2DEDC", 0.85f), TextAlignmentOptions.Center, 0f, height - 42f, width, 28f, 5f);
                }
            }

            if (onClick == null)
                return;
            var button = card.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            var trigger = card.AddComponent<EventTrigger>();
            void Paint(bool over, bool down)
            {
                rt.localScale = Vector3.one * (down ? 0.98f : over ? 1.02f : 1f);
                var color = down ? new Color(frame.r * 0.92f, frame.g * 0.92f, frame.b * 0.92f, frame.a) : over ? HexColor("#ECDED2") : frame;
                foreach (var edge in edges)
                {
                    if (edge != null)
                        edge.color = color;
                }
            }

            AddRouteTrigger(trigger, EventTriggerType.PointerEnter, () => Paint(true, false));
            AddRouteTrigger(trigger, EventTriggerType.PointerExit, () => Paint(false, false));
            AddRouteTrigger(trigger, EventTriggerType.PointerDown, () => Paint(true, true));
            AddRouteTrigger(trigger, EventTriggerType.PointerUp, () => Paint(true, false));
        }

        static void AddRouteTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void BuildRouteName(RectTransform card, string title, string iconName, float fromBottom)
        {
            var spacing = string.IsNullOrEmpty(title) || title.Length <= 2 ? 35f : 10f;
            var probe = RouteText(card, "Probe", title, BattleHudKit.SerifMedium, 32f, SpecInk, TextAlignmentOptions.MidlineLeft, 0f, 0f, 8f, 8f, spacing);
            probe.ForceMeshUpdate();
            var textW = Mathf.Max(8f, probe.preferredWidth);
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            var icon = string.IsNullOrEmpty(iconName) ? null : SpecSprite(iconName);
            var rowW = textW + (icon != null ? 60f : 0f);
            var row = new GameObject("NameRow", typeof(RectTransform));
            row.transform.SetParent(card, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, fromBottom);
            rowRt.sizeDelta = new Vector2(rowW, 40f);
            var textX = 0f;
            if (icon != null)
            {
                var iconImage = SpecImage(rowRt, "Icon", iconName);
                var iconRt = iconImage.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(20f, 0f);
                iconRt.sizeDelta = new Vector2(40f, 40f);
                iconImage.preserveAspect = true;
                textX = 60f;
            }

            RouteText(rowRt, "Name", title, BattleHudKit.SerifMedium, 32f, SpecInk, TextAlignmentOptions.MidlineLeft, textX, 0f, textW + 4f, 40f, spacing);
        }

        static void BuildAdvance(RectTransform card, float cardW, float cardH)
        {
            var width = cardW - 32f;
            var button = TopBox(card, "Advance", 16f, cardH - 72f, width, 64f);
            var sprite = SpecSprite("btn_advance");
            var image = BattleHudKit.AddImage(button, sprite != null ? sprite : BattleUISprites.Solid, sprite != null ? Color.white : HexColor("#4E403D", 0.85f));
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            var probe = RouteText(button, "Probe", "進む", BattleHudKit.SerifMedium, 34f, SpecInk, TextAlignmentOptions.MidlineLeft, 0f, 0f, 8f, 8f, 30f);
            probe.ForceMeshUpdate();
            var textW = Mathf.Max(32f, probe.preferredWidth);
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            var group = textW + 20f + 18f;
            var start = (width - group) * 0.5f;
            RouteText(button, "Label", "進む", BattleHudKit.SerifMedium, 34f, SpecInk, TextAlignmentOptions.MidlineLeft, start, 16f, textW + 4f, 32f, 30f);
            var chevron = SpecImage(button, "Chevron", "icon_chevron");
            TopLeft(chevron.rectTransform, start + textW + 20f, 23f, 18f, 18f);
            chevron.preserveAspect = true;
        }

        static void BuildFixedTag(RectTransform card)
        {
            var tag = new GameObject("FixedTag", typeof(RectTransform));
            tag.transform.SetParent(card, false);
            var rt = tag.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(208f, 48f);
            var sprite = SpecSprite("fixed_tag");
            BattleHudKit.AddImage(rt, sprite != null ? sprite : BattleUISprites.Solid, sprite != null ? Color.white : HexColor("#121114", 0.92f));
            var probe = RouteText(rt, "Probe", "固定", BattleHudKit.SerifMedium, 24f, HexColor("#D9D5D9"), TextAlignmentOptions.MidlineLeft, 0f, 0f, 8f, 8f, 20f);
            probe.ForceMeshUpdate();
            var textW = Mathf.Max(24f, probe.preferredWidth);
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            var start = (208f - (20f + 14f + textW)) * 0.5f;
            var gem = SpecImage(rt, "Gem", "icon_fixed_diamond");
            TopLeft(gem.rectTransform, start, 14f, 20f, 20f);
            gem.preserveAspect = true;
            RouteText(rt, "Text", "固定", BattleHudKit.SerifMedium, 24f, HexColor("#D9D5D9"), TextAlignmentOptions.MidlineLeft, start + 34f, 12f, textW + 8f, 24f, 20f);
        }

        string RouteLabel(string typeId)
        {
            if (typeId == "boss" || typeId == "danger")
                return "強敵";
            if (typeId == "elevator")
                return "エレベーター";
            var type = _catalog != null ? _catalog.FindType(typeId) : null;
            if (type != null && !string.IsNullOrEmpty(type.Name))
                return type.Name;
            var card = _catalog != null ? _catalog.FindCard(typeId) : null;
            return card != null && !string.IsNullOrEmpty(card.Title) ? card.Title : typeId;
        }

        static string RouteIcon(string typeId)
        {
            switch (typeId)
            {
                case "event": return "icon_event";
                case "rest": return "icon_rest";
                case "treasure": return "icon_treasure";
                case "battle":
                case "boss":
                case "danger": return "icon_battle";
                default: return null;
            }
        }

        void BuildRouteFooter(Transform root)
        {
            var rule = TopBox(root, "FooterRule", 52f, 936f, 1828f, 1f);
            BattleHudKit.AddImage(rule, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.18f));
            var hp = _hudParty != null ? (int)_hudParty.CurrentHp : 0;
            var hpMax = _hudParty != null ? (int)_hudParty.MaxHp : 0;
            RouteText(root, "HpLabel", "HP", BattleHudKit.SerifRegular, 24f, SpecInk, TextAlignmentOptions.MidlineLeft, 80f, 968f, 48f, 24f, 4f);
            RouteText(root, "HpValue", hp.ToString(), BattleHudKit.SerifRegular, 36f, HexColor("#F699BA"), TextAlignmentOptions.MidlineLeft, 160f, 962f, 84f, 32f, 0f);
            RouteText(root, "HpMax", "/ " + hpMax, BattleHudKit.SerifRegular, 26f, SpecSub, TextAlignmentOptions.MidlineLeft, 248f, 968f, 120f, 24f, 0f);
            var hpAmount = _hudParty != null && _hudParty.MaxHp > 0f ? Mathf.Clamp01(_hudParty.CurrentHp / _hudParty.MaxHp) : 0f;
            BuildRouteGauge(root, "HpMeter", 80f, 1008f, 496f, 12f, HexColor("#F490B4"), new Color(1f, 1f, 1f, 0.2f), true, hpAmount);

            var split = TopBox(root, "FooterSplit", 624f, 972f, 1f, 64f);
            BattleHudKit.AddImage(split, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.22f));
            var mad = _hudMadness != null ? _hudMadness.CurrentMadness : 0;
            var madMax = _hudMadness != null ? _hudMadness.MaxMadness : 0;
            RouteText(root, "MadLabel", "狂気", BattleHudKit.SerifRegular, 24f, SpecInk, TextAlignmentOptions.MidlineLeft, 676f, 968f, 72f, 24f, 5f);
            var madValue = RouteText(root, "MadValue", mad.ToString(), BattleHudKit.SerifRegular, 36f, SpecInk, TextAlignmentOptions.MidlineLeft, 780f, 962f, 120f, 32f, 0f);
            madValue.ForceMeshUpdate();
            var madX = 780f + Mathf.Max(20f, madValue.preferredWidth) + 12f;
            RouteText(root, "MadMax", "/ " + madMax, BattleHudKit.SerifRegular, 26f, SpecSub, TextAlignmentOptions.MidlineLeft, madX, 968f, 120f, 24f, 0f);
            BuildRouteGauge(root, "MadMeter", 676f, 1008f, 428f, 12f, HexColor("#8A4FD0"), HexColor("#6E6A66"), false, madMax > 0 ? mad / (float)madMax : 0f);

            var list = _hudTrust ?? Array.Empty<TrustGaugeView>();
            var shown = Mathf.Min(3, list.Length);
            float[] avatarX = { 1232f, 1480f, 1728f };
            for (var i = 0; i < shown; i++)
            {
                var gauge = list[i];
                if (gauge == null)
                    continue;
                var cx = avatarX[i];
                var portrait = gauge.Portrait != null ? gauge.Portrait : BattlePortraitResolver.LoadCharacter(gauge.Name);
                var face = BattleHudKit.CirclePortrait(root, "Portrait" + i, portrait, 96f, new Vector2(0f, 1f), new Vector2(cx, -1000f), new Color(1f, 1f, 1f, 0.25f), 1f);
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
                RouteText(root, "Name" + i, gauge.Name ?? "", BattleHudKit.SerifRegular, 20f, HexColor("#DEDEDE"), TextAlignmentOptions.MidlineLeft, nameX, 969f, 150f, 24f, 5f);
                var underline = TopBox(root, "NameRule" + i, nameX - 4f, 999f, 64f, 1f);
                BattleHudKit.AddImage(underline, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.3f));
                RouteText(root, "Trust" + i, $"<size=22>信頼</size><space=12><size=24>{gauge.Trust}</size>", BattleHudKit.SerifRegular, 22f, HexColor("#E9EAEB"), TextAlignmentOptions.MidlineLeft, nameX, 1009f, 160f, 26f, 0f);
            }
        }

        bool TryBindDungeonRoute(
            Transform root,
            int layer,
            string[] typeIds,
            bool[] cleared,
            int currentIndex,
            Action<int> onEnter,
            Action onLoadout,
            Action onRelics,
            Action onStats,
            Action onAbandon)
        {
            var prefab = DungeonRouteView.Load();
            if (_buildingPrefab || prefab == null || !prefab.Ready)
                return false;

            var view = Instantiate(prefab, root);
            view.name = "DungeonRoute";
            Stretch(view.GetComponent<RectTransform>());
            RepairRouteVisuals(view);
            var count = typeIds != null ? Mathf.Min(4, typeIds.Length) : 0;
            if (view.floorTitle != null)
                view.floorTitle.text = $"第{layer}層";
            if (view.coins != null)
                view.coins.text = _hudParty != null ? _hudParty.RunCoins.ToString() : "0";

            var hp = _hudParty != null ? (int)_hudParty.CurrentHp : 0;
            var hpMax = _hudParty != null ? (int)_hudParty.MaxHp : 0;
            if (view.hpValue != null)
                view.hpValue.text = hp.ToString();
            if (view.hpMax != null)
                view.hpMax.text = "/ " + hpMax;
            if (view.hpFill != null)
                view.hpFill.fillAmount = hpMax > 0 ? Mathf.Clamp01(hp / (float)hpMax) : 0f;

            var mad = _hudMadness != null ? _hudMadness.CurrentMadness : 0;
            var madMax = _hudMadness != null ? _hudMadness.MaxMadness : 0;
            if (view.madValue != null)
                view.madValue.text = mad.ToString();
            if (view.madMax != null)
                view.madMax.text = "/ " + madMax;
            if (view.madFill != null)
                view.madFill.fillAmount = madMax > 0 ? Mathf.Clamp01(mad / (float)madMax) : 0f;

            BindRoutePortraits(view);
            LayoutRouteCards(view, count, cleared, currentIndex);
            var used = new Dictionary<string, int>();
            for (var i = 0; i < count; i++)
            {
                var done = cleared != null && i < cleared.Length && cleared[i];
                var current = i == currentIndex && !done;
                var index = i;
                var slot = view.cardSlots[i];
                BuildSpecCard(slot, i, typeIds[i], 0f, 0f, slot.sizeDelta.x, slot.sizeDelta.y, current, done, i == count - 1, used,
                    current ? () => onEnter?.Invoke(index) : null, slot);
            }

            PlaceRouteTrack(view, RouteCardCenters(view, count), cleared, currentIndex);
            WireRouteMenu(view, onLoadout, onRelics, onStats, onAbandon);
            return true;
        }

        static void RepairRouteVisuals(DungeonRouteView view)
        {
            foreach (var image in view.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null)
                    continue;
                var circle = image.name is "Ring" or "Mask" or "Face";
                image.sprite = circle ? BattleUISprites.Circle : BattleUISprites.Solid;
                if (image.sprite != null && image.color.a <= 0f)
                    image.color = Color.white;
            }

            foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "FloorTitle")
                    text.font = BattleHudKit.SerifMedium;
                else if (text.name == "Label")
                    text.font = BattleHudKit.Sans;
                else
                    text.font = BattleHudKit.SerifRegular;
            }
        }

        void BindRoutePortraits(DungeonRouteView view)
        {
            var list = _hudTrust ?? Array.Empty<TrustGaugeView>();
            for (var i = 0; i < 3; i++)
            {
                var gauge = i < list.Length ? list[i] : null;
                if (view.portraitNames != null && i < view.portraitNames.Length && view.portraitNames[i] != null)
                    view.portraitNames[i].text = gauge?.Name ?? "";
                if (view.portraitTrust != null && i < view.portraitTrust.Length && view.portraitTrust[i] != null)
                    view.portraitTrust[i].text = gauge != null ? $"<size=22>信頼</size><space=12><size=24>{gauge.Trust}</size>" : "";
                if (view.portraits == null || i >= view.portraits.Length || view.portraits[i] == null)
                    continue;
                var portrait = gauge == null ? null : gauge.Portrait != null ? gauge.Portrait : BattlePortraitResolver.LoadCharacter(gauge.Name);
                view.portraits[i].sprite = portrait;
                view.portraits[i].color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        void LayoutRouteCards(DungeonRouteView view, int count, bool[] cleared, int currentIndex)
        {
            var x = 0f;
            for (var i = 0; i < count && i < view.cardSlots.Length; i++)
            {
                var slot = view.cardSlots[i];
                if (slot == null)
                    continue;
                var done = cleared != null && i < cleared.Length && cleared[i];
                var current = i == currentIndex && !done;
                var width = current ? view.currentWidth : view.normalWidth;
                var height = current ? view.currentHeight : view.normalHeight;
                var drop = current ? 0f : view.normalDrop;
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(width, height);
                slot.anchoredPosition = new Vector2(x + width * 0.5f, -(drop + height * 0.5f));
                x += width + view.gap;
            }
        }

        static float[] RouteCardCenters(DungeonRouteView view, int count)
        {
            var centers = new float[count];
            var origin = view.cards != null ? view.cards.anchoredPosition.x : 152f;
            for (var i = 0; i < count && i < view.cardSlots.Length; i++)
            {
                var slot = view.cardSlots[i];
                if (slot == null)
                    continue;
                centers[i] = origin + slot.anchoredPosition.x;
            }

            return centers;
        }

        static void PlaceRouteTrack(DungeonRouteView view, float[] centers, bool[] cleared, int currentIndex)
        {
            if (centers == null || centers.Length == 0)
                return;
            for (var i = 0; i < centers.Length; i++)
            {
                var done = cleared != null && i < cleared.Length && cleared[i];
                var current = i == currentIndex && !done;
                if (view.trackNumbers != null && i < view.trackNumbers.Length && view.trackNumbers[i] != null)
                {
                    var number = view.trackNumbers[i];
                    number.color = done ? HexColor("#8A858B") : HexColor("#EAE6E5");
                    var rt = number.rectTransform;
                    var pos = rt.anchoredPosition;
                    pos.x = centers[i];
                    rt.anchoredPosition = pos;
                }

                if (view.dots == null || i >= view.dots.Length || view.dots[i] == null)
                    continue;
                var dot = view.dots[i];
                dot.sprite = SpecSprite(current ? "dot_current" : done ? "dot_passed" : "dot_unreached");
                dot.color = dot.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                var dotRt = dot.rectTransform;
                var dotPos = dotRt.anchoredPosition;
                dotPos.x = centers[i] - dotRt.sizeDelta.x * 0.5f;
                dotRt.anchoredPosition = dotPos;
            }

            if (view.trackLine == null)
                return;
            var line = view.trackLine;
            var linePos = line.anchoredPosition;
            linePos.x = centers[0];
            line.anchoredPosition = linePos;
            line.sizeDelta = new Vector2(Mathf.Max(2f, centers[centers.Length - 1] - centers[0]), line.sizeDelta.y);
        }

        void WireRouteMenu(DungeonRouteView view, Action onLoadout, Action onRelics, Action onStats, Action onAbandon)
        {
            var show = onLoadout != null || onRelics != null || onStats != null || onAbandon != null;
            if (view.menuIcon != null)
                view.menuIcon.SetActive(show);
            if (view.menuButton != null)
                view.menuButton.gameObject.SetActive(show);
            if (!show)
            {
                if (view.menuRoot != null)
                    view.menuRoot.SetActive(false);
                return;
            }

            if (view.menuRoot != null)
            {
                view.menuRoot.SetActive(false);
                var blocker = view.menuRoot.GetComponent<Button>();
                if (blocker != null)
                {
                    blocker.onClick.RemoveAllListeners();
                    blocker.onClick.AddListener(() => view.menuRoot.SetActive(false));
                }
            }

            if (view.menuButton != null)
            {
                view.menuButton.onClick.RemoveAllListeners();
                view.menuButton.onClick.AddListener(() =>
                {
                    if (view.menuRoot == null)
                        return;
                    view.menuRoot.transform.SetAsLastSibling();
                    view.menuRoot.SetActive(!view.menuRoot.activeSelf);
                });
            }

            Action[] actions =
            {
                onLoadout,
                onRelics,
                onStats,
                () => ConfirmAbandon(onAbandon)
            };
            if (view.menuItems == null)
                return;
            for (var i = 0; i < view.menuItems.Length && i < actions.Length; i++)
            {
                var button = view.menuItems[i];
                var action = actions[i];
                if (button == null)
                    continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (view.menuRoot != null)
                        view.menuRoot.SetActive(false);
                    action?.Invoke();
                });
            }
        }

        static Image BuildRouteGauge(Transform root, string name, float x, float y, float w, float h, Color fill, Color border, bool highlight, float amount)
        {
            var meter = TopBox(root, name, x, y, w, h);
            BattleHudKit.AddImage(meter, BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.06f));
            FrameEdges(meter, 1f, border);
            var fillRt = TopBox(meter, "Fill", 1f, 1f, w - 2f, h - 2f);
            var image = BattleHudKit.AddImage(fillRt, BattleUISprites.Solid, fill);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = Mathf.Clamp01(amount);
            if (highlight)
            {
                var bar = TopBox(meter, "Highlight", 1f, h - 2f, (w - 2f) * image.fillAmount, 1f);
                BattleHudKit.AddImage(bar, BattleUISprites.Solid, HexColor("#FFACCD", 0.6f));
            }

            return image;
        }

        static Image[] FrameEdges(RectTransform card, float thickness, Color color)
        {
            Image Edge(string edge, Vector2 min, Vector2 max, Vector2 omin, Vector2 omax) =>
                BattleHudKit.Img(card, edge, BattleUISprites.Solid, color, min, max, omin, omax);
            return new[]
            {
                Edge("FrameT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero),
                Edge("FrameB", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness)),
                Edge("FrameL", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f)),
                Edge("FrameR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero)
            };
        }

        static void PlaceCorner(RectTransform card, float cx, float cy, float sx, float sy)
        {
            var rt = new GameObject("Corner", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(card, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.localScale = new Vector3(sx, sy, 1f);
            var image = BattleHudKit.AddImage(rt, SpecSprite("card_corner_l"), Color.white);
            image.preserveAspect = true;
        }

        static void CutRule(Transform root, float y, float x0, float x1, float[] centers, float halfGap, Color color)
        {
            var cursor = x0;
            for (var i = 0; i < centers.Length; i++)
            {
                var gap = centers[i] - halfGap;
                if (gap > cursor + 0.5f)
                {
                    var segment = TopBox(root, "HeaderRule" + i, cursor, y, gap - cursor, 1f);
                    BattleHudKit.AddImage(segment, BattleUISprites.Solid, color);
                }

                cursor = Mathf.Max(cursor, centers[i] + halfGap);
            }

            if (x1 > cursor + 0.5f)
            {
                var tail = TopBox(root, "HeaderRuleEnd", cursor, y, x1 - cursor, 1f);
                BattleHudKit.AddImage(tail, BattleUISprites.Solid, color);
            }
        }

        static RectTransform TopBox(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            TopLeft(rt, x, y, w, h);
            return rt;
        }

        static TMP_Text RouteText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, float x, float y, float w, float h, float spacing)
        {
            var tmp = BattleHudKit.Text(parent, name, text, font, size, color, align, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -(y + h)), new Vector2(x + w, -y));
            tmp.characterSpacing = spacing;
            return tmp;
        }

        static Image SpecImage(Transform parent, string name, string file)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = SpecSprite(file);
            image.color = image.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            return image;
        }

        static Sprite SpecSprite(string name)
        {
            var path = "Run/Path/Spec/" + name;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;
            var all = Resources.LoadAll<Sprite>(path);
            if (all != null && all.Length > 0)
                return all[0];
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
                return null;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Color HexColor(string html, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(html, out var color))
                color = Color.white;
            color.a = alpha;
            return color;
        }

        static readonly Color SpecInk = HexColor("#F4F1EF");
        static readonly Color SpecSub = HexColor("#E2DEDC");

        void PlaceArtMask(Image maskImage, Sprite frameSprite, float scale)
        {
            var rt = maskImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var maskSprite = maskImage.sprite;
            var cardW = 1075f * scale;
            var cardH = 1500f * scale;
            if (maskSprite == null || frameSprite == null || frameSprite.textureRect.width < 1f || frameSprite.textureRect.height < 1f)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(709.8f * scale, 1111f * scale);
                return;
            }

            var maskRect = maskSprite.textureRect;
            var frameRect = frameSprite.textureRect;
            var w = maskRect.width / frameRect.width * cardW;
            var h = maskRect.height / frameRect.height * cardH;
            var dx = ((maskRect.x + maskRect.width * 0.5f) - (frameRect.x + frameRect.width * 0.5f)) / frameRect.width * cardW;
            var dy = ((maskRect.y + maskRect.height * 0.5f) - (frameRect.y + frameRect.height * 0.5f)) / frameRect.height * cardH;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(dx, dy);
        }

        static void Cover(Image image, float boxW, float boxH)
        {
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (image.sprite == null)
            {
                rt.sizeDelta = new Vector2(boxW, boxH);
                rt.anchoredPosition = Vector2.zero;
                return;
            }
            var rect = image.sprite.rect;
            var scale = Mathf.Max(boxW / rect.width, boxH / rect.height);
            rt.sizeDelta = new Vector2(rect.width * scale, rect.height * scale);
            rt.anchoredPosition = Vector2.zero;
        }

        string NextArt(string type, Dictionary<string, int> used)
        {
            var card = _catalog.FindCard(type);
            if (card == null || card.Illustrations.Count == 0)
                return null;
            used.TryGetValue(type, out var index);
            used[type] = index + 1;
            return card.Illustrations[index % card.Illustrations.Count];
        }

        void EnterLayer()
        {
            var ids = new string[3];
            for (var i = 0; i < 3; i++)
                ids[i] = _board.Cells[_stop[i], i];
            var done = _onDone;
            gameObject.SetActive(false);
            Destroy(gameObject);
            done?.Invoke(ids);
        }

        Color TypeColor(string type)
        {
            var def = _catalog != null ? _catalog.FindType(type) : null;
            if (def == null)
                return new Color(0.2f, 0.16f, 0.14f, 1f);
            return new Color(def.R / 255f, def.G / 255f, def.B / 255f, 1f);
        }

        Image Panel(Transform parent, string name, float x, float y, float w, float h, string sprite, float ppu)
        {
            var image = MakeImage(parent, name, sprite, true, ppu);
            TopLeft(image.rectTransform, x, y, w, h);
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, bool serif, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            TopLeft(rt, x, y, w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = serif ? Serif() : Sans();
            if (tmp.font == null)
                tmp.font = serif ? BattleHudKit.Mincho : BattleHudKit.Gothic;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.text = text ?? "";
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        Image MakeImage(Transform parent, string name, string spriteKey, bool sliced, float ppu)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            var sprite = SpriteOf(spriteKey);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                image.pixelsPerUnitMultiplier = ppu;
            }
            else
            {
                BattleUITheme.EnsureSprite(image);
                image.color = new Color(0.12f, 0.09f, 0.08f, 0.92f);
            }
            return image;
        }

        static Sprite SpriteOf(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            var path = "RouteLottery/Sprites/" + key;
            return Resources.Load<Sprite>(path + "_2x")
                ?? Resources.Load<Sprite>(path + "_1x")
                ?? Resources.Load<Sprite>(path);
        }

        static TMP_FontAsset _serif;
        static TMP_FontAsset _sans;
        static TMP_FontAsset _brush;

        static TMP_FontAsset Serif() => _serif != null ? _serif : _serif = MakeFont("Battle/E5b/fonts/NotoSerifJP");
        static TMP_FontAsset Sans() => _sans != null ? _sans : _sans = MakeFont("Battle/E5b/fonts/NotoSansJP");
        static TMP_FontAsset Brush() => _brush != null ? _brush : _brush = MakeFont("RouteLottery/Fonts/YujiSyuku-Regular");

        static TMP_FontAsset MakeFont(string path)
        {
            var source = Resources.Load<Font>(path);
            if (source == null)
                return null;
            try
            {
                var asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 4096, 4096, AtlasPopulationMode.Dynamic, true);
                if (asset == null)
                    return null;
                var table = asset.fontWeightTable;
                if (table != null && table.Length >= 10)
                {
                    table[7] = new TMP_FontWeightPair { regularTypeface = asset };
                    table[9] = new TMP_FontWeightPair { regularTypeface = asset };
                }
                return asset;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Center(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
