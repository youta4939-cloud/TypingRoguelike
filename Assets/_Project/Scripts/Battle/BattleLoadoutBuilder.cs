using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public static class BattleLoadoutBuilder
    {
        public static List<BattleSkillData> BuildActiveSkills(PartyLoadoutDefinition party)
        {
            var list = new List<BattleSkillData>();
            if (party == null || party.members == null)
                return list;

            foreach (var member in party.members)
            {
                if (member.character?.innateSkill != null)
                    list.Add(member.character.innateSkill.ToRuntimeData());

                if (member.equippedCards == null)
                    continue;

                foreach (var card in member.equippedCards)
                {
                    if (card != null)
                        list.Add(card.ToRuntimeData());
                }
            }

            return list;
        }

        public static string[] GetWords(WordListDefinition wordList)
        {
            if (wordList == null || wordList.words == null || wordList.words.Length == 0)
                return CreateFallbackWordPool();
            return wordList.words
                .Concat(CreateExpandedWordPool())
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Where(word => word.Contains("|"))
                .Select(word => word.Trim())
                .Distinct()
                .ToArray();
        }

        public static List<BattleSkillData> CreatePrototypeLoadout()
        {
            var encounter = Resources.Load<BattleEncounterDefinition>("Battle/PrototypeBossEncounter");
            if (encounter?.party != null)
                return BuildActiveSkills(encounter.party);

            Debug.LogWarning("PrototypeBossEncounter が未配置です。Tools > TypingRoguelike > Generate Prototype Data を実行してください。");
            return CreateHardcodedFallbackLoadout();
        }

        public static string[] CreatePrototypeWordPool()
        {
            var encounter = Resources.Load<BattleEncounterDefinition>("Battle/PrototypeBossEncounter");
            if (encounter?.wordList != null)
                return GetWords(encounter.wordList);
            return CreateFallbackWordPool();
        }

        public static List<BattleSkillData> CreateHardcodedFallbackLoadout()
        {
            return new List<BattleSkillData>
            {
                new(0, "アリス", "⚔️二連斬", new[] { ElementColor.Red, ElementColor.Blue }, 28, 0, "28ダメ"),
                new(1, "アリス", "🔥ファイア", new[] { ElementColor.Red, ElementColor.Red, ElementColor.Yellow }, 48, 0, "48ダメ"),
                new(2, "アリス", "💥ヴォルテックス",
                    new[] { ElementColor.Red, ElementColor.Yellow, ElementColor.Purple, ElementColor.Green, ElementColor.Red },
                    98, 0, "98大ダメ"),
                new(3, "エレン", "🍃プチヒール", new[] { ElementColor.Green, ElementColor.Yellow }, 0, 15, "15回復",
                    SkillEffectKind.HealOnly),
                new(4, "エレン", "🌿ヒール", new[] { ElementColor.Green, ElementColor.Green, ElementColor.Blue }, 0, 28,
                    "28回復", SkillEffectKind.HealOnly),
                new(5, "エレン", "✨パーフェクトケア",
                    new[] { ElementColor.Green, ElementColor.Yellow, ElementColor.Green, ElementColor.Purple }, 0, 42,
                    "42回復", SkillEffectKind.HealOnly),
                new(6, "ルナ", "💧ウォータ", new[] { ElementColor.Blue, ElementColor.Purple }, 28, 0, "28ダメ"),
                new(7, "ルナ", "⚡バースト", new[] { ElementColor.Yellow, ElementColor.Yellow, ElementColor.Red }, 58, 0,
                    "58ダメ"),
                new(8, "ルナ", "🌌メテオフォール",
                    new[] { ElementColor.Purple, ElementColor.Purple, ElementColor.Yellow, ElementColor.Blue, ElementColor.Red },
                    105, 0, "105大ダメ")
            };
        }

        public static string[] CreateFallbackWordPool() => CreateExpandedWordPool();

        public static string[] CreateExpandedWordPool() => new[]
        {
            "猫|neko", "犬|inu", "鳥|tori", "狼|ookami", "熊|kuma",
            "空|sora", "星|hoshi", "月|tsuki", "太陽|taiyou", "雲|kumo",
            "雨|ame", "雪|yuki", "風|kaze", "雷|kaminari", "炎|honoo",
            "水|mizu", "氷|koori", "森|mori", "山|yama", "海|umi",
            "剣|tsurugi", "刀|katana", "盾|tate", "弓|yumi", "槍|yari",
            "魔法|mahou", "呪文|jumon", "回復|kaifuku", "攻撃|kougeki", "防御|bougyo",
            "勇者|yuusha", "騎士|kishi", "魔女|majo", "僧侶|souryo", "盗賊|touzoku",
            "竜|ryuu", "悪魔|akuma", "幽霊|yuurei", "骸骨|gaikotsu", "吸血鬼|kyuuketsuki",
            "洞窟|doukutsu", "遺跡|iseki", "墓地|bochi", "神殿|shinden", "塔|tou",
            "宝箱|takarabako", "黄金|ougon", "王冠|oukan", "水晶|suishou", "薬草|yakusou",
            "戦闘|sentou", "勝利|shouri", "危険|kiken", "運命|unmei", "冒険|bouken",
            "斬撃|zangeki", "突撃|totsugeki", "必殺|hissatsu", "連撃|rengeki", "反撃|hangeki",
            "紅蓮|guren", "吹雪|fubuki", "嵐|arashi", "隕石|inseki", "彗星|suisei",
            "暗黒|ankoku", "閃光|senkou", "雷鳴|raimei", "深淵|shinen", "幻影|genei|gennei",
            "希望|kibou", "勇気|yuuki", "友情|yuujou", "記憶|kioku", "奇跡|kiseki",
            "静寂|seijaku", "混沌|konton", "永遠|eien", "瞬間|shunkan", "世界|sekai",
            "小説|shousetsu", "物語|monogatari", "言葉|kotoba", "文字|moji", "入力|nyuuryoku",
            "速度|sokudo", "集中|shuuchuu", "技術|gijutsu", "挑戦|chousen", "達人|tatsujin",
            "火球|kakyuu", "氷結|hyouketsu", "旋風|senpuu", "流星|ryuusei", "治癒|chiyu",
            "守護|shugo", "覚醒|kakusei", "暴走|bousou", "召喚|shoukan", "結界|kekkai",
            "夜明け|yoake", "黄昏|tasogare", "真夜中|mayonaka", "満月|mangetsu", "流れ星|nagareboshi"
        };
    }
}
