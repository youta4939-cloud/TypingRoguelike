using System;
using System.Collections.Generic;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    [Serializable]
    public sealed class PersonalityWeightSave
    {
        public string id = "";
        public int w_attack;
        public int w_careful;
        public int w_explore;
    }

    public sealed class PersonalityChartView
    {
        public string Id = "";
        public string Name = "";
        public int Affinity;
        public string[] AxisNames = Array.Empty<string>();
        public int[] Stages = Array.Empty<int>();
        public int StageMax = 5;
    }

    /// <summary>Resources/Data/personality.json。数値の初期値は持たず、表が無いときは何もしない。</summary>
    public static class PersonalityCatalog
    {
        const string ResourcePath = "Data/personality";
        static PersonalityFile _file;
        static bool _warned;

        public static PersonalityFile File => Load();

        public static int AgreeAffinity => Load()?.agreeAffinity ?? 0;

        public static int[] DebugAffinity
        {
            get
            {
                var values = Load()?.debugAffinity;
                return values != null ? (int[])values.Clone() : Array.Empty<int>();
            }
        }

        public static PersonalityCharacter[] Characters
        {
            get
            {
                var list = Load()?.characters;
                return list ?? Array.Empty<PersonalityCharacter>();
            }
        }

        public static string DisplayName(string id)
        {
            if (StarterCharacters.IsStarter(id))
                return StarterCharacters.Shown(id);
            var character = Find(id);
            if (character != null && !string.IsNullOrEmpty(character.name))
                return character.name;
            return id ?? "";
        }

        public static PersonalityCharacter Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var list = Characters;
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i] != null && StarterCharacters.Same(list[i].id, id))
                    return list[i];
            }

            return null;
        }

        public static void NotifyPassive1(string name, int before, int after)
        {
            var file = Load();
            if (file == null || string.IsNullOrEmpty(file.passive1Message))
                return;
            if (before >= file.passive1Threshold || after < file.passive1Threshold)
                return;
            Debug.Log(file.passive1Message.Replace("{name}", name ?? ""));
        }

        public static void RollMood(MetaProgressData profile, System.Random rng)
        {
            if (profile == null)
                return;
            var file = Load();
            profile.personalityWeights ??= new List<PersonalityWeightSave>();
            profile.personalityWeights.Clear();
            profile.personalityMoodActive = false;
            if (file?.characters == null)
                return;

            profile.personalityMoodActive = true;
            for (var i = 0; i < file.characters.Length; i++)
            {
                var character = file.characters[i];
                if (character == null || string.IsNullOrEmpty(character.id))
                    continue;
                var attack = character.w_attack;
                var careful = character.w_careful;
                var explore = character.w_explore;
                if (character.mood_random && file.mood != null && rng != null)
                {
                    var pick = rng.Next(3);
                    attack = pick == 0 ? file.mood.high : file.mood.low;
                    careful = pick == 1 ? file.mood.high : file.mood.low;
                    explore = pick == 2 ? file.mood.high : file.mood.low;
                }

                profile.personalityWeights.Add(new PersonalityWeightSave
                {
                    id = character.id,
                    w_attack = attack,
                    w_careful = careful,
                    w_explore = explore
                });
            }
        }

        public static void ClearMood(MetaProgressData profile)
        {
            if (profile == null)
                return;
            profile.personalityMoodActive = false;
            profile.personalityWeights?.Clear();
        }

        public static int Weight(MetaProgressData profile, PersonalityCharacter character, string tag)
        {
            var attack = character != null ? character.w_attack : 0;
            var careful = character != null ? character.w_careful : 0;
            var explore = character != null ? character.w_explore : 0;
            if (profile != null && profile.personalityMoodActive && profile.personalityWeights != null && character != null)
            {
                for (var i = 0; i < profile.personalityWeights.Count; i++)
                {
                    var saved = profile.personalityWeights[i];
                    if (saved == null || !StarterCharacters.Same(saved.id, character.id))
                        continue;
                    if (StarterCharacters.IsStarter(character.id))
                        saved.id = StarterCharacters.CanonicalId(character.id);
                    attack = saved.w_attack;
                    careful = saved.w_careful;
                    explore = saved.w_explore;
                    break;
                }
            }

            if (tag == "careful")
                return careful;
            if (tag == "explore")
                return explore;
            return attack;
        }

        public static string TagOf(EventOption option)
        {
            var file = Load();
            var fallback = file != null && !string.IsNullOrEmpty(file.defaultTag) ? file.defaultTag : "";
            if (option == null || file == null)
                return fallback;

            var tagged = MatchAlias(file, option.tag);
            if (!string.IsNullOrEmpty(tagged))
                return tagged;

            var rules = file.tagRules;
            if (rules == null)
                return fallback;
            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || string.IsNullOrEmpty(rule.tag))
                    continue;
                var field = rule.when == "knowledge" ? option.knowledge : option.stance;
                if (field == rule.equals)
                    return rule.tag;
            }

            return fallback;
        }

        public static string PickLine(string[] lines, System.Random rng)
        {
            if (lines == null || lines.Length == 0 || rng == null)
                return "";
            var count = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    count++;
            }

            if (count == 0)
                return "";
            var pick = rng.Next(count);
            for (var i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                if (pick == 0)
                    return lines[i];
                pick--;
            }

            return "";
        }

        public static int AffinityOf(MetaProgressData profile, string id)
        {
            if (profile?.characterTrust == null || string.IsNullOrEmpty(id))
                return 0;
            for (var i = 0; i < profile.characterTrust.Count; i++)
            {
                var save = profile.characterTrust[i];
                if (save != null && StarterCharacters.Same(save.characterId, id))
                    return save.trust;
            }

            return 0;
        }

        public static PersonalityChartView[] BuildCharts(MetaProgressData profile)
        {
            var file = Load();
            if (file?.characters == null || file.axes == null || file.chart == null || file.chart.stages < 2)
                return Array.Empty<PersonalityChartView>();

            var charts = new List<PersonalityChartView>();
            for (var i = 0; i < file.characters.Length; i++)
            {
                var character = file.characters[i];
                if (character == null || string.IsNullOrEmpty(character.id))
                    continue;
                var names = new string[file.axes.Length];
                var stages = new int[file.axes.Length];
                for (var a = 0; a < file.axes.Length; a++)
                {
                    var axis = file.axes[a];
                    names[a] = axis != null ? axis.name : "";
                    stages[a] = Stage(file, profile, character, axis != null ? axis.source : "");
                }

                charts.Add(new PersonalityChartView
                {
                    Id = character.id,
                    Name = StarterCharacters.Shown(character.id),
                    Affinity = AffinityOf(profile, character.id),
                    AxisNames = names,
                    Stages = stages,
                    StageMax = file.chart.stages
                });
            }

            return charts.ToArray();
        }

        static int Stage(PersonalityFile file, MetaProgressData profile, PersonalityCharacter character, string source)
        {
            var stages = file.chart.stages;
            if (source == "talk")
                return ScalePercent(character.talk, file.chart.percentMax, stages);
            if (source == "yieldRate")
                return ScalePercent(character.yieldRate, file.chart.percentMax, stages);
            var weight = Weight(profile, character, source == "w_careful" ? "careful" : source == "w_explore" ? "explore" : "attack");
            return ScaleWeight(weight, file.chart.weightMin, file.chart.weightMax, stages);
        }

        static int ScalePercent(int value, int max, int stages)
        {
            if (stages < 2)
                return 1;
            if (max <= 0)
                return 1;
            var t = Mathf.Clamp01(value / (float)max);
            return Mathf.Clamp(Mathf.RoundToInt(1f + t * (stages - 1)), 1, stages);
        }

        static int ScaleWeight(int value, int min, int max, int stages)
        {
            if (stages < 2)
                return 1;
            if (max <= min)
                return Mathf.Clamp(value, 1, stages);
            var t = Mathf.Clamp01((value - min) / (float)(max - min));
            return Mathf.Clamp(Mathf.RoundToInt(1f + t * (stages - 1)), 1, stages);
        }

        static string MatchAlias(PersonalityFile file, string raw)
        {
            if (string.IsNullOrEmpty(raw) || file.tagAliases == null)
                return "";
            for (var i = 0; i < file.tagAliases.Length; i++)
            {
                var alias = file.tagAliases[i];
                if (alias == null || string.IsNullOrEmpty(alias.tag) || alias.names == null)
                    continue;
                for (var n = 0; n < alias.names.Length; n++)
                {
                    if (alias.names[n] == raw)
                        return alias.tag;
                }
            }

            return "";
        }

        static PersonalityFile Load()
        {
            if (_file != null)
                return _file;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("personality.json を読めなかった。");
                }

                return null;
            }

            _file = JsonUtility.FromJson<PersonalityFile>(asset.text);
            if (_file == null && !_warned)
            {
                _warned = true;
                Debug.LogWarning("personality.json の形式が読めなかった。");
            }

            return _file;
        }
    }
}
