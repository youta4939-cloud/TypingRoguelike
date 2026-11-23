using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>燭台ストック、HP、狂気値。座標は layout_stockpanel_v4_4.json。</summary>
    public sealed class StockPanel : MonoBehaviour
    {
        const string Root = "Battle/StockPanel/";
        const int Capacity = 5;

        public int StockCount => _count;
        public bool IsLowHP => _hpMax > 0 && _hp >= 0 && _hp / (float)_hpMax <= 0.25f;

        public FlameRoute Route
        {
            get => _route;
            set => _route = value == FlameRoute.Shader && FlameShaderDriver.Supported
                ? FlameRoute.Shader
                : FlameRoute.Sheet;
        }

        StockLayout _layout;
        RectTransform _overlay;
        StockSlot[] _slots;
        readonly GemColor[] _colors = new GemColor[Capacity];
        readonly Dictionary<GemColor, Sprite[]> _frames = new Dictionary<GemColor, Sprite[]>();
        RingGauge _hpGauge;
        RingGauge _madGauge;
        StatReadout _hpText;
        StatReadout _madText;
        StockPanelFX _fx;
        Image _projectile;
        Image _projectileHalo;
        Image _landFlash;
        Image _landRing;
        readonly Image[] _trails = new Image[5];
        readonly Image[] _embers = new Image[Capacity];
        int _count;
        int _flying = -1;
        int _token;
        int _hp = -1;
        int _hpMax = 1;
        int _mad = -1;
        int _madMax = 1;
        int _madShown;
        FlameRoute _route = FlameRoute.Sheet;
        Coroutine _chip;
        Coroutine _madRoutine;
        Image _vignette;
        bool _missing;

        public static StockPanel Create(RectTransform battleRoot, TMP_FontAsset serif, TMP_FontAsset sans)
        {
            var panelGo = new GameObject("StockPanel", typeof(RectTransform));
            var panel = panelGo.GetComponent<RectTransform>();
            panel.SetParent(battleRoot, false);
            var view = panelGo.AddComponent<StockPanel>();

            var overlayGo = new GameObject("StockPanelOverlayUnderCards", typeof(RectTransform));
            var overlay = overlayGo.GetComponent<RectTransform>();
            overlay.SetParent(battleRoot, false);
            view.Build(panel, overlay, serif, sans);
            return view;
        }

        public void SetHP(int current, int max, bool animate = true)
        {
            current = Mathf.Max(0, current);
            max = Mathf.Max(1, max);
            if (_hp < 0 || !animate)
            {
                _hp = current;
                _hpMax = max;
                if (_hpGauge != null)
                {
                    _hpGauge.Amount = Ratio(current, max);
                    _hpGauge.SetChip(0f, false);
                }
                RefreshFit();
                return;
            }

            if (current < _hp)
            {
                float old = Ratio(_hp, _hpMax);
                _hp = current;
                _hpMax = max;
                float next = Ratio(current, max);
                _hpGauge.Amount = next;
                if (_chip != null)
                    StopCoroutine(_chip);
                _chip = StartCoroutine(ChipRoutine(old, next));
                _hpText.PlayPunch(0.18f, false);
            }
            else
            {
                _hp = current;
                _hpMax = max;
                _hpGauge.Amount = Ratio(current, max);
            }
            RefreshFit();
        }

        public void SetMadness(int current, int max, bool animate = true)
        {
            current = Mathf.Max(0, current);
            max = Mathf.Max(1, max);
            if (current == _mad && max == _madMax)
                return;
            int previous = _mad < 0 ? current : _mad;
            _mad = current;
            _madMax = max;
            if (!animate || current <= previous)
            {
                if (_madRoutine != null)
                    StopCoroutine(_madRoutine);
                _madShown = current;
                _madGauge.Amount = Ratio(current, max);
                RefreshFit();
                return;
            }

            if (_madRoutine != null)
                StopCoroutine(_madRoutine);
            _madRoutine = StartCoroutine(MadnessRoutine(previous, current, max));
        }

        public bool AddStock(GemColor color, RectTransform fromCard = null)
        {
            if (_count >= Capacity)
                return false;
            int index = _count;
            _colors[index] = color;
            _count++;
            StartCoroutine(Ignite(index, color, fromCard, _token));
            return true;
        }

        public void ConsumeStock(int count)
        {
            if (count <= 0 || _count <= 0)
                return;
            count = Mathf.Min(count, _count);
            StartCoroutine(ConsumeRoutine(count, _token));
        }

        public void ClearStock()
        {
            _token++;
            _count = 0;
            _flying = -1;
            HideFlight();
            for (int i = 0; i < _slots.Length; i++)
                ResetSlot(_slots[i]);
            ApplyLogical();
        }

        public void PlayHit(int damage)
        {
            _ = damage;
            _fx.PlayHit();
            _hpText.PlayPunch(0.18f, false);
            if (_vignette != null)
                _vignette.rectTransform.SetAsLastSibling();
        }

        public void Sync(IReadOnlyList<ElementColor> orbs, RectTransform fromCard, bool animate)
        {
            int n = orbs != null ? Mathf.Min(Capacity, orbs.Count) : 0;
            var desired = new GemColor[n];
            for (int i = 0; i < n; i++)
                desired[i] = GemPalette.FromElement(orbs[i]);
            if (Same(desired))
                return;
            if (!animate)
            {
                SetImmediate(desired);
                return;
            }
            if (n == _count + 1 && Prefix(desired, _count))
            {
                AddStock(desired[n - 1], fromCard);
                return;
            }
            if (_count == Capacity && n == Capacity && Shifted(desired))
            {
                ConsumeStock(1);
                AddStock(desired[Capacity - 1], fromCard);
                return;
            }
            SetImmediate(desired);
        }

        [ContextMenu("v4.4 参照タイムライン")]
        public void PlayReferenceTimeline()
        {
            if (!Application.isPlaying)
                return;
            _token++;
            StopAllCoroutines();
            _chip = null;
            _madRoutine = null;
            HideFlight();
            SetImmediate(new[] { GemColor.Y, GemColor.G, GemColor.B });
            SetHP(72, 100, false);
            SetMadness(28, 100, false);
            StartCoroutine(ReferenceRoutine());
        }

        void Build(RectTransform panel, RectTransform overlay, TMP_FontAsset serif, TMP_FontAsset sans)
        {
            FlameShaderDriver.Warmup();
            _layout = StockLayout.Load();
            _overlay = overlay;
            StretchTopLeft(panel, _layout.PanelPos, _layout.PanelSize);
            StretchTopLeft(overlay, Vector2.zero, new Vector2(1920f, 1080f));

            var sprites = new StockSprites(Warn);
            FullImage(panel, "Base", sprites.Base);
            var slotsRoot = NewRect(panel, "Slots");
            _slots = new StockSlot[Capacity];
            for (int i = 0; i < Capacity; i++)
                _slots[i] = BuildSlot(slotsRoot, _layout.Slots[i], sprites, i);
            FullImage(panel, "Fronts", sprites.Fronts);
            for (int i = 0; i < Capacity; i++)
                _slots[i].Tint = CenterImage(panel, "Tint" + i, sprites.BowlTint, _layout.Slots[i].Tint);

            BuildMedallion(panel, "MedallionHP", _layout.Hp, sprites.Heart, sprites, GemPalette.Hex("FF5A50"), out _hpGauge);
            BuildMedallion(panel, "MedallionMad", _layout.Madness, sprites.Brain, sprites, GemPalette.Hex("C878FF"), out _madGauge);

            var hit = Mask(panel, "HpFlash", sprites.HpFlash, _layout.HpFlash, GemPalette.Hex("FF3228"));
            var low = Mask(panel, "LowHp", sprites.LowHp, _layout.LowHp, GemPalette.Hex("FF3228"));
            var mad = Mask(panel, "MadGlow", sprites.MadGlow, _layout.MadGlow, GemPalette.Hex("AA50FF"));
            _vignette = Vignette(panel.parent as RectTransform);

            var texts = NewRect(panel, "Texts");
            _hpText = texts.gameObject.AddComponent<StatReadout>();
            _hpText.Build(texts, sans, serif, _layout.HpPlate, "HP",
                GemPalette.Hex("ECCE96"), Color.white, GemPalette.Hex("FFC4BA"), GemPalette.Hex("C8C4CC"), GemPalette.Hex("FF786E"));
            _madText = texts.gameObject.AddComponent<StatReadout>();
            _madText.Build(texts, sans, serif, _layout.MadPlate, "狂気",
                GemPalette.Hex("C4A0EC"), GemPalette.Hex("E2CEFF"), GemPalette.Hex("E2CEFF"), GemPalette.Hex("B0A0C4"), GemPalette.Hex("F0AAFF"));

            _fx = gameObject.AddComponent<StockPanelFX>();
            _fx.Bind(hit, low, mad, _vignette);

            for (int i = 0; i < Capacity; i++)
            {
                _embers[i] = CenterImage(overlay, "Ember" + i, sprites.Particle, Vector2.zero);
                _embers[i].rectTransform.sizeDelta = new Vector2(4.4f, 4.4f);
                _embers[i].enabled = false;
            }
            _projectileHalo = CenterImage(overlay, "ProjectileHalo", sprites.HaloR, Vector2.zero);
            _projectile = FlameImage(overlay, "Projectile", Vector2.zero);
            _landFlash = CenterImage(overlay, "IgnitionFlash", sprites.Flash, Vector2.zero);
            _landRing = CenterImage(overlay, "IgnitionRing", sprites.Ring, Vector2.zero);
            for (int i = 0; i < _trails.Length; i++)
            {
                _trails[i] = CenterImage(overlay, "Trail" + i, sprites.Trail, Vector2.zero);
                _trails[i].enabled = false;
            }
            _projectile.enabled = false;
            _projectileHalo.enabled = false;
            _landFlash.enabled = false;
            _landRing.enabled = false;
            _lumA = sprites.LumA;
            _lumB = sprites.LumB;
            foreach (GemColor color in System.Enum.GetValues(typeof(GemColor)))
                _ramps[color] = sprites.Ramp(color);
            _frames[GemColor.R] = sprites.Frames(GemColor.R);
            _frames[GemColor.Y] = sprites.Frames(GemColor.Y);
            _frames[GemColor.G] = sprites.Frames(GemColor.G);
            _frames[GemColor.B] = sprites.Frames(GemColor.B);
            _frames[GemColor.P] = sprites.Frames(GemColor.P);
            _haloSprites = sprites.Halos;
            ApplyLogical();
            RefreshFit();
        }

        Dictionary<GemColor, Sprite> _haloSprites;
        Texture _lumA;
        Texture _lumB;
        readonly Dictionary<GemColor, Texture> _ramps = new Dictionary<GemColor, Texture>();

        void Update()
        {
            if (_slots == null)
                return;
            float time = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            _fx.Tick(time, IsLowHP);
            _hpGauge.Flash = _fx.HitFlash;
            _madGauge.Flash = _fx.MadnessFlash;
            _hpGauge.Tick();
            _madGauge.Tick();
            _hpText.Tick(dt);
            _madText.Tick(dt);

            int next = _flying >= 0 ? _flying : (_count < Capacity ? _count : -1);
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                bool has = i < _count && i != _flying;
                slot.Lit = has;
                slot.Next = i == next;
                if (has)
                    slot.Gem = _colors[i];
                if (Route == FlameRoute.Shader && slot.Lit)
                    EnsureShader(slot);
                Sprite[] frames = null;
                if (has)
                    _frames.TryGetValue(slot.Gem, out frames);
                slot.Tick(time, Route, frames);
                UpdateEmber(i, time);
            }
        }

        void EnsureShader(StockSlot slot)
        {
            if (slot.Shader == null || slot.ShaderReady && slot.ShaderBound == slot.Gem)
                return;
            if (_lumA == null || !_ramps.TryGetValue(slot.Gem, out var ramp))
                return;
            slot.Shader.Bind(slot.ShaderFlame, _lumA, _lumB, ramp, slot.Offset);
            if (slot.ShaderHalo != null && _haloSprites != null && _haloSprites.TryGetValue(slot.Gem, out var halo))
                slot.ShaderHalo.sprite = halo;
            slot.ShaderBound = slot.Gem;
            slot.ShaderReady = true;
        }

        void UpdateEmber(int index, float time)
        {
            var image = _embers[index];
            var slot = _slots[index];
            if (image == null)
                return;
            float phase = Mathf.Repeat(time + slot.Offset * 1.7f, 2.1f);
            float q = phase / 1.6f;
            bool on = slot.Lit && q > 0f && q < 1f;
            image.enabled = on;
            if (!on)
                return;
            float x = slot.BallAbs.x + 8f * Mathf.Sin(5f * q + index) + (index - 2f) * 2f;
            float yDown = slot.BallAbs.y - 28.5f - 21.6f * q;
            image.rectTransform.anchoredPosition = new Vector2(x, -yDown);
            var color = GemPalette.Light(slot.Gem);
            color.a = 0.9f * Mathf.Sin(Mathf.PI * q);
            image.color = color;
        }

        IEnumerator Ignite(int index, GemColor color, RectTransform fromCard, int token)
        {
            _flying = index;
            if (fromCard != null)
                StartCoroutine(CardFlash(fromCard, GemPalette.Light(color)));
            float travel = 0.3f;
            float elapsed = 0f;
            Vector2 start = fromCard != null ? CenterOf(fromCard) : new Vector2(1156f, -690f);
            Vector2 ball = new Vector2(_slots[index].BallAbs.x, -_slots[index].BallAbs.y);
            _projectile.enabled = true;
            _projectileHalo.enabled = _haloSprites != null && _haloSprites.ContainsKey(color);
            if (_projectileHalo.enabled)
                _projectileHalo.sprite = _haloSprites[color];
            while (elapsed < travel)
            {
                if (token != _token)
                    yield break;
                elapsed += Time.unscaledDeltaTime;
                float q = Mathf.Clamp01(elapsed / travel);
                float e = StockPanelTween.Smooth(q);
                float arc = Mathf.Sin(Mathf.PI * e);
                var pos = start + (ball - start) * e + new Vector2(34f * arc, 26f * arc);
                float scale = 0.55f + 0.45f * e;
                PlaceFlame(_projectile, pos, scale, color, 1f);
                if (_projectileHalo.enabled)
                {
                    _projectileHalo.rectTransform.anchoredPosition = pos;
                    _projectileHalo.rectTransform.localScale = new Vector3(scale, scale, 1f);
                    var halo = Color.white;
                    halo.a = 0.6f * 1.6f;
                    _projectileHalo.color = halo;
                }
                PlaceTrail(start, ball, e, color);
                yield return null;
            }

            HideFlight();
            _flying = -1;
            var slot = _slots[index];
            slot.Lit = true;
            slot.Gem = color;
            float land = 0.45f;
            elapsed = 0f;
            while (elapsed < land)
            {
                if (token != _token)
                    yield break;
                elapsed += Time.unscaledDeltaTime;
                float q = Mathf.Clamp01(elapsed / land);
                float e = 1f - Mathf.Pow(1f - q, 3f);
                slot.Scale = 1f + 0.14f * (1f - e) * Mathf.Cos(5f * q);
                slot.Boost = 1f + 0.9f * (1f - e);
                if (elapsed < 0.35f)
                {
                    float fq = elapsed / 0.35f;
                    float radius = Mathf.Lerp(30f, 40f, fq);
                    ShowBurst(_landFlash, ball, radius * 2f, GemPalette.Hex("FFECFF"), 0.7f * (1f - fq) * (1f - fq));
                }
                else if (_landFlash != null)
                    _landFlash.enabled = false;
                float ringRadius = 22f + 34f * e;
                ShowBurst(_landRing, ball, ringRadius * 2f, GemPalette.Light(color), 0.78f * (1f - q));
                yield return null;
            }

            slot.Scale = 1f;
            slot.Boost = 1f;
            if (_landFlash != null)
                _landFlash.enabled = false;
            if (_landRing != null)
                _landRing.enabled = false;
        }

        IEnumerator ConsumeRoutine(int count, int token)
        {
            var ghosts = new Image[count];
            var origin = new Vector2[Capacity];
            for (int i = 0; i < Capacity; i++)
                origin[i] = _slots[i].Ball;
            for (int i = 0; i < count; i++)
                ghosts[i] = Ghost(_slots[i]);
            for (int i = 0; i < _count - count; i++)
                _colors[i] = _colors[i + count];
            _count -= count;
            int moving = _count;
            ApplyLogical();
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                if (token != _token)
                {
                    DestroyGhosts(ghosts);
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / 0.2f);
                for (int i = 0; i < ghosts.Length; i++)
                {
                    if (ghosts[i] == null)
                        continue;
                    var color = ghosts[i].color;
                    color.a = 1f - u;
                    ghosts[i].color = color;
                }
                for (int i = 0; i < moving && i < _slots.Length; i++)
                    _slots[i].Slide = Vector2.Lerp(origin[i + count] - _slots[i].Ball, Vector2.zero, u);
                yield return null;
            }

            DestroyGhosts(ghosts);
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].Slide = Vector2.zero;
        }

        IEnumerator ChipRoutine(float from, float to)
        {
            _hpGauge.SetChip(from, true);
            yield return new WaitForSecondsRealtime(0.4f);
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                float e = StockPanelTween.Smooth(elapsed / 0.4f);
                _hpGauge.SetChip(Mathf.Lerp(from, to, e), true);
                yield return null;
            }
            _hpGauge.SetChip(0f, false);
            _chip = null;
        }

        IEnumerator MadnessRoutine(int from, int to, int max)
        {
            _fx.PlayMadness();
            _madText.PlayPunch(0.15f, true);
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                float e = StockPanelTween.Smooth(elapsed / 0.4f);
                _madShown = Mathf.RoundToInt(Mathf.Lerp(from, to, e));
                _madGauge.Amount = Ratio(_madShown, max);
                RefreshFit();
                yield return null;
            }
            _madShown = to;
            _madGauge.Amount = Ratio(to, max);
            RefreshFit();
            _madRoutine = null;
        }

        IEnumerator CardFlash(RectTransform card, Color color)
        {
            var go = new GameObject("CardFlash", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(card, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var flash = go.GetComponent<Image>();
            flash.sprite = BattleUISprites.Solid;
            flash.raycastTarget = false;
            flash.color = new Color(color.r, color.g, color.b, 150f / 255f);
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                var tint = flash.color;
                tint.a = Mathf.Lerp(150f / 255f, 0f, elapsed / 0.25f);
                flash.color = tint;
                yield return null;
            }
            Destroy(go);
        }

        IEnumerator ReferenceRoutine()
        {
            yield return new WaitForSecondsRealtime(1f);
            AddStock(GemColor.R, null);
            yield return new WaitForSecondsRealtime(1.6f);
            SetHP(60, 100, true);
            PlayHit(12);
            yield return new WaitForSecondsRealtime(1f);
            SetMadness(31, 100, true);
        }

        void RefreshFit()
        {
            if (_hpText == null || _madText == null || _hp < 0)
                return;
            string hpNum = _hp.ToString();
            string hpMax = "/" + _hpMax;
            string madNum = _madShown.ToString();
            string madMax = "/" + _madMax;
            float needHp = _hpText.Measure(hpNum, hpMax);
            float needMad = _madText.Measure(madNum, madMax);
            float fit = Mathf.Min(1f, 139.9f / Mathf.Max(1f, needHp), 139.9f / Mathf.Max(1f, needMad));
            _hpText.Apply(hpNum, hpMax, fit, IsLowHP);
            _madText.Apply(madNum, madMax, fit, false);
        }

        void ApplyLogical()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                bool has = i < _count;
                _slots[i].Lit = has && i != _flying;
                _slots[i].Next = !has && i == (_flying >= 0 ? _flying : _count);
                if (has)
                    _slots[i].Gem = _colors[i];
                if (!has)
                {
                    _slots[i].Alpha = 1f;
                    _slots[i].Scale = 1f;
                    _slots[i].Boost = 1f;
                }
            }
        }

        void SetImmediate(GemColor[] colors)
        {
            _token++;
            _count = colors.Length;
            _flying = -1;
            HideFlight();
            for (int i = 0; i < _count; i++)
                _colors[i] = colors[i];
            for (int i = 0; i < _slots.Length; i++)
                ResetSlot(_slots[i]);
            ApplyLogical();
        }

        static void ResetSlot(StockSlot slot)
        {
            slot.Alpha = 1f;
            slot.Scale = 1f;
            slot.Boost = 1f;
            slot.Slide = Vector2.zero;
            slot.Lit = false;
            slot.ShaderReady = false;
        }

        bool Same(GemColor[] desired)
        {
            if (desired.Length != _count)
                return false;
            for (int i = 0; i < _count; i++)
            {
                if (desired[i] != _colors[i])
                    return false;
            }
            return true;
        }

        bool Prefix(GemColor[] desired, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (desired[i] != _colors[i])
                    return false;
            }
            return true;
        }

        bool Shifted(GemColor[] desired)
        {
            for (int i = 0; i < Capacity - 1; i++)
            {
                if (desired[i] != _colors[i + 1])
                    return false;
            }
            return true;
        }

        void PlaceFlame(Image image, Vector2 pos, float scale, GemColor color, float alpha)
        {
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (_frames.TryGetValue(color, out var frames) && frames.Length > 0)
            {
                int frame = Mathf.FloorToInt(Mathf.Repeat(Time.unscaledTime, FlameSheetPlayer.Period) * FlameSheetPlayer.Fps) % frames.Length;
                image.sprite = frames[frame];
            }
            var tint = Color.white;
            tint.a = alpha;
            image.color = tint;
        }

        void PlaceTrail(Vector2 start, Vector2 ball, float e, GemColor color)
        {
            var tint = GemPalette.Mid(color);
            for (int j = 1; j <= 5; j++)
            {
                var image = _trails[j - 1];
                float te = e - j * 0.06f;
                if (te <= 0f)
                {
                    image.enabled = false;
                    continue;
                }
                float arc = Mathf.Sin(Mathf.PI * te);
                var pos = start + (ball - start) * te + new Vector2(34f * arc, 26f * arc);
                float radius = 9f - j;
                image.enabled = true;
                image.rectTransform.anchoredPosition = pos;
                image.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
                tint.a = 0.5f * (1f - j / 6f);
                image.color = tint;
            }
        }

        static void ShowBurst(Image image, Vector2 pos, float diameter, Color color, float alpha)
        {
            if (image == null)
                return;
            image.enabled = alpha > 0.01f;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            color.a = alpha;
            image.color = color;
        }

        void HideFlight()
        {
            if (_projectile != null)
                _projectile.enabled = false;
            if (_projectileHalo != null)
                _projectileHalo.enabled = false;
            for (int i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] != null)
                    _trails[i].enabled = false;
            }
        }

        Image Ghost(StockSlot slot)
        {
            var image = FlameImage(slot.transform.parent, "FadeFlame", slot.Ball);
            image.sprite = slot.Flame != null ? slot.Flame.sprite : null;
            image.color = Color.white;
            return image;
        }

        static void DestroyGhosts(Image[] ghosts)
        {
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] != null)
                    Destroy(ghosts[i].gameObject);
            }
        }

        Vector2 CenterOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var world = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            var local = _overlay.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        StockSlot BuildSlot(RectTransform parent, StockLayout.SlotAnchor anchor, StockSprites sprites, int index)
        {
            var root = NewRect(parent, "Slot" + index);
            var slot = root.gameObject.AddComponent<StockSlot>();
            slot.Ball = anchor.Ball;
            slot.MouthPos = anchor.Mouth;
            slot.TintPos = anchor.Tint;
            slot.BallAbs = anchor.BallAbs;
            slot.Offset = anchor.Offset;
            slot.Alpha = 1f;
            slot.Scale = 1f;
            slot.Boost = 1f;
            slot.Cold = CenterImage(root, "EmberCold", sprites.Cold, anchor.Mouth);
            slot.Halo = CenterImage(root, "EmberHalo", sprites.Halo, anchor.Mouth + new Vector2(0f, 10f));
            slot.Mouth = CenterImage(root, "EmberMouth", sprites.Mouth, anchor.Mouth);
            slot.Smoke = ImageAt(root, "EmberSmoke", sprites.Smoke, anchor.Mouth + new Vector2(0f, 4f), new Vector2(0.5f, 0f));
            slot.ShaderHalo = CenterImage(root, "FlameGlowHalo", sprites.HaloR, anchor.Ball);
            slot.ShaderFlame = RawAt(root, "FlameShader", anchor.Ball);
            slot.Shader = slot.ShaderFlame.gameObject.AddComponent<FlameShaderDriver>();
            slot.Flame = FlameImage(root, "Flame", anchor.Ball);
            slot.Sheet = slot.Flame.gameObject.AddComponent<FlameSheetPlayer>();
            if (FlameShaderDriver.Supported)
                slot.Shader.Bind(slot.ShaderFlame, sprites.LumA, sprites.LumB, sprites.Ramp(GemColor.R), anchor.Offset);
            slot.ShaderHalo.enabled = false;
            slot.ShaderFlame.enabled = false;
            return slot;
        }

        void BuildMedallion(RectTransform parent, string name, StockLayout.MedallionAnchor anchor, Sprite icon, StockSprites sprites, Color color, out RingGauge gauge)
        {
            var host = NewRect(parent, name);
            var offset = anchor.IconOffset;
            var iconPos = new Vector2(anchor.Centre.x - offset.x, anchor.Centre.y + offset.y);
            CenterImage(host, "Icon", icon, iconPos);
            gauge = host.gameObject.AddComponent<RingGauge>();
            gauge.Build(host, anchor.Centre, sprites.Track, sprites.Fill, sprites.Glow, color);
        }

        Image Vignette(RectTransform battleRoot)
        {
            var sprite = Resources.Load<Sprite>("Battle/E5b/parts/fx/edge_vignette_1920");
            if (sprite == null || battleRoot == null)
                return null;
            var image = FullImage(battleRoot, "HitVignette", sprite);
            image.rectTransform.sizeDelta = new Vector2(1920f, 1080f);
            image.color = new Color(1f, 0.196f, 0.157f, 0f);
            image.enabled = false;
            return image;
        }

        void Warn(string path)
        {
            if (_missing)
                return;
            _missing = true;
            Debug.LogWarning("StockPanel のスプライトを読めませんでした: " + path);
        }

        static float Ratio(int current, int max) => max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;

        static void StretchTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            StretchTopLeft(rt, Vector2.zero, Vector2.zero);
            return rt;
        }

        static Image FullImage(RectTransform parent, string name, Sprite sprite)
        {
            var rt = NewRect(parent, name);
            rt.sizeDelta = parent.sizeDelta;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        static Image CenterImage(RectTransform parent, string name, Sprite sprite, Vector2 pos)
        {
            var size = sprite != null
                ? new Vector2(sprite.rect.width, sprite.rect.height) * 0.5f
                : new Vector2(32f, 32f);
            return ImageAt(parent, name, sprite, pos, new Vector2(0.5f, 0.5f), size);
        }

        static Image FlameImage(Transform parent, string name, Vector2 pos)
        {
            return ImageAt(parent, name, null, pos, new Vector2(0.5f, 0.42f), new Vector2(86f, 86f));
        }

        static Image ImageAt(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        static Image ImageAt(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 pivot)
        {
            var size = sprite != null
                ? new Vector2(sprite.rect.width, sprite.rect.height) * 0.5f
                : new Vector2(32f, 32f);
            return ImageAt((Transform)parent, name, sprite, pos, pivot, size);
        }

        static RawImage RawAt(RectTransform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.42f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(86f, 86f);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        static Image Mask(RectTransform parent, string name, Sprite sprite, StockLayout.MaskAnchor anchor, Color color)
        {
            var image = ImageAt(parent, name, sprite, anchor.Pos, new Vector2(0f, 1f), anchor.Size);
            color.a = 0f;
            image.color = color;
            image.enabled = false;
            return image;
        }

        sealed class StockSprites
        {
            public readonly Sprite Base;
            public readonly Sprite Fronts;
            public readonly Sprite Cold;
            public readonly Sprite Mouth;
            public readonly Sprite Halo;
            public readonly Sprite Smoke;
            public readonly Sprite BowlTint;
            public readonly Sprite Particle;
            public readonly Sprite Flash;
            public readonly Sprite Ring;
            public readonly Sprite Trail;
            public readonly Sprite Heart;
            public readonly Sprite Brain;
            public readonly Sprite Track;
            public readonly Sprite Fill;
            public readonly Sprite Glow;
            public readonly Sprite HpFlash;
            public readonly Sprite LowHp;
            public readonly Sprite MadGlow;
            public readonly Sprite HaloR;
            public readonly Texture LumA;
            public readonly Texture LumB;
            public readonly Dictionary<GemColor, Sprite> Halos = new Dictionary<GemColor, Sprite>();
            readonly Dictionary<GemColor, Sprite[]> _frames = new Dictionary<GemColor, Sprite[]>();
            readonly Dictionary<GemColor, Texture> _ramps = new Dictionary<GemColor, Texture>();
            readonly System.Action<string> _warn;

            public StockSprites(System.Action<string> warn)
            {
                _warn = warn;
                Base = Load("Panel/panel_base_2x");
                Fronts = Load("Panel/panel_bowl_fronts_2x");
                Cold = Load("FX/ember_cold_2x");
                Mouth = Load("FX/ember_next_mouth_glow_2x");
                Halo = Load("FX/ember_next_halo_2x");
                Smoke = Load("FX/ember_next_smoke_2x");
                BowlTint = Load("FX/bowl_tint_glow_2x");
                Particle = Load("FX/ember_particle_2x");
                Flash = Load("FX/ignition_flash_2x");
                Ring = Load("FX/ignition_ring_2x");
                Trail = Load("FX/trail_dot_2x");
                Heart = Load("Medallion/icon_heart_2x");
                Brain = Load("Medallion/icon_brain_2x");
                Track = Load("Gauge/ring_track_2x");
                Fill = Load("Gauge/ring_fill_2x");
                Glow = Load("Gauge/ring_glow_2x");
                HpFlash = Load("FX/fx_hp_flash_mask_2x");
                LowHp = Load("FX/fx_lowhp_glow_mask_2x");
                MadGlow = Load("FX/fx_madness_glow_mask_2x");
                LumA = Resources.Load<Texture>(Root + "Flames/Shader/flame_lum_A");
                LumB = Resources.Load<Texture>(Root + "Flames/Shader/flame_lum_B");
                foreach (GemColor color in System.Enum.GetValues(typeof(GemColor)))
                {
                    string letter = GemPalette.Letter(color);
                    Halos[color] = Load("Flames/Shader/flame_" + letter + "_glowhalo_2x");
                    _ramps[color] = Resources.Load<Texture>(Root + "Flames/Shader/flame_ramp_" + letter);
                    var frames = Resources.LoadAll<Sprite>(Root + "Flames/Sheets/flame_" + letter + "_loop_9x6_2x");
                    System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                    _frames[color] = frames;
                    if (frames.Length == 0)
                        warn("Flames/Sheets/flame_" + letter + "_loop_9x6_2x");
                }
                HaloR = Halos[GemColor.R];
            }

            public Sprite[] Frames(GemColor color) => _frames.TryGetValue(color, out var frames) ? frames : System.Array.Empty<Sprite>();
            public Texture Ramp(GemColor color) => _ramps.TryGetValue(color, out var ramp) ? ramp : null;

            Sprite Load(string path)
            {
                var sprite = Resources.Load<Sprite>(Root + path);
                if (sprite == null)
                    _warn(path);
                return sprite;
            }
        }
    }
}
