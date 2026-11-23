using System;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "TypingRoguelike/Character Config")]
    public sealed class CharacterConfig : ScriptableObject
    {
        public string characterId = "chara01";
        public string displayName = "chara01";
        [Tooltip("顔画像。未設定のあいだは仮の色面になる。")]
        public Sprite portrait;
        public TrustPersonality personality = TrustPersonality.Brave;
        public TrustPassiveTier[] tiers =
        {
            new TrustPassiveTier { requiredTrust = 25 },
            new TrustPassiveTier { requiredTrust = 50 },
            new TrustPassiveTier { requiredTrust = 75 },
            new TrustPassiveTier { requiredTrust = 100 }
        };

        public string PersonalityName(TrustRules rules)
        {
            if (rules == null)
                return FallbackPersonalityName(personality);
            return personality switch
            {
                TrustPersonality.Brave => string.IsNullOrEmpty(rules.braveName) ? "勇敢" : rules.braveName,
                TrustPersonality.Timid => string.IsNullOrEmpty(rules.timidName) ? "臆病" : rules.timidName,
                _ => string.IsNullOrEmpty(rules.curiousName) ? "好奇心" : rules.curiousName
            };
        }

        public static string FallbackPersonalityName(TrustPersonality personality) => personality switch
        {
            TrustPersonality.Brave => "勇敢",
            TrustPersonality.Timid => "臆病",
            _ => "好奇心"
        };

        public TrustPassiveTier ActiveTier(int trust)
        {
            TrustPassiveTier best = null;
            if (tiers == null)
                return null;
            foreach (var tier in tiers)
            {
                if (tier == null || trust < tier.requiredTrust)
                    continue;
                if (best == null || tier.requiredTrust >= best.requiredTrust)
                    best = tier;
            }

            return best;
        }
    }
}
