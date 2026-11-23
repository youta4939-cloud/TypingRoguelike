using System;

namespace TypingRoguelike.Data
{
    [Serializable]
    public sealed class PersonalityFile
    {
        public int version = 1;
        public int agreeAffinity;
        public int passive1Threshold;
        public string passive1Message = "";
        public string defaultTag = "";
        public PersonalityMood mood = new();
        public PersonalityChartScale chart = new();
        public PersonalityAxis[] axes = Array.Empty<PersonalityAxis>();
        public PersonalityTagAlias[] tagAliases = Array.Empty<PersonalityTagAlias>();
        public PersonalityTagRule[] tagRules = Array.Empty<PersonalityTagRule>();
        public int[] debugAffinity = Array.Empty<int>();
        public PersonalityCharacter[] characters = Array.Empty<PersonalityCharacter>();
    }

    [Serializable]
    public sealed class PersonalityMood
    {
        public int high;
        public int low;
    }

    [Serializable]
    public sealed class PersonalityChartScale
    {
        public int stages;
        public int percentMax;
        public int weightMin;
        public int weightMax;
    }

    [Serializable]
    public sealed class PersonalityAxis
    {
        public string name = "";
        public string source = "";
    }

    [Serializable]
    public sealed class PersonalityTagAlias
    {
        public string tag = "";
        public string[] names = Array.Empty<string>();
    }

    [Serializable]
    public sealed class PersonalityTagRule
    {
        public string tag = "";
        public string when = "";
        public string equals = "";
    }

    [Serializable]
    public sealed class PersonalityCharacter
    {
        public string id = "";
        public string name = "";
        public int talk;
        public int w_attack;
        public int w_careful;
        public int w_explore;
        public int yieldRate;
        public bool mood_random;
        public string[] lines_agree = Array.Empty<string>();
        public string[] lines_disagree = Array.Empty<string>();
        public string[] lines_yield = Array.Empty<string>();
    }
}
