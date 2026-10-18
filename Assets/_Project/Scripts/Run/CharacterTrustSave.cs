using System;

namespace TypingRoguelike.Run
{
    [Serializable]
    public sealed class CharacterTrustSave
    {
        public string characterId;
        public int trust;
        public float aggressiveDrift;
        public int spokeCount;
        public int followedCount;
    }
}
