using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using TypingRoguelike.RouteLottery;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>塔探索フロー（ダンジョン探索仕様 v1.2）。UI は別系統。</summary>
    public sealed partial class RunFlowController : MonoBehaviour
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
        GameObject _resultRoot;
        LayerConfig _layerConfig;
        BoardCatalog _boards;
        YakuCatalog _yaku;
        TrustRoster _trust;
        System.Random _opinionRng;
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
        Action _afterCardReward;
        List<SkillDefinition> _pendingRewardChoices;
        bool _pendingRewardMustTake;
        string _pendingRewardNotice;
        int _merchantRelicSoldNodeId = -1;
        string _pendingOpinionSummary;

        public RunPartyState Party => _party;
        public RunMadnessState Madness => _madness;

        void Awake()
        {
            try
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
            ApplySwordsmanInnate();
            ApplyAxeInnate();
                _profile = MetaProgression.Load();
                _trust = new TrustRoster(TrustCatalog.LoadOrFallback(), _profile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"RunFlowController Awake 失敗: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Start() => EnsureHubVisible();

        /// <summary>拠点 UI。Start 失敗時の再試行用にも BattleSceneSetup から呼ぶ。</summary>
        public void EnsureHubVisible()
        {
            try
            {
                ShowHub();
                Debug.Log("[TypingRoguelike] 拠点 UI を表示しました。");
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
            RunScore.ClearOuting(_profile);
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
            ApplySwordsmanInnate();
            ApplyAxeInnate();

            ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            _madness.ResetForNewRun(ComputeFullMadnessCap());

            _runSeed = _profile.towerSeed;
            _opinionRng = null;
            _trust.ResetRunCounters(_profile);
            PersonalityCatalog.RollMood(_profile, OpinionRng());
            OpenBoardPreparation(1, startAtElevator: false);
        }

        int TotalFloors => Mathf.Clamp(_layerConfig != null ? _layerConfig.totalLayers : 20, 1, 99);

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
            if (_profile != null && _profile.layerBuffFloor != floor)
                ClearLayerBuffs(floor);

            var index = restoreIndex;
            if (index < 0 || index >= LayerSquares.Count)
            {
                Debug.LogWarning($"保存していたマス {restoreIndex} は一本道にない。この層の先頭から始める。");
                index = 0;
            }

            var custom = !string.IsNullOrEmpty(_profile?.reelLayout);
            _dungeon.SetFloor(
                floor,
                TotalFloors,
                _runSeed,
                _layerConfig,
                GridForLayer(board),
                custom ? "custom" : (board != null ? board.presetId : ""),
                _yaku,
                index,
                _profile.clearedTowerNodeIds,
                startAtElevator,
                preFourthHealApplied,
                RoadFor(floor));
            RememberClearedSquares();
        }

        BoardGrid GridForLayer(BoardPreset board)
        {
            var catalog = ReelPartCatalog.Load();
            var saved = ReelLayout.Parse(_profile?.reelLayout, catalog, _profile != null && _profile.reelDebugUnlocked);
            if (saved != null)
                return saved.ToGrid(catalog);
            if (board != null)
                return board.ToGrid();
            return ReelLayout.BalanceStarter(catalog).ToGrid(catalog);
        }

        string[] RoadFor(int floor)
        {
            if (_profile == null || _profile.towerRunRoadFloor != floor || string.IsNullOrEmpty(_profile.towerRunRoad))
                return null;
            var parts = _profile.towerRunRoad.Split(',');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return parts;
        }

        void SaveMemoryBoard(string layout)
        {
            if (_profile == null)
                return;
            _profile.memoryBoard = layout ?? "";
            MetaProgression.Save(_profile);
        }

        void OpenBoardPreparation(int layer, bool startAtElevator)
        {
            try
            {
                _overlay.HideAll();
                _pathUi.Hide();
                SyncMadnessCapFromRelics();
                RouteLotteryPresenter.Show(
                    transform,
                    layer,
                    _runSeed,
                    LayerSquares.FixedSquare(layer, _layerConfig),
                    startAtElevator,
                    _profile != null ? _profile.memoryBoard : "",
                    SaveMemoryBoard,
                    ids => ConfirmLotteryRoad(ids, layer, startAtElevator),
                    _party,
                    _madness,
                    _trust != null ? _trust.Gauges(_profile) : null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ダンジョンルーレットの表示に失敗: {ex.Message}\n{ex.StackTrace}");
                EnsureHubVisible();
            }
        }

        void ConfirmLotteryRoad(string[] ids, int layer, bool startAtElevator)
        {
            if (_profile == null || ids == null || ids.Length < 3)
                return;
            _profile.towerRunRoad = string.Join(",", ids);
            _profile.towerRunRoadFloor = layer;
            _profile.towerRunEventWarpFloor = 0;
            _profile.clearedTowerNodeIds?.Clear();
            _profile.towerRunPreFourthHealApplied = false;
            LoadLayer(
                layer,
                restoreIndex: startAtElevator ? LayerSquares.Count - 1 : 0,
                startAtElevator,
                ResolveBoard(_profile.towerRunBoardId),
                preFourthHealApplied: startAtElevator);
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
                    new (string, System.Action)[] { ("進む", EnterOpenSquare) });
                return;
            }

            EnterOpenSquare();
        }

        void EnterOpenSquare()
        {
            var index = _dungeon.FirstUnclearedIndex();
            if (index < 0)
            {
                ShowMap();
                return;
            }

            OnMapNodeSelected(index);
        }

        void ConfirmBuiltBoard(ReelLayout layout, bool debugUnlocked, int layer, bool startAtElevator)
        {
            if (layout == null || _profile == null)
                return;
            var catalog = ReelPartCatalog.Load();
            var grid = layout.ToGrid(catalog);
            if (!LayerSquares.MeetsMinimums(grid, _layerConfig, layer, out _))
                return;

            _profile.reelLayout = layout.Serialize();
            _profile.reelDebugUnlocked = debugUnlocked;
            _profile.towerRunBoardId = "custom";
            _profile.clearedTowerNodeIds?.Clear();
            _profile.towerRunPreFourthHealApplied = false;
            LoadLayer(layer, restoreIndex: 0, startAtElevator, board: null, preFourthHealApplied: startAtElevator);
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

            _pathUi.Hide();
            var squares = _dungeon.Squares;
            var ids = new string[squares != null ? squares.Length : 0];
            var cleared = new bool[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = RoadTypeId(squares[i]);
                cleared[i] = squares[i].Cleared;
            }

            try
            {
                RouteLotteryPresenter.ShowLayer(
                    transform,
                    _dungeon.Floor,
                    LayerSquares.FixedSquare(_dungeon.Floor, _layerConfig),
                    ids,
                    cleared,
                    _dungeon.FirstUnclearedIndex(),
                    OnMapNodeSelected,
                    OpenRunLoadout,
                    OpenRunRelicManage,
                    OpenRunPartyStats,
                    AbandonRun,
                    _party,
                    _madness,
                    _trust != null ? _trust.Gauges(_profile) : null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"層ルートの表示に失敗: {ex.Message}\n{ex.StackTrace}");
            }
        }

        static string RoadTypeId(LayerSquare square)
        {
            if (square == null)
                return "battle";
            if (!string.IsNullOrEmpty(square.RoadId))
                return square.RoadId;
            switch (square.Type)
            {
                case DungeonNodeType.Combat: return "battle";
                case DungeonNodeType.Rest: return "rest";
                case DungeonNodeType.Treasure: return "treasure";
                case DungeonNodeType.Merchant: return "shop";
                case DungeonNodeType.Event: return "event";
                case DungeonNodeType.Trap: return "danger";
                case DungeonNodeType.Elevator: return "elevator";
                case DungeonNodeType.Boss:
                case DungeonNodeType.MidBoss: return "boss";
                default: return "battle";
            }
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

        /// <summary>
        /// 拠点へ帰還したあとの再入場。1階からでも、どの解放エレベーターからでも同じ。
        /// 帰還前のセーブは、エレベーターマスで止まっているものを帰還扱いにする。
        /// </summary>
        bool ShouldRestoreVitalsAfterElevatorReturn()
        {
            if (_profile == null)
                return false;
            if (_profile.towerRunVitalsRested)
                return true;
            return _profile.towerRunNodeId == LayerSquares.Count - 1 && !_profile.towerRunElevatorRestConsumed;
        }

        void ApplyElevatorReturnVitals()
        {
            _madness.SetConfiguredMadnessCap(ComputeFullMadnessCap());
            _madness.ClearDeviceCapPenalty();
            _madness.RestoreCurrentAndJackpot(
                0,
                _profile.towerRunBonusJackpotChance,
                _profile.towerRunJackpotAccumulatedChance);
            _profile.towerRunVitalsRested = false;
            _profile.towerRunElevatorRestConsumed = true;
            _profile.towerRunCurrentHp = _party.CurrentHp;
            _profile.towerRunMaxHp = _party.MaxHp;
            _profile.towerRunMadnessCurrent = 0;
            _profile.towerRunMadnessMax = _madness.MaxMadness;
            MetaProgression.Save(_profile);
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
                ShowMap,
                PersonalityCatalog.BuildCharts(_profile),
                SetDebugAffinityFromRun);
        }

        void OpenHubPartyStats()
        {
            _overlay.ShowPartyStats(
                transform,
                _party,
                dungeonRun,
                ResolveRelicCatalog(),
                null,
                "拠点に戻る",
                ShowHub,
                PersonalityCatalog.BuildCharts(_profile),
                SetDebugAffinityFromHub);
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
            var sum = 0;
            if (_party?.Members != null)
            {
                foreach (var member in _party.Members)
                    sum += member?.character != null ? member.character.hp : 0;
            }

            if (sum > 0)
                return sum;

            var maxHp = dungeonRun?.bossEncounter?.playerMaxHp
                        ?? dungeonRun?.combatEncounter?.playerMaxHp
                        ?? fallbackEncounter?.playerMaxHp
                        ?? 1000f;
            return maxHp;
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
            var raised = RunMadnessState.DefaultMaxMadness + Mathf.Max(0, proxyBonus) + Mathf.Max(0, relicBonus);
            return Mathf.Clamp(raised, 1, RunMadnessState.MaxRaisedMadness);
        }

        void ShowRunResult(bool death, string subtitle, Action onHub)
        {
            CloseRunResult();
            _overlay.HideAll();
            _pathUi.Hide();
            DestroyBattleIfAny();
            var score = RunScore.Read(_profile);
            RunScore.Bank(_profile, death);
            MetaProgression.Save(_profile);

            var canvasGo = new GameObject("RunResultCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();
            RunOverlayUI.EnsureGlobalEventSystem();

            var source = RunResultView.Load();
            if (source == null || !source.Ready)
            {
                Destroy(canvasGo);
                onHub?.Invoke();
                return;
            }

            var view = Instantiate(source, canvasGo.transform);
            view.name = "RunResult";
            var rt = view.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            view.Apply(subtitle, death, score, _profile.playerXp, () =>
            {
                CloseRunResult();
                onHub?.Invoke();
            });
            _resultRoot = canvasGo;
        }

        void CloseRunResult()
        {
            if (_resultRoot == null)
                return;
            Destroy(_resultRoot);
            _resultRoot = null;
        }

        void ShowHub()
        {
            CloseRunResult();
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
                OpenHubPartyStats,
                OpenJackpotDebug);
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
            RunScore.ClearOuting(_profile);
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
            ApplySwordsmanInnate();
            ApplyAxeInnate();

            ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            _madness.ResetForNewRun(ComputeFullMadnessCap());

            _runSeed = _profile.towerSeed;
            _opinionRng = null;
            _trust.ResetRunCounters(_profile);
            PersonalityCatalog.RollMood(_profile, OpinionRng());
            if (LayerSquares.FixedSquare(floor, _layerConfig) != DungeonNodeType.Elevator)
            {
                Debug.LogWarning($"階 {floor} の4マス目がエレベーターではない。1 層から開始します。");
                OpenBoardPreparation(1, startAtElevator: false);
                return;
            }

            OpenFloorAfterElevator(floor);
        }

        /// <summary>エレベーター階は踏破済みとして飛ばし、次の層のルーレットから始める。</summary>
        void OpenFloorAfterElevator(int elevatorFloor)
        {
            var next = elevatorFloor + 1;
            if (next > TotalFloors)
            {
                ShowHub();
                return;
            }

            if (HasCommittedFloor(next))
            {
                LoadLayer(
                    next,
                    restoreIndex: 0,
                    startAtElevator: false,
                    ResolveBoard(_profile.towerRunBoardId),
                    _profile.towerRunPreFourthHealApplied);
                if (_profile.towerRunActive)
                    PersistActiveRun();
                else
                    ActivateAndSaveRun();
                ShowMap();
                return;
            }

            OpenBoardPreparation(next, startAtElevator: false);
        }

        void ResumeActiveTowerRun()
        {
            if (_profile == null || !_profile.towerRunActive)
            {
                BeginNewRun();
                return;
            }

            RestoreActiveRunState();
            LoadLayer(
                _profile.towerRunFloor,
                _profile.towerRunNodeId,
                startAtElevator: false,
                ResolveBoard(_profile.towerRunBoardId),
                _profile.towerRunPreFourthHealApplied);
            if (TryResumeEventBattle())
                return;
            if (_profile.towerRunEventWarpFloor > 0)
            {
                OpenBoardPreparation(_profile.towerRunEventWarpFloor, startAtElevator: false);
                return;
            }

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
            ApplySwordsmanInnate();
            ApplyAxeInnate();
            _party.ApplyMemberSaves(_profile.towerRunMembers, GetSkillCatalog());
            ApplySwordsmanInnate();
            ApplyAxeInnate();
            _party.AddCoins(_profile.towerRunCoins);
            _party.SetBlood(_profile.towerRunBlood);
            _party.SetCarriedFocus(_profile.towerRunFocus);

            ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            var rested = ShouldRestoreVitalsAfterElevatorReturn();
            if (!rested && _profile.towerRunCurrentHp > 0f)
            {
                var savedMax = _profile.towerRunMaxHp > 0f ? _profile.towerRunMaxHp : _party.MaxHp;
                var ratio = savedMax > 0f ? _profile.towerRunCurrentHp / savedMax : 1f;
                _party.SetCurrentHp(_party.MaxHp * Mathf.Clamp01(ratio));
            }

            if (rested)
                ApplyElevatorReturnVitals();
            else
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
            if (fromElevator)
            {
                if (LayerSquares.FixedSquare(floor, _layerConfig) != DungeonNodeType.Elevator)
                {
                    Debug.LogWarning($"階 {floor} の4マス目がエレベーターではない。1 層から開始します。");
                    OpenBoardPreparation(1, startAtElevator: false);
                    return;
                }

                OpenFloorAfterElevator(floor);
                return;
            }
            else if (!HasCommittedFloor(floor))
            {
                OpenBoardPreparation(floor, startAtElevator: false);
                return;
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
            if (pool?.cards == null)
                return Array.Empty<SkillDefinition>();
            var config = SwordsmanSkillConfig.Load();
            var axe = AxeSkillConfig.Load();
            var list = new List<SkillDefinition>();
            foreach (var card in pool.cards)
            {
                if (card == null)
                    continue;
                if (config != null && config.HidesReward(card.skillId))
                    continue;
                if (axe != null && axe.HidesReward(card))
                    continue;
                list.Add(card);
            }
            return list.ToArray();
        }

        void ApplySwordsmanInnate()
        {
            var config = SwordsmanSkillConfig.Load();
            var replace = config?.options == null || config.options.replaceAliceSkills;
            SkillDefinition innate = null;
            if (replace)
            {
                var id = config?.options != null ? config.options.aliceInnateSkillId : 100;
                var pool = ResolveRewardPool();
                if (pool?.cards != null)
                {
                    foreach (var card in pool.cards)
                    {
                        if (card != null && card.skillId == id)
                        {
                            innate = card;
                            break;
                        }
                    }
                }
            }

            _party?.ApplySwordsmanInnate(innate, replace);
        }

        void ApplyAxeInnate()
        {
            var config = AxeSkillConfig.Load();
            var replace = config?.options == null || config.options.replaceTargetSkills;
            SkillDefinition innate = null;
            if (replace)
            {
                var id = config != null ? config.InnateSkillId : 200;
                var pool = ResolveRewardPool();
                if (pool?.cards != null)
                {
                    foreach (var card in pool.cards)
                    {
                        if (card != null && card.skillId == id)
                        {
                            innate = card;
                            break;
                        }
                    }
                }
            }

            _party?.ApplyAxeInnate(innate, replace, config != null ? config.targetCharacterName : "ルナ");
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
            ShowRunResult(true, "死亡", ShowHub);
        }

        void OnMapNodeSelected(int nodeId)
        {
            RouteLotteryPresenter.Hide(transform);
            if (!_dungeon.CanEnter(nodeId))
                return;

            _dungeon.MoveTo(nodeId);
            var node = _dungeon.Current;
            if (node == null)
                return;

            RunScore.AddSquare(_profile, _dungeon.Floor);
            EnterSquare(node);
        }

        void EnterSquare(LayerSquare node)
        {
            // Story mist owns the route until the screen is fully covered.
            if (node.Type != DungeonNodeType.Event || node.RoadId == "danger")
                _pathUi.Hide();
            if (node.RoadId == "danger")
            {
                ResolveDangerNode();
                return;
            }
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
                    StartStoryEvent();
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

        string TakeOpinionSummary()
        {
            var text = _pendingOpinionSummary;
            _pendingOpinionSummary = null;
            return string.IsNullOrWhiteSpace(text) ? "" : text.Trim();
        }

        void SetDebugAffinityFromRun(string id, int value)
        {
            ApplyDebugAffinity(id, value);
            OpenRunPartyStats();
        }

        void SetDebugAffinityFromHub(string id, int value)
        {
            ApplyDebugAffinity(id, value);
            OpenHubPartyStats();
        }

        void ApplyDebugAffinity(string id, int value)
        {
            if (_trust == null || _profile == null || string.IsNullOrEmpty(id))
                return;
            _trust.SetTrust(id, value, _profile);
            if (_profile.towerRunActive)
                PersistActiveRun();
            else
                MetaProgression.Save(_profile);
        }

        void OpenJackpotDebug()
        {
            _overlay.ShowJackpotDebug(transform, null);
        }

        void OpenTrustDebug()
        {
            _pathUi.ShowTrustDebug(_trust.Gauges(_profile), (id, value) =>
            {
                var notice = _trust.SetTrust(id, value, _profile);
                MetaProgression.Save(_profile);
                if (!string.IsNullOrEmpty(notice))
                {
                    _pathUi.ShowModal("パッシブ", notice, new (string, Action)[] { ("戻る", ShowMap) });
                    return;
                }

                ShowMap();
            });
        }

        void ApplyTrustPostBattleHeal()
        {
            var heal = _trust.CollectBattle(_profile).PostBattleHealFraction;
            if (heal <= 0f)
                return;
            _party.HealFraction(heal);
        }

        System.Random OpinionRng()
        {
            if (_opinionRng == null)
            {
                var serial = _profile != null ? _profile.opinionDrawSerial : 0;
                _opinionRng = new System.Random(unchecked(_runSeed * 397 ^ (serial + 1) * 7919));
            }

            return _opinionRng;
        }

        void ResolveDangerNode()
        {
            var picked = DemeritCatalog.Pick(EventRng());
            if (picked == null)
            {
                CompleteNodeAndReturnToMap("空白", "このマスの出来事を読めなかった。");
                return;
            }

            if (ApplyDangerDemerit(picked, out var note))
                return;
            PersistActiveRun();
            if (_party != null && _party.IsRunDead)
            {
                TryBeginRunDeath(fromBattle: false);
                return;
            }

            CompleteNodeAndReturnToMap("空白", note);
        }

        bool HasCommittedFloor(int floor)
        {
            if (_profile == null)
                return false;
            if (_profile.towerRunRoadFloor == floor && !string.IsNullOrEmpty(_profile.towerRunRoad))
                return true;
            return _profile.towerRunActive
                && _profile.towerRunFloor == floor
                && (_profile.towerRunNodeId > 0
                    || (_profile.clearedTowerNodeIds != null && _profile.clearedTowerNodeIds.Count > 0));
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

            var note = TakeOpinionSummary();
            var body = $"罠が作動した。HP が {loss} 減った。\n{FormatHpLine()}";
            if (!string.IsNullOrEmpty(note))
                body = note + "\n\n" + body;
            CompleteNodeAndReturnToMap("罠", body);
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
            var note = TakeOpinionSummary();
            var restBody = $"焚き火で休んだ。HP が最大の {(int)(frac * 100)}% 回復した。{madnessLine}\n{FormatHpLine()}";
            if (!string.IsNullOrEmpty(note))
                restBody = note + "\n\n" + restBody;
            _pathUi.ShowModal(
                "休息",
                restBody,
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
            var note = TakeOpinionSummary();
            var lootLines =
                (string.IsNullOrEmpty(note) ? "" : note + "\n\n") +
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

        void ResolveMerchantNode()
        {
            var healCost = dungeonRun != null ? dungeonRun.merchantHealCost : 10;
            var relicCost = dungeonRun != null ? dungeonRun.merchantRelicCost : 60;
            var healPct = (int)((dungeonRun != null ? dungeonRun.merchantHealFraction : 0.12f) * 100f);
            var nodeId = _dungeon.Floor * 10 + (_dungeon.Current?.Id ?? 0);
            var relic = MerchantOffersRelic(nodeId) ? PickOfferedRelic(nodeId, 17) : null;
            var note = TakeOpinionSummary();
            var body =
                (string.IsNullOrEmpty(note) ? "" : note + "\n\n") +
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
            _profile.towerRunElevatorRestConsumed = true;
            PersistActiveRun();
            ShowElevatorChoice();
        }

        void ShowElevatorChoice()
        {
            _pathUi.ShowModal(
                "エレベーター",
                $"この層はここまで。\n現在 {_dungeon.Floor} / {TotalFloors} 層。\n\n拠点へ帰還すると、HP は満タン、狂気は 0 に戻る。\n次の層へ進むと、HP も狂気も持ったまま。",
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
            if (_party.RunHpInitialized)
                _party.SetCurrentHp(_party.MaxHp);
            else
                ApplyProxyBrainRunMaxHp(preserveCurrentHp: false);
            _madness.ResetMadnessAfterJackpot();
            _profile.towerRunVitalsRested = true;
            _profile.towerRunElevatorRestConsumed = false;
            // TODO 未決事項5: 素材は倉庫へ移さず、探索の所持のまま保存する。
            _trust.LogEnd("帰還", _profile);
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
            ShowRunResult(false, "エレベーター帰還", ShowHub);
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
            ApplyTrustBattleModifiers(ref mods);
            ApplyFloorStrengthBand(ref mods);
            ApplyGlobalEnemyHpScale(ref mods);
            ApplyOpinionCombatModifiers(ref mods, node);
            ApplyLayerCombatModifiers(ref mods);
            ApplyEventBattleModifiers(ref mods, node);
            DestroyBattleIfAny();
            var root = new GameObject("BattleRoot");
            root.transform.SetParent(transform, false);
            _battle = root.AddComponent<BattleController>();
            try
            {
                var floor = _dungeon.Floor;
                _battle.Initialize(
                    enc,
                    _party,
                    _madness,
                    OnBattleVictory,
                    OnBattleDefeat,
                    OnBattleInsanity,
                    mods,
                    PartyCombatStats.Compute(_party, dungeonRun),
                    floor);
                _battle.OnCorrectCharacter = () => RunScore.AddTyped(_profile, floor, 1);
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

        void ApplyTrustBattleModifiers(ref EncounterBattleModifiers mods)
        {
            var bonus = _trust.CollectBattle(_profile);
            var relicMul = mods.PlayerDamageMultiplier > 0f ? mods.PlayerDamageMultiplier : 1f;
            mods.PlayerDamageMultiplier = relicMul * (1f + bonus.AttackBonus);
            if (bonus.DamageTakenReduction > 0f)
                mods.DamageTakenMultiplier = 1f - bonus.DamageTakenReduction;
            if (bonus.OpeningFreezeSeconds > 0f)
                mods.OpeningEnemyStopSeconds += bonus.OpeningFreezeSeconds;
            mods.NegateMissHit = bonus.NegateMissHit;
            mods.ShowEnemyAttackTime = bonus.ShowEnemyAttackTime;
        }

        /// <summary>5層ごとに敵が強くなる。1–5層は基準、6–10層が一段、11層以降がもう一段。</summary>
        void ApplyFloorStrengthBand(ref EncounterBattleModifiers mods)
        {
            var floor = _dungeon != null ? _dungeon.Floor : 1;
            var band = Mathf.Max(0, (floor - 1) / 5);
            if (band <= 0)
                return;

            if (band == 1)
            {
                mods.EnemyHpMultiplier *= 1.15f;
                mods.EnemyDamageMultiplier *= 1.3f;
                mods.EnemyAttackIntervalMultiplier = IntervalMultiplier(mods) * 0.9f;
                return;
            }

            mods.EnemyHpMultiplier *= 1.3f;
            mods.EnemyDamageMultiplier *= 1.6f;
            mods.EnemyAttackIntervalMultiplier = IntervalMultiplier(mods) * 0.8f;
        }

        void ApplyGlobalEnemyHpScale(ref EncounterBattleModifiers mods)
        {
            var scale = dungeonRun != null && dungeonRun.globalEnemyHpScale > 0f
                ? dungeonRun.globalEnemyHpScale
                : 1f;
            var current = mods.EnemyHpMultiplier <= 0f ? 1f : mods.EnemyHpMultiplier;
            mods.EnemyHpMultiplier = current * scale;
        }

        static float IntervalMultiplier(EncounterBattleModifiers mods) =>
            mods.EnemyAttackIntervalMultiplier <= 0f ? 1f : mods.EnemyAttackIntervalMultiplier;

        void ApplyOpinionCombatModifiers(ref EncounterBattleModifiers mods, LayerSquare node)
        {
            if (node == null || node.Type != DungeonNodeType.Combat || _profile == null)
                return;
            var hpScale = _profile.opinionCombatEnemyHpScale <= 0f ? 1f : _profile.opinionCombatEnemyHpScale;
            if (!Mathf.Approximately(hpScale, 1f))
                mods.EnemyHpMultiplier *= hpScale;
            mods.OpeningAttackTimeDeltaSeconds += _profile.opinionCombatOpeningSeconds;
            var note = TakeOpinionSummary();
            if (string.IsNullOrEmpty(note))
                return;
            mods.StartLogMessage = mods.HasLogOverride ? mods.StartLogMessage + "\n" + note : note;
        }

        void ClearOpinionCombatMods()
        {
            if (_profile == null)
                return;
            _profile.opinionCombatEnemyHpScale = 1f;
            _profile.opinionCombatOpeningSeconds = 0f;
            _profile.opinionCombatCoinScale = 1f;
        }

        EncounterBattleModifiers BuildCombatMods() => new()
        {
            EnemyHpMultiplier = dungeonRun != null ? dungeonRun.combatEnemyHpMultiplier : 0.72f,
            EnemyDamageMultiplier = 1f,
            EnemySpeedMultiplier = 1f
        };

        EncounterBattleModifiers BuildEliteMods()
        {
            var hp = dungeonRun != null ? dungeonRun.combatEnemyHpMultiplier : 0.72f;
            var eliteHpScale = dungeonRun?.eliteEnemyHpMultiplier ?? 1.35f;
            return new EncounterBattleModifiers
            {
                EnemyHpMultiplier = hp * Mathf.Clamp(eliteHpScale * 0.55f, 0.85f, 1.15f),
                EnemyDamageMultiplier = 1f,
                EnemySpeedMultiplier = 1f,
                EnemyTier = ExEnemyTier.Elite,
                StartLogMessage = "💀 強敵が道を塞いでいる！"
            };
        }

        static EncounterBattleModifiers BuildBossMods() => new()
        {
            EnemyHpMultiplier = 0.9f,
            EnemyDamageMultiplier = 1f,
            EnemySpeedMultiplier = 1f,
            EnemyTier = ExEnemyTier.Boss,
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
                RunScore.AddBoss(_profile, _dungeon.Floor);
            else
                RunScore.AddTrash(_profile, _dungeon.Floor);
            ApplyTrustPostBattleHeal();
            ApplyLayerPostBattleHeal();
            if (IsTowerBossVictory())
            {
                ClearOpinionCombatMods();
                MarkCurrentCleared();
                StartCoroutine(BossVictoryRoutine());
                return;
            }

            var coins = GrantCoinsForCurrentBattle();
            _party.AddCoins(coins);
            var eventBattle = _profile?.eventBattle;
            var victoryText = eventBattle?.active == true ? eventBattle.victoryText : "";
            var eventTitle = eventBattle?.eventTitle;
            if (ApplyEventBattleRewards(out var eventNote))
                return;
            ClearOpinionCombatMods();
            StartCoroutine(CombatVictoryRoutine(coins, eventNote, victoryText, eventTitle));
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
                notice: "塔の主を倒した。刻印を1枚選ぶ。取らないこともできる。",
                onFinished: () => StartCoroutine(FinishBossVictory()));
        }

        int GrantCoinsForCurrentBattle()
        {
            var cur = _dungeon.Current;
            if (cur == null)
                return dungeonRun != null ? dungeonRun.combatCoinReward : 8;

            var eventElite = cur.Type == DungeonNodeType.Event
                && _profile?.eventBattle != null
                && _profile.eventBattle.active
                && _profile.eventBattle.elite;
            if (cur.Type is DungeonNodeType.Combat or DungeonNodeType.MidBoss || eventElite || cur.Type == DungeonNodeType.Event)
            {
                var elite = cur.Type == DungeonNodeType.MidBoss || eventElite;
                var reward = dungeonRun != null
                    ? elite ? dungeonRun.eliteCoinReward : dungeonRun.combatCoinReward
                    : elite ? 15 : 8;
                reward = _dungeon.ScaleCoinGain(reward);
                reward = ApplyLayerCoinScale(reward);
                if (!elite && cur.Type == DungeonNodeType.Combat && _profile != null && _profile.opinionCombatCoinScale > 0f &&
                    !Mathf.Approximately(_profile.opinionCombatCoinScale, 1f))
                    reward = Mathf.Max(0, Mathf.RoundToInt(reward * _profile.opinionCombatCoinScale));
                return reward;
            }

            return ApplyLayerCoinScale(_dungeon.ScaleCoinGain(dungeonRun != null ? dungeonRun.combatCoinReward : 8));
        }

        IEnumerator CombatVictoryRoutine(int coins, string eventNote, string victoryText, string eventTitle)
        {
            yield return null;
            _overlay.HideAll();
            SyncJackpotFromBattle();
            DestroyBattleIfAny();
            ClearPendingEventBattle();
            MarkCurrentCleared();
            PersistActiveRun();
            var notice = $"戦闘に勝った。塔片 +{coins}。刻印を1枚選ぶ。取らないこともできる。";
            if (!string.IsNullOrEmpty(eventNote))
                notice += "\n" + eventNote;
            var followUp = _eventWarpFloor > 0 || _grantEventRelicAfterCards
                ? (Action)FinishEventBattleExtras
                : null;
            if (string.IsNullOrWhiteSpace(victoryText))
                OfferCardReward(notice: notice, onFinished: followUp);
            else
                _pathUi.ShowModal(eventTitle, victoryText, new (string, Action)[]
                {
                    ("次へ", () => OfferCardReward(notice: notice, onFinished: followUp))
                });
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
            _trust.LogEnd("クリア", _profile);
            _trust.ResetRunCounters(_profile);
            PersonalityCatalog.ClearMood(_profile);
            _opinionRng = null;
            MetaProgression.RollTowerSeed(_profile);
            MetaProgression.Save(_profile);
            RecordRunResult(true);
            _pathUi.Hide();
            ShowRunResult(false, "塔を踏破", ShowHub);
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
            _party.ResetBlood();
            _party.ResetCarriedFocus();
            _madness.ResetForNewRun(ComputeFullMadnessCap());
            _trust.LogEnd("全滅", _profile);
            _opinionRng = null;
            MetaProgression.ApplyDeathReset(_profile);
            RecordRunResult(false);
        }

        IEnumerator HandleDefeat(bool fromBattle, bool fromInsanity)
        {
            if (_defeatHandling)
                yield break;

            _defeatHandling = true;
            if (fromBattle)
                yield return new WaitForSecondsRealtime(1.2f);

            _overlay.HideAll();
            DestroyBattleIfAny();
            ApplyWipe();
            _pathUi.Hide();
            ShowRunResult(true, fromInsanity ? "発狂" : "死亡", ShowHub);
            _defeatHandling = false;
        }

        void OfferCardReward(bool mustTake = false, string notice = null, Action onFinished = null)
        {
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
            if (_dungeon.Current?.Type == DungeonNodeType.Combat)
                count += _trust.CollectBattle(_profile).CombatCardChoices;
            if (_profile.opinionPendingCardChoices > 0)
            {
                count += _profile.opinionPendingCardChoices;
                _profile.opinionPendingCardChoices = 0;
            }

            var partyNames = _party.Members
                .Select(m => m.character?.characterName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
            var excluded = _party.GetOwnedSkillIds();
            var swordsman = SwordsmanSkillConfig.Load();
            var axe = AxeSkillConfig.Load();
            if (pool?.cards != null)
            {
                foreach (var card in pool.cards)
                {
                    if (card == null)
                        continue;
                    if (swordsman != null && swordsman.HidesReward(card.skillId))
                        excluded.Add(card.skillId);
                    if (axe != null && axe.HidesReward(card))
                        excluded.Add(card.skillId);
                }
            }

            var choices = CardRewardPicker.PickChoices(
                pool,
                count,
                null,
                excluded,
                partyNames);
            if (choices.Count == 0)
            {
                FinishCardReward("刻印", "入手できる新しい刻印はなかった。");
                return;
            }

            _pendingRewardChoices = choices;
            _pendingRewardMustTake = mustTake;
            _pendingRewardNotice = notice;
            ShowPendingCardReward();
        }

        void ShowPendingCardReward()
        {
            if (_pendingRewardChoices == null || _pendingRewardChoices.Count == 0)
            {
                FinishCardReward();
                return;
            }

            try
            {
                ShowSideMap();
                _overlay.ShowCardReward(
                    transform,
                    _party,
                    _pendingRewardChoices,
                    HandleRewardCard,
                    _pendingRewardMustTake ? null : BeginSkillXpGrant,
                    _pendingRewardNotice);
            }
            catch (Exception ex)
            {
                Debug.LogError($"カード報酬 UI: {ex.Message}");
                FinishCardReward();
            }
        }

        void BeginSkillXpGrant()
        {
            _overlay.ShowSkillXpPick(
                transform,
                _party,
                SkillProgression.XpPerSkippedReward,
                (memberIndex, slotIndex) =>
                {
                    if (!_party.TryGrantSlotXp(memberIndex, slotIndex, SkillProgression.XpPerSkippedReward, out var grant))
                    {
                        ShowPendingCardReward();
                        return;
                    }

                    _pendingRewardChoices = null;
                    var body = grant.Leveled
                        ? $"{grant.SkillName}\nLv.{grant.PreviousLevel} → Lv.{grant.NewLevel}\n{SkillProgression.FormatXp(grant.Xp)}"
                        : $"{grant.SkillName}\n経験値 +{SkillProgression.XpPerSkippedReward}\n{SkillProgression.FormatXp(grant.Xp)}";
                    FinishCardReward(grant.Leveled ? "刻印強化" : "経験値", body);
                },
                ShowPendingCardReward);
        }

        void FinishCardReward(string modalTitle = null, string modalBody = null)
        {
            var next = _afterCardReward;
            _afterCardReward = null;
            _pendingRewardChoices = null;
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
                case RewardEquipOutcome.AlreadyOwned:
                    FinishCardReward(
                        "刻印",
                        $"{card.displayName} は所持済みです。\nレベルは戦闘の発動と、取得を見送ったときの経験値で上がります。");
                    break;
                case RewardEquipOutcome.NeedReplace:
                    _overlay.ShowReplaceSlot(
                        transform,
                        _party,
                        memberIndex,
                        card,
                        () => FinishCardReward(),
                        () => FinishCardReward());
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


        void ClearPendingEventBattle()
        {
            if (_profile == null)
                return;
            _profile.towerRunEventBattlePending = false;
            if (_profile.eventBattle != null)
                _profile.eventBattle.active = false;
        }

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

        void DestroyBattleIfAny()
        {
            if (_battle == null)
                return;
            Destroy(_battle.gameObject);
            _battle = null;
        }
    }
}
