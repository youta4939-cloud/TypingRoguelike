using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// E5b 戦闘HUD。座標は 1920x1080、左上原点。
    /// レア度はスキル定義に無いので、条件の色数で決める（2以下シルバー、3ゴールド、4以上虹）。
    /// </summary>
    public static class BattleLayoutE5b
    {
        const string Root = "Battle/E5b/";

        static TMP_FontAsset _serif;
        static TMP_FontAsset _sans;
        static bool _fontsLinked;

        public static void BuildContactShadow(RectTransform root)
        {
            Img(Place(root, "EnemyContactShadow", 940f, 920f, 429f, 125f),
                Sprite("parts/enemy/enemy_contact_shadow"), Color.white);
        }

        public static void Build(RectTransform root, BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills, BattleVisualContext visuals)
        {
            refs.AttackRingFillsOnApproach = true;
            Img(Place(root, "EnemyGroundFog", 533f, 882f, 1243f, 186f),
                Sprite("parts/enemy/enemy_ground_fog"), Color.white);
            Img(Place(root, "EdgeVignette", 0f, 0f, 1920f, 1080f),
                Sprite("parts/fx/edge_vignette_1920"), Color.white);
            Img(Place(root, "SilhouettesRight", 1436f, 0f, 484f, 1080f),
                Sprite("parts/fx/silhouettes_right"), Color.white);

            var stage = BuildAlly(root, visuals);
            Img(Place(root, "SeamInnerShadow", 430f, 0f, 230f, 1080f),
                Sprite("parts/seam/seam_inner_shadow_1x"), Color.white);
            Img(Place(root, "SeamEdgeLine", 430f, 0f, 230f, 1080f),
                Sprite("parts/seam/seam_edge_line_1x"), Color.white);
            var flash = Img(Place(root, "SeamEdgeFlash", 430f, 0f, 230f, 1080f),
                Sprite("parts/seam/seam_edge_flash_1x"), new Color(1f, 1f, 1f, 0f));
            TryAdditive(flash);
            Img(Place(root, "InkSplash", 235f, 32f, 132f, 93f),
                Sprite("parts/fx/ink_splash_small"), Color.white);

            BuildSkillList(root, refs, skills, visuals);
            BuildRing(root, refs);
            refs.Stock = StockPanel.Create(root, Serif(), Sans());
            BuildWordCards(root, refs);
            var noticeFace = BuildNotice(root, refs);
            BuildCornerText(root, refs);

            stage.Flash = flash;
            stage.NoticeFace = noticeFace;
            stage.Log = refs.LogText;
            stage.NoticeFill = refs.E5bNoticeFill;
            stage.NoticeOutline = refs.E5bNoticeOutline;
            refs.E5b = stage;
        }

        public static GameObject StockGem(Transform parent, ElementColor? color, float size)
        {
            var host = parent.GetComponent<BattleE5bStock>();
            var index = host != null ? host.Next++ : parent.childCount;
            var x = 1024f + index * 66f;
            var go = new GameObject(color.HasValue ? "StockGem" : "StockEmpty");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -796f);
            rt.sizeDelta = new Vector2(54f, 54f);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = color.HasValue
                ? Sprite("parts/gems/gem_" + Letter(color.Value) + "_on_54_1x")
                : Sprite("parts/gems/gem_empty_slot_54_1x");
            image.enabled = image.sprite != null;
            _ = size;
            return go;
        }

        public static void PaintWord(WordBoxUI box, ElementColor color, bool focused, bool dimmed,
            string kanji, string typed, string untyped, bool active)
        {
            var col = ElementColorUtility.ToUnityColor(color);
            var alpha = 0.15f * (focused ? 1.25f : dimmed ? 0.45f : 1f);
            if (box.Fill != null)
                box.Fill.color = new Color(col.r, col.g, col.b, alpha);
            if (box.Frame != null)
                box.Frame.color = dimmed
                    ? new Color(0.55f, 0.55f, 0.58f, 0.85f)
                    : Color.Lerp(Color.white, col, focused ? 0.45f : 0.2f);
            if (box.WordText != null)
            {
                box.WordText.text = kanji;
                box.WordText.color = dimmed ? new Color(0.62f, 0.62f, 0.66f) : Color.white;
            }

            if (box.RubyText != null)
                box.RubyText.text = WordRuby(typed, untyped, active && !dimmed);
            if (box.TagText != null)
                box.TagText.text = string.Empty;
            if (box.Progress != null)
            {
                var total = (typed?.Length ?? 0) + (untyped?.Length ?? 0);
                var ratio = total > 0 ? (typed?.Length ?? 0) / (float)total : 0f;
                var show = active && !dimmed && ratio > 0f;
                box.Progress.fillAmount = ratio;
                if (box.Progress.transform.parent != null)
                    box.Progress.transform.parent.gameObject.SetActive(show);
            }
        }

        static string WordRuby(string typed, string untyped, bool active)
        {
            typed ??= string.Empty;
            untyped ??= string.Empty;
            if (!active)
                return $"<color=#C4BCC8>{typed}{untyped}</color>";
            if (typed.Length == 0)
                return $"<color=#FFFFFF>{untyped}</color>";
            if (untyped.Length == 0)
                return $"<color=#FFD660>{typed}</color>";
            return $"<color=#FFD660>{typed}</color><color=#FFFFFF>{untyped}</color>";
        }

        static BattleE5bStage BuildAlly(RectTransform root, BattleVisualContext visuals)
        {
            var panel = Place(root, "AllyPanel", 0f, 0f, 640f, 1080f);
            var maskImage = Img(panel, Sprite("parts/seam/ally_panel_mask_1x"), Color.white);
            var mask = panel.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _ = maskImage;

            Img(Place(panel, "AllyCameraBg", 0f, 0f, 640f, 1080f),
                Sprite("parts/ally_panel/ally_camera_bg"), Color.white);

            var portrait = Img(Place(panel, "AllyPortrait", 0f, 102f, 775f, 978f), null, Color.clear);
            portrait.preserveAspect = true;
            var leaver = Img(Place(panel, "AllyPortraitLeaving", 0f, 102f, 775f, 978f), null, Color.clear);
            leaver.preserveAspect = true;
            leaver.enabled = false;
            var liveHost = Place(panel, "AllyLive", 0f, 0f, 640f, 1080f);
            var live = liveHost.gameObject.AddComponent<PortraitPanelView>();
            var lunaHost = Place(panel, "AllyLuna", 0f, 0f, 640f, 1080f);
            var luna = lunaHost.gameObject.AddComponent<KatanaPortraitPanelView>();
            Img(Place(panel, "AllyBokeh", 21f, 32f, 448f, 1005f),
                Sprite("parts/ally_panel/ally_bokeh_embers"), Color.white);

            var skillName = Tmp(Place(panel, "SkillCallout", 16f, 490f, 480f, 250f), "",
                Serif(), 72f, Color.white, TextAlignmentOptions.Center);
            StyleSkillName(skillName);
            skillName.gameObject.SetActive(false);

            var stage = panel.gameObject.AddComponent<BattleE5bStage>();
            stage.Portrait = portrait;
            stage.Leaver = leaver;
            stage.SkillName = skillName;
            stage.Bodies = Bodies(visuals);
            stage.Live = live;
            stage.Luna = luna;
            if (live.Ready)
                stage.ShowImmediate(PortraitPanelView.Character, default);
            else if (luna.Ready)
                stage.ShowImmediate(KatanaPortraitPanelView.Character, default);
            else
            {
                var opening = stage.Bodies.ContainsKey("ルナ") ? "ルナ" : FirstBody(stage.Bodies);
                if (!string.IsNullOrEmpty(opening) && stage.Bodies.TryGetValue(opening, out var pose))
                    stage.ShowImmediate(opening, pose);
            }

            return stage;
        }

        static void BuildSkillList(RectTransform root, BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills, BattleVisualContext visuals)
        {
            var list = Place(root, "SkillList", 0f, 0f, 0f, 0f);
            FillSkillGroups(list, refs, skills, visuals);
        }

        public static void RefillSkills(RectTransform list, BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills, BattleVisualContext visuals)
        {
            if (list == null || refs == null)
                return;
            refs.SkillRows?.Clear();
            for (var i = list.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(list.GetChild(i).gameObject);
            FillSkillGroups(list, refs, skills, visuals);
        }

        public static void RefreshStage(BattleE5bStage stage, BattleVisualContext visuals)
        {
            if (stage == null)
                return;
            stage.Bodies = Bodies(visuals);
            if (stage.Live != null && stage.Live.Ready)
                stage.ShowImmediate(PortraitPanelView.Character, default);
            else if (stage.Luna != null && stage.Luna.Ready)
                stage.ShowImmediate(KatanaPortraitPanelView.Character, default);
            else
            {
                var opening = stage.Bodies != null && stage.Bodies.ContainsKey("ルナ")
                    ? "ルナ"
                    : FirstBody(stage.Bodies);
                if (!string.IsNullOrEmpty(opening) && stage.Bodies != null &&
                    stage.Bodies.TryGetValue(opening, out var pose))
                    stage.ShowImmediate(opening, pose);
            }
        }

        static void FillSkillGroups(RectTransform list, BattleUIRefs refs,
            IReadOnlyList<BattleSkillData> skills, BattleVisualContext visuals)
        {
            var y = 448f;
            foreach (var name in GroupOrder(skills))
            {
                var group = new List<BattleSkillData>();
                if (skills != null)
                {
                    foreach (var skill in skills)
                    {
                        if (skill != null && skill.CharacterName == name)
                            group.Add(skill);
                    }
                }

                if (group.Count == 0)
                    continue;

                var top = y;
                for (var i = 0; i < group.Count; i++)
                {
                    BuildRow(list, refs, group[i], y);
                    if (i < group.Count - 1)
                        y += 68f;
                }

                BuildGroupMark(list, name, top, y, visuals);
                y += 80f;
            }
        }

        static void BuildRow(RectTransform list, BattleUIRefs refs, BattleSkillData skill, float y)
        {
            var row = Place(list, "Skill_" + skill.Id, 346f, y, 400f, 60f);
            var rarity = RarityOf(skill);
            var glow = Img(Place(row, "Glow", -30f, -30f, 460f, 120f),
                Sprite("parts/skill_list/can_fire_glow_9s_1x"), new Color(1f, 0.769f, 0.314f, 0.92f));
            glow.type = Image.Type.Sliced;
            glow.enabled = false;
            var band = Img(Place(row, "Band", 0f, 0f, 400f, 60f),
                Sprite("parts/skill_list/row_band_white_1x"), new Color(0.031f, 0.024f, 0.047f, 0.81f));
            var highlight = Img(Place(row, "Next", 0f, 0f, 400f, 60f),
                Sprite("parts/skill_list/row_next_highlight_1x"), Color.white);
            highlight.enabled = false;

            var edgeLName = rarity == "rainbow" ? "edge_bar_rainbow_left_1x" : "edge_bar_" + rarity + "_1x";
            var edgeRName = rarity == "rainbow" ? "edge_bar_rainbow_right_1x" : "edge_bar_" + rarity + "_1x";
            var edgeL = Img(Place(row, "EdgeL", -12f, -12f, 29f, 84f),
                Sprite("parts/skill_list/" + edgeLName), Color.white);
            var edgeR = Img(Place(row, "EdgeR", 383f, -12f, 29f, 84f),
                Sprite("parts/skill_list/" + edgeRName), Color.white);
            var underline = Img(Place(row, "Underline", 12f, 54f, 376f, 2f),
                Sprite("parts/skill_list/underline_" + rarity + "_1x"), new Color(1f, 1f, 1f, 0.78f));

            var gems = skill.Pattern ?? System.Array.Empty<ElementColor>();
            var capsuleW = 14f + gems.Length * 33f;
            var capsuleX = 384f - capsuleW;
            var fill = Img(Place(row, "CapsuleFill", capsuleX, 10f, capsuleW, 40f),
                Sprite("parts/skill_list/gem_capsule_fill_9s_1x"), new Color(0.05f, 0.04f, 0.07f, 0.92f));
            fill.type = Image.Type.Sliced;
            var outlineColor = OutlineColor(rarity);
            var outline = Img(Place(row, "CapsuleOutline", capsuleX, 10f, capsuleW, 40f),
                Sprite("parts/skill_list/gem_capsule_outline_9s_1x"), outlineColor);
            outline.type = Image.Type.Sliced;

            var gemImages = new Image[gems.Length];
            for (var i = 0; i < gems.Length; i++)
            {
                gemImages[i] = Img(Place(row, "Gem" + i, capsuleX + 7f + i * 33f, 14f, 32f, 32f),
                    GemSprite(gems[i], "dim3"), Color.white);
            }

            var effectRight = capsuleX - 8f;
            var effectWidth = Mathf.Min(112f, Mathf.Max(56f, effectRight - 168f));
            var effectX = effectRight - effectWidth;
            var effect = Tmp(Place(row, "Effect", effectX, 14f, effectWidth, 32f),
                BattlePartyCardUI.ShortEffect(skill), Sans(), 14f, Hex("E8CE96"),
                TextAlignmentOptions.MidlineRight);
            effect.overflowMode = TextOverflowModes.Ellipsis;
            var nameWidth = Mathf.Max(48f, effectX - 46f);
            var name = Tmp(Place(row, "Name", 42f, 10f, nameWidth, 40f), SkillLabel(skill.DisplayName),
                Serif(), 34f, NameColor(rarity), TextAlignmentOptions.MidlineLeft);
            name.enableAutoSizing = true;
            name.fontSizeMin = 16f;
            name.fontSizeMax = 34f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.gameObject.AddComponent<RectMask2D>();
            StyleSkillName(name);
            if (rarity == "rainbow")
                ApplyRainbow(name);

            var marker = Img(Place(row, "Queue", 10f, 16f, 24f, 24f),
                Sprite("parts/skill_list/order_badge_blank_1x"), Color.white);
            marker.enabled = false;

            var view = new BattleE5bRow
            {
                Band = band,
                Highlight = highlight,
                Glow = glow,
                Underline = underline,
                EdgeL = edgeL,
                EdgeR = edgeR,
                CapsuleOutline = outline,
                Name = name,
                Effect = effect,
                Marker = marker,
                Gems = gemImages,
                Pattern = gems,
                Rarity = rarity,
                EffectText = BattlePartyCardUI.ShortEffect(skill),
                Outline = outlineColor,
                EdgeLNormal = edgeL.sprite,
                EdgeRNormal = edgeR.sprite,
                EdgeLCd = Sprite("parts/skill_list/edge_bar_" + (rarity == "rainbow" ? "silver" : rarity) + "_cd_1x"),
                EdgeRCd = Sprite("parts/skill_list/edge_bar_" + (rarity == "rainbow" ? "silver" : rarity) + "_cd_1x"),
                NextMarker = Sprite("parts/skill_list/next_marker_1x"),
                Badge2 = Sprite("parts/skill_list/order_badge_2_1x"),
                Badge3 = Sprite("parts/skill_list/order_badge_3_1x")
            };
            refs.SkillRows[skill.Id] = new BattlePartyCardUI.SkillRowView
            {
                Skill = skill,
                E5b = view
            };
        }

        static void BuildGroupMark(RectTransform list, string character, float firstY, float lastY,
            BattleVisualContext visuals)
        {
            var centre = firstY + 30f;
            var ring = Img(Place(list, character + "Ring", 306f - 29f, centre - 29f, 58f, 58f),
                Sprite("parts/skill_list/face_marker_ring_1x"), Color.white);
            var faceHost = Place(ring.rectTransform, "Face", 6f, 6f, 46f, 46f);
            var faceMask = Img(faceHost, Sprite("parts/skill_list/face_marker_mask_1x"), Color.white);
            var mask = faceHost.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _ = faceMask;
            var face = Img(Place(faceHost, "Portrait", 0f, 0f, 46f, 46f), FaceSprite(character, visuals), Color.white);
            face.preserveAspect = true;
            var label = Tmp(Place(list, character + "Label", 277f, centre + 34f, 58f, 18f), character,
                Sans(), 13f, Hex("E2D6C8"), TextAlignmentOptions.Center);
            var span = lastY - firstY;
            if (span > 1f)
            {
                Img(Place(list, character + "Line", 304.5f, centre, 3f, span), null,
                    new Color(0.784f, 0.698f, 0.549f, 0.9f));
            }

            var row = firstY + 68f;
            while (row <= lastY + 1f)
            {
                Img(Place(list, character + "Dot", 301f, row + 30f - 5f, 10f, 10f),
                    Sprite("parts/skill_list/group_node_dot_1x"), Color.white);
                row += 68f;
            }
        }

        static void BuildRing(RectTransform root, BattleUIRefs refs)
        {
            var box = Place(root, "AttackRing", 956f, 355f, 400f, 400f);
            refs.EnemyAttackRingTrack = Img(box, Sprite("parts/ring/ring_track_1x"), Color.white);
            var progress = Img(Place(box, "Progress", 0f, 0f, 400f, 400f),
                Sprite("parts/ring/ring_progress_full_1x"), Color.white);
            Radial(progress);
            progress.fillAmount = 0f;
            var tick = Img(Place(box, "Tick", 0f, 0f, 400f, 400f),
                Sprite("parts/ring/ring_end_tick_1x"), Color.white);
            Radial(tick);
            tick.fillAmount = 0f;
            var follow = box.gameObject.AddComponent<BattleE5bFillFollow>();
            follow.Source = progress;
            follow.Target = tick;
            refs.AttackBar = progress;
            refs.EnemyAttackRing = progress;

            var label = Tmp(Place(box, "Countdown", 120f, 170f, 160f, 36f), "",
                Sans(), 18f, new Color(0.86f, 0.94f, 1f, 0f), TextAlignmentOptions.Center);
            refs.AttackBarLabel = label;

            var tag = Place(root, "HeavyTag", 1156f - 60f, 360f - 12f, 120f, 24f);
            var tagImage = Img(tag, Sprite("parts/ring/tag_box_9s_1x"), Color.white);
            tagImage.type = Image.Type.Sliced;
            var hot = tag.gameObject.AddComponent<BattleE5bHotTag>();
            hot.Box = tagImage;
            hot.Normal = tagImage.sprite;
            hot.Hot = Sprite("parts/ring/tag_box_hot_9s_1x");
            var tagText = Tmp(Place(tag, "Label", 0f, 0f, 120f, 24f), "強攻撃",
                Sans(), 15f, Hex("DCF0FF"), TextAlignmentOptions.Center);
            tag.gameObject.SetActive(false);
            refs.HeavyAttackCallout = tag.gameObject;
        }

        static void BuildWordCards(RectTransform root, BattleUIRefs refs)
        {
            var spots = new[]
            {
                new Vector2(884f, 468f),
                new Vector2(1176f, 468f),
                new Vector2(1030f, 630f)
            };
            for (var i = 0; i < spots.Length; i++)
                refs.WordBoxes[i] = BuildWordCard(root, "WordCard" + i, spots[i].x, spots[i].y);
        }

        static WordBoxUI BuildWordCard(RectTransform root, string name, float x, float y)
        {
            var card = Place(root, name, x, y, 252f, 122f);
            var frame = Img(card, Sprite("reused/wordcard_v2/frame_9slice_1x"), Color.white);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            var fill = Img(Place(card, "Fill", 10f, 10f, 232f, 102f), null, new Color(1f, 1f, 1f, 0.15f));
            var kanji = Tmp(Place(card, "Kanji", 16f, 18f, 220f, 60f), "",
                Sans(), 54f, Color.white, TextAlignmentOptions.Center);
            var ruby = Tmp(Place(card, "Ruby", 16f, 74f, 220f, 32f), "",
                Sans(), 26f, Color.white, TextAlignmentOptions.Center);
            StyleWordCard(kanji);
            StyleWordCard(ruby);
            var track = Img(Place(card, "RubyTrack", 56f, 108f, 140f, 5f), null,
                new Color(0.03f, 0.02f, 0.03f, 0.92f));
            var typedBar = Img(Place(track.rectTransform, "RubyFill", 0f, 0f, 140f, 5f), null, Hex("FFD660"));
            typedBar.type = Image.Type.Filled;
            typedBar.fillMethod = Image.FillMethod.Horizontal;
            typedBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            typedBar.fillAmount = 0f;
            track.gameObject.SetActive(false);
            var stripe = Img(Place(card, "Stripe", 0f, 0f, 1f, 1f), null, Color.clear);
            stripe.enabled = false;
            var tag = Tmp(Place(card, "Tag", 0f, 0f, 1f, 1f), "", Sans(), 1f, Color.clear,
                TextAlignmentOptions.Center);
            tag.gameObject.SetActive(false);
            return new WordBoxUI
            {
                FrameRoot = card,
                Frame = frame,
                Fill = fill,
                ElementStripe = stripe,
                TagText = tag,
                WordText = kanji,
                RubyText = ruby,
                Progress = typedBar,
                E5b = true
            };
        }

        static void BuildStock(RectTransform root, BattleUIRefs refs)
        {
            var capsule = Img(Place(root, "StockCapsule", 980f, 760f, 352f, 72f),
                Sprite("parts/hud/stock_capsule_9s_1x"), Color.white);
            capsule.type = Image.Type.Sliced;
            var label = Tmp(Place(root, "StockLabel", 820f, 780f, 148f, 32f), "STOCK",
                Sans(), 15f, Hex("D6C8AC"), TextAlignmentOptions.MidlineRight);
            var orbs = Place(root, "E5bStock", 0f, 0f, 0f, 0f);
            orbs.gameObject.AddComponent<BattleE5bStock>();
            refs.StockOrbsContainer = orbs;
        }

        static Image BuildNotice(RectTransform root, BattleUIRefs refs)
        {
            var fill = Img(Place(root, "NoticeFill", 1515f, 786f, 300f, 60f),
                Sprite("parts/hud/notice_fill_9s_1x"), new Color(0.133f, 0.024f, 0.039f, 0.9f));
            fill.type = Image.Type.Sliced;
            var outline = Img(Place(root, "NoticeOutline", 1515f, 786f, 300f, 60f),
                Sprite("parts/hud/notice_outline_9s_1x"), new Color(0.9f, 0.35f, 0.4f, 0.78f));
            outline.type = Image.Type.Sliced;
            refs.E5bNoticeFill = fill;
            refs.E5bNoticeOutline = outline;
            Img(Place(root, "NoticeRing", 1523f, 793f, 46f, 46f),
                Sprite("parts/hud/notice_face_ring_1x"), Color.white);
            var face = Img(Place(root, "NoticeFace", 1524f, 794f, 44f, 44f),
                Sprite("reference/notice_face_ellen_44"), Color.white);
            face.preserveAspect = true;
            refs.LogText = Tmp(Place(root, "NoticeText", 1579f, 790f, 220f, 52f), "戦闘開始",
                Sans(), 21f, Hex("FAF4E8"), TextAlignmentOptions.MidlineLeft);
            refs.LogText.textWrappingMode = TextWrappingModes.Normal;
            refs.LogText.overflowMode = TextOverflowModes.Ellipsis;
            return face;
        }

        static void BuildHp(RectTransform root, BattleUIRefs refs)
        {
            var frame = Img(Place(root, "HpFrame", 823f, 849f, 666f, 32f),
                Sprite("parts/hud/hp_frame_9s_1x"), Color.white);
            frame.type = Image.Type.Sliced;
            var fill = Img(Place(root, "HpFill", 826f, 852f, 660f, 26f),
                Sprite("parts/hud/hp_fill_1x"), new Color(0.35f, 0.72f, 1f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            refs.PlayerHpBar = fill;
            var hp = Tmp(Place(root, "HpLabel", 836f, 852f, 40f, 26f), "HP",
                Sans(), 17f, Hex("E6F0FF"), TextAlignmentOptions.MidlineLeft);
            refs.PlayerHpText = Tmp(Place(root, "HpValue", 1500f, 848f, 220f, 34f), "",
                Sans(), 26f, Color.white, TextAlignmentOptions.MidlineLeft);
        }

        static BattleE5bFillFollow BuildMadness(RectTransform root, BattleUIRefs refs)
        {
            Img(Place(root, "MadnessCase", 1800f, 550f, 92f, 420f),
                Sprite("parts/madness/madness_case_glass_1x"), Color.white);
            Img(Place(root, "MadnessCaps", 1800f, 550f, 92f, 420f),
                Sprite("parts/madness/madness_caps_1x"), Color.white);
            var glow = Img(Place(root, "MadnessGlow", 1830f, 650f, 32f, 302f),
                Sprite("parts/madness/madness_liquid_glow_9s_1x"), new Color(0.55f, 1f, 0.45f, 0.85f));
            glow.type = Image.Type.Filled;
            glow.fillMethod = Image.FillMethod.Vertical;
            glow.fillOrigin = (int)Image.OriginVertical.Bottom;
            var liquid = Img(Place(root, "MadnessLiquid", 1830f, 650f, 32f, 302f),
                Sprite("parts/madness/madness_liquid_1x"), Color.white);
            liquid.type = Image.Type.Filled;
            liquid.fillMethod = Image.FillMethod.Vertical;
            liquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            liquid.fillAmount = 0f;
            var ticks = Img(Place(root, "MadnessTicks", 1800f, 550f, 92f, 420f),
                Sprite("parts/madness/madness_ticks_highlight_1x"), Color.white);
            ticks.type = Image.Type.Filled;
            ticks.fillMethod = Image.FillMethod.Vertical;
            ticks.fillOrigin = (int)Image.OriginVertical.Bottom;
            Img(Place(root, "MadnessBrain", 1819f, 584f, 54f, 42f),
                Sprite("reference/madness_brain_icon_54x42"), Color.white);
            var title = Tmp(Place(root, "MadnessTitle", 1810f, 552f, 72f, 22f), "狂気",
                Sans(), 15f, Hex("AAF08C"), TextAlignmentOptions.Center);
            refs.MadnessFill = liquid;
            refs.MadnessEmpty = null;
            refs.MadnessText = Tmp(Place(root, "MadnessValue", 1640f, 780f, 170f, 48f), "",
                Sans(), 22f, Hex("DCFFC8"), TextAlignmentOptions.MidlineRight);
            var follow = liquid.gameObject.AddComponent<BattleE5bFillFollow>();
            follow.Target = ticks;
            follow.Also = glow;
            follow.Value = refs.MadnessText.rectTransform;
            return follow;
        }

        static void BuildCornerText(RectTransform root, BattleUIRefs refs)
        {
            refs.RerollBadge = Tmp(Place(root, "Reroll", 1500f, 988f, 350f, 22f), "リロール 3/3",
                Sans(), 15f, Hex("FF8278"), TextAlignmentOptions.MidlineRight);
            refs.JackpotMeterText = Tmp(Place(root, "Jackpot", 1450f, 1014f, 454f, 40f), "",
                Sans(), 15f, Hex("E6D6A0"), TextAlignmentOptions.TopRight);
            refs.JackpotMeterText.textWrappingMode = TextWrappingModes.Normal;
            Tmp(Place(root, "PauseHint", 1450f, 1056f, 454f, 18f), "Space ポーズ   Enter リロール",
                Sans(), 12f, Hex("96908C"), TextAlignmentOptions.MidlineRight);
        }

        static void Radial(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
        }

        static Dictionary<string, PortraitPose> Bodies(BattleVisualContext visuals)
        {
            var map = new Dictionary<string, PortraitPose>();
            var lunaArt = Sprite("parts/ally_panel/portrait_luna_graded");
            if (lunaArt == null)
                lunaArt = Sprite("reused/chars/luna_standing_cutout");
            if (visuals?.Characters == null)
                return map;
            foreach (var pair in visuals.Characters)
            {
                if (pair.Key == "ルナ" && lunaArt != null)
                {
                    map[pair.Key] = new PortraitPose
                    {
                        Sprite = lunaArt,
                        Position = new Vector2(0f, -102f),
                        Size = new Vector2(775f, 978f)
                    };
                }
                else if (pair.Value?.Portrait != null)
                {
                    map[pair.Key] = new PortraitPose
                    {
                        Sprite = pair.Value.Portrait,
                        Position = new Vector2(110f, -180f),
                        Size = new Vector2(420f, 420f)
                    };
                }
            }

            return map;
        }

        static string FirstBody(Dictionary<string, PortraitPose> bodies)
        {
            foreach (var key in bodies.Keys)
                return key;
            return null;
        }

        static Sprite FaceSprite(string character, BattleVisualContext visuals)
        {
            if (visuals != null && visuals.Characters.TryGetValue(character, out var visual) && visual.Portrait != null)
                return visual.Portrait;
            var file = StarterCharacters.ArtKey(character) switch
            {
                "アリス" => "Alice",
                "エレン" => "Ellen",
                "ルナ" => "Luna",
                _ => null
            };
            return file == null ? null : Sprite("reused/portraits/" + file);
        }

        static IEnumerable<string> GroupOrder(IReadOnlyList<BattleSkillData> skills)
        {
            var seen = new HashSet<string>();
            foreach (var name in new[] { "アリス", "エレン", "ルナ" })
            {
                seen.Add(name);
                yield return name;
            }

            if (skills == null)
                yield break;
            foreach (var skill in skills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.CharacterName) || !seen.Add(skill.CharacterName))
                    continue;
                yield return skill.CharacterName;
            }
        }

        static string RarityOf(BattleSkillData skill)
        {
            var n = skill?.Pattern?.Length ?? 0;
            if (n >= 4)
                return "rainbow";
            if (n == 3)
                return "gold";
            return "silver";
        }

        static Color NameColor(string rarity) => rarity switch
        {
            "gold" => Hex("FCE4A6"),
            "rainbow" => Color.white,
            _ => Hex("EEF4FF")
        };

        static Color OutlineColor(string rarity) => rarity switch
        {
            "gold" => Hex("D69628"),
            "rainbow" => Hex("60D678"),
            _ => Hex("96B0D2")
        };

        static Sprite GemSprite(ElementColor color, string state) =>
            Sprite("parts/gems/gem_" + Letter(color) + "_" + state + "_32_1x");

        static string Letter(ElementColor color) => color switch
        {
            ElementColor.Red => "R",
            ElementColor.Green => "G",
            ElementColor.Blue => "B",
            ElementColor.Yellow => "Y",
            ElementColor.Purple => "P",
            _ => "R"
        };

        static RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.enabled = sprite != null || color.a > 0.01f;
            return image;
        }

        static TextMeshProUGUI Tmp(RectTransform rt, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align)
        {
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            if (font != null && (font == _serif || font == _sans))
                label.fontWeight = FontWeight.Black;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        /// <summary>絵文字などの記号と全角スペースを外す。空きは TMP の space タグ。</summary>
        public static string SkillLabel(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            var builder = new StringBuilder(raw.Length);
            var pendingSpace = false;
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < raw.Length && char.IsLowSurrogate(raw[i + 1]))
                        i++;
                    continue;
                }

                if (c == '\u3000')
                    continue;
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.OtherSymbol ||
                    category == UnicodeCategory.ModifierSymbol ||
                    category == UnicodeCategory.NonSpacingMark ||
                    category == UnicodeCategory.EnclosingMark ||
                    category == UnicodeCategory.Format)
                    continue;

                if (pendingSpace)
                {
                    builder.Append("<space=0.4em>");
                    pendingSpace = false;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        static void StyleWordCard(TMP_Text text)
        {
            if (text == null)
                return;
            text.fontMaterial = new Material(text.fontMaterial);
            var material = text.fontMaterial;
            if (material.HasProperty("_FaceDilate"))
                material.SetFloat("_FaceDilate", 0.36f);
            material.EnableKeyword("OUTLINE_ON");
            if (material.HasProperty("_OutlineWidth"))
            {
                material.SetFloat("_OutlineWidth", 0.14f);
                material.SetColor("_OutlineColor", new Color(0.03f, 0.016f, 0.03f, 1f));
            }
        }

        static void StyleSkillName(TMP_Text text)
        {
            if (text == null)
                return;
            text.fontMaterial = new Material(text.fontMaterial);
            var material = text.fontMaterial;
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor("_OutlineColor", Color.black);
            material.SetFloat("_OutlineWidth", 0.2f);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.96f));
            material.SetFloat("_UnderlayOffsetX", 0.5f);
            material.SetFloat("_UnderlayOffsetY", -0.6f);
            material.SetFloat("_UnderlayDilate", 0.4f);
            material.SetFloat("_UnderlaySoftness", 0.4f);
        }

        static void ApplyRainbow(TMP_Text text)
        {
            var sprite = Sprite("parts/skill_list/name_gradient_rainbow");
            if (sprite == null || sprite.texture == null)
            {
                text.color = Hex("FFC4E2");
                return;
            }

            text.horizontalMapping = TextureMappingOptions.Line;
            text.fontMaterial.SetTexture("_FaceTex", sprite.texture);
            text.color = Color.white;
        }

        static void TryAdditive(Image image)
        {
            if (image == null)
                return;
            var shader = Shader.Find("UI/Additive");
            if (shader != null)
                image.material = new Material(shader);
        }

        static Sprite Sprite(string path) => Resources.Load<Sprite>(Root + path);

        static TMP_FontAsset Serif()
        {
            var asset = CreateFont("Battle/E5b/fonts/NotoSerifJP", ref _serif, true);
            LinkFonts();
            return asset;
        }

        static TMP_FontAsset Sans()
        {
            var asset = CreateFont("Battle/E5b/fonts/NotoSansJP", ref _sans, false);
            LinkFonts();
            return asset;
        }

        static TMP_FontAsset CreateFont(string path, ref TMP_FontAsset cache, bool serif)
        {
            if (cache != null)
                return cache;
            var source = Resources.Load<Font>(path);
            if (source != null)
            {
                try
                {
                    cache = TMP_FontAsset.CreateFontAsset(
                        source, 90, 9, GlyphRenderMode.SDFAA, 4096, 4096,
                        AtlasPopulationMode.Dynamic, true);
                    if (cache != null)
                        PinWeight(cache);
                }
                catch (System.Exception)
                {
                    cache = null;
                }
            }

            return cache != null ? cache : serif ? BattleHudKit.Mincho : BattleHudKit.Gothic;
        }

        static void LinkFonts()
        {
            if (_fontsLinked || _serif == null || _sans == null)
                return;
            _fontsLinked = true;
            AddFallback(_serif, _sans);
            AddFallback(_sans, _serif);
        }

        static void AddFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
        {
            asset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!asset.fallbackFontAssetTable.Contains(fallback))
                asset.fallbackFontAssetTable.Add(fallback);
        }

        static void PinWeight(TMP_FontAsset asset)
        {
            var table = asset.fontWeightTable;
            if (table == null || table.Length < 10)
                return;
            table[7] = new TMP_FontWeightPair { regularTypeface = asset };
            table[9] = new TMP_FontWeightPair { regularTypeface = asset };
        }

        static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }
    }

    public struct PortraitPose
    {
        public Sprite Sprite;
        public Vector2 Position;
        public Vector2 Size;
    }

    public sealed class BattleE5bStock : MonoBehaviour
    {
        public int Next;
    }

    public sealed class BattleE5bRow
    {
        public Image Band;
        public Image Highlight;
        public Image Glow;
        public Image Underline;
        public Image EdgeL;
        public Image EdgeR;
        public Image CapsuleOutline;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Effect;
        public Image Marker;
        public Image[] Gems;
        public ElementColor[] Pattern;
        public string Rarity;
        public string EffectText;
        public Color Outline;
        public Sprite EdgeLNormal;
        public Sprite EdgeRNormal;
        public Sprite EdgeLCd;
        public Sprite EdgeRCd;
        public Sprite NextMarker;
        public Sprite Badge2;
        public Sprite Badge3;

        public void Apply(int matched, bool ready, bool cooling, int queue)
        {
            var next = queue == 1;
            if (Band != null)
            {
                Band.color = next
                    ? new Color(0.133f, 0.102f, 0.149f, 0.73f)
                    : new Color(0.031f, 0.024f, 0.047f, 0.81f);
            }

            if (Highlight != null)
                Highlight.enabled = next;
            if (Glow != null)
                Glow.enabled = ready && !cooling;
            if (EdgeL != null)
            {
                EdgeL.sprite = cooling && EdgeLCd != null ? EdgeLCd : EdgeLNormal;
                EdgeL.color = new Color(1f, 1f, 1f, cooling ? 0.47f : 1f);
            }

            if (EdgeR != null)
            {
                EdgeR.sprite = cooling && EdgeRCd != null ? EdgeRCd : EdgeRNormal;
                EdgeR.color = new Color(1f, 1f, 1f, cooling ? 0.47f : 1f);
            }

            if (Underline != null)
                Underline.color = new Color(1f, 1f, 1f, cooling ? 0.35f : 0.78f);
            if (Name != null)
            {
                Name.fontSizeMax = next ? 35f : 34f;
                Name.color = cooling ? Hex("9696A0") : Rarity == "rainbow" ? Color.white : NameColor();
            }

            if (Effect != null)
            {
                Effect.text = cooling ? "CD中" : EffectText;
                Effect.color = cooling ? Hex("B0ACB8") : Hex("E8CE96");
            }

            if (CapsuleOutline != null)
            {
                var color = Outline;
                color.a = cooling ? 0.43f : 1f;
                CapsuleOutline.color = color;
            }

            if (Marker != null)
            {
                Sprite sprite = null;
                var size = 24f;
                if (queue == 1)
                {
                    sprite = NextMarker;
                    size = 36f;
                }
                else if (queue == 2)
                    sprite = Badge2;
                else if (queue == 3)
                    sprite = Badge3;
                Marker.sprite = sprite;
                Marker.enabled = sprite != null;
                Marker.rectTransform.sizeDelta = new Vector2(size, size);
                Marker.rectTransform.anchoredPosition = new Vector2(22f - size * 0.5f, -(28f - size * 0.5f));
            }

            if (Gems == null || Pattern == null)
                return;
            for (var i = 0; i < Gems.Length && i < Pattern.Length; i++)
            {
                var state = cooling ? "dim2" : i < matched ? "on" : "dim3";
                Gems[i].sprite = Resources.Load<Sprite>(
                    "Battle/E5b/parts/gems/gem_" + Letter(Pattern[i]) + "_" + state + "_32_1x");
            }
        }

        Color NameColor() => Rarity == "gold" ? Hex("FCE4A6") : Hex("EEF4FF");

        static string Letter(ElementColor color) => color switch
        {
            ElementColor.Red => "R",
            ElementColor.Green => "G",
            ElementColor.Blue => "B",
            ElementColor.Yellow => "Y",
            ElementColor.Purple => "P",
            _ => "R"
        };

        static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }
    }

    public sealed class BattleE5bStage : MonoBehaviour
    {
        public Image Portrait;
        public Image Leaver;
        public Image Flash;
        public TextMeshProUGUI SkillName;
        public Image NoticeFace;
        public TMP_Text Log;
        public Image NoticeFill;
        public Image NoticeOutline;
        public PortraitPanelView Live;
        public KatanaPortraitPanelView Luna;
        public Dictionary<string, PortraitPose> Bodies = new();

        string _shown;

        public void NotifyHit()
        {
            if (IsAlice(_shown))
                Live.Rig.PlayHit();
            else if (IsLuna(_shown))
                Luna.Rig.PlayHit();
        }

        public void ShowImmediate(string character, PortraitPose pose)
        {
            if (IsLive(character))
            {
                if (Portrait != null)
                    Portrait.enabled = false;
                if (Leaver != null)
                    Leaver.enabled = false;
                if (Live != null)
                    Live.SetAlpha(IsAlice(character) ? 1f : 0f);
                if (Luna != null)
                    Luna.SetAlpha(IsLuna(character) ? 1f : 0f);
                _shown = character;
                return;
            }

            if (Live != null)
                Live.SetAlpha(0f);
            if (Luna != null)
                Luna.SetAlpha(0f);
            if (Portrait == null || pose.Sprite == null)
                return;
            Portrait.sprite = pose.Sprite;
            Portrait.rectTransform.anchoredPosition = pose.Position;
            Portrait.rectTransform.sizeDelta = pose.Size;
            Portrait.color = Color.white;
            Portrait.enabled = true;
            _shown = character;
        }

        public void Play(BattleSkillData skill)
        {
            if (skill == null)
                return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
            if (IsAlice(skill.CharacterName))
            {
                Live.Rig.PlaySkill();
                StartCoroutine(SwapRoutine(default, skill.CharacterName));
            }
            else if (IsLuna(skill.CharacterName))
            {
                Luna.Rig.PlaySkill();
                StartCoroutine(SwapRoutine(default, skill.CharacterName));
            }
            else if (Bodies != null && Bodies.TryGetValue(skill.CharacterName, out var pose))
                StartCoroutine(SwapRoutine(pose, skill.CharacterName));
            if (NoticeFace != null)
            {
                var face = Face(skill.CharacterName);
                if (face != null)
                    NoticeFace.sprite = face;
            }
        }

        Sprite Face(string character)
        {
            var file = StarterCharacters.ArtKey(character) switch
            {
                "アリス" => "Alice",
                "エレン" => "Ellen",
                "ルナ" => "Luna",
                _ => null
            };
            if (file != null)
            {
                var sprite = Resources.Load<Sprite>("Battle/E5b/reused/portraits/" + file);
                if (sprite != null)
                    return sprite;
            }

            return Bodies != null && Bodies.TryGetValue(character, out var pose) ? pose.Sprite : null;
        }

        void LateUpdate()
        {
            if (Log == null || NoticeFill == null || NoticeOutline == null)
                return;
            var heal = Log.text != null && Log.text.Contains("回復");
            NoticeFill.color = heal
                ? new Color(0.039f, 0.078f, 0.055f, 0.88f)
                : new Color(0.133f, 0.024f, 0.039f, 0.9f);
            NoticeOutline.color = heal
                ? new Color(0.588f, 0.941f, 0.667f, 0.78f)
                : new Color(0.95f, 0.45f, 0.42f, 0.78f);
        }

        IEnumerator FlashRoutine()
        {
            if (Flash == null)
                yield break;
            var elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha;
                if (elapsed < 0.06f)
                    alpha = elapsed / 0.06f;
                else if (elapsed < 0.1f)
                    alpha = 1f;
                else
                    alpha = 1f - (elapsed - 0.1f) / 0.2f;
                var color = Flash.color;
                color.a = Mathf.Clamp01(alpha);
                Flash.color = color;
                yield return null;
            }

            var end = Flash.color;
            end.a = 0f;
            Flash.color = end;
        }

        IEnumerator NameRoutine(string skillName)
        {
            if (SkillName == null)
                yield break;
            SkillName.gameObject.SetActive(true);
            SkillName.text = BattleLayoutE5b.SkillLabel(skillName);
            SkillName.color = Color.white;
            var elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                var color = SkillName.color;
                color.a = elapsed < 0.8f ? 1f : 1f - (elapsed - 0.8f) / 0.2f;
                SkillName.color = color;
                yield return null;
            }

            SkillName.gameObject.SetActive(false);
        }

        bool IsAlice(string character) =>
            StarterCharacters.Same(character, PortraitPanelView.Character) && Live != null && Live.Ready && Live.Rig != null;

        bool IsLuna(string character) =>
            StarterCharacters.Same(character, KatanaPortraitPanelView.Character) && Luna != null && Luna.Ready && Luna.Rig != null;

        bool IsLive(string character) => IsAlice(character) || IsLuna(character);

        void LiveAlpha(string character, float alpha)
        {
            if (IsAlice(character))
                Live.SetAlpha(alpha);
            else if (IsLuna(character))
                Luna.SetAlpha(alpha);
        }

        IEnumerator SwapRoutine(PortraitPose next, string character)
        {
            if (_shown == character)
                yield break;
            if (IsLive(character) && IsLive(_shown))
            {
                yield return CrossfadeLives(character);
                yield break;
            }

            if (IsLive(character))
            {
                yield return SwapToLive(character);
                yield break;
            }

            if (IsLive(_shown))
            {
                yield return SwapFromLive(next, character);
                yield break;
            }

            if (Portrait == null || next.Sprite == null)
                yield break;
            var home = Portrait.rectTransform.anchoredPosition;
            var homeSize = Portrait.rectTransform.sizeDelta;
            if (Leaver != null && Portrait.sprite != null)
            {
                Leaver.sprite = Portrait.sprite;
                Leaver.rectTransform.anchoredPosition = home;
                Leaver.rectTransform.sizeDelta = homeSize;
                Leaver.color = Color.white;
                Leaver.enabled = true;
            }

            Portrait.sprite = next.Sprite;
            Portrait.rectTransform.sizeDelta = next.Size;
            Portrait.rectTransform.anchoredPosition = next.Position + new Vector2(-260f, 0f);
            Portrait.color = new Color(1f, 1f, 1f, 0f);
            var elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - k) * (1f - k);
                if (Leaver != null && Leaver.enabled)
                {
                    Leaver.rectTransform.anchoredPosition = home + new Vector2(-220f * eased, 0f);
                    Leaver.color = new Color(1f, 1f, 1f, 1f - eased);
                }

                Portrait.rectTransform.anchoredPosition = Vector2.Lerp(
                    next.Position + new Vector2(-260f, 0f), next.Position, eased);
                Portrait.color = new Color(1f, 1f, 1f, eased);
                yield return null;
            }

            if (Leaver != null)
                Leaver.enabled = false;
            Portrait.rectTransform.anchoredPosition = next.Position;
            Portrait.color = Color.white;
            _shown = character;
        }

        IEnumerator CrossfadeLives(string character)
        {
            var from = _shown;
            var elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 2f);
                LiveAlpha(from, 1f - eased);
                LiveAlpha(character, eased);
                yield return null;
            }

            LiveAlpha(from, 0f);
            LiveAlpha(character, 1f);
            _shown = character;
        }

        IEnumerator SwapToLive(string character)
        {
            var home = Vector2.zero;
            if (Leaver != null && Portrait != null && Portrait.enabled && Portrait.sprite != null)
            {
                home = Portrait.rectTransform.anchoredPosition;
                Leaver.sprite = Portrait.sprite;
                Leaver.rectTransform.anchoredPosition = home;
                Leaver.rectTransform.sizeDelta = Portrait.rectTransform.sizeDelta;
                Leaver.color = Color.white;
                Leaver.enabled = true;
            }

            if (Portrait != null)
                Portrait.enabled = false;
            if (Live != null && !IsAlice(character))
                Live.SetAlpha(0f);
            if (Luna != null && !IsLuna(character))
                Luna.SetAlpha(0f);
            LiveAlpha(character, 0f);
            var elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 2f);
                if (Leaver != null && Leaver.enabled)
                {
                    Leaver.rectTransform.anchoredPosition = home + new Vector2(-220f * eased, 0f);
                    Leaver.color = new Color(1f, 1f, 1f, 1f - eased);
                }

                LiveAlpha(character, eased);
                yield return null;
            }

            if (Leaver != null)
                Leaver.enabled = false;
            LiveAlpha(character, 1f);
            _shown = character;
        }

        IEnumerator SwapFromLive(PortraitPose next, string character)
        {
            if (Portrait == null || next.Sprite == null)
            {
                LiveAlpha(_shown, 0f);
                _shown = character;
                yield break;
            }

            Portrait.sprite = next.Sprite;
            Portrait.rectTransform.sizeDelta = next.Size;
            Portrait.enabled = true;
            Portrait.color = new Color(1f, 1f, 1f, 0f);
            Portrait.rectTransform.anchoredPosition = next.Position + new Vector2(-260f, 0f);
            var elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 2f);
                LiveAlpha(_shown, 1f - eased);
                Portrait.rectTransform.anchoredPosition = Vector2.Lerp(
                    next.Position + new Vector2(-260f, 0f), next.Position, eased);
                Portrait.color = new Color(1f, 1f, 1f, eased);
                yield return null;
            }

            LiveAlpha(_shown, 0f);
            Portrait.rectTransform.anchoredPosition = next.Position;
            Portrait.color = Color.white;
            _shown = character;
        }
    }

    public sealed class BattleE5bHotTag : MonoBehaviour
    {
        public Image Box;
        public Sprite Normal;
        public Sprite Hot;

        void Update()
        {
            if (Box == null)
                return;
            var hot = Mathf.PingPong(Time.unscaledTime * 3f, 1f) > 0.5f;
            Box.sprite = hot && Hot != null ? Hot : Normal;
        }
    }

    public sealed class BattleE5bFillFollow : MonoBehaviour
    {
        public Image Source;
        public Image Target;
        public Image Also;
        public RectTransform Value;

        void LateUpdate()
        {
            var amount = Source != null ? Source.fillAmount : GetComponent<Image>() != null
                ? GetComponent<Image>().fillAmount
                : 0f;
            if (Target != null)
                Target.fillAmount = amount;
            if (Also != null)
                Also.fillAmount = amount;
            if (Value != null)
            {
                var level = 650f + (1f - amount) * 302f;
                Value.anchoredPosition = new Vector2(1640f, -level);
            }
        }
    }
}
