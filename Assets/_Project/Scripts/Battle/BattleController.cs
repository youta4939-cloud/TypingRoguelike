using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TypingRoguelike.Battle.Visual;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public sealed partial class BattleController : MonoBehaviour
    {
        [SerializeField] BattleEncounterDefinition encounter;
        [SerializeField] JackpotPresentationDefinition jackpotPresentation;

        RunPartyState _partyState;
        RunMadnessState _madness;
        Action _onVictory;
        Action _onDefeat;
        public Action OnCorrectCharacter;
        Action _onInsanity;
        bool _outcomeHandled;
        EncounterBattleModifiers _encounterMods;
        HashSet<int> _innateSkillIds = new();

        float _tickInterval = 0.05f;
        float _wordClearDamage = 15f;
        float _totalAttack;
        float _wordDamageCoefficient = 0.04f;
        float _baseCritChance;
        float _critMultiplier = 1.5f;
        float _globalEnemyHpScale = 1f;
        readonly SkillDamageModifiers _skillDamage = new SkillDamageModifiers();
        int _critTestMode;
        bool _debugForceCrit;
        Func<BattleSkillData, float> _debugCritChance;
        Func<BattleSkillData, float?> _debugCritMultiplier;
        int _partyDefense;
        float _defenseConstant = 400f;
        float _jackpotAccumulationScale = 1f;
        int _maxRerolls = 3;
        float _enemyDamageMultiplier = 1f;
        float _enrageHpFraction = 0.5f;
        float _enrageIntervalMultiplier = 1f;
        string _emojiNormal = "🐲";
        string _emojiEnraged = "🔥🐲";
        string _emojiDefeated = "💀";
        EnemySwing _activeSwing;
        int _currentAttackSlot = -1;
        EnemyDefinition _battleEnemyDef;
        EnemySpritePairPresenter _spritePresenter;
        bool _spriteAttackStarted;
        float _spriteImpactLead;
        EnemyAttackPattern _spriteAttackPattern;
        bool _resolvingEnemySwing;

        List<BattleSkillData> _activeSkills;
        TypingEngine _typing;
        OrbStock _stock;
        RectTransform _pendingStockCard;
        bool _stockVisualReady;
        BattleUIRefs _ui;

        float _playerHp = 100f;
        float _maxPlayerHp = 100f;
        float _enemyHp = 1000f;
        float _maxEnemyHp = 1000f;
        float _enemyAttackSlowRemaining;
        int _enemyAttackSlowPercent;
        float _enemyStopRemaining;
        EnemyBreakState _enemyBreak;
        AudioSource _shieldSe;
        AudioClip _shieldCrackClip;
        AudioClip _enemyBreakClip;
        bool _negateMissHit;
        bool _missHitArmed;
        bool _negateMissUsed;
        bool _showEnemyAttackTime;
        bool _isEnraged;
        bool _isGameOver;
        bool _isPaused;
        bool _runMenuOpen;
        bool _countdownActive;
        bool _resultContinued;
        readonly HashSet<int> _cooldownSkillIds = new();
        readonly Queue<BattleSkillData> _cutinQueue = new();
        readonly Queue<PendingJackpot> _jackpotQueue = new();
        readonly List<string> _wordsSinceJackpot = new();
        JackpotPresentationDefinition _jackpotLook;
        AudioSource _jackpotSe;
        JackpotBgmSlot[] _heldBgm;
        Coroutine _bgmFade;
        readonly JackpotFreezeSystem _jackpot = new();
        bool _presentationPlaying;
        bool _jackpotPlaying;
        float _jackpotMultiplier = 2f;
        float _tickAccumulator;
        float _battleElapsed;
        int _hpBefore;
        int _madnessBefore;
        float _damageDealt;
        int _wordsCleared;
        int _skillsTriggered;
        int _dungeonFloor = 1;

        readonly List<GameObject> _stockOrbObjects = new();
        readonly List<GameObject> _candidateRows = new();
        TypingAudioPlayer _typingAudio;
        ExAreaConfig _exConfig;
        ExAreaState _ex;
        bool _focusCarryCommitted;
        string _focusCarryLog = "";
        string _pendingWordLog;
        string _recentSkillLog;
        ExAreaView _exView;
        ExAreaDebugPanel _exDebug;
        SwordsmanSkillRunner _swordsman;
        SwordsmanDebugPanel _swordsmanDebug;
        TextMeshProUGUI _kiwamiMark;
        BattleSkillData[] _debugAlice;
        float _kobuBonus;
        float _kiwamiCrit;
        float _exSpecialTimer;
        bool _exSpecialEnabled;
        readonly List<ExTickHit> _exHits = new List<ExTickHit>();

        public float ExportJackpotAccumulatedChance() => _jackpot.BaseAccumulatedChance;

        public void Initialize(
            BattleEncounterDefinition enc,
            RunPartyState party,
            RunMadnessState madness,
            Action onVictory,
            Action onDefeat = null,
            Action onInsanity = null,
            EncounterBattleModifiers mods = default,
            PartyCombatStats partyStats = default,
            int dungeonFloor = 1)
        {
            encounter = enc;
            _dungeonFloor = Mathf.Max(1, dungeonFloor);
            _partyState = party;
            _madness = madness;
            _onVictory = onVictory;
            _onDefeat = onDefeat;
            _onInsanity = onInsanity;
            _encounterMods = mods;
            _negateMissHit = mods.NegateMissHit;
            _showEnemyAttackTime = mods.ShowEnemyAttackTime;
            _missHitArmed = false;
            _negateMissUsed = false;
            _totalAttack = partyStats.TotalAttack;
            _wordDamageCoefficient = partyStats.WordDamageCoefficient > 0f
                ? partyStats.WordDamageCoefficient
                : 0.04f;
            _baseCritChance = Mathf.Clamp01(partyStats.BaseCritChance);
            _critMultiplier = partyStats.CritMultiplier > 0f ? partyStats.CritMultiplier : 1.5f;
            _globalEnemyHpScale = partyStats.GlobalEnemyHpScale > 0f ? partyStats.GlobalEnemyHpScale : 1f;
            _skillDamage.Clear();
            _critTestMode = 0;
            _debugForceCrit = false;
            _debugCritChance = null;
            _debugCritMultiplier = null;
            _partyDefense = partyStats.TotalDefense;
            _defenseConstant = partyStats.DefenseConstant > 0f ? partyStats.DefenseConstant : 400f;
            var hasPartyStats = partyStats.TotalAttack > 0 || partyStats.TotalDefense > 0 || partyStats.TotalLuck > 0;
            _jackpotAccumulationScale = hasPartyStats ? partyStats.JackpotAccumulationScale : 1f;
            _outcomeHandled = false;
            _resultContinued = false;
            _countdownActive = true;
            _battleElapsed = 0f;
            _damageDealt = 0f;
            _wordsCleared = 0;
            _skillsTriggered = 0;
            _cutinQueue.Clear();
            _jackpotQueue.Clear();
            _wordsSinceJackpot.Clear();
            _presentationPlaying = false;
            _jackpotPlaying = false;

            var enemy = ResolveBattleEnemy(enc);
            var breakColors = enemy != null && enemy.breakEnabled
                ? enemy.breakRequirements
                : Array.Empty<ElementColor>();
            _enemyBreak = new EnemyBreakState(breakColors,
                enemy != null ? enemy.breakDuration : 4f,
                enemy != null ? enemy.breakDamageMultiplier : 1.5f);
            // スキル一覧は Build に渡すので、先にロードアウトを組む
            ApplyEncounter(enc, party, partyStats, enemy);
            SetupTypingAudio(enc);
            _stock = new OrbStock();
            var visuals = BattleVisualContext.FromEncounter(enc, party, enemy);
            _ui = BattleUIBuilder.Build(transform, _activeSkills, _innateSkillIds, visuals);
            _ui?.AttackForecast?.gameObject.SetActive(false);
            // プレハブ経路は RespawnEnemy で後から作るため、ここでも取り直す
            _spritePresenter = _ui != null ? _ui.SpritePairPresenter : null;
            if (_spritePresenter == null && visuals != null && visuals.UseSpritePair && _ui?.EnemyHitRoot != null)
            {
                _spritePresenter = EnemySpritePairPresenter.Create(_ui.EnemyHitRoot, visuals);
                _ui.SpritePairPresenter = _spritePresenter;
                if (_spritePresenter != null)
                    _ui.BossPortrait = _spritePresenter.MainImage;
            }
            // ConfigureEnemySwings 時は Presenter 未作成のため、ここで初撃リードを付け直す
            RefreshSpriteAttackLeadForOpening();
            _ui.EnemyStatus?.Configure(_enemyBreak);
            // カウントダウン中はモーション時計を進めない
            _spritePresenter?.SetPaused(true);
            _shieldSe = gameObject.AddComponent<AudioSource>();
            _shieldSe.playOnAwake = false;
            _shieldCrackClip = Resources.Load<AudioClip>("Battle/Audio/ShieldCrack");
            _enemyBreakClip = Resources.Load<AudioClip>("Battle/Audio/EnemyBreak");
            RefreshPortraitImages(visuals);
            _typing.GenerateWords();
            SetupExArea();
            RefreshAllUI();
            SetupDeckBattle();
            var startMsg = _encounterMods.HasLogOverride ? _encounterMods.StartLogMessage : enc?.startLogMessage;
            var resolvedStartMessage = !string.IsNullOrEmpty(startMsg) ? startMsg : "戦闘開始！";
            if (_enemyStopRemaining > 0f)
                resolvedStartMessage += $"\n敵の攻撃が {_enemyStopRemaining:0} 秒止まっている。";
            if (_ui.ResultContinueButton != null)
                _ui.ResultContinueButton.onClick.AddListener(ContinueAfterResult);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_ui.DebugKillButton != null)
                _ui.DebugKillButton.onClick.AddListener(DebugInstantKillForTest);
            if (_ui.DebugCritTestButton != null)
                _ui.DebugCritTestButton.onClick.AddListener(CycleCritTest);
            if (_ui.DebugAliceSlashButton != null)
                _ui.DebugAliceSlashButton.onClick.AddListener(DebugAliceSlash);
#endif
            SetLog("戦闘準備...");
            StartCoroutine(PlayBattleCountdown(resolvedStartMessage));
        }

        /// <summary>報酬 UI 表示中は入力と敵タイマーを止める（Canvas は残す）。</summary>
        public void SetRunMenuOpen(bool open) => _runMenuOpen = open;

        IEnumerator PlayBattleCountdown(string startMessage)
        {
            if (_ui?.CountdownOverlay == null || _ui.CountdownRing == null || _ui.CountdownText == null)
            {
                _countdownActive = false;
                RevealAttackForecast();
                SetLog(startMessage);
                yield break;
            }

            _ui.CountdownOverlay.SetActive(true);
            var ringRt = _ui.CountdownRing.rectTransform;
            for (var number = 3; number >= 1; number--)
            {
                _ui.CountdownText.text = number.ToString();
                _ui.CountdownText.color = number == 1 ? BattleUITheme.AccentBoss : Color.white;
                var elapsed = 0f;
                const float stepDuration = 0.78f;
                while (elapsed < stepDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / stepDuration);
                    _ui.CountdownRing.fillAmount = 1f - t;
                    ringRt.localEulerAngles = new Vector3(0, 0, -360f * t);
                    var punch = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI);
                    _ui.CountdownText.rectTransform.localScale = Vector3.one * (1f + punch * 0.28f);
                    yield return null;
                }
            }

            _ui.CountdownRing.fillAmount = 1f;
            _ui.CountdownRing.color = BattleUITheme.AccentAlly;
            _ui.CountdownText.text = "GO!";
            _ui.CountdownText.fontSize = 72;
            _ui.CountdownText.color = BattleUITheme.AccentAlly;
            var goElapsed = 0f;
            while (goElapsed < 0.5f)
            {
                goElapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(goElapsed / 0.5f);
                _ui.CountdownText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 0.75f, t);
                yield return null;
            }

            _ui.CountdownOverlay.SetActive(false);
            _countdownActive = false;
            RevealAttackForecast();
            _battleElapsed = 0f;
            if (!_isPaused && _enemyStopRemaining <= 0f)
                _spritePresenter?.SetPaused(false);
            // GO 直後に開幕短縮で既にリード内なら、ここでモーション開始
            TryStartSpriteAttackMotion();
            SetLog(startMessage);
        }

        void RevealAttackForecast()
        {
            var forecast = _ui?.AttackForecast;
            if (forecast == null)
                return;
            RefreshAttackCountdown(_enemyStopRemaining);
            forecast.gameObject.SetActive(true);
        }

        void RefreshPortraitImages(BattleVisualContext visuals)
        {
            if (_ui == null || visuals == null)
                return;

            foreach (var pair in _ui.CharPortraits)
            {
                var img = pair.Value;
                if (img == null)
                    continue;

                visuals.Characters.TryGetValue(pair.Key, out var visual);
                var sprite = BattlePortraitResolver.LoadCharacter(pair.Key) ?? visual?.Portrait;
                if (sprite == null)
                    continue;

                img.sprite = sprite;
                img.color = Color.white;
            }

            if (_ui.BossPortrait == null || _spritePresenter != null)
                return;

            var boss = _battleEnemyDef != null
                ? BattlePortraitResolver.ResolveBossIllustration(_battleEnemyDef)
                : encounter?.enemy?.battleIllustration;
            if (boss == null)
                return;

            _ui.BossPortrait.sprite = boss;
            _ui.BossPortrait.color = Color.white;
            BattleUIBuilder.FitStaticEnemy(_ui.BossPortrait, visuals.IsLargeEnemy);
        }

        void ApplyEncounter(
            BattleEncounterDefinition enc,
            RunPartyState party,
            PartyCombatStats partyStats,
            EnemyDefinition enemy)
        {
            _innateSkillIds.Clear();
            if (party != null)
            {
                foreach (var m in party.Members)
                {
                    if (m.innateSlot != null)
                        _innateSkillIds.Add(m.innateSlot.skillId);
                }
            }

            if (enc == null)
            {
                _activeSkills = BattleLoadoutBuilder.CreateHardcodedFallbackLoadout();
                _typing = new TypingEngine(BattleLoadoutBuilder.CreateFallbackWordPool(), 3);
                _jackpot.Configure(0f, 0.0000075f * Mathf.Max(0f, _jackpotAccumulationScale), 0.99f, 0.1f);
                if (_madness != null)
                    _jackpot.ApplyRunAccumulatedChance(_madness.AccumulatedJackpotChance);
                _jackpotMultiplier = 2f;
                return;
            }

            _activeSkills = party != null ? party.BuildActiveSkills() : BattleLoadoutBuilder.BuildActiveSkills(enc.party);
            if (_activeSkills.Count == 0)
                _activeSkills = BattleLoadoutBuilder.CreateHardcodedFallbackLoadout();

            var words = BattleLoadoutBuilder.GetWords(enc.wordList);
            _maxRerolls = Mathf.Max(0, enc.rerollsPerBattle + _encounterMods.RerollBonus);
            _typing = new TypingEngine(words, _maxRerolls);
            _tickInterval = enc.enemyTickInterval;
            _wordClearDamage = enc.wordClearDamage > 0f ? enc.wordClearDamage : 15f;
            enc.ResolveJackpotRollParameters(
                out var jackpotBase,
                out var jackpotPerChar,
                out var jackpotMax,
                out var jackpotCap);
            _jackpot.Configure(
                jackpotBase,
                jackpotPerChar * Mathf.Max(0f, _jackpotAccumulationScale),
                jackpotMax,
                jackpotCap);
            if (_madness != null)
                _jackpot.ApplyRunAccumulatedChance(_madness.AccumulatedJackpotChance);
            _jackpotMultiplier = enc.jackpotMultiplier > 0f ? enc.jackpotMultiplier : 2f;
            _maxPlayerHp = enc.playerMaxHp;
            if (party != null && party.RunHpInitialized)
            {
                _maxPlayerHp = party.MaxHp;
                _playerHp = party.CurrentHp;
            }
            else
            {
                _playerHp = _maxPlayerHp;
                party?.InitRunHp(_maxPlayerHp);
            }

            _hpBefore = Mathf.RoundToInt(_playerHp);
            _madnessBefore = _madness != null ? _madness.CurrentMadness : 0;

            var hpMul = _encounterMods.EnemyHpMultiplier <= 0f ? 1f : _encounterMods.EnemyHpMultiplier;
            var dmgMul = _encounterMods.EnemyDamageMultiplier <= 0f ? 1f : _encounterMods.EnemyDamageMultiplier;

            var e = enemy != null ? enemy : enc.enemy;
            if (e != null)
            {
                _maxEnemyHp = e.maxHp * hpMul;
                _enemyHp = _maxEnemyHp;
                _enemyDamageMultiplier = dmgMul;
                _enemyAttackSlowRemaining = 0f;
                _enemyAttackSlowPercent = 0;
                _enemyStopRemaining = Mathf.Max(0f, _encounterMods.OpeningEnemyStopSeconds);
                _enrageIntervalMultiplier = LegacyEnrageIntervalMultiplier(e);
                _enrageHpFraction = e.enrageHpFraction > 0f ? e.enrageHpFraction : 0.5f;
                _emojiNormal = e.emojiNormal;
                _emojiEnraged = e.emojiEnraged;
                _emojiDefeated = e.emojiDefeated;
                _isEnraged = false;
                ConfigureEnemySwings(e);
                ApplyOpeningAttackDelta(_encounterMods.OpeningAttackTimeDeltaSeconds);
                ApplyFirstAttackDelayDelta(_encounterMods.FirstAttackDelayDeltaSeconds);
            }
        }

        EnemyDefinition ResolveBattleEnemy(BattleEncounterDefinition enc)
        {
            if (_encounterMods.EnemyOverride != null)
                return _encounterMods.EnemyOverride;

            var category = _encounterMods.EnemyTier switch
            {
                ExEnemyTier.Elite => EnemySpawnCategory.Elite,
                ExEnemyTier.Boss => EnemySpawnCategory.Boss,
                _ => EnemySpawnCategory.Normal
            };
            var catalog = EnemyCatalogDefinition.LoadOrFallback();
            return EnemyEncounterResolver.PickEnemy(enc, catalog, _dungeonFloor, category)
                   ?? enc?.enemy;
        }

        void ConfigureEnemySwings(EnemyDefinition enemy)
        {
            _activeSwing = default;
            _currentAttackSlot = -1;
            _battleEnemyDef = enemy;
            if (enemy == null)
                return;

            var first = EnemyDefinitionAttackUtil.FirstRotateSlot(enemy);
            if (first < 0)
            {
                Debug.LogWarning("敵に有効な攻撃パターンがありません。");
                return;
            }

            BeginAttackOnSlot(first);
            // 開幕短縮などで既に着弾リード内に入っている場合、1撃目だけモーション開始が欠けるのを防ぐ
            TryStartSpriteAttackMotion();
        }

        void BeginAttackOnSlot(int slotIndex)
        {
            if (_battleEnemyDef == null || slotIndex < 0)
            {
                _activeSwing = default;
                _currentAttackSlot = -1;
                return;
            }

            var pattern = EnemyDefinitionAttackUtil.GetResolvedSlot(_battleEnemyDef, slotIndex);
            if (!pattern.IsActive)
            {
                _activeSwing = default;
                _currentAttackSlot = -1;
                return;
            }

            _currentAttackSlot = slotIndex;
            var baseInterval = Mathf.Max(0.05f, pattern.intervalSeconds);
            var waitSeconds = pattern.firstDelaySeconds > 0f ? pattern.firstDelaySeconds : baseInterval;

            waitSeconds = IntervalLength(Mathf.Max(0.05f, waitSeconds));
            _spriteAttackStarted = false;
            _spriteAttackPattern = pattern;
            if (_spritePresenter != null)
            {
                _spriteImpactLead = Mathf.Max(0.05f, _spritePresenter.ResolveImpactLead(pattern));
                // 待ちが短すぎると同フレームで着弾が連発するので、演出リード＋余裕を下限にする
                waitSeconds = Mathf.Max(waitSeconds, _spriteImpactLead + 0.2f);
            }
            _activeSwing = EnemySwing.Open(
                pattern.damage,
                baseInterval,
                waitSeconds,
                pattern.TelegraphSeconds);
        }

        /// <summary>
        /// ApplyEncounter 時点では Presenter がまだ無いので、UI 生成後に初撃の着弾リードを付け直す。
        /// </summary>
        void RefreshSpriteAttackLeadForOpening()
        {
            if (_spritePresenter == null || !_activeSwing.Enabled || _spriteAttackStarted)
                return;

            _spriteImpactLead = Mathf.Max(0.05f, _spritePresenter.ResolveImpactLead(_spriteAttackPattern));
            var minWait = _spriteImpactLead + 0.2f;
            if (_activeSwing.Remaining < minWait)
            {
                _activeSwing.Remaining = minWait;
                if (_activeSwing.Cycle < _activeSwing.Remaining)
                    _activeSwing.Cycle = _activeSwing.Remaining;
            }
        }

        /// <summary>
        /// タイマー0＝着弾。モーションは ImpactLead 秒前から開始。
        /// 開幕短縮などでリードを大きく下回る場合は Remaining をリードまで戻し、
        /// ためをスキップせず着弾をわずかに遅らせる。
        /// </summary>
        void TryStartSpriteAttackMotion()
        {
            if (_spritePresenter == null || _spriteAttackStarted)
                return;
            if (_countdownActive)
                return;
            ref var swing = ref _activeSwing;
            if (!swing.Enabled)
                return;

            // Presenter 後付けや初期化順ずれで lead が 0 のまま残らないようにする
            if (_spriteImpactLead < 0.05f)
                _spriteImpactLead = Mathf.Max(0.05f, _spritePresenter.ResolveImpactLead(_spriteAttackPattern));

            if (swing.Remaining > _spriteImpactLead)
                return;

            // リード未満まで短縮されていたら、ため全量を確保してから開始（初撃の同時発火防止）
            if (swing.Remaining < _spriteImpactLead - 0.001f)
            {
                swing.Remaining = _spriteImpactLead;
                if (swing.Cycle < swing.Remaining)
                    swing.Cycle = swing.Remaining;
            }

            _spriteAttackStarted = true;
            _spritePresenter.SetAttackSpeedMul(EnemyAttackSpeedMultiplier());
            _spritePresenter.BeginAttack(_spriteAttackPattern, 0f);
            SyncSpriteAttackClock();
        }

        /// <summary>モーション時刻 = ImpactLead - Remaining（タイマー0で着弾）。</summary>
        void SyncSpriteAttackClock()
        {
            if (_spritePresenter == null || !_spriteAttackStarted)
                return;
            if (!_activeSwing.Enabled)
                return;
            if (_activeSwing.Remaining > _spriteImpactLead)
                return;
            _spritePresenter.SetAttackSpeedMul(EnemyAttackSpeedMultiplier());
            var motionTime = _spriteImpactLead - Mathf.Max(0f, _activeSwing.Remaining);
            _spritePresenter.SetAttackMotionTime(motionTime);
        }

        void AdvanceToNextRotatedAttack()
        {
            if (_battleEnemyDef == null || _currentAttackSlot < 0)
                return;

            var next = EnemyDefinitionAttackUtil.AdvanceRotateSlot(_battleEnemyDef, _currentAttackSlot);
            if (next < 0)
                return;
            BeginAttackOnSlot(next);
        }

        static float LegacyEnrageIntervalMultiplier(EnemyDefinition enemy)
        {
            if (enemy.attackSpeed > 0f && enemy.enragedAttackSpeed > 0f)
                return enemy.attackSpeed / enemy.enragedAttackSpeed;
            return enemy.enrageIntervalMultiplier > 0f ? enemy.enrageIntervalMultiplier : 1f;
        }

        void ApplyOpeningAttackDelta(float deltaSeconds)
        {
            if (Mathf.Approximately(deltaSeconds, 0f))
                return;
            if (deltaSeconds > 0f)
            {
                _enemyStopRemaining += deltaSeconds;
                return;
            }

            var sooner = -deltaSeconds;
            if (_enemyStopRemaining > 0f)
            {
                var used = Mathf.Min(_enemyStopRemaining, sooner);
                _enemyStopRemaining -= used;
                sooner -= used;
            }

            if (sooner <= 0f)
                return;
            ShortenSwing(ref _activeSwing, sooner);
            // 開幕短縮でも着弾リード分は残し、初撃のためが潰えないようにする
            if (_activeSwing.Enabled && _spriteImpactLead > 0.05f &&
                _activeSwing.Remaining < _spriteImpactLead)
            {
                _activeSwing.Remaining = _spriteImpactLead;
                if (_activeSwing.Cycle < _activeSwing.Remaining)
                    _activeSwing.Cycle = _activeSwing.Remaining;
            }
            TryStartSpriteAttackMotion();
        }

        void ApplyFirstAttackDelayDelta(float deltaSeconds)
        {
            if (Mathf.Approximately(deltaSeconds, 0f))
                return;

            if (_activeSwing.Enabled && _activeSwing.WaitingForOpening)
            {
                _activeSwing.Remaining = Mathf.Max(1f, _activeSwing.Remaining + deltaSeconds);
                _activeSwing.Cycle = _activeSwing.Remaining;
            }
        }

        static void ShortenSwing(ref EnemySwing swing, float seconds)
        {
            if (!swing.Enabled)
                return;
            swing.Remaining = Mathf.Max(0f, swing.Remaining - seconds);
        }

        void Update()
        {
            if (_isGameOver || _runMenuOpen || _jackpotPlaying)
                return;
            if (_countdownActive)
                return;

            HandleKeyboard();

            if (_isGameOver)
                return;

            if (_isPaused)
                return;

            if (DeckMode)
            {
                RefreshWordGuideHighlights();
                _ui?.AttackForecast?.Advance(Time.deltaTime);
            }

            var breakHeldThisFrame = _enemyBreak != null && _enemyBreak.IsBroken;
            if (_enemyBreak != null)
            {
                if (_enemyBreak.Tick(Time.deltaTime)) SetLog("敵がブレイクから復帰した。");
                _ui?.EnemyStatus?.Tick(_enemyBreak, Time.deltaTime);
            }

            TickExArea(Time.deltaTime);
            _swordsman?.Tick(Time.deltaTime);
            TickAxe(Time.deltaTime);
            RefreshKiwamiMark();
            RefreshSwordsmanDebug();

            _battleElapsed += Time.deltaTime;
            TickEnemyAttackSlow(Time.deltaTime);
            if (breakHeldThisFrame)
            {
                _spritePresenter?.SetPaused(true);
                HoldStoppedAttackBar();
                RefreshAttackCountdown(_enemyBreak.Remaining);
                RefreshHeavyTelegraph();
                return;
            }
            if (_enemyStopRemaining > 0f)
            {
                _spritePresenter?.SetPaused(true);
                _enemyStopRemaining -= Time.deltaTime;
                HoldStoppedAttackBar();
                RefreshAttackCountdown(_enemyStopRemaining);
                RefreshHeavyTelegraph();
                if (_enemyStopRemaining <= 0f)
                {
                    _enemyStopRemaining = 0f;
                    SetLog("敵が動き出した。");
                }

                return;
            }

            // 停止解除後はポーズを戻す（メニューポーズは別経路）
            if (!_isPaused)
                _spritePresenter?.SetPaused(false);

            _tickAccumulator += Time.deltaTime;
            // 1フレームで複数回着弾しない（ラグ後の catch-up で連打になるのを防ぐ）
            var resolvedSwingThisFrame = false;
            while (_tickAccumulator >= _tickInterval)
            {
                _tickAccumulator -= _tickInterval;
                if (TickEnemyTimer(out var resolved) && resolved)
                {
                    resolvedSwingThisFrame = true;
                    break;
                }
            }
            if (resolvedSwingThisFrame)
                _tickAccumulator = 0f;
            else
                SyncSpriteAttackClock();

            if (_ui?.AttackForecast != null)
                RefreshAttackCountdown(0f);
        }

        void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                TogglePause();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard.f4Key.wasPressedThisFrame)
            {
                _exDebug?.Toggle();
                _swordsmanDebug?.Toggle();
                _axeDebug?.Toggle();
                RefreshExDebug();
                RefreshSwordsmanDebug();
                RefreshAxeDebug();
                return;
            }
