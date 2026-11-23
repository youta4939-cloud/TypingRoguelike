using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>54 コマ、15fps、3.6 秒。スロットごとに時間をずらす。</summary>
    public sealed class FlameSheetPlayer : MonoBehaviour
    {
        public const float Period = 3.6f;
        public const float Fps = 15f;

        public float Offset;
        Image _image;
        Sprite[] _frames;
        int _frame = -1;

        public void Bind(Image image, Sprite[] frames, float offset)
        {
            _image = image;
            _frames = frames;
            Offset = offset;
            _frame = -1;
        }

        public void Tick(float time)
        {
            if (_image == null)
                return;
            var sprite = Frame(time);
            if (sprite == null || _image.sprite == sprite)
                return;
            _image.sprite = sprite;
        }

        public Sprite Frame(float time)
        {
            if (_frames == null || _frames.Length == 0)
                return null;
            float wrapped = Mathf.Repeat(time + Offset, Period);
            int frame = Mathf.FloorToInt(wrapped * Fps) % _frames.Length;
            if (frame < 0)
                frame += _frames.Length;
            _frame = frame;
            return _frames[frame];
        }
    }
}
