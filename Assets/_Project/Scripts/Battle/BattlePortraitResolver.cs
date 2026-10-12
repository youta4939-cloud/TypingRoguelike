using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public static class BattlePortraitResolver
    {
        const string PortraitRoot = "Battle/Art/Portraits";
        const string BossPath = "Battle/Art/Boss_Dragon";

        public static Sprite LoadCharacter(string characterName)
        {
            var key = characterName switch
            {
                "アリス" => "Alice",
                "エレン" => "Ellen",
                "ルナ" => "Luna",
                _ => null
            };
            if (string.IsNullOrEmpty(key))
                return null;

            return LoadSpriteFromResources($"{PortraitRoot}/{key}");
        }

        public static Sprite ResolveCharacterPortrait(CharacterDefinition character)
        {
            if (character == null)
                return null;

            var fromResources = LoadCharacter(character.characterName);
            if (fromResources != null)
                return fromResources;

            return character.battlePortrait;
        }

        public static Sprite LoadBossFromResources() => LoadSpriteFromResources(BossPath);

        public static Sprite ResolveBossIllustration(EnemyDefinition enemy)
        {
            var fromResources = LoadBossFromResources();
            if (fromResources != null)
                return fromResources;

            return enemy?.battleIllustration;
        }

        public static Sprite LoadSpriteFromResources(string pathWithoutExtension)
        {
            if (string.IsNullOrEmpty(pathWithoutExtension))
                return null;

            var sprite = Resources.Load<Sprite>(pathWithoutExtension);
            if (sprite != null)
                return sprite;

            var sprites = Resources.LoadAll<Sprite>(pathWithoutExtension);
            if (sprites != null && sprites.Length > 0)
                return sprites[0];

            return null;
        }
    }
}
