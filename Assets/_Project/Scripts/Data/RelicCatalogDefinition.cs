using System.Linq;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "RelicCatalog", menuName = "TypingRoguelike/Relic Catalog")]
    public sealed class RelicCatalogDefinition : ScriptableObject
    {
        [Header("拠点店 — 素材パック")]
        public int materialBundleShardCost = 40;
        public int materialBundleAmount = 3;

        public RelicDefinition[] relics;

        public RelicDefinition FindById(int relicId)
        {
            if (relics == null || relicId < 0)
                return null;

            return relics.FirstOrDefault(r => r != null && r.relicId == relicId);
        }

        public RelicDefinition PickRandom(System.Random rng)
        {
            if (relics == null || relics.Length == 0)
                return null;

            var eligible = relics.Where(r => r != null).ToList();
            if (eligible.Count == 0)
                return null;

            return eligible[rng.Next(eligible.Count)];
        }
    }
}
