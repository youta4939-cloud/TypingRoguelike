using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// 刀の娘の立ち絵。頂点係数で呼吸・ポニー・紐を変形し、味方パネルの Image として描く。
    /// </summary>
    public sealed class KatanaPortraitRig : MonoBehaviour
    {
        public const float Ppu = 100f;

        public bool baked = true;
        public bool idleDemoSkill;
        public bool routeB;
        public bool Rendering = true;
        public float Opacity = 1f;

        public event Action<float> OnHitFlash;

        public Vector2 Shake => _shake;

        const string ResourceDir = "Battle/Portraits/Katana/";
        const float StepHz = 240f;
        const int RingSize = 480;

        readonly List<Part> _parts = new List<Part>();
        readonly List<Spring> _springs = new List<Spring>();
        readonly Vector2[] _ponyRing = new Vector2[RingSize];

        Part _head;
        Image _band;
        Image _sparkle;
        Sprite _headSprite;
        Sprite _hurt;
        float[] _taus = { 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f };
        Bake[] _bake = Array.Empty<Bake>();

        Vector2 _pivot = new Vector2(620f, 1000f);
        float _panelScale = 0.62f;
        float _panelX = -126f;
        float _panelY = 122f;
        float _bladeAngle = 163.101f;
        Vector2 _bladeFrom = new Vector2(772f, 368f);
        Vector2 _bladeTo = new Vector2(614f, 416f);
        float _breathPx = 5.5f;
        float _headFollow = 0.8f;

        bool _ready;
        bool _hurtWarned;
        bool _demoArmed = true;
        float _time;
        float _skillStart = -100f;
        float _hitStart = -100f;
        float _sentFlash = -1f;
        Vector2 _shake;
        Kin _prevKin;
        bool _havePrev;

        enum FieldKind { Body, Ponytail, Head, CordL, CordR }

        public bool Build(RectTransform host)
        {
            var rig = Load("Data/portrait_rig_katana_v2");
            var meshes = Load("Data/portrait_meshes_katana_v2");
            var anim = Load("Data/portrait_anim_katana_v2");
            if (rig == null || meshes == null)
            {
                Debug.LogError("刀の立ち絵 JSON を読めませんでした。");
                return false;
            }

            ReadRig(rig);
            var parts = rig.Get("parts");
            if (parts == null)
                return false;
            for (int i = 0; i < parts.Count; i++)
            {
                var node = parts.At(i);
                var part = CreatePart(host, node, meshes);
                if (part == null)
                    continue;
                _parts.Add(part);
                if (part.Kind == FieldKind.Head)
                    _head = part;
            }

            if (_parts.Count == 0)
                return false;

            ReadSprings(rig.Get("springs"));
            ReadBake(anim);
            CreateFx(host);
            _hurt = Resources.Load<Sprite>(ResourceDir + "Sprites/head_hurt");
            Preroll();
            _ready = true;
            Apply(Kinematic(_time));
            var mask = host != null ? host.GetComponentInParent<Mask>() : null;
            if (mask != null)
                MaskUtilities.NotifyStencilStateChanged(mask);
            return true;
        }

        void LateUpdate()
        {
            if (!_ready)
                return;
            float dt = Time.unscaledDeltaTime;
            if (dt < 0f)
                dt = 0f;
            _time += dt;
            MaybeDemoSkill();
            var kin = Kinematic(_time);
            if (!baked && !routeB)
                AdvanceSprings(dt, kin);
            _prevKin = kin;
            _havePrev = true;
            if (_head != null && _hurt == null && _time - _hitStart > 0.6f && _hitStart > 0f)
                SetFace(PortraitFace.Default);
            Apply(kin);
        }

        public void PlaySkill()
        {
            _skillStart = _time;
        }

        public void PlayHit()
        {
            _hitStart = _time;
            SetFace(PortraitFace.Hurt);
        }

        public void SetFace(PortraitFace face)
        {
            if (_head == null)
                return;
            if (face == PortraitFace.Hurt)
            {
                if (_hurt == null)
                {
                    if (!_hurtWarned)
                    {
                        Debug.LogWarning("Hurt face not available for katana girl");
                        _hurtWarned = true;
                    }

                    return;
                }

                _head.Graphic.sprite = _hurt;
                return;
            }

            if (_headSprite != null)
                _head.Graphic.sprite = _headSprite;
        }

        public void Upload()
        {
            if (!_ready)
                return;
            Apply(Kinematic(_time));
        }

        void MaybeDemoSkill()
        {
            if (!idleDemoSkill || baked)
                return;
            float loop = Mathf.Repeat(_time, 8f);
            if (loop < 1f)
                _demoArmed = true;
            if (_demoArmed && loop >= 5.15f && loop < 5.25f)
            {
                _demoArmed = false;
                PlaySkill();
            }
        }

        Kin Kinematic(float time)
        {
            float breath;
            float beat;
            float theta;
            float tx;
            float ty;
            float tyHead;
            float bakedLift = 0f;
            if (baked && _bake.Length > 1)
            {
                SampleBake(time, out var frame, out _);
                breath = frame.Breath;
                beat = frame.Beat;
                theta = frame.Theta;
                tx = frame.Tx;
                ty = frame.Ty;
                tyHead = frame.TyHead;
                bakedLift = frame.Lift;
            }
            else
            {
                breath = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * time / 4f);
                beat = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * time);
                theta = 0.20f * Mathf.Sin(2f * Mathf.PI * time / 2f);
                tx = 0.6f * Mathf.Sin(2f * Mathf.PI * time / 2f - 0.35f);
                ty = -_breathPx * breath + 1.1f * beat;
                tyHead = ty + (1f - _headFollow) * _breathPx * breath;
            }

            float lift = baked && idleDemoSkill ? bakedLift : 0f;
            float skillT = time - _skillStart;
            if (skillT >= 0f && skillT < 1.2f)
                lift = Env(skillT);
            float dLift = lift - bakedLift;
            if (baked && _bake.Length > 1)
            {
                ty -= 1.6f * dLift;
                tyHead -= 1.28f * dLift;
            }
            else
            {
                ty -= 1.6f * lift;
                tyHead = ty + (1f - _headFollow) * (_breathPx * breath + 1.6f * lift);
            }

            float recoil = Recoil(time);
            theta += -0.8f * recoil;
            tx += -5f * recoil;
            return new Kin(theta, tx, ty, tyHead, breath, lift);
        }

        void Apply(Kin kin)
        {
            float hitT = _time - _hitStart;
            _shake = Vector2.zero;
            if (hitT >= 0f && hitT < 0.4f)
            {
                float s = 4f * Mathf.Exp(-hitT / 0.09f) * Mathf.Sin(2f * Mathf.PI * 22f * hitT);
                _shake = new Vector2(s, 0.6f * s);
            }

            float flash = 0f;
            if (hitT >= 0f && hitT < 0.6f)
                flash = 0.8f * Mathf.Exp(-hitT / 0.11f);
            if (Mathf.Abs(flash - _sentFlash) > 0.01f || (flash == 0f && _sentFlash > 0f))
            {
                _sentFlash = flash;
                OnHitFlash?.Invoke(flash);
            }

            var tint = Color.Lerp(Color.white, new Color(1f, 40f / 255f, 46f / 255f), flash);
            tint.a = Rendering ? Opacity : 0f;
            Vector2 pony = SpringOut("pony");
            Vector2 side = SpringOut("side");
            Vector2 cordL = SpringOut("cordL");
            Vector2 cordR = SpringOut("cordR");
            if (baked && _bake.Length > 1)
            {
                SampleBake(_time, out var frame, out float u);
                Bake next = _bake[(IndexOf(frame) + 1) % _bake.Length];
                side = Vector2.Lerp(frame.Side, next.Side, u);
                cordL = Vector2.Lerp(frame.CordL, next.CordL, u);
                cordR = Vector2.Lerp(frame.CordR, next.CordR, u);
            }

            for (int p = 0; p < _parts.Count; p++)
                Pose(_parts[p], kin, pony, side, cordL, cordR, tint);
            PlaceGlint(kin);
        }

        int IndexOf(Bake frame)
        {
            for (int i = 0; i < _bake.Length; i++)
            {
                if (_bake[i].Time == frame.Time)
                    return i;
            }

            return 0;
        }

        void Pose(Part part, Kin kin, Vector2 pony, Vector2 side, Vector2 cordL, Vector2 cordR, Color tint)
        {
            var graphic = part.Graphic;
            graphic.color = tint;
            graphic.enabled = true;
            int n = part.Rest.Length;
            float scale = _panelScale;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = part.Rest[i];
                Vector2 posed = routeB ? Rigid(part, p, kin) : Deform(part, i, p, kin, pony, side, cordL, cordR);
                float lx = (posed.x + _shake.x - part.Origin.x) * scale;
                float ly = -((posed.y + _shake.y - part.Origin.y) * scale);
                graphic.Positions[i] = new Vector2(lx, ly);
            }

            graphic.SetVerticesDirty();
        }

        Vector2 Rigid(Part part, Vector2 p, Kin kin)
        {
            return part.Kind == FieldKind.Head ? U(p, kin.Theta, kin.Tx, kin.TyHead) : U(p, kin.Theta, kin.Tx, kin.Ty);
        }

        Vector2 Deform(Part part, int i, Vector2 p, Kin kin, Vector2 pony, Vector2 side, Vector2 cordL, Vector2 cordR)
        {
            Vector2 body = U(p, kin.Theta, kin.Tx, kin.Ty);
            float wU = At(part.WU, i);
            switch (part.Kind)
            {
                case FieldKind.Head:
                    return U(p, kin.Theta, kin.Tx, kin.TyHead) + At(part.WSide, i) * side;
                case FieldKind.Ponytail:
                {
                    Vector2 delayed = baked && _bake.Length > 1
                        ? BakedPony(At(part.Tau, i))
                        : SampleRing(_time - At(part.Tau, i));
                    return p + wU * (body - p) + At(part.G, i) * delayed;
                }
                case FieldKind.CordL:
                    return p + wU * (body - p) + At(part.WCord, i) * new Vector2(cordL.x, 0.25f * cordL.y);
                case FieldKind.CordR:
                    return p + wU * (body - p) + At(part.WCord, i) * new Vector2(cordR.x, 0.25f * cordR.y);
                default:
                    return p + wU * (body - p) + kin.Breath * new Vector2(At(part.ExpandX, i), At(part.ExpandY, i));
            }
        }

        Vector2 BakedPony(float tau)
        {
            SampleBake(_time, out var a, out float u);
            int index = 0;
            for (int i = 0; i < _bake.Length; i++)
            {
                if (_bake[i].Time == a.Time)
                {
                    index = i;
                    break;
                }
            }

            var b = _bake[(index + 1) % _bake.Length];
            return Vector2.Lerp(Along(a.Pony, tau), Along(b.Pony, tau), u);
        }

        Vector2 Along(Vector2[] samples, float tau)
        {
            if (samples == null || samples.Length == 0)
                return Vector2.zero;
            if (tau <= _taus[0])
                return samples[0];
            int last = Mathf.Min(samples.Length, _taus.Length) - 1;
            if (tau >= _taus[last])
                return samples[last];
            for (int i = 0; i < last; i++)
            {
                float a = _taus[i];
                float b = _taus[i + 1];
                if (tau > b)
                    continue;
                float u = (tau - a) / Mathf.Max(0.0001f, b - a);
                return Vector2.Lerp(samples[i], samples[Mathf.Min(i + 1, samples.Length - 1)], u);
            }

            return samples[last];
        }

        Vector2 U(Vector2 p, float thetaDeg, float tx, float ty)
        {
            float rad = thetaDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            float dx = p.x - _pivot.x;
            float dy = p.y - _pivot.y;
            return new Vector2(_pivot.x + c * dx - s * dy + tx, _pivot.y + s * dx + c * dy + ty);
        }

        Vector2 ToPanel(Vector2 canvas)
        {
            float sx = (canvas.x + _shake.x) * _panelScale + _panelX;
            float sy = (canvas.y + _shake.y) * _panelScale + _panelY;
            return new Vector2(sx, -sy);
        }

        void PlaceGlint(Kin kin)
        {
            if (_band == null || _sparkle == null)
                return;
            float skillT = _time - _skillStart;
            float k = 0f;
            float u = 0f;
            if (skillT >= 0f && skillT <= 1.2f)
            {
                u = skillT / 1.2f;
                k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
            }
            else if (baked && idleDemoSkill && _bake.Length > 1)
            {
                SampleBake(_time, out var frame, out float blend);
                int index = IndexOf(frame);
                k = Mathf.Lerp(frame.GlintK, _bake[(index + 1) % _bake.Length].GlintK, blend);
                u = frame.GlintU;
            }

            bool show = k > 0.001f && Rendering && Opacity > 0.001f;
            _band.enabled = show;
            _sparkle.enabled = show;
            if (!show)
                return;

            Vector2 dir = _bladeTo - _bladeFrom;
            float length = dir.magnitude;
            Vector2 forward = length > 0.001f ? dir / length : Vector2.right;
            float dist = -10f + Mathf.Clamp01(u) * (length + 20f);
            Vector2 centre = _bladeFrom + forward * dist;
            float along = Mathf.Clamp(Vector2.Dot(centre - _bladeFrom, forward), 0f, length);
            Vector2 spark = _bladeFrom + forward * along;
            _band.rectTransform.anchoredPosition = ToPanel(U(centre, kin.Theta, kin.Tx, kin.Ty));
            _sparkle.rectTransform.anchoredPosition = ToPanel(U(spark, kin.Theta, kin.Tx, kin.Ty));
            float angle = -(_bladeAngle + kin.Theta);
            _band.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            _sparkle.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            var band = new Color(1f, 0.96f, 0.90f, 0.75f * k * Opacity);
            var star = new Color(1f, 0.957f, 0.894f, 0.8f * k * Opacity);
            _band.color = band;
            _sparkle.color = star;
        }

        void AdvanceSprings(float dt, Kin kin)
        {
            int steps = Mathf.Clamp(Mathf.RoundToInt(dt * StepHz), 1, 24);
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                float u = (s + 1f) / steps;
                var step = _havePrev ? Kin.Lerp(_prevKin, kin, u) : kin;
                for (int i = 0; i < _springs.Count; i++)
                    _springs[i].Step(this, step, _time - dt + u * dt, h);
                var pony = SpringOut("pony");
                int slot = Mod(Mathf.FloorToInt((_time - dt + u * dt) * StepHz));
                _ponyRing[slot] = pony;
            }
        }

        void Preroll()
        {
            _time = -4f;
            _havePrev = false;
            var kin = Kinematic(_time);
            const int steps = 960;
            float h = 4f / steps;
            for (int s = 0; s < steps; s++)
            {
                _time += h;
                var next = Kinematic(_time);
                if (!baked)
                {
                    for (int i = 0; i < _springs.Count; i++)
                        _springs[i].Step(this, next, _time, h);
                    _ponyRing[Mod(Mathf.FloorToInt(_time * StepHz))] = SpringOut("pony");
                }

                kin = next;
            }

            _time = 0f;
            _prevKin = kin;
            _havePrev = true;
            _skillStart = -100f;
            _hitStart = -100f;
        }

        Vector2 SampleRing(float time)
        {
            float f = time * StepHz;
            int i0 = Mathf.FloorToInt(f);
            float u = f - i0;
            return Vector2.Lerp(_ponyRing[Mod(i0)], _ponyRing[Mod(i0 + 1)], u);
        }

        Vector2 SpringOut(string name)
        {
            for (int i = 0; i < _springs.Count; i++)
            {
                if (_springs[i].Name == name)
                    return _springs[i].Output;
            }

            return Vector2.zero;
        }

        static int Mod(int value)
        {
            int m = value % RingSize;
            return m < 0 ? m + RingSize : m;
        }

        static float At(float[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
                return 0f;
            return values[index];
        }

        static float Env(float ts)
        {
            if (ts <= 0f)
                return 0f;
            if (ts < 0.30f)
                return Smooth(ts / 0.30f);
            if (ts < 0.60f)
                return 1f;
            if (ts >= 1.20f)
                return 0f;
            return 1f - Smooth((ts - 0.60f) / 0.60f);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Recoil(float time, float hitStart)
        {
            float th = time - hitStart;
            if (th < 0f || th > 1.5f)
                return 0f;
            if (th < 0.07f)
                return Smooth(th / 0.07f);
            float x = th - 0.07f;
            return Mathf.Max(-0.25f, Mathf.Exp(-6.5f * x) * Mathf.Cos(2f * Mathf.PI * 1.6f * x));
        }

        float Recoil(float time) => Recoil(time, _hitStart);

        void SampleBake(float time, out Bake frame, out float blend)
        {
            float duration = _bake.Length / 30f;
            float loop = duration > 0f ? Mathf.Repeat(time, duration) : 0f;
            float f = loop * 30f;
            int i0 = Mathf.FloorToInt(f) % _bake.Length;
            if (i0 < 0)
                i0 += _bake.Length;
            blend = f - Mathf.Floor(f);
            frame = _bake[i0];
        }

        void ReadRig(JNode rig)
        {
            var piv = rig.Get("deform_model") != null ? rig.Get("deform_model").Get("PIV") : null;
            if (piv != null && piv.Count >= 2)
                _pivot = new Vector2(piv.At(0).Float(620f), piv.At(1).Float(1000f));
            var model = rig.Get("deform_model");
            if (model != null)
            {
                if (model.Get("BREATH_PX") != null)
                    _breathPx = model.Get("BREATH_PX").Float(_breathPx);
                if (model.Get("HEAD_K") != null)
                    _headFollow = model.Get("HEAD_K").Float(_headFollow);
            }

            var panel = rig.Get("panel") != null ? rig.Get("panel").Get("placement") : null;
            if (panel != null)
                ReadPlacement(panel.Text());
            var glint = rig.Get("glint");
            if (glint != null)
            {
                _bladeFrom = Pair(glint.Get("blade_from_canvas_px"), _bladeFrom);
                _bladeTo = Pair(glint.Get("blade_to_canvas_px"), _bladeTo);
                if (glint.Get("blade_angle_canvas_deg") != null)
                    _bladeAngle = glint.Get("blade_angle_canvas_deg").Float(_bladeAngle);
            }

            var delay = rig.Get("pony_delay") != null ? rig.Get("pony_delay").Get("taus_s") : null;
            if (delay != null && delay.Count > 0)
            {
                _taus = new float[delay.Count];
                for (int i = 0; i < delay.Count; i++)
                    _taus[i] = delay.At(i).Float();
            }
        }

        void ReadPlacement(string text)
        {
            var numbers = Pull(text);
            if (numbers.Count >= 1)
                _panelScale = numbers[0];
            if (numbers.Count >= 3)
            {
                _panelX = numbers[1];
                _panelY = numbers[2];
            }
        }

        void ReadSprings(JNode node)
        {
            if (node == null || node.Map == null)
                return;
            foreach (var pair in node.Map)
                _springs.Add(Spring.From(pair.Key, pair.Value));
        }

        void ReadBake(JNode anim)
        {
            var data = anim != null && anim.Get("idle_loop") != null ? anim.Get("idle_loop").Get("data") : null;
            if (data == null || data.Count == 0)
                return;
            _bake = new Bake[data.Count];
            for (int i = 0; i < data.Count; i++)
                _bake[i] = Bake.From(data.At(i), _taus.Length);
        }

        Part CreatePart(RectTransform host, JNode node, JNode meshes)
        {
            if (node == null)
                return null;
            string name = node.Get("name") != null ? node.Get("name").Text() : "part";
            var mesh = meshes.Get(name);
            if (mesh == null)
                return null;
            var sprite = Resources.Load<Sprite>(ResourceDir + "Sprites/" + name);
            if (sprite == null || sprite.texture == null)
            {
                Debug.LogWarning("刀のパーツ画像がありません: " + name);
                return null;
            }

            var rect = node.Get("rect_px");
            float rx = rect != null && rect.Get("x") != null ? rect.Get("x").Float() : 0f;
            float ry = rect != null && rect.Get("y") != null ? rect.Get("y").Float() : 0f;
            float rw = rect != null && rect.Get("w") != null ? rect.Get("w").Float(1f) : 1f;
            float rh = rect != null && rect.Get("h") != null ? rect.Get("h").Float(1f) : 1f;
            var rt = NewRect(name, host, new Vector2(0f, 1f), new Vector2(rw * _panelScale, rh * _panelScale));
            rt.anchoredPosition = new Vector2(rx * _panelScale + _panelX, -(ry * _panelScale + _panelY));
            var graphic = rt.gameObject.AddComponent<KatanaPartGraphic>();
            graphic.raycastTarget = false;
            graphic.maskable = true;
            graphic.sprite = sprite;
            graphic.Uvs = Pairs(mesh.Get("uv"));
            graphic.Tris = Ints(mesh.Get("triangles"));
            var rest = Pairs(mesh.Get("vertices_canvas_px"));
            graphic.Positions = new Vector2[rest.Length];
            var fields = mesh.Get("vertex_fields");
            var part = new Part
            {
                Kind = KindOf(node.Get("field") != null ? node.Get("field").Text() : ""),
                Origin = new Vector2(rx, ry),
                Rest = rest,
                Graphic = graphic,
                WU = Floats(fields != null ? fields.Get("wU") : null),
                ExpandX = Floats(fields != null ? fields.Get("expand_x") : null),
                ExpandY = Floats(fields != null ? fields.Get("expand_y") : null),
                G = Floats(fields != null ? fields.Get("g") : null),
                Tau = Floats(fields != null ? fields.Get("tau_s") : null),
                WSide = Floats(fields != null ? fields.Get("w_side") : null),
                WCord = Floats(fields != null ? fields.Get("w_cord") : null)
            };
            if (part.Kind == FieldKind.Head)
                _headSprite = sprite;
            graphic.SetAllDirty();
            return part;
        }

        void CreateFx(RectTransform host)
        {
            _band = FxImage(host, "fx_glint_band", 65f * _panelScale);
            _sparkle = FxImage(host, "fx_sparkle", 81f);
        }

        Image FxImage(RectTransform host, string name, float size)
        {
            var sprite = Resources.Load<Sprite>(ResourceDir + "Sprites/" + name);
            var rt = NewRect(name, host, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            return image;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            return rt;
        }

        static FieldKind KindOf(string field)
        {
            switch (field)
            {
                case "ponytail": return FieldKind.Ponytail;
                case "head": return FieldKind.Head;
                case "cordL": return FieldKind.CordL;
                case "cordR": return FieldKind.CordR;
                default: return FieldKind.Body;
            }
        }

        static JNode Load(string relative)
        {
            var asset = Resources.Load<TextAsset>(ResourceDir + relative);
            if (asset == null || string.IsNullOrEmpty(asset.text))
                return null;
            return JNode.Parse(asset.text);
        }

        static Vector2 Pair(JNode node, Vector2 fallback)
        {
            if (node == null || node.Count < 2)
                return fallback;
            return new Vector2(node.At(0).Float(fallback.x), node.At(1).Float(fallback.y));
        }

        static Vector2[] Pairs(JNode node)
        {
            if (node == null || node.Count == 0)
                return Array.Empty<Vector2>();
            var values = new Vector2[node.Count];
            for (int i = 0; i < node.Count; i++)
            {
                var item = node.At(i);
                values[i] = new Vector2(item != null ? item.At(0).Float() : 0f, item != null && item.Count > 1 ? item.At(1).Float() : 0f);
            }

            return values;
        }

        static float[] Floats(JNode node)
        {
            if (node == null || node.Count == 0)
                return Array.Empty<float>();
            var values = new float[node.Count];
            for (int i = 0; i < node.Count; i++)
                values[i] = node.At(i) != null ? node.At(i).Float() : 0f;
            return values;
        }

        static int[] Ints(JNode node)
        {
            if (node == null || node.Count == 0)
                return Array.Empty<int>();
            var values = new int[node.Count];
            for (int i = 0; i < node.Count; i++)
                values[i] = node.At(i) != null ? Mathf.RoundToInt(node.At(i).Float()) : 0;
            return values;
        }

        static List<float> Pull(string text)
        {
            var list = new List<float>();
            if (string.IsNullOrEmpty(text))
                return list;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool sign = c == '-' || c == '+';
                if (!sign && (c < '0' || c > '9'))
                    continue;
                if (sign && (i + 1 >= text.Length || text[i + 1] < '0' || text[i + 1] > '9'))
                    continue;
                int j = i + 1;
                while (j < text.Length && ((text[j] >= '0' && text[j] <= '9') || text[j] == '.'))
                    j++;
                if (float.TryParse(text.Substring(i, j - i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
                    list.Add(value);
                i = j - 1;
            }

            return list;
        }

        sealed class Part
        {
            public FieldKind Kind;
            public Vector2 Origin;
            public Vector2[] Rest;
            public KatanaPartGraphic Graphic;
            public float[] WU;
            public float[] ExpandX;
            public float[] ExpandY;
            public float[] G;
            public float[] Tau;
            public float[] WSide;
            public float[] WCord;
        }

        struct Kin
        {
            public float Theta;
            public float Tx;
            public float Ty;
            public float TyHead;
            public float Breath;
            public float Lift;

            public Kin(float theta, float tx, float ty, float tyHead, float breath, float lift)
            {
                Theta = theta;
                Tx = tx;
                Ty = ty;
                TyHead = tyHead;
                Breath = breath;
                Lift = lift;
            }

            public static Kin Lerp(Kin a, Kin b, float u) => new Kin(
                Mathf.Lerp(a.Theta, b.Theta, u),
                Mathf.Lerp(a.Tx, b.Tx, u),
                Mathf.Lerp(a.Ty, b.Ty, u),
                Mathf.Lerp(a.TyHead, b.TyHead, u),
                Mathf.Lerp(a.Breath, b.Breath, u),
                Mathf.Lerp(a.Lift, b.Lift, u));
        }

        sealed class Spring
        {
            static readonly Regex IdlePattern = new Regex(
                @"([0-9]*\.?[0-9]+)\*sin\(2\*?pi\*t/([0-9]*\.?[0-9]+)([+-][0-9]*\.?[0-9]+)\)",
                RegexOptions.CultureInvariant);

            public string Name;
            public float Wn;
            public float Zeta;
            public float Inertia = 1f;
            public Vector2 Anchor;
            public bool FollowHead;
            public Vector2 Amp;
            public Vector2 Omega;
            public Vector2 Phase;
            public Vector2 X;
            public Vector2 V;
            public Vector2 Output;
            Vector2 _prev;
            Vector2 _prev2;
            int _samples;

            public static Spring From(string name, JNode node)
            {
                var spring = new Spring { Name = name };
                float hz = node.Get("f_hz") != null ? node.Get("f_hz").Float(0.75f) : 0.75f;
                spring.Wn = 2f * Mathf.PI * hz;
                spring.Zeta = node.Get("damping") != null ? node.Get("damping").Float(0.28f) : 0.28f;
                spring.Inertia = node.Get("inertia_gain") != null ? node.Get("inertia_gain").Float(1f) : 1f;
                spring.Anchor = Pair(node.Get("anchor_canvas_px"), Vector2.zero);
                string follow = node.Get("anchor_moves_with") != null ? node.Get("anchor_moves_with").Text() : "Body";
                spring.FollowHead = follow == "Head";
                string idle = node.Get("idle") != null ? node.Get("idle").Text() : string.Empty;
                var matches = IdlePattern.Matches(idle);
                for (int i = 0; i < matches.Count && i < 2; i++)
                {
                    float amp = float.Parse(matches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float period = float.Parse(matches[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float phase = float.Parse(matches[i].Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (i == 0)
                    {
                        spring.Amp.x = amp;
                        spring.Omega.x = period > 0f ? 2f * Mathf.PI / period : 0f;
                        spring.Phase.x = phase;
                    }
                    else
                    {
                        spring.Amp.y = amp;
                        spring.Omega.y = period > 0f ? 2f * Mathf.PI / period : 0f;
                        spring.Phase.y = phase;
                    }
                }

                return spring;
            }

            public void Step(KatanaPortraitRig rig, Kin kin, float time, float dt)
            {
                Vector2 anchor = rig.U(Anchor, kin.Theta, kin.Tx, FollowHead ? kin.TyHead : kin.Ty);
                Vector2 accel = Vector2.zero;
                if (_samples >= 2 && dt > 0.000001f)
                    accel = (anchor - 2f * _prev + _prev2) / (dt * dt);
                _prev2 = _prev;
                _prev = anchor;
                if (_samples < 2)
                    _samples++;
                float wn2 = Wn * Wn;
                var idle = new Vector2(
                    Amp.x * Mathf.Sin(Omega.x * time + Phase.x),
                    Amp.y * Mathf.Sin(Omega.y * time + Phase.y));
                Vector2 force = -Inertia * 4f * accel + wn2 * idle;
                Vector2 acc = force - 2f * Zeta * Wn * V - wn2 * X;
                V += dt * acc;
                X += dt * V;
                Output = new Vector2(6f * (float)Math.Tanh(X.x / 6f), 6f * (float)Math.Tanh(X.y / 6f));
            }
        }

        struct Bake
        {
            public float Time;
            public float Theta;
            public float Tx;
            public float Ty;
            public float TyHead;
            public float Breath;
            public float Beat;
            public float Lift;
            public float GlintU;
            public float GlintK;
            public Vector2 Side;
            public Vector2 CordL;
            public Vector2 CordR;
            public Vector2[] Pony;

            public static Bake From(JNode node, int ponyCount)
            {
                var bake = new Bake
                {
                    Time = node.Get("t") != null ? node.Get("t").Float() : 0f,
                    Theta = node.Get("theta_deg") != null ? node.Get("theta_deg").Float() : 0f,
                    Tx = node.Get("tx") != null ? node.Get("tx").Float() : 0f,
                    Ty = node.Get("ty") != null ? node.Get("ty").Float() : 0f,
                    TyHead = node.Get("ty_head") != null ? node.Get("ty_head").Float() : 0f,
                    Breath = node.Get("breath") != null ? node.Get("breath").Float() : 0f,
                    Beat = node.Get("beat") != null ? node.Get("beat").Float() : 0f,
                    Lift = node.Get("lift") != null ? node.Get("lift").Float() : 0f,
                    GlintK = node.Get("glint_k") != null ? node.Get("glint_k").Float() : 0f,
                    Side = Pair(node.Get("side"), Vector2.zero),
                    CordL = Pair(node.Get("cordL"), Vector2.zero),
                    CordR = Pair(node.Get("cordR"), Vector2.zero),
                    Pony = new Vector2[ponyCount]
                };
                var glintU = node.Get("glint_u");
                bake.GlintU = glintU != null && glintU.Type == JNode.Kind.Number ? glintU.Float() : 0f;
                var pony = node.Get("pony_delayed");
                if (pony != null)
                {
                    int n = Mathf.Min(ponyCount, pony.Count);
                    for (int i = 0; i < n; i++)
                        bake.Pony[i] = Pair(pony.At(i), Vector2.zero);
                }

                return bake;
            }
        }
    }

    public sealed class KatanaPartGraphic : Image
    {
        public Vector2[] Positions = Array.Empty<Vector2>();
        public Vector2[] Uvs = Array.Empty<Vector2>();
        public int[] Tris = Array.Empty<int>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = Positions != null && Uvs != null ? Mathf.Min(Positions.Length, Uvs.Length) : 0;
            if (n < 3 || Tris == null || Tris.Length < 3)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            var tint = color;
            int written = 0;
            for (int i = 0; i < n; i++)
            {
                vh.AddVert(new Vector3(Positions[i].x, Positions[i].y, 0f), tint, Uvs[i]);
                written++;
            }

            int triangles = 0;
            for (int i = 0; i + 2 < Tris.Length; i += 3)
            {
                int a = Tris[i];
                int b = Tris[i + 1];
                int c = Tris[i + 2];
                if ((uint)a >= (uint)written || (uint)b >= (uint)written || (uint)c >= (uint)written)
                    continue;
                vh.AddTriangle(a, b, c);
                triangles++;
            }

            if (triangles == 0)
                base.OnPopulateMesh(vh);
        }
    }

    /// <summary>E5b の味方パネルに刀の娘を載せる。表示キャラはルナ。</summary>
    public sealed class KatanaPortraitPanelView : MonoBehaviour
    {
        public const string Character = "ルナ";

        public bool baked = true;
        public bool idleDemoSkill;
        public bool routeB;

        public KatanaPortraitRig Rig { get; private set; }
        public bool Ready { get; private set; }

        void Awake()
        {
            if (Application.isPlaying)
                EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (!Application.isPlaying || (Ready && Rig != null))
                return;
            Rig = gameObject.AddComponent<KatanaPortraitRig>();
            Rig.baked = baked;
            Rig.idleDemoSkill = idleDemoSkill;
            Rig.routeB = routeB;
            try
            {
                Ready = Rig.Build(transform as RectTransform);
            }
            catch (Exception error)
            {
                Debug.LogError("刀の立ち絵リグの構築に失敗しました: " + error.Message);
                Ready = false;
            }

            if (!Ready)
            {
                Rig.enabled = false;
                for (int i = transform.childCount - 1; i >= 0; i--)
                    Destroy(transform.GetChild(i).gameObject);
                return;
            }

            SetAlpha(0f);
        }

        void Update()
        {
            if (Rig == null)
                return;
            Rig.baked = baked;
            Rig.idleDemoSkill = idleDemoSkill;
            Rig.routeB = routeB;
        }

        public void SetAlpha(float alpha)
        {
            if (Rig == null)
                return;
            Rig.Opacity = Mathf.Clamp01(alpha);
            Rig.Rendering = alpha > 0.001f;
            Rig.Upload();
        }
    }
}
