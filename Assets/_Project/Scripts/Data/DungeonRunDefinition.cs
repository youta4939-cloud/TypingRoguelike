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
        [Range(0.05f, 0.35f)] public float restHealFraction = 0.15f;
        [Tooltip("道中戦闘（通常・強敵）の敵 HP 倍率。ボスは別")]
        [Range(0.35f, 1.25f)] public float combatEnemyHpMultiplier = 0.72f;

        [Header("パーティステ係数")]
        [Tooltip("単語与ダメ = Σ攻撃 × この値")]
        public float partyAttackCoefficient = 1f;
        [Tooltip("被ダメ軽減 = Σ防御 × この値（敵攻撃から減算）")]
        public float partyDefenseCoefficient = 0.5f;
        [Tooltip("JACKPOT 1語ごとの加算 = Σ運 × この値（0.0005 → 運10で+0.5%）")]
        public float partyLuckJackpotBonusPerPoint = 0.0005f;
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
