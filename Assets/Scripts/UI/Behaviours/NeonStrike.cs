using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A MENU'S SIGN STRIKING ON (2026-09-29, BUILD_SPEC §4): when a cabinet opens, its title catches first and its frame
    /// tube a tenth of a second after it, each the way a tube catches - HALF for .05 s, DARK for .04 s, then LIT. FLASHES
    /// off: one step, dark to lit, no catch; REDUCED MOTION: lit from the first frame. On the unscaled clock (the pause
    /// holds the game's own), and only when the owner says the screen OPENED (<see cref="Restart"/>) - a screen shown
    /// again from its own settings page stands lit, and nothing strikes twice.
    /// </summary>
    public sealed class NeonStrike : MonoBehaviour
    {
        public NeonWord Title;
        public NeonTube Tube;
        public Color[] TubeHue = UITheme.Magenta;

        /// <summary>The tube catches this long after the title.</summary>
        public const float TubeDelay = 0.10f;
        private const float HalfFor = 0.05f, DarkFor = 0.04f;

        private float _t0 = float.NegativeInfinity;
        private bool _done;

        /// <summary>The screen opened: strike from dark.</summary>
        public void Restart()
        {
            _t0 = Time.unscaledTime;
            _done = false;
            Step();
        }

        private void OnEnable() => Step();

        private void Update()
        {
            if (!_done) Step();
        }

        private void Step()
        {
            float t = Time.unscaledTime - _t0;
            var title = At(t);
            var tube = At(t - TubeDelay);
            if (Title != null) Title.Show(title);
            if (Tube != null) Tube.Show(tube, TubeHue, true);
            _done = title == NeonIcons.State.Lit && tube == NeonIcons.State.Lit;
        }

        /// <summary>The state a tube is in <paramref name="t"/> seconds after it began to catch.</summary>
        public static NeonIcons.State At(float t)
        {
            if (Motion.Reduced || float.IsNaN(t)) return NeonIcons.State.Lit;
            if (t < 0f) return NeonIcons.State.Dark;
            if (Motion.NoFlashes) return NeonIcons.State.Lit;
            if (t < HalfFor) return NeonIcons.State.Half;
            if (t < HalfFor + DarkFor) return NeonIcons.State.Dark;
            return NeonIcons.State.Lit;
        }
    }
}
