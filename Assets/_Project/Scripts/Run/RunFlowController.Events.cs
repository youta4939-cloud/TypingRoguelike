using System;
using System.Collections.Generic;
using System.Text;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public sealed partial class RunFlowController
    {
        EventDefinition _storyEvent;
        string _storyTargetId = "";
        string _storyTargetName = "仲間";
        PersonalityOpinion[] _opinions = Array.Empty<PersonalityOpinion>();
        System.Random _eventRng;
        bool _grantEventRelicAfterCards;

        void StartStoryEvent()
        {
            var ev = ResolveOpenEvent();
            if (ev == null)
            {
                MarkCurrentCleared();
                PersistActiveRun();
                ShowMap();
                _pathUi.ShowModal(
                    "出来事",
                    "この階で起きることは、もう残っていなかった。",
                    new (string, Action)[] { ("マップに戻る", null) });
                return;
            }

            _storyEvent = ev;
            _dungeon.RememberEvent(ev.id);
            _opinions = PersonalityOpinions.Roll(_profile, ev.options, EventRng());
            LogOpinions();
            PersistActiveRun();

            var left = ev.options != null && ev.options.Length > 0 ? ev.options[0] : null;
            var right = ev.options != null && ev.options.Length > 1 ? ev.options[1] : null;
            var showChance = EventText.NeedsChanceLabel(left, right);
            _pathUi.ShowStoryEvent(new StoryEventView
            {
                Badge = $"第{_dungeon.Floor}層 {FloorOrdinal(_dungeon.CurrentIndex + 1)} ・ イベント",
                Title = string.IsNullOrEmpty(ev.title) ? "出来事" : ev.title,
                Body = EventText.Fill(ev.body, _storyTargetName),
                Category = ev.category,
                Coins = _party != null ? _party.RunCoins : 0,
                Materials = _exploration != null ? _exploration.MaterialCount : 0,
                Hp = _party != null ? (int)_party.CurrentHp : 0,
                HpMax = _party != null ? (int)_party.MaxHp : 0,
                Madness = _madness != null ? _madness.CurrentMadness : 0,
                MadnessMax = _madness != null ? _madness.MaxMadness : 0,
                ShowChance = showChance,
                Aside = YieldAside(),
                Choices = new[]
                {
                    ChoiceView(left, 0, showChance),
                    ChoiceView(right, 1, showChance)
                },
                Party = _trust != null ? _trust.Gauges(_profile) : Array.Empty<TrustGaugeView>()
            }, CommitStoryEvent);
        }

        StoryEventChoiceView ChoiceView(EventOption option, int index, bool showChance)
        {
            var fallback = index == 0 ? "左" : "右";
            return new StoryEventChoiceView
            {
                Label = EventText.Fill(option != null && !string.IsNullOrEmpty(option.label) ? option.label : fallback, _storyTargetName),
                Effects = EventText.DescribeOptionLines(option, _party, _storyTargetName, showChance),
                Marks = TrustMarks(option, index)
            };
        }

        StoryEventTrustMark[] TrustMarks(EventOption option, int optionIndex)
        {
            var gain = PersonalityCatalog.AgreeAffinity;
            var marks = new List<StoryEventTrustMark>();
            if (_opinions != null)
            {
                for (var i = 0; i < _opinions.Length; i++)
                {
                    var opinion = _opinions[i];
                    if (opinion == null || opinion.Speak != PersonalitySpeak.Agree || opinion.Choice != optionIndex)
                        continue;
                    if (string.IsNullOrEmpty(opinion.Name))
                        continue;
                    marks.Add(new StoryEventTrustMark
                    {
                        CharacterId = opinion.Id,
                        Name = opinion.Name,
                        Up = true,
                        Delta = gain,
                        Line = opinion.Line,
                        Supporter = true
                    });
                }
            }

            AddChoiceTrustPreview(option, marks);
            return marks.ToArray();
        }

        void AddChoiceTrustPreview(EventOption option, List<StoryEventTrustMark> marks)
        {
            if (option == null || _trust == null)
                return;
            var gauges = _trust.Gauges(_profile);
            if (gauges == null)
                return;
            for (var i = 0; i < gauges.Length; i++)
            {
                var gauge = gauges[i];
                if (gauge == null || string.IsNullOrEmpty(gauge.Name))
                    continue;
                var extra = GuaranteedTrustDelta(option, gauge.CharacterId, gauge.Name);
                if (extra == 0)
                    continue;
                StoryEventTrustMark found = null;
                for (var m = 0; m < marks.Count; m++)
                {
                    if (marks[m] != null && marks[m].Name == gauge.Name)
                    {
                        found = marks[m];
                        break;
                    }
                }

                if (found != null)
                {
                    found.Delta += extra;
                    continue;
                }

                marks.Add(new StoryEventTrustMark
                {
                    CharacterId = gauge.CharacterId,
                    Name = gauge.Name,
                    Up = extra > 0,
                    Delta = extra
                });
            }
        }

        int GuaranteedTrustDelta(EventOption option, string characterId, string displayName)
        {
            if (option?.outcomes == null || _trust == null)
                return 0;
            var formation = _trust.Formation(_party);
            var inFormation = false;
            if (formation != null)
            {
                for (var i = 0; i < formation.Count; i++)
                {
                    var character = formation[i];
                    if (character != null && StarterCharacters.Same(character.characterId, characterId))
                        inFormation = true;
                }
            }

            var isTarget = (!string.IsNullOrEmpty(characterId) && StarterCharacters.Same(characterId, _storyTargetId))
                || (!string.IsNullOrEmpty(displayName) && (displayName == _storyTargetName || StarterCharacters.Same(displayName, _storyTargetName)));
            int? common = null;
            var saw = false;
            for (var i = 0; i < option.outcomes.Length; i++)
            {
                var outcome = option.outcomes[i];
                if (outcome == null || outcome.chance <= 0)
                    continue;
                saw = true;
                var delta = 0;
                var effects = outcome.effects;
                if (effects != null)
                {
                    for (var e = 0; e < effects.Length; e++)
                    {
                        var effect = effects[e];
                        if (effect == null || effect.type != "trust" || effect.value == 0)
                            continue;
                        if (effect.target == "all")
                        {
                            if (inFormation)
                                delta += effect.value;
                        }
                        else if (isTarget)
                            delta += effect.value;
                    }
                }

                if (common == null)
                    common = delta;
                else if (common.Value != delta)
                    return 0;
            }

            return saw && common.HasValue ? common.Value : 0;
        }

        string YieldAside()
        {
            if (_opinions == null || _opinions.Length == 0)
                return "";
            var lines = new StringBuilder();
            for (var i = 0; i < _opinions.Length; i++)
            {
                var opinion = _opinions[i];
                if (opinion == null || opinion.Speak != PersonalitySpeak.Yield)
                    continue;
                if (lines.Length > 0)
                    lines.Append("　");
                lines.Append(opinion.Name);
                lines.Append("（遠慮）");
                if (!string.IsNullOrEmpty(opinion.Line))
                    lines.Append(opinion.Line);
            }

            return lines.ToString();
        }

        void LogOpinions()
        {
            if (_opinions == null)
                return;
            for (var i = 0; i < _opinions.Length; i++)
            {
                var opinion = _opinions[i];
                if (opinion == null)
                    continue;
                var line = PersonalityOpinions.Describe(opinion);
                if (!string.IsNullOrEmpty(opinion.Line))
                    line += " 「" + opinion.Line + "」";
                Debug.Log("[Opinion] " + line);
            }
        }

        static string FloorOrdinal(int index)
        {
            var n = index < 1 ? 1 : index;
            var tens = n % 100;
            string suffix;
            if (tens >= 11 && tens <= 13)
                suffix = "th";
            else
            {
                switch (n % 10)
                {
                    case 1: suffix = "st"; break;
                    case 2: suffix = "nd"; break;
                    case 3: suffix = "rd"; break;
                    default: suffix = "th"; break;
                }
            }

            return n + suffix;
        }

        void CommitStoryEvent(int optionIndex)
        {
            var ev = _storyEvent;
            _storyEvent = null;
            if (ev?.options == null || optionIndex < 0 || optionIndex >= ev.options.Length)
                return;

            var option = ev.options[optionIndex];
            var outcome = RollOutcome(option);
            var trustNote = ApplyVoiceTrust(optionIndex);
            ClearOpenEvent();

            if (TryBeginEventBattle(ev, outcome, trustNote))
                return;

            var summary = ApplyRewardEffects(outcome?.effects, out var runOver, out var warpFloor, out var relic);
            if (!string.IsNullOrEmpty(trustNote))
                summary = string.IsNullOrEmpty(summary) ? trustNote : trustNote + "\n" + summary;
            if (runOver)
                return;

            if (_profile != null)
                _profile.towerRunEventWarpFloor = warpFloor;
            MarkCurrentCleared();
            PersistActiveRun();
            var body = EventText.Fill(outcome != null ? outcome.text : "", _storyTargetName);
            if (!string.IsNullOrEmpty(summary))
                body = body + "\n\n" + summary;
            _pathUi.ShowModal(
                ev.title,
                body,
                new (string, Action)[]
                {
                    ("次へ", () => FinishStoryEvent(warpFloor, relic))
                });
        }

        bool TryBeginEventBattle(EventDefinition ev, EventOutcome outcome, string trustNote)
        {
            var effects = outcome?.effects;
            if (effects == null)
                return false;

            var battle = false;
            var elite = false;
            var enemyId = "";
            var enemyHp = 0;
            var opening = 0;
            var rewards = new List<EventEffect>();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                    continue;
                switch (effect.type)
                {
                    case "battle":
                        battle = true;
                        elite = effect.kind == "elite";
                        enemyId = effect.enemyId;
                        break;
                    case "battle_enemy_hp_percent":
                        enemyHp += effect.value;
                        break;
                    case "battle_opening_seconds":
                        opening += effect.value;
                        break;
                    default:
                        rewards.Add(effect);
                        break;
                }
            }

            if (!battle)
                return false;

            _profile.eventBattle ??= new EventBattlePendingSave();
            var pending = _profile.eventBattle;
            pending.active = true;
            pending.elite = elite;
            pending.enemyId = enemyId;
            pending.enemyHpPercent = enemyHp;
            pending.openingSeconds = opening;
            pending.eventTitle = ev.title;
            pending.battleIntro = EventText.Fill(outcome.battleIntro, _storyTargetName);
            pending.victoryText = EventText.Fill(outcome.victoryText, _storyTargetName);
            pending.resultText = EventText.Fill(outcome.text, _storyTargetName);
            if (!string.IsNullOrEmpty(trustNote))
                pending.resultText += "\n" + trustNote;
            pending.rewards = rewards.ToArray();
            PersistActiveRun();
            ContinuePendingEventBattle();
            return true;
        }

        void ContinuePendingEventBattle()
        {
            var pending = _profile?.eventBattle;
            if (pending == null || string.IsNullOrWhiteSpace(pending.battleIntro) ||
                _dungeon.Current?.Type != DungeonNodeType.Event)
            {
                BeginPendingEventBattle();
                return;
            }

            _pathUi.ShowModal(pending.eventTitle, pending.battleIntro, new (string, Action)[]
            {
                ("戦闘へ", () =>
                {
                    pending.battleIntro = "";
                    PersistActiveRun();
                    BeginPendingEventBattle();
                })
            });
        }

        void BeginPendingEventBattle()
        {
            var pending = _profile != null ? _profile.eventBattle : null;
            var node = _dungeon.Current;
            if (pending == null || !pending.active || node == null || node.Type != DungeonNodeType.Event)
            {
                ClearPendingEventBattle();
                ShowMap();
                return;
            }

            var mods = pending.elite ? BuildEliteMods() : BuildCombatMods();
            if (!string.IsNullOrEmpty(pending.enemyId))
            {
                var enemies = EnemyCatalogDefinition.LoadOrFallback()?.enemies;
                mods.EnemyOverride = System.Array.Find(enemies ?? System.Array.Empty<EnemyDefinition>(),
                    enemy => enemy != null && enemy.enemyId == pending.enemyId);
                if (mods.EnemyOverride == null)
                {
                    Debug.LogError($"イベント戦闘の敵 ID が EnemyCatalog にありません: {pending.enemyId}");
                    ClearPendingEventBattle();
                    CompleteNodeAndReturnToMap();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(pending.resultText))
            {
                mods.StartLogMessage = string.IsNullOrEmpty(mods.StartLogMessage)
                    ? pending.resultText
                    : mods.StartLogMessage + "\n" + pending.resultText;
            }

            StartBattleForNode(
                node,
                pending.elite ? ResolveEliteEncounter() : ResolveCombatEncounter(),
                mods);
        }

        bool TryResumeEventBattle()
        {
            var pending = _profile != null ? _profile.eventBattle : null;
            if (pending == null || !pending.active)
                return false;
            ContinuePendingEventBattle();
            return true;
        }

        /// <summary>勝利後の報酬。戦闘は終わっている。発狂か全滅なら true。</summary>
        bool ApplyEventBattleRewards(out string note)
        {
            note = "";
            var pending = _profile != null ? _profile.eventBattle : null;
            if (pending == null || !pending.active)
                return false;

            var rewards = pending.rewards;
            pending.active = false;
            pending.rewards = Array.Empty<EventEffect>();
            var summary = ApplyRewardEffects(rewards, out var runOver, out var warpFloor, out var relic);
            note = summary;
            if (runOver)
                return true;
            if (_profile != null)
                _profile.towerRunEventWarpFloor = warpFloor;
            if (warpFloor > 0)
                _eventWarpFloor = warpFloor;
            _grantEventRelicAfterCards = relic;
            return false;
        }

        int _eventWarpFloor;

        void FinishEventBattleExtras()
        {
            var warp = _eventWarpFloor;
            var relic = _grantEventRelicAfterCards;
            _eventWarpFloor = 0;
            _grantEventRelicAfterCards = false;
            if (warp > 0 || relic)
                FinishStoryEvent(warp, relic);
        }

        void FinishStoryEvent(int warpFloor, bool relic)
        {
            if (relic)
            {
                var offer = PickOfferedRelic(_dungeon.CurrentNodeId, 91);
                if (offer == null)
                {
                    LeaveStoryEvent(warpFloor);
                    return;
                }

                PromptRelicEquip(offer, 0, () => LeaveStoryEvent(warpFloor), () => LeaveStoryEvent(warpFloor));
                return;
            }

            LeaveStoryEvent(warpFloor);
        }

        void LeaveStoryEvent(int warpFloor)
        {
            if (warpFloor > 0 && warpFloor != _dungeon.Floor)
            {
                if (_profile != null)
                    _profile.towerRunEventWarpFloor = warpFloor;
                PersistActiveRun();
                _pathUi.Hide();
                OpenBoardPreparation(warpFloor, startAtElevator: false);
                return;
            }

            if (_profile != null)
                _profile.towerRunEventWarpFloor = 0;
            PersistActiveRun();
            if (!TryPresentLayerExit())
                ShowMap();
        }

        EventDefinition ResolveOpenEvent()
        {
            var floor = _dungeon.Floor;
            var index = _dungeon.CurrentIndex;
            if (_profile != null &&
                _profile.towerRunOpenEventFloor == floor &&
                _profile.towerRunOpenEventIndex == index)
            {
                var again = EventCatalog.Find(_profile.towerRunOpenEventId);
                if (again != null)
                {
                    BindStoryTarget(again, _profile.towerRunOpenEventTargetId);
                    return again;
                }
            }

            var picked = EventCatalog.Pick(BuildEventContext(), EventRng());
            if (picked == null || _profile == null)
                return picked;

            _profile.towerRunOpenEventId = picked.id;
            _profile.towerRunOpenEventFloor = floor;
            _profile.towerRunOpenEventIndex = index;
            BindStoryTarget(picked, "");
            _profile.towerRunOpenEventTargetId = _storyTargetId;
            return picked;
        }

        EventPickContext BuildEventContext()
        {
            var bossEvery = _layerConfig != null && _layerConfig.bossEvery > 0 ? _layerConfig.bossEvery : 10;
            return new EventPickContext(
                _dungeon.Floor,
                _party.RunCoins,
                TotalFloors,
                bossEvery,
                _dungeon.SeenEventIds,
                _profile != null ? _profile.previousRunEventIds : null);
        }

        void BindStoryTarget(EventDefinition ev, string savedId)
        {
            var formation = _trust.Formation(_party);
            CharacterConfig chosen = null;
            if (!string.IsNullOrEmpty(savedId))
            {
                for (var i = 0; i < formation.Count; i++)
                {
                    if (formation[i] != null && StarterCharacters.Same(formation[i].characterId, savedId))
                    {
                        chosen = formation[i];
                        break;
                    }
                }
            }

            if (chosen == null && formation.Count > 0)
            {
                var index = EventRng().Next(formation.Count);
                for (var n = 0; n < formation.Count; n++)
                {
                    var candidate = formation[(index + n) % formation.Count];
                    if (candidate == null)
                        continue;
                    chosen = candidate;
                    break;
                }
            }

            _storyTargetId = chosen != null ? chosen.characterId : "";
            _storyTargetName = chosen != null && !string.IsNullOrEmpty(chosen.displayName) ? chosen.displayName : "仲間";
            _ = ev;
        }

        void ClearOpenEvent()
        {
            if (_profile == null)
                return;
            _profile.towerRunOpenEventId = "";
            _profile.towerRunOpenEventFloor = -1;
            _profile.towerRunOpenEventIndex = -1;
            _profile.towerRunOpenEventTargetId = "";
        }

        EventOutcome RollOutcome(EventOption option)
        {
            var outcomes = option?.outcomes;
            if (outcomes == null || outcomes.Length == 0)
            {
                return new EventOutcome
                {
                    chance = 100,
                    text = "何も起きなかった。",
                    effects = Array.Empty<EventEffect>()
                };
            }

            var roll = EventRng().Next(100);
            var cursor = 0;
            for (var i = 0; i < outcomes.Length; i++)
            {
                var outcome = outcomes[i];
                if (outcome == null)
                    continue;
                cursor += Mathf.Max(0, outcome.chance);
                if (roll < cursor)
                    return outcome;
            }

            return outcomes[outcomes.Length - 1];
        }

        string ApplyVoiceTrust(int optionIndex)
        {
            if (_opinions == null || _opinions.Length == 0 || _profile == null || _trust == null)
                return "";

            var gain = PersonalityCatalog.AgreeAffinity;
            var lines = new StringBuilder();
            var adopted = optionIndex == 0 ? "選択肢A" : "選択肢B";
            for (var i = 0; i < _opinions.Length; i++)
            {
                var opinion = _opinions[i];
                if (opinion == null || string.IsNullOrEmpty(opinion.Id))
                    continue;
                var before = _trust.GetTrust(opinion.Id, _profile);
                string notice = "";
                var adoptedSide = opinion.Speak == PersonalitySpeak.Agree && opinion.Choice == optionIndex;
                if (adoptedSide && gain != 0)
                    notice = _trust.AdjustTrust(opinion.Id, gain, _profile);
                var actual = _trust.GetTrust(opinion.Id, _profile) - before;
                var verdict = opinion.Speak == PersonalitySpeak.Agree
                    ? (adoptedSide ? " 採用" : " 不採用")
                    : "";
                Debug.Log("[Opinion] " + PersonalityOpinions.Describe(opinion)
                    + verdict
                    + " 好感度" + (actual > 0 ? "+" : "") + actual
                    + " / プレイヤー " + adopted);
                if (actual == 0)
                    continue;
                if (lines.Length > 0)
                    lines.Append('\n');
                lines.Append(opinion.Name);
                lines.Append("の好感度 ");
                lines.Append(actual > 0 ? "+" : "");
                lines.Append(actual);
                if (!string.IsNullOrEmpty(notice))
                {
                    lines.Append("（");
                    lines.Append(notice);
                    lines.Append("）");
                }
            }

            return lines.ToString();
        }

        string ApplyRewardEffects(EventEffect[] effects, out bool runOver, out int warpFloor, out bool relic)
        {
            runOver = false;
            warpFloor = 0;
            relic = false;
            if (effects == null || effects.Length == 0)
                return "";

            var lines = new StringBuilder();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null || string.IsNullOrEmpty(effect.type))
                    continue;
                if (effect.type == "battle" || effect.type == "battle_enemy_hp_percent" || effect.type == "battle_opening_seconds")
                    continue;

                var line = ApplyOneReward(effect, ref warpFloor, ref relic);
                if (!string.IsNullOrEmpty(line))
                {
                    if (lines.Length > 0)
                        lines.Append('\n');
                    lines.Append(line);
                }

                if (_party.IsRunDead)
                {
                    runOver = true;
                    TryBeginRunDeath(fromBattle: false);
                    return lines.ToString();
                }

                if (_madness.CurrentMadness >= _madness.MaxMadness && effect.type == "madness" && effect.value > 0)
                {
                    runOver = true;
                    return lines.ToString();
                }
            }

            return lines.ToString();
        }

        string ApplyOneReward(EventEffect effect, ref int warpFloor, ref bool relic)
        {
            switch (effect.type)
            {
                case "hp_percent":
                    var points = EventText.HpPoints(_party, effect.value);
                    _party.ApplyHpDelta(points);
                    return $"HP {Signed(points)}";
                case "madness":
                    if (ApplyMadnessDeltaFromEvent(effect.value))
                        return $"狂気 {Signed(effect.value)}";
                    return $"狂気 {Signed(effect.value)}";
                case "coins":
                    return ApplyCoins(effect.value);
                case "coins_percent":
                    return ApplyCoins(EventText.CoinPoints(_party, effect.value));
                case "layer_word_damage_percent":
                    return AddLayerPercent(ref _profile.layerWordDamagePercent, effect.value, "この層の単語ダメージ");
                case "layer_damage_taken_percent":
                    return AddLayerPercent(ref _profile.layerDamageTakenPercent, effect.value, "この層の被ダメージ");
                case "layer_enemy_hp_percent":
                    return AddLayerPercent(ref _profile.layerEnemyHpPercent, effect.value, "この層の敵HP");
                case "layer_opening_seconds":
                    TouchLayerBuffs();
                    _profile.layerOpeningSeconds += effect.value;
                    return EventText.DescribeEffects(new[] { effect }, _party, _storyTargetName);
                case "layer_battle_coin_percent":
                    return AddLayerPercent(ref _profile.layerBattleCoinPercent, effect.value, "この層の戦闘報酬");
                case "layer_rerolls":
                    TouchLayerBuffs();
                    _profile.layerRerollDelta += effect.value;
                    return $"この層のリロール {Signed(effect.value)}";
                case "layer_heal_after_battle_percent":
                    return AddLayerPercent(ref _profile.layerHealAfterBattlePercent, effect.value, "この層、戦闘後のHP");
                case "warp":
                    var bossEvery = _layerConfig != null && _layerConfig.bossEvery > 0 ? _layerConfig.bossEvery : 10;
                    var dest = EventCatalog.WarpDestination(_dungeon.Floor, effect.value, TotalFloors, bossEvery);
                    if (dest == _dungeon.Floor)
                        return "道は、この層の先へは続いていなかった";
                    warpFloor = dest;
                    return dest > _dungeon.Floor ? $"{dest}層へ進んだ" : $"{dest}層へ戻った";
                case "trust":
                    return ApplyTrustEffect(effect);
                case "card_choices":
                    var extra = Mathf.Max(1, effect.value);
                    _profile.opinionPendingCardChoices += extra;
                    return $"次の刻印の選択肢 +{extra}";
                case "skill_upgrade_random":
                    if (_party.TryUpgradeRandomSkill(EventRng(), out var skillName, out var level))
                        return $"{skillName} が Lv.{level} になった";
                    _party.AddCoins(15);
                    return "強化できる刻印がなかった。塔片 +15";
                case "relic_gain":
                    relic = true;
                    return "レリックを受け取れる";
                default:
                    Debug.LogWarning($"未対応のイベント効果: {effect.type}");
                    return "";
            }
        }

        string ApplyCoins(int delta)
        {
            if (delta > 0)
            {
                _party.AddCoins(delta);
                return $"塔片 +{delta}";
            }

            if (delta < 0)
            {
                var spent = Mathf.Min(_party.RunCoins, -delta);
                _party.TrySpendCoins(spent);
                return $"塔片 −{spent}";
            }

            return "塔片 変化なし";
        }

        string ApplyTrustEffect(EventEffect effect)
        {
            if (effect.target == "all")
            {
                var formation = _trust.Formation(_party);
                var notices = new StringBuilder();
                for (var i = 0; i < formation.Count; i++)
                {
                    var character = formation[i];
                    if (character == null)
                        continue;
                    var notice = _trust.AdjustTrust(character.characterId, effect.value, _profile);
                    if (!string.IsNullOrEmpty(notice))
                    {
                        if (notices.Length > 0)
                            notices.Append(" ");
                        notices.Append(notice);
                    }
                }

                var line = $"全員の好感度 {Signed(effect.value)}";
                return notices.Length == 0 ? line : line + "（" + notices + "）";
            }

            var who = string.IsNullOrEmpty(_storyTargetId) ? "" : _storyTargetId;
            var tier = _trust.AdjustTrust(who, effect.value, _profile);
            var text = $"{_storyTargetName}の好感度 {Signed(effect.value)}";
            return string.IsNullOrEmpty(tier) ? text : text + "（" + tier + "）";
        }

        string AddLayerPercent(ref int field, int delta, string label)
        {
            TouchLayerBuffs();
            field += delta;
            return $"{label} {Signed(delta)}%";
        }

        void TouchLayerBuffs()
        {
            if (_profile == null)
                return;
            if (_profile.layerBuffFloor != _dungeon.Floor)
                ClearLayerBuffs(_dungeon.Floor);
        }

        void ClearLayerBuffs(int floor)
        {
            if (_profile == null)
                return;
            _profile.layerBuffFloor = floor;
            _profile.layerWordDamagePercent = 0;
            _profile.layerDamageTakenPercent = 0;
            _profile.layerEnemyHpPercent = 0;
            _profile.layerOpeningSeconds = 0;
            _profile.layerBattleCoinPercent = 0;
            _profile.layerRerollDelta = 0;
            _profile.layerHealAfterBattlePercent = 0;
        }

        void ApplyLayerCombatModifiers(ref EncounterBattleModifiers mods)
        {
            if (_profile == null || _dungeon == null || _profile.layerBuffFloor != _dungeon.Floor)
                return;

            if (_profile.layerWordDamagePercent != 0)
            {
                var word = mods.WordDamageMultiplier > 0f ? mods.WordDamageMultiplier : 1f;
                mods.WordDamageMultiplier = word * (1f + _profile.layerWordDamagePercent / 100f);
            }

            if (_profile.layerDamageTakenPercent != 0)
            {
                var taken = mods.DamageTakenMultiplier > 0f ? mods.DamageTakenMultiplier : 1f;
                mods.DamageTakenMultiplier = taken * (1f + _profile.layerDamageTakenPercent / 100f);
            }

            if (_profile.layerEnemyHpPercent != 0)
                mods.EnemyHpMultiplier *= Mathf.Max(0.05f, 1f + _profile.layerEnemyHpPercent / 100f);
            mods.OpeningAttackTimeDeltaSeconds += _profile.layerOpeningSeconds;
            mods.RerollBonus += _profile.layerRerollDelta;
        }

        void ApplyEventBattleModifiers(ref EncounterBattleModifiers mods, LayerSquare node)
        {
            var pending = _profile != null ? _profile.eventBattle : null;
            if (pending == null || !pending.active || node == null || node.Type != DungeonNodeType.Event)
                return;
            if (pending.enemyHpPercent != 0)
                mods.EnemyHpMultiplier *= Mathf.Max(0.05f, 1f + pending.enemyHpPercent / 100f);
            mods.OpeningAttackTimeDeltaSeconds += pending.openingSeconds;
        }

        void ApplyLayerPostBattleHeal()
        {
            if (_profile == null || _dungeon == null || _profile.layerBuffFloor != _dungeon.Floor)
                return;
            if (_profile.layerHealAfterBattlePercent <= 0)
                return;
            _party.HealFraction(_profile.layerHealAfterBattlePercent / 100f);
        }

        int ApplyLayerCoinScale(int reward)
        {
            if (reward <= 0 || _profile == null || _dungeon == null || _profile.layerBuffFloor != _dungeon.Floor)
                return reward;
            if (_profile.layerBattleCoinPercent == 0)
                return reward;
            return Mathf.Max(0, Mathf.RoundToInt(reward * (1f + _profile.layerBattleCoinPercent / 100f)));
        }

        bool ApplyDangerDemerit(DemeritEvent picked, out string note)
        {
            note = "";
            if (picked == null)
                return false;
            var lines = new StringBuilder();
            if (!string.IsNullOrEmpty(picked.text))
                lines.Append(picked.text);
            var effects = picked.effects ?? Array.Empty<EventEffect>();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                    continue;
                if (!ApplyOneDemerit(effect, out var piece))
                {
                    Debug.Log("[Demerit] " + picked.id + " 探索終了");
                    note = "";
                    return true;
                }

                if (string.IsNullOrEmpty(piece))
                    continue;
                if (lines.Length > 0)
                    lines.Append('\n');
                lines.Append(piece);
            }

            note = lines.ToString();
            Debug.Log("[Demerit] " + picked.id + " " + note.Replace('\n', ' '));
            return false;
        }

        bool ApplyOneDemerit(EventEffect effect, out string piece)
        {
            piece = "";
            switch (effect.type)
            {
                case "madness":
                    piece = "狂気 " + Signed(effect.value);
                    return !ApplyMadnessDeltaFromEvent(effect.value);
                case "hp_percent":
                    if (_party == null)
                        return true;
                    var points = EventText.HpPoints(_party, effect.value);
                    _party.ApplyHpDelta(points);
                    piece = "HP " + Signed(points);
                    return true;
                case "coins":
                    piece = ApplyCoins(effect.value);
                    return true;
                case "affinity_all":
                    piece = AdjustListedAffinity(effect.value, one: false);
                    return true;
                case "affinity_one":
                    piece = AdjustListedAffinity(effect.value, one: true);
                    return true;
                default:
                    return true;
            }
        }

        string AdjustListedAffinity(int delta, bool one)
        {
            var characters = PersonalityCatalog.Characters;
            if (characters.Length == 0 || _trust == null || _profile == null || delta == 0)
                return "";
            var ids = new List<string>();
            var names = new List<string>();
            for (var i = 0; i < characters.Length; i++)
            {
                if (characters[i] == null || string.IsNullOrEmpty(characters[i].id))
                    continue;
                ids.Add(characters[i].id);
                names.Add(characters[i].name);
            }

            if (ids.Count == 0)
                return "";
            var start = 0;
            var end = ids.Count;
            if (one)
            {
                start = EventRng().Next(ids.Count);
                end = start + 1;
            }

            var lines = new StringBuilder();
            for (var i = start; i < end; i++)
            {
                var before = _trust.GetTrust(ids[i], _profile);
                _trust.AdjustTrust(ids[i], delta, _profile);
                var actual = _trust.GetTrust(ids[i], _profile) - before;
                if (lines.Length > 0)
                    lines.Append('\n');
                lines.Append(names[i]);
                lines.Append("の好感度 ");
                lines.Append(actual > 0 ? "+" : "");
                lines.Append(actual);
            }

            return lines.ToString();
        }

        System.Random EventRng()
        {
            if (_eventRng == null)
            {
                var serial = _profile != null ? _profile.opinionDrawSerial : 0;
                _eventRng = new System.Random(unchecked(_runSeed * 911 ^ (serial + 1) * 3571));
            }

            return _eventRng;
        }

        static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
