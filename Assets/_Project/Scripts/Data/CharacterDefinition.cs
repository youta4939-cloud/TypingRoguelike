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

        [Header("ステータス（RUN中固定）")]
        [Tooltip("共有HPへの分担。3人の合計がランの最大HP。")]
        public int hp = 250;
        [Tooltip("ATK。単語ダメ＝全員のATK合計×係数、スキルダメ＝撃ったキャラのATK×%")]
        public int attack = 5;
        public int defense = 4;
        public int luck = 3;
    }
}
