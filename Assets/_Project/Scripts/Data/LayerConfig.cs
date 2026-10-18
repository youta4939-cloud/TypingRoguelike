using UnityEngine;

namespace TypingRoguelike.Data
{
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
            return config;
        }
    }
}
