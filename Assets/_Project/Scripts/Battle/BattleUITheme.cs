using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>コード生成 UI の共通配色・スプライト。</summary>
    public static class BattleUITheme
    {
        public static readonly Color BgVoid = new(0.04f, 0.02f, 0.05f, 1f);
        public static readonly Color BgCard = new(0.11f, 0.05f, 0.08f, 0.98f);
        public static readonly Color BgPanel = new(0.07f, 0.03f, 0.05f, 0.92f);
        public static readonly Color BgPanelAlt = new(0.05f, 0.02f, 0.04f, 0.88f);
        public static readonly Color AccentBoss = new(0.94f, 0.27f, 0.27f);
        public static readonly Color AccentAlly = new(0.06f, 0.73f, 0.51f);
        public static readonly Color AccentWarn = new(0.96f, 0.62f, 0.04f);
        public static readonly Color TextPrimary = new(0.95f, 0.95f, 0.97f);
        public static readonly Color TextMuted = new(0.55f, 0.58f, 0.65f);
        public static readonly Color TextTyped = new(0.45f, 0.95f, 0.65f);
        public static readonly Color TextUntyped = new(0.78f, 0.8f, 0.86f);
        public static readonly Color TextDisabled = new(0.35f, 0.35f, 0.38f);
        public static readonly Color BarTrack = new(0.08f, 0.04f, 0.06f, 0.95f);
        public static readonly Color InnateRow = new(0.28f, 0.2f, 0.06f, 0.9f);
        public static readonly Color CardRow = new(0.05f, 0.11f, 0.22f, 0.9f);
        public static readonly Color Divider = new(0.94f, 0.27f, 0.27f, 0.25f);
        public static readonly Color Gold = new(0.93f, 0.69f, 0.24f);
        public static readonly Color Arcane = new(0.55f, 0.38f, 0.96f);
        public static readonly Color Cyan = new(0.18f, 0.8f, 0.92f);
        public static readonly Color Glass = new(0.025f, 0.03f, 0.065f, 0.9f);
        public static readonly Color GlassSoft = new(0.035f, 0.04f, 0.085f, 0.72f);

        // Dark Fantasy × Stylish（モック準拠）
        public static readonly Color MainPurple = new(0.69f, 0.26f, 1f);       // #B043FF
        public static readonly Color DarkPurple = new(0.43f, 0.13f, 1f);       // #6D22FF
        public static readonly Color AccentRedStylish = new(1f, 0.19f, 0.19f); // #FF3030
        public static readonly Color PanelDark = new(0.102f, 0.102f, 0.102f);  // #1A1A1A
        public static readonly Color Grey80 = new(0.69f, 0.69f, 0.69f);         // #B0B0B0
        public static readonly Color ElementGreenStylish = new(0.33f, 0.84f, 0.42f); // #55D66A
        public static readonly Color ElementYellowStylish = new(1f, 0.83f, 0.23f);   // #FFD43B

        static Font _font;
        static Font _fontBold;
        static Font _fontDisplay;

        public static Font Font
        {
            get
            {
                if (_font)
                    return _font;
                _font = LoadFont("Battle/Fonts/NotoSansJP-Regular", "Battle/Fonts/NotoSansJP-Medium")
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static Font FontBold
        {
            get
            {
                if (_fontBold)
                    return _fontBold;
                _fontBold = LoadFont("Battle/Fonts/NotoSansJP-Bold", "Battle/Fonts/NotoSansJP-Black") ?? Font;
                return _fontBold;
            }
        }

        /// <summary>英字名・数字用（Cinzel / DIN が無い場合は Bold にフォールバック）。</summary>
        public static Font FontDisplay
        {
            get
            {
                if (_fontDisplay)
                    return _fontDisplay;
                _fontDisplay = LoadFont("Battle/Fonts/Cinzel-Bold", "Battle/Fonts/DINAlternate-Bold") ?? FontBold;
                return _fontDisplay;
            }
        }

        static Font LoadFont(params string[] resourcePaths)
        {
            foreach (var path in resourcePaths)
            {
                var f = Resources.Load<Font>(path);
                if (f != null)
                    return f;
            }

            return null;
        }

        public static void ResetFontCache()
        {
            _font = null;
            _fontBold = null;
            _fontDisplay = null;
        }

        public static void StylePanel(Image img, Color color)
        {
            img.color = color;
            EnsureSprite(img);
        }

        public static void EnsureSprite(Image img)
        {
            if (img.sprite != null)
                return;
            img.sprite = BattleUISprites.Solid;
            img.type = Image.Type.Simple;
        }

        public static void StyleGlass(Image img, Color accent, float alpha = 0.9f)
        {
            img.sprite = BattleUITemplateLoader.Default?.windowPanel ?? BattleUISprites.Solid;
            img.type = BattleUITemplateLoader.Default?.windowPanel != null
                ? Image.Type.Sliced
                : Image.Type.Simple;
            img.color = new Color(Glass.r, Glass.g, Glass.b, alpha);
            var outline = img.GetComponent<Outline>() ?? img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public static void AddTextShadow(Text text, Color color, Vector2 distance)
        {
            var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        public static string RichTypedWord(string typed, string untyped, bool active)
        {
            if (!active)
                return $"<color=#{ToHtml(TextDisabled)}>{typed}{untyped}</color>";

            if (string.IsNullOrEmpty(untyped))
                return $"<color=#{ToHtml(TextTyped)}>{typed}</color>";

            return $"<color=#{ToHtml(TextTyped)}>{typed}</color><color=#{ToHtml(TextUntyped)}>{untyped}</color>";
        }

        static string ToHtml(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
