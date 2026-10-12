#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public static class PrototypeDataGenerator
    {
        const string Root = "Assets/_Project/Data/Prototype";
        const string ResourcesBattle = "Assets/_Project/Resources/Battle";

        [MenuItem("Tools/TypingRoguelike/Generate Prototype Data")]
        public static void GenerateFromMenu()
        {
            Generate();
            EditorUtility.DisplayDialog("TypingRoguelike", "プロトタイプデータを生成しました。", "OK");
        }

        public static void Generate()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Skills");
            EnsureFolder(Root + "/Characters");
            EnsureFolder(ResourcesBattle);

            var skills = CreateSkills();
            var chars = CreateCharacters(skills);
            var party = CreateParty(chars, skills);
            var words = CreateWordList();
            var enemyBoss = CreateEnemyBoss();
            var enemyNormal = CreateEnemyNormal();
            var enemyElite = CreateEnemyElite();
            var cardPool = CreateCardPool(skills);
            var typingAudio = CreateDefaultTypingAudio();
            var encounterBoss = CreateEncounter(enemyBoss, words, party, cardPool, typingAudio, "PrototypeBossEncounter", "🔥 ボス戦開始！");
            var encounterCombat = CreateEncounter(enemyNormal, words, party, cardPool, typingAudio, "PrototypeCombatEncounter", "⚔ 魔物が現れた！");
            var encounterElite = CreateEncounter(enemyElite, words, party, cardPool, typingAudio, "PrototypeEliteEncounter", "💀 強敵の気配…");
            CreateDungeonRun(encounterCombat, encounterElite, encounterBoss, cardPool);
            CreateMaterialCatalog();
            RelicCsvImporter.Import(RelicCsvImporter.DefaultCsvPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BattleArtGenerator.Generate();
            BattlePortraitImportFixer.Fix();
            BattleUITemplateGenerator.Generate();

            Debug.Log($"[TypingRoguelike] Prototype data ready. Dungeon: {ResourcesBattle}/PrototypeDungeonRun.asset");
        }

        static SkillDefinition[] CreateSkills()
        {
            var list = BuildSkillCardTable();

            var assets = new SkillDefinition[list.Length];
            for (var i = 0; i < list.Length; i++)
            {
                var card = list[i];
                var file = $"{Root}/Skills/Skill_{card.id:D2}_{SafeFile(card.name)}.asset";
                assets[i] = LoadOrCreate<SkillDefinition>(file);
                assets[i].skillId = card.id;
                assets[i].characterName = card.ch;
                assets[i].displayName = card.name;
                assets[i].pattern = card.pat;
                assets[i].damage = card.dmg;
                assets[i].heal = card.heal;
                assets[i].effectText = card.fx;
                assets[i].effectKind = card.kind;
                assets[i].effectValue = card.effectValue;
                assets[i].effectValueInt = card.effectValueInt;
                EditorUtility.SetDirty(assets[i]);
            }

            return assets;
        }

        /// <summary>id 0〜25: 専属＋支援。パターン長が長いほど威力↑。回復は攻撃なし。</summary>
        static SkillCardTemplate[] BuildSkillCardTable() => new[]
        {
            Card(0, "アリス", "⚔️二連斬", new[] { ElementColor.Red, ElementColor.Blue }, 28, 0, "28ダメ"),
            Card(1, "アリス", "🔥ファイア", new[] { ElementColor.Red, ElementColor.Red, ElementColor.Yellow }, 48, 0, "48ダメ"),
            Card(2, "アリス", "💥ヴォルテックス",
                new[] { ElementColor.Red, ElementColor.Yellow, ElementColor.Purple, ElementColor.Green, ElementColor.Red },
                98, 0, "98大ダメ"),
            Card(3, "エレン", "🍃プチヒール", new[] { ElementColor.Green, ElementColor.Yellow }, 0, 15, "15回復",
                SkillEffectKind.HealOnly),
            Card(4, "エレン", "🌿ヒール", new[] { ElementColor.Green, ElementColor.Green, ElementColor.Blue }, 0, 28,
                "28回復", SkillEffectKind.HealOnly),
            Card(5, "エレン", "✨パーフェクトケア",
                new[] { ElementColor.Green, ElementColor.Yellow, ElementColor.Green, ElementColor.Purple }, 0, 42,
                "42回復", SkillEffectKind.HealOnly),
            Card(6, "ルナ", "💧ウォータ", new[] { ElementColor.Blue, ElementColor.Purple }, 28, 0, "28ダメ"),
            Card(7, "ルナ", "⚡バースト", new[] { ElementColor.Yellow, ElementColor.Yellow, ElementColor.Red }, 58, 0,
                "58ダメ"),
            Card(8, "ルナ", "🌌メテオフォール",
                new[] { ElementColor.Purple, ElementColor.Purple, ElementColor.Yellow, ElementColor.Blue, ElementColor.Red },
                105, 0, "105大ダメ"),
            Card(9, "アリス", "🗡️赤撃", new[] { ElementColor.Red, ElementColor.Red }, 30, 0, "30ダメ"),
            Card(10, "アリス", "🌀旋風", new[] { ElementColor.Yellow, ElementColor.Red, ElementColor.Green }, 52, 0,
                "52ダメ"),
            Card(11, "アリス", "🛡️カウンター", new[] { ElementColor.Blue, ElementColor.Red, ElementColor.Blue }, 44, 0,
                "44ダメ"),
            Card(12, "アリス", "☄️クラッシュ", new[] { ElementColor.Purple, ElementColor.Red, ElementColor.Red }, 72, 0,
                "72ダメ"),
            Card(13, "アリス", "🌪️ソードストーム",
                new[] { ElementColor.Red, ElementColor.Yellow, ElementColor.Blue, ElementColor.Red }, 68, 0, "68ダメ"),
            Card(14, "エレン", "💚ライフ", new[] { ElementColor.Green, ElementColor.Blue }, 0, 14, "14回復",
                SkillEffectKind.HealOnly),
            Card(15, "エレン", "🌸再生", new[] { ElementColor.Green, ElementColor.Purple, ElementColor.Yellow }, 0, 26,
                "26回復", SkillEffectKind.HealOnly),
            Card(16, "エレン", "🫧バリア", new[] { ElementColor.Blue, ElementColor.Green, ElementColor.Green }, 0, 0,
                "敵攻撃50%減・10秒", SkillEffectKind.EnemyDelay, 10f, effectValueInt: 50),
            Card(17, "エレン", "🌈オーバーヒール",
                new[] { ElementColor.Yellow, ElementColor.Green, ElementColor.Blue, ElementColor.Green }, 0, 40,
                "40回復", SkillEffectKind.HealOnly),
            Card(18, "ルナ", "❄️アイス", new[] { ElementColor.Blue, ElementColor.Blue }, 26, 0, "26ダメ"),
            Card(19, "ルナ", "🔮ミラー", new[] { ElementColor.Purple, ElementColor.Blue, ElementColor.Yellow }, 48, 0,
                "48ダメ"),
            Card(20, "ルナ", "⚡サンダー", new[] { ElementColor.Yellow, ElementColor.Purple, ElementColor.Red }, 54, 0,
                "54ダメ"),
            Card(21, "ルナ", "🌠コメット", new[] { ElementColor.Blue, ElementColor.Yellow, ElementColor.Purple, ElementColor.Red },
                76, 0, "76ダメ"),
            Card(22, "ルナ", "🌙ムーンライト", new[] { ElementColor.Purple, ElementColor.Green, ElementColor.Blue }, 12, 0,
                "12ダメ+敵攻撃50%減10秒", SkillEffectKind.EnemyDelay, 10f, effectValueInt: 50),
            Card(23, "アリス", "🔱デュアル", new[] { ElementColor.Red, ElementColor.Purple, ElementColor.Yellow }, 50, 0,
                "50ダメ"),
            Card(24, "エレン", "📜詠唱", new[] { ElementColor.Green, ElementColor.Blue }, 12, 0, "12ダメ+リロール+1",
                SkillEffectKind.RerollRestore, effectValueInt: 1),
            Card(25, "ルナ", "💫スターレイン",
                new[] { ElementColor.Yellow, ElementColor.Blue, ElementColor.Purple, ElementColor.Yellow, ElementColor.Red },
                100, 0, "100大ダメ"),
            Card(26, "アリス", "裂空斬", new[] { ElementColor.Red, ElementColor.Green, ElementColor.Red }, 40, 0, "40ダメ"),
            Card(27, "アリス", "血祭", new[] { ElementColor.Red, ElementColor.Purple, ElementColor.Red }, 36, 12, "36ダメ+12回復"),
            Card(28, "アリス", "号令", new[] { ElementColor.Yellow, ElementColor.Red, ElementColor.Blue }, 22, 0, "22ダメ+リロール+1",
                SkillEffectKind.RerollRestore, effectValueInt: 1),
            Card(29, "エレン", "応急", new[] { ElementColor.Green, ElementColor.Blue, ElementColor.Yellow }, 0, 22, "22回復",
                SkillEffectKind.HealOnly),
            Card(30, "エレン", "清光", new[] { ElementColor.Green, ElementColor.Purple, ElementColor.Green }, 0, 32, "32回復",
                SkillEffectKind.HealOnly),
            Card(31, "エレン", "守護", new[] { ElementColor.Blue, ElementColor.Green, ElementColor.Yellow }, 0, 0, "敵攻撃40%減・8秒",
                SkillEffectKind.EnemyDelay, 8f, effectValueInt: 40),
            Card(32, "ルナ", "霜牙", new[] { ElementColor.Blue, ElementColor.Blue, ElementColor.Purple }, 36, 0, "36ダメ"),
            Card(33, "ルナ", "星屑", new[] { ElementColor.Yellow, ElementColor.Purple, ElementColor.Blue }, 44, 0, "44ダメ"),
            Card(34, "ルナ", "月蝕", new[] { ElementColor.Purple, ElementColor.Red, ElementColor.Yellow, ElementColor.Purple }, 70, 0, "70ダメ")
        };

        static CharacterDefinition[] CreateCharacters(SkillDefinition[] skills)
        {
            var alice = LoadOrCreate<CharacterDefinition>($"{Root}/Characters/Char_Alice.asset");
            alice.characterName = "アリス";
            alice.roleLabel = "アタッカー";
            alice.attack = 7;
            alice.defense = 3;
            alice.luck = 2;
            alice.innateSkill = skills[0];
            alice.explorationPassive = ExplorationPassiveKind.Cautious;
            alice.explorationPassiveValue = 5;
            EditorUtility.SetDirty(alice);

            var ellen = LoadOrCreate<CharacterDefinition>($"{Root}/Characters/Char_Ellen.asset");
            ellen.characterName = "エレン";
            ellen.roleLabel = "ヒーラー";
            ellen.attack = 3;
            ellen.defense = 6;
            ellen.luck = 2;
            ellen.innateSkill = skills[3];
            ellen.explorationPassive = ExplorationPassiveKind.Pathfinder;
            EditorUtility.SetDirty(ellen);

            var luna = LoadOrCreate<CharacterDefinition>($"{Root}/Characters/Char_Luna.asset");
            luna.characterName = "ルナ";
            luna.roleLabel = "メイジ";
            luna.attack = 5;
            luna.defense = 3;
            luna.luck = 8;
            luna.innateSkill = skills[6];
            luna.explorationPassive = ExplorationPassiveKind.DangerSense;
            EditorUtility.SetDirty(luna);

            return new[] { alice, ellen, luna };
        }

        static void CreateMaterialCatalog()
        {
            var catalog = LoadOrCreate<MaterialCatalogDefinition>($"{Root}/MaterialCatalog_Prototype.asset");
            catalog.materials = new[]
            {
                CreateMaterial(1, "狼の牙", 1, 10, 12),
                CreateMaterial(2, "薬草", 1, 10, 12),
                CreateMaterial(3, "銅鉱石", 1, 10, 10),
                CreateMaterial(4, "ゴブリンの角", 11, 20, 10),
                CreateMaterial(5, "黒鉄", 11, 20, 10),
                CreateMaterial(6, "アンデッドの骨", 11, 20, 8),
                CreateMaterial(7, "サラマンダーの鱗", 21, 30, 8),
                CreateMaterial(8, "魔晶", 21, 99, 8),
                CreateMaterial(9, "呪木", 21, 99, 6),
                CreateMaterial(900, "未分類素材", 1, 99, 1)
            };
            EditorUtility.SetDirty(catalog);

            var resourcePath = $"{ResourcesBattle}/PrototypeMaterialCatalog.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MaterialCatalogDefinition>(resourcePath);
            if (existing == null)
                AssetDatabase.CopyAsset($"{Root}/MaterialCatalog_Prototype.asset", resourcePath);
            else
            {
                existing.materials = catalog.materials;
                EditorUtility.SetDirty(existing);
            }
        }

        static MaterialDefinition CreateMaterial(int id, string name, int floorMin, int floorMax, int weight)
        {
            var path = $"{Root}/Materials/Mat_{id}_{name}.asset";
            EnsureFolder(Root + "/Materials");
            var mat = LoadOrCreate<MaterialDefinition>(path);
            mat.materialId = id;
            mat.displayName = name;
            mat.floorMin = floorMin;
            mat.floorMax = floorMax;
            mat.pickWeight = weight;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static PartyLoadoutDefinition CreateParty(CharacterDefinition[] chars, SkillDefinition[] skills)
        {
            var party = LoadOrCreate<PartyLoadoutDefinition>($"{Root}/Party_Prototype.asset");
            party.members = new[]
            {
                new PartyMemberLoadout
                {
                    character = chars[0],
                    equippedCards = new[] { skills[1], skills[2] }
                },
                new PartyMemberLoadout
                {
                    character = chars[1],
                    equippedCards = new[] { skills[4], skills[5] }
                },
                new PartyMemberLoadout
                {
                    character = chars[2],
                    equippedCards = new[] { skills[7], skills[8] }
                }
            };
            EditorUtility.SetDirty(party);
            return party;
        }

        static WordListDefinition CreateWordList()
        {
            var wl = LoadOrCreate<WordListDefinition>($"{Root}/WordList_EasyEnglish.asset");
            wl.words = BattleLoadoutBuilder.CreateExpandedWordPool();
            EditorUtility.SetDirty(wl);
            return wl;
        }

        static EnemyDefinition CreateEnemyBoss()
        {
            var e = LoadOrCreate<EnemyDefinition>($"{Root}/Enemy_PrototypeBoss.asset");
            e.displayName = "BOSS";
            e.emojiNormal = "🐲";
            e.emojiEnraged = "🔥🐲";
            e.emojiDefeated = "💀";
            e.maxHp = 1000f;
            e.attackDamage = 25f;
            e.attackTimerMax = 100f;
            e.attackSpeed = 0.35f;
            e.enragedAttackSpeed = 0.55f;
            e.enrageHpFraction = 0.5f;
            EditorUtility.SetDirty(e);
            return e;
        }

        static EnemyDefinition CreateEnemyNormal()
        {
            var e = LoadOrCreate<EnemyDefinition>($"{Root}/Enemy_PrototypeNormal.asset");
            e.displayName = "魔物";
            e.emojiNormal = "👾";
            e.emojiEnraged = "😈";
            e.emojiDefeated = "💨";
            e.maxHp = 260f;
            e.attackDamage = 18f;
            e.attackTimerMax = 100f;
            e.attackSpeed = 0.32f;
            e.enragedAttackSpeed = 0.48f;
            e.enrageHpFraction = 0.45f;
            EditorUtility.SetDirty(e);
            return e;
        }

        static EnemyDefinition CreateEnemyElite()
        {
            var e = LoadOrCreate<EnemyDefinition>($"{Root}/Enemy_PrototypeElite.asset");
            e.displayName = "強敵";
            e.emojiNormal = "🦂";
            e.emojiEnraged = "☠️";
            e.emojiDefeated = "💀";
            e.maxHp = 380f;
            e.attackDamage = 22f;
            e.attackTimerMax = 100f;
            e.attackSpeed = 0.38f;
            e.enragedAttackSpeed = 0.58f;
            e.enrageHpFraction = 0.5f;
            EditorUtility.SetDirty(e);
            return e;
        }

        static SkillCardPoolDefinition CreateCardPool(SkillDefinition[] skills)
        {
            var pool = LoadOrCreate<SkillCardPoolDefinition>($"{ResourcesBattle}/PrototypeRewardCardPool.asset");
            var reward = new List<SkillDefinition>();
            foreach (var s in skills)
            {
                if (s == null)
                    continue;
                if (s.skillId is 0 or 3 or 6)
                    continue;
                reward.Add(s);
            }

            pool.cards = reward.ToArray();
            EditorUtility.SetDirty(pool);
            return pool;
        }

        static TypingAudioDefinition CreateDefaultTypingAudio()
        {
            EnsureFolder(ResourcesBattle + "/Audio");
            var audio = LoadOrCreate<TypingAudioDefinition>($"{ResourcesBattle}/Audio/DefaultTypingAudio.asset");
            audio.useProceduralFallback = true;
            audio.correctVolume = 0.35f;
            audio.missVolume = 0.45f;
            audio.completeVolume = 0.5f;
            EditorUtility.SetDirty(audio);
            return audio;
        }

        static BattleEncounterDefinition CreateEncounter(
            EnemyDefinition enemy,
            WordListDefinition words,
            PartyLoadoutDefinition party,
            SkillCardPoolDefinition cardPool,
            TypingAudioDefinition typingAudio,
            string resourceFileName,
            string startLog)
        {
            var enc = LoadOrCreate<BattleEncounterDefinition>($"{ResourcesBattle}/{resourceFileName}.asset");
            enc.enemy = enemy;
            enc.wordList = words;
            enc.party = party;
            enc.rewardCardPool = cardPool;
            enc.typingAudio = typingAudio;
            enc.rewardChoiceCount = 3;
            enc.playerMaxHp = 100f;
            enc.wordClearDamage = 15f;
            enc.rerollsPerBattle = 3;
            enc.enemyTickInterval = 0.05f;
            enc.jackpotMultiplier = resourceFileName.Contains("Boss") ? 2.5f : 2f;
            enc.baseFreezeChance = 0f;
            enc.freezeChancePerCharacter = 0.000025f;
            enc.baseJackpotChanceCap = 0.1f;
            enc.maxFreezeChance = 0.99f;
            enc.startLogMessage = startLog;
            EditorUtility.SetDirty(enc);
            return enc;
        }

        internal static void EnsureFolderPublic(string path) => EnsureFolder(path);

        internal static string SafeFileNamePublic(string name) => SafeFile(name);

        internal static T LoadOrCreatePublic<T>(string path) where T : ScriptableObject => LoadOrCreate<T>(path);

        static void CreateDungeonRun(
            BattleEncounterDefinition combat,
            BattleEncounterDefinition elite,
            BattleEncounterDefinition boss,
            SkillCardPoolDefinition pool)
        {
            var run = LoadOrCreate<DungeonRunDefinition>($"{ResourcesBattle}/PrototypeDungeonRun.asset");
            run.combatEncounter = combat;
            run.eliteEncounter = elite;
            run.bossEncounter = boss;
            run.sharedRewardPool = pool;
            run.towerFloors = 15;
            run.pathLayers = 6;
            run.branchWidth = 3;
            run.restHealFraction = 0.15f;
            run.restNodesPerFloorMin = 1;
            run.restNodesPerFloorMax = 2;
            run.combatEnemyHpMultiplier = 0.72f;
            run.partyAttackCoefficient = 1f;
            run.partyDefenseCoefficient = 0.5f;
            run.partyLuckJackpotBonusPerPoint = 0.0005f;
            run.combatCoinReward = 8;
            run.eliteCoinReward = 15;
            run.treasureCoinReward = 10;
            run.merchantHealCost = 10;
            run.merchantHealFraction = 0.12f;
            run.merchantCardCost = 22;
            run.rewardChoiceCount = 3;
            run.eliteEnemyHpMultiplier = 1.35f;
            run.eliteEnemyDamageMultiplier = 1.15f;
            EditorUtility.SetDirty(run);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent!, name);
        }

        readonly struct SkillCardTemplate
        {
            public readonly int id;
            public readonly string ch;
            public readonly string name;
            public readonly ElementColor[] pat;
            public readonly int dmg;
            public readonly int heal;
            public readonly string fx;
            public readonly SkillEffectKind kind;
            public readonly float effectValue;
            public readonly int effectValueInt;

            public SkillCardTemplate(
                int id,
                string ch,
                string name,
                ElementColor[] pat,
                int dmg,
                int heal,
                string fx,
                SkillEffectKind kind = SkillEffectKind.None,
                float effectValue = 0f,
                int effectValueInt = 0)
            {
                this.id = id;
                this.ch = ch;
                this.name = name;
                this.pat = pat;
                this.dmg = dmg;
                this.heal = heal;
                this.fx = fx;
                this.kind = kind;
                this.effectValue = effectValue;
                this.effectValueInt = effectValueInt;
            }
        }

        static SkillCardTemplate Card(
            int id,
            string ch,
            string name,
            ElementColor[] pat,
            int dmg,
            int heal,
            string fx,
            SkillEffectKind kind = SkillEffectKind.None,
            float effectValue = 0f,
            int effectValueInt = 0) =>
            new(id, ch, name, pat, dmg, heal, fx, kind, effectValue, effectValueInt);

        static string SafeFile(string displayName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                displayName = displayName.Replace(c, '_');
            return displayName.Replace(" ", "_");
        }
    }

    [InitializeOnLoad]
    static class PrototypeDataAutoBootstrap
    {
        static PrototypeDataAutoBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                const string encounterPath = "Assets/_Project/Resources/Battle/PrototypeBossEncounter.asset";
                const string dungeonPath = "Assets/_Project/Resources/Battle/PrototypeDungeonRun.asset";
                if (File.Exists(encounterPath) && File.Exists(dungeonPath))
                    return;
                PrototypeDataGenerator.Generate();
            };
        }
    }
}
#endif
