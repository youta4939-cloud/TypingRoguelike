using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    [DisallowMultipleComponent]
    public sealed class TypingAudioPlayer : MonoBehaviour
    {
        const int VoiceCount = 6;

        TypingAudioDefinition _def;
        AudioSource[] _voices;
        int _voiceIndex;
        AudioClip[] _keyClips;
        AudioClip _procCorrect;
        AudioClip _procMiss;
        AudioClip _procComplete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOutputBuffer()
        {
            var config = AudioSettings.GetConfiguration();
            if (config.dspBufferSize == 512)
                return;

            config.dspBufferSize = 512;
            AudioSettings.Reset(config);
        }

        public void Setup(TypingAudioDefinition definition)
        {
            _def = definition != null
                ? definition
                : ScriptableObject.CreateInstance<TypingAudioDefinition>();
            ApplyOutputBuffer();
            EnsureVoices();
            EnsureKeyClips();
            EnsureProceduralClips();
        }

        void EnsureKeyClips()
        {
            var assigned = _def != null ? _def.keyClips : null;
            if (assigned != null && assigned.Length > 0)
            {
                var filled = 0;
                for (var i = 0; i < assigned.Length; i++)
                {
                    if (assigned[i] != null)
                        filled++;
                }

                if (filled > 0)
                    _keyClips = assigned;
            }

            if (_keyClips == null || _keyClips.Length == 0)
                _keyClips = Resources.LoadAll<AudioClip>("Battle/Audio/KeyTouch");
            PreloadKeyClips();
        }

        void PreloadKeyClips()
        {
            if (_keyClips == null)
                return;

            for (var i = 0; i < _keyClips.Length; i++)
            {
                var clip = _keyClips[i];
                if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
                    clip.LoadAudioData();
            }
        }

        void EnsureVoices()
        {
            if (_voices != null && _voices.Length == VoiceCount)
                return;

            _voices = new AudioSource[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.dopplerLevel = 0f;
                src.priority = 0;
                src.bypassEffects = true;
                src.bypassListenerEffects = true;
                src.bypassReverbZones = true;
                src.ignoreListenerPause = true;
                src.pitch = 1f;
                _voices[i] = src;
            }
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
            PlayResolve(PickKeyClip() ?? _def?.correctClip ?? _procCorrect, _def?.correctVolume ?? 1f);
        }

        AudioClip PickKeyClip()
        {
            if (_keyClips == null || _keyClips.Length == 0)
                return null;

            for (var attempt = 0; attempt < _keyClips.Length; attempt++)
            {
                var clip = _keyClips[Random.Range(0, _keyClips.Length)];
                if (clip != null)
                    return clip;
            }

            return null;
        }

        public void PlayMiss()
        {
            PlayResolve(_def?.missClip ?? _procMiss, _def?.missVolume ?? 0.45f);
        }

        public void PlayComplete()
        {
            PlayResolve(_def?.completeClip ?? _procComplete, _def?.completeVolume ?? 0.5f);
        }

        void PlayResolve(AudioClip clip, float volume)
        {
            if (clip == null || _voices == null || _voices.Length == 0)
                return;

            var src = _voices[_voiceIndex];
            _voiceIndex = (_voiceIndex + 1) % _voices.Length;
            if (src == null)
                return;

            src.pitch = 1f;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.timeSamples = 0;
            src.Play();
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
