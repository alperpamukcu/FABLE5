using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE MUSIC PLAYER ON THE BEAM (2026-09-15, the author: "oyunun üst barına müzikleri geçebileceğimiz bir müzik
    /// oynatıcı ekleyelim"). The beam's middle stood empty between the night's well and the house strips; a well of the
    /// same make now sits there with three small keys — the song before, hold, the song after — a note mark, the song's
    /// title over which list it is on and its place in it, and four still level bars. Still, because the beam is in
    /// the look tests' pictures and nothing on it may animate at rest. The keys drive Sfx's player; the words come off
    /// it every frame (RefreshMusicPlayer), and under the pointer the well says what it is and what the keys do.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _playerWell;
        private Text _playerTitle, _playerPlace;
        private Image _playerHoldMark;
        private string _playerShown;   // the song the words are for; rewritten only when it changes

        // COMPACT (2026-09-22, the author's eighth list: "müzik çalar daha da kompakt hale getirilebilir"): 22 keys four
        // apart, the song and its place in one short column, the meter close behind - 300 at most, and narrower when
        // the beam's other wells need the room (TycoonHud.TopBar's LayTopBar calls FitMusicPlayer).
        private const float PlayerW = 300f, PlayerH = 42f, PlayerX = -44f, PlayerKeySize = 22f, PlayerKeyGap = 4f;
        private const float PlayerMeterW = 30f;
        private float _playerTextX;

        private void BuildMusicPlayer(RectTransform top)
        {
            _playerWell = NewRect("Player", top);
            Place(_playerWell, new Vector2(0.5f, 0.5f), new Vector2(PlayerW, PlayerH), new Vector2(PlayerX, 0));
            var well = _playerWell.gameObject.AddComponent<Image>();
            well.sprite = ChromeArt.Well();
            well.type = Image.Type.Sliced;
            well.color = Color.white;
            well.raycastTarget = true;

            float kx = 8f;
            PlayerKey("PREV", "prev", kx, () => { Sfx.SkipTrack(-1); Sfx.Play("click"); }); kx += PlayerKeySize + PlayerKeyGap;
            var hold = PlayerKey("HOLD", "pause", kx, () => { Sfx.MusicPaused = !Sfx.MusicPaused; Sfx.Play("click"); RefreshMusicPlayer(true); });
            _playerHoldMark = hold.Find("Face/Mark").GetComponent<Image>();
            kx += PlayerKeySize + PlayerKeyGap;
            PlayerKey("NEXT", "next", kx, () => { Sfx.SkipTrack(+1); Sfx.Play("click"); }); kx += PlayerKeySize + 10f;

            var note = NewRect("Note", _playerWell);
            Place(note, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(kx, 0));
            note.pivot = new Vector2(0, 0.5f);
            var ni = note.gameObject.AddComponent<Image>();
            ni.sprite = NightArt.Mark("note"); ni.color = UITheme.Magenta[3]; ni.raycastTarget = false;
            kx += 22f;
            _playerTextX = kx;

            _playerTitle = NewText("Title", _playerWell, _body, 8, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
            Place(_playerTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(PlayerW - kx - 56f, 12), new Vector2(kx, 8f));
            _playerTitle.rectTransform.pivot = new Vector2(0, 0.5f);
            _playerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            _playerPlace = NewText("Place", _playerWell, _body, 8, TextAnchor.MiddleLeft, UITheme.Magenta[3]);
            Place(_playerPlace.rectTransform, new Vector2(0, 0.5f), new Vector2(PlayerW - kx - 56f, 12), new Vector2(kx, -8f));
            _playerPlace.rectTransform.pivot = new Vector2(0, 0.5f);
            _playerPlace.horizontalOverflow = HorizontalWrapMode.Overflow;

            // four level bars, still: a picture of music, not a meter of it
            int[] bars = { 8, 14, 5, 11 };
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = NewRect("Bar" + i, _playerWell);
                Place(bar, new Vector2(1, 0), new Vector2(4, bars[i]), new Vector2(-10f - (bars.Length - 1 - i) * 6f, 11f));
                bar.pivot = new Vector2(1, 0);
                var bi = bar.gameObject.AddComponent<Image>();
                bi.color = UITheme.Magenta[3]; bi.raycastTarget = false;
            }

            HoverTip(_playerWell, NightArt.Mark("note"), UIText.T("build.player.tip_title"), () =>
            {
                var (at, of) = Sfx.NowPlayingPlace;
                return UIText.T("build.player.tip_line", ("song", SongTitle(Sfx.NowPlaying)), ("at", at), ("of", of));
            });
            RefreshMusicPlayer(true);
        }

        /// <summary>One of the player's three keys: 26 on the 42 well, so its mark is drawn at exactly 1x.</summary>
        /// <summary>The player at <paramref name="w"/> wide: the keys and the note keep their places, the song and its
        /// place take what is left before the meter, and a title longer than that is cut at a word rather than run
        /// under the bars.</summary>
        private void FitMusicPlayer(float w)
        {
            if (_playerWell == null) return;
            _playerWell.sizeDelta = new Vector2(w, PlayerH);
            float textW = Mathf.Max(40f, w - _playerTextX - PlayerMeterW - 10f);
            foreach (var t in new[] { _playerTitle, _playerPlace })
            {
                if (t == null) continue;
                t.rectTransform.sizeDelta = new Vector2(textW, t.rectTransform.sizeDelta.y);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private RectTransform PlayerKey(string id, string mark, float x, System.Action onClick)
        {
            var key = NewButton(_playerWell, id, new Vector2(0, 0.5f), new Vector2(PlayerKeySize, PlayerKeySize),
                new Vector2(x, 0), UITheme.Night[2], onClick, NightArt.Mark(mark));
            key.name = id;
            var mi = key.Find("Face/Mark").GetComponent<Image>();
            mi.color = UITheme.Cyan[4];
            return key;
        }

        /// <summary>The words on the well, from the player: rewritten when the song changes, or when asked.</summary>
        private void RefreshMusicPlayer(bool force = false)
        {
            if (_playerTitle == null) return;
            string now = Sfx.NowPlaying;
            bool held = Sfx.MusicPaused;
            string shown = (now ?? "") + (held ? "|held" : "");
            if (!force && shown == _playerShown) return;
            _playerShown = shown;
            _playerTitle.text = SongTitle(now);
            var (at, of) = Sfx.NowPlayingPlace;
            string mood = now == null ? "" : now.Substring(0, now.LastIndexOf('_') < 0 ? now.Length : now.LastIndexOf('_'));
            _playerPlace.text = of == 0 ? "" : UIText.T("build.player.place", ("mood", MoodWord(mood)), ("at", at), ("of", of));
            if (_playerHoldMark != null) _playerHoldMark.sprite = NightArt.Mark(held ? "play" : "pause");
        }

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
