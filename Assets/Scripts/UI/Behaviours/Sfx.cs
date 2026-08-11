using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Global sound settings, the same shape as <see cref="Motion"/>: PlayerPrefs-backed
    /// statics a settings surface flips. Volume and mute live here so muting survives a
    /// restart the way reduced motion does.
    /// </summary>
    public static class Sound
    {
        private const string VolKey = "lastcall.volume";
        private const string MuteKey = "lastcall.muted";
        private static bool _loaded;
        private static float _volume;
        private static bool _muted;

        private static void Load()
        {
            if (_loaded) return;
            _volume = PlayerPrefs.GetFloat(VolKey, 0.8f);
            _muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            _loaded = true;
        }

        public static float Volume
        {
            get { Load(); return _volume; }
            set { Load(); _volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolKey, _volume); }
        }

        public static bool Muted
        {
            get { Load(); return _muted; }
            set { Load(); _muted = value; PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); }
        }

        /// <summary>What actually reaches the speakers.</summary>
        public static float Effective => Muted ? 0f : Volume;
    }

    /// <summary>
    /// The audio pipeline (v5 P17 — the project's first). One hidden object, a small pool of
    /// one-shot sources, one looping ambience source and one looping action source (pour or
    /// shake). Clips load from Resources/Audio by name and are cached; a missing clip plays
    /// as silence rather than throwing, so audio can land clip by clip.
    ///
    /// Pitch jitter is a tiny counter-based wobble, NOT a random stream: audio is
    /// presentation, but the determinism rule is easiest kept by never rolling dice at all.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int OneShotVoices = 6;

        private static Sfx _instance;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource[] _voices;
        private int _next;                 // round-robin through the voices
        private int _jitter;               // deterministic pitch wobble counter
        private AudioSource _ambience;
        private AudioSource _loop;         // the held action: pour or shake
        private float _ambienceTarget;     // ducked while a stage is open

        private static Sfx Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Sfx>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        private void Build()
        {
            _voices = new AudioSource[OneShotVoices];
            for (int i = 0; i < OneShotVoices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true; _ambience.playOnAwake = false; _ambience.volume = 0f;
            _loop = gameObject.AddComponent<AudioSource>();
            _loop.loop = true; _loop.playOnAwake = false;
        }

        private AudioClip Clip(string name)
        {
            if (_clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>($"Audio/{name}");
            _clips[name] = c;               // cache the miss too — no per-frame load retries
            return c;
        }

        /// <summary>One-shot, with a whisper of deterministic pitch wobble so five clinks in
        /// a row read as five glasses rather than a sampler.</summary>
        public static void Play(string name, float volume = 1f)
        {
            var i = Instance;
            var clip = i.Clip(name);
            if (clip == null) return;
            var v = i._voices[i._next];
            i._next = (i._next + 1) % OneShotVoices;
            i._jitter = (i._jitter * 73 + 41) % 97;
            v.pitch = 0.97f + 0.06f * (i._jitter / 96f);
            v.PlayOneShot(clip, volume * Sound.Effective);
        }

        /// <summary>Starts (or keeps) the held action loop — "pour_loop", "shake_loop" — and
        /// stops it when <paramref name="name"/> is null.</summary>
        public static void HoldLoop(string name, float volume = 1f)
        {
            var i = Instance;
            if (name == null)
            {
                if (i._loop.isPlaying) i._loop.Stop();
                return;
            }
            var clip = i.Clip(name);
            if (clip == null) return;
            i._loop.volume = volume * Sound.Effective;
            if (i._loop.clip == clip && i._loop.isPlaying) return;
            i._loop.clip = clip;
            i._loop.Play();
        }

        /// <summary>The bar bed. Call every frame with whether a stage is open; the volume
        /// eases toward loud or ducked, so menus muffle the room instead of gating it.</summary>
        public static void Ambience(bool ducked)
        {
            var i = Instance;
            if (i._ambience.clip == null)
            {
                i._ambience.clip = i.Clip("ambience_loop");
                if (i._ambience.clip == null) return;
                i._ambience.Play();
            }
            i._ambienceTarget = (ducked ? 0.25f : 0.7f) * Sound.Effective;
        }

        private void Update()
        {
            if (_ambience != null && _ambience.clip != null)
                _ambience.volume = Mathf.MoveTowards(_ambience.volume, _ambienceTarget,
                    Time.unscaledDeltaTime * 0.9f);
        }
    }
}
