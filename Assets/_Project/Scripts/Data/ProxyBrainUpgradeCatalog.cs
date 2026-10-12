using System;
using UnityEngine;

namespace TypingRoguelike.Data
{
    public enum ProxyBrainUpgradeTrackKind
    {
        MadnessMaxBonus = 0,
        PartyMaxHpPercent = 1,
        RestMadnessRecovery = 2
    }

    [Serializable]
    public sealed class ProxyBrainUpgradeTier
    {
        [Tooltip("狂気上限なら加算値。最大HPなら元HPに対する加算%。休憩狂気回復なら加算値。")]
        public float effectValue;
        public int cost;
        [TextArea(2, 4)] public string repairShopLine;
    }

    [Serializable]
    public sealed class ProxyBrainUpgradeTrack
    {
        public string displayName = "強化";
        public ProxyBrainUpgradeTrackKind kind;
        [Tooltip("段階1→2→3の順に購入")]
        public ProxyBrainUpgradeTier[] tiers = new ProxyBrainUpgradeTier[3];
    }

    [CreateAssetMenu(fileName = "ProxyBrainUpgradeCatalog", menuName = "TypingRoguelike/Proxy Brain Upgrade Catalog")]
    public sealed class ProxyBrainUpgradeCatalog : ScriptableObject
    {
        [Header("休憩マス — 狂気回復（永続強化は tiers に加算）")]
        public int restMadnessRecoveryBase;

        public ProxyBrainUpgradeTrack madnessMaxTrack;
        public ProxyBrainUpgradeTrack partyHpTrack;
        public ProxyBrainUpgradeTrack restMadnessTrack;

        public ProxyBrainUpgradeTrack GetTrack(ProxyBrainUpgradeTrackKind kind) => kind switch
        {
            ProxyBrainUpgradeTrackKind.MadnessMaxBonus => madnessMaxTrack,
            ProxyBrainUpgradeTrackKind.PartyMaxHpPercent => partyHpTrack,
            ProxyBrainUpgradeTrackKind.RestMadnessRecovery => restMadnessTrack,
            _ => null
        };

        public ProxyBrainUpgradeTrack GetTrack(int trackIndex) =>
            trackIndex switch
            {
                0 => madnessMaxTrack,
                1 => partyHpTrack,
                2 => restMadnessTrack,
                _ => null
            };
    }
}
