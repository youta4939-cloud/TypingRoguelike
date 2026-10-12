using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    public sealed class RunOverlayUI
    {
        Font _font;
        RectTransform _root;
        GameObject _hubPanel;
        GameObject _rewardPanel;
        GameObject _loadoutPanel;
        GameObject _replacePanel;
        GameObject _selectionPanel;
        GameObject _deathNoticePanel;
        Action<SkillDefinition> _onRewardPicked;
        Action _onLoadoutClosed;
        RunPartyState _party;
        SkillDefinition _selectedInventoryCard;
        Text _loadoutTitle;
        Text _loadoutStatus;

        Font UiFont => _font ??= BattleUITheme.Font;

        public void EnsureRoot(Transform parent)
        {
            if (_root != null)
                return;

            var go = new GameObject("RunOverlayCanvas");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = go.GetComponent<RectTransform>();

            EnsureGlobalEventSystem();
        }

        public static void EnsureGlobalEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var uiModule = es.AddComponent<InputSystemUIInputModule>();
            try
            {
                uiModule.AssignDefaultActions();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UI Input の初期化をスキップしました: {ex.Message}");
            }
        }

        public void ShowHub(
            Transform host,
            MetaProgressData profile,
            SkillCardPoolDefinition pool,
            Action onStartRun,
            Action onResumeRun = null,
            string resumeRunLabel = null,
            IReadOnlyList<int> elevatorFloors = null,
            Action<int> onStartFromElevatorFloor = null,
            Action onOpenRelicHub = null,
            Action onOpenHubShop = null,
            Action onOpenPartyStats = null)
        {
            EnsureRoot(host);
            HideAll();
            _hubPanel = CreateFullScreenPanel("ArcaneSanctum", new Color(0.004f, 0.007f, 0.022f, 1f));
            _hubPanel.AddComponent<CanvasGroup>();
            _hubPanel.AddComponent<RunPanelEntrance>();
            BuildOverlayBackdrop(_hubPanel.transform, BattleUITheme.Arcane);

            var window = BattleUIChrome.CreateLuxuryWindow(_hubPanel.transform, "SanctumWindow",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), "ARCANE SANCTUM", BattleUITheme.Gold);
            var body = window.Find("Body") ?? window;
            var title = CreateLabel(body, "Title", "刻印の聖堂", 38, TextAnchor.MiddleCenter,
                new Vector2(0.2f, 0.84f), new Vector2(0.8f, 0.98f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(title, new Color(0, 0, 0, 0.95f), new Vector2(3, -3));
            CreateLabel(body, "Currency", $"◆ 魔石  {profile?.arcaneShards ?? 0}　　貯金 {profile?.bankedCoins ?? 0}", 21, TextAnchor.MiddleCenter,
                new Vector2(0.35f, 0.76f), new Vector2(0.65f, 0.84f), Vector2.zero, Vector2.zero,
                BattleUITheme.Gold);
            var whMat = MetaProgression.WarehouseMaterialUnits(profile);
            var whRel = profile?.warehouseRelicIds?.Count ?? 0;
            CreateLabel(body, "Warehouse", $"素材倉庫 {whMat}　レリック倉庫 {whRel} 個", 16,
                TextAnchor.MiddleCenter, new Vector2(0.28f, 0.7f), new Vector2(0.72f, 0.76f),
                Vector2.zero, Vector2.zero, BattleUITheme.Cyan);
            var hubBtnY = 0.64f;
            if (onOpenRelicHub != null)
            {
                CreateButton(body, "レリック装備・倉庫",
                    new Vector2(0.5f, hubBtnY), new Vector2(0.5f, hubBtnY), Vector2.zero, new Vector2(340, 52),
                    onOpenRelicHub);
                hubBtnY -= 0.08f;
            }

            if (onOpenHubShop != null)
            {
                CreateButton(body, "店・レリック開発",
                    new Vector2(0.5f, hubBtnY), new Vector2(0.5f, hubBtnY), Vector2.zero, new Vector2(340, 52),
                    onOpenHubShop);
                hubBtnY -= 0.08f;
            }

            if (onOpenPartyStats != null)
            {
                CreateButton(body, "パーティステータス",
                    new Vector2(0.5f, hubBtnY), new Vector2(0.5f, hubBtnY), Vector2.zero, new Vector2(340, 52),
                    onOpenPartyStats);
            }

            var record = CreateSubPanel(body, "Record", new Vector2(0.05f, 0.18f), new Vector2(0.34f, 0.74f));
            CreateLabel(record, "Header", "RUN RECORD", 15, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero,
                BattleUITheme.AccentAlly);
            var recordText =
                $"挑戦回数　{profile?.totalRuns ?? 0}\n\n" +
                $"踏破 / 帰還　{profile?.completedRuns ?? 0} / {profile?.extractedRuns ?? 0}\n\n" +
                $"最高進行　{profile?.bestClearedNodes ?? 0} ノード";
            CreateLabel(record, "Stats", recordText, 18, TextAnchor.UpperLeft,
                new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.78f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);

            var archive = CreateSubPanel(body, "Archive", new Vector2(0.66f, 0.18f), new Vector2(0.95f, 0.74f));
            var totalCards = pool?.cards?.Count(card => card != null) ?? 0;
            var unlockedCards = pool?.cards?.Count(card => card != null && profile != null &&
                profile.IsUnlocked(card.skillId)) ?? 0;
            var lockedCards = MetaProgression.CountUnlockableSkills(profile, pool);
            CreateLabel(archive, "Header", "SIGIL ARCHIVE", 15, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero,
                BattleUITheme.Cyan);
            CreateLabel(archive, "Progress", $"解放済み  {unlockedCards} / {totalCards}", 20, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);

            var lastUnlocked = pool?.cards?.FirstOrDefault(card =>
                card != null && profile != null && card.skillId == profile.lastUnlockedSkillId);
            var elevatorLine = profile?.unlockedElevatorFloors != null && profile.unlockedElevatorFloors.Count > 0
                ? $"解放エレベーター階: {string.Join(", ", profile.unlockedElevatorFloors)}F"
                : "エレベーター未解放";
            CreateLabel(archive, "Latest", elevatorLine, 14, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.6f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextMuted);

            var unlockLabel = lockedCards > 0 ? "図鑑（旧プロト）" : "図鑑（旧プロト）";
            var unlockButton = CreateButton(archive, unlockLabel,
                new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), Vector2.zero, new Vector2(310, 58),
                null);
            unlockButton.interactable = false;

            CreateLabel(body, "StartRule",
                profile != null && profile.towerRunActive
                    ? "中断中の探索は、刻印・レリックを持ったまま 1 階か解放済みエレベーター階から再開できる"
                    : "出発時は各キャラの初期スキルのみ。カードは塔内で集める",
                16,
                TextAnchor.MiddleCenter, new Vector2(0.35f, 0.47f), new Vector2(0.65f, 0.54f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var hasElevatorRestart = elevatorFloors != null && elevatorFloors.Count > 0 &&
                                     onStartFromElevatorFloor != null;
            CreateLabel(body, "RunCopy",
                profile != null && profile.towerRunActive
                    ? "中断中の探索がある。拠点で整備してから塔へ戻れる。"
                    : hasElevatorRestart
                        ? "塔は再配置された。解放済みエレベーターから、新しい RUN を始められる。"
                        : "1 階から新しく塔へ入る。",
                15, TextAnchor.MiddleCenter,
                new Vector2(0.24f, 0.56f), new Vector2(0.76f, 0.68f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextMuted);

            var hasResume = onResumeRun != null && profile != null && profile.towerRunActive &&
                            !string.IsNullOrEmpty(resumeRunLabel);
            var anchorY = 0.42f;
            if (hasResume)
            {
                CreateButton(body, resumeRunLabel,
                    new Vector2(0.5f, anchorY), new Vector2(0.5f, anchorY), Vector2.zero, new Vector2(420, 72),
                    onResumeRun);
                anchorY -= 0.1f;
                CreateButton(body, "1 階から再挑戦（踏破記録を保持）",
                    new Vector2(0.5f, anchorY), new Vector2(0.5f, anchorY), Vector2.zero, new Vector2(420, 64),
                    onStartRun);
                anchorY -= 0.09f;
            }
            else
            {
                CreateButton(body, "ダンジョンへ出発（1 階）",
                    new Vector2(0.5f, anchorY), new Vector2(0.5f, anchorY), Vector2.zero, new Vector2(390, 72),
                    onStartRun);
                anchorY -= 0.1f;
            }

            if (hasElevatorRestart)
            {
                foreach (var floor in elevatorFloors.OrderBy(f => f))
                {
                    var f = floor;
                    var elevatorLabel = profile != null && profile.towerRunActive
                        ? $"エレベーター（{f} 階）から続ける"
                        : $"エレベーター（{f} 階）から再入";
                    CreateButton(body, elevatorLabel,
                        new Vector2(0.5f, anchorY), new Vector2(0.5f, anchorY), Vector2.zero, new Vector2(420, 58),
                        () => onStartFromElevatorFloor(f));
                    anchorY -= 0.075f;
                }
            }

            if (profile != null && profile.lastRunReward > 0)
            {
                CreateLabel(body, "LastReward", $"前回の獲得魔石  +{profile.lastRunReward}", 15,
                    TextAnchor.MiddleCenter, new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.32f),
                    Vector2.zero, Vector2.zero, BattleUITheme.Gold);
            }
        }

        public void ShowCardReward(
            Transform host,
            RunPartyState party,
            IReadOnlyList<SkillDefinition> choices,
            Action<SkillDefinition> onPicked,
            Action onDecline = null)
        {
            EnsureRoot(host);
            HideAll();
            _onRewardPicked = onPicked;
            _rewardPanel = CreateFullScreenPanel("CardReward", new Color(0.005f, 0.008f, 0.025f, 0.96f));
            _rewardPanel.AddComponent<CanvasGroup>();
            _rewardPanel.AddComponent<RunPanelEntrance>();
            BuildOverlayBackdrop(_rewardPanel.transform, BattleUITheme.Arcane);
            var window = BattleUIChrome.CreateLuxuryWindow(_rewardPanel.transform, "RewardWindow",
                new Vector2(0.055f, 0.08f), new Vector2(0.945f, 0.92f), "ARCANE REWARD", BattleUITheme.Gold);
            var body = window.Find("Body") ?? window;
            var title = CreateLabel(body, "Title", "新たな刻印をひとつ選べ", 30, TextAnchor.UpperCenter,
                new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.98f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(title, new Color(0, 0, 0, 0.95f), new Vector2(3, -3));
            CreateLabel(body, "Sub", "空き枠へ自動装備。同じ刻印は Lv.3 まで強化。満杯なら入れ替え（初期スキルは残る）", 14, TextAnchor.UpperCenter,
                new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.87f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var partySummary = CreateSubPanel(body, "CurrentLoadout", new Vector2(0.025f, 0.05f), new Vector2(0.31f, 0.76f));
            BuildRewardPartySummary(partySummary, party);

            var row = new GameObject("Choices");
            row.transform.SetParent(body, false);
            var rt = row.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.33f, 0.05f);
            rt.anchorMax = new Vector2(0.975f, 0.76f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 34;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            foreach (var card in choices)
            {
                if (card == null)
                    continue;
                CreateCardButton(row.transform, card, party, () =>
                {
                    var callback = _onRewardPicked;
                    HideAll();
                    callback?.Invoke(card);
                });
            }

            if (onDecline != null)
            {
                CreateButton(_rewardPanel.transform, "見送る",
                    new Vector2(0.38f, 0.08f), new Vector2(0.38f, 0.08f), Vector2.zero, new Vector2(210, 46),
                    () =>
                    {
                        HideAll();
                        onDecline.Invoke();
                    });
            }
        }

        void BuildRewardPartySummary(RectTransform root, RunPartyState party)
        {
            CreateLabel(root, "Header", "CURRENT LOADOUT", 14, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.91f), new Vector2(0.97f, 0.99f), Vector2.zero, Vector2.zero,
                BattleUITheme.Gold);

            if (party?.Members == null || party.Members.Count == 0)
            {
                CreateLabel(root, "Empty", "編成情報なし", 15, TextAnchor.MiddleCenter,
                    new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero,
                    BattleUITheme.TextMuted);
                return;
            }

            var memberHeight = 0.86f / party.Members.Count;
            for (var i = 0; i < party.Members.Count; i++)
            {
                var member = party.Members[i];
                var yMax = 0.89f - i * memberHeight;
                var yMin = yMax - memberHeight + 0.015f;
                var card = CreateSubPanel(root, $"RewardMember_{i}",
                    new Vector2(0.025f, yMin), new Vector2(0.975f, yMax));
                var cardImage = card.GetComponent<Image>();
                BattleUIImage.ApplyPanel(cardImage, new Color(0.025f, 0.035f, 0.075f, 0.96f),
                    BattleUITemplateLoader.Default?.partyCard);

                var portraitFrame = CreateSubPanel(card, "Portrait", new Vector2(0.025f, 0.15f), new Vector2(0.2f, 0.85f));
                var portraitImage = portraitFrame.GetComponent<Image>();
                var portraitSprite = BattlePortraitResolver.ResolveCharacterPortrait(member.character);
                if (portraitSprite != null)
                {
                    portraitImage.sprite = portraitSprite;
                    portraitImage.type = Image.Type.Simple;
                    portraitImage.color = Color.white;
                }
                else
                {
                    portraitImage.color = new Color(0.2f, 0.22f, 0.3f, 1f);
                }
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;

                var charName = member.character != null ? member.character.characterName : "?";
                var name = CreateLabel(card, "Name", charName, 15, TextAnchor.MiddleLeft,
                    new Vector2(0.23f, 0.77f), new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero,
                    BattleUITheme.TextPrimary);
                name.fontStyle = FontStyle.Bold;

                CreateRewardSkillRow(card, "Innate", member.innateSlot, true, 1, 0.53f, 0.75f);
                CreateRewardSkillRow(card, "Slot1", member.GetSlot(0), false, member.GetSlotLevel(0), 0.28f, 0.5f);
                CreateRewardSkillRow(card, "Slot2", member.GetSlot(1), false, member.GetSlotLevel(1), 0.03f, 0.25f);
            }
        }

        void CreateRewardSkillRow(
            Transform parent,
            string name,
            SkillDefinition skill,
            bool innate,
            int level,
            float yMin,
            float yMax)
        {
            var color = innate
                ? new Color(BattleUITheme.InnateRow.r, BattleUITheme.InnateRow.g, BattleUITheme.InnateRow.b, 0.9f)
                : new Color(BattleUITheme.CardRow.r, BattleUITheme.CardRow.g, BattleUITheme.CardRow.b, 0.9f);
            var row = CreateSubPanel(parent, name, new Vector2(0.23f, yMin), new Vector2(0.97f, yMax));
            row.GetComponent<Image>().color = color;

            var prefix = innate ? "★ " : "◆ ";
            var displayName = skill != null ? SkillProgression.FormatName(skill, innate ? 1 : level) : "空き";
            var effect = skill != null ? SkillProgression.Describe(skill, innate ? 1 : level) : string.Empty;
            var label = CreateLabel(row, "Label", $"{prefix}{displayName}\n<size=10>{effect}</size>", 11, TextAnchor.MiddleLeft,
                Vector2.zero, new Vector2(0.58f, 1f), new Vector2(7, 0), Vector2.zero,
                skill != null ? BattleUITheme.TextPrimary : BattleUITheme.TextMuted);
            label.supportRichText = true;

            if (skill?.pattern == null || skill.pattern.Length == 0)
                return;

            var patternHost = BattleUIOrbPattern.CreateHost(row, new Vector2(0.58f, 0.12f), new Vector2(0.98f, 0.88f));
            BattleUIOrbPattern.Apply(patternHost, skill.pattern, 11f);
        }

        public void ShowBackpackReplacement(
            Transform host,
            RunPartyState party,
            SkillDefinition newCard,
            Action<SkillDefinition> onDiscardExisting,
            Action onDeclineNewCard)
        {
            ShowSkillSelection(
                host,
                "BACKPACK FULL",
                $"バックパック 6 / 6　{newCard.displayName} と入れ替えるカードを選択",
                party?.Inventory,
                -1,
                onDiscardExisting,
                onDeclineNewCard,
                "新しいカードを諦める");
        }

        void ShowSkillSelection(
            Transform host,
            string header,
            string subtitle,
            IReadOnlyList<SkillDefinition> cards,
            int selectedSkillId,
            Action<SkillDefinition> onPicked,
            Action onBack,
            string backLabel = "拠点へ戻る")
        {
            EnsureRoot(host);
            HideAll();
            _selectionPanel = CreateFullScreenPanel("SkillSelection", new Color(0.004f, 0.007f, 0.022f, 0.98f));
            _selectionPanel.AddComponent<CanvasGroup>();
            _selectionPanel.AddComponent<RunPanelEntrance>();
            BuildOverlayBackdrop(_selectionPanel.transform, BattleUITheme.Arcane);

            var window = BattleUIChrome.CreateLuxuryWindow(_selectionPanel.transform, "SelectionWindow",
                new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.92f), header, BattleUITheme.Gold);
            var body = window.Find("Body") ?? window;
            var title = CreateLabel(body, "Title", subtitle, 24, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.98f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;

            var gridGo = new GameObject("SkillGrid");
            gridGo.transform.SetParent(body, false);
            var gridRt = gridGo.AddComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.035f, 0.12f);
            gridRt.anchorMax = new Vector2(0.965f, 0.84f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.cellSize = new Vector2(320, 80);
            grid.spacing = new Vector2(14, 10);
            grid.childAlignment = TextAnchor.UpperCenter;

            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card != null)
                        CreateCompactSkillButton(gridRt, card, card.skillId == selectedSkillId, onPicked);
                }
            }

            if (onBack != null)
            {
                CreateButton(_selectionPanel.transform, backLabel,
                    new Vector2(0.62f, 0.08f), new Vector2(0.62f, 0.08f), Vector2.zero, new Vector2(210, 46),
                    onBack);
            }
        }

        void CreateCompactSkillButton(
            Transform parent,
            SkillDefinition card,
            bool selected,
            Action<SkillDefinition> onPicked)
        {
            var go = new GameObject($"Archive_{card.skillId}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            BattleUIImage.ApplyPanel(image,
                selected ? new Color(0.22f, 0.16f, 0.04f, 0.98f) : new Color(0.025f, 0.04f, 0.085f, 0.96f),
                BattleUITemplateLoader.Default?.partyCard);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = selected ? BattleUITheme.Gold : CardAccent(card);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onPicked?.Invoke(card));

            var label = CreateLabel(rt, "Name", $"{card.displayName}\n<size=10>{card.effectText}</size>", 14,
                TextAnchor.MiddleLeft, new Vector2(0.04f, 0.08f), new Vector2(0.67f, 0.92f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            label.supportRichText = true;
            label.fontStyle = FontStyle.Bold;
            if (selected)
                CreateLabel(rt, "Equipped", "EQUIPPED", 9, TextAnchor.UpperRight,
                    new Vector2(0.55f, 0.7f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero,
                    BattleUITheme.Gold);

            if (card.pattern != null && card.pattern.Length > 0)
            {
                var patternHost = BattleUIOrbPattern.CreateHost(rt, new Vector2(0.68f, 0.16f), new Vector2(0.97f, 0.68f));
                BattleUIOrbPattern.Apply(patternHost, card.pattern, 10f);
            }
        }

        /// <summary>探索中のパーティ確認（閲覧のみ。カード入れ替えは獲得時のみ）。</summary>
        public void ShowLoadout(Transform host, RunPartyState party, Action onClosed)
        {
            EnsureRoot(host);
            HideAll();
            _party = party;
            _onLoadoutClosed = onClosed;
            _selectedInventoryCard = null;

            _loadoutPanel = CreateFullScreenPanel("Loadout", new Color(0.006f, 0.008f, 0.024f, 0.98f));
            _loadoutPanel.AddComponent<CanvasGroup>();
            _loadoutPanel.AddComponent<RunPanelEntrance>();
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.Arcane);
            _loadoutTitle = CreateLabel(_loadoutPanel.transform, "Title", string.Empty, 26, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.97f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);
            _loadoutTitle.fontStyle = FontStyle.Bold;
            _loadoutStatus = CreateLabel(_loadoutPanel.transform, "Status", string.Empty, 16, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.915f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var body = new GameObject("Body");
            body.transform.SetParent(_loadoutPanel.transform, false);
            var bodyRt = body.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.05f, 0.12f);
            bodyRt.anchorMax = new Vector2(0.95f, 0.88f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;

            var left = CreateSubPanel(body.transform, "Members", new Vector2(0, 0), new Vector2(0.55f, 1));
            var right = CreateSubPanel(body.transform, "Inventory", new Vector2(0.57f, 0), new Vector2(1, 1));

            SetLoadoutStatus("カードの入れ替えは宝箱・商人で獲得したときのみ可能");
            RefreshLoadoutPanels(left.transform, right.transform);

            var closeBtn = CreateButton(_loadoutPanel.transform, "探索に戻る", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 24f), new Vector2(320, 48), () =>
                {
                    HideAll();
                    _onLoadoutClosed?.Invoke();
                });
        }

        void RefreshLoadoutPanels(Transform membersRoot, Transform inventoryRoot)
        {
            ClearChildren(membersRoot);
            ClearChildren(inventoryRoot);

            if (_loadoutTitle != null)
                _loadoutTitle.text = $"パーティ確認　　追加スキル {_party.EquippedSkillCardCount} 枚";

            CreateLabel(inventoryRoot, "InvTitle", "INVENTORY", 16, TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.99f), Vector2.zero, Vector2.zero, BattleUITheme.Cyan);

            CreateLabel(inventoryRoot, "InvNote",
                "スキルカードのバックパックはありません。\nレリック・素材は塔内バックパック（マップの「レリック」）。",
                14, TextAnchor.UpperLeft, new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.88f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var memberCount = Mathf.Max(1, _party.Members.Count);
            var height = 1f / memberCount;
            for (var m = 0; m < _party.Members.Count; m++)
            {
                var member = _party.Members[m];
                var name = member.character != null ? member.character.characterName : "?";
                var yMax = 1f - m * height;
                var block = CreateSubPanel(membersRoot, $"Member_{m}",
                    new Vector2(0.02f, yMax - height + 0.012f), new Vector2(0.98f, yMax - 0.012f));
                BattleUIImage.ApplyPanel(block.GetComponent<Image>(), new Color(0.025f, 0.035f, 0.08f, 0.96f),
                    BattleUITemplateLoader.Default?.partyCard);

                var portrait = CreateSubPanel(block, "Portrait", new Vector2(0.015f, 0.08f), new Vector2(0.16f, 0.92f));
                var portraitImage = portrait.GetComponent<Image>();
                var portraitSprite = BattlePortraitResolver.ResolveCharacterPortrait(member.character);
                if (portraitSprite != null)
                {
                    portraitImage.sprite = portraitSprite;
                    portraitImage.type = Image.Type.Simple;
                    portraitImage.color = Color.white;
                    portraitImage.preserveAspect = true;
                }

                var nameLabel = CreateLabel(block, "Name", name, 17, TextAnchor.MiddleLeft,
                    new Vector2(0.18f, 0.76f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero,
                    BattleUITheme.TextPrimary);
                nameLabel.fontStyle = FontStyle.Bold;

                CreateSlotDisplay(block, member, MemberRunState.InnateSlotIndex, 0.52f);
                CreateSlotDisplay(block, member, 0, 0.28f);
                CreateSlotDisplay(block, member, 1, 0.04f);
            }
        }

        void CreateBackpackRow(Transform parent, SkillDefinition card)
        {
            var go = new GameObject(card != null ? $"Pack_{card.skillId}" : "Pack_Empty");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 58;
            var img = go.AddComponent<Image>();
            var selected = card != null && card == _selectedInventoryCard;
            BattleUIImage.ApplyPanel(img,
                selected ? new Color(0.22f, 0.16f, 0.04f, 0.98f) : new Color(0.03f, 0.045f, 0.09f, 0.95f),
                BattleUITemplateLoader.Default?.partyCard);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = selected ? BattleUITheme.Gold
                : card != null ? CardAccent(card) : new Color(0.3f, 0.34f, 0.45f, 0.35f);
            outline.effectDistance = selected ? new Vector2(2.5f, -2.5f) : new Vector2(1.5f, -1.5f);

            if (card == null)
            {
                CreateLabel(go.transform, "Empty", "― 空き ―", 13, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
                return;
            }

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                _selectedInventoryCard = selected ? null : card;
                SetLoadoutStatus(_selectedInventoryCard != null
                    ? $"{card.displayName} を選択中 — 入れ替える装備枠をクリック"
                    : "選択を解除しました");
                RefreshLoadout();
            });

            var label = CreateLabel(go.transform, "Name",
                $"{card.displayName}\n<size=10>{card.characterName}　{card.effectText}</size>", 14,
                TextAnchor.MiddleLeft, new Vector2(0.03f, 0.05f), new Vector2(0.64f, 0.95f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            label.supportRichText = true;
            label.fontStyle = FontStyle.Bold;
            if (card.pattern is { Length: > 0 })
            {
                var patternHost = BattleUIOrbPattern.CreateHost(go.transform, new Vector2(0.64f, 0.18f), new Vector2(0.97f, 0.82f));
                BattleUIOrbPattern.Apply(patternHost, card.pattern, 12f);
            }
        }

        void CreateSlotDisplay(Transform parent, MemberRunState member, int slotIndex, float anchorY)
        {
            var skill = member.GetSlot(slotIndex);
            var isInnateSlot = slotIndex == MemberRunState.InnateSlotIndex;
            var slotName = isInnateSlot ? "★ 初期" : $"◆ カード{slotIndex + 1}";
            var panel = CreateSubPanel(parent, $"Slot_{slotIndex}", new Vector2(0.18f, anchorY), new Vector2(0.98f, anchorY + 0.21f));
            var img = panel.GetComponent<Image>();
            img.color = isInnateSlot ? BattleUITheme.InnateRow : BattleUITheme.CardRow;

            var label = skill != null ? $"{slotName}　{skill.displayName}" : $"{slotName}　（空き）";
            CreateLabel(panel, "Text", label, 13, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0), new Vector2(0.62f, 1), new Vector2(8, 0), Vector2.zero,
                skill != null ? BattleUITheme.TextPrimary : BattleUITheme.TextMuted);
            if (skill?.pattern is { Length: > 0 })
            {
                var patternHost = BattleUIOrbPattern.CreateHost(panel, new Vector2(0.62f, 0.14f), new Vector2(0.98f, 0.86f));
                BattleUIOrbPattern.Apply(patternHost, skill.pattern, 11f);
            }
        }

        void RefreshLoadout()
        {
            if (_loadoutPanel == null)
                return;
            var left = _loadoutPanel.transform.Find("Body/Members");
            var right = _loadoutPanel.transform.Find("Body/Inventory");
            if (left != null && right != null)
                RefreshLoadoutPanels(left, right);
        }

        void SetLoadoutStatus(string message)
        {
            if (_loadoutStatus != null)
                _loadoutStatus.text = message;
        }

        public void ShowReplaceSlot(
            Transform host,
            RunPartyState party,
            int memberIndex,
            SkillDefinition newCard,
            Action onDone,
            Action onDecline = null,
            bool cardSlotsOnly = false)
        {
            EnsureRoot(host);
            if (_replacePanel != null)
                UnityEngine.Object.Destroy(_replacePanel);

            var member = party.Members[memberIndex];
            var charName = member.character != null ? member.character.characterName : "?";
            _replacePanel = CreateFullScreenPanel("ReplaceSlot", new Color(0.005f, 0.008f, 0.025f, 0.96f));
            _replacePanel.AddComponent<CanvasGroup>();
            _replacePanel.AddComponent<RunPanelEntrance>();
            BuildOverlayBackdrop(_replacePanel.transform, CardAccent(newCard));
            var window = BattleUIChrome.CreateLuxuryWindow(_replacePanel.transform, "ReplaceWindow",
                new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.84f), "REWRITE THE SIGIL", BattleUITheme.Gold);
            var windowBody = window.Find("Body") ?? window;
            var title = CreateLabel(windowBody, "Title", $"{charName} のスロットを再刻印", 27, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;
            CreateLabel(windowBody, "Sub", $"新しい刻印　{newCard.displayName}", 16, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero, CardAccent(newCard));
            CreateLabel(windowBody, "NewEffect", SkillProgression.Describe(newCard, 1), 15, TextAnchor.UpperCenter,
                new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.74f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);

            var body = CreateSubPanel(windowBody, "Body", new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.64f));
            var v = body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.padding = new RectOffset(12, 12, 12, 12);
            v.childControlHeight = true;
            v.childForceExpandHeight = false;

            if (!cardSlotsOnly)
            {
                CreateReplaceRow(body, "★ 初期", member.innateSlot, 1, () =>
                {
                    party.ReplaceEquippedSlot(memberIndex, MemberRunState.InnateSlotIndex, newCard);
                    HideAll();
                    onDone?.Invoke();
                });
            }

            CreateReplaceRow(body, "◆ カード1", member.GetSlot(0), member.GetSlotLevel(0), () =>
            {
                party.ReplaceEquippedSlot(memberIndex, 0, newCard);
                HideAll();
                onDone?.Invoke();
            });
            CreateReplaceRow(body, "◆ カード2", member.GetSlot(1), member.GetSlotLevel(1), () =>
            {
                party.ReplaceEquippedSlot(memberIndex, 1, newCard);
                HideAll();
                onDone?.Invoke();
            });

            if (onDecline != null)
            {
                CreateButton(windowBody, "見送る（取得しない）",
                    new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(280, 48),
                    () =>
                    {
                        HideAll();
                        onDecline.Invoke();
                    });
            }
        }

        public void ShowRunRelicManage(
            Transform host,
            RunExplorationInventory loot,
            RelicCatalogDefinition catalog,
            Action onClosed)
        {
            EnsureRoot(host);
            HideAll();
            _loadoutPanel = CreateFullScreenPanel("RunRelics", new Color(0.006f, 0.008f, 0.024f, 0.98f));
            _loadoutPanel.AddComponent<CanvasGroup>();
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.Arcane);
            CreateLabel(_loadoutPanel.transform, "Title", "レリック（塔内）", 26, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.96f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f));
            BuildRunRelicPanel(body, loot, catalog);

            CreateButton(_loadoutPanel.transform, "マップに戻る",
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(280, 48),
                () =>
                {
                    HideAll();
                    onClosed?.Invoke();
                });
        }

        public void ShowHubShopDevelopPanel(
            Transform host,
            MetaProgressData profile,
            RelicCatalogDefinition catalog,
            Action onClosed)
        {
            EnsureRoot(host);
            HideAll();
            _loadoutPanel = CreateFullScreenPanel("HubShop", new Color(0.006f, 0.008f, 0.024f, 0.98f));
            _loadoutPanel.AddComponent<CanvasGroup>();
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.Cyan);
            CreateLabel(_loadoutPanel.transform, "Title", "拠点 — 店・レリック開発", 24,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                Vector2.zero, Vector2.zero, BattleUITheme.Cyan);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f));
            BuildHubShopDevelopPanel(body, profile, catalog);

            CreateButton(_loadoutPanel.transform, "聖堂に戻る",
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(280, 48),
                () =>
                {
                    MetaProgression.Save(profile);
                    HideAll();
                    onClosed?.Invoke();
                });
        }

        public void ShowPartyStats(
            Transform host,
            RunPartyState party,
            DungeonRunDefinition runConfig,
            RelicCatalogDefinition relicCatalog,
            RunExplorationInventory exploration,
            string closeButtonLabel,
            Action onClosed)
        {
            EnsureRoot(host);
            HideAll();
            var stats = PartyCombatStats.Compute(party, runConfig);
            var relicMods = exploration != null
                ? RelicRunModifiers.Compute(exploration, relicCatalog)
                : default;

            _loadoutPanel = CreateFullScreenPanel("PartyStats", new Color(0.006f, 0.008f, 0.024f, 0.98f));
            _loadoutPanel.AddComponent<CanvasGroup>();
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.AccentAlly);
            CreateLabel(_loadoutPanel.transform, "Title", "パーティステータス", 26, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);

            var bodyText = stats.BuildDetailText(party, runConfig, relicMods);
            CreateLabel(_loadoutPanel.transform, "Body", bodyText, 17, TextAnchor.UpperLeft,
                new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.86f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);

            CreateButton(_loadoutPanel.transform, closeButtonLabel,
                new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.06f), Vector2.zero, new Vector2(300, 48),
                () =>
                {
                    HideAll();
                    onClosed?.Invoke();
                });
        }

        public void ShowHubRelicPanel(
            Transform host,
            MetaProgressData profile,
            RelicCatalogDefinition catalog,
            Action onClosed)
        {
            EnsureRoot(host);
            HideAll();
            _loadoutPanel = CreateFullScreenPanel("HubRelics", new Color(0.006f, 0.008f, 0.024f, 0.98f));
            _loadoutPanel.AddComponent<CanvasGroup>();
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.Gold);
            CreateLabel(_loadoutPanel.transform, "Title", "拠点 — レリック装備（次の出発時）", 24,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                Vector2.zero, Vector2.zero, BattleUITheme.Gold);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f));
            BuildHubRelicPanel(body, profile, catalog);

            CreateButton(_loadoutPanel.transform, "聖堂に戻る",
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(280, 48),
                () =>
                {
                    MetaProgression.Save(profile);
                    HideAll();
                    onClosed?.Invoke();
                });
        }

        void BuildRunRelicPanel(
            RectTransform body,
            RunExplorationInventory loot,
            RelicCatalogDefinition catalog,
            int confirmDiscardSlot = -1,
            string notice = null)
        {
            ClearChildren(body);
            var hint = confirmDiscardSlot >= 0
                ? "もう一度タップすると外れる。外したレリックはこの探索では失われる。"
                : "装備は3つまで。枠をタップすると外せる。";
            if (!string.IsNullOrEmpty(notice))
                hint = notice;
            CreateLabel(body, "Hint", hint, 14,
                TextAnchor.UpperLeft, new Vector2(0.04f, 0.9f), new Vector2(0.96f, 0.98f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var exclusive = RelicRunModifiers.Compute(loot, catalog).ExclusiveEquipped;
            for (var slot = 0; slot < RunExplorationInventory.RelicSlotCount; slot++)
            {
                var s = slot;
                var id = loot.GetEquippedRelicId(s);
                var def = catalog?.FindById(id);
                var label = def != null
                    ? $"枠{s + 1}: {def.displayName}\n{def.effectText}"
                    : $"枠{s + 1}: （空）";
                if (confirmDiscardSlot == s && def != null)
                    label = $"枠{s + 1}: {def.displayName} を破棄する\n{def.effectText}";
                CreateButton(body, label,
                    new Vector2(0.5f, 0.76f - s * 0.14f), new Vector2(0.5f, 0.76f - s * 0.14f),
                    Vector2.zero, new Vector2(640, 64),
                    () =>
                    {
                        if (def == null)
                            return;
                        if (confirmDiscardSlot != s)
                        {
                            BuildRunRelicPanel(body, loot, catalog, s);
                            return;
                        }

                        loot.DiscardEquippedRelic(s);
                        BuildRunRelicPanel(body, loot, catalog);
                    });
            }

            var y = 0.38f;
            CreateLabel(body, "BpHeader", "バックパック", 15, TextAnchor.UpperLeft,
                new Vector2(0.04f, y), new Vector2(0.5f, y + 0.06f), Vector2.zero, Vector2.zero, BattleUITheme.Cyan);
            y -= 0.08f;
            for (var i = 0; i < loot.BackpackRelicIds.Count; i++)
            {
                var index = i;
                var def = catalog?.FindById(loot.BackpackRelicIds[index]);
                var name = def?.displayName ?? $"#{loot.BackpackRelicIds[index]}";
                CreateButton(body, name,
                    new Vector2(0.5f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(480, 42),
                    () =>
                    {
                        var incoming = catalog?.FindById(loot.BackpackRelicIds[index]);
                        if (exclusive && (incoming == null || !incoming.exclusiveEquip))
                        {
                            BuildRunRelicPanel(body, loot, catalog, notice: "単独装備中は、他のレリックを付けられない。");
                            return;
                        }

                        if (incoming != null && incoming.exclusiveEquip && loot.EquippedRelicCount() > 0)
                        {
                            BuildRunRelicPanel(body, loot, catalog, notice: "単独装備にするには、先に他のレリックを外す。");
                            return;
                        }

                        for (var slot = 0; slot < RunExplorationInventory.RelicSlotCount; slot++)
                        {
                            if (loot.GetEquippedRelicId(slot) >= 0)
                                continue;
                            if (loot.TryEquipFromBackpack(index, slot))
                                break;
                        }

                        BuildRunRelicPanel(body, loot, catalog);
                    });
                y -= 0.07f;
            }
        }

        void BuildHubShopDevelopPanel(RectTransform body, MetaProgressData profile, RelicCatalogDefinition catalog)
        {
            ClearChildren(body);
            var shards = profile?.arcaneShards ?? 0;
            var materials = MetaProgression.WarehouseMaterialUnits(profile);
            CreateLabel(body, "Wallet", $"◆ 魔石 {shards}　　素材倉庫 {materials}",
                18, TextAnchor.UpperCenter, new Vector2(0.04f, 0.9f), new Vector2(0.96f, 0.98f),
                Vector2.zero, Vector2.zero, BattleUITheme.Gold);

            var status = CreateLabel(body, "Status", string.Empty, 15, TextAnchor.UpperCenter,
                new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextMuted);

            var y = 0.78f;
            CreateLabel(body, "ShopHeader", "— 店（素材）—", 15, TextAnchor.UpperLeft,
                new Vector2(0.04f, y), new Vector2(0.96f, y + 0.05f), Vector2.zero, Vector2.zero,
                BattleUITheme.AccentAlly);
            y -= 0.07f;
            CreateLabel(body, "RelicNote", "レリックは塔の行商人か、出来事で手に入る。", 15, TextAnchor.UpperLeft,
                new Vector2(0.04f, y), new Vector2(0.96f, y + 0.05f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
            y -= 0.08f;

            if (catalog?.materialBundleShardCost > 0 && catalog.materialBundleAmount > 0)
            {
                var bundleLabel =
                    $"素材パック ×{catalog.materialBundleAmount}（{catalog.materialBundleShardCost} 魔石）";
                CreateButton(body, bundleLabel,
                    new Vector2(0.5f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(520, 42),
                    () =>
                    {
                        var matCatalog =
                            Resources.Load<MaterialCatalogDefinition>("Battle/PrototypeMaterialCatalog");
                        if (MetaHubEconomy.TryPurchaseMaterialBundle(profile, catalog, matCatalog))
                            status.text = "素材を倉庫に追加した。";
                        else
                            status.text = "魔石が足りない。";
                        BuildHubShopDevelopPanel(body, profile, catalog);
                    });
                y -= 0.065f;
            }

            CreateLabel(body, "Footnote", "レリックの購入と開発は、いまは塔の中だけ。",
                13, TextAnchor.LowerCenter, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.1f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
        }

        void BuildHubRelicPanel(RectTransform body, MetaProgressData profile, RelicCatalogDefinition catalog)
        {
            ClearChildren(body);
            CreateLabel(body, "Hint", "倉庫のレリックは、いまの探索には持ち込めない。塔の商人と出来事で手に入る。", 14,
                TextAnchor.UpperLeft, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.98f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            for (var slot = 0; slot < MetaRelicLoadout.SlotCount; slot++)
            {
                var s = slot;
                var id = MetaRelicLoadout.GetHubSlot(profile, s);
                var def = catalog?.FindById(id);
                var label = def != null ? $"出発枠{s + 1}: {def.displayName}" : $"出発枠{s + 1}: （空）";
                CreateButton(body, label + "　［外す］",
                    new Vector2(0.5f, 0.82f - s * 0.11f), new Vector2(0.5f, 0.82f - s * 0.11f),
                    Vector2.zero, new Vector2(540, 46),
                    () =>
                    {
                        MetaRelicLoadout.ReturnHubSlotToWarehouse(profile, s);
                        BuildHubRelicPanel(body, profile, catalog);
                    });
            }

            profile.warehouseRelicIds ??= new List<int>();
            var y = 0.46f;
            CreateLabel(body, "WhHeader", "倉庫のレリック（タップで空き枠へ）", 14, TextAnchor.UpperLeft,
                new Vector2(0.04f, y), new Vector2(0.96f, y + 0.05f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextMuted);
            y -= 0.07f;
            foreach (var relicId in profile.warehouseRelicIds.Distinct())
            {
                var id = relicId;
                var def = catalog?.FindById(id);
                var count = profile.warehouseRelicIds.Count(r => r == id);
                var name = def != null ? $"{def.displayName} x{count}" : $"#{id} x{count}";
                CreateButton(body, name,
                    new Vector2(0.5f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(480, 40),
                    () =>
                    {
                        for (var slot = 0; slot < MetaRelicLoadout.SlotCount; slot++)
                        {
                            if (MetaRelicLoadout.GetHubSlot(profile, slot) >= 0)
                                continue;
                            if (MetaRelicLoadout.AssignHubSlotFromWarehouse(profile, slot, id))
                                break;
                        }

                        BuildHubRelicPanel(body, profile, catalog);
                    });
                y -= 0.065f;
            }
        }

        void CreateReplaceRow(RectTransform parent, string slotLabel, SkillDefinition skill, int level, Action onPick)
        {
            var go = new GameObject(slotLabel);
            go.transform.SetParent(parent, false);
            var row = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 84;
            var img = go.AddComponent<Image>();
            EnsureImageDrawable(img);
            img.color = onPick == null
                ? new Color(0.2f, 0.15f, 0.05f, 0.9f)
                : new Color(0.06f, 0.1f, 0.18f, 0.92f);

            if (onPick != null)
            {
                var btn = go.AddComponent<Button>();
                btn.onClick.AddListener(() => onPick());
            }

            var name = skill != null ? SkillProgression.FormatName(skill, level) : "（空）";
            var effect = skill != null ? SkillProgression.Describe(skill, level) : "効果なし";
            var label = CreateLabel(row, "Label", $"{slotLabel}\n{name}\n<size=14>{effect}</size>", 16, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(0.62f, 1), new Vector2(12, 0), Vector2.zero, Color.white);
            label.supportRichText = true;

            if (skill?.pattern != null && skill.pattern.Length > 0)
            {
                var host = BattleUIOrbPattern.CreateHost(row, new Vector2(0.64f, 0.18f), new Vector2(0.98f, 0.82f));
                BattleUIOrbPattern.Apply(host, skill.pattern, 14f);
            }
        }

        void CreateCardButton(Transform parent, SkillDefinition card, RunPartyState party, Action onClick)
        {
            var panel = CreateSubPanel(parent, card.displayName, Vector2.zero, Vector2.one);
            panel.offsetMin = new Vector2(12, 12);
            panel.offsetMax = new Vector2(-12, -12);
            var layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 300;
            layout.minHeight = 440;
            var img = panel.GetComponent<Image>();
            img.sprite = null;
            EnsureImageDrawable(img);
            img.type = Image.Type.Simple;
            img.color = new Color(0.02f, 0.02f, 0.028f, 1f);

            var accent = CardAccent(card);
            var displayLevel = CardDisplayLevel(card, party);
            PlaceCardSprite(panel, "Illustration", CardArtSprite(), new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.9f));
            PlaceCardSprite(panel, "CardFrame", CardFrameSprite(displayLevel), Vector2.zero, Vector2.one);
            PlaceCardSprite(panel, "NamePlate", Resources.Load<Sprite>("Battle/UI/Cards/CardNamePlate"),
                new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.76f));
            PlaceCardSprite(panel, "EffectPlate", Resources.Load<Sprite>("Battle/UI/Cards/CardEffectPlate"),
                new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.58f));

            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.35f);
            outline.effectDistance = new Vector2(1, -1);

            var btn = panel.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.16f, 1.12f, 1.2f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.82f, 1f);
            btn.colors = colors;
            var motion = panel.gameObject.AddComponent<RewardCardMotion>();
            motion.Configure(outline, null, accent);

            var name = CreateLabel(panel, "Name", card.displayName, 24, TextAnchor.MiddleCenter,
                new Vector2(0.16f, 0.615f), new Vector2(0.84f, 0.745f), Vector2.zero, Vector2.zero, Color.white);
            name.font = BattleUITheme.FontBold;
            name.fontStyle = FontStyle.Bold;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            BattleUITheme.AddTextShadow(name, new Color(0, 0, 0, 0.95f), new Vector2(2, -2));
            var owner = CreateLabel(panel, "Owner", $"{card.characterName}  Lv.{displayLevel}", 12, TextAnchor.MiddleCenter,
                new Vector2(0.16f, 0.74f), new Vector2(0.84f, 0.8f), Vector2.zero, Vector2.zero, accent);
            owner.font = BattleUITheme.FontBold;
            owner.fontStyle = FontStyle.Bold;
            var effectLine = card.effectText;
            if (party != null && party.TryGetEquippedLevel(card, out var ownedLevel) && ownedLevel < SkillProgression.MaxLevel)
            {
                effectLine =
                    $"強化  Lv.{ownedLevel} → Lv.{ownedLevel + 1}\n" +
                    $"{SkillProgression.Describe(card, ownedLevel)} → {SkillProgression.Describe(card, ownedLevel + 1)}";
            }

            var effect = CreateLabel(panel, "Fx", effectLine, 15, TextAnchor.MiddleCenter,
                new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.54f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            effect.font = BattleUITheme.Font;
            effect.fontStyle = FontStyle.Normal;
            effect.horizontalOverflow = HorizontalWrapMode.Wrap;
            effect.verticalOverflow = VerticalWrapMode.Truncate;
            effect.lineSpacing = 1.05f;

            if (card.pattern != null && card.pattern.Length > 0)
            {
                var host = BattleUIOrbPattern.CreateHost(panel, new Vector2(0.16f, 0.05f), new Vector2(0.84f, 0.16f));
                BattleUIOrbPattern.Apply(host, card.pattern, 19f);
            }
        }

        static int CardDisplayLevel(SkillDefinition card, RunPartyState party)
        {
            if (card != null && party != null && party.TryGetEquippedLevel(card, out var owned))
                return Mathf.Clamp(owned < SkillProgression.MaxLevel ? owned + 1 : owned, 1, SkillProgression.MaxLevel);
            return 1;
        }

        static Sprite CardArtSprite() => Resources.Load<Sprite>("Battle/UI/Cards/CardArt_NoImage");

        static Sprite CardFrameSprite(int level)
        {
            var file = level >= 3 ? "CardFrame_Rainbow" : level == 2 ? "CardFrame_Gold" : "CardFrame_Silver";
            return Resources.Load<Sprite>($"Battle/UI/Cards/{file}");
        }

        static void PlaceCardSprite(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (sprite == null)
                return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        static Color CardAccent(SkillDefinition card)
        {
            if (card == null)
                return BattleUITheme.Arcane;
            if (card.characterName == "アリス")
                return BattleUITheme.AccentBoss;
            if (card.characterName == "エレン")
                return BattleUITheme.AccentAlly;
            if (card.characterName == "ルナ")
                return BattleUITheme.Cyan;
            return BattleUITheme.Arcane;
        }

        static string CardLabel(SkillDefinition card) => $"{card.displayName} ({card.effectText})";

        public void ShowDeathNotice(Transform host, int arcaneReward, string customBody = null)
        {
            EnsureRoot(host);
            if (_deathNoticePanel != null)
                UnityEngine.Object.Destroy(_deathNoticePanel);

            _deathNoticePanel = CreateFullScreenPanel("DeathNotice", new Color(0, 0, 0, 0.72f));
            var insanity = !string.IsNullOrEmpty(customBody);
            var window = BattleUIChrome.CreateLuxuryWindow(_deathNoticePanel.transform, "DeathWindow",
                new Vector2(0.28f, 0.32f), new Vector2(0.72f, 0.68f),
                insanity ? "発狂" : "GAME OVER", BattleUITheme.AccentBoss);
            var body = window.Find("Body") ?? window;
            var defaultBody =
                "パーティは力尽きた。\n塔は再配置され、解放済みエレベーターから再挑戦できる。\n\n" +
                "RUN 中のレリックと素材は失われる。";
            CreateLabel(body, "BodyText",
                insanity ? customBody + "\n\n塔は再配置され、解放済みエレベーターから再挑戦できる。\n\nRUN 中のレリックと素材は失われる。" : defaultBody,
                20, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.88f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            if (arcaneReward > 0)
            {
                CreateLabel(body, "Reward", $"獲得魔石  +{arcaneReward}", 18, TextAnchor.MiddleCenter,
                    new Vector2(0.2f, 0.22f), new Vector2(0.8f, 0.36f), Vector2.zero, Vector2.zero,
                    BattleUITheme.Gold);
            }

            CreateButton(body, "聖堂で整備する",
                new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(320, 52),
                () =>
                {
                    if (_deathNoticePanel != null)
                    {
                        UnityEngine.Object.Destroy(_deathNoticePanel);
                        _deathNoticePanel = null;
                    }
                });
        }

        public void HideAll()
        {
            if (_deathNoticePanel != null)
                UnityEngine.Object.Destroy(_deathNoticePanel);
            _deathNoticePanel = null;
            if (_hubPanel != null)
                UnityEngine.Object.Destroy(_hubPanel);
            if (_rewardPanel != null)
                UnityEngine.Object.Destroy(_rewardPanel);
            if (_loadoutPanel != null)
                UnityEngine.Object.Destroy(_loadoutPanel);
            if (_replacePanel != null)
                UnityEngine.Object.Destroy(_replacePanel);
            if (_selectionPanel != null)
                UnityEngine.Object.Destroy(_selectionPanel);
            _hubPanel = null;
            _rewardPanel = null;
            _loadoutPanel = null;
            _replacePanel = null;
            _selectionPanel = null;
        }

        public void TryCloseLoadout()
        {
            if (_loadoutPanel == null)
                return;

            HideAll();
            _onLoadoutClosed?.Invoke();
        }

        static void BuildOverlayBackdrop(Transform parent, Color accent)
        {
            BattleUIChrome.CreateGlow(parent, "CentralArcaneGlow", new Vector2(0.16f, 0.02f),
                new Vector2(0.84f, 0.98f), new Color(accent.r, accent.g, accent.b, 0.2f));
            BattleUIChrome.CreateGlow(parent, "GoldenHalo", new Vector2(0.32f, 0.2f),
                new Vector2(0.68f, 0.8f), new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.1f));

            for (var i = 0; i < 26; i++)
            {
                var mote = new GameObject($"RewardMote_{i}");
                mote.transform.SetParent(parent, false);
                var rt = mote.AddComponent<RectTransform>();
                var x = Mathf.Repeat(0.04f + i * 0.153f, 0.92f) + 0.04f;
                var y = Mathf.Repeat(0.05f + i * 0.271f, 0.9f) + 0.05f;
                rt.anchorMin = new Vector2(x, y);
                rt.anchorMax = new Vector2(x, y);
                var size = 2f + i % 4;
                rt.sizeDelta = new Vector2(size, size);
                var image = mote.AddComponent<Image>();
                image.sprite = i % 3 == 0 ? BattleUISprites.Diamond : BattleUISprites.Circle;
                image.color = new Color(
                    Mathf.Lerp(accent.r, 1f, 0.35f),
                    Mathf.Lerp(accent.g, 0.75f, 0.35f),
                    Mathf.Lerp(accent.b, 0.35f, 0.35f),
                    0.2f);
                image.raycastTarget = false;
            }
        }

        GameObject CreateFullScreenPanel(string name, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bg;
            EnsureImageDrawable(img);
            return go;
        }

        RectTransform CreateSubPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            BattleUIImage.ApplyPanel(img, new Color(0.018f, 0.024f, 0.055f, 0.86f),
                BattleUITemplateLoader.Default?.windowPanel);
            return rt;
        }

        static void EnsureImageDrawable(Image img)
        {
            if (img.sprite != null)
                return;
            BattleUITheme.EnsureSprite(img);
        }

        Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
            Action onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (size != default)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;
            }
            else
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var img = go.AddComponent<Image>();
            BattleUIImage.ApplyPanel(img, new Color(0.13f, 0.06f, 0.1f, 0.97f),
                BattleUITemplateLoader.Default?.button);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 36;
            var text = CreateLabel(go.transform, "Text", label, 14, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            text.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(text, new Color(0, 0, 0, 0.9f), new Vector2(2, -2));
            return btn;
        }

        Text CreateLabel(Transform parent, string name, string text, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            if (sizeDelta != Vector2.zero || pos != Vector2.zero)
            {
                rt.sizeDelta = sizeDelta;
                rt.anchoredPosition = pos;
            }
            else
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var label = go.AddComponent<Text>();
            label.font = UiFont;
            label.fontSize = size;
            label.alignment = anchor;
            label.text = text;
            label.color = color;
            return label;
        }

        static void ClearChildren(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    public sealed class ArchiveSwapPulse : MonoBehaviour
    {
        Outline _outline;
        Color _baseOutlineColor;
        Vector2 _baseOutlineDistance;
        bool _baseOutlineEnabled;
        bool _pulsing;

        public void Configure(Outline outline)
        {
            _outline = outline;
            if (_outline != null)
            {
                _baseOutlineColor = _outline.effectColor;
                _baseOutlineDistance = _outline.effectDistance;
                _baseOutlineEnabled = _outline.enabled;
            }
        }

        public void SetPulsing(bool pulsing)
        {
            _pulsing = pulsing;
            if (_outline == null)
                return;
            if (!pulsing)
            {
                _outline.enabled = _baseOutlineEnabled;
                _outline.effectColor = _baseOutlineColor;
                _outline.effectDistance = _baseOutlineDistance;
            }
        }

        void Update()
        {
            var targetScale = 1f;
            if (_pulsing)
            {
                var wave = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
                targetScale = Mathf.Lerp(1.015f, 1.065f, wave);
                if (_outline != null)
                {
                    _outline.enabled = true;
                    _outline.effectColor = Color.Lerp(BattleUITheme.Cyan, Color.white, wave * 0.7f);
                    _outline.effectDistance = Vector2.one * Mathf.Lerp(2f, 4f, wave);
                }
            }

            transform.localScale = Vector3.Lerp(
                transform.localScale,
                Vector3.one * targetScale,
                Time.unscaledDeltaTime * 12f);
        }
    }

    public sealed class RewardCardMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Outline _outline;
        Image _sigil;
        Color _accent;
        Vector3 _targetScale = Vector3.one;
        float _targetRotation;

        public void Configure(Outline outline, Image sigil, Color accent)
        {
            _outline = outline;
            _sigil = sigil;
            _accent = accent;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = Vector3.one * 1.055f;
            _targetRotation = -1.5f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = Vector3.one;
            _targetRotation = 0f;
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * 11f);
            var euler = transform.localEulerAngles;
            var z = Mathf.LerpAngle(euler.z, _targetRotation, Time.unscaledDeltaTime * 11f);
            transform.localEulerAngles = new Vector3(0, 0, z);
            var hover = Mathf.InverseLerp(1f, 1.055f, transform.localScale.x);
            if (_outline != null)
                _outline.effectColor = new Color(_accent.r, _accent.g, _accent.b, Mathf.Lerp(0.62f, 1f, hover));
            if (_sigil != null)
                _sigil.transform.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * Mathf.Lerp(16f, 52f, hover));
        }
    }
}
