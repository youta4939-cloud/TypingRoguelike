namespace TypingRoguelike.Battle
{
    public struct EncounterBattleModifiers
    {
        public float EnemyHpMultiplier;
        public float EnemyDamageMultiplier;
        public float EnemySpeedMultiplier;
        /// <summary>0 なら 1.0 扱い。</summary>
        public float PlayerDamageMultiplier;
        public string StartLogMessage;
        /// <summary>0 ならジャックポットのレリック補正なし。</summary>
        public int JackpotMadnessThreshold;
        public float JackpotChanceBonus;
        /// <summary>0 以下は 1 扱い。</summary>
        public float JackpotDamageMultiplier;
        /// <summary>戦闘開始後、敵の攻撃カウントを止める秒数。</summary>
        public float OpeningEnemyStopSeconds;

        public static EncounterBattleModifiers None => default;

        public bool HasLogOverride => !string.IsNullOrEmpty(StartLogMessage);
    }
}
