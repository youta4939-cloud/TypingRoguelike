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
        public EnemyBattleVisualKind BattleVisual = EnemyBattleVisualKind.Auto;
        public string BossDisplayName = "BOSS";
        public string BossEmojiNormal = "🐲";
        public string BossEmojiEnraged = "🔥🐲";
        public string BossEmojiDefeated = "💀";
        public EnemyDefinition Enemy;

        public bool UseSpritePair =>
            Enemy != null && Enemy.visual.graphicMode == EnemyGraphicMode.SpritePair;

        public sealed class CharacterVisual
        {
            public string Role;
            public string EnglishName;
            public Sprite Portrait;
        }

        public static BattleVisualContext FromEncounter(
            BattleEncounterDefinition enc,
            RunPartyState party,
            EnemyDefinition enemyOverride = null)
        {
            var ctx = new BattleVisualContext();
            var enemy = enemyOverride != null ? enemyOverride : enc?.enemy;
            ctx.Enemy = enemy;
            if (enemy != null)
            {
                ctx.BattleVisual = enemy.visual.graphicMode == EnemyGraphicMode.SpritePair
                    ? EnemyBattleVisualKind.StaticIllustration
                    : enemy.battleVisual;
                ctx.BossIllustration = BattlePortraitResolver.ResolveBossIllustration(enemy);
                ctx.BossDisplayName = enemy.displayName;
                ctx.BossEmojiNormal = enemy.emojiNormal;
                ctx.BossEmojiEnraged = enemy.emojiEnraged;
                ctx.BossEmojiDefeated = enemy.emojiDefeated;
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

            return StarterCharacters.ArtKey(character?.characterName) switch
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
