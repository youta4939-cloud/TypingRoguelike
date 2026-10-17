using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "TypingAudio", menuName = "TypingRoguelike/Typing Audio")]
    public sealed class TypingAudioDefinition : ScriptableObject
    {
        [Header("Clips（未設定なら仮ビープ）")]
        [Tooltip("正しい1打ごとに、この中からランダム。空なら Resources/Battle/Audio/KeyTouch を使う")]
        public AudioClip[] keyClips;
        public AudioClip correctClip;
        public AudioClip missClip;
        public AudioClip completeClip;

        [Header("Volume")]
        [Range(0f, 1f)] public float correctVolume = 1f;
        [Range(0f, 1f)] public float missVolume = 0.45f;
        [Range(0f, 1f)] public float completeVolume = 0.5f;

        [Tooltip("クリップ未設定時にコード生成の短い音を使う")]
        public bool useProceduralFallback = true;

        [Header("Procedural（fallback 用 Hz）")]
        public float correctToneHz = 880f;
        public float missToneHz = 220f;
        public float completeToneHz = 1320f;
    }
}
