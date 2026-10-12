using System.Collections.Generic;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public sealed class BattleUIRefs
    {
        public Text PlayerHpText;
        public Text EnemyHpText;
        public Text EnemyEmoji;
        public Text LogText;
        public Text RerollBadge;
        public Text JackpotMeterText;
        public Image MadnessFill;
        public Text MadnessText;
        public Text AttackBarLabel;
        public Image PlayerHpBar;
        public Image EnemyHpBar;
        public Image AttackBar;
        public Image EnemyAttackRing;
        public Image EnemyAttackRingTrack;
        public Image DamageOverlay;
        public GameObject PauseOverlay;
        public GameObject CutinOverlay;
        public Text CutinChar;
        public Text CutinSkill;
        public RectTransform RootCard;
        public RectTransform StockOrbsContainer;
        public RectTransform CandidatesContainer;
        public WordBoxUI[] WordBoxes = new WordBoxUI[3];
        public Dictionary<int, RectTransform> SkillPatternOrbHosts = new();
        public Dictionary<int, Graphic> SkillRowGraphics = new();
        public Dictionary<string, RectTransform> CharCards = new();
        public Dictionary<string, Image> CharPortraits = new();
        public Image BossPortrait;
        public Image CutinPortrait;
        public RectTransform EnemyDamagePopupRoot;
        public RectTransform ArenaRoot;
        public RectTransform PartyApproachRoot;
        public RectTransform EnemyStage;
        public RectTransform EnemyHitRoot;
        public Image EnemyAura;
        public GameObject CountdownOverlay;
        public Image CountdownRing;
        public Text CountdownText;
        public GameObject JackpotOverlay;
        public Image JackpotBlackout;
        public RectTransform JackpotLightStreak;
        public CanvasGroup JackpotCutinGroup;
        public RectTransform JackpotCutinRoot;
        public Text JackpotTitle;
        public Text JackpotDamageText;
        public GameObject ResultOverlay;
        public CanvasGroup ResultCanvasGroup;
        public Text ResultRank;
        public Text ResultStats;
        public Button ResultContinueButton;
        public Button DebugKillButton;
    }

    public sealed class WordBoxUI
    {
        public RectTransform FrameRoot;
        public Image Glow;
        public Image Frame;
        public Image Fill;
        public Image Progress;
        public Image ElementStripe;
        public Text TagText;
        public Text RubyText;
        public Text WordText;
    }

    public static class BattleUIBuilder
    {
        public static BattleUIRefs Build(
            Transform parent,
            IReadOnlyList<BattleSkillData> skills,
            HashSet<int> innateSkillIds = null,
            BattleVisualContext visuals = null)
        {
            visuals ??= BattleVisualContext.FromEncounter(null, null);
            var template = BattleUITemplateLoader.Default;
            var refs = new BattleUIRefs();

            var canvasGo = new GameObject("BattleCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bg = CreatePanel(canvasGo.transform, "Background", Vector2.zero, Vector2.one,
                new Color(0.025f, 0.025f, 0.055f, 1f));
            BuildBattleBackdrop(bg, visuals);

            refs.RootCard = CreatePanel(bg.transform, "BattleHud", Vector2.zero, Vector2.one,
                new Color(0, 0, 0, 0)).GetComponent<RectTransform>();

            BuildArena(refs.RootCard, refs, visuals);
            BuildTypingRail(refs.RootCard, refs);

            // 左レール: 情報量を保ちつつ画面幅を抑える。
            var partyWindow = BattleUIChrome.CreateLuxuryWindow(refs.RootCard, "PartyWindow",
                new Vector2(0.012f, 0.055f), new Vector2(0.28f, 0.96f), "PARTY", BattleUITheme.MainPurple);
            SetPanelOpacity(partyWindow, 0.92f);
            var partyBody = partyWindow.Find("Body");
            BattlePartyCardUI.Build(
                partyBody != null ? partyBody : partyWindow,
                refs,
                skills,
                innateSkillIds,
                visuals);
            BuildMadnessMeter(partyBody != null ? partyBody : partyWindow, refs);

            BuildBossPlate(refs, visuals);
            BuildBattleLog(refs);
            BuildPartyHpStrip(refs);
            BuildControlStrip(refs);

            // 最下部: 左にストック、右に発動候補。候補が少なくても巨大な空白を作らない。
            var orbHud = CreatePanel(refs.RootCard, "OrbHud", new Vector2(0.295f, 0.02f),
                new Vector2(0.535f, 0.138f), new Color(0.02f, 0.025f, 0.05f, 0.9f), template?.windowPanel);
            AddOutline(orbHud.gameObject, new Color(0.95f, 0.62f, 0.08f, 0.55f), 1);
            refs.JackpotMeterText = CreateLabel(
                orbHud,
                "JackpotMeter",
                "次 0%",
                14,
                TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.52f),
                new Vector2(0.96f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                BattleUITheme.Gold);
            refs.JackpotMeterText.fontStyle = FontStyle.Bold;
            refs.JackpotMeterText.horizontalOverflow = HorizontalWrapMode.Wrap;
            refs.JackpotMeterText.verticalOverflow = VerticalWrapMode.Overflow;
            var stockRow = new GameObject("StockOrbs");
            stockRow.transform.SetParent(orbHud, false);
            refs.StockOrbsContainer = stockRow.AddComponent<RectTransform>();
            refs.StockOrbsContainer.anchorMin = new Vector2(0.04f, 0.06f);
            refs.StockOrbsContainer.anchorMax = new Vector2(0.96f, 0.5f);
            refs.StockOrbsContainer.offsetMin = Vector2.zero;
            refs.StockOrbsContainer.offsetMax = Vector2.zero;
            var hlg = stockRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            var skillHud = CreatePanel(refs.RootCard, "SkillHintHud", new Vector2(0.545f, 0.02f),
                new Vector2(0.975f, 0.138f), new Color(0.02f, 0.025f, 0.05f, 0.9f), template?.windowPanel);
            AddOutline(skillHud.gameObject, new Color(0.55f, 0.4f, 0.95f, 0.55f), 1);
            CreateLabel(skillHud, "NextLabel", "NEXT SKILL", 12, TextAnchor.UpperLeft,
                new Vector2(0.025f, 0.7f), new Vector2(0.975f, 0.96f), Vector2.zero, Vector2.zero,
                new Color(0.72f, 0.62f, 1f));
            var candRow = new GameObject("Candidates");
            candRow.transform.SetParent(skillHud, false);
            refs.CandidatesContainer = candRow.AddComponent<RectTransform>();
            refs.CandidatesContainer.anchorMin = new Vector2(0.02f, 0.04f);
            refs.CandidatesContainer.anchorMax = new Vector2(0.98f, 0.7f);
            refs.CandidatesContainer.offsetMin = Vector2.zero;
            refs.CandidatesContainer.offsetMax = Vector2.zero;
            var vlg = candRow.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            refs.DamageOverlay = CreatePanel(refs.RootCard, "DamageOverlay", Vector2.zero, Vector2.one,
                new Color(1f, 0, 0, 0)).GetComponent<Image>();
            refs.DamageOverlay.raycastTarget = false;

            BuildCountdownOverlay(refs);
            BuildResultOverlay(refs, template);
            BuildPauseOverlay(refs);
            BuildCutinOverlay(refs, template);
            BuildJackpotOverlay(refs, template, visuals);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BuildDebugKillButton(refs, template);
#endif

            RunOverlayUI.EnsureGlobalEventSystem();
            return refs;
        }

        static void BuildBattleBackdrop(RectTransform bg, BattleVisualContext visuals)
        {
            var backgroundSprite = Resources.Load<Sprite>("Battle/Art/Backgrounds/Battle_Background");
            if (backgroundSprite != null)
            {
                var art = CreatePanel(bg, "EnvironmentArt", Vector2.zero, Vector2.one, Color.white);
                var artImage = art.GetComponent<Image>();
                artImage.sprite = backgroundSprite;
                artImage.type = Image.Type.Simple;
                artImage.preserveAspect = false;
                artImage.raycastTarget = false;
            }
            else
            {
                CreatePanel(bg, "Sky", new Vector2(0, 0.42f), Vector2.one,
                    new Color(0.055f, 0.055f, 0.14f, 1f));
                CreatePanel(bg, "Horizon", new Vector2(0, 0.32f), new Vector2(1, 0.58f),
                    new Color(0.28f, 0.055f, 0.11f, 0.34f));
                CreatePanel(bg, "Ground", Vector2.zero, new Vector2(1, 0.42f),
                    new Color(0.018f, 0.018f, 0.035f, 1f));

                var moon = CreatePanel(bg, "EnemyBackGlow", new Vector2(0.5f, 0.52f),
                    new Vector2(0.83f, 0.92f), new Color(0.7f, 0.08f, 0.12f, 0.13f));
                moon.GetComponent<Image>().raycastTarget = false;
            }

            BattleUIChrome.CreateGlow(bg, "CrimsonNebula", new Vector2(0.37f, 0.25f),
                new Vector2(1.02f, 1.08f), new Color(0.82f, 0.04f, 0.14f, 0.19f));
            BattleUIChrome.CreateGlow(bg, "ArcaneNebula", new Vector2(0.05f, 0.36f),
                new Vector2(0.58f, 1.02f), new Color(0.25f, 0.2f, 0.86f, 0.12f));

            var topGradient = CreatePanel(bg, "AtmosphereGradient", new Vector2(0, 0.32f), Vector2.one,
                new Color(0.18f, 0.05f, 0.2f, 0.22f));
            topGradient.GetComponent<Image>().sprite = BattleUISprites.VerticalGradient;
            topGradient.GetComponent<Image>().raycastTarget = false;

            for (var i = 0; i < 22; i++)
            {
                var star = new GameObject($"ArcaneMote_{i}");
                star.transform.SetParent(bg, false);
                var rt = star.AddComponent<RectTransform>();
                var x = Mathf.Repeat(i * 0.173f + 0.07f, 1f);
                var y = 0.34f + Mathf.Repeat(i * 0.287f, 1f) * 0.62f;
                rt.anchorMin = new Vector2(x, y);
                rt.anchorMax = new Vector2(x, y);
                var size = 2f + (i % 4);
                rt.sizeDelta = new Vector2(size, size);
                var image = star.AddComponent<Image>();
                image.sprite = i % 3 == 0 ? BattleUISprites.Diamond : BattleUISprites.Circle;
                image.color = i % 2 == 0
                    ? new Color(1f, 0.55f, 0.38f, 0.28f)
                    : new Color(0.48f, 0.62f, 1f, 0.22f);
                image.raycastTarget = false;
            }

            // 周辺をグラデーションで落とし、アリーナ中央へ視線を集める。
            CreateGradientShade(bg, "LeftShade", Vector2.zero, new Vector2(0.36f, 1f), false, true, 0.82f);
            CreateGradientShade(bg, "RightShade", new Vector2(0.86f, 0f), Vector2.one, false, false, 0.55f);
            CreateGradientShade(bg, "TopShade", new Vector2(0f, 0.74f), Vector2.one, true, false, 0.7f);
            CreateGradientShade(bg, "BottomShade", Vector2.zero, new Vector2(1f, 0.34f), true, true, 0.85f);

            var spot = new GameObject("ArenaSpotlight");
            spot.transform.SetParent(bg, false);
            var spotRt = spot.AddComponent<RectTransform>();
            spotRt.anchorMin = spotRt.anchorMax = ArenaAnchor;
            spotRt.sizeDelta = new Vector2(1250f, 1050f);
            var spotImg = spot.AddComponent<Image>();
            spotImg.sprite = BattleUISprites.RadialGlow;
            spotImg.color = new Color(0.16f, 0.32f, 0.85f, 0.22f);
            spotImg.raycastTarget = false;
        }

        /// <summary>
        /// グラデーションの暗幕。vertical=true なら上下方向、darkAtMin=true なら下端/左端が最も暗い。
        /// </summary>
        static void CreateGradientShade(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            bool vertical, bool darkAtMin, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = vertical ? BattleUISprites.VerticalGradient : BattleUISprites.HorizontalGradient;
            img.color = new Color(0.005f, 0.006f, 0.018f, alpha);
            img.raycastTarget = false;
            if (darkAtMin)
                rt.localScale = vertical ? new Vector3(1f, -1f, 1f) : new Vector3(-1f, 1f, 1f);
        }

        // 敵ステージ＝右上の斜め平面。タイピングカードは傾けない（重なり防止）。
        static readonly Vector2 ArenaAnchor = new(0.735f, 0.58f);
        static readonly Vector2 EnemyFocusLocal = Vector2.zero;
        static readonly Vector3 ArenaTiltEuler = new(6f, -18f, 0f);
        const float ArenaSize = 780f;
        const float AttackRingDiameter = 260f;
        static readonly Color ArenaAccent = new(0.78f, 0.58f, 0.22f);
        static readonly Vector2 WordCardSize = new(300f, 112f);

        static RectTransform CreateCentered(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static Image CreateCenteredImage(Transform parent, string name, Vector2 position, Vector2 size,
            Sprite sprite, Color color)
        {
            var img = CreateCentered(parent, name, position, size).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        static void BuildArena(RectTransform root, BattleUIRefs refs, BattleVisualContext visuals)
        {
            var arenaGo = new GameObject("Arena");
            arenaGo.transform.SetParent(root, false);
            var arena = arenaGo.AddComponent<RectTransform>();
            arena.anchorMin = arena.anchorMax = ArenaAnchor;
            arena.pivot = new Vector2(0.5f, 0.42f);
            arena.sizeDelta = new Vector2(ArenaSize, ArenaSize);
            arena.localRotation = Quaternion.Euler(ArenaTiltEuler);
            refs.ArenaRoot = arena;

            BuildGroundPlane(arena);

            var focus = CreateCentered(arena, "CombatFocus", EnemyFocusLocal, new Vector2(420f, 420f));
            BuildAttackRing(focus, refs);
            BuildEnemy(focus, refs, visuals);
            BuildPartyApproachSlot(root, refs);
        }

        /// <summary>タイピング用カード列（画面正面・敵と別レイヤー）。</summary>
        static void BuildTypingRail(RectTransform root, BattleUIRefs refs)
        {
            var template = BattleUITemplateLoader.Default;
            var rail = CreatePanel(root, "TypingRail", new Vector2(0.27f, 0.188f), new Vector2(0.975f, 0.348f),
                new Color(0.02f, 0.022f, 0.045f, 0.82f), template?.windowPanel);
            AddOutline(rail.gameObject, new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.45f), 1);
            CreateLabel(rail, "RailLabel", "TYPE", 11, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.86f), new Vector2(0.12f, 0.98f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted)
                .fontStyle = FontStyle.Bold;

            var rowGo = new GameObject("WordRow");
            rowGo.transform.SetParent(rail, false);
            var row = rowGo.AddComponent<RectTransform>();
            row.anchorMin = new Vector2(0.02f, 0.06f);
            row.anchorMax = new Vector2(0.98f, 0.84f);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18;
            hlg.padding = new RectOffset(4, 4, 2, 2);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            for (var i = 0; i < refs.WordBoxes.Length; i++)
                refs.WordBoxes[i] = CreateWordCardLayoutChild(row, $"WordBox{i}", WordCardSize);
        }

        static void BuildGroundPlane(RectTransform arena)
        {
            var floorSprite = BattleUISprites.ArenaFloorPremium;
            if (floorSprite != null)
            {
                var floor = CreateCenteredImage(arena, "ArenaFloor", new Vector2(0f, -72f), new Vector2(520f, 300f),
                    floorSprite, new Color(1f, 1f, 1f, 0.75f));
                floor.transform.localScale = new Vector3(1f, 0.48f, 1f);
            }

            CreateCenteredImage(arena, "FloorShadow", new Vector2(0f, -68f), new Vector2(480f, 160f),
                BattleUISprites.RadialGlow, new Color(0f, 0f, 0f, 0.55f));
        }

        static void BuildAttackRing(RectTransform focus, BattleUIRefs refs)
        {
            var d = AttackRingDiameter;
            var ringPos = new Vector2(0f, -88f);
            refs.EnemyAttackRingTrack = CreateCenteredImage(focus, "AttackRingTrack", ringPos,
                new Vector2(d, d), BattleUISprites.Ring, new Color(0.04f, 0.05f, 0.1f, 0.65f));
            refs.EnemyAttackRing = CreateCenteredImage(focus, "AttackRingFill", ringPos, new Vector2(d, d),
                BattleUISprites.Ring, WithAlpha(new Color(0.95f, 0.35f, 0.28f), 0.88f));
            refs.EnemyAttackRing.type = Image.Type.Filled;
            refs.EnemyAttackRing.fillMethod = Image.FillMethod.Radial360;
            refs.EnemyAttackRing.fillOrigin = (int)Image.Origin360.Top;
            refs.EnemyAttackRing.fillClockwise = true;
            refs.EnemyAttackRing.fillAmount = 1f;
            refs.AttackBar = refs.EnemyAttackRing;

            var plate = CreateCentered(focus, "AttackLabelPlate", ringPos + new Vector2(0f, -d * 0.5f - 16f),
                new Vector2(140f, 22f));
            var plateImg = plate.gameObject.AddComponent<Image>();
            BattleUITheme.StylePanel(plateImg, new Color(0.01f, 0.02f, 0.05f, 0.75f));
            plateImg.raycastTarget = false;
            refs.AttackBarLabel = CreateLabel(plate, "AttackLabel", "次の攻撃", 12, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
            refs.AttackBarLabel.fontStyle = FontStyle.Bold;
        }

        static void BuildEnemy(RectTransform focus, BattleUIRefs refs, BattleVisualContext visuals)
        {
            refs.EnemyStage = CreateCentered(focus, "EnemyStage", new Vector2(0f, 36f), new Vector2(360f, 360f));
            refs.EnemyHitRoot = CreateCentered(refs.EnemyStage, "EnemyHitRoot", Vector2.zero, new Vector2(360f, 360f));

            refs.EnemyAura = CreateCenteredImage(refs.EnemyHitRoot, "EnemyAura", new Vector2(0f, 20f), new Vector2(380f, 380f),
                BattleUISprites.RadialGlow, new Color(0.88f, 0.08f, 0.18f, 0.12f));

            var bossImgGo = new GameObject("BossIllustration");
            bossImgGo.transform.SetParent(refs.EnemyHitRoot, false);
            var bossRt = bossImgGo.AddComponent<RectTransform>();
            bossRt.anchorMin = new Vector2(0.08f, 0.02f);
            bossRt.anchorMax = new Vector2(0.92f, 0.96f);
            bossRt.offsetMin = Vector2.zero;
            bossRt.offsetMax = Vector2.zero;
            refs.BossPortrait = bossImgGo.AddComponent<Image>();
            refs.BossPortrait.sprite = visuals.BossIllustration;
            refs.BossPortrait.preserveAspect = true;
            refs.BossPortrait.color = visuals.BossIllustration != null
                ? Color.white
                : new Color(1f, 1f, 1f, 0f);
            refs.BossPortrait.raycastTarget = false;
            var bossShadow = bossImgGo.AddComponent<Shadow>();
            bossShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            bossShadow.effectDistance = new Vector2(0f, -10f);

            refs.EnemyEmoji = CreateLabel(refs.EnemyHitRoot, "EnemyEmoji", visuals.BossEmojiNormal, 160,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                visuals.BossIllustration == null ? BattleUITheme.TextPrimary : Color.clear);
            refs.EnemyEmoji.raycastTarget = false;

            var popupLayer = new GameObject("EnemyDamagePopups");
            popupLayer.transform.SetParent(refs.EnemyHitRoot, false);
            refs.EnemyDamagePopupRoot = popupLayer.AddComponent<RectTransform>();
            refs.EnemyDamagePopupRoot.anchorMin = Vector2.zero;
            refs.EnemyDamagePopupRoot.anchorMax = Vector2.one;
            refs.EnemyDamagePopupRoot.offsetMin = Vector2.zero;
            refs.EnemyDamagePopupRoot.offsetMax = Vector2.zero;
        }

        /// <summary>手前の味方側スロット。将来「飛んで着地して攻撃」の出発／着地に使う。</summary>
        static void BuildPartyApproachSlot(RectTransform root, BattleUIRefs refs)
        {
            refs.PartyApproachRoot = CreatePanel(root, "PartyApproachRoot", new Vector2(0.27f, 0.355f),
                new Vector2(0.36f, 0.445f), Color.clear).GetComponent<RectTransform>();
            var pad = CreateCenteredImage(refs.PartyApproachRoot, "LaunchPad", Vector2.zero, new Vector2(180f, 64f),
                BattleUISprites.Circle, new Color(0.06f, 0.12f, 0.1f, 0.45f));
            pad.transform.localScale = new Vector3(1f, 0.42f, 1f);
            CreateCenteredImage(refs.PartyApproachRoot, "LaunchRim", Vector2.zero, new Vector2(180f, 64f),
                BattleUISprites.ThinRing, WithAlpha(BattleUITheme.AccentAlly, 0.4f)).transform.localScale =
                new Vector3(1f, 0.42f, 1f);
            CreateLabel(refs.PartyApproachRoot, "LaunchLabel", "ALLY", 10, TextAnchor.LowerCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0.35f), Vector2.zero, Vector2.zero, BattleUITheme.AccentAlly)
                .fontStyle = FontStyle.Bold;
        }

        static void BuildBossPlate(BattleUIRefs refs, BattleVisualContext visuals)
        {
            var title = string.IsNullOrEmpty(visuals.BossDisplayName)
                ? "BOSS"
                : visuals.BossDisplayName.ToUpperInvariant();

            var plate = CreateTopCentered(refs.RootCard, "BossPlate", -20f, new Vector2(900f, 86f));
            var bg = plate.gameObject.AddComponent<Image>();
            BattleUITheme.StylePanel(bg, new Color(0.015f, 0.012f, 0.035f, 0.82f));
            bg.raycastTarget = false;
            CreateRule(plate, "RuleTop", 1f, new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.65f));
            CreateRule(plate, "RuleBottom", 0f, new Color(0.9f, 0.2f, 0.25f, 0.5f));

            CreateLabel(plate, "Tag", "ENEMY", 12, TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0.5f), new Vector2(0.3f, 0.95f), Vector2.zero, Vector2.zero,
                new Color(0.95f, 0.45f, 0.45f, 0.9f)).fontStyle = FontStyle.Bold;
            var name = CreateLabel(plate, "BossName", $"◆  {title}  ◆", 24, TextAnchor.MiddleCenter,
                new Vector2(0.2f, 0.48f), new Vector2(0.8f, 1f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            name.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(name, new Color(0, 0, 0, 0.95f), new Vector2(2, -2));

            refs.EnemyHpBar = CreateBar(plate, "EnemyHpBar", new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.44f),
                BattleUITheme.BarTrack, new Color(0.92f, 0.14f, 0.2f));
            refs.EnemyHpText = CreateLabel(plate, "EnemyHp", "HP 1000 / 1000", 15, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.44f), Vector2.zero, Vector2.zero, Color.white);
            refs.EnemyHpText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(refs.EnemyHpText, new Color(0, 0, 0, 1f), new Vector2(1, -1));
        }

        static void BuildBattleLog(BattleUIRefs refs)
        {
            var plate = CreateTopCentered(refs.RootCard, "BattleLog", -112f, new Vector2(720f, 34f));
            var bg = plate.gameObject.AddComponent<Image>();
            BattleUITheme.StylePanel(bg, new Color(0f, 0f, 0f, 0.55f));
            bg.raycastTarget = false;
            refs.LogText = CreateLabel(plate, "Log", "戦闘開始！", 17, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.74f, 0.5f));
            BattleUITheme.AddTextShadow(refs.LogText, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));
        }

        static void BuildPartyHpStrip(BattleUIRefs refs)
        {
            var strip = CreatePanel(refs.RootCard, "PartyHpStrip", new Vector2(0.295f, 0.148f),
                new Vector2(0.5f, 0.182f), Color.clear);
            strip.GetComponent<Image>().raycastTarget = false;
            CreateLabel(strip, "Label", "PARTY HP", 13, TextAnchor.MiddleLeft,
                Vector2.zero, new Vector2(0.24f, 1f), Vector2.zero, Vector2.zero, BattleUITheme.AccentAlly).fontStyle =
                FontStyle.Bold;
            refs.PlayerHpBar = CreateBar(strip, "PlayerHpBar", new Vector2(0.25f, 0.12f), new Vector2(1f, 0.88f),
                BattleUITheme.BarTrack, BattleUITheme.AccentAlly);
            refs.PlayerHpText = CreateLabel(strip, "PlayerHp", "100 / 100", 13, TextAnchor.MiddleCenter,
                new Vector2(0.25f, 0f), Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            refs.PlayerHpText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(refs.PlayerHpText, new Color(0, 0, 0, 1f), new Vector2(1, -1));
        }

        static void BuildControlStrip(BattleUIRefs refs)
        {
            var strip = CreatePanel(refs.RootCard, "ControlStrip", new Vector2(0.71f, 0.148f),
                new Vector2(0.975f, 0.182f), Color.clear);
            strip.GetComponent<Image>().raycastTarget = false;
            CreateLabel(strip, "Hint", "Space ポーズ　Enter リロール", 13, TextAnchor.MiddleLeft,
                Vector2.zero, new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
            refs.RerollBadge = CreateLabel(strip, "Reroll", "リロール 3/3", 14, TextAnchor.MiddleRight,
                new Vector2(0.62f, 0f), Vector2.one, Vector2.zero, Vector2.zero, BattleUITheme.AccentBoss);
            refs.RerollBadge.fontStyle = FontStyle.Bold;
        }

        static RectTransform CreateTopCentered(Transform parent, string name, float y, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(ArenaAnchor.x, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = size;
            return rt;
        }

        static void CreateRule(Transform parent, string name, float anchorY, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 2f);
            var img = go.AddComponent<Image>();
            BattleUITheme.StylePanel(img, color);
            img.raycastTarget = false;
        }

        static void BuildCountdownOverlay(BattleUIRefs refs)
        {
            refs.CountdownOverlay = CreatePanel(refs.RootCard, "CountdownOverlay", Vector2.zero, Vector2.one,
                new Color(0.005f, 0.008f, 0.02f, 0.52f)).gameObject;
            BattleUIChrome.CreateGlow(refs.CountdownOverlay.transform, "CountdownGlow",
                new Vector2(ArenaAnchor.x - 0.18f, 0.2f), new Vector2(ArenaAnchor.x + 0.18f, 0.86f),
                new Color(BattleUITheme.Cyan.r, BattleUITheme.Cyan.g, BattleUITheme.Cyan.b, 0.2f));

            var ringGo = new GameObject("CountdownRing");
            ringGo.transform.SetParent(refs.CountdownOverlay.transform, false);
            var ringRt = ringGo.AddComponent<RectTransform>();
            ringRt.anchorMin = ArenaAnchor;
            ringRt.anchorMax = ArenaAnchor;
            ringRt.sizeDelta = new Vector2(270, 270);
            refs.CountdownRing = ringGo.AddComponent<Image>();
            refs.CountdownRing.sprite = BattleUISprites.Circle;
            refs.CountdownRing.type = Image.Type.Filled;
            refs.CountdownRing.fillMethod = Image.FillMethod.Radial360;
            refs.CountdownRing.fillOrigin = (int)Image.Origin360.Top;
            refs.CountdownRing.fillClockwise = true;
            refs.CountdownRing.fillAmount = 1f;
            refs.CountdownRing.color = BattleUITheme.Cyan;
            refs.CountdownRing.raycastTarget = false;

            var inner = CreatePanel(ringRt, "RingInner", new Vector2(0.12f, 0.12f),
                new Vector2(0.88f, 0.88f), new Color(0.008f, 0.012f, 0.035f, 0.96f));
            inner.GetComponent<Image>().sprite = BattleUISprites.Circle;
            inner.GetComponent<Image>().raycastTarget = false;

            refs.CountdownText = CreateLabel(refs.CountdownOverlay.transform, "Count", "3", 112, TextAnchor.MiddleCenter,
                ArenaAnchor, ArenaAnchor,
                Vector2.zero, new Vector2(270, 270), Color.white);
            refs.CountdownText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(refs.CountdownText, new Color(0, 0, 0, 0.95f), new Vector2(5, -5));
            CreateLabel(refs.CountdownOverlay.transform, "Ready", "PREPARE TO TYPE", 18, TextAnchor.MiddleCenter,
                new Vector2(0.35f, 0.31f), new Vector2(0.65f, 0.38f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextMuted);
        }

        static void BuildResultOverlay(BattleUIRefs refs, BattleUITemplateDefinition template)
        {
            refs.ResultOverlay = CreatePanel(refs.RootCard, "ResultOverlay", Vector2.zero, Vector2.one,
                new Color(0.004f, 0.006f, 0.018f, 0.94f)).gameObject;
            refs.ResultCanvasGroup = refs.ResultOverlay.AddComponent<CanvasGroup>();
            BattleUIChrome.CreateGlow(refs.ResultOverlay.transform, "VictoryGoldGlow",
                new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.96f),
                new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.2f));

            var window = BattleUIChrome.CreateLuxuryWindow(refs.ResultOverlay.transform, "ResultWindow",
                new Vector2(0.235f, 0.13f), new Vector2(0.765f, 0.87f), "BATTLE REPORT", BattleUITheme.Gold, template);
            var body = window.Find("Body") ?? window;
            var victory = CreateLabel(body, "Victory", "VICTORY", 48, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.96f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);
            victory.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(victory, new Color(0, 0, 0, 0.95f), new Vector2(4, -4));

            refs.ResultRank = CreateLabel(body, "Rank", "A", 104, TextAnchor.MiddleCenter,
                new Vector2(0.12f, 0.43f), new Vector2(0.42f, 0.76f), Vector2.zero, Vector2.zero, Color.white);
            refs.ResultRank.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(refs.ResultRank, new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g,
                BattleUITheme.Gold.b, 0.8f), new Vector2(4, -4));
            refs.ResultStats = CreateLabel(body, "Stats", "", 18, TextAnchor.MiddleLeft,
                new Vector2(0.44f, 0.39f), new Vector2(0.9f, 0.76f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);

            var buttonRt = CreatePanel(body, "ContinueButton", new Vector2(0.22f, 0.08f),
                new Vector2(0.78f, 0.22f), Color.white, template?.button);
            refs.ResultContinueButton = buttonRt.gameObject.AddComponent<Button>();
            var buttonColors = refs.ResultContinueButton.colors;
            buttonColors.highlightedColor = new Color(1.18f, 1.12f, 1.02f, 1f);
            buttonColors.pressedColor = new Color(0.75f, 0.7f, 0.72f, 1f);
            refs.ResultContinueButton.colors = buttonColors;
            var buttonText = CreateLabel(buttonRt, "Text", "報酬へ進む", 19, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            buttonText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(buttonText, new Color(0, 0, 0, 0.95f), new Vector2(2, -2));

            refs.ResultOverlay.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>テスト用：戦闘画面中央の「撃破」ボタン。</summary>
        static void BuildDebugKillButton(BattleUIRefs refs, BattleUITemplateDefinition template)
        {
            var buttonRt = CreatePanel(refs.RootCard, "DebugKillButton", new Vector2(0.885f, 0.79f),
                new Vector2(0.975f, 0.84f), new Color(0.45f, 0.1f, 0.12f, 0.82f), template?.button);
            buttonRt.SetAsLastSibling();
            AddOutline(buttonRt.gameObject, new Color(1f, 0.35f, 0.35f, 0.85f), 2);
            refs.DebugKillButton = buttonRt.gameObject.AddComponent<Button>();
            var buttonColors = refs.DebugKillButton.colors;
            buttonColors.highlightedColor = new Color(1.15f, 0.45f, 0.45f, 1f);
            buttonColors.pressedColor = new Color(0.55f, 0.08f, 0.1f, 1f);
            refs.DebugKillButton.colors = buttonColors;
            var buttonText = CreateLabel(buttonRt, "Text", "撃破", 22, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            buttonText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(buttonText, new Color(0, 0, 0, 0.95f), new Vector2(2, -2));
        }
#endif

        static void BuildPauseOverlay(BattleUIRefs refs)
        {
            refs.PauseOverlay = CreatePanel(refs.RootCard, "PauseOverlay", Vector2.zero, Vector2.one,
                new Color(0.01f, 0.015f, 0.035f, 0.88f)).gameObject;
            refs.PauseOverlay.SetActive(false);
            CreateLabel(refs.PauseOverlay.transform, "PauseTitle", "一時停止", 48, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), Vector2.zero,
                new Vector2(600, 80), BattleUITheme.TextPrimary);
            CreateLabel(refs.PauseOverlay.transform, "PauseSub", "Space で再開", 20, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero,
                new Vector2(600, 40), BattleUITheme.AccentAlly);
        }

        static void BuildCutinOverlay(BattleUIRefs refs, BattleUITemplateDefinition template)
        {
            refs.CutinOverlay = CreatePanel(refs.RootCard, "CutinOverlay", Vector2.zero, Vector2.one,
                new Color(0, 0, 0, 0)).gameObject;
            refs.CutinOverlay.SetActive(false);
            var cutinBanner = CreatePanel(refs.CutinOverlay.transform, "Banner",
                new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.62f),
                new Color(0.08f, 0.02f, 0.08f, 0.96f), template?.windowPanel);
            AddOutline(cutinBanner.gameObject, BattleUITheme.AccentBoss, 2);
            refs.CutinPortrait = BattleUIChrome.CreatePortraitPanel(cutinBanner, null, template, 112f, 150f);
            var portraitRt = refs.CutinPortrait.transform.parent.GetComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0, 0.5f);
            portraitRt.anchorMax = new Vector2(0, 0.5f);
            portraitRt.anchoredPosition = new Vector2(90, 0);
            refs.CutinChar = CreateLabel(cutinBanner, "Char", "", 24, TextAnchor.MiddleRight,
                new Vector2(0.3f, 0.58f), new Vector2(0.94f, 0.84f), Vector2.zero, Vector2.zero,
                BattleUITheme.AccentBoss);
            refs.CutinSkill = CreateLabel(cutinBanner, "Skill", "", 38, TextAnchor.MiddleRight,
                new Vector2(0.3f, 0.18f), new Vector2(0.94f, 0.56f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
        }

        static void BuildJackpotOverlay(
            BattleUIRefs refs,
            BattleUITemplateDefinition template,
            BattleVisualContext visuals)
        {
            refs.JackpotOverlay = CreatePanel(
                refs.RootCard,
                "JackpotOverlay",
                Vector2.zero,
                Vector2.one,
                new Color(0, 0, 0, 0)).gameObject;

            refs.JackpotBlackout = CreatePanel(
                refs.JackpotOverlay.transform,
                "Blackout",
                Vector2.zero,
                Vector2.one,
                Color.black).GetComponent<Image>();

            var streak = CreatePanel(
                refs.JackpotOverlay.transform,
                "LightStreak",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Color.white);
            streak.sizeDelta = new Vector2(760f, 18f);
            refs.JackpotLightStreak = streak;
            BattleUIChrome.CreateGlow(
                streak,
                "StreakGlow",
                new Vector2(-0.08f, -4f),
                new Vector2(1.08f, 5f),
                new Color(0.65f, 0.85f, 1f, 0.5f));

            refs.JackpotCutinRoot = CreatePanel(
                refs.JackpotOverlay.transform,
                "TripleCutin",
                new Vector2(0.06f, 0.16f),
                new Vector2(0.94f, 0.84f),
                new Color(0.025f, 0.01f, 0.055f, 0.97f),
                template?.windowPanel);
            refs.JackpotCutinGroup = refs.JackpotCutinRoot.gameObject.AddComponent<CanvasGroup>();
            AddOutline(refs.JackpotCutinRoot.gameObject, BattleUITheme.Gold, 3);

            refs.JackpotTitle = CreateLabel(
                refs.JackpotCutinRoot,
                "Title",
                "JACKPOT",
                64,
                TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.78f),
                new Vector2(0.9f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                BattleUITheme.Gold);
            refs.JackpotTitle.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(
                refs.JackpotTitle,
                new Color(0, 0, 0, 0.95f),
                new Vector2(4, -4));

            var characterIndex = 0;
            foreach (var pair in visuals.Characters)
            {
                if (characterIndex >= 3)
                    break;

                var xMin = 0.04f + characterIndex * 0.32f;
                var xMax = xMin + 0.28f;
                var card = CreatePanel(
                    refs.JackpotCutinRoot,
                    $"JackpotCharacter_{characterIndex}",
                    new Vector2(xMin, 0.12f),
                    new Vector2(xMax, 0.76f),
                    new Color(0.11f, 0.035f, 0.13f, 0.96f),
                    template?.partyCard);
                var portrait = BattleUIChrome.CreatePortraitPanel(
                    card,
                    pair.Value.Portrait,
                    template,
                    210f,
                    280f);
                var portraitFrame = portrait.transform.parent.GetComponent<RectTransform>();
                portraitFrame.anchorMin = new Vector2(0.5f, 0.56f);
                portraitFrame.anchorMax = new Vector2(0.5f, 0.56f);
                portraitFrame.anchoredPosition = Vector2.zero;
                CreateLabel(
                    card,
                    "CharacterName",
                    pair.Key,
                    26,
                    TextAnchor.MiddleCenter,
                    new Vector2(0.05f, 0.02f),
                    new Vector2(0.95f, 0.18f),
                    Vector2.zero,
                    Vector2.zero,
                    Color.white).fontStyle = FontStyle.Bold;
                characterIndex++;
            }

            refs.JackpotDamageText = CreateLabel(
                refs.JackpotOverlay.transform,
                "JackpotDamage",
                string.Empty,
                82,
                TextAnchor.MiddleCenter,
                new Vector2(0.12f, 0.04f),
                new Vector2(0.88f, 0.18f),
                Vector2.zero,
                Vector2.zero,
                Color.white);
            refs.JackpotDamageText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(
                refs.JackpotDamageText,
                new Color(0, 0, 0, 0.95f),
                new Vector2(5, -5));

            refs.JackpotOverlay.SetActive(false);
        }

        static void SetPanelOpacity(RectTransform panel, float alpha)
        {
            var image = panel != null ? panel.GetComponent<Image>() : null;
            if (image == null)
                return;
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        static void BuildMadnessMeter(Transform parent, BattleUIRefs refs)
        {
            CreateLabel(parent, "MadnessHeader", "狂気", 11, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.28f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextMuted).fontStyle = FontStyle.Bold;

            var track = CreatePanel(parent, "MadnessTrack", new Vector2(0.06f, 0.05f), new Vector2(0.2f, 0.23f),
                BattleUITheme.BarTrack);
            track.GetComponent<Image>().raycastTarget = false;
            var fillGo = CreatePanel(track, "MadnessFill", new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.96f),
                RunMadnessState.StageColor(RunMadnessState.MadnessStage.Calm), BattleUITemplateLoader.Default?.barFill);
            refs.MadnessFill = fillGo.GetComponent<Image>();
            refs.MadnessFill.type = Image.Type.Filled;
            refs.MadnessFill.fillMethod = Image.FillMethod.Vertical;
            refs.MadnessFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            refs.MadnessFill.fillAmount = 0f;
            refs.MadnessFill.raycastTarget = false;

            refs.MadnessText = CreateLabel(parent, "MadnessValue", "0 / 1000", 13, TextAnchor.MiddleLeft,
                new Vector2(0.22f, 0.05f), new Vector2(0.96f, 0.23f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
            refs.MadnessText.fontStyle = FontStyle.Bold;
        }

        static WordBoxUI CreateWordCardLayoutChild(Transform parent, string name, Vector2 size)
        {
            var rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.AddComponent<RectTransform>();
            var le = rootGo.AddComponent<LayoutElement>();
            le.minWidth = size.x;
            le.preferredWidth = size.x;
            le.flexibleWidth = 1f;
            le.minHeight = size.y;
            le.preferredHeight = size.y;
            return PopulateWordCard(root, size);
        }

        static WordBoxUI PopulateWordCard(RectTransform root, Vector2 size)
        {
            var tpl = BattleUITemplateLoader.Default;
            var premium = BattleUISprites.WordCardPremium;

            var glowRt = CreatePanel(root, "Glow", Vector2.zero, Vector2.one, Color.clear);
            glowRt.offsetMin = new Vector2(-12f, -8f);
            glowRt.offsetMax = new Vector2(12f, 8f);
            var glow = glowRt.GetComponent<Image>();
            glow.sprite = BattleUISprites.RadialGlow;
            glow.color = WithAlpha(ArenaAccent, 0.08f);
            glow.raycastTarget = false;

            var frameRt = CreatePanel(root, "Frame", Vector2.zero, Vector2.one, Color.white);
            var frame = frameRt.GetComponent<Image>();
            if (premium != null)
            {
                frame.sprite = premium;
                frame.type = Image.Type.Sliced;
                frame.color = Color.white;
            }
            else
                BattleUIImage.ApplyPanel(frame, Color.white, tpl?.wordBox ?? tpl?.windowPanel);
            frame.raycastTarget = false;

            var fill = CreatePanel(frameRt, "Fill", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
            fill.offsetMin = new Vector2(16f, 14f);
            fill.offsetMax = new Vector2(-16f, -14f);

            var badgePanel = CreatePanel(fill, "ElementBadge", new Vector2(0.8f, 0.7f), new Vector2(0.98f, 0.96f),
                new Color(0.02f, 0.03f, 0.07f, 0.92f));
            var badgeImg = badgePanel.GetComponent<Image>();
            BattleUITheme.EnsureSprite(badgeImg);
            var tag = CreateLabel(badgePanel, "Tag", "?", 12, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            tag.fontStyle = FontStyle.Bold;

            var word = CreateLabel(fill, "Word", "", 30, TextAnchor.UpperCenter,
                new Vector2(0.04f, 0.4f), new Vector2(0.78f, 0.98f), Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            word.supportRichText = true;
            word.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(word, new Color(0, 0, 0, 0.95f), new Vector2(2, -2));

            var ruby = CreateLabel(fill, "Ruby", "", 18, TextAnchor.LowerCenter,
                new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.44f), Vector2.zero, Vector2.zero, BattleUITheme.TextMuted);
            ruby.supportRichText = true;
            ruby.fontStyle = FontStyle.Bold;

            var track = CreatePanel(fill, "ProgressTrack", new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.12f),
                new Color(1f, 1f, 1f, 0.08f));
            track.GetComponent<Image>().raycastTarget = false;
            var progressRt = CreatePanel(track, "Progress", Vector2.zero, Vector2.one, BattleUITheme.TextTyped);
            var progress = progressRt.GetComponent<Image>();
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = 0f;
            progress.raycastTarget = false;

            return new WordBoxUI
            {
                FrameRoot = root,
                Glow = glow,
                Frame = frame,
                Fill = fill.GetComponent<Image>(),
                ElementStripe = badgeImg,
                Progress = progress,
                TagText = tag,
                RubyText = ruby,
                WordText = word
            };
        }

        static Image CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bg, Color fill)
        {
            var template = BattleUITemplateLoader.Default;
            var bgGo = CreatePanel(parent, name + "Bg", anchorMin, anchorMax, bg, template?.barTrack);
            var backGlow = CreatePanel(bgGo.transform, "Glow", new Vector2(0, 0.05f), new Vector2(1, 0.95f),
                new Color(fill.r, fill.g, fill.b, 0.18f));
            backGlow.GetComponent<Image>().raycastTarget = false;
            var fillGo = CreatePanel(bgGo.transform, "Fill", new Vector2(0.01f, 0.16f), new Vector2(0.99f, 0.84f),
                fill, template?.barFill);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sliced = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            BattleUIImage.ApplyPanel(img, color, sliced);
            return rt;
        }

        static void AddOutline(GameObject go, Color color, int distance)
        {
            var outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        static Text CreateLabel(Transform parent, string name, string text, int fontSize, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            if (sizeDelta != Vector2.zero || anchoredPos != Vector2.zero)
            {
                rt.sizeDelta = sizeDelta;
                rt.anchoredPosition = anchoredPos;
            }
            else
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var label = go.AddComponent<Text>();
            label.font = BattleUITheme.Font;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.text = text;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static RectTransform CreatePanelPublic(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sliced = null)
            => CreatePanel(parent, name, anchorMin, anchorMax, color, sliced);

        public static Text CreateLabelPublic(Transform parent, string name, string text, int fontSize, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
            => CreateLabel(parent, name, text, fontSize, anchor, anchorMin, anchorMax, anchoredPos, sizeDelta, color);

        public static void AddOutlinePublic(GameObject go, Color color, int distance) => AddOutline(go, color, distance);

        static void CreateVignette(Transform bg)
        {
            CreatePanel(bg, "VignetteTop", new Vector2(0, 0.75f), new Vector2(1, 1), new Color(0, 0, 0, 0.45f));
            CreatePanel(bg, "VignetteBottom", new Vector2(0, 0), new Vector2(1, 0.2f), new Color(0, 0, 0, 0.5f));
        }

        static void CreateGridOverlay(Transform bg)
        {
            var grid = CreatePanel(bg, "Grid", Vector2.zero, Vector2.one, new Color(1, 0.2f, 0.2f, 0.03f));
            grid.GetComponent<Image>().raycastTarget = false;
        }

        static void AddGlowPlate(RectTransform root, Color accent)
        {
            var glow = CreatePanel(root, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Color(accent.r, accent.g, accent.b, 0.08f));
            var rt = glow.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900, 500);
            glow.SetAsFirstSibling();
        }
    }
}
