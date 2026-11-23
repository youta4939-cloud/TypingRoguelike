using UnityEngine;

namespace TypingRoguelike.Battle
{
    public enum GemColor { R, Y, G, B, P }

    public enum FlameRoute { Sheet, Shader }

    public sealed class StockLayout
    {
        public Vector2 PanelPos = new Vector2(810.4f, -753.7f);
        public Vector2 PanelSize = new Vector2(691.2f, 248.4f);
        public SlotAnchor[] Slots = new SlotAnchor[5];
        public MedallionAnchor Hp = new MedallionAnchor
        {
            Centre = new Vector2(137.7f, -171.5f),
            IconOffset = new Vector2(0f, 0.5f)
        };
        public MedallionAnchor Madness = new MedallionAnchor
        {
            Centre = new Vector2(552.6f, -172.1f),
            IconOffset = new Vector2(-0.5f, 1f)
        };
        public PlateAnchor HpPlate = new PlateAnchor { X = 170.6f, Y = 151.7f, W = 139.9f, H = 49.7f };
        public PlateAnchor MadPlate = new PlateAnchor { X = 378.5f, Y = 151.7f, W = 139.9f, H = 49.7f };
        public MaskAnchor HpFlash = new MaskAnchor { Pos = new Vector2(92f, -126f), Size = new Vector2(231f, 91f) };
        public MaskAnchor LowHp = new MaskAnchor { Pos = new Vector2(92f, -126f), Size = new Vector2(231f, 91f) };
        public MaskAnchor MadGlow = new MaskAnchor { Pos = new Vector2(366f, -126f), Size = new Vector2(232f, 91f) };

        public struct SlotAnchor
        {
            public Vector2 Ball;
            public Vector2 Mouth;
            public Vector2 Tint;
            public Vector2 BallAbs;
            public float Offset;
        }

        public struct MedallionAnchor
        {
            public Vector2 Centre;
            public Vector2 IconOffset;
        }

        public struct PlateAnchor
        {
            public float X, Y, W, H;
            public float Right => X + W;
            public float CenterY => -(Y + H * 0.5f);
        }

        public struct MaskAnchor
        {
            public Vector2 Pos;
            public Vector2 Size;
        }

        public static StockLayout Load()
        {
            var layout = new StockLayout();
            var text = Resources.Load<TextAsset>("Battle/StockPanel/Data/layout_stockpanel_v4_4");
            if (text == null)
            {
                Debug.LogWarning("StockPanel のレイアウト JSON が無いので、同梱の数値を使います。");
                layout.FillDefaults();
                return layout;
            }

            try
            {
                layout.Read(JNode.Parse(text.text));
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("StockPanel の JSON を読めなかったので、同梱の数値を使います。 " + error.Message);
                layout.FillDefaults();
            }

            return layout;
        }

        void Read(JNode root)
        {
            FillDefaults();
            var panel = root.Get("panel")?.Get("rect");
            var top = panel?.Get("topleft");
            var size = panel?.Get("size");
            if (top != null && top.Count >= 2)
                PanelPos = new Vector2(top.At(0).Float(810.4f), -top.At(1).Float(753.7f));
            if (size != null && size.Count >= 2)
                PanelSize = new Vector2(size.At(0).Float(691.2f), size.At(1).Float(248.4f));

            var slots = root.Get("slots");
            int count = slots != null ? Mathf.Min(5, slots.Count) : 0;
            for (int i = 0; i < count; i++)
            {
                var node = slots.At(i);
                var slot = Slots[i];
                slot.Ball = UnityPair(node.Get("ball_centre"), slot.Ball);
                slot.Mouth = UnityPair(node.Get("bowl_mouth"), slot.Mouth);
                slot.Tint = UnityPair(node.Get("bowl_tint_glow_centre"), slot.Tint);
                slot.BallAbs = AbsPair(node.Get("ball_centre"), slot.BallAbs);
                var offset = node.Get("time_offset_s");
                if (offset != null && offset.Type == JNode.Kind.Number)
                    slot.Offset = offset.Float(slot.Offset);
                Slots[i] = slot;
            }

            ReadMedallion(root.Get("medallions")?.Get("hp"), ref Hp);
            ReadMedallion(root.Get("medallions")?.Get("madness"), ref Madness);
            ReadPlate(root.Get("plates")?.Get("hp"), ref HpPlate);
            ReadPlate(root.Get("plates")?.Get("madness"), ref MadPlate);
            ReadMask(root.Get("fx_masks")?.Get("fx_hp_flash_mask"), ref HpFlash);
            ReadMask(root.Get("fx_masks")?.Get("fx_lowhp_glow_mask"), ref LowHp);
            ReadMask(root.Get("fx_masks")?.Get("fx_madness_glow_mask"), ref MadGlow);
        }

