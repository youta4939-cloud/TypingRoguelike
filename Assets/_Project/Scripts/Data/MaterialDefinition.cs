using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "Material", menuName = "TypingRoguelike/Material Definition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        public int materialId;
        public string displayName = "素材";
        [Tooltip("主な出現開始階（1-based）")]
        public int floorMin = 1;
        [Tooltip("主な出現終了階（1-based）")]
        public int floorMax = 99;
        [Min(1)] public int pickWeight = 10;
    }
}
