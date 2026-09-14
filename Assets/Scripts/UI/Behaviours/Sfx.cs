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
    /// shake) — with a pour's far half beside it and a layer for the vessel moving under the
    /// pour (2026-09-15). Clips load from Resources/Audio by name and are cached; a missing clip plays
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
        private AudioSource _loopFar;      // a pour's far half, crossfaded with it by the drop (2026-09-15)
        private AudioSource _motion;       // the vessel moving under the pour (2026-09-15)
        private AudioLowPassFilter _loopLp, _motionLp;
        private float _ambienceTarget;     // ducked while a stage is open
        private AudioSource _rain;         // the city through the window, under everything (2026-09-15)
        private float _rainTarget;
        private AudioSource[] _music;      // two, so a track fades into the next (2026-09-15)
        private int _musicActive;
        private string _mood;
        private bool _musicDucked;
        private float _trackStartedAt;
        private readonly Dictionary<string, List<AudioClip>> _playlists = new Dictionary<string, List<AudioClip>>();
        private readonly Dictionary<string, int> _musicCursor = new Dictionary<string, int>();
        private readonly Dictionary<string, AudioClip[]> _takes = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, int> _takeAt = new Dictionary<string, int>();

        /// <summary>A low-pass this high passes everything the bank holds.</summary>
        private const float OpenCutoff = 22000f;
        /// <summary>A pour let go right over the drink keeps only what is under this: the glug, not the hiss.</summary>
        private const float NearCutoff = 2400f;
        /// <summary>A vessel barely moving is only a low rub; it opens toward <see cref="OpenCutoff"/> flat out.</summary>
        private const float MotionStillCutoff = 1200f;
        /// <summary>The motion layer's level flat out, under the pour's.</summary>
        private const float MotionLevel = 0.5f;

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
            EnsureListener();
            _voices = new AudioSource[OneShotVoices];
            for (int i = 0; i < OneShotVoices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true; _ambience.playOnAwake = false; _ambience.volume = 0f;
            _rain = gameObject.AddComponent<AudioSource>();
            _rain.loop = true; _rain.playOnAwake = false; _rain.volume = 0f;
            var oldMusic = transform.Find("Music");
            if (oldMusic != null) Destroy(oldMusic.gameObject);
            var musicHost = new GameObject("Music");
            musicHost.transform.SetParent(transform, false);
            _music = new[] { musicHost.AddComponent<AudioSource>(), musicHost.AddComponent<AudioSource>() };
            foreach (var m in _music) { m.playOnAwake = false; m.loop = false; m.volume = 0f; }
            _loop = HeldSource("HeldLoop", out _loopLp);
            _loopFar = HeldSource("HeldLoopFar", out _);
            _motion = HeldSource("HeldMotion", out _motionLp);
        }

        /// <summary>A looping source on a child of its own, so the low-pass beside it is its alone (2026-09-15). A survivor
        /// of a domain reload is rebuilt, so a child of the same name is thrown away first.</summary>
        private AudioSource HeldSource(string name, out AudioLowPassFilter lowPass)
        {
            var old = transform.Find(name);
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = true; source.playOnAwake = false;
            lowPass = go.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = OpenCutoff;
            return source;
        }

        /// <summary>
        /// THE ROOM HAD NO EARS (2026-08-27, and it is why none of this was audible).
        ///
        /// The author: "oyun içi sesleri play modda duyamıyorum." Everything measured
        /// healthy — the Game view's mute off, AudioListener.volume 1, PlayerPrefs
        /// unmuted, the sources genuinely playing at the right levels — because every
        /// one of those is about the SENDING side. Unity renders no audio at all without
        /// an AudioListener, and the scene had exactly zero: FindObjectsByType returned
        /// LISTENERS=0, and the Main Camera carried none. A whole sound bank was playing
        /// into a room with no microphone in it.
        ///
        /// The listener belongs on the camera and this class also puts it there when the
        /// scene has one — but it is guaranteed HERE, because Sfx is the one object that
        /// exists whenever a sound is asked for, in the real scene and in the test
        /// scenes alike. A second listener would draw a warning every frame, so this only
        /// ever fills a hole; it never adds one beside an existing pair of ears.
        /// </summary>
        private void EnsureListener()
        {
            if (FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) != null) return;
            var cam = Camera.main;
            var host = cam != null ? cam.gameObject : gameObject;
            host.AddComponent<AudioListener>();
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
        /// <param name="pitch">
        /// A deliberate pitch, where the CALLER knows something the sampler does not —
        /// which of six stools is speaking, for instance. Left at 0 the usual whisper of
        /// deterministic wobble applies instead, so five clinks in a row read as five
        /// glasses rather than one sample played five times.
        /// </param>
        public static void Play(string name, float volume = 1f, float pitch = 0f)
        {
            var i = Instance;
            var clip = i.Take(name);
            if (clip == null) return;
            var v = i._voices[i._next];
            i._next = (i._next + 1) % OneShotVoices;
            i._jitter = (i._jitter * 73 + 41) % 97;
            v.pitch = pitch > 0f ? pitch : 0.97f + 0.06f * (i._jitter / 96f);
            v.PlayOneShot(clip, volume * Sound.Effective);
        }

        /// <summary>
        /// Starts (or keeps) the held action loop — "pour_glass", "pour_tin", "shake_loop",
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
        /// <param name="fall">
        /// HOW FAR THE STREAM DROPS (2026-09-15, the author: "suyun yakından ya da uzaktan düşmesine göre değişen sese
        /// ihtiyacımız var"), 0 a mouth at the drink .. 1 the top of the lift; below 0 for a loop with no drop. A stream let
        /// go right over the drink lands soft — the glug is the sound, and it is dull — and one dropped from high hits
        /// hard: louder, brighter, splashing. So a clip with a `_far` twin crossfades into it on equal power as the drop
        /// grows, and the near one's top closes as it shrinks; an EQ alone only dims a near pour, it does not change what
        /// it is.
        /// </param>
        /// <param name="pan">Where the pour is across the bench, -1 left .. 1 right.</param>
        public static void HoldLoop(string name, float volume = 1f, float energy = -1f, float fall = -1f, float pan = 0f)
        {
            var i = Instance;
            if (name == null)
            {
                if (i._loop.isPlaying) i._loop.Stop();
                if (i._loopFar.isPlaying) i._loopFar.Stop();
                i._loopHasTarget = false;
                return;
            }
            var clip = i.Clip(name);
            if (clip == null) return;
            // energy < 0 means "this loop has no effort behind it" — a tap runs at the rate
            // the tap runs at, however you feel about it.
            float e = energy < 0f ? -1f : Mathf.Clamp01(energy);
            float level = volume * Sound.Effective * (e < 0f ? 1f : 0.55f + 0.45f * e);
            i._loopPitchTarget = e < 0f ? 1f : 0.92f + 0.18f * e;
            float f = fall < 0f ? -1f : Mathf.Clamp01(fall);
            var far = f >= 0f ? i.Clip(name + "_far") : null;
            if (f >= 0f) level *= 0.7f + 0.45f * f;                 // a longer drop hits harder
            i._loopVolTarget = far != null ? level * Mathf.Cos(f * Mathf.PI * 0.5f) : level;
            i._loopFarVolTarget = far != null ? level * Mathf.Sin(f * Mathf.PI * 0.5f) : 0f;
            i._loopCutTarget = f >= 0f ? Mathf.Lerp(NearCutoff, OpenCutoff, Mathf.Pow(f, 0.7f)) : OpenCutoff;
            i._loopPanTarget = Mathf.Clamp(pan, -1f, 1f);
            i._loopHasTarget = true;
            if (far == null) { if (i._loopFar.isPlaying) i._loopFar.Stop(); }
            else if (i._loopFar.clip != far || !i._loopFar.isPlaying)
            {
                i._loopFar.clip = far;
                i._loopFar.volume = i._loopFarVolTarget;
                i._loopFar.pitch = i._loopPitchTarget;
                i._loopFar.panStereo = i._loopPanTarget;
                i._loopFar.Play();
            }
            if (i._loop.clip == clip && i._loop.isPlaying) return;
            // A NEW loop starts AT its target rather than easing up from silence: the ease
            // is for changes within a held action, not for its beginning, and fading every
            // pour in over a fifth of a second would read as a late sound.
            i._loop.clip = clip;
            i._loop.volume = i._loopVolTarget;
            i._loop.pitch = i._loopPitchTarget;
            i._loop.panStereo = i._loopPanTarget;
            i._loopLp.cutoffFrequency = i._loopCutTarget;
            i._loop.Play();
        }

        private float _loopVolTarget, _loopFarVolTarget, _loopPitchTarget = 1f, _loopCutTarget = OpenCutoff, _loopPanTarget;
        private bool _loopHasTarget;

        /// <summary>
        /// THE VESSEL MOVING (2026-09-15, the author: "Bardağın hareketine ... göre değişen sese ihtiyacımız var"): a held
        /// layer beside the pour for a catching glass or tin — its drink swashing and its foot on the counter. Call it every
        /// frame the vessel can move, with <paramref name="amount"/> 0..1 from its speed and its rocking; level, pitch and
        /// brightness climb with it, and <paramref name="pan"/> follows it across the bench. A frame without a call fades it
        /// out, so a stage that stops driving it cannot leave it running.
        /// </summary>
        public static void HoldMotion(string name, float amount, float pan = 0f)
        {
            var i = Instance;
            var clip = name != null ? i.Clip(name) : null;
            float a = clip != null ? Mathf.Clamp01(amount) : 0f;
            i._motionVolTarget = MotionLevel * a * Sound.Effective;
            i._motionPitchTarget = 0.9f + 0.25f * a;
            i._motionCutTarget = Mathf.Lerp(MotionStillCutoff, OpenCutoff, a * a);
            i._motionPanTarget = Mathf.Clamp(pan, -1f, 1f);
            i._motionFrame = Time.frameCount;
            if (clip == null || a <= 0.001f || (i._motion.clip == clip && i._motion.isPlaying)) return;
            // A slide starts from rest, so this one DOES ease up from silence.
            i._motion.clip = clip;
            i._motion.volume = 0f;
            i._motion.pitch = i._motionPitchTarget;
            i._motion.panStereo = i._motionPanTarget;
            i._motionLp.cutoffFrequency = i._motionCutTarget;
            i._motion.Play();
        }

        private float _motionVolTarget, _motionPitchTarget = 1f, _motionCutTarget = OpenCutoff, _motionPanTarget;
        private int _motionFrame = -10;

        /// <summary>The bar bed. Call every frame with whether a stage is open; the volume
        /// eases toward loud or ducked, so menus muffle the room instead of gating it.</summary>
        public static void Ambience(bool ducked)
        {
            var i = Instance;
            // THE ROOM, NOT THE MUSIC (2026-09-15): this bed WAS the music until the music got a channel of its own
            // (Music). It is the bar's murmur while the night is on and the empty room once it is over, with the rain on
            // the window under both. A bed with no file stays silent, as every clip here does.
            var rain = i.Clip("ambience_rain");
            if (rain != null)
            {
                if (i._rain.clip != rain) i._rain.clip = rain;
                if (!i._rain.isPlaying) i._rain.Play();
                i._rainTarget = RainLevel * (ducked ? BedDuck : 1f) * Sound.Effective;
            }
            var bed = i.Clip(i._mood == "dayend" || i._mood == "closed" ? "ambience_empty" : "ambience_crowd");
            if (i._ambience.clip != bed)
            {
                i._ambienceTarget = 0f;                                   // the old bed goes before the new one comes
                if (i._ambience.clip != null && i._ambience.isPlaying && i._ambience.volume > 0.001f) return;
                i._ambience.clip = bed;
                i._ambience.volume = 0f;
            }
            if (bed == null) return;
            // KEEP IT PLAYING, not merely ASSIGNED (2026-08-27). This started the bed
            // only on the frame the clip was first loaded, so anything that stopped the
            // source afterwards stopped the music for the rest of the session and
            // nothing noticed: an AudioClip reimported while the editor is in play mode
            // does it (measured — `playing=False` with the clip still attached), and so
            // would a device change or a scene load. Checked every frame because this is
            // already called every frame, and isPlaying is a field read.
            if (!i._ambience.isPlaying) i._ambience.Play();
            i._ambienceTarget = BedLevel * (ducked ? BedDuck : 1f) * (i._mood == "story" ? 0.5f : 1f) * Sound.Effective;
        }

        /// <summary>
        /// A clip recorded in takes — `name_1`, `name_2` ... up to the first missing number — plays them in turn, so five
        /// clinks in a row are five glasses (SES_LISTESI §2.5); in order, never rolled, the house rule. Without takes, the
        /// one clip by its own name.
        /// </summary>
        private AudioClip Take(string name)
        {
            if (!_takes.TryGetValue(name, out var takes))
            {
                var found = new List<AudioClip>();
                for (int n = 1; n <= 8; n++)
                {
                    var c = Clip(name + "_" + n);
                    if (c == null) break;
                    found.Add(c);
                }
                takes = found.ToArray();
                _takes[name] = takes;
            }
            if (takes.Length == 0) return Clip(name);
            _takeAt.TryGetValue(name, out int at);
            _takeAt[name] = at + 1;
            return takes[at % takes.Length];
        }

        // ── THE MUSIC (2026-09-15, the author: "Eksik olan sesleri ve müzikleri güncelleyelim ... arkaplanda biraz daha
        // 80ler elektronik jazz olmalı rahatlatıcı bir oyun olmalı") ─────────────────────────────────────────────────
        /// <summary>The music's level at full, under the effects: a bed you notice is too loud.</summary>
        private const float MusicLevel = 0.55f;
        /// <summary>How much of the music stays while a bench or a card has the player's attention.</summary>
        private const float MusicDuck = 0.6f;
        /// <summary>Seconds a track takes to fade into the next, and one mood into another.</summary>
        private const float MusicFade = 3f;
        /// <summary>The room's murmur and the rain under it at full; both drop to BedDuck while a stage is open.</summary>
        private const float BedLevel = 0.35f, RainLevel = 0.18f, BedDuck = 0.4f;
        private const int MaxTracks = 12;

        /// <summary>
        /// The bar's music, called every frame with its mood — "night", "lastcall", "story", "dayend" or "closed". A
        /// mood's tracks are `music_{mood}_1`, `_2` ... in Resources/Audio, up to the first missing number. They play in
        /// that order, never shuffled (the house rule), each fading into the next, and a mood keeps its place, so the
        /// night picks up where it left off after the books. A mood with no tracks borrows the nearest one's (closed →
        /// dayend → night, story → lastcall → night), and a game with no music at all keeps the old synthesised bed.
        /// </summary>
        public static void Music(string mood, bool ducked)
        {
            var i = Instance;
            i._musicDucked = ducked;
            if (mood == i._mood) return;
            i._mood = mood;
            i.NextTrack();
        }

        private List<AudioClip> Playlist(string mood)
        {
            if (_playlists.TryGetValue(mood, out var list)) return list;
            list = new List<AudioClip>();
            for (int n = 1; n <= MaxTracks; n++)
            {
                var c = Clip("music_" + mood + "_" + n);
                if (c == null) break;
                list.Add(c);
            }
            _playlists[mood] = list;
            return list;
        }

        private static string MusicFallback(string mood) =>
            mood == "closed" ? "dayend" : mood == "story" ? "lastcall" : mood == "night" ? null : "night";

        /// <summary>Starts the mood's next track on the quiet source; StepMusic fades it up and the other down.</summary>
        private void NextTrack()
        {
            string from = _mood;
            var list = from != null ? Playlist(from) : null;
            while (from != null && list.Count == 0)
            {
                from = MusicFallback(from);
                list = from != null ? Playlist(from) : null;
            }
            AudioClip clip;
            if (from != null)
            {
                _musicCursor.TryGetValue(from, out int at);
                clip = list[at % list.Count];
                _musicCursor[from] = at + 1;
            }
            else clip = Clip("ambience_loop");
            if (clip == null) return;
            var incoming = _music[1 - _musicActive];
            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.loop = clip.length <= MusicFade * 2f;
            incoming.Play();
            _musicActive = 1 - _musicActive;
            _trackStartedAt = Time.unscaledTime;
        }

        private void StepMusic(float dt)
        {
            if (_music == null || _mood == null) return;
            var active = _music[_musicActive];
            var fading = _music[1 - _musicActive];
            float rate = dt * MusicLevel / MusicFade;
            active.volume = Mathf.MoveTowards(active.volume,
                MusicLevel * Sound.Effective * (_musicDucked ? MusicDuck : 1f), rate);
            fading.volume = Mathf.MoveTowards(fading.volume, 0f, rate);
            if (fading.isPlaying && fading.volume <= 0.0001f) fading.Stop();
            // The next track begins its fade before this one ends, so the night never stops; a track that stopped anyway
            // (a device change, a reimport in play) is picked up the same way — once it has had time to start.
            if (active.clip != null && !active.loop && Time.unscaledTime - _trackStartedAt > MusicFade
                && (!active.isPlaying || active.clip.length - active.time <= MusicFade))
                NextTrack();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_ambience != null && _ambience.clip != null)
                _ambience.volume = Mathf.MoveTowards(_ambience.volume, _ambienceTarget,
                    dt * 0.9f);
            if (_rain != null && _rain.clip != null)
                _rain.volume = Mathf.MoveTowards(_rain.volume, _rainTarget, dt * 0.5f);
            StepMusic(dt);
            // The held loop chases its level and its pitch instead of jumping to them.
            // Pitch is chased HALF as fast as volume: a level that lags is unnoticeable,
            // while a pitch that snaps is a warble you cannot un-hear.
            if (_loopHasTarget && _loop != null && _loop.isPlaying)
            {
                _loop.volume = Mathf.MoveTowards(_loop.volume, _loopVolTarget, dt * 2.6f);
                _loop.pitch = Mathf.MoveTowards(_loop.pitch, _loopPitchTarget, dt * 1.3f);
                _loop.panStereo = Mathf.MoveTowards(_loop.panStereo, _loopPanTarget, dt * 3f);
                // A cutoff is heard by its ratio, so it eases in octaves: a linear walk would crawl through the
                // treble and rush the bass.
                if (_loopLp != null) _loopLp.cutoffFrequency = EaseCutoff(_loopLp.cutoffFrequency, _loopCutTarget, dt);
                if (_loopFar != null && _loopFar.isPlaying)
                {
                    _loopFar.volume = Mathf.MoveTowards(_loopFar.volume, _loopFarVolTarget, dt * 2.6f);
                    _loopFar.pitch = _loop.pitch;
                    _loopFar.panStereo = _loop.panStereo;
                }
            }
            if (_motion != null && _motion.isPlaying)
            {
                bool driven = Time.frameCount - _motionFrame <= 1;
                float target = driven ? _motionVolTarget : 0f;
                _motion.volume = Mathf.MoveTowards(_motion.volume, target, dt * 2.2f);
                _motion.pitch = Mathf.MoveTowards(_motion.pitch, _motionPitchTarget, dt * 1.3f);
                _motion.panStereo = Mathf.MoveTowards(_motion.panStereo, _motionPanTarget, dt * 3f);
                if (_motionLp != null) _motionLp.cutoffFrequency = EaseCutoff(_motionLp.cutoffFrequency, _motionCutTarget, dt);
                if (target <= 0.0001f && _motion.volume <= 0.0001f) _motion.Stop();
            }
        }

        private static float EaseCutoff(float now, float target, float dt) =>
            Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(now, 10f)), Mathf.Log(Mathf.Max(target, 10f)), 1f - Mathf.Exp(-8f * dt)));
    }
}
