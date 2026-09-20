using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// Audio without any asset files: every clip is synthesised at startup, matching the
    /// project's all-procedural approach. Authored AudioClips can still be dropped into
    /// <see cref="SfxClips"/> and they take priority over the generated ones.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Volumes")]
        [Range(0f, 1f)] public float MusicVolume = 0.16f;
        [Range(0f, 1f)] public float SfxVolume   = 0.45f;

        [Header("Optional authored clips")]
        public AudioClip      BackgroundMusic;
        public List<NamedClip> SfxClips = new();

        [System.Serializable]
        public class NamedClip { public string key; public AudioClip clip; }

        private const int SampleRate = 44100;
        private const int PoolSize   = 8;

        private AudioSource _musicSource;
        private readonly Dictionary<string, AudioClip> _sfx     = new();
        private readonly List<AudioSource>             _sfxPool = new();
        private int _nextSource;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            BuildSfxBank();

            _musicSource        = gameObject.AddComponent<AudioSource>();
            _musicSource.loop   = true;
            _musicSource.volume = MusicVolume;
            _musicSource.playOnAwake = false;
            _musicSource.clip   = BackgroundMusic != null ? BackgroundMusic : BuildAmbientLoop();
            _musicSource.Play();

            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _sfxPool.Add(src);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Playback ────────────────────────────────────────────────────────────

        public void PlaySfx(string key, float volume = 1f)
        {
            if (!_sfx.TryGetValue(key, out var clip) || clip == null) return;
            var src = _sfxPool[_nextSource];
            _nextSource = (_nextSource + 1) % _sfxPool.Count;
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxVolume);
        }

        public void PlaySfxAt(string key, Vector3 position, float volume = 1f)
        {
            if (_sfx.TryGetValue(key, out var clip) && clip != null)
                AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume) * SfxVolume);
        }

        public void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            if (_musicSource != null) _musicSource.volume = MusicVolume;
        }

        public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;

        public void ToggleMusic()
        {
            if (_musicSource == null) return;
            if (_musicSource.isPlaying) _musicSource.Pause();
            else                        _musicSource.UnPause();
        }

        // ── Clip synthesis ──────────────────────────────────────────────────────

        private void BuildSfxBank()
        {
            _sfx["click"]     = Blip("sfx_click",   new[] { 880f },                 0.07f, 0.35f);
            _sfx["sale"]      = Blip("sfx_sale",    new[] { 784f, 1047f, 1319f },   0.30f, 0.40f);
            _sfx["restock"]   = Blip("sfx_restock", new[] { 330f, 440f },           0.16f, 0.35f);
            _sfx["build"]     = Blip("sfx_build",   new[] { 196f, 262f },           0.14f, 0.40f);
            _sfx["deny"]      = Blip("sfx_deny",    new[] { 220f, 175f },           0.18f, 0.40f);
            _sfx["day_start"] = Blip("sfx_open",    new[] { 523f, 659f, 784f },     0.36f, 0.35f);
            _sfx["day_end"]   = Blip("sfx_close",   new[] { 659f, 523f, 392f },     0.40f, 0.35f);
            _sfx["game_over"] = Blip("sfx_over",    new[] { 392f, 330f, 262f, 196f },0.70f, 0.40f);

            foreach (var nc in SfxClips)
                if (nc != null && nc.clip != null && !string.IsNullOrEmpty(nc.key))
                    _sfx[nc.key] = nc.clip;
        }

        /// <summary>A short arpeggio of soft sine tones with an exponential decay.</summary>
        private static AudioClip Blip(string name, float[] notes, float duration, float amplitude)
        {
            int total   = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            int perNote = Mathf.Max(1, total / notes.Length);
            var data    = new float[total];

            for (int n = 0; n < notes.Length; n++)
            {
                float freq  = notes[n];
                int   start = n * perNote;
                int   end   = Mathf.Min(total, start + perNote);

                for (int i = start; i < end; i++)
                {
                    float t    = (i - start) / (float)SampleRate;
                    float life = (i - start) / (float)(end - start);
                    float env  = Mathf.Exp(-4.5f * life) * Mathf.Min(1f, life * 60f);
                    data[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * amplitude
                             + Mathf.Sin(4f * Mathf.PI * freq * t) * env * amplitude * 0.12f;
                }
            }

            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A slow, seamless pad that loops as low-key shop ambience.</summary>
        private static AudioClip BuildAmbientLoop()
        {
            const float length = 8f;
            int total = Mathf.RoundToInt(SampleRate * length);
            var data  = new float[total];

            // A gentle Amaj7-ish drone; harmonics are integer multiples of the loop
            // length so the clip loops without a click.
            float[] freqs = { 110f, 165f, 220f, 277.5f };
            float[] gains = { 0.30f, 0.18f, 0.12f, 0.08f };

            for (int i = 0; i < total; i++)
            {
                float t     = i / (float)SampleRate;
                float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t / length);
                float sample = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float cycles = Mathf.Round(freqs[f] * length);       // whole cycles per loop
                    sample += Mathf.Sin(2f * Mathf.PI * cycles * t / length) * gains[f];
                }
                data[i] = sample * swell * 0.35f;
            }

            var clip = AudioClip.Create("bgm_shop", total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
