using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    [Serializable]
    public sealed class MysteryNodeSave
    {
        public int nodeId;
        public int resolvedType;
        public int dangerPercent;
    }

    [Serializable]
    public sealed class MetaProgressData
    {
        public int version = 3;
        public int arcaneShards;
        // TODO 未決事項1: 貯金する通貨は塔片のまま。名前と、魔石へ変換して一本にするかは未決。
        public int bankedCoins;
        public int completedRuns;
        public int extractedRuns;
        public int failedRuns;
        public int totalRuns;
        public int bestClearedNodes;
        public int lastRunReward;
        public int lastUnlockedSkillId = -1;
        public List<int> unlockedSkillIds = new();
        public int towerSeed;
        public List<int> revealedTowerNodeIds = new();
        public List<int> clearedTowerNodeIds = new();
        public List<int> unlockedElevatorFloors = new();

        public bool towerRunActive;
        public int towerRunSeed;
        public int towerRunFloor = 1;
        public int towerRunNodeId;
        public int towerRunCoins;
        public float towerRunCurrentHp;
        public float towerRunMaxHp;
        public int towerRunMadnessCurrent;
        public int towerRunMadnessMax = RunMadnessState.DefaultMaxMadness;
        public float towerRunBonusJackpotChance;
        public float towerRunJackpotAccumulatedChance;
        public int towerRunClearedNodes;
        public List<TowerRunMemberSave> towerRunMembers = new();

        public int warehouseMaterials;
        public List<MaterialStackSave> warehouseMaterialStacks = new();
        public List<int> warehouseRelicIds = new();
        public int hubRelicSlot0 = -1;
        public int hubRelicSlot1 = -1;
        public int hubRelicSlot2 = -1;

        public List<int> towerRunEquippedRelicIds = new();
        public List<int> towerRunBackpackRelicIds = new();
        public int towerRunMaterials;
        public List<MaterialStackSave> towerRunMaterialStacks = new();
        public List<string> towerRunSeenEventIds = new();
        public List<string> previousRunEventIds = new();
        public List<int> towerRunSkipFromNodeIds = new();
        public bool towerRunEventBattlePending;
        public string towerRunLastDangerLine;
        public List<MysteryNodeSave> towerRunMysteryNodes = new();

        public bool IsUnlocked(int skillId) => unlockedSkillIds.Contains(skillId);
    }

    public static class MetaProgression
    {
        const string SaveFileName = "typing_roguelike_profile.json";
        static readonly int[] StarterSkillIds = { 1, 4, 7, 9, 14, 18 };

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static MetaProgressData Load()
        {
            MetaProgressData data = null;
            try
            {
                if (File.Exists(SavePath))
                    data = JsonUtility.FromJson<MetaProgressData>(File.ReadAllText(SavePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"プロフィールの読み込みに失敗しました。新規データで開始します: {ex.Message}");
            }

            data ??= new MetaProgressData();
            Normalize(data);
            return data;
        }

        public static void Save(MetaProgressData data)
        {
            if (data == null)
                return;

            Normalize(data);
            try
            {
                var directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var tempPath = SavePath + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
                File.Copy(tempPath, SavePath, true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"プロフィールの保存に失敗しました: {ex.Message}");
            }
        }

        public static int RegisterRunResult(
            MetaProgressData data,
            bool cleared,
            int clearedNodes,
            bool extracted = false)
        {
            if (data == null)
                return 0;

            clearedNodes = Mathf.Max(0, clearedNodes);
            // 死亡・放棄・撤退では魔石を出さない。extracted を true で呼ぶ箇所は残っていない。
            // TODO 未決事項2: 踏破時の魔石（120 + 踏破マス×15）を残すかは未決。式は現状維持。
            var reward = cleared ? 120 + clearedNodes * 15 : 0;
            data.arcaneShards += reward;
            data.totalRuns++;
            if (cleared)
                data.completedRuns++;
            else if (extracted)
                data.extractedRuns++;
            else
                data.failedRuns++;
            data.bestClearedNodes = Mathf.Max(data.bestClearedNodes, clearedNodes);
            data.lastRunReward = reward;
            Save(data);
            return reward;
        }

        /// <summary>探索中の塔片を拠点の貯金へ移す。パーティ側の所持は呼び出し元で 0 にする。</summary>
        public static void BankRunCoins(MetaProgressData data, RunPartyState party)
        {
            if (data == null || party == null)
                return;

            // TODO 未決事項1: bankedCoins は塔片の貯金。魔石（arcaneShards）へは移さない。
            data.bankedCoins = Mathf.Max(0, data.bankedCoins) + Mathf.Max(0, party.RunCoins);
            Save(data);
        }

        [System.Obsolete("v1.2: スキルカードはメタ倉庫に保存しない。")]
        public static int AddSkillsToArchive(MetaProgressData data, IEnumerable<SkillDefinition> skills)
        {
            if (data == null || skills == null)
                return 0;

            var added = 0;
            foreach (var skill in skills.Where(skill => skill != null).Distinct())
            {
                if (data.IsUnlocked(skill.skillId))
                    continue;
                data.unlockedSkillIds.Add(skill.skillId);
                data.lastUnlockedSkillId = skill.skillId;
                added++;
            }

            if (added > 0)
                Save(data);
            return added;
        }

        public static void UnlockElevatorFloor(MetaProgressData data, int floor)
        {
            if (data == null || floor < 1)
                return;

            data.unlockedElevatorFloors ??= new List<int>();
            if (data.unlockedElevatorFloors.Contains(floor))
                return;
            data.unlockedElevatorFloors.Add(floor);
            data.unlockedElevatorFloors = data.unlockedElevatorFloors.OrderBy(f => f).ToList();
            Save(data);
        }

        [System.Obsolete("帰還ではレリックと素材を倉庫へ移さない。未決事項3・5が決まるまでメソッドは残す。")]
        public static void CommitRunLootToMeta(MetaProgressData data, RunExplorationInventory loot)
        {
            if (data == null)
                return;

            data.warehouseRelicIds ??= new List<int>();
            data.warehouseMaterialStacks ??= new List<MaterialStackSave>();
            if (loot != null)
            {
                var warehouse = new MaterialWarehouse();
                warehouse.ReadFrom(data.warehouseMaterialStacks);
                warehouse.MergeRunBackpack(loot.Materials);
                warehouse.WriteTo(data.warehouseMaterialStacks);
                data.warehouseMaterials = warehouse.TotalUnits();

                foreach (var relicId in loot.EnumerateAllRelicIds())
                    data.warehouseRelicIds.Add(relicId);
                loot.Clear();
            }

            Save(data);
        }

        public static int WarehouseMaterialUnits(MetaProgressData data)
        {
            if (data == null)
                return 0;
            if (data.warehouseMaterialStacks != null && data.warehouseMaterialStacks.Count > 0)
            {
                var wh = new MaterialWarehouse();
                wh.ReadFrom(data.warehouseMaterialStacks);
                return wh.TotalUnits();
            }

            return Mathf.Max(0, data.warehouseMaterials);
        }

        public static bool TrySpendWarehouseMaterials(MetaProgressData data, int amount)
        {
            if (data == null || amount <= 0)
                return amount <= 0;

            data.warehouseMaterialStacks ??= new List<MaterialStackSave>();
            var wh = new MaterialWarehouse();
            wh.ReadFrom(data.warehouseMaterialStacks);
            if (data.warehouseMaterials > 0 && wh.TotalUnits() == 0)
                wh.AddUnits(900, data.warehouseMaterials);

            if (!wh.TrySpendTotal(amount))
                return false;

            wh.WriteTo(data.warehouseMaterialStacks);
            data.warehouseMaterials = wh.TotalUnits();
            Save(data);
            return true;
        }

        public static void SaveActiveTowerRun(
            MetaProgressData data,
            int runSeed,
            int floor,
            int currentNodeId,
            RunPartyState party,
            RunExplorationInventory loot,
            int clearedNodesThisRun,
            RunMadnessState madness = null)
        {
            if (data == null || party == null)
                return;

            data.towerRunActive = true;
            data.towerRunSeed = runSeed;
            data.towerRunFloor = Mathf.Max(1, floor);
            data.towerRunNodeId = currentNodeId;
            data.towerRunCoins = party.RunCoins;
            data.towerRunCurrentHp = party.CurrentHp;
            data.towerRunMaxHp = party.MaxHp;
            data.towerRunClearedNodes = Mathf.Max(0, clearedNodesThisRun);
            data.towerRunMembers ??= new List<TowerRunMemberSave>();
            party.WriteMemberSaves(data.towerRunMembers);
            data.towerRunEquippedRelicIds ??= new List<int>();
            data.towerRunBackpackRelicIds ??= new List<int>();
            data.towerRunMaterialStacks ??= new List<MaterialStackSave>();
            loot?.WriteToSave(data.towerRunEquippedRelicIds, data.towerRunBackpackRelicIds, data.towerRunMaterialStacks);
            data.towerRunMaterials = loot?.MaterialCount ?? 0;
            if (madness != null)
            {
                data.towerRunMadnessCurrent = madness.CurrentMadness;
                data.towerRunMadnessMax = madness.MaxMadness;
                data.towerRunBonusJackpotChance = madness.BonusJackpotChance;
                data.towerRunJackpotAccumulatedChance = madness.AccumulatedJackpotChance;
            }

            Save(data);
        }

        /// <summary>死亡時: 中断 RUN 破棄・踏破リセット・新 RunSeed（恒久エレベーター階は維持）。</summary>
        public static void ApplyDeathReset(MetaProgressData data)
        {
            if (data == null)
                return;

            ClearActiveTowerRun(data);
            data.revealedTowerNodeIds ??= new List<int>();
            data.revealedTowerNodeIds.Clear();
            data.clearedTowerNodeIds ??= new List<int>();
            data.clearedTowerNodeIds.Clear();
            RollTowerSeed(data);
            Save(data);
        }

        static void RollTowerSeed(MetaProgressData data)
        {
            var next = unchecked(data.towerSeed * 1103515245 + 12345);
            if (next == 0)
                next = 731927;
            data.towerSeed = next;
        }

        public static void ClearActiveTowerRun(MetaProgressData data)
        {
            if (data == null)
                return;

            data.towerRunActive = false;
            data.towerRunSeed = 0;
            data.towerRunFloor = 1;
            data.towerRunNodeId = 0;
            data.towerRunCoins = 0;
            data.towerRunCurrentHp = 0;
            data.towerRunMaxHp = 0;
            data.towerRunMadnessCurrent = 0;
            data.towerRunMadnessMax = RunMadnessState.DefaultMaxMadness;
            data.towerRunBonusJackpotChance = 0f;
            data.towerRunJackpotAccumulatedChance = 0f;
            data.towerRunClearedNodes = 0;
            data.towerRunMembers ??= new List<TowerRunMemberSave>();
            data.towerRunMembers.Clear();
            data.towerRunMaterialStacks ??= new List<MaterialStackSave>();
            data.towerRunMaterialStacks.Clear();
            data.towerRunMaterials = 0;
            data.towerRunEquippedRelicIds ??= new List<int>();
            data.towerRunEquippedRelicIds.Clear();
            data.towerRunBackpackRelicIds ??= new List<int>();
            data.towerRunBackpackRelicIds.Clear();
            data.towerRunSeenEventIds ??= new List<string>();
            data.towerRunSeenEventIds.Clear();
            data.towerRunSkipFromNodeIds ??= new List<int>();
            data.towerRunSkipFromNodeIds.Clear();
            data.towerRunEventBattlePending = false;
            data.towerRunLastDangerLine = null;
            data.towerRunMysteryNodes ??= new List<MysteryNodeSave>();
            data.towerRunMysteryNodes.Clear();
            Save(data);
        }

        public static void RevealTowerNode(MetaProgressData data, int nodeId)
        {
            if (data == null || nodeId < 0)
                return;

            data.revealedTowerNodeIds ??= new List<int>();
            if (data.revealedTowerNodeIds.Contains(nodeId))
                return;
            data.revealedTowerNodeIds.Add(nodeId);
            Save(data);
        }

        public static void MarkTowerNodeCleared(MetaProgressData data, int nodeId)
        {
            if (data == null || nodeId < 0)
                return;

            data.clearedTowerNodeIds ??= new List<int>();
            if (data.clearedTowerNodeIds.Contains(nodeId))
                return;
            data.clearedTowerNodeIds.Add(nodeId);
            Save(data);
        }

        public static int CountUnlockableSkills(MetaProgressData data, SkillCardPoolDefinition pool)
        {
            if (pool?.cards == null)
                return 0;
            return pool.cards.Count(card => card != null && (data == null || !data.IsUnlocked(card.skillId)));
        }

        static void Normalize(MetaProgressData data)
        {
            data.version = Mathf.Max(1, data.version);
            data.arcaneShards = Mathf.Max(0, data.arcaneShards);
            data.bankedCoins = Mathf.Max(0, data.bankedCoins);
            data.extractedRuns = Mathf.Max(0, data.extractedRuns);
            data.unlockedSkillIds ??= new List<int>();
            data.revealedTowerNodeIds ??= new List<int>();
            data.clearedTowerNodeIds ??= new List<int>();
            data.unlockedElevatorFloors ??= new List<int>();
            data.towerRunMembers ??= new List<TowerRunMemberSave>();
            data.warehouseRelicIds ??= new List<int>();
            data.towerRunEquippedRelicIds ??= new List<int>();
            data.towerRunBackpackRelicIds ??= new List<int>();
            data.warehouseMaterialStacks ??= new List<MaterialStackSave>();
            data.towerRunMaterialStacks ??= new List<MaterialStackSave>();
            data.towerRunSeenEventIds ??= new List<string>();
            data.previousRunEventIds ??= new List<string>();
            data.towerRunSkipFromNodeIds ??= new List<int>();
            data.towerRunMysteryNodes ??= new List<MysteryNodeSave>();

            if (data.version < 2)
            {
                if (data.warehouseMaterials > 0 && data.warehouseMaterialStacks.Count == 0)
                    data.warehouseMaterialStacks.Add(new MaterialStackSave { materialId = 900, count = data.warehouseMaterials });
                if (data.towerRunMaterials > 0 && data.towerRunMaterialStacks.Count == 0)
                    data.towerRunMaterialStacks.Add(new MaterialStackSave { materialId = 900, count = data.towerRunMaterials });
                data.version = 2;
            }

            if (data.version < 3)
            {
                // TODO 未決事項4: 旧セーブの魔石と倉庫レリックは変換せず残す。bankedCoins は 0 から始める。
                // 中断中の towerRunCoins はそのまま（再開時の所持塔片）。
                data.version = 3;
            }

            var wh = new MaterialWarehouse();
            wh.ReadFrom(data.warehouseMaterialStacks);
            data.warehouseMaterials = wh.TotalUnits() > 0 ? wh.TotalUnits() : Mathf.Max(0, data.warehouseMaterials);
            if (data.towerSeed == 0)
                data.towerSeed = 731927;
            foreach (var skillId in StarterSkillIds)
            {
                if (!data.unlockedSkillIds.Contains(skillId))
                    data.unlockedSkillIds.Add(skillId);
            }

            data.unlockedSkillIds = data.unlockedSkillIds
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            data.revealedTowerNodeIds = data.revealedTowerNodeIds
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            data.clearedTowerNodeIds = data.clearedTowerNodeIds
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            data.unlockedElevatorFloors = data.unlockedElevatorFloors
                .Where(f => f >= 1)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
        }
    }
}
