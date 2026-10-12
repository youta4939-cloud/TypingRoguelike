using UnityEngine;

namespace TypingRoguelike.Battle
{
    public static class ElementColorUtility
    {
        public static string ToJapanese(ElementColor color) => color switch
        {
            ElementColor.Red => "赤",
            ElementColor.Blue => "青",
            ElementColor.Green => "緑",
            ElementColor.Yellow => "黄",
            ElementColor.Purple => "紫",
            _ => string.Empty
        };

        public static Color ToUnityColor(ElementColor color) => color switch
        {
            ElementColor.Red => new Color(0.94f, 0.27f, 0.27f),
            ElementColor.Blue => new Color(0.23f, 0.51f, 0.96f),
            ElementColor.Green => new Color(0.06f, 0.73f, 0.51f),
            ElementColor.Yellow => new Color(0.96f, 0.62f, 0.04f),
            ElementColor.Purple => new Color(0.55f, 0.36f, 0.96f),
            _ => Color.white
        };
    }
}
