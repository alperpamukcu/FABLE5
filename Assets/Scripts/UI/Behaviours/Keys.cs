using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastCall.UI
{
    /// <summary>What a key can do in the bar. The order is the order the CONTROLS page lists them in.</summary>
    public enum KeyAction { Pause, Book, Cellar, PageBack, PageForward, NextTrack, MusicToggle }

    /// <summary>
    /// THE KEY BINDINGS (2026-09-15, the author: "esc ekranı ve ayaralar key bind ses"). The game read three keys
    /// straight off the keyboard — Escape, and the arrows for the book's pages — and nothing else; the pause menu, the
    /// book, the cellar and the music player wanted keys of their own, and a player wants to move them. One table,
    /// PlayerPrefs-backed the way <see cref="Sound"/> and <see cref="Motion"/> are, read through <see cref="Pressed"/>
    /// so no screen touches <see cref="Keyboard"/> for a bound action itself.
    ///
    /// New Input System only (CLAUDE.md): a binding is a <see cref="Key"/>, and the keyboard is asked by that key.
    /// </summary>
    public static class Keys
    {
        private static readonly Dictionary<KeyAction, Key> Defaults = new Dictionary<KeyAction, Key>
        {
            [KeyAction.Pause] = Key.Escape,
            [KeyAction.Book] = Key.B,
            [KeyAction.Cellar] = Key.C,
            [KeyAction.PageBack] = Key.LeftArrow,
            [KeyAction.PageForward] = Key.RightArrow,
            [KeyAction.NextTrack] = Key.N,
            [KeyAction.MusicToggle] = Key.M,
        };

        private static readonly Dictionary<KeyAction, Key> Bound = new Dictionary<KeyAction, Key>();
        private static bool _loaded;

        private static string PrefKey(KeyAction action) => "lastcall.key." + action;

        private static void Load()
        {
            if (_loaded) return;
            foreach (var pair in Defaults)
            {
                string saved = PlayerPrefs.GetString(PrefKey(pair.Key), "");
                Bound[pair.Key] = saved.Length > 0 && Enum.TryParse(saved, out Key k) && k != Key.None ? k : pair.Value;
            }
            _loaded = true;
        }

        /// <summary>The key an action is on.</summary>
        public static Key Get(KeyAction action)
        {
            Load();
            return Bound[action];
        }

        /// <summary>Moves an action onto <paramref name="key"/> and remembers it. An action already on that key
        /// takes this action's old key, so two actions never share one.</summary>
        public static void Set(KeyAction action, Key key)
        {
            Load();
            if (key == Key.None) return;
            var old = Bound[action];
            foreach (var other in new List<KeyAction>(Bound.Keys))
                if (other != action && Bound[other] == key)
                {
                    Bound[other] = old;
                    PlayerPrefs.SetString(PrefKey(other), old.ToString());
                }
            Bound[action] = key;
            PlayerPrefs.SetString(PrefKey(action), key.ToString());
        }

        /// <summary>Every action back on its first key.</summary>
        public static void ResetAll()
        {
            Load();
            foreach (var pair in Defaults)
            {
                Bound[pair.Key] = pair.Value;
                PlayerPrefs.DeleteKey(PrefKey(pair.Key));
            }
        }

        /// <summary>Was the action's key pressed this frame? False with no keyboard.</summary>
        public static bool Pressed(KeyAction action)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var control = kb[Get(action)];
            return control != null && control.wasPressedThisFrame;
        }

        /// <summary>The key pressed this frame, for a binding being listened for — or None. Modifier keys alone
        /// and the keys the interface needs for itself (Escape cancels the listen) are not offered.</summary>
        public static Key AnyPressed()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.anyKey.wasPressedThisFrame) return Key.None;
            foreach (var control in kb.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                var key = control.keyCode;
                if (key == Key.None || key == Key.Escape || key == Key.LeftShift || key == Key.RightShift
                    || key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt
                    || key == Key.LeftWindows || key == Key.RightWindows || key == Key.LeftMeta || key == Key.RightMeta)
                    continue;
                return key;
            }
            return Key.None;
        }

        /// <summary>The word on a key cap: short, in capitals, the way a keyboard prints it.</summary>
        public static string Label(Key key)
        {
            switch (key)
            {
                case Key.Escape: return "ESC";
                case Key.Space: return "SPACE";
                case Key.Enter: return "ENTER";
                case Key.Tab: return "TAB";
                case Key.Backspace: return "BKSP";
                case Key.LeftArrow: return "LEFT";
                case Key.RightArrow: return "RIGHT";
                case Key.UpArrow: return "UP";
                case Key.DownArrow: return "DOWN";
                case Key.LeftShift: return "SHIFT";
                case Key.RightShift: return "R SHIFT";
                case Key.LeftCtrl: return "CTRL";
                case Key.RightCtrl: return "R CTRL";
                case Key.LeftAlt: return "ALT";
                case Key.RightAlt: return "R ALT";
                case Key.Delete: return "DEL";
                case Key.Insert: return "INS";
                case Key.Home: return "HOME";
                case Key.End: return "END";
                case Key.PageUp: return "PG UP";
                case Key.PageDown: return "PG DN";
                case Key.CapsLock: return "CAPS";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                case Key.Comma: return ",";
                case Key.Period: return ".";
                case Key.Slash: return "/";
                case Key.Backslash: return "\\";
                case Key.Semicolon: return ";";
                case Key.Quote: return "'";
                case Key.LeftBracket: return "[";
                case Key.RightBracket: return "]";
                case Key.Backquote: return "`";
            }
            string s = key.ToString();
            if (s.StartsWith("Digit")) return s.Substring(5);
            if (s.StartsWith("Numpad")) return "NUM " + s.Substring(6).ToUpperInvariant();
            return s.ToUpperInvariant();
        }
    }
}
