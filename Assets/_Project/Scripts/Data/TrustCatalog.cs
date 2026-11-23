using System;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [Serializable]
    public sealed class OpinionEvent
    {
        public DungeonNodeType tile = DungeonNodeType.Combat;
        public string sceneName = "";
        [TextArea] public string sceneText = "";
        public OpinionChoice choiceA = new();
        public OpinionChoice choiceB = new();

        public OpinionChoice ChoiceAt(int index) => index == 0 ? choiceA : choiceB;

        public int IndexOfStance(OpinionStance stance)
        {
            if (choiceA != null && choiceA.stance == stance)
                return 0;
            if (choiceB != null && choiceB.stance == stance)
                return 1;
            return stance == OpinionStance.Aggressive ? 0 : 1;
        }
    }

    [CreateAssetMenu(fileName = "TrustCatalog", menuName = "TypingRoguelike/Trust Catalog")]
    public sealed class TrustCatalog : ScriptableObject
    {
        public TrustRules rules;
        public CharacterConfig[] characters;
        public OpinionLineTable lines;
        public OpinionEvent[] events;

        public OpinionEvent FindEvent(DungeonNodeType tile)
        {
            if (events == null)
                return null;
            foreach (var entry in events)
            {
                if (entry != null && entry.tile == tile)
                    return entry;
            }

            return events.Length > 0 ? events[0] : null;
        }

        public CharacterConfig FindCharacter(string characterId)
        {
            if (characters == null || string.IsNullOrEmpty(characterId))
                return null;
            foreach (var character in characters)
            {
                if (character != null && StarterCharacters.Same(character.characterId, characterId))
                    return character;
            }

            return null;
        }

        public static TrustCatalog LoadOrFallback()
        {
            var loaded = Resources.Load<TrustCatalog>("Run/TrustCatalog");
            return loaded != null ? loaded : RuntimeFallback();
        }

        public static TrustCatalog RuntimeFallback()
        {
            var catalog = CreateInstance<TrustCatalog>();
            catalog.rules = CreateInstance<TrustRules>();
            catalog.lines = CreateInstance<OpinionLineTable>();
            catalog.characters = new[]
            {
                MakeCharacter(StarterCharacters.Chara01, StarterCharacters.Shown(StarterCharacters.Chara01), TrustPersonality.Brave,
                    Tier(25, "攻撃+10%", TrustPassiveKind.AttackPercent, 0.1f),
                    Tier(50, "戦闘開始時に敵が5秒止まる", TrustPassiveKind.OpeningFreezeSeconds, 5f),
                    Tier(75, "攻撃+20%", TrustPassiveKind.AttackPercent, 0.2f),
                    Tier(100, "戦闘開始時に敵が10秒止まる", TrustPassiveKind.OpeningFreezeSeconds, 10f)),
                MakeMomo(),
                MakeCharacter(StarterCharacters.Chara03, StarterCharacters.Shown(StarterCharacters.Chara03), TrustPersonality.Curious,
                    Tier(25, "攻撃+5%（コンボの仮）", TrustPassiveKind.AttackPercent, 0.05f),
                    Tier(50, "戦闘の刻印の選択肢+1", TrustPassiveKind.CardRewardChoices, 1f),
                    Tier(75, "攻撃+10%（コンボの仮）", TrustPassiveKind.AttackPercent, 0.1f),
                    Tier(100, "敵の次の攻撃までの時間を表示", TrustPassiveKind.ShowEnemyAttackTime, 1f))
            };
            catalog.events = new[]
            {
                Event(DungeonNodeType.Combat, "骨をかじる音", "通路の先で、何かが骨をかじる音がする。まだこちらに気づいていないが、数は分からない。",
                    "先に仕掛ける", OpinionStance.Aggressive, OpinionKnowledge.Unknown, OpinionEffectKind.EnemyHpFraction, -0.1f,
                    "様子を見る", OpinionStance.Defensive, OpinionKnowledge.Known, OpinionEffectKind.OpeningAttackSeconds, 3f,
                    OpinionEffectKind.OpeningAttackSeconds, -2f, OpinionEffectKind.BattleCoinFraction, -0.2f),
                Event(DungeonNodeType.Rest, "湧き水のある小部屋", "石の割れ目から、澄んだ水が湧いている。奥からはもっと強い水音がするが、暗くて先が見えない。",
                    "水脈を探る", OpinionStance.Aggressive, OpinionKnowledge.Known, OpinionEffectKind.Coins, 30f,
                    "ここで休む", OpinionStance.Defensive, OpinionKnowledge.Unknown, OpinionEffectKind.HealFraction, 0.1f),
                Event(DungeonNodeType.Merchant, "亡者の露店", "布を広げた亡者が、黙って品を並べている。背中の袋にも何か入っているようだが、こちらを見ようとしない。",
                    "袋を見せてもらう", OpinionStance.Aggressive, OpinionKnowledge.Unknown, OpinionEffectKind.Coins, 30f,
                    "並んだ品を見る", OpinionStance.Defensive, OpinionKnowledge.Known, OpinionEffectKind.HealFraction, 0.1f),
                Event(DungeonNodeType.Treasure, "鎖の巻かれた箱", "床に、鎖で何重にも縛られた箱がある。鎖は、内側から引きちぎられかけている。",
                    "鎖を解く", OpinionStance.Aggressive, OpinionKnowledge.Unknown, OpinionEffectKind.Coins, 30f,
                    "そっとしておく", OpinionStance.Defensive, OpinionKnowledge.Known, OpinionEffectKind.HealFraction, 0.1f),
                Event(DungeonNodeType.Event, "壁の向こうの声", "壁の向こうから、誰かが呼んでいる。聞き覚えがある気もするし、ない気もする。",
                    "声をたどる", OpinionStance.Aggressive, OpinionKnowledge.Unknown, OpinionEffectKind.CardChoices, 1f,
                    "耳をふさぐ", OpinionStance.Defensive, OpinionKnowledge.Known, OpinionEffectKind.HealFraction, 0.1f),
                Event(DungeonNodeType.Trap, "色の違う石畳", "足元の石畳が、一枚だけ色が違う。避けて通るには、崩れかけた壁際を伝うしかない。",
                    "駆け抜ける", OpinionStance.Aggressive, OpinionKnowledge.Known, OpinionEffectKind.Coins, 30f,
                    "壁際を伝う", OpinionStance.Defensive, OpinionKnowledge.Unknown, OpinionEffectKind.HealFraction, 0.1f)
            };
            return catalog;
        }

        static CharacterConfig MakeMomo()
        {
            var config = MakeCharacter(StarterCharacters.Chara02, StarterCharacters.Shown(StarterCharacters.Chara02), TrustPersonality.Timid,
                Tier(25, "被ダメージ−10%", TrustPassiveKind.DamageTakenPercent, 0.1f),
                null,
                Tier(75, "被ダメージ−20%", TrustPassiveKind.DamageTakenPercent, 0.2f),
                null);
            config.tiers[1] = new TrustPassiveTier
            {
                requiredTrust = 50,
                label = "被ダメージ−10%＋戦闘後HP5%回復",
                effects = new[]
                {
                    new TrustPassiveEffect { kind = TrustPassiveKind.DamageTakenPercent, value = 0.1f },
                    new TrustPassiveEffect { kind = TrustPassiveKind.PostBattleHealPercent, value = 0.05f }
                }
            };
            config.tiers[3] = new TrustPassiveTier
            {
                requiredTrust = 100,
                label = "被ダメージ−20%＋ミスの被弾を1回無効",
                effects = new[]
                {
                    new TrustPassiveEffect { kind = TrustPassiveKind.DamageTakenPercent, value = 0.2f },
                    new TrustPassiveEffect { kind = TrustPassiveKind.NegateMissHit, value = 1f }
                }
            };
            return config;
        }

        static CharacterConfig MakeCharacter(string id, string name, TrustPersonality personality, params TrustPassiveTier[] tiers)
        {
            var config = CreateInstance<CharacterConfig>();
            config.characterId = id;
            config.displayName = name;
            config.personality = personality;
            config.tiers = tiers;
            return config;
        }

        static TrustPassiveTier Tier(int trust, string label, TrustPassiveKind kind, float value) => new()
        {
            requiredTrust = trust,
            label = label,
            effects = new[] { new TrustPassiveEffect { kind = kind, value = value } }
        };

        static OpinionEvent Event(
            DungeonNodeType tile, string sceneName, string sceneText,
            string aBody, OpinionStance aStance, OpinionKnowledge aKnowledge, OpinionEffectKind aEffect, float aValue,
            string bBody, OpinionStance bStance, OpinionKnowledge bKnowledge, OpinionEffectKind bEffect, float bValue,
            OpinionEffectKind aEffect2 = OpinionEffectKind.None, float aValue2 = 0f,
            OpinionEffectKind bEffect2 = OpinionEffectKind.None, float bValue2 = 0f) => new()
        {
            tile = tile,
            sceneName = sceneName,
            sceneText = sceneText,
            choiceA = new OpinionChoice
            {
                body = aBody, stance = aStance, knowledge = aKnowledge,
                effect = aEffect, effectValue = aValue, effect2 = aEffect2, effectValue2 = aValue2
            },
            choiceB = new OpinionChoice
            {
                body = bBody, stance = bStance, knowledge = bKnowledge,
                effect = bEffect, effectValue = bValue, effect2 = bEffect2, effectValue2 = bValue2
            }
        };
    }
}
