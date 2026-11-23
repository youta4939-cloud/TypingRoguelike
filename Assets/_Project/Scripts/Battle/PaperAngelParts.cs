using UnityEngine;

namespace TypingRoguelike.Battle
{
    public enum PaperAngelPart
    {
        Shadow = 0,
        WingLInner = 1,
        WingLOuter = 2,
        WingRInner = 3,
        WingROuter = 4,
        Feet = 5,
        FaceInner = 6,
        Body = 7,
        Seal = 8,
        SealGlow = 9
    }

    /// <summary>1476×1908。parts_pivots.json と同じ Pivot（左上原点・y 下向き）。</summary>
    public static class PaperAngelParts
    {
        public const int CanvasWidth = 1476;
        public const int CanvasHeight = 1908;
        /// <summary>大口と同じ見かけサイズ: 100 PPU @ 345px ≡ 1476px 幅。</summary>
        public const float Ppu = 100f * CanvasWidth / BigMouthParts.CanvasWidth;
        public const string ResourceFolder = "Battle/Art/Enemies/PaperAngel/";

        public readonly struct Spec
        {
            public readonly PaperAngelPart Part;
            public readonly string Name;
            public readonly int PivotX;
            public readonly int PivotY;
            public readonly bool OnRoot;
            public readonly bool Additive;

            public Spec(
                PaperAngelPart part,
                string name,
                int pivotX,
                int pivotY,
                bool onRoot = false,
                bool additive = false)
            {
                Part = part;
                Name = name;
                PivotX = pivotX;
                PivotY = pivotY;
                OnRoot = onRoot;
                Additive = additive;
            }
        }

        /// <summary>README の描画順（奥→手前）。</summary>
        public static readonly Spec[] All =
        {
            new Spec(PaperAngelPart.Shadow, "01_shadow", 700, 1880, onRoot: true),
            new Spec(PaperAngelPart.WingLInner, "02_wing_L_inner", 610, 797),
            new Spec(PaperAngelPart.WingLOuter, "03_wing_L_outer", 610, 797),
            new Spec(PaperAngelPart.WingRInner, "04_wing_R_inner", 830, 797),
            new Spec(PaperAngelPart.WingROuter, "05_wing_R_outer", 830, 797),
            new Spec(PaperAngelPart.Feet, "06_feet", 700, 1880),
            new Spec(PaperAngelPart.FaceInner, "07_face_inner", 690, 640),
            new Spec(PaperAngelPart.Body, "08_body", 700, 1880),
            new Spec(PaperAngelPart.Seal, "09_seal", 686, 1083),
            new Spec(PaperAngelPart.SealGlow, "10_seal_glow_fx", 686, 1083, additive: true)
        };

        public static Spec Get(PaperAngelPart part)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Part == part)
                    return All[i];
            }

            return All[0];
        }

        public static Spec BodySpec => Get(PaperAngelPart.Body);

        public static Vector2 NormalizedPivot(int pivotX, int pivotY)
        {
            var unityY = (CanvasHeight - 1) - pivotY;
            return new Vector2(pivotX / (float)CanvasWidth, unityY / (float)CanvasHeight);
        }

        public static Vector3 LocalOffset(Spec part, Spec body, float ppu, bool pixels)
        {
            var dx = part.PivotX - body.PivotX;
            var dy = -(part.PivotY - body.PivotY);
            if (pixels || ppu <= 0f)
                return new Vector3(dx, dy, 0f);
            return new Vector3(dx / ppu, dy / ppu, 0f);
        }

        public static float UiRootScale =>
            BigMouthParts.UiRootScale * BigMouthParts.CanvasWidth / CanvasWidth;
    }
}
