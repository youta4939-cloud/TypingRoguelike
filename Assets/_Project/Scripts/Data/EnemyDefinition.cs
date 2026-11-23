using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "Enemy", menuName = "TypingRoguelike/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Tooltip("ファイル名・カタログ参照用")]
        public string enemyId;
        [TextArea(2, 6)] public string designerNotes;

        [Header("出現")]
        public EnemySpawnCategory spawnCategory = EnemySpawnCategory.Normal;
        public int floorMin = 1;
        public int floorMax = 99;
        [Tooltip("同階層・同種類の敵プール内での出現しやすさ（大きいほど選ばれやすい）。攻撃間隔ではない。")]
        [Min(1)] public int pickWeight = 10;

        public string displayName = "BOSS";
        [Tooltip("Auto=紙の天使があれば優先、なければ大口")]
        public EnemyBattleVisualKind battleVisual = EnemyBattleVisualKind.Auto;
        [Tooltip("戦闘画面のボス立ち絵（未設定時は Resources/Battle/Art/Boss_Dragon）")]
        public Sprite battleIllustration;
        public EnemyVisualRegistration visual = EnemyVisualRegistration.Default;
        public string emojiNormal = "🐲";
        public string emojiEnraged = "🔥🐲";
        public string emojiDefeated = "💀";

        public float maxHp = 1000f;
        public float attackDamage = 25f;
        public float attackTimerMax = 100f;
        public float attackSpeed = 0.35f;
        public float enragedAttackSpeed = 0.55f;
        [Range(0.05f, 1f)] public float enrageHpFraction = 0.5f;

        [Header("レガシー（攻撃3枠の初回待ちを使用）")]
        [HideInInspector] public float openingAttackDelay = 2.5f;

        [Header("小攻撃（間隔かダメージが 0 なら無し）")]
        public float lightAttackInterval;
        public float lightAttackDamage;

        [Header("強攻撃（間隔かダメージが 0 なら無し）")]
        public float heavyAttackInterval;
        public float heavyAttackDamage;
        [Tooltip("着弾の何秒前から予告するか")]
        public float heavyAttackTelegraphSeconds;
        [Tooltip("強攻撃の初回までの秒数。0 なら間隔を使う")]
        public float heavyAttackFirstSeconds;

        [Tooltip("狂暴化中、両方の攻撃間隔へかける。1回目の初撃・初回にはかけない")]
        public float enrageIntervalMultiplier = 0.75f;

        [Header("攻撃（最大3・ローテ）")]
        public EnemyAttackPickMode attackPickMode = EnemyAttackPickMode.Rotate;
        public EnemyAttackPattern[] attacks = {
            new(), new(), new()
        };

        [Header("ブレイク（順不同・復帰後は同じ条件）")]
        public bool breakEnabled = true;
        public TypingRoguelike.Battle.ElementColor[] breakRequirements = {
            TypingRoguelike.Battle.ElementColor.Red, TypingRoguelike.Battle.ElementColor.Red, TypingRoguelike.Battle.ElementColor.Red,
            TypingRoguelike.Battle.ElementColor.Blue, TypingRoguelike.Battle.ElementColor.Blue, TypingRoguelike.Battle.ElementColor.Blue,
            TypingRoguelike.Battle.ElementColor.Yellow, TypingRoguelike.Battle.ElementColor.Yellow, TypingRoguelike.Battle.ElementColor.Yellow
        };
        [Min(0.1f)] public float breakDuration = 4f;
        [Min(1f)] public float breakDamageMultiplier = 1.5f;
    }
}
