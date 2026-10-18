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
        [Range(1, 99)] public int totalLayers = 15;

        [Tooltip("この層数ごとに、4マス目をエレベーターにする。ボスの層と重なったときはボスを優先する")]
        [Range(1, 30)] public int elevatorEvery = 3;

        [Tooltip("この層数ごとに、4マス目をボスにする")]
        [Range(1, 30)] public int bossEvery = 5;

        [Tooltip("罠マスで最大HPから減らす割合。0.1 で 10%")]
        [Range(0.01f, 0.9f)] public float trapHpLossFraction = 0.1f;

        [Tooltip("層の範囲ごとに、各列へ最低いくつ置くか。満たさない盤面は準備画面で選べない。")]
        public LayerTypeMinimum[] columnMinimums =
        {
            new LayerTypeMinimum { fromLayer = 1, toLayer = 99, type = DungeonNodeType.Combat, minimumPerColumn = 3 }
        };

        public List<LayerTypeMinimum> MinimumsFor(int layer)
        {
            var list = new List<LayerTypeMinimum>();
            if (columnMinimums == null)
                return list;
            foreach (var rule in columnMinimums)
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
            return loaded != null ? loaded : RuntimeFallback();
        }

        public static LayerConfig RuntimeFallback()
        {
            var config = CreateInstance<LayerConfig>();
            config.totalLayers = 15;
            config.elevatorEvery = 3;
            config.bossEvery = 5;
            config.trapHpLossFraction = 0.1f;
            config.columnMinimums = new[]
            {
                new LayerTypeMinimum { fromLayer = 1, toLayer = 99, type = DungeonNodeType.Combat, minimumPerColumn = 3 }
            };
            return config;
        }
    }
}
