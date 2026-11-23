#if UNITY_EDITOR
using TypingRoguelike.Battle;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class EnemyEditorBreakColors
    {
        public static Color FaceColor(ElementColor color) => EnemyStatusHud.FaceColor(color);

        public static string Abbrev(ElementColor color) => color switch
        {
            ElementColor.Red => "R",
            ElementColor.Blue => "B",
            ElementColor.Yellow => "Y",
            ElementColor.Green => "G",
            ElementColor.Purple => "P",
            _ => "?"
        };

        public static string JapaneseName(ElementColor color) => color switch
        {
            ElementColor.Red => "赤",
            ElementColor.Blue => "青",
            ElementColor.Yellow => "黄",
            ElementColor.Green => "緑",
            ElementColor.Purple => "紫",
            _ => color.ToString()
        };

        public static ElementColor Cycle(ElementColor current)
        {
            var values = (ElementColor[])System.Enum.GetValues(typeof(ElementColor));
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != current)
                    continue;
                return values[(i + 1) % values.Length];
            }

            return ElementColor.Red;
        }
    }
}
#endif