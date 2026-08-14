using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// "THIS BRANCH IS NOT CHROME." The audit's scaling and grid laws (GDD 16 §0, §3) govern
    /// the UI's own furniture — keys, marks, plates, lamps, readouts — which is drawn on a
    /// whole-unit grid at whole multiples of its own art.
    ///
    /// Some things under the same canvas are NOT that. The glasses on the rack stand at
    /// different depths and are scaled by perspective; the register is a prop in the room.
    /// They are placed by where they are in the bar, and rounding them to whole units would
    /// put them on the wrong shelf.
    ///
    /// The exemption carries a REASON and the audit prints it, so an exemption is a sentence
    /// somebody wrote rather than a silence. If a branch cannot say why it is exempt, it is
    /// not exempt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiAuditExempt : MonoBehaviour
    {
        [SerializeField] private string reason = "";

        public string Reason => reason;

        /// <summary>Marks a branch, with the sentence that justifies it.</summary>
        public static void Mark(Component host, string why)
        {
            if (host == null) return;
            var e = host.gameObject.GetComponent<UiAuditExempt>()
                    ?? host.gameObject.AddComponent<UiAuditExempt>();
            e.reason = why;
        }
    }
}
