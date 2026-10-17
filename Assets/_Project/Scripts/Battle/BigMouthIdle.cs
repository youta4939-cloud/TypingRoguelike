using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>大口の敵の待機（4.8秒で一巡する呼吸）とかみつき。</summary>
    public sealed class BigMouthIdle : MonoBehaviour
    {
        [Header("パーツ")]
        public Transform body;
        public Transform mouth;
        public Transform teethUpper;
        public Transform teethLower;
        public Transform armL;
        public Transform armR;
        public Transform earringL;
        public Transform earringR;
        public Transform shadow;

        [Header("単位")]
        public float ppu = BigMouthParts.Ppu;
        [Tooltip("UI に置くときはオン。位置の単位がピクセルになる")]
        public bool positionsArePixels;

        [Header("待機 — 周期")]
        public float breathPeriod = 2.4f;
        public float jawPeriod = 1.2f;
        public float armPeriod = 1.2f;
        public float rightArmDelay = 0.3f;
        public float earringDelay = 0.15f;

        [Header("待機 — 振れ幅")]
        public float breathScaleX = 0.015f;
        public float breathScaleY = 0.03f;
        public float upperTeethPixels = 1.5f;
        public float lowerTeethPixels = 4f;
        public float mouthScaleY = 0.03f;
        public float armDegrees = 9f;
        public float earringDegrees = 6f;
        public float shadowScaleX = 0.04f;
        [Range(0f, 1f)] public float shadowMinAlpha = 0.85f;

        [Header("攻撃 — かみつき 0.4秒")]
        public float attackOpenTime = 0.12f;
        public float attackBiteTime = 0.28f;
        public float attackEndTime = 0.4f;
        public float attackUpperTeethPixels = 10f;
        public float attackLowerOpenPixels = 18f;
        public float attackLowerBitePixels = 4f;
        public float attackMouthOpenScale = 1.12f;
        public float attackMouthBiteScale = 1.03f;
        public float attackBodyOpenScale = 1.08f;
        public float attackBodyBiteScale = 1.04f;
        public Vector2 attackBodyOpenPixels = new(-6f, 4f);
        public Vector2 attackBodyBitePixels = new(-3f, 2f);
        public float attackArmLOpen = 25f;
        public float attackArmLBite = 15f;
        public float attackArmROpen = -15f;
        public float attackArmRBite = -8f;

        Vector3 _teethUpperRest;
        Vector3 _teethLowerRest;
        Vector3 _bodyRest;
        bool _attacking;
        Coroutine _attack;

        void Awake() => CacheRest();

        void Start() => CacheRest();

        void CacheRest()
        {
            if (teethUpper != null)
                _teethUpperRest = teethUpper.localPosition;
            if (teethLower != null)
                _teethLowerRest = teethLower.localPosition;
            if (body != null)
                _bodyRest = body.localPosition;
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
            var duration = Mathf.Max(0.05f, attackEndTime);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ApplyAttack(elapsed);
                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyAttack(duration);
            _attacking = false;
            _attack = null;
        }

        void ApplyIdle(float time)
        {
            var breath = S01(time, breathPeriod);
            var jaw = S01(time, jawPeriod);
            if (body != null)
                body.localScale = new Vector3(1f + breathScaleX * breath, 1f + breathScaleY * breath, 1f);
            if (mouth != null)
                mouth.localScale = new Vector3(1f, 1f + mouthScaleY * jaw, 1f);

            var unit = Unit(1f);
            if (teethUpper != null)
                teethUpper.localPosition = _teethUpperRest + new Vector3(0f, upperTeethPixels * jaw * unit, 0f);
            if (teethLower != null)
                teethLower.localPosition = _teethLowerRest + new Vector3(0f, -lowerTeethPixels * jaw * unit, 0f);

            if (armL != null)
                armL.localRotation = Quaternion.Euler(0f, 0f, armDegrees * S(time, armPeriod));
            if (armR != null)
                armR.localRotation = Quaternion.Euler(0f, 0f, -armDegrees * S(time, armPeriod, rightArmDelay));
            if (earringL != null)
                earringL.localRotation = Quaternion.Euler(0f, 0f, earringDegrees * S(time, armPeriod, earringDelay));
            if (earringR != null)
                earringR.localRotation = Quaternion.Euler(0f, 0f, earringDegrees * S(time, armPeriod, earringDelay));

            if (shadow != null)
            {
                shadow.localScale = new Vector3(1f + shadowScaleX * breath, 1f, 1f);
                SetAlpha(shadow, Mathf.Lerp(1f, shadowMinAlpha, breath));
            }
        }

        void ApplyAttack(float time)
        {
            var open = PoseAt(time);
            if (body != null)
            {
                body.localScale = new Vector3(open.BodyScale, open.BodyScale, 1f);
                body.localPosition = _bodyRest + new Vector3(open.BodyX * Unit(1f), open.BodyY * Unit(1f), 0f);
            }

            if (mouth != null)
                mouth.localScale = new Vector3(1f, open.MouthScale, 1f);
            if (teethUpper != null)
                teethUpper.localPosition = _teethUpperRest + new Vector3(0f, open.TeethUpper * Unit(1f), 0f);
            if (teethLower != null)
                teethLower.localPosition = _teethLowerRest + new Vector3(0f, -open.TeethLower * Unit(1f), 0f);
            if (armL != null)
                armL.localRotation = Quaternion.Euler(0f, 0f, open.ArmL);
            if (armR != null)
                armR.localRotation = Quaternion.Euler(0f, 0f, open.ArmR);
            if (earringL != null)
                earringL.localRotation = Quaternion.identity;
            if (earringR != null)
                earringR.localRotation = Quaternion.identity;
            if (shadow != null)
            {
                shadow.localScale = Vector3.one;
                SetAlpha(shadow, 1f);
            }
        }

        AttackPose PoseAt(float time)
        {
            var rest = AttackPose.Rest;
            var opened = new AttackPose
            {
                TeethUpper = attackUpperTeethPixels,
                TeethLower = attackLowerOpenPixels,
                MouthScale = attackMouthOpenScale,
                BodyScale = attackBodyOpenScale,
                BodyX = attackBodyOpenPixels.x,
                BodyY = -attackBodyOpenPixels.y,
                ArmL = attackArmLOpen,
                ArmR = attackArmROpen
            };
            var bite = new AttackPose
            {
                TeethUpper = attackUpperTeethPixels,
                TeethLower = attackLowerBitePixels,
                MouthScale = attackMouthBiteScale,
                BodyScale = attackBodyBiteScale,
                BodyX = attackBodyBitePixels.x,
                BodyY = -attackBodyBitePixels.y,
                ArmL = attackArmLBite,
                ArmR = attackArmRBite
            };

            if (time <= attackOpenTime)
                return AttackPose.Lerp(rest, opened, EaseOut(Mathf.InverseLerp(0f, attackOpenTime, time)));
            if (time <= attackBiteTime)
                return AttackPose.Lerp(opened, bite, EaseIn(Mathf.InverseLerp(attackOpenTime, attackBiteTime, time)));
            return AttackPose.Lerp(bite, rest, EaseInOut(Mathf.InverseLerp(attackBiteTime, attackEndTime, time)));
        }

        float Unit(float pixels) => positionsArePixels || ppu <= 0f ? pixels : pixels / ppu;

        static float S(float time, float period, float delay = 0f)
        {
            if (period <= 0.0001f)
                return 0f;
            return Mathf.Sin(2f * Mathf.PI * (time - delay) / period);
        }

        static float S01(float time, float period, float delay = 0f)
        {
            if (period <= 0.0001f)
                return 0f;
            return 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (time - delay) / period);
        }

        static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
        static float EaseIn(float t) => Mathf.Clamp01(t) * Mathf.Clamp01(t);

        static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static void SetAlpha(Transform target, float alpha)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                var color = renderer.color;
                color.a = alpha;
                renderer.color = color;
                return;
            }

            var image = target.GetComponent<Image>();
            if (image == null)
                return;
            var tint = image.color;
            tint.a = alpha;
            image.color = tint;
        }

        struct AttackPose
        {
            public float TeethUpper;
            public float TeethLower;
            public float MouthScale;
            public float BodyScale;
            public float BodyX;
            public float BodyY;
            public float ArmL;
            public float ArmR;

            public static AttackPose Rest => new AttackPose
            {
                MouthScale = 1f,
                BodyScale = 1f
            };

            public static AttackPose Lerp(AttackPose a, AttackPose b, float t)
            {
                return new AttackPose
                {
                    TeethUpper = Mathf.Lerp(a.TeethUpper, b.TeethUpper, t),
                    TeethLower = Mathf.Lerp(a.TeethLower, b.TeethLower, t),
                    MouthScale = Mathf.Lerp(a.MouthScale, b.MouthScale, t),
                    BodyScale = Mathf.Lerp(a.BodyScale, b.BodyScale, t),
                    BodyX = Mathf.Lerp(a.BodyX, b.BodyX, t),
                    BodyY = Mathf.Lerp(a.BodyY, b.BodyY, t),
                    ArmL = Mathf.Lerp(a.ArmL, b.ArmL, t),
                    ArmR = Mathf.Lerp(a.ArmR, b.ArmR, t)
                };
            }
        }
    }
}
