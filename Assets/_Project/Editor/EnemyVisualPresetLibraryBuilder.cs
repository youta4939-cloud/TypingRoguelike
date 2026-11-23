#if UNITY_EDITOR
using System.IO;
using TypingRoguelike.Battle.Visual;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class EnemyVisualPresetLibraryBuilder
    {
        const string AssetPath = "Assets/_Project/Resources/Battle/EnemyVisualPresetLibrary.asset";

        [MenuItem("Tools/TypingRoguelike/Create Enemy Visual Preset Library")]
        public static void CreateOrRefresh()
        {
            PrototypeDataGenerator.EnsureFolderPublic("Assets/_Project/Resources/Battle");
            var lib = AssetDatabase.LoadAssetAtPath<EnemyVisualPresetLibrary>(AssetPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<EnemyVisualPresetLibrary>();
                AssetDatabase.CreateAsset(lib, AssetPath);
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Selection.activeObject = lib;
            EditorUtility.DisplayDialog("TypingRoguelike", $"作成・更新しました:\n{AssetPath}", "OK");
        }
    }
}
#endif