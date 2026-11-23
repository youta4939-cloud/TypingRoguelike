using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>Keep the route visible until drifting mist fully covers the screen.</summary>
    public sealed class StoryMistTransition : MonoBehaviour
    {
        public const float Duration = 1.2f;
        public bool IsRunning { get; private set; }
        GameObject _previous;
        GameObject _veil;
        CanvasGroup _destination;
        RawImage _mist;
        Material _mistMaterial;
        float _elapsed;
        static Texture2D _clouds;

        public void Begin(GameObject previous, Transform overlayHost)
        {
            _previous = previous;
            _destination = GetComponent<CanvasGroup>();
            if (_destination == null) _destination = gameObject.AddComponent<CanvasGroup>();
            _destination.alpha = 0f;
            _destination.interactable = false;
            _destination.blocksRaycasts = false;
            if (_previous != null)
            {
                var input = _previous.GetComponent<CanvasGroup>();
                if (input == null) input = _previous.AddComponent<CanvasGroup>();
                input.interactable = false;
                input.blocksRaycasts = false;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _veil = new GameObject("EventMist", typeof(RectTransform), typeof(RawImage));
            _veil.transform.SetParent(overlayHost, false);
            _veil.layer = gameObject.layer;
            var rect = (RectTransform)_veil.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _mist = _veil.GetComponent<RawImage>();
            _mist.texture = Clouds();
            var shader = Resources.Load<Shader>("Run/StoryMist");
            if (shader != null)
            {
                _mistMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _mist.material = _mistMaterial;
            }
            // A full-screen raycast surface also consumes clicks while transparent.
            _mist.raycastTarget = true;
            _elapsed = 0f;
            IsRunning = true;
            Advance(0f);
        }

        void Update() => Advance(Time.unscaledDeltaTime);

        public void Advance(float seconds)
        {
            if (!IsRunning) return;
            _elapsed += Mathf.Max(0f, seconds);
            var p = Mathf.Clamp01(_elapsed / Duration);
            // Reach opacity 1 before replacing the underlying screen: no visible cut.
            var cover = p < 0.45f ? Smooth(p / 0.45f)
                : p < 0.55f ? 1f : 1f - Smooth((p - 0.55f) / 0.45f);
            _mist.color = new Color(1f, 1f, 1f, cover);
            _mist.uvRect = new Rect(p * 0.3f, p * 0.08f, 0.75f, 0.85f);
            if (p >= 0.5f)
            {
                ReleasePrevious();
                _destination.alpha = 1f;
            }
            if (p >= 1f) Complete();
        }

        public void Complete()
        {
            IsRunning = false;
            ReleasePrevious();
            if (_destination != null)
            {
                _destination.alpha = 1f;
                _destination.interactable = true;
                _destination.blocksRaycasts = true;
            }
            if (_veil != null)
            {
                _veil.SetActive(false);
                Dispose(_veil);
                _veil = null;
            }
            if (_mistMaterial != null)
            {
                Dispose(_mistMaterial);
                _mistMaterial = null;
            }
            enabled = false;
        }

        void OnDisable() { if (IsRunning) Complete(); }
        void ReleasePrevious()
        {
            if (_previous == null) return;
            _previous.SetActive(false);
            Dispose(_previous);
            _previous = null;
        }
        static void Dispose(Object target)
        {
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
        static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        internal static Texture2D SharedMistClouds() => Clouds();

        static Texture2D Clouds()
        {
            if (_clouds != null) return _clouds;
            // Small, shared, seamless texture. No per-frame texture allocation or shader dependency.
            const int size = 128;
            _clouds = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Event mist (procedural)", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (float)x / size;
                var v = (float)y / size;
                var n = 0f;
                for (var octave = 0; octave < 3; octave++)
                {
                    var f = 4f * (1 << octave);
                    var a = Mathf.Lerp(Mathf.PerlinNoise(u * f + 13, v * f + 31),
                        Mathf.PerlinNoise((u - 1) * f + 13, v * f + 31), Smooth(u));
                    var b = Mathf.Lerp(Mathf.PerlinNoise(u * f + 13, (v - 1) * f + 31),
                        Mathf.PerlinNoise((u - 1) * f + 13, (v - 1) * f + 31), Smooth(u));
                    n += Mathf.Lerp(a, b, Smooth(v)) * (0.57f / (1 << octave));
                }
                // Expand the noise range: low-contrast dark pixels read as a blackout.
                var shade = (byte)(255f * Smooth(Mathf.InverseLerp(0.25f, 0.75f, n / 0.9975f)));
                pixels[y * size + x] = new Color32(shade, shade, shade, 255);
            }
            _clouds.SetPixels32(pixels);
            _clouds.Apply(false, true);
            return _clouds;
        }
    }
}
