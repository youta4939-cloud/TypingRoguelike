using System;
using System.Collections.Generic;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>危険マスを回避したときの文。Resources/Events/danger_lines_layer1.json。</summary>
    public static class DangerLineCatalog
    {
        const string ResourceName = "Events/danger_lines_layer1";
        static Dictionary<string, List<string>> _lines;

        public static string Pick(string kind, string previous)
        {
            var lines = Lines(kind);
            if (lines.Count == 0)
                return "気配がすれ違った。";

            var pool = new List<string>();
            foreach (var line in lines)
            {
                if (line != previous)
                    pool.Add(line);
            }

            if (pool.Count == 0)
                pool.AddRange(lines);
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        static List<string> Lines(string kind)
        {
            _lines ??= Load();
            return _lines.TryGetValue(kind ?? string.Empty, out var list) ? list : new List<string>();
        }

        static Dictionary<string, List<string>> Load()
        {
            var result = new Dictionary<string, List<string>>();
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning("danger_lines_layer1.json がありません。");
                return result;
            }

            result["madness"] = ReadStringArray(asset.text, "madness");
            result["coins"] = ReadStringArray(asset.text, "coins");
            result["hp"] = ReadStringArray(asset.text, "hp");
            return result;
        }

        static List<string> ReadStringArray(string json, string key)
        {
            var list = new List<string>();
            var keyAt = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyAt < 0)
                return list;
            var bracket = json.IndexOf('[', keyAt);
            if (bracket < 0)
                return list;

            var i = bracket + 1;
            while (i < json.Length && json[i] != ']')
            {
                if (json[i] != '"')
                {
                    i++;
                    continue;
                }

                var end = json.IndexOf('"', i + 1);
                if (end < 0)
                    break;
                list.Add(json.Substring(i + 1, end - i - 1));
                i = end + 1;
            }

            return list;
        }
    }
}
