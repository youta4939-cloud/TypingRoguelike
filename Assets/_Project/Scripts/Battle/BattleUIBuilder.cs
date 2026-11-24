using System.Collections.Generic;
using TMPro;
using TypingRoguelike.Data;
using TypingRoguelike.Battle.Deck;
using TypingRoguelike.Battle.Visual;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public sealed class BattleUIRefs
    {
        public TMP_Text PlayerHpText;
        public TMP_Text EnemyHpText;
        public Text EnemyEmoji;
        public TMP_Text LogText;
        public TMP_Text RerollBadge;
        public TMP_Text JackpotMeterText;
        public Image MadnessFill;
        public Image MadnessEmpty;
        public TMP_Text MadnessText;
        public TMP_Text AttackBarLabel;
        public BattleAttackForecastView AttackForecast;
        public Image PlayerHpBar;
        public Image EnemyHpBar;
        public BossHpGauge EnemyHpGauge;
        public EnemyStatusHud EnemyStatus;
        public Image AttackBar;
        public bool AttackRingFillsOnApproach;
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
        public Dictionary<int, GameObject> SkillReadyMarks = new();
        public Dictionary<int, BattlePartyCardUI.SkillRowView> SkillRows = new();
        public BattleE5bStage E5b;
        public StockPanel Stock;
        public Image E5bNoticeFill;
        public Image E5bNoticeOutline;
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
        public GameObject HeavyAttackCallout;
        public Text HeavyAttackLabel;
        public Image HeavyAttackGauge;
        public BigMouthIdle BigMouth;
        public PaperAngelIdle PaperAngel;
        public SkeletonSamuraiIdle SkeletonSamurai;
        public EnemySpritePairPresenter SpritePairPresenter;
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
        public Text ResultTitle;
        public Text ResultHp;
        public Text ResultMadness;
        public Text ResultTime;
        public Button ResultContinueButton;
        public Button DebugKillButton;
        public Text DebugCombatReadout;
        public Button DebugCritTestButton;
        public Button DebugAliceSlashButton;
        public RectTransform SkillListRoot;
        public DeckHandUIView DeckHand;
    }

    public sealed class WordBoxUI
    {
        public RectTransform FrameRoot;
        public Image Glow;
        public Image Frame;
        public Image Fill;
        public Image Progress;
        public Image ElementStripe;
        public TMP_Text TagText;
        public TMP_Text RubyText;
        public TMP_Text WordText;
        public bool E5b;
        public DeckHandUIView.WordGuide DeckGuide;
    }

    public static class BattleUIBuilder
    {
        public static bool SuppressPrefab;

        public static BattleUIRefs Build(
            Transform parent,
            IReadOnlyList<BattleSkillData> skills,
            HashSet<int> innateSkillIds = null,
            BattleVisualContext visuals = null)
        {
            visuals ??= BattleVisualContext.FromEncounter(null, null);
            if (!SuppressPrefab && !GameModeConfig.UseDeckMode)
            {
                var bound = TryBindPrefab(parent, skills, visuals);
                if (bound != null)
                    return bound;
            }
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
            var deckMode = GameModeConfig.UseDeckMode;
            if (deckMode)
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bg = CreatePanel(canvasGo.transform, "Background", Vector2.zero, Vector2.one,
                new Color(0.025f, 0.025f, 0.055f, 1f));

            refs.RootCard = CreatePanel(bg.transform, "BattleHud", Vector2.zero, Vector2.one,
                new Color(0, 0, 0, 0)).GetComponent<RectTransform>();

            if (deckMode)
            {
                refs.RootCard.anchorMin = refs.RootCard.anchorMax = new Vector2(0.5f, 0.5f);
                refs.RootCard.anchoredPosition = Vector2.zero;
                refs.RootCard.sizeDelta = scaler.referenceResolution;
            }

            BattleDungeonBackdrop.Ensure(refs.RootCard);
            BuildArena(refs.RootCard, refs, visuals);
            BattleLayoutE5b.Build(refs.RootCard, refs, skills, visuals);

            BuildBossPlate(refs, visuals);

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

        /// <summary>背景を沈め、周辺を落として中央の敵へ視線を集める。</summary>
        static void BuildBackdropShading(RectTransform bg)
        {
            var dim = CreatePanel(bg, "Dim", Vector2.zero, Vector2.one, new Color(0.01f, 0.008f, 0.014f, 0.5f));
            dim.GetComponent<Image>().raycastTarget = false;

            CreateGradientShade(bg, "LeftShade", Vector2.zero, new Vector2(0.34f, 1f), false, true, 0.9f);
            CreateGradientShade(bg, "RightShade", new Vector2(0.8f, 0f), Vector2.one, false, false, 0.8f);
            CreateGradientShade(bg, "TopShade", new Vector2(0f, 0.8f), Vector2.one, true, false, 0.75f);
            CreateGradientShade(bg, "BottomShade", Vector2.zero, new Vector2(1f, 0.3f), true, true, 0.9f);

            var spot = new GameObject("ArenaSpotlight");
            spot.transform.SetParent(bg, false);
            var spotRt = spot.AddComponent<RectTransform>();
            spotRt.anchorMin = spotRt.anchorMax = ArenaAnchor;
            spotRt.sizeDelta = new Vector2(1100f, 1000f);
            var spotImg = spot.AddComponent<Image>();
            spotImg.sprite = BattleUISprites.RadialGlow;
            spotImg.color = new Color(0.1f, 0.35f, 0.45f, 0.16f);
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

        // 敵は画面中央。単語カードは円の左・右・下。
        static readonly Vector2 ArenaAnchor = new(0.6f, 0.515f);
        static readonly Vector2 EnemyFocusLocal = Vector2.zero;
        const float ArenaSize = 600f;
        const float AttackRingDiameter = 560f;
        static readonly Vector2 WordCardSize = new(252f, 106f);
        static readonly Color ArenaAccent = new(0.78f, 0.58f, 0.22f);

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
            arena.pivot = new Vector2(0.5f, 0.5f);
            arena.sizeDelta = new Vector2(ArenaSize, ArenaSize);
            refs.ArenaRoot = arena;

            var focus = CreateCentered(arena, "CombatFocus", EnemyFocusLocal,
                new Vector2(AttackRingDiameter, AttackRingDiameter));
            BuildEnemy(focus, refs, visuals);
            PlaceEnemyOnFloor(refs, visuals);
            BuildPartyApproachSlot(root, refs);
        }

        static void PlaceEnemyOnFloor(BattleUIRefs refs, BattleVisualContext visuals)
        {
            if (refs.ArenaRoot == null || refs.EnemyStage == null || refs.EnemyHitRoot == null)
                return;

            refs.ArenaRoot.anchorMin = refs.ArenaRoot.anchorMax = visuals.GroundPosition;
            refs.ArenaRoot.anchoredPosition = Vector2.zero;
            var feet = BattleVisualContext.FeetAnchor.y;
            refs.EnemyStage.anchoredPosition = Vector2.up * (refs.EnemyHitRoot.sizeDelta.y * (0.5f - feet));

            var size = visuals.IsLargeEnemy ? new Vector2(330f, 66f) : new Vector2(205f, 42f);
            var shadow = CreateCenteredImage(refs.ArenaRoot, "EnemyFloorShadow", Vector2.zero, size,
                BattleUISprites.RadialGlow, new Color(0.035f, 0.028f, 0.04f, 0.87f));
            shadow.transform.SetAsFirstSibling();

            if (refs.EnemyEmoji != null)
            {
                refs.EnemyEmoji.alignment = TextAnchor.LowerCenter;
                refs.EnemyEmoji.rectTransform.anchorMin = new Vector2(0f, feet);
            }
        }

        /// <summary>単語カードは攻撃円の左・右・下。板では囲まない。</summary>
        static void BuildTypingRail(RectTransform root, BattleUIRefs refs)
        {
            // 円の中心からのオフセット（参照解像度 1920x1080 の px）。
            var offsets = new[]
            {
                new Vector2(-282f, 18f),
                new Vector2(292f, 18f),
                new Vector2(0f, -284f)
            };
            for (var i = 0; i < refs.WordBoxes.Length; i++)
            {
                var rt = BattleHudKit.Place(root, $"WordBox{i}", ArenaAnchor, new Vector2(0.5f, 0.5f), offsets[i],
                    WordCardSize);
                refs.WordBoxes[i] = PopulateWordCard(rt, WordCardSize);
            }
        }

        static void BuildGroundPlane(RectTransform arena)
        {
            CreateCenteredImage(arena, "InnerDark", Vector2.zero, new Vector2(AttackRingDiameter, AttackRingDiameter),
                BattleUISprites.RadialGlow, new Color(0f, 0f, 0f, 0.55f));
        }

        static void BuildAttackRing(RectTransform focus, BattleUIRefs refs)
        {
            var d = AttackRingDiameter;
            var ringPos = Vector2.zero;
            var cyan = new Color(0.55f, 0.95f, 1f);
            CreateCenteredImage(focus, "AttackRingGlow", ringPos, new Vector2(d + 90f, d + 90f),
                BattleUISprites.RadialGlow, new Color(cyan.r, cyan.g, cyan.b, 0.1f));
            CreateCenteredImage(focus, "AttackRingOuterHair", ringPos, new Vector2(d + 26f, d + 26f),
                BattleUISprites.ThinRing, new Color(cyan.r, cyan.g, cyan.b, 0.22f));
            refs.EnemyAttackRingTrack = CreateCenteredImage(focus, "AttackRingTrack", ringPos,
                new Vector2(d, d), BattleUISprites.Ring, new Color(0.05f, 0.12f, 0.14f, 0.7f));
            refs.EnemyAttackRing = CreateCenteredImage(focus, "AttackRingFill", ringPos, new Vector2(d, d),
                BattleUISprites.Ring, new Color(0.72f, 0.98f, 1f, 0.95f));
            var ringBloom = CreateCenteredImage(refs.EnemyAttackRing.transform, "Bloom", Vector2.zero,
                new Vector2(d + 14f, d + 14f), BattleUISprites.Ring, new Color(cyan.r, cyan.g, cyan.b, 0.35f));
            ringBloom.type = Image.Type.Filled;
            ringBloom.fillMethod = Image.FillMethod.Radial360;
            ringBloom.fillOrigin = (int)Image.Origin360.Top;
            refs.EnemyAttackRing.gameObject.AddComponent<RingBloomFollow>().Bloom = ringBloom;
            refs.EnemyAttackRing.type = Image.Type.Filled;
            refs.EnemyAttackRing.fillMethod = Image.FillMethod.Radial360;
            refs.EnemyAttackRing.fillOrigin = (int)Image.Origin360.Top;
            refs.EnemyAttackRing.fillClockwise = true;
            refs.EnemyAttackRing.fillAmount = 1f;
            refs.AttackBar = refs.EnemyAttackRing;

            var label = CreateCentered(focus, "AttackLabel", new Vector2(0f, d * 0.5f + 24f), new Vector2(160f, 30f));
            refs.AttackBarLabel = BattleHudKit.Text(label, "Text", "", BattleHudKit.Condensed, 24f, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            BattleHudKit.Outline(refs.AttackBarLabel, 0.22f, Color.black);
        }

        static void CreateAnimatedEnemy(BattleUIRefs refs, BattleVisualContext visuals)
        {
            var kind = visuals?.BattleVisual ?? EnemyBattleVisualKind.Auto;
            if (kind == EnemyBattleVisualKind.StaticIllustration)
                return;

            Component rig = null;
            if (kind == EnemyBattleVisualKind.SkeletonSamurai)
            {
                refs.SkeletonSamurai = SkeletonSamuraiAssembler.CreateUi(refs.EnemyHitRoot);
                rig = refs.SkeletonSamurai;
            }
            else if (kind == EnemyBattleVisualKind.PaperAngel)
            {
                refs.PaperAngel = PaperAngelAssembler.CreateUi(refs.EnemyHitRoot);
                rig = refs.PaperAngel;
            }
            else if (kind == EnemyBattleVisualKind.BigMouth || kind == EnemyBattleVisualKind.Auto)
            {
                refs.BigMouth = BigMouthAssembler.CreateUi(refs.EnemyHitRoot);
                rig = refs.BigMouth;
            }

            if (rig == null)
                return;

            var rect = (RectTransform)rig.transform;
            rect.anchorMin = rect.anchorMax = BattleVisualContext.FeetAnchor;
            var height = rect.sizeDelta.y * (1f - rect.pivot.y);
            var maxHeight = visuals != null && visuals.IsLargeEnemy ? BattleVisualContext.LargeEnemyMaxHeight : 360f;
            var scale = Mathf.Min(rect.localScale.x, maxHeight / Mathf.Max(1f, height));
            rect.localScale = Vector3.one * scale;
        }

        static void BuildStaticEnemy(BattleUIRefs refs, BattleVisualContext visuals)
        {
            var illustration = CreateCenteredImage(refs.EnemyHitRoot, "BossIllustration", Vector2.zero,
                Vector2.zero, visuals.BossIllustration, visuals.BossIllustration != null ? Color.white : Color.clear);
            illustration.rectTransform.anchorMin = illustration.rectTransform.anchorMax = BattleVisualContext.FeetAnchor;
            illustration.rectTransform.pivot = visuals.Enemy != null
                ? visuals.Enemy.visual.feetPivotIdle
                : new Vector2(0.5f, 0f);
            refs.BossPortrait = illustration;
            FitStaticEnemy(illustration, visuals.IsLargeEnemy);
        }

        public static void FitStaticEnemy(Image illustration, bool large)
        {
            if (illustration.sprite == null)
                return;
            var source = illustration.sprite.rect.size;
            var maxSize = large ? new Vector2(560f, BattleVisualContext.LargeEnemyMaxHeight) : new Vector2(400f, 330f);
            var scale = Mathf.Min(maxSize.x / source.x, maxSize.y / source.y);
            illustration.rectTransform.sizeDelta = source * scale;
        }

        static void BuildEnemy(RectTransform focus, BattleUIRefs refs, BattleVisualContext visuals)
        {
            refs.EnemyStage = CreateCentered(focus, "EnemyStage", new Vector2(0f, 10f), new Vector2(420f, 420f));
            refs.EnemyHitRoot = CreateCentered(refs.EnemyStage, "EnemyHitRoot", Vector2.zero, new Vector2(420f, 420f));

            refs.EnemyAura = CreateCenteredImage(refs.EnemyHitRoot, "EnemyAura", new Vector2(0f, 10f), new Vector2(460f, 460f),
                BattleUISprites.RadialGlow, new Color(0.88f, 0.08f, 0.18f, 0.14f));

            if (visuals != null && visuals.UseSpritePair)
            {
                refs.SpritePairPresenter = EnemySpritePairPresenter.Create(refs.EnemyHitRoot, visuals);
                if (refs.SpritePairPresenter != null)
                {
                    refs.BossPortrait = refs.SpritePairPresenter.MainImage;
                    refs.EnemyEmoji = CreateLabel(refs.EnemyHitRoot, "EnemyEmoji", string.Empty, 160,
                        TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
                    refs.EnemyEmoji.raycastTarget = false;
                    AttachEnemyDamagePopups(refs);
                    BuildHeavyAttackCallout(refs);
                    return;
                }
            }

            CreateAnimatedEnemy(refs, visuals);

            if (refs.BigMouth == null && refs.PaperAngel == null && refs.SkeletonSamurai == null)
                BuildStaticEnemy(refs, visuals);

            var showEmoji = refs.BigMouth == null && refs.PaperAngel == null && refs.SkeletonSamurai == null
                            && visuals.BossIllustration == null;
            refs.EnemyEmoji = CreateLabel(refs.EnemyHitRoot, "EnemyEmoji", visuals.BossEmojiNormal, 160,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                showEmoji ? BattleUITheme.TextPrimary : Color.clear);
            refs.EnemyEmoji.raycastTarget = false;

            AttachEnemyDamagePopups(refs);
            BuildHeavyAttackCallout(refs);
        }

        static void AttachEnemyDamagePopups(BattleUIRefs refs)
        {
            var popupLayer = new GameObject("EnemyDamagePopups");
            popupLayer.transform.SetParent(refs.EnemyHitRoot, false);
            refs.EnemyDamagePopupRoot = popupLayer.AddComponent<RectTransform>();
            refs.EnemyDamagePopupRoot.anchorMin = Vector2.zero;
            refs.EnemyDamagePopupRoot.anchorMax = Vector2.one;
            refs.EnemyDamagePopupRoot.offsetMin = Vector2.zero;
            refs.EnemyDamagePopupRoot.offsetMax = Vector2.zero;
            RaiseDamagePopups(refs.EnemyDamagePopupRoot);
        }

        static void BuildHeavyAttackCallout(BattleUIRefs refs)
        {
            var callout = CreateCentered(refs.EnemyStage, "HeavyCallout", new Vector2(0f, 214f), new Vector2(240f, 72f));
            refs.HeavyAttackCallout = callout.gameObject;
            var plate = callout.gameObject.AddComponent<Image>();
            plate.sprite = BattleUISprites.Solid;
            plate.color = new Color(0.28f, 0.03f, 0.04f, 0.88f);
            plate.raycastTarget = false;

            refs.HeavyAttackLabel = CreateLabel(callout, "Label", "強攻撃！", 22, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.42f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                new Color(1f, 0.84f, 0.28f));
            refs.HeavyAttackLabel.fontStyle = FontStyle.Bold;
            AddOutline(refs.HeavyAttackLabel.gameObject, new Color(0.35f, 0.04f, 0.02f, 1f), 1);

            CreateCenteredImage(callout, "GaugeTrack", new Vector2(0f, -16f), new Vector2(200f, 12f),
                BattleUISprites.Solid, new Color(0.12f, 0.06f, 0.06f, 1f));
            refs.HeavyAttackGauge = CreateCenteredImage(callout, "GaugeFill", new Vector2(0f, -16f), new Vector2(200f, 12f),
                BattleUISprites.Solid, new Color(1f, 0.28f, 0.12f, 1f));
            refs.HeavyAttackGauge.type = Image.Type.Filled;
            refs.HeavyAttackGauge.fillMethod = Image.FillMethod.Horizontal;
            refs.HeavyAttackGauge.fillOrigin = (int)Image.OriginHorizontal.Left;
            refs.HeavyAttackGauge.fillAmount = 1f;
            callout.gameObject.SetActive(false);
        }

        /// <summary>手前の味方側スロット。将来「飛んで着地して攻撃」の出発／着地に使う。</summary>
        static void BuildPartyApproachSlot(RectTransform root, BattleUIRefs refs)
        {
            refs.PartyApproachRoot = CreatePanel(root, "PartyApproachRoot", new Vector2(0.40f, 0.34f),
                new Vector2(0.48f, 0.42f), Color.clear).GetComponent<RectTransform>();
            refs.PartyApproachRoot.GetComponent<Image>().raycastTarget = false;
        }

        static void BuildBossPlate(BattleUIRefs refs, BattleVisualContext visuals)
        {
            refs.EnemyStatus = EnemyStatusHud.Create(refs.RootCard,
                string.IsNullOrEmpty(visuals.BossDisplayName) ? "敵" : visuals.BossDisplayName);
            refs.EnemyHpGauge = null;
        }

        /// <summary>枠・暗い溝・遅れて減るトレイル・艶のあるフィルで構成した HP バー。</summary>
        static Image BuildHpBar(RectTransform parent, string name, Vector2 offsetMin, Vector2 offsetMax,
            Color frame, Color track, Color fill, Color trail, int segments)
        {
            var root = BattleHudKit.Rect(parent, name, new Vector2(0f, 1f), new Vector2(1f, 1f), offsetMin, offsetMax);
            BattleHudKit.Img(root, "Shadow", BattleUISprites.Solid, new Color(0f, 0f, 0f, 0.55f), Vector2.zero,
                Vector2.one, new Vector2(-4f, -6f), new Vector2(4f, 3f));
            BattleHudKit.Img(root, "Track", BattleUISprites.Solid, track, Vector2.zero, Vector2.one);
            var trailImg = BattleHudKit.Img(root, "Trail", BattleUISprites.Solid, trail, Vector2.zero, Vector2.one,
                new Vector2(3f, 3f), new Vector2(-3f, -3f));
            trailImg.type = Image.Type.Filled;
            trailImg.fillMethod = Image.FillMethod.Horizontal;
            trailImg.fillAmount = 1f;
            var fillImg = BattleHudKit.Img(root, "Fill", BattleHudKit.BarGloss, fill, Vector2.zero, Vector2.one,
                new Vector2(3f, 3f), new Vector2(-3f, -3f));
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            BattleHudKit.Img(root, "Sheen", BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.1f),
                new Vector2(0f, 0.62f), new Vector2(1f, 0.8f), new Vector2(3f, 0f), new Vector2(-3f, 0f));
            for (var i = 1; i < segments; i++)
            {
                var x = i / (float)segments;
                BattleHudKit.Img(root, $"Tick{i}", BattleUISprites.Solid, new Color(0f, 0f, 0f, 0.35f),
                    new Vector2(x, 0f), new Vector2(x, 1f), new Vector2(-1f, 3f), new Vector2(1f, -3f));
            }

            BattleHudKit.Img(root, "Frame", BattleHudKit.FrameThin, frame, Vector2.zero, Vector2.one);
            var trailFx = root.gameObject.AddComponent<HudBarTrail>();
            trailFx.Fill = fillImg;
            trailFx.Trail = trailImg;
            return fillImg;
        }

        static void BuildBattleLog(BattleUIRefs refs)
        {
            var host = BattleHudKit.Place(refs.RootCard, "Log", new Vector2(ArenaAnchor.x, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 70f), new Vector2(560f, 28f));
            refs.LogText = BattleHudKit.Text(host, "Text", "戦闘開始！", BattleHudKit.Gothic, 17f,
                new Color(1f, 0.82f, 0.62f), TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            refs.LogText.overflowMode = TextOverflowModes.Ellipsis;
            BattleHudKit.Outline(refs.LogText, 0.16f, Color.black);
        }

        const float PlayerBarLeft = 572f;
        const float PlayerBarRightMargin = 92f;

        static void BuildPartyHpStrip(BattleUIRefs refs)
        {
            var strip = BattleHudKit.Rect(refs.RootCard, "PartyHpStrip", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(PlayerBarLeft, 22f), new Vector2(-PlayerBarRightMargin, 22f + 38f));
            BattleHudKit.Img(strip, "Glow", BattleUISprites.RadialGlow, new Color(0.2f, 0.55f, 1f, 0.12f),
                Vector2.zero, Vector2.one, new Vector2(-60f, -30f), new Vector2(60f, 30f));
            var holder = BattleHudKit.Rect(strip, "BarHolder", new Vector2(0f, 0f), new Vector2(1f, 1f));
            refs.PlayerHpBar = BuildHpBar(holder, "PlayerBar", new Vector2(0f, -38f), new Vector2(0f, 0f),
                new Color(0.3f, 0.66f, 1f, 1f), new Color(0.015f, 0.04f, 0.09f, 0.97f),
                new Color(0.16f, 0.5f, 0.98f, 1f), new Color(0.78f, 0.92f, 1f, 0.85f), 10);

            var hpTag = BattleHudKit.Text(strip, "HpTag", "HP", BattleHudKit.Condensed, 24f,
                new Color(0.82f, 0.92f, 1f), TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(14f, 0f), Vector2.zero);
            hpTag.characterSpacing = 10f;
            BattleHudKit.Outline(hpTag, 0.24f, Color.black);

            refs.PlayerHpText = BattleHudKit.Text(strip, "PlayerHp", "100 / 100", BattleHudKit.Mincho, 27f,
                Color.white, TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-14f, 1f));
            BattleHudKit.Outline(refs.PlayerHpText, 0.26f, Color.black);
        }

        static void BuildControlStrip(BattleUIRefs refs)
        {
            var strip = BattleHudKit.Place(refs.RootCard, "ControlStrip", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(PlayerBarLeft + 4f, 68f), new Vector2(300f, 26f));
            var hint = BattleHudKit.Text(strip, "Hint", "Space ポーズ   Enter リロール", BattleHudKit.Gothic, 13f,
                BattleHudKit.TextDim, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(0.62f, 1f));
            BattleHudKit.Outline(hint, 0.1f, Color.black, 0.4f);
            refs.RerollBadge = BattleHudKit.Text(strip, "Reroll", "リロール 3/3", BattleHudKit.Gothic, 15f,
                BattleUITheme.AccentBoss, TextAlignmentOptions.MidlineLeft, new Vector2(0.64f, 0f), Vector2.one);
            BattleHudKit.Outline(refs.RerollBadge, 0.16f, Color.black);
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
                new Color(0.02f, 0.01f, 0.03f, 0.68f)).gameObject;
            refs.ResultCanvasGroup = refs.ResultOverlay.AddComponent<CanvasGroup>();

            var window = CreatePanel(refs.ResultOverlay.transform, "ResultWindow",
                new Vector2(0.31f, 0.22f), new Vector2(0.69f, 0.78f),
                new Color(0.07f, 0.045f, 0.04f, 0.96f), template?.windowPanel);
            AddOutline(window.gameObject, BattleUITheme.Gold, 2);

            refs.ResultTitle = CreateLabel(window, "Victory", "Victory", 54, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero, BattleUITheme.Gold);
            refs.ResultTitle.font = BattleUITheme.FontDisplay;
            refs.ResultTitle.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(refs.ResultTitle, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));

            var rule = CreatePanel(window, "Rule", new Vector2(0.22f, 0.72f), new Vector2(0.78f, 0.735f),
                new Color(BattleUITheme.Gold.r, BattleUITheme.Gold.g, BattleUITheme.Gold.b, 0.85f));
            rule.GetComponent<Image>().raycastTarget = false;

            refs.ResultHp = CreateStatLine(window, "HpLine", 0.56f);
            refs.ResultMadness = CreateStatLine(window, "MadnessLine", 0.42f);
            refs.ResultTime = CreateStatLine(window, "TimeLine", 0.28f);

            var buttonRt = CreatePanel(window, "ContinueButton", new Vector2(0.30f, 0.07f),
                new Vector2(0.70f, 0.20f), Color.white, template?.button);
            refs.ResultContinueButton = buttonRt.gameObject.AddComponent<Button>();
            var buttonColors = refs.ResultContinueButton.colors;
            buttonColors.highlightedColor = new Color(1.12f, 1.08f, 0.96f, 1f);
            buttonColors.pressedColor = new Color(0.72f, 0.64f, 0.48f, 1f);
            refs.ResultContinueButton.colors = buttonColors;
            var buttonText = CreateLabel(buttonRt, "Text", "NEXT", 26, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.16f, 0.08f, 0.04f, 1f));
            buttonText.font = BattleUITheme.FontDisplay;
            buttonText.fontStyle = FontStyle.Bold;

            refs.ResultOverlay.SetActive(false);
        }

        static Text CreateStatLine(Transform parent, string name, float y)
        {
            var label = CreateLabel(parent, name, "", 28, TextAnchor.MiddleCenter,
                new Vector2(0.08f, y), new Vector2(0.92f, y + 0.12f), Vector2.zero, Vector2.zero,
                BattleUITheme.TextPrimary);
            label.font = BattleUITheme.FontDisplay;
            label.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(label, new Color(0f, 0f, 0f, 0.75f), new Vector2(2f, -2f));
            return label;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>テスト用：戦闘画面中央の「撃破」ボタン。</summary>
        static void BuildDebugKillButton(BattleUIRefs refs, BattleUITemplateDefinition template)
        {
            var buttonRt = CreatePanel(refs.RootCard, "DebugKillButton", new Vector2(0.925f, 0.945f),
                new Vector2(0.985f, 0.982f), new Color(0.45f, 0.1f, 0.12f, 0.82f), template?.button);
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

            refs.DebugCombatReadout = CreateLabel(refs.RootCard, "DebugCombatReadout", "", 16, TextAnchor.UpperLeft,
                new Vector2(0.58f, 0.82f), new Vector2(0.985f, 0.94f), Vector2.zero, Vector2.zero,
                Color.white);
            refs.DebugCombatReadout.horizontalOverflow = HorizontalWrapMode.Wrap;
            refs.DebugCombatReadout.verticalOverflow = VerticalWrapMode.Overflow;
            refs.DebugCombatReadout.raycastTarget = false;
            refs.DebugCombatReadout.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(refs.DebugCombatReadout, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));

            var critRt = CreatePanel(refs.RootCard, "DebugCritTestButton", new Vector2(0.58f, 0.772f),
                new Vector2(0.76f, 0.812f), new Color(0.28f, 0.16f, 0.08f, 0.9f), template?.button);
            critRt.SetAsLastSibling();
            refs.DebugCritTestButton = critRt.gameObject.AddComponent<Button>();
            var critText = CreateLabel(critRt, "Text", "会心テスト", 16, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            critText.fontStyle = FontStyle.Bold;

            var slashRt = CreatePanel(refs.RootCard, "DebugAliceSlashButton", new Vector2(0.77f, 0.772f),
                new Vector2(0.985f, 0.812f), new Color(0.18f, 0.12f, 0.28f, 0.9f), template?.button);
            slashRt.SetAsLastSibling();
            refs.DebugAliceSlashButton = slashRt.gameObject.AddComponent<Button>();
            var slashText = CreateLabel(slashRt, "Text", "テスト：アリス ATK30%", 15, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            slashText.fontStyle = FontStyle.Bold;
            buttonRt.SetAsLastSibling();
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

        static void BuildRightRail(BattleUIRefs refs)
        {
            const float railWidth = 330f;
            var rail = BattleHudKit.Place(refs.RootCard, "RightRail", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -132f), new Vector2(railWidth, 520f));
            BuildRailHeader(rail, "NEXT SKILL", 0f);

            refs.CandidatesContainer = BattleHudKit.Rect(rail, "Candidates", new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, -48f));
            var vlg = refs.CandidatesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var stock = BattleHudKit.Place(refs.RootCard, "Stock", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 104f), new Vector2(railWidth, 104f));
            BuildRailHeader(stock, "STOCK", 0f);

            refs.StockOrbsContainer = BattleHudKit.Rect(stock, "StockOrbs", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(4f, 0f), new Vector2(0f, 54f));
            var hlg = refs.StockOrbsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var jackpot = BattleHudKit.Place(refs.RootCard, "JackpotMeter", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 66f), new Vector2(railWidth, 30f));
            refs.JackpotMeterText = BattleHudKit.Text(jackpot, "Text", "次 0%", BattleHudKit.Gothic, 12f,
                BattleHudKit.TextDim, TextAlignmentOptions.BottomRight, Vector2.zero, Vector2.one);
            refs.JackpotMeterText.textWrappingMode = TextWrappingModes.Normal;
            BattleHudKit.Outline(refs.JackpotMeterText, 0.1f, Color.black, 0.4f);
        }

        /// <summary>英字の見出しと、その下で右へ消える細線。</summary>
        static void BuildRailHeader(RectTransform parent, string title, float top)
        {
            var label = BattleHudKit.Text(parent, title + "Label", title, BattleHudKit.Condensed, 25f,
                new Color(0.96f, 0.94f, 0.9f), TextAlignmentOptions.BottomLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(2f, top - 36f), new Vector2(0f, top));
            label.characterSpacing = 16f;
            BattleHudKit.Outline(label, 0.2f, Color.black);
            BattleHudKit.Img(parent, title + "Rule", BattleHudKit.FadeRight, new Color(1f, 1f, 1f, 0.35f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, top - 42f), new Vector2(0f, top - 40f));
        }

        static void BuildMadnessMeter(Transform parent, BattleUIRefs refs)
        {
            var capsule = BattleHudKit.Place(parent, "MadnessCapsule", Vector2.zero, Vector2.zero,
                new Vector2(10f, 10f), new Vector2(440f, 123f));
            BattleHudKit.Img(capsule, "Glow", BattleUISprites.RadialGlow, new Color(0.45f, 1f, 0.2f, 0.16f),
                Vector2.zero, Vector2.one, new Vector2(-30f, -40f), new Vector2(30f, 40f));
            var art = BattleHudKit.Art("MadnessCapsule");
            BattleHudKit.Img(capsule, "Capsule", art, Color.white, Vector2.zero, Vector2.one);

            // 画像内のガラス部分（脳より右）をゲージとして使う。
            var gauge = BattleHudKit.Rect(capsule, "Gauge", new Vector2(0.45f, 0.18f), new Vector2(0.78f, 0.82f));
            refs.MadnessFill = BattleHudKit.Img(gauge, "Tint", BattleUISprites.Solid,
                new Color(0.5f, 1f, 0.3f, 0.2f), Vector2.zero, Vector2.one);
            refs.MadnessFill.type = Image.Type.Filled;
            refs.MadnessFill.fillMethod = Image.FillMethod.Horizontal;
            refs.MadnessFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            refs.MadnessFill.fillAmount = 0f;
            refs.MadnessEmpty = BattleHudKit.Img(gauge, "Empty", BattleUISprites.Solid,
                new Color(0.01f, 0.02f, 0.015f, 0.72f), Vector2.zero, Vector2.one);
            refs.MadnessEmpty.type = Image.Type.Filled;
            refs.MadnessEmpty.fillMethod = Image.FillMethod.Horizontal;
            refs.MadnessEmpty.fillOrigin = (int)Image.OriginHorizontal.Right;
            refs.MadnessEmpty.fillAmount = 1f;

            refs.MadnessText = BattleHudKit.Text(gauge, "MadnessValue", "", BattleHudKit.Mincho, 28f, Color.white,
                TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, 0f));
            BattleHudKit.Outline(refs.MadnessText, 0.3f, Color.black);
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
            _ = size;
            var glow = BattleHudKit.Img(root, "Glow", BattleUISprites.RadialGlow, new Color(1f, 1f, 1f, 0.08f),
                Vector2.zero, Vector2.one, new Vector2(-46f, -40f), new Vector2(46f, 40f));
            BattleHudKit.Img(root, "Shadow", BattleUISprites.Solid, new Color(0f, 0f, 0f, 0.6f), Vector2.zero,
                Vector2.one, new Vector2(6f, -8f), new Vector2(6f, -8f));

            var fill = BattleHudKit.Img(root, "Fill", BattleUISprites.Solid, new Color(0.01f, 0.01f, 0.015f, 0.97f),
                Vector2.zero, Vector2.one);
            var frame = BattleHudKit.Img(root, "Frame", BattleHudKit.FrameThick, Color.white, Vector2.zero, Vector2.one);

            var track = BattleHudKit.Img(root, "ProgressTrack", BattleUISprites.Solid, new Color(1f, 1f, 1f, 0.06f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 12f), new Vector2(-12f, 16f));
            var progress = BattleHudKit.Img(track.rectTransform, "Progress", BattleUISprites.Solid,
                BattleUITheme.TextTyped, Vector2.zero, Vector2.one);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = 0f;

            var word = BattleHudKit.Text(root, "Word", "", BattleHudKit.Mincho, 40f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 0.42f), new Vector2(1f, 1f),
                new Vector2(14f, 0f), new Vector2(-14f, -6f));
            word.enableAutoSizing = true;
            word.fontSizeMin = 24f;
            word.fontSizeMax = 40f;
            BattleHudKit.Outline(word, 0.2f, Color.black);

            var ruby = BattleHudKit.Text(root, "Ruby", "", BattleHudKit.Condensed, 25f, BattleUITheme.TextUntyped,
                TextAlignmentOptions.Center, new Vector2(0f, 0.12f), new Vector2(1f, 0.46f),
                new Vector2(12f, 0f), new Vector2(-12f, 0f));
            ruby.characterSpacing = 4f;
            ruby.enableAutoSizing = true;
            ruby.fontSizeMin = 16f;
            ruby.fontSizeMax = 25f;
            BattleHudKit.Outline(ruby, 0.16f, Color.black, 0.5f);

            // 属性の宝石。カード左上の角に重ねる。
            var gem = BattleHudKit.Place(root, "ElementGem", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(4f, -4f), new Vector2(38f, 38f));
            BattleHudKit.AddImage(gem, BattleUISprites.Diamond, Color.white);
            var gemFill = BattleHudKit.Img(gem, "Fill", BattleUISprites.Diamond, Color.white, Vector2.zero, Vector2.one,
                new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var tag = BattleHudKit.Text(gem, "Tag", "", BattleHudKit.Gothic, 15f, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 1f));
            BattleHudKit.Outline(tag, 0.25f, Color.black, 0.4f);

            return new WordBoxUI
            {
                FrameRoot = root,
                Glow = glow,
                Frame = frame,
                Fill = fill,
                ElementStripe = gemFill,
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

        public static GameObject BuildPrefabSource()
        {
            SuppressPrefab = true;
            var host = new GameObject("BattlePrefabHost");
            try
            {
                var skills = new List<BattleSkillData>
                {
                    new(1, "アリス", "見本スキル", new[] { ElementColor.Red, ElementColor.Red }, 10, 0, "見本"),
                    new(2, "エレン", "見本スキル", new[] { ElementColor.Blue, ElementColor.Blue }, 10, 0, "見本"),
                    new(3, "ルナ", "見本スキル", new[] { ElementColor.Green, ElementColor.Green }, 10, 0, "見本")
                };
                var refs = Build(host.transform, skills, null, BattleVisualContext.FromEncounter(null, null));
                if (refs.CountdownOverlay != null)
                    refs.CountdownOverlay.SetActive(false);
                return host.transform.Find("BattleCanvas").gameObject;
            }
            catch
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
                throw;
            }
            finally
            {
                SuppressPrefab = false;
            }
        }

        static BattleUIRefs TryBindPrefab(Transform parent, IReadOnlyList<BattleSkillData> skills, BattleVisualContext visuals)
        {
            var prefab = Resources.Load<GameObject>("UI/BattleHud");
            if (prefab == null)
                return null;
            GameObject canvas = null;
            try
            {
                canvas = UnityEngine.Object.Instantiate(prefab, parent);
                canvas.name = "BattleCanvas";
                var hud = FindDeep(canvas.transform, "BattleHud") as RectTransform;
                if (hud == null)
                {
                    UnityEngine.Object.DestroyImmediate(canvas);
                    return null;
                }

                var refs = new BattleUIRefs { RootCard = hud };
                BindBattleHud(refs, hud, skills, visuals);
                return refs;
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("戦闘プレハブを使えなかったので、コードで画面を作ります: " + error.Message);
                if (canvas != null)
                    UnityEngine.Object.DestroyImmediate(canvas);
                return null;
            }
        }

        static void EnsureHorizontalFill(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            if (image.sprite == null && sprite != null)
                image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        static void BindBattleHud(BattleUIRefs refs, RectTransform hud, IReadOnlyList<BattleSkillData> skills,
            BattleVisualContext visuals)
        {
            refs.PlayerHpBar = ImageOf(hud, "HpFill");
            refs.PlayerHpText = TmpOf(hud, "HpValue");
            refs.MadnessFill = ImageOf(hud, "MadnessLiquid");
            refs.MadnessText = TmpOf(hud, "MadnessValue");
            refs.LogText = TmpOf(hud, "NoticeText");
            refs.RerollBadge = TmpOf(hud, "Reroll");
            refs.JackpotMeterText = TmpOf(hud, "Jackpot");
            refs.E5bNoticeFill = ImageOf(hud, "NoticeFill");
            refs.E5bNoticeOutline = ImageOf(hud, "NoticeOutline");
            refs.DamageOverlay = ImageOf(hud, "DamageOverlay");
            refs.ArenaRoot = FindDeep(hud, "Arena") as RectTransform;
            refs.PartyApproachRoot = FindDeep(hud, "PartyApproachRoot") as RectTransform;
            refs.EnemyStage = FindDeep(hud, "EnemyStage") as RectTransform;
            refs.EnemyHitRoot = FindDeep(hud, "EnemyHitRoot") as RectTransform;
            refs.EnemyAura = FindDeep(refs.EnemyHitRoot, "EnemyAura")?.GetComponent<Image>();
            refs.EnemyEmoji = FindDeep(refs.EnemyHitRoot, "EnemyEmoji")?.GetComponent<Text>();
            refs.EnemyDamagePopupRoot = FindDeep(refs.EnemyHitRoot, "EnemyDamagePopups") as RectTransform;
            refs.EnemyHpText = FindDeep(hud, "EnemyHp")?.GetComponent<TMP_Text>();
            var enemyBar = FindDeep(hud, "EnemyBar") ?? FindDeep(hud, "EnemyBarBg");
            var enemyFill = enemyBar != null ? enemyBar.Find("Fill")?.GetComponent<Image>() : null;
            var enemyTrail = enemyBar != null ? enemyBar.Find("Trail")?.GetComponent<Image>() : null;
            var enemyFrame = enemyBar != null ? enemyBar.Find("Frame")?.GetComponent<Image>() : null;
            var enemyTrack = enemyBar != null ? enemyBar.Find("Track")?.GetComponent<Image>() : null;
            // プレハブ保存で実行時の絵が外れる。絵の無い Image は矩形いっぱいに塗られ、減る設定が画面に出ない。
            EnsureHorizontalFill(enemyFill, BattleHudKit.BarGloss);
            EnsureHorizontalFill(enemyTrail, BattleUISprites.Solid);
            if (enemyTrack != null && enemyTrack.sprite == null)
                enemyTrack.sprite = BattleUISprites.Solid;
            if (enemyFrame != null && enemyFrame.sprite == null)
            {
                enemyFrame.sprite = BattleHudKit.FrameThin;
                enemyFrame.type = Image.Type.Sliced;
            }

            refs.EnemyHpBar = enemyFill;

            var ring = hud.Find("AttackRing");
            if (ring != null)
            {
                refs.EnemyAttackRingTrack = ring.GetComponent<Image>();
                var progress = ring.Find("Progress")?.GetComponent<Image>();
                refs.AttackBar = progress;
                refs.EnemyAttackRing = progress;
                refs.AttackBarLabel = ring.Find("Countdown")?.GetComponent<TMP_Text>();
                refs.AttackRingFillsOnApproach = true;
            }

            var heavy = FindDeep(hud, "HeavyCallout");
            if (heavy != null)
            {
                refs.HeavyAttackLabel = heavy.Find("Label")?.GetComponent<Text>();
                refs.HeavyAttackGauge = heavy.Find("GaugeFill")?.GetComponent<Image>();
            }

            var tag = hud.Find("HeavyTag");
            if (tag != null)
                refs.HeavyAttackCallout = tag.gameObject;
            var orbs = hud.Find("E5bStock") as RectTransform;
            refs.StockOrbsContainer = orbs;
            var counter = orbs != null ? orbs.GetComponent<BattleE5bStock>() : null;
            if (counter != null)
                counter.Next = 0;

            for (var i = 0; i < refs.WordBoxes.Length; i++)
            {
                var card = hud.Find("WordCard" + i);
                if (card != null)
                    refs.WordBoxes[i] = ReadWordCard(card);
            }

            var ally = hud.Find("AllyPanel");
            if (ally != null)
            {
                var stage = ally.GetComponent<BattleE5bStage>();
                if (stage == null)
                    stage = ally.gameObject.AddComponent<BattleE5bStage>();
                refs.E5b = stage;
                stage.Portrait = FindDeep(ally, "AllyPortrait")?.GetComponent<Image>();
                stage.Leaver = FindDeep(ally, "AllyPortraitLeaving")?.GetComponent<Image>();
                var liveHost = ally.Find("AllyLive");
                if (liveHost != null)
                {
                    stage.Live = liveHost.GetComponent<PortraitPanelView>();
                    if (stage.Live == null)
                        stage.Live = liveHost.gameObject.AddComponent<PortraitPanelView>();
                    stage.Live.EnsureBuilt();
                }

                var lunaHost = ally.Find("AllyLuna");
                if (lunaHost != null)
                {
                    stage.Luna = lunaHost.GetComponent<KatanaPortraitPanelView>();
                    if (stage.Luna == null)
                        stage.Luna = lunaHost.gameObject.AddComponent<KatanaPortraitPanelView>();
                    stage.Luna.EnsureBuilt();
                }

                stage.SkillName = FindDeep(ally, "SkillCallout")?.GetComponent<TextMeshProUGUI>();
                stage.Log = refs.LogText;
                stage.NoticeFill = refs.E5bNoticeFill;
                stage.NoticeOutline = refs.E5bNoticeOutline;
                var noticeFace = hud.Find("NoticeFace")?.GetComponent<Image>();
                if (noticeFace != null)
                    stage.NoticeFace = noticeFace;
                var flash = hud.Find("SeamEdgeFlash")?.GetComponent<Image>();
                if (flash != null)
                    stage.Flash = flash;
                BattleLayoutE5b.RefreshStage(stage, visuals);
            }

            BattleLayoutE5b.RefillSkills(hud.Find("SkillList") as RectTransform, refs, skills, visuals);
            foreach (var name in new[] { "ParallaxBackdrop", "EnvironmentArt", "EnemyContactShadow", "EnemyGroundFog", "SilhouettesRight" })
            {
                var oldLayer = FindDeep(hud.parent, name);
                if (oldLayer != null)
                    oldLayer.gameObject.SetActive(false);
            }
            // The editor can bake a generated HUD into the prefab.
            BattleDungeonBackdrop.Ensure(hud);
            var savedShadow = FindDeep(refs.ArenaRoot, "EnemyFloorShadow");
            if (savedShadow != null)
                UnityEngine.Object.DestroyImmediate(savedShadow.gameObject);
            PlaceEnemyOnFloor(refs, visuals);
            RespawnEnemy(refs, visuals);
            RebuildStock(hud, refs);
            BindOverlay(refs, hud);
            RepairBattleVisuals(hud);
            // Saved HUDs retain the old enemy plate; replace it on both prefab and code paths.
            var oldPlate = FindDeep(hud, "EnemyHpPlate");
            if (oldPlate != null) oldPlate.gameObject.SetActive(false);
            var savedStatus = FindDeep(hud, "EnemyStatusHud");
            if (savedStatus != null) savedStatus.gameObject.SetActive(false);
            refs.EnemyStatus = EnemyStatusHud.Create(hud, visuals.BossDisplayName);
            refs.EnemyHpText = null;
            refs.EnemyHpBar = null;
            refs.EnemyHpGauge = null;
        }

        static void BindOverlay(BattleUIRefs refs, RectTransform hud)
        {
            refs.CountdownOverlay = hud.Find("CountdownOverlay")?.gameObject;
            refs.CountdownRing = FindDeep(refs.CountdownOverlay != null ? refs.CountdownOverlay.transform : null, "CountdownRing")
                ?.GetComponent<Image>();
            refs.CountdownText = FindDeep(refs.CountdownOverlay != null ? refs.CountdownOverlay.transform : null, "Count")
                ?.GetComponent<Text>();
            refs.ResultOverlay = hud.Find("ResultOverlay")?.gameObject;
            if (refs.ResultOverlay != null)
            {
                refs.ResultCanvasGroup = refs.ResultOverlay.GetComponent<CanvasGroup>();
                var window = FindDeep(refs.ResultOverlay.transform, "ResultWindow");
                refs.ResultTitle = window != null ? window.Find("Victory")?.GetComponent<Text>() : null;
                refs.ResultHp = window != null ? window.Find("HpLine")?.GetComponent<Text>() : null;
                refs.ResultMadness = window != null ? window.Find("MadnessLine")?.GetComponent<Text>() : null;
                refs.ResultTime = window != null ? window.Find("TimeLine")?.GetComponent<Text>() : null;
                refs.ResultContinueButton = FindDeep(refs.ResultOverlay.transform, "ContinueButton")?.GetComponent<Button>();
            }

            refs.PauseOverlay = hud.Find("PauseOverlay")?.gameObject;
            refs.CutinOverlay = hud.Find("CutinOverlay")?.gameObject;
            if (refs.CutinOverlay != null)
            {
                var banner = FindDeep(refs.CutinOverlay.transform, "Banner");
                refs.CutinPortrait = FindDeep(banner, "Portrait")?.GetComponent<Image>();
                refs.CutinChar = banner != null ? banner.Find("Char")?.GetComponent<Text>() : null;
                refs.CutinSkill = banner != null ? banner.Find("Skill")?.GetComponent<Text>() : null;
            }

            refs.JackpotOverlay = hud.Find("JackpotOverlay")?.gameObject;
            if (refs.JackpotOverlay != null)
            {
                refs.JackpotBlackout = FindDeep(refs.JackpotOverlay.transform, "Blackout")?.GetComponent<Image>();
                refs.JackpotLightStreak = FindDeep(refs.JackpotOverlay.transform, "LightStreak") as RectTransform;
                refs.JackpotCutinRoot = FindDeep(refs.JackpotOverlay.transform, "TripleCutin") as RectTransform;
                refs.JackpotCutinGroup = refs.JackpotCutinRoot != null
                    ? refs.JackpotCutinRoot.GetComponent<CanvasGroup>()
                    : null;
                refs.JackpotTitle = refs.JackpotCutinRoot != null
                    ? refs.JackpotCutinRoot.Find("Title")?.GetComponent<Text>()
                    : null;
                refs.JackpotDamageText = FindDeep(refs.JackpotOverlay.transform, "JackpotDamage")?.GetComponent<Text>();
            }

            var debug = hud.Find("DebugKillButton");
            var readout = hud.Find("DebugCombatReadout");
            var critTest = hud.Find("DebugCritTestButton");
            var aliceSlash = hud.Find("DebugAliceSlashButton");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            refs.DebugKillButton = debug != null ? debug.GetComponent<Button>() : null;
            refs.DebugCombatReadout = readout != null ? readout.GetComponent<Text>() : refs.DebugCombatReadout;
            refs.DebugCritTestButton = critTest != null ? critTest.GetComponent<Button>() : refs.DebugCritTestButton;
            refs.DebugAliceSlashButton = aliceSlash != null ? aliceSlash.GetComponent<Button>() : refs.DebugAliceSlashButton;
#else
            if (debug != null)
                UnityEngine.Object.Destroy(debug.gameObject);
            if (readout != null)
                UnityEngine.Object.Destroy(readout.gameObject);
            if (critTest != null)
                UnityEngine.Object.Destroy(critTest.gameObject);
            if (aliceSlash != null)
                UnityEngine.Object.Destroy(aliceSlash.gameObject);
#endif
        }

        static void RespawnEnemy(BattleUIRefs refs, BattleVisualContext visuals)
        {
            if (refs.EnemyHitRoot == null)
                return;
            refs.BigMouth = null;
            refs.PaperAngel = null;
            refs.SkeletonSamurai = null;
            refs.BossPortrait = null;
            refs.SpritePairPresenter = null;
            for (var i = refs.EnemyHitRoot.childCount - 1; i >= 0; i--)
            {
                var child = refs.EnemyHitRoot.GetChild(i);
                var childName = child.name;
                if (childName == "EnemyAura" || childName == "EnemyEmoji" || childName == "EnemyDamagePopups")
                    continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            // 本番はほぼプレハブ経由。ここで SpritePair を作らないとゲーム画面では静止立ち絵のままになる。
            if (visuals != null && visuals.UseSpritePair)
            {
                refs.SpritePairPresenter = EnemySpritePairPresenter.Create(refs.EnemyHitRoot, visuals);
                if (refs.SpritePairPresenter != null)
                {
                    refs.BossPortrait = refs.SpritePairPresenter.MainImage;
                    if (refs.EnemyEmoji != null)
                    {
                        refs.EnemyEmoji.text = string.Empty;
                        refs.EnemyEmoji.color = Color.clear;
                    }
                    FinishEnemyRespawnFx(refs);
                    return;
                }
            }

            CreateAnimatedEnemy(refs, visuals);
            if (refs.BigMouth == null && refs.PaperAngel == null && refs.SkeletonSamurai == null)
                BuildStaticEnemy(refs, visuals);

            if (refs.EnemyEmoji != null)
            {
                var showEmoji = refs.BigMouth == null && refs.PaperAngel == null && refs.SkeletonSamurai == null
                                && (visuals == null || visuals.BossIllustration == null);
                refs.EnemyEmoji.text = visuals != null ? visuals.BossEmojiNormal : "";
                refs.EnemyEmoji.color = showEmoji ? BattleUITheme.TextPrimary : Color.clear;
            }

            FinishEnemyRespawnFx(refs);
        }

        static void FinishEnemyRespawnFx(BattleUIRefs refs)
        {
            // 旧円型かみつき演出 EnemyBiteFx は廃止（StrikeFX_v1 に置換）
            RaiseDamagePopups(refs.EnemyDamagePopupRoot);
        }

        /// <summary>ダメージ数字を敵画像や後から乗ったHUDより手前に出す。</summary>
        public static void RaiseDamagePopups(RectTransform popupRoot)
        {
            if (popupRoot == null)
                return;
            popupRoot.SetAsLastSibling();
            Canvas host = null;
            var canvases = popupRoot.GetComponentsInParent<Canvas>(true);
            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].gameObject != popupRoot.gameObject)
                {
                    host = canvases[i];
                    break;
                }
            }

            var canvas = popupRoot.GetComponent<Canvas>();
            if (canvas == null)
                canvas = popupRoot.gameObject.AddComponent<Canvas>();
            if (host != null)
                canvas.additionalShaderChannels = host.additionalShaderChannels;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 40;
        }

        static void RebuildStock(RectTransform hud, BattleUIRefs refs)
        {
            var old = hud.Find("StockPanel") as RectTransform;
            var oldOverlay = hud.Find("StockPanelOverlayUnderCards") as RectTransform;
            var panelRect = CaptureRect(old);
            var overlayRect = CaptureRect(oldOverlay);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (oldOverlay != null)
                UnityEngine.Object.DestroyImmediate(oldOverlay.gameObject);
            refs.Stock = StockPanel.Create(hud, BattleHudKit.SerifRegular, BattleHudKit.Sans);
            ApplyRect(refs.Stock != null ? refs.Stock.GetComponent<RectTransform>() : null, panelRect);
            ApplyRect(hud.Find("StockPanelOverlayUnderCards") as RectTransform, overlayRect);
        }

        struct SavedRect
        {
            public bool Ok;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
        }

        static SavedRect CaptureRect(RectTransform rect)
        {
            if (rect == null)
                return default;
            return new SavedRect
            {
                Ok = true,
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                OffsetMin = rect.offsetMin,
                OffsetMax = rect.offsetMax
            };
        }

        static void ApplyRect(RectTransform rect, SavedRect saved)
        {
            if (rect == null || !saved.Ok)
                return;
            rect.anchorMin = saved.AnchorMin;
            rect.anchorMax = saved.AnchorMax;
            rect.pivot = saved.Pivot;
            rect.anchoredPosition = saved.AnchoredPosition;
            rect.sizeDelta = saved.SizeDelta;
            rect.offsetMin = saved.OffsetMin;
            rect.offsetMax = saved.OffsetMax;
        }

        static WordBoxUI ReadWordCard(Transform card)
        {
            var track = card.Find("RubyTrack");
            return new WordBoxUI
            {
                FrameRoot = card as RectTransform,
                Frame = card.GetComponent<Image>(),
                Fill = card.Find("Fill")?.GetComponent<Image>(),
                WordText = card.Find("Kanji")?.GetComponent<TMP_Text>(),
                RubyText = card.Find("Ruby")?.GetComponent<TMP_Text>(),
                Progress = track != null ? track.Find("RubyFill")?.GetComponent<Image>() : null,
                ElementStripe = card.Find("Stripe")?.GetComponent<Image>(),
                TagText = card.Find("Tag")?.GetComponent<TMP_Text>(),
                E5b = true
            };
        }

        static Image ImageOf(Transform hud, string name)
        {
            var child = hud.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static TMP_Text TmpOf(Transform hud, string name)
        {
            var child = hud.Find(name);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        static void RepairBattleVisuals(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null)
                    continue;
                var imageName = image.name;
                if (imageName.IndexOf("Aura", System.StringComparison.Ordinal) >= 0
                    || imageName.IndexOf("Glow", System.StringComparison.Ordinal) >= 0)
                    image.sprite = BattleUISprites.RadialGlow;
                else if (imageName.IndexOf("Ring", System.StringComparison.Ordinal) >= 0)
                    image.sprite = BattleUISprites.Circle;
                else
                    image.sprite = BattleUISprites.Solid;
            }

            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.font = tmp.fontSize >= 40f ? BattleHudKit.SerifRegular : BattleHudKit.Sans;
                if (tmp.font == null)
                    tmp.font = BattleHudKit.Gothic;
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.font != null)
                    continue;
                text.font = text.fontSize >= 28
                    ? BattleUITheme.FontDisplay ?? BattleUITheme.FontBold
                    : BattleUITheme.FontBold ?? BattleUITheme.FontDisplay;
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
