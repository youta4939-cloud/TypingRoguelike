using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// 大口のかみつき。演出は敵の周囲だけ。単語カードより奥に置き、画面全体は揺らさない。
    /// </summary>
    public sealed class EnemyBiteFx : MonoBehaviour
    {
        const int SparkCount = 11;
        const int StreakCount = 5;

        static Sprite _arc;
        static Sprite _streak;
        static Sprite _spark;

        readonly Piece[] _sparks = new Piece[SparkCount];
        readonly Piece[] _streaks = new Piece[StreakCount];
        readonly Vector2[] _sparkDir = new Vector2[SparkCount];
        readonly float[] _sparkDist = new float[SparkCount];
        readonly float[] _sparkSize = new float[SparkCount];

        Piece _bloom;
        Piece _upper;
        Piece _lower;
        Piece _smear;
        Piece _core;
        Piece _ring;
        Coroutine _play;

        static readonly Vector2 Mouth = new(-4f, 16f);
        static readonly Vector2 BiteDir = new(-0.78f, -0.22f);

        public static EnemyBiteFx Create(RectTransform parent)
        {
            var go = new GameObject("EnemyBiteFx", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(420f, 420f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            return go.AddComponent<EnemyBiteFx>();
        }

        void Awake()
        {
            EnsureSprites();
            _bloom = Make("Bloom", BattleUISprites.RadialGlow, 220f);
            _upper = Make("UpperFang", _arc, 210f);
            _lower = Make("LowerFang", _arc, 210f);
            _smear = Make("Smear", _arc, 250f);
            _core = Make("Core", _spark, 70f);
            _ring = Make("Ring", BattleUISprites.ThinRing, 80f);
            for (var i = 0; i < _streaks.Length; i++)
                _streaks[i] = Make("Streak" + i, _streak, 120f);
            for (var i = 0; i < _sparks.Length; i++)
                _sparks[i] = Make("Spark" + i, _spark, 16f);
            HideAll();
        }

        public void Play()
        {
            if (!isActiveAndEnabled)
                return;
            if (_play != null)
                StopCoroutine(_play);
            _play = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            HideAll();
            var dir = BiteDir.normalized;
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (var i = 0; i < _sparks.Length; i++)
            {
                var spread = Mathf.Lerp(-38f, 42f, i / (float)(_sparks.Length - 1));
                if (i >= _sparks.Length - 3)
                    spread += 70f;
                var rad = (angle + spread) * Mathf.Deg2Rad;
                _sparkDir[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                _sparkDist[i] = Random.Range(36f, 108f);
                _sparkSize[i] = Random.Range(7f, 16f);
            }

            const float snap = 0.11f;
            const float end = 0.46f;
            var elapsed = 0f;
            while (elapsed < end)
            {
                elapsed += Time.unscaledDeltaTime;
                Draw(elapsed, snap, dir, angle);
                yield return null;
            }

            HideAll();
            _play = null;
        }

        void Draw(float time, float snap, Vector2 dir, float angle)
        {
            var wind = Mathf.Clamp01(time / snap);
            var bite = Mathf.Clamp01((time - snap) / 0.07f);
            var biteEase = 1f - Mathf.Pow(1f - bite, 3f);
            var fade = Mathf.Clamp01((time - (snap + 0.05f)) / 0.28f);

            var bloomA = time < snap
                ? Mathf.Lerp(0f, 0.28f, wind)
                : Mathf.Lerp(0.55f, 0f, fade);
            Show(_bloom, Mouth, 0f, Vector2.one * Mathf.Lerp(150f, 250f, biteEase),
                new Color(0.85f, 0.08f, 0.05f, bloomA));

            if (time < snap)
            {
                Hide(_upper);
                Hide(_lower);
                Hide(_smear);
                Hide(_core);
                Hide(_ring);
                for (var i = 0; i < _streaks.Length; i++)
                {
                    var gather = Vector2.Lerp(Mouth + dir * (70f + i * 8f), Mouth + dir * 12f, wind);
                    Show(_streaks[i], gather, angle, new Vector2(Mathf.Lerp(20f, 70f, wind), 8f),
                        new Color(1f, 0.35f, 0.2f, 0.18f * wind));
                }

                for (var i = 0; i < _sparks.Length; i++)
                    Hide(_sparks[i]);
                return;
            }

            var upperRot = Mathf.Lerp(-32f, -6f, biteEase);
            var lowerRot = Mathf.Lerp(32f, 6f, biteEase);
            var jaw = Vector2.Lerp(new Vector2(0f, 22f), new Vector2(dir.x * 10f, 4f), biteEase);
            var fangColor = new Color(1f, 0.92f, 0.88f, (1f - fade) * Mathf.Clamp01(bite * 3f));
            Show(_upper, Mouth + new Vector2(jaw.x, jaw.y), upperRot, new Vector2(200f, 168f), fangColor);
            Show(_lower, Mouth + new Vector2(jaw.x, -jaw.y), lowerRot + 180f, new Vector2(200f, 168f), fangColor);

            var smearA = Mathf.Clamp01(1f - (time - snap) / 0.22f) * 0.45f;
            Show(_smear, Mouth + dir * 16f, angle - 8f, new Vector2(230f, 150f),
                new Color(1f, 0.22f, 0.12f, smearA));

            var flash = Mathf.Clamp01(1f - (time - snap) / 0.09f);
            Show(_core, Mouth + dir * 8f, 0f, Vector2.one * Mathf.Lerp(28f, 92f, 1f - flash),
                new Color(1f, 0.97f, 0.92f, flash));

            var ringT = Mathf.Clamp01((time - snap) / 0.2f);
            Show(_ring, Mouth, 0f, Vector2.one * Mathf.Lerp(36f, 210f, 1f - Mathf.Pow(1f - ringT, 2f)),
                new Color(1f, 0.28f, 0.16f, (1f - ringT) * 0.75f));

            for (var i = 0; i < _streaks.Length; i++)
            {
                var t = Mathf.Clamp01((time - snap) / 0.16f);
                var along = dir * Mathf.Lerp(8f, 28f + i * 10f, 1f - Mathf.Pow(1f - t, 2f));
                var sway = new Vector2(-dir.y, dir.x) * ((i - 2) * 7f);
                Show(_streaks[i], Mouth + along + sway, angle + (i - 2) * 4f,
                    new Vector2(Mathf.Lerp(40f, 118f, t), 7f + i),
                    new Color(1f, 0.45f, 0.28f, (1f - t) * 0.85f));
            }

            for (var i = 0; i < _sparks.Length; i++)
            {
                var delay = i * 0.008f;
                var t = Mathf.Clamp01((time - snap - delay) / 0.26f);
                if (t <= 0f)
                {
                    Hide(_sparks[i]);
                    continue;
                }

                var travel = 1f - Mathf.Pow(1f - t, 2f);
                var pos = Mouth + _sparkDir[i] * (_sparkDist[i] * travel);
                var a = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
                Show(_sparks[i], pos, 0f, Vector2.one * Mathf.Lerp(_sparkSize[i], 3f, t),
                    new Color(1f, 0.55f, 0.32f, Mathf.Clamp01(a)));
            }
        }

        static void Show(Piece piece, Vector2 pos, float rot, Vector2 size, Color color)
        {
            piece.Image.enabled = true;
            piece.Rect.anchoredPosition = pos;
            piece.Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
            piece.Rect.sizeDelta = size;
            piece.Image.color = color;
        }

        static void Hide(Piece piece)
        {
            if (piece?.Image != null)
                piece.Image.enabled = false;
        }

        void HideAll()
        {
            Hide(_bloom);
            Hide(_upper);
            Hide(_lower);
            Hide(_smear);
            Hide(_core);
            Hide(_ring);
            for (var i = 0; i < _streaks.Length; i++)
                Hide(_streaks[i]);
            for (var i = 0; i < _sparks.Length; i++)
                Hide(_sparks[i]);
        }

        Piece Make(string name, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.one;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.enabled = false;
            return new Piece { Image = image, Rect = rt };
        }

        static void EnsureSprites()
        {
            if (_arc == null)
                _arc = CreateArc(256);
            if (_streak == null)
                _streak = CreateStreak(256, 48);
            if (_spark == null)
                _spark = CreateSpark(64);
        }

        static Sprite CreateArc(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    var ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 0f)
                        ang += 360f;
                    var along = Mathf.InverseLerp(18f, 162f, ang);
                    var inside = along > 0f && along < 1f;
                    var taper = inside ? Mathf.Sin(along * Mathf.PI) : 0f;
                    var thickness = 0.035f + 0.09f * taper;
                    var radial = 1f - Mathf.Clamp01(Mathf.Abs(dist - 0.58f) / thickness);
                    radial = radial * radial * (3f - 2f * radial);
                    var a = radial * taper;
                    var edge = Mathf.Clamp01(Mathf.Abs(dist - 0.58f) / Mathf.Max(0.001f, thickness));
                    var color = Color.Lerp(new Color(1f, 0.97f, 0.94f, 1f), new Color(1f, 0.16f, 0.08f, 1f), edge);
                    color.a = a;
                    pixels[y * size + x] = color;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateStreak(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)(width - 1);
                    var v = Mathf.Abs(y / (float)(height - 1) * 2f - 1f);
                    var along = Mathf.Sin(u * Mathf.PI);
                    var across = Mathf.Clamp01(1f - v);
                    across *= across;
                    var a = along * across;
                    var color = Color.Lerp(new Color(1f, 0.95f, 0.9f, 1f), new Color(1f, 0.28f, 0.12f, 1f), v);
                    color.a = a;
                    pixels[y * width + x] = color;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateSpark(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / (size * 0.5f);
                    var a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    var color = Color.Lerp(new Color(1f, 0.98f, 0.95f, 1f), new Color(1f, 0.25f, 0.1f, 1f), Mathf.Clamp01(d * 1.4f));
                    color.a = a;
                    pixels[y * size + x] = color;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        sealed class Piece
        {
            public Image Image;
            public RectTransform Rect;
        }
    }
}
