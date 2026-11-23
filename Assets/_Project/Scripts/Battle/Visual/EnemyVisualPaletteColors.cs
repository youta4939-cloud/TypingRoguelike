using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
    public static class EnemyVisualPaletteColors
    {
        public struct PaletteSet
        {
            public Color Core;
            public Color Body;
            public Color Halo;
            public Color Splash;
            public Color Afterimage;
            public Color EdgeGlow;
        }

        public static PaletteSet Resolve(EnemyEffectPalette palette) => palette switch
        {
            EnemyEffectPalette.BloodRed => new PaletteSet
            {
                Core = Hex("#D9664C"),
                Body = Hex("#9E080A"),
                Halo = Hex("#290000"),
                Splash = Hex("#800505"),
                Afterimage = Hex("#BF1F26"),
                EdgeGlow = Hex("#800303")
            },
            EnemyEffectPalette.PoisonGreen => new PaletteSet
            {
                Core = Hex("#C8F060"), Body = Hex("#3D8A1A"), Halo = Hex("#0C2A08"),
                Splash = Hex("#4A7A12"), Afterimage = Hex("#7CB830"), EdgeGlow = Hex("#2A6010")
            },
            EnemyEffectPalette.CursePurple => new PaletteSet
            {
                Core = Hex("#E0A0FF"), Body = Hex("#6A2088"), Halo = Hex("#1A0624"),
                Splash = Hex("#7A30A0"), Afterimage = Hex("#B060D0"), EdgeGlow = Hex("#501070")
            },
            EnemyEffectPalette.IceBlue => new PaletteSet
            {
                Core = Hex("#D0F4FF"), Body = Hex("#3A88C8"), Halo = Hex("#0A2038"),
                Splash = Hex("#8EC8E8"), Afterimage = Hex("#70B8E0"), EdgeGlow = Hex("#2060A0")
            },
            EnemyEffectPalette.HolyGold => new PaletteSet
            {
                Core = Hex("#FFF2C0"), Body = Hex("#D4A017"), Halo = Hex("#4A3008"),
                Splash = Hex("#FFE080"), Afterimage = Hex("#F0D070"), EdgeGlow = Hex("#A07010")
            },
            _ => new PaletteSet
            {
                Core = Hex("#E8E0D0"), Body = Hex("#8A7A64"), Halo = Hex("#3A3428"),
                Splash = Hex("#C8B8A0"), Afterimage = Hex("#D8C8B0"), EdgeGlow = Hex("#6A6050")
            }
        };

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c))
                return c;
            return Color.white;
        }
    }
}