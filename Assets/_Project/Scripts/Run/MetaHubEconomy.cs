using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public static class MetaHubEconomy
    {
        public static bool TryPurchaseRelic(MetaProgressData data, RelicDefinition relic)
        {
            if (data == null || relic == null || relic.hubShopPriceShards <= 0)
                return false;
            if (data.arcaneShards < relic.hubShopPriceShards)
                return false;

            data.arcaneShards -= relic.hubShopPriceShards;
            AddRelicToWarehouse(data, relic.relicId);
            return true;
        }

        public static bool TryDevelopRelic(MetaProgressData data, RelicDefinition relic)
        {
            if (data == null || relic == null || relic.developMaterialCost <= 0)
                return false;
            if (!MetaProgression.TrySpendWarehouseMaterials(data, relic.developMaterialCost))
                return false;

            AddRelicToWarehouse(data, relic.relicId);
            return true;
        }

        public static bool TryPurchaseMaterialBundle(
            MetaProgressData data,
            RelicCatalogDefinition catalog,
            MaterialCatalogDefinition materialCatalog,
            int sampleFloor = 5)
        {
            if (data == null || catalog == null)
                return false;
            if (catalog.materialBundleShardCost <= 0 || catalog.materialBundleAmount <= 0)
                return false;
            if (data.arcaneShards < catalog.materialBundleShardCost)
                return false;

            data.arcaneShards -= catalog.materialBundleShardCost;
            data.warehouseMaterialStacks ??= new List<MaterialStackSave>();
            var warehouse = new MaterialWarehouse();
            warehouse.ReadFrom(data.warehouseMaterialStacks);
            var mat = materialCatalog?.PickForFloor(sampleFloor, new System.Random())?.materialId ?? 900;
            warehouse.AddUnits(mat, catalog.materialBundleAmount);
            warehouse.WriteTo(data.warehouseMaterialStacks);
            data.warehouseMaterials = warehouse.TotalUnits();
            MetaProgression.Save(data);
            return true;
        }

        static void AddRelicToWarehouse(MetaProgressData data, int relicId)
        {
            if (data == null || relicId < 0)
                return;

            data.warehouseRelicIds ??= new List<int>();
            data.warehouseRelicIds.Add(relicId);
        }
    }
}
