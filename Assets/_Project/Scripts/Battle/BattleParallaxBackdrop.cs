using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// 戦闘背景を奥から空・塔・回廊・提灯の4枚で重ね、手前ほど大きく左右に揺らす。
    /// </summary>
    public sealed class BattleParallaxBackdrop : MonoBehaviour
    {
        const string Folder = "Battle/Art/Backgrounds/Parallax/";
        const float BaseSwayPixels = 56f;
        const float SwayRadiansPerSecond = 0.35f;

        struct LayerSpec
        {
            public string ResourceName;
            public string ObjectName;
            public float Sway;
            public float VerticalPixels;
            public float TiltDegrees;
        }

        static readonly LayerSpec[] Layers =
        {
            new LayerSpec { ResourceName = "sky", ObjectName = "Sky", Sway = 0.1f },
            new LayerSpec { ResourceName = "far", ObjectName = "Far", Sway = 0.3f },
            new LayerSpec { ResourceName = "mid", ObjectName = "Mid", Sway = 0.6f },
            new LayerSpec
            {
                ResourceName = "near",
                ObjectName = "Near",
                Sway = 1.2f,
                VerticalPixels = 16f,
                TiltDegrees = 1.1f
            }
        };

        RectTransform[] _rects;
        float[] _sway;
        float[] _vertical;
        float[] _tilt;

        public static bool TryBuild(RectTransform parent)
        {
            if (parent == null)
                return false;

            var sprites = new Sprite[Layers.Length];
            for (var i = 0; i < Layers.Length; i++)
            {
                sprites[i] = LoadLayer(Folder + Layers[i].ResourceName);
                if (sprites[i] == null)
                    return false;
            }

            var rootGo = new GameObject("ParallaxBackdrop");
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var backdrop = rootGo.AddComponent<BattleParallaxBackdrop>();
            backdrop._rects = new RectTransform[Layers.Length];
            backdrop._sway = new float[Layers.Length];
            backdrop._vertical = new float[Layers.Length];
            backdrop._tilt = new float[Layers.Length];

            for (var i = 0; i < Layers.Length; i++)
            {
                var spec = Layers[i];
                var bleed = 24f + BaseSwayPixels * spec.Sway;
                var go = new GameObject(spec.ObjectName);
                go.transform.SetParent(root, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(-bleed, -bleed);
                rt.offsetMax = new Vector2(bleed, bleed);
                var image = go.AddComponent<Image>();
                image.sprite = sprites[i];
                image.color = Color.white;
                image.raycastTarget = false;
                image.preserveAspect = false;
                backdrop._rects[i] = rt;
                backdrop._sway[i] = spec.Sway;
                backdrop._vertical[i] = spec.VerticalPixels;
                backdrop._tilt[i] = spec.TiltDegrees;
            }

            return true;
        }

        void Update()
        {
            if (_rects == null)
                return;

            var time = Time.unscaledTime;
            var wave = Mathf.Sin(time * SwayRadiansPerSecond);
            for (var i = 0; i < _rects.Length; i++)
            {
                var rt = _rects[i];
                if (rt == null)
                    continue;

                var x = wave * BaseSwayPixels * _sway[i];
                var y = _vertical[i] > 0f ? Mathf.Sin(time * 0.85f) * _vertical[i] : 0f;
                rt.anchoredPosition = new Vector2(x, y);
                var tilt = _tilt[i] > 0f ? Mathf.Sin(time * 0.7f) * _tilt[i] : 0f;
                rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }
        }

        static Sprite LoadLayer(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
                return sprite;

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                return null;

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
