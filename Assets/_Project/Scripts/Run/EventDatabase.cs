using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public sealed class EventQuery
    {
        public int Floor = 1;
        public int Cards;
        public int Coins;
        public int Relics;
        public float Hp;
        public float MaxHp;
        public bool HasNextRow;
        public IReadOnlyCollection<string> SeenIds;
        public IReadOnlyCollection<string> PreviousRunIds;
    }

    public static class EventDatabase
    {
        static List<EventDefinition> _events;

        public static IReadOnlyList<EventDefinition> All
        {
            get
            {
                _events ??= Load();
                return _events;
            }
        }

        public static void Reload() => _events = Load();

        public static EventDefinition Pick(EventQuery query)
        {
            var pool = Eligible(query).ToList();
            if (pool.Count == 0)
                return null;

            var total = 0f;
            foreach (var ev in pool)
                total += Weight(ev, query);
            if (total <= 0f)
                return pool[0];

            var roll = UnityEngine.Random.value * total;
            var cursor = 0f;
            foreach (var ev in pool)
            {
                cursor += Weight(ev, query);
                if (roll < cursor)
                    return ev;
            }

            return pool[pool.Count - 1];
        }

        public static IEnumerable<EventDefinition> Eligible(EventQuery query)
        {
            foreach (var ev in All)
            {
                if (!MatchesFloor(ev, query.Floor))
                    continue;
                if (Contains(query.SeenIds, ev.id))
                    continue;
                if (!EventRules.ConditionMet(ev?.condition, query))
                    continue;
                yield return ev;
            }
        }

        public static bool MatchesFloor(EventDefinition ev, int floor)
        {
            if (ev == null || string.IsNullOrEmpty(ev.id))
                return false;
            if (ev.layer <= 1)
                return true;
            return ev.layer == Mathf.Max(1, floor);
        }

        static float Weight(EventDefinition ev, EventQuery query)
        {
            var weight = ev.weight <= 0 ? 1f : ev.weight;
            if (Contains(query.PreviousRunIds, ev.id))
                weight /= 3f;
            return weight;
        }

        static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id))
                return false;
            foreach (var item in ids)
            {
                if (item == id)
                    return true;
            }

            return false;
        }

        static List<EventDefinition> Load()
        {
            var list = new List<EventDefinition>();
            var assets = Resources.LoadAll<TextAsset>("Events");
            if (assets == null)
                return list;

            foreach (var asset in assets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                    continue;
                EventCatalogFile file;
                try
                {
                    // 配布JSONは話者キーが char。JsonUtility はフィールド名 character で読む。
                    var json = asset.text.Replace("\"char\":", "\"character\":");
                    file = JsonUtility.FromJson<EventCatalogFile>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"イベント JSON を読めませんでした: {asset.name} {ex.Message}");
                    continue;
                }

                if (file?.events == null)
                    continue;
                foreach (var ev in file.events)
                {
                    if (ev != null && !string.IsNullOrEmpty(ev.id))
                        list.Add(ev);
                }
            }

            Debug.Log($"イベント定義を {list.Count} 件読み込んだ。");
            return list;
        }
    }

    public static class EventRules
    {
        public static bool ConditionMet(EventCondition condition, EventQuery query)
        {
            var type = condition?.type?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(type) || type == "none")
                return true;

            var value = condition.value;
            switch (type)
            {
                case "min_cards":
                    return query.Cards >= value;
                case "min_coins":
                    return query.Coins >= value;
                case "min_relics":
                    return query.Relics >= value;
                case "min_hp_percent":
                    if (query.MaxHp <= 0f)
                        return false;
                    return query.Hp / query.MaxHp > value / 100f;
                case "has_next_row":
                    return query.HasNextRow;
                default:
                    return false;
            }
        }

        public static bool OptionAvailable(EventOption option, EventQuery query) =>
            option != null && ConditionMet(option.condition, query);

        public static bool OptionShown(EventOption option, EventQuery query)
        {
            if (option == null)
                return false;
            if (option.outcomes == null || option.outcomes.Count == 0)
                return true;
            return ActiveOutcomes(option, query).Count > 0;
        }

        public static List<EventOutcome> ActiveOutcomes(EventOption option, EventQuery query)
        {
            var list = new List<EventOutcome>();
            if (option?.outcomes == null)
                return list;
            foreach (var outcome in option.outcomes)
            {
                if (outcome != null && !IsZeroCoinLoss(outcome, query))
                    list.Add(outcome);
            }

            return list;
        }

        static bool IsZeroCoinLoss(EventOutcome outcome, EventQuery query)
        {
            if (outcome?.effects == null)
                return false;
            foreach (var effect in outcome.effects)
            {
                var type = effect?.type?.Trim().ToLowerInvariant();
                if (type == "coins_percent" && effect.value < 0 && CoinDelta(query.Coins, effect.value) == 0)
                    return true;
            }

            return false;
        }

        public static string TypingText(EventOption option)
        {
            if (option == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(option.input))
                return option.input.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(option.label) && option.label.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
                return option.label.ToLowerInvariant();
            return string.Empty;
        }

        public static EventOutcome Roll(EventOption option, EventQuery query)
        {
            var outcomes = ActiveOutcomes(option, query);
            if (outcomes.Count == 0)
                return null;

            var sum = 0f;
            foreach (var outcome in outcomes)
                sum += Mathf.Max(0f, outcome.chance);
            if (sum <= 0f)
                return outcomes[0];

            var roll = UnityEngine.Random.value * sum;
            var cursor = 0f;
            foreach (var outcome in outcomes)
            {
                cursor += Mathf.Max(0f, outcome.chance);
                if (roll < cursor)
                    return outcome;
            }

            return outcomes[outcomes.Count - 1];
        }

        public static string DescribeOption(EventOption option, EventQuery query)
        {
            if (option?.outcomes == null || option.outcomes.Count == 0)
                return "マップへ戻る";

            var outcomes = ActiveOutcomes(option, query);
            if (outcomes.Count == 0)
                return string.Empty;

            var sum = 0f;
            foreach (var outcome in outcomes)
                sum += Mathf.Max(0f, outcome.chance);

            var lines = new List<string>();
            foreach (var outcome in outcomes)
            {
                var percent = sum <= 0f ? 0f : Mathf.Max(0f, outcome.chance) / sum * 100f;
                var effects = DescribeEffects(outcome, query);
                lines.Add(string.IsNullOrEmpty(effects)
                    ? $"　{FormatPercent(percent)}%"
                    : $"　{FormatPercent(percent)}%：{effects}");
            }

            return string.Join("\n", lines);
        }

        public static string DescribeEffects(EventOutcome outcome, EventQuery query)
        {
            if (outcome?.effects == null || outcome.effects.Count == 0)
                return string.Empty;
            return string.Join("、", outcome.effects.Select(effect => DescribeEffect(effect, query)));
        }

        public static string DescribeEffect(EventEffect effect, EventQuery query)
        {
            var type = effect?.type?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (type)
            {
                case "madness":
                    return $"狂気 {Signed(ScaleFixed(effect.value, query.Floor))}";
                case "hp":
                    return $"HP {Signed(ScaleFixed(effect.value, query.Floor))}";
                case "coins_percent":
                    return $"通貨 {Signed(CoinDelta(query.Coins, effect.value))}（所持金の{Mathf.Abs(effect.value)}%）";
                case "card_gain":
                    return "刻印入手";
                case "card_upgrade_choose":
                    return "刻印強化";
                case "card_remove_choose":
                    return "刻印削除";
                case "relic_gain":
                    return "レリック入手";
                case "relic_lose_random":
                    return "レリック1つ喪失";
                case "battle":
                    return "戦闘";
                case "skip_row":
                    return "次の段を飛ばす";
                case "reveal_floor":
                    return "この階を開示";
                case "reveal_elevator":
                    return "エレベーターを開示";
                case "reveal_treasure":
                    return "宝箱を開示";
                default:
                    return type;
            }
        }

        public static int ScaleFixed(int value, int floor)
        {
            if (value == 0)
                return 0;
            var layer = Mathf.Max(1, floor);
            var scaled = value * (1.0 + 0.5 * (layer - 1));
            return RoundToTens(scaled);
        }

        public static int CoinDelta(int coins, int percent) =>
            Mathf.RoundToInt(coins * (percent / 100f));

        public static EventLine PickLine(EventDefinition ev, IReadOnlyCollection<string> partyNames)
        {
            if (ev?.lines == null || partyNames == null)
                return null;
            var matches = ev.lines
                .Where(line => line != null && !string.IsNullOrEmpty(line.text) && LineMatchesParty(line, partyNames))
                .ToList();
            if (matches.Count == 0)
                return null;
            return matches[UnityEngine.Random.Range(0, matches.Count)];
        }

        static int RoundToTens(double value)
        {
            var sign = value < 0 ? -1 : 1;
            var abs = Math.Abs(value);
            var tens = (int)Math.Floor(abs / 10.0 + 1e-8);
            var rem = abs - tens * 10.0;
            if (rem >= 5.0 - 1e-6)
                tens++;
            return tens * 10 * sign;
        }

        static bool LineMatchesParty(EventLine line, IReadOnlyCollection<string> partyNames)
        {
            var name = EventCharacterIds.ToCharacterName(line.character);
            foreach (var party in partyNames)
            {
                if (party == name || party == line.character)
                    return true;
            }

            return false;
        }

        static string Signed(int value)
        {
            if (value > 0)
                return $"+{value}";
            if (value < 0)
                return $"−{System.Math.Abs(value)}";
            return "0";
        }

        static string FormatPercent(float percent)
        {
            var rounded = Mathf.Round(percent * 10f) / 10f;
            return Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.05f
                ? $"{Mathf.RoundToInt(rounded)}"
                : $"{rounded:0.0}";
        }
    }
}
