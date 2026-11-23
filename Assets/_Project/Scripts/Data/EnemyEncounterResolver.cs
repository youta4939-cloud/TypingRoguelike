using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TypingRoguelike.Data
{
  public static class EnemyEncounterResolver
  {
    public static EnemyDefinition PickEnemy(
        BattleEncounterDefinition enc,
        EnemyCatalogDefinition catalog,
        int floor,
        EnemySpawnCategory category,
        System.Random rng = null)
    {
      rng ??= new System.Random();
      floor = Mathf.Max(1, floor);

      if (catalog != null)
      {
        var fromCatalog = catalog.PickForFloor(floor, category, rng);
        if (fromCatalog != null)
          return fromCatalog;
      }

      return PickFromEncounterPool(enc, floor, category, rng);
    }

    public static EnemyDefinition PickFromEncounterPool(
        BattleEncounterDefinition enc,
        int floor,
        EnemySpawnCategory category,
        System.Random rng)
    {
      if (enc == null)
        return null;

      var candidates = CollectCandidates(enc.enemyPool, floor, category);
      if (candidates.Count == 0 && enc.enemy != null && IsEligible(enc.enemy, floor, category))
        return enc.enemy;
      if (candidates.Count == 0)
        candidates = CollectCandidates(enc.enemyPool, floor, category, ignoreCategory: true);
      if (candidates.Count == 0)
        return enc.enemy;

      return PickWeighted(candidates, rng);
    }

    static List<EnemyDefinition> CollectCandidates(
        EnemyDefinition[] pool,
        int floor,
        EnemySpawnCategory category,
        bool ignoreCategory = false)
    {
      var list = new List<EnemyDefinition>();
      if (pool == null)
        return list;
      foreach (var e in pool)
      {
        if (e == null)
          continue;
        if (!ignoreCategory && e.spawnCategory != category)
          continue;
        if (floor < e.floorMin || floor > e.floorMax)
          continue;
        list.Add(e);
      }

      return list;
    }

    static bool IsEligible(EnemyDefinition e, int floor, EnemySpawnCategory category) =>
        e != null &&
        e.spawnCategory == category &&
        floor >= e.floorMin &&
        floor <= e.floorMax;

    static EnemyDefinition PickWeighted(IReadOnlyList<EnemyDefinition> candidates, System.Random rng)
    {
      var total = 0;
      for (var i = 0; i < candidates.Count; i++)
        total += Mathf.Max(1, candidates[i].pickWeight);
      if (total <= 0)
        return candidates[0];

      var roll = rng.Next(total);
      for (var i = 0; i < candidates.Count; i++)
      {
        roll -= Mathf.Max(1, candidates[i].pickWeight);
        if (roll < 0)
          return candidates[i];
      }

      return candidates[candidates.Count - 1];
    }
  }
}