using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public static class MetaRelicLoadout
    {
        public const int SlotCount = 3;

        public static int GetHubSlot(MetaProgressData data, int slotIndex)
        {
            if (data == null || slotIndex < 0 || slotIndex >= SlotCount)
                return -1;
            return slotIndex switch
            {
                0 => data.hubRelicSlot0,
                1 => data.hubRelicSlot1,
                2 => data.hubRelicSlot2,
                _ => -1
            };
        }

        public static void SetHubSlot(MetaProgressData data, int slotIndex, int relicId)
        {
            if (data == null || slotIndex < 0 || slotIndex >= SlotCount)
                return;

            switch (slotIndex)
            {
                case 0: data.hubRelicSlot0 = relicId; break;
                case 1: data.hubRelicSlot1 = relicId; break;
                case 2: data.hubRelicSlot2 = relicId; break;
            }
        }

        public static void ReturnHubSlotToWarehouse(MetaProgressData data, int slotIndex)
        {
            if (data == null)
                return;

            var id = GetHubSlot(data, slotIndex);
            if (id < 0)
                return;

            data.warehouseRelicIds ??= new List<int>();
            data.warehouseRelicIds.Add(id);
            SetHubSlot(data, slotIndex, -1);
        }

        public static bool AssignHubSlotFromWarehouse(MetaProgressData data, int slotIndex, int relicId)
        {
            if (data == null || relicId < 0)
                return false;

            data.warehouseRelicIds ??= new List<int>();
            var index = data.warehouseRelicIds.IndexOf(relicId);
            if (index < 0)
                return false;

            ReturnHubSlotToWarehouse(data, slotIndex);
            data.warehouseRelicIds.RemoveAt(index);
            SetHubSlot(data, slotIndex, relicId);
            return true;
        }

        // TODO 未決事項3: 探索開始時には呼んでいない。拠点の装備を塔へ持ち込むかは未決なので、呼ばないままにする。
        public static void ApplyHubLoadoutToRun(
            MetaProgressData data,
            RunExplorationInventory loot,
            RelicCatalogDefinition catalog)
        {
            if (data == null || loot == null)
                return;

            loot.Clear();
            for (var i = 0; i < SlotCount; i++)
            {
                var id = GetHubSlot(data, i);
                if (id < 0)
                    continue;
                if (catalog?.FindById(id) == null)
                    continue;
                loot.SetEquippedRelic(i, id);
            }
        }

        public static void ClearHubLoadout(MetaProgressData data)
        {
            if (data == null)
                return;

            for (var i = 0; i < SlotCount; i++)
                ReturnHubSlotToWarehouse(data, i);
        }
    }
}
