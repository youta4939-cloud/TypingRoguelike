using System;
using System.Linq;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "MaterialCatalog", menuName = "TypingRoguelike/Material Catalog")]
    public sealed class MaterialCatalogDefinition : ScriptableObject
    {
        public MaterialDefinition[] materials;

        public MaterialDefinition FindById(int materialId)
        {
            if (materials == null || materialId < 0)
                return null;
            return materials.FirstOrDefault(m => m != null && m.materialId == materialId);
        }

        public MaterialDefinition PickForFloor(int floor, System.Random rng)
        {
            if (materials == null || materials.Length == 0)
                return null;

            floor = Mathf.Max(1, floor);
            var eligible = materials
                .Where(m => m != null && floor >= m.floorMin && floor <= m.floorMax)
                .ToList();
            if (eligible.Count == 0)
            {
                eligible = materials.Where(m => m != null).ToList();
                if (eligible.Count == 0)
                    return null;
            }

            var total = eligible.Sum(m => Mathf.Max(1, m.pickWeight));
            var roll = rng.Next(total);
            foreach (var m in eligible)
            {
                roll -= Mathf.Max(1, m.pickWeight);
                if (roll < 0)
                    return m;
            }

            return eligible[eligible.Count - 1];
        }
    }
}
