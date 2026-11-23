using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>HP バーの減少分を少し遅れて追従させる。</summary>
    public sealed class HudBarTrail : MonoBehaviour
    {
        public Image Fill;
        public Image Trail;
        public float Delay = 0.35f;
        public float Speed = 0.9f;

        float _last = -1f;
        float _hold;

        void Update()
        {
            if (Fill == null || Trail == null)
                return;

            var target = Fill.fillAmount;
            if (!Mathf.Approximately(target, _last))
            {
                if (target > Trail.fillAmount || _last < 0f)
                    Trail.fillAmount = target;
                _hold = Delay;
                _last = target;
            }

            if (Trail.fillAmount <= target)
                return;

            if (_hold > 0f)
            {
                _hold -= Time.unscaledDeltaTime;
                return;
            }

            Trail.fillAmount = Mathf.MoveTowards(Trail.fillAmount, target, Speed * Time.unscaledDeltaTime);
        }
    }
}
