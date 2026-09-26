using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A NEON TITLE (2026-09-25, the menus' effects): the word holds its lit colour and, every few seconds, stutters
    /// two or three times to the step under it on the same ramp - a tube catching, never going dark. A fixed table on
    /// the unscaled clock (the menus are up while the night's clock is held), no random numbers, low contrast (one
    /// ramp step) and never fast, so nothing strobes; steady under reduced motion, and with FLASHES off
    /// (Motion.NoFlashes, 2026-09-26).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class NeonFlicker : MonoBehaviour
    {
        public Color Lit = UITheme.Amber[4];
        public Color Dim = UITheme.Amber[3];

        /// <summary>One step every 0.08 s: 1 = lit, 0 = the step under. About five seconds a round, two stutters in it.</summary>
        private static readonly byte[] Table =
        {
            1,1,1,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,1,1, 1,1,1,1,1,1,0,1,0,0, 1,1,1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,0,1, 1,1,1,1,1,1,1,1,1,1,
        };
        private const float Step = 0.08f;

        private Text _text;

        private void OnEnable()
        {
            _text = GetComponent<Text>();
            if (_text != null) _text.color = Lit;
        }

        private void Update()
        {
            if (_text == null) return;
            if (Motion.NoFlashes) { if (_text.color != Lit) _text.color = Lit; return; }
            int i = (int)(Time.unscaledTime / Step) % Table.Length;
            var want = Table[i] == 1 ? Lit : Dim;
            if (_text.color != want) _text.color = want;
        }
    }
}
