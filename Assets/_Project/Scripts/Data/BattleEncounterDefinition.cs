using UnityEngine;
using UnityEngine.Serialization;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "BattleEncounter", menuName = "TypingRoguelike/Battle Encounter")]
    public sealed class BattleEncounterDefinition : ScriptableObject
    {
        public EnemyDefinition enemy;
        public WordListDefinition wordList;
        public PartyLoadoutDefinition party;

        [Header("Player / Rules")]
        public float playerMaxHp = 100f;
        public float wordClearDamage = 15f;
        public int rerollsPerBattle = 3;
        public float enemyTickInterval = 0.05f;

        [Header("Typing SFX")]
        public TypingAudioDefinition typingAudio;

        [Header("Jackpot / 狂気")]
        [Tooltip("JackpotDamage = 現在狂気 × この倍率")]
        public float jackpotMultiplier = 2f;
        [Tooltip("戦闘開始時・JACKPOT後の初期当選率（0 = 蓄積のみ）")]
        [Range(0f, 1f)] public float baseFreezeChance = 0f;
        [Tooltip("Jackpot外れ時、完了した単語1文字あたり加算（0.000025 = 0.0025%）")]
        [FormerlySerializedAs("freezeChancePerWord")]
        [Range(0f, 1f)] public float freezeChancePerCharacter = 0.000025f;
        [Tooltip("通常攻撃だけで蓄積できる当選率の上限")]
        [Range(0f, 1f)] public float baseJackpotChanceCap = 0.1f;
        [Tooltip("スキル等込みの最終当選率上限")]
        [Range(0f, 1f)] public float maxFreezeChance = 0.99f;

        [TextArea] public string startLogMessage = "🔥 ボス戦開始！敵の強撃に警戒せよ！";

        /// <summary>旧プロトタイプ資産（0.06% 系）を新Jackpot仕様へ読み替え。</summary>
        public void ResolveJackpotRollParameters(
            out float baseChance,
            out float chancePerCharacter,
            out float maxFinalChance,
            out float accumulationCap)
        {
            baseChance = baseFreezeChance;
            chancePerCharacter = freezeChancePerCharacter;
            maxFinalChance = maxFreezeChance;
            accumulationCap = baseJackpotChanceCap;

            baseChance = Mathf.Max(0f, baseChance);

            // 旧レート（1語+1% / 0.0125%・字）を現行 0.0025%・字 へ
            if (chancePerCharacter >= 0.0001f || chancePerCharacter <= 0f)
                chancePerCharacter = 0.000025f;

            if (maxFinalChance <= 0.05f)
                maxFinalChance = 0.99f;
            if (accumulationCap <= 0f)
                accumulationCap = 0.1f;
        }

        [Header("Rewards (Phase 3)")]
        public SkillCardPoolDefinition rewardCardPool;
        public int rewardChoiceCount = 3;
    }
}
