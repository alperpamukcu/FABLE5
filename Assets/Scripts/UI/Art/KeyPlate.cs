using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE ONE KEY (GDD 16 §2, 2026-08-14, the author: "butonlar ayarlar menüsü oyunun
    /// ekrandaki HUD paneller bunların aynı sanat dilinde olması gerekiyor").
    ///
    /// The game was speaking FOUR button dialects at once:
    ///   · the market drew `ChromeArt.Key()` — chamfered, edged, with a throw under its face
    ///   · the service flow loaded a `plate` sprite out of Resources, with a pressed twin
    ///   · the HUD drew a flat coloured rect with a word on it
    ///   · the settings menu drew a bare rect that did not even press
    /// A player who learns one of those has learned none of the others, and a screen made of
    /// two of them at once reads as two screens stapled together — which is most of what the
    /// board looked like before it was rebuilt.
    ///
    /// The key that wins is `ChromeArt.Key()`, because it is the one that was DRAWN rather
    /// than picked: it exists because the author looked at the market's generated lozenge and
    /// said "ADD butonu çok yapay duruyor" (2026-08-11). It is grey by construction and takes
    /// its colour from the caller, so the same drawing is the amber primary, the grey second
    /// and the dark key at the end of the board.
    ///
    /// Everything the player can press goes through here.
    /// </summary>
    public static class KeyPlate
    {
        /// <summary>How deep the key's throw is. A label sitting on the throw looks dropped;
        /// every caption on a key is inset by this much along the bottom.</summary>
        public const float Throw = 3f;

        /// <summary>
        /// Dresses <paramref name="rt"/> as the key and gives it the house press.
        /// The Image is created if the rect has none, so this can dress a plate that has
        /// already been built as well as one that has not.
        /// </summary>
        /// <param name="face">what MOVES when pressed — a child holding the label, so the
        /// caption travels with the key instead of sliding out from under it. Defaults to
        /// the key itself.</param>
        public static Image Dress(RectTransform rt, Color fill, Button button = null,
                                  RectTransform face = null)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.Key();
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.color = fill;
            // A key catches the pointer; the drawing is the target so hover and press read
            // off the same object the player is aiming at.
            img.raycastTarget = true;
            if (button != null) button.targetGraphic = img;

            var sink = rt.GetComponent<PressSink>() ?? rt.gameObject.AddComponent<PressSink>();
            sink.Face = face != null ? face : rt;
            sink.Depth = 3f;
            sink.Lift = 2f;
            sink.Squash = 0.015f;
            sink.Tint = img;
            return img;
        }
    }
}
