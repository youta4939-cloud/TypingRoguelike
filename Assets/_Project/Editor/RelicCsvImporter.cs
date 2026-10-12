#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class RelicCsvImporter
    {
        public const string DefaultCsvPath = "Assets/_Project/Docs/relics.csv";
        const string RelicRoot = "Assets/_Project/Data/Prototype/Relics";
        const string CatalogPath = "Assets/_Project/Resources/Battle/PrototypeRelicCatalog.asset";

        [MenuItem("Tools/TypingRoguelike/Import Relics from CSV")]
        public static void ImportFromMenu()
        {
            Import(DefaultCsvPath);
            EditorUtility.DisplayDialog("TypingRoguelike", "relics.csv からレリックを反映しました。", "OK");
        }

        public static RelicDefinition[] Import(string csvAssetPath = DefaultCsvPath)
        {
            var rows = Parse(File.ReadAllText(GetFullPath(csvAssetPath)));
            PrototypeDataGenerator.EnsureFolderPublic(RelicRoot);
            var assets = new List<RelicDefinition>();
            foreach (var row in rows)
            {
                var safeName = PrototypeDataGenerator.SafeFileNamePublic(row.DisplayName);
                var path = $"{RelicRoot}/Relic_{row.RelicId:D2}_{safeName}.asset";
                var relic = PrototypeDataGenerator.LoadOrCreatePublic<RelicDefinition>(path);
                row.ApplyTo(relic);
                EditorUtility.SetDirty(relic);
                assets.Add(relic);
            }

            var catalog = PrototypeDataGenerator.LoadOrCreatePublic<RelicCatalogDefinition>(CatalogPath);
            catalog.materialBundleShardCost = 40;
            catalog.materialBundleAmount = 3;
            catalog.relics = assets.OrderBy(r => r.relicId).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return assets.ToArray();
        }

        public static List<RelicCsvRow> Parse(string csvText)
        {
            var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return new List<RelicCsvRow>();

            var header = SplitCsvLine(lines[0]);
            var rows = new List<RelicCsvRow>();
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].TrimStart().StartsWith("#"))
                    continue;
                var cols = SplitCsvLine(lines[i]);
                if (cols.Count == 0)
                    continue;
                rows.Add(RelicCsvRow.From(header, cols));
            }

            return rows;
        }

        static string GetFullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = string.Empty;
            var inQuotes = false;
            foreach (var ch in line)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(current.Trim());
                    current = string.Empty;
                    continue;
                }

                current += ch;
            }

            result.Add(current.Trim());
            return result;
        }

        public sealed class RelicCsvRow
        {
            public int RelicId;
            public string DisplayName;
            public string EffectText;
            public int BackpackSlotBonus;
            public int PlayerDamageBonusPercent;
            public int DangerEncounterReductionPercent;
            public int HubShopPriceShards;
            public int DevelopMaterialCost;
            public int MadnessMaxBonus;
            public int JackpotMadnessThreshold;
            public float JackpotChanceBonus;
            public float JackpotDamageMultiplier = 1f;
            public bool RevealsAllNodes;
            public bool ExclusiveEquip;
            public float BattleStartEnemyStopSeconds;

            public static RelicCsvRow From(IReadOnlyList<string> header, IReadOnlyList<string> cols)
            {
                var row = new RelicCsvRow();
                for (var i = 0; i < header.Count && i < cols.Count; i++)
                {
                    var key = header[i].Trim();
                    var val = cols[i].Trim();
                    switch (key)
                    {
                        case "relicId": row.RelicId = ParseInt(val); break;
                        case "displayName": row.DisplayName = val; break;
                        case "effectText": row.EffectText = val; break;
                        case "backpackSlotBonus": row.BackpackSlotBonus = ParseInt(val); break;
                        case "playerDamageBonusPercent": row.PlayerDamageBonusPercent = ParseInt(val); break;
                        case "dangerEncounterReductionPercent": row.DangerEncounterReductionPercent = ParseInt(val); break;
                        case "hubShopPriceShards": row.HubShopPriceShards = ParseInt(val); break;
                        case "developMaterialCost": row.DevelopMaterialCost = ParseInt(val); break;
                        case "madnessMaxBonus": row.MadnessMaxBonus = ParseInt(val); break;
                        case "jackpotMadnessThreshold": row.JackpotMadnessThreshold = ParseInt(val); break;
                        case "jackpotChanceBonus": row.JackpotChanceBonus = ParseFloat(val); break;
                        case "jackpotDamageMultiplier": row.JackpotDamageMultiplier = ParseFloat(val); break;
                        case "revealsAllNodes": row.RevealsAllNodes = ParseBool(val); break;
                        case "exclusiveEquip": row.ExclusiveEquip = ParseBool(val); break;
                        case "battleStartEnemyStopSeconds": row.BattleStartEnemyStopSeconds = ParseFloat(val); break;
                    }
                }

                return row;
            }

            public void ApplyTo(RelicDefinition relic)
            {
                relic.relicId = RelicId;
                relic.displayName = DisplayName;
                relic.effectText = EffectText;
                relic.backpackSlotBonus = BackpackSlotBonus;
                relic.playerDamageBonusPercent = PlayerDamageBonusPercent;
                relic.dangerEncounterReductionPercent = DangerEncounterReductionPercent;
                relic.hubShopPriceShards = HubShopPriceShards;
                relic.developMaterialCost = DevelopMaterialCost;
                relic.madnessMaxBonus = MadnessMaxBonus;
                relic.jackpotMadnessThreshold = JackpotMadnessThreshold;
                relic.jackpotChanceBonus = JackpotChanceBonus;
                relic.jackpotDamageMultiplier = JackpotDamageMultiplier <= 0f ? 1f : JackpotDamageMultiplier;
                relic.revealsAllNodes = RevealsAllNodes;
                relic.exclusiveEquip = ExclusiveEquip;
                relic.battleStartEnemyStopSeconds = BattleStartEnemyStopSeconds;
            }

            static int ParseInt(string s) => int.TryParse(s, out var n) ? n : 0;
            static float ParseFloat(string s) => float.TryParse(s, out var n) ? n : 0f;
            static bool ParseBool(string s) => s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
