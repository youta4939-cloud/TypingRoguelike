using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public static class EventText
    {
        public const string BenefitColor = "#80BAAB";
        public const string RiskColor = "#CD8173";
        public const string MadnessColor = "#B39ACA";
        static readonly Regex SignedValue = new(@"[+＋\-−]\d+(?:\.\d+)?(?:%|％|秒|回|層)?");

        // Color the delta, not its description. A negative enemy-HP modifier is
        // beneficial; madness deliberately uses its own resource color.
        public static string DisplayLine(StoryEventEffectLine line)
        {
            if (line == null) return "";
            var text = (line.Text ?? "").Replace("&", "＆").Replace("<", "＜").Replace(">", "＞");
            var color = line.Kind == "madness" ? MadnessColor
                : line.Tone > 0 ? BenefitColor : line.Tone < 0 ? RiskColor : null;
            return color == null ? text : SignedValue.Replace(text, m => $"<color={color}>{m.Value}</color>");
        }
        public static string Fill(string text, string targetName)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Replace("{target}", string.IsNullOrEmpty(targetName) ? "仲間" : targetName);
        }

        public static bool NeedsChanceLabel(EventOption left, EventOption right) =>
            HasUncertain(left) || HasUncertain(right);

        public static StoryEventEffectLine[] DescribeOptionLines(
            EventOption option,
            RunPartyState party,
            string targetName,
            bool showChance)
        {
            var lines = new List<StoryEventEffectLine>();
            if (option?.outcomes == null || option.outcomes.Length == 0)
            {
                lines.Add(Line("", "変化なし", 0, ""));
                return lines.ToArray();
            }

            var wrote = false;
            for (var i = 0; i < option.outcomes.Length; i++)
            {
                var outcome = option.outcomes[i];
                if (outcome == null)
                    continue;
                if (wrote && showChance)
                    lines.Add(Line("", "", 0, "gap"));
                AppendOutcomeLines(lines, outcome, party, targetName, showChance);
                wrote = true;
            }

            if (!wrote)
                lines.Add(Line("", "変化なし", 0, ""));
            return lines.ToArray();
        }

        static void AppendOutcomeLines(
            List<StoryEventEffectLine> lines,
            EventOutcome outcome,
            RunPartyState party,
            string targetName,
            bool showChance)
        {
            var battle = new List<StoryEventEffectLine>();
            var rewards = new List<StoryEventEffectLine>();
            var effects = outcome.effects;
            if (effects != null)
            {
                for (var i = 0; i < effects.Length; i++)
                {
                    var effect = effects[i];
                    if (effect == null || string.IsNullOrEmpty(effect.type))
                        continue;
                    var text = DescribeOne(effect, party, targetName);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    var line = Line("", text, ToneOf(effect), effect.type);
                    if (effect.type == "battle" || effect.type == "battle_enemy_hp_percent" || effect.type == "battle_opening_seconds")
                        battle.Add(line);
                    else
                        rewards.Add(line);
                }
            }

            var chance = showChance ? Mathf.Max(0, outcome.chance) + "%:" : "";
            if (battle.Count == 0)
            {
                if (rewards.Count == 0)
                    rewards.Add(Line("", "変化なし", 0, ""));
                rewards[0].Chance = chance;
                lines.AddRange(rewards);
                return;
            }

            battle[0].Chance = chance;
            lines.AddRange(battle);
            if (rewards.Count == 0)
                return;
            lines.Add(Line("", "勝利後", 0, "note"));
            lines.AddRange(rewards);
        }

        static StoryEventEffectLine Line(string chance, string text, int tone, string kind) =>
            new StoryEventEffectLine { Chance = chance ?? "", Text = text ?? "", Tone = tone, Kind = kind ?? "" };

        static bool HasUncertain(EventOption option)
        {
            if (option?.outcomes == null)
                return false;
            for (var i = 0; i < option.outcomes.Length; i++)
            {
                var outcome = option.outcomes[i];
                if (outcome != null && outcome.chance < 100)
                    return true;
            }

            return false;
        }

        static int ToneOf(EventEffect effect)
        {
            var value = effect.value;
            switch (effect.type)
            {
                case "hp_percent":
                case "coins":
                case "coins_percent":
                case "layer_word_damage_percent":
                case "layer_battle_coin_percent":
                case "layer_rerolls":
                case "layer_heal_after_battle_percent":
                    return value > 0 ? 1 : value < 0 ? -1 : 0;
                case "madness":
                case "battle_enemy_hp_percent":
                case "layer_damage_taken_percent":
                case "layer_enemy_hp_percent":
                    return value > 0 ? -1 : value < 0 ? 1 : 0;
                case "battle_opening_seconds":
                case "layer_opening_seconds":
                    return value > 0 ? 1 : value < 0 ? -1 : 0;
                case "battle":
                    return -1;
                case "warp":
                case "skill_upgrade_random":
                case "relic_gain":
                    return 1;
                default:
                    return 0;
            }
        }

        public static string DescribeEffects(EventEffect[] effects, RunPartyState party, string targetName)
        {
            if (effects == null || effects.Length == 0)
                return "変化なし";

            var battle = false;
            var builder = new StringBuilder();
            var rewards = new StringBuilder();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null || string.IsNullOrEmpty(effect.type))
                    continue;
                if (effect.type == "battle" || effect.type == "battle_enemy_hp_percent" || effect.type == "battle_opening_seconds")
                {
                    if (effect.type == "battle")
                        battle = true;
                    Append(builder, DescribeOne(effect, party, targetName));
                    continue;
                }

                Append(rewards, DescribeOne(effect, party, targetName));
            }

            if (!battle)
                return rewards.Length == 0 ? "変化なし" : rewards.ToString();
            if (rewards.Length == 0)
                return builder.Length == 0 ? "戦闘" : builder.ToString();
            if (builder.Length > 0)
                builder.Append("、");
            builder.Append("勝利後 ");
            builder.Append(rewards);
            return builder.ToString();
        }

        static void Append(StringBuilder builder, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (builder.Length > 0)
                builder.Append("、");
            builder.Append(text);
        }

        static string DescribeOne(EventEffect effect, RunPartyState party, string targetName)
        {
            var value = effect.value;
            switch (effect.type)
            {
                case "hp_percent":
                    return $"HP {Signed(value)}%（{Signed(HpPoints(party, value))}）";
                case "madness":
                    return $"狂気 {Signed(value)}";
                case "coins":
                    return $"塔片 {Signed(value)}";
                case "coins_percent":
                    return $"塔片 {Signed(value)}%（{Signed(CoinPoints(party, value))}）";
                case "battle":
                    return effect.kind == "elite" ? "強敵と戦闘" : "戦闘";
                case "battle_enemy_hp_percent":
                    return $"敵HP {Signed(value)}%";
                case "battle_opening_seconds":
                    return Opening("敵の初撃まで", value);
                case "layer_word_damage_percent":
                    return $"この層の単語ダメージ {Signed(value)}%";
                case "layer_damage_taken_percent":
                    return $"この層の被ダメージ {Signed(value)}%";
                case "layer_enemy_hp_percent":
                    return $"この層の敵HP {Signed(value)}%";
                case "layer_opening_seconds":
                    return Opening("この層、敵の初撃まで", value);
                case "layer_battle_coin_percent":
                    return $"この層の戦闘報酬 {Signed(value)}%";
                case "layer_rerolls":
                    return $"この層のリロール {Signed(value)}";
                case "layer_heal_after_battle_percent":
                    return $"この層、戦闘後にHP {Signed(value)}%";
                case "warp":
                    return value >= 0 ? $"{value}層先へ" : $"{-value}層前へ";
                case "relic_gain":
                    return "レリック入手";
                default:
                    return "";
            }
        }

        public static int HpPoints(RunPartyState party, int percent)
        {
            if (party == null || party.MaxHp <= 0f || percent == 0)
                return 0;
            var points = Mathf.RoundToInt(party.MaxHp * percent / 100f);
            if (points == 0)
                points = percent > 0 ? 1 : -1;
            return points;
        }

        public static int CoinPoints(RunPartyState party, int percent)
        {
            if (party == null || percent == 0)
                return 0;
            return Mathf.RoundToInt(party.RunCoins * percent / 100f);
        }

        static string Opening(string label, int seconds)
        {
            if (seconds == 0)
                return label + " 変化なし";
            return $"{label} {Signed(seconds)}秒";
        }

        static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }

    public sealed class StoryEventEffectLine
    {
        public string Chance = "";
        public string Text = "";
        public int Tone;
        public string Kind = "";
    }

    public sealed class StoryEventTrustMark
    {
        public string CharacterId = "";
        public string Name = "";
        public bool Up;
        public int Delta;
        public string Line = "";
        public bool Supporter;
    }

    public sealed class StoryEventChoiceView
    {
        public string Label = "";
        public StoryEventEffectLine[] Effects = Array.Empty<StoryEventEffectLine>();
        public StoryEventTrustMark[] Marks = Array.Empty<StoryEventTrustMark>();
    }

    public sealed class StoryEventView
    {
        public string Badge = "";
        public string Title = "";
        public string Body = "";
        public string Category = "";
        public int Coins;
        public int Materials;
        public int Hp;
        public int HpMax;
        public int Madness;
        public int MadnessMax;
        public bool ShowChance;
        public string Aside = "";
        public StoryEventChoiceView[] Choices = Array.Empty<StoryEventChoiceView>();
        public TrustGaugeView[] Party = Array.Empty<TrustGaugeView>();
    }
}
