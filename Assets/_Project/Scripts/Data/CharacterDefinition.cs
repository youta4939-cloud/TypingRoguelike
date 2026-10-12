using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "Character", menuName = "TypingRoguelike/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public string characterName = "アリス";
        [Tooltip("UI用英字表記（例: ALICE）。空なら自動推定。")]
        public string englishName;
        public string roleLabel = "アタッカー";
        public Sprite battlePortrait;
        public SkillDefinition innateSkill;

        [Header("パーティ合算ステ（RUN中固定）")]
        public int attack = 5;
        public int defense = 4;
        public int luck = 3;

        [Header("探索パッシブ（編成内で同種は1人のみ有効）")]
        public ExplorationPassiveKind explorationPassive = ExplorationPassiveKind.None;
        [Tooltip("Cautious 時: Danger 遭遇率から引く%")]
        public int explorationPassiveValue;
    }
}
