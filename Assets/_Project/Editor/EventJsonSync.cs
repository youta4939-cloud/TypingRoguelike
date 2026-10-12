using System.IO;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    /// <summary>Docs/events_layer1.json を Resources へコピーする。</summary>
    [InitializeOnLoad]
    public static class EventJsonSync
    {
        const string AssetPath = "Assets/_Project/Resources/Events/events_layer1.json";

        static EventJsonSync() => Sync();

        public static void Sync()
        {
            var project = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(project))
                return;

            var source = Path.Combine(project, "Docs", "events_layer1.json");
            if (!File.Exists(source))
                return;

            var dest = Path.Combine(project, AssetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? project);
            if (File.Exists(dest) && File.ReadAllText(source) == File.ReadAllText(dest))
                return;

            File.Copy(source, dest, true);
            AssetDatabase.ImportAsset(AssetPath);
            Debug.Log("イベント JSON を Resources/Events へコピーした。");
        }
    }
}
