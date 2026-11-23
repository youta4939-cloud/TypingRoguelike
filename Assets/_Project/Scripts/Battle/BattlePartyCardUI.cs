using System.Collections.Generic;
using TMPro;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>戦闘左列。血しぶきの上の円形肖像と、装備スキル3段。</summary>
    public static class BattlePartyCardUI
    {
        public static readonly Color SkillBand = BattleHudKit.Band;
        public static readonly Color SkillBandCooldown = new(0.03f, 0.03f, 0.035f, 0.5f);

        public const float ColumnWidth = 472f;
        public const float MemberHeight = 214f;
        const float MemberGap = 6f;
        const float PortraitCenterX = 80f;
        const float PortraitSize = 132f;
        const float RowLeft = 170f;
        const float RowHeight = 64f;
        const float RowGap = 7f;

        static readonly string[] Roster = { "アリス", "エレン", "ルナ" };

        public sealed class SkillRowView
        {
            public BattleSkillData Skill;
            public Image Band;
            public Image Border;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Effect;
            public BattleHudKit.DotsView Dots;
            public GameObject Ready;
            public CanvasGroup Group;
            public BattleE5bRow E5b;
        }

        public static void Build(
            Transform partyColumn,
            BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills,
            HashSet<int> innateSkillIds,
            BattleVisualContext visuals)
        {
            _ = innateSkillIds;
            for (var i = 0; i < Roster.Length; i++)
                BuildMember(partyColumn, refs, skills, visuals, Roster[i], i);
        }

        static void BuildMember(
            Transform parent,
            BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills,
            BattleVisualContext visuals,
            string name,
            int index)
        {
            visuals.Characters.TryGetValue(name, out var visual);
            var block = BattleHudKit.Place(parent, $"Char_{name}", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -index * (MemberHeight + MemberGap)), new Vector2(ColumnWidth, MemberHeight));
            refs.CharCards[name] = block;

            var splat = BattleHudKit.Place(block, "BloodSplatter", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PortraitCenterX, 0f), new Vector2(220f, 224f));
            BattleHudKit.AddImage(splat, BattleHudKit.Art("BloodSplatter"), new Color(0.85f, 0.85f, 0.85f, 0.92f))
                .preserveAspect = true;
            splat.localRotation = Quaternion.Euler(0f, 0f, 35f + index * 110f);

            refs.CharPortraits[name] = BattleHudKit.CirclePortrait(block, "Portrait", visual?.Portrait, PortraitSize,
                new Vector2(0f, 0.5f), new Vector2(PortraitCenterX, 0f), new Color(0f, 0f, 0f, 0.85f), 3f);

            var nameTag = BattleHudKit.Text(block, "CharName", StarterCharacters.Shown(name), BattleHudKit.Mincho, 17f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(PortraitCenterX - 60f, 2f), new Vector2(PortraitCenterX + 60f, 26f));
            BattleHudKit.Outline(nameTag, 0.25f, Color.black);

            var owned = new List<BattleSkillData>();
            if (skills != null)
            {
                foreach (var skill in skills)
                {
                    if (StarterCharacters.Same(skill.CharacterName, name))
                        owned.Add(skill);
                }
            }

            var total = 3 * RowHeight + 2 * RowGap;
            var top = -(MemberHeight - total) * 0.5f;
            for (var i = 0; i < owned.Count && i < 3; i++)
                BuildSkillRow(block, refs, owned[i], top - i * (RowHeight + RowGap));

            if (index < Roster.Length - 1)
            {
                BattleHudKit.Img(block, "Separator", BattleHudKit.FadeRight, BattleHudKit.Separator,
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(RowLeft, -MemberGap * 0.5f - 1f), new Vector2(0f, -MemberGap * 0.5f));
            }
        }

        static void BuildSkillRow(RectTransform block, BattleUIRefs refs, BattleSkillData skill, float topY)
        {
            var row = BattleHudKit.Rect(block, $"Skill_{skill.Id}", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(RowLeft, topY - RowHeight), new Vector2(0f, topY));
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var band = BattleHudKit.AddImage(row, BattleUISprites.Solid, SkillBand);
            refs.SkillRowGraphics[skill.Id] = band;

            // 右端にスキル絵をうっすら敷く。
            var artClip = BattleHudKit.Rect(row, "ArtClip", new Vector2(0.38f, 0f), new Vector2(1f, 1f));
            artClip.gameObject.AddComponent<RectMask2D>();
            var artSprite = BattleHudKit.SkillArt(skill, out var tint);
            if (artSprite != null)
            {
                var art = BattleHudKit.Place(artClip, "Art", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    Vector2.zero, new Vector2(200f, 200f * artSprite.rect.height / Mathf.Max(1f, artSprite.rect.width)));
                BattleHudKit.AddImage(art, artSprite, new Color(tint.r, tint.g, tint.b, 0.62f));
            }

            BattleHudKit.Img(artClip, "ArtFade", BattleHudKit.FadeRight, SkillBand, Vector2.zero, new Vector2(0.75f, 1f));

            var border = BattleHudKit.Img(row, "ReadyBorder", BattleHudKit.FrameThin, BattleHudKit.Gold,
                Vector2.zero, Vector2.one);
            border.enabled = false;

            var nameText = BattleHudKit.Text(row, "SkillName", skill.DisplayName, BattleHudKit.Mincho, 23f,
                Color.white, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0.46f), new Vector2(1f, 1f),
                new Vector2(12f, 0f), new Vector2(-96f, -4f));
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 15f;
            nameText.fontSizeMax = 23f;
            BattleHudKit.Outline(nameText, 0.22f, new Color(0f, 0f, 0f, 0.95f));

            var effect = BattleHudKit.Text(row, "Effect", ShortEffect(skill), BattleHudKit.Gothic, 14f,
                BattleHudKit.TextSoft, TextAlignmentOptions.TopLeft, new Vector2(0f, 0f), new Vector2(1f, 0.46f),
                new Vector2(13f, 4f), new Vector2(-96f, -1f));
            effect.overflowMode = TextOverflowModes.Ellipsis;
            BattleHudKit.Outline(effect, 0.12f, new Color(0f, 0f, 0f, 0.9f), 0.5f);

            var dots = BattleHudKit.Dots(row, skill.Pattern, 15f, 4f, new Vector2(-8f, -6f));
            BattleHudKit.SetDots(dots, 0);
            refs.SkillPatternOrbHosts[skill.Id] = dots.Root;

            var ready = BattleHudKit.Text(row, "Ready", "READY", BattleHudKit.Condensed, 15f, BattleHudKit.Gold,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-90f, -20f), new Vector2(-10f, -2f));
            ready.characterSpacing = 8f;
            BattleHudKit.Outline(ready, 0.2f, Color.black);
            ready.gameObject.SetActive(false);
            refs.SkillReadyMarks[skill.Id] = ready.gameObject;

            refs.SkillRows[skill.Id] = new SkillRowView
            {
                Skill = skill,
                Band = band,
                Border = border,
                Name = nameText,
                Effect = effect,
                Dots = dots,
                Ready = ready.gameObject,
                Group = group
            };
        }

        /// <summary>ストック進捗・発動可・クールダウンを各行へ反映する。</summary>
        public static void Refresh(BattleUIRefs refs, OrbStock stock, HashSet<int> readyIds,
            ICollection<int> cooldownIds)
        {
            if (refs?.SkillRows == null)
                return;

            var queue = new List<int>();
            foreach (var kv in refs.SkillRows)
            {
                if (kv.Value?.E5b == null || kv.Value.Skill?.Pattern == null)
                    continue;
                if (cooldownIds != null && cooldownIds.Contains(kv.Key))
                    continue;
                queue.Add(kv.Key);
            }

            queue.Sort((a, b) =>
            {
                var sa = refs.SkillRows[a].Skill;
                var sb = refs.SkillRows[b].Skill;
                var ma = BattleHudKit.MatchedPrefix(stock, sa.Pattern);
                var mb = BattleHudKit.MatchedPrefix(stock, sb.Pattern);
                var byMatch = mb.CompareTo(ma);
                if (byMatch != 0)
                    return byMatch;
                var remain = (sa.Pattern.Length - ma).CompareTo(sb.Pattern.Length - mb);
                return remain != 0 ? remain : a.CompareTo(b);
            });
            if (queue.Count > 3)
                queue.RemoveRange(3, queue.Count - 3);

            foreach (var kv in refs.SkillRows)
            {
                var view = kv.Value;
                var ready = readyIds != null && readyIds.Contains(kv.Key);
                var cooling = cooldownIds != null && cooldownIds.Contains(kv.Key);
                var matched = cooling ? 0 : BattleHudKit.MatchedPrefix(stock, view.Skill.Pattern);
                if (view.E5b != null)
                {
                    var place = queue.IndexOf(kv.Key);
                    view.E5b.Apply(matched, ready, cooling, place < 0 ? 0 : place + 1);
                    continue;
                }

                if (view.Band == null || view.Name == null || view.Group == null)
                    continue;

                BattleHudKit.SetDots(view.Dots, matched);
                view.Band.color = ready
                    ? new Color(0.16f, 0.11f, 0.03f, 0.9f)
                    : cooling ? SkillBandCooldown : SkillBand;
                view.Border.enabled = ready;
                view.Name.color = ready ? BattleHudKit.Gold : Color.white;
                view.Group.alpha = cooling ? 0.45f : 1f;
                if (view.Ready != null)
                    view.Ready.SetActive(ready);
            }
        }

        public static string ShortEffect(BattleSkillData skill)
        {
            if (skill == null)
                return "";
            if (!string.IsNullOrEmpty(skill.EffectText))
            {
                var line = skill.EffectText.Replace("\r", "").Replace("\n", " ");
                return line.Length > 16 ? line.Substring(0, 16) + "…" : line;
            }

            if (skill.Damage > 0 && skill.Heal > 0)
                return $"{skill.Damage}ダメージ / HP{skill.Heal}回復";
            if (skill.Damage > 0)
                return $"{skill.Damage}ダメージ";
            if (skill.Heal > 0)
                return $"HPを{skill.Heal}回復";
            return "";
        }
    }
}
