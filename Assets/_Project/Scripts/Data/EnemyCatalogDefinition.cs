using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "TypingRoguelike/Enemy Catalog")]
    public sealed class EnemyCatalogDefinition : ScriptableObject
    {
        public EnemyDefinition[] enemies;

        public EnemyDefinition PickForFloor(int floor, EnemySpawnCategory category, System.Random rng)
        {
            if (enemies == null || enemies.Length == 0)
                return null;

            floor = Mathf.Max(1, floor);
            var eligible = enemies
                .Where(e => e != null &&
                            floor >= e.floorMin && floor <= e.floorMax &&
                            e.spawnCategory == category)
                .ToList();
            if (eligible.Count == 0)
            {
                eligible = enemies.Where(e => e != null && e.spawnCategory == category).ToList();
                if (eligible.Count == 0)
                    eligible = enemies.Where(e => e != null).ToList();
                if (eligible.Count == 0)
                    return null;
            }

            var total = eligible.Sum(e => Mathf.Max(1, e.pickWeight));
            var roll = rng.Next(total);
            foreach (var e in eligible)
            {
                roll -= Mathf.Max(1, e.pickWeight);
                if (roll < 0)
                    return e;
            }

            return eligible[eligible.Count - 1];
        }

        public void RebuildFromProjectEnemies(IEnumerable<EnemyDefinition> source)
        {
            enemies = source?
                .Where(e => e != null)
                .OrderBy(e => e.floorMin)
                .ThenBy(e => e.displayName)
                .ToArray() ?? System.Array.Empty<EnemyDefinition>();
        }

        public static EnemyCatalogDefinition LoadOrFallback()
        {
            var loaded = Resources.Load<EnemyCatalogDefinition>("Battle/EnemyCatalog");
            return loaded;
        }
    }
}