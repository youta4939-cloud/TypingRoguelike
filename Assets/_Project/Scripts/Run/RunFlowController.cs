using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>塔探索フロー（ダンジョン探索仕様 v1.2）。UI は別系統。</summary>
    public sealed class RunFlowController : MonoBehaviour
    {
        [SerializeField] DungeonRunDefinition dungeonRun;
        [SerializeField] BattleEncounterDefinition fallbackEncounter;

        readonly RunPartyState _party = new();
        readonly RunExplorationInventory _exploration = new();
        readonly RunMadnessState _madness = new();
        readonly DungeonRunState _dungeon = new();
        readonly RunOverlayUI _overlay = new();
        readonly LayerPathUI _pathUi = new();

        BattleController _battle;
        LayerConfig _layerConfig;
        BoardCatalog _boards;
        YakuCatalog _yaku;
        int _runSeed;
        int _clearedNodesThisRun;
        MetaProgressData _profile;
        bool _runResultRecorded;
        bool _defeatHandling;
        int _pendingMaterialId = -1;
        string _pendingTreasureMaterialLog;
        int _pendingTreasureCoins;
        bool _pendingTreasureOffersCard;
        string _pendingTreasureRelicMsg;
        string _pendingTreasureRelicLine;
        bool _awaitingTreasureModal;
        bool _cardRewardMustTake;
        Action _afterCardReward;
        int _merchantRelicSoldNodeId = -1;
        readonly EventScreenUI _events = new();
        EventDefinition _activeEvent;
        EventOutcome _activeOutcome;
        int _effectCursor;
        readonly List<string> _effectNotes = new();
        readonly HashSet<int> _chosenCardIds = new();
        bool _eventStartsBattle;
        bool _eventFromDebug;

        public RunPartyState Party => _party;
        public RunMadnessState Madness => _madness;

        void Awake()
        {
            Debug.Log("[TypingRoguelike] RunFlowController Awake");
            if (dungeonRun == null)
                dungeonRun = Resources.Load<DungeonRunDefinition>("Battle/PrototypeDungeonRun");
            _layerConfig = LayerConfig.LoadOrFallback();
            _boards = BoardCatalog.LoadOrFallback();
            _yaku = YakuCatalog.LoadOrFallback();

            if (fallbackEncounter == null)
                fallbackEncounter = Resources.Load<BattleEncounterDefinition>("Battle/PrototypeCombatEncounter")
                                    ?? Resources.Load<BattleEncounterDefinition>("Battle/PrototypeBossEncounter");

            var partyDef = ResolvePartyDefinition();
            _party.InitializeForNewDungeonRun(partyDef);
            _profile = MetaProgression.Load();
        }

        void Start()
        {
            try
            {
                ShowHub();
                Debug.Log("[TypingRoguelike] 拠点 UI を表示しました。SampleScene で Play しているか確認してください。");
            }
            catch (Exception ex)
            {
                Debug.LogError($"拠点 UI の初期化に失敗: {ex.Message}\n{ex.StackTrace}");
            }
        }

        PartyLoadoutDefinition ResolvePartyDefinition()
        {
            if (dungeonRun?.bossEncounter?.party != null)
                return dungeonRun.bossEncounter.party;
            if (dungeonRun?.combatEncounter?.party != null)
                return dungeonRun.combatEncounter.party;
            return fallbackEncounter?.party;
        }

        /// <summary>拠点の「1 階から」。中断 RUN があるときは踏破記録を維持して同一 RUN を 1 階入口から再開。</summary>
        void BeginRunFromHubFirstFloor()
        {
            if (_profile != null && _profile.towerRunActive)
            {
                StartActiveTowerRunFromFloorOne();
                return;
            }

            BeginNewRun();
        }

        void BeginNewRun()
        {
            _overlay.HideAll();
            _pathUi.Hide();
            DestroyBattleIfAny();
            _runResultRecorded = false;
            _clearedNodesThisRun = 0;
            _exploration.Clear();
            _dungeon.ClearSeenEvents();

            MetaProgression.ClearActiveTowerRun(_profile);
            _profile.revealedTowerNodeIds ??= new List<int>();
            _profile.revealedTowerNodeIds.Clear();
            _profile.clearedTowerNodeIds ??= new List<int>();
            _profile.clearedTowerNodeIds.Clear();
            MetaProgression.Save(_profile);

            var partyDef = ResolvePartyDefinition();
            _party.InitializeForNewDungeonRun(partyDef);

            ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            _madness.ResetForNewRun(ComputeFullMadnessCap());

            _runSeed = _profile.towerSeed;
            OpenBoardPreparation(1, startAtElevator: false);
        }

        int TotalFloors => Mathf.Clamp(_layerConfig != null ? _layerConfig.totalLayers : 15, 1, 99);

        void GenerateFirstFloor() => OpenBoardPreparation(1, startAtElevator: false);

        BoardPreset ResolveBoard(string presetId)
        {
            var found = _boards != null ? _boards.Find(presetId) : null;
            if (found != null)
                return found;
            if (_boards?.presets != null)
            {
                foreach (var preset in _boards.presets)
                {
                    if (preset != null)
                        return preset;
                }
            }

            return null;
        }

        void LoadLayer(int floor, int restoreIndex, bool startAtElevator, BoardPreset board, bool preFourthHealApplied)
        {
            var index = restoreIndex;
            if (index < 0 || index >= LayerSquares.Count)
            {
                Debug.LogWarning($"保存していたマス {restoreIndex} は一本道にない。この層の先頭から始める。");
                index = 0;
            }

            _dungeon.SetFloor(
                floor,
                TotalFloors,
                _runSeed,
                _layerConfig,
                board != null ? board.ToGrid() : null,
                board != null ? board.presetId : "",
                _yaku,
                index,
                _profile.clearedTowerNodeIds,
                startAtElevator,
                preFourthHealApplied);
            RememberClearedSquares();
        }

        void OpenBoardPreparation(int layer, bool startAtElevator)
        {
            var presets = _boards?.presets ?? System.Array.Empty<BoardPreset>();
            var choices = new BoardChoice[presets.Length];
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var grid = preset != null ? preset.ToGrid() : null;
                var reason = "";
                var selectable = preset != null && LayerSquares.MeetsMinimums(grid, _layerConfig, layer, out reason);
                choices[i] = new BoardChoice
                {
                    Name = preset != null && !string.IsNullOrEmpty(preset.displayName) ? preset.displayName : "盤面",
                    Odds = grid != null ? BoardGrid.DescribeOdds(grid) : "",
                    Selectable = selectable,
                    Reason = selectable ? "" : reason
                };
            }

            _pathUi.ShowBoardPreparation(
                transform,
                layer,
                TotalFloors,
                LayerSquares.FixedSquare(layer, _layerConfig),
                LayerSquares.DescribeMinimums(_layerConfig, layer),
                choices,
                index => ConfirmBoard(index >= 0 && index < presets.Length ? presets[index] : null, layer, startAtElevator),
                () => ReelDraw.LogThousandRolls(_boards));
        }

        void ConfirmBoard(BoardPreset board, int layer, bool startAtElevator)
        {
            if (board == null)
                return;

            _profile.clearedTowerNodeIds?.Clear();
            _profile.towerRunPreFourthHealApplied = false;
            LoadLayer(layer, restoreIndex: 0, startAtElevator, board, preFourthHealApplied: startAtElevator);
            if (!startAtElevator)
                GrantFlatYakuCoins();
            if (_profile.towerRunActive)
                PersistActiveRun();
            else
                ActivateAndSaveRun();

            if (!startAtElevator && _dungeon.ActiveYaku != null)
            {
                var yaku = _dungeon.ActiveYaku;
                _pathUi.ShowModal(
                    "役",
                    $"{yaku.displayName}\n{yaku.DescribeEffect()}",
                    new (string, System.Action)[] { ("進む", ShowMap) });
                return;
            }

            ShowMap();
        }

        void GrantFlatYakuCoins()
        {
            var yaku = _dungeon.ActiveYaku;
            if (yaku == null || yaku.bonus != YakuBonusKind.FlatCoins)
                return;
            var amount = Mathf.Max(0, Mathf.RoundToInt(yaku.bonusValue));
            if (amount > 0)
                _party.AddCoins(amount);
        }

        void TryApplyPreFourthHeal()
        {
            var yaku = _dungeon.ActiveYaku;
            if (yaku == null || yaku.bonus != YakuBonusKind.HealBeforeFourth || _dungeon.PreFourthHealApplied)
                return;
            if (_dungeon.Squares == null || _dungeon.Squares.Length < LayerSquares.Count)
                return;
            if (!_dungeon.Squares[2].Cleared || _dungeon.Squares[3].Cleared)
                return;

            _dungeon.MarkPreFourthHealApplied();
            var fraction = yaku.bonusValue <= 0f ? 0.2f : yaku.bonusValue;
            _party.HealFraction(fraction);
            PersistActiveRun();
        }

        void RememberClearedSquares()
        {
            if (_profile == null)
                return;
            _profile.clearedTowerNodeIds ??= new List<int>();
            _profile.clearedTowerNodeIds.Clear();
            if (_dungeon.Squares == null)
                return;
            foreach (var square in _dungeon.Squares)
            {
                if (square.Cleared)
                    _profile.clearedTowerNodeIds.Add(square.Index);
            }
        }

        void ShowMap()
        {
            _pathUi.ReleasePause();
            if (TryBeginRunDeath(fromBattle: false))
                return;

            SyncMadnessCapFromRelics();
            TryApplyPreFourthHeal();
            if (TryPresentLayerExit())
                return;

            _pathUi.Show(transform, _dungeon, _party, _exploration, _madness, OnMapNodeSelected, AbandonRun,
                OpenRunLoadout, OpenRunRelicManage, OpenRunPartyStats);
            EventDebugUI.Ensure(transform, BuildEventDebugHooks());
        }

        bool TryPresentLayerExit()
        {
            if (!_dungeon.LayerComplete)
                return false;

            var last = _dungeon.Squares[LayerSquares.Count - 1];
            if (last.Type == DungeonNodeType.Elevator)
            {
                ShowElevatorChoice();
                return true;
            }

            if (last.Type == DungeonNodeType.Boss && _dungeon.Floor >= TotalFloors)
            {
                CompleteTowerRun();
                return true;
            }

            ShowPreparation();
            return true;
        }

        void ShowPreparation()
        {
            if (_dungeon.Floor >= TotalFloors)
            {
                CompleteTowerRun();
                return;
            }

            OpenBoardPreparation(_dungeon.Floor + 1, startAtElevator: false);
        }

        void ShowSideMap()
        {
        }

        void ShowMapTabHint()
        {
        }

        void ShowRouteMap()
        {
            ShowMap();
        }

        /// <summary>マップイベント等から狂気度を変更。発狂したら true。</summary>
        public bool ApplyMadnessDeltaFromEvent(int delta)
        {
            var insanity = _madness.ApplyDeltaAndCheckInsanity(delta);
            if (insanity)
                TryBeginRunInsanity(fromBattle: false);
            else
            {
                _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
                PersistActiveRun();
            }

            return insanity;
        }

        void ActivateAndSaveRun()
        {
            if (_profile == null)
                return;
            _profile.towerRunActive = true;
            PersistActiveRun();
        }

        void PersistActiveRun()
        {
            if (_profile == null || !_profile.towerRunActive)
                return;

            SyncSeenEventsToProfile();
            _profile.towerRunBoardId = _dungeon.BoardId ?? "";
            _profile.towerRunPreFourthHealApplied = _dungeon.PreFourthHealApplied;
            MetaProgression.SaveActiveTowerRun(
                _profile,
                _runSeed,
                _dungeon.Floor,
                _dungeon.CurrentNodeId,
                _party,
                _exploration,
                _clearedNodesThisRun,
                _madness);
        }

        void RestoreMadnessFromProfile()
        {
            _madness.SetConfiguredMadnessCap(ComputeFullMadnessCap());
            var savedMax = _profile.towerRunMadnessMax > 0
                ? _profile.towerRunMadnessMax
                : _madness.ConfiguredMadnessCap;
            var penalty = Mathf.Max(0, _madness.ConfiguredMadnessCap - savedMax);
            _madness.SetDeviceCapPenalty(penalty);
            _madness.RestoreCurrentAndJackpot(
                _profile.towerRunMadnessCurrent,
                _profile.towerRunBonusJackpotChance,
                _profile.towerRunJackpotAccumulatedChance);
        }

        void SyncJackpotFromBattle()
        {
            if (_battle == null)
                return;

            _madness.SetAccumulatedJackpotChance(_battle.ExportJackpotAccumulatedChance());
        }

        void OpenRunLoadout()
        {
            ShowSideMap();
            _overlay.ShowLoadout(transform, _party, ShowMap);
        }

        void OpenRunPartyStats()
        {
            ShowSideMap();
            _overlay.ShowPartyStats(
                transform,
                _party,
                dungeonRun,
                ResolveRelicCatalog(),
                _exploration,
                "マップに戻る",
                ShowMap);
        }

        void OpenHubPartyStats()
        {
            _overlay.ShowPartyStats(
                transform,
                _party,
                dungeonRun,
                ResolveRelicCatalog(),
                null,
                "聖堂に戻る",
                ShowHub);
        }

        void OpenRunRelicManage()
        {
            ShowSideMap();
            _overlay.ShowRunRelicManage(transform, _exploration, ResolveRelicCatalog(), () =>
            {
                SyncMadnessCapFromRelics();
                PersistActiveRun();
                ShowMap();
            });
        }

        void OpenHubRelicPanel()
        {
            _overlay.ShowHubRelicPanel(transform, _profile, ResolveRelicCatalog(), () =>
            {
                MetaProgression.Save(_profile);
                ShowHub();
            });
        }

        void OpenHubShopDevelopPanel()
        {
            _overlay.ShowHubShopDevelopPanel(transform, _profile, ResolveRelicCatalog(), () =>
            {
                MetaProgression.Save(_profile);
                ShowHub();
            });
        }

        void OpenHubRepairPanel()
        {
            _overlay.ShowHubRepairPanel(transform, _profile, ResolveProxyBrainCatalog(), () =>
            {
                MetaProgression.Save(_profile);
                ShowHub();
            });
        }

        RelicCatalogDefinition ResolveRelicCatalog() =>
            Resources.Load<RelicCatalogDefinition>("Battle/PrototypeRelicCatalog");

        MaterialCatalogDefinition ResolveMaterialCatalog() =>
            Resources.Load<MaterialCatalogDefinition>("Battle/PrototypeMaterialCatalog");

        ProxyBrainUpgradeCatalog ResolveProxyBrainCatalog() =>
            Resources.Load<ProxyBrainUpgradeCatalog>("Run/ProxyBrainUpgradeCatalog");

        float ResolveEncounterBaselineMaxHp()
        {
            var maxHp = dungeonRun?.bossEncounter?.playerMaxHp
                        ?? dungeonRun?.combatEncounter?.playerMaxHp
                        ?? fallbackEncounter?.playerMaxHp
                        ?? 100f;
            return maxHp * 1.2f;
        }

        void ApplyProxyBrainRunMaxHp(bool preserveCurrentHp)
        {
            var baseline = ResolveEncounterBaselineMaxHp();
            var bonus = ProxyBrainUpgrades.ComputePartyMaxHpPercentBonus(_profile, ResolveProxyBrainCatalog());
            var max = baseline * (1f + bonus / 100f);
            if (preserveCurrentHp)
                _party.SetMaxHpPreservingCurrent(max);
            else
                _party.InitRunHp(max);
        }

        int ComputeFullMadnessCap()
        {
            var proxyBonus = ProxyBrainUpgrades.ComputeMadnessBaseBonus(_profile, ResolveProxyBrainCatalog());
            var relicBonus = RelicRunModifiers.Compute(_exploration, ResolveRelicCatalog()).MadnessMaxBonus;
            return RunMadnessState.DefaultMaxMadness + Mathf.Max(0, proxyBonus) + Mathf.Max(0, relicBonus);
        }

        void ShowHub()
        {
            _pathUi.Hide();
            DestroyBattleIfAny();
            // TODO 未決事項6: 「中断した探索へ戻る（N階）」を残すかは未決。残したまま、1階と解放済みエレベーター階も同じ探索の再開にする。
            var resumeLabel = _profile.towerRunActive
                ? $"中断した探索へ戻る（{_profile.towerRunFloor} 階）"
                : null;
            var elevatorFloors = _profile.unlockedElevatorFloors;
            _overlay.ShowHub(
                transform,
                _profile,
                ResolveRewardPool(),
                BeginRunFromHubFirstFloor,
                ResumeActiveTowerRun,
                resumeLabel,
                elevatorFloors,
                BeginNewRunFromElevatorFloor,
                OpenHubRelicPanel,
                OpenHubShopDevelopPanel,
                OpenHubRepairPanel,
                OpenHubPartyStats);
        }

        void BeginNewRunFromElevatorFloor(int floor)
        {
            if (_profile?.unlockedElevatorFloors == null || !_profile.unlockedElevatorFloors.Contains(floor))
            {
                Debug.LogWarning($"未解放のエレベーター階: {floor}");
                return;
            }

            if (_profile.towerRunActive)
            {
                ContinueActiveRun(floor, fromElevator: true);
                return;
            }

            _overlay.HideAll();
            _pathUi.Hide();
            DestroyBattleIfAny();
            _runResultRecorded = false;
            _clearedNodesThisRun = 0;
            _exploration.Clear();
            _dungeon.ClearSeenEvents();

            MetaProgression.ClearActiveTowerRun(_profile);
            _profile.revealedTowerNodeIds ??= new List<int>();
            _profile.revealedTowerNodeIds.Clear();
            _profile.clearedTowerNodeIds ??= new List<int>();
            _profile.clearedTowerNodeIds.Clear();
            MetaProgression.Save(_profile);

            var partyDef = ResolvePartyDefinition();
            _party.InitializeForNewDungeonRun(partyDef);

            ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            _madness.ResetForNewRun(ComputeFullMadnessCap());

            _runSeed = _profile.towerSeed;
            if (LayerSquares.FixedSquare(floor, _layerConfig) != DungeonNodeType.Elevator)
            {
                Debug.LogWarning($"階 {floor} の4マス目がエレベーターではない。1 層から開始します。");
                OpenBoardPreparation(1, startAtElevator: false);
                return;
            }

            OpenBoardPreparation(floor, startAtElevator: true);
        }

        void ResumeActiveTowerRun()
        {
            if (_profile == null || !_profile.towerRunActive)
            {
                BeginNewRun();
                return;
            }

            RestoreActiveRunState();
            if (TryStartPendingEventBattle())
                return;
            LoadLayer(
                _profile.towerRunFloor,
                _profile.towerRunNodeId,
                startAtElevator: false,
                ResolveBoard(_profile.towerRunBoardId),
                _profile.towerRunPreFourthHealApplied);
            ShowMap();
        }

        void StartActiveTowerRunFromFloorOne()
        {
            if (_profile == null || !_profile.towerRunActive)
            {
                BeginNewRun();
                return;
            }

            ContinueActiveRun(1, fromElevator: false);
        }

        /// <summary>中断中の探索の刻印・レリック・HP・狂気・塔片を戻す。階の配置は呼び出し側。</summary>
        void RestoreActiveRunState()
        {
            _overlay.HideAll();
            _pathUi.Hide();
            DestroyBattleIfAny();
            _runResultRecorded = false;
            _clearedNodesThisRun = _profile.towerRunClearedNodes;

            _runSeed = _profile.towerRunSeed != 0 ? _profile.towerRunSeed : _profile.towerSeed;

            var partyDef = ResolvePartyDefinition();
            _party.InitializeForNewDungeonRun(partyDef);
            _party.ApplyMemberSaves(_profile.towerRunMembers, GetSkillCatalog());
            _party.AddCoins(_profile.towerRunCoins);

            ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            if (_profile.towerRunCurrentHp > 0f)
                _party.SetCurrentHp(_profile.towerRunCurrentHp);
            RestoreMadnessFromProfile();

            _profile.towerRunMaterialStacks ??= new List<MaterialStackSave>();
            _exploration.RestoreFromSave(
                _profile.towerRunEquippedRelicIds,
                _profile.towerRunBackpackRelicIds,
                _profile.towerRunMaterialStacks);
            if (_exploration.MaterialCount == 0 && _profile.towerRunMaterials > 0)
                _exploration.RestoreLegacyMaterialCount(_profile.towerRunMaterials);
            _dungeon.ReplaceSeenEvents(_profile.towerRunSeenEventIds);
        }

        void ContinueActiveRun(int floor, bool fromElevator)
        {
            RestoreActiveRunState();
            if (TryStartPendingEventBattle())
                return;
            if (fromElevator)
            {
                if (LayerSquares.FixedSquare(floor, _layerConfig) != DungeonNodeType.Elevator)
                {
                    Debug.LogWarning($"階 {floor} の4マス目がエレベーターではない。1 層から開始します。");
                    OpenBoardPreparation(1, startAtElevator: false);
                    return;
                }

                if (string.IsNullOrEmpty(_profile.towerRunBoardId) || floor != _profile.towerRunFloor)
                {
                    OpenBoardPreparation(floor, startAtElevator: true);
                    return;
                }

                LoadLayer(floor, restoreIndex: 0, startAtElevator: true, ResolveBoard(_profile.towerRunBoardId), preFourthHealApplied: true);
            }
            else
            {
                LoadLayer(
                    floor,
                    restoreIndex: 0,
                    startAtElevator: false,
                    ResolveBoard(_profile.towerRunBoardId),
                    _profile.towerRunPreFourthHealApplied);
            }

            MetaProgression.SaveActiveTowerRun(
                _profile,
                _runSeed,
                _dungeon.Floor,
                _dungeon.CurrentNodeId,
                _party,
                _exploration,
                _clearedNodesThisRun,
                _madness);
            ShowMap();
        }

        SkillDefinition[] GetSkillCatalog()
        {
            var pool = ResolveRewardPool();
            return pool?.cards ?? Array.Empty<SkillDefinition>();
        }

        void AbandonRun()
        {
            _pathUi.ShowModal(
                "探索を放棄しますか？",
                "放棄すると全滅と同じ扱いになる（刻印・レリック・塔片を失う）。\n拠点へ戻るならエレベーターを使う。",
                new (string, Action)[]
                {
                    ("放棄する", ConfirmAbandonRun),
                    ("探索を続ける", null)
                });
        }

        void ConfirmAbandonRun()
        {
            ApplyWipe();
            _pathUi.Hide();
            ShowHub();
        }

        void OnMapNodeSelected(int nodeId)
        {
            if (!_dungeon.CanEnter(nodeId))
                return;

            _dungeon.MoveTo(nodeId);
            var node = _dungeon.Current;
            if (node == null)
                return;
            _pathUi.Hide();

            switch (node.Type)
            {
                case DungeonNodeType.Combat:
                    StartGuaranteedCombat(node);
                    break;
                case DungeonNodeType.MidBoss:
                    StartLegacyBattleNode(node, forceElite: true);
                    break;
                case DungeonNodeType.Boss:
                    StartBattleForNode(node, ResolveBossEncounter(), BuildBossMods());
                    break;
                case DungeonNodeType.Rest:
                    ResolveRestNode();
                    break;
                case DungeonNodeType.Treasure:
                    ResolveTreasureNode();
                    break;
                case DungeonNodeType.Event:
                    BeginEvent(null, fromDebug: false);
                    break;
                case DungeonNodeType.Trap:
                    ResolveTrapNode();
                    break;
                case DungeonNodeType.Merchant:
                    ResolveMerchantNode();
                    break;
                case DungeonNodeType.Elevator:
                    ResolveElevatorNode();
                    break;
                default:
                    CompleteNodeAndReturnToMap();
                    break;
            }
        }

        void ResolveTrapNode()
        {
            var fraction = _layerConfig != null ? _layerConfig.trapHpLossFraction : 0.1f;
            var loss = Mathf.Max(1, Mathf.RoundToInt(_party.MaxHp * Mathf.Clamp01(fraction)));
            _party.ApplyHpDelta(-loss);
            PersistActiveRun();
            if (_party.IsRunDead)
            {
                TryBeginRunDeath(fromBattle: false);
                return;
            }

            CompleteNodeAndReturnToMap("罠", $"罠が作動した。HP が {loss} 減った。\n{FormatHpLine()}");
        }

        void ResolveEmptyEventNode()
        {
            MarkCurrentCleared();
            ShowMap();
            var reason = EventDatabase.All.Count == 0
                ? "出来事の定義が1件も読み込めていない。"
                : "条件を満たす出来事が無かった。";
            _pathUi.ShowModal(
                "イベント",
                reason,
                new (string, Action)[] { ("マップに戻る", null) });
            _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
        }

        void ResolveNormalNode()
        {
            var current = _dungeon.Current;
            Debug.Log($"ResolveNormalNode type={current?.Type} id={current?.Id}");
            MarkCurrentCleared();
            ShowMap();
            _pathUi.ShowModal(
                "探索",
                "静かな通路を進んだ。",
                new (string, Action)[] { ("マップに戻る", null) });
            _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
        }

        /// <summary>戦闘マスは踏んだら必ず戦闘。</summary>
        void StartGuaranteedCombat(LayerSquare node)
        {
            StartLegacyBattleNode(node, forceElite: false);
        }

        /// <summary>v1.2 以前の強敵マス互換。戦闘マスもここから入る。</summary>
        void StartLegacyBattleNode(LayerSquare node, bool forceElite)
        {
            _pathUi.Hide();
            StartBattleForNode(
                node,
                forceElite ? ResolveEliteEncounter() : ResolveCombatEncounter(),
                forceElite ? BuildEliteMods() : BuildCombatMods());
        }

        void ResolveRestNode()
        {
            var frac = dungeonRun != null ? dungeonRun.restHealFraction : 0.3f;
            _party.HealFraction(frac);
            var madnessRecovery = ProxyBrainUpgrades.ComputeRestMadnessRecovery(_profile, ResolveProxyBrainCatalog());
            var madnessLine = string.Empty;
            if (madnessRecovery > 0 && _madness.CurrentMadness > 0)
            {
                var before = _madness.CurrentMadness;
                _madness.ApplyDelta(-madnessRecovery);
                var reduced = before - _madness.CurrentMadness;
                if (reduced > 0)
                    madnessLine = $"\n狂気が {reduced} 静まった。（{_madness.CurrentMadness} / {_madness.MaxMadness}）";
            }

            MarkCurrentCleared();
            ShowMapTabHint();
            _pathUi.ShowModal(
                "休息",
                $"焚き火で休んだ。HP が最大の {(int)(frac * 100)}% 回復した。{madnessLine}\n{FormatHpLine()}",
                new (string, Action)[] { ("マップに戻る", ShowRouteMap) });
            _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
        }

        void ResolveTreasureNode()
        {
            var coins = _dungeon.ScaleCoinGain(dungeonRun != null ? dungeonRun.treasureCoinReward : 10);
            var offersCard = dungeonRun == null || dungeonRun.treasureOffersCard;
            var matMin = dungeonRun != null ? dungeonRun.treasureMaterialMin : 1;
            var matMax = dungeonRun != null ? Mathf.Max(matMin, dungeonRun.treasureMaterialMax) : 2;
            var materialRolls = UnityEngine.Random.Range(matMin, matMax + 1);
            _party.AddCoins(coins);
            _pendingTreasureCoins = coins;
            _pendingTreasureOffersCard = offersCard;
            _pendingTreasureRelicLine = TryRollTreasureRelic(out _pendingTreasureRelicMsg);
            _awaitingTreasureModal = true;
            _pendingTreasureMaterialLog = GrantFloorMaterials(materialRolls);
            if (_pendingMaterialId >= 0)
                return;

            ShowTreasureRewardModal();
        }

        void ShowTreasureRewardModal()
        {
            _awaitingTreasureModal = false;
            ShowMap();
            var lootLines =
                $"塔片 +{_pendingTreasureCoins}\n{_pendingTreasureMaterialLog}{_pendingTreasureRelicLine}\n{FormatCoinLine()}";

            if (!_pendingTreasureOffersCard)
            {
                MarkCurrentCleared();
                _pathUi.ShowModal(
                    "宝箱",
                    $"古い財貨を見つけた。\n{lootLines}{_pendingTreasureRelicMsg}",
                    new (string, Action)[] { ("マップに戻る", null) });
                _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
                return;
            }

            _pathUi.ShowModal(
                "宝箱",
                $"古い財貨と刻印の残片を見つけた。\n{lootLines}{_pendingTreasureRelicMsg}\n\n刻印を1枚選べる。",
                new (string, Action)[]
                {
                    ("刻印を選ぶ", () => OfferCardReward()),
                    ("塔片だけ持ち帰る", CompleteNodeAndReturnToMap)
                });
            _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
        }

        string TryRollTreasureRelic(out string modalExtra)
        {
            modalExtra = string.Empty;
            return string.Empty;
        }

        void ResolveEventNode()
        {
            var nodeId = _dungeon.Floor * 10 + (_dungeon.Current?.Id ?? 0);
            var relic = PickOfferedRelic(nodeId, 901);
            if (relic == null)
            {
                ResolveNormalNode();
                return;
            }

            ShowMap();
            _pathUi.ShowModal(
                "出来事",
                $"床に古い遺物が落ちている。\n\n「{relic.displayName}」\n{relic.effectText}\n\n装備 {CountEquippedRelics()} / 3",
                new (string, Action)[]
                {
                    ("拾う", () => PromptRelicEquip(
                        relic,
                        0,
                        () => CompleteNodeAndReturnToMap(
                            "遺物",
                            $"「{relic.displayName}」を装備した。\n{relic.effectText}\n\n装備 {CountEquippedRelics()} / 3"),
                        ResolveEventNode)),
                    ("見送る", CompleteNodeAndReturnToMap)
                });
        }

        void ResolveMerchantNode()
        {
            var healCost = dungeonRun != null ? dungeonRun.merchantHealCost : 10;
            var relicCost = dungeonRun != null ? dungeonRun.merchantRelicCost : 60;
            var healPct = (int)((dungeonRun != null ? dungeonRun.merchantHealFraction : 0.12f) * 100f);
            var nodeId = _dungeon.Floor * 10 + (_dungeon.Current?.Id ?? 0);
            var relic = MerchantOffersRelic(nodeId) ? PickOfferedRelic(nodeId, 17) : null;
            var body =
                $"旅の商人が荷を広げている。\n{FormatCoinLine()}\n装備 {CountEquippedRelics()} / 3\n\n" +
                $"・体力回復（最大HPの{healPct}%）… {healCost} 塔片";
            if (relic != null)
                body += $"\n・レリック「{relic.displayName}」… {relicCost} 塔片\n　{relic.effectText}";

            var actions = new List<(string, Action)>
            {
                ($"体力回復（{healCost}塔片）", () => TryMerchantHeal(healCost))
            };
            if (relic != null)
            {
                var offer = relic;
                actions.Add(($"レリック購入（{relicCost}塔片）", () => PromptRelicEquip(
                    offer,
                    relicCost,
                    () =>
                    {
                        _merchantRelicSoldNodeId = nodeId;
                        ShowMapTabHint();
                        _pathUi.ShowModal(
                            "取引完了",
                            $"「{offer.displayName}」を装備した。\n{offer.effectText}\n{FormatCoinLine()}",
                            new (string, Action)[]
                            {
                                ("店に戻る", ResolveMerchantNode),
                                ("立ち去る", CompleteNodeAndReturnToMap)
                            });
                    },
                    ResolveMerchantNode)));
            }

            actions.Add(("立ち去る", CompleteNodeAndReturnToMap));
            ShowMapTabHint();
            _pathUi.ShowModal("行商人", body, actions);
        }

        bool MerchantOffersRelic(int nodeId)
        {
            if (_merchantRelicSoldNodeId == nodeId)
                return false;
            var chance = dungeonRun != null ? dungeonRun.merchantRelicOfferChance : 0.5f;
            if (chance <= 0f)
                return false;
            return StableUnit(nodeId, 17) < chance;
        }

        RelicDefinition PickOfferedRelic(int nodeId, int salt)
        {
            var catalog = ResolveRelicCatalog();
            if (catalog?.relics == null)
                return null;
            var list = catalog.relics.Where(r => r != null).ToArray();
            if (list.Length == 0)
                return null;

            unchecked
            {
                var h = _runSeed * 73856093 ^ nodeId * 19349663 ^ salt * 83492791;
                var index = (int)((uint)h % (uint)list.Length);
                return list[index];
            }
        }

        float StableUnit(int nodeId, int salt)
        {
            unchecked
            {
                var h = _runSeed * 73856093 ^ nodeId * 19349663 ^ salt * 83492791;
                return (uint)h / (float)uint.MaxValue;
            }
        }

        void PromptRelicEquip(RelicDefinition relic, int cost, Action onEquipped, Action onCancel)
        {
            if (relic == null)
            {
                onCancel?.Invoke();
                return;
            }

            var catalog = ResolveRelicCatalog();
            if (relic.exclusiveEquip && _exploration.IsRelicEquipped(relic.relicId))
            {
                _pathUi.ShowModal(
                    "レリック",
                    $"「{relic.displayName}」はすでに装備している。",
                    new (string, Action)[] { ("戻る", onCancel) });
                return;
            }

            if (relic.exclusiveEquip && _exploration.EquippedRelicCount() > 0)
            {
                _pathUi.ShowModal(
                    "単独装備",
                    $"「{relic.displayName}」\n{relic.effectText}\n\n" +
                    "これ以外のレリックは装備できない。\n今の装備を外して替える？\n" +
                    $"{DescribeEquippedRelics()}\n外したレリックはこの探索では失われる。",
                    new (string, Action)[]
                    {
                        ("外して装備", () => CommitRelicEquip(relic, cost, true, 0, onEquipped, onCancel)),
                        ("やめる", onCancel)
                    });
                return;
            }

            if (RelicRunModifiers.Compute(_exploration, catalog).ExclusiveEquipped)
            {
                var blocker = ExclusiveRelicName(catalog);
                _pathUi.ShowModal(
                    "装備できない",
                    $"「{blocker}」を装備中は、他のレリックを持てない。\n" +
                    $"入手「{relic.displayName}」\n{relic.effectText}\n\n" +
                    $"{DescribeEquippedRelics()}\n外して替えると、外したレリックはこの探索では失われる。",
                    new (string, Action)[]
                    {
                        ("替えて装備", () => CommitRelicEquip(relic, cost, true, 0, onEquipped, onCancel)),
                        ("やめる", onCancel)
                    });
                return;
            }

            var empty = _exploration.FindEmptyRelicSlot();
            if (empty >= 0)
            {
                CommitRelicEquip(relic, cost, false, empty, onEquipped, onCancel);
                return;
            }

            var actions = new List<(string, Action)>();
            for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
            {
                var slot = i;
                var id = _exploration.GetEquippedRelicId(slot);
                var name = catalog?.FindById(id)?.displayName ?? "空";
                actions.Add(($"枠{slot + 1}「{name}」と入替", () =>
                    CommitRelicEquip(relic, cost, false, slot, onEquipped, onCancel)));
            }

            actions.Add(("やめる", onCancel));
            _pathUi.ShowModal(
                "装備枠がいっぱい",
                $"入手「{relic.displayName}」\n{relic.effectText}\n\n" +
                $"外す相手（この探索では失われる）\n{DescribeEquippedRelics()}\n{FormatCoinLine()}",
                actions);
        }

        void CommitRelicEquip(
            RelicDefinition relic,
            int cost,
            bool clearOthers,
            int slot,
            Action onEquipped,
            Action onCancel)
        {
            if (cost > 0 && _party.RunCoins < cost)
            {
                _pathUi.ShowModal(
                    "塔片が足りない",
                    $"必要 {cost} 塔片 / 所持 {_party.RunCoins} 塔片",
                    new (string, Action)[] { ("戻る", onCancel) });
                return;
            }

            if (cost > 0 && !_party.TrySpendCoins(cost))
            {
                onCancel?.Invoke();
                return;
            }

            if (clearOthers)
                _exploration.DiscardAllEquippedRelics();
            _exploration.SetEquippedRelic(slot, relic.relicId);
            OnRelicsChanged();
            onEquipped?.Invoke();
        }

        string ExclusiveRelicName(RelicCatalogDefinition catalog)
        {
            for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
            {
                var def = catalog?.FindById(_exploration.GetEquippedRelicId(i));
                if (def != null && def.exclusiveEquip)
                    return def.displayName;
            }

            return "単独装備のレリック";
        }

        string DescribeEquippedRelics()
        {
            var catalog = ResolveRelicCatalog();
            var lines = new List<string>();
            for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
            {
                var def = catalog?.FindById(_exploration.GetEquippedRelicId(i));
                lines.Add(def == null
                    ? $"枠{i + 1} （空）"
                    : $"枠{i + 1} {def.displayName}：{def.effectText}");
            }

            return string.Join("\n", lines);
        }

        void OnRelicsChanged()
        {
            SyncMadnessCapFromRelics();
            PersistActiveRun();
        }

        void SyncMadnessCapFromRelics()
        {
            var penalty = _madness.DeviceCapPenalty;
            _madness.SetConfiguredMadnessCap(ComputeFullMadnessCap());
            _madness.SetDeviceCapPenalty(penalty);
        }

        void TryMerchantHeal(int cost)
        {
            if (!_party.TrySpendCoins(cost))
            {
                ShowInsufficientCoins(cost);
                return;
            }

            var frac = dungeonRun != null ? dungeonRun.merchantHealFraction : 0.12f;
            _party.HealFraction(frac);
            MarkCurrentCleared();
            ShowMapTabHint();
            _pathUi.ShowModal(
                "取引完了",
                $"ポーションを飲んだ。\n{FormatHpLine()}\n{FormatCoinLine()}",
                new (string, Action)[] { ("マップに戻る", ShowRouteMap) });
            _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
        }

        void ShowInsufficientCoins(int cost)
        {
            ShowMapTabHint();
            _pathUi.ShowModal(
                "塔片が足りない",
                $"必要 {cost} 塔片 / 所持 {_party.RunCoins} 塔片",
                new (string, Action)[] { ("店に戻る", ResolveMerchantNode) });
        }

        void ResolveElevatorNode()
        {
            MarkCurrentCleared();
            MetaProgression.UnlockElevatorFloor(_profile, _dungeon.Floor);
            PersistActiveRun();
            ShowElevatorChoice();
        }

        void ShowElevatorChoice()
        {
            _pathUi.ShowModal(
                "エレベーター",
                $"この層はここまで。\n現在 {_dungeon.Floor} / {TotalFloors} 層。\n\n拠点へ帰還するか、次の層へ進む。",
                new (string, Action)[]
                {
                    ("拠点へ帰還", ReturnToHubFromElevator),
                    ("次の層へ", ShowPreparation)
                });
        }

        void ReturnToHubFromElevator()
        {
            _overlay.HideAll();
            DestroyBattleIfAny();
            _madness.ClearDeviceCapPenalty();
            SyncMadnessCapFromRelics();
            // TODO 未決事項5: 素材は倉庫へ移さず、探索の所持のまま保存する。
            MetaProgression.BankRunCoins(_profile, _party);
            _party.ClearRunCoins();
            MetaProgression.SaveActiveTowerRun(
                _profile,
                _runSeed,
                _dungeon.Floor,
                _dungeon.CurrentNodeId,
                _party,
                _exploration,
                _clearedNodesThisRun,
                _madness);
            ShowHub();
        }

        string FormatHpLine() => $"現在 HP {(int)_party.CurrentHp} / {(int)_party.MaxHp}";
        string FormatCoinLine() => $"所持塔片　{_party.RunCoins}";
        string FormatRunLootLine()
        {
            var mat = _exploration.Materials.FormatSummary(ResolveMaterialCatalog());
            return $"素材 [{mat}]　レリック装備 {CountEquippedRelics()} / 3";
        }

        string GrantFloorMaterials(int rolls)
        {
            if (rolls <= 0)
                return string.Empty;

            var catalog = ResolveMaterialCatalog();
            var rng = new System.Random(unchecked(_runSeed + _dungeon.Floor * 9176 + rolls));
            var lines = new List<string>();
            for (var i = 0; i < rolls; i++)
            {
                var def = catalog?.PickForFloor(_dungeon.Floor, rng);
                if (def == null)
                    continue;
                TryGrantMaterial(def.materialId, def.displayName, lines);
            }

            return lines.Count > 0 ? string.Join("\n", lines) + "\n" : "素材: 取得なし\n";
        }

        void TryGrantMaterial(int materialId, string displayName, List<string> logLines)
        {
            var result = _exploration.TryAddMaterial(materialId);
            switch (result)
            {
                case MaterialBackpack.TryAddResult.Added:
                    logLines?.Add($"素材 +1 ({displayName})");
                    return;
                case MaterialBackpack.TryAddResult.NeedDiscard:
                    _pendingMaterialId = materialId;
                    PromptMaterialDiscard(displayName, logLines);
                    return;
                case MaterialBackpack.TryAddResult.Invalid:
                    logLines?.Add($"素材 ({displayName}) はこれ以上持てない。");
                    return;
            }
        }

        void PromptMaterialDiscard(string displayName, List<string> logLines)
        {
            var catalog = ResolveMaterialCatalog();
            var stacks = _exploration.Materials.Stacks;
            var actions = new List<(string, Action)>();
            for (var i = 0; i < stacks.Count; i++)
            {
                var index = i;
                var stack = stacks[i];
                var name = catalog?.FindById(stack.materialId)?.displayName ?? $"#{stack.materialId}";
                actions.Add(($"「{name}×{stack.count}」を捨てる", () =>
                {
                    if (_exploration.Materials.ReplaceSlotAndAdd(index, _pendingMaterialId))
                        _pendingTreasureMaterialLog += $"素材入替: {displayName} を獲得\n";
                    _pendingMaterialId = -1;
                    if (_awaitingTreasureModal)
                        ShowTreasureRewardModal();
                    else
                        CompleteNodeAndReturnToMap();
                }));
            }

            actions.Add(("今回は拾わない", () =>
            {
                _pendingMaterialId = -1;
                if (_awaitingTreasureModal)
                    ShowTreasureRewardModal();
                else
                    CompleteNodeAndReturnToMap();
            }));

            ShowMap();
            _pathUi.ShowModal(
                "バックパック満杯",
                $"素材枠が6種類に達している。\n「{displayName}」を入れるには何かを捨てる。",
                actions);
        }
        int CountEquippedRelics()
        {
            var n = 0;
            for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
            {
                if (_exploration.GetEquippedRelicId(i) >= 0)
                    n++;
            }

            return n;
        }

        void StartBattleForNode(LayerSquare node, BattleEncounterDefinition enc, EncounterBattleModifiers mods)
        {
            if (enc == null)
            {
                Debug.LogWarning("エンカウント定義がありません。");
                ClearPendingEventBattle();
                CompleteNodeAndReturnToMap();
                return;
            }

            _pathUi.Hide();
            SyncMadnessCapFromRelics();
            ApplyRelicBattleModifiers(ref mods);
            ApplyFloorStrengthBand(ref mods);
            DestroyBattleIfAny();
            var root = new GameObject("BattleRoot");
            root.transform.SetParent(transform, false);
            _battle = root.AddComponent<BattleController>();
            try
            {
                _battle.Initialize(
                    enc,
                    _party,
                    _madness,
                    OnBattleVictory,
                    OnBattleDefeat,
                    OnBattleInsanity,
                    mods,
                    PartyCombatStats.Compute(_party, dungeonRun));
            }
            catch (Exception ex)
            {
                Debug.LogError($"戦闘 UI の初期化に失敗: {ex.Message}\n{ex.StackTrace}");
                ClearPendingEventBattle();
                CompleteNodeAndReturnToMap();
            }
        }

        void ApplyRelicBattleModifiers(ref EncounterBattleModifiers mods)
        {
            var relic = RelicRunModifiers.Compute(_exploration, ResolveRelicCatalog());
            if (relic.PlayerDamageBonusPercent > 0)
                mods.PlayerDamageMultiplier = 1f + relic.PlayerDamageBonusPercent / 100f;
            mods.JackpotMadnessThreshold = relic.JackpotMadnessThreshold;
            mods.JackpotChanceBonus = relic.JackpotChanceBonus;
            mods.JackpotDamageMultiplier = relic.JackpotDamageMultiplier;
            mods.OpeningEnemyStopSeconds = relic.OpeningEnemyStopSeconds;
        }

        /// <summary>5層ごとに敵HP・攻撃を一段上げる。1–5層は基準、6–10層、11–15層。</summary>
        void ApplyFloorStrengthBand(ref EncounterBattleModifiers mods)
        {
            var floor = _dungeon != null ? _dungeon.Floor : 1;
            var band = Mathf.Max(0, (floor - 1) / 5);
            if (band <= 0)
                return;

            mods.EnemyHpMultiplier *= 1f + band * 0.45f;
            mods.EnemyDamageMultiplier *= 1f + band * 0.3f;
        }

        EncounterBattleModifiers BuildCombatMods() => new()
        {
            EnemyHpMultiplier = dungeonRun != null ? dungeonRun.combatEnemyHpMultiplier : 0.72f,
            EnemyDamageMultiplier = 0.62f,
            EnemySpeedMultiplier = 0.88f
        };

        EncounterBattleModifiers BuildEliteMods()
        {
            var hp = dungeonRun != null ? dungeonRun.combatEnemyHpMultiplier : 0.72f;
            var eliteHpScale = dungeonRun?.eliteEnemyHpMultiplier ?? 1.35f;
            return new EncounterBattleModifiers
            {
                EnemyHpMultiplier = hp * Mathf.Clamp(eliteHpScale * 0.55f, 0.85f, 1.15f),
                EnemyDamageMultiplier = Mathf.Max(0.55f, (dungeonRun?.eliteEnemyDamageMultiplier ?? 1.2f) * 0.62f),
                EnemySpeedMultiplier = 0.88f,
                StartLogMessage = "💀 強敵が道を塞いでいる！"
            };
        }

        static EncounterBattleModifiers BuildBossMods() => new()
        {
            EnemyHpMultiplier = 0.9f,
            EnemyDamageMultiplier = 0.68f,
            EnemySpeedMultiplier = 0.88f,
            StartLogMessage = "🔥 塔の主が姿を現した！"
        };

        BattleEncounterDefinition ResolveCombatEncounter() =>
            dungeonRun?.combatEncounter ?? fallbackEncounter;

        BattleEncounterDefinition ResolveEliteEncounter() =>
            dungeonRun?.eliteEncounter ?? dungeonRun?.combatEncounter ?? fallbackEncounter;

        BattleEncounterDefinition ResolveBossEncounter() =>
            dungeonRun?.bossEncounter ?? fallbackEncounter;

        void OnBattleVictory()
        {
            if (_party.IsRunDead)
            {
                OnBattleDefeat();
                return;
            }

            _battle?.SetRunMenuOpen(true);
            if (IsTowerBossVictory())
            {
                MarkCurrentCleared();
                StartCoroutine(BossVictoryRoutine());
                return;
            }

            var coins = GrantCoinsForCurrentBattle();
            _party.AddCoins(coins);
            StartCoroutine(CombatVictoryRoutine(coins));
        }

        IEnumerator BossVictoryRoutine()
        {
            yield return null;
            _overlay.HideAll();
            SyncJackpotFromBattle();
            DestroyBattleIfAny();
            ClearPendingEventBattle();
            PersistActiveRun();
            OfferCardReward(
                mustTake: true,
                notice: "塔の主を倒した。刻印を1枚選ぶ。",
                onFinished: () => StartCoroutine(FinishBossVictory()));
        }

        int GrantCoinsForCurrentBattle()
        {
            var cur = _dungeon.Current;
            if (cur == null)
                return dungeonRun != null ? dungeonRun.combatCoinReward : 8;

            if (cur.Type is DungeonNodeType.Combat or DungeonNodeType.MidBoss)
            {
                var elite = cur.Type == DungeonNodeType.MidBoss;
                var reward = dungeonRun != null
                    ? elite ? dungeonRun.eliteCoinReward : dungeonRun.combatCoinReward
                    : elite ? 15 : 8;
                return _dungeon.ScaleCoinGain(reward);
            }

            return _dungeon.ScaleCoinGain(dungeonRun != null ? dungeonRun.combatCoinReward : 8);
        }

        IEnumerator CombatVictoryRoutine(int coins)
        {
            yield return null;
            _overlay.HideAll();
            SyncJackpotFromBattle();
            DestroyBattleIfAny();
            ClearPendingEventBattle();
            MarkCurrentCleared();
            PersistActiveRun();
            OfferCardReward(
                mustTake: true,
                notice: $"戦闘に勝った。塔片 +{coins}。刻印を1枚選ぶ。");
        }

        bool IsTowerBossVictory()
        {
            var cur = _dungeon.Current;
            return cur != null && cur.Type == DungeonNodeType.Boss;
        }

        IEnumerator FinishBossVictory()
        {
            yield return null;
            _overlay.HideAll();
            DestroyBattleIfAny();
            ShowPreparation();
        }

        void CompleteTowerRun()
        {
            ArchiveSeenEvents();
            // TODO 未決事項2: 最終階踏破の魔石報酬は未決。RecordRunResult(true) の呼び出しは変えない。
            _overlay.HideAll();
            DestroyBattleIfAny();
            MetaProgression.ClearActiveTowerRun(_profile);
            _profile.revealedTowerNodeIds ??= new List<int>();
            _profile.revealedTowerNodeIds.Clear();
            _profile.clearedTowerNodeIds ??= new List<int>();
            _profile.clearedTowerNodeIds.Clear();
            MetaProgression.RollTowerSeed(_profile);
            MetaProgression.Save(_profile);
            RecordRunResult(true);
            _pathUi.Hide();
            ShowHub();
        }

        void OnBattleDefeat()
        {
            TryBeginRunDeath(fromBattle: true);
        }

        void OnBattleInsanity()
        {
            TryBeginRunInsanity(fromBattle: true);
        }

        bool TryBeginRunDeath(bool fromBattle)
        {
            if (!_party.IsRunDead)
                return false;
            if (_defeatHandling)
                return true;

            StartCoroutine(HandleDefeat(fromBattle, fromInsanity: false));
            return true;
        }

        bool TryBeginRunInsanity(bool fromBattle)
        {
            if (_defeatHandling)
                return true;

            StartCoroutine(HandleDefeat(fromBattle, fromInsanity: true));
            return true;
        }

        /// <summary>全滅。刻印・レリック・塔片を失い、マップを作り直す。魔石は増えない。</summary>
        void ApplyWipe()
        {
            ArchiveSeenEvents();
            // TODO 未決事項5: 素材も探索インベントリごと消える。全滅で素材を失うかは未決。
            _exploration.Clear();
            _party.ResetAcquiredSkillCards();
            _party.ClearRunCoins();
            _madness.ResetForNewRun(ComputeFullMadnessCap());
            MetaProgression.ApplyDeathReset(_profile);
            RecordRunResult(false);
        }

        IEnumerator HandleDefeat(bool fromBattle, bool fromInsanity)
        {
            if (_defeatHandling)
                yield break;

            _defeatHandling = true;
            _events.Hide();
            _activeEvent = null;
            _activeOutcome = null;
            if (fromBattle)
                yield return new WaitForSecondsRealtime(1.2f);

            _overlay.HideAll();
            DestroyBattleIfAny();
            ApplyWipe();
            _pathUi.Hide();
            ShowHub();
            _overlay.ShowDeathNotice(transform, 0, fromInsanity ? "狂気が限界に達し、探索は終わった。" : null);
            _defeatHandling = false;
        }

        void OfferCardReward(bool mustTake = false, string notice = null, Action onFinished = null)
        {
            _cardRewardMustTake = mustTake;
            _afterCardReward = onFinished;

            var pool = ResolveRewardPool();
            if (pool == null)
            {
                Debug.LogWarning("報酬プール未設定。Tools → Generate Prototype Data を実行してください。");
                FinishCardReward();
                return;
            }

            var count = dungeonRun != null ? dungeonRun.rewardChoiceCount : 3;
            if (count <= 0)
                count = 3;
            if (fallbackEncounter != null && dungeonRun == null && fallbackEncounter.rewardChoiceCount > 0)
                count = fallbackEncounter.rewardChoiceCount;
            if (_dungeon.Current?.Type == DungeonNodeType.Combat &&
                _dungeon.ActiveYaku != null &&
                _dungeon.ActiveYaku.bonus == YakuBonusKind.CombatCardChoices)
                count += Mathf.Max(1, Mathf.RoundToInt(_dungeon.ActiveYaku.bonusValue));

            var partyNames = _party.Members
                .Select(m => m.character?.characterName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
            var choices = CardRewardPicker.PickChoices(
                pool,
                count,
                null,
                _party.GetMaxedSkillIds(),
                partyNames);
            if (choices.Count == 0)
            {
                FinishCardReward("刻印", "入手できる新しい刻印はなかった。");
                return;
            }

            try
            {
                ShowSideMap();
                _overlay.ShowCardReward(
                    transform,
                    _party,
                    choices,
                    HandleRewardCard,
                    mustTake ? null : () => FinishCardReward(),
                    notice);
            }
            catch (Exception ex)
            {
                Debug.LogError($"カード報酬 UI: {ex.Message}");
                FinishCardReward();
            }
        }

        void FinishCardReward(string modalTitle = null, string modalBody = null)
        {
            var next = _afterCardReward;
            _afterCardReward = null;
            _cardRewardMustTake = false;
            if (next == null)
            {
                CompleteNodeAndReturnToMap(modalTitle, modalBody);
                return;
            }

            if (string.IsNullOrEmpty(modalTitle))
            {
                next();
                return;
            }

            ShowMap();
            _pathUi.ShowModal(
                modalTitle,
                modalBody ?? string.Empty,
                new (string, Action)[] { ("続ける", next) });
        }

        SkillCardPoolDefinition ResolveRewardPool()
        {
            if (dungeonRun?.sharedRewardPool != null)
                return dungeonRun.sharedRewardPool;
            if (dungeonRun?.bossEncounter?.rewardCardPool != null)
                return dungeonRun.bossEncounter.rewardCardPool;
            return fallbackEncounter?.rewardCardPool
                   ?? Resources.Load<SkillCardPoolDefinition>("Battle/PrototypeRewardCardPool");
        }

        void HandleRewardCard(SkillDefinition card)
        {
            if (card == null)
            {
                FinishCardReward();
                return;
            }

            var outcome = _party.TryEquipReward(card, out var memberIndex, out var newLevel);
            switch (outcome)
            {
                case RewardEquipOutcome.Equipped:
                    Debug.Log($"カード自動装備: {card.displayName}");
                    FinishCardReward();
                    break;
                case RewardEquipOutcome.LeveledUp:
                    FinishCardReward(
                        "刻印強化",
                        $"{card.displayName}\nLv.{newLevel - 1} → Lv.{newLevel}\n" +
                        $"{SkillProgression.Describe(card, newLevel - 1)}\n→ {SkillProgression.Describe(card, newLevel)}");
                    break;
                case RewardEquipOutcome.AlreadyMax:
                    FinishCardReward(
                        "刻印",
                        $"{card.displayName} はすでに Lv.{SkillProgression.MaxLevel} です。");
                    break;
                case RewardEquipOutcome.NeedReplace:
                    _overlay.ShowReplaceSlot(
                        transform,
                        _party,
                        memberIndex,
                        card,
                        () => FinishCardReward(),
                        _cardRewardMustTake ? null : () => FinishCardReward(),
                        cardSlotsOnly: true);
                    break;
                case RewardEquipOutcome.NoMatchingMember:
                    FinishCardReward(
                        "刻印",
                        $"{card.displayName} を装備できる仲間がパーティにいない。");
                    break;
            }
        }

        void CompleteNodeAndReturnToMap() => CompleteNodeAndReturnToMap(null, null);

        void CompleteNodeAndReturnToMap(string modalTitle, string modalBody)
        {
            _pathUi.ReleasePause();
            _overlay.HideAll();
            DestroyBattleIfAny();
            MarkCurrentCleared();
            if (!string.IsNullOrEmpty(modalTitle))
            {
                _pathUi.ShowModal(
                    modalTitle,
                    modalBody ?? string.Empty,
                    new (string, Action)[]
                    {
                        ("次へ", () =>
                        {
                            if (!TryPresentLayerExit())
                                ShowMap();
                        })
                    });
                return;
            }

            if (!TryPresentLayerExit())
                ShowMap();
        }

        void MarkCurrentCleared()
        {
            var current = _dungeon.Current;
            if (current == null)
                return;

            if (!current.Cleared)
                _clearedNodesThisRun++;
            _dungeon.MarkCurrentCleared();
            RememberClearedSquares();
        }

        int RecordRunResult(bool cleared, bool extracted = false)
        {
            if (_runResultRecorded)
                return _profile?.lastRunReward ?? 0;

            _runResultRecorded = true;
            return MetaProgression.RegisterRunResult(
                _profile, cleared, _clearedNodesThisRun, extracted);
        }

        EventQuery BuildEventQuery() => new()
        {
            Floor = _dungeon != null ? Mathf.Max(1, _dungeon.Floor) : 1,
            Cards = CountAcquiredCards(),
            Coins = _party.RunCoins,
            Relics = _exploration.EquippedRelicCount(),
            Hp = _party.CurrentHp,
            MaxHp = _party.MaxHp,
            HasNextRow = _dungeon.HasRowAfterNext(),
            SeenIds = _dungeon.SeenEventIds,
            PreviousRunIds = _profile?.previousRunEventIds
        };

        int CountAcquiredCards()
        {
            var count = 0;
            foreach (var member in _party.Members)
            {
                if (member?.slot0 != null) count++;
                if (member?.slot1 != null) count++;
            }

            return count;
        }

        EventDebugHooks BuildEventDebugHooks() => new()
        {
            All = () => EventDatabase.All,
            ConditionMet = ev => EventRules.ConditionMet(ev?.condition, BuildEventQuery()),
            Start = ev => BeginEvent(ev, fromDebug: true),
            AddMadness = delta =>
            {
                if (!ApplyMadnessDeltaFromEvent(delta))
                    _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
            },
            AddHp = delta =>
            {
                _party.ApplyHpDelta(delta);
                PersistActiveRun();
                if (_party.IsRunDead)
                    TryBeginRunDeath(fromBattle: false);
                else
                    _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
            },
            AddCoins = delta =>
            {
                if (delta >= 0)
                    _party.AddCoins(delta);
                else
                    _party.TrySpendCoins(Mathf.Min(_party.RunCoins, -delta));
                PersistActiveRun();
                _pathUi.Refresh(_dungeon, _party, _exploration, _madness);
            },
        };

        void BeginEvent(EventDefinition forced, bool fromDebug)
        {
            if (fromDebug && _activeEvent != null)
                return;

            if (!fromDebug && EventDatabase.All.Count == 0)
                EventDatabase.Reload();

            var query = BuildEventQuery();
            var ev = forced ?? EventDatabase.Pick(query);
            if (ev == null)
            {
                Debug.LogWarning(
                    $"EventV2 の候補が0件。定義={EventDatabase.All.Count} 階={query.Floor} " +
                    $"カード={query.Cards} 通貨={query.Coins} レリック={query.Relics}");
                if (!fromDebug)
                    ResolveEmptyEventNode();
                return;
            }

            _eventFromDebug = fromDebug;
            _activeEvent = ev;
            _activeOutcome = null;
            _eventStartsBattle = false;
            if (!fromDebug)
            {
                _dungeon.RememberEvent(ev.id);
                SyncSeenEventsToProfile();
                PersistActiveRun();
            }

            var names = PartyNames();
            ShowMapTabHint();
            _events.Show(transform, ev, query, names, OnEventOption, () => FinishEvent(clearNode: false));
        }

        void OnEventOption(int index)
        {
            var options = _activeEvent?.options;
            if (options == null || index < 0 || index >= options.Count)
            {
                FinishEvent(clearNode: !_eventFromDebug);
                return;
            }

            _activeOutcome = EventRules.Roll(options[index], BuildEventQuery());
            if (_activeOutcome == null)
            {
                FinishEvent(clearNode: !_eventFromDebug);
                return;
            }

            _effectCursor = 0;
            _effectNotes.Clear();
            _chosenCardIds.Clear();
            _eventStartsBattle = false;
            ContinueEventEffects();
        }

        void ContinueEventEffects()
        {
            var effects = _activeOutcome?.effects;
            while (effects != null && _effectCursor < effects.Count)
            {
                var effect = effects[_effectCursor];
                _effectCursor++;
                if (effect == null || string.IsNullOrEmpty(effect.type))
                    continue;

                switch (effect.type.Trim().ToLowerInvariant())
                {
                    case "madness":
                        if (ApplyEventMadness(effect))
                            return;
                        break;
                    case "hp":
                        if (ApplyEventHp(effect))
                            return;
                        break;
                    case "coins_percent":
                        ApplyEventCoins(effect);
                        break;
                    case "card_gain":
                        ApplyEventCardGain();
                        return;
                    case "card_upgrade_choose":
                        PromptEventCard(upgrade: true);
                        return;
                    case "card_remove_choose":
                        PromptEventCard(upgrade: false);
                        return;
                    case "relic_gain":
                        ApplyEventRelicGain();
                        return;
                    case "relic_lose_random":
                        ApplyEventRelicLoss();
                        break;
                    case "battle":
                        _eventStartsBattle = true;
                        _effectNotes.Add("戦闘開始");
                        break;
                    case "skip_row":
                    case "reveal_floor":
                    case "reveal_elevator":
                    case "reveal_treasure":
                        _effectNotes.Add("この効果は使わない");
                        break;
                }
            }

            ShowEventResult();
        }

        bool ApplyEventMadness(EventEffect effect)
        {
            var delta = EventRules.ScaleFixed(effect.value, _dungeon.Floor);
            _effectNotes.Add($"狂気 {Signed(delta)}");
            if (!ApplyMadnessDeltaFromEvent(delta))
                return false;
            _events.Hide();
            return true;
        }

        bool ApplyEventHp(EventEffect effect)
        {
            var delta = EventRules.ScaleFixed(effect.value, _dungeon.Floor);
            _party.ApplyHpDelta(delta);
            _effectNotes.Add($"HP {Signed(delta)}");
            PersistActiveRun();
            if (!_party.IsRunDead)
                return false;
            _events.Hide();
            TryBeginRunDeath(fromBattle: false);
            return true;
        }

        void ApplyEventCoins(EventEffect effect)
        {
            var delta = EventRules.CoinDelta(_party.RunCoins, effect.value);
            if (delta >= 0)
            {
                delta = _dungeon.ScaleCoinGain(delta);
                _party.AddCoins(delta);
            }
            else
                _party.TrySpendCoins(Mathf.Min(_party.RunCoins, -delta));
            _effectNotes.Add($"通貨 {Signed(delta)}（所持金の{Mathf.Abs(effect.value)}%）");
        }

        void ApplyEventCardGain()
        {
            var names = PartyNames();
            var choices = CardRewardPicker.PickChoices(
                ResolveRewardPool(), 1, null, _party.GetMaxedSkillIds(), names);
            if (choices.Count == 0)
            {
                _effectNotes.Add("入手できる刻印がない");
                ContinueEventEffects();
                return;
            }

            var card = choices[0];
            var outcome = _party.TryEquipReward(card, out var memberIndex, out var newLevel);
            switch (outcome)
            {
                case RewardEquipOutcome.NeedReplace:
                    _events.Hide();
                    _overlay.ShowReplaceSlot(transform, _party, memberIndex, card, () =>
                    {
                        _effectNotes.Add($"刻印 {card.displayName}");
                        ContinueEventEffects();
                    }, () =>
                    {
                        _effectNotes.Add("刻印を見送った");
                        ContinueEventEffects();
                    }, cardSlotsOnly: true);
                    return;
                case RewardEquipOutcome.LeveledUp:
                    _effectNotes.Add($"{card.displayName} Lv.{newLevel}");
                    break;
                case RewardEquipOutcome.Equipped:
                    _effectNotes.Add($"刻印 {card.displayName}");
                    break;
                default:
                    _effectNotes.Add("刻印は入らなかった");
                    break;
            }

            ContinueEventEffects();
        }

        void PromptEventCard(bool upgrade)
        {
            var choices = new List<EventCardChoice>();
            for (var memberIndex = 0; memberIndex < _party.Members.Count; memberIndex++)
            {
                var member = _party.Members[memberIndex];
                for (var slot = 0; slot <= 1; slot++)
                {
                    var skill = member.GetSlot(slot);
                    if (skill == null || _chosenCardIds.Contains(skill.skillId))
                        continue;
                    if (upgrade && member.GetSlotLevel(slot) >= SkillProgression.MaxLevel)
                        continue;
                    var who = member.character?.characterName ?? "仲間";
                    choices.Add(new EventCardChoice(
                        memberIndex, slot, $"{who}  {skill.displayName}  Lv.{member.GetSlotLevel(slot)}"));
                }
            }

            if (choices.Count == 0)
            {
                _effectNotes.Add("選べる刻印がない");
                ContinueEventEffects();
                return;
            }

            _events.ShowCardPick(transform, upgrade ? "強化する刻印" : "外す刻印", choices, choice =>
            {
                if (choice.MemberIndex < 0)
                {
                    _effectNotes.Add("選べる刻印がない");
                    ContinueEventEffects();
                    return;
                }

                var member = _party.Members[choice.MemberIndex];
                var skill = member.GetSlot(choice.SlotIndex);
                if (skill == null)
                {
                    ContinueEventEffects();
                    return;
                }

                _chosenCardIds.Add(skill.skillId);
                if (upgrade && member.TryLevelUp(skill, out var level) && level <= SkillProgression.MaxLevel)
                    _effectNotes.Add($"{skill.displayName} Lv.{level}");
                else if (!upgrade)
                {
                    _party.ClearEquippedSlot(choice.MemberIndex, choice.SlotIndex);
                    _effectNotes.Add($"{skill.displayName} を外した");
                }
                else
                    _effectNotes.Add($"{skill.displayName} は最大");

                ContinueEventEffects();
            });
        }

        void ApplyEventRelicGain()
        {
            var relic = PickRandomRelic();
            if (relic == null)
            {
                _effectNotes.Add("レリックがない");
                ContinueEventEffects();
                return;
            }

            PromptRelicEquip(relic, 0, () =>
            {
                _effectNotes.Add($"レリック {relic.displayName}");
                ContinueEventEffects();
            }, ContinueEventEffects);
        }

        void ApplyEventRelicLoss()
        {
            var slots = new List<int>();
            for (var i = 0; i < RunExplorationInventory.RelicSlotCount; i++)
            {
                if (_exploration.GetEquippedRelicId(i) >= 0)
                    slots.Add(i);
            }

            if (slots.Count == 0)
            {
                _effectNotes.Add("外すレリックがない");
                return;
            }

            var slot = slots[UnityEngine.Random.Range(0, slots.Count)];
            var name = ResolveRelicCatalog()?.FindById(_exploration.GetEquippedRelicId(slot))?.displayName ?? "レリック";
            _exploration.DiscardEquippedRelic(slot);
            OnRelicsChanged();
            _effectNotes.Add($"{name} を失った");
        }

        RelicDefinition PickRandomRelic()
        {
            var relics = ResolveRelicCatalog()?.relics;
            if (relics == null)
                return null;
            var list = relics.Where(relic => relic != null).ToList();
            if (list.Count == 0)
                return null;
            var free = list.Where(relic => !_exploration.IsRelicEquipped(relic.relicId)).ToList();
            var pool = free.Count > 0 ? free : list;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        void ShowEventResult()
        {
            PersistActiveRun();
            var text = string.IsNullOrEmpty(_activeOutcome?.text) ? "……" : _activeOutcome.text;
            var confirm = _eventStartsBattle ? "戦闘開始" : "マップへ戻る";
            _events.ShowResult(transform, text, string.Join("、", _effectNotes), confirm, OnEventConfirm);
        }

        void OnEventConfirm()
        {
            var battle = _eventStartsBattle;
            var clearNode = !_eventFromDebug;
            _events.Hide();
            _activeEvent = null;
            _activeOutcome = null;
            _eventFromDebug = false;
            _eventStartsBattle = false;
            if (battle)
            {
                var node = _dungeon.Current;
                if (node == null)
                {
                    ClearPendingEventBattle();
                    ShowMap();
                    return;
                }

                if (clearNode && _profile != null)
                {
                    _profile.towerRunEventBattlePending = true;
                    PersistActiveRun();
                }

                StartBattleForNode(node, ResolveCombatEncounter(), BuildCombatMods());
                return;
            }

            FinishEvent(clearNode);
        }

        void FinishEvent(bool clearNode)
        {
            _events.Hide();
            _activeEvent = null;
            _activeOutcome = null;
            _eventFromDebug = false;
            if (clearNode)
                CompleteNodeAndReturnToMap();
            else
                ShowMap();
        }

        bool TryStartPendingEventBattle()
        {
            if (_profile == null || !_profile.towerRunEventBattlePending)
                return false;
            if (_party.IsRunDead)
            {
                ClearPendingEventBattle();
                return false;
            }

            LoadLayer(
                Mathf.Max(1, _profile.towerRunFloor),
                _profile.towerRunNodeId,
                startAtElevator: false,
                ResolveBoard(_profile.towerRunBoardId),
                _profile.towerRunPreFourthHealApplied);
            var node = _dungeon.Current;
            if (node == null)
            {
                ClearPendingEventBattle();
                PersistActiveRun();
                return false;
            }

            StartBattleForNode(node, ResolveCombatEncounter(), BuildCombatMods());
            return true;
        }

        void ClearPendingEventBattle()
        {
            if (_profile != null)
                _profile.towerRunEventBattlePending = false;
        }

        List<string> PartyNames() => _party.Members
            .Select(member => member.character?.characterName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        void SyncSeenEventsToProfile()
        {
            if (_profile == null)
                return;
            _profile.towerRunSeenEventIds = new List<string>(_dungeon.SeenEventIds);
        }

        void ArchiveSeenEvents()
        {
            if (_profile == null)
                return;
            _profile.previousRunEventIds = new List<string>(_dungeon.SeenEventIds);
            _dungeon.ClearSeenEvents();
            _profile.towerRunSeenEventIds = new List<string>();
        }

        static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();

        void DestroyBattleIfAny()
        {
            if (_battle == null)
                return;
            Destroy(_battle.gameObject);
            _battle = null;
        }
    }
}
