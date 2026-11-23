using System;

namespace TypingRoguelike.Data
{
    [Serializable]
    public sealed class EventFile
    {
        public int version = 1;
        public EventDefinition[] events = Array.Empty<EventDefinition>();
    }

    [Serializable]
    public sealed class EventDefinition
    {
        public string id = "";
        public string category = "";
        public string title = "";
        public int minLayer = 1;
        public int maxLayer = 15;
        public EventCondition[] conditions = Array.Empty<EventCondition>();
        public string body = "";
        public EventOption[] options = Array.Empty<EventOption>();
    }

    [Serializable]
    public sealed class EventCondition
    {
        public string type = "";
        public int value;
    }

    [Serializable]
    public sealed class EventOption
    {
        public string label = "";
        public string stance = "";
        public string knowledge = "";
        public string tag = "";
        public EventOutcome[] outcomes = Array.Empty<EventOutcome>();
    }

    [Serializable]
    public sealed class EventOutcome
    {
        public int chance = 100;
        public string text = "";
        public string battleIntro = "";
        public string victoryText = "";
        public EventEffect[] effects = Array.Empty<EventEffect>();
    }

    [Serializable]
    public sealed class EventEffect
    {
        public string type = "";
        public int value;
        public string kind = "";
        public string enemyId = "";
        public string target = "";
    }
}
