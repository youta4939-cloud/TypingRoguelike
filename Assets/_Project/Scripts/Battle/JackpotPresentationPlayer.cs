using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public struct JackpotBgmSlot
    {
        public AudioSource Source;
        public float Time;
        public float Volume;
        public bool WasPlaying;
    }

    public static class JackpotBgm
    {
        public static JackpotBgmSlot[] Capture(AudioSource except)
        {
            var found = UnityEngine.Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var held = new List<JackpotBgmSlot>();
            foreach (var source in found)
            {
                if (source == null || source == except || !source.isPlaying)
                    continue;
                var clip = source.clip;
                var longClip = clip != null && clip.length >= 8f;
                if (!source.loop && !longClip)
                    continue;

                held.Add(new JackpotBgmSlot
                {
                    Source = source,
                    Time = source.time,
                    Volume = source.volume,
                    WasPlaying = true
                });
                source.volume = 0f;
                source.Pause();
            }

            return held.ToArray();
        }

        public static void RePause(JackpotBgmSlot[] slots)
        {
            if (slots == null)
                return;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Source == null)
                    continue;
                if (slot.Source.isPlaying)
                    slot.Time = slot.Source.time;
                slot.Source.volume = 0f;
                slot.Source.Pause();
                slots[i] = slot;
            }
        }

        public static void Restore(JackpotBgmSlot[] slots)
        {
            if (slots == null)
                return;
            foreach (var slot in slots)
                Apply(slot, slot.Volume);
        }

        public static void BeginFromMark(JackpotBgmSlot[] slots)
        {
            if (slots == null)
                return;
            foreach (var slot in slots)
                Apply(slot, 0f);
        }

        static void Apply(JackpotBgmSlot slot, float volume)
        {
            var source = slot.Source;
            if (source == null)
                return;
            source.volume = volume;
            if (!slot.WasPlaying || source.clip == null)
                return;

            var length = source.clip.length;
            var time = length > 0.05f ? Mathf.Clamp(slot.Time, 0f, length - 0.02f) : 0f;
            source.time = time;
            source.UnPause();
            if (!source.isPlaying)
            {
                source.time = time;
                source.Play();
            }
        }
    }

    public static class JackpotPresentationPlayer
    {
        sealed class Blade
        {
            public RectTransform Rect;
            public float Born;
            public float HitAt;
            public Vector2 From;
            public Vector2 To;
            public bool Struck;
            public float Pitch;
        }

        sealed class View
        {
            public GameObject Root;
            public RectTransform Area;
            public Image Black;
            public RectTransform LineLeft;
            public RectTransform LineRight;
            public RectTransform Flare;
            public CanvasGroup Cutins;
            public RectTransform[] Bands;
            public RectTransform BladeRoot;
            public Text Damage;
        }

        public static IEnumerator Play(
            Transform host,
            JackpotPresentationDefinition def,
            int damage,
            IReadOnlyList<string> words,
            RectTransform enemy,
            AudioSource se,
            Action onBlackoutImpact,
            Action onBladeHit,
            Action onDamageLanded)
        {
            def = def != null ? def : ScriptableObject.CreateInstance<JackpotPresentationDefinition>();
            var view = CreateView(host, def);
            try
            {
                var phases = new IEnumerator[]
                {
                    PhaseBlackout(view, def, se, onBlackoutImpact),
                    PhaseCutin(view, def, se),
                    PhaseAttacks(view, def, se, words, enemy, onBladeHit),
                    PhaseTotal(view, def, se, damage, onDamageLanded)
                };
                foreach (var phase in phases)
                {
                    while (phase.MoveNext())
                        yield return phase.Current;
                }
            }
            finally
            {
                if (view.Root != null)
                    UnityEngine.Object.Destroy(view.Root);
            }
        }

        static View CreateView(Transform host, JackpotPresentationDefinition def)
        {
            var view = new View();
            var go = new GameObject("JackpotPresentation");
            if (host != null)
                go.transform.SetParent(host, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            view.Root = go;
            view.Area = go.GetComponent<RectTransform>();

            var blocker = CreateImage(view.Area, "Blocker", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            blocker.raycastTarget = true;

            view.Black = CreateImage(view.Area, "Black", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            view.Black.raycastTarget = false;

            var additive = ResolveAdditive(def);
            view.LineLeft = CreateBeam(view.Area, "LineLeft", true, def.lineSprite, def.lineColor, def.lineThickness, additive);
            view.LineRight = CreateBeam(view.Area, "LineRight", false, def.lineSprite, def.lineColor, def.lineThickness, additive);
            view.Flare = CreateFlare(view.Area, def.flareSprite, def.flareColor, additive);

            view.Cutins = CreateCutins(view.Area, def, out var bands);
            view.Bands = bands;
            view.Cutins.alpha = 0f;
            view.Cutins.gameObject.SetActive(false);

            var blades = CreateRect(view.Area, "Blades", Vector2.zero, Vector2.one);
            view.BladeRoot = blades;

            view.Damage = CreateText(view.Area, "TotalDamage", def.damageFont != null ? def.damageFont : BattleUITheme.FontDisplay,
                def.damageFontSize, def.damageColor);
            view.Damage.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            return view;
        }

        static IEnumerator PhaseBlackout(View view, JackpotPresentationDefinition def, AudioSource se, Action onImpact)
        {
            var seconds = Mathf.Max(0.05f, def.blackoutSeconds);
            var impactAt = seconds * Mathf.Clamp(def.blackoutImpactNormalized, 0.2f, 0.85f);
            var flareEnd = impactAt + (seconds - impactAt) * 0.78f;
            var elapsed = 0f;
            var impacted = false;
            view.Flare.gameObject.SetActive(false);
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var half = Mathf.Max(8f, view.Area.rect.width * 0.5f + 36f);
                if (elapsed < impactAt)
                {
                    var t = EaseIn(Mathf.Clamp01(elapsed / impactAt));
                    SetBeamWidth(view.LineLeft, half * t);
                    SetBeamWidth(view.LineRight, half * t);
                    SetBlack(view, 0f);
                }
                else
                {
                    SetBeamWidth(view.LineLeft, half);
                    SetBeamWidth(view.LineRight, half);
                    if (!impacted)
                    {
                        impacted = true;
                        onImpact?.Invoke();
                        PlaySe(se, def.blackoutClip, def.blackoutVolume, 1f);
                        view.Flare.gameObject.SetActive(true);
                    }

                    var burst = Mathf.Clamp01((elapsed - impactAt) / Mathf.Max(0.01f, flareEnd - impactAt));
                    var fade = Mathf.Clamp01((elapsed - flareEnd) / Mathf.Max(0.01f, seconds - flareEnd));
                    var flareT = elapsed >= flareEnd ? 1f : burst;
                    var scale = Mathf.Lerp(1.45f, 0f, EaseOut(flareT));
                    view.Flare.sizeDelta = Vector2.one * (def.flareMaxSize * scale);
                    var flareImage = view.Flare.GetComponent<Image>();
                    if (flareImage != null)
                    {
                        var color = def.flareColor;
                        color.a *= 1f - flareT;
                        flareImage.color = color;
                    }

                    var lineFade = Mathf.Clamp01(burst * 1.4f);
                    FadeBeam(view.LineLeft, def.lineColor, 1f - lineFade);
                    FadeBeam(view.LineRight, def.lineColor, 1f - lineFade);
                    var black = elapsed >= flareEnd ? 1f : Mathf.SmoothStep(0f, 1f, burst);
                    if (fade > 0f)
                        black = 1f;
                    SetBlack(view, black);
                }

                yield return null;
            }

            SetBlack(view, 1f);
            view.LineLeft.gameObject.SetActive(false);
            view.LineRight.gameObject.SetActive(false);
            view.Flare.gameObject.SetActive(false);
        }

        static IEnumerator PhaseCutin(View view, JackpotPresentationDefinition def, AudioSource se)
        {
            var seconds = Mathf.Max(0.05f, def.cutinSeconds);
            view.Cutins.gameObject.SetActive(true);
            view.Cutins.alpha = 1f;
            SetBlack(view, 1f);
            PlaySe(se, def.cutinClip, def.cutinVolume, 1f);
            var elapsed = 0f;
            var slide = Mathf.Min(0.42f, seconds * 0.4f);
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                for (var i = 0; i < view.Bands.Length; i++)
                {
                    var band = view.Bands[i];
                    if (band == null)
                        continue;
                    var start = i * seconds * 0.22f;
                    var t = EaseOut(Mathf.Clamp01((elapsed - start) / slide));
                    var from = i % 2 == 0 ? -1500f : 1500f;
                    var pos = band.anchoredPosition;
                    pos.x = Mathf.Lerp(from, 0f, t);
                    band.anchoredPosition = pos;
                }

                yield return null;
            }
        }

        static IEnumerator PhaseAttacks(
            View view,
            JackpotPresentationDefinition def,
            AudioSource se,
            IReadOnlyList<string> words,
            RectTransform enemy,
            Action onBladeHit)
        {
            var seconds = Mathf.Max(0.05f, def.attackSeconds);
            var labels = new List<string>();
            if (words != null)
            {
                foreach (var word in words)
                {
                    if (!string.IsNullOrEmpty(word))
                        labels.Add(word);
                }
            }

            if (labels.Count == 0)
                labels.Add("JACKPOT");

            var count = labels.Count;
            var slot = seconds / count;
            var travel = Mathf.Min(0.28f, slot * 0.75f);
            var target = EnemyLocalPoint(view.Area, enemy);
            var blades = new Blade[count];
            for (var i = 0; i < count; i++)
            {
                var hitAt = (i + 1) * slot - slot * 0.15f;
                var pitch = count <= 1
                    ? 1f
                    : Mathf.Lerp(1f, Mathf.Max(1f, def.hitPitchMax), i / (float)(count - 1));
                blades[i] = new Blade
                {
                    Born = Mathf.Max(0f, hitAt - travel),
                    HitAt = hitAt,
                    From = new Vector2(i % 2 == 0 ? -780f : 780f, Mathf.Lerp(-220f, 260f, (i % 5) / 4f)),
                    To = target,
                    Pitch = pitch,
                    Rect = CreateBlade(view.BladeRoot, labels[i], def)
                };
                blades[i].Rect.gameObject.SetActive(false);
            }

            var reveal = Mathf.Min(0.18f, seconds * 0.12f);
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var cover = 1f - Mathf.Clamp01(elapsed / reveal);
                SetBlack(view, cover);
                view.Cutins.alpha = cover;
                foreach (var blade in blades)
                {
                    if (elapsed < blade.Born)
                        continue;
                    if (!blade.Rect.gameObject.activeSelf)
                        blade.Rect.gameObject.SetActive(true);
                    var t = Mathf.Clamp01((elapsed - blade.Born) / Mathf.Max(0.01f, blade.HitAt - blade.Born));
                    var eased = EaseIn(t);
                    blade.Rect.anchoredPosition = Vector2.Lerp(blade.From, blade.To, eased);
                    var delta = blade.To - blade.From;
                    blade.Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    if (!blade.Struck && elapsed >= blade.HitAt)
                    {
                        blade.Struck = true;
                        PlaySe(se, def.hitClip, def.hitVolume, blade.Pitch);
                        onBladeHit?.Invoke();
                        blade.Rect.gameObject.SetActive(false);
                    }
                }

                yield return null;
            }

            view.Cutins.alpha = 0f;
            view.Cutins.gameObject.SetActive(false);
            SetBlack(view, 0f);
        }

        static IEnumerator PhaseTotal(
            View view,
            JackpotPresentationDefinition def,
            AudioSource se,
            int damage,
            Action onDamageLanded)
        {
            var seconds = Mathf.Max(0.05f, def.totalSeconds);
            var countSeconds = seconds * Mathf.Clamp(def.totalCountPortion, 0.45f, 0.9f);
            view.Damage.gameObject.SetActive(true);
            view.Damage.text = "0";
            view.Damage.rectTransform.localScale = Vector3.one;
            view.Damage.rectTransform.localEulerAngles = Vector3.zero;
            var elapsed = 0f;
            var stamped = false;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBlack(view, Mathf.Lerp(0f, 0.55f, Mathf.Clamp01(elapsed / 0.08f)));
                if (elapsed < countSeconds)
                {
                    var t = EaseOut(Mathf.Clamp01(elapsed / countSeconds));
                    var shown = Mathf.RoundToInt(Mathf.Lerp(0f, damage, t));
                    view.Damage.text = shown.ToString();
                    view.Damage.rectTransform.localScale = Vector3.one;
                }
                else if (!stamped)
                {
                    stamped = true;
                    view.Damage.text = damage.ToString();
                    PlaySe(se, def.totalClip, def.totalVolume, 1f);
                    onDamageLanded?.Invoke();
                }

                if (stamped)
                {
                    var stampT = Mathf.Clamp01((elapsed - countSeconds) / Mathf.Max(0.05f, seconds - countSeconds));
                    var squeeze = Mathf.Sin(Mathf.Clamp01(stampT / 0.42f) * Mathf.PI);
                    view.Damage.rectTransform.localScale = Vector3.one * (1f - squeeze * 0.14f);
                    var shake = Mathf.Sin(stampT * Mathf.PI * 3f) * 5f * (1f - stampT);
                    view.Damage.rectTransform.localEulerAngles = new Vector3(0f, 0f, shake);
                }

                yield return null;
            }
        }

        static RectTransform CreateBlade(RectTransform parent, string word, JackpotPresentationDefinition def)
        {
            var rt = CreateRect(parent, "Blade", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 64f);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = def.lineSprite;
            image.color = def.bladeColor;
            image.raycastTarget = false;
            BattleUITheme.EnsureSprite(image);
            var text = CreateText(rt, "Word", BattleUITheme.Font, 28, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.fontSize = 32;
            text.text = word;
            text.raycastTarget = false;
            return rt;
        }

        static Vector2 EnemyLocalPoint(RectTransform area, RectTransform enemy)
        {
            if (enemy == null)
                return new Vector2(0f, 40f);
            var screen = RectTransformUtility.WorldToScreenPoint(null, enemy.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            return local;
        }

        static RectTransform CreateBeam(
            RectTransform parent,
            string name,
            bool fromLeft,
            Sprite sprite,
            Color color,
            float thickness,
            Material additive)
        {
            var anchor = fromLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            var rt = CreateRect(parent, name, anchor, anchor);
            rt.pivot = fromLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, thickness);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            BattleUITheme.EnsureSprite(image);
            ApplyJackpotGlowImage(image, additive);
            return rt;
        }

        static RectTransform CreateFlare(RectTransform parent, Sprite sprite, Color color, Material additive)
        {
            var rt = CreateRect(parent, "Flare", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            BattleUITheme.EnsureSprite(image);
            ApplyJackpotGlowImage(image, additive);
            return rt;
        }

        static void ApplyJackpotGlowImage(Image image, Material additive)
        {
            if (image == null)
                return;
            image.type = Image.Type.Simple;
            if (additive != null)
                image.material = additive;
        }

        static CanvasGroup CreateCutins(RectTransform parent, JackpotPresentationDefinition def, out RectTransform[] bands)
        {
            var root = CreateRect(parent, "Cutins", Vector2.zero, Vector2.one);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            bands = new RectTransform[3];
            var heights = new[] { 170f, 0f, -170f };
            for (var i = 0; i < 3; i++)
            {
                var band = CreateRect(root, $"Band{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                band.pivot = new Vector2(0.5f, 0.5f);
                band.sizeDelta = new Vector2(2400f, def.bandHeight);
                band.anchoredPosition = new Vector2(i % 2 == 0 ? -1500f : 1500f, heights[i]);
                band.localEulerAngles = new Vector3(0f, 0f, def.bandAngle);
                var image = band.gameObject.AddComponent<Image>();
                image.color = def.bandColor;
                image.raycastTarget = false;
                BattleUITheme.EnsureSprite(image);
                var face = CreateRect(band, "Face", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                face.sizeDelta = new Vector2(128f, def.bandHeight * 0.92f);
                var faceImage = face.gameObject.AddComponent<Image>();
                faceImage.raycastTarget = false;
                var sprite = def.faceSprites != null && i < def.faceSprites.Length ? def.faceSprites[i] : null;
                if (sprite != null)
                {
                    faceImage.sprite = sprite;
                    faceImage.color = Color.white;
                    faceImage.preserveAspect = true;
                }
                else
                {
                    faceImage.color = def.placeholderShadow;
                    BattleUITheme.EnsureSprite(faceImage);
                    var head = CreateRect(face, "Head", new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f));
                    head.sizeDelta = new Vector2(54f, 54f);
                    var headImage = head.gameObject.AddComponent<Image>();
                    headImage.color = def.placeholderShadow * 0.85f;
                    headImage.raycastTarget = false;
                    BattleUITheme.EnsureSprite(headImage);
                }

                bands[i] = band;
            }

            return group;
        }

        static Material _cachedAdditiveMaterial;

        static Material ResolveAdditive(JackpotPresentationDefinition def)
        {
            if (def != null && def.additiveMaterial != null)
                return def.additiveMaterial;
            if (_cachedAdditiveMaterial != null)
                return _cachedAdditiveMaterial;
            _cachedAdditiveMaterial = Resources.Load<Material>("Battle/JackpotUiAdditive");
            if (_cachedAdditiveMaterial != null)
                return _cachedAdditiveMaterial;
            var shader = Shader.Find("TypingRoguelike/UIAdditive");
            if (shader == null)
                return null;
            _cachedAdditiveMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _cachedAdditiveMaterial;
        }

        static void PlaySe(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (source == null)
                return;
            if (clip == null)
                clip = FallbackTone(pitch);
            source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        static AudioClip _tone;

        static AudioClip FallbackTone(float pitch)
        {
            if (_tone != null)
                return _tone;
            const int rate = 44100;
            var count = Mathf.RoundToInt(rate * 0.08f);
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)rate;
                var envelope = Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((count - i) / (rate * 0.03f));
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * envelope * 0.3f;
            }

            _tone = AudioClip.Create("JackpotFallback", count, 1, rate, false);
            _tone.SetData(data, 0);
            _ = pitch;
            return _tone;
        }

        static void SetBeamWidth(RectTransform beam, float width)
        {
            var size = beam.sizeDelta;
            size.x = width;
            beam.sizeDelta = size;
        }

        static void FadeBeam(RectTransform beam, Color color, float alpha)
        {
            var image = beam.GetComponent<Image>();
            if (image == null)
                return;
            color.a *= alpha;
            image.color = color;
        }

        static void SetBlack(View view, float alpha)
        {
            var color = view.Black.color;
            color.a = Mathf.Clamp01(alpha);
            view.Black.color = color;
        }

        static float EaseIn(float t) => t * t;
        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        static Image CreateImage(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var rt = CreateRect(parent, name, min, max);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            BattleUITheme.EnsureSprite(image);
            return image;
        }

        static Text CreateText(RectTransform parent, string name, Font font, int size, Color color)
        {
            var rt = CreateRect(parent, name, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f));
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font != null ? font : BattleUITheme.FontDisplay;
            text.fontSize = Mathf.Max(16, size);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static RectTransform CreateRect(RectTransform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
