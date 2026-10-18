using System;
using System.Collections.Generic;
using System.Text;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public readonly struct TrustBattleBonus
    {
        public float AttackBonus { get; }
        public float DamageTakenReduction { get; }
        public float OpeningFreezeSeconds { get; }
        public float PostBattleHealFraction { get; }
        public int CombatCardChoices { get; }
        public bool NegateMissHit { get; }
        public bool ShowEnemyAttackTime { get; }

        public TrustBattleBonus(
            float attackBonus,
            float damageTakenReduction,
            float openingFreezeSeconds,
            float postBattleHealFraction,
            int combatCardChoices,
            bool negateMissHit,
            bool showEnemyAttackTime)
        {
            AttackBonus = attackBonus;
            DamageTakenReduction = damageTakenReduction;
            OpeningFreezeSeconds = openingFreezeSeconds;
            PostBattleHealFraction = postBattleHealFraction;
            CombatCardChoices = combatCardChoices;
            NegateMissHit = negateMissHit;
            ShowEnemyAttackTime = showEnemyAttackTime;
        }
    }

    public sealed class OpinionSpeakerRoll
    {
        public CharacterConfig Character;
        public CharacterTrustSave Save;
        public float AggressiveChance;
        public int ChoiceIndex;
        public string Line;
        public string Expression;
    }

    public sealed class OpinionRollResult
    {
        public OpinionEvent Event;
        public OpinionSpeakerRoll[] Speakers;
        public string AbsentId;
        public bool Agreed;
    }

    public sealed class OpinionCommitResult
    {
        public string Summary;
        public OpinionEffectKind Effect;
        public float EffectValue;
        public OpinionEffectKind Effect2;
        public float EffectValue2;
    }

    /// <summary>信頼度・性格のゆらぎ・意見の抽選。セーブはプロフィール側。</summary>
    public sealed class TrustRoster
    {
        readonly TrustCatalog _catalog;

        public TrustRoster(TrustCatalog catalog, MetaProgressData profile)
        {
            _catalog = catalog ?? TrustCatalog.RuntimeFallback();
            Bind(profile);
        }

        public TrustRules Rules => _catalog.rules;

        public void Bind(MetaProgressData profile)
        {
            if (profile == null)
                return;
            profile.characterTrust ??= new List<CharacterTrustSave>();
            if (_catalog.characters == null)
                return;
            foreach (var character in _catalog.characters)
            {
                if (character == null || string.IsNullOrEmpty(character.characterId))
                    continue;
                if (FindSave(profile, character.characterId) == null)
                {
                    profile.characterTrust.Add(new CharacterTrustSave
                    {
                        characterId = character.characterId
                    });
                }
            }
        }

        public IReadOnlyList<CharacterConfig> Formation(RunPartyState party)
        {
            var matched = new List<CharacterConfig>();
            if (party?.Members != null && _catalog.characters != null)
            {
                foreach (var member in party.Members)
                {
                    var name = member?.character?.characterName;
                    if (string.IsNullOrEmpty(name))
                        continue;
                    foreach (var config in _catalog.characters)
                    {
                        if (config == null)
                            continue;
                        if (config.displayName == name || config.characterId == name)
                            matched.Add(config);
                    }
                }
            }

            if (matched.Count >= 2)
                return matched;
            return _catalog.characters ?? Array.Empty<CharacterConfig>();
        }

        public OpinionRollResult Roll(DungeonNodeType tile, RunPartyState party, MetaProgressData profile, System.Random rng)
        {
            Bind(profile);
            var formation = Formation(party);
            var speakers = OpinionSpeakerPicker.PickSpeakers(
                formation,
                profile != null ? profile.opinionLastAbsentId : "",
                Rules != null && Rules.preferLastAbsent,
                rng);
            if (speakers.Length < 2)
                return null;

            var opinion = _catalog.FindEvent(tile);
            if (opinion?.choiceA == null || opinion.choiceB == null)
                return null;

            var picked = new HashSet<string>();
            var rolls = new OpinionSpeakerRoll[speakers.Length];
            var log = new StringBuilder();
            log.Append("[Opinion] ");
            log.Append(LayerPathUI.SquareName(tile));
            for (var i = 0; i < speakers.Length; i++)
            {
                var character = speakers[i];
                var save = FindSave(profile, character.characterId);
                var chance = AggressiveChance(character, save, opinion.choiceA, opinion.choiceB);
                var aggressive = rng.NextDouble() < chance;
                var index = opinion.IndexOfStance(aggressive ? OpinionStance.Aggressive : OpinionStance.Defensive);
                var scene = aggressive ? OpinionScene.Aggressive : OpinionScene.Defensive;
                var line = _catalog.lines != null ? _catalog.lines.Find(character.characterId, scene, rng) : null;
                rolls[i] = new OpinionSpeakerRoll
                {
                    Character = character,
                    Save = save,
                    AggressiveChance = chance,
                    ChoiceIndex = index,
                    Line = line != null ? line.line : "",
                    Expression = line != null ? line.expression : ""
                };
                picked.Add(character.characterId);
                var choice = opinion.ChoiceAt(index);
                log.Append(" / ");
                log.Append(character.displayName);
                log.Append('(');
                log.Append(character.PersonalityName(Rules));
                log.Append(") 攻め");
                log.Append((chance * 100f).ToString("0"));
                log.Append("% → ");
                log.Append(choice != null && choice.stance == OpinionStance.Aggressive ? "攻め" : "守り");
                if (!string.IsNullOrEmpty(rolls[i].Expression))
                {
                    log.Append(" 表情:");
                    log.Append(rolls[i].Expression);
                }
            }

            string absent = "";
            foreach (var character in formation)
            {
                if (character == null || picked.Contains(character.characterId))
                    continue;
                absent = character.characterId;
                break;
            }

            var agreed = rolls.Length >= 2 && rolls[0].ChoiceIndex == rolls[1].ChoiceIndex;
            log.Append(agreed ? " / 意見一致" : " / 意見が割れた");
            Debug.Log(log.ToString());
            return new OpinionRollResult
            {
                Event = opinion,
                Speakers = rolls,
                AbsentId = absent ?? "",
                Agreed = agreed
            };
        }

        public OpinionScreenView BuildScreen(OpinionRollResult roll, MetaProgressData profile, int layer, int squareIndex, DungeonNodeType tile)
        {
            _ = squareIndex;
            var chrome = OpinionChrome.LoadOrFallback();
            var speaking = new HashSet<string>();
            var bubbles = new OpinionBubbleView[roll.Speakers.Length];
            for (var i = 0; i < roll.Speakers.Length; i++)
            {
                var speaker = roll.Speakers[i];
                if (speaker.Character != null)
                    speaking.Add(speaker.Character.characterId);
                bubbles[i] = new OpinionBubbleView
                {
                    CharacterId = speaker.Character.characterId,
                    Name = speaker.Character.displayName,
                    Line = string.IsNullOrEmpty(speaker.Line) ? "……" : $"「{speaker.Line}」",
                    ChoiceIndex = speaker.ChoiceIndex,
                    Portrait = speaker.Character.portrait
                };
            }

            var gauges = Gauges(profile);
            foreach (var gauge in gauges)
            {
                if (gauge == null)
                    continue;
                gauge.Dim = !speaking.Contains(gauge.CharacterId);
                var config = _catalog.FindCharacter(gauge.CharacterId);
                gauge.Portrait = config != null ? config.portrait : null;
            }

            var sceneName = roll.Event != null ? roll.Event.sceneName : "";
            var tileName = LayerPathUI.SquareName(tile);
            var agreedIndex = roll.Agreed ? roll.Speakers[0].ChoiceIndex : -1;
            var buttons = new OpinionButtonView[2];
            for (var i = 0; i < 2; i++)
            {
                var choice = roll.Event.ChoiceAt(i);
                var faces = new List<OpinionFaceView>();
                var previews = new List<OpinionTrustPreview>();
                foreach (var speaker in roll.Speakers)
                {
                    if (speaker?.Character == null)
                        continue;
                    var from = speaker.Save != null ? speaker.Save.trust : 0;
                    var to = Mathf.Clamp(from + TrustDelta(roll, speaker, i, layer), 0, 100);
                    previews.Add(new OpinionTrustPreview
                    {
                        CharacterId = speaker.Character.characterId,
                        From = from,
                        To = to,
                        Notice = TierNotice(speaker.Character, from, to)
                    });
                    if (speaker.ChoiceIndex != i)
                        continue;
                    faces.Add(new OpinionFaceView
                    {
                        CharacterId = speaker.Character.characterId,
                        Portrait = speaker.Character.portrait
                    });
                }

                buttons[i] = new OpinionButtonView
                {
                    Title = choice != null ? choice.body : "",
                    Effect = choice != null ? choice.DescribeEffect() : "",
                    ChoiceIndex = i,
                    Emphasized = agreedIndex == i,
                    Dimmed = roll.Agreed && agreedIndex != i,
                    Faces = faces.ToArray(),
                    Previews = previews.ToArray()
                };
            }

            return new OpinionScreenView
            {
                SceneTitle = string.IsNullOrEmpty(sceneName) ? $"{tileName}マス" : $"{tileName}マス — {sceneName}",
                SceneText = roll.Event != null ? roll.Event.sceneText : "",
                TileIcon = chrome != null ? chrome.IconFor(tile) : null,
                Agreed = roll.Agreed,
                Bubbles = bubbles,
                Buttons = buttons,
                Gauges = gauges
            };
        }

        static string TierNotice(CharacterConfig character, int from, int to)
        {
            if (character == null || from == to)
                return "";
            var before = character.ActiveTier(from);
            var after = character.ActiveTier(to);
            var beforeLabel = before != null ? before.label : "";
            var afterLabel = after != null ? after.label : "";
            if (beforeLabel == afterLabel)
                return "";
            if (to > from)
                return afterLabel ?? "";
            return "パッシブ低下";
        }

        public OpinionCommitResult Commit(OpinionRollResult roll, int choiceIndex, int layer, MetaProgressData profile, System.Random rng)
        {
            Bind(profile);
            if (profile != null)
                profile.opinionLastAbsentId = roll.AbsentId ?? "";

            var choice = roll.Event.ChoiceAt(choiceIndex);
            var rules = Rules;
            var before = SnapshotLabels(profile);
            var text = new StringBuilder();
            var sidedWithPair = roll.Agreed && roll.Speakers[0].ChoiceIndex == choiceIndex;
            if (roll.Agreed)
                text.AppendLine(sidedWithPair ? "意見一致" : "ふたりの逆を選んだ");

            foreach (var speaker in roll.Speakers)
            {
                var save = speaker.Save ?? FindSave(profile, speaker.Character.characterId);
                if (save == null)
                    continue;
                speaker.Save = save;
                save.spokeCount++;
                var followed = speaker.ChoiceIndex == choiceIndex;
                var delta = TrustDelta(roll, speaker, choiceIndex, layer);
                OpinionScene scene;
                if (followed && roll.Agreed)
                {
                    save.followedCount++;
                    scene = OpinionScene.Agree;
                }
                else if (followed)
                {
                    save.followedCount++;
                    scene = OpinionScene.Followed;
                    if (TryDrift(speaker, save, rules, roll.Event))
                        text.AppendLine($"{speaker.Character.displayName}の性格が揺れた。");
                }
                else
                    scene = OpinionScene.Ignored;

                save.trust = Mathf.Clamp(save.trust + delta, 0, 100);
                var spoken = _catalog.lines != null ? _catalog.lines.Find(speaker.Character.characterId, scene, rng) : null;
                var sign = delta > 0 ? "+" : "";
                text.Append(speaker.Character.displayName);
                if (spoken != null && !string.IsNullOrEmpty(spoken.line))
                {
                    text.Append("「");
                    text.Append(spoken.line);
                    text.Append('」');
                }

                text.Append(" 信頼度 ");
                text.Append(sign);
                text.Append(delta);
                text.AppendLine();
            }

            if (choice != null)
            {
                text.Append("効果：");
                text.AppendLine(choice.DescribeEffect());
            }

            var notice = TierNotices(profile, before);
            if (!string.IsNullOrEmpty(notice))
                text.AppendLine(notice);

            return new OpinionCommitResult
            {
                Summary = text.ToString().TrimEnd(),
                Effect = choice != null ? choice.effect : OpinionEffectKind.None,
                EffectValue = choice != null ? choice.effectValue : 0f,
                Effect2 = choice != null ? choice.effect2 : OpinionEffectKind.None,
                EffectValue2 = choice != null ? choice.effectValue2 : 0f
            };
        }

        public TrustGaugeView[] Gauges(MetaProgressData profile)
        {
            Bind(profile);
            var formation = Formation(null);
            var views = new List<TrustGaugeView>();
            foreach (var character in formation)
            {
                if (character == null)
                    continue;
                var save = FindSave(profile, character.characterId);
                var trust = save != null ? save.trust : 0;
                var tier = character.ActiveTier(trust);
                views.Add(new TrustGaugeView
                {
                    CharacterId = character.characterId,
                    Name = character.displayName,
                    Personality = character.PersonalityName(Rules),
                    Trust = trust,
                    PassiveLabel = tier != null && !string.IsNullOrEmpty(tier.label) ? tier.label : "未発動"
                });
            }

            return views.ToArray();
        }

        public string SetTrust(string characterId, int trust, MetaProgressData profile)
        {
            Bind(profile);
            var character = _catalog.FindCharacter(characterId);
            var save = FindSave(profile, characterId);
            if (character == null || save == null)
                return "";
            var before = character.ActiveTier(save.trust);
            save.trust = Mathf.Clamp(trust, 0, 100);
            var after = character.ActiveTier(save.trust);
            var beforeLabel = before != null ? before.label : "未発動";
            var afterLabel = after != null ? after.label : "未発動";
            if (beforeLabel == afterLabel)
                return "";
            return $"{character.displayName}：{afterLabel}";
        }

        public TrustBattleBonus CollectBattle(MetaProgressData profile)
        {
            Bind(profile);
            var attack = 0f;
            var taken = 0f;
            var freeze = 0f;
            var heal = 0f;
            var cards = 0;
            var negate = false;
            var showTime = false;
            foreach (var character in Formation(null))
            {
                if (character == null)
                    continue;
                var save = FindSave(profile, character.characterId);
                var tier = character.ActiveTier(save != null ? save.trust : 0);
                if (tier?.effects == null)
                    continue;
                foreach (var effect in tier.effects)
                {
                    if (effect == null)
                        continue;
                    switch (effect.kind)
                    {
                        case TrustPassiveKind.AttackPercent:
                            attack += Mathf.Max(0f, effect.value);
                            break;
                        case TrustPassiveKind.DamageTakenPercent:
                            taken += Mathf.Max(0f, effect.value);
                            break;
                        case TrustPassiveKind.OpeningFreezeSeconds:
                            freeze = Mathf.Max(freeze, effect.value);
                            break;
                        case TrustPassiveKind.PostBattleHealPercent:
                            heal += Mathf.Max(0f, effect.value);
                            break;
                        case TrustPassiveKind.NegateMissHit:
                            negate = true;
                            break;
                        case TrustPassiveKind.CardRewardChoices:
                            cards += Mathf.Max(1, Mathf.RoundToInt(effect.value));
                            break;
                        case TrustPassiveKind.ShowEnemyAttackTime:
                            showTime = true;
                            break;
                    }
                }
            }

            var cap = Rules != null ? Rules.attackBonusCap : 0.2f;
            attack = Mathf.Min(attack, Mathf.Max(0f, cap));
            taken = Mathf.Clamp(taken, 0f, 0.9f);
            return new TrustBattleBonus(attack, taken, freeze, heal, cards, negate, showTime);
        }

        public void LogEnd(string reason, MetaProgressData profile)
        {
            Bind(profile);
            var text = new StringBuilder();
            text.Append("[Trust] ラン終了（");
            text.Append(reason);
            text.AppendLine("）");
            foreach (var character in Formation(null))
            {
                if (character == null)
                    continue;
                var save = FindSave(profile, character.characterId);
                text.Append("  ");
                text.Append(character.displayName);
                text.Append(" 意見 ");
                text.Append(save != null ? save.spokeCount : 0);
                text.Append(" / 乗られた ");
                text.Append(save != null ? save.followedCount : 0);
                text.AppendLine();
            }

            Debug.Log(text.ToString().TrimEnd());
        }

        public void ResetRunCounters(MetaProgressData profile)
        {
            Bind(profile);
            if (profile?.characterTrust == null)
                return;
            foreach (var save in profile.characterTrust)
            {
                if (save == null)
                    continue;
                save.spokeCount = 0;
                save.followedCount = 0;
            }

            profile.opinionLastAbsentId = "";
            profile.opinionDrawSerial = 0;
            profile.opinionPendingCardChoices = 0;
            profile.opinionResolvedFloor = 0;
            profile.opinionResolvedIndex = -1;
            profile.opinionCombatEnemyHpScale = 1f;
            profile.opinionCombatOpeningSeconds = 0f;
            profile.opinionCombatCoinScale = 1f;
        }

        public static void Wipe(MetaProgressData profile)
        {
            if (profile?.characterTrust != null)
            {
                foreach (var save in profile.characterTrust)
                {
                    if (save == null)
                        continue;
                    save.trust = 0;
                    save.aggressiveDrift = 0f;
                    save.spokeCount = 0;
                    save.followedCount = 0;
                }
            }

            if (profile == null)
                return;
            profile.opinionLastAbsentId = "";
            profile.opinionDrawSerial = 0;
            profile.opinionPendingCardChoices = 0;
            profile.opinionResolvedFloor = 0;
            profile.opinionResolvedIndex = -1;
            profile.opinionCombatEnemyHpScale = 1f;
            profile.opinionCombatOpeningSeconds = 0f;
            profile.opinionCombatCoinScale = 1f;
        }

        float AggressiveChance(CharacterConfig character, CharacterTrustSave save, OpinionChoice left, OpinionChoice right)
        {
            var rules = Rules;
            var drift = save != null ? save.aggressiveDrift : 0f;
            var brave = rules != null ? rules.braveAggressiveChance : 0.75f;
            var timid = rules != null ? rules.timidAggressiveChance : 0.25f;
            var unknown = rules != null ? rules.curiousUnknownChance : 0.7f;
            var even = rules != null ? rules.curiousEvenChance : 0.5f;
            float baseChance;
            switch (character.personality)
            {
                case TrustPersonality.Brave:
                    baseChance = brave;
                    break;
                case TrustPersonality.Timid:
                    baseChance = timid;
                    break;
                default:
                    var leftUnknown = left != null && left.knowledge == OpinionKnowledge.Unknown;
                    var rightUnknown = right != null && right.knowledge == OpinionKnowledge.Unknown;
                    if (leftUnknown != rightUnknown)
                    {
                        var unknownIsAggressive =
                            (leftUnknown && left.stance == OpinionStance.Aggressive) ||
                            (rightUnknown && right.stance == OpinionStance.Aggressive);
                        baseChance = unknownIsAggressive ? unknown : 1f - unknown;
                    }
                    else
                        baseChance = even;
                    break;
            }

            var min = rules != null ? rules.chanceMin : 0.15f;
            var max = rules != null ? rules.chanceMax : 0.85f;
            if (max < min)
                max = min;
            return Mathf.Clamp(baseChance + drift, min, max);
        }

        int TrustDelta(OpinionRollResult roll, OpinionSpeakerRoll speaker, int choiceIndex, int layer)
        {
            var rules = Rules;
            var followed = speaker.ChoiceIndex == choiceIndex;
            if (followed && roll.Agreed)
                return rules != null ? rules.ScaleGain(rules.agreeTrust, layer) : 5;
            if (followed)
                return rules != null ? rules.ScaleGain(rules.followTrust, layer) : 15;

            var trust = speaker.Save != null ? speaker.Save.trust : 0;
            var high = rules != null ? rules.highTrustThreshold : 75;
            var mild = rules != null ? rules.ignoreTrust : -5;
            var hard = rules != null ? rules.ignoreTrustAtHigh : -10;
            return trust >= high ? hard : mild;
        }

        bool TryDrift(OpinionSpeakerRoll speaker, CharacterTrustSave save, TrustRules rules, OpinionEvent opinion)
        {
            var choice = opinion != null ? opinion.ChoiceAt(speaker.ChoiceIndex) : null;
            var votedAggressive = choice != null && choice.stance == OpinionStance.Aggressive;
            var unlikely = votedAggressive ? speaker.AggressiveChance < 0.5f : speaker.AggressiveChance > 0.5f;
            if (!unlikely)
                return false;
            var step = rules != null ? rules.driftStep : 0.03f;
            var limit = rules != null ? rules.driftLimit : 0.15f;
            save.aggressiveDrift = Mathf.Clamp(
                save.aggressiveDrift + (votedAggressive ? step : -step),
                -Mathf.Abs(limit),
                Mathf.Abs(limit));
            Debug.Log($"[Opinion] {speaker.Character.displayName} の攻め確率がゆらいだ（差 {save.aggressiveDrift * 100f:0}%)");
            return true;
        }

        Dictionary<string, string> SnapshotLabels(MetaProgressData profile)
        {
            var map = new Dictionary<string, string>();
            foreach (var character in Formation(null))
            {
                if (character == null)
                    continue;
                var save = FindSave(profile, character.characterId);
                var tier = character.ActiveTier(save != null ? save.trust : 0);
                map[character.characterId] = tier != null ? tier.label : "";
            }

            return map;
        }

        string TierNotices(MetaProgressData profile, Dictionary<string, string> before)
        {
            var text = new StringBuilder();
            foreach (var character in Formation(null))
            {
                if (character == null || !before.TryGetValue(character.characterId, out var oldLabel))
                    continue;
                var save = FindSave(profile, character.characterId);
                var tier = character.ActiveTier(save != null ? save.trust : 0);
                var next = tier != null ? tier.label : "";
                if (next == oldLabel || string.IsNullOrEmpty(next))
                    continue;
                if (text.Length > 0)
                    text.AppendLine();
                text.Append(character.displayName);
                text.Append('：');
                text.Append(next);
            }

            return text.ToString();
        }

        static CharacterTrustSave FindSave(MetaProgressData profile, string characterId)
        {
            if (profile?.characterTrust == null || string.IsNullOrEmpty(characterId))
                return null;
            foreach (var save in profile.characterTrust)
            {
                if (save != null && save.characterId == characterId)
                    return save;
            }

            return null;
        }
    }
}
