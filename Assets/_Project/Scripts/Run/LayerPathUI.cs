using System;
using System.Collections.Generic;
using TypingRoguelike.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
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
        TrustGaugeView[] _trustGauges;
        System.Action _trustDebug;
        System.Action<string, int> _trustSet;
        readonly List<OpinionGaugeWidget> _opinionGauges = new();

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
            Action onTrustDebug = null)
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

            _trustGauges = trust;
            _trustDebug = onTrustDebug;
            BuildStatus(body, party, madness, exploration, onOpenLoadout, onOpenRelics, onOpenPartyStats, onAbandonRun);
            BuildSquares(body, dungeon, onSquareSelected);
            BuildTrustRow(body, trust, 0.02f, 0.24f);
        }

        public void ShowBoardPreparation(
            Transform host,
            int nextLayer,
            int totalLayers,
            DungeonNodeType fourth,
            string minimums,
            BoardChoice[] choices,
            Action<int> onConfirm,
            Action onDebugRolls,
            TrustGaugeView[] trust = null)
        {
            EnsureRoot(host);
            _prepLayer = nextLayer;
            _prepTotal = totalLayers;
            _prepFourth = fourth;
            _prepMinimums = minimums ?? "";
            _prepChoices = choices ?? System.Array.Empty<BoardChoice>();
            _prepConfirm = onConfirm;
            _prepDebug = onDebugRolls;
            _trustGauges = trust;
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
                new Vector2(0.34f, 0.36f), new Vector2(0.96f, 0.76f), BattleUITheme.TextPrimary);

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
            BuildTrustRow(body, _trustGauges, 0.15f, 0.30f);
        }

        public void ShowOpinion(OpinionScreenView view, Action<int> onPick)
        {
            EnsureRoot(null);
            HideModal();
            _opinionGauges.Clear();
            if (_panel != null)
                UnityEngine.Object.Destroy(_panel);

            var chrome = TypingRoguelike.Data.OpinionChrome.LoadOrFallback();
            _panel = CreateFullScreen("Opinion", new Color(0.012f, 0.014f, 0.035f, 1f));
            var window = BattleUIChrome.CreateLuxuryWindow(
                _panel.transform, "OpinionWindow", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                "", BattleUITheme.Gold);
            var body = window.Find("Body");
            if (body == null || view == null)
                return;

            var headerIcon = CreateRect(body, "TileIcon", new Vector2(0.03f, 0.9f), new Vector2(0.09f, 0.99f));
            ApplyPart(headerIcon.gameObject.AddComponent<Image>(), view.TileIcon, new Color(0.35f, 0.55f, 0.72f, 1f));
            var title = CreateLabel(body, "SceneTitle", view.SceneTitle ?? "", 28, TextAnchor.MiddleLeft,
                new Vector2(0.11f, 0.9f), new Vector2(0.97f, 0.99f), BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;

            BuildOpinionPanels(body, view.Gauges, chrome);

            var scenePlate = CreateRect(body, "SceneTextPlate", new Vector2(0.03f, 0.62f), new Vector2(0.97f, 0.73f));
            var sceneImage = scenePlate.gameObject.AddComponent<Image>();
            sceneImage.color = new Color(0.08f, 0.07f, 0.06f, 0.72f);
            BattleUITheme.EnsureSprite(sceneImage);
            sceneImage.raycastTarget = false;
            CreateLabel(scenePlate, "SceneText", view.SceneText ?? "", 22, TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), BattleUITheme.TextPrimary);

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
                bandImage.color = new Color(0.86f, 0.68f, 0.22f, 0.96f);
                BattleUITheme.EnsureSprite(bandImage);
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
                panelImage.color = gauge.Dim ? new Color(0.1f, 0.1f, 0.12f, 0.45f) : new Color(0.14f, 0.11f, 0.09f, 0.88f);
                BattleUITheme.EnsureSprite(panelImage);
                panelImage.raycastTarget = false;
                if (gauge.Dim)
                {
                    var group = panel.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0.72f;
                }

                var frame = CreateRect(panel, "FaceFrame", new Vector2(0.04f, 0.28f), new Vector2(0.28f, 0.94f));
                ApplyPart(frame.gameObject.AddComponent<Image>(), chrome != null ? chrome.faceFrame : null, new Color(0.55f, 0.4f, 0.22f, 1f));
                var face = CreateRect(frame, "Face", new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
                ApplyPart(face.gameObject.AddComponent<Image>(), gauge.Portrait, FaceColor(gauge.CharacterId));

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
                ApplyPart(trackImage, chrome != null ? chrome.gaugeTrack : null, new Color(0.06f, 0.06f, 0.08f, 0.95f));
                var preview = CreateRect(track, "GaugePreview", Vector2.zero, Vector2.one);
                var previewImage = preview.gameObject.AddComponent<Image>();
                ApplyPart(previewImage, chrome != null ? chrome.gaugeFill : null, new Color(1f, 0.92f, 0.45f, 1f));
                previewImage.type = Image.Type.Filled;
                previewImage.fillMethod = Image.FillMethod.Horizontal;
                previewImage.fillAmount = 0f;
                previewImage.raycastTarget = false;
                var fill = CreateRect(track, "GaugeFill", Vector2.zero, Vector2.one);
                var fillImage = fill.gameObject.AddComponent<Image>();
                ApplyPart(fillImage, chrome != null ? chrome.gaugeFill : null,
                    gauge.Dim ? new Color(0.45f, 0.32f, 0.18f, 0.7f) : new Color(0.93f, 0.62f, 0.28f, 1f));
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillAmount = Mathf.Clamp01(gauge.Trust / 100f);
                fillImage.raycastTarget = false;
                foreach (var mark in new[] { 0.25f, 0.5f, 0.75f })
                {
                    var tick = CreateRect(track, "GaugeTick", new Vector2(mark, 0f), new Vector2(mark, 1f));
                    tick.offsetMin = new Vector2(-1.5f, 0f);
                    tick.offsetMax = new Vector2(1.5f, 0f);
                    var tickImage = tick.gameObject.AddComponent<Image>();
                    tickImage.color = new Color(1f, 1f, 1f, 0.9f);
                    BattleUITheme.EnsureSprite(tickImage);
                    tickImage.raycastTarget = false;
                }

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
            ApplyPart(frame.gameObject.AddComponent<Image>(), chrome != null ? chrome.faceFrame : null, new Color(0.55f, 0.4f, 0.22f, 1f));
            var face = CreateRect(frame, "Face", new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            ApplyPart(face.gameObject.AddComponent<Image>(), bubble.Portrait, FaceColor(bubble.CharacterId));
            CreateLabel(slot, "Name", bubble.Name ?? "", 14, TextAnchor.UpperCenter,
                wide ? new Vector2(0.02f, 0.02f) : new Vector2(0.05f, 0.42f),
                wide ? new Vector2(0.28f, 0.16f) : new Vector2(0.95f, 0.56f),
                BattleUITheme.TextPrimary);

            var bubbleMin = wide ? new Vector2(0.32f, 0.12f) : new Vector2(0.04f, 0.02f);
            var bubbleMax = wide ? new Vector2(0.98f, 0.9f) : new Vector2(0.96f, 0.4f);
            var plate = CreateRect(slot, "BubbleFrame", bubbleMin, bubbleMax);
            ApplyPart(plate.gameObject.AddComponent<Image>(), chrome != null ? chrome.bubbleFrame : null, new Color(0.96f, 0.93f, 0.86f, 0.96f));
            var tail = CreateRect(plate, "BubbleTail", new Vector2(0.4f, -0.1f), new Vector2(0.58f, 0.04f));
            ApplyPart(tail.gameObject.AddComponent<Image>(), chrome != null ? chrome.bubbleTail : null, new Color(0.96f, 0.93f, 0.86f, 0.96f));
            CreateLabel(plate, "Line", bubble.Line, wide ? 24 : 18, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f), new Color(0.12f, 0.08f, 0.06f, 1f));
        }

        RectTransform CreateOpinionButton(Transform parent, OpinionButtonView button, TypingRoguelike.Data.OpinionChrome chrome, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var rt = CreateRect(parent, "ChoiceButton_" + button.ChoiceIndex, anchorMin, anchorMax);
            var image = rt.gameObject.AddComponent<Image>();
            var sprite = button.Emphasized
                ? chrome?.choiceButtonAgreed
                : button.Dimmed ? chrome?.choiceButtonDim : chrome?.choiceButton;
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
                var chip = CreateRect(rt, "Face", new Vector2(left, 0.06f), new Vector2(left + 0.1f, 0.28f));
                ApplyPart(chip.gameObject.AddComponent<Image>(), faceView.Portrait, FaceColor(faceView.CharacterId));
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

        static Color FaceColor(string characterId) => characterId switch
        {
            "momo" => new Color(0.42f, 0.7f, 0.52f, 1f),
            "nagi" => new Color(0.42f, 0.46f, 0.82f, 1f),
            _ => new Color(0.78f, 0.32f, 0.28f, 1f)
        };

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

        void BuildTrustRow(Transform parent, TrustGaugeView[] gauges, float yMin, float yMax)
        {
            if (parent == null || gauges == null || gauges.Length == 0)
                return;
            var slot = 0.92f / gauges.Length;
            for (var i = 0; i < gauges.Length; i++)
            {
                var gauge = gauges[i];
                var xMin = 0.04f + i * slot;
                var xMax = xMin + slot - 0.02f;
                CreateLabel(parent, "TrustName" + i, $"{gauge.Name}（{gauge.Personality}） {gauge.Trust}",
                    16, TextAnchor.UpperLeft, new Vector2(xMin, yMax - 0.08f), new Vector2(xMax, yMax), BattleUITheme.TextPrimary);
                var track = CreateRect(parent, "TrustTrack" + i, new Vector2(xMin, yMin + 0.04f), new Vector2(xMax, yMin + 0.09f));
                var trackImage = track.gameObject.AddComponent<Image>();
                trackImage.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
                BattleUITheme.EnsureSprite(trackImage);
                var fill = CreateRect(track, "Fill", Vector2.zero, Vector2.one);
                var fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = new Color(0.93f, 0.62f, 0.28f, 1f);
                BattleUITheme.EnsureSprite(fillImage);
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillAmount = Mathf.Clamp01(gauge.Trust / 100f);
                fillImage.raycastTarget = false;
                CreateLabel(parent, "TrustPassive" + i, gauge.PassiveLabel ?? "未発動",
                    14, TextAnchor.UpperLeft, new Vector2(xMin, yMin), new Vector2(xMax, yMin + 0.04f), BattleUITheme.TextMuted);
            }
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
            if (_trustDebug != null)
                CreateButton(parent, "信頼", new Vector2(0.54f, 0.76f), new Vector2(0.7f, 0.84f), _trustDebug);
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
