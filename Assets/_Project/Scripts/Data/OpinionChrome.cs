using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Data
{
    /// <summary>意見画面の画像部品。未設定の枠は仮の色面になり、あとから同じ欄へ素材を入れられる。</summary>
    [CreateAssetMenu(fileName = "OpinionChrome", menuName = "TypingRoguelike/Opinion Chrome")]
    public sealed class OpinionChrome : ScriptableObject
    {
        [Header("マスアイコン")]
        public Sprite combatIcon;
        public Sprite restIcon;
        public Sprite merchantIcon;
        public Sprite treasureIcon;
        public Sprite eventIcon;
        public Sprite trapIcon;

        [Header("枠")]
        public Sprite faceFrame;
        public Sprite bubbleFrame;
        public Sprite bubbleTail;

        [Header("ボタン")]
        public Sprite choiceButton;
        public Sprite choiceButtonAgreed;
        public Sprite choiceButtonDim;

        [Header("ゲージ")]
        public Sprite gaugeTrack;
        public Sprite gaugeFill;

        public Sprite IconFor(DungeonNodeType tile) => tile switch
        {
            DungeonNodeType.Rest => restIcon,
            DungeonNodeType.Merchant => merchantIcon,
            DungeonNodeType.Treasure => treasureIcon,
            DungeonNodeType.Event => eventIcon,
            DungeonNodeType.Trap => trapIcon,
            _ => combatIcon
        };

        public static OpinionChrome LoadOrFallback()
        {
            var loaded = Resources.Load<OpinionChrome>("Run/OpinionChrome");
            return loaded != null ? loaded : CreateInstance<OpinionChrome>();
        }
    }
}
