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
                    // LOOK FOR ONE THAT ALREADY EXISTS FIRST (2026-08-27). This went straight
                    // to `new GameObject` and left a duplicate behind every time the statics
                    // were reset without the object being: a script recompile while the
                    // editor sits in play mode does exactly that, because `_instance` is a
                    // static and the object survives on DontDestroyOnLoad. Measured in play
                    // after one recompile: sixteen AudioSources on two "Sfx" objects, the
                    // orphan still running its own ambience bed under the live one. Two beds
                    // at once is a phasing wash, which is the 'bozuk ses' the brief forbids —
                    // and it compounds with every reload.
                    _instance = FindFirstObjectByType<Sfx>(FindObjectsInactive.Include);
                    if (_instance != null)
                    {
                        // A survivor's private arrays do NOT necessarily come back through a
                        // domain reload — they are not serialized — so a found instance is
                        // re-built if its voices are gone. Without this the reuse above turns
                        // one silent bug into a NullReferenceException on the first click.
                        if (_instance._voices == null) _instance.Build();
                        return _instance;
                    }
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Sfx>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        /// <summary>A second one can still be seated by a scene load; it stands down rather
        /// than adding a second ambience bed and a second pool of voices.</summary>
        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
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

        /// <summary>
        /// Starts (or keeps) the held action loop — "pour_loop", "shake_loop", "stir_loop",
        /// "tap_pull", "rim_turn" — and stops it when <paramref name="name"/> is null.
        ///
        /// THE ENERGY REACHES THE SOUND NOW (2026-08-27). This took a name and a volume, so
        /// the shake loop was nailed to one level: a player shaking the tin flat out and a
        /// player barely wobbling it heard the identical, unchanging noise, even though
        /// `_shakeEnergy` and `_stirEnergy` are both computed every frame from real cursor
        /// travel. The work was being measured and then thrown away at the last step.
        ///
        /// `energy` (0..1) now drives BOTH the level and the pitch, because that is what
        /// effort does to a real sound — a harder shake is louder AND faster, and moving only
        /// one of the two reads as a volume knob rather than as force.
        ///
        /// Both are EASED rather than set. A loop's volume stepping frame to frame is zipper
        /// noise, and a pitch stepping is a warble — either would be the 'bozuk ses' the
        /// brief rules out, and they would arrive precisely when the player is working
        /// hardest.
        /// </summary>
        public static void HoldLoop(string name, float volume = 1f, float energy = -1f)
        {
            var i = Instance;
            if (name == null)
            {
                if (i._loop.isPlaying) i._loop.Stop();
                i._loopHasTarget = false;
                return;
            }
            var clip = i.Clip(name);
            if (clip == null) return;
            // energy < 0 means "this loop has no effort behind it" — a tap runs at the rate
            // the tap runs at, however you feel about it.
            float e = energy < 0f ? -1f : Mathf.Clamp01(energy);
            i._loopVolTarget = volume * Sound.Effective * (e < 0f ? 1f : 0.55f + 0.45f * e);
            i._loopPitchTarget = e < 0f ? 1f : 0.92f + 0.18f * e;
            i._loopHasTarget = true;
            if (i._loop.clip == clip && i._loop.isPlaying) return;
            // A NEW loop starts AT its target rather than easing up from silence: the ease
            // is for changes within a held action, not for its beginning, and fading every
            // pour in over a fifth of a second would read as a late sound.
            i._loop.clip = clip;
            i._loop.volume = i._loopVolTarget;
            i._loop.pitch = i._loopPitchTarget;
            i._loop.Play();
        }

        private float _loopVolTarget, _loopPitchTarget = 1f;
        private bool _loopHasTarget;

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
            float dt = Time.unscaledDeltaTime;
            if (_ambience != null && _ambience.clip != null)
                _ambience.volume = Mathf.MoveTowards(_ambience.volume, _ambienceTarget,
                    dt * 0.9f);
            // The held loop chases its level and its pitch instead of jumping to them.
            // Pitch is chased HALF as fast as volume: a level that lags is unnoticeable,
            // while a pitch that snaps is a warble you cannot un-hear.
            if (_loopHasTarget && _loop != null && _loop.isPlaying)
            {
                _loop.volume = Mathf.MoveTowards(_loop.volume, _loopVolTarget, dt * 2.6f);
                _loop.pitch = Mathf.MoveTowards(_loop.pitch, _loopPitchTarget, dt * 1.3f);
            }
        }
    }
}
