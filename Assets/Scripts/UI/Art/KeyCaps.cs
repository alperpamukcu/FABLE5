using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastCall.UI
{
    /// <summary>
    /// THE AUTHOR'S KEY CAPS (2026-09-15, the author: "KEybind için ... Classic burdaki dosyalardaki görselleri
    /// kullan animasyonlu olduğundan 2 frame olabilir" — Desktop/konsept art/Classic, the Dark set, brought in by
    /// Tools/menu_pack.py). One PNG a key, two frames side by side: the cap up and the cap pressed, 17x16 a frame
    /// (a few keys wider). The settings' CONTROLS page shows the key an action is on as its cap, at 2x; the pressed
    /// frame plays while the real key is held and blinks while the row listens for a new one.
    ///
    /// A key the pack has no cap for — Escape, the function row, the numpad, the brackets — is drawn on the pack's
    /// blank cap (EMPTY1 for one letter, EMPTY2 for a word) and the caller letters it in the cap's own ink.
    /// </summary>
    public static class KeyCaps
    {
        /// <summary>A frame's height; every cap in the set is this tall.</summary>
        public const int Height = 16;

        /// <summary>The colour the pack prints its letters in.</summary>
        public static readonly Color Ink = new Color32(242, 240, 229, 255);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Sheets = new Dictionary<string, Texture2D>();

        /// <summary>The cap for <paramref name="key"/>, up or pressed; <paramref name="blank"/> says the pack had no
        /// cap for it and the caller must print the word. Null only when the pack is missing altogether.</summary>
        public static Sprite Cap(Key key, bool pressed, out bool blank)
        {
            string name = Name(key, out blank);
            string id = "cap:" + name + (pressed ? ":down" : ":up");
            if (Cache.TryGetValue(id, out var got) && got != null) return got;
            if (!Sheets.TryGetValue(name, out var tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>("Keys/" + name);
                if (tex != null) Sheets[name] = tex; else { Sheets.Remove(name); return null; }
            }
            int w = tex.width / 2;
            var s = Sprite.Create(tex, new Rect(pressed ? w : 0, 0, w, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = id;
            return Cache[id] = s;
        }

        /// <summary>The pack's file for a key; the blank caps for what it does not have.</summary>
        private static string Name(Key key, out bool blank)
        {
            blank = false;
            switch (key)
            {
                case Key.Space: return "SPACE";
                case Key.Tab: return "TAB";
                case Key.Backspace: return "BACKSPACE";
                case Key.CapsLock: return "CAPS";
                case Key.LeftShift: case Key.RightShift: return "SHIFT";
                case Key.LeftCtrl: case Key.RightCtrl: return "CTRL";
                case Key.LeftAlt: return "ALT";
                case Key.RightAlt: return "ALTGR";
                case Key.LeftWindows: case Key.RightWindows: return "WINDOWS";
                case Key.LeftArrow: return "ARROWLEFT";
                case Key.RightArrow: return "ARROWRIGHT";
                case Key.UpArrow: return "ARROWUP";
                case Key.DownArrow: return "ARROWDOWN";
                case Key.Semicolon: return "COLON";
                case Key.Quote: return "QUOTE";
                case Key.Backquote: return "TILDE";
                case Key.Comma: return "LESSTHAN";
                case Key.Period: return "GREATERTHAN";
                case Key.Slash: return "QUESTIONMARK";
                case Key.Backslash: return "PIPE";
                case Key.Equals: return "PLUS";
            }
            string s = key.ToString();
            if (s.Length == 1 && s[0] >= 'A' && s[0] <= 'Z') return s;
            if (s.Length == 6 && s.StartsWith("Digit")) return s.Substring(5);
            blank = true;
            return Keys.Label(key).Length <= 1 ? "EMPTY1" : "EMPTY2";
        }
    }
}
