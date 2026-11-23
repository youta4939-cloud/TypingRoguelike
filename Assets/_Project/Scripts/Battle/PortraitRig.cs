using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public enum PortraitFace { Default, Hurt }

    public enum PortraitBackend
    {
        LinearBlend,
        SpriteSkin,
        RigidParts
    }

    /// <summary>
    /// デモン娘の立ち絵。骨の回転で各パーツ画像を動かし、味方パネルの Image として描く。
    /// </summary>
    public sealed class PortraitRig : MonoBehaviour
    {
        public const float Ppu = 100f;

        public float bpm = 60f;
        public float amplitude = 1f;
        public bool baked;
        public bool Rendering = true;
        public float Opacity = 1f;

        public event Action<float> OnHitFlash;

        public PortraitBackend Backend
        {
            get => _backend;
            set
            {
                if (_backend == value)
                    return;
                bool wasRigid = _backend == PortraitBackend.RigidParts;
                _backend = value;
                if (wasRigid != (value == PortraitBackend.RigidParts) && _ready)
                    RebuildGraphics();
            }
        }

        public float FlashStrength => _flashGain * Mathf.Clamp01(_pose.Flash);

        const string ResourceDir = "Battle/Portraits/Demon/";

        readonly List<Bone> _bones = new List<Bone>();
        readonly Dictionary<string, Bone> _byName = new Dictionary<string, Bone>();
        readonly List<Bone> _springs = new List<Bone>();
        readonly List<Part> _parts = new List<Part>();
        readonly List<Vector2> _glintArc = new List<Vector2>();

        Transform _root;
        RectTransform _partsRoot;
        Image _glint;
        Image _flashImage;
        Texture2D _glintTex;
        Sprite _glintSprite;
        Sprite _hurtSprite;
        Part _head;
        Transform _ikTarget;
        Transform _chestPoint;
        Vector3 _chestRest;
        Vector2 _chestCanvas;
        Vector2 _ikCanvas;
        Bone _slvr0;
        Bone _slvr1;
        Bone _body;
        Bone _headBone;
        Bone _halberd;
        float _bendSign = -1f;

        Pose _pose;
        Vector2 _chestState;
        Vector2 _chestShown;
        Vector2 _chestVel;
        Vector2 _chestPrev;
        Vector2 _chestPrev2;
        int _chestSamples;
        int _missingSprites;
        Vector2 _camPrev;
        Vector2 _camPrev2;
        int _camSamples;

        bool _ready;
        PortraitBackend _backend = PortraitBackend.LinearBlend;
        PortraitFace _face = PortraitFace.Default;
        float _time;
        float _accum;
        float _step = 1f / 240f;
        float _camGain = 0.6f;
        float _flashGain = 0.8f;
        float _flashR = 1f;
        float _flashG = 40f / 255f;
        float _flashB = 46f / 255f;
        float _sentFlash = -1f;
        float _skillStart = -10000f;
        float _hitStart = -10000f;
        float _hurtUntil = -1f;
        bool _wasBaked;
        float _bakeOrigin;
        BakeFrame[] _baked = Array.Empty<BakeFrame>();
        float _bakeFps = 30f;
        float _bakeDuration = 9f;

        float _idlePeriod = 4f;
        float _breathAmp = 0.6f;
        float _tiltAmp = 0.4f;
        float _tiltPhase = -0.5f;
        float _halbAmp = 0.35f;
        float _halbPhase = -0.9f;
        float _bobAmp = 12f;
        float _leanAmp = 2.5f;
        float _hipAmp = 6f;
        float _headAmp = 1f;
        float _nodAmp = 0.6f;
        float _halbGroove = 0.7f;
        float _beatDip = 0.42f;
        float _headLag = 0.5f;
        float _nodDelay = 0.15f;
        float _halbLag = 0.12f;
        float _bobWobble = 0.09f;
        float _breathDy = 5f;
        float _breathDx = 0.009f;
        float _breathY0 = 720f;
        float _breathYSpan = 300f;
        float _breathX0 = 560f;
        float _breathYMid = 520f;
        float _breathSigma = 120f;
        float _gwSkill = 0.85f;
        float _gwSkillRise = 0.22f;
        float _gwSkillHold = 0.9f;
        float _gwSkillBack = 0.9f;
        float _gwHit = 0.6f;
        float _gwHitRise = 0.05f;
        float _gwHitHold = 0.45f;
        float _gwHitBack = 0.9f;
        float _kRise = 0.15f;
        float _kHold = 0.45f;
        float _kBack = 0.55f;
        float _skillLean = 3.2f;
        float _skillTilt = 1.6f;
        float _skillHalb = 3.5f;
        float _halbDelay = 0.03f;
        float _halbRise = 0.16f;
        float _halbHold = 0.45f;
        float _halbBack = 0.55f;
        float _camScaleK = 0.04f;
        float _camDxK = 20f;
        float _camDyK = -6f;
        float _skillWingRise = 0.16f;
        float _skillWingHold = 0.5f;
        float _skillWingBack = 0.5f;
        float _skillWingR0 = -23f;
        float _skillWingR1 = -9f;
        float _skillWingL = -20f;
        float _glintStart = 0.1f;
        float _glintEnd = 0.58f;
        float _hitLean = -3.4f;
        float _hitTilt = -2.2f;
        float _hitHalb = -2.4f;
        float _hitCamDx = -18f;
        float _hitCamDy = -4f;
        float _recoilRise = 0.07f;
        float _recoilFloor = -0.25f;
        float _recoilFreq = 1.6f;
        float _recoilDecay = 6.5f;
        float _shakeWindow = 0.4f;
        float _shakeAmp = 5f;
        float _shakeDecay = 0.09f;
        float _shakeFreq = 22f;
        float _shakeY = 0.6f;
        float _flashWindow = 0.6f;
        float _flashDecay = 0.11f;
        float _hitWingRise = 0.04f;
        float _hitWingHold = 0.08f;
        float _hitWingBack = 0.1f;
        float _hitWingR0 = 11f;
        float _hitWingR1 = 7f;
        float _hitWingL = 11f;
        float _hairDrill = 0.75f;
        float _hairLock = 0.5f;
        float _bumpSharp = 9f;
        float _chestF = 2.4f;
        float _chestZ = 0.62f;
        float _chestGainX = 3f;
        float _chestGainY = 1.5f;
        float _chestLimX = 2f;
        float _chestLimY = 6f;
        float _focusX = 615f;
        float _focusY = 300f;
        float _anchorX = 440f;
        float _anchorY = 300f;
        WingDef _wingR0;
        WingDef _wingR1;
        WingDef _wingL;

        struct Pose
        {
            public float Breath, Lean, Tilt, Halb, Bob, Hip;
            public float CamScale, CamDx, CamDy, ShakeX, ShakeY, Flash;
            public float WingR0, WingR1, WingL;
            public float GlintU;
            public bool Glint;
            public bool Hurt;
        }

        struct WingDef
        {
            public bool HasGw;
            public float GwAmp, GwPhase, BumpAmp, BumpT, SinAmp, SinPhase;
        }

        struct BakeFrame
        {
            public float Lean, Tilt, Halb, Breath, Bob, Hip, Flash;
            public float CamScale, CamDx, CamDy, ShakeX, ShakeY, ChestX, ChestY, GlintU;
            public bool Glint, Hurt;
            public Dictionary<string, float> Angles;
        }

        sealed class Bone
        {
            public string Name;
            public string Parent;
            public Transform Xf;
            public Vector2 HeadCanvas;
            public Vector2 HeadRig;
            public Vector2 RestLocal;
            public Vector2 RestDir = Vector2.right;
            public Vector3 LocalTail;
            public float Length = 1f;
            public bool Breath;
            public bool Spring;
            public bool Ik;
            public float Freq = 1f;
            public float Damp = 0.5f;
            public float Amp;
            public float Phase;
            public float Gain;
            public float Limit = 10f;
            public float Phi;
            public float Omega;
            public float Shown;
            public Matrix4x4 BindInv = Matrix4x4.identity;
            public Vector2 Prev;
            public Vector2 Prev2;
            public int Samples;
        }

        sealed class Part
        {
            public string Name;
            public int Order;
            public bool Rigid;
            public string Attach;
            public string RouteB;
            public float RectX, RectY, RectW, RectH;
            public Vector2 Pivot;
            public Vector2 PivotNorm = new Vector2(0.5f, 0.5f);
            public Sprite Sprite;
            public Image View;
            public Vector3[] Rest;
            public Vector2[] Uv = Array.Empty<Vector2>();
            public int[] Index = Array.Empty<int>();
            public float[] Weight = Array.Empty<float>();
            public readonly List<int> Tris = new List<int>();
        }

        public bool Build(RectTransform host, PortraitPanelView view)
        {
            _root = transform;
            var rigText = Resources.Load<TextAsset>(ResourceDir + "Data/portrait_rig_v10");
            var meshText = Resources.Load<TextAsset>(ResourceDir + "Data/portrait_meshes_v10");
            var animText = Resources.Load<TextAsset>(ResourceDir + "Data/portrait_anim_v10");
            if (rigText == null || meshText == null || animText == null)
            {
                Debug.LogError("立ち絵の JSON を Resources から読めませんでした。");
                return false;
            }

            var rig = JNode.Parse(rigText.text);
            var meshes = JNode.Parse(meshText.text);
            var anim = JNode.Parse(animText.text);
            ReadAnim(anim);
            ReadFields(rig);
            CreateBones(rig);
            CreateParts(rig, meshes.Get("meshes"));
            CreateHelpers();
            CreateUi(host);
            if (view != null)
                view.bpm = bpm;
            Preroll(3f);
            Upload();
            var mask = host != null ? host.GetComponentInParent<Mask>() : null;
            if (mask != null)
                MaskUtilities.NotifyStencilStateChanged(mask);
            _ready = _parts.Count > 0;
            if (_ready)
                Debug.Log("アリスの戦闘立ち絵を読み込みました（" + _parts.Count + " パーツ）。");
            return _ready;
        }

        public void PlaySkill()
        {
            _skillStart = _time;
        }

        public void PlayHit()
        {
            _hitStart = _time;
            _hurtUntil = _time + 0.4f;
            SetFace(PortraitFace.Hurt);
        }

        public void SetFace(PortraitFace face)
        {
            if (face == PortraitFace.Default)
                _hurtUntil = -1f;
            if (_face == face)
                return;
            _face = face;
            ApplyFace();
        }

        void LateUpdate()
        {
            if (!_ready)
                return;
            if (baked != _wasBaked)
            {
                if (baked)
                    _bakeOrigin = _time;
                else
                    ResetHistory();
                _wasBaked = baked;
            }

            _accum += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            int guard = 0;
            while (_accum >= _step && guard++ < 16)
            {
                _accum -= _step;
                Substep();
            }

            if (!baked && _hurtUntil >= 0f && _time >= _hurtUntil)
            {
                _hurtUntil = -1f;
                SetFace(PortraitFace.Default);
            }
            else if (baked)
            {
                SetFace(_pose.Hurt ? PortraitFace.Hurt : PortraitFace.Default);
            }

            float flash = Rendering ? FlashStrength * Opacity : 0f;
            if (_flashImage != null)
                _flashImage.color = new Color(_flashR, _flashG, _flashB, flash);
            if (Mathf.Abs(flash - _sentFlash) > 0.001f)
            {
                _sentFlash = flash;
                OnHitFlash?.Invoke(flash);
            }

            if (Rendering)
                Upload();
        }

        void OnDestroy()
        {
            if (_glintSprite != null)
                Destroy(_glintSprite);
            if (_glintTex != null)
                Destroy(_glintTex);
        }

        void Substep()
        {
            _time += _step;
            if (baked && _baked.Length > 0)
                ApplyBakedPose();
            else
                ComputePose();
            PlacePose();
            SolveIk();
            Springs(_step);
        }

        void Preroll(float seconds)
        {
            bool was = baked;
            baked = false;
            _time = -seconds;
            _accum = 0f;
            int steps = Mathf.CeilToInt(seconds / _step);
            for (int i = 0; i < steps; i++)
                Substep();
            _time = 0f;
            _accum = 0f;
            baked = was;
        }

        void ComputePose()
        {
            float rate = Mathf.Max(1f, bpm);
            float period = _idlePeriod * (60f / rate);
            float tb = _time * rate / 60f;
            float w = Mathf.PI * 2f * _time / period;
            float sway = Mathf.Cos(Mathf.PI * (tb - _beatDip));
            float gw = Groove();
            float amp = amplitude;
            float ts = _time - _skillStart;
            float th = _time - _hitStart;
            float k = Env(ts, _kRise, _kHold, _kBack);
            float recoil = Recoil(th);

            _pose = default;
            _pose.Breath = _breathAmp * Mathf.Sin(w);
            _pose.Bob = _bobAmp * amp * gw * Bobf(tb);
            _pose.Hip = -_hipAmp * amp * gw * sway;
            _pose.Lean = _leanAmp * amp * gw * sway + _skillLean * k + _hitLean * recoil;
            _pose.Tilt = _tiltAmp * Mathf.Sin(w + _tiltPhase);
            _pose.Tilt += -_headAmp * amp * gw * Mathf.Cos(Mathf.PI * (tb - _beatDip - _headLag));
            _pose.Tilt += -_nodAmp * amp * gw * Bobf(tb - _nodDelay);
            _pose.Tilt += _skillTilt * k + _hitTilt * recoil;
            _pose.Halb = _halbAmp * Mathf.Sin(w + _halbPhase);
            _pose.Halb += _halbGroove * amp * gw * Mathf.Cos(Mathf.PI * (tb - _beatDip - _halbLag));
            _pose.Halb += _skillHalb * Env(ts - _halbDelay, _halbRise, _halbHold, _halbBack) + _hitHalb * recoil;
            _pose.CamScale = 1f + _camScaleK * k;
            _pose.CamDx = _camDxK * k + _hitCamDx * Mathf.Max(recoil, 0f);
            _pose.CamDy = _camDyK * k + _hitCamDy * Mathf.Max(recoil, 0f);
            if (th >= 0f && th < _shakeWindow)
            {
                float s = _shakeAmp * Mathf.Exp(-th / Mathf.Max(0.0001f, _shakeDecay))
                    * Mathf.Sin(Mathf.PI * 2f * _shakeFreq * th);
                _pose.ShakeX = s;
                _pose.ShakeY = _shakeY * s;
            }

            if (th >= 0f && th < _flashWindow)
                _pose.Flash = Mathf.Exp(-th / Mathf.Max(0.0001f, _flashDecay));

            float kw = Env(ts, _skillWingRise, _skillWingHold, _skillWingBack);
            float tw = Env(th, _hitWingRise, _hitWingHold, _hitWingBack);
            _pose.WingR0 = EvalWing(_wingR0, gw, tb, w, period) + _skillWingR0 * kw + _hitWingR0 * tw;
            _pose.WingR1 = EvalWing(_wingR1, gw, tb, w, period) + _skillWingR1 * kw + _hitWingR1 * tw;
            _pose.WingL = EvalWing(_wingL, gw, tb, w, period) + _skillWingL * kw + _hitWingL * tw;

            if (ts >= _glintStart && ts <= _glintEnd && _glintEnd > _glintStart)
            {
                _pose.Glint = true;
                _pose.GlintU = Mathf.Clamp01((ts - _glintStart) / (_glintEnd - _glintStart));
            }
        }

        void ApplyBakedPose()
        {
            float seq = _time - _bakeOrigin;
            if (_bakeDuration > 0.01f)
            {
                seq %= _bakeDuration;
                if (seq < 0f)
                    seq += _bakeDuration;
            }

            int index = Mathf.Clamp(Mathf.RoundToInt(seq * _bakeFps), 0, _baked.Length - 1);
            var frame = _baked[index];
            _pose.Breath = frame.Breath;
            _pose.Lean = frame.Lean;
            _pose.Tilt = frame.Tilt;
            _pose.Halb = frame.Halb;
            _pose.Bob = frame.Bob;
            _pose.Hip = frame.Hip;
            _pose.CamScale = frame.CamScale <= 0f ? 1f : frame.CamScale;
            _pose.CamDx = frame.CamDx;
            _pose.CamDy = frame.CamDy;
            _pose.ShakeX = frame.ShakeX;
            _pose.ShakeY = frame.ShakeY;
            _pose.Flash = frame.Flash;
            _pose.Glint = frame.Glint;
            _pose.GlintU = frame.GlintU;
            _pose.Hurt = frame.Hurt;
            _chestShown = new Vector2(frame.ChestX, frame.ChestY);
            _bakedAngles = frame.Angles;
        }

        Dictionary<string, float> _bakedAngles;

        void PlacePose()
        {
            var groove = Find("Groove");
            if (groove != null)
                SetLocal(groove, new Vector2(_pose.Hip, _pose.Bob), 0f);
            if (_body != null)
                SetLocal(_body, Vector2.zero, _pose.Lean);
            PlaceBreath();
            if (_headBone != null)
                SetLocal(_headBone, BreathDisp(_headBone.HeadCanvas, _pose.Breath), _pose.Tilt);
            if (_halberd != null)
                SetLocal(_halberd, BreathDisp(_halberd.HeadCanvas, _pose.Breath), _pose.Halb);
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                if (bone == groove || bone == _body || bone == _headBone || bone == _halberd)
                    continue;
                if (bone.Name == "BreathY" || bone.Name == "BreathX" || bone.Name == "ChestSoft" || bone.Name == "Anchor")
                    continue;
                var delta = bone.Breath ? BreathDisp(bone.HeadCanvas, _pose.Breath) : Vector2.zero;
                SetLocal(bone, delta, 0f);
            }

            if (_chestPoint != null)
                _chestPoint.localPosition = _chestRest + ToUnit(BreathDisp(_chestCanvas, _pose.Breath));
            var chest = Find("ChestSoft");
            if (chest != null)
                SetLocal(chest, new Vector2(_chestShown.x * 3f, _chestShown.y * 3f), 0f);
        }

        void PlaceBreath()
        {
            var breathY = Find("BreathY");
            if (breathY != null)
                SetLocal(breathY, new Vector2(0f, -_breathDy * _pose.Breath * 3f), 0f);
            var breathX = Find("BreathX");
            if (breathX != null)
            {
                SetLocal(breathX, Vector2.zero, 0f);
                breathX.Xf.localScale = new Vector3(1f + 3f * _breathDx * _pose.Breath, 1f, 1f);
            }
        }

        void Springs(float dt)
        {
            if (baked && _bakedAngles != null)
            {
                foreach (var pair in _bakedAngles)
                {
                    if (_byName.TryGetValue(pair.Key, out var bone))
                        SetClockwise(bone, pair.Value);
                }

                return;
            }

            float period = _idlePeriod * (60f / Mathf.Max(1f, bpm));
            float gravity = -(_pose.Tilt + _pose.Lean);
            var cam = new Vector2(_pose.CamDx + _pose.ShakeX, _pose.CamDy + _pose.ShakeY);
            var camA = Accel(ref _camPrev, ref _camPrev2, ref _camSamples, cam, dt) * _camGain;
            for (int i = 0; i < _springs.Count; i++)
                SetClockwise(_springs[i], _springs[i].Shown);

            for (int i = 0; i < _springs.Count; i++)
            {
                var bone = _springs[i];
                var head = WorldCanvas(bone.Xf.position);
                var a = Accel(ref bone.Prev, ref bone.Prev2, ref bone.Samples, head, dt) + camA;
                float target = bone.Amp * Mathf.Sin(Mathf.PI * 2f * _time / period - bone.Phase);
                if (bone.Name == "WR_0") target += _pose.WingR0;
                else if (bone.Name == "WR_1") target += _pose.WingR1;
                else if (bone.Name == "WL_0") target += _pose.WingL;
                else if (bone.Name == "DL_1" || bone.Name == "DR_1") target += _hairDrill * gravity;
                else if (bone.Name == "SLL_1" || bone.Name == "SLR_1") target += _hairLock * gravity;

                var dir = BoneDir(bone);
                float inertia = 0f;
                if (bone.Length > 1f)
                    inertia = bone.Gain / bone.Length * (a.x * dir.y - a.y * dir.x) * 57.2958f;
                float wn = Mathf.PI * 2f * bone.Freq;
                float acc = -wn * wn * (bone.Phi - target) - 2f * bone.Damp * wn * bone.Omega + inertia;
                bone.Omega += acc * dt;
                bone.Phi += bone.Omega * dt;
                float lim = Mathf.Max(0.01f, bone.Limit);
                bone.Phi = Mathf.Clamp(bone.Phi, -2f * lim, 2f * lim);
                bone.Shown = lim * (float)Math.Tanh(bone.Phi / lim);
            }

            for (int i = 0; i < _springs.Count; i++)
                SetClockwise(_springs[i], _springs[i].Shown);

            if (_chestPoint != null)
            {
                var a = Accel(ref _chestPrev, ref _chestPrev2, ref _chestSamples, WorldCanvas(_chestPoint.position), dt);
                float wn = Mathf.PI * 2f * _chestF;
                var acc = -wn * wn * _chestState - 2f * _chestZ * wn * _chestVel
                    - new Vector2(_chestGainX * a.x, _chestGainY * a.y);
                _chestVel += acc * dt;
                _chestState += _chestVel * dt;
                _chestState.x = Mathf.Clamp(_chestState.x, -2f * _chestLimX, 2f * _chestLimX);
                _chestState.y = Mathf.Clamp(_chestState.y, -2f * _chestLimY, 2f * _chestLimY);
                _chestShown = new Vector2(
                    _chestLimX * (float)Math.Tanh(_chestState.x / Mathf.Max(0.01f, _chestLimX)),
                    _chestLimY * (float)Math.Tanh(_chestState.y / Mathf.Max(0.01f, _chestLimY)));
                var chest = Find("ChestSoft");
                if (chest != null)
                    SetLocal(chest, new Vector2(_chestShown.x * 3f, _chestShown.y * 3f), 0f);
            }
        }

        void SolveIk()
        {
            if (_slvr0 == null || _slvr1 == null || _ikTarget == null)
                return;
            Vector2 shoulder = WorldCanvas(_slvr0.Xf.position);
            Vector2 target = WorldCanvas(_ikTarget.position);
            Vector2 aim = target - shoulder;
            float reach = aim.magnitude;
            if (reach < 0.001f)
                return;
            float dist = Mathf.Clamp(reach, Mathf.Abs(_slvr0.Length - _slvr1.Length) + 0.5f, _slvr0.Length + _slvr1.Length - 0.5f);
            aim /= reach;
            float cos = Mathf.Clamp((_slvr0.Length * _slvr0.Length + dist * dist - _slvr1.Length * _slvr1.Length)
                / (2f * _slvr0.Length * dist), -1f, 1f);
            float alpha = Mathf.Acos(cos) * Mathf.Rad2Deg;
            var upperDir = RotateClockwise(aim, _bendSign * alpha);
            float parentClock = _slvr0.Xf.parent != null ? WorldClockwise(_slvr0.Xf.parent) : 0f;
            SetClockwise(_slvr0, ClockwiseFromTo(_slvr0.RestDir, upperDir) - parentClock);

            Vector2 elbow = shoulder + upperDir * _slvr0.Length;
            Vector2 fore = target - elbow;
            if (fore.sqrMagnitude < 0.01f)
                fore = _slvr1.RestDir;
            else
                fore.Normalize();
            SetClockwise(_slvr1, ClockwiseFromTo(_slvr1.RestDir, fore) - WorldClockwise(_slvr0.Xf));
        }

        public void Upload()
        {
            for (int i = 0; i < _parts.Count; i++)
                PlaceImage(_parts[i]);
            UploadGlint();
        }

        void PlaceImage(Part part)
        {
            if (part.View == null)
                return;
            string boneName = string.IsNullOrEmpty(part.Attach) ? part.RouteB : part.Attach;
            if (!_byName.TryGetValue(boneName, out var bone) && !_byName.TryGetValue(part.RouteB, out bone))
                return;

            Vector3 local = ToUnit(part.Pivot - bone.HeadCanvas);
            Vector3 rig = _root.InverseTransformPoint(bone.Xf.TransformPoint(local));
            Vector3 panel = CanvasToPanel(rig.x * Ppu, -rig.y * Ppu);
            var rt = part.View.rectTransform;
            rt.anchoredPosition = new Vector2(panel.x, panel.y);
            rt.localRotation = bone.Xf.rotation;
            float scale = _pose.CamScale > 0.01f ? _pose.CamScale : 1f;
            rt.localScale = new Vector3(scale, scale, 1f);
            var color = Color.white;
            color.a = Opacity;
            part.View.color = color;
        }

        void UploadGlint()
        {
            if (_glint == null)
                return;
            if (!_pose.Glint || _halberd == null || _glintArc.Count == 0)
            {
                _glint.enabled = false;
                return;
            }

            Vector2 canvas = Along(_glintArc, _pose.GlintU);
            Vector3 local = ToUnit(canvas - _halberd.HeadCanvas);
            Vector3 rig = _root.InverseTransformPoint(_halberd.Xf.TransformPoint(local));
            Vector3 center = CanvasToPanel(rig.x * Ppu, -rig.y * Ppu);
            float strength = Mathf.Sin(Mathf.PI * Mathf.Clamp01(_pose.GlintU));
            float half = 28f * Mathf.Max(0.01f, _pose.CamScale) * (0.65f + 0.35f * strength);
            var rt = _glint.rectTransform;
            rt.anchoredPosition = new Vector2(center.x, center.y);
            rt.sizeDelta = new Vector2(half * 2f, half * 2f);
            _glint.color = new Color(1f, 0.96f, 0.82f, strength * Opacity);
            _glint.enabled = strength > 0.02f;
        }

        void RebuildGraphics()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].View != null)
                    Destroy(_parts[i].View.gameObject);
                _parts[i].View = null;
            }

            BuildGraphics();
            ApplyFace();
            Upload();
        }

        void CreateUi(RectTransform host)
        {
            _partsRoot = NewRect("Parts", host);
            BuildGraphics();
            _glintTex = MakeBlob();
            _glintSprite = Sprite.Create(
                _glintTex,
                new Rect(0f, 0f, _glintTex.width, _glintTex.height),
                new Vector2(0.5f, 0.5f),
                Ppu);
            var glintGo = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            var glintRt = glintGo.GetComponent<RectTransform>();
            glintRt.SetParent(_partsRoot, false);
            glintRt.anchorMin = glintRt.anchorMax = new Vector2(0f, 1f);
            glintRt.pivot = new Vector2(0.5f, 0.5f);
            glintRt.sizeDelta = new Vector2(56f, 56f);
            _glint = glintGo.GetComponent<Image>();
            _glint.sprite = _glintSprite;
            _glint.raycastTarget = false;
            _glint.enabled = false;
            var flashRt = NewRect("HitFlash", host);
            _flashImage = flashRt.gameObject.AddComponent<Image>();
            _flashImage.color = new Color(_flashR, _flashG, _flashB, 0f);
            _flashImage.raycastTarget = false;
            ApplyFace();
        }

        void BuildGraphics()
        {
            _parts.Sort((a, b) => a.Order.CompareTo(b.Order));
            for (int i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                var go = new GameObject(part.Name, typeof(RectTransform), typeof(Image));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_partsRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = part.PivotNorm;
                rt.sizeDelta = new Vector2(Mathf.Max(1f, part.RectW), Mathf.Max(1f, part.RectH));
                var image = go.GetComponent<Image>();
                image.sprite = part.Sprite;
                image.raycastTarget = false;
                image.color = Color.white;
                part.View = image;
            }
        }

        void CreateBones(JNode rig)
        {
            var list = rig.Get("bones");
            int count = list != null ? list.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var node = list.At(i);
                var bone = new Bone { Name = node.Get("name").Text() };
                var parent = node.Get("parent");
                bone.Parent = parent != null && parent.Type == JNode.Kind.String ? parent.Text() : null;
                bone.HeadCanvas = Vec(node.Get("head_canvas_px"));
                var rigLocal = node.Get("head_rig_local");
                bone.HeadRig = rigLocal != null && rigLocal.Count >= 2
                    ? Vec(rigLocal)
                    : new Vector2(bone.HeadCanvas.x / Ppu, -bone.HeadCanvas.y / Ppu);
                var tail = node.Get("tail_canvas_px");
                if (tail != null && tail.Count >= 2)
                {
                    var tailCanvas = Vec(tail);
                    var delta = tailCanvas - bone.HeadCanvas;
                    float mag = delta.magnitude;
                    bone.Length = node.Get("length_px") != null ? node.Get("length_px").Float(mag) : mag;
                    bone.RestDir = mag > 0.001f ? delta / mag : Vector2.right;
                    bone.LocalTail = ToUnit(delta);
                }

                bone.Breath = node.Get("note") != null && node.Get("note").Text().Contains("breath_disp");
                bone.Ik = node.Get("kind") != null && node.Get("kind").Text() == "ik";
                var spring = node.Get("spring");
                if (spring != null && spring.Type == JNode.Kind.Object)
                {
                    bone.Spring = true;
                    bone.Freq = spring.Get("f_hz").Float(bone.Freq);
                    bone.Damp = spring.Get("damping").Float(bone.Damp);
                    bone.Amp = spring.Get("idle_amp_deg").Float();
                    bone.Phase = spring.Get("idle_phase_rad").Float();
                    bone.Gain = spring.Get("inertia_gain").Float();
                    bone.Limit = spring.Get("soft_limit_deg").Float(bone.Limit);
                }

                bone.BindInv = Matrix4x4.TRS(new Vector3(bone.HeadRig.x, bone.HeadRig.y, 0f), Quaternion.identity, Vector3.one).inverse;
                var go = new GameObject(bone.Name);
                go.transform.SetParent(_root, false);
                bone.Xf = go.transform;
                _bones.Add(bone);
                _byName[bone.Name] = bone;
                if (bone.Spring)
                    _springs.Add(bone);
            }

            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                Bone parent = null;
                if (bone.Parent != null)
                    _byName.TryGetValue(bone.Parent, out parent);
                bone.Xf.SetParent(parent != null ? parent.Xf : _root, false);
                var origin = parent != null ? parent.HeadRig : Vector2.zero;
                bone.RestLocal = bone.HeadRig - origin;
                bone.Xf.localPosition = new Vector3(bone.RestLocal.x, bone.RestLocal.y, 0f);
                bone.Xf.localRotation = Quaternion.identity;
                bone.Xf.localScale = Vector3.one;
            }

            _body = Find("Body");
            _headBone = Find("Head");
            _halberd = Find("Halberd");
            _byName.TryGetValue("SLVR_0", out _slvr0);
            _byName.TryGetValue("SLVR_1", out _slvr1);
        }

        void CreateHelpers()
        {
            if (_halberd != null)
            {
                var go = new GameObject("IkTarget");
                _ikTarget = go.transform;
                _ikTarget.SetParent(_halberd.Xf, false);
                _ikTarget.localPosition = ToUnit(_ikCanvas - _halberd.HeadCanvas);
            }

            if (_body != null)
            {
                var go = new GameObject("ChestPoint");
                _chestPoint = go.transform;
                _chestPoint.SetParent(_body.Xf, false);
                _chestRest = ToUnit(_chestCanvas - _body.HeadCanvas);
                _chestPoint.localPosition = _chestRest;
            }

            if (_slvr0 != null && _slvr1 != null)
            {
                var aim = _ikCanvas - _slvr0.HeadCanvas;
                var upper = _slvr0.LocalTail;
                var upperCanvas = new Vector2(upper.x * Ppu, -upper.y * Ppu);
                _bendSign = ClockwiseFromTo(aim, upperCanvas) >= 0f ? 1f : -1f;
            }
        }

        void CreateParts(JNode rig, JNode meshes)
        {
            var list = rig.Get("parts");
            int count = list != null ? list.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var node = list.At(i);
                var part = new Part
                {
                    Name = node.Get("name").Text(),
                    Order = node.Get("sorting_order") != null ? (int)node.Get("sorting_order").Float() : i,
                    Rigid = node.Get("mode") == null || node.Get("mode").Text() != "skinned",
                    Attach = node.Get("attach_bone") != null ? node.Get("attach_bone").Text() : "Body",
                    RouteB = node.Get("routeB_bone") != null ? node.Get("routeB_bone").Text() : "Body"
                };
                var rect = node.Get("rect_px");
                if (rect != null)
                {
                    part.RectX = rect.Get("x").Float();
                    part.RectY = rect.Get("y").Float();
                    part.RectW = rect.Get("w").Float();
                    part.RectH = rect.Get("h").Float();
                }

                part.Pivot = Vec(node.Get("pivot_canvas_px"));
                part.PivotNorm = Vec(node.Get("sprite_pivot_normalized"));
                if (part.RectW > 1f && part.RectH > 1f && part.PivotNorm == Vector2.zero)
                {
                    part.PivotNorm = new Vector2(
                        (part.Pivot.x - part.RectX) / part.RectW,
                        (part.RectY + part.RectH - part.Pivot.y) / part.RectH);
                }
                var sprite = LoadSprite(node.Get("sprite") != null ? node.Get("sprite").Text() : part.Name);
                if (sprite == null)
                {
                    _missingSprites++;
                    continue;
                }

                part.Sprite = sprite;
                if (!part.Rigid && meshes != null)
                {
                    var mesh = meshes.Get(part.Name);
                    if (mesh != null)
                        ReadMesh(part, mesh);
                    else
                        part.Rigid = true;
                }

                _parts.Add(part);
                if (part.Name == "head")
                    _head = part;
            }

            _hurtSprite = LoadSprite("Sprites/head_hurt.png");
            if (_missingSprites > 0)
                Debug.LogWarning("立ち絵スプライトを " + _missingSprites + " 枚読めませんでした。Unity のインポート完了後にもう一度再生してください。");
        }

        void ReadMesh(Part part, JNode mesh)
        {
            var verts = mesh.Get("vertices_sprite_px");
            var tris = mesh.Get("triangles");
            var weights = mesh.Get("weights");
            int n = verts != null ? verts.Count : 0;
            part.Rest = new Vector3[n];
            part.Uv = new Vector2[n];
            part.Index = new int[n * 4];
            part.Weight = new float[n * 4];
            for (int i = 0; i < part.Index.Length; i++)
                part.Index[i] = -1;
            for (int i = 0; i < n; i++)
            {
                var xy = verts.At(i);
                float vx = xy.At(0).Float();
                float vy = xy.At(1).Float();
                float cx = part.RectX + vx;
                float cy = part.RectY + part.RectH - vy;
                part.Rest[i] = new Vector3(cx / Ppu, -cy / Ppu, 0f);
                part.Uv[i] = new Vector2(part.RectW > 0f ? vx / part.RectW : 0f, part.RectH > 0f ? vy / part.RectH : 0f);
                var inf = weights != null ? weights.At(i) : null;
                int influences = inf != null ? Mathf.Min(4, inf.Count) : 0;
                float sum = 0f;
                for (int k = 0; k < influences; k++)
                {
                    var pair = inf.At(k);
                    if (pair == null || pair.Count < 2)
                        continue;
                    if (_byName.TryGetValue(pair.At(0).Text(), out var bone))
                    {
                        part.Index[i * 4 + k] = _bones.IndexOf(bone);
                        part.Weight[i * 4 + k] = pair.At(1).Float();
                        sum += part.Weight[i * 4 + k];
                    }
                }

                if (sum > 0.001f && Mathf.Abs(sum - 1f) > 0.02f)
                {
                    for (int k = 0; k < 4; k++)
                        part.Weight[i * 4 + k] /= sum;
                }
            }

            int tn = tris != null ? tris.Count : 0;
            tn -= tn % 3;
            part.Tris.Clear();
            for (int i = 0; i < tn; i++)
                part.Tris.Add((int)tris.At(i).Float());
        }

        void ReadAnim(JNode anim)
        {
            var param = anim.Get("params");
            if (param == null)
                return;
            var idle = param.Get("idle");
            if (idle != null)
            {
                _idlePeriod = idle.Get("period_s") != null ? idle.Get("period_s").Float(_idlePeriod) : _idlePeriod;
                TakeSin(idle.Get("breath"), ref _breathAmp, out _);
                TakeSin(idle.Get("tilt"), ref _tiltAmp, out _tiltPhase);
                TakeSin(idle.Get("halb"), ref _halbAmp, out _halbPhase);
            }

            var groove = param.Get("groove");
            if (groove != null)
            {
                if (groove.Get("bpm") != null)
                    bpm = groove.Get("bpm").Float(bpm);
                _beatDip = groove.Get("beat_dip") != null ? groove.Get("beat_dip").Float(_beatDip) : _beatDip;
                var numbers = groove.Get("params");
                if (numbers != null)
                {
                    _bobAmp = numbers.Get("bob").Float(_bobAmp);
                    _leanAmp = numbers.Get("lean").Float(_leanAmp);
                    _hipAmp = numbers.Get("hip").Float(_hipAmp);
                    _headAmp = numbers.Get("head").Float(_headAmp);
                    _nodAmp = numbers.Get("nod").Float(_nodAmp);
                    _halbGroove = numbers.Get("halb").Float(_halbGroove);
                }

                var wobble = groove.Get("bobf") != null ? groove.Get("bobf").Text() : "";
                int at = wobble.IndexOf("ph+", StringComparison.Ordinal);
                if (at >= 0)
                {
                    var pulled = Pull(wobble.Substring(at + 3));
                    if (pulled.Count > 0)
                        _bobWobble = Mathf.Abs(pulled[0]);
                }

                var lags = Pull(groove.Get("tilt") != null ? groove.Get("tilt").Text() : "");
                if (lags.Count >= 1) _headLag = Mathf.Abs(lags[0]);
                if (lags.Count >= 2) _nodDelay = Mathf.Abs(lags[1]);
                var halbLag = Pull(groove.Get("halb") != null ? groove.Get("halb").Text() : "");
                if (halbLag.Count >= 1) _halbLag = Mathf.Abs(halbLag[0]);
                var gw = Pull(groove.Get("gw") != null ? groove.Get("gw").Text() : "");
                if (gw.Count >= 9)
                {
                    _gwSkill = gw[1];
                    _gwSkillRise = gw[2];
                    _gwSkillHold = gw[3];
                    _gwSkillBack = gw[4];
                    _gwHit = gw[5];
                    _gwHitRise = gw[6];
                    _gwHitHold = gw[7];
                    _gwHitBack = gw[8];
                }
            }

            var wings = param.Get("wing_targets_deg");
            if (wings != null)
            {
                ReadWing(wings.Get("WR_0"), ref _wingR0);
                ReadWing(wings.Get("WR_1"), ref _wingR1);
                ReadWing(wings.Get("WL_0"), ref _wingL);
                var bump = Pull(wings.Get("bump") != null ? wings.Get("bump").Text() : "");
                if (bump.Count > 0)
                    _bumpSharp = Mathf.Abs(bump[0]);
            }

            var hair = Pull(param.Get("hair_gravity") != null ? param.Get("hair_gravity").Text() : "");
            if (hair.Count >= 1) _hairDrill = hair[0];
            if (hair.Count >= 2) _hairLock = hair[1];
            var chest = param.Get("chest_spring");
            if (chest != null)
            {
                _chestF = chest.Get("f").Float(_chestF);
                _chestZ = chest.Get("z").Float(_chestZ);
                _chestGainX = chest.Get("gain_x").Float(_chestGainX);
                _chestGainY = chest.Get("gain_y").Float(_chestGainY);
                _chestLimX = chest.Get("lim_x").Float(_chestLimX);
                _chestLimY = chest.Get("lim_y").Float(_chestLimY);
            }

            var skill = param.Get("skill");
            if (skill != null)
            {
                var k = Pull(skill.Get("k") != null ? skill.Get("k").Text() : "");
                if (k.Count >= 3) { _kRise = k[0]; _kHold = k[1]; _kBack = k[2]; }
                var lean = Pull(skill.Get("lean") != null ? skill.Get("lean").Text() : "");
                if (lean.Count >= 1) _skillLean = lean[0];
                var tilt = Pull(skill.Get("tilt") != null ? skill.Get("tilt").Text() : "");
                if (tilt.Count >= 1) _skillTilt = tilt[0];
                var halb = Pull(skill.Get("halb") != null ? skill.Get("halb").Text() : "");
                if (halb.Count >= 5)
                {
                    _skillHalb = halb[0];
                    _halbDelay = -halb[1];
                    _halbRise = halb[2];
                    _halbHold = halb[3];
                    _halbBack = halb[4];
                }

                var cam = Pull(skill.Get("cam") != null ? skill.Get("cam").Text() : "");
                if (cam.Count >= 4) { _camScaleK = cam[1]; _camDxK = cam[2]; _camDyK = cam[3]; }
                var wing = Pull(skill.Get("wings") != null ? skill.Get("wings").Text() : "");
                if (wing.Count >= 6)
                {
                    _skillWingRise = wing[0];
                    _skillWingHold = wing[1];
                    _skillWingBack = wing[2];
                    _skillWingR0 = wing[3];
                    _skillWingR1 = wing[4];
                    _skillWingL = wing[5];
                }

                var glint = Pull(skill.Get("glint") != null ? skill.Get("glint").Text() : "");
                if (glint.Count >= 2) { _glintStart = glint[0]; _glintEnd = glint[1]; }
                var arc = skill.Get("glint_arc_canvas_px");
                if (arc != null)
                {
                    for (int i = 0; i < arc.Count; i++)
                        _glintArc.Add(Vec(arc.At(i)));
                }
            }

            var hit = param.Get("hit");
            if (hit != null)
            {
                var lean = Pull(hit.Get("lean") != null ? hit.Get("lean").Text() : "");
                if (lean.Count >= 1) _hitLean = lean[0];
                var tilt = Pull(hit.Get("tilt") != null ? hit.Get("tilt").Text() : "");
                if (tilt.Count >= 1) _hitTilt = tilt[0];
                var halb = Pull(hit.Get("halb") != null ? hit.Get("halb").Text() : "");
                if (halb.Count >= 1) _hitHalb = halb[0];
                var camText = hit.Get("cam") != null ? hit.Get("cam").Text() : "";
                _hitCamDx = FirstAfter(camText, "dx", _hitCamDx);
                _hitCamDy = FirstAfter(camText, "dy", _hitCamDy);
                var recoil = Pull(hit.Get("recoil") != null ? hit.Get("recoil").Text() : "");
                if (recoil.Count >= 6)
                {
                    _recoilRise = recoil[0];
                    _recoilFloor = recoil[2];
                    _recoilFreq = recoil[4];
                    _recoilDecay = recoil[5];
                }

                var shake = Pull(hit.Get("shake") != null ? hit.Get("shake").Text() : "");
                if (shake.Count >= 6)
                {
                    _shakeWindow = shake[0];
                    _shakeAmp = shake[1];
                    _shakeDecay = shake[2];
                    _shakeFreq = shake[4];
                    _shakeY = shake[5];
                }

                var flash = Pull(hit.Get("flash") != null ? hit.Get("flash").Text() : "");
                if (flash.Count >= 2) { _flashWindow = flash[0]; _flashDecay = flash[1]; }
                if (flash.Count >= 6)
                {
                    _flashR = flash[3] / 255f;
                    _flashG = flash[4] / 255f;
                    _flashB = flash[5] / 255f;
                }

                if (flash.Count >= 1)
                    _flashGain = flash[flash.Count - 1];
                var wing = Pull(hit.Get("wings") != null ? hit.Get("wings").Text() : "");
                if (wing.Count >= 6)
                {
                    _hitWingRise = wing[0];
                    _hitWingHold = wing[1];
                    _hitWingBack = wing[2];
                    _hitWingR0 = wing[3];
                    _hitWingR1 = wing[4];
                    _hitWingL = wing[5];
                }
            }

            var model = param.Get("spring_model");
            if (model != null)
            {
                float hz = model.Get("substep_hz") != null ? model.Get("substep_hz").Float(240f) : 240f;
                _step = 1f / Mathf.Max(30f, hz);
                var described = Pull(model.Get("a") != null ? model.Get("a").Text() : "");
                if (described.Count > 0)
                    _camGain = described[described.Count - 1];
            }

            ReadBake(anim.Get("battle_sequence"));
        }

        void ReadBake(JNode sequence)
        {
            if (sequence == null || sequence.Get("data") == null)
                return;
            _bakeFps = sequence.Get("fps") != null ? sequence.Get("fps").Float(30f) : 30f;
            var data = sequence.Get("data");
            _baked = new BakeFrame[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                var frame = data.At(i);
                var bake = new BakeFrame
                {
                    Lean = frame.Get("lean").Float(),
                    Tilt = frame.Get("tilt").Float(),
                    Halb = frame.Get("halb").Float(),
                    Breath = frame.Get("breath").Float(),
                    Bob = frame.Get("bob").Float(),
                    Hip = frame.Get("hipx").Float(),
                    Flash = frame.Get("flash").Float(),
                    Hurt = frame.Get("face") != null && frame.Get("face").Text() == "Hurt",
                    Angles = new Dictionary<string, float>()
                };
                var cam = frame.Get("cam");
                if (cam != null && cam.Count >= 3)
                {
                    bake.CamScale = cam.At(0).Float(1f);
                    bake.CamDx = cam.At(1).Float();
                    bake.CamDy = cam.At(2).Float();
                }

                var shake = frame.Get("shake");
                if (shake != null && shake.Count >= 2)
                {
                    bake.ShakeX = shake.At(0).Float();
                    bake.ShakeY = shake.At(1).Float();
                }

                var chest = frame.Get("chest");
                if (chest != null && chest.Count >= 2)
                {
                    bake.ChestX = chest.At(0).Float();
                    bake.ChestY = chest.At(1).Float();
                }

                var glint = frame.Get("glint");
                if (glint != null && glint.Type == JNode.Kind.Number)
                {
                    bake.Glint = true;
                    bake.GlintU = glint.Float();
                }

                var angles = frame.Get("bone_deg");
                if (angles != null && angles.Map != null)
                {
                    foreach (var pair in angles.Map)
                        bake.Angles[pair.Key] = pair.Value.Float();
                }

                _baked[i] = bake;
            }

            _bakeDuration = _baked.Length / Mathf.Max(1f, _bakeFps);
        }

        void ReadFields(JNode rig)
        {
            var fields = rig.Get("fields");
            var breath = fields != null ? fields.Get("breath") : null;
            if (breath != null)
            {
                _breathDy = breath.Get("BREATH_DY").Float(_breathDy);
                _breathDx = breath.Get("BREATH_DX").Float(_breathDx);
                var formula = Pull(breath.Get("formula") != null ? breath.Get("formula").Text() : "");
                if (formula.Count >= 5)
                {
                    _breathY0 = formula[0];
                    _breathYSpan = formula[1];
                    _breathX0 = formula[2];
                    _breathYMid = formula[3];
                    _breathSigma = formula[4];
                }
            }

            var chest = fields != null ? fields.Get("chest_softness") : null;
            if (chest != null && chest.Get("center_px") != null)
                _chestCanvas = Vec(chest.Get("center_px"));
            else
                _chestCanvas = new Vector2(540f, 560f);

            var ik = rig.Get("chains") != null ? rig.Get("chains").Get("SLVR") : null;
            ik = ik != null ? ik.Get("ik") : null;
            if (ik != null && ik.Get("target_canvas_px") != null)
                _ikCanvas = Vec(ik.Get("target_canvas_px"));
            else
                _ikCanvas = new Vector2(945f, 690f);

            var camera = rig.Get("panel") != null ? rig.Get("panel").Get("camera") : null;
            var numbers = Pull(camera != null ? camera.Text() : "");
            if (numbers.Count >= 4)
            {
                _focusX = numbers[0];
                _focusY = numbers[1];
                _anchorX = numbers[2];
                _anchorY = numbers[3];
            }
        }

        void ApplyFace()
        {
            if (_head == null || _head.View == null)
                return;
            _head.View.sprite = _face == PortraitFace.Hurt && _hurtSprite != null ? _hurtSprite : _head.Sprite;
        }

        void ResetHistory()
        {
            _camSamples = 0;
            _chestSamples = 0;
            for (int i = 0; i < _springs.Count; i++)
                _springs[i].Samples = 0;
        }

        float Groove()
        {
            float skill = Env(_time - _skillStart, _gwSkillRise, _gwSkillHold, _gwSkillBack);
            float hit = Env(_time - _hitStart, _gwHitRise, _gwHitHold, _gwHitBack);
            return Mathf.Clamp01(1f - _gwSkill * skill - _gwHit * hit);
        }

        float Recoil(float th)
        {
            if (th < 0f)
                return 0f;
            if (th < _recoilRise)
                return Ease(th / Mathf.Max(0.0001f, _recoilRise));
            float u = th - _recoilRise;
            float d = Mathf.Exp(-_recoilDecay * u) * Mathf.Cos(Mathf.PI * 2f * _recoilFreq * u);
            return Mathf.Max(_recoilFloor, d);
        }

        float Bobf(float x)
        {
            float ph = x - Mathf.Floor(x);
            return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * 2f * (ph + _bobWobble * Mathf.Sin(Mathf.PI * 2f * ph)));
        }

        float EvalWing(WingDef wing, float gw, float tb, float w, float period)
        {
            float y = 0f;
            if (wing.HasGw)
                y += wing.GwAmp * amplitude * gw * Mathf.Cos(Mathf.PI * (tb + wing.GwPhase));
            float c = Mathf.Cos(Mathf.PI * 2f * (_time - wing.BumpT) / period);
            y += wing.BumpAmp * Mathf.Exp(_bumpSharp * (c - 1f));
            y += wing.SinAmp * Mathf.Sin(w + wing.SinPhase);
            return y;
        }

        Vector2 BreathDisp(Vector2 canvas, float breath)
        {
            float t = _breathYSpan > 0.001f ? Mathf.Clamp01((_breathY0 - canvas.y) / _breathYSpan) : 0f;
            float s = t * t * (3f - 2f * t);
            float dy = -_breathDy * breath * s;
            float sigma = Mathf.Max(1f, _breathSigma);
            float dx = _breathDx * breath * (canvas.x - _breathX0)
                * Mathf.Exp(-Mathf.Pow((canvas.y - _breathYMid) / sigma, 2f));
            return new Vector2(dx, dy);
        }

        Vector3 CanvasToPanel(float x, float y)
        {
            float px = (x - _focusX) * _pose.CamScale + _anchorX + _pose.CamDx + _pose.ShakeX;
            float py = (y - _focusY) * _pose.CamScale + _anchorY + _pose.CamDy + _pose.ShakeY;
            return new Vector3(px, -py, 0f);
        }

        Vector2 WorldCanvas(Vector3 world)
        {
            Vector3 rig = _root.InverseTransformPoint(world);
            return new Vector2(rig.x * Ppu, -rig.y * Ppu);
        }

        Vector2 BoneDir(Bone bone)
        {
            Vector3 head = _root.InverseTransformPoint(bone.Xf.position);
            Vector3 tail = _root.InverseTransformPoint(bone.Xf.TransformPoint(bone.LocalTail));
            var canvas = new Vector2((tail.x - head.x) * Ppu, -(tail.y - head.y) * Ppu);
            return canvas.sqrMagnitude > 0.0001f ? canvas.normalized : bone.RestDir;
        }

        void SetLocal(Bone bone, Vector2 canvasDelta, float clockwise)
        {
            bone.Xf.localPosition = new Vector3(
                bone.RestLocal.x + canvasDelta.x / Ppu,
                bone.RestLocal.y - canvasDelta.y / Ppu,
                0f);
            SetClockwise(bone, clockwise);
            if (bone.Name != "BreathX")
                bone.Xf.localScale = Vector3.one;
        }

        static void SetClockwise(Bone bone, float degrees)
        {
            bone.Xf.localRotation = Quaternion.Euler(0f, 0f, -degrees);
        }

        Bone Find(string name)
        {
            _byName.TryGetValue(name, out var bone);
            return bone;
        }

        static Vector3 ToUnit(Vector2 canvasDelta)
        {
            return new Vector3(canvasDelta.x / Ppu, -canvasDelta.y / Ppu, 0f);
        }

        static Vector2 Vec(JNode node)
        {
            if (node == null || node.Count < 2)
                return Vector2.zero;
            return new Vector2(node.At(0).Float(), node.At(1).Float());
        }

        static Vector2 Along(List<Vector2> points, float u)
        {
            if (points.Count == 0)
                return Vector2.zero;
            if (points.Count == 1 || u <= 0f)
                return points[0];
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
                total += Vector2.Distance(points[i - 1], points[i]);
            float want = total * Mathf.Clamp01(u);
            for (int i = 1; i < points.Count; i++)
            {
                float seg = Vector2.Distance(points[i - 1], points[i]);
                if (want <= seg || i == points.Count - 1)
                {
                    float t = seg < 0.001f ? 0f : want / seg;
                    return Vector2.Lerp(points[i - 1], points[i], Mathf.Clamp01(t));
                }

                want -= seg;
            }

            return points[points.Count - 1];
        }

        static Vector2 RotateClockwise(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float ClockwiseFromTo(Vector2 from, Vector2 to)
        {
            float dot = from.x * to.x + from.y * to.y;
            float cross = from.x * to.y - from.y * to.x;
            return Mathf.Atan2(cross, dot) * Mathf.Rad2Deg;
        }

        static float WorldClockwise(Transform xf)
        {
            float z = xf.eulerAngles.z;
            if (z > 180f)
                z -= 360f;
            return -z;
        }

        static Vector2 Accel(ref Vector2 prev, ref Vector2 prev2, ref int samples, Vector2 now, float dt)
        {
            Vector2 a = Vector2.zero;
            if (samples >= 2 && dt > 0.000001f)
                a = (now - 2f * prev + prev2) / (dt * dt);
            prev2 = prev;
            prev = now;
            if (samples < 2)
                samples++;
            return a;
        }

        static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Env(float ts, float rise, float hold, float back)
        {
            if (ts <= 0f || rise <= 0.0001f)
                return 0f;
            if (ts < rise)
                return Ease(ts / rise);
            if (ts < hold)
                return 1f;
            if (back <= 0.0001f || ts >= hold + back)
                return 0f;
            return 1f - Ease((ts - hold) / back);
        }

        static void TakeSin(JNode node, ref float amp, out float phase)
        {
            phase = 0f;
            var numbers = Pull(node != null ? node.Text() : "");
            if (numbers.Count >= 1)
                amp = numbers[0];
            if (numbers.Count >= 2)
                phase = numbers[1];
        }

        static void ReadWing(JNode node, ref WingDef wing)
        {
            wing = default;
            if (node == null)
                return;
            string text = node.Text();
            wing.HasGw = text.Contains("gw");
            var numbers = Pull(text);
            int i = 0;
            if (wing.HasGw && numbers.Count >= 2)
            {
                wing.GwAmp = numbers[i++];
                wing.GwPhase = numbers[i++];
            }

            if (i + 1 < numbers.Count)
            {
                wing.BumpAmp = numbers[i++];
                wing.BumpT = Mathf.Abs(numbers[i++]);
            }

            if (i + 1 < numbers.Count)
            {
                wing.SinAmp = numbers[i++];
                wing.SinPhase = numbers[i];
            }
        }

        static float FirstAfter(string text, string token, float fallback)
        {
            if (string.IsNullOrEmpty(text))
                return fallback;
            int at = text.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
                return fallback;
            var numbers = Pull(text.Substring(at + token.Length));
            return numbers.Count > 0 ? numbers[0] : fallback;
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
                if (float.TryParse(text.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    list.Add(value);
                i = j - 1;
            }

            return list;
        }

        static Sprite LoadSprite(string relative)
        {
            int slash = relative.LastIndexOf('/');
            int dot = relative.LastIndexOf('.');
            int start = slash >= 0 ? slash + 1 : 0;
            if (dot < start)
                dot = relative.Length;
            return Resources.Load<Sprite>(ResourceDir + "Sprites/" + relative.Substring(start, dot - start));
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(640f, 1080f);
            rt.localScale = Vector3.one;
        }

        static Texture2D MakeBlob()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float radius = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - radius) / radius;
                    float dy = (y - radius) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = d >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, d);
                    byte alpha = (byte)Mathf.RoundToInt(a * a * 255f);
                    pixels[y * size + x] = new Color32(255, 244, 210, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }

    /// <summary>
    /// E5b の味方パネルに立ち絵を載せる。表示中のキャラはアリス。
    /// </summary>
    public sealed class PortraitPanelView : MonoBehaviour
    {
        public const string Character = "アリス";

        [Tooltip("立ち絵は各パーツを Image で描く。骨の回転と揺れは共通。")]
        public PortraitBackend backend = PortraitBackend.LinearBlend;
        public float bpm = 60f;
        public float amplitude = 1f;
        public bool baked;

        public PortraitRig Rig { get; private set; }
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
            var root = new GameObject("DemonPortraitRig");
            root.hideFlags = HideFlags.DontSave;
            Rig = root.AddComponent<PortraitRig>();
            try
            {
                Ready = Rig.Build(transform as RectTransform, this);
            }
            catch (Exception error)
            {
                Debug.LogError("立ち絵リグの構築に失敗しました: " + error.Message);
                Ready = false;
            }

            if (!Ready)
            {
                Destroy(root);
                Rig = null;
                for (int i = transform.childCount - 1; i >= 0; i--)
                    Destroy(transform.GetChild(i).gameObject);
            }
        }

        void Update()
        {
            if (Rig == null)
                return;
            Rig.bpm = bpm;
            Rig.amplitude = amplitude;
            Rig.baked = baked;
            Rig.Backend = backend;
        }

        public void SetAlpha(float alpha)
        {
            if (Rig == null)
                return;
            Rig.Opacity = Mathf.Clamp01(alpha);
            Rig.Rendering = alpha > 0.001f;
            Rig.Upload();
        }

        void OnDestroy()
        {
            if (Rig != null)
                Destroy(Rig.gameObject);
        }
    }
}
