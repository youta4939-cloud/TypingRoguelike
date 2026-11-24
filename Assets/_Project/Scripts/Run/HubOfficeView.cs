using System;
using System.Collections;
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
    /// <summary>The approved office hub. Art and text remain separate, resolution-independent UI.</summary>
    public sealed class HubOfficeView : MonoBehaviour
    {
        static readonly Color Paper = new Color32(229, 221, 208, 255);
        static readonly Color Muted = new Color32(169, 148, 134, 255);
        static readonly Color Red = new Color32(183, 47, 64, 255);
        static readonly Color Line = new Color32(151, 119, 100, 95);
        static readonly Color Panel = new Color32(28, 29, 32, 255);
        RectTransform _home, _modal;
        CanvasGroup _homeGroup;
        Text _context, _liftDisplay, _liftStatus, _liftPage, _liftHeader;
        Font _bodyFont, _headingFont;
        MetaProgressData _profile;
        Action _start, _resume, _relic, _shop, _repair;
        Action<int> _elevator;
        Action _back;
        Button _departure, _closeDoor;
        HubElevatorSelection _selection;
        List<int> _floors;
        readonly List<Button> _floorButtons = new List<Button>();
        RectTransform _floorGrid, _leftDoor, _rightDoor;
        Coroutine _travel;
        int _floorCount, _page;
        bool _moving, _leaving, _transitioning;
        internal bool KeyboardNavigation { get; private set; }

        internal void UseKeyboardNavigation(bool keyboard)
        {
            if (KeyboardNavigation == keyboard) return;
            KeyboardNavigation = keyboard;
            foreach (var focus in GetComponentsInChildren<HubOfficeFocus>()) focus.Refresh();
        }

        public void Build(MetaProgressData profile, Action start, Action resume,
            IReadOnlyList<int> floors, Action<int> elevator, Action relic, Action shop,
            Action repair)
        {
            _profile = profile;
            _start = start; _resume = resume; _elevator = elevator;
            _relic = relic; _shop = shop; _repair = repair;
            _floors = (floors ?? Array.Empty<int>()).Where(f => f > 0).Distinct().OrderBy(f => f).ToList();
            var config = Resources.Load<LayerConfig>("Run/LayerConfig");
            _floorCount = Mathf.Max(18, config != null ? config.totalLayers : 20,
                _floors.Count > 0 ? _floors.Max() : 1);
            _bodyFont = BattleUITheme.Font;
            _headingFont = Resources.Load<Font>("Battle/Fonts/ShipporiMinchoB1-ExtraBold") ?? _bodyFont;
            _home = Rect(transform, "Office", 0, 0, 1, 1);
            _homeGroup = _home.gameObject.AddComponent<CanvasGroup>();
            Art(_home, "Background", "UI/HubOffice/Background", 0, 0, 1, 1, true);
            Gradient(_home, "Readability", 0, 0, .78f, 1,
                new Color(.04f, .045f, .055f, .96f), new Color(.04f, .045f, .055f, 0));
            Aura(_home, "CharacterBackdropShadow", .51f, -.08f, 1.13f, .98f, new Color(0, 0, 0, .32f));
            Aura(_home, "WindowBounce", .65f, .29f, 1.04f, .95f, new Color(.61f, .68f, .79f, .075f));
            Aura(_home, "GroundContact", .58f, -.10f, 1.04f, .20f, new Color(0, 0, 0, .5f));
            // Composition follows the HTML: right-hand character with feet outside the viewport.
            var character = Art(_home, "Character", "UI/HubOffice/Character", .50f, -.30f, 1.17f, .93f, false);
            if (character != null) character.color = new Color(.9f, .9f, .93f, 1);
            Gradient(_home, "FloorShade", .5f, 0, 1, .18f,
                new Color(.04f, .045f, .055f, .95f), Color.clear, true);
            Box(_home, "Rail", .035f, 0, .036f, 1, Line);
            Box(_home, "CurrencyBand", .67f, .899f, .968f, .961f, new Color32(16, 18, 23, 235), 9);
            Label(_home, "Currency", $"魔石  {profile?.arcaneShards ?? 0:N0}     │     貯金  {profile?.bankedCoins ?? 0:N0}",
                24, .60f, .90f, .95f, .96f, Paper, false, TextAnchor.MiddleRight);
            Box(_home, "CurrencyRule", .68f, .96f, .957f, .961f, Line);
            var menu = Rect(_home, "Menu", .08f, .17f, .48f, .80f);
            Label(menu, "Eyebrow", "—   次の探索へ", 17, 0, .93f, 1, 1, Muted);
            SurfaceShadow(menu, "DepartShadow", 0, .71f, 1, .93f, 18);
            _departure = Button(menu, "Depart", "塔へ行く", 0, .71f, 1, .93f,
                new Color32(23, 22, 26, 245), () => Wipe(ShowEntrance), 92, true, TextAnchor.MiddleLeft, 18);
            var departureShape = _departure.GetComponent<HubOfficeShape>();
            departureShape.Gradient = true; departureShape.Vertical = true;
            departureShape.EndColor = new Color32(65, 54, 57, 245);
            Gradient(_departure.transform, "TopRule", 0, .988f, .95f, 1,
                new Color(1, .81f, .65f, .47f), new Color(1, .81f, .65f, .025f));
            TextShadow(_departure.transform.Find("Text").GetComponent<Text>(), 3, -4, .65f);
            var glint = Box(_departure.transform, "FocusGlint", 0, .982f, .76f, 1, new Color32(255, 183, 147, 190));
            glint.Gradient = true; glint.EndColor = Color.clear; glint.gameObject.SetActive(false);
            _departure.transition = Selectable.Transition.None;
            _departure.gameObject.AddComponent<HubOfficeFocus>().Configure(_departure.GetComponent<HubOfficeShape>(), () =>
            {
                if (_context != null) _context.text = "塔の入口へ。階段かエレベーターを選ぶ。";
            });
            Label(menu, "RouteHint", "階段・エレベーターを選ぶ", 17, 0, .65f, .80f, .71f, Muted);
            Box(menu, "DepartRule", 0, .63f, 1, .632f, Line);
            var active = profile != null && profile.towerRunActive;
            Label(menu, "RunState", active
                ? $"中断中   第{profile.towerRunFloor}層     レリック保持"
                : "新しい探索を始める", 19, 0, .55f, 1, .62f, Muted);
            Label(menu, "Preparation", "出発前の準備", 15, 0, .45f, 1, .51f, Muted);
            Service(menu, "Relic", "装備・倉庫", "レリックを整える", "装", "01", 0, .235f, .49f, .44f, _relic != null ? (Action)(() => Leave(_relic)) : null,
                "レリックを装備し、持ち帰った物資を整理する。");
            Service(menu, "Shop", "商店・開発", "物資の購入と開発", "商", "02", .51f, .235f, 1, .44f, _shop != null ? (Action)(() => Leave(_shop)) : null,
                "物資の購入と、レリックの開発を行う。");
            Service(menu, "Repair", "修復屋", "探索者の最大HPを強化", "修", "03", 0, .015f, .49f, .22f,
                _repair != null ? (Action)(() => Leave(_repair)) : null, "修復屋を開く。");
            Service(menu, "Record", "探索記録", "探索の軌跡を辿る", "録", "04", .51f, .015f, 1, .22f, ShowRecord,
                "これまでの探索と、持ち帰った成果を振り返る。");
            _context = Label(_home, "Context", "帰還時の状態を引き継ぎ、塔へ戻る。", 17,
                .08f, .105f, .48f, .16f, Muted);
            Box(_home, "FooterRule", .07f, .08f, .95f, .081f, Line);
            var settings = Button(_home, "Settings", "", .005f, .008f, .045f, .068f, Panel, ShowSettings);
            var gear = Box(settings.transform, "Gear", .17f, .16f, .83f, .84f, Paper);
            gear.Gear = true;
            Focus(_departure);
        }

        void Service(Transform parent, string name, string title, string subtitle, string mark, string index,
            float x0, float y0, float x1, float y1, Action action, string description)
        {
            SurfaceShadow(parent, name + "Shadow", x0, y0, x1, y1, 18);
            var button = Button(parent, name, "", x0, y0, x1, y1,
                new Color32(23, 22, 26, 245), action, 20, false, TextAnchor.MiddleLeft, 18);
            var surface = button.GetComponent<HubOfficeShape>();
            surface.Gradient = true; surface.Vertical = true;
            surface.EndColor = new Color32(65, 54, 57, 245);
            button.interactable = action != null;
            var rt = button.transform;
            Gradient(rt, "UpperSheen", .015f, .73f, .94f, .98f, Color.clear, new Color(1, .85f, .72f, .055f), true);
            Gradient(rt, "EdgeReflection", .005f, .02f, .009f, .98f,
                new Color(1, .78f, .65f, .015f), new Color(1, .78f, .65f, .24f), true);
            Box(rt, "BottomBevel", .01f, 0, 1, .014f, new Color(0, 0, 0, .65f));
            Label(rt, "Watermark", mark, 100, .68f, -.05f, 1, .95f,
                new Color(1, .85f, .7f, .08f), true, TextAnchor.LowerRight);
            var nameText = Label(rt, "Name", title, 29, .09f, .35f, .94f, .83f, Paper, true);
            TextShadow(nameText, 1.5f, -2, .7f);
            Label(rt, "Description", subtitle, 14, .09f, .12f, .90f, .40f, Muted);
            Label(rt, "Index", index + " —", 12, .70f, .72f, .94f, .96f, Muted, false, TextAnchor.MiddleRight);
            Gradient(rt, "TopRule", 0, .985f, .95f, 1,
                new Color(1, .81f, .65f, .47f), new Color(1, .81f, .65f, .025f));
            button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<HubOfficeFocus>().Configure(button.GetComponent<HubOfficeShape>(), () =>
            {
                if (_context != null) _context.text = description;
            });
        }

        void Wipe(Action change, bool doorsClosed = false)
        {
            if (_transitioning || _leaving || change == null) return;
            _transitioning = true;
            // The canvas survives HideAll, which destroys this hub at the midpoint.
            var rect = Rect(transform.parent, "TowerTransition", 0, 0, 1, 1);
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;
            rect.gameObject.AddComponent<GraphicRaycaster>();
            var curtain = rect.gameObject.AddComponent<HubDepartTransition>();
            curtain.Play(doorsClosed,
                () => { if (this != null && isActiveAndEnabled) change(); },
                () =>
                {
                    if (this == null)
                        return;
                    _transitioning = false;
                    if (_homeGroup != null)
                        _homeGroup.alpha = 1f;
                });
        }

        void Leave(Action action)
        {
            if (_leaving || action == null) return;
            _leaving = true;
            _homeGroup.interactable = false;
            if (_modal != null) _modal.GetComponent<CanvasGroup>().interactable = false;
            action();
        }

        void ShowEntrance()
        {
            Modal("TowerEntrance", "登塔業務 ／ 出発経路", CloseModal);
            Label(_modal, "Title", "塔の入口", 41, .07f, .77f, .90f, .89f, Paper, true);
            Label(_modal, "Rule", _profile != null && _profile.towerRunActive
                ? "刻印・レリックを引き継いで探索を続けます。"
                : "階段かエレベーターを選んでください。", 20, .07f, .66f, .92f, .77f, Muted);
            var stairs = Button(_modal, "Stairs", "階段\n<size=19>1層から探索を続ける</size>",
                .07f, .28f, .48f, .62f, new Color32(43, 35, 39, 255), () => Wipe(() => Leave(_start)), 38, true);
            var lift = Button(_modal, "Elevator", "エレベーター\n<size=19>解放した層へ移動</size>",
                .52f, .28f, .93f, .62f, new Color32(43, 35, 39, 255), ShowElevator, 34, true);
            lift.interactable = _elevator != null && _floors.Count > 0;
            if (!lift.interactable)
                Label(_modal, "ElevatorLocked", "エレベーター未解放", 17, .52f, .21f, .93f, .28f, Muted);
            if (_profile != null && _profile.towerRunActive && _resume != null)
                Button(_modal, "Resume", $"中断地点の第{_profile.towerRunFloor}層へ戻る →",
                    .07f, .08f, .93f, .20f, Color.clear, () => Wipe(() => Leave(_resume)), 21);
            Focus(stairs);
        }

        void ShowRecord()
        {
            Modal("RunRecord", "登塔業務 ／ 探索記録", CloseModal);
            Label(_modal, "Title", "探索記録", 41, .07f, .74f, .93f, .88f, Paper, true);
            Label(_modal, "Record", $"挑戦回数     {_profile?.totalRuns ?? 0}\n\n" +
                $"踏破 ／ 帰還     {_profile?.completedRuns ?? 0} ／ {_profile?.extractedRuns ?? 0}\n\n" +
                $"最高進行     {_profile?.bestClearedNodes ?? 0} ノード", 26, .09f, .15f, .91f, .69f, Paper);
        }

        void ShowSettings()
        {
            Modal("HubSettings", "登塔業務 ／ 設定・セーブ", CloseModal);
            Label(_modal, "Title", "設定・セーブ", 41, .07f, .75f, .93f, .88f, Paper, true);
            var volume = Label(_modal, "Volume", "音量  " + Mathf.RoundToInt(AudioListener.volume * 100) + "%",
                25, .07f, .59f, .60f, .70f, Paper);
            Button(_modal, "VolumeDown", "−", .66f, .58f, .77f, .71f, Panel, () =>
            { AudioListener.volume = Mathf.Clamp01(AudioListener.volume - .1f); volume.text = "音量  " + Mathf.RoundToInt(AudioListener.volume * 100) + "%"; }, 30);
            Button(_modal, "VolumeUp", "＋", .80f, .58f, .91f, .71f, Panel, () =>
            { AudioListener.volume = Mathf.Clamp01(AudioListener.volume + .1f); volume.text = "音量  " + Mathf.RoundToInt(AudioListener.volume * 100) + "%"; }, 30);
            var status = Label(_modal, "SaveStatus", "", 19, .07f, .14f, .93f, .27f, Muted);
            Button(_modal, "Save", "現在の進行を保存", .07f, .32f, .93f, .48f,
                new Color32(75, 38, 46, 255), () =>
                {
                    if (_profile == null) return;
                    MetaProgression.Save(_profile);
                    status.text = "保存しました。";
                }, 27);
        }

        void ShowElevator()
        {
            _selection = new HubElevatorSelection(_floors);
            _page = 0;
            Modal("ElevatorPanel", "登塔業務 ／ エレベーター", ShowEntrance);
            _modal.anchorMin = new Vector2(.09f, .06f);
            _modal.anchorMax = new Vector2(.91f, .94f);
            Label(_modal, "Title", "塔内昇降機", 25, .055f, .82f, .35f, .89f, Paper, true);
            Label(_modal, "LiftIdentity", "TOWER / ELEVATOR", 13, .36f, .83f, .61f, .88f, Muted, false, TextAnchor.MiddleRight);
            var doorFrame = Metal(_modal, "DoorFrame", .055f, .15f, .615f, .81f);
            // A masked doorway lets the doors slide into the frame rather than over the UI.
            var shaftImage = Box(doorFrame, "Shaft", .025f, .02f, .975f, .98f, new Color32(13, 16, 19, 255));
            shaftImage.gameObject.AddComponent<RectMask2D>();
            var shaft = shaftImage.transform;
            Gradient(shaft, "InteriorLight", 0, 0, 1, 1, new Color32(42, 43, 46, 255), new Color32(14, 17, 20, 255), true);
            Label(shaft, "InteriorMark", "塔", 150, .1f, .31f, .9f, .77f,
                new Color(1, .85f, .7f, .035f), true, TextAnchor.MiddleCenter);
            Box(shaft, "InteriorRail", .04f, .29f, .96f, .30f, new Color32(90, 81, 73, 255));
            Box(shaft, "CeilingLight", .06f, .975f, .94f, .983f, new Color32(221, 202, 169, 255));
            Box(shaft, "LeftGuide", .03f, 0, .034f, 1, new Color32(71, 65, 62, 255));
            Box(shaft, "RightGuide", .966f, 0, .97f, 1, new Color32(71, 65, 62, 255));
            _leftDoor = Metal(shaft, "LeftDoor", 0, 0, .5f, 1);
            _rightDoor = Metal(shaft, "RightDoor", .5f, 0, 1, 1);
            Box(_leftDoor, "DoorSeam", .984f, 0, 1, 1, new Color32(12, 14, 16, 255));
            Box(_rightDoor, "DoorSeam", 0, 0, .016f, 1, new Color32(12, 14, 16, 255));
            Box(_leftDoor, "Handle", .91f, .38f, .925f, .57f, new Color32(113, 104, 94, 255));
            Box(_rightDoor, "Handle", .075f, .38f, .09f, .57f, new Color32(113, 104, 94, 255));
            SetDoors(0);
            Box(_modal, "StatusLamp", .06f, .09f, .067f, .105f, Red);
            _liftStatus = Label(_modal, "LiftStatus", "扉は開いています。", 20, .08f, .075f, .61f, .12f, Paper);
            Label(_modal, "Instructions", "行き先を選択  →  「閉」で移動開始", 16, .055f, .02f, .615f, .07f, Muted);
            var panel = Metal(_modal, "ControlPlate", .66f, .07f, .945f, .88f);
            Screw(panel, "TopLeftScrew", .035f, .97f);
            Screw(panel, "TopRightScrew", .965f, .97f);
            Screw(panel, "BottomLeftScrew", .035f, .025f);
            Screw(panel, "BottomRightScrew", .965f, .025f);
            Box(panel, "DisplayRim", .09f, .805f, .91f, .95f, new Color32(94, 86, 78, 255));
            Box(panel, "DisplayBackground", .098f, .812f, .902f, .943f, new Color32(9, 14, 17, 255));
            _liftHeader = Label(panel, "DisplayCaption", "DESTINATION / 行き先", 12,
                .14f, .90f, .86f, .937f, Muted, false, TextAnchor.MiddleCenter);
            _liftDisplay = Label(panel, "Display", "—", 48, .12f, .825f, .88f, .90f,
                new Color32(245, 185, 117, 255), false, TextAnchor.MiddleCenter);
            _floorGrid = Rect(panel, "Floors", .12f, .27f, .88f, .78f);
            _closeDoor = Button(panel, "CloseDoor", "▷ ◁\n<size=17>閉</size>", .53f, .065f, .88f, .175f,
                new Color32(92, 67, 66, 255), StartLift, 29, false, TextAnchor.MiddleCenter, 5);
            _closeDoor.interactable = false;
            Button(panel, "OpenDoor", "◁ ▷\n<size=17>開</size>", .12f, .065f, .47f, .175f,
                new Color32(80, 79, 80, 255), OpenLift, 29, false, TextAnchor.MiddleCenter, 5);
            Box(panel, "DoorRule", .12f, .19f, .88f, .192f, Line);
            _liftPage = Label(panel, "PageRange", "", 13, .27f, .215f, .73f, .262f, Paper, false, TextAnchor.MiddleCenter);
            var pages = Mathf.CeilToInt(_floorCount / 18f);
            if (pages > 1)
            {
                Button(panel, "PreviousPage", "‹", .10f, .205f, .27f, .27f, Color.clear,
                    () => { _page = (_page + pages - 1) % pages; BuildFloorButtons(); }, 25);
                Button(panel, "NextPage", "›", .73f, .205f, .90f, .27f, Color.clear,
                    () => { _page = (_page + 1) % pages; BuildFloorButtons(); }, 25);
            }
            Label(panel, "BlankRule", "無記名：未解放", 12, .12f, .012f, .88f, .047f, Muted, false, TextAnchor.MiddleCenter);
            BuildFloorButtons();
            Canvas.ForceUpdateCanvases();
            SetDoors(0);
        }

        void BuildFloorButtons()
        {
            if (_liftPage != null) _liftPage.text = $"{_page * 18 + 1:00} — {Mathf.Min((_page + 1) * 18, _floorCount):00}";
            foreach (var b in _floorButtons) { b.gameObject.SetActive(false); Destroy(b.gameObject); }
            _floorButtons.Clear();
            for (var i = 0; i < 18; i++)
            {
                var floor = _page * 18 + 18 - i;
                if (floor > _floorCount) continue;
                var col = i % 3; var row = i / 3;
                var x = col / 3f; var y = 1 - (row + 1) / 6f;
                var unlocked = _selection.IsUnlocked(floor) && _elevator != null;
                var selected = _selection.SelectedFloor == floor;
                var button = Button(_floorGrid, "Floor_" + floor, unlocked ? floor.ToString() : "",
                    x + .035f, y + .018f, x + .298f, y + .148f,
                    selected ? new Color32(224, 162, 92, 255) : new Color32(108, 104, 100, 255),
                    () => SelectFloor(floor), 25);
                var image = button.GetComponent<HubOfficeShape>();
                image.Circle = true;
                // Concentric rims and an inset face make the controls read as physical pushbuttons.
                Box(button.transform, "ButtonBezel", .05f, .05f, .95f, .95f,
                    selected ? new Color32(173, 110, 59, 255) : new Color32(24, 27, 30, 255)).Circle = true;
                var face = Box(button.transform, "ButtonFace", .12f, .12f, .88f, .88f,
                    selected ? new Color32(123, 78, 43, 255) : new Color32(65, 66, 68, 255));
                face.Circle = true;
                button.targetGraphic = face;
                var text = button.transform.Find("Text").GetComponent<Text>();
                text.color = selected ? new Color32(255, 225, 173, 255) : Paper;
                text.transform.SetAsLastSibling();
                if (!unlocked)
                {
                    image.color = new Color32(57, 59, 61, 255);
                    face.color = new Color32(38, 41, 43, 255);
                }
                button.interactable = unlocked && !_moving;
                _floorButtons.Add(button);
            }
            Focus(_floorButtons.FirstOrDefault(b => b.interactable && b.name == "Floor_" + _selection.SelectedFloor)
                ?? _floorButtons.FirstOrDefault(b => b.interactable));
        }

        void SelectFloor(int floor)
        {
            if (_moving || !_selection.Select(floor)) return;
            _liftDisplay.text = floor.ToString("00") + " F";
            _liftHeader.text = "DESTINATION / 選択済み";
            _liftStatus.text = floor + "層を選択。\n「閉」で移動します。";
            _closeDoor.interactable = true;
            BuildFloorButtons();
        }

        void OpenLift()
        {
            if (_leaving) return;
            if (_travel != null) { StopCoroutine(_travel); _travel = null; }
            _moving = false;
            _selection.OpenDoors();
            _liftDisplay.text = "—";
            _liftHeader.text = "DESTINATION / 行き先";
            _liftStatus.text = "扉は開いています。";
            _closeDoor.interactable = false;
            SetDoors(0);
            BuildFloorButtons();
        }

        void StartLift()
        {
            if (_moving || !_selection.SelectedFloor.HasValue || _elevator == null) return;
            _moving = true;
            _closeDoor.interactable = false;
            foreach (var b in _floorButtons) b.interactable = false;
            _travel = StartCoroutine(CloseAndTravel());
        }

        IEnumerator CloseAndTravel()
        {
            _liftStatus.text = "扉が閉まります。";
            _liftHeader.text = "DOOR CLOSING / 閉扉";
            for (var time = 0f; time < .65f; time += Time.unscaledDeltaTime)
            { SetDoors(Mathf.SmoothStep(0, 1, time / .65f)); yield return null; }
            SetDoors(1);
            _liftStatus.text = _selection.SelectedFloor + "層へ移動します。";
            _liftHeader.text = "▲  MOVING / 移動中";
            if (_selection.TryDispatch(out var floor)) Wipe(() => Leave(() => _elevator(floor)), true);
        }

        void SetDoors(float closed)
        {
            if (_leftDoor == null || _rightDoor == null) return;
            _leftDoor.anchoredPosition = new Vector2(-_leftDoor.rect.width * (1 - closed), 0);
            _rightDoor.anchoredPosition = new Vector2(_rightDoor.rect.width * (1 - closed), 0);
        }

        void Modal(string name, string heading, Action back)
        {
            DisposeModal();
            _homeGroup.interactable = false; _homeGroup.blocksRaycasts = false;
            var overlay = Box(transform, name, 0, 0, 1, 1, new Color(0, 0, 0, .82f));
            if (name == "RunRecord" || name == "PartyMenu" || name == "HubSettings")
                StartCoroutine(FadePopup(overlay.gameObject.AddComponent<CanvasGroup>()));
            // Modal overlay blocks clicks but the home illustration remains visible.
            _modal = Rect(overlay.transform, "Window", .23f, .16f, .77f, .84f);
            _modal.gameObject.AddComponent<CanvasGroup>();
            Box(_modal, "Surface", 0, 0, 1, 1, Panel);
            Label(_modal, "Heading", heading, 16, .04f, .92f, .78f, .99f, Muted);
            Box(_modal, "HeadingRule", 0, .91f, 1, .912f, Line);
            var close = Button(_modal, "Back", "戻る ×", .79f, .92f, .97f, .99f, Color.clear, back, 17);
            _back = back;
            Focus(close);
        }

        internal static IEnumerator FadePopup(CanvasGroup group)
        {
            for (float elapsed = 0; group != null && elapsed < .12f; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.SmoothStep(0, 1, elapsed / .12f);
                yield return null;
            }
            if (group != null) group.alpha = 1;
        }

        void DisposeModal()
        {
            if (_travel != null) { StopCoroutine(_travel); _travel = null; }
            _moving = false;
            _floorButtons.Clear();
            if (_modal != null)
            {
                var overlay = _modal.parent.gameObject;
                overlay.SetActive(false);
                Destroy(overlay);
                _modal = null;
            }
        }

        void CloseModal()
        {
            DisposeModal(); _back = null;
            _homeGroup.interactable = true; _homeGroup.blocksRaycasts = true;
            Focus(_departure);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.tabKey.wasPressedThisFrame))
                UseKeyboardNavigation(true);
            if (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > .1f ||
                Gamepad.current.leftStick.ReadValue().sqrMagnitude > .3f))
                UseKeyboardNavigation(true);
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > .1f)
                UseKeyboardNavigation(false);
            if (!_transitioning && !_leaving && _modal != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                _back?.Invoke();
        }

        static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static RectTransform Metal(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var rim = Box(parent, name, x0, y0, x1, y1, new Color32(103, 96, 90, 255));
            Box(rim.transform, "Inset", .008f, .005f, .992f, .995f, new Color32(16, 20, 24, 255));
            Gradient(rim.transform, "BrushedMetal", .014f, .009f, .986f, .991f,
                new Color32(69, 71, 75, 255), new Color32(35, 39, 44, 255));
            Box(rim.transform, "EdgeLight", .014f, .988f, .986f, .991f, new Color32(133, 123, 111, 160));
            Box(rim.transform, "EdgeShadow", .014f, .009f, .986f, .014f, new Color32(9, 12, 16, 255));
            for (var i = 1; i <= 12; i++)
            {
                var y = .02f + i * .073f;
                Box(rim.transform, "MetalGrain_" + i, .02f, y, .98f, y + .001f,
                    new Color(1, 1, 1, i % 3 == 0 ? .025f : .012f));
            }
            return rim.rectTransform;
        }

        static void TextShadow(Text text, float x, float y, float alpha)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, alpha);
            shadow.effectDistance = new Vector2(x, y);
            shadow.useGraphicAlpha = true;
        }

        static void SurfaceShadow(Transform parent, string name, float x0, float y0, float x1, float y1, float notch)
        {
            var holder = Rect(parent, name, x0, y0, x1, y1);
            // Layered low-alpha silhouettes approximate a soft shadow without extra image assets.
            for (var i = 5; i >= 0; i--)
            {
                var expand = i * 2f;
                var shadow = Box(holder, "Falloff_" + i, 0, 0, 1, 1,
                    new Color(0, 0, 0, i == 0 ? .19f : .045f), notch);
                shadow.rectTransform.offsetMin = new Vector2(-expand, -expand - 7);
                shadow.rectTransform.offsetMax = new Vector2(expand, expand - 7);
            }
        }

        static void Aura(Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
        {
            var shape = Box(parent, name, x0, y0, x1, y1, color);
            shape.Radial = true;
        }

        static void Screw(Transform parent, string name, float x, float y)
        {
            var head = Box(parent, name, x - .013f, y - .008f, x + .013f, y + .008f,
                new Color32(126, 117, 105, 255));
            head.Circle = true;
            Box(head.transform, "Slot", .25f, .44f, .75f, .56f, new Color32(27, 29, 32, 255));
        }

        static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static HubOfficeShape Box(Transform parent, string name, float x0, float y0, float x1, float y1, Color color, float notch = 0)
        {
            var graphic = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<HubOfficeShape>();
            graphic.color = color; graphic.Notch = notch;
            graphic.raycastTarget = name == "TowerEntrance" || name == "PartyMenu" || name == "RunRecord" ||
                name == "HubSettings" || name == "ElevatorPanel";
            return graphic;
        }

        static void Gradient(Transform parent, string name, float x0, float y0, float x1, float y1,
            Color start, Color end, bool vertical = false)
        {
            var graphic = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<HubOfficeShape>();
            graphic.color = start; graphic.EndColor = end; graphic.Gradient = true;
            graphic.Vertical = vertical; graphic.raycastTarget = false;
        }

        static RawImage Art(Transform parent, string name, string resource, float x0, float y0, float x1, float y1, bool cover)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) { Debug.LogWarning("拠点画像が見つかりません: " + resource); return null; }
            var holder = Rect(parent, name + "Area", x0, y0, x1, y1);
            var image = Rect(holder, name, 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectRatio = (float)texture.width / texture.height;
            fitter.aspectMode = cover ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
            return image;
        }

        Text Label(Transform parent, string name, string value, int size, float x0, float y0, float x1, float y1,
            Color color, bool heading = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            label.text = value; label.font = heading ? _headingFont : _bodyFont; label.fontSize = size;
            label.color = color; label.alignment = alignment; label.raycastTarget = false;
            label.supportRichText = true; label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, size * 3 / 4); label.resizeTextMaxSize = size;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        Button Button(Transform parent, string name, string value, float x0, float y0, float x1, float y1,
            Color color, Action action, int size = 24, bool heading = false,
            TextAnchor alignment = TextAnchor.MiddleCenter, float notch = 0)
        {
            var shape = Box(parent, name, x0, y0, x1, y1, color, notch);
            shape.raycastTarget = true;
            var button = shape.gameObject.AddComponent<Button>();
            button.targetGraphic = shape;
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.35f, .95f, .95f, 1);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.8f, .65f, .65f, 1);
            colors.disabledColor = new Color(.45f, .45f, .45f, 1); colors.fadeDuration = .12f;
            button.colors = colors;
            button.onClick.AddListener(() => { if (!_leaving && !_transitioning) action?.Invoke(); });
            Label(shape.transform, "Text", value, size, .04f, .04f, .96f, .96f, Paper, heading, alignment);
            return button;
        }
    }

    public sealed class HubOfficeFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Action _preview;
        HubOfficeShape _shape;
        HubOfficeView _owner;
        Color _normal, _normalEnd;
        Transform _glint;
        bool _hovered, _selected;
        public void Configure(HubOfficeShape shape, Action preview)
        { _glint = transform.Find("FocusGlint"); _shape = shape; _normal = shape.color; _normalEnd = shape.EndColor; _preview = preview; _owner = GetComponentInParent<HubOfficeView>(); }
        public void Refresh()
        {
            if (_shape == null) return;
            var active = _owner != null && _owner.KeyboardNavigation ? _selected : _hovered;
            _shape.color = active ? new Color32(57, 19, 32, 238) : _normal;
            if (_glint != null) _glint.gameObject.SetActive(active);
            _shape.Highlight = active;
            _shape.EndColor = active ? new Color32(171, 60, 70, 248) : _normalEnd;
            _shape.SetVerticesDirty();
            if (active) _preview?.Invoke();
        }
        public void OnPointerEnter(PointerEventData eventData)
        { _hovered = true; _owner?.UseKeyboardNavigation(false); Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; Refresh(); }
        public void OnSelect(BaseEventData eventData) { _selected = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { _selected = false; Refresh(); }
    }

    /// <summary>Native UI geometry: notched file tabs, round buttons, and directional fades.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubOfficeShape : MaskableGraphic
    {
        public float Notch;
        public bool Circle, Gradient, Vertical, Radial, Gear, Highlight;
        public Color EndColor;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (Gear)
            {
                const int steps = 32;
                float radius = Mathf.Min(r.width, r.height) * .5f;
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * 2 * Mathf.PI / steps;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float outer = i % 4 == 1 || i % 4 == 2 ? radius : radius * .76f;
                    vh.AddVert(r.center + dir * radius * .36f, color, Vector2.zero);
                    vh.AddVert(r.center + dir * outer, color, Vector2.zero);
                    int a = i * 2, b = (a + 2) % (steps * 2);
                    vh.AddTriangle(a, a + 1, b); vh.AddTriangle(b, a + 1, b + 1);
                }
                return;
            }
            if (Radial)
            {
                const int segments = 48, rings = 8;
                vh.AddVert(r.center, color, Vector2.zero);
                for (var ring = 1; ring <= rings; ring++)
                {
                    var t = (float)ring / rings;
                    var c = color; c.a *= Mathf.Pow(1 - t, 2);
                    for (var i = 0; i < segments; i++)
                    {
                        var angle = i * Mathf.PI * 2 / segments;
                        vh.AddVert(r.center + new Vector2(Mathf.Cos(angle) * r.width, Mathf.Sin(angle) * r.height) * (.5f * t), c, Vector2.zero);
                        var current = 1 + (ring - 1) * segments + i;
                        var next = 1 + (ring - 1) * segments + (i + 1) % segments;
                        if (ring == 1) vh.AddTriangle(0, current, next);
                        else
                        {
                            vh.AddTriangle(current - segments, current, next);
                            vh.AddTriangle(current - segments, next, next - segments);
                        }
                    }
                }
                return;
            }
            if (Circle)
            {
                const int steps = 40;
                var radius = Mathf.Min(r.width, r.height) * .5f;
                vh.AddVert(r.center, color, Vector2.zero);
                for (var i = 0; i <= steps; i++)
                {
                    var a = i * Mathf.PI * 2 / steps;
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
                    if (i > 0) vh.AddTriangle(0, i, i + 1);
                }
                return;
            }
            var n = Mathf.Min(Notch, Mathf.Min(r.width, r.height) * .5f);
            var points = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                new Vector2(r.xMax - n, r.yMax), new Vector2(r.xMax, r.yMax - n), new Vector2(r.xMax, r.yMin) };
            foreach (var p in points)
            {
                var t = Vertical ? Mathf.InverseLerp(r.yMin, r.yMax, p.y) : Mathf.InverseLerp(r.xMin, r.xMax, p.x);
                if (Highlight) t = .62f * t + .38f * (1 - Mathf.InverseLerp(r.xMin, r.xMax, p.x));
                vh.AddVert(p, Gradient ? Color.Lerp(color, EndColor, t) : color, Vector2.zero);
            }
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3); vh.AddTriangle(0, 3, 4);
        }
    }
}
