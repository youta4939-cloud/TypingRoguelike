using UnityEngine;

namespace TypingRoguelike.Run
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RunPanelEntrance : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _rect;
        Vector3 _targetScale;
        float _elapsed;
        const float Duration = 0.32f;

        void Awake()
        {
            if (!TryGetComponent(out _group))
            {
                enabled = false;
                return;
            }

            _rect = transform as RectTransform;
            _targetScale = transform.localScale;
            _group.alpha = 1f;
            if (_rect != null)
                _rect.localScale = _targetScale;
            enabled = false;
        }

        void Update()
        {
            if (_group == null)
            {
                enabled = false;
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= Duration)
            {
                FinishEntrance();
                enabled = false;
                return;
            }

            var t = Mathf.Clamp01(_elapsed / Duration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            _group.alpha = eased;
            if (_rect != null)
                _rect.localScale = Vector3.Lerp(_targetScale * 0.985f, _targetScale, eased);
        }

        void FinishEntrance()
        {
            if (_group == null)
                return;
            _group.alpha = 1f;
            if (_rect != null)
                _rect.localScale = _targetScale;
        }

        void OnDisable()
        {
            if (_elapsed > 0f && _elapsed < Duration)
                FinishEntrance();
        }
    }
}
