using UnityEngine;

namespace TypingRoguelike.Data
{
    /// <summary>戦闘 UI の 9-slice フレーム等。差し替えは Sprite 参照または Resources/Battle/UI/ の PNG 上書き。</summary>
    [CreateAssetMenu(fileName = "BattleUITemplate", menuName = "TypingRoguelike/Battle UI Template")]
    public sealed class BattleUITemplateDefinition : ScriptableObject
    {
        [Header("9-slice Panels")]
        public Sprite windowPanel;
        public Sprite partyCard;
        public Sprite portraitFrame;
        public Sprite wordBox;
        public Sprite bossFrame;
        public Sprite button;

        [Header("Bars")]
        public Sprite barTrack;
        public Sprite barFill;

        [Header("Slice border (pixels)")]
        public int panelBorder = 24;

        [Header("Theme")]
        public Color accentPrimary = new(0.94f, 0.27f, 0.27f);
        public Color accentSecondary = new(0.85f, 0.65f, 0.15f);
        public Color textPrimary = Color.white;
        public Color textMuted = new(0.55f, 0.58f, 0.65f);
    }
}
