using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TypingRoguelike.Battle;
using TypingRoguelike.Battle.Deck;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    public sealed class RunOverlayUI
    {
        public static bool SuppressPrefab;
        Font _font;
        RectTransform _root;
        GameObject _hubPanel;
        GameObject _rewardPanel;
        GameObject _loadoutPanel;
        GameObject _replacePanel;
        GameObject _selectionPanel;
        GameObject _deathNoticePanel;
        GameObject _jackpotDebugPanel;
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
            Action onOpenRepairShop = null,
            Action onOpenPartyStats = null,
            Action onOpenJackpotDebug = null)
        {
            EnsureRoot(host);
            HideAll();
            _hubPanel = CreateFullScreenPanel("OfficeHub", new Color(0.04f, 0.045f, 0.055f, 1));
            _hubPanel.AddComponent<HubOfficeView>().Build(profile, onStartRun, onResumeRun,
                elevatorFloors, onStartFromElevatorFloor, onOpenRelicHub, onOpenHubShop,
                onOpenRepairShop);
        }

        public void ShowJackpotDebug(Transform host, Action onClosed)
        {
            EnsureRoot(host);
            if (_jackpotDebugPanel != null)
                UnityEngine.Object.Destroy(_jackpotDebugPanel);

            _jackpotDebugPanel = CreateFullScreenPanel("JackpotDebug", new Color(0.005f, 0.008f, 0.02f, 0.94f));
            var window = BattleUIChrome.CreateLuxuryWindow(
                _jackpotDebugPanel.transform, "JackpotDebugWindow",
                new Vector2(0.24f, 0.16f), new Vector2(0.76f, 0.84f),
                "JACKPOT", BattleUITheme.Gold);
            var body = window.Find("Body") ?? window;
            var title = CreateLabel(body, "Title", "ジャックポット抽選", 28, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);
            title.fontStyle = FontStyle.Bold;
            CreateLabel(body, "Hint", "確率を入れて1000回引く。10 と 10% は10%。0.1 は10%。0.1% はそのまま。",
                15, TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.84f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var field = CreateSubPanel(body, "ChanceField", new Vector2(0.22f, 0.58f), new Vector2(0.78f, 0.7f));
            var fieldImage = field.GetComponent<Image>();
            fieldImage.color = new Color(0.05f, 0.04f, 0.03f, 0.96f);
            var inputText = CreateLabel(field, "Text", "", 24, TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            inputText.supportRichText = false;
            inputText.raycastTarget = false;
            var input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = fieldImage;
            input.textComponent = inputText;
            input.characterValidation = InputField.CharacterValidation.None;
            input.text = "10%";

            var result = CreateLabel(body, "Result", "まだ引いていない", 20, TextAnchor.UpperCenter,
                new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.54f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            result.horizontalOverflow = HorizontalWrapMode.Wrap;
            result.verticalOverflow = VerticalWrapMode.Overflow;

            CreateButton(body, "1000回抽選",
                new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.14f), Vector2.zero, new Vector2(280, 52),
                () =>
                {
                    if (!JackpotRollSimulation.TryParseChance(input.text, out var chance))
                    {
                        result.text = "確率を読み取れない。10、10%、0.1、0.1% のように入れてください。";
                        return;
                    }

                    var report = JackpotRollSimulation.Run(chance);
                    result.text = report;
                    Debug.Log("[JackpotRoll] " + report.Replace("\n", " / "));
                });
            CreateButton(_jackpotDebugPanel.transform, "閉じる",
                new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(220, 46),
                () =>
                {
                    if (_jackpotDebugPanel != null)
                        UnityEngine.Object.Destroy(_jackpotDebugPanel);
                    _jackpotDebugPanel = null;
                    onClosed?.Invoke();
                });
        }

        public void ShowDeckReward(Transform host, RunPartyState party, IReadOnlyList<DeckCardDefinition> cards,
            int coins, string notice, Action<DeckCardDefinition> onPicked)
        {
            EnsureRoot(host);
            HideAll();
            _rewardPanel = CreateFullScreenPanel("DeckReward", new Color(0.015f, 0.021f, 0.044f, 0.98f));
            _rewardPanel.AddComponent<DeckRewardView>().Build(cards, party, coins, notice, onPicked);
        }

        public void ShowCardReward(
            Transform host,
            RunPartyState party,
            IReadOnlyList<SkillDefinition> choices,
            Action<SkillDefinition> onPicked,
            Action onDecline = null,
            string notice = null)
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
            CreateLabel(body, "Sub",
                string.IsNullOrEmpty(notice)
                    ? "空き枠へ自動装備。所持済みは出ない。取得しない場合は装備中スキルの経験値+10"
                    : notice,
                14, TextAnchor.UpperCenter,
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
                CreateButton(_rewardPanel.transform, "取得せず経験値+10",
                    new Vector2(0.5f, 0.055f), new Vector2(0.5f, 0.055f), Vector2.zero, new Vector2(320, 46),
                    () =>
                    {
                        HideAll();
                        onDecline.Invoke();
                    });
            }
        }

        public void ShowSkillXpPick(
            Transform host,
            RunPartyState party,
            int amount,
            Action<int, int> onConfirm,
            Action onBack) =>
            ShowSkillXpPick(host, party, amount, -1, -1, onConfirm, onBack);

        void ShowSkillXpPick(
            Transform host,
            RunPartyState party,
            int amount,
            int selectedMember,
            int selectedSlot,
            Action<int, int> onConfirm,
            Action onBack)
        {
            EnsureRoot(host);
            HideAll();
            _selectionPanel = CreateFullScreenPanel("SkillXpPick", new Color(0.005f, 0.008f, 0.025f, 0.96f));
            _selectionPanel.AddComponent<CanvasGroup>();
            _selectionPanel.AddComponent<RunPanelEntrance>();
            BuildOverlayBackdrop(_selectionPanel.transform, BattleUITheme.Arcane);
            var window = BattleUIChrome.CreateLuxuryWindow(_selectionPanel.transform, "XpWindow",
                new Vector2(0.14f, 0.08f), new Vector2(0.86f, 0.92f), "GRANT EXPERIENCE", BattleUITheme.Gold);
            var body = window.Find("Body") ?? window;
            var title = CreateLabel(body, "Title", "経験値を渡すスキルを選ぶ", 28, TextAnchor.UpperCenter,
                new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.98f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            title.fontStyle = FontStyle.Bold;
            CreateLabel(body, "Sub", $"装備中のスキルを選び、確定で経験値 +{amount}", 16, TextAnchor.UpperCenter,
                new Vector2(0.08f, 0.81f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var entries = ListEquippedSlots(party);
            if (entries.Count == 0)
            {
                CreateLabel(body, "Empty", "装備中のスキルがない", 18, TextAnchor.MiddleCenter,
                    new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
            }
            else
            {
                var top = 0.78f;
                var height = Mathf.Min(0.09f, 0.62f / entries.Count);
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var yMax = top - i * (height + 0.008f);
                    var selected = entry.MemberIndex == selectedMember && entry.SlotIndex == selectedSlot;
                    var row = CreateSubPanel(body, $"Xp_{entry.MemberIndex}_{entry.SlotIndex}",
                        new Vector2(0.08f, yMax - height), new Vector2(0.92f, yMax));
                    var image = row.GetComponent<Image>();
                    image.color = selected
                        ? new Color(0.22f, 0.16f, 0.04f, 0.98f)
                        : new Color(0.025f, 0.04f, 0.085f, 0.96f);
                    var outline = row.gameObject.AddComponent<Outline>();
                    outline.effectColor = selected ? BattleUITheme.Gold : BattleUITheme.TextMuted;
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                    var button = row.gameObject.AddComponent<Button>();
                    var memberIndex = entry.MemberIndex;
                    var slotIndex = entry.SlotIndex;
                    button.onClick.AddListener(() =>
                        ShowSkillXpPick(host, party, amount, memberIndex, slotIndex, onConfirm, onBack));

                    var skill = entry.Skill;
                    var level = entry.Level;
                    var text = $"{entry.OwnerName}　{SkillProgression.FormatName(skill, level)}\n<size=13>{SkillProgression.FormatXp(entry.Xp)}　{SkillProgression.Describe(skill, level, entry.Attack)}</size>";
                    var label = CreateLabel(row, "Label", text, 16, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-12, 0), BattleUITheme.TextPrimary);
                    label.supportRichText = true;
                }
            }

            var canConfirm = selectedMember >= 0 && selectedSlot >= 0;
            var confirm = CreateButton(_selectionPanel.transform, "確定",
                new Vector2(0.38f, 0.06f), new Vector2(0.38f, 0.06f), Vector2.zero, new Vector2(220, 48),
                () =>
                {
                    if (!canConfirm)
                        return;
                    HideAll();
                    onConfirm?.Invoke(selectedMember, selectedSlot);
                });
            confirm.interactable = canConfirm;
            CreateButton(_selectionPanel.transform, "戻る",
                new Vector2(0.62f, 0.06f), new Vector2(0.62f, 0.06f), Vector2.zero, new Vector2(220, 48),
                () =>
                {
                    HideAll();
                    onBack?.Invoke();
                });
        }

        readonly struct EquippedSlotEntry
        {
            public EquippedSlotEntry(int memberIndex, int slotIndex, string ownerName, SkillDefinition skill, int level, int xp, int attack)
            {
                MemberIndex = memberIndex;
                SlotIndex = slotIndex;
                OwnerName = ownerName;
                Skill = skill;
                Level = level;
                Xp = xp;
                Attack = attack;
            }

            public int MemberIndex { get; }
            public int SlotIndex { get; }
            public string OwnerName { get; }
            public SkillDefinition Skill { get; }
            public int Level { get; }
            public int Xp { get; }
            public int Attack { get; }
        }

        static List<EquippedSlotEntry> ListEquippedSlots(RunPartyState party)
        {
            var result = new List<EquippedSlotEntry>();
            if (party?.Members == null)
                return result;

            for (var i = 0; i < party.Members.Count; i++)
            {
                var member = party.Members[i];
                var owner = member.character != null ? StarterCharacters.Shown(member.character.characterName) : "?";
                var attack = 0;
                AddEquipped(result, member, i, MemberRunState.InnateSlotIndex, owner, attack);
                AddEquipped(result, member, i, 0, owner, attack);
                AddEquipped(result, member, i, 1, owner, attack);
            }

            return result;
        }

        static void AddEquipped(
            List<EquippedSlotEntry> result,
            MemberRunState member,
            int memberIndex,
            int slotIndex,
            string owner,
            int attack)
        {
            var skill = member.GetSlot(slotIndex);
            if (skill == null)
                return;
            result.Add(new EquippedSlotEntry(
                memberIndex,
                slotIndex,
                owner,
                skill,
                member.GetSlotLevel(slotIndex),
                member.GetSlotXp(slotIndex),
                attack));
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

                var charName = member.character != null ? StarterCharacters.Shown(member.character.characterName) : "?";
                var name = CreateLabel(card, "Name", charName, 15, TextAnchor.MiddleLeft,
                    new Vector2(0.23f, 0.77f), new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero,
                    BattleUITheme.TextPrimary);
                name.fontStyle = FontStyle.Bold;

                var attack = 0;
                CreateRewardSkillRow(card, "Innate", member.innateSlot, true, member.GetSlotLevel(MemberRunState.InnateSlotIndex), member.GetSlotXp(MemberRunState.InnateSlotIndex), attack, 0.53f, 0.75f);
                CreateRewardSkillRow(card, "Slot1", member.GetSlot(0), false, member.GetSlotLevel(0), member.GetSlotXp(0), attack, 0.28f, 0.5f);
                CreateRewardSkillRow(card, "Slot2", member.GetSlot(1), false, member.GetSlotLevel(1), member.GetSlotXp(1), attack, 0.03f, 0.25f);
            }
        }

        void CreateRewardSkillRow(
            Transform parent,
            string name,
            SkillDefinition skill,
            bool innate,
            int level,
            int xp,
            int ownerAttack,
            float yMin,
            float yMax)
        {
            var color = innate
                ? new Color(BattleUITheme.InnateRow.r, BattleUITheme.InnateRow.g, BattleUITheme.InnateRow.b, 0.9f)
                : new Color(BattleUITheme.CardRow.r, BattleUITheme.CardRow.g, BattleUITheme.CardRow.b, 0.9f);
            var row = CreateSubPanel(parent, name, new Vector2(0.23f, yMin), new Vector2(0.97f, yMax));
            row.GetComponent<Image>().color = color;

            var prefix = innate ? "★ " : "◆ ";
            var displayName = skill != null ? SkillProgression.FormatName(skill, level) : "空き";
            var effect = skill != null
                ? $"{SkillProgression.FormatXp(xp)}　{SkillProgression.Describe(skill, level, ownerAttack)}"
                : string.Empty;
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

            var label = CreateLabel(rt, "Name", $"{card.displayName}\n<size=10>{SkillProgression.Describe(card, 1, AttackFor(card))}</size>", 14,
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
                var name = member.character != null ? StarterCharacters.Shown(member.character.characterName) : "?";
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
                $"{card.displayName}\n<size=10>{StarterCharacters.Shown(card.characterName)}　{SkillProgression.Describe(card, 1, AttackFor(card))}</size>", 14,
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

            var label = skill != null
                ? $"{slotName}　{SkillProgression.FormatName(skill, member.GetSlotLevel(slotIndex))}\n<size=11>{SkillProgression.FormatXp(member.GetSlotXp(slotIndex))}</size>"
                : $"{slotName}　（空き）";
            var slotLabel = CreateLabel(panel, "Text", label, 13, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0), new Vector2(0.62f, 1), new Vector2(8, 0), Vector2.zero,
                skill != null ? BattleUITheme.TextPrimary : BattleUITheme.TextMuted);
            slotLabel.supportRichText = true;
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
            var charName = member.character != null ? StarterCharacters.Shown(member.character.characterName) : "?";
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
            var ownerAttack = 0;
            CreateLabel(windowBody, "NewEffect", SkillProgression.Describe(newCard, 1, ownerAttack), 15, TextAnchor.UpperCenter,
                new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.74f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);

            var body = CreateSubPanel(windowBody, "Body", new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.64f));
            var v = body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.padding = new RectOffset(12, 12, 12, 12);
            v.childControlHeight = true;
            v.childForceExpandHeight = false;

            if (!cardSlotsOnly)
            {
                CreateReplaceRow(body, "★ 初期", member.innateSlot, member.GetSlotLevel(MemberRunState.InnateSlotIndex), ownerAttack, () =>
                {
                    party.ReplaceEquippedSlot(memberIndex, MemberRunState.InnateSlotIndex, newCard);
                    HideAll();
                    onDone?.Invoke();
                });
            }

            CreateReplaceRow(body, "◆ カード1", member.GetSlot(0), member.GetSlotLevel(0), ownerAttack, () =>
            {
                party.ReplaceEquippedSlot(memberIndex, 0, newCard);
                HideAll();
                onDone?.Invoke();
            });
            CreateReplaceRow(body, "◆ カード2", member.GetSlot(1), member.GetSlotLevel(1), ownerAttack, () =>
            {
                party.ReplaceEquippedSlot(memberIndex, 1, newCard);
                HideAll();
                onDone?.Invoke();
            });

            if (onDecline != null)
            {
                CreateButton(windowBody, "取らない",
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

        public void ShowHubRepairPanel(
            Transform host,
            MetaProgressData profile,
            ProxyBrainUpgradeCatalog catalog,
            Action onClosed)
        {
            EnsureRoot(host);
            HideAll();
            _loadoutPanel = CreateFullScreenPanel("HubRepair", new Color(0.008f, 0.01f, 0.026f, 0.98f));
            _loadoutPanel.AddComponent<CanvasGroup>();
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.AccentBoss);

            CreateLabel(_loadoutPanel.transform, "Title", "拠点 — 修復屋", 24,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                Vector2.zero, Vector2.zero, BattleUITheme.AccentBoss);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f));
            BuildHubRepairPanel(body, profile, catalog, _loadoutPanel.transform, onClosed);

            CreateButton(_loadoutPanel.transform, "拠点に戻る",
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(280, 48),
                () =>
                {
                    MetaProgression.Save(profile);
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
            var fadeGroup = _loadoutPanel.AddComponent<CanvasGroup>();
            host.GetComponent<RunFlowController>()?.StartCoroutine(HubOfficeView.FadePopup(fadeGroup));
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.Cyan);
            CreateLabel(_loadoutPanel.transform, "Title", "拠点 — 店・レリック開発", 24,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                Vector2.zero, Vector2.zero, BattleUITheme.Cyan);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f));
            BuildHubShopDevelopPanel(body, profile, catalog);

            CreateButton(_loadoutPanel.transform, "拠点に戻る",
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
            Action onClosed,
            PersonalityChartView[] charts = null,
            Action<string, int> onSetAffinity = null)
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
            CreateLabel(_loadoutPanel.transform, "Title", "ステータス", 26, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.97f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.88f));
            BuildDungeonStatus(body, party, stats, relicMods, charts, onSetAffinity);

            CreateButton(_loadoutPanel.transform, closeButtonLabel,
                new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.06f), Vector2.zero, new Vector2(300, 48),
                () =>
                {
                    HideAll();
                    onClosed?.Invoke();
                });
        }

        void BuildDungeonStatus(
            RectTransform body,
            RunPartyState party,
            PartyCombatStats stats,
            RelicRunModifiers relicMods,
            PersonalityChartView[] charts,
            Action<string, int> onSetAffinity)
        {
            var chartCount = charts != null ? charts.Length : 0;
            if (chartCount > 0)
                BuildPersonalityCharts(body, charts, onSetAffinity);
            if (party?.Members == null || party.Members.Count == 0)
            {
                if (chartCount == 0)
                {
                    CreateLabel(body, "Empty", "編成情報なし", 18, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
                }

                return;
            }

            var count = party.Members.Count;
            var blockHeight = 0.76f / count;
            var cardRight = chartCount > 0 ? 0.64f : 0.985f;
            for (var i = 0; i < count; i++)
            {
                var member = party.Members[i];
                var yMax = 0.98f - i * blockHeight;
                var card = CreateSubPanel(body, $"Status_{i}",
                    new Vector2(0.015f, yMax - blockHeight + 0.012f), new Vector2(cardRight, yMax - 0.006f));
                BattleUIImage.ApplyPanel(card.GetComponent<Image>(), new Color(0.025f, 0.035f, 0.08f, 0.96f),
                    BattleUITemplateLoader.Default?.partyCard);

                var character = member.character;
                var name = character != null ? StarterCharacters.Shown(character.characterName) : "?";
                var role = character != null ? character.roleLabel : string.Empty;
                var nameLabel = CreateLabel(card, "Name", $"{name}　{role}", 18, TextAnchor.MiddleLeft,
                    new Vector2(0.02f, 0.78f), new Vector2(0.98f, 0.98f), new Vector2(12, 0), Vector2.zero,
                    BattleUITheme.TextPrimary);
                nameLabel.fontStyle = FontStyle.Bold;

                var hp = character != null ? character.hp : 0;
                var attack = RunPartyState.GetAttack(character);
                var defense = character != null ? character.defense : 0;
                var luck = character != null ? character.luck : 0;
                CreateLabel(card, "Stats", $"HP {hp}　　攻撃 {attack}　　防御 {defense}　　運 {luck}", 15,
                    TextAnchor.MiddleLeft, new Vector2(0.02f, 0.62f), new Vector2(0.98f, 0.78f),
                    new Vector2(12, 0), Vector2.zero, BattleUITheme.Gold);

                AddStatusSkillLine(card, member, MemberRunState.InnateSlotIndex, "★ 初期", 0.42f, 0.62f);
                AddStatusSkillLine(card, member, 0, "◆ カード1", 0.22f, 0.42f);
                AddStatusSkillLine(card, member, 1, "◆ カード2", 0.02f, 0.22f);
            }

            var luckShown = Mathf.Min(stats.TotalLuck, PartyCombatStats.LuckCap);
            var footer = $"パーティ合計　HP {stats.TotalHp}　攻撃 {stats.TotalAttack}　防御 {stats.TotalDefense}　運 {luckShown}";
            if (party.RunHpInitialized)
                footer = $"探索 HP {(int)party.CurrentHp} / {(int)party.MaxHp}　　" + footer;
            if (relicMods.PlayerDamageBonusPercent > 0)
                footer += $"　　与ダメ +{relicMods.PlayerDamageBonusPercent}%";
            CreateLabel(body, "Footer", footer, 14, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0f), new Vector2(chartCount > 0 ? 0.63f : 0.98f, 0.2f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
        }

        void BuildPersonalityCharts(RectTransform body, PersonalityChartView[] charts, Action<string, int> onSetAffinity)
        {
            var count = charts.Length;
            var block = 1f / count;
            var debugValues = onSetAffinity != null ? PersonalityCatalog.DebugAffinity : Array.Empty<int>();
            for (var i = 0; i < count; i++)
            {
                var chart = charts[i];
                if (chart == null)
                    continue;
                var yMax = 1f - i * block;
                var card = CreateSubPanel(body, "Chart_" + i,
                    new Vector2(0.655f, yMax - block + 0.012f), new Vector2(0.99f, yMax - 0.006f));
                CreateLabel(card, "Name", $"{chart.Name}　好感度 {chart.Affinity}", 16, TextAnchor.MiddleCenter,
                    new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);
                var summary = new System.Text.StringBuilder();
                var axes = chart.AxisNames ?? Array.Empty<string>();
                var stages = chart.Stages ?? Array.Empty<int>();
                var n = Mathf.Min(axes.Length, stages.Length);
                for (var a = 0; a < n; a++)
                {
                    if (summary.Length > 0)
                        summary.Append("　");
                    summary.Append(axes[a]);
                    summary.Append(' ');
                    summary.Append(stages[a]);
                }

                var summaryLabel = CreateLabel(card, "Axes", summary.ToString(), 12, TextAnchor.MiddleCenter,
                    new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
                summaryLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

                var radarGo = new GameObject("Radar");
                radarGo.transform.SetParent(card, false);
                var radarRt = radarGo.AddComponent<RectTransform>();
                radarRt.anchorMin = new Vector2(0.18f, debugValues.Length > 0 ? 0.24f : 0.08f);
                radarRt.anchorMax = new Vector2(0.82f, 0.72f);
                radarRt.offsetMin = Vector2.zero;
                radarRt.offsetMax = Vector2.zero;
                var radar = radarGo.AddComponent<PersonalityRadarGraphic>();
                var values = new float[n > 0 ? n : 3];
                var max = chart.StageMax < 1 ? 1 : chart.StageMax;
                for (var a = 0; a < values.Length; a++)
                    values[a] = a < n ? Mathf.Clamp01(stages[a] / (float)max) : 0.2f;
                radar.color = Color.white;
                radar.SetShape(values, max);

                if (debugValues.Length == 0 || string.IsNullOrEmpty(chart.Id))
                    continue;
                var shown = Mathf.Min(debugValues.Length, 5);
                for (var d = 0; d < shown; d++)
                {
                    var value = debugValues[d];
                    var x = shown == 1 ? 0.5f : 0.12f + 0.76f * d / (shown - 1);
                    var id = chart.Id;
                    CreateButton(card, value.ToString(),
                        new Vector2(x, 0.08f), new Vector2(x, 0.08f), Vector2.zero, new Vector2(44f, 28f),
                        () => onSetAffinity(id, value));
                }
            }
        }

        void AddStatusSkillLine(Transform parent, MemberRunState member, int slotIndex, string slotName, float yMin, float yMax)
        {
            var skill = member.GetSlot(slotIndex);
            var attack = 0;
            var text = skill == null
                ? $"{slotName}　（空き）"
                : $"{slotName}　{SkillProgression.FormatName(skill, member.GetSlotLevel(slotIndex))}　　{SkillProgression.FormatXp(member.GetSlotXp(slotIndex))}\n<size=12>{SkillProgression.Describe(skill, member.GetSlotLevel(slotIndex), attack)}</size>";
            var label = CreateLabel(parent, $"Skill_{slotIndex}", text, 14, TextAnchor.MiddleLeft,
                new Vector2(0.02f, yMin), new Vector2(0.98f, yMax), new Vector2(12, 0), Vector2.zero,
                skill != null ? BattleUITheme.TextPrimary : BattleUITheme.TextMuted);
            label.supportRichText = true;
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
            var fadeGroup = _loadoutPanel.AddComponent<CanvasGroup>();
            host.GetComponent<RunFlowController>()?.StartCoroutine(HubOfficeView.FadePopup(fadeGroup));
            BuildOverlayBackdrop(_loadoutPanel.transform, BattleUITheme.Gold);
            CreateLabel(_loadoutPanel.transform, "Title", "拠点 — レリック装備（次の出発時）", 24,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                Vector2.zero, Vector2.zero, BattleUITheme.Gold);

            var body = CreateSubPanel(_loadoutPanel.transform, "Body", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f));
            BuildHubRelicPanel(body, profile, catalog);

            CreateButton(_loadoutPanel.transform, "拠点に戻る",
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

        void BuildHubRepairPanel(
            RectTransform body,
            MetaProgressData profile,
            ProxyBrainUpgradeCatalog catalog,
            Transform host,
            Action onClosed)
        {
            ClearChildren(body);
            var banked = profile?.bankedCoins ?? 0;
            CreateLabel(body, "Wallet", $"貯金（塔片） {banked}",
                18, TextAnchor.UpperCenter, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.98f),
                Vector2.zero, Vector2.zero, BattleUITheme.Gold);
            CreateLabel(body, "Hint", "持ち帰った塔片で代理脳を強化する。全滅しても装置の強化は残る。",
                14, TextAnchor.UpperCenter, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.92f),
                Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);

            var y = 0.78f;
            AppendRepairTrackRow(body, host, profile, catalog, onClosed, ProxyBrainUpgradeTrackKind.MadnessMaxBonus, ref y);
            AppendRepairTrackRow(body, host, profile, catalog, onClosed, ProxyBrainUpgradeTrackKind.PartyMaxHpPercent, ref y);
            AppendRepairTrackRow(body, host, profile, catalog, onClosed, ProxyBrainUpgradeTrackKind.RestMadnessRecovery, ref y);
        }

        void AppendRepairTrackRow(
            RectTransform body,
            Transform host,
            MetaProgressData profile,
            ProxyBrainUpgradeCatalog catalog,
            Action onClosed,
            ProxyBrainUpgradeTrackKind kind,
            ref float y)
        {
            var track = catalog?.GetTrack(kind);
            var title = track != null && !string.IsNullOrEmpty(track.displayName)
                ? track.displayName
                : kind.ToString();
            var progress = ProxyBrainUpgrades.FormatTrackProgress(profile, kind);
            CreateLabel(body, $"Hdr_{kind}", $"— {title}（{progress}）", 15, TextAnchor.UpperLeft,
                new Vector2(0.04f, y), new Vector2(0.96f, y + 0.045f), Vector2.zero, Vector2.zero,
                BattleUITheme.AccentAlly);
            y -= 0.055f;

            if (ProxyBrainUpgrades.TryGetNextTier(profile, catalog, kind, out var nextTier, out _))
            {
                var preview = ProxyBrainUpgrades.FormatEffectPreview(kind, nextTier);
                var label = $"購入：{preview}（{nextTier.cost} 塔片）";
                var canBuy = ProxyBrainUpgrades.CanPurchase(profile, catalog, kind, out var block, out _);
                if (!canBuy && block == ProxyBrainUpgrades.PurchaseBlockReason.InsufficientFunds)
                    label += " — 貯金が足りない";
                var btn = CreateButton(body, label,
                    new Vector2(0.5f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(560, 44),
                    () =>
                    {
                        if (!ProxyBrainUpgrades.TryPurchase(profile, catalog, kind, out var line, out _))
                            return;
                        ShowRepairShopFlavorDialog(host, line, () =>
                        {
                            HideAll();
                            ShowHubRepairPanel(host, profile, catalog, onClosed);
                        });
                    });
                btn.interactable = canBuy;
                if (!canBuy)
                {
                    var colors = btn.colors;
                    colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.85f);
                    btn.colors = colors;
                }
            }
            else
            {
                CreateLabel(body, $"Max_{kind}", "最大段階まで強化済み — これ以上は買えない",
                    14, TextAnchor.UpperLeft, new Vector2(0.06f, y - 0.01f), new Vector2(0.94f, y + 0.04f),
                    Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
            }

            y -= 0.075f;
        }

        void ShowRepairShopFlavorDialog(Transform host, string line, Action onDismiss)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                onDismiss?.Invoke();
                return;
            }

            var panel = CreateFullScreenPanel("RepairFlavor", new Color(0, 0, 0, 0.65f));
            panel.transform.SetParent(host, false);
            var window = BattleUIChrome.CreateLuxuryWindow(panel.transform, "RepairLine",
                new Vector2(0.22f, 0.34f), new Vector2(0.78f, 0.62f), "修復屋", BattleUITheme.AccentBoss);
            var winBody = window.Find("Body") ?? window;
            CreateLabel(winBody, "Line", line, 19, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
            CreateButton(winBody, "続ける",
                new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(240, 46),
                () =>
                {
                    UnityEngine.Object.Destroy(panel);
                    onDismiss?.Invoke();
                });
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

        void CreateReplaceRow(RectTransform parent, string slotLabel, SkillDefinition skill, int level, int ownerAttack, Action onPick)
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
            var effect = skill != null ? SkillProgression.Describe(skill, level, ownerAttack) : "効果なし";
            var label = CreateLabel(row, "Label", $"{slotLabel}\n{name}\n<size=14>{effect}</size>", 16, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(0.62f, 1), new Vector2(12, 0), Vector2.zero, Color.white);
            label.supportRichText = true;

            if (skill?.pattern != null && skill.pattern.Length > 0)
            {
                var host = BattleUIOrbPattern.CreateHost(row, new Vector2(0.64f, 0.18f), new Vector2(0.98f, 0.82f));
                BattleUIOrbPattern.Apply(host, skill.pattern, 14f);
            }
        }

        const string RewardCardSprites = "Battle/UI/Cards/";

        void CreateCardButton(Transform parent, SkillDefinition card, RunPartyState party, Action onClick)
        {
            // The layout group sizes the slot. The card fits inside it at a fixed 3:4 ratio.
            var slot = new GameObject("RewardCardSlot", typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            var panel = CreateSubPanel(slot.transform, card.displayName, Vector2.zero, Vector2.one);
            var aspect = panel.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 3f / 4f;

            var hitArea = panel.GetComponent<Image>();
            hitArea.color = Color.clear;
            var rarity = card.rarity?.Trim().ToUpperInvariant() switch
            {
                "SR" => "Rainbow",
                "R" => "Gold",
                _ => "Silver"
            };

            PlaceCardSprite(panel, "Illustration", "CardArt_NoImage", CardPoint(10, 590), CardPoint(440, 10));
            PlaceCardSprite(panel, "EffectShade", "CardEffectPlate", CardPoint(10, 590), CardPoint(440, 10));
            var frame = PlaceCardSprite(panel, "CardFrame", $"CardFrame_{rarity}", Vector2.zero, Vector2.one);
            var levelPlate = rarity == "Silver" ? "CardNamePlate" : $"CardLevelPlate_{rarity}";
            PlaceCardSprite(panel, "LevelPlate", levelPlate, CardPoint(139, 58), CardPoint(311, 16));

            var level = CardDisplayLevel(card, party);
            CreateCardLabel(panel, "Level", $"Lv {level}", BattleUITheme.FontDisplay, 26,
                TextAnchor.MiddleCenter, 151, 17, 299, 53, new Color32(236, 232, 244, 255));

            CreateCardLabel(panel, "Owner", StarterCharacters.Shown(card.characterName), BattleUITheme.FontBold, 12,
                TextAnchor.MiddleLeft, 27, 28, 133, 56, BattleUITheme.TextPrimary);

            var orbPlateLeft = PlaceCardOrbs(panel, card.pattern, rarity);
            var titleFont = Resources.Load<Font>("Battle/Fonts/ShipporiMinchoB1-ExtraBold") ?? BattleUITheme.FontDisplay;
            CreateCardLabel(panel, "Name", card.displayName, titleFont, 32, TextAnchor.MiddleLeft,
                28, 392, orbPlateLeft - 10, 452, new Color32(252, 246, 238, 255));

            var effect = SkillProgression.Describe(card, level, 0) ?? string.Empty;
            if (!effect.Contains("<"))
                effect = System.Text.RegularExpressions.Regex.Replace(effect, @"[+＋\-−]?\d+(?:[.．]\d+)?[%％]?",
                    "<nobr><color=#F2D58A>$0</color></nobr>");
            var effectLabel = BattleHudKit.Text(panel, "Fx", effect, BattleHudKit.Gothic, 21,
                new Color32(240, 236, 246, 255), TextAlignmentOptions.TopLeft,
                CardPoint(29, 563), CardPoint(421, 466));
            effectLabel.textWrappingMode = TextWrappingModes.Normal;
            effectLabel.enableAutoSizing = true;
            effectLabel.fontSizeMin = 16f;
            effectLabel.fontSizeMax = 21f;
            effectLabel.overflowMode = TextOverflowModes.Ellipsis;

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = frame != null ? frame : hitArea;
            button.onClick.AddListener(() => onClick());
            var colors = button.colors;
            colors.highlightedColor = new Color(1.16f, 1.12f, 1.2f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.82f, 1f);
            button.colors = colors;
            panel.gameObject.AddComponent<RewardCardMotion>();
        }

        Text CreateCardLabel(Transform panel, string name, string value, Font font, int maxSize, TextAnchor alignment,
            float left, float top, float right, float bottom, Color color)
        {
            var text = CreateLabel(panel, name, value, maxSize, alignment,
                CardPoint(left, bottom), CardPoint(right, top), Vector2.zero, Vector2.zero, color);
            text.font = font;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = maxSize;
            BattleUITheme.AddTextShadow(text, new Color(0f, 0f, 0f, 0.9f), new Vector2(1, -1));
            return text;
        }

        static float PlaceCardOrbs(Transform panel, ElementColor[] pattern, string rarity)
        {
            const float right = 424f;
            if (pattern == null || pattern.Length == 0)
                return right;

            // Up to five circles use the approved spacing. Older data with more still shows every color.
            var spacing = Mathf.Min(30f, 150f / pattern.Length);
            var left = right - 18f - spacing * pattern.Length;
            var plate = PlaceCardSprite(panel, "OrbPlate", $"CardOrbPlate_{rarity}",
                CardPoint(left, 448), CardPoint(right, 400));
            if (plate != null)
            {
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 16f;
            }

            var radius = Mathf.Min(13.5f, spacing * 0.45f);
            for (var i = 0; i < pattern.Length; i++)
            {
                var center = left + 9f + spacing * (i + 0.5f);
                PlaceCardSprite(panel, $"Orb_{i}", $"CardOrb_{pattern[i]}",
                    CardPoint(center - radius, 424f + radius), CardPoint(center + radius, 424f - radius));
            }

            return left;
        }

        static Vector2 CardPoint(float x, float y) => new Vector2(x / 450f, 1f - y / 600f);

        static int CardDisplayLevel(SkillDefinition card, RunPartyState party)
        {
            if (card != null && party != null && party.TryGetEquippedLevel(card, out var owned))
                return SkillProgression.ClampLevel(owned);
            return 1;
        }

        static Image PlaceCardSprite(Transform parent, string name, string asset, Vector2 anchorMin, Vector2 anchorMax)
        {
            var sprite = Resources.Load<Sprite>(RewardCardSprites + asset);
            if (sprite == null)
                return null;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        static Color CardAccent(SkillDefinition card)
        {
            if (card == null)
                return BattleUITheme.Arcane;
            if (StarterCharacters.Same(card.characterName, StarterCharacters.Chara01))
                return BattleUITheme.AccentBoss;
            if (StarterCharacters.Same(card.characterName, StarterCharacters.Chara02))
                return BattleUITheme.AccentAlly;
            if (StarterCharacters.Same(card.characterName, StarterCharacters.Chara03))
                return BattleUITheme.Cyan;
            return BattleUITheme.Arcane;
        }

        int AttackFor(SkillDefinition skill, RunPartyState party = null)
        {
            _ = skill;
            _ = party;
            return 0;
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
                "探索者は力尽きた。\n塔は再配置され、解放済みエレベーターから再挑戦できる。\n\n" +
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

            CreateButton(body, "拠点で整備する",
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
            if (_jackpotDebugPanel != null)
                UnityEngine.Object.Destroy(_jackpotDebugPanel);
            _hubPanel = null;
            _rewardPanel = null;
            _loadoutPanel = null;
            _replacePanel = null;
            _selectionPanel = null;
            _jackpotDebugPanel = null;
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

        public GameObject BuildHubPrefabSource(Transform host)
        {
            SuppressPrefab = true;
            try
            {
                var profile = new MetaProgressData
                {
                    arcaneShards = 120,
                    bankedCoins = 40,
                    totalRuns = 3,
                    completedRuns = 1,
                    extractedRuns = 1,
                    bestClearedNodes = 12,
                    lastRunReward = 12,
                    towerRunActive = true,
                    unlockedElevatorFloors = new List<int> { 3 }
                };
                ShowHub(host, profile, null, () => { }, () => { }, "探索を再開", new[] { 3 }, _ => { },
                    () => { }, () => { }, () => { }, () => { }, () => { });
                return _hubPanel;
            }
            finally
            {
                SuppressPrefab = false;
            }
        }

        bool TryBindHub(
            MetaProgressData profile,
            SkillCardPoolDefinition pool,
            Action onStartRun,
            Action onResumeRun,
            string resumeRunLabel,
            IReadOnlyList<int> elevatorFloors,
            Action<int> onStartFromElevatorFloor,
            Action onOpenRelicHub,
            Action onOpenHubShop,
            Action onOpenRepairShop,
            Action onOpenPartyStats,
            Action onOpenJackpotDebug)
        {
            var prefab = Resources.Load<GameObject>("UI/Hub");
            if (prefab == null || _root == null)
                return false;
            _hubPanel = UnityEngine.Object.Instantiate(prefab, _root);
            _hubPanel.name = "ArcaneSanctum";
            var panelRt = _hubPanel.GetComponent<RectTransform>();
            if (panelRt != null)
            {
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
            }

            var whMat = MetaProgression.WarehouseMaterialUnits(profile);
            var whRel = profile?.warehouseRelicIds?.Count ?? 0;
            var totalCards = pool?.cards?.Count(card => card != null) ?? 0;
            var unlockedCards = pool?.cards?.Count(card => card != null && profile != null &&
                profile.IsUnlocked(card.skillId)) ?? 0;
            var elevatorLine = profile?.unlockedElevatorFloors != null && profile.unlockedElevatorFloors.Count > 0
                ? $"解放エレベーター階: {string.Join(", ", profile.unlockedElevatorFloors)}F"
                : "エレベーター未解放";
            var hasElevatorRestart = elevatorFloors != null && elevatorFloors.Count > 0 &&
                                     onStartFromElevatorFloor != null;
            var hasResume = onResumeRun != null && profile != null && profile.towerRunActive &&
                            !string.IsNullOrEmpty(resumeRunLabel);
            SetHubText("Currency", $"◆ 魔石  {profile?.arcaneShards ?? 0}　　貯金 {profile?.bankedCoins ?? 0}");
            SetHubText("Warehouse", $"素材倉庫 {whMat}　レリック倉庫 {whRel} 個");
            SetHubText("Stats",
                $"挑戦回数　{profile?.totalRuns ?? 0}\n\n" +
                $"踏破 / 帰還　{profile?.completedRuns ?? 0} / {profile?.extractedRuns ?? 0}\n\n" +
                $"最高進行　{profile?.bestClearedNodes ?? 0} ノード");
            SetHubText("Progress", $"解放済み  {unlockedCards} / {totalCards}");
            SetHubText("Latest", elevatorLine);
            SetHubText("StartRule",
                profile != null && profile.towerRunActive
                    ? "中断中の探索は、刻印・レリックを持ったまま 1 階か解放済みエレベーター階から再開できる"
                    : "出発時は各キャラの初期スキルのみ。カードは塔内で集める");
            SetHubText("RunCopy",
                profile != null && profile.towerRunActive &&
                (profile.towerRunVitalsRested ||
                 (profile.towerRunNodeId == LayerSquares.Count - 1 && !profile.towerRunElevatorRestConsumed))
                    ? "エレベーターから帰還済み。1階からでも、どのエレベーターからでも HP は満タン、狂気は 0。"
                    : profile != null && profile.towerRunActive
                    ? "中断中の探索がある。拠点で整備してから塔へ戻れる。"
                    : hasElevatorRestart
                        ? "塔は再配置された。解放済みエレベーターから、新しい RUN を始められる。"
                        : "1 階から新しく塔へ入る。");
            var reward = FindNamed(_hubPanel.transform, "LastReward");
            if (reward != null)
            {
                reward.gameObject.SetActive(profile != null && profile.lastRunReward > 0);
                var rewardText = reward.GetComponent<Text>();
                if (rewardText != null && profile != null)
                    rewardText.text = $"前回の獲得魔石  +{profile.lastRunReward}";
            }

            WireHubButton("Relic", null, onOpenRelicHub, onOpenRelicHub != null);
            WireHubButton("Shop", null, onOpenHubShop, onOpenHubShop != null);
            WireHubButton("Repair", null, onOpenRepairShop, onOpenRepairShop != null);
            WireHubButton("PartyStats", null, onOpenPartyStats, onOpenPartyStats != null);
            WireHubButton("Jackpot", null, onOpenJackpotDebug, onOpenJackpotDebug != null);
            WireHubButton("Resume", resumeRunLabel, hasResume ? onResumeRun : null, hasResume);
            WireHubButton("Depart",
                hasResume ? "1 階から再挑戦（踏破記録を保持）" : "ダンジョンへ出発（1 階）",
                onStartRun, true);
            var archive = FindNamed(_hubPanel.transform, "ArchiveButton")?.GetComponent<Button>();
            if (archive != null)
                archive.interactable = false;

            var window = FindNamed(_hubPanel.transform, "SanctumWindow");
            var body = window != null ? window.Find("Body") : FindNamed(_hubPanel.transform, "Body");
            var samples = new List<RectTransform>();
            CollectNamed(body != null ? body : _hubPanel.transform, "Elevator", samples);
            samples.Sort((a, b) => b.anchorMin.y.CompareTo(a.anchorMin.y));
            var originMin = new Vector2(0.5f, 0.22f);
            var originMax = originMin;
            var step = new Vector2(0f, -0.075f);
            var spotPos = Vector2.zero;
            var spotSize = new Vector2(420f, 58f);
            var elevatorParent = body != null ? body : _hubPanel.transform;
            if (samples.Count > 0)
            {
                originMin = samples[0].anchorMin;
                originMax = samples[0].anchorMax;
                spotPos = samples[0].anchoredPosition;
                spotSize = samples[0].sizeDelta;
                elevatorParent = samples[0].parent;
                if (samples.Count > 1)
                    step = samples[1].anchorMin - samples[0].anchorMin;
            }

            foreach (var sample in samples)
                UnityEngine.Object.DestroyImmediate(sample.gameObject);
            if (hasElevatorRestart)
            {
                var ordered = elevatorFloors.OrderBy(floor => floor).ToList();
                for (var i = 0; i < ordered.Count; i++)
                {
                    var floor = ordered[i];
                    var label = profile != null && profile.towerRunActive
                        ? $"エレベーター（{floor} 階）から続ける"
                        : $"エレベーター（{floor} 階）から再入";
                    var button = CreateButton(elevatorParent, label, originMin + step * i, originMax + step * i,
                        spotPos, spotSize, () => onStartFromElevatorFloor(floor));
                    button.name = "Elevator";
                }
            }

            foreach (var text in _hubPanel.GetComponentsInChildren<Text>(true))
            {
                if (text.font == null)
                    text.font = BattleUITheme.FontBold ?? BattleUITheme.FontDisplay;
            }

            foreach (var image in _hubPanel.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                    BattleUITheme.EnsureSprite(image);
            }

            return true;
        }

        void SetHubText(string name, string value)
        {
            var text = FindNamed(_hubPanel.transform, name)?.GetComponent<Text>();
            if (text == null)
                return;
            text.text = value;
            if (text.font == null)
                text.font = BattleUITheme.FontBold ?? BattleUITheme.FontDisplay;
        }

        void WireHubButton(string name, string label, Action action, bool visible)
        {
            var target = FindNamed(_hubPanel.transform, name);
            if (target == null)
                return;
            target.gameObject.SetActive(visible);
            var button = target.GetComponent<Button>();
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            if (action != null)
                button.onClick.AddListener(() => action());
            button.interactable = action != null;
            if (string.IsNullOrEmpty(label))
                return;
            var text = target.Find("Text")?.GetComponent<Text>();
            if (text != null)
                text.text = label;
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static void CollectNamed(Transform root, string name, List<RectTransform> into)
        {
            if (root == null)
                return;
            if (root.name == name && root is RectTransform rect)
                into.Add(rect);
            for (var i = 0; i < root.childCount; i++)
                CollectNamed(root.GetChild(i), name, into);
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
