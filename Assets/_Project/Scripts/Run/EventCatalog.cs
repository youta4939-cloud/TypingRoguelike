using System;
using System.Collections.Generic;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public readonly struct EventPickContext
    {
        public readonly int Layer;
        public readonly int Coins;
        public readonly int TotalFloors;
        public readonly int BossEvery;
        public readonly IReadOnlyList<string> Seen;
        public readonly IReadOnlyList<string> Previous;

        public EventPickContext(
            int layer,
            int coins,
            int totalFloors,
            int bossEvery,
            IReadOnlyList<string> seen,
            IReadOnlyList<string> previous)
        {
            Layer = layer;
            Coins = coins;
            TotalFloors = totalFloors;
            BossEvery = bossEvery <= 0 ? 10 : bossEvery;
            Seen = seen;
            Previous = previous;
        }
    }

    /// <summary>Resources/Events/events.json を読み、条件を満たす出来事を1つ引く。</summary>
    public static class EventCatalog
    {
        static EventFile _file;

        public static EventDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var events = All();
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i] != null && events[i].id == id)
                    return events[i];
            }

            return null;
        }

        public static EventDefinition Pick(EventPickContext context, System.Random rng)
        {
            var events = All();
            var weight = 0;
            for (var i = 0; i < events.Length; i++)
            {
                if (!Eligible(events[i], context, ignoreSeen: false))
                    continue;
                weight += Weight(events[i], context);
            }

            if (weight <= 0 || rng == null)
                return null;

            var roll = rng.Next(weight);
            for (var i = 0; i < events.Length; i++)
            {
                var ev = events[i];
                if (!Eligible(ev, context, ignoreSeen: false))
                    continue;
                roll -= Weight(ev, context);
                if (roll < 0)
                    return ev;
            }

            return null;
        }

        public static bool Eligible(EventDefinition ev, EventPickContext context, bool ignoreSeen)
        {
            if (ev == null)
                return false;
            if (context.Layer < ev.minLayer || context.Layer > ev.maxLayer)
                return false;
            if (!ignoreSeen && Contains(context.Seen, ev.id))
                return false;
            var conditions = ev.conditions;
            if (conditions == null)
                return true;
            for (var i = 0; i < conditions.Length; i++)
            {
                if (!Meets(conditions[i], context))
                    return false;
            }

            return ev.options != null && ev.options.Length >= 2;
        }

        /// <summary>上方向。ボス層はまたがず、次のボス層で止まる。今がボス層なら進めない。</summary>
        public static int WarpDestination(int floor, int delta, int totalFloors, int bossEvery)
        {
            floor = Math.Max(1, floor);
            totalFloors = Math.Max(1, totalFloors);
            bossEvery = bossEvery <= 0 ? 10 : bossEvery;
            if (delta == 0)
                return floor;
            if (delta < 0)
                return Math.Max(1, floor + delta);
            if (floor % bossEvery == 0)
                return floor;

            var nextBoss = ((floor - 1) / bossEvery + 1) * bossEvery;
            var dest = Math.Min(floor + delta, nextBoss);
            return Math.Min(dest, totalFloors);
        }

        public static bool CanWarpUp(int floor, int totalFloors, int bossEvery) =>
            WarpDestination(floor, 1, totalFloors, bossEvery) > floor;

        public static bool CanWarpDown(int floor) => floor > 1;

        static EventDefinition[] All()
        {
            if (_file != null)
                return _file.events ?? Array.Empty<EventDefinition>();

            var asset = Resources.Load<TextAsset>("Events/events");
            if (asset == null)
            {
                Debug.LogError("Events/events.json が読めません。");
                _file = new EventFile();
                return Array.Empty<EventDefinition>();
            }

            _file = JsonUtility.FromJson<EventFile>(asset.text) ?? new EventFile();
            var ledger = Resources.Load<TextAsset>("Events/events-unity");
            if (ledger != null)
            {
                try
                {
                    var additions = JsonUtility.FromJson<EventFile>(ledger.text)?.events;
                    var merged = new List<EventDefinition>(_file.events ?? Array.Empty<EventDefinition>());
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var ev in merged)
                        if (ev != null)
                            ids.Add(ev.id);
                    foreach (var ev in additions ?? Array.Empty<EventDefinition>())
                    {
                        if (ev != null && !string.IsNullOrEmpty(ev.id) && ids.Add(ev.id))
                            merged.Add(ev);
                        else
                            Debug.LogWarning($"台帳イベントの ID が空欄、または重複しています: {ev?.id}");
                    }
                    _file.events = merged.ToArray();
                }
                catch (ArgumentException ex)
                {
                    Debug.LogError($"Events/events-unity.json の読み込みに失敗: {ex.Message}");
                }
            }
            return _file.events ?? Array.Empty<EventDefinition>();
        }

        static int Weight(EventDefinition ev, EventPickContext context) =>
            Contains(context.Previous, ev.id) ? 1 : 3;

        static bool Meets(EventCondition condition, EventPickContext context)
        {
            if (condition == null || string.IsNullOrEmpty(condition.type) || condition.type == "none")
                return true;
            switch (condition.type)
            {
                case "min_coins":
                    return context.Coins >= condition.value;
                case "can_warp_up":
                    return CanWarpUp(context.Layer, context.TotalFloors, context.BossEvery);
                case "can_warp_down":
                    return CanWarpDown(context.Layer);
                default:
                    Debug.LogWarning($"未知のイベント条件: {condition.type}");
                    return false;
            }
        }

        static bool Contains(IReadOnlyList<string> ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }

            return false;
        }
    }
}
