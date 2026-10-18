using UnityEngine;

namespace TypingRoguelike.Run
{
    public sealed class TrustGaugeView
    {
        public string CharacterId;
        public string Name;
        public string Personality;
        public int Trust;
        public string PassiveLabel;
        public bool Dim;
        public Sprite Portrait;
    }

    public sealed class OpinionBubbleView
    {
        public string CharacterId;
        public string Name;
        public string Line;
        public int ChoiceIndex;
        public Sprite Portrait;
    }

    public sealed class OpinionFaceView
    {
        public string CharacterId;
        public Sprite Portrait;
    }

    public sealed class OpinionTrustPreview
    {
        public string CharacterId;
        public int From;
        public int To;
        public string Notice;
    }

    public sealed class OpinionButtonView
    {
        public string Title;
        public string Effect;
        public int ChoiceIndex;
        public bool Emphasized;
        public bool Dimmed;
        public OpinionFaceView[] Faces;
        public OpinionTrustPreview[] Previews;
    }

    public sealed class OpinionScreenView
    {
        public string SceneTitle;
        public string SceneText;
        public Sprite TileIcon;
        public bool Agreed;
        public OpinionBubbleView[] Bubbles;
        public OpinionButtonView[] Buttons;
        public TrustGaugeView[] Gauges;
    }
}
