using System.Collections;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    /// <summary>骸骨の侍。map_anim.py の 4 秒ループ（呼吸・帯・笠・刀は小さめ）。</summary>
    public sealed class SkeletonSamuraiIdle : MonoBehaviour
    {
        public RectTransform rootShift;
        public Transform torso;
        public Transform sashL;
        public Transform sashRope;
        public Transform sashR;
        public Transform armR;
        public Transform armL;
        public Transform skull;
        public Transform talismanL;
        public Transform chainLOuter;
        public Transform chainLInner;
        public Transform talismanR;
        public Transform chainR;

        public float loopPeriod = 4f;
        public float breathScale = 0.004f;
        public float attackDuration = 0.4f;
        public float attackLungePixels = 10f;
        public float attackKatanaDegrees = 6f;

        Vector2 _rootRest;
        bool _attacking;
        float _attackT;
        Coroutine _attack;

        void Awake()
        {
            if (rootShift != null)
                _rootRest = rootShift.anchoredPosition;
        }

        void Update()
        {
            var p = 2f * Mathf.PI * (Time.time / Mathf.Max(0.01f, loopPeriod));
            if (torso != null)
            {
                var breath = 1f + breathScale * Mathf.Sin(p);
                torso.localScale = new Vector3(1f, breath, 1f);
            }

            Swing(sashL, 2.5f, 2f * p - 0.9f);
            Swing(sashRope, 2f, 2f * p - 1.3f);
            Swing(sashR, 2.5f, 2f * p - 1.7f);
            Swing(armR, 0.6f, p + 0.5f);
            var katana = 0.6f * Mathf.Sin(p + 2f);
            if (_attacking)
                katana += attackKatanaDegrees * AttackEnvelope(_attackT);
            SetAngle(armL, katana);
            Swing(skull, 0.6f, p - 0.7f);
            Swing(talismanL, 6f, 2f * p - 0.3f);
            Swing(chainLOuter, 4f, 2f * p - 0.9f);
            Swing(chainLInner, 4f, 2f * p - 1.6f);
            Swing(talismanR, 6f, 2f * p - 2.2f);
            Swing(chainR, 4f, 2f * p - 2.8f);

            if (rootShift != null && !_attacking)
                rootShift.anchoredPosition = _rootRest;
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
            var duration = Mathf.Max(0.05f, attackDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                _attackT = elapsed / duration;
                if (rootShift != null)
                {
                    var lunge = attackLungePixels * (_attackT < 0.35f
                        ? EaseOut(_attackT / 0.35f)
                        : 1f - EaseIn((_attackT - 0.35f) / 0.65f));
                    rootShift.anchoredPosition = _rootRest + new Vector2(0f, -lunge);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            _attacking = false;
            _attack = null;
            if (rootShift != null)
                rootShift.anchoredPosition = _rootRest;
        }

        static void Swing(Transform target, float degrees, float phase)
        {
            SetAngle(target, degrees * Mathf.Sin(phase));
        }

        static void SetAngle(Transform target, float degrees)
        {
            if (target == null)
                return;
            target.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        static float AttackEnvelope(float t) =>
            t < 0.4f ? 1f - (1f - t / 0.4f) * (1f - t / 0.4f) : 1f - Mathf.Clamp01((t - 0.4f) / 0.6f);

        static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
        static float EaseIn(float t) => Mathf.Clamp01(t) * Mathf.Clamp01(t);
    }
}
