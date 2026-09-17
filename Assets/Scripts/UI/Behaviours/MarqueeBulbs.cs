using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A FAIRGROUND SIGN'S LAMPS (2026-09-17, the author: "serve it butonunun içerisinde açık renkli yüzeyinin
    /// etrafında spot ampuller gibi yanıp sönen panayır ışığı istiyorum"). A ring of bulbs standing just inside a
    /// key's face, chasing round it: every third lamp is lit and the pattern steps, so the eye reads a run of
    /// light travelling round the sign rather than a row of dots flashing together.
    ///
    /// The lamps themselves are <see cref="ChromeArt.Bulb"/> drawn white, so this only has to run each one between
    /// <see cref="Dead"/> and <see cref="Lit"/> — and the step is a WHOLE step, not a fade: a bulb is on or it is
    /// off, the way a real one is, and the pixel grain stays clean.
    ///
    /// <see cref="On"/> turns the whole sign off in one place: a key that cannot be pressed should not be
    /// advertising itself.
    /// </summary>
    public sealed class MarqueeBulbs : MonoBehaviour
    {
        public Image[] Bulbs;
        public Color Lit = Color.white, Dead = Color.grey;

        /// <summary>Lamps between one lit one. 3 gives the classic chase; 1 would blink the whole ring.</summary>
        public int Every = 3;

        /// <summary>Steps a second — how fast the run travels round the sign.</summary>
        public float Rate = 6f;

        /// <summary>The sign's power. Off leaves every lamp dead rather than hiding them: an unlit bulb is still
        /// part of the drawing.</summary>
        public bool On = true;

        private int _at = -1;

        private void OnEnable() => _at = -1;

        private void Update()
        {
            if (Bulbs == null || Bulbs.Length == 0) return;
            int n = Mathf.Max(1, Every);
            // unscaled: the bench slows nothing, but a paused game should not leave the sign frozen mid-run
            int step = On ? Mathf.FloorToInt(Time.unscaledTime * Rate) % n : -1;
            if (step == _at) return;
            _at = step;
            for (int i = 0; i < Bulbs.Length; i++)
            {
                var b = Bulbs[i];
                if (b == null) continue;
                b.color = step >= 0 && i % n == step ? Lit : Dead;
            }
        }
    }
}
