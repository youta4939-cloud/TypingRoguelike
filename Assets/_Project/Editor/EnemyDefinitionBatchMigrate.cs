#if UNITY_EDITOR
using TypingRoguelike.Data;
using UnityEditor;

namespace TypingRoguelike.EditorTools
{
    public static class EnemyDefinitionBatchMigrate
    {
        [MenuItem("Tools/TypingRoguelike/Migrate All Enemy Attack Slots")]
        public static void MigrateAttackSlots()
        {
            var count = 0;
            foreach (var e in EnemyCatalogSync.LoadAllEnemies())
            {
                if (e == null)
                    continue;
                if (!EnemyDefinitionAttackUtil.TryMigrateLegacyIfNeeded(e))
                    continue;
                EditorUtility.SetDirty(e);
                count++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("TypingRoguelike", $"攻撃3枠へ取り込み: {count} 件", "OK");
        }
    }
}
#endif