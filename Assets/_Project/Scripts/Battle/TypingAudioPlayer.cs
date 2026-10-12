using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    [DisallowMultipleComponent]
    public sealed class TypingAudioPlayer : MonoBehaviour
    {
        TypingAudioDefinition _def;
        AudioSource _source;
        AudioClip _procCorrect;
        AudioClip _procMiss;
        AudioClip _procComplete;

        public void Setup(TypingAudioDefinition definition)
        {
            _def = definition != null
                ? definition
                : ScriptableObject.CreateInstance<TypingAudioDefinition>();
            EnsureSource();
            EnsureProceduralClips();
        }

        void EnsureSource()
        {
            if (_source != null)
                return;
            _source = gameObject.GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
        }

        void EnsureProceduralClips()
        {
            if (_def == null || !_def.useProceduralFallback)
                return;

            _procCorrect ??= CreateToneClip("TypingCorrect", _def.correctToneHz, 0.045f);
            _procMiss ??= CreateToneClip("TypingMiss", _def.missToneHz, 0.07f);
            _procComplete ??= CreateToneClip("TypingComplete", _def.completeToneHz, 0.11f);
        }

        public void PlayCorrect()
        {
            PlayResolve(_def?.correctClip ?? _procCorrect, _def?.correctVolume ?? 0.35f, 0.97f, 1.04f);
        }

        public void PlayMiss()
        {
            PlayResolve(_def?.missClip ?? _procMiss, _def?.missVolume ?? 0.45f, 0.92f, 0.98f);
        }

        public void PlayComplete()
        {
            PlayResolve(_def?.completeClip ?? _procComplete, _def?.completeVolume ?? 0.5f, 0.98f, 1.06f);
        }

        void PlayResolve(AudioClip clip, float volume, float pitchMin, float pitchMax)
        {
            if (clip == null || _source == null)
                return;

            _source.pitch = Random.Range(pitchMin, pitchMax);
            _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        static AudioClip CreateToneClip(string name, float frequencyHz, float durationSeconds)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var attack = Mathf.Clamp01(t / 0.006f);
                var release = Mathf.Clamp01((sampleCount - i) / (sampleRate * 0.025f));
                var envelope = attack * release;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequencyHz * t) * envelope * 0.28f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
