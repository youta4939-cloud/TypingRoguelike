using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public static class ProxyBrainUpgrades
    {
        public const int MaxTier = 3;

        public static int GetPurchasedTier(MetaProgressData data, ProxyBrainUpgradeTrackKind kind)
        {
            if (data == null)
                return 0;

            return kind switch
            {
                ProxyBrainUpgradeTrackKind.MadnessMaxBonus =>
                    Mathf.Clamp(data.proxyBrainMadnessMaxTier, 0, MaxTier),
                ProxyBrainUpgradeTrackKind.PartyMaxHpPercent =>
                    Mathf.Clamp(data.proxyBrainPartyHpTier, 0, MaxTier),
                ProxyBrainUpgradeTrackKind.RestMadnessRecovery =>
                    Mathf.Clamp(data.proxyBrainRestMadnessTier, 0, MaxTier),
                _ => 0
            };
        }

        static void SetPurchasedTier(MetaProgressData data, ProxyBrainUpgradeTrackKind kind, int tier)
        {
            tier = Mathf.Clamp(tier, 0, MaxTier);
            switch (kind)
            {
                case ProxyBrainUpgradeTrackKind.MadnessMaxBonus:
                    data.proxyBrainMadnessMaxTier = tier;
                    break;
                case ProxyBrainUpgradeTrackKind.PartyMaxHpPercent:
                    data.proxyBrainPartyHpTier = tier;
                    break;
                case ProxyBrainUpgradeTrackKind.RestMadnessRecovery:
                    data.proxyBrainRestMadnessTier = tier;
                    break;
            }
        }

        public static float SumPurchasedEffects(MetaProgressData data, ProxyBrainUpgradeCatalog catalog,
            ProxyBrainUpgradeTrackKind kind)
        {
            if (data == null || catalog == null)
                return 0f;

            var track = catalog.GetTrack(kind);
            if (track?.tiers == null)
                return 0f;

            var purchased = GetPurchasedTier(data, kind);
            var sum = 0f;
            for (var i = 0; i < purchased && i < track.tiers.Length; i++)
            {
                var tier = track.tiers[i];
                if (tier != null)
                    sum += tier.effectValue;
            }

            return sum;
        }

        public static int ComputeMadnessBaseBonus(MetaProgressData data, ProxyBrainUpgradeCatalog catalog) =>
            Mathf.RoundToInt(SumPurchasedEffects(data, catalog, ProxyBrainUpgradeTrackKind.MadnessMaxBonus));

        public static float ComputePartyMaxHpPercentBonus(MetaProgressData data, ProxyBrainUpgradeCatalog catalog) =>
            SumPurchasedEffects(data, catalog, ProxyBrainUpgradeTrackKind.PartyMaxHpPercent);

        public static int ComputeRestMadnessRecovery(MetaProgressData data, ProxyBrainUpgradeCatalog catalog)
        {
            var baseAmount = catalog != null ? Mathf.Max(0, catalog.restMadnessRecoveryBase) : 0;
            var bonus = SumPurchasedEffects(data, catalog, ProxyBrainUpgradeTrackKind.RestMadnessRecovery);
            return baseAmount + Mathf.RoundToInt(bonus);
        }

        public static bool TryGetNextTier(
            MetaProgressData data,
            ProxyBrainUpgradeCatalog catalog,
            ProxyBrainUpgradeTrackKind kind,
            out ProxyBrainUpgradeTier tierDef,
            out int tierIndex)
        {
            tierDef = null;
            tierIndex = -1;
            if (data == null || catalog == null)
                return false;

            var purchased = GetPurchasedTier(data, kind);
            if (purchased >= MaxTier)
                return false;

            var track = catalog.GetTrack(kind);
            if (track?.tiers == null || purchased >= track.tiers.Length)
                return false;

            tierDef = track.tiers[purchased];
            tierIndex = purchased;
            return tierDef != null && tierDef.cost > 0;
        }

        public enum PurchaseBlockReason
        {
            None,
            Invalid,
            Maxed,
            InsufficientFunds
        }

        public static bool CanPurchase(
            MetaProgressData data,
            ProxyBrainUpgradeCatalog catalog,
            ProxyBrainUpgradeTrackKind kind,
            out PurchaseBlockReason blockReason,
            out ProxyBrainUpgradeTier nextTier)
        {
            blockReason = PurchaseBlockReason.None;
            nextTier = null;
            if (data == null || catalog == null)
            {
                blockReason = PurchaseBlockReason.Invalid;
                return false;
            }

            if (!TryGetNextTier(data, catalog, kind, out nextTier, out _))
            {
                blockReason = GetPurchasedTier(data, kind) >= MaxTier
                    ? PurchaseBlockReason.Maxed
                    : PurchaseBlockReason.Invalid;
                return false;
            }

            if (data.bankedCoins < nextTier.cost)
            {
                blockReason = PurchaseBlockReason.InsufficientFunds;
                return false;
            }

            return true;
        }

        public static bool TryPurchase(
            MetaProgressData data,
            ProxyBrainUpgradeCatalog catalog,
            ProxyBrainUpgradeTrackKind kind,
            out string repairShopLine,
            out PurchaseBlockReason blockReason)
        {
            repairShopLine = null;
            if (!CanPurchase(data, catalog, kind, out blockReason, out var tier))
                return false;

            data.bankedCoins -= tier.cost;
            var nextTier = GetPurchasedTier(data, kind) + 1;
            SetPurchasedTier(data, kind, nextTier);
            repairShopLine = tier.repairShopLine;
            MetaProgression.Save(data);
            return true;
        }

        public static string FormatEffectPreview(ProxyBrainUpgradeTrackKind kind, ProxyBrainUpgradeTier tier)
        {
            if (tier == null)
                return string.Empty;

            return kind switch
            {
                ProxyBrainUpgradeTrackKind.MadnessMaxBonus => $"基本上限 +{(int)tier.effectValue}",
                ProxyBrainUpgradeTrackKind.PartyMaxHpPercent => $"最大HP +{tier.effectValue:0.#}%（元HP基準）",
                ProxyBrainUpgradeTrackKind.RestMadnessRecovery => $"休憩の狂気回復 +{(int)tier.effectValue}",
                _ => string.Empty
            };
        }

        public static string FormatTrackProgress(MetaProgressData data, ProxyBrainUpgradeTrackKind kind)
        {
            var purchased = GetPurchasedTier(data, kind);
            return purchased >= MaxTier ? "最大段階" : $"段階 {purchased} / {MaxTier}";
        }
    }
}
