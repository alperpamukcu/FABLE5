using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Widens a rect past the safe frame until it spans the whole window, without moving
    /// it vertically or moving anything inside it.
    ///
    /// The safe frame stops the game from drifting (see <see cref="DesignFrame"/>), but a
    /// few things are not the game — they are its chrome, and chrome that stops short of
    /// the window edge reads as a floating strip rather than as a bar across the top of
    /// the room (2026-08-11, the author: "bazıları oldu bazıları olmadı", with the top
    /// fascia hanging in the middle of a wide window).
    ///
    /// So the split is: the FASCIA bleeds, the INSTRUMENTS do not. The black board, its
    /// hairline and its neon tube run to both edges however wide the window is; the clock,
    /// the till and the standing stay anchored to the safe frame, where they were placed
    /// and where they will still be at any other window size. That is the ordinary
    /// arrangement — a full-bleed background with its content inside the safe area — and
    /// it is the only one that answers both halves of the complaint at once.
    ///
    /// Assumes the rect stretches horizontally across its parent (anchors 0→1 in x), which
    /// is what the offsets it writes mean.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BleedWidth : MonoBehaviour
    {
        private RectTransform _self, _parent, _canvas;

        public static void Apply(RectTransform rt) => rt.gameObject.AddComponent<BleedWidth>();

        private void Awake()
        {
            _self = (RectTransform)transform;
            _parent = transform.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _canvas = (RectTransform)canvas.transform;
        }

        private void OnEnable() => Bleed();

        private void LateUpdate() => Bleed();

        private void Bleed()
        {
            if (_self == null) _self = (RectTransform)transform;
            if (_parent == null) _parent = transform.parent as RectTransform;
            if (_canvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) _canvas = (RectTransform)canvas.transform;
            }
            if (_parent == null || _canvas == null) return;

            // How far past the parent each side has to reach. Never negative: a window
            // narrower than the safe frame is already cropping, and pulling the fascia in
            // to meet it would leave a gap at exactly the edge it is there to cover.
            float over = Mathf.Max(0f, (_canvas.rect.width - _parent.rect.width) * 0.5f);
            var min = _self.offsetMin;
            var max = _self.offsetMax;
            if (Mathf.Approximately(min.x, -over) && Mathf.Approximately(max.x, over)) return;
            _self.offsetMin = new Vector2(-over, min.y);
            _self.offsetMax = new Vector2(over, max.y);
        }
    }
}
