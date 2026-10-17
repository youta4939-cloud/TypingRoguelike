using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public sealed class BattleController : MonoBehaviour
    {
        [SerializeField] BattleEncounterDefinition encounter;
        [SerializeField] JackpotPresentationDefinition jackpotPresentation;

        RunPartyState _partyState;
        RunMadnessState _madness;
        Action _onVictory;
        Action _onDefeat;
        Action _onInsanity;
        bool _outcomeHandled;
        EncounterBattleModifiers _encounterMods;
        HashSet<int> _innateSkillIds = new();

        float _tickInterval = 0.05f;
        float _wordClearDamage = 15f;
        float _damageReduction;
        float _jackpotLuckBonusPerWord;
        int _maxRerolls = 3;
        float _enemyAttackDamage = 25f;
        float _enemyTimerMax = 100f;
        float _enrageHpFraction = 0.5f;
        string _emojiNormal = "🐲";
        string _emojiEnraged = "🔥🐲";
        string _emojiDefeated = "💀";
        float _enragedSpeed;

        List<BattleSkillData> _activeSkills;
        TypingEngine _typing;
        OrbStock _stock;
        BattleUIRefs _ui;

        float _playerHp = 100f;
        float _maxPlayerHp = 100f;
        float _enemyHp = 1000f;
        float _maxEnemyHp = 1000f;
        float _enemyTimer = 100f;
        float _enemySpeed = 0.35f;
        float _enemyAttackSlowRemaining;
        int _enemyAttackSlowPercent;
        float _enemyStopRemaining;
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
        float _damageDealt;
        int _wordsCleared;
        int _skillsTriggered;

        readonly List<GameObject> _stockOrbObjects = new();
        readonly List<GameObject> _candidateRows = new();
        TypingAudioPlayer _typingAudio;

        public float ExportJackpotAccumulatedChance() => _jackpot.BaseAccumulatedChance;

        public void Initialize(
            BattleEncounterDefinition enc,
            RunPartyState party,
            RunMadnessState madness,
            Action onVictory,
            Action onDefeat = null,
            Action onInsanity = null,
            EncounterBattleModifiers mods = default,
            PartyCombatStats partyStats = default)
        {
            encounter = enc;
            _partyState = party;
            _madness = madness;
            _onVictory = onVictory;
            _onDefeat = onDefeat;
            _onInsanity = onInsanity;
            _encounterMods = mods;
            _damageReduction = partyStats.DamageReduction;
            _jackpotLuckBonusPerWord = partyStats.JackpotLuckBonusPerWord;
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

            ApplyEncounter(enc, party, partyStats);
            SetupTypingAudio(enc);
            _stock = new OrbStock();
            var visuals = BattleVisualContext.FromEncounter(enc, party);
            _ui = BattleUIBuilder.Build(transform, _activeSkills, _innateSkillIds, visuals);
            RefreshPortraitImages(visuals);
            _typing.GenerateWords();
            RefreshAllUI();
            var startMsg = _encounterMods.HasLogOverride ? _encounterMods.StartLogMessage : enc?.startLogMessage;
            var resolvedStartMessage = !string.IsNullOrEmpty(startMsg) ? startMsg : "戦闘開始！";
            if (_enemyStopRemaining > 0f)
                resolvedStartMessage += $"\n敵の攻撃が {_enemyStopRemaining:0} 秒止まっている。";
            if (_ui.ResultContinueButton != null)
                _ui.ResultContinueButton.onClick.AddListener(ContinueAfterResult);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_ui.DebugKillButton != null)
                _ui.DebugKillButton.onClick.AddListener(DebugInstantKillForTest);
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
            _battleElapsed = 0f;
            SetLog(startMessage);
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

            if (_ui.BossPortrait == null)
                return;

            var boss = BattlePortraitResolver.LoadBossFromResources();
            if (boss == null && encounter?.enemy != null)
                boss = encounter.enemy.battleIllustration;
            if (boss == null)
                return;

            _ui.BossPortrait.sprite = boss;
            _ui.BossPortrait.color = Color.white;
        }

        void ApplyEncounter(BattleEncounterDefinition enc, RunPartyState party, PartyCombatStats partyStats)
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
                _jackpot.Configure(0f, 0.000025f, 0.99f, 0.1f);
                if (_madness != null)
                    _jackpot.ApplyRunAccumulatedChance(_madness.AccumulatedJackpotChance);
                _jackpotMultiplier = 2f;
                return;
            }

            _activeSkills = party != null ? party.BuildActiveSkills() : BattleLoadoutBuilder.BuildActiveSkills(enc.party);
            if (_activeSkills.Count == 0)
                _activeSkills = BattleLoadoutBuilder.CreateHardcodedFallbackLoadout();

            var words = BattleLoadoutBuilder.GetWords(enc.wordList);
            _maxRerolls = enc.rerollsPerBattle;
            _typing = new TypingEngine(words, enc.rerollsPerBattle);
            _tickInterval = enc.enemyTickInterval;
            _wordClearDamage = partyStats.TotalAttack > 0
                ? partyStats.WordClearDamage
                : enc.wordClearDamage;
            enc.ResolveJackpotRollParameters(
                out var jackpotBase,
                out var jackpotPerChar,
                out var jackpotMax,
                out var jackpotCap);
            _jackpot.Configure(jackpotBase, jackpotPerChar, jackpotMax, jackpotCap);
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

            var hpMul = _encounterMods.EnemyHpMultiplier <= 0f ? 1f : _encounterMods.EnemyHpMultiplier;
            var dmgMul = _encounterMods.EnemyDamageMultiplier <= 0f ? 1f : _encounterMods.EnemyDamageMultiplier;
            var speedMul = _encounterMods.EnemySpeedMultiplier <= 0f ? 1f : _encounterMods.EnemySpeedMultiplier;

            if (enc.enemy != null)
            {
                var e = enc.enemy;
                _maxEnemyHp = e.maxHp * hpMul;
                _enemyHp = _maxEnemyHp;
                _enemyAttackDamage = e.attackDamage * dmgMul;
                _enemyTimerMax = e.attackTimerMax;
                _enemyTimer = _enemyTimerMax;
                _enemySpeed = e.attackSpeed * speedMul;
                _enemyAttackSlowRemaining = 0f;
                _enemyAttackSlowPercent = 0;
                _enemyStopRemaining = Mathf.Max(0f, _encounterMods.OpeningEnemyStopSeconds);
                _enragedSpeed = e.enragedAttackSpeed * speedMul;
                _enrageHpFraction = e.enrageHpFraction;
                _emojiNormal = e.emojiNormal;
                _emojiEnraged = e.emojiEnraged;
                _emojiDefeated = e.emojiDefeated;
            }
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

            _battleElapsed += Time.deltaTime;
            TickEnemyAttackSlow(Time.deltaTime);
            if (_enemyStopRemaining > 0f)
            {
                _enemyStopRemaining -= Time.deltaTime;
                HoldStoppedAttackBar();
                if (_enemyStopRemaining <= 0f)
                {
                    _enemyStopRemaining = 0f;
                    SetLog("敵が動き出した。");
                }

                return;
            }

            _tickAccumulator += Time.deltaTime;
            while (_tickAccumulator >= _tickInterval)
            {
                _tickAccumulator -= _tickInterval;
                TickEnemyTimer();
            }
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

            if (_isPaused || _isGameOver)
                return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (_typing.TryReroll())
                {
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

                var keyResult = _typing.ProcessKey(c);
                PlayTypingSound(keyResult);
                if (keyResult.Completed.HasValue)
                {
                    var completed = keyResult.Completed.Value;
                    _wordsCleared++;
                    var wentInsane = _madness != null && _madness.AddFromWordAttack(completed.InputLength);
                    RefreshMadnessMeter();
                    if (wentInsane)
                    {
                        SetLog("狂気が限界に達した… 発狂！");
                        TriggerInsanityGameOver();
                        break;
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
                        ApplyDamageToEnemy(_wordClearDamage, $"単語クリア！ {(int)_wordClearDamage} ダメージ！");

                    _stock.Push(completed.Color);
                    _typing.GenerateWords();
                    TryTriggerSkill();
                    RefreshStockUI();
                    RefreshWordBoxes(false);
                }
                else
                    RefreshWordBoxes(false);

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

        float CurrentJackpotRollBonus()
        {
            var bonus = _jackpotLuckBonusPerWord + (_madness?.BonusJackpotChance ?? 0f);
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
            if (_enemyAttackSlowRemaining <= 0f || _enemyAttackSlowPercent <= 0)
                return 1f;
            return Mathf.Clamp01(1f - _enemyAttackSlowPercent / 100f);
        }

        void TickEnemyTimer()
        {
            _enemyTimer -= _enemySpeed * EnemyAttackSpeedMultiplier();
            if (_enemyTimer <= 0f)
            {
                _enemyTimer = _enemyTimerMax;
                EnemyAttack();
            }

            if (_ui?.AttackBar != null)
            {
                var fill = Mathf.Clamp01(_enemyTimer / _enemyTimerMax);
                _ui.AttackBar.fillAmount = fill;
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
            }
        }

        void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_ui?.PauseOverlay != null)
                _ui.PauseOverlay.SetActive(_isPaused);
            SetLog(_isPaused ? "⏸️ 一時停止中" : "再開しました！");
            if (!_isPaused)
                TryTriggerSkill();
        }

        void TryTriggerSkill()
        {
            var skills = SkillResolver.ResolveTriggeredSkills(
                _activeSkills, _stock, _cooldownSkillIds, out var blocked);
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
            foreach (var skill in skills)
                ApplySkillEffects(skill, simultaneous);

            if (simultaneous)
            {
                var summary = string.Join(" + ", skills.Select(s => $"{s.CharacterName}/{s.DisplayName}"));
                SetLog($"✨ 同時発動！ {summary}");
            }

            foreach (var skill in skills)
            {
                _skillsTriggered++;
                PulseCharCard(skill.CharacterName);
                EnqueueCutin(skill);
            }

            RefreshSkillCooldownUI();
            RefreshStockUI();
            RefreshRerollBadge();
            RefreshCandidatesUI();
        }

        void ApplySkillEffects(BattleSkillData skill, bool suppressLog)
        {
            var msg = BuildSkillTriggerMessage(skill);
            if (skill.DealsDamage)
                ApplyDamageToEnemy(skill.Damage, suppressLog ? null : msg);
            else if (!suppressLog)
                SetLog(msg);

            if (skill.Heal > 0)
            {
                _playerHp = Mathf.Min(_maxPlayerHp, _playerHp + skill.Heal);
                RefreshHpBars();
            }

            ApplySkillUtilityEffects(skill);
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

        static string BuildSkillTriggerMessage(BattleSkillData skill)
        {
            var parts = new List<string>();
            if (skill.DealsDamage)
                parts.Add($"{skill.Damage}ダメ");
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
            return $"✨ {skill.CharacterName}の{skill.DisplayName}！ {detail}";
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

            _ui.CutinChar.text = skill.CharacterName;
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
                    ApplyDamageToEnemy(show.Damage, null, showFeedback: false);
                    if (_ui?.EnemyStage != null)
                        StartCoroutine(PunchEnemyStage());
                    if (!_isGameOver)
                        SetLog($"JACKPOT！ 狂気 × {_jackpotMultiplier:0.##} → {show.Damage} ダメージ！");
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
                    ApplyDamageToEnemy(show.Damage, null, showFeedback: false);
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

        void EnemyAttack()
        {
            if (_outcomeHandled)
                return;

            _ui?.BigMouth?.PlayAttack();
            _ui?.EnemyBiteFx?.Play();
            StartCoroutine(BiteImpact());

            var damageTaken = Mathf.Max(1f, _enemyAttackDamage - _damageReduction);
            _playerHp = Mathf.Max(0f, _playerHp - damageTaken);
            RefreshHpBars();

            if (_playerHp <= 0f)
            {
                _isGameOver = true;
                SetLog("☠️ パーティは力尽きた... GAME OVER");
                CompleteDefeat();
            }
        }

        void CompleteDefeat()
        {
            if (_outcomeHandled)
                return;

            _outcomeHandled = true;
            _partyState?.SetCurrentHp(0);
            _onDefeat?.Invoke();
        }

        IEnumerator BiteImpact()
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
#endif

        void ApplyDamageToEnemy(float amount, string skillText, bool showFeedback = true)
        {
            var dmgMul = _encounterMods.PlayerDamageMultiplier > 0f
                ? _encounterMods.PlayerDamageMultiplier
                : 1f;
            amount *= dmgMul;
            var hpBeforeDamage = _enemyHp;
            _enemyHp = Mathf.Max(0f, _enemyHp - amount);
            _damageDealt += Mathf.Min(hpBeforeDamage, amount);
            RefreshHpBars();
            if (showFeedback)
                ShowEnemyDamageNumber(amount);
            if (showFeedback && _ui?.EnemyStage != null)
                StartCoroutine(PunchEnemyStage());

            if (_enemyHp <= _maxEnemyHp * _enrageHpFraction && !_isEnraged)
            {
                _isEnraged = true;
                _enemySpeed = _enragedSpeed;
                if (_ui?.EnemyEmoji != null)
                    _ui.EnemyEmoji.text = _emojiEnraged;
                if (_ui?.EnemyAura != null)
                    _ui.EnemyAura.color = new Color(1f, 0.025f, 0.05f, 0.48f);
                SetLog("⚠️ ボスが狂暴化！攻撃速度が増加！");
            }
            else if (!_isGameOver && !string.IsNullOrEmpty(skillText))
                SetLog(skillText);

            if (_enemyHp <= 0f)
            {
                _isGameOver = true;
                if (_ui?.EnemyEmoji != null)
                    _ui.EnemyEmoji.text = _emojiDefeated;
                if (!_outcomeHandled)
                {
                    _partyState?.SetCurrentHp(_playerHp);
                    if (_playerHp <= 0f)
                    {
                        SetLog("☠️ パーティは力尽きた... GAME OVER");
                        CompleteDefeat();
                        return;
                    }

                    _outcomeHandled = true;
                    SetLog("🎉 敵撃破！VICTORY！");
                    ShowBattleResult();
                }
            }
        }

        void ShowBattleResult()
        {
            _runMenuOpen = true;
            if (_ui?.ResultOverlay == null)
            {
                _onVictory?.Invoke();
                return;
            }

            var hpRate = _maxPlayerHp > 0f ? _playerHp / _maxPlayerHp : 0f;
            var rank = hpRate >= 0.8f && _battleElapsed <= 75f
                ? "S"
                : hpRate >= 0.58f
                    ? "A"
                    : hpRate >= 0.3f ? "B" : "C";
            var rankColor = rank switch
            {
                "S" => BattleUITheme.Gold,
                "A" => BattleUITheme.Cyan,
                "B" => BattleUITheme.AccentAlly,
                _ => BattleUITheme.TextMuted
            };

            _ui.ResultRank.text = rank;
            _ui.ResultRank.color = rankColor;
            _ui.ResultStats.text =
                $"戦闘時間　{_battleElapsed:0.0} 秒\n" +
                $"クリア単語　{_wordsCleared}\n" +
                $"スキル発動　{_skillsTriggered}\n" +
                $"総ダメージ　{Mathf.RoundToInt(_damageDealt)}\n" +
                $"残りHP　{Mathf.RoundToInt(_playerHp)} / {Mathf.RoundToInt(_maxPlayerHp)}";
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
                if (_ui.ResultRank != null)
                    _ui.ResultRank.rectTransform.localScale =
                        Vector3.one * (1f + Mathf.Sin(eased * Mathf.PI) * 0.18f);
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
            if (_madness == null || _ui?.MadnessFill == null || _ui.MadnessText == null)
                return;

            var max = _madness.MaxMadness;
            var current = _madness.CurrentMadness;
            _ui.MadnessFill.fillAmount = max > 0 ? current / (float)max : 0f;
            _ui.MadnessFill.color = RunMadnessState.StageColor(_madness.Stage);
            _ui.MadnessText.text = $"{current} / {max}";
        }

        void RefreshJackpotMeter()
        {
            if (_ui?.JackpotMeterText == null)
                return;

            var luck = _jackpotLuckBonusPerWord + (_madness?.BonusJackpotChance ?? 0f);
            var relicBonus = RelicJackpotActive() ? Mathf.Max(0f, _encounterMods.JackpotChanceBonus) : 0f;
            var roll = _jackpot.ResolveRollChance(luck + relicBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (RunDebugState.JackpotRollChanceOverride is float forced)
                roll = forced;
#endif
            var line = $"次 {FormatJackpotChancePercent(roll)} ＝ 蓄積 {FormatJackpotChancePercent(_jackpot.BaseAccumulatedChance)}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (RunDebugState.JackpotRollForce10Percent)
                line += "\n（DEBUG: 当選率 10% 固定）";
#endif
            if (luck > 0f)
                line += $" ＋ 運 {FormatJackpotChancePercent(luck)}";
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
            if (_ui == null)
                return;
            if (_ui.PlayerHpText != null)
                _ui.PlayerHpText.text = $"{(int)_playerHp} / {(int)_maxPlayerHp}";
            if (_ui.EnemyHpGauge != null)
                _ui.EnemyHpGauge.SetHP(Mathf.RoundToInt(_enemyHp), Mathf.Max(1, Mathf.RoundToInt(_maxEnemyHp)));
            else if (_ui.EnemyHpText != null)
                _ui.EnemyHpText.text = $"HP {(int)_enemyHp} / {(int)_maxEnemyHp}";
            if (_ui.PlayerHpBar != null)
                _ui.PlayerHpBar.fillAmount = _maxPlayerHp > 0 ? _playerHp / _maxPlayerHp : 0;
            if (_ui.EnemyHpGauge == null && _ui.EnemyHpBar != null)
                _ui.EnemyHpBar.fillAmount = _maxEnemyHp > 0 ? _enemyHp / _maxEnemyHp : 0;
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

            var candidates = SkillResolver.GetCandidates(_activeSkills, _stock, _cooldownSkillIds);
            var chanceColors = new HashSet<ElementColor>();
            foreach (var c in candidates)
                chanceColors.Add(c.NeededColor);

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
                box.ElementStripe.color = dimmed ? Color.Lerp(col, Color.black, 0.55f) : col;
                box.Fill.color = focused
                    ? Color.Lerp(new Color(0.015f, 0.02f, 0.045f, 0.97f), col, 0.24f)
                    : Color.Lerp(new Color(0.015f, 0.02f, 0.045f, 0.95f), col, dimmed ? 0.03f : 0.08f);
                var premiumFrame = BattleUISprites.WordCardPremium != null;
                if (premiumFrame)
                {
                    box.Frame.color = dimmed
                        ? new Color(0.55f, 0.55f, 0.6f, 0.88f)
                        : focused || highlight
                            ? Color.white
                            : new Color(0.92f, 0.94f, 1f, 1f);
                }
                else
                {
                    box.Frame.color = dimmed
                        ? new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f, 0.85f)
                        : focused || highlight
                            ? Color.Lerp(col, Color.white, 0.45f)
                            : new Color(col.r, col.g, col.b, 0.95f);
                }

                if (box.Glow != null)
                    box.Glow.color = new Color(col.r, col.g, col.b, focused ? 0.6f : highlight ? 0.32f : dimmed ? 0.04f : 0.14f);

                if (box.FrameRoot != null)
                {
                    var s = focused ? 1.06f : dimmed ? 0.9f : 1f;
                    box.FrameRoot.localScale = new Vector3(s, s, 1f);
                }
                if (box.Frame != null && box.Frame.TryGetComponent<Outline>(out var outline))
                {
                    outline.effectColor = focused
                        ? new Color(0.75f, 0.97f, 1f, 1f)
                        : highlight
                            ? new Color(1f, 0.85f, 0.4f, 0.8f)
                            : new Color(0.32f, 0.8f, 1f, dimmed ? 0.12f : 0.4f);
                    outline.effectDistance = focused ? new Vector2(3, -3) : new Vector2(2, -2);
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

        void RefreshStockUI()
        {
            if (_ui?.StockOrbsContainer == null)
                return;

            foreach (var go in _stockOrbObjects)
                Destroy(go);
            _stockOrbObjects.Clear();

            const int maxStock = 5;
            for (var i = 0; i < maxStock; i++)
            {
                var go = new GameObject(i < _stock.Orbs.Count ? "Orb" : "OrbEmpty");
                go.transform.SetParent(_ui.StockOrbsContainer, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(28, 28);
                var img = go.AddComponent<Image>();
                BattleUITheme.EnsureSprite(img);
                if (i < _stock.Orbs.Count)
                {
                    img.color = ElementColorUtility.ToUnityColor(_stock.Orbs[i]);
                    AddOrbOutline(go, new Color(1, 1, 1, 0.35f));
                }
                else
                {
                    img.color = new Color(0.12f, 0.08f, 0.1f, 0.85f);
                    AddOrbOutline(go, new Color(1, 1, 1, 0.12f));
                }

                _stockOrbObjects.Add(go);
            }
        }

        static void AddOrbOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1, -1);
        }

        void RefreshCandidatesUI()
        {
            if (_ui?.CandidatesContainer == null)
                return;

            foreach (var row in _candidateRows)
                Destroy(row);
            _candidateRows.Clear();

            var candidates = SkillResolver.GetCandidates(_activeSkills, _stock, _cooldownSkillIds);
            if (candidates.Count == 0)
            {
                _candidateRows.Add(CreateCandidateHintRow());
                return;
            }

            foreach (var c in candidates)
            {
                if (c.IsPatternComplete && c.IsOnCooldown)
                    _candidateRows.Add(CreateCandidateCooldownRow(c.Skill));
                else
                    _candidateRows.Add(CreateCandidateRow(c.Skill, c.NeededColor));
            }
        }

        GameObject CreateCandidateHintRow()
        {
            var go = new GameObject("CandidateHint");
            go.transform.SetParent(_ui.CandidatesContainer, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 38;
            var img = go.AddComponent<Image>();
            BattleUITheme.StylePanel(img, new Color(0.12f, 0.05f, 0.08f, 0.94f));
            CreateCandidateLabel(go.transform, "オーブを溜めてスキルを発動", new Vector2(0, 0), new Vector2(1, 1));
            return go;
        }

        GameObject CreateCandidateCooldownRow(BattleSkillData skill)
        {
            var go = new GameObject("CandidateCooldown");
            go.transform.SetParent(_ui.CandidatesContainer, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 42;
            var img = go.AddComponent<Image>();
            BattleUITheme.StylePanel(img, new Color(0.18f, 0.08f, 0.06f, 0.94f));

            var title = $"{skill.CharacterName}  {skill.DisplayName}  ⏳クールダウン";
            CreateCandidateLabel(go.transform, title, new Vector2(0, 0), new Vector2(0.58f, 1));

            var patternHost = BattleUIOrbPattern.CreateHost(go.transform, new Vector2(0.58f, 0.1f), new Vector2(0.98f, 0.9f));
            BattleUIOrbPattern.Apply(patternHost, skill.Pattern, 15f, highlightIndex: -1);

            return go;
        }

        GameObject CreateCandidateRow(BattleSkillData skill, ElementColor neededColor)
        {
            var go = new GameObject("Candidate");
            go.transform.SetParent(_ui.CandidatesContainer, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 42;
            var img = go.AddComponent<Image>();
            BattleUITheme.StylePanel(img, new Color(0.12f, 0.05f, 0.08f, 0.94f));

            var title = $"{skill.CharacterName}  {skill.DisplayName}  → 次の1語";
            CreateCandidateLabel(go.transform, title, new Vector2(0, 0), new Vector2(0.58f, 1));

            var patternHost = BattleUIOrbPattern.CreateHost(go.transform, new Vector2(0.58f, 0.1f), new Vector2(0.98f, 0.9f));
            var highlight = FindHighlightIndex(skill.Pattern, neededColor);
            BattleUIOrbPattern.Apply(patternHost, skill.Pattern, 15f, highlight);

            var accentGo = new GameObject("Accent");
            accentGo.transform.SetParent(go.transform, false);
            var art = accentGo.AddComponent<RectTransform>();
            art.anchorMin = new Vector2(0, 0);
            art.anchorMax = new Vector2(0, 1);
            art.pivot = new Vector2(0, 0.5f);
            art.sizeDelta = new Vector2(5, 0);
            art.anchoredPosition = Vector2.zero;
            var accentImg = accentGo.AddComponent<Image>();
            BattleUITheme.StylePanel(accentImg, ElementColorUtility.ToUnityColor(neededColor));

            return go;
        }

        static int FindHighlightIndex(ElementColor[] pattern, ElementColor neededColor)
        {
            if (pattern == null || pattern.Length == 0)
                return -1;
            for (var i = pattern.Length - 1; i >= 0; i--)
            {
                if (pattern[i] == neededColor)
                    return i;
            }

            return pattern.Length - 1;
        }

        static void CreateCandidateLabel(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(parent, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(12, 4);
            rt.offsetMax = new Vector2(-6, -4);
            var label = textGo.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.fontSize = 13;
            label.text = text;
            label.color = BattleUITheme.TextPrimary;
            label.alignment = TextAnchor.MiddleLeft;
        }

        void RefreshSkillCooldownUI()
        {
            if (_ui == null)
                return;

            foreach (var kv in _ui.SkillRowGraphics)
            {
                var onCooldown = _cooldownSkillIds.Contains(kv.Key);
                if (onCooldown)
                    kv.Value.color = new Color(0.12f, 0.12f, 0.14f, 0.55f);
            }
        }

        void SetLog(string message)
        {
            if (_ui?.LogText != null)
                _ui.LogText.text = message;
        }
    }
}
