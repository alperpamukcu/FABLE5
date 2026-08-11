using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastCall.UI
{
    /// <summary>
    /// Reports the pointer entering and leaving, and NOTHING else.
    ///
    /// This exists because <see cref="EventTrigger"/> is not a listener, it is a wall: it
    /// implements every handler interface UGUI has — including <c>IScrollHandler</c> — and an
    /// event that reaches a handler stops bubbling there whether or not anything was
    /// registered for it. So a market tile carrying an EventTrigger for its hover text also
    /// swallowed the mouse wheel, and the aisle froze under the pointer wherever there was
    /// something to read (the author, 2026-08-09). Every tile is a card, so the aisle froze
    /// almost everywhere.
    ///
    /// Implementing only the two interfaces we want leaves the wheel free to reach the
    /// ScrollRect above it, which is the whole fix.
    /// </summary>
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Entered;
        public Action Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
    }
}
