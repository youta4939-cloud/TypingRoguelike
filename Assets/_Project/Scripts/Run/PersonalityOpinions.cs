using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    public enum PersonalitySpeak
    {
        Agree,
        Silent,
        Yield
    }

    public sealed class PersonalityOpinion
    {
        public string Id = "";
        public string Name = "";
        public PersonalitySpeak Speak;
        public int Choice = -1;
        public string Line = "";
    }

    /// <summary>
    /// 表示順をシャッフルし、会話率で発言、性格タグの重みで賛同側、
    /// すでに発言した側と反対のときだけ遠慮率で取り下げ。
    /// </summary>
    public static class PersonalityOpinions
    {
        public static PersonalityOpinion[] Roll(MetaProgressData profile, EventOption[] options, System.Random rng)
        {
            var characters = PersonalityCatalog.Characters;
            if (characters.Length == 0 || rng == null)
                return System.Array.Empty<PersonalityOpinion>();

            var order = new PersonalityCharacter[characters.Length];
            var count = 0;
            for (var i = 0; i < characters.Length; i++)
            {
                if (characters[i] == null || string.IsNullOrEmpty(characters[i].id))
                    continue;
                order[count++] = characters[i];
            }

            for (var i = count - 1; i > 0; i--)
            {
                var swap = rng.Next(i + 1);
                var held = order[i];
                order[i] = order[swap];
                order[swap] = held;
            }

            var left = options != null && options.Length > 0 ? options[0] : null;
            var right = options != null && options.Length > 1 ? options[1] : null;
            var tagA = PersonalityCatalog.TagOf(left);
            var tagB = PersonalityCatalog.TagOf(right);
            var results = new PersonalityOpinion[count];
            var spoken = 0;
            var spokenChoice = new int[count];

            for (var i = 0; i < count; i++)
            {
                var character = order[i];
                var opinion = new PersonalityOpinion
                {
                    Id = character.id,
                    Name = character.name
                };
                results[i] = opinion;
                if (rng.Next(100) >= character.talk)
                {
                    opinion.Speak = PersonalitySpeak.Silent;
                    continue;
                }

                var weightA = PersonalityCatalog.Weight(profile, character, tagA);
                var weightB = PersonalityCatalog.Weight(profile, character, tagB);
                int choice;
                if (tagA == tagB || weightA + weightB <= 0)
                    choice = rng.Next(2);
                else
                    choice = rng.NextDouble() < weightA / (double)(weightA + weightB) ? 0 : 1;

                var opposes = false;
                for (var s = 0; s < spoken; s++)
                {
                    if (spokenChoice[s] != choice)
                        opposes = true;
                }

                if (opposes && rng.Next(100) < character.yieldRate)
                {
                    opinion.Speak = PersonalitySpeak.Yield;
                    opinion.Choice = choice;
                    opinion.Line = PersonalityCatalog.PickLine(character.lines_yield, rng);
                    continue;
                }

                opinion.Speak = PersonalitySpeak.Agree;
                opinion.Choice = choice;
                opinion.Line = PersonalityCatalog.PickLine(character.lines_agree, rng);
                spokenChoice[spoken++] = choice;
            }

            return results;
        }

        public static string Describe(PersonalityOpinion opinion)
        {
            if (opinion == null)
                return "";
            var side = opinion.Choice == 0 ? "選択肢A" : opinion.Choice == 1 ? "選択肢B" : "";
            switch (opinion.Speak)
            {
                case PersonalitySpeak.Silent:
                    return opinion.Name + " 黙る";
                case PersonalitySpeak.Yield:
                    return opinion.Name + " " + side + " 遠慮";
                default:
                    return opinion.Name + " " + side + " 賛成";
            }
        }
    }
}
