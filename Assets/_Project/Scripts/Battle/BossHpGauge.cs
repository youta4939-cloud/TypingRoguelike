using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>ボスHP。中身は即時、減った分の帯は遅れて縮み、右端から血しぶきが出る。</summary>
    public sealed class BossHpGauge : MonoBehaviour
    {
        public const float BarWidth = 1440f;
        public const float BarHeight = 220f;
        const string ResourceFolder = "Battle/Art/EnemyHP/";

        [Header("HP")]
        public int currentHp = 10000;
        public int maxHp = 10000;

        [Header("ゲージ範囲（画像x）")]
        public float fillStartPx = 79f;
        public float fillEndPx = 1382f;
        public float barWidthPx = BarWidth;
        public float fillCenterFromTop = 104f;

        [Header("ダメージ帯")]
        public float trailDelay = 0.4f;
        public float trailDuration = 0.5f;

        [Header("血しぶき")]
        public float splashDuration = 0.55f;
        [Tooltip("幅が開くまでの時間。残りはフェード")]
        public float splashBurstTime = 0.1f;
        [Tooltip("噴き出しの幅（ゲージ画像のピクセル）。減った量では伸ばさない")]
        public float splashWidthPx = 180f;
        [Tooltip("噴き出しの高さ")]
        public float splashHeight = 0.45f;

        [Header("数字")]
        public float countDuration = 0.3f;
        public float currentFontSize = 128f;
        public float maxFontSize = 60f;
        public float outlineWidth = 0.22f;
        public Vector2 textOffset = new(-690f, -68f);
        public Color suffixSilver = new(0.78f, 0.78f, 0.82f, 1f);
        public Color suffixDanger = new(0.89f, 0.18f, 0.14f, 1f);
        public Color silverTop = Color.white;
        public Color silverBottom = new(0.62f, 0.63f, 0.66f, 1f);
        public Color dangerTop = new(1f, 0.32f, 0.24f, 1f);
        public Color dangerBottom = new(0.62f, 0.05f, 0.06f, 1f);

        [Header("危険（30%以下）")]
        [Range(0f, 1f)] public float dangerThreshold = 0.3f;
        public float dangerPulsePeriod = 1.2f;
        [Range(0f, 1f)] public float dangerAlphaMin = 0.4f;
        [Range(0f, 1f)] public float dangerAlphaMax = 1f;

        [Header("配置")]
        public float tiltDegrees = 0f;

        Image _trail;
        Image _fill;
        Image _glow;
        RectTransform _splash;
        Image _splashImage;
        float _splashNativeWidth = 840f;
        TextMeshProUGUI _hpText;

        int _targetCurrent;
        int _targetMax;
        int _displayCurrent;
        float _fillRatio = 1f;
        float _trailRatio = 1f;
        bool _hasShown;
        bool _danger;
        bool _built;
        Coroutine _trailRoutine;
        Coroutine _splashRoutine;
        Coroutine _countRoutine;
        static TMP_FontAsset _font;

        public static BossHpGauge Create(RectTransform parent, Vector2 topAnchor, float topOffset, float scale)
        {
            var go = new GameObject("HPGauge", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = topAnchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(BarWidth, BarHeight);
            rt.localScale = Vector3.one * scale;
            var half = BarHeight * scale * 0.5f;
            rt.anchoredPosition = new Vector2(0f, topOffset - half);
            var gauge = go.AddComponent<BossHpGauge>();
            rt.localRotation = Quaternion.Euler(0f, 0f, gauge.tiltDegrees);
            return gauge;
        }

        void Awake() => Build();

        void Update()
        {
            if (!_built)
                return;
            if (_hasShown && (currentHp != _targetCurrent || maxHp != _targetMax))
                SetHP(currentHp, maxHp, true);
            PulseDanger();
        }

        public void SetHP(int current, int max, bool animate = true)
        {
            if (!_built)
                Build();
            max = Mathf.Max(1, max);
            current = Mathf.Clamp(current, 0, max);
            if (_hasShown && current == _targetCurrent && max == _targetMax)
            {
                currentHp = current;
                maxHp = max;
                return;
            }

            var fromNumber = _hasShown ? _displayCurrent : current;
            var damaged = _hasShown && animate && current < _targetCurrent;
            var previousRatio = _targetMax > 0 ? _targetCurrent / (float)_targetMax : 1f;
            currentHp = current;
            maxHp = max;
            _targetCurrent = current;
            _targetMax = max;
            var ratio = current / (float)max;
            _fillRatio = ratio;
            ApplyFill(ratio);
            PlaceSplash(ratio);
            _danger = ratio <= dangerThreshold;
            ApplyGradient();

            if (!_hasShown || !animate)
            {
                _hasShown = true;
                _displayCurrent = current;
                _trailRatio = ratio;
                ApplyTrail(ratio);
                ApplyText(current, max);
                HideSplash();
                return;
            }

            _hasShown = true;
            if (damaged)
            {
                BeginTrail(ratio);
                BeginSplash(previousRatio, ratio);
            }
            else
            {
                StopTrail();
                _trailRatio = ratio;
                ApplyTrail(ratio);
                HideSplash();
            }

            BeginCount(fromNumber, current, max);
        }

        void Build()
        {
            if (_built)
                return;
            _built = true;

            _trail = CreateSprite("DamageTrail", "part_damage_trail", true);
            _fill = CreateSprite("Fill", "part_fill", true);
            var frameImage = CreateSprite("Frame", "part_frame", false);
            _glow = CreateSprite("DangerGlow", "part_danger_glow", false);
            if (frameImage != null && frameImage.sprite != null)
                ApplyFrameAndGlow(frameImage);
            _splashImage = CreateSprite("BloodSplash", "part_blood_splash", false);
            if (_splashImage != null)
            {
                _splash = _splashImage.rectTransform;
                var splashSprite = _splashImage.sprite;
                _splash.pivot = new Vector2(0f, 0.5f);
                _splashNativeWidth = splashSprite != null ? splashSprite.rect.width : 840f;
                _splash.sizeDelta = splashSprite != null
                    ? new Vector2(splashSprite.rect.width, splashSprite.rect.height)
                    : new Vector2(840f, 320f);
                _splash.localScale = new Vector3(0f, splashHeight, 1f);
            }

            _hpText = CreateText();
            if (_glow != null)
                _glow.rectTransform.SetSiblingIndex(0);
            if (_trail != null)
                _trail.rectTransform.SetSiblingIndex(1);
            if (_fill != null)
                _fill.rectTransform.SetSiblingIndex(2);
            var frame = transform.Find("Frame");
            frame?.SetSiblingIndex(3);
            _splash?.SetSiblingIndex(4);
            if (_hpText != null)
                _hpText.rectTransform.SetSiblingIndex(5);

            ApplyFill(0f);
            ApplyTrail(0f);
            HideSplash();
        }

        Image CreateSprite(string objectName, string resourceName, bool filled)
        {
            var sprite = Resources.Load<Sprite>(ResourceFolder + resourceName);
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.sizeDelta = sprite != null
                ? new Vector2(sprite.rect.width, sprite.rect.height)
                : new Vector2(BarWidth, BarHeight);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = false;
            if (!filled)
                return image;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;
            return image;
        }

        TextMeshProUGUI CreateText()
        {
            var go = new GameObject("HPText", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(1100f, 160f);
            rt.anchoredPosition = textOffset;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = LoadFont();
            text.fontSize = currentFontSize;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.characterSpacing = -2f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            text.extraPadding = true;
            text.enableVertexGradient = true;
            if (text.fontMaterial != null)
                text.fontMaterial = new Material(text.fontMaterial);
            text.outlineWidth = outlineWidth;
            text.outlineColor = Color.black;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(4f, -3f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        static TMP_FontAsset LoadFont()
        {
            if (_font != null)
                return _font;
            var source = Resources.Load<Font>(ResourceFolder + "BarlowCondensed-BoldItalic");
            if (source != null)
            {
                _font = TMP_FontAsset.CreateFontAsset(
                    source, 96, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            }

            return _font != null ? _font : TMP_Settings.defaultFontAsset;
        }

        void BeginTrail(float ratio)
        {
            StopTrail();
            _trailRoutine = StartCoroutine(TrailRoutine(ratio));
        }

        void StopTrail()
        {
            if (_trailRoutine == null)
                return;
            StopCoroutine(_trailRoutine);
            _trailRoutine = null;
        }

        IEnumerator TrailRoutine(float target)
        {
            var wait = Mathf.Max(0f, trailDelay);
            while (wait > 0f)
            {
                wait -= Time.deltaTime;
                yield return null;
            }

            var from = _trailRatio;
            var duration = Mathf.Max(0.01f, trailDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = EaseOut(Mathf.Clamp01(elapsed / duration));
                _trailRatio = Mathf.Lerp(from, target, t);
                ApplyTrail(_trailRatio);
                yield return null;
            }

            _trailRatio = target;
            ApplyTrail(target);
            _trailRoutine = null;
        }

        void BeginSplash(float fromRatio, float toRatio)
        {
            if (_splash == null)
                return;
            if (Mathf.Abs(fromRatio - toRatio) < 0.0001f)
                return;
            var width = Mathf.Clamp(splashWidthPx, 1f, _splashNativeWidth);
            PlaceSplash(Mathf.Min(fromRatio, toRatio));
            if (_splashRoutine != null)
                StopCoroutine(_splashRoutine);
            _splashRoutine = StartCoroutine(SplashRoutine(width / Mathf.Max(1f, _splashNativeWidth)));
        }

        IEnumerator SplashRoutine(float targetScaleX)
        {
            var burst = Mathf.Max(0.04f, splashBurstTime);
            var fade = Mathf.Max(0.12f, splashDuration - burst);
            var elapsed = 0f;
            while (elapsed < burst)
            {
                elapsed += Time.deltaTime;
                var u = EaseOutCubic(Mathf.Clamp01(elapsed / burst));
                var y = Mathf.Lerp(splashHeight * 0.35f, splashHeight * 1.18f, u);
                _splash.localScale = new Vector3(Mathf.Lerp(0.02f, targetScaleX, u), y, 1f);
                SetSplashAlpha(1f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fade)
            {
                elapsed += Time.deltaTime;
                var u = Mathf.Clamp01(elapsed / fade);
                var y = Mathf.Lerp(splashHeight * 1.18f, splashHeight, EaseOut(u));
                _splash.localScale = new Vector3(targetScaleX, y, 1f);
                SetSplashAlpha(1f - EaseIn(u));
                yield return null;
            }

            HideSplash();
            _splashRoutine = null;
        }

        void BeginCount(int from, int to, int max)
        {
            if (_countRoutine != null)
                StopCoroutine(_countRoutine);
            _countRoutine = StartCoroutine(CountRoutine(from, to, max));
        }

        IEnumerator CountRoutine(int from, int to, int max)
        {
            var duration = Mathf.Max(0.01f, countDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = EaseOut(Mathf.Clamp01(elapsed / duration));
                _displayCurrent = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                ApplyText(_displayCurrent, max);
                yield return null;
            }

            _displayCurrent = to;
            ApplyText(to, max);
            _countRoutine = null;
        }

        void ApplyFill(float ratio)
        {
            if (_fill != null)
                _fill.fillAmount = ToFillAmount(ratio);
        }

        void ApplyTrail(float ratio)
        {
            if (_trail != null)
                _trail.fillAmount = ToFillAmount(ratio);
        }

        float FillImageX(float ratio) =>
            fillStartPx + (fillEndPx - fillStartPx) * Mathf.Clamp01(ratio);

        float ToFillAmount(float ratio)
        {
            var width = Mathf.Max(1f, barWidthPx);
            return Mathf.Clamp01(FillImageX(ratio) / width);
        }

        void PlaceSplash(float ratio)
        {
            if (_splash == null)
                return;
            var imageX = fillStartPx + (fillEndPx - fillStartPx) * Mathf.Clamp01(ratio);
            var x = imageX - barWidthPx * 0.5f;
            var y = BarHeight * 0.5f - fillCenterFromTop;
            _splash.anchoredPosition = new Vector2(x, y);
        }

        void HideSplash()
        {
            if (_splashImage == null)
                return;
            var color = _splashImage.color;
            color.a = 0f;
            _splashImage.color = color;
            _splash.localScale = new Vector3(0f, splashHeight, 1f);
        }

        void SetSplashAlpha(float alpha)
        {
            if (_splashImage == null)
                return;
            var color = _splashImage.color;
            color.a = alpha;
            _splashImage.color = color;
        }

        void ApplyText(int current, int max)
        {
            if (_hpText == null)
                return;
            _hpText.fontSize = currentFontSize;
            _hpText.rectTransform.anchoredPosition = textOffset;
            var suffix = ColorUtility.ToHtmlStringRGB(_danger ? suffixDanger : suffixSilver);
            _hpText.text = $"{Mathf.Max(0, current)}<size={maxFontSize:0}><color=#{suffix}> / {Mathf.Max(1, max)}</color></size>";
            ApplyGradient();
        }

        void ApplyGradient()
        {
            if (_hpText == null)
                return;
            var top = _danger ? dangerTop : silverTop;
            var bottom = _danger ? dangerBottom : silverBottom;
            _hpText.color = Color.white;
            _hpText.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        void PulseDanger()
        {
            if (_glow == null)
                return;
            if (!_danger)
            {
                SetGlow(0f);
                return;
            }

            var period = Mathf.Max(0.05f, dangerPulsePeriod);
            var wave = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / period);
            SetGlow(Mathf.Lerp(dangerAlphaMin, dangerAlphaMax, wave));
        }

        void SetGlow(float alpha)
        {
            var color = _glow.color;
            color.a = alpha;
            _glow.color = color;
        }

        static float EaseIn(float t) => t * t;

        static float EaseOutCubic(float t)
        {
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        void ApplyFrameAndGlow(Image frameImage)
        {
            var source = frameImage.sprite;
            if (source == null || source.texture == null || !source.texture.isReadable)
                return;

            var rect = source.textureRect;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height);
            var pixels = source.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
            var opened = new Color[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a > 0.15f && pixel.r < 0.1f && pixel.g < 0.1f && pixel.b < 0.1f)
                    pixel.a = 0f;
                opened[i] = pixel;
            }

            var frameTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            frameTex.SetPixels(opened);
            frameTex.Apply();
            frameImage.sprite = Sprite.Create(frameTex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            frameImage.rectTransform.sizeDelta = new Vector2(width, height);

            if (_glow == null)
                return;
            var glow = BuildOuterGlow(opened, width, height);
            if (glow == null)
                return;
            _glow.sprite = glow;
            _glow.rectTransform.sizeDelta = new Vector2(glow.rect.width, glow.rect.height);
            _glow.color = Color.white;
        }

        static Sprite BuildOuterGlow(Color[] frame, int width, int height)
        {
            const int pad = 36;
            var glowW = width + pad * 2;
            var glowH = height + pad * 2;
            var alpha = new float[glowW * glowH];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    alpha[(y + pad) * glowW + (x + pad)] = frame[y * width + x].a;
            }

            var blurred = Blur(Blur(alpha, glowW, glowH, 8), glowW, glowH, 14);
            var colors = new Color32[glowW * glowH];
            for (var i = 0; i < colors.Length; i++)
            {
                var spill = blurred[i] * (1f - alpha[i]);
                var a = Mathf.Clamp01(Mathf.Pow(Mathf.Max(0f, spill), 0.7f) * 1.6f);
                colors[i] = new Color32(255, 28, 18, (byte)(a * 255f));
            }

            var tex = new Texture2D(glowW, glowH, TextureFormat.RGBA32, false);
            tex.SetPixels32(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, glowW, glowH), new Vector2(0.5f, 0.5f), 100f);
        }

        static float[] Blur(float[] src, int width, int height, int radius)
        {
            var horiz = new float[src.Length];
            var div = radius * 2 + 1;
            for (var y = 0; y < height; y++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                    sum += src[y * width + Mathf.Clamp(k, 0, width - 1)];
                for (var x = 0; x < width; x++)
                {
                    horiz[y * width + x] = sum / div;
                    sum += src[y * width + Mathf.Clamp(x + radius + 1, 0, width - 1)];
                    sum -= src[y * width + Mathf.Clamp(x - radius, 0, width - 1)];
                }
            }

            var vert = new float[src.Length];
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                    sum += horiz[Mathf.Clamp(k, 0, height - 1) * width + x];
                for (var y = 0; y < height; y++)
                {
                    vert[y * width + x] = sum / div;
                    sum += horiz[Mathf.Clamp(y + radius + 1, 0, height - 1) * width + x];
                    sum -= horiz[Mathf.Clamp(y - radius, 0, height - 1) * width + x];
                }
            }

            return vert;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
