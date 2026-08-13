using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastCall.EditorTools
{
    /// <summary>
    /// The one-click cure for a PlayMode run that died holding the mouse (2026-08-13, the
    /// author: "play mode başlatıldığında kendi kendine oynuyor").
    ///
    /// The suite drives a VIRTUAL mouse, and `InputTestFixture` only takes it away again if
    /// the run reaches its teardown. A run that is cancelled, killed, or wedged leaves the
    /// editor session holding that fake device instead of the real one: the pointer sits at
    /// (0,0), every hover and click the game sees comes from nowhere, and the bar appears to
    /// play itself while ignoring the player. It is a bewildering symptom with nothing to do
    /// with the game.
    ///
    /// The cure is to throw the input devices away and let the system re-discover the real
    /// one on the next event. It is a MENU ITEM and not an automatic sweep on purpose: in the
    /// editor the genuine mouse is also reported as non-native, so a guard that fired at
    /// every play would take the player's own pointer away — measured, 2026-08-13, which is
    /// how this ended up a button instead.
    /// </summary>
    public static class GhostInputGuard
    {
        [MenuItem("LastCall/Clear Ghost Input (after a killed PlayMode run)")]
        public static void Clear()
        {
            var devices = new List<InputDevice>(InputSystem.devices);
            foreach (var device in devices) InputSystem.RemoveDevice(device);
            Debug.Log($"[LastCall] Dropped {devices.Count} input device(s). Move the mouse over " +
                      "the Game view and the real one comes back; if the pointer is still dead, " +
                      "restart the editor — nothing else in the project can bring a native " +
                      "device back by itself.");
        }
    }
}
