using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "OpinionLineTable", menuName = "TypingRoguelike/Opinion Line Table")]
    public sealed class OpinionLineTable : ScriptableObject
    {
        public OpinionLine[] lines = System.Array.Empty<OpinionLine>();

        public OpinionLine Find(string characterId, OpinionScene scene, System.Random rng)
        {
            if (lines == null || string.IsNullOrEmpty(characterId))
                return null;
            OpinionLine match = null;
            var count = 0;
            foreach (var line in lines)
            {
                if (line == null || line.characterId != characterId || line.scene != scene)
                    continue;
                count++;
                if (rng.Next(count) == 0)
                    match = line;
            }

            return match;
        }
    }
}
