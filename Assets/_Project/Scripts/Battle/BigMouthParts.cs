using UnityEngine;

namespace TypingRoguelike.Battle
{
    public enum BigMouthPart
    {
        Body = 0,
        Mouth = 1,
        TeethUpper = 2,
        TeethLower = 3,
        ArmL = 4,
        ArmR = 5,
        EarringL = 6,
        EarringR = 7,
        Legs = 8,
        Shadow = 9
    }

    /// <summary>345×520、左上原点・y下向きの Pivot。Unity では y を反転して使う。</summary>
    public static class BigMouthParts
    {
        public const int CanvasWidth = 345;
        public const int CanvasHeight = 520;
        public const float Ppu = 100f;
        public const string ResourceFolder = "Battle/Art/Enemies/BigMouth/";

        public readonly struct Spec
        {
            public readonly BigMouthPart Part;
            public readonly string Name;
            public readonly int PivotX;
            public readonly int PivotY;
            public readonly int SortingOrder;
            public readonly BigMouthPart Parent;

            public Spec(BigMouthPart part, string name, int pivotX, int pivotY, int sortingOrder, BigMouthPart parent)
            {
                Part = part;
                Name = name;
                PivotX = pivotX;
                PivotY = pivotY;
                SortingOrder = sortingOrder;
                Parent = parent;
            }
        }

        public static readonly Spec[] All =
        {
            new Spec(BigMouthPart.Shadow, "10_shadow", 179, 475, 0, BigMouthPart.Body),
            new Spec(BigMouthPart.Legs, "09_legs", 179, 408, 1, BigMouthPart.Body),
            new Spec(BigMouthPart.Body, "01_body_skin", 179, 460, 5, BigMouthPart.Body),
            new Spec(BigMouthPart.Mouth, "02_mouth_inner", 174, 224, 2, BigMouthPart.Body),
            new Spec(BigMouthPart.TeethLower, "04_teeth_lower", 174, 232, 3, BigMouthPart.Body),
            new Spec(BigMouthPart.TeethUpper, "03_teeth_upper", 174, 160, 4, BigMouthPart.Body),
            new Spec(BigMouthPart.ArmL, "05_arm_L", 132, 226, 6, BigMouthPart.Body),
            new Spec(BigMouthPart.ArmR, "06_arm_R", 216, 226, 7, BigMouthPart.Body),
            new Spec(BigMouthPart.EarringL, "07_earring_L", 71, 140, 8, BigMouthPart.Body),
            new Spec(BigMouthPart.EarringR, "08_earring_R", 286, 137, 9, BigMouthPart.Body)
        };

        public static Spec Get(BigMouthPart part)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Part == part)
                    return All[i];
            }

            return All[0];
        }

        public static Vector2 NormalizedPivot(int pivotX, int pivotY)
        {
            var unityY = (CanvasHeight - 1) - pivotY;
            return new Vector2(pivotX / (float)CanvasWidth, unityY / (float)CanvasHeight);
        }

        /// <summary>親 Pivot から子 Pivot へのずれ。Unity の y は上向き。</summary>
        public static Vector3 LocalOffset(Spec part, Spec parent, float ppu, bool pixels)
        {
            var dx = part.PivotX - parent.PivotX;
            var dy = -(part.PivotY - parent.PivotY);
            if (pixels || ppu <= 0f)
                return new Vector3(dx, dy, 0f);
            return new Vector3(dx / ppu, dy / ppu, 0f);
        }
    }
}
