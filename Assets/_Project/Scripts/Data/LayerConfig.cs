using System;
using System.Collections.Generic;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [System.Serializable]
    public sealed class LayerTypeMinimum
    {
        public int fromLayer = 1;
        public int toLayer = 99;
        public DungeonNodeType type = DungeonNodeType.Combat;
        public int minimumPerColumn = 3;
    }

    [CreateAssetMenu(fileName = "LayerConfig", menuName = "TypingRoguelike/Layer Config")]
    public sealed class LayerConfig : ScriptableObject
    {
        [Tooltip("塔の総層数")]
        [Range(1, 99)] public int totalLayers = 20;

        [Tooltip("この層数ごとに、4マス目をエレベーターにする。ボスの層と重なったときはボスを優先する")]
        [Range(1, 30)] public int elevatorEvery = 3;

        [Tooltip("この層数ごとに、4マス目をボスにする")]
        [Range(1, 30)] public int bossEvery = 10;

        [Tooltip("罠マスで最大HPから減らす割合。0.1 で 10%")]
        [Range(0.01f, 0.9f)] public float trapHpLossFraction = 0.1f;

        [Tooltip("層の範囲ごとに、各列へ最低いくつ置くか。満たさない盤面は準備画面で選べない。")]
        public LayerTypeMinimum[] columnMinimums =
        {
            new LayerTypeMinimum { fromLayer = 1, toLayer = 99, type = DungeonNodeType.Combat, minimumPerColumn = 2 }
        };

        public List<LayerTypeMinimum> MinimumsFor(int layer)
        {
            var list = new List<LayerTypeMinimum>();
            var source = ReelBoardSettings.Minimums ?? columnMinimums;
            if (source == null)
                return list;
            foreach (var rule in source)
            {
                if (rule == null || rule.minimumPerColumn <= 0)
                    continue;
                if (layer < rule.fromLayer || layer > rule.toLayer)
                    continue;
                list.Add(rule);
            }

            return list;
        }

        public static LayerConfig LoadOrFallback()
        {
            var loaded = Resources.Load<LayerConfig>("Run/LayerConfig");
            var config = loaded != null ? loaded : RuntimeFallback();
            ReelBoardSettings.Apply();
            return config;
        }

        public static LayerConfig RuntimeFallback()
        {
            var config = CreateInstance<LayerConfig>();
            config.totalLayers = 20;
            config.elevatorEvery = 3;
            config.bossEvery = 10;
            config.trapHpLossFraction = 0.1f;
            config.columnMinimums = new[]
            {
                new LayerTypeMinimum { fromLayer = 1, toLayer = 99, type = DungeonNodeType.Combat, minimumPerColumn = 2 }
            };
            return config;
        }
    }

    /// <summary>Data/ReelBoard.json。行数、各列の最低マス数、4マス目のおすすめ文言。</summary>
    public static class ReelBoardSettings
    {
        public static LayerTypeMinimum[] Minimums;
        static readonly List<AdviceLine> Advice = new();

        public static void Apply()
        {
            Advice.Clear();
            Advice.Add(new AdviceLine { Type = DungeonNodeType.MidBoss, Text = "おすすめ：回復を厚く" });
            Advice.Add(new AdviceLine { Type = DungeonNodeType.Boss, Text = "おすすめ：回復を厚く" });
            Advice.Add(new AdviceLine { Type = DungeonNodeType.Elevator, Text = "おすすめ：宝を厚く" });
            Minimums = null;
            BoardGrid.Rows = 6;

            var asset = Resources.Load<TextAsset>("Data/ReelBoard");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return;
            try
            {
                var file = JsonUtility.FromJson<ReelBoardFile>(asset.text);
                if (file == null)
                    return;
                if (file.rows > 0)
                    BoardGrid.Rows = file.rows;
                if (file.minimums != null && file.minimums.Length > 0)
                    Minimums = ReadMinimums(file.minimums);
                if (file.advice != null && file.advice.Length > 0)
                {
                    Advice.Clear();
                    foreach (var line in file.advice)
                    {
                        if (line == null || string.IsNullOrEmpty(line.text))
                            continue;
                        Advice.Add(new AdviceLine { Type = ParseSquare(line.square), Text = line.text });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"リール盤面の設定を読めませんでした: {ex.Message}");
            }
        }

        public static string AdviceFor(DungeonNodeType type)
        {
            if (Advice.Count == 0)
                Apply();
            foreach (var line in Advice)
            {
                if (line.Type == type)
                    return line.Text ?? "";
            }

            return "";
        }

        static LayerTypeMinimum[] ReadMinimums(ReelMinRecord[] records)
        {
            var list = new List<LayerTypeMinimum>();
            foreach (var record in records)
            {
                if (record == null)
                    continue;
                var symbol = string.IsNullOrEmpty(record.type) ? 'B' : record.type[0];
                list.Add(new LayerTypeMinimum
                {
                    fromLayer = record.fromLayer > 0 ? record.fromLayer : 1,
                    toLayer = record.toLayer > 0 ? record.toLayer : 99,
                    type = BoardGrid.ParseSymbol(symbol),
                    minimumPerColumn = record.minimumPerColumn
                });
            }

            return list.ToArray();
        }

        static DungeonNodeType ParseSquare(string name)
        {
            if (string.IsNullOrEmpty(name))
                return DungeonNodeType.MidBoss;
            switch (name.Trim())
            {
                case "Boss":
                case "ボス":
                    return DungeonNodeType.Boss;
                case "Elevator":
                case "エレベーター":
                    return DungeonNodeType.Elevator;
                default:
                    return DungeonNodeType.MidBoss;
            }
        }

        sealed class AdviceLine
        {
            public DungeonNodeType Type;
            public string Text;
        }

#pragma warning disable CS0649
        [Serializable]
        sealed class ReelBoardFile
        {
            public int rows;
            public ReelMinRecord[] minimums;
            public ReelAdviceRecord[] advice;
        }

        [Serializable]
        sealed class ReelMinRecord
        {
            public int fromLayer;
            public int toLayer;
            public string type;
            public int minimumPerColumn;
        }

        [Serializable]
        sealed class ReelAdviceRecord
        {
            public string square;
            public string text;
        }
#pragma warning restore CS0649
    }
}
