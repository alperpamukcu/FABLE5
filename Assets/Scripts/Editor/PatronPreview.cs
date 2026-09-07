using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// THE CAST, PLAYING (2026-09-07, the author: "tüm karakterleri ve animasyonlarını
    /// inceleyebileceğim bir önizleme kur, hepsinin animasyonlarını ve görsellerini oyun
    /// içindense oradan kontrol edeyim").
    ///
    /// Twenty-four people, eight clips each, and until now the only way to look at any of it
    /// was to enter play mode and wait for the right customer to walk in and be annoyed. This
    /// is a window: the cast down the left, the selected patron playing on the right at the
    /// GAME'S OWN RATE, and a contact sheet that lays the whole cast out in one frame.
    ///
    /// IT PLAYS THE GAME'S TIMING, not a preview's. PatronFps (12) and the drink's hold chart
    /// live in TycoonHud, which the editor assembly may not reference — so the two numbers are
    /// restated here with the reason written down, and a test in the Play suite would be the
    /// place to pin them if they ever drift. What matters for looking at a clip is that the
    /// walk loops, the one-shots run once and hold their last frame (which is the idle pose,
    /// because every one-shot is drawn in two halves), and the drink sips on the chart.
    /// </summary>
    public sealed class PatronPreview : EditorWindow
    {
        private const string PatronRoot = "Assets/Resources/Patron";
        private const float Fps = 12f;                 // TycoonHud.PatronFps
        private const float DrinkCycleSeconds = 4.4f;  // TycoonHud.DrinkCycleSeconds

        private static readonly string[] Clips =
            { "idle", "order", "drink", "cheer", "upset", "walk", "look_right", "look_left" };

        private sealed class Patron
        {
            public string Slug;
            public readonly Dictionary<string, Sprite[]> Frames = new Dictionary<string, Sprite[]>();
            public Sprite Face;
            public int Missing;          // clips with no frames at all
            public int BlackPixels = -1; // measured on demand: the keyline gate
        }

        private readonly List<Patron> _cast = new List<Patron>();
        private int _picked;
        private int _clip = 1;           // order: the clip with the most to look at
        private bool _playing = true;
        private bool _loop = true;
        private float _t;
        private double _lastTick;
        private int _zoom = 2;
        private Vector2 _listScroll, _bodyScroll;
        private bool _sheetMode;
        private int _sheetClip = 1;
        private float _sheetT;
        private string _filter = "";

        [MenuItem("LastCall/Patron Preview")]
        public static void Open()
        {
            var w = GetWindow<PatronPreview>("Patrons");
            w.minSize = new Vector2(720, 480);
            w.Reload();
        }

        private void OnEnable()
        {
            _lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            if (_cast.Count == 0) Reload();
        }

        private void OnDisable() => EditorApplication.update -= Tick;

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTick);
            _lastTick = now;
            if (!_playing) return;
            _t += dt;
            _sheetT += dt;
            Repaint();
        }

        private void Reload()
        {
            _cast.Clear();
            if (!Directory.Exists(PatronRoot)) return;
            foreach (var dir in Directory.GetDirectories(PatronRoot).OrderBy(d => d))
            {
                var p = new Patron { Slug = Path.GetFileName(dir) };
                foreach (var clip in Clips)
                {
                    var frames = LoadClip(p.Slug, clip);
                    if (frames.Length == 0) p.Missing++;
                    p.Frames[clip] = frames;
                }
                p.Face = AssetDatabase.LoadAssetAtPath<Sprite>($"{PatronRoot}/{p.Slug}/face.png");
                _cast.Add(p);
            }
            _picked = Mathf.Clamp(_picked, 0, Mathf.Max(0, _cast.Count - 1));
        }

        private static Sprite[] LoadClip(string slug, string clip)
        {
            string dir = $"{PatronRoot}/{slug}/{clip}";
            if (!Directory.Exists(dir)) return Array.Empty<Sprite>();
            return Directory.GetFiles(dir, "*.png")
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .ToArray();
        }

        /// <summary>The game's own frame law, restated: the walk loops, the drink runs on its
        /// hold chart, everything else plays once and holds the last frame — which is the idle
        /// pose, since every one-shot is two halves joined.</summary>
        private static int FrameIndex(string clip, float t, int n, bool loop)
        {
            if (n <= 1) return 0;
            if (clip == "walk") return Mathf.FloorToInt(t * Fps) % n;
            if (clip == "drink")
            {
                float u = Mathf.Repeat(t, DrinkCycleSeconds) * Fps;
                int acc = 0;
                for (int i = 0; i < n; i++)
                {
                    acc += DrinkTicks(i, n);
                    if (u < acc) return i;
                }
                return n - 1;
            }
            if (loop)
            {
                // A one-shot, looped for LOOKING at it: the clip's own length plus a beat of
                // rest on the last frame, so the eye can see where it lands before it restarts.
                float cycle = n / Fps + 0.8f;
                t = Mathf.Repeat(t, cycle);
            }
            return Mathf.Min(n - 1, Mathf.FloorToInt(t * Fps));
        }

        /// <summary>TycoonHud's drink chart: the sip is the middle frame and it is held.</summary>
        private static int DrinkTicks(int i, int n)
        {
            int mid = (n - 1) / 2;
            int d = Mathf.Abs(i - mid);
            if (d <= 1) return 5;
            if (d <= 4) return 2;
            return 1;
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_cast.Count == 0)
            {
                EditorGUILayout.HelpBox("No patrons under " + PatronRoot, MessageType.Warning);
                return;
            }
            if (_sheetMode) { DrawSheet(); return; }

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawStage();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _sheetMode = GUILayout.Toggle(_sheetMode, _sheetMode ? "Whole cast" : "One patron",
                EditorStyles.toolbarButton, GUILayout.Width(90));
            if (GUILayout.Button(_playing ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(52)))
                _playing = !_playing;
            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(58)))
                _t = _sheetT = 0f;
            _loop = GUILayout.Toggle(_loop, "Loop one-shots", EditorStyles.toolbarButton, GUILayout.Width(104));
            GUILayout.Space(8);
            GUILayout.Label("Clip", GUILayout.Width(28));
            int clipIndex = _sheetMode ? _sheetClip : _clip;
            int next = EditorGUILayout.Popup(clipIndex, Clips, EditorStyles.toolbarPopup, GUILayout.Width(96));
            if (next != clipIndex) { _t = _sheetT = 0f; if (_sheetMode) _sheetClip = next; else _clip = next; }
            GUILayout.Space(8);
            GUILayout.Label("Zoom", GUILayout.Width(38));
            _zoom = Mathf.RoundToInt(GUILayout.HorizontalSlider(_zoom, 1, 4, GUILayout.Width(70)));
            GUILayout.Label(_zoom + "x", GUILayout.Width(24));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(58))) Reload();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190));
            _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (int i = 0; i < _cast.Count; i++)
            {
                var p = _cast[i];
                if (_filter.Length > 0 && p.Slug.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var row = EditorGUILayout.BeginHorizontal(i == _picked ? "SelectionRect" : GUIStyle.none,
                    GUILayout.Height(40));
                var faceRect = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
                if (p.Face != null) DrawSprite(faceRect, p.Face);
                GUILayout.BeginVertical();
                GUILayout.Label(p.Slug, EditorStyles.boldLabel);
                GUILayout.Label(p.Missing == 0 ? "8 clips" : (8 - p.Missing) + " clips · " + p.Missing + " missing",
                    EditorStyles.miniLabel);
                GUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    _picked = i;
                    _t = 0f;
                    Event.current.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStage()
        {
            var p = _cast[_picked];
            string clip = Clips[_clip];
            var frames = p.Frames[clip];
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(p.Slug + " · " + clip + " · " + frames.Length + " frames", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (frames.Length > 0)
                GUILayout.Label("frame " + (FrameIndex(clip, _t, frames.Length, _loop) + 1) + "/" + frames.Length
                    + "   " + (frames.Length / Fps).ToString("0.00") + "s at 12 fps", EditorStyles.miniLabel);
            if (GUILayout.Button("Ping folder", EditorStyles.miniButton, GUILayout.Width(84)))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    $"{PatronRoot}/{p.Slug}"));
            if (GUILayout.Button(p.BlackPixels < 0 ? "Check ink" : p.BlackPixels + " black px",
                    EditorStyles.miniButton, GUILayout.Width(96)))
                p.BlackPixels = CountBlack(p);
            EditorGUILayout.EndHorizontal();

            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);
            if (frames.Length == 0)
            {
                EditorGUILayout.HelpBox("This clip has no frames on disk.", MessageType.Warning);
            }
            else
            {
                int index = FrameIndex(clip, _t, frames.Length, _loop);
                var sprite = frames[index];
                float w = sprite.rect.width * _zoom, h = sprite.rect.height * _zoom;
                var rect = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false));
                EditorGUI.DrawRect(rect, new Color(0.14f, 0.09f, 0.19f, 1f));
                // The rig's own foot line at 210 of the 220 canvas, so a frame standing off
                // the line is visible here rather than in the room.
                float footY = rect.y + 210f / Mathf.Max(1f, sprite.rect.height) * h;
                EditorGUI.DrawRect(new Rect(rect.x, footY, rect.width, 1f), new Color(1f, 1f, 1f, 0.18f));
                DrawSprite(rect, sprite);

                GUILayout.Space(6);
                GUILayout.Label("Every frame", EditorStyles.miniBoldLabel);
                DrawStrip(frames, index);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStrip(Sprite[] frames, int lit)
        {
            const float Cell = 64f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 220f) / (Cell + 4f)));
            for (int i = 0; i < frames.Length; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + perRow, frames.Length); k++)
                {
                    var cell = GUILayoutUtility.GetRect(Cell, Cell, GUILayout.Width(Cell));
                    EditorGUI.DrawRect(cell, k == lit ? new Color(0.26f, 0.18f, 0.34f, 1f)
                                                      : new Color(0.14f, 0.09f, 0.19f, 1f));
                    DrawSprite(cell, frames[k]);
                    if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
                    {
                        _playing = false;
                        _t = k / Fps;
                        Event.current.Use();
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSheet()
        {
            string clip = Clips[_sheetClip];
            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);
            float cell = 110f * _zoom * 0.5f + 60f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(position.width / cell));
            for (int i = 0; i < _cast.Count; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + perRow, _cast.Count); k++)
                {
                    var p = _cast[k];
                    EditorGUILayout.BeginVertical(GUILayout.Width(cell - 6f));
                    var frames = p.Frames[clip];
                    var rect = GUILayoutUtility.GetRect(cell - 10f, cell - 10f);
                    EditorGUI.DrawRect(rect, new Color(0.14f, 0.09f, 0.19f, 1f));
                    if (frames.Length > 0)
                        DrawSprite(rect, frames[FrameIndex(clip, _sheetT, frames.Length, true)]);
                    if (GUILayout.Button(p.Slug, EditorStyles.miniButton))
                    {
                        _picked = k;
                        _clip = _sheetClip;
                        _sheetMode = false;
                        _t = 0f;
                    }
                    EditorGUILayout.EndVertical();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Draws a sprite fitted into a rect, point-filtered and un-stretched.</summary>
        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            var tex = sprite.texture;
            var uv = new Rect(sprite.rect.x / tex.width, sprite.rect.y / tex.height,
                              sprite.rect.width / tex.width, sprite.rect.height / tex.height);
            float aspect = sprite.rect.width / sprite.rect.height;
            float w = rect.width, h = rect.width / aspect;
            if (h > rect.height) { h = rect.height; w = rect.height * aspect; }
            var fit = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y + (rect.height - h) * 0.5f, w, h);
            var was = tex.filterMode;
            tex.filterMode = FilterMode.Point;
            GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
            tex.filterMode = was;
        }

        /// <summary>How many near-black pixels this patron still carries — the house forbids a
        /// black keyline (Tools/patron_ink.py is the fix). Counted on the idle frame only, which
        /// is the frame the ink pass is judged on.</summary>
        private static int CountBlack(Patron p)
        {
            var frames = p.Frames["idle"];
            if (frames.Length == 0 || frames[0].texture == null) return 0;
            try
            {
                var px = frames[0].texture.GetPixels32();
                int n = 0;
                foreach (var c in px)
                    if (c.a > 0 && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 14) n++;
                return n;
            }
            catch (UnityException)
            {
                return -1;   // not readable: the importer rule should have made it so
            }
        }
    }
}
