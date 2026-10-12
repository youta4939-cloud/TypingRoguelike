using System.Collections.Generic;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public sealed class BattleVisualContext
    {
        public readonly Dictionary<string, CharacterVisual> Characters = new();
        public Sprite BossIllustration;
        public string BossDisplayName = "BOSS";
        public string BossEmojiNormal = "🐲";
        public string BossEmojiEnraged = "🔥🐲";
        public string BossEmojiDefeated = "💀";

        public sealed class CharacterVisual
        {
            public string Role;
            public string EnglishName;
            public Sprite Portrait;
        }

        public static BattleVisualContext FromEncounter(BattleEncounterDefinition enc, RunPartyState party)
        {
            var ctx = new BattleVisualContext();
            if (enc?.enemy != null)
            {
                ctx.BossIllustration = BattlePortraitResolver.ResolveBossIllustration(enc.enemy);
                ctx.BossDisplayName = enc.enemy.displayName;
                ctx.BossEmojiNormal = enc.enemy.emojiNormal;
                ctx.BossEmojiEnraged = enc.enemy.emojiEnraged;
                ctx.BossEmojiDefeated = enc.enemy.emojiDefeated;
            }

            if (party != null)
            {
                foreach (var m in party.Members)
                    RegisterMember(ctx, m.character);
            }
            else if (enc?.party?.members != null)
            {
                foreach (var m in enc.party.members)
                    RegisterMember(ctx, m.character);
            }

            EnsureFallbackCharacters(ctx);
            return ctx;
        }

        static void RegisterMember(BattleVisualContext ctx, CharacterDefinition character)
        {
            if (character == null || string.IsNullOrEmpty(character.characterName))
                return;
            if (ctx.Characters.ContainsKey(character.characterName))
                return;

            var portrait = BattlePortraitResolver.ResolveCharacterPortrait(character);
            ctx.Characters[character.characterName] = new CharacterVisual
            {
                Role = character.roleLabel,
                EnglishName = ResolveEnglishName(character),
                Portrait = portrait
            };
        }

        static string ResolveEnglishName(CharacterDefinition character)
        {
            if (!string.IsNullOrWhiteSpace(character?.englishName))
                return character.englishName.Trim().ToUpperInvariant();

            return character?.characterName switch
            {
                "アリス" => "ALICE",
                "エレン" => "ELLEN",
                "ルナ" => "LUNA",
                _ => string.Empty
            };
        }

        static void EnsureFallbackCharacters(BattleVisualContext ctx)
        {
            foreach (var (name, role) in new[] { ("アリス", "アタッカー"), ("エレン", "ヒーラー"), ("ルナ", "メイジ") })
            {
                if (ctx.Characters.ContainsKey(name))
                    continue;
                ctx.Characters[name] = new CharacterVisual
                {
                    Role = role,
                    EnglishName = name switch
                    {
                        "アリス" => "ALICE",
                        "エレン" => "ELLEN",
                        "ルナ" => "LUNA",
                        _ => string.Empty
                    },
                    Portrait = BattlePortraitResolver.LoadCharacter(name)
                };
            }
        }
    }
}
