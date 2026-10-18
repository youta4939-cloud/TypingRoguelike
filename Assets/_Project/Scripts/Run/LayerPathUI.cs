using System;
using System.Collections.Generic;
using TypingRoguelike.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>1層4マスの一本道。次の1マスだけ進める。</summary>
    public sealed class LayerPathUI
    {
        GameObject _root;
        GameObject _panel;
        GameObject _modal;
        Text _hpText;
        Text _coinText;
        Text _madnessText;
        Image _hpFill;
        Image _madnessFill;
        int _prepIndex;
        int _prepLayer;
        int _prepTotal;
        DungeonNodeType _prepFourth;
        string _prepMinimums;
        BoardChoice[] _prepChoices;
        System.Action<int> _prepConfirm;
        System.Action _prepDebug;

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
            Action onOpenPartyStats)
        {
            EnsureRoot(host);
            HideModal();
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            _panel = CreateFullScreen("LayerPath", new Color(0.012f, 0.014f, 0.035f, 1f));
            var window = BattleUIChrome.CreateLuxuryWindow(
                _panel.transform, "PathWindow", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f),
                $"{dungeon.Floor} / {dungeon.TotalFloors} 層", BattleUITheme.Arcane);
            var body = window.Find("Body");
            if (body == null)
                return;

            BuildStatus(body, party, madness, exploration, onOpenLoadout, onOpenRelics, onOpenPartyStats, onAbandonRun);
            BuildSquares(body, dungeon, onSquareSelected);
        }

        public void ShowBoardPreparation(
            Transform host,
            int nextLayer,
            int totalLayers,
            DungeonNodeType fourth,
            string minimums,
            BoardChoice[] choices,
            Action<int> onConfirm,
            Action onDebugRolls)
        {
            EnsureRoot(host);
            _prepLayer = nextLayer;
            _prepTotal = totalLayers;
            _prepFourth = fourth;
            _prepMinimums = minimums ?? "";
            _prepChoices = choices ?? System.Array.Empty<BoardChoice>();
            _prepConfirm = onConfirm;
            _prepDebug = onDebugRolls;
            _prepIndex = FirstSelectable(_prepChoices);
            RebuildBoardPreparation();
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
            if (_hpText != null && party != null)
                _hpText.text = $"HP {(int)party.CurrentHp} / {(int)party.MaxHp}";
            if (_coinText != null && party != null)
                _coinText.text = $"塔片 {party.RunCoins}";
            if (_hpFill != null && party != null && party.MaxHp > 0f)
                _hpFill.fillAmount = Mathf.Clamp01(party.CurrentHp / party.MaxHp);
            if (madness != null)
            {
                if (_madnessText != null)
                    _madnessText.text = $"狂気 {madness.CurrentMadness} / {madness.MaxMadness}";
                if (_madnessFill != null)
                {
                    var max = madness.MaxMadness;
                    _madnessFill.fillAmount = max > 0 ? madness.CurrentMadness / (float)max : 0f;
                    _madnessFill.color = RunMadnessState.StageColor(madness.Stage);
                }
            }

            _ = dungeon;
            _ = exploration;
        }

        public void ShowModal(string title, string body, IReadOnlyList<(string label, Action onClick)> actions)
        {
            HideModal();
            _modal = CreateFullScreen("LayerModal", new Color(0.005f, 0.008f, 0.02f, 0.86f));
            var win = BattleUIChrome.CreateLuxuryWindow(
                _modal.transform, "Modal", new Vector2(0.27f, 0.22f), new Vector2(0.73f, 0.78f),
                title, BattleUITheme.Gold);
            var bodyT = win.Find("Body");
            if (bodyT != null)
            {
                CreateLabel(bodyT, "Body", body, 22, TextAnchor.UpperCenter,
                    new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.92f), BattleUITheme.TextPrimary);
            }

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

        void RebuildBoardPreparation()
        {
            HideModal();
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            _panel = CreateFullScreen("BoardPrep", new Color(0.012f, 0.014f, 0.035f, 1f));
            var window = BattleUIChrome.CreateLuxuryWindow(
                _panel.transform, "PrepWindow", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f),
                $"{_prepLayer} / {_prepTotal} 層の準備", BattleUITheme.Gold);
            var body = window.Find("Body");
            if (body == null)
                return;

            var selected = _prepIndex >= 0 && _prepChoices != null && _prepIndex < _prepChoices.Length
                ? _prepChoices[_prepIndex]
                : null;
            CreateLabel(body, "Fourth",
                $"4マス目：{SquareName(_prepFourth)}\n{_prepMinimums}",
                20, TextAnchor.UpperLeft, new Vector2(0.34f, 0.78f), new Vector2(0.96f, 0.96f), BattleUITheme.TextPrimary);

            var odds = selected != null ? selected.Odds : "盤面を選ぶと、出現割合を表示する。";
            if (selected != null && !selected.Selectable)
                odds = selected.Reason + "\n\n" + odds;
            CreateLabel(body, "Odds", odds, 18, TextAnchor.UpperLeft,
                new Vector2(0.34f, 0.18f), new Vector2(0.96f, 0.76f), BattleUITheme.TextPrimary);

            var count = _prepChoices?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                var index = i;
                var choice = _prepChoices[i];
                var yMax = 0.92f - i * 0.16f;
                var label = choice.Selectable ? choice.Name : $"{choice.Name}\n選べない";
                var color = index == _prepIndex
                    ? new Color(1f, 0.48f, 0.08f, 1f)
                    : choice.Selectable
                        ? new Color(0.2f, 0.16f, 0.12f, 1f)
                        : new Color(0.18f, 0.18f, 0.2f, 1f);
                if (!choice.Selectable)
                {
                    var plate = CreateRect(body, "Locked", new Vector2(0.04f, yMax - 0.13f), new Vector2(0.3f, yMax));
                    var image = plate.gameObject.AddComponent<Image>();
                    image.color = color;
                    BattleUITheme.EnsureSprite(image);
                    CreateLabel(plate, "Text", label, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                        BattleUITheme.TextMuted);
                    continue;
                }

                CreateButton(body, label, new Vector2(0.04f, yMax - 0.13f), new Vector2(0.3f, yMax), () =>
                {
                    _prepIndex = index;
                    RebuildBoardPreparation();
                }, color);
            }

            var canGo = selected != null && selected.Selectable;
            if (canGo)
            {
                CreateButton(body, "次へ", new Vector2(0.34f, 0.03f), new Vector2(0.62f, 0.14f), () =>
                {
                    var confirm = _prepConfirm;
                    var index = _prepIndex;
                    confirm?.Invoke(index);
                });
            }
            else
            {
                CreateLabel(body, "NeedPick", "条件を満たす盤面を選ぶ", 18, TextAnchor.MiddleCenter,
                    new Vector2(0.34f, 0.03f), new Vector2(0.62f, 0.14f), BattleUITheme.TextMuted);
            }

            CreateButton(body, "1000回抽選をログ", new Vector2(0.66f, 0.03f), new Vector2(0.96f, 0.14f), () => _prepDebug?.Invoke());
        }

        static int FirstSelectable(BoardChoice[] choices)
        {
            if (choices == null)
                return -1;
            for (var i = 0; i < choices.Length; i++)
            {
                if (choices[i] != null && choices[i].Selectable)
                    return i;
            }

            return -1;
        }

        void BuildStatus(
            Transform parent,
            RunPartyState party,
            RunMadnessState madness,
            RunExplorationInventory exploration,
            Action onOpenLoadout,
            Action onOpenRelics,
            Action onOpenPartyStats,
            Action onAbandonRun)
        {
            _hpText = CreateLabel(parent, "Hp", party != null ? $"HP {(int)party.CurrentHp} / {(int)party.MaxHp}" : "HP",
                18, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.88f), new Vector2(0.34f, 0.96f), BattleUITheme.TextPrimary);
            _coinText = CreateLabel(parent, "Coins", party != null ? $"塔片 {party.RunCoins}" : "塔片",
                18, TextAnchor.MiddleLeft, new Vector2(0.36f, 0.88f), new Vector2(0.56f, 0.96f), BattleUITheme.Gold);
            _madnessText = CreateLabel(parent, "Madness",
                madness != null ? $"狂気 {madness.CurrentMadness} / {madness.MaxMadness}" : "狂気",
                18, TextAnchor.MiddleLeft, new Vector2(0.58f, 0.88f), new Vector2(0.82f, 0.96f), BattleUITheme.Arcane);
            _hpFill = null;
            _madnessFill = null;
            _ = exploration;

            CreateButton(parent, "編成", new Vector2(0.04f, 0.76f), new Vector2(0.18f, 0.84f), onOpenLoadout);
            CreateButton(parent, "レリック", new Vector2(0.2f, 0.76f), new Vector2(0.36f, 0.84f), onOpenRelics);
            CreateButton(parent, "能力", new Vector2(0.38f, 0.76f), new Vector2(0.52f, 0.84f), onOpenPartyStats);
            CreateButton(parent, "放棄", new Vector2(0.82f, 0.76f), new Vector2(0.96f, 0.84f), onAbandonRun);
        }

        void BuildSquares(Transform parent, DungeonRunState dungeon, Action<int> onSquareSelected)
        {
            CreateLabel(parent, "Hint", "左から順に進む。橙のマスが次。",
                18, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.7f), BattleUITheme.TextMuted);

            var open = dungeon.FirstUnclearedIndex();
            for (var i = 0; i < dungeon.Squares.Length; i++)
            {
                var index = i;
                var square = dungeon.Squares[i];
                var xMin = 0.06f + i * 0.235f;
                var xMax = xMin + 0.2f;
                var canClick = index == open;
                var color = canClick
                    ? new Color(1f, 0.48f, 0.08f, 1f)
                    : SquareColor(square.Type);
                if (square.Cleared)
                    color = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);
                else if (!canClick)
                    color = new Color(color.r * 0.75f, color.g * 0.75f, color.b * 0.75f, 1f);

                var label = $"{index + 1}\n{SquareName(square.Type)}";
                if (canClick)
                    CreateButton(parent, label, new Vector2(xMin, 0.28f), new Vector2(xMax, 0.58f),
                        () => onSquareSelected?.Invoke(index), color);
                else
                {
                    var plate = CreateRect(parent, $"Square_{index}", new Vector2(xMin, 0.28f), new Vector2(xMax, 0.58f));
                    var image = plate.gameObject.AddComponent<Image>();
                    image.color = color;
                    BattleUITheme.EnsureSprite(image);
                    CreateLabel(plate, "Text", label, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white)
                        .fontStyle = FontStyle.Bold;
                }

                if (i >= dungeon.Squares.Length - 1)
                    continue;
                var line = CreateRect(parent, $"Link_{index}", new Vector2(xMax - 0.005f, 0.41f), new Vector2(xMax + 0.04f, 0.45f));
                var lineImage = line.gameObject.AddComponent<Image>();
                lineImage.color = new Color(0.93f, 0.86f, 0.72f, 0.95f);
                BattleUITheme.EnsureSprite(lineImage);
                lineImage.raycastTarget = false;
            }
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
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = go;
        }

        GameObject CreateFullScreen(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            BattleUITheme.EnsureSprite(image);
            return go;
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

    public sealed class BoardChoice
    {
        public string Name;
        public string Odds;
        public bool Selectable;
        public string Reason;
    }
}