#endif

            if (_isPaused || _isGameOver)
                return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                var avoid = new HashSet<char>();
                _ex?.CollectFirstLetters(avoid, null);
                if (_typing.TryReroll(avoid))
                {
                    RepairExWords();
                    SetLog($"🔄 リロール実行！ (残り{_typing.RerollCount}回)");
                    RefreshWordBoxes(false);
                    RefreshRerollBadge();
                }
                else
                    SetLog("⚠️ リロール回数がありません！");
                return;
            }

            for (var c = 'a'; c <= 'z'; c++)
            {
                var key = Key.A + (c - 'a');
                if (!keyboard[key].wasPressedThisFrame)
                    continue;

                TypeBattleKey(c);
                break;
            }
        }

        void SetupTypingAudio(BattleEncounterDefinition enc)
        {
            _typingAudio = GetComponent<TypingAudioPlayer>();
            if (_typingAudio == null)
                _typingAudio = gameObject.AddComponent<TypingAudioPlayer>();
            _typingAudio.Setup(ResolveTypingAudio(enc));
        }

        static TypingAudioDefinition ResolveTypingAudio(BattleEncounterDefinition enc)
        {
            if (enc?.typingAudio != null)
                return enc.typingAudio;
            var loaded = Resources.Load<TypingAudioDefinition>("Battle/Audio/DefaultTypingAudio");
            return loaded != null
                ? loaded
                : ScriptableObject.CreateInstance<TypingAudioDefinition>();
        }

        void PlayTypingSound(KeyProcessResult result)
        {
            if (_typingAudio == null || _jackpotPlaying || _runMenuOpen || _countdownActive || _isPaused)
                return;

            if (result.Completed.HasValue || result.Advanced)
                _typingAudio.PlayCorrect();
            else if (result.Missed)
                _typingAudio.PlayMiss();
        }

        bool RelicJackpotActive() =>
            _madness != null &&
            _encounterMods.JackpotMadnessThreshold > 0 &&
            _madness.CurrentMadness >= _encounterMods.JackpotMadnessThreshold;

        float ComputeWordDamage(int inputLength)
        {
            var multiplier = _encounterMods.WordDamageMultiplier > 0f ? _encounterMods.WordDamageMultiplier : 1f;
            if (DeckMode)
                return Mathf.Max(1f, inputLength * Deck.DeckCombatTuning.WordDamagePerChar * multiplier);
            if (_totalAttack <= 0f || _wordDamageCoefficient <= 0f)
                return Mathf.Max(1f, Mathf.Round(_wordClearDamage * multiplier));
            var attack = Mathf.RoundToInt(_totalAttack);
            if (_axe != null)
                attack += _axe.WordAttackBonus();
            var word = CombatMath.WordDamage(attack, _wordDamageCoefficient);
            return Mathf.Max(1f, Mathf.Round(word * multiplier));
        }

        float CurrentJackpotRollBonus()
        {
            var bonus = _madness?.BonusJackpotChance ?? 0f;
            if (RelicJackpotActive())
                bonus += Mathf.Max(0f, _encounterMods.JackpotChanceBonus);
            return bonus;
        }

        void HoldStoppedAttackBar()
        {
            if (_ui?.AttackBar == null)
                return;

            _ui.AttackBar.fillAmount = 1f;
            _ui.AttackBar.color = new Color(0.45f, 0.85f, 1f, 0.95f);
        }

        void TickEnemyAttackSlow(float deltaSeconds)
        {
            if (_enemyAttackSlowRemaining <= 0f)
                return;

            _enemyAttackSlowRemaining -= deltaSeconds;
            if (_enemyAttackSlowRemaining <= 0f)
            {
                _enemyAttackSlowRemaining = 0f;
                _enemyAttackSlowPercent = 0;
            }
        }

        float EnemyAttackSpeedMultiplier()
        {
            var legacy = 1f;
            if (_enemyAttackSlowRemaining > 0f && _enemyAttackSlowPercent > 0)
                legacy = Mathf.Clamp01(1f - _enemyAttackSlowPercent / 100f);
            var axe = _axe != null ? _axe.Status.SpeedMultiplier : 1f;
            var mode = _axe?.Config?.options != null ? _axe.Config.options.aslowStacking : "max";
            if (mode == "add")
                return Mathf.Clamp01(1f - ((1f - legacy) + (1f - axe)));
            return Mathf.Min(legacy, axe);
        }

        bool TickEnemyTimer(out bool resolvedSwing)
        {
            resolvedSwing = false;
            var step = Mathf.Max(0.01f, _tickInterval) * EnemyAttackSpeedMultiplier();
            if (!_isGameOver && !_outcomeHandled)
                resolvedSwing = AdvanceActiveSwing(step);
            RefreshAttackPresentation();
            RefreshHeavyTelegraph();
            return true;
        }

        bool AdvanceActiveSwing(float step)
        {
            ref var swing = ref _activeSwing;
            if (!swing.Enabled || _isGameOver || _outcomeHandled || _resolvingEnemySwing)
                return false;

            swing.Remaining -= step;

            TryStartSpriteAttackMotion();
            SyncSpriteAttackClock();

            if (swing.Remaining > 0f)
                return false;

            _resolvingEnemySwing = true;
            try
            {
                // 再入で同じスイングが連発しないよう先に無効化
                var damage = swing.Damage;
                swing.Enabled = false;
                swing.Remaining = 0f;
                swing.WaitingForOpening = false;
                // ダメージ＝着弾。モーション／斬撃を先に着弾時刻へ合わせてから与ダメージ
                _spritePresenter?.NotifySwingImpact();
                EnemyAttack(damage);
                _ui?.AttackForecast?.BeginAttackCycle();
                AdvanceToNextRotatedAttack();
            }
            finally
            {
                _resolvingEnemySwing = false;
            }

            return true;
        }

        float IntervalLength(float baseInterval)
        {
            var intervalMul = _encounterMods.EnemyAttackIntervalMultiplier <= 0f
                ? 1f
                : _encounterMods.EnemyAttackIntervalMultiplier;
            var speedMul = _encounterMods.EnemySpeedMultiplier <= 0f
                ? 1f
                : _encounterMods.EnemySpeedMultiplier;
            var enrage = _isEnraged ? _enrageIntervalMultiplier : 1f;
            return Mathf.Max(0.05f, baseInterval * intervalMul * enrage / speedMul);
        }

        void RefreshAttackPresentation()
        {
            if (_ui?.AttackBar == null)
                return;
            if (!TryNextSwing(out var remaining, out var cycle))
            {
                _ui.AttackBar.fillAmount = 0f;
                RefreshAttackCountdown(0f);
                return;
            }

            var remainingFill = cycle <= 0f ? 0f : Mathf.Clamp01(remaining / cycle);
            var fill = _ui.AttackRingFillsOnApproach
                ? (1f - remainingFill) * (330f / 360f)
                : remainingFill;
            _ui.AttackBar.fillAmount = fill;
            if (_ui.AttackRingFillsOnApproach)
            {
                RefreshAttackCountdown(0f);
                return;
            }
            var danger = fill < 0.25f;
            var slowed = _enemyAttackSlowRemaining > 0f;
            var calm = new Color(0.32f, 0.8f, 1f, 0.95f);
            var warn = new Color(1f, 0.7f, 0.2f, 0.95f);
            _ui.AttackBar.color = danger
                ? Color.Lerp(new Color(1f, 0.08f, 0.1f), Color.white,
                    (Mathf.Sin(Time.unscaledTime * 12f) + 1f) * 0.18f)
                : slowed
                    ? Color.Lerp(BattleUITheme.AccentAlly, calm, 0.35f)
                    : Color.Lerp(warn, calm, Mathf.InverseLerp(0.25f, 0.6f, fill));
            if (_ui.EnemyAttackRingTrack != null)
            {
                _ui.EnemyAttackRingTrack.color = danger
                    ? new Color(0.35f, 0.04f, 0.06f, 0.85f)
                    : new Color(0.06f, 0.14f, 0.32f, 0.75f);
            }

            if (_ui.AttackBarLabel != null)
                _ui.AttackBarLabel.color = danger ? new Color(1f, 0.32f, 0.28f) : BattleUITheme.TextMuted;
            RefreshAttackCountdown(0f);
        }

        void RefreshHeavyTelegraph()
        {
            var callout = _ui?.HeavyAttackCallout;
            if (callout == null)
                return;

            var show = _activeSwing.Enabled &&
                       _activeSwing.Telegraph > 0f &&
                       _activeSwing.Remaining > 0f &&
                       _activeSwing.Remaining <= _activeSwing.Telegraph;
            if (callout.activeSelf != show)
                callout.SetActive(show);
            if (!show || _ui.HeavyAttackGauge == null)
                return;
            _ui.HeavyAttackGauge.fillAmount = Mathf.Clamp01(_activeSwing.Remaining / _activeSwing.Telegraph);
        }

        bool TryNextSwing(out float remaining, out float cycle)
        {
            remaining = 0f;
            cycle = 0f;
            if (!_activeSwing.Enabled)
                return false;
            remaining = _activeSwing.Remaining;
            cycle = _activeSwing.Cycle;
            return true;
        }

        void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_ui?.PauseOverlay != null)
                _ui.PauseOverlay.SetActive(_isPaused);
            // timeScale は止めないため、SpritePair は明示フラグで止める（unscaled 連打防止）
            _spritePresenter?.SetPaused(_isPaused);
            SetLog(_isPaused ? "⏸️ 一時停止中" : "再開しました！");
            if (!_isPaused)
                TryTriggerSkill();
        }

        void SetupExArea()
        {
            _exView?.Destroy();
            _exDebug?.Destroy();
            _exConfig = ExAreaConfig.Load();
            var slots = _exConfig.slots > 0 ? _exConfig.slots : 1;
            _ex = new ExAreaState(slots);
            _kobuBonus = 0f;
            _kiwamiCrit = 0f;
            _skillDamage.Clear();
            _skillDamage.AddCritChanceSource(FocusCritChance);
            _skillDamage.AddFinalMultiplierSource(KobuFinal);
            _skillDamage.AddCritMultiplierOverrideSource(KiwamiCrit);
            _swordsman = new SwordsmanSkillRunner(new BattleSwordsmanBridge(this), SwordsmanSkillConfig.Load());
            _swordsman.BeginBattle(_partyState != null ? _partyState.CarriedFocus : 0);
            SetupAxe();
            _exSpecialEnabled = _exConfig.enemySpecials != null && _exConfig.enemySpecials.enabled;
            _exSpecialTimer = NextSpecialDelay(true);
            if (_ui?.RootCard != null)
            {
                _exView = ExAreaView.Create(_ui.RootCard, slots);
                _exView.Refresh(_ex, _exConfig);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _exDebug = ExAreaDebugPanel.Create(
                    _ui.RootCard,
                    AddExColor,
                    kind =>
                    {
                        if (kind == ExKind.Focus)
                            _swordsman?.AddFocus(1, true, true);
                        else
                            AddExStatus(kind, 1);
                        RefreshExView();
                    },
                    () =>
                    {
                        _swordsman?.AddFocus(5, true, true);
                        RefreshExView();
                    },
                    () =>
                    {
                        _ex.ConsumeAll(ExIds.Focus);
                        RefreshExView();
                    },
                    () =>
                    {
                        _ex.Clear();
                        RefreshExView();
                    },
                    () =>
                    {
                        _exSpecialEnabled = !_exSpecialEnabled;
                        SetLog(_exSpecialEnabled ? "敵の特殊攻撃をオンにした" : "敵の特殊攻撃をオフにした");
                        RefreshExDebug();
                    },
                    () => TryEnemySpecial(true),
                    DebugExCommand);
                _swordsmanDebug?.Destroy();
                _swordsmanDebug = SwordsmanDebugPanel.Create(_ui.RootCard, _swordsman.Config, DebugEquipSwordsman, DebugResetSwordsman, DebugSwordsmanCommand);
                RefreshSwordsmanDebug();
#endif
                RaiseBattleOverlays();
            }
        }

        void RaiseBattleOverlays()
        {
            _ui?.CountdownOverlay?.transform.SetAsLastSibling();
            _ui?.ResultOverlay?.transform.SetAsLastSibling();
            _ui?.PauseOverlay?.transform.SetAsLastSibling();
            _ui?.CutinOverlay?.transform.SetAsLastSibling();
            _ui?.JackpotOverlay?.transform.SetAsLastSibling();
        }

        void TickExArea(float dt)
        {
            if (_ex == null || _exConfig == null || _isGameOver)
                return;
            RefreshSpecialLocks();
            _exHits.Clear();
            _ex.Tick(
                dt,
                _exConfig.poison.intervalSec,
                _exConfig.poison.damagePerStack,
                _exConfig.paralysis.intervalSec,
                _exConfig.paralysis.damagePerStack,
                _exConfig.frenzy.intervalSec,
                _exConfig.frenzy.madnessPerBurst,
                _exHits);
            foreach (var hit in _exHits)
            {
                if (_isGameOver)
                    break;
                if (hit.Kind == ExKind.Frenzy)
                    ApplyExMadness(hit.Amount);
                else
                    ApplyExPlayerDamage(hit.Amount, _exConfig.NameOf(hit.Kind) + "のダメージ " + hit.Amount);
            }

            if (_exSpecialEnabled && !_isGameOver && !(_enemyBreak?.IsBroken ?? false))
            {
                _exSpecialTimer -= dt;
                if (_exSpecialTimer <= 0f)
                {
                    TryEnemySpecial(false);
                    _exSpecialTimer = NextSpecialDelay(false);
                }
            }

            RefreshExView();
        }

        float NextSpecialDelay(bool opening)
        {
            var specials = _exConfig?.enemySpecials;
            if (specials == null)
                return 0f;
            if (opening)
            {
                var min = specials.firstDelayMinSec;
                var max = Mathf.Max(min, specials.firstDelayMaxSec);
                return UnityEngine.Random.Range(min, max);
            }

            var interval = specials.normalIntervalSec;
            if (_encounterMods.EnemyTier == ExEnemyTier.Elite && specials.eliteIntervalSec > 0f)
                interval = specials.eliteIntervalSec;
            else if (_encounterMods.EnemyTier == ExEnemyTier.Boss && specials.bossIntervalSec > 0f)
                interval = specials.bossIntervalSec;
            var jitter = Mathf.Clamp(specials.jitter, 0f, 0.9f);
            var scale = 1f + UnityEngine.Random.Range(-jitter, jitter);
            return Mathf.Max(0.05f, interval * scale);
        }

        void TryEnemySpecial(bool announceSkip)
        {
            if (_ex == null || _exConfig?.enemySpecials?.kinds == null)
                return;
            var choices = new List<ExKind>();
            foreach (var id in _exConfig.enemySpecials.kinds)
            {
                var kind = ExIds.Parse(id);
                if (kind == ExKind.None || kind == ExKind.ColorStock || kind == ExKind.Focus)
                    continue;
                if (_ex.CanAccept(kind))
                    choices.Add(kind);
            }

            if (choices.Count == 0)
            {
                if (announceSkip)
                    SetLog("特殊攻撃は置き場所がないので通常攻撃のまま");
                return;
            }

            var pick = choices[UnityEngine.Random.Range(0, choices.Count)];
            AddExStatus(pick, 1);
            SetLog(_exConfig.NameOf(pick) + "が降りてきた");
        }

        void AddExColor(string colorId)
        {
            if (_ex == null)
                return;
            var added = _ex.TryAddColor(colorId);
            if (!added.Accepted)
            {
                SetLog("色ストックは入らなかった");
                RefreshExView();
                return;
            }

            if (added.Ejected)
                SetLog("古い色ストックが押し出された");
            var slot = _ex.Slots[_ex.Count - 1];
            AssignExWord(slot);
            RepairExWords();
            RefreshExView();
        }

        void AddExStatus(ExKind kind, int times)
        {
            if (_ex == null || _exConfig == null || times <= 0)
                return;
            var effect = _exConfig.Effect(kind);
            var interval = effect != null ? effect.intervalSec : 0f;
            var maxStacks = kind == ExKind.Focus && effect != null ? effect.maxStacks : 0;
            var reset = kind == ExKind.Frenzy;
            for (var i = 0; i < times; i++)
            {
                var added = _ex.TryAddStatus(kind, maxStacks, interval, reset);
                if (!added.Accepted)
                {
                    SetLog(_exConfig.NameOf(kind) + "は入らなかった");
                    break;
                }

                if (added.Ejected)
                    SetLog("古い色ストックが押し出された");
            }

            ExSlot fresh = null;
            foreach (var slot in _ex.Slots)
            {
                if (slot.Kind == kind && kind != ExKind.Focus && !slot.HasWord)
                    fresh = slot;
            }
            if (fresh != null)
                AssignExWord(fresh);
            RepairExWords();
            RefreshExView();
        }

        bool AssignExWord(ExSlot slot)
        {
            if (slot == null || _typing == null)
                return false;
            var blocked = new HashSet<char>();
            _typing.CollectFirstLetters(blocked);
            _ex.CollectFirstLetters(blocked, slot);
            if (_typing.TryPickWord(blocked, out var display, out var inputs))
            {
                slot.SetWord(display, inputs);
                return true;
            }

            blocked.Clear();
            _ex.CollectFirstLetters(blocked, slot);
            if (!_typing.TryPickWord(blocked, out display, out inputs))
                return false;
            slot.SetWord(display, inputs);
            var avoid = new HashSet<char>();
            _ex.CollectFirstLetters(avoid, null);
            _typing.GenerateWords(avoid);
            RefreshWordBoxes(false);
            return true;
        }

        void RegenerateScreenWords()
        {
            var avoid = new HashSet<char>();
            _ex?.CollectFirstLetters(avoid, null);
            _typing.GenerateWords(avoid);
            RepairExWords();
        }

        void RepairExWords()
        {
            if (_ex == null || _typing == null)
                return;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var screen = new HashSet<char>();
                _typing.CollectFirstLetters(screen);
                if (!_ex.ClashesWith(screen))
                    return;
                var slot = _ex.FirstClashingSlot(screen);
                if (slot == null || slot.Progress > 0 || !AssignExWord(slot))
                    return;
            }
        }

        bool ScreenWordInProgress()
        {
            if (_typing == null)
                return false;
            foreach (var slot in _typing.Slots)
            {
                if (slot.Progress > 0)
                    return true;
            }
            return false;
        }

        void TypeBattleKey(char key)
        {
            if (_typing == null)
                return;

            RefreshSpecialLocks();
            var screenLive = ScreenWordInProgress();
            var exReady = _ex != null && _exConfig != null;
            var exLive = exReady && _ex.HasProgress;
            var screenResult = new KeyProcessResult(null, false, false);
            var exResult = ExKeyResult.None;
            var ranScreen = false;

            if (!screenLive && !exLive)
            {
                ranScreen = true;
                screenResult = _typing.ProcessKey(key);
                if (exReady && _ex.MatchesFirst(key))
                    exResult = _ex.ProcessKey(key, _exConfig.frenzy.intervalSec);
            }
            else
            {
                if (screenLive)
                {
                    ranScreen = true;
                    screenResult = _typing.ProcessKey(key, false);
                }
                if (exLive)
                    exResult = _ex.ProcessKey(key, _exConfig.frenzy.intervalSec);
            }

            var screenHit = ranScreen && (screenResult.Advanced || screenResult.Completed.HasValue);
            var exHit = exResult.Completed || (exResult.Advanced && !exResult.Missed);
            var hit = screenHit || exHit;
            if (hit)
                OnCorrectCharacter?.Invoke();
            if (!hit)
            {
                _missHitArmed = true;
                _typing.ResetActivity();
                if (exLive || exResult.Missed)
                    _ex.ClearTypingProgress();
            }

            WordCompletion? exCompleted = null;
            if (exResult.Completed && _exConfig != null)
            {
                exCompleted = new WordCompletion(
                    _exConfig.ToElement(exResult.ColorId),
                    exResult.InputLength,
                    exResult.DisplayText);
            }

            PlayTypingSound(new KeyProcessResult(screenResult.Completed ?? exCompleted, hit, !hit));
            if (exResult.Completed && !_isGameOver)
            {
                var cardFirst = _exConfig.specialRules == null || _exConfig.specialRules.cardDamageFirst;
                var damageCard = IsSpecialDamage(exResult.Kind);
                var cardLine = cardFirst && damageCard ? ApplySpecialCardHit(exResult) : null;
                if (!_isGameOver)
                {
                    var countAsWord = _exConfig.exWordCountsAsNormalWord;
                    var pushOrb = exResult.Kind == ExKind.ColorStock;
                    if (countAsWord)
                        ApplyWordRewards(exCompleted.Value, pushOrb, false, cardLine);
                    else if (pushOrb)
                    {
                        _stock.Push(_exConfig.ToElement(exResult.ColorId));
                        TryTriggerSkill();
                        RefreshStockUI();
                    }
                }

                if (!cardFirst && damageCard && !_isGameOver)
                    ApplySpecialCardHit(exResult);

                if (exResult.NeedsNewWord && !_isGameOver)
                {
                    foreach (var slot in _ex.Slots)
                    {
                        if (slot.Kind != exResult.Kind || slot.HasWord)
                            continue;
                        if (IsSpecialDamage(slot.Kind))
                            AssignCardWord(slot, _exConfig.FindCard(CardId(slot.Kind)));
                        else
                            AssignExWord(slot);
                        break;
                    }
                    RepairExWords();
                }
            }

            if (screenResult.Completed.HasValue && !_isGameOver)
            {
                ApplyWordRewards(screenResult.Completed.Value, true, true);
                if (_ex != null && _ex.HasProgress)
                {
                    _typing.ParkAll();
                    RefreshWordBoxes(false);
                }
            }
            else if (!_isGameOver)
                RefreshWordBoxes(false);

            if (!_isGameOver && !ScreenWordInProgress() && (_ex == null || !_ex.HasProgress))
            {
                _typing.ResetActivity();
                RefreshWordBoxes(false);
            }

            RefreshExView();
        }

        void ApplyWordRewards(WordCompletion completed, bool pushOrb, bool regenerateScreen, string logPrefix = null)
        {
            _wordsCleared++;
            var wentInsane = _madness != null && _madness.AddFromWordAttack(completed.InputLength);
            RefreshMadnessMeter();
            if (wentInsane)
            {
                SetLog("狂気が限界に達した… 発狂！");
                TriggerInsanityGameOver();
                return;
            }

            // Before word damage and automatic skills: the finishing word benefits too.
            if (pushOrb && _enemyBreak != null)
            {
                var wasBroken = _enemyBreak.IsBroken;
                var consumed = _enemyBreak.CompleteWord(completed.Color);
                if (consumed >= 0)
                {
                    _ui?.EnemyStatus?.Shatter(consumed);
                    var clip = _enemyBreak.IsBroken ? _enemyBreakClip : _shieldCrackClip;
                    if (clip != null) _shieldSe.PlayOneShot(clip, _enemyBreak.IsBroken ? 0.75f : 0.45f);
                }
                if (!wasBroken && _enemyBreak.IsBroken)
                {
                    _spritePresenter?.SetBroken(true);
                    SetLog($"BREAK！ {_enemyBreak.Duration:0.#}秒停止・与ダメージ×{_enemyBreak.DamageMultiplier:0.##}");
                }

                if (wasBroken && !_enemyBreak.IsBroken)
                    _spritePresenter?.SetBroken(false);
            }

            _wordsSinceJackpot.Add(string.IsNullOrEmpty(completed.DisplayText) ? "？" : completed.DisplayText);
            var bonusJackpot = CurrentJackpotRollBonus();
            var jackpotWon = _jackpot.RegisterWordAndRoll(
                bonusJackpot,
                completed.InputLength,
                RunDebugState.JackpotRollChanceOverride);
            PersistJackpotAccumulation();
            RefreshJackpotMeter();
            if (jackpotWon)
            {
                var jackpotDamage = _madness?.ComputeJackpotDamage(_jackpotMultiplier) ?? 0;
                if (RelicJackpotActive())
                {
                    var mul = _encounterMods.JackpotDamageMultiplier > 0f
                        ? _encounterMods.JackpotDamageMultiplier
                        : 1f;
                    jackpotDamage = Mathf.Max(0, Mathf.RoundToInt(jackpotDamage * mul));
                }
                _madness?.ResetMadnessAfterJackpot();
                _jackpot.Reset();
                PersistJackpotAccumulation();
                RefreshMadnessMeter();
                if (!_isGameOver)
                {
                    var words = _wordsSinceJackpot.ToArray();
                    _wordsSinceJackpot.Clear();
                    EnqueueJackpot(jackpotDamage, words);
                }
            }
            else
            {
                var wordDamage = WithIncoming(ComputeWordDamage(completed.InputLength), AxeIncomingEnabled(true, false));
                var line = $"単語クリア！ {(int)wordDamage} ダメージ！";
                if (!string.IsNullOrEmpty(logPrefix))
                    line = logPrefix + "\n" + line;
                _pendingWordLog = line;
                ApplyDamageToEnemy(wordDamage, line, true, false, false);
            }

            if (_isGameOver)
            {
                _pendingWordLog = null;
                return;
            }

            if (pushOrb)
            {
                _pendingStockCard = CompletedCard(completed);
                _stock.Push(completed.Color);
                if (regenerateScreen)
                    RegenerateScreenWords();
                TryTriggerSkill();
                RefreshStockUI();
            }
            else if (regenerateScreen)
            {
                RegenerateScreenWords();
            }

            RefreshWordBoxes(false);
            if (_enemyHp <= 0f && !_outcomeHandled)
                ResolveEnemyDown(null);
            else if (!_outcomeHandled)
                _pendingWordLog = null;
        }

        void ApplyExPlayerDamage(int amount, string log)
        {
            if (amount <= 0 || _isGameOver || _outcomeHandled)
                return;
            var scaled = (float)amount;
            ApplyDefensiveMitigation(ref scaled, true);
            amount = Mathf.Max(0, Mathf.RoundToInt(scaled));
            if (amount <= 0)
                return;
            _playerHp = Mathf.Max(0f, _playerHp - amount);
            RefreshHpBars();
            _ui?.Stock?.PlayHit(amount);
            SetLog(log);
            if (_playerHp <= 0f)
            {
                _isGameOver = true;
                SetLog("☠️ 探索者は力尽きた... GAME OVER");
                CompleteDefeat();
            }
        }

        void ApplyExMadness(int amount)
        {
            if (amount == 0 || _madness == null || _isGameOver || _outcomeHandled)
                return;
            SetLog("伝播が弾けた");
            var insane = _madness.ApplyDeltaAndCheckInsanity(amount);
            RefreshMadnessMeter();
            if (!insane)
                return;
            SetLog("狂気が限界に達した… 発狂！");
            TriggerInsanityGameOver();
        }

        void RefreshExView()
        {
            _exView?.Refresh(_ex, _exConfig);
            _exView?.SetBlood(_partyState != null ? _partyState.Blood : 0);
            RefreshExDebug();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshDebugCombatReadout();
#endif
        }

        void RefreshExDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_exDebug == null || !_exDebug.IsOpen || _ex == null || _exConfig == null)
                return;
            var crit = _ex.Stack(ExKind.Focus) * _exConfig.focus.critChancePerStack * 100f;
            _exDebug.SetStatus(
                StatusLine(_exConfig.NameOf(ExKind.Frenzy), ExKind.Frenzy) + "\n" +
                StatusLine(_exConfig.NameOf(ExKind.Poison), ExKind.Poison) + "\n" +
                StatusLine(_exConfig.NameOf(ExKind.Paralysis), ExKind.Paralysis) + "\n" +
                "会心 " + crit.ToString("0.#") + "%\n" +
                "特殊攻撃 " + (_exSpecialEnabled ? "オン" : "オフ") + "\n" +
                SpecialCardStatus());
