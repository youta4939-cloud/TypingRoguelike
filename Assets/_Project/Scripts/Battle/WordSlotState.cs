namespace TypingRoguelike.Battle
{
    public sealed class WordSlotState
    {
        public ElementColor Color;
        public string DisplayText = string.Empty;
        public string Text = string.Empty;
        public string[] InputOptions = System.Array.Empty<string>();
        public int Progress;
        public bool Active = true;
    }
}
