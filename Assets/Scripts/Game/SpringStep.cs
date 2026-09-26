using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// ONE DAMPED SPRING, STEPPED SMALL (2026-09-26, with the settings' frame rates). The hand that holds a bottle or
    /// the tin (UI's PourHand) follows the pointer on a stiff spring - omega 30, zeta 0.85 - integrated one
    /// semi-implicit Euler step a frame. That step is stable only while omega * dt stays small: computed, its spectral
    /// radius is 0.68 at 60 fps, 0.78 at 35 and 1.26 at 30, so at 32 fps and under the grip rang up and flew off, and
    /// a machine whose vsync halved to 30 hit it without anyone choosing a frame rate. The frame's dt is now cut into
    /// steps of at most 1/60 s: one step - bit for bit the old one - at 60 fps and faster, two at 30, three at 20.
    /// Pure arithmetic, so the EditMode tests can hold it to that.
    /// </summary>
    public static class SpringStep
    {
        /// <summary>The longest step the springs take.</summary>
        public const float MaxStep = 1f / 60f;

        /// <summary>A frame's jitter over 1/60 (a 60 Hz frame measured at 16.9 ms) is still one step: the
        /// stability limit is near 1/32, so a step of up to 1.05/60 is as safe and keeps 60 fps on one step.</summary>
        private const float Slack = 1.05f;

        /// <summary>How many steps a frame of <paramref name="dt"/> is cut into (0 for no time at all).</summary>
        public static int Substeps(float dt)
        {
            if (!(dt > 0f)) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(dt / (MaxStep * Slack)));
        }

        /// <summary>One semi-implicit Euler step of <paramref name="h"/> seconds toward <paramref name="target"/> -
        /// the velocity first, then the position on the new velocity.</summary>
        public static void Damped(ref float x, ref float v, float target, float omega, float zeta, float h)
        {
            v += (omega * omega * (target - x) - 2f * zeta * omega * v) * h;
            x += v * h;
        }

        /// <summary><see cref="Damped(ref float, ref float, float, float, float, float)"/> on both axes.</summary>
        public static void Damped(ref Vector2 x, ref Vector2 v, Vector2 target, float omega, float zeta, float h)
        {
            v += (omega * omega * (target - x) - 2f * zeta * omega * v) * h;
            x += v * h;
        }

        /// <summary>A whole frame of <paramref name="dt"/> toward a still target, in <see cref="Substeps"/> steps.</summary>
        public static void Advance(ref float x, ref float v, float target, float omega, float zeta, float dt)
        {
            int n = Substeps(dt);
            if (n == 0) return;
            float h = dt / n;
            for (int i = 0; i < n; i++) Damped(ref x, ref v, target, omega, zeta, h);
        }
    }
}
