using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "WordList", menuName = "TypingRoguelike/Word List")]
    public sealed class WordListDefinition : ScriptableObject
    {
        public string[] words =
        {
            "cat", "dog", "run", "sun", "sky"
        };
    }
}
