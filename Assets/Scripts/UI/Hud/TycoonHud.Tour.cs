using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Tour: the hostess shows the player round on the first night (2026-09-28, TycoonRun.Tour).
    //
    // The author: "Bu öğreticide sahneye ana karakterimiz gelir konuşarak sırayla temelden detaya doğru öğretir ekranı ve
    // neler yapıldığını." Core runs the tour - its steps, its held door, its one guest - and this file is the scene: she
    // walks on in her own art (TycoonHud.Hostess, the same figure as her visits), her lines ride the talk plate, and the
    // thing she is talking about is lifted out of a dimmed room with a brass frame and a pointer. The plate goes to the
    // top of the screen whenever what she points at is in the lower half (the counter, the cellar, a bench), so it never
    // covers the thing it asks for; it takes no clicks but its own two keys. What Core cannot see (a card put away, the
    // cellar open, the lid on, the book open or shut) is reported to it here, once a frame, for the step on screen only.
    public sealed partial class TycoonHud
    {
        /// <summary>The house tour is on the screen: an open night whose tour is running.</summary>
        private static bool TourUp(TycoonRun run) =>
            run != null && run.Phase == TycoonPhase.DayOpen && run.TourRunning;

        // ── the plate ────────────────────────────────────────────────────────────────────────────────────────────────

        private bool _plateOnTour;                  // the plate is raised and docked for the tour
        private const int TourPlateOrder = 27;      // over the bench (25) and the card (20), under the curtain (30)
        private const int TourSpotOrder = 26;       // the spotlight, between the bench and the plate

        /// <summary>
        /// Her lines for the step on screen (SyncLastCall's first path while the tour runs): her face, her name, one line
        /// a key. A thing said ends on GO ON, which tells Core it was heard; a step that waits for the player ends on no
        /// key at all - the line stays up until the player has done it. SKIP THE TOUR stands under it the whole way.
        /// The plate waits for her to reach her mark, as it does on her visits.
        /// </summary>
        private void SyncTourPlate(TycoonRun run)
        {
            var step = run.TourStep;
            var host = Hostess;
            if (host == null) { run.SkipTour(); return; }   // nobody to say it: the night opens as it did before
            string part = "tour:" + step.Id;
            if (part != _plateStage)
            {
                _plateStage = part;
                _plateAt = 0;
                _plateScript.Clear();
                _hostessSaidAt = -1;
                foreach (var line in UIText.DataLines("tour", step.Id, "say", step.Say))
                    if (!string.IsNullOrWhiteSpace(line)) _plateScript.Add((host.Name, LookForStory(host)?.Slug, line));
                if (_plateScript.Count == 0) _plateScript.Add((host.Name, LookForStory(host)?.Slug, ""));
            }
            DockPlateForTour(true);
            bool standing = _hostessBeat == HostessBeat.Talk || _hostessView == null || _hostessView.Look == null;
            if (standing != _plate.gameObject.activeSelf) _plate.gameObject.SetActive(standing);
            if (!standing) return;

            int at = Mathf.Clamp(_plateAt, 0, _plateScript.Count - 1);
            var (who, look, said) = _plateScript[at];
            _plateName.text = UIText.Caps(who);
            _plateLine.text = FillTourLine(run, said);
            var face = LookNamed(look);
            _plateFace.sprite = face?.Face;
            _plateFace.enabled = _plateFace.sprite != null;
            bool last = at >= _plateScript.Count - 1;
            bool keyUp = !last || step.Wait == TourWait.Heard;
            if (keyUp != _plateKey.gameObject.activeSelf) _plateKey.gameObject.SetActive(keyUp);
            if (keyUp) _plateKeyLabel.text = UIText.T("chrome.story.go_on");
            if (!_plateNoKey.gameObject.activeSelf) _plateNoKey.gameObject.SetActive(true);
            var noLabel = _plateNoKey.Find("Face/Label")?.GetComponent<Text>();
            if (noLabel != null) noLabel.text = UIText.T("chrome.tour.skip");
            if (_gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(false);
            if (_postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(false);
        }

        /// <summary>The plate's GO ON on a tour step (OnPlateKey): the next line, or - on the last line of a thing said -
        /// the next step.</summary>
        private void TourPlateKey(TycoonRun run)
        {
            Sfx.Play("key_press", 0.5f);
            if (_plateAt < _plateScript.Count - 1) { _plateAt++; return; }
            if (run.TourStep == null || run.TourStep.Wait != TourWait.Heard) return;
            _plateStage = "";
            run.HearTour();
        }

        /// <summary>SKIP THE TOUR (OnSayNoTonight): the night opens where it stands; she stays for the first job.</summary>
        private void TourSkipKey(TycoonRun run)
        {
            Sfx.Play("key_press", 0.5f);
            _plateStage = "";
            run.SkipTour();
            HideTourSpot();
        }

        /// <summary>The {drink} and {bottle} slots, filled in the player's language once the table has spoken: the
        /// guest's drink (behind the card until it is read, and the tour never says it before) and the style word of the
        /// next bottle the drink wants - the word the cellar card itself prints.</summary>
        private string FillTourLine(TycoonRun run, string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            var guest = run.TourGuest;
            if (line.Contains("{drink}"))
            {
                string drink = "";
                if (guest != null && guest.IdInspected)
                {
                    var recipe = guest.Order.Wanted;
                    drink = UIText.Data("recipe", recipe.Id, "name", recipe.Name);
                }
                line = line.Replace("{drink}", drink);
            }
            if (line.Contains("{bottle}"))
            {
                string id = run.TourNextBottle, word = "";
                var card = id != null ? _cellarCards.Find(c => c.Id == id) : null;
                if (card != null) word = UIText.Data("bottle", card.Id, "style", card.Info?.Style ?? card.Name).Replace('_', ' ');
                line = line.Replace("{bottle}", word);
            }
            return line;
        }

        /// <summary>
        /// Up for the tour: over the bench and the card, and at the TOP of the screen whenever the thing she points at is
        /// in the lower half, or a bench, the card or the book is open with nothing to point at. Down again: back to its
        /// own layer (7) at the foot, where her visits and the lessons use it.
        /// </summary>
        private void DockPlateForTour(bool on)
        {
            if (_plate == null) return;
            var canvas = _plate.GetComponent<Canvas>();
            if (!on)
            {
                if (!_plateOnTour) return;
                _plateOnTour = false;
                if (canvas != null) canvas.sortingOrder = 7;
                _plate.anchorMin = _plate.anchorMax = new Vector2(0.5f, 0f);
                _plate.pivot = new Vector2(0.5f, 0f);
                _plate.anchoredPosition = new Vector2(0f, 14f);
                // ...and its keys as everybody else found them: the listen key up, the second one saying no again.
                if (_plateKey != null) _plateKey.gameObject.SetActive(true);
                var noLabel = _plateNoKey != null ? _plateNoKey.Find("Face/Label")?.GetComponent<Text>() : null;
                if (noLabel != null) noLabel.text = UIText.T("hud.plate.say_no");
                return;
            }
            _plateOnTour = true;
            if (canvas != null) canvas.sortingOrder = TourPlateOrder;
            bool top = _tourSpotOn
                ? _tourHole.center.y < 0f   // the spot's space is centred on the screen: below its middle is the lower half
                : (_flow != null && _flow.IsOpen) || (_idRoot != null && _idRoot.gameObject.activeSelf) || _bookOpen;
            var anchor = new Vector2(0.5f, top ? 1f : 0f);
            if (_plate.anchorMin != anchor)
            {
                _plate.anchorMin = _plate.anchorMax = anchor;
                _plate.pivot = anchor;
            }
            _plate.anchoredPosition = new Vector2(0f, top ? -(TopBarH + 8f) : 14f);
        }

        // ── what only the screen can see ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Tells Core what it cannot see for itself, for the step on screen only (Core ignores anything else).</summary>
        private void ReportTourSights(TycoonRun run)
        {
            var step = run.TourStep;
            if (step == null) return;
            bool seen;
            switch (step.Wait)
            {
                case TourWait.CardPutAway: seen = _idRoot == null || !_idRoot.gameObject.activeSelf; break;
                case TourWait.CellarOpen: seen = CellarOpen; break;
                case TourWait.BottleInHand: seen = _flow != null && _flow.TourSeesABottleInHand; break;
                case TourWait.Capped: seen = _flow != null && _flow.TourSeesTheLidOn; break;
                case TourWait.OnTheCounter: seen = (_flow == null || !_flow.IsOpen) && run.DrinkReady; break;
                case TourWait.BookOpen: seen = _bookOpen; break;
                case TourWait.BookShut: seen = !_bookOpen; break;
                default: return;
            }
            if (seen) run.TourSaw(step.Wait);
        }

        // ── the spotlight ────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform _tourSpot;                  // full screen, its own canvas; takes no clicks
        private readonly Image[] _tourDim = new Image[4];
        private readonly Image[] _tourFrame = new Image[4];
        private RectTransform _tourArrow;
        private bool _tourSpotOn;
        private Rect _tourHole;                            // where the light is now, in the spot's own (centred) space
        private float _tourHoleT;                          // 0..1 of the glide to a new target
        private Rect _tourHoleFrom;
        private string _tourPointFor = "";
        private int _tourSeat = -1;                        // the stool the tour's guest sat on, kept after they leave
        private readonly Dictionary<string, RectTransform> _tourFound = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, float> _tourLookedAt = new Dictionary<string, float>();

        private const float TourPad = 8f, TourFrameW = 2f, TourArrowSize = 32f;

        /// <summary>
        /// Once a frame (from SyncHostess): the screen's reports, the light on the thing she is talking about, and - once
        /// the tour is over - everything of it put away. Held while she walks in: the room is lit for her entrance first.
        /// </summary>
        private void StepTour(TycoonRun run)
        {
            if (!TourUp(run))
            {
                DockPlateForTour(false);
                HideTourSpot();
                _tourSeat = -1;
                return;
            }
            ReportTourSights(run);
            if (!TourUp(run)) { HideTourSpot(); return; }   // that report was the last thing it waited for
            RememberTourSeat(run);
            var step = run.TourStep;
            bool standing = _hostessBeat == HostessBeat.Talk || _hostessView == null || _hostessView.Look == null;
            if (!standing || !TourTargetRect(run, step.Point, out Rect hole)) { HideTourSpot(); return; }
            ShowTourSpot(hole, step.Point, step.Wait == TourWait.Heard);
        }

        private void RememberTourSeat(TycoonRun run)
        {
            if (run.TourGuest == null) return;
            for (int i = 0; i < _seats.Count; i++)
                if (_seats[i].Visit == run.TourGuest) { _tourSeat = i; return; }
        }

        /// <summary>Where a step's point is on the screen this frame, in the spot's own space; false when it is not there
        /// to be shown (a bench that is not out, a guest not seated yet, a glass already carried off).</summary>
        private bool TourTargetRect(TycoonRun run, string point, out Rect rect)
        {
            rect = default;
            if (string.IsNullOrEmpty(point)) return false;
            EnsureTourSpot();
            var seat = _tourSeat >= 0 && _tourSeat < _seats.Count ? _seats[_tourSeat] : null;
            switch (point)
            {
                // The beam's pieces are asked for by NAME where the bar is still being redrawn (the bill and the music
                // come and go with its passes), and fall back to what always stands there.
                // THE BEAM WENT SIMPLE (2026-09-28, the bar's second pass: "sadeleştirelim ... butonları kaldır sadece
                // yıldız gözüksün"): the clock, the money and the stars. The bill rides the money's hover card and the
                // house's hearts and medals the stars' card; the keys left the bar (settings are Escape's).
                case "clock": return RectOf(_hourWell, out rect);
                case "till":
                case "bill": return RectOf(_moneyWell, out rect);
                case "stars":
                case "house": return RectOf(_starsWell, out rect);
                case "corner": return false;
                case "stool":
                case "guest": return seat != null && seat.Visit != null && RectOf(seat.Root, out rect);
                case "patience": return seat != null && seat.Visit != null && RectOf(seat.Gauge, out rect);
                case "dirty_glass": return seat != null && RectOf(seat.DirtyProp, out rect);
                case "card": return _idRoot != null && _idRoot.gameObject.activeSelf && RectOf(_idCardRt, out rect);
                case "cellar": return RectOf(FoundByName("ShutterDoor"), out rect);
                case "bottle":
                {
                    // From a bench the cellar is behind it: the way to the next bottle is the bench's way back - unless it
                    // is the bottle already in the hand, which is poured where it stands.
                    string id = run.TourNextBottle;
                    if (_flow != null && _flow.IsOpen)
                        return RectOf(_flow.TourTarget(id != null && _flow.TourBottleInHand == id ? "bench_bottle" : "back"), out rect);
                    // ...and with the cellar rolled back, the way to it is its shutter.
                    if (!CellarOpen) return RectOf(FoundByName("ShutterDoor"), out rect);
                    int i = id != null ? _cellarCards.FindIndex(c => c.Id == id) : -1;
                    if (i < 0 || !CellarOpen || !CardBottleRect(i, _tourSpot, out Vector2 c0, out Vector2 size, out _)) return false;
                    rect = new Rect(c0 - size * 0.5f, size);
                    return true;
                }
                case "counter_glass": return run.DrinkReady && (_flow == null || !_flow.IsOpen) && RectOf(_drinkGlass, out rect);
                case "cloth": return RectOf(_clothRt, out rect);
                case "book": return RectOf(_bookProp, out rect);
                case "tap": return RectOf(FoundByName("PropDoor_taps", prefix: true), out rect);
                default: return _flow != null && RectOf(_flow.TourTarget(point), out rect);
            }
        }

        /// <summary>A live RectTransform's box in the spot's space - through the camera its canvas renders with.</summary>
        private bool RectOf(RectTransform target, out Rect rect)
        {
            rect = default;
            if (target == null || !target.gameObject.activeInHierarchy || _tourSpot == null) return false;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            TightenToTheDrawing(target, corners);
            var canvas = target.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            var cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            foreach (var c in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(cam, c);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_tourSpot, screen, null, out Vector2 local);
                lo = Vector2.Min(lo, local);
                hi = Vector2.Max(hi, local);
            }
            rect = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
            return rect.width > 1f && rect.height > 1f;
        }

        /// <summary>
        /// A picture drawn inside a bigger rect (the lid's art keeps the tin's full canvas so it can sit on it) is framed
        /// where it is DRAWN: the aspect-kept box of its sprite, and inside that the sprite's own tight mesh - a tight
        /// mesh hugs the opaque pixels, a full-rect one changes nothing.
        /// </summary>
        private static void TightenToTheDrawing(RectTransform target, Vector3[] corners)
        {
            var img = target.GetComponent<Image>();
            var sprite = img != null ? img.sprite : null;
            if (sprite == null || !img.enabled) return;
            var r = target.rect;
            Rect drawn = r;
            if (img.preserveAspect && sprite.rect.height > 0f && r.height > 0f)
            {
                float aspect = sprite.rect.width / sprite.rect.height;
                if (r.width / r.height > aspect)
                {
                    float w = r.height * aspect;
                    drawn = new Rect(r.center.x - w * 0.5f, r.yMin, w, r.height);
                }
                else
                {
                    float h = r.width / aspect;
                    drawn = new Rect(r.xMin, r.center.y - h * 0.5f, r.width, h);
                }
            }
            var b = sprite.bounds;
            var verts = sprite.vertices;
            float u0 = 0f, v0 = 0f, u1 = 1f, v1 = 1f;
            if (verts != null && verts.Length > 0 && b.size.x > 0f && b.size.y > 0f)
            {
                u0 = v0 = 1f; u1 = v1 = 0f;
                foreach (var v in verts)
                {
                    float u = (v.x - b.min.x) / b.size.x, w = (v.y - b.min.y) / b.size.y;
                    u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                    v0 = Mathf.Min(v0, w); v1 = Mathf.Max(v1, w);
                }
            }
            var lo = new Vector2(drawn.xMin + drawn.width * u0, drawn.yMin + drawn.height * v0);
            var hi = new Vector2(drawn.xMin + drawn.width * u1, drawn.yMin + drawn.height * v1);
            corners[0] = target.TransformPoint(new Vector3(lo.x, lo.y, 0f));
            corners[1] = target.TransformPoint(new Vector3(lo.x, hi.y, 0f));
            corners[2] = target.TransformPoint(new Vector3(hi.x, hi.y, 0f));
            corners[3] = target.TransformPoint(new Vector3(hi.x, lo.y, 0f));
        }

        /// <summary>A live piece of the top bar by name, or null.</summary>
        private RectTransform OnTheBeam(string name)
        {
            var beam = FoundByName("TopBar");
            if (beam == null) return null;
            foreach (var rt in beam.GetComponentsInChildren<RectTransform>(false))
                if (rt.name == name) return rt;
            return null;
        }

        /// <summary>A named piece of the room the HUD holds no field for (the cellar's shutter, the tap's plate), found once
        /// and kept while it lives.</summary>
        private RectTransform FoundByName(string name, bool prefix = false)
        {
            if (_tourFound.TryGetValue(name, out var found) && found != null) return found;
            // Not there (yet): looked for again at most once a second, never a whole-scene search a frame.
            if (_tourLookedAt.TryGetValue(name, out float at) && Time.unscaledTime - at < 1f) return null;
            _tourLookedAt[name] = Time.unscaledTime;
            found = null;
            foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (prefix ? rt.name.StartsWith(name, System.StringComparison.Ordinal) : rt.name == name) { found = rt; break; }
            if (found != null) _tourFound[name] = found;
            return found;
        }

        private void EnsureTourSpot()
        {
            if (_tourSpot != null) return;
            _tourSpot = NewRect("TourSpot", _hudRoot);
            _tourSpot.anchorMin = Vector2.zero;
            _tourSpot.anchorMax = Vector2.one;
            _tourSpot.offsetMin = _tourSpot.offsetMax = Vector2.zero;
            var canvas = _tourSpot.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = TourSpotOrder;
            for (int i = 0; i < 4; i++)
            {
                _tourDim[i] = NewSpotImage("Dim" + i, UITheme.Night[0]);
                _tourFrame[i] = NewSpotImage("Frame" + i, UITheme.Amber[3]);
            }
            _tourArrow = NewRect("Pointer", _tourSpot);
            _tourArrow.anchorMin = _tourArrow.anchorMax = new Vector2(0.5f, 0.5f);
            _tourArrow.pivot = new Vector2(1f, 0.5f);   // the point of the arrow is its right edge
            _tourArrow.sizeDelta = new Vector2(TourArrowSize, TourArrowSize);
            var arrow = _tourArrow.gameObject.AddComponent<Image>();
            arrow.sprite = ItemArt.Load("ib_m_pointer");
            arrow.color = UITheme.Amber[4];
            arrow.preserveAspect = true;
            arrow.raycastTarget = false;
            _tourSpot.gameObject.SetActive(false);
            UiAuditExempt.Mark(_tourSpot, "the tour's light follows whatever she is pointing at, not a fixed place");
        }

        private Image NewSpotImage(string name, Color color)
        {
            var rt = NewRect(name, _tourSpot);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Lifts <paramref name="target"/> out of the room: the rest dimmed (harder while she is only talking,
        /// lighter while the player works), a pulsing brass frame round it and the pointer beside it, pointing in from
        /// the side nearer the middle of the screen. A new target is glided to; reduced motion snaps and holds still.</summary>
        private void ShowTourSpot(Rect target, string point, bool talking)
        {
            EnsureTourSpot();
            target = Rect.MinMaxRect(target.xMin - TourPad, target.yMin - TourPad, target.xMax + TourPad, target.yMax + TourPad);
            if (!_tourSpotOn || point != _tourPointFor)
            {
                _tourHoleFrom = _tourSpotOn ? _tourHole : target;
                _tourHoleT = _tourSpotOn && !Motion.Reduced ? 0f : 1f;
                _tourPointFor = point;
            }
            _tourSpotOn = true;
            if (!_tourSpot.gameObject.activeSelf) _tourSpot.gameObject.SetActive(true);
            _tourSpot.SetAsLastSibling();
            _tourHoleT = Mathf.Min(1f, _tourHoleT + Time.unscaledDeltaTime / 0.25f);
            float e = 1f - (1f - _tourHoleT) * (1f - _tourHoleT);
            var hole = _tourHole = new Rect(
                Vector2.Lerp(_tourHoleFrom.position, target.position, e),
                Vector2.Lerp(_tourHoleFrom.size, target.size, e));

            var full = _tourSpot.rect;
            float dim = talking ? 0.62f : 0.34f;
            SetBox(_tourDim[0], full.xMin, hole.yMax, full.xMax, full.yMax, dim);   // above
            SetBox(_tourDim[1], full.xMin, full.yMin, full.xMax, hole.yMin, dim);   // below
            SetBox(_tourDim[2], full.xMin, hole.yMin, hole.xMin, hole.yMax, dim);   // left
            SetBox(_tourDim[3], hole.xMax, hole.yMin, full.xMax, hole.yMax, dim);   // right

            float pulse = Motion.Reduced ? 1f : 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
            SetBox(_tourFrame[0], hole.xMin, hole.yMax - TourFrameW, hole.xMax, hole.yMax, pulse);
            SetBox(_tourFrame[1], hole.xMin, hole.yMin, hole.xMax, hole.yMin + TourFrameW, pulse);
            SetBox(_tourFrame[2], hole.xMin, hole.yMin, hole.xMin + TourFrameW, hole.yMax, pulse);
            SetBox(_tourFrame[3], hole.xMax - TourFrameW, hole.yMin, hole.xMax, hole.yMax, pulse);

            // The pointer comes in from the side facing the middle of the screen, so it never runs off the edge the
            // thing it points at is standing against: from above or below, unless the thing stands about the screen's
            // middle height, where there is room either side (a key in a row would have its neighbour under it).
            Vector2 c = hole.center;
            bool sideways = Mathf.Abs(c.y) / Mathf.Max(1f, full.height) < 0.18f;
            Vector2 dir = sideways ? new Vector2(c.x > 0f ? 1f : -1f, 0f) : new Vector2(0f, c.y > 0f ? 1f : -1f);
            float bob = Motion.Reduced ? 0f : 5f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            Vector2 edge = new Vector2(dir.x == 0f ? c.x : dir.x > 0f ? hole.xMin : hole.xMax,
                                       dir.y == 0f ? c.y : dir.y > 0f ? hole.yMin : hole.yMax);
            _tourArrow.anchoredPosition = edge - dir * (6f + bob);
            _tourArrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private static void SetBox(Image img, float x0, float y0, float x1, float y1, float alpha)
        {
            var rt = img.rectTransform;
            rt.anchoredPosition = new Vector2(x0, y0);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
            var col = img.color;
            col.a = alpha;
            img.color = col;
        }

        private void HideTourSpot()
        {
            _tourSpotOn = false;
            _tourPointFor = "";
            if (_tourSpot != null && _tourSpot.gameObject.activeSelf) _tourSpot.gameObject.SetActive(false);
        }
    }
}
