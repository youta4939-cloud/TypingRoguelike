using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "Relic", menuName = "TypingRoguelike/Relic Definition")]
    public sealed class RelicDefinition : ScriptableObject
    {
        public int relicId;
        public string displayName = "レリック";
        [TextArea(2, 4)] public string effectText;

        [Header("効果（装備中のみ。同種は加算）")]
        [Tooltip("レリックバックパック上限の加算")]
        [Range(0, 12)] public int backpackSlotBonus;
        [Tooltip("戦闘の与ダメージ加算（%）")]
        [Range(0, 50)] public int playerDamageBonusPercent;

        [Header("拠点 — 店・開発（0 で不可）")]
        [Tooltip("魔石での購入価格")]
        public int hubShopPriceShards;
        [Tooltip("開発に必要な素材数")]
        public int developMaterialCost;

        [Header("塔内 — 狂気・開幕")]
        [Tooltip("装備中、狂気の上限に加算")]
        public int madnessMaxBonus;
        [Tooltip("この狂気以上でジャックポット補正。0 で無効")]
        public int jackpotMadnessThreshold;
        [Tooltip("閾値以上のとき、その語の当選率に加算（0.1 = +10%）")]
        public float jackpotChanceBonus;
        [Tooltip("閾値以上のとき、ジャックポット威力に掛ける。1 で変化なし")]
        public float jackpotDamageMultiplier = 1f;
        [Tooltip("装備中は他のレリックを持てない")]
        public bool exclusiveEquip;
        [Tooltip("戦闘開始時、敵の攻撃カウントを止める秒数")]
        public float battleStartEnemyStopSeconds;
    }
}
