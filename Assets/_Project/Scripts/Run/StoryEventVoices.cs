using System;
using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public readonly struct EventVoice
    {
        public readonly string CharacterId;
        public readonly string Name;
        public readonly int OptionIndex;
        public readonly string Line;

        public EventVoice(string characterId, string name, int optionIndex, string line)
        {
            CharacterId = characterId ?? "";
            Name = name ?? "";
            OptionIndex = optionIndex;
            Line = line ?? "";
        }
    }

    /// <summary>名前と口調が決まるまでの仮セリフ。性格と選択肢のタグだけで選ぶ。</summary>
    public static class StoryEventVoices
    {
        static readonly string[] BraveFor = { "やるなら、先に手を出す。", "ごちゃごちゃ言ってるあいだに、終わる。" };
        static readonly string[] BraveAgainst = { "……今日は、やめておくか。", "無理に行く話でもない。" };
        static readonly string[] TimidFor = { "怖いです。でも、このままは嫌です。", "……やります。後悔しても知りません。" };
        static readonly string[] TimidAgainst = { "やめましょう。危ないです。", "戻れるうちに、戻りませんか。" };
        static readonly string[] CuriousFor = { "中身、見ないと気になる。", "触らなきゃ、一生わからない。" };
        static readonly string[] CuriousAgainst = { "触らないほうが、あとでおもしろいかも。", "今は、見てるだけにします。" };

        public static EventVoice[] Pick(IReadOnlyList<CharacterConfig> formation, EventOption[] options, Random rng)
        {
            if (formation == null || options == null || options.Length < 2 || rng == null)
                return Array.Empty<EventVoice>();

            var voices = new List<EventVoice>();
            for (var i = 0; i < formation.Count && voices.Count < 2; i++)
            {
                var character = formation[i];
                if (character == null)
                    continue;
                var index = Endorse(character.personality, options);
                voices.Add(new EventVoice(
                    character.characterId,
                    character.displayName,
                    index,
                    Line(character.personality, index == 0 ? options[0] : options[index], options, rng)));
            }

            return voices.ToArray();
        }

        public static int Endorse(TrustPersonality personality, EventOption[] options)
        {
            if (options == null || options.Length == 0)
                return 0;
            if (personality == TrustPersonality.Curious)
            {
                for (var i = 0; i < options.Length; i++)
                {
                    if (options[i] != null && options[i].knowledge == "unknown")
                        return i;
                }
            }

            var stance = personality == TrustPersonality.Timid ? "defensive" : "aggressive";
            for (var i = 0; i < options.Length; i++)
            {
                if (options[i] != null && options[i].stance == stance)
                    return i;
            }

            return 0;
        }

        static string Line(TrustPersonality personality, EventOption endorsed, EventOption[] options, Random rng)
        {
            var aggressive = endorsed != null && endorsed.stance == "aggressive";
            var pool = personality switch
            {
                TrustPersonality.Timid => aggressive ? TimidFor : TimidAgainst,
                TrustPersonality.Curious => aggressive || (endorsed != null && endorsed.knowledge == "unknown")
                    ? CuriousFor
                    : CuriousAgainst,
                _ => aggressive ? BraveFor : BraveAgainst
            };
            _ = options;
            return pool[rng.Next(pool.Length)];
        }
    }
}
