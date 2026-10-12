using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    /// <summary>RUN 内の素材・レリック（装備3＋バックパック）。スキルカードは含まない。</summary>
    public sealed class RunExplorationInventory
    {
        public const int RelicSlotCount = 3;
        public const int BaseBackpackRelics = 8;

        readonly int[] _equippedRelicIds = { -1, -1, -1 };
        readonly List<int> _backpackRelicIds = new();
        readonly MaterialBackpack _materials = new();

        public MaterialBackpack Materials => _materials;
        public int MaterialCount => _materials.TotalUnits();
        public IReadOnlyList<int> BackpackRelicIds => _backpackRelicIds;

        public int GetEquippedRelicId(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= RelicSlotCount)
                return -1;
            return _equippedRelicIds[slotIndex];
        }

        public void Clear()
        {
            _materials.Clear();
            _backpackRelicIds.Clear();
            for (var i = 0; i < RelicSlotCount; i++)
                _equippedRelicIds[i] = -1;
        }

        public MaterialBackpack.TryAddResult TryAddMaterial(int materialId, int amount = 1) =>
            _materials.TryAdd(materialId, amount);

        public enum AcquireRelicResult
        {
            Equipped,
            Backpack,
            BackpackFull
        }

        public int GetMaxBackpackSlots(RelicCatalogDefinition catalog) =>
            BaseBackpackRelics + RelicRunModifiers.Compute(this, catalog).BackpackSlotBonus;

        public AcquireRelicResult TryAcquireRelic(RelicDefinition relic, RelicCatalogDefinition catalog = null)
        {
            if (relic == null)
                return AcquireRelicResult.BackpackFull;

            for (var i = 0; i < RelicSlotCount; i++)
            {
                if (_equippedRelicIds[i] >= 0)
                    continue;
                _equippedRelicIds[i] = relic.relicId;
                return AcquireRelicResult.Equipped;
            }

            if (_backpackRelicIds.Count >= GetMaxBackpackSlots(catalog))
                return AcquireRelicResult.BackpackFull;

            _backpackRelicIds.Add(relic.relicId);
            return AcquireRelicResult.Backpack;
        }

        public int TotalDangerReduction(RelicCatalogDefinition catalog) =>
            RelicRunModifiers.Compute(this, catalog).DangerEncounterReductionPercent;

        public IEnumerable<int> EnumerateAllRelicIds()
        {
            for (var i = 0; i < RelicSlotCount; i++)
            {
                if (_equippedRelicIds[i] >= 0)
                    yield return _equippedRelicIds[i];
            }

            foreach (var id in _backpackRelicIds)
                yield return id;
        }

        public void WriteToSave(
            List<int> equippedOut,
            List<int> backpackOut,
            List<MaterialStackSave> materialStacksOut)
        {
            equippedOut ??= new List<int>();
            backpackOut ??= new List<int>();
            materialStacksOut ??= new List<MaterialStackSave>();
            equippedOut.Clear();
            backpackOut.Clear();
            for (var i = 0; i < RelicSlotCount; i++)
                equippedOut.Add(_equippedRelicIds[i]);
            backpackOut.AddRange(_backpackRelicIds);
            _materials.WriteTo(materialStacksOut);
        }

        public void RestoreFromSave(
            IReadOnlyList<int> equipped,
            IReadOnlyList<int> backpack,
            IReadOnlyList<MaterialStackSave> materialStacks)
        {
            Clear();
            _materials.ReadFrom(materialStacks);
            if (equipped != null)
            {
                for (var i = 0; i < RelicSlotCount && i < equipped.Count; i++)
                    _equippedRelicIds[i] = equipped[i];
            }

            if (backpack != null)
                _backpackRelicIds.AddRange(backpack);
        }

        /// <summary>旧セーブ互換（整数のみ）。</summary>
        public void RestoreLegacyMaterialCount(int legacyTotal, int fallbackMaterialId = 900)
        {
            if (legacyTotal <= 0)
                return;

            _materials.TryAdd(fallbackMaterialId, legacyTotal);
        }

        public bool TryEquipFromBackpack(int backpackIndex, int slotIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= _backpackRelicIds.Count)
                return false;
            if (slotIndex < 0 || slotIndex >= RelicSlotCount)
                return false;

            var id = _backpackRelicIds[backpackIndex];
            var displaced = _equippedRelicIds[slotIndex];
            _equippedRelicIds[slotIndex] = id;
            _backpackRelicIds.RemoveAt(backpackIndex);
            if (displaced >= 0)
                _backpackRelicIds.Add(displaced);
            return true;
        }

        public int EquippedRelicCount()
        {
            var n = 0;
            for (var i = 0; i < RelicSlotCount; i++)
            {
                if (_equippedRelicIds[i] >= 0)
                    n++;
            }

            return n;
        }

        public bool IsRelicEquipped(int relicId)
        {
            if (relicId < 0)
                return false;
            for (var i = 0; i < RelicSlotCount; i++)
            {
                if (_equippedRelicIds[i] == relicId)
                    return true;
            }

            return false;
        }

        public int FindEmptyRelicSlot()
        {
            for (var i = 0; i < RelicSlotCount; i++)
            {
                if (_equippedRelicIds[i] < 0)
                    return i;
            }

            return -1;
        }

        public void DiscardEquippedRelic(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= RelicSlotCount)
                return;
            _equippedRelicIds[slotIndex] = -1;
        }

        public void DiscardAllEquippedRelics()
        {
            for (var i = 0; i < RelicSlotCount; i++)
                _equippedRelicIds[i] = -1;
        }

        public void SetEquippedRelic(int slotIndex, int relicId)
        {
            if (slotIndex < 0 || slotIndex >= RelicSlotCount)
                return;
            _equippedRelicIds[slotIndex] = relicId;
        }

        public bool TryUnequipToBackpack(int slotIndex, RelicCatalogDefinition catalog = null)
        {
            if (slotIndex < 0 || slotIndex >= RelicSlotCount)
                return false;
            if (_equippedRelicIds[slotIndex] < 0)
                return false;
            if (_backpackRelicIds.Count >= GetMaxBackpackSlots(catalog))
                return false;

            _backpackRelicIds.Add(_equippedRelicIds[slotIndex]);
            _equippedRelicIds[slotIndex] = -1;
            return true;
        }
    }
}
