using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "DungeonRun", menuName = "TypingRoguelike/Dungeon Run Definition")]
    public sealed class DungeonRunDefinition : ScriptableObject
    {
        [Header("Encounters")]
        public BattleEncounterDefinition combatEncounter;
        public BattleEncounterDefinition eliteEncounter;
        public BattleEncounterDefinition bossEncounter;

        [Header("Rest / ラン内通貨（塔片）")]
        [Range(0.05f, 0.35f)] public float restHealFraction = 0.2f;
        [Tooltip("道中戦闘（通常・強敵）の敵 HP 倍率。ボスは別")]
        [Range(0.35f, 1.25f)] public float combatEnemyHpMultiplier = 0.72f;
        [Tooltip("全戦闘の敵HPに最後に掛ける。1にすると元に戻る（仮）。")]
        [Range(0.1f, 10f)] public float globalEnemyHpScale = 2f;

        [Header("パーティステ係数")]
        [Tooltip("単語与ダメ = 全員のATK合計 × この値。文字数では増えない。0.04 なら合計300で12。")]
        public float partyAttackCoefficient = 0.04f;
        [Tooltip("旧・減算用。被ダメは partyDefenseConstant を使う。")]
        public float partyDefenseCoefficient = 0.5f;
        [Tooltip("被ダメ = 攻撃 × この値 / (Σ防御 + この値)。初期防御400なら半分。")]
        public float partyDefenseConstant = 400f;
        [Header("スキルダメージ（会心）")]
        [Tooltip("仮。基礎会心率。0なら、外から足さない限り会心しない。")]
        public float baseCritChance = 0f;
        [Tooltip("仮。会心したときのダメージ倍率。")]
        public float critMultiplier = 1.5f;
        [Tooltip("未使用。ジャックポットの伸びは運合計 / 120。")]
        public float partyLuckJackpotBonusPerPoint = 0f;
        public int combatCoinReward = 8;
        public int eliteCoinReward = 15;
        public int treasureCoinReward = 10;
        [Tooltip("宝箱で塔片に加えて刻印選択を出す")]
        public bool treasureOffersCard = true;
        [Header("宝箱 — 素材・レリック")]
        public int treasureMaterialMin = 1;
        public int treasureMaterialMax = 2;
        [Range(0f, 1f)] public float treasureRelicChance = 0f;
        [Header("行商人")]
        public int merchantHealCost = 10;
        [Range(0.05f, 0.25f)] public float merchantHealFraction = 0.12f;
        [Tooltip("商人マスのうち、高額レリックを出す割合")]
        [Range(0f, 1f)] public float merchantRelicOfferChance = 0.5f;
        public int merchantRelicCost = 60;

        [Header("刻印イベント（カード報酬）")]
        public SkillCardPoolDefinition sharedRewardPool;
        public int rewardChoiceCount = 3;

        [Header("Elite scaling (combat テンプレに乗算)")]
        public float eliteEnemyHpMultiplier = 1.55f;
        public float eliteEnemyDamageMultiplier = 1.2f;
    }
}
