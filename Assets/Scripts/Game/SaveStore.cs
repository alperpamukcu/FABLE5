using System;
using System.Globalization;
using System.IO;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>The head of a save, for the menu's CONTINUE key: which night, how much in
    /// the till, where the standing stood, on which seed, saved when.</summary>
    [Serializable]
    public sealed class SaveSummary
    {
        public int day;
        public int money;
        public double stars;
        public string seed;
        public string savedAt;
    }

    /// <summary>
    /// THE ONE SAVE ON DISK (2026-09-26, the author: "oyuna kayıt sistemi getirilsin artık").
    /// Core writes the run down at dawn (<see cref="RunSnapshot"/>, the save point's whole
    /// story); this is the file half, and the only file half — one autosave slot, written
    /// when the market closes, cleared when the bar goes under or a fresh run starts.
    ///
    /// Atomic on purpose: the new save lands beside the old and replaces it in one move,
    /// with the old kept as <c>run.prev.json</c> — a crash mid-write costs nothing, and a
    /// save that will not parse falls back to nothing rather than to a corrupt run.
    ///
    /// The tests never touch a player's save: the PlayMode fixtures call
    /// <see cref="DisableForSession"/> beside the other session pins, and a disabled store
    /// neither reads, writes nor clears.
    /// </summary>
    public static class SaveStore
    {
        [Serializable]
        private sealed class SaveFile
        {
            public int format = 1;
            public SaveSummary summary;
            public RunSnapshot run;
        }

        private static bool s_disabled;

        /// <summary>The pin does not outlive the play it was set for: with domain reload off
        /// a static survives play sessions, and the suite's DisableForSession was still
        /// standing when the AUTHOR next pressed play — measured as an autosave that never
        /// landed (r242). The same reset every session pin in this layer carries.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession() => s_disabled = false;

        /// <summary>The fixtures' pin: this session neither reads, writes nor clears the
        /// player's save (mirrors PlayerOptions.UseDefaultsForSession).</summary>
        public static void DisableForSession() => s_disabled = true;

        private static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        private static string MainPath => Path.Combine(Dir, "run.json");
        private static string PrevPath => Path.Combine(Dir, "run.prev.json");
        private static string FreshPath => Path.Combine(Dir, "run.new.json");

        /// <summary>The dawn write (wired to <see cref="TycoonRun.ContinueToNextDay(Action{RunSnapshot})"/>
        /// by the scene's own call). Never throws out of itself — a save that cannot be
        /// written is a log line, not a lost night.</summary>
        public static void Autosave(RunSnapshot snap)
        {
            if (s_disabled || snap == null) return;
            try
            {
                Directory.CreateDirectory(Dir);
                var file = new SaveFile
                {
                    summary = new SaveSummary
                    {
                        day = snap.day,
                        money = snap.money,
                        stars = snap.StandingValue,
                        seed = snap.seed,
                        savedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    },
                    run = snap,
                };
                File.WriteAllText(FreshPath, JsonUtility.ToJson(file));
                if (File.Exists(MainPath)) File.Replace(FreshPath, MainPath, PrevPath);
                else File.Move(FreshPath, MainPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LastCall] Autosave failed: {e.Message}");
            }
        }

        /// <summary>The menu's cheap question: is there a run to continue, and what does its
        /// head say? False on no file, an unreadable file, or a version this game does not read.</summary>
        public static bool TryPeek(out SaveSummary summary)
        {
            summary = null;
            if (!TryRead(out var file)) return false;
            summary = file.summary ?? new SaveSummary
            {
                day = file.run.day, money = file.run.money,
                stars = file.run.StandingValue, seed = file.run.seed,
            };
            return true;
        }

        /// <summary>The whole snapshot, for <see cref="GameBootstrap.TryStartSavedRun"/>.</summary>
        public static bool TryLoad(out RunSnapshot snap)
        {
            snap = null;
            if (!TryRead(out var file)) return false;
            snap = file.run;
            return true;
        }

        private static bool TryRead(out SaveFile file)
        {
            file = null;
            if (s_disabled) return false;
            try
            {
                if (!File.Exists(MainPath)) return false;
                file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(MainPath));
                if (file == null || file.run == null || file.format != 1
                    || file.run.version != RunSnapshot.Version)
                {
                    Debug.Log("[LastCall] The save on disk is from a different game; leaving it be.");
                    file = null;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LastCall] The save on disk would not read: {e.Message}");
                file = null;
                return false;
            }
        }

        /// <summary>A dead or abandoned run: bankruptcy files it, and a fresh run starts
        /// clean. The previous-save backup goes with it — CONTINUE must never resurrect a
        /// bar the player watched close.</summary>
        public static void Clear()
        {
            if (s_disabled) return;
            try
            {
                if (File.Exists(MainPath)) File.Delete(MainPath);
                if (File.Exists(PrevPath)) File.Delete(PrevPath);
                if (File.Exists(FreshPath)) File.Delete(FreshPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LastCall] Clearing the save failed: {e.Message}");
            }
        }
    }
}
