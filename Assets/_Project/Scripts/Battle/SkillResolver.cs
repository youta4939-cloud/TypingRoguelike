using System.Collections.Generic;
using System.Linq;

namespace TypingRoguelike.Battle
{
    public static class SkillResolver
    {
        public static List<SkillCandidate> GetCandidates(
            IReadOnlyList<BattleSkillData> skills,
            OrbStock stock,
            IReadOnlyCollection<int> cooldownSkillIds)
        {
            var cooldown = cooldownSkillIds ?? System.Array.Empty<int>();
            var candidates = new List<SkillCandidate>();
            foreach (var skill in skills)
            {
                var patternLen = skill.Pattern.Length;
                if (patternLen == 0)
                    continue;

                if (TrySuffixMatch(stock, skill.Pattern, patternLen, out _))
                {
                    var onCooldown = cooldown.Contains(skill.Id);
                    candidates.Add(new SkillCandidate(
                        skill,
                        skill.Pattern[patternLen - 1],
                        isPatternComplete: true,
                        isOnCooldown: onCooldown));
                    continue;
                }

                if (cooldown.Contains(skill.Id))
                    continue;

                var needLen = patternLen - 1;
                if (needLen <= 0)
                    continue;

                if (!stock.TryGetSuffix(needLen, out var suffix))
                    continue;

                var match = true;
                for (var i = 0; i < needLen; i++)
                {
                    if (suffix[i] != skill.Pattern[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    candidates.Add(new SkillCandidate(skill, skill.Pattern[patternLen - 1]));
            }

            return candidates;
        }

        /// <summary>末尾一致したスキルをすべて返す（包含関係の短いパターンも同時発動）。</summary>
        public static List<BattleSkillData> ResolveTriggeredSkills(
            IReadOnlyList<BattleSkillData> skills,
            OrbStock stock,
            IReadOnlyCollection<int> cooldownSkillIds,
            out List<BattleSkillData> blockedByCooldown)
        {
            blockedByCooldown = new List<BattleSkillData>();
            var cooldown = cooldownSkillIds ?? System.Array.Empty<int>();
            var ready = new List<BattleSkillData>();

            foreach (var skill in skills)
            {
                var patternLen = skill.Pattern.Length;
                if (patternLen == 0)
                    continue;

                if (!TrySuffixMatch(stock, skill.Pattern, patternLen, out _))
                    continue;

                if (cooldown.Contains(skill.Id))
                {
                    blockedByCooldown.Add(skill);
                    continue;
                }

                ready.Add(skill);
            }

            return ready
                .OrderByDescending(s => s.Pattern.Length)
                .ThenBy(s => s.Id)
                .ToList();
        }

        public static BattleSkillData TryResolveTriggeredSkill(
            IReadOnlyList<BattleSkillData> skills,
            OrbStock stock,
            int? lastUsedSkillId,
            out BattleSkillData blockedByCooldown)
        {
            var cooldown = lastUsedSkillId.HasValue
                ? new[] { lastUsedSkillId.Value }
                : System.Array.Empty<int>();
            var list = ResolveTriggeredSkills(skills, stock, cooldown, out var blocked);
            blockedByCooldown = blocked.Count > 0 ? blocked[0] : null;
            return list.Count > 0 ? list[0] : null;
        }

        public static BattleSkillData TryResolveTriggeredSkill(
            IReadOnlyList<BattleSkillData> skills,
            OrbStock stock,
            int? lastUsedSkillId) =>
            TryResolveTriggeredSkill(skills, stock, lastUsedSkillId, out _);

        static bool TrySuffixMatch(OrbStock stock, ElementColor[] pattern, int patternLen, out ElementColor[] suffix)
        {
            suffix = null;
            if (!stock.TryGetSuffix(patternLen, out suffix))
                return false;

            for (var i = 0; i < patternLen; i++)
            {
                if (suffix[i] != pattern[i])
                    return false;
            }

            return true;
        }
    }
}
