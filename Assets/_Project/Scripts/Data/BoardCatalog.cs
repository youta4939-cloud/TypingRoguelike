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
                Make("balance", "バランス", "BBRSTE"),
                Make("combat", "戦闘寄り", "BBBBBX"),
                Make("rest", "回復寄り", "BBRRRS", "BBRBTS", "BBRBTS"),
                Make("treasure", "宝寄り", "TTBBRR", "TTXBBT", "TTXBBT")
            };
            return catalog;
        }

        static BoardPreset Make(string id, string name, string column1, string column2 = null, string column3 = null)
        {
            var preset = CreateInstance<BoardPreset>();
            preset.presetId = id;
            preset.displayName = name;
            preset.column1 = column1;
            preset.column2 = column2 ?? column1;
            preset.column3 = column3 ?? column1;
            return preset;
        }
    }
}
