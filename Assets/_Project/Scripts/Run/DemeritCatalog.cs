using System.Collections.Generic;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>Resources/Data/demerit_events.json。空白マスは表の行を等確率で1つ引く。</summary>
    public static class DemeritCatalog
    {
        const string ResourcePath = "Data/demerit_events";
        static DemeritFile _file;
        static bool _warned;

        public static DemeritEvent Pick(System.Random rng)
        {
            var events = Load()?.events;
            if (events == null || events.Length == 0 || rng == null)
                return null;
            var pool = new List<DemeritEvent>();
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i] != null)
                    pool.Add(events[i]);
            }

            if (pool.Count == 0)
                return null;
            return pool[rng.Next(pool.Count)];
        }

        static DemeritFile Load()
        {
            if (_file != null)
                return _file;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("demerit_events.json を読めなかった。");
                }

                return null;
            }

            _file = JsonUtility.FromJson<DemeritFile>(asset.text);
            return _file;
        }
    }
}
