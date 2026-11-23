#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class EnemyCatalogSync
    {
        public const string CatalogPath = "Assets/_Project/Resources/Battle/EnemyCatalog.asset";
        const string CombatEncounterPath = "Assets/_Project/Resources/Battle/PrototypeCombatEncounter.asset";
        const string EliteEncounterPath = "Assets/_Project/Resources/Battle/PrototypeEliteEncounter.asset";
        const string BossEncounterPath = "Assets/_Project/Resources/Battle/PrototypeBossEncounter.asset";

        public static EnemyCatalogDefinition SyncFromAllEnemies()
        {
            var enemies = LoadAllEnemies();
            ApplyDefaultSpawnCategories(enemies);
            var catalog = PrototypeDataGenerator.LoadOrCreatePublic<EnemyCatalogDefinition>(CatalogPath);
            catalog.RebuildFromProjectEnemies(enemies);
            EditorUtility.SetDirty(catalog);
            SyncEncounterPool(CombatEncounterPath, EnemySpawnCategory.Normal, enemies);
            SyncEncounterPool(EliteEncounterPath, EnemySpawnCategory.Elite, enemies);
            SyncEncounterPool(BossEncounterPath, EnemySpawnCategory.Boss, enemies);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static void SyncEncounterPool(string encounterPath, EnemySpawnCategory category, List<EnemyDefinition> all)
        {
            var enc = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>(encounterPath);
            if (enc == null)
                return;

            var pool = all.Where(e => e != null && e.spawnCategory == category).ToArray();
            if (pool.Length == 0)
                return;

            enc.enemyPool = pool;
            enc.enemy = pool[0];
            EditorUtility.SetDirty(enc);
        }

        public static List<EnemyDefinition> LoadAllEnemies()
        {
            var list = new List<EnemyDefinition>();
            var guids = AssetDatabase.FindAssets("t:EnemyDefinition");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var e = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                if (e != null)
                    list.Add(e);
            }

            return list.OrderBy(e => e.floorMin).ThenBy(e => e.displayName).ToList();
        }

        /// <summary>ファイル名に Boss / Elite が含まれるプロトタイプの種類を直す。</summary>
        static void ApplyDefaultSpawnCategories(List<EnemyDefinition> enemies)
        {
            foreach (var e in enemies)
            {
                if (e == null)
                    continue;
                var n = e.name ?? string.Empty;
                if (n.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    e.spawnCategory = EnemySpawnCategory.Boss;
                    EditorUtility.SetDirty(e);
                }
                else if (n.IndexOf("Elite", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    e.spawnCategory = EnemySpawnCategory.Elite;
                    EditorUtility.SetDirty(e);
                }
                else if (e.displayName == "強敵" && e.spawnCategory == EnemySpawnCategory.Normal)
                {
                    e.spawnCategory = EnemySpawnCategory.Elite;
                    EditorUtility.SetDirty(e);
                }
            }
        }
    }
}
#endif