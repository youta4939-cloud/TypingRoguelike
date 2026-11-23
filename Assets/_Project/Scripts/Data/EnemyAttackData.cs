using System;
using UnityEngine;

namespace TypingRoguelike.Data
{
    public enum EnemyAttackEffectKind
    {
        None = 0,
        Telegraph = 1,
        PlayerAttackSlow = 2
    }

    public enum EnemyAttackPickMode
    {
        Rotate = 0,
        Weighted = 1
    }

    public enum EnemyAttackVisualIntensity
    {
        Inherit = 0,
        Small = 1,
        Medium = 2,
        Large = 3
    }

    public enum EnemySpawnCategory
    {
        Normal = 0,
        Elite = 1,
        Boss = 2
    }

    [Serializable]
    public struct EnemyAttackPattern
    {
        public float damage;
        public float intervalSeconds;
        public EnemyAttackEffectKind effect;
        [Min(0f)] public float effectValue;
        [Tooltip("0 なら intervalSeconds を初回待ちに使う")]
        public float firstDelaySeconds;
        public EnemyAttackVisualIntensity visualIntensity;

        public bool IsActive => damage > 0f && intervalSeconds > 0f;

        public float TelegraphSeconds =>
            effect == EnemyAttackEffectKind.Telegraph ? Mathf.Max(0f, effectValue) : 0f;
    }
}