using UnityEngine;

namespace TypingRoguelike.Battle
{
    /// <summary>1024×1536。全 PNG は同じキャンバス。pivot は回転中心だけに使う。</summary>
    public static class SkeletonSamuraiParts
    {
        public const int CanvasWidth = 1024;
        public const int CanvasHeight = 1536;
        public const float Ppu = 100f;
        public const string ResourceFolder = "Battle/Art/Enemies/SkeletonSamurai/";
        public const int FeetX = 515;
        public const int FeetY = 1440;
        public const int WaistX = 515;
        public const int WaistY = 650;
        public const int NeckX = 498;
        public const int NeckY = 334;

        public static readonly Vector2Int Feet = new(FeetX, FeetY);
        public static readonly Vector2Int Waist = new(WaistX, WaistY);
        public static readonly Vector2Int Neck = new(NeckX, NeckY);

        public static Vector2 NormalizedPivot(int pivotX, int pivotY)
        {
            return new Vector2(
                pivotX / (float)CanvasWidth,
                1f - pivotY / (float)CanvasHeight);
        }

        public static Vector2 Offset(Vector2Int point, Vector2Int origin)
        {
            return new Vector2(point.x - origin.x, -(point.y - origin.y));
        }

        public static float UiRootScale =>
            BigMouthParts.UiRootScale * BigMouthParts.CanvasWidth / CanvasWidth;
    }
}
