using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// THE TRAILER'S CAMERA (2026-10-02, the author: "videoyu sen kayıt et sen oluştur"). Records the Game view to an
    /// MP4 while a trailer shot plays itself (TrailerShots), so the footage is the real game under a scripted hand and
    /// can be taken again, identically, whenever the game changes.
    ///
    /// Three choices, each paid for by a property of this project:
    ///
    ///  1. THE CLOCK IS THE CAMERA'S. Time.captureDeltaTime steps the game exactly 1/fps per rendered frame, so a slow
    ///     capture never shows as a stutter and the file plays at true speed. Nearly everything the UI animates runs on
    ///     unscaled time, and the camera MEASURES whether that clock follows the step on this editor before it trusts
    ///     it: if it does not, it drops the step and keeps time by the wall clock instead (duplicating a frame when the
    ///     capture falls behind), which is uglier but never fast-forwards an animation. The choice is written to the
    ///     shot's .json and to the console.
    ///  2. THE HAND IS DRAWN IN. The game's cursor is a hardware cursor (CursorSkin), which no screen capture sees, and
    ///     a trailer of a mouse game with no hand in it reads as a demo reel. The camera stamps the game's own cursor
    ///     art at the virtual mouse, pressed while the button is down, at 2x on a 1080 frame like the game's large hand.
    ///  3. NO SOUND. The audio engine runs on the wall clock, not on the stepped one, so captured sound would drift off
    ///     the picture; the edit lays music and effects in afterwards.
    ///
    /// The shot marks its beats (Mark) - the card opening, the first drop, the serve - and the marks are written beside
    /// the film as seconds, so the edit can cut on them instead of by eye.
    /// </summary>
    public sealed class TrailerCamera : MonoBehaviour
    {
        public const int Fps = 60;

        private static TrailerCamera _live;

        private string _path;
        private int _frames;
        private bool _stopping, _clocked = true, _decided;
        private int _probeFrames;
        private float _probeSum;
        private float _wallStart;
        private readonly List<string> _marks = new List<string>();
        private Texture2D _cursorIdle, _cursorPressed;
        private int _cursorScale = 2;
#if UNITY_EDITOR
        private UnityEditor.Media.MediaEncoder _encoder;
#endif

        /// <summary>Seconds of film recorded so far (0 when nothing is rolling).</summary>
        public static float Seconds => _live != null ? _live._frames / (float)Fps : 0f;

        public static bool Rolling => _live != null && !_live._stopping;

        public static string Folder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", "Trailer"));

        /// <summary>Starts rolling into Recordings/Trailer/{shot}.mp4. The Game view must already be at the size
        /// the film is wanted at (TrailerShots pins it to 1920x1080).</summary>
        public static void Roll(string shot)
        {
            if (_live != null) Cut();
            var go = new GameObject("TrailerCamera");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _live = go.AddComponent<TrailerCamera>();
            _live.Begin(shot);
        }

        /// <summary>A beat in the shot, written to the .json as the second it happened at.</summary>
        public static void Mark(string what)
        {
            if (_live == null) return;
            _live._marks.Add($"    {{ \"t\": {Seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}, \"mark\": \"{what.Replace("\"", "'")}\" }}");
            Debug.Log($"[trailer] {Seconds:0.00}s {what}");
        }

        /// <summary>Stops and closes the file. Safe to call when nothing is rolling.</summary>
        public static void Cut()
        {
            if (_live == null) return;
            _live.Finish();
            Destroy(_live.gameObject);
            _live = null;
        }

        private void Begin(string shot)
        {
            Directory.CreateDirectory(Folder);
            _path = Path.Combine(Folder, shot + ".mp4");
            if (File.Exists(_path)) File.Delete(_path);
            _cursorIdle = Readable("cursor_hand");
            _cursorPressed = Readable("cursor_hand_pressed") ?? _cursorIdle;
            _cursorScale = Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
#if UNITY_EDITOR
            var video = new UnityEditor.Media.VideoTrackAttributes
            {
                frameRate = new UnityEditor.Media.MediaRational(Fps),
                width = (uint)(Screen.width & ~1),
                height = (uint)(Screen.height & ~1),
                includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
            };
            _encoder = new UnityEditor.Media.MediaEncoder(_path, video);
#endif
            Time.captureDeltaTime = 1f / Fps;
            _wallStart = Time.realtimeSinceStartup;
            Debug.Log($"[trailer] rolling {shot} at {Screen.width}x{Screen.height}");
            StartCoroutine(Pump());
        }

        private IEnumerator Pump()
        {
            var end = new WaitForEndOfFrame();
            while (!_stopping)
            {
                yield return end;
                if (_stopping) break;
                if (!_decided) Probe();
                if (_clocked)
                {
                    Shoot(1);
                }
                else
                {
                    // the wall keeps the time: as many frames as the clock says are due, the same picture repeated
                    int due = Mathf.FloorToInt((Time.realtimeSinceStartup - _wallStart) * Fps) - _frames;
                    if (due > 0) Shoot(Mathf.Min(due, Fps));
                }
            }
        }

        /// <summary>Does the unscaled clock follow the stepped one on this editor? Thirty frames decide it.</summary>
        private void Probe()
        {
            // the first few frames still carry the step being set; the next thirty are the verdict
            if (++_probeFrames <= 5) return;
            _probeSum += Mathf.Abs(Time.unscaledDeltaTime - 1f / Fps);
            if (_probeFrames < 35) return;
            _decided = true;
            _clocked = _probeSum / (_probeFrames - 5) < 0.002f;
            if (!_clocked)
            {
                Time.captureDeltaTime = 0f;
                _wallStart = Time.realtimeSinceStartup - _frames / (float)Fps;
            }
            Mark(_clocked ? "clock: stepped (1/60 per frame)" : "clock: wall (unscaled time ignores the step here)");
        }

        private void Shoot(int copies)
        {
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) return;
            var frame = shot;
            if (shot.format != TextureFormat.RGBA32 || (shot.width & 1) != 0 || (shot.height & 1) != 0)
            {
                frame = new Texture2D(shot.width & ~1, shot.height & ~1, TextureFormat.RGBA32, false);
                frame.SetPixels(shot.GetPixels(0, 0, frame.width, frame.height));
                frame.Apply(false);
            }
            StampHand(frame);
