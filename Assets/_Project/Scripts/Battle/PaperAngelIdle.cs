using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>紙の天使 — map_anim.py と同じ 4 秒ループ（羽ばたき・浮遊・印の脈動）。</summary>
    public sealed class PaperAngelIdle : MonoBehaviour
    {
        public Transform rig;
        public Transform shadow;
        public Transform wingLInner;
        public Transform wingLOuter;
        public Transform wingRInner;
        public Transform wingROuter;
        public Transform faceInner;
        public Transform sealPulse;

        public bool positionsArePixels = true;
        public float loopPeriod = 4f;
        public float floatPixels = 7f;
        public float wingInnerDegrees = 6f;
        public float wingOuterDegrees = 9f;
        public float outerWingPhase = 0.6f;
        public float faceNodDegrees = 1.2f;
        public float faceBobPixels = 1.5f;
        public float sealPulseScale = 0.04f;

        public float attackDuration = 0.45f;
        public float attackLungePixels = 16f;
        public float attackWingSpread = 12f;

        Vector3 _rigRest;
        Vector3 _faceRest;
        Vector3 _sealScaleRest = Vector3.one;
        bool _attacking;
        Coroutine _attack;

        void Awake() => CacheRest();
        void Start() => CacheRest();

        void CacheRest()
        {
            if (rig != null)
                _rigRest = rig.localPosition;
            if (faceInner != null)
                _faceRest = faceInner.localPosition;
            if (sealPulse != null)
                _sealScaleRest = sealPulse.localScale;
        }

        void ApplySealPulse(Transform seal, float pulse01)
        {
            if (seal == null)
                return;
            var sealScale = Mathf.Clamp(1f + sealPulseScale * pulse01, 1f, 1.04f);
            seal.localScale = new Vector3(
                _sealScaleRest.x * sealScale,
                _sealScaleRest.y * sealScale,
                _sealScaleRest.z);
        }

        void Update()
        {
            if (_attacking)
                return;
            ApplyIdle(Time.time);
        }

        public void PlayAttack()
        {
            if (!isActiveAndEnabled)
                return;
            if (_attack != null)
                StopCoroutine(_attack);
            _attack = StartCoroutine(AttackRoutine());
        }

        IEnumerator AttackRoutine()
        {
            _attacking = true;
            CacheRest();
            var duration = Mathf.Max(0.05f, attackDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ApplyAttack(elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyAttack(1f);
            _attacking = false;
            _attack = null;
        }

        void ApplyIdle(float time)
        {
            var p = LoopPhase(time);
            var up = Mathf.Sin(p);
            var flap = Mathf.Sin(2f * p);
            var flapOuter = Mathf.Sin(2f * p - outerWingPhase);
            var pulse = 0.5f + 0.5f * Mathf.Sin(2f * p - 1.2f);
            var faceWave = Mathf.Sin(p - 0.8f);

            if (rig != null)
                rig.localPosition = _rigRest + new Vector3(0f, floatPixels * up, 0f);

            if (shadow != null)
            {
                var s = 1f - 0.06f * up;
                shadow.localScale = new Vector3(s, s, 1f);
                SetAlpha(shadow, 1f - 0.25f * up);
            }

            SetWing(wingLInner, wingInnerDegrees * flap);
            SetWing(wingLOuter, wingOuterDegrees * flapOuter);
            SetWing(wingRInner, -wingInnerDegrees * flap);
            SetWing(wingROuter, -wingOuterDegrees * flapOuter);

            if (faceInner != null)
            {
                faceInner.localRotation = Quaternion.Euler(0f, 0f, faceNodDegrees * faceWave);
                faceInner.localPosition = _faceRest + new Vector3(0f, faceBobPixels * faceWave, 0f);
            }

            ApplySealPulse(sealPulse, pulse);
        }

        void ApplyAttack(float t)
        {
            var ease = t < 0.35f ? EaseOut(t / 0.35f) : EaseIn((t - 0.35f) / 0.65f);
            var lunge = attackLungePixels * (t < 0.35f ? ease : 1f - ease);
            if (rig != null)
                rig.localPosition = _rigRest + new Vector3(0f, -lunge, 0f);

            var spread = attackWingSpread * (t < 0.4f ? EaseOut(t / 0.4f) : 1f - EaseIn((t - 0.4f) / 0.6f));
            SetWing(wingLInner, spread);
            SetWing(wingLOuter, spread * 1.2f);
            SetWing(wingRInner, -spread);
            SetWing(wingROuter, -spread * 1.2f);
        }

        float LoopPhase(float time)
        {
            var period = Mathf.Max(0.01f, loopPeriod);
            return 2f * Mathf.PI * (time / period);
        }

        static void SetWing(Transform wing, float degrees)
        {
            if (wing == null)
                return;
            wing.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
        static float EaseIn(float t) => Mathf.Clamp01(t) * Mathf.Clamp01(t);

        static void SetAlpha(Transform target, float alpha)
        {
            var image = target.GetComponent<Image>();
            if (image == null)
                return;
            var c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }
}
