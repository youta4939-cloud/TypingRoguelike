using System;
using System.Collections.Generic;

namespace TypingRoguelike.Data
{
    /// <summary>
    /// Resources/Events/*.json を JsonUtility で読む。
    /// 想定する形:
    /// { "events": [ {
    ///   "id": "smith", "layer": 1, "weight": 1,
    ///   "title": "研ぎ師", "body": "本文",
    ///   "condition": { "type": "min_cards", "value": 1 },
    ///   "lines": [ { "character": "アリス", "text": "セリフ" } ],
    ///   "options": [ {
    ///     "label": "ひたる", "input": "hitaru",
    ///     "condition": { "type": "none", "value": 0 },
    ///     "outcomes": [ {
    ///       "chance": 80, "text": "結果の一文",
    ///       "effects": [ { "type": "madness", "value": -150 } ]
    ///     } ]
    ///   } ]
    /// } ] }
    /// layer が 1 以下のイベントは全階層の候補。数値は 1 層基準で、現在階層の倍率を掛ける。
    /// input はタイピング用の英字。無い場合、label が英字ならそれを使う。
    /// </summary>
    [Serializable]
    public sealed class EventCatalogFile
    {
        public List<EventDefinition> events = new();
    }

    [Serializable]
    public sealed class EventDefinition
    {
        public string id;
        public string title;
        public string body;
        public int layer = 1;
        public int weight = 1;
        public EventCondition condition = new();
        public List<EventLine> lines = new();
        public List<EventOption> options = new();
    }

    [Serializable]
    public sealed class EventLine
    {
        public string character;
        public string text;
    }

    [Serializable]
    public sealed class EventOption
    {
        public string label;
        public string input;
        public EventCondition condition = new();
        public List<EventOutcome> outcomes = new();
    }

    [Serializable]
    public sealed class EventOutcome
    {
        public float chance = 100f;
        public string text;
        public List<EventEffect> effects = new();
    }

    [Serializable]
    public sealed class EventEffect
    {
        public string type;
        public int value;
    }

    [Serializable]
    public sealed class EventCondition
    {
        public string type = "none";
        public int value;
    }
}
