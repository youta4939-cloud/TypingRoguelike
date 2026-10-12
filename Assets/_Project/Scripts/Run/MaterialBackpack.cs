using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    /// <summary>RUN 内素材バックパック: 最大6種類、各最大3個。</summary>
    public sealed class MaterialBackpack
    {
        public const int MaxKinds = 6;
        public const int MaxPerKind = 3;

        readonly List<MaterialStackSave> _stacks = new();

        public IReadOnlyList<MaterialStackSave> Stacks => _stacks;

        public int TotalUnits() => _stacks.Sum(s => Math.Max(0, s.count));

        public int DistinctKindCount => _stacks.Count(s => s.materialId >= 0 && s.count > 0);

        public void Clear() => _stacks.Clear();

        public enum TryAddResult
        {
            Added,
            NeedDiscard,
            Invalid
        }

        public TryAddResult TryAdd(int materialId, int amount = 1)
        {
            if (materialId < 0 || amount <= 0)
                return TryAddResult.Invalid;

            var existing = _stacks.FirstOrDefault(s => s.materialId == materialId);
            if (existing != null)
            {
                if (existing.count >= MaxPerKind)
                    return TryAddResult.Invalid;
                existing.count = Math.Min(MaxPerKind, existing.count + amount);
                return TryAddResult.Added;
            }

            if (_stacks.Count >= MaxKinds)
                return TryAddResult.NeedDiscard;

            _stacks.Add(new MaterialStackSave { materialId = materialId, count = Math.Min(MaxPerKind, amount) });
            return TryAddResult.Added;
        }

        /// <summary>満杯時: 指定スロットを空けてから1個追加。</summary>
        public bool ReplaceSlotAndAdd(int slotIndex, int newMaterialId, int amount = 1)
        {
            if (newMaterialId < 0 || amount <= 0 || slotIndex < 0 || slotIndex >= _stacks.Count)
                return false;

            _stacks.RemoveAt(slotIndex);
            var result = TryAdd(newMaterialId, amount);
            return result == TryAddResult.Added;
        }

        public void WriteTo(List<MaterialStackSave> target)
        {
            target ??= new List<MaterialStackSave>();
            target.Clear();
            foreach (var s in _stacks.Where(s => s.materialId >= 0 && s.count > 0))
                target.Add(new MaterialStackSave { materialId = s.materialId, count = s.count });
        }

        public void ReadFrom(IReadOnlyList<MaterialStackSave> source)
        {
            Clear();
            if (source == null)
                return;

            foreach (var s in source)
            {
                if (s == null || s.materialId < 0 || s.count <= 0)
                    continue;
                _stacks.Add(new MaterialStackSave
                {
                    materialId = s.materialId,
                    count = Math.Min(MaxPerKind, s.count)
                });
            }

            while (_stacks.Count > MaxKinds)
                _stacks.RemoveAt(_stacks.Count - 1);
        }

        public string FormatSummary(MaterialCatalogDefinition catalog)
        {
            if (_stacks.Count == 0)
                return "素材なし";

            var parts = new List<string>();
            foreach (var s in _stacks)
            {
                var name = catalog?.FindById(s.materialId)?.displayName ?? $"#{s.materialId}";
                parts.Add($"{name}×{s.count}");
            }

            return string.Join("　", parts);
        }
    }

    /// <summary>拠点素材倉庫（種類・個数上限なし）。</summary>
    public sealed class MaterialWarehouse
    {
        readonly List<MaterialStackSave> _stacks = new();

        public IReadOnlyList<MaterialStackSave> Stacks => _stacks;

        public int TotalUnits() => _stacks.Sum(s => Math.Max(0, s.count));

        public void Clear() => _stacks.Clear();

        public void AddUnits(int materialId, int amount)
        {
            if (materialId < 0 || amount <= 0)
                return;

            var existing = _stacks.FirstOrDefault(s => s.materialId == materialId);
            if (existing != null)
                existing.count += amount;
            else
                _stacks.Add(new MaterialStackSave { materialId = materialId, count = amount });
        }

        public bool TrySpendTotal(int amount)
        {
            if (amount <= 0)
                return true;
            if (TotalUnits() < amount)
                return false;

            var left = amount;
            for (var i = 0; i < _stacks.Count && left > 0; i++)
            {
                var take = Math.Min(_stacks[i].count, left);
                _stacks[i].count -= take;
                left -= take;
            }

            _stacks.RemoveAll(s => s.count <= 0);
            return left <= 0;
        }

        public void WriteTo(List<MaterialStackSave> target)
        {
            target ??= new List<MaterialStackSave>();
            target.Clear();
            foreach (var s in _stacks.Where(s => s.materialId >= 0 && s.count > 0))
                target.Add(new MaterialStackSave { materialId = s.materialId, count = s.count });
        }

        public void ReadFrom(IReadOnlyList<MaterialStackSave> source)
        {
            Clear();
            if (source == null)
                return;

            foreach (var s in source)
            {
                if (s == null || s.materialId < 0 || s.count <= 0)
                    continue;
                AddUnits(s.materialId, s.count);
            }
        }

        public void MergeRunBackpack(MaterialBackpack run)
        {
            if (run == null)
                return;

            foreach (var s in run.Stacks)
                AddUnits(s.materialId, s.count);
        }
    }
}
