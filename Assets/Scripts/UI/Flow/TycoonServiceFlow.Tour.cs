using UnityEngine;

namespace LastCall.UI
{
    // TycoonServiceFlow, part Tour: what the house tour points at on the benches, and the two things it can only see
    // here (TycoonHud.Tour, 2026-09-28). Read-only: nothing on a bench behaves differently while she talks.
    public sealed partial class TycoonServiceFlow
    {
        /// <summary>The piece of the open bench a tour step points at, or null when that bench is not the one out.</summary>
        internal RectTransform TourTarget(string point)
        {
            switch (point)
            {
                case "bench_bottle": return _stage == Stage.Shaker ? _pourBottle : null;
                case "tin_gauge": return _stage == Stage.Shaker ? _shakerMixBar : null;
                case "lid": return _stage == Stage.Shaker ? _shakerTop : null;
                case "shaker": return _stage == Stage.Serve ? _serveShaker : null;
                case "serve_key": return _stage == Stage.Serve ? _serveDone : null;
                case "back": return Named(OpenPanel, "EdgeBack");
                // 2026-09-29: the first-use lessons and the tour's finer steps
                case "tin": return _stage == Stage.Shaker ? _shakerVessel : null;
                case "spoon": return _stage == Stage.Shaker && _spoonRt != null && _spoonRt.gameObject.activeInHierarchy ? _spoonRt : null;
                case "mix_column": return _stage == Stage.Shaker ? Named(_shakerPanel, "WorkColumn") : null;
                case "serve_glass": return _stage == Stage.Serve ? _serveGlass : null;
                case "bench_steps": return _stage == Stage.Shaker ? _shakerSteps : Named(OpenPanel, "StepPanel");
                case "bin_key": return Named(OpenPanel, "Bin");
                default: return null;
            }
        }

        private RectTransform OpenPanel =>
            _stage == Stage.Shaker ? _shakerPanel : _stage == Stage.Serve ? _servePanel : _stage == Stage.Tap ? _tapPanel : null;

        /// <summary>A live piece of a bench by name (the way back is AddEdgeBack's "EdgeBack", wherever the key row hangs it).</summary>
        private static RectTransform Named(RectTransform panel, string name)
        {
            if (panel == null) return null;
            foreach (var rt in panel.GetComponentsInChildren<RectTransform>(false))
                if (rt.name == name) return rt;
            return null;
        }

        /// <summary>The bottle in the hand on the shaker bench, or null.</summary>
        internal string TourBottleInHand => _stage == Stage.Shaker ? _focusBottle?.Id : null;

        /// <summary>A bottle taken off the cellar is in the hand: the pick opens the shaker bench with it.</summary>
        internal bool TourSeesABottleInHand => _stage == Stage.Shaker;

        /// <summary>The lid is on the tin - or the tin has already gone over to the glass, which a built drink does
        /// the moment it is capped.</summary>
        internal bool TourSeesTheLidOn => _stage == Stage.Serve || (_stage == Stage.Shaker && _capped);
    }
}
