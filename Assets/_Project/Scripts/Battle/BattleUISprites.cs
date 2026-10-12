using UnityEngine;

namespace TypingRoguelike.Battle
{
    /// <summary>Unity 6 等で組み込み UI/Skin/*.psd が取れない場合のランタイム用スプライト。</summary>
    public static class BattleUISprites
    {
        static Sprite _solid;
        static Sprite _circle;
        static Sprite _verticalGradient;
        static Sprite _horizontalGradient;
        static Sprite _radialGlow;
        static Sprite _diamond;

        public static Sprite Solid => _solid ? _solid : _solid = CreateSolid(4, 4);

        public static Sprite Circle => _circle ? _circle : _circle = CreateCircle(128);

        public static Sprite VerticalGradient =>
            _verticalGradient ? _verticalGradient : _verticalGradient = CreateGradient(4, 128, true);

        public static Sprite HorizontalGradient =>
            _horizontalGradient ? _horizontalGradient : _horizontalGradient = CreateGradient(128, 4, false);

        public static Sprite RadialGlow => _radialGlow ? _radialGlow : _radialGlow = CreateRadialGlow(128);

        public static Sprite Diamond => _diamond ? _diamond : _diamond = CreateDiamond(96);

        static Sprite _ring;
        static Sprite _thinRing;

        /// <summary>太めの円環（攻撃タイマー用）。</summary>
        public static Sprite Ring => _ring ? _ring : _ring = CreateRing(512, 0.86f);

        /// <summary>細い円環（装飾用）。</summary>
        public static Sprite ThinRing => _thinRing ? _thinRing : _thinRing = CreateRing(512, 0.975f);

        static Sprite _wordCardPremium;
        static Sprite _arenaFloorPremium;

        public static Sprite WordCardPremium
        {
            get
            {
                if (_wordCardPremium)
                    return _wordCardPremium;
                _wordCardPremium = Resources.Load<Sprite>("Battle/UI/Premium_Word_Card");
                if (!_wordCardPremium)
                    _wordCardPremium = Resources.Load<Sprite>("Battle/UI/Commercial_Word_Frame");
                return _wordCardPremium;
            }
        }

        public static Sprite ArenaFloorPremium
        {
            get
            {
                if (_arenaFloorPremium)
                    return _arenaFloorPremium;
                _arenaFloorPremium = Resources.Load<Sprite>("Battle/UI/Premium_Arena_Floor");
                return _arenaFloorPremium;
            }
        }

        public static void ResetCache()
        {
            _wordCardPremium = null;
            _arenaFloorPremium = null;
            _ring = null;
            _thinRing = null;
            _solid = null;
            _circle = null;
            _verticalGradient = null;
            _horizontalGradient = null;
            _radialGlow = null;
            _diamond = null;
        }

        static Sprite CreateSolid(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = (size - 1) * 0.5f;
            var radius = size * 0.48f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var a = Mathf.Clamp01(radius - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateGradient(int width, int height, bool vertical)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var t = vertical
                        ? y / (float)(height - 1)
                        : x / (float)(width - 1);
                    var alpha = Mathf.SmoothStep(0f, 1f, t);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateRadialGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - normalized), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateRing(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = (size - 1) * 0.5f;
            var outer = size * 0.5f - 1f;
            var inner = outer * innerRatio;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateDiamond(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = (size - 1) * 0.5f;
            var radius = size * 0.46f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    var alpha = Mathf.Clamp01(radius - distance + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
