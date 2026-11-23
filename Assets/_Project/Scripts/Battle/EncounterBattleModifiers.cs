namespace TypingRoguelike.Battle
{
    public enum ExEnemyTier
    {
        Normal = 0,
        Elite = 1,
        Boss = 2
    }

    public struct EncounterBattleModifiers
    {
        public TypingRoguelike.Data.EnemyDefinition EnemyOverride;
        public float EnemyHpMultiplier;
        public float EnemyDamageMultiplier;
        public float EnemySpeedMultiplier;
        /// <summary>0 なら 1.0 扱い。</summary>
        public float PlayerDamageMultiplier;
        /// <summary>0 なら 1.0 扱い。単語ダメージだけに掛かる。</summary>
        public float WordDamageMultiplier;
        /// <summary>戦闘開始時のリロール回数に足す。負なら減る。</summary>
        public int RerollBonus;
        public string StartLogMessage;
        /// <summary>0 ならジャックポットのレリック補正なし。</summary>
        public int JackpotMadnessThreshold;
        public float JackpotChanceBonus;
        /// <summary>0 以下は 1 扱い。</summary>
        public float JackpotDamageMultiplier;
        /// <summary>戦闘開始後、敵の攻撃カウントを止める秒数。</summary>
        public float OpeningEnemyStopSeconds;
        /// <summary>最初の攻撃までの時間を足す。負ならその分早くなる。</summary>
        public float OpeningAttackTimeDeltaSeconds;
        /// <summary>初撃までの秒数を足す。0 は未使用。補正したあとは 1 秒未満にしない。</summary>
        public float FirstAttackDelayDeltaSeconds;
        /// <summary>0 なら 1。2回目以降の攻撃間隔にかける。</summary>
        public float EnemyAttackIntervalMultiplier;
        /// <summary>0 なら 1.0 扱い。信頼度パッシブの被ダメージ軽減。</summary>
        public float DamageTakenMultiplier;
        public bool NegateMissHit;
        public bool ShowEnemyAttackTime;
        public ExEnemyTier EnemyTier;

        public static EncounterBattleModifiers None => default;

        public bool HasLogOverride => !string.IsNullOrEmpty(StartLogMessage);
    }
}
