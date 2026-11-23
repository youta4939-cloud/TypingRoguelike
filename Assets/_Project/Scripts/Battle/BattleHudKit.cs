using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>戦闘 HUD の共通部品（TMP フォント・オーブ・枠・スキル行）。</summary>
    public static class BattleHudKit
    {
        public static readonly Color Gold = new(0.98f, 0.8f, 0.4f);
        public static readonly Color GoldDeep = new(0.66f, 0.47f, 0.18f);
        public static readonly Color EnemyRed = new(0.84f, 0.08f, 0.1f);
        public static readonly Color AllyBlue = new(0.22f, 0.62f, 1f);
        public static readonly Color TextSoft = new(0.84f, 0.82f, 0.8f, 1f);
        public static readonly Color TextDim = new(0.56f, 0.54f, 0.56f, 1f);
        public static readonly Color Band = new(0.035f, 0.03f, 0.04f, 0.86f);
        public static readonly Color Separator = new(1f, 1f, 1f, 0.1f);

        const string FontRoot = "Battle/Fonts/";
        const string ArtRoot = "Battle/Art/HUD/";

        static TMP_FontAsset _mincho;
        static TMP_FontAsset _gothic;
        static TMP_FontAsset _gothicMedium;
        static TMP_FontAsset _condensed;
        static TMP_FontAsset _serifMedium;
        static TMP_FontAsset _serifRegular;
        static TMP_FontAsset _sans;
        static readonly Dictionary<string, Material> MaterialCache = new();
        static readonly Dictionary<string, Sprite> ArtCache = new();

        // ---------- Fonts ----------

        /// <summary>見出し・スキル名・数値（明朝の極太）。</summary>
        public static TMP_FontAsset Mincho
        {
            get
            {
                if (_mincho != null)
                    return _mincho;
                _mincho = CreateFont("ShipporiMinchoB1-ExtraBold");
                AddFallback(_mincho, Gothic);
                return _mincho;
            }
        }

        /// <summary>効果説明などの本文。</summary>
        public static TMP_FontAsset Gothic
        {
            get
            {
                if (_gothic != null)
                    return _gothic;
                _gothic = CreateFont("ZenKakuGothicNew-Bold");
                return _gothic;
            }
        }

        /// <summary>UIラベル・補助テキスト（ゴシック Medium）。</summary>
        public static TMP_FontAsset GothicMedium
        {
            get
            {
                if (_gothicMedium != null)
                    return _gothicMedium;
                _gothicMedium = CreateFont("ZenKakuGothicNew-Medium");
                if (_gothicMedium == null || _gothicMedium == TMP_Settings.defaultFontAsset)
                    _gothicMedium = Gothic;
                return _gothicMedium;
            }
        }

        /// <summary>ダンジョン画面の見出し・短いラベル（Noto Serif JP Medium）。</summary>
        public static TMP_FontAsset SerifMedium
        {
            get
            {
                if (_serifMedium != null)
                    return _serifMedium;
                _serifMedium = CreateFont("NotoSerifJP-Medium");
                if (_serifMedium == null || _serifMedium == TMP_Settings.defaultFontAsset)
                    _serifMedium = SerifRegular;
                else
                    AddFallback(_serifMedium, SerifRegular);
                return _serifMedium;
            }
        }

        /// <summary>ダンジョン画面の状態ラベル・数値（Noto Serif JP Regular）。</summary>
        public static TMP_FontAsset SerifRegular
        {
            get
            {
                if (_serifRegular != null)
                    return _serifRegular;
                _serifRegular = CreateFont("NotoSerifJP-Regular");
                if (_serifRegular == null || _serifRegular == TMP_Settings.defaultFontAsset)
                    _serifRegular = Mincho;
                return _serifRegular;
            }
        }

        /// <summary>メニュー本文（Noto Sans JP Regular）。</summary>
        public static TMP_FontAsset Sans
        {
            get
            {
                if (_sans != null)
                    return _sans;
                _sans = CreateFont("NotoSansJP-Regular");
                if (_sans == null || _sans == TMP_Settings.defaultFontAsset)
                    _sans = Gothic;
                return _sans;
            }
        }

        /// <summary>英字ラベル・ローマ字。</summary>
        public static TMP_FontAsset Condensed
        {
            get
            {
                if (_condensed != null)
                    return _condensed;
                _condensed = CreateFont("BarlowCondensed-SemiBold");
                AddFallback(_condensed, Gothic);
                return _condensed;
            }
        }

        /// <summary>
        /// 実行時フォント（明朝・ゴシック）に無い絵文字を除く。残すと □ とコンソール警告になる。
        /// </summary>
        public static string StripUnsupportedGlyphs(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                        i++;
                    continue;
                }

                if (char.IsLowSurrogate(c))
                    continue;

                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.OtherSymbol ||
                    category == UnicodeCategory.ModifierSymbol ||
                    category == UnicodeCategory.NonSpacingMark ||
                    category == UnicodeCategory.EnclosingMark ||
                    category == UnicodeCategory.Format)
                    continue;

                builder.Append(c);
            }

            return builder.ToString().Trim();
        }

        static TMP_FontAsset CreateFont(string fileName)
        {
            var source = Resources.Load<Font>(FontRoot + fileName);
            if (source == null)
                return TMP_Settings.defaultFontAsset;
            var asset = TMP_FontAsset.CreateFontAsset(
                source, 72, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
                return TMP_Settings.defaultFontAsset;
            asset.name = fileName + " (Runtime)";
            return asset;
        }

        static void AddFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
        {
            if (asset == null || fallback == null || asset == fallback)
                return;
            asset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!asset.fallbackFontAssetTable.Contains(fallback))
                asset.fallbackFontAssetTable.Add(fallback);
        }

        // ---------- Text ----------

        public static TextMeshProUGUI Text(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions align,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin = default,
            Vector2 offsetMax = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.text = StripUnsupportedGlyphs(text);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        /// <summary>袋文字（アウトライン）と落ち影。マテリアルは条件ごとに共有する。</summary>
        public static void Outline(TMP_Text tmp, float width, Color outlineColor, float shadow = 0.75f)
        {
            if (tmp == null || tmp.font == null || tmp.font.material == null)
                return;
            var key = $"{tmp.font.GetInstanceID()}_{width:0.00}_{ColorUtility.ToHtmlStringRGBA(outlineColor)}_{shadow:0.00}";
            if (!MaterialCache.TryGetValue(key, out var mat) || mat == null)
            {
                mat = new Material(tmp.font.material) { name = "HUD_" + key };
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                if (shadow > 0f)
                {
                    mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, shadow));
                    mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
                    mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                    mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
                    mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
                }

                MaterialCache[key] = mat;
            }

            tmp.fontSharedMaterial = mat;
        }

        // ---------- Layout helpers ----------

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>アンカー1点＋ピボット＋サイズで置く。</summary>
        public static RectTransform Place(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, anchor);
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            return AddImage(rt, sprite, color);
        }

        public static Image AddImage(RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : BattleUISprites.Solid;
            img.color = color;
            img.raycastTarget = false;
            if (img.sprite != null && img.sprite.border != Vector4.zero)
                img.type = Image.Type.Sliced;
            return img;
        }

        // ---------- Art ----------

        public static Sprite Art(string name)
        {
            if (ArtCache.TryGetValue(name, out var cached) && cached != null)
                return cached;
            var sprite = Resources.Load<Sprite>(ArtRoot + name);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(ArtRoot + name);
                if (tex != null)
                    sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            ArtCache[name] = sprite;
            return sprite;
        }

        /// <summary>スキル個別画像（SkillCards/{id}）。無ければ属性色の仮画像。</summary>
        public static Sprite SkillArt(BattleSkillData skill, out Color tint)
        {
            tint = Color.white;
            if (skill != null)
            {
                var own = Resources.Load<Sprite>($"Battle/Art/SkillCards/{skill.Id}");
                if (own != null)
                    return own;
            }

            var element = skill?.Pattern != null && skill.Pattern.Length > 0
                ? skill.Pattern[skill.Pattern.Length - 1]
                : ElementColor.Purple;
            if (element == ElementColor.Red)
                return Art("SkillArt_Red");
            tint = Color.Lerp(ElementColorUtility.ToUnityColor(element), Color.white, 0.15f);
            return Art("SkillArt_Neutral");
        }

        // ---------- Procedural sprites ----------

        static Sprite _orbBody;
        static Sprite _orbShine;
        static Sprite _orbRing;
        static Sprite _pill;
        static Sprite _frameThin;
        static Sprite _frameThick;
        static Sprite _fadeRight;
        static Sprite _barGloss;

        /// <summary>陰影つき球（色は Image.color で乗算）。</summary>
        public static Sprite OrbBody => _orbBody ? _orbBody : _orbBody = CreateOrbBody(128);

        /// <summary>球の白いハイライト。</summary>
        public static Sprite OrbShine => _orbShine ? _orbShine : _orbShine = CreateOrbShine(128);

        /// <summary>未達成ドット用の輪。</summary>
        public static Sprite OrbRing => _orbRing ? _orbRing : _orbRing = CreateRing(128, 0.74f);

        /// <summary>角丸カプセル（スライス）。</summary>
        public static Sprite Pill => _pill ? _pill : _pill = CreatePill(64);

        public static Sprite FrameThin => _frameThin ? _frameThin : _frameThin = CreateFrame(32, 2);

        public static Sprite FrameThick => _frameThick ? _frameThick : _frameThick = CreateFrame(32, 5);

        /// <summary>左が不透明、右へ消える帯。</summary>
        public static Sprite FadeRight => _fadeRight ? _fadeRight : _fadeRight = CreateFadeRight(128);

        /// <summary>バー用の縦方向の艶。</summary>
        public static Sprite BarGloss => _barGloss ? _barGloss : _barGloss = CreateBarGloss(64);

        static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            return tex;
        }

        static Sprite Finish(Texture2D tex, Color[] px, Vector4 border = default)
        {
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, border);
        }

        static Sprite CreateOrbBody(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            var c = (size - 1) * 0.5f;
            var r = size * 0.47f;
            var light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x - c) / r;
                var dy = (y - c) / r;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var a = Mathf.Clamp01((1f - d) * r + 0.5f);
                var nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d * d));
                var n = new Vector3(dx, dy, nz);
                var diffuse = Mathf.Clamp01(Vector3.Dot(n, light));
                var shade = 0.42f + 0.68f * diffuse;
                var rim = Mathf.SmoothStep(0.78f, 1f, d);
                shade *= 1f - rim * 0.55f;
                var bounce = Mathf.SmoothStep(0.55f, 0.95f, d) * Mathf.Clamp01(-dy) * 0.25f;
                var v = Mathf.Clamp01(shade + bounce);
                px[y * size + x] = new Color(v, v, v, a);
            }

            return Finish(tex, px);
        }

        static Sprite CreateOrbShine(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            var c = (size - 1) * 0.5f;
            var r = size * 0.47f;
            var hc = new Vector2(c - r * 0.32f, c + r * 0.38f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / r;
                var inside = Mathf.Clamp01((1f - d) * r + 0.5f);
                var hd = Vector2.Distance(new Vector2(x, y), hc) / (r * 0.42f);
                var spot = Mathf.Pow(Mathf.Clamp01(1f - hd), 1.6f) * 0.95f;
                var rimLight = Mathf.SmoothStep(0.86f, 0.97f, d) * Mathf.Clamp01((c - y) / r + 0.2f) * 0.35f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(spot + rimLight) * inside);
            }

            return Finish(tex, px);
        }

        static Sprite CreateRing(int size, float innerRatio)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            var c = (size - 1) * 0.5f;
            var outer = size * 0.47f;
            var inner = outer * innerRatio;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                var a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            return Finish(tex, px);
        }

        static Sprite CreatePill(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            var r = size * 0.5f - 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
            }

            var b = size * 0.5f - 1f;
            return Finish(tex, px, new Vector4(b, b, b, b));
        }

        static Sprite CreateFrame(int size, int thickness)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                px[y * size + x] = new Color(1f, 1f, 1f, edge < thickness ? 1f : 0f);
            }

            var b = thickness + 1;
            return Finish(tex, px, new Vector4(b, b, b, b));
        }

        static Sprite CreateFadeRight(int width)
        {
            var tex = NewTex(width, 4);
            var px = new Color[width * 4];
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < width; x++)
            {
                var t = x / (float)(width - 1);
                px[y * width + x] = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, t));
            }

            return Finish(tex, px);
        }

        static Sprite CreateBarGloss(int height)
        {
            var tex = NewTex(4, height);
            var px = new Color[4 * height];
            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1);
                var v = Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, t));
                if (t > 0.72f && t < 0.86f)
                    v = Mathf.Min(1f, v + 0.18f);
                for (var x = 0; x < 4; x++)
                    px[y * 4 + x] = new Color(v, v, v, 1f);
            }

            return Finish(tex, px);
        }

        // ---------- Orbs ----------

        /// <summary>艶のある色玉（本体＋ハイライト）。</summary>
        public static Image Orb(Transform parent, string name, Color color, float size)
        {
            var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(size, size);
            var body = AddImage(rt, OrbBody, color);
            Img(rt, "Shine", OrbShine, Color.white, Vector2.zero, Vector2.one);
            return body;
        }

        public sealed class DotsView
        {
            public RectTransform Root;
            public Image[] Fill;
            public Image[] Ring;
            public ElementColor[] Pattern;
        }

        /// <summary>発動条件のドット列。暗いカプセルの上に並べる。右端基準。</summary>
        public static DotsView Dots(Transform parent, ElementColor[] pattern, float orbSize, float gap, Vector2 rightCenter)
        {
            pattern ??= System.Array.Empty<ElementColor>();
            var n = pattern.Length;
            var padX = orbSize * 0.45f;
            var width = n * orbSize + Mathf.Max(0, n - 1) * gap + padX * 2f;
            var height = orbSize + orbSize * 0.55f;
            var root = Place(parent, "Dots", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), rightCenter,
                new Vector2(width, height));
            AddImage(root, Pill, new Color(0f, 0f, 0f, 0.62f));

            var view = new DotsView
            {
                Root = root,
                Fill = new Image[n],
                Ring = new Image[n],
                Pattern = pattern
            };
            for (var i = 0; i < n; i++)
            {
                var col = ElementColorUtility.ToUnityColor(pattern[i]);
                var x = padX + i * (orbSize + gap) + orbSize * 0.5f;
                var slot = Place(root, $"Dot{i}", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(orbSize, orbSize));
                view.Ring[i] = Img(slot, "Ring", OrbRing, col, Vector2.zero, Vector2.one);
                view.Fill[i] = Orb(slot, "Fill", col, orbSize);
            }

            return view;
        }

        public static void SetDots(DotsView view, int matched, float alpha = 1f)
        {
            if (view == null)
                return;
            for (var i = 0; i < view.Pattern.Length; i++)
            {
                var col = ElementColorUtility.ToUnityColor(view.Pattern[i]);
                var on = i < matched;
                if (view.Fill[i] != null)
                {
                    view.Fill[i].gameObject.SetActive(on);
                    view.Fill[i].color = new Color(col.r, col.g, col.b, alpha);
                }

                if (view.Ring[i] != null)
                    view.Ring[i].color = new Color(col.r, col.g, col.b, (on ? 0f : 0.9f) * alpha);
            }
        }

        /// <summary>ストック末尾がパターン先頭と一致している長さ。</summary>
        public static int MatchedPrefix(OrbStock stock, ElementColor[] pattern)
        {
            if (stock == null || pattern == null)
                return 0;
            var max = Mathf.Min(pattern.Length, stock.Orbs.Count);
            for (var k = max; k > 0; k--)
            {
                if (!stock.TryGetSuffix(k, out var suffix))
                    continue;
                var ok = true;
                for (var i = 0; i < k; i++)
                {
                    if (suffix[i] != pattern[i])
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    return k;
            }

            return 0;
        }

        // ---------- Portraits ----------

        /// <summary>円形にくり抜いた肖像。</summary>
        public static Image CirclePortrait(Transform parent, string name, Sprite sprite, float size, Vector2 anchor,
            Vector2 position, Color ringColor, float ringWidth = 2f)
        {
            var holder = Place(parent, name, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            if (ringWidth > 0f)
            {
                var ring = Img(holder, "Ring", BattleUISprites.Circle, ringColor, Vector2.zero, Vector2.one,
                    new Vector2(-ringWidth, -ringWidth), new Vector2(ringWidth, ringWidth));
                ring.transform.SetAsFirstSibling();
            }

            var maskRt = Rect(holder, "Mask", Vector2.zero, Vector2.one);
            var maskImg = AddImage(maskRt, BattleUISprites.Circle, new Color(0.1f, 0.07f, 0.09f, 1f));
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _ = maskImg;
            var face = Img(maskRt, "Face", sprite, sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f),
                Vector2.zero, Vector2.one);
            face.preserveAspect = false;
            return face;
        }

        // ---------- NEXT SKILL row ----------

        /// <summary>右列 NEXT SKILL の1行。</summary>
        public static GameObject NextSkillRow(Transform parent, BattleSkillData skill, Sprite portrait, int matched,
            bool highlight, bool cooldown)
        {
            const float height = 76f;
            var go = new GameObject("NextSkill_" + (skill?.Id.ToString() ?? "hint"));
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = cooldown ? 0.45f : 1f;
            group.blocksRaycasts = false;

            if (highlight)
            {
                Img(rt, "Highlight", FadeRight, new Color(Gold.r, Gold.g, Gold.b, 0.26f), Vector2.zero, Vector2.one,
                    new Vector2(-8f, 3f), new Vector2(0f, -3f));
                Img(rt, "HighlightEdge", BattleUISprites.Solid, Gold, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(-8f, 6f), new Vector2(-5f, -6f));
            }

            Img(rt, "Separator", FadeRight, Separator, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 1f));

            CirclePortrait(rt, "Portrait", portrait, 52f, new Vector2(0f, 0.5f), new Vector2(30f, 0f),
                highlight ? Gold : new Color(1f, 1f, 1f, 0.28f));

            var name = Text(rt, "Name", skill?.DisplayName ?? "", Mincho, 22f, highlight ? Gold : Color.white,
                TextAlignmentOptions.BottomLeft, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                new Vector2(66f, -2f), new Vector2(-6f, -8f));
            name.enableAutoSizing = true;
            name.fontSizeMin = 15f;
            name.fontSizeMax = 22f;
            Outline(name, 0.2f, new Color(0f, 0f, 0f, 0.95f));

            var effectText = cooldown ? "クールダウン中" : BattlePartyCardUI.ShortEffect(skill);
            var effect = Text(rt, "Effect", effectText, Gothic, 14f, TextSoft, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(67f, 6f), new Vector2(-90f, 0f));
            effect.overflowMode = TextOverflowModes.Ellipsis;
            Outline(effect, 0.12f, new Color(0f, 0f, 0f, 0.9f), 0.5f);

            var dots = Dots(rt, skill?.Pattern, 14f, 4f, new Vector2(-4f, -12f));
            SetDots(dots, matched);
            return go;
        }

        public static GameObject HintRow(Transform parent, string message)
        {
            var go = new GameObject("NextSkillHint");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 52f;
            var label = Text(rt, "Text", message, Gothic, 15f, TextDim, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, Vector2.one, new Vector2(4f, 0f), Vector2.zero);
            Outline(label, 0.1f, Color.black, 0.5f);
            return go;
        }

        /// <summary>ストックの玉。color が null なら空き枠。</summary>
        public static GameObject StockOrb(Transform parent, ElementColor? color, float size)
        {
            if (parent != null && parent.GetComponent<BattleE5bStock>() != null)
                return BattleLayoutE5b.StockGem(parent, color, size);

            var go = new GameObject(color.HasValue ? "Orb" : "OrbEmpty");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;

            if (color.HasValue)
            {
                var col = ElementColorUtility.ToUnityColor(color.Value);
                Img(rt, "Glow", BattleUISprites.RadialGlow, new Color(col.r, col.g, col.b, 0.55f), Vector2.zero,
                    Vector2.one, new Vector2(-size * 0.35f, -size * 0.35f), new Vector2(size * 0.35f, size * 0.35f));
                Img(rt, "Rim", OrbRing, new Color(GoldDeep.r, GoldDeep.g, GoldDeep.b, 0.95f), Vector2.zero, Vector2.one,
                    new Vector2(-2f, -2f), new Vector2(2f, 2f));
                Img(rt, "Body", OrbBody, Color.Lerp(col, Color.white, 0.08f), Vector2.zero, Vector2.one);
                Img(rt, "Shine", OrbShine, Color.white, Vector2.zero, Vector2.one);
            }
            else
            {
                Img(rt, "Hole", BattleUISprites.Circle, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one,
                    new Vector2(3f, 3f), new Vector2(-3f, -3f));
                Img(rt, "Rim", OrbRing, new Color(Gold.r, Gold.g, Gold.b, 0.55f), Vector2.zero, Vector2.one);
            }

            return go;
        }
    }
}
