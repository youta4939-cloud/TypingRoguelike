using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "TrustRules", menuName = "TypingRoguelike/Trust Rules")]
    public sealed class TrustRules : ScriptableObject
    {
        [Header("性格の表示名")]
        public string braveName = "勇敢";
        public string timidName = "臆病";
        public string curiousName = "好奇心";

        [Header("攻めを推す基本確率")]
        [Range(0f, 1f)] public float braveAggressiveChance = 0.75f;
        [Range(0f, 1f)] public float timidAggressiveChance = 0.25f;
        [Range(0f, 1f)] public float curiousUnknownChance = 0.7f;
        [Range(0f, 1f)] public float curiousEvenChance = 0.5f;
        [Range(0f, 1f)] public float chanceMin = 0.15f;
        [Range(0f, 1f)] public float chanceMax = 0.85f;

        [Header("ゆらぎ")]
        [Range(0f, 0.5f)] public float driftStep = 0.03f;
        [Range(0f, 0.5f)] public float driftLimit = 0.15f;

        [Header("信頼度")]
        public int agreeTrust = 5;
        public int followTrust = 15;
        public int ignoreTrust = -5;
        public int ignoreTrustAtHigh = -10;
        public int highTrustThreshold = 75;
        public bool preferLastAbsent;

        [Tooltip("パッシブの攻撃上昇を、編成全体でこの割合までに抑える")]
        [Range(0f, 1f)] public float attackBonusCap = 0.2f;

        public TrustLayerMultiplier[] layerMultipliers =
        {
            new TrustLayerMultiplier { fromLayer = 1, toLayer = 99, multiplier = 1f }
        };

        public float MultiplierFor(int layer)
        {
            if (layerMultipliers == null)
                return 1f;
            foreach (var band in layerMultipliers)
            {
                if (band == null)
                    continue;
                if (layer >= band.fromLayer && layer <= band.toLayer)
                    return Mathf.Max(0f, band.multiplier);
            }

            return 1f;
        }

        public int ScaleGain(int amount, int layer)
        {
            if (amount <= 0)
                return amount;
            return Mathf.RoundToInt(amount * MultiplierFor(layer));
        }
    }
}