#if UNITY_EDITOR
            for (int i = 0; i < copies; i++) _encoder.AddFrame(frame);
#endif
            _frames += copies;
            if (frame != shot) Destroy(frame);
            Destroy(shot);
        }

        /// <summary>The game's own hand, at the virtual mouse, hotspot (3,1) like CursorSkin's.</summary>
        private void StampHand(Texture2D frame)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var art = mouse.leftButton.isPressed ? _cursorPressed : _cursorIdle;
            if (art == null) return;
            Vector2 at = mouse.position.ReadValue();
            int s = _cursorScale;
            var src = art.GetPixels32();
            var dst = frame.GetRawTextureData<Color32>();
            int fw = frame.width, fh = frame.height, aw = art.width, ah = art.height;
            // screen y runs up from the bottom, like the texture's rows; the art's row 0 is its bottom
            int left = Mathf.RoundToInt(at.x) - 3 * s;
            int top = Mathf.RoundToInt(at.y) + 1 * s;
            for (int ay = 0; ay < ah; ay++)
            for (int ax = 0; ax < aw; ax++)
            {
                var c = src[ay * aw + ax];
                if (c.a == 0) continue;
                for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                {
                    int x = left + ax * s + dx;
                    int y = top - (ah - ay) * s + dy;
                    if (x < 0 || y < 0 || x >= fw || y >= fh) continue;
                    int k = y * fw + x;
                    if (c.a == 255) { dst[k] = c; continue; }
                    var b = dst[k];
                    float a = c.a / 255f;
                    dst[k] = new Color32((byte)(c.r * a + b.r * (1 - a)), (byte)(c.g * a + b.g * (1 - a)),
                                         (byte)(c.b * a + b.b * (1 - a)), 255);
                }
            }
        }

        /// <summary>A CPU copy of an Items sprite, whatever its import settings say about read/write.</summary>
        private static Texture2D Readable(string name)
        {
            var sprite = Resources.Load<Sprite>("Items/" + name);
            if (sprite == null) return null;
            var r = sprite.textureRect;
            var rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sprite.texture, rt);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0, false);
            copy.Apply(false);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private void Finish()
        {
            _stopping = true;
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            _encoder?.Dispose();
            _encoder = null;
#endif
            var json = new StringBuilder();
            json.Append("{\n  \"film\": \"").Append(Path.GetFileName(_path)).Append("\",\n");
            json.Append("  \"fps\": ").Append(Fps).Append(",\n");
            json.Append("  \"seconds\": ").Append(Seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"clock\": \"").Append(_clocked ? "stepped" : "wall").Append("\",\n");
            json.Append("  \"marks\": [\n").Append(string.Join(",\n", _marks)).Append("\n  ]\n}\n");
            File.WriteAllText(Path.ChangeExtension(_path, ".json"), json.ToString());
            Debug.Log($"[trailer] cut {Path.GetFileName(_path)}: {Seconds:0.0}s, {_frames} frames, clock {(_clocked ? "stepped" : "wall")}");
            if (_cursorIdle != null) Destroy(_cursorIdle);
            if (_cursorPressed != null && _cursorPressed != _cursorIdle) Destroy(_cursorPressed);
        }

        private void OnDestroy()
        {
            if (_live == this && !_stopping) Finish();
            if (_live == this) _live = null;
        }
    }
}
