using System.Collections.Generic;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>戦闘左カラム：Dark Fantasy 風キャラ＋スキルカード。</summary>
    public static class BattlePartyCardUI
    {
        const string ArtRoot = "Battle/UI/Stylish/";

        static readonly (string Name, float AnchorTop)[] Roster =
        {
            ("アリス", 0.87f),
            ("エレン", 0.57f),
            ("ルナ", 0.27f)
        };

        static Sprite Art(string name) => Resources.Load<Sprite>(ArtRoot + name);

        public static void Build(
            Transform partyPanel,
            BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills,
            HashSet<int> innateSkillIds,
            BattleVisualContext visuals)
        {
            for (var i = 0; i < Roster.Length; i++)
            {
                var (name, anchorTop) = Roster[i];
                BuildCharacterCard(
                    partyPanel,
                    refs,
                    skills,
                    innateSkillIds,
                    visuals,
                    name,
                    anchorTop,
                    i + 1);
            }
        }

        static void BuildCharacterCard(
            Transform parent,
            BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills,
            HashSet<int> innateSkillIds,
            BattleVisualContext visuals,
            string name,
            float anchorTop,
            int rosterIndex)
        {
            visuals.Characters.TryGetValue(name, out var visual);
            var cardHeight = 0.28f;
            var card = BattleUIBuilder.CreatePanelPublic(
                parent,
                $"Char_{name}",
                new Vector2(0.02f, anchorTop - cardHeight),
                new Vector2(0.98f, anchorTop),
                new Color(0.04f, 0.02f, 0.06f, 0.92f));
            refs.CharCards[name] = card;

            var splashSprite = Art("InkSplash");
            var splash = BattleUIBuilder.CreatePanelPublic(
                card,
                "InkSplash",
                new Vector2(-0.02f, 0.02f),
                new Vector2(0.48f, 0.98f),
                Color.white,
                splashSprite);
            var splashImage = splash.GetComponent<Image>();
            splashImage.raycastTarget = false;
            if (splashSprite != null)
            {
                splashImage.type = Image.Type.Simple;
                splashImage.preserveAspect = true;
                splashImage.color = new Color(1f, 1f, 1f, 0.85f);
            }

            var portrait = BattleUIChrome.CreatePortraitPanel(
                card.transform,
                visual?.Portrait,
                BattleUITemplateLoader.Default,
                88f,
                112f);
            var portraitFrameRt = portrait.transform.parent.GetComponent<RectTransform>();
            portraitFrameRt.anchorMin = new Vector2(0, 0.5f);
            portraitFrameRt.anchorMax = new Vector2(0, 0.5f);
            portraitFrameRt.pivot = new Vector2(0, 0.5f);
            portraitFrameRt.anchoredPosition = new Vector2(8f, 0f);
            refs.CharPortraits[name] = portrait;

            var numberSprite = Art($"Number{rosterIndex:00}");
            if (numberSprite != null)
            {
                var numberPlate = BattleUIBuilder.CreatePanelPublic(
                    card,
                    "Number",
                    new Vector2(0.02f, 0.04f),
                    new Vector2(0.28f, 0.34f),
                    Color.white,
                    numberSprite);
                var numberImage = numberPlate.GetComponent<Image>();
                numberImage.type = Image.Type.Simple;
                numberImage.preserveAspect = true;
                numberImage.raycastTarget = false;
            }

            CreateDivider(card);

            var english = visual?.EnglishName ?? string.Empty;
            CreateText(
                card,
                "NameJp",
                name,
                17,
                BattleUITheme.FontBold,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                new Vector2(0.44f, 0.72f),
                new Vector2(0.98f, 0.96f),
                Color.white);
            if (!string.IsNullOrEmpty(english))
            {
                CreateText(
                    card,
                    "NameEn",
                    english,
                    11,
                    BattleUITheme.FontDisplay,
                    FontStyle.Bold,
                    TextAnchor.UpperLeft,
                    new Vector2(0.44f, 0.58f),
                    new Vector2(0.98f, 0.72f),
                    BattleUITheme.MainPurple);
            }

            var role = visual?.Role ?? string.Empty;
            if (!string.IsNullOrEmpty(role))
            {
                CreateText(
                    card,
                    "Role",
                    role,
                    10,
                    BattleUITheme.Font,
                    FontStyle.Normal,
                    TextAnchor.UpperLeft,
                    new Vector2(0.44f, 0.48f),
                    new Vector2(0.98f, 0.58f),
                    BattleUITheme.Grey80);
            }

            var skillY = 0.44f;
            foreach (var skill in skills)
            {
                if (skill.CharacterName != name)
                    continue;

                skillY = BuildSkillRow(card, refs, skill, innateSkillIds, skillY);
                if (skillY < 0.06f)
                    break;
            }

            var frame = Art("CharacterFrame");
            if (frame != null)
            {
                var overlay = BattleUIBuilder.CreatePanelPublic(
                    card, "CharacterFrame", Vector2.zero, Vector2.one, Color.white, frame);
                var frameImage = overlay.GetComponent<Image>();
                frameImage.type = Image.Type.Simple;
                frameImage.raycastTarget = false;
                overlay.SetAsLastSibling();
            }
            else
                BattleUIBuilder.AddOutlinePublic(card.gameObject, BattleUITheme.MainPurple, 2);
        }

        static float BuildSkillRow(
            RectTransform card,
            BattleUIRefs refs,
            BattleSkillData skill,
            HashSet<int> innateSkillIds,
            float anchorTop)
        {
            const float rowHeight = 0.14f;
            var isInnate = innateSkillIds != null && innateSkillIds.Contains(skill.Id);
            var skillFrame = Art(isInnate ? "SkillFrame_Active" : "SkillFrame_Normal");
            var row = BattleUIBuilder.CreatePanelPublic(
                card,
                $"Skill_{skill.Id}",
                new Vector2(0.43f, anchorTop - rowHeight),
                new Vector2(0.97f, anchorTop),
                skillFrame != null ? Color.white : new Color(0.08f, 0.08f, 0.1f, 0.92f),
                skillFrame);
            var rowImage = row.GetComponent<Image>();
            refs.SkillRowGraphics[skill.Id] = rowImage;
            if (skillFrame != null)
                rowImage.type = Image.Type.Simple;

            var element = skill.Pattern != null && skill.Pattern.Length > 0 ? skill.Pattern[0] : ElementColor.Purple;
            var elementSprite = ElementArt(element);
            var icon = BattleUIBuilder.CreatePanelPublic(
                row,
                "ElementIcon",
                new Vector2(0.02f, 0.08f),
                new Vector2(0.16f, 0.92f),
                elementSprite != null ? Color.white : ElementColorUtility.ToUnityColor(element),
                elementSprite);
            var iconImage = icon.GetComponent<Image>();
            iconImage.raycastTarget = false;
            if (elementSprite != null)
            {
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
            }

            CreateText(
                row,
                "SkillName",
                skill.DisplayName,
                13,
                BattleUITheme.FontBold,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                new Vector2(0.16f, 0.48f),
                new Vector2(0.98f, 0.96f),
                Color.white);

            CreateText(
                row,
                "ConditionLabel",
                "発動条件",
                9,
                BattleUITheme.Font,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Vector2(0.16f, 0.08f),
                new Vector2(0.42f, 0.46f),
                BattleUITheme.Grey80);

            var patternHost = BattleUIOrbPattern.CreateHost(row.transform, new Vector2(0.42f, 0.1f), new Vector2(0.98f, 0.9f));
            refs.SkillPatternOrbHosts[skill.Id] = patternHost;
            BattleUIOrbPattern.Apply(patternHost, skill.Pattern, 11f);

            return anchorTop - rowHeight - 0.015f;
        }

        static void CreateDivider(RectTransform card)
        {
            var divider = Art("CharacterDivider");
            var line = BattleUIBuilder.CreatePanelPublic(
                card,
                "Divider",
                new Vector2(0.34f, 0.04f),
                new Vector2(0.46f, 0.96f),
                divider != null ? Color.white : new Color(BattleUITheme.MainPurple.r, BattleUITheme.MainPurple.g, BattleUITheme.MainPurple.b, 0.45f),
                divider);
            var image = line.GetComponent<Image>();
            image.raycastTarget = false;
            if (divider != null)
            {
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
        }

        static Sprite ElementArt(ElementColor element) => element switch
        {
            ElementColor.Red => Art("Element_Red"),
            ElementColor.Green => Art("Element_Green"),
            ElementColor.Yellow => Art("Element_Yellow"),
            ElementColor.Purple => Art("Element_Purple"),
            ElementColor.Blue => Art("Element_Purple"),
            _ => null
        };

        static Text CreateText(
            Transform parent,
            string name,
            string text,
            int size,
            Font font,
            FontStyle style,
            TextAnchor anchor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var label = BattleUIBuilder.CreateLabelPublic(
                parent,
                name,
                text,
                size,
                anchor,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero,
                color);
            label.font = font;
            label.fontStyle = style;
            return label;
        }
    }
}
