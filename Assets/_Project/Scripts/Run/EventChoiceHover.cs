using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace TypingRoguelike.Run
{
    public sealed class EventChoiceHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] Color RestBorder = new(0.43f, 0.34f, 0.23f, 0.85f);
        [SerializeField] Color FocusBorder = new(0.73f, 0.59f, 0.4f, 1f);
        Image[] _border;
        bool _pointer;
        bool _selected;
        bool _active;
        Action _onEnter;
        Action _onExit;

        public void Bind(Image[] border, Action onEnter, Action onExit)
        {
            _border = border;
            _onEnter = onEnter;
            _onExit = onExit;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData) { _pointer = true; Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { _pointer = false; Refresh(); }
        public void OnSelect(BaseEventData eventData) { _selected = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { _selected = false; Refresh(); }

        void Refresh()
        {
            var active = _pointer || _selected;
            if (_border != null)
                foreach (var edge in _border) edge.color = active ? FocusBorder : RestBorder;
            if (active == _active)
                return;
            _active = active;
            if (active) _onEnter?.Invoke(); else _onExit?.Invoke();
        }
    }
}
