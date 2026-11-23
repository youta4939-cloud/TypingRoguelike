using System;

namespace TypingRoguelike.Data
{
    [Serializable]
    public sealed class DemeritFile
    {
        public int version = 1;
        public DemeritEvent[] events = Array.Empty<DemeritEvent>();
    }

    [Serializable]
    public sealed class DemeritEvent
    {
        public string id = "";
        public string text = "";
        public EventEffect[] effects = Array.Empty<EventEffect>();
    }
}
