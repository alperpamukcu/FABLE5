namespace LastCall.UI
{
    /// <summary>
    /// THE MUSIC ON THE BEAM (2026-09-15, the author: "oyunun üst barına müzikleri geçebileceğimiz bir müzik oynatıcı
    /// ekleyelim").
    ///
    /// THE JUKEBOX KEY (2026-09-28, the register under the neon). The player was a 300-unit well of three 22 keys, a note,
    /// the song over its list and four still level bars - the widest thing on the beam, and none of it about the night
    /// being served; it became one key, the next record on a press.
    ///
    /// ...AND OFF THE BEAM (2026-09-28, second pass, the author: "butonları kaldır sadece yıldız gözüksün"). The music
    /// is where it always also was: the next record and hold on their own keys (Keys' NextTrack and MusicToggle, read in
    /// UpdateHotkeys), and Settings → AUDIO's player, which has the list, the seek and both skips. What stays here are
    /// the music's words, which that player speaks.
    /// </summary>
    public sealed partial class TycoonHud
    {
        /// <summary>A song's title from the string table (music.night_1 ...), or its file tail while it has none.</summary>
        private static string SongTitle(string song) =>
            string.IsNullOrEmpty(song) ? "" : UIText.T("music." + song);

        private static string MoodWord(string mood) =>
            mood == "night" ? UIText.T("build.player.mood_night")
            : mood == "lastcall" ? UIText.T("build.player.mood_lastcall")
            : mood == "story" ? UIText.T("build.player.mood_story")
            : mood == "dayend" ? UIText.T("build.player.mood_dayend")
            : UIText.Caps(mood);
    }
}