#endif
        }

        string StatusLine(string name, ExKind kind)
        {
            if (_ex.Stack(kind) <= 0)
                return name + " なし";
            return name + " 次まで " + Mathf.Max(0f, _ex.Timer(kind)).ToString("0.0") + "秒";
        }

        void ClearExArea()
        {
            var kept = CommitCarriedFocus();
            _ex?.Clear();
            if (kept > 0 && _ex != null)
            {
                var max = _exConfig?.focus != null && _exConfig.focus.maxStacks > 0
                    ? _exConfig.focus.maxStacks
                    : 20;
                _ex.AddStacks(ExKind.Focus, kept, max);
            }

            RefreshExView();
        }

        int CommitCarriedFocus()
        {
            if (_focusCarryCommitted || _swordsman == null || _partyState == null || _ex == null)
                return _partyState != null ? _partyState.CarriedFocus : 0;
            _focusCarryCommitted = true;
            var held = _ex.Stack(ExKind.Focus);
            var kept = _swordsman.ComputeCarry(held);
            _partyState.SetCarriedFocus(kept);
            _focusCarryLog = held > 0 ? _swordsman.CarryMessage(kept) : "";
            return kept;
        }

        public bool AddSpecialCard(SpecialCardKind kind, int count, int casterIndex, int bloodOnKill = 1)
        {
            if (_ex == null || _exConfig == null || count <= 0)
                return false;
            var card = _exConfig.FindCard(CardId(kind));
            var name = card != null && !string.IsNullOrEmpty(card.nameJa) ? card.nameJa : kind.ToString();
            var placed = 0;
            for (var i = 0; i < count; i++)
            {
                if (kind == SpecialCardKind.RedRefill)
                {
                    var color = card != null && !string.IsNullOrEmpty(card.color) ? card.color : "red";
                    var added = _ex.TryAddColor(color);
                    if (!added.Accepted)
                    {
                        SetLog("Exエリアがいっぱいで" + name + "が入らない");
                        break;
                    }

                    if (added.Ejected)
                        SetLog("古い色ストックが押し出された");
                    AssignCardWord(_ex.Slots[_ex.Count - 1], card);
                    placed++;
                    continue;
                }

                var result = _ex.TryAddSpecial(ToExKind(kind), casterIndex, bloodOnKill);
                if (!result.Accepted)
                {
                    SetLog("Exエリアがいっぱいで" + name + "が入らない");
                    break;
                }

                if (result.Ejected)
                    SetLog("古い色ストックが押し出された");
                var slot = FindExSlot(ToExKind(kind));
                if (slot != null)
                {
                    slot.ConsumeAllOnType = kind == SpecialCardKind.Battou && ConsumesAll(card);
                    if (!slot.HasWord)
                        AssignCardWord(slot, card);
                }

                placed++;
            }

            RegenerateScreenWords();
            RefreshExView();
            return placed == count;
        }

        public int GetCount(SpecialCardKind kind)
        {
            if (_ex == null)
                return 0;
            if (kind != SpecialCardKind.RedRefill)
                return _ex.Stack(ToExKind(kind));
            var card = _exConfig?.FindCard("red_refill");
            var color = card != null && !string.IsNullOrEmpty(card.color) ? card.color : "red";
            var count = 0;
            foreach (var slot in _ex.Slots)
            {
                if (slot.Kind == ExKind.ColorStock && slot.ColorId == color)
                    count++;
            }
            return count;
        }

        void DebugExCommand(string command)
        {
            var caster = AliceOrFirstIndex();
            switch (command)
            {
                case "red":
                    AddSpecialCard(SpecialCardKind.RedRefill, 1, caster);
                    break;
                case "battou":
                    AddSpecialCard(SpecialCardKind.Battou, 1, caster);
                    break;
                case "battou5":
                    AddSpecialCard(SpecialCardKind.Battou, 5, caster);
                    break;
                case "issen":
                    AddSpecialCard(SpecialCardKind.IssenKiwami, 1, caster);
                    break;
                case "issen5":
                    AddSpecialCard(SpecialCardKind.IssenKiwami, 5, caster);
                    break;
                case "hyakki":
                    AddSpecialCard(SpecialCardKind.HyakkiYakou, 1, caster);
                    break;
                case "blood5":
                    _partyState?.AddBlood(5, HyakkiBloodCap());
                    RefreshExView();
                    break;
                case "blood0":
                    _partyState?.ResetBlood();
                    RefreshExView();
                    break;
                case "consume":
                    ToggleBattouConsume();
                    break;
            }
        }

        void ToggleBattouConsume()
        {
            var card = _exConfig?.FindCard("battou");
            if (card == null)
                return;
            card.consume = ConsumesAll(card) ? "one" : "all";
            var slot = FindExSlot(ExKind.Battou);
            if (slot != null)
                slot.ConsumeAllOnType = ConsumesAll(card);
            SetLog(ConsumesAll(card) ? "抜刀の使い方：全部" : "抜刀の使い方：1つ");
            RefreshExDebug();
        }

        void RefreshSpecialLocks()
        {
            if (_ex == null || _exConfig == null)
                return;
            var block = _exConfig.specialRules != null
                && _exConfig.specialRules.blockedByParalysis
                && _ex.Stack(ExKind.Paralysis) > 0;
            foreach (var slot in _ex.Slots)
            {
                var locked = block && IsSpecialDamage(slot.Kind);
                slot.InputLocked = locked;
                if (locked)
                    slot.Progress = 0;
            }
        }

        string ApplySpecialCardHit(ExKeyResult result)
        {
            var card = _exConfig?.FindCard(CardId(result.Kind));
            if (card == null || _isGameOver)
                return null;
            _swordsman?.NoteExCard(card.id);
            var stacks = result.StackCount > 0 ? result.StackCount : 1;
            var percent = card.damagePercent;
            if (result.Kind == ExKind.Battou)
                percent = SpecialCardMath.Percent(card.damagePercent, card.percentPerCount, stacks);
            else if (result.Kind == ExKind.HyakkiYakou)
                percent = SpecialCardMath.Percent(card.damagePercent, card.percentPerBlood, _partyState != null ? _partyState.Blood : 0);

            var caster = CasterMember(result.CasterIndex);
            var attack = RunPartyState.GetAttack(caster);
            var skill = new BattleSkillData(
                0,
                caster?.character != null ? caster.character.characterName : "",
                card.nameJa,
                Array.Empty<ElementColor>(),
                0,
                0,
                "",
                casterAttack: attack,
                casterIndex: result.CasterIndex,
                damagePercent: percent,
                forceCrit: card.forceCrit);
            var asSkill = _exConfig.specialRules == null || _exConfig.specialRules.countsAsSkillDamage;
            var hit = ComputeSkillHit(skill, asSkill);
            var shown = Mathf.RoundToInt(hit.Damage);
            if (shown <= 0)
                return null;
            shown = Mathf.RoundToInt(WithIncoming(shown, AxeIncomingEnabled(false, true)));
            var title = string.IsNullOrEmpty(card.nameJa) ? "カード" : card.nameJa;
            var line = hit.IsCrit ? $"会心！ {title} {shown}ダメ" : $"{title}×{stacks}！ {shown}ダメ";
            var multiplier = _encounterMods.PlayerDamageMultiplier > 0f ? _encounterMods.PlayerDamageMultiplier : 1f;
            if (result.Kind == ExKind.HyakkiYakou && _enemyHp > 0f && shown * multiplier * (_enemyBreak?.DamageMultiplier ?? 1f) >= _enemyHp)
            {
                var gain = result.BloodOnKill > 0 ? result.BloodOnKill : 1;
                var total = _partyState != null ? _partyState.AddBlood(gain, card.bloodCap) : gain;
                line += $"\n怨血 +{gain}（合計{total}）";
            }

            ApplyDamageToEnemy(shown, line, true, false);
            return _isGameOver ? null : line;
        }

        void AssignCardWord(ExSlot slot, ExCardDefinition card)
        {
            if (slot == null)
                return;
            var mode = _exConfig != null ? _exConfig.CardWordMode(card) : "fixed";
            if (mode == "pool" || card == null || string.IsNullOrEmpty(card.word))
            {
                AssignExWord(slot);
                return;
            }

            var inputs = TypingEngine.ExpandInputVariants(card.word);
            if (inputs == null || inputs.Length == 0)
                inputs = new[] { card.word };
            slot.SetWord("", inputs);
        }

        string SpecialCardStatus()
        {
            if (_ex == null || _exConfig == null)
                return "";
            var battou = _exConfig.FindCard("battou");
            var lines = new List<string>
            {
                "抜刀の使い方 " + (ConsumesAll(battou) ? "全部" : "1つ"),
                "怨血 " + (_partyState != null ? _partyState.Blood : 0) + CapLabel(HyakkiBloodCap())
            };
            AppendCardPreview(lines, ExKind.Battou);
            AppendCardPreview(lines, ExKind.IssenKiwami);
            AppendCardPreview(lines, ExKind.HyakkiYakou);
            return string.Join("\n", lines);
        }

        void AppendCardPreview(List<string> lines, ExKind kind)
        {
            var count = _ex.Stack(kind);
            if (count <= 0)
                return;
            var card = _exConfig.FindCard(CardId(kind));
            var slot = FindExSlot(kind);
            var caster = CasterMember(slot != null ? slot.CasterIndex : -1);
            var attack = RunPartyState.GetAttack(caster);
            var casterName = caster?.character != null
                ? StarterCharacters.Shown(caster.character.characterName)
                : "?";
            var percent = card != null ? card.damagePercent : 0f;
            if (kind == ExKind.Battou && card != null)
                percent = SpecialCardMath.Percent(card.damagePercent, card.percentPerCount, count);
            else if (kind == ExKind.HyakkiYakou && card != null)
                percent = SpecialCardMath.Percent(card.damagePercent, card.percentPerBlood, _partyState != null ? _partyState.Blood : 0);
            var normal = Preview(attack, percent, false);
            var crit = Preview(attack, percent, true);
            lines.Add($"{_exConfig.NameOf(kind)}×{count} {casterName} ATK{attack} 会心なし{normal}／会心{crit}");
        }

        int Preview(int attack, float percent, bool forceCrit)
        {
            var critMultiplier = _skillDamage.CritMultiplierOverride(null)
                ?? (_critMultiplier > 0f ? _critMultiplier : 1.5f);
            var asSkill = _exConfig.specialRules == null || _exConfig.specialRules.countsAsSkillDamage;
            var hit = CombatMath.ComputeSkillDamage(new SkillDamageRequest
            {
                CasterAttack = attack,
                Percent = percent,
                SkillOwnMultiplier = 1f,
                FinalMultiplier = asSkill ? _skillDamage.FinalMultiplier(null) : 1f,
                CritChance = 0f,
                ForceCrit = forceCrit,
                CritMultiplier = critMultiplier,
                Roll = 1f
            });
            return Mathf.RoundToInt(hit.Damage);
        }

        static string CapLabel(int cap) => cap > 0 ? "／上限" + cap : "／上限なし";

        int HyakkiBloodCap()
        {
            var card = _exConfig?.FindCard("hyakki_yakou");
            return card != null ? card.bloodCap : 0;
        }

        static bool ConsumesAll(ExCardDefinition card) =>
            card == null || !string.Equals(card.consume, "one", StringComparison.OrdinalIgnoreCase);

        static bool IsSpecialDamage(ExKind kind) =>
            kind == ExKind.Battou || kind == ExKind.IssenKiwami || kind == ExKind.HyakkiYakou;

        static ExKind ToExKind(SpecialCardKind kind)
        {
            switch (kind)
            {
                case SpecialCardKind.Battou: return ExKind.Battou;
                case SpecialCardKind.IssenKiwami: return ExKind.IssenKiwami;
                case SpecialCardKind.HyakkiYakou: return ExKind.HyakkiYakou;
                default: return ExKind.ColorStock;
            }
        }

        static string CardId(SpecialCardKind kind)
        {
            switch (kind)
            {
                case SpecialCardKind.RedRefill: return "red_refill";
                case SpecialCardKind.Battou: return "battou";
                case SpecialCardKind.IssenKiwami: return "issen_kiwami";
                default: return "hyakki_yakou";
            }
        }

        static string CardId(ExKind kind)
        {
            switch (kind)
            {
                case ExKind.Battou: return "battou";
                case ExKind.IssenKiwami: return "issen_kiwami";
                case ExKind.HyakkiYakou: return "hyakki_yakou";
                default: return "red_refill";
            }
        }

        ExSlot FindExSlot(ExKind kind)
        {
            if (_ex == null)
                return null;
            foreach (var slot in _ex.Slots)
            {
                if (slot.Kind == kind)
                    return slot;
            }
            return null;
        }

        MemberRunState CasterMember(int index)
        {
            var members = _partyState?.Members;
            if (members == null || members.Count == 0)
                return null;
            if (index >= 0 && index < members.Count)
                return members[index];
            return members[0];
        }

        int AliceOrFirstIndex()
        {
            var members = _partyState?.Members;
            if (members == null || members.Count == 0)
                return 0;
            for (var i = 0; i < members.Count; i++)
            {
                var name = members[i]?.character != null ? members[i].character.characterName : "";
                if (StarterCharacters.Same(name, StarterCharacters.Chara01))
                    return i;
            }

            return 0;
        }

        static bool IsAliceSkill(BattleSkillData skill) =>
            skill != null && StarterCharacters.Same(skill.CharacterName, StarterCharacters.Chara01);

        float FocusCritChance(BattleSkillData skill)
        {
            if (!IsAliceSkill(skill) || _ex == null || _exConfig?.focus == null)
                return 0f;
            return Mathf.Max(0f, _ex.Stack(ExKind.Focus) * _exConfig.focus.critChancePerStack);
        }

        SkillDamageResult ComputeSkillHit(
            BattleSkillData skill,
            bool asSkillDamage = true,
            float? percent = null,
            float skillOwnMultiplier = 1f,
            bool? forceCrit = null)
        {
            var chance = Mathf.Clamp01(_baseCritChance + _skillDamage.SumCritChance(skill));
            var critMultiplier = _skillDamage.CritMultiplierOverride(skill)
                ?? (_critMultiplier > 0f ? _critMultiplier : 1.5f);
            var request = new SkillDamageRequest
            {
                CasterAttack = skill.CasterAttack,
                Percent = percent ?? skill.DamagePercent,
                FlatDamage = skill.Damage,
                SkillOwnMultiplier = skillOwnMultiplier,
                FinalMultiplier = asSkillDamage ? _skillDamage.FinalMultiplier(skill) : 1f,
                CritChance = chance,
                ForceCrit = (forceCrit ?? skill.ForceCrit) || _debugForceCrit,
                CritMultiplier = critMultiplier,
                Roll = UnityEngine.Random.value
            };
            return CombatMath.ComputeSkillDamage(request);
        }

        void TryTriggerSkill()
        {
            if (DeckMode)
            {
                TryTriggerDeckCards();
                return;
            }

            if (_ex != null && _ex.Stack(ExKind.Paralysis) > 0)
            {
                var pending = SkillResolver.ResolveTriggeredSkills(
                    EligibleSkills(), _stock, _cooldownSkillIds, out var waiting);
                if (pending.Count > 0 || waiting.Count > 0)
                    SetLog("麻痺でスキルが出ない");
                return;
            }

            var skills = SkillResolver.ResolveTriggeredSkills(
                EligibleSkills(), _stock, _cooldownSkillIds, out var blocked);
            if (skills.Count == 0)
            {
                if (blocked.Count > 0)
                {
                    var names = string.Join("・", blocked.Select(s => s.DisplayName));
                    SetLog(
                        $"⏳ {names}：オーブは揃ったがクールダウン中（別の刻印を発動後に再度可）");
                    RefreshCandidatesUI();
                }
                else
                    _cooldownSkillIds.Clear();

                return;
            }

            _cooldownSkillIds.Clear();
            foreach (var skill in skills)
                _cooldownSkillIds.Add(skill.Id);

            var simultaneous = skills.Count > 1;
            var levelNotes = new List<string>();
            var focusCrit = false;
            foreach (var skill in skills)
            {
                var note = GrantActivationXp(skill);
                if (note != null)
                    levelNotes.Add(note);
                if (_swordsman != null && _swordsman.Owns(skill))
                {
                    if (ApplySwordsmanSkill(skill, simultaneous, simultaneous ? null : note))
                        focusCrit = true;
                }
                else if (_axe != null && _axe.Owns(skill))
                {
                    ApplyAxeSkill(skill, simultaneous, simultaneous ? null : note);
                    _swordsman?.OnAllySkillFired(skill);
                }
                else
                {
                    if (ApplySkillEffects(skill, simultaneous, simultaneous ? null : note))
                        focusCrit = true;
                    _swordsman?.OnAllySkillFired(skill);
                }
            }

            if (simultaneous)
            {
                var summary = string.Join(" + ", skills.Select(s => $"{s.CharacterName}/{s.DisplayName}"));
                if (levelNotes.Count > 0)
                    summary += "\n" + string.Join(" / ", levelNotes);
                if (focusCrit)
                    summary += "\n会心！";
                SetLog($"✨ 同時発動！ {summary}");
            }

            foreach (var skill in skills)
            {
                _skillsTriggered++;
                PulseCharCard(skill.CharacterName);
                if (_ui?.E5b != null)
                    _ui.E5b.Play(skill);
            }

            RefreshSkillCooldownUI();
            RefreshStockUI();
            RefreshRerollBadge();
            RefreshCandidatesUI();
        }

        string GrantActivationXp(BattleSkillData skill)
        {
            if (_partyState == null || skill == null)
                return null;
            if (!_partyState.TryGrantSkillXp(skill.Id, SkillProgression.XpPerActivation, out var grant) || !grant.Leveled)
                return null;
            return $"{grant.SkillName} が Lv.{grant.NewLevel} になった";
        }

        bool ApplySkillEffects(BattleSkillData skill, bool suppressLog, string levelNote = null)
        {
            var crit = false;
            string msg;
            if (skill.DealsDamage)
            {
                var result = ComputeSkillHit(skill);
                var damage = Mathf.Max(1f, Mathf.Round(result.Damage));
                damage = WithIncoming(damage, true);
                crit = result.IsCrit;
                msg = BuildSkillTriggerMessage(skill, Mathf.RoundToInt(damage), crit);
                if (!string.IsNullOrEmpty(levelNote))
                    msg += "\n" + levelNote;
                ApplyDamageToEnemy(damage, suppressLog ? null : msg, true, false);
            }
            else
            {
                msg = BuildSkillTriggerMessage(skill, 0, false);
                if (!string.IsNullOrEmpty(levelNote))
                    msg += "\n" + levelNote;
                if (!suppressLog)
                    SetLog(msg);
            }

            if (skill.Heal > 0)
            {
                _playerHp = Mathf.Min(_maxPlayerHp, _playerHp + skill.Heal);
                RefreshHpBars();
            }

            ApplySkillUtilityEffects(skill);
            return crit;
        }

        void EnqueueCutin(BattleSkillData skill)
        {
            _cutinQueue.Enqueue(skill);
            EnsurePresentationQueue();
        }

        void EnqueueJackpot(int damage, string[] words)
        {
            _jackpotPlaying = true;
            _jackpotQueue.Enqueue(new PendingJackpot(damage, words));
            EnsurePresentationQueue();
        }

        void EnsurePresentationQueue()
        {
            if (!_presentationPlaying)
                StartCoroutine(ProcessPresentationQueue());
        }

        IEnumerator ProcessPresentationQueue()
        {
            _presentationPlaying = true;
            while (_cutinQueue.Count > 0 || _jackpotQueue.Count > 0)
            {
                while (_cutinQueue.Count > 0)
                    yield return PlayCutinRoutine(_cutinQueue.Dequeue());

                if (_jackpotQueue.Count > 0)
                    yield return PlayJackpotSequence(_jackpotQueue.Dequeue());
            }

            _presentationPlaying = false;
        }

        static string BuildSkillTriggerMessage(BattleSkillData skill, int damage, bool crit)
        {
            var parts = new List<string>();
            if (skill.DealsDamage)
                parts.Add(crit ? $"会心！ {damage}ダメ" : $"{damage}ダメ");
            if (skill.Heal > 0)
                parts.Add($"{skill.Heal}回復");
            if (skill.EffectKind == SkillEffectKind.EnemyDelay && skill.EffectValue > 0f)
            {
                var slowPct = skill.EffectValueInt > 0 ? skill.EffectValueInt : 50;
                parts.Add($"敵攻撃{slowPct}%減・{skill.EffectValue:0.#}秒");
            }
            if (skill.EffectKind == SkillEffectKind.RerollRestore && skill.EffectValueInt > 0)
                parts.Add($"リロール+{skill.EffectValueInt}");
            if (parts.Count == 0 && !string.IsNullOrEmpty(skill.EffectText))
                parts.Add(skill.EffectText);
            var detail = parts.Count > 0 ? string.Join(" + ", parts) : "発動";
            return $"✨ {StarterCharacters.Shown(skill.CharacterName)}の{skill.DisplayName}！ {detail}";
        }

        void ApplySkillUtilityEffects(BattleSkillData skill)
        {
            switch (skill.EffectKind)
            {
                case SkillEffectKind.EnemyDelay when skill.EffectValue > 0f:
                {
                    var slowPct = Mathf.Clamp(skill.EffectValueInt > 0 ? skill.EffectValueInt : 50, 1, 90);
                    _enemyAttackSlowPercent = Mathf.Max(_enemyAttackSlowPercent, slowPct);
                    _enemyAttackSlowRemaining = Mathf.Max(_enemyAttackSlowRemaining, skill.EffectValue);
                    break;
                }
                case SkillEffectKind.RerollRestore when skill.EffectValueInt > 0:
                    _typing?.AddReroll(skill.EffectValueInt, _maxRerolls);
                    break;
            }
        }

        IEnumerator PlayCutinRoutine(BattleSkillData skill)
        {
            if (_ui?.CutinOverlay == null)
                yield break;

            _ui.CutinChar.text = StarterCharacters.Shown(skill.CharacterName);
            _ui.CutinSkill.text = skill.DisplayName;
            if (_ui.CutinPortrait != null)
            {
                Sprite portrait = null;
                if (_ui.CharPortraits != null && _ui.CharPortraits.TryGetValue(skill.CharacterName, out var p))
                    portrait = p.sprite;
                _ui.CutinPortrait.sprite = portrait;
                _ui.CutinPortrait.color = portrait != null ? Color.white : new Color(0.35f, 0.35f, 0.4f);
            }

            _ui.CutinOverlay.SetActive(true);
            var banner = _ui.CutinOverlay.transform.Find("Banner") as RectTransform;
            var group = banner != null
                ? banner.GetComponent<CanvasGroup>() ?? banner.gameObject.AddComponent<CanvasGroup>()
                : null;
            var elapsed = 0f;
            const float duration = 0.95f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var intro = Mathf.Clamp01(t / 0.2f);
                var outro = Mathf.Clamp01((t - 0.76f) / 0.24f);
                var eased = 1f - Mathf.Pow(1f - intro, 3f);
                if (banner != null)
                    banner.localScale = new Vector3(Mathf.Lerp(0.08f, 1.02f, eased), Mathf.Lerp(0.7f, 1f, eased), 1f);
                if (group != null)
                    group.alpha = Mathf.Min(intro * 2f, 1f) * (1f - outro);
                if (_ui.CutinSkill != null)
                    _ui.CutinSkill.transform.localScale = Vector3.one * (1f + Mathf.Sin(intro * Mathf.PI) * 0.12f);
                yield return null;
            }

            _ui.CutinOverlay.SetActive(false);
        }

        readonly struct PendingJackpot
        {
            public readonly int Damage;
            public readonly string[] Words;

            public PendingJackpot(int damage, string[] words)
            {
                Damage = damage;
                Words = words ?? Array.Empty<string>();
            }
        }

        IEnumerator PlayJackpotSequence(PendingJackpot show)
        {
            _jackpotPlaying = true;
            if (_ui?.JackpotOverlay != null)
                _ui.JackpotOverlay.SetActive(false);
            var look = JackpotLook();
            var se = EnsureJackpotSe();
            var landed = false;
            var routine = JackpotPresentationPlayer.Play(
                transform,
                look,
                show.Damage,
                show.Words,
                _ui != null ? _ui.EnemyStage : null,
                se,
                SuspendBgm,
                () =>
                {
                    if (_ui?.EnemyStage != null)
                        StartCoroutine(PunchEnemyStage());
                },
                () =>
                {
                    if (landed)
                        return;
                    landed = true;
                    var jackpotDealt = Mathf.RoundToInt(WithIncoming(show.Damage, true));
                    ApplyDamageToEnemy(jackpotDealt, null, showFeedback: false, scaleIncoming: false);
                    if (_ui?.EnemyStage != null)
                        StartCoroutine(PunchEnemyStage());
                    if (!_isGameOver)
                        SetLog($"JACKPOT！ 狂気 × {_jackpotMultiplier:0.##} → {jackpotDealt} ダメージ！");
                });
            try
            {
                while (routine.MoveNext())
                    yield return routine.Current;
            }
            finally
            {
                if (!landed && isActiveAndEnabled)
                {
                    landed = true;
                    ApplyDamageToEnemy(Mathf.RoundToInt(WithIncoming(show.Damage, true)), null, showFeedback: false, scaleIncoming: false);
                }

                var leftover = transform != null ? transform.Find("JackpotPresentation") : null;
                if (leftover != null)
                    Destroy(leftover.gameObject);
                _typing?.ClearTypingProgress();
                RefreshJackpotMeter();
                _jackpotPlaying = false;
                if (isActiveAndEnabled)
                    BeginBgmFade(look.bgmFadeSeconds);
                else
                    RestoreBgmImmediate();
            }
        }

        JackpotPresentationDefinition JackpotLook()
        {
            if (jackpotPresentation != null)
                return jackpotPresentation;
            if (_jackpotLook == null)
                _jackpotLook = Resources.Load<JackpotPresentationDefinition>("Battle/JackpotPresentation");
            if (_jackpotLook == null)
                _jackpotLook = ScriptableObject.CreateInstance<JackpotPresentationDefinition>();
            return _jackpotLook;
        }

        AudioSource EnsureJackpotSe()
        {
            if (_jackpotSe != null)
                return _jackpotSe;
            var go = new GameObject("JackpotSE");
            go.transform.SetParent(transform, false);
            _jackpotSe = go.AddComponent<AudioSource>();
            _jackpotSe.playOnAwake = false;
            _jackpotSe.loop = false;
            _jackpotSe.spatialBlend = 0f;
            return _jackpotSe;
        }

        void SuspendBgm()
        {
            if (_bgmFade != null)
            {
                StopCoroutine(_bgmFade);
                _bgmFade = null;
            }

            if (_heldBgm == null || _heldBgm.Length == 0)
                _heldBgm = JackpotBgm.Capture(_jackpotSe);
            else
                JackpotBgm.RePause(_heldBgm);
        }

        void BeginBgmFade(float seconds)
        {
            if (_heldBgm == null || _heldBgm.Length == 0)
            {
                _heldBgm = null;
                return;
            }
            if (_bgmFade != null)
                StopCoroutine(_bgmFade);
            _bgmFade = StartCoroutine(FadeBgm(seconds));
        }

        IEnumerator FadeBgm(float seconds)
        {
            var slots = _heldBgm;
            JackpotBgm.BeginFromMark(slots);
            var duration = Mathf.Max(0.05f, seconds);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (_heldBgm != slots)
                    yield break;
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                foreach (var slot in slots)
                {
                    if (slot.Source != null)
                        slot.Source.volume = Mathf.Lerp(0f, slot.Volume, t);
                }

                yield return null;
            }

            if (_heldBgm == slots)
            {
                foreach (var slot in slots)
                {
                    if (slot.Source != null)
                        slot.Source.volume = slot.Volume;
                }

                _heldBgm = null;
            }

            _bgmFade = null;
        }

        void RestoreBgmImmediate()
        {
            if (_bgmFade != null)
            {
                StopCoroutine(_bgmFade);
                _bgmFade = null;
            }

            JackpotBgm.Restore(_heldBgm);
            _heldBgm = null;
        }

        void TriggerInsanityGameOver()
        {
            if (_outcomeHandled)
                return;

            _isGameOver = true;
            _outcomeHandled = true;
            ClearExArea();
            if (!string.IsNullOrEmpty(_focusCarryLog))
                SetLog(_focusCarryLog + "\n狂気が限界に達した… 発狂！");
            _onInsanity?.Invoke();
        }

        void PersistJackpotAccumulation() =>
            _madness?.SetAccumulatedJackpotChance(_jackpot.BaseAccumulatedChance);

        void OnDestroy()
        {
            PersistJackpotAccumulation();
            RestoreBgmImmediate();
        }

        void PulseCharCard(string charName)
        {
            if (_ui == null || !_ui.CharCards.TryGetValue(charName, out var card))
                return;
            StartCoroutine(PulseRect(card, 0.38f, 0.08f));
        }

        static IEnumerator PulseRect(RectTransform rt, float duration, float amount)
        {
            if (rt == null)
                yield break;
            var elapsed = 0f;
            var startScale = rt.localScale;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rt.localScale = startScale * (1f + Mathf.Sin(t * Mathf.PI) * amount);
                yield return null;
            }
            if (rt != null)
                rt.localScale = startScale;
        }

        IEnumerator ShakeRect(RectTransform rt, float duration, float amount)
        {
            var start = rt.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                rt.anchoredPosition = start + new Vector2(Mathf.Sin(elapsed * 40f) * amount, 0);
                yield return null;
            }

            rt.anchoredPosition = start;
        }

        void RefreshAttackCountdown(float extraStop)
        {
            if (_ui?.AttackForecast != null)
            {
                var stopped = extraStop > 0f;
                var hasSwing = TryNextSwing(out var pending, out _);
                var forecastRate = Mathf.Max(0.01f, EnemyAttackSpeedMultiplier());
                var partialTick = Mathf.Clamp(_tickAccumulator, 0f, _tickInterval);
                var secondsLeft = stopped ? extraStop : hasSwing ? Mathf.Max(0f, pending / forecastRate - partialTick) : -1f;
                _ui.AttackForecast.Present(secondsLeft, hasSwing ? DeckIncomingDamage(_activeSwing.Damage) : -1, stopped);
                return;
            }

            if (!_showEnemyAttackTime || _ui?.AttackBarLabel == null)
                return;
            if (extraStop > 0f)
            {
                _ui.AttackBarLabel.text = $"停止 {extraStop:0.0}秒";
                return;
            }

            if (!TryNextSwing(out var remaining, out _))
            {
                _ui.AttackBarLabel.text = string.Empty;
                return;
            }

            var rate = Mathf.Max(0.01f, EnemyAttackSpeedMultiplier());
            var seconds = Mathf.Max(0f, remaining / rate);
            _ui.AttackBarLabel.text = $"{seconds:0.0}秒";
        }

        int DeckIncomingDamage(float rawDamage)
        {
            var attackMultiplier = _enemyDamageMultiplier <= 0f ? 1f : _enemyDamageMultiplier;
            var takenMultiplier = _encounterMods.DamageTakenMultiplier > 0f ? _encounterMods.DamageTakenMultiplier : 1f;
            return Mathf.RoundToInt(rawDamage * attackMultiplier * takenMultiplier);
        }

        void EnemyAttack(float rawDamage)
        {
            if (_outcomeHandled)
                return;
            if (_negateMissHit && !_negateMissUsed && _missHitArmed)
            {
                _negateMissUsed = true;
                SetLog("ミスの被弾を無効にした。");
                return;
            }

            if (_ui?.SkeletonSamurai != null)
                _ui.SkeletonSamurai.PlayAttack();
            else if (_ui?.PaperAngel != null)
                _ui.PaperAngel.PlayAttack();
            else
                _ui?.BigMouth?.PlayAttack();
            StartCoroutine(EnemyHitFeedback());

            var takenMul = _encounterMods.DamageTakenMultiplier > 0f ? _encounterMods.DamageTakenMultiplier : 1f;
            var scaled = rawDamage * (_enemyDamageMultiplier <= 0f ? 1f : _enemyDamageMultiplier);
            float damageTaken;
            if (DeckMode)
            {
                damageTaken = DeckIncomingDamage(rawDamage);
                if (_deck != null)
                {
                    var shield = _deck.Shield;
                    damageTaken = _deck.ApplyShieldToDamage(Mathf.RoundToInt(damageTaken));
                    if (shield > 0 && _deck.Shield == 0)
                    {
                        _deckHandUi?.PlayShieldCrack(shield);
                        if (_shieldSe != null && _shieldCrackClip != null)
                            _shieldSe.PlayOneShot(_shieldCrackClip, 0.55f);
                    }
                }
            }
            else
            {
                var constant = _defenseConstant > 0f ? _defenseConstant : 400f;
                var denom = _partyDefense + constant;
                var mitigated = denom > 0f ? scaled * constant / denom : scaled;
                damageTaken = mitigated * takenMul;
                ApplyDefensiveMitigation(ref damageTaken, false);
                damageTaken = Mathf.Max(1f, Mathf.Round(damageTaken));
            }
            _playerHp = Mathf.Max(0f, _playerHp - damageTaken);
            RefreshHpBars();
            _ui?.Stock?.PlayHit(Mathf.RoundToInt(damageTaken));
            _ui?.E5b?.NotifyHit();

            if (_playerHp <= 0f)
            {
                _isGameOver = true;
                SetLog("☠️ 探索者は力尽きた... GAME OVER");
                CompleteDefeat();
            }
        }

        void CompleteDefeat()
        {
            if (_outcomeHandled)
                return;

            _outcomeHandled = true;
            ClearExArea();
            if (!string.IsNullOrEmpty(_focusCarryLog))
                SetLog(_focusCarryLog + "\n☠️ 探索者は力尽きた... GAME OVER");
            _partyState?.SetCurrentHp(0);
            _onDefeat?.Invoke();
        }

        IEnumerator EnemyHitFeedback()
        {
            yield return new WaitForSecondsRealtime(0.11f);
            var rt = _ui?.EnemyHitRoot != null ? _ui.EnemyHitRoot : _ui?.EnemyStage;
            if (rt != null)
                StartCoroutine(ShakeRect(rt, 0.08f, 2.5f));

            var bar = _ui?.PlayerHpBar;
            if (bar == null)
                yield break;
            var original = bar.color;
            bar.color = Color.Lerp(original, new Color(1f, 0.42f, 0.38f), 0.75f);
            yield return new WaitForSecondsRealtime(0.07f);
            if (bar != null)
                bar.color = original;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void DebugInstantKillForTest()
        {
            if (_isGameOver || _outcomeHandled || _enemyHp <= 0f)
                return;
            ApplyDamageToEnemy(_enemyHp, "【テスト】撃破");
        }

        void CycleCritTest()
        {
            UnregisterDebugCrit();
            _critTestMode = (_critTestMode + 1) % 3;
            if (_critTestMode == 1)
            {
                _debugCritChance = _ => 1f;
                _skillDamage.AddCritChanceSource(_debugCritChance);
                SetLog("会心テスト：会心率100%");
            }
            else if (_critTestMode == 2)
            {
                _debugForceCrit = true;
                _debugCritMultiplier = _ => 2f;
                _skillDamage.AddCritMultiplierOverrideSource(_debugCritMultiplier);
                SetLog("会心テスト：確定会心 倍率×2");
            }
            else
            {
                SetLog("会心テスト：オフ");
            }

            RefreshDebugCombatReadout();
        }

        void UnregisterDebugCrit()
        {
            if (_debugCritChance != null)
            {
                _skillDamage.RemoveCritChanceSource(_debugCritChance);
                _debugCritChance = null;
            }

            if (_debugCritMultiplier != null)
            {
                _skillDamage.RemoveCritMultiplierOverrideSource(_debugCritMultiplier);
                _debugCritMultiplier = null;
            }

            _debugForceCrit = false;
        }

        void DebugAliceSlash()
        {
            if (_isGameOver || _outcomeHandled || _enemyHp <= 0f)
                return;
            var attack = _partyState != null ? _partyState.AttackOf(StarterCharacters.Chara01) : 0;
            if (attack <= 0)
                attack = 120;
            var probe = new BattleSkillData(
                0,
                StarterCharacters.Chara01,
                "テスト斬り",
                Array.Empty<ElementColor>(),
                0,
                0,
                "",
                damagePercent: 0.3f,
                casterAttack: attack);
            var result = ComputeSkillHit(probe);
            var shown = Mathf.Max(1, Mathf.RoundToInt(result.Damage));
            var text = result.IsCrit ? $"会心！ テスト斬り {shown}ダメ" : $"テスト斬り {shown}ダメ";
            ApplyDamageToEnemy(shown, text);
        }

        void RefreshDebugCombatReadout()
        {
            if (_ui?.DebugCombatReadout == null)
                return;
            var probe = new BattleSkillData(
                0, StarterCharacters.Chara01, "", Array.Empty<ElementColor>(), 0, 0, "");
            var external = _skillDamage.SumCritChance(probe);
            var chance = Mathf.Clamp01(_baseCritChance + external);
            var critMultiplier = _skillDamage.CritMultiplierOverride(probe) ?? _critMultiplier;
            var word = ComputeWordDamage(0);
            var dealt = WithIncoming(word, AxeIncomingEnabled(true, false));
            var force = _debugForceCrit ? "／確定会心" : "";
            _ui.DebugCombatReadout.text =
                $"敵HP {Mathf.RoundToInt(_enemyHp)} / {Mathf.RoundToInt(_maxEnemyHp)}（敵HP全体倍率 ×{_globalEnemyHpScale:0.##}）\n" +
                BuildDebugAttackLine() + "\n" +
                $"単語ダメ {dealt:0}（元 {word:0}）\n" +
                $"会心率 {chance * 100f:0}%（基礎{_baseCritChance * 100f:0}% + 外から{external * 100f:0}%）／会心倍率 ×{critMultiplier:0.#}{force}";
        }

        string BuildDebugAttackLine()
        {
            if (_partyState?.Members == null)
                return $"ATK 合計{_totalAttack:0}";
            var parts = new List<string>();
            var sum = 0;
            foreach (var member in _partyState.Members)
            {
                var attack = RunPartyState.GetAttack(member);
                sum += attack;
                parts.Add($"{StarterCharacters.Shown(member?.character?.characterName)}{attack}");
            }

            return $"ATK {string.Join(" / ", parts)}（合計{sum}）";
        }
#endif

        void ApplyDamageToEnemy(
            float amount,
            string skillText,
            bool showFeedback = true,
            bool scaleIncoming = true,
            bool resolveOutcome = true)
        {
            if (scaleIncoming)
                amount = WithIncoming(amount, true);
            var dmgMul = _encounterMods.PlayerDamageMultiplier > 0f
                ? _encounterMods.PlayerDamageMultiplier
                : 1f;
            amount *= dmgMul;
            amount *= _enemyBreak?.DamageMultiplier ?? 1f;
            var hpBeforeDamage = _enemyHp;
            _enemyHp = Mathf.Max(0f, _enemyHp - amount);
            _damageDealt += Mathf.Min(hpBeforeDamage, amount);
            RefreshHpBars();
            if (showFeedback)
                ShowEnemyDamageNumber(amount);
            if (showFeedback && _spritePresenter != null)
                _spritePresenter.PlayHitReaction();
            else if (showFeedback && _ui?.EnemyStage != null)
                StartCoroutine(PunchEnemyStage());

            if (_enemyHp <= _maxEnemyHp * _enrageHpFraction && !_isEnraged)
            {
                _isEnraged = true;
                _spritePresenter?.SetEnraged(true);
                ApplyEnrageToRunningIntervals();
                if (_ui?.EnemyEmoji != null)
                    _ui.EnemyEmoji.text = _emojiEnraged;
                if (_ui?.EnemyAura != null)
                    _ui.EnemyAura.color = new Color(1f, 0.025f, 0.05f, 0.48f);
                SetLog("⚠️ 狂暴化！攻撃間隔が短くなった！");
            }
            else if (!_isGameOver && !string.IsNullOrEmpty(skillText))
                SetLog(skillText);

            if (_enemyHp <= 0f && resolveOutcome)
                ResolveEnemyDown(skillText);
        }

        void ResolveEnemyDown(string skillText)
        {
            if (_outcomeHandled)
                return;
            _isGameOver = true;
            _spritePresenter?.PlayDefeat();
            if (_ui?.EnemyEmoji != null)
                _ui.EnemyEmoji.text = _emojiDefeated;
            _partyState?.SetCurrentHp(_playerHp);
            if (_playerHp <= 0f)
            {
                _pendingWordLog = null;
                _recentSkillLog = null;
                SetLog("☠️ 探索者は力尽きた... GAME OVER");
                CompleteDefeat();
                return;
            }

            _outcomeHandled = true;
            ClearExArea();
            var line = "🎉 敵撃破！VICTORY！";
            if (!string.IsNullOrEmpty(_focusCarryLog))
                line = _focusCarryLog + "\n" + line;
            if (!string.IsNullOrEmpty(_recentSkillLog) && _recentSkillLog != skillText)
                line = _recentSkillLog + "\n" + line;
            if (!string.IsNullOrEmpty(skillText))
                line = skillText + "\n" + line;
            if (!string.IsNullOrEmpty(_pendingWordLog) && _pendingWordLog != skillText)
                line = _pendingWordLog + "\n" + line;
            _pendingWordLog = null;
            _recentSkillLog = null;
            SetLog(line);
            ShowBattleResult();
        }

        void ShowBattleResult()
        {
            _runMenuOpen = true;
            if (_ui?.ResultOverlay == null)
            {
                _onVictory?.Invoke();
                return;
            }

            var hpAfter = Mathf.RoundToInt(_playerHp);
            var madnessAfter = _madness != null ? _madness.CurrentMadness : 0;
            if (_ui.ResultTitle != null)
                _ui.ResultTitle.text = "Victory";
            if (_ui.ResultHp != null)
                _ui.ResultHp.text = $"HP  {_hpBefore} → {hpAfter}";
            if (_ui.ResultMadness != null)
                _ui.ResultMadness.text = $"狂気値  {_madnessBefore} → {madnessAfter}";
            if (_ui.ResultTime != null)
                _ui.ResultTime.text = FormatBattleTime(_battleElapsed);
            if (_ui.ResultContinueButton != null)
                _ui.ResultContinueButton.interactable = false;
            _ui.ResultOverlay.SetActive(true);
            _ui.ResultOverlay.transform.SetAsLastSibling();
            StartCoroutine(AnimateBattleResult());
        }

        IEnumerator AnimateBattleResult()
        {
            var group = _ui.ResultCanvasGroup;
            var window = _ui.ResultOverlay.transform.Find("ResultWindow") as RectTransform;
            if (group != null)
                group.alpha = 0f;
            if (window != null)
                window.localScale = Vector3.one * 0.82f;

            var elapsed = 0f;
            const float duration = 0.55f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                if (group != null)
                    group.alpha = eased;
                if (window != null)
                    window.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, eased);
                if (_ui.ResultTitle != null)
                    _ui.ResultTitle.rectTransform.localScale =
                        Vector3.one * (1f + Mathf.Sin(eased * Mathf.PI) * 0.08f);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.25f);
            if (_ui?.ResultContinueButton != null)
                _ui.ResultContinueButton.interactable = true;
        }

        void ContinueAfterResult()
        {
            if (_resultContinued)
                return;
            _resultContinued = true;
            if (_ui?.ResultContinueButton != null)
                _ui.ResultContinueButton.interactable = false;
            if (_playerHp <= 0f)
            {
                _partyState?.SetCurrentHp(0);
                _onDefeat?.Invoke();
                return;
            }

            _onVictory?.Invoke();
        }

        static string FormatBattleTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"TIME  {total / 60:00}：{total % 60:00}";
        }

        IEnumerator PunchEnemyStage()
        {
            if (_ui?.EnemyStage == null)
                yield break;

            var stage = _ui.EnemyHitRoot != null ? _ui.EnemyHitRoot : _ui.EnemyStage;
            var startScale = Vector3.one;
            var startRotation = stage.localEulerAngles;
            const float duration = 0.24f;
            var elapsed = 0f;

            while (elapsed < duration && stage != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var punch = Mathf.Sin(t * Mathf.PI);
                stage.localScale = startScale * (1f + punch * 0.055f);
                stage.localEulerAngles = startRotation + new Vector3(0, 0, Mathf.Sin(t * Mathf.PI * 4f) * 1.8f * (1f - t));
                if (_ui.BossPortrait != null)
                    _ui.BossPortrait.color = Color.Lerp(Color.white, new Color(1f, 0.38f, 0.38f), punch * 0.7f);
                yield return null;
            }

            if (stage != null)
            {
                stage.localScale = startScale;
                stage.localEulerAngles = startRotation;
            }
            if (_ui?.BossPortrait != null)
                _ui.BossPortrait.color = Color.white;
        }

        void ShowEnemyDamageNumber(float amount)
        {
            if (_ui?.EnemyDamagePopupRoot == null || amount <= 0f)
                return;

            BattleUIBuilder.RaiseDamagePopups(_ui.EnemyDamagePopupRoot);

            var impactPosition = new Vector2(
                UnityEngine.Random.Range(-70f, 70f),
                UnityEngine.Random.Range(-15f, 45f));
            ShowEnemySlashEffect(impactPosition);

            var go = new GameObject("DamageNumber");
            go.transform.SetParent(_ui.EnemyDamagePopupRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280f, 110f);
            rt.anchoredPosition = impactPosition;

            var text = go.AddComponent<Text>();
            text.font = BattleUITheme.Font;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = Mathf.RoundToInt(amount).ToString();
            text.color = new Color(0.025f, 0.025f, 0.03f, 1f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.75f);
            shadow.effectDistance = new Vector2(7f, -7f);
            shadow.useGraphicAlpha = true;

            go.transform.SetAsLastSibling();
            StartCoroutine(AnimateEnemyDamageNumber(rt, text, outline, shadow));
        }

        void ShowEnemySlashEffect(Vector2 impactPosition)
        {
            var root = new GameObject("SlashImpact");
            root.transform.SetParent(_ui.EnemyDamagePopupRoot, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(360f, 260f);
            rootRt.anchoredPosition = impactPosition;
            rootRt.localScale = Vector3.one * 0.8f;

            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var slashA = CreateSlashStreak(rootRt, "SlashA", 38f, new Vector2(0, 8f), 310f, 15f);
            var slashB = CreateSlashStreak(rootRt, "SlashB", -48f, new Vector2(10f, -4f), 250f, 11f);
            var slashC = CreateSlashStreak(rootRt, "SlashC", 38f, new Vector2(-18f, -20f), 190f, 6f);

            root.transform.SetAsLastSibling();
            StartCoroutine(AnimateEnemySlash(rootRt, canvasGroup, slashA, slashB, slashC));
        }

        static RectTransform CreateSlashStreak(
            Transform parent,
            string name,
            float rotation,
            Vector2 position,
            float width,
            float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = position;
            rt.localEulerAngles = new Vector3(0, 0, rotation);
            rt.localScale = new Vector3(0.04f, 1f, 1f);

            var image = go.AddComponent<Image>();
            BattleUITheme.EnsureSprite(image);
            image.color = new Color(1f, 0.98f, 0.9f, 1f);
            image.raycastTarget = false;

            var glow = go.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 0.08f, 0.12f, 0.92f);
            glow.effectDistance = new Vector2(7f, -7f);
            glow.useGraphicAlpha = true;
            return rt;
        }

        static IEnumerator AnimateEnemySlash(
            RectTransform root,
            CanvasGroup canvasGroup,
            RectTransform slashA,
            RectTransform slashB,
            RectTransform slashC)
        {
            const float duration = 0.58f;
            var elapsed = 0f;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var reveal = Mathf.Clamp01(t / 0.2f);
                reveal = 1f - Mathf.Pow(1f - reveal, 3f);

                if (slashA != null)
                    slashA.localScale = new Vector3(reveal, Mathf.Lerp(1.8f, 0.75f, t), 1f);
                if (slashB != null)
                {
                    var delayed = Mathf.Clamp01((t - 0.07f) / 0.2f);
                    slashB.localScale = new Vector3(1f - Mathf.Pow(1f - delayed, 3f), Mathf.Lerp(1.5f, 0.7f, t), 1f);
                }
                if (slashC != null)
                {
                    var delayed = Mathf.Clamp01((t - 0.12f) / 0.18f);
                    slashC.localScale = new Vector3(1f - Mathf.Pow(1f - delayed, 3f), 0.7f, 1f);
                }

                root.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.08f, Mathf.Clamp01(t / 0.22f));
                canvasGroup.alpha = t < 0.38f ? 1f : 1f - ((t - 0.38f) / 0.62f);
                yield return null;
            }

            if (root != null)
                Destroy(root.gameObject);
        }

        static IEnumerator AnimateEnemyDamageNumber(
            RectTransform rt,
            Text text,
            Outline outline,
            Shadow shadow)
        {
            const float duration = 0.9f;
            var elapsed = 0f;
            var startPosition = rt.anchoredPosition;

            while (elapsed < duration && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.18f)
                    scale = Mathf.Lerp(0.45f, 1.35f, t / 0.18f);
                else if (t < 0.36f)
                    scale = Mathf.Lerp(1.35f, 1f, (t - 0.18f) / 0.18f);
                else
                    scale = 1f;

                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = startPosition + new Vector2(
                    Mathf.Sin(t * Mathf.PI * 2f) * 7f,
                    Mathf.Lerp(0f, 95f, 1f - Mathf.Pow(1f - t, 2f)));

                var alpha = t < 0.58f ? 1f : 1f - ((t - 0.58f) / 0.42f);
                var textColor = text.color;
                textColor.a = alpha;
                text.color = textColor;
                outline.effectColor = new Color(1f, 1f, 1f, alpha);
                shadow.effectColor = new Color(0f, 0f, 0f, alpha * 0.75f);

                yield return null;
            }

            if (rt != null)
                Destroy(rt.gameObject);
        }

        void RefreshAllUI()
        {
            if (_ui?.EnemyEmoji != null)
                _ui.EnemyEmoji.text = _emojiNormal;
            RefreshHpBars();
            RefreshWordBoxes(false);
            RefreshStockUI();
            RefreshCandidatesUI();
            RefreshRerollBadge();
            RefreshSkillCooldownUI();
            RefreshJackpotMeter();
            RefreshMadnessMeter();
        }

        void RefreshMadnessMeter()
        {
            RefreshDeckHandUI();
            if (_madness == null)
                return;
            if (_ui?.Stock != null)
                _ui.Stock.SetMadness(_madness.CurrentMadness, _madness.MaxMadness, true);
            if (_ui?.MadnessFill == null || _ui.MadnessText == null)
                return;

            var max = _madness.MaxMadness;
            var current = _madness.CurrentMadness;
            var ratio = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            var stage = RunMadnessState.StageColor(_madness.Stage);
            _ui.MadnessFill.fillAmount = ratio;
            var liquidAlpha = _ui.MadnessEmpty != null ? 0.22f : 1f;
            _ui.MadnessFill.color = new Color(stage.r, stage.g, stage.b, liquidAlpha);
            if (_ui.MadnessEmpty != null)
                _ui.MadnessEmpty.fillAmount = 1f - ratio;
            _ui.MadnessText.text = $"<size=72%>狂気</size> {current}<size=72%>/{max}</size>";
        }

        void RefreshJackpotMeter()
        {
            if (_ui?.JackpotMeterText == null)
                return;

            var eventBonus = _madness?.BonusJackpotChance ?? 0f;
            var relicBonus = RelicJackpotActive() ? Mathf.Max(0f, _encounterMods.JackpotChanceBonus) : 0f;
            var roll = _jackpot.ResolveRollChance(eventBonus + relicBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (RunDebugState.JackpotRollChanceOverride is float forced)
                roll = forced;
#endif
            var line = $"次 {FormatJackpotChancePercent(roll)} ＝ 蓄積 {FormatJackpotChancePercent(_jackpot.BaseAccumulatedChance)}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (RunDebugState.JackpotRollForce10Percent)
                line += "\n（DEBUG: 当選率 10% 固定）";
#endif
            if (eventBonus > 0f)
                line += $" ＋ 加算 {FormatJackpotChancePercent(eventBonus)}";
            if (_encounterMods.JackpotMadnessThreshold > 0)
            {
                line += relicBonus > 0f
                    ? $" ＋ 賽 {FormatJackpotChancePercent(relicBonus)}"
                    : $"\n賽は狂気{_encounterMods.JackpotMadnessThreshold}未満（今 {_madness?.CurrentMadness ?? 0}）";
            }

            _ui.JackpotMeterText.text = line;
        }

        static string FormatJackpotChancePercent(float chanceFraction, bool includeSuffix = true)
        {
            var percent = chanceFraction * 100f;
            var text = percent >= 1f ? $"{percent:0.###}" : $"{percent:0.####}";
            return includeSuffix ? $"{text}%" : text;
        }

        void RefreshHpBars()
        {
            RefreshDeckHandUI();
            if (_ui == null)
                return;
            if (_ui.Stock != null)
                _ui.Stock.SetHP(Mathf.RoundToInt(_playerHp), Mathf.Max(1, Mathf.RoundToInt(_maxPlayerHp)), true);
            if (_ui.PlayerHpText != null)
                _ui.PlayerHpText.text = $"{(int)_playerHp}<size=72%> / {(int)_maxPlayerHp}</size>";
            if (_ui.EnemyStatus != null)
                _ui.EnemyStatus.SetHP(Mathf.RoundToInt(_enemyHp), Mathf.Max(1, Mathf.RoundToInt(_maxEnemyHp)));
            else if (_ui.EnemyHpGauge != null)
                _ui.EnemyHpGauge.SetHP(Mathf.RoundToInt(_enemyHp), Mathf.Max(1, Mathf.RoundToInt(_maxEnemyHp)));
            else if (_ui.EnemyHpText != null)
                _ui.EnemyHpText.text = $"{(int)_enemyHp}<size=72%><color=#C9B8B8> / {(int)_maxEnemyHp}</color></size>";
            if (_ui.PlayerHpBar != null)
                _ui.PlayerHpBar.fillAmount = _maxPlayerHp > 0 ? _playerHp / _maxPlayerHp : 0;
            if (_ui.EnemyHpGauge == null && _ui.EnemyHpBar != null)
                _ui.EnemyHpBar.fillAmount = _maxEnemyHp > 0 ? _enemyHp / _maxEnemyHp : 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshDebugCombatReadout();
#endif
        }

        void RefreshRerollBadge()
        {
            if (_ui?.RerollBadge == null || _typing == null)
                return;
            _ui.RerollBadge.text = $"リロール {_typing.RerollCount}/{_maxRerolls}";
            _ui.RerollBadge.color = _typing.RerollCount == 0
                ? new Color(0.33f, 0.33f, 0.33f)
                : new Color(0.97f, 0.44f, 0.44f);
        }

        void RefreshWordBoxes(bool rerollFlash)
        {
            if (_ui == null || _typing == null)
                return;

            var chanceColors = new HashSet<ElementColor>();
            if (!DeckMode)
            {
                foreach (var c in SkillResolver.GetCandidates(EligibleSkills(), _stock, _cooldownSkillIds))
                    chanceColors.Add(c.NeededColor);
            }

            for (var i = 0; i < 3; i++)
            {
                var slot = _typing.Slots[i];
                var box = _ui.WordBoxes[i];
                if (box == null)
                    continue;

                var col = ElementColorUtility.ToUnityColor(slot.Color);
                var highlight = chanceColors.Contains(slot.Color);
                var focused = slot.Active && slot.Progress > 0;
                var dimmed = !slot.Active;
                if (box.E5b)
                {
                    var typedCount = Mathf.Clamp(slot.Progress, 0, slot.Text.Length);
                    var typedRomaji = slot.Text.Substring(0, typedCount);
                    var untypedRomaji = slot.Text.Substring(typedCount);
                    BattleLayoutE5b.PaintWord(box, slot.Color, focused, dimmed, slot.DisplayText,
                        typedRomaji, untypedRomaji, slot.Active);
                    if (box.DeckGuide != null)
                        PaintDeckWordGuide(box, slot);
                    continue;
                }
                box.ElementStripe.color = dimmed ? Color.Lerp(col, Color.black, 0.55f) : col;
                var ink = new Color(0.01f, 0.01f, 0.015f, 0.97f);
                box.Fill.color = focused ? Color.Lerp(ink, col, 0.16f) : Color.Lerp(ink, col, dimmed ? 0f : 0.05f);
                box.Frame.color = dimmed
                    ? new Color(0.42f, 0.42f, 0.45f, 0.9f)
                    : focused
                        ? Color.Lerp(Color.white, col, 0.3f)
                        : highlight
                            ? new Color(1f, 0.93f, 0.74f, 1f)
                            : Color.white;

                if (box.Glow != null)
                    box.Glow.color = new Color(col.r, col.g, col.b, focused ? 0.55f : highlight ? 0.3f : dimmed ? 0.02f : 0.1f);

                if (box.FrameRoot != null)
                {
                    var s = focused ? 1.06f : dimmed ? 0.92f : 1f;
                    box.FrameRoot.localScale = new Vector3(s, s, 1f);
                }

                box.TagText.text = ElementColorUtility.ToJapanese(slot.Color);
                box.TagText.color = dimmed ? BattleUITheme.TextDisabled : Color.white;

                box.WordText.text = slot.DisplayText;
                box.WordText.color = dimmed ? BattleUITheme.TextDisabled : Color.white;
                var progress = Mathf.Clamp(slot.Progress, 0, slot.Text.Length);
                if (box.Progress != null)
                    box.Progress.fillAmount = slot.Text.Length > 0 ? progress / (float)slot.Text.Length : 0f;
                var typed = slot.Text.Substring(0, progress);
                var untyped = slot.Text.Substring(progress);
                box.RubyText.text = BattleUITheme.RichTypedWord(typed, untyped, slot.Active);
            }

            RefreshCandidatesUI();
        }

        RectTransform CompletedCard(WordCompletion completed)
        {
            if (_typing == null || _ui?.WordBoxes == null)
                return null;
            var slots = _typing.Slots;
            for (var i = 0; i < slots.Count && i < _ui.WordBoxes.Length; i++)
            {
                var slot = slots[i];
                if (!slot.Active || string.IsNullOrEmpty(slot.Text) || slot.Progress < slot.Text.Length)
                    continue;
                if (slot.Color == completed.Color && slot.DisplayText == completed.DisplayText)
                    return _ui.WordBoxes[i]?.FrameRoot;
            }
            return null;
        }

        void RefreshStockUI()
        {
            if (DeckMode)
            {
                _pendingStockCard = null;
                _stockVisualReady = true;
                RefreshDeckHandUI();
                RefreshWordBoxes(false);
                return;
            }
            if (_ui?.Stock != null)
            {
                _ui.Stock.Sync(_stock.Orbs, _pendingStockCard, _stockVisualReady);
                _pendingStockCard = null;
                _stockVisualReady = true;
                return;
            }
            if (_ui?.StockOrbsContainer == null)
                return;

            var stockHost = _ui.StockOrbsContainer.GetComponent<BattleE5bStock>();
            if (stockHost != null)
                stockHost.Next = 0;
            foreach (var go in _stockOrbObjects)
            {
                go.SetActive(false);
                Destroy(go);
            }
            _stockOrbObjects.Clear();

            for (var i = 0; i < OrbStock.MaxLength; i++)
            {
                ElementColor? color = i < _stock.Orbs.Count ? _stock.Orbs[i] : null;
                _stockOrbObjects.Add(BattleHudKit.StockOrb(_ui.StockOrbsContainer, color, 46f));
            }
        }

        void RefreshCandidatesUI()
        {
            if (DeckMode)
            {
                RefreshDeckHandUI();
                return;
            }

            if (_ui?.CandidatesContainer == null)
                return;

            foreach (var row in _candidateRows)
                Destroy(row);
            _candidateRows.Clear();

            var candidates = SkillResolver.GetCandidates(EligibleSkills(), _stock, _cooldownSkillIds);
            if (candidates.Count == 0)
            {
                _candidateRows.Add(BattleHudKit.HintRow(_ui.CandidatesContainer, "色を揃えてスキルを狙え"));
                return;
            }

            // 発動可能 → 次の1色で発動 → クールダウン中 の順に、最大6件。
            candidates.Sort((a, b) => CandidateRank(a).CompareTo(CandidateRank(b)));
            var highlighted = false;
            for (var i = 0; i < candidates.Count && i < 6; i++)
            {
                var c = candidates[i];
                var cooling = c.IsPatternComplete && c.IsOnCooldown;
                var highlight = !highlighted && !cooling;
                highlighted |= highlight;
                var matched = c.IsPatternComplete ? c.Skill.Pattern.Length : c.Skill.Pattern.Length - 1;
                _candidateRows.Add(BattleHudKit.NextSkillRow(_ui.CandidatesContainer, c.Skill,
                    CharacterPortraitSprite(c.Skill.CharacterName), matched, highlight, cooling));
            }
        }

        static int CandidateRank(SkillCandidate c) =>
            c.IsPatternComplete ? (c.IsOnCooldown ? 2 : 0) : 1;

        Sprite CharacterPortraitSprite(string characterName) =>
            _ui?.CharPortraits != null && _ui.CharPortraits.TryGetValue(characterName, out var img) && img != null
                ? img.sprite
                : null;

        void RefreshSkillCooldownUI()
        {
            if (_ui == null)
                return;

            if (DeckMode)
            {
                RefreshDeckHandUI();
                return;
            }

            var readyIds = new HashSet<int>();
            if (_activeSkills != null && _stock != null)
            {
                foreach (var candidate in SkillResolver.GetCandidates(EligibleSkills(), _stock, _cooldownSkillIds))
                {
                    if (candidate.IsPatternComplete && !candidate.IsOnCooldown)
                        readyIds.Add(candidate.Skill.Id);
                }
            }

            BattlePartyCardUI.Refresh(_ui, _stock, readyIds, _cooldownSkillIds);
        }

        void SetLog(string message)
        {
            if (_ui?.LogText != null)
                _ui.LogText.text = BattleHudKit.StripUnsupportedGlyphs(message);
        }

        void ApplyEnrageToRunningIntervals()
        {
            var mul = _enrageIntervalMultiplier > 0f ? _enrageIntervalMultiplier : 1f;
            ScaleRunningInterval(ref _activeSwing, mul);
        }

        static void ScaleRunningInterval(ref EnemySwing swing, float multiplier)
        {
            if (!swing.Enabled || swing.WaitingForOpening || multiplier <= 0f)
                return;
            swing.Remaining *= multiplier;
            swing.Cycle = Mathf.Max(0.05f, swing.Cycle * multiplier);
        }

        struct EnemySwing
        {
            public bool Enabled;
            public float Remaining;
            public float Cycle;
            public float BaseInterval;
            public float Damage;
            public float Telegraph;
            public bool WaitingForOpening;

            public static EnemySwing Open(float damage, float interval, float opening, float telegraph) =>
                new()
                {
                    Enabled = true,
                    Remaining = opening,
                    Cycle = opening,
                    BaseInterval = interval,
                    Damage = damage,
                    Telegraph = telegraph,
                    WaitingForOpening = true
                };
        }
    }
}
