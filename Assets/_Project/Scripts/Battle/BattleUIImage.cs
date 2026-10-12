using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public static class BattleUIImage
    {
        public static void ApplyPanel(Image img, Color tint, Sprite sliced = null, int border = 0)
        {
            img.color = tint;
            if (sliced != null)
            {
                img.sprite = sliced;
                img.type = Image.Type.Sliced;
                if (border > 0)
                    img.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                BattleUITheme.EnsureSprite(img);
                img.type = Image.Type.Simple;
            }
        }

        public static void ApplyTemplatePanel(Image img, Color tint, BattleUITemplateDefinition template, Sprite sprite)
        {
            var border = template != null ? template.panelBorder : 0;
            ApplyPanel(img, tint, sprite, border);
        }
    }
}
