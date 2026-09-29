using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A SIGN KEY (2026-09-29, the menus rebuilt in the week's language - BUILD_SPEC §2): a plate one Night step proud of
    /// the ground it stands on, a Graphite rim with the room's light on its top row, the terrazzo in its face, its neon
    /// mark at the left and its word dead centre. It runs the key's states, all by tint and two units of travel:
    ///   REST     the plate, the mark HALF-lit in its hue
    ///   POINTED  a ClubBlue tube lit round the plate's rim - the one colour the game answers a pointer with - and the
    ///            mark struck LIT (one brightening step, never a blink: FLASHES and REDUCED MOTION change nothing here)
    ///   PRESSED  the plate sinks two units and its lit row goes out
    ///   PRIMARY  the one thing to press on a screen: Amber[3] enamel, an Amber[4] lit row, an Amber[1] rim, the word
    ///            Night[1] and the mark INKED on it (the only amber a menu shows - the gold icons are gone)
    ///   DEAD     a key that cannot be pressed: a Graphite[2] outline on the ground, the word Night[4], the mark dark glass
    ///   ARMED    a key that asks first (NEW RUN, START OVER): the first press swaps its word for the question and bends a
    ///            ViceRed tube round it; a second press inside the window does the thing; left alone it goes back
    ///
    /// IT SITS BESIDE A REAL BUTTON (the critic, 2026-09-29): the smoke test reads CONTINUE's Button.interactable, and the
    /// key's own dead state is read off the same flag, so the one switch a caller throws is the Button's.
    /// </summary>
    public sealed class SignKey : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                 IPointerDownHandler, IPointerUpHandler
    {
        public Button Button;
        public RectTransform Body;
        public Image Plate, Surface;
        public Text Label;
        /// <summary>The key's mark: a 42-unit NeonIcons.View, or a 30-unit NeonIcons.SmallView on the settings' 34-tall
        /// keys - both the same tube, run by the same states.</summary>
        public NeonTube Icon;
        public NeonTube Hover;

        /// <summary>The mark's tube colour on a dark key: Magenta for the verbs, Cyan for the second line.</summary>
        public Color[] Hue = UITheme.Magenta;

        /// <summary>The amber enamel key: the one thing to press on the screen.</summary>
        public bool Primary;

        /// <summary>The Night step the key stands on (1: the field or a recess; 2: a cabinet's face). The plate is one
        /// step proud of it.</summary>
        public int Ground = 1;

        /// <summary>The key's own word, and the question it asks first (null: it does not ask).</summary>
        public string Word, AskWord;

        /// <summary>How long the question stands, in real seconds (ArmedKey's window).</summary>
        public float AskWindow = 2.6f;

        /// <summary>What the key does (after the question, for a key that asks).</summary>
        public Action Pressed;

        /// <summary>The word's pad a side: the mark's slot (6 + 42) and eight units of air.</summary>
        public const float Pad = 56f;

        /// <summary>This key's pad a side (<see cref="Pad"/> by default; 52 on the settings' 34-tall keys, whose small
        /// mark is 30 - 6 + 30 + 16 of air), which <see cref="FitWord"/> measures the word against.</summary>
        public float WordPad = Pad;

        private bool _over, _down, _shownLive = true, _armedShown;
        private float _askUntil = -1f;

        private static float s_lastTick = -1f;
        private const float TickGap = 0.09f;

        public bool Armed => _askUntil > 0f && Time.unscaledTime < _askUntil;

        /// <summary>The Button's click.</summary>
        public void Click()
        {
            if (Button != null && !Button.interactable) return;
            if (AskWord == null) { Pressed?.Invoke(); return; }
            if (Armed)
            {
                Disarm();
                Pressed?.Invoke();
                return;
            }
            _askUntil = Time.unscaledTime + AskWindow;
            Sfx.Play("click");
            Apply();
        }

        public void Disarm()
        {
            if (_askUntil < 0f) return;
            _askUntil = -1f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData _)
        {
            _over = true;
            Apply();
            if (Button != null && !Button.interactable) return;
            // the pack's keys ticked under the pointer on this gap (PackKey): a sweep down a column is one run of ticks
            if (Time.unscaledTime - s_lastTick < TickGap) return;
            s_lastTick = Time.unscaledTime;
            Sfx.Play("hover", 0.14f);
        }

        public void OnPointerExit(PointerEventData _) { _over = false; _down = false; Apply(); }
        public void OnPointerDown(PointerEventData _) { _down = true; Apply(); }
        public void OnPointerUp(PointerEventData _) { _down = false; Apply(); }

        private void OnEnable() { _over = false; _down = false; _askUntil = -1f; Apply(); }   // shown again: at rest
        private void OnDisable() { _over = false; _down = false; _askUntil = -1f; }

        private void Update()
        {
            bool live = Button == null || Button.interactable;
            bool armed = Armed;
            if (live != _shownLive || armed != _armedShown) Apply();
        }

        /// <summary>Draws the state the key is in.</summary>
        public void Apply()
        {
            bool live = Button == null || Button.interactable;
            bool armed = live && Armed;
            _shownLive = live;
            _armedShown = armed;
            bool down = live && _down;
            bool pointed = live && (_over || _down);
            var night = UITheme.Night;
            int g = Mathf.Clamp(Ground, 0, 3);

            Color face, rim, lit, word;
            Color? chip;
            if (!live)
            {
                face = night[g]; rim = UITheme.Graphite[2]; lit = face; chip = null; word = night[4];
            }
            else if (Primary)
            {
                face = UITheme.Amber[3]; rim = UITheme.Amber[1]; lit = down ? face : UITheme.Amber[4];
                chip = UITheme.Amber[2]; word = night[1];
            }
            else
            {
                face = night[g + 1]; rim = UITheme.Graphite[3]; lit = down ? face : UITheme.Graphite[4];
                chip = night[Mathf.Min(4, g + 2)]; word = UITheme.Cream[4];
            }
            if (Plate != null) Plate.sprite = MenuArt.KeyPlate(face, rim, lit);
            if (Surface != null)
            {
                Surface.enabled = chip.HasValue;
                if (chip.HasValue) Surface.color = chip.Value;
            }
            if (Body != null) Body.anchoredPosition = new Vector2(0f, down ? -2f : 0f);

            if (Icon != null)
            {
                if (!live) Icon.Show(NeonIcons.State.Dark, Hue, false);
                else if (Primary) Icon.Ink(night[1], UITheme.Amber[1]);
                else Icon.Show(pointed ? NeonIcons.State.Lit : NeonIcons.State.Half, Hue, false);
            }

            if (Hover != null)
            {
                Hover.Visible = armed || pointed;
                if (armed) Hover.Show(NeonIcons.State.Lit, UITheme.ViceRed, true);
                else if (pointed) Hover.Show(NeonIcons.State.Lit, UITheme.ClubBlue, true);
            }

            if (Label != null)
            {
                Label.color = word;
                string want = armed && AskWord != null ? AskWord : Word;
                if (want != null && Label.text != want)
                {
                    Label.text = want;
                    FitWord();
                }
            }
        }

        /// <summary>
        /// THE WORD KEEPS TO ITS KEY: body 16 while it clears the mark's slot on both sides, else body 8 - the house's
        /// documented step down, never an overflow (the question is longer than the verb in most languages: German's
        /// "SICHER? NOCHMAL DRÜCKEN" is 258 units at 16, the critic's measure).
        /// </summary>
        public void FitWord()
        {
            if (Label == null) return;
            var rt = (RectTransform)transform;
            Label.fontSize = LanguageFonts.Size(Label.font, 16);
            if (Label.preferredWidth + 2f * WordPad > rt.sizeDelta.x)
                Label.fontSize = LanguageFonts.Size(Label.font, 8);
        }
    }
}
