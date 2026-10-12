using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "Enemy", menuName = "TypingRoguelike/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string displayName = "BOSS";
        [Tooltip("戦闘画面のボス立ち絵（未設定時は Resources/Battle/Art/Boss_Dragon）")]
        public Sprite battleIllustration;
        public string emojiNormal = "🐲";
        public string emojiEnraged = "🔥🐲";
        public string emojiDefeated = "💀";

        public float maxHp = 1000f;
        public float attackDamage = 25f;
        public float attackTimerMax = 100f;
        public float attackSpeed = 0.35f;
        public float enragedAttackSpeed = 0.55f;
        [Range(0.05f, 1f)] public float enrageHpFraction = 0.5f;
    }
}