        static void ReadMedallion(JNode node, ref MedallionAnchor anchor)
        {
            if (node == null)
                return;
            anchor.Centre = UnityPair(node.Get("centre"), anchor.Centre);
            var offset = node.Get("icon_centre_offset_px_1x");
            if (offset != null && offset.Count >= 2)
                anchor.IconOffset = new Vector2(offset.At(0).Float(), offset.At(1).Float());
        }

        static void ReadPlate(JNode node, ref PlateAnchor plate)
        {
            if (node == null)
                return;
            var rel = node.Get("rel");
            var size = node.Get("size");
            if (rel != null && rel.Count >= 2)
            {
                plate.X = rel.At(0).Float(plate.X);
                plate.Y = rel.At(1).Float(plate.Y);
            }
            if (size != null && size.Count >= 2)
            {
                plate.W = size.At(0).Float(plate.W);
                plate.H = size.At(1).Float(plate.H);
            }
        }

        static void ReadMask(JNode node, ref MaskAnchor mask)
        {
            if (node == null)
                return;
            mask.Pos = UnityPair(node.Get("topleft"), mask.Pos);
            var size = node.Get("size_1x");
            if (size != null && size.Count >= 2)
                mask.Size = new Vector2(size.At(0).Float(mask.Size.x), size.At(1).Float(mask.Size.y));
        }

        static Vector2 UnityPair(JNode node, Vector2 fallback)
        {
            var unity = node?.Get("unity");
            if (unity == null || unity.Count < 2)
                return fallback;
            return new Vector2(unity.At(0).Float(fallback.x), unity.At(1).Float(fallback.y));
        }

        static Vector2 AbsPair(JNode node, Vector2 fallback)
        {
            var abs = node?.Get("abs");
            if (abs == null || abs.Count < 2)
                return fallback;
            return new Vector2(abs.At(0).Float(fallback.x), abs.At(1).Float(fallback.y));
        }

        void FillDefaults()
        {
            float[] offsets = { 0f, 1.13f, 2.29f, 0.61f, 1.77f };
            float[] ballX = { 124.2f, 234.9f, 345.6f, 456.3f, 567.0f };
            float[] absX = { 934.6f, 1045.3f, 1156.0f, 1266.7f, 1377.4f };
            for (int i = 0; i < 5; i++)
            {
                Slots[i] = new SlotAnchor
                {
                    Ball = new Vector2(ballX[i], -39.4f),
                    Mouth = new Vector2(ballX[i], -61.0f),
                    Tint = new Vector2(ballX[i], -62.1f),
                    BallAbs = new Vector2(absX[i], 793.1f),
                    Offset = offsets[i]
                };
            }
        }
    }

    public static class GemPalette
    {
        public static Color Mid(GemColor color)
        {
            switch (color)
            {
                case GemColor.R: return Hex("CC1222");
                case GemColor.Y: return Hex("FCBC14");
                case GemColor.G: return Hex("24B056");
                case GemColor.B: return Hex("1C5CE2");
                default: return Hex("962ECC");
            }
        }

        public static Color Light(GemColor color)
        {
            switch (color)
            {
                case GemColor.R: return Hex("FF7870");
                case GemColor.Y: return Hex("FFF4A0");
                case GemColor.G: return Hex("A0F696");
                case GemColor.B: return Hex("80BEFF");
                default: return Hex("E4A0FF");
            }
        }

        public static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }

        public static GemColor FromElement(ElementColor color)
        {
            switch (color)
            {
                case ElementColor.Red: return GemColor.R;
                case ElementColor.Yellow: return GemColor.Y;
                case ElementColor.Green: return GemColor.G;
                case ElementColor.Blue: return GemColor.B;
                default: return GemColor.P;
            }
        }

        public static string Letter(GemColor color)
        {
            switch (color)
            {
                case GemColor.R: return "R";
                case GemColor.Y: return "Y";
                case GemColor.G: return "G";
                case GemColor.B: return "B";
                default: return "P";
            }
        }
    }
}
