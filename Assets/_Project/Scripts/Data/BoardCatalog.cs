using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "BoardCatalog", menuName = "TypingRoguelike/Board Catalog")]
    public sealed class BoardCatalog : ScriptableObject
    {
        public BoardPreset[] presets;

        public BoardPreset Find(string presetId)
        {
            if (presets == null || string.IsNullOrEmpty(presetId))
                return null;
            foreach (var preset in presets)
            {
                if (preset != null && preset.presetId == presetId)
                    return preset;
            }

            return null;
        }

        public static BoardCatalog LoadOrFallback()
        {
            var loaded = Resources.Load<BoardCatalog>("Run/BoardCatalog");
            return loaded != null ? loaded : RuntimeFallback();
        }

        public static BoardCatalog RuntimeFallback()
        {
            var catalog = CreateInstance<BoardCatalog>();
            catalog.presets = new[]
            {
                Make("balance", "バランス", "BBBRRSTEE"),
                Make("combat", "戦闘寄り", "BBBBBRTEE"),
                Make("rest", "回復寄り", "BBBRRRRSE"),
                Make("treasure", "宝寄り", "BBBTTTTXX")
            };
            return catalog;
        }

        static BoardPreset Make(string id, string name, string columns)
        {
            var preset = CreateInstance<BoardPreset>();
            preset.presetId = id;
            preset.displayName = name;
            preset.column1 = columns;
            preset.column2 = columns;
            preset.column3 = columns;
            return preset;
        }
    }
}
