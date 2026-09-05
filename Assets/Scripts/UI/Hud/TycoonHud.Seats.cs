using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Seats: the floor: who is on which stool, how they walk in and out, what they order,.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        private void ResetSeats()
        {
            foreach (var v in _seats)
            {
                v.Visit = null;
                v.Note = default;
                HushSeat(v);
                v.Look = null;          // see AdvanceExit: a stool with nobody on it has no face
                v.Exiting = false;
                v.ExitT = 0f;
                v.WalkT = 0f;
                if (v.Group != null) v.Group.alpha = 1f;
                if (v.Root != null) v.Root.gameObject.SetActive(false);
                SyncPatronBody(v);
            }
        }

        /// <summary>Is there anybody still on the screen? A stool's view is hidden by
        /// <see cref="AdvanceExit"/> on the frame it reaches the door, and a settled tab
        /// keeps counting for a beat after that — both have to be finished, or the books
        /// land on top of the thing they are counting.</summary>
        private bool FloorIsClear()
        {
            if (_tabFloats > 0) return false;
            foreach (var v in _seats)
                if (v.Root != null && v.Root.gameObject.activeSelf) return false;
            return true;
        }

        // ── the floor ───────────────────────────────────────────────────────────


        /// <summary>The font on the counter, clicked: straight to the draught station. Nothing
        /// is checked here that the flow does not check itself — but a panel already open
        /// keeps the room, exactly as a seat does, because a click through an open sheet is
        /// how the bench lost its tin twice.</summary>
        private void OnTapClicked()
        {
            if (_flow != null && _flow.IsOpen) return;
            _flow?.OpenTap();
        }

        private void OnSeatClicked(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;   // finish the build first
            // NOT WITH THE ROOM LIFTED (2026-08-22, the author: "Ekran aşağı kayıkken yani
            // backbar açıkken servis yapılmamalı"). With the cellar open you are turned round
            // to the bar's own body, the drinkers have ridden up out of the way and their
            // tickets are down — a stool that still answered a click there would be serving
            // somebody you cannot see, from a room you are not facing.
            if (CellarOpen) return;
            var visit = _seats[index].Visit;
            if (visit == null) return;

            // A bowl in hand goes down in front of them (v5 P16). Before the waiting check,
            // because a customer nursing a drink is exactly who takes a bowl of nuts — and
            // Core's own refusals do the talking when the snack cannot land (never alone,
            // bowl empty), so the toast is the rule speaking, not the menu's guess at it.
            if (_snackInHand != null)
            {
                var snack = _snackInHand;
                _snackInHand = null;
                try
                {
                    run.ServeSnack(snack.Id, visit);
                    Sfx.Play("bowl_down", 0.75f);
                    Toast($"{snack.Name.ToUpperInvariant()} — ON THE TAB");
                }
                catch (InvalidOperationException e) { Toast(e.Message.ToUpperInvariant()); }
                RefreshSnackRow(run);
                return;
            }

            if (visit.State != VisitState.Waiting) return;
            if (!visit.HasOrdered) return;   // still deciding — no order to read yet (2026-07-23)

            // Clicking a customer reads their licence (GDD 24 §5), and that is ALL it does
            // again (2026-08-11): serving is dragging the glass onto them. One click, one
            // meaning — the click-to-serve road had a drink on the counter turning the
            // licence into a second-click affair, which is how you end up serving somebody
            // you meant to read.
            ShowId(visit);
        }

        /// <summary>
        /// THE TICKET'S BOTTOM ROW: what they want, a rule, then how they want it served.
        /// Returns how wide it came out — the plate is sized to its widest line and this row
        /// is very often it.
        ///
        /// Every mark is asked for by the PREPARATION'S OWN ID, so a preparation added to the
        /// garnish pool tomorrow needs a mask in ChromeArt and nothing else here. A mark that
        /// does not exist yet comes back null and leaves a gap rather than throwing — the
        /// same contract every other mark in this game is drawn under.
        ///
        /// Placed by hand, not by a layout group: this row is centred on a plate whose width
        /// is decided in the same frame, and a layout group would settle a frame late — which
        /// on a ticket that grows one character at a time is a row that visibly lags its own
        /// balloon. (16 §0: positions here are absolute, deliberately.)
        /// </summary>
        private float LayOutOrderIcons(SeatView view, CustomerVisit visit, bool show)
        {
            var row = view.IconRow;
            if (row == null) return 0f;
            if (!show || visit == null)
            {
                if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                return 0f;
            }
            if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);

            var drink = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            var spec = visit.Order.Garnishes;
            int marks = 0;
            for (int i = 0; i < view.Garnish.Length; i++)
            {
                var mark = spec != null && i < spec.Count ? ChromeArt.Mark(spec[i].Id) : null;
                view.Garnish[i].sprite = mark;
                view.Garnish[i].enabled = mark != null;
                if (mark != null) marks++;
            }
            if (view.Icon != null)
            {
                view.Icon.sprite = drink;
                view.Icon.enabled = drink != null;
            }

            // 24 for the drink because that is the size DrinkIcon draws a glass at, and 16 for
            // the marks because that is the size they are authored at. Neither is scaled: a
            // pixel drawing squeezed to fit a row it does not belong in is the fault this
            // whole ticket was rebuilt to stop making.
            const float DrinkW = 24f, MarkW = 16f, RuleW = 1f, Gap = 5f, MarkGap = 3f;
            float w = drink != null ? DrinkW : 0f;
            if (marks > 0)
            {
                if (w > 0f) w += Gap + RuleW + Gap;
                w += marks * MarkW + (marks - 1) * MarkGap;
            }
            row.sizeDelta = new Vector2(w, IconRowH);

            float x = 0f;
            if (drink != null)
            {
                view.Icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += DrinkW;
            }
            if (view.IconRule != null)
            {
                bool ruled = marks > 0 && drink != null;
                view.IconRule.enabled = ruled;
                if (ruled)
                {
                    view.IconRule.rectTransform.anchoredPosition = new Vector2(x + Gap, 0f);
                    x += Gap + RuleW + Gap;
                }
            }
            for (int i = 0; i < view.Garnish.Length; i++)
            {
                if (!view.Garnish[i].enabled) continue;
                view.Garnish[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += MarkW + MarkGap;
            }
            return w;
        }

        /// <summary>Which seat the pointer is over, or −1. A rect test on the stool's own
        /// hit plate, so the drop lands wherever the customer is standing rather than on a
        /// guessed column — and it costs nothing to ask five stools.</summary>
        private int SeatUnderPointer(Mouse mouse)
        {
            if (mouse == null) return -1;
            var p = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var root = _seats[i].Root;
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                if (_seats[i].Visit == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(root, p, null)) return i;
            }
            return -1;
        }

        /// <summary>
        /// HOW LONG A LINE STAYS UP (2026-09-04, the author: "birkaç saniye görünüp sonra yok
        /// olmalı"). Long enough to read a clause of the game's own 8px face twice over at a
        /// comfortable pace, short enough that a room of six is never a wall of paper.
        /// </summary>
        private const float SaySeconds = 4.5f;

        /// <summary>How wide a spoken line is allowed to get before it wraps. Wider than the
        /// ticket's cap because a balloon is up for four seconds and a ticket is up all
        /// night: a line that overhangs a neighbour's head briefly is readable, one that
        /// parks there is a layout fault. Still inside the gap between two stools.</summary>
        private const float SayMaxW = SeatGap - 4f;

        /// <summary>
        /// Puts one line in a drinker's balloon and starts its clock. An empty line says
        /// nothing at all — a pint with a good head and every garnish on it has no business
        /// making a speech, and silence there is the note working rather than failing.
        /// </summary>
        private void SayIt(SeatView view, string line)
        {
            if (view == null || view.Say == null || view.SayText == null) return;
            if (string.IsNullOrEmpty(line)) { HushSeat(view); return; }
            view.SayText.text = line.ToUpperInvariant();
            view.SayUntil = Time.unscaledTime + SaySeconds;
            view.Say.gameObject.SetActive(true);
            LayOutSay(view);
        }

        /// <summary>Takes a balloon down and forgets what was in it.</summary>
        private static void HushSeat(SeatView view)
        {
            if (view == null || view.Say == null) return;
            view.SayUntil = 0f;
            if (view.SayText != null) view.SayText.text = "";
            if (view.Say.gameObject.activeSelf) view.Say.gameObject.SetActive(false);
        }

        /// <summary>
        /// THE BALLOON IS THE SIZE OF WHAT IS IN IT (the author: "baloncuk metnin boyutuna
        /// göre boyutlandırılacak"). One measurement, both ways: the line's own preferred
        /// width up to the cap, then however many rows that width forces, then the plate is
        /// the sum of them plus the padding and the balloon's own foot. The same arithmetic
        /// the ticket does — it is the same drawn balloon, and neither may ever clip type.
        /// </summary>
        private void LayOutSay(SeatView view)
        {
            var text = view.SayText;
            float widest = text.preferredWidth;
            float cardW = Mathf.Clamp(widest + TagPad * 2f, TagMinW, SayMaxW);
            float textW = cardW - TagPad * 2f;
            int lines = text.text.Length == 0 ? 0
                : Mathf.Max(1, Mathf.CeilToInt(widest / Mathf.Max(1f, textW)));
            view.Say.sizeDelta = new Vector2(cardW,
                lines * view.SayLineH + TagPad * 2f + TagFoot);
        }

        /// <summary>Hands the ready drink to seat <paramref name="index"/> (the glass was dragged
        /// onto them). Returns true if it was served.</summary>
        private bool ServeSeat(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return false;
            var visit = _seats[index].Visit;
            if (visit == null || visit.State != VisitState.Waiting) return false;
            if (!visit.HasOrdered) return false;   // can't hand a drink to someone still deciding
            if (!visit.IdInspected) return false;  // only TAKEN orders are servable (HUD rule, 2026-08-11)
            if (!run.DrinkReady) return false;     // only what is in the glass goes out

            // WAS IT ALREADY PERFECT BEFORE THIS ONE? Core keeps a set, not an event, so
            // the first time is a thing only the caller can see: ask before, ask after
            // (2026-08-25, the author: a perfect pour must announce itself). The key is
            // the ORDERED recipe — the same one TycoonRun files the perfect under.
            var asked = visit.Order.Wanted;
            bool knewItAlready = run.IsPerfected(asked.Id);

            // WHAT THEY WILL SAY ABOUT IT, TAKEN NOW (2026-09-04). ServeTo empties the
            // serving glass into the visit, so the pour can only be read on this side of the
            // call — and it is read ONCE and kept, because the note must not change while
            // they are drinking it. Against the ORDERED drink: that is the pour the player
            // was aiming at, so that is the pour worth coaching.
            // …and what they asked for ON it goes in with it: a garnish that was ordered
            // and never arrived is the second half of what they have to say. visit.Order is
            // legal here because ServeSeat has already refused an unread card.
            _seats[index].Note = PourAdvice.For(asked, run.ServingGlass,
                id => run.Shelf.Find(id)?.Ingredient, visit.Order.Spec);
            // …and they SAY it, now, over the glass they were just handed — not when they
            // start drinking. An extra round never enters Drinking at all (CustomerVisit
            // .Resolve refreshes the order and stays Waiting), and the drink they are
            // commenting on is the one that just landed either way.
            SayIt(_seats[index], _seats[index].Note.Sentence);

            var verdict = run.ServeTo(visit);
            CloseId();
            Sfx.Play("serve_clink");                          // the glass lands in front of them
            if (verdict.PerfectMake && !knewItAlready && run.IsPerfected(asked.Id))
                NotePerfect(asked);
            LogVerdict(visit, verdict);
            StartCoroutine(ServeReaction(index, verdict));   // reaction + payment float up
            return true;
        }

        // ── the bussing beat (D2, v5 P14) ────────────────────────────────────────
        // A drinker leaves the empty glass on the stool, and the stool stays blocked until it
        // is cleared: the player's click does it now, the bar's slow clock does it in seven
        // seconds. The prop appears where the customer sat; clicking it is the bussing.

        private void RefreshDirtyGlasses(TycoonRun run)
        {
            foreach (var v in _seats)
            {
                bool show = v.Dirty != null && !v.Dirty.Cleared;
                if (show && v.DirtyProp == null)
                {
                    var prop = NewRect("DirtyGlass", _hudRoot);
                    prop.anchorMin = prop.anchorMax = new Vector2(0, 0);
                    prop.pivot = new Vector2(0.5f, 0);
                    prop.sizeDelta = new Vector2(34, 52);
                    var img = prop.gameObject.AddComponent<Image>();
                    // The SAME glass everywhere (the author, 2026-08-02): the empty on the
                    // counter is the drawn vessel the drink was served in, at its line's
                    // tier — not a stock photo of some other glass.
                    GlasswareDefinition dirtyDef = null;
                    if (run != null && v.Dirty.GlasswareId != null)
                        foreach (var g in run.Glassware)
                            if (g.Id == v.Dirty.GlasswareId) { dirtyDef = g; break; }
                    // If the line is unknown the glass stays UNDRAWN rather than borrowing
                    // a stock one — that is the same rule, held at its edge. The old
                    // `ItemArt.Glass` fallback did exactly what the rule forbids, and its
                    // art was a pre-v3 leftover deleted with the fridge; the colour set
                    // below is what a sprite-less prop is already dressed in.
                    if (dirtyDef != null)
                        img.sprite = GlassArt.For(dirtyDef, run.GlassTier(dirtyDef.Id)).Sprite;
                    img.preserveAspect = true;
                    img.color = new Color(1f, 1f, 1f, 0.85f);
                    if (img.sprite == null) img.color = new Color(0.8f, 0.9f, 0.95f, 0.5f);
                    var view = v;
                    var btn = prop.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() =>
                    {
                        if (view.Dirty == null) return;
                        view.Dirty.Bus();
                        Sfx.Play("glass_down", 0.9f);
                        Toast("GLASS CLEARED — SEAT IS FREE");
                    });
                    // ...and it says so before it is pressed (2026-08-26): the author's
                    // rule is about this KIND of interaction, not about the menu alone.
                    var dirtyRelay = prop.gameObject.AddComponent<HoverRelay>();
                    var dirtyRt = prop;
                    dirtyRelay.Entered = () => ShowPropTip(dirtyRt, "CLEAR THE GLASS");
                    dirtyRelay.Exited = () => HidePropTip(dirtyRt);
                    var sink = prop.gameObject.AddComponent<PressSink>();
                    sink.Face = prop; sink.Depth = 3f; sink.Lift = 3f; sink.Tint = img;
                    v.DirtyProp = prop;
                }
                else if (!show && v.DirtyProp != null)
                {
                    Destroy(v.DirtyProp.gameObject);
                    v.DirtyProp = null;
                    v.Dirty = null;
                }
                // ON the counter's drawn surface, not floating at the waist-clip line (the
                // author's report): the clip line is the counter's BACK edge; the top surface
                // the glass stands on reads ~36px lower in the scene.
                //
                // AND IT IS PLACED EVERY FRAME, not once when it is made (2026-08-25, the
                // author: "içilip tezgahta kalan bardaklar tezgahla beraber hareket etmiyorlar
                // ekranda sabit kalıyorlar"). The empties are the last thing standing on the
                // bar that was still written as a one-off position, so when the cellar lifted
                // the counter they stayed exactly where the drinker had left them — on the
                // screen rather than on the wood. The book beside them takes the same lift off
                // the same dial (PlaceBookProp); this is that, for the glasses.
                if (v.DirtyProp != null)
                    v.DirtyProp.anchoredPosition =
                        new Vector2(v.SeatX, CounterLineY - 36f + CounterLift);
            }
        }

        private void BuildSnackRow(RectTransform root)
        {
            var run = Run;
            if (run == null || run.Snacks.Count == 0) return;
            float x = 24f;
            foreach (var snack in run.Snacks)
            {
                var s = snack;
                var bowl = NewRect($"Snack_{s.Id}", root);
                bowl.anchorMin = bowl.anchorMax = bowl.pivot = new Vector2(0f, 0f);
                bowl.sizeDelta = new Vector2(76, 84);
                // ON the counter, which is what the comment above has always claimed:
                // at 96 they stood on the bar's FRONT panel, across the shelf bays,
                // and the glassware that belongs in those bays had nowhere to go.
                bowl.anchoredPosition = new Vector2(x, 190f);
                x += 82f;
                var hit = bowl.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0.001f);

                var art = NewRect("Art", bowl);
                Place(art, new Vector2(0.5f, 1), new Vector2(72, 54), new Vector2(0, 0));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load($"snack_{s.Id}");
                img.preserveAspect = true; img.raycastTarget = false;
                if (img.sprite == null) img.color = UITheme.Amber[2];   // no art yet: a warm chip

                var label = NewText("N", bowl, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
                Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(96, 24), Vector2.zero);
                label.text = s.Name.ToUpperInvariant();

                var btn = bowl.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                var snackRelay = bowl.gameObject.AddComponent<HoverRelay>();
                var snackRt = bowl;
                var snackName = s.Name.ToUpperInvariant();
                snackRelay.Entered = () => ShowPropTip(snackRt, "TAKE THE " + snackName);
                snackRelay.Exited = () => HidePropTip(snackRt);
                btn.onClick.AddListener(() =>
                {
                    var r = Run;
                    if (r == null || r.Phase != TycoonPhase.DayOpen) return;
                    if (r.SnackLeft(s.Id) <= 0) { Toast($"THE {s.Name.ToUpperInvariant()} BOWL IS EMPTY TODAY"); return; }
                    _snackInHand = _snackInHand == s ? null : s;   // click again to put it back
                    Sfx.Play(_snackInHand != null ? "garnish" : "glass_down", 0.8f);
                    Toast(_snackInHand != null
                        ? $"{s.Name.ToUpperInvariant()} IN HAND — CLICK A CUSTOMER"
                        : "PUT IT BACK");
                    RefreshSnackRow(r);
                });
                var sink = bowl.gameObject.AddComponent<PressSink>();
                sink.Face = art; sink.Depth = 4f; sink.Lift = 3f; sink.Tint = img;

                _snackBowls.Add((s, img, label));
            }
        }

        /// <summary>Stock counts and the in-hand highlight, redrawn after anything changes.</summary>
        private void RefreshSnackRow(TycoonRun run)
        {
            foreach (var (snack, art, stock) in _snackBowls)
            {
                int left = run.SnackLeft(snack.Id);
                stock.text = left > 0
                    ? $"{snack.Name.ToUpperInvariant()} · {left}"
                    : $"{snack.Name.ToUpperInvariant()} · OUT";
                var baseCol = art.sprite != null ? Color.white : UITheme.Amber[2];
                art.color = left <= 0 ? new Color(baseCol.r, baseCol.g, baseCol.b, 0.35f)
                    : _snackInHand == snack ? new Color(1f, 1f, 0.82f, 1f)
                    : baseCol;
            }
        }

        // ── the counter's prep RAIL (2026-08-26) ──────────────────────────────
        //
        // The author: "tezgah sahnesinde buz limon tuz şeker gibi nesneler için tezgah
        // boyuna oranlı görseller üretilecek, eğer oyuncu servis et dedikten sonra buz
        // limon şeker koymayı unutursa diye." A drink that leaves the bench unfinished
        // used to mean a walk back through the whole flow; the four stations stand on the
        // ROOM's counter now, at the counter's own scale (32px art at a whole 2×,
        // Tools/bench_props_gen.py minis), beside where the made drink rests. They only
        // offered themselves while a served drink was standing there, and they were the
        // FORGIVING door: a plain press, no lap and no aim.
        //
        // IT IS THE WHOLE VERB NOW (2026-08-26, the author: "buz, zeytin, tuz, şeker,
        // nane gibi bardağa koyulan şeyler ana sahnede tezgahta dursun ... bardağa ana
        // sahnedeki nesnelerden sürükleyerek koyulabilecek"). What goes IN a glass
        // stopped being part of building the mix: you pour the drink out of the tin,
        // come back to the room, and finish it on the bar with your hands. So the props
        // STAND on the counter all night, whether or not there is a glass in front of
        // them, and they are DRAGGED — the same verb, and the same weight, as carrying
        // the drink to a stool. Six, not four: the olive and the mint used to live on
        // the glass bench's garnish rail and came here with the rest of them.
        private RectTransform _prepRail;
        private readonly List<PrepProp> _prepProps = new List<PrepProp>();

        private sealed class PrepProp
        {
            public string Id;
            public RectTransform Rt;
            public Image Img;
            /// <summary>What leaves the dish when it is picked up: a CUBE off the bucket,
            /// a WEDGE off the bowl, a spear off the jar (2026-08-26, the author: "buz
            /// kovasindan buz alirsin buz kovasi degil"). Null on the two rims, which are
            /// the one case where the dish itself is carried — you turn the glass IN it.
            /// The sprite is the same one that ends up floating in the drink, so what you
            /// pick up and what you see in the glass are one object.</summary>
            public string Carry;
            public Vector2 CarrySize;
            /// <summary>The volumeless mark this prop puts on the glass, or null when the
            /// prop is stock rather than a mark.</summary>
            public PreparationDefinition Prep;
            /// <summary>The ingredient STYLE this prop pours, or null when it is a mark.
            /// Resolved to a shelf bottle at drop time, because the bar's stock changes.</summary>
            public string Style;
            public bool IsRim;           // turned in the dish, not dropped in the glass
            public string Word;          // what the pointer is told it does
        }

        // ── the rim, turned on the counter (2026-08-26) ──────────────────────────
        //
        // The lap arithmetic is the glass bench's own, moved with the dishes: hold the dish
        // over the drink and run a full circle round its MOUTH with the cursor. It came here
        // because the dishes did, and it came WHOLE - taking the dishes off that bench and
        // applying salt on a single drop would have deleted a skill the author asked for
        // eight days earlier ("tuz artik bardagin etrafinda cevirerek ... ufak bir skill
        // oyunu") without anybody asking for it back.
        //
        // The numbers are the bench's, to the unit, so a player who learned the lap there
        // does not have to learn it again: a band round the mouth where the sweep counts, a
        // single-frame jump bigger than a third of a lap thrown away as the cursor crossing
        // the glass rather than a hand moving, and a part-run lap KEPT against its dish.
        private const int RimSegments = 14;
        private const float RimLap = 2f * Mathf.PI;
        private const float RimNear = 28f, RimFar = 150f;
        private readonly Dictionary<string, float> _rimSwept = new Dictionary<string, float>();
        private RectTransform _rimRing;
        private readonly List<Image> _rimTicks = new List<Image>();
        private float _rimAngle;
        private bool _rimAngleKnown;

        /// <summary>Standing on the counter, right of where the made drink rests. The rail
        /// rides CounterLift like everything else on the bar, so an open cellar takes it up
        /// with the room rather than leaving six dishes hanging in the air.</summary>
        // BETWEEN THE SINK AND THE DRIP MAT (2026-08-26, the author: "garnishler ekranda
        // sola kaymalı, bira matı ile sink arasında olmalı"). At X0 100 the rail ran to
        // stage 555 and its last two dishes stood ON the beer font; at -250 the six span
        // stage 195..380 — clear of the basin's right edge (181) and well short of the drip
        // mat (480). The finished drink moved right with it (see GlassHome), so the counter
        // reads left to right the way the night runs: sink, the makings, the drink, the tap.
        private const float PrepRailY = -196f, PrepRailX0 = -250f, PrepRailGap = 74f;

        // The piece in the hand: a copy of the prop's own drawing, following the cursor.
        private RectTransform _prepCarry;
        private Image _prepCarryImg;
        private PrepProp _prepHeld;

        // ── the grains a carried pinch sheds (2026-08-26) ────────────────────────
        //
        // The author: "surukleyen kucuk taneler dokuluyor gibi gozukebilir." A clump of
        // salt in a hand LEAKS, and the leak is what makes it read as loose crystals
        // rather than as a small white object. Two units square, falling under their own
        // gravity, fading as they go — and shed by DISTANCE TRAVELLED rather than by time,
        // so a pinch held still does not bleed onto the counter and one swept across the
        // bar leaves a trail behind the hand.
        private readonly List<(RectTransform Rt, Image Img, Vector2 Vel, float Born)> _grains
            = new List<(RectTransform, Image, Vector2, float)>();
        private Vector2 _grainLastAt;

        /// <summary>Set by <see cref="StepRimLap"/> on a frame the lap actually turned;
        /// read and cleared once a frame by the rail's step. One loop source, one
        /// decider — the tin bench's rule, applied to the counter.</summary>
        private bool _rimLoopWanted;

        // NOBODY IN THIS BAR HAS A VOICE ANY MORE (2026-09-04, the author: "konuşma sesi
        // olmayacak"). SpeakSeat lived here: four murmur clips, pitched by the stool so the
        // six seats sounded like six people, fired on the greeting, the order and the way
        // out. What they say is WRITTEN now — the bubble over the head carries the order, the
        // thinking beat and the note on the drink — and a murmur under a written line is the
        // same information twice, in the one channel that cannot be read at a glance or
        // turned off separately. The stool, the till and the room keep every sound they had;
        // only the mouths are quiet.
        private float _grainCarried;
        private const float GrainEvery = 26f;     // units of travel between crystals
        private const float GrainLife = 0.55f;
        private const float GrainFall = 520f;

        private void BuildMiniPreps(RectTransform root)
        {
            _prepRail = NewRect("CounterPreps", root);
            _prepRail.anchorMin = _prepRail.anchorMax = _prepRail.pivot = new Vector2(0.5f, 0.5f);
            _prepRail.sizeDelta = Vector2.zero;
            // Art first, fallback second: the 2026-08-26 counter set is drawn at the bar's
            // own eye line (the author: "masanin acisiyla ayni aciya sahip"), and the
            // 2026-08-25 minis stay behind it so a missing drawing never costs a verb.
            // SIX, AND TWO OF THEM ARE NOT PREPARATIONS (2026-08-26, the author listed
            // "buz, zeytin, tuz, seker, nane"). Ice and the two rims and the twist are
            // PreparationDefinitions - volumeless marks on the glass. The olive and the
            // mint are INGREDIENTS: they are stock, they come off the shelf, they run out,
            // and they are what recipes.json's "olive" and "mint" style bands are graded
            // against. So the rail carries two kinds of prop and each drops through its own
            // Core verb - AddPreparationAtGlass for a mark, PourAtGlass for a pinch of
            // stock. A garnish whose bottle the bar does not stock, or has emptied, is
            // simply not built: an empty jar on the counter is a promise the bar cannot keep.
            (string id, string art, string fallback, PreparationDefinition prep,
             string style, string word, string carry, float carryH)[] rail =
            {
                ("ice", "counter_ice", "bench_mini_ice", Preparations.Ice, null, "ICE",
                 "glass_ice", 34f),
                ("lemon_twist", "counter_lemon", "bench_mini_lemon", Preparations.LemonTwist,
                 null, "LEMON", "glass_lemon", 40f),
                ("olive", "counter_olive", "garnish_olive", null, "olive", "OLIVE",
                 "glass_olive", 52f),
                ("mint", "counter_mint", "garnish_mint", null, "mint", "MINT",
                 "glass_mint", 40f),
                // A PINCH, not the dish (2026-08-26, the author: "surukledigimiz tuz ve
                // seker daha cok tuz ve seker yumagi gibi olmali"). Carrying the whole
                // cellar was the same mistake the bucket made, and the answer is the same:
                // what leaves a dish of salt is salt. The lap still turns the GLASS in it —
                // the pinch in the hand is what you are turning it through.
                ("salt_rim", "counter_salt", "bench_mini_salt", Preparations.SaltRim,
                 null, "TURN IT IN THE SALT", "carry_salt", 32f),
                ("sugar_rim", "counter_sugar", "bench_mini_sugar", Preparations.SugarRim,
                 null, "TURN IT IN THE SUGAR", "carry_sugar", 30f),
            };
            for (int i = 0; i < rail.Length; i++)
            {
                var (id, art, fallback, prep, style, word, carry, carryH) = rail[i];
                var rt = NewRect("MP_" + id, _prepRail);
                Place(rt, new Vector2(0.5f, 0.5f), new Vector2(64, 64),
                    new Vector2(PrepRailX0 + i * PrepRailGap, PrepRailY));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load(art) ?? ItemArt.Load(fallback);
                img.preserveAspect = true;
                if (img.sprite == null) img.color = UITheme.Cyan[3];
                var glow = rt.gameObject.AddComponent<HoverGlow>();
                glow.Graphics = new Graphic[] { img };
                var carryArt = carry != null ? ItemArt.Load(carry) : null;
                var prop = new PrepProp
                {
                    Id = id, Rt = rt, Img = img, Prep = prep, Style = style, Word = word,
                    Carry = carryArt != null ? carry : null,
                    CarrySize = carryArt != null
                        ? new Vector2(carryH * (carryArt.rect.width / carryArt.rect.height),
                                      carryH)
                        : new Vector2(64f, 64f),
                    // A rim is TURNED, not dropped (2026-08-25's skill, re-homed here on
                    // 2026-08-26 when the dishes left the glass bench). See StepPrepCarry.
                    IsRim = id == "salt_rim" || id == "sugar_rim",
                };

                // PICKED UP, NOT PRESSED. A whole-rect PointerDown rather than a Button:
                // what follows is a carry, and a Button fires on the way back UP, after the
                // drop has already been decided.
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => GrabPrep(prop));
                rt.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
                var relay = rt.gameObject.AddComponent<HoverRelay>();
                var theRt = rt;
                relay.Entered = () => ShowPropTip(theRt, word);
                relay.Exited = () => HidePropTip(theRt);
                _prepProps.Add(prop);
            }

            // The piece in the hand. Built once, hidden, and dressed at every pick-up: a
            // sprite spawned per drag would be a new GameObject on every touch of the bar.
            _prepCarry = NewRect("PrepInHand", root);
            _prepCarry.anchorMin = _prepCarry.anchorMax = _prepCarry.pivot = new Vector2(0.5f, 0.5f);
            _prepCarry.sizeDelta = new Vector2(64, 64);
            _prepCarryImg = _prepCarry.gameObject.AddComponent<Image>();
            _prepCarryImg.preserveAspect = true;
            _prepCarryImg.raycastTarget = false;
            _prepCarry.gameObject.SetActive(false);

            _prepRail.gameObject.SetActive(false);
        }

        /// <summary>Takes a piece off the rail. Refused between days and behind a panel —
        /// the bar is not yours to reach across while the books are open.</summary>
        private void GrabPrep(PrepProp prop)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (CellarOpen || _prepCarry == null) return;
            _prepHeld = prop;
            // WHAT COMES OUT OF THE DISH, not the dish (2026-08-26). The hand used to
            // lift the whole bucket; you take a cube out of a bucket, and the cube you
            // take is the cube that ends up in the drink — the same sprite, so the pick,
            // the carry and the float are one object all the way through.
            var inHand = prop.Carry != null ? ItemArt.Load(prop.Carry) : null;
            _prepCarryImg.sprite = inHand ?? prop.Img.sprite;
            _prepCarryImg.color = _prepCarryImg.sprite != null ? Color.white : UITheme.Cyan[3];
            _prepCarry.sizeDelta = inHand != null ? prop.CarrySize : new Vector2(64f, 64f);
            _prepCarry.anchoredPosition = prop.Rt.anchoredPosition + _prepRail.anchoredPosition;
            _prepCarry.gameObject.SetActive(true);
            _prepCarry.SetAsLastSibling();
            _grainCarried = 0f;
            _grainLastAt = _prepCarry.anchoredPosition;
            if (prop.IsRim) Sfx.Play("grain_pinch", 0.5f);
            HidePropTip(prop.Rt);
            Sfx.Play("click", 0.4f);
        }

        /// <summary>The carry itself: the piece follows the cursor, and letting go over the
        /// glass puts it in the drink. Anywhere else it simply goes back — a garnish dropped
        /// on the floor is not a mechanic anybody asked for.</summary>
        private void StepPrepCarry(TycoonRun run)
        {
            if (_prepHeld == null) return;
            var mouse = Mouse.current;
            if (mouse == null) { DropPrep(false); return; }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_prepCarry.parent, mouse.position.ReadValue(), null,
                    out Vector2 at))
            {
                if (_prepHeld.IsRim)
                {
                    _grainCarried += (at - _grainLastAt).magnitude;
                    while (_grainCarried >= GrainEvery) { _grainCarried -= GrainEvery; ShedGrain(at); }
                }
                _grainLastAt = at;
                _prepCarry.anchoredPosition = at;
            }
            // A RIM DISH WORKS WHILE IT IS HELD. Everything else waits for the release.
            bool glassOut = _glassShown && !_glassServing && !_glassReturning
                            && _drinkGlass != null && run != null && run.DrinkReady;
            if (_prepHeld.IsRim && glassOut)
            {
                bool done = _prepHeld.Prep != null
                            && !run.ServingGlass.HasPreparation(_prepHeld.Id)
                            && StepRimLap(run, _prepHeld, mouse.position.ReadValue());
                if (done) { DropPrep(false); return; }
            }
            else ShowRimRing(false);

            if (mouse.leftButton.isPressed) return;

            bool overGlass = glassOut
                && RectTransformUtility.RectangleContainsScreenPoint(
                       _drinkGlass, mouse.position.ReadValue(), null);
            if (!overGlass && _prepHeld != null) Sfx.Play("dish_down", 0.55f);
            DropPrep(overGlass);
        }

        /// <summary>One crystal off the pinch, thrown a little sideways and then falling.
        /// Its sideways kick comes from the grain COUNT, not from a roll: the same hand
        /// движение sheds the same trail, and the determinism rule never comes up.</summary>
        private void ShedGrain(Vector2 at)
        {
            if (_prepHeld == null || _prepCarry == null) return;
            var rt = NewRect("Grain", (RectTransform)_prepCarry.parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3f, 3f);
            rt.anchoredPosition = at + new Vector2(0f, -12f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = _prepHeld.Id == "salt_rim"
                ? new Color(0.95f, 0.96f, 0.97f) : new Color(0.94f, 0.89f, 0.77f);
            img.raycastTarget = false;
            float kick = ((_grains.Count * 37) % 41) / 20f - 1f;   // -1..1, walked, not rolled
            _grains.Add((rt, img, new Vector2(kick * 34f, -20f), Time.unscaledTime));
        }

        /// <summary>The shed crystals, falling. Cheap: a handful of 3-unit rects with a
        /// half-second life, and the list is empty the moment the hand is empty.</summary>
        private void StepGrains()
        {
            if (_grains.Count == 0) return;
            float now = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = _grains.Count - 1; i >= 0; i--)
            {
                var (rt, img, vel, born) = _grains[i];
                float k = (now - born) / GrainLife;
                if (rt == null || k >= 1f)
                {
                    if (rt != null) Destroy(rt.gameObject);
                    _grains.RemoveAt(i);
                    continue;
                }
                vel = new Vector2(vel.x * 0.94f, vel.y - GrainFall * dt);
                rt.anchoredPosition += vel * dt;
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, 1f - k * k);
                _grains[i] = (rt, img, vel, born);
            }
        }

        private void DropPrep(bool intoTheGlass)
        {
            var prop = _prepHeld;
            _prepHeld = null;
            if (_prepCarry != null) _prepCarry.gameObject.SetActive(false);
            ShowRimRing(false);
            _rimAngleKnown = false;
            _rimLoopWanted = false;
            Sfx.HoldLoop(null);
            if (!intoTheGlass || prop == null) return;
            // A RIM IS NEVER APPLIED BY A DROP. Putting the dish down over the glass is
            // putting the dish down; what puts salt on a rim is the lap, and a half-run one
            // waits on the counter until the dish is picked up again.
            if (prop.IsRim) return;
            var run = Run;
            if (run == null) return;

            // Stock, not a mark: the olive and the mint are poured out of a bottle the bar
            // owns, through the one Core verb that puts an ingredient straight into the
            // serving glass. A pinch, measured against the GLASS rather than the tin -
            // PourGarnish's own fraction, in the vessel this drop is actually aimed at.
            if (prop.Prep == null)
            {
                var bottle = GarnishOnTheShelf(run, prop.Style);
                if (bottle == null) { Toast("NONE LEFT"); return; }
                double pinch = run.ServingGlass.Capacity * GarnishPinch;
                if (run.PourAtGlass(bottle.Id, pinch) <= 0)
                { Toast("THE GLASS IS FULL"); return; }
                Sfx.Play("garnish");
                Toast(bottle.Name.ToUpperInvariant() + " IN THE DRINK", UITheme.Lime[3]);
                return;
            }

            if (prop.Id != "ice" && run.ServingGlass.HasPreparation(prop.Id))
            {
                Toast("ALREADY ON THAT DRINK");
                return;
            }
            run.AddPreparationAtGlass(prop.Prep);
            Sfx.Play(prop.Id == "ice" ? "ice_drop" : "garnish");
            Toast(prop.Id == "ice"
                ? "ICE IN THE GLASS x" + run.ServingGlass.IceCubes
                : prop.Prep.Name.ToUpperInvariant() + " ON THE DRINK", UITheme.Cyan[3]);
        }

        /// <summary>How much of the glass one tap of a garnish is worth. The tin's own
        /// GarnishClickFraction, so a pinch is a pinch wherever it is taken.</summary>
        private const double GarnishPinch = 0.05;

        /// <summary>The bar's bottle of this garnish style, or null if it stocks none or has
        /// emptied the one it had. Asked at DROP time and never cached: a jar the bar ran out
        /// of halfway through a night must stop pouring halfway through that night.</summary>
        private static IngredientCard GarnishOnTheShelf(TycoonRun run, string style)
        {
            if (run == null || string.IsNullOrEmpty(style)) return null;
            foreach (var b in run.Shelf.Bottles)
                if (!b.IsEmpty && b.Ingredient.Type == IngredientType.Garnish
                    && b.Ingredient.Info != null && b.Ingredient.Info.Style == style)
                    return b.Ingredient;
            return null;
        }

        /// <summary>
        /// The lap, run against the drink standing on the counter. Called from StepPrepCarry
        /// while a rim dish is in the hand; returns true when the crust went on, which is the
        /// signal to put the dish back.
        /// </summary>
        private bool StepRimLap(TycoonRun run, PrepProp prop, Vector2 screen)
        {
            var mouth = GlassMouth();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_prepCarry.parent, screen, null, out Vector2 local))
                return false;
            ShowRimRing(true);
            PlaceRimRing(mouth, prop);

            _rimLoopWanted = true;      // consumed once a frame by StepPreps

            var arm = local - mouth;
            float dist = arm.magnitude;
            if (dist < RimNear || dist > RimFar) { _rimAngleKnown = false; return false; }

            float angle = Mathf.Atan2(arm.y, arm.x);
            if (_rimAngleKnown)
            {
                float step = Mathf.Abs(Mathf.DeltaAngle(_rimAngle * Mathf.Rad2Deg,
                                                        angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (step < RimLap / 3f)
                {
                    _rimSwept.TryGetValue(prop.Id, out float swept);
                    swept += step;
                    _rimSwept[prop.Id] = swept;
                    if (swept >= RimLap)
                    {
                        run.AddPreparationAtGlass(prop.Prep);
                        Sfx.Play("rim_done", 0.9f);
                        _rimSwept.Remove(prop.Id);
                        Toast((prop.Id == "salt_rim" ? "SALT" : "SUGAR") + " ON THE RIM",
                              UITheme.Lime[3]);
                        _rimAngleKnown = false;
                        return true;
                    }
                }
            }
            _rimAngle = angle;
            _rimAngleKnown = true;
            return false;
        }

        /// <summary>Where the drink's mouth is, in the rail's own space.</summary>
        private Vector2 GlassMouth() =>
            _drinkGlass == null ? Vector2.zero
            : _drinkGlass.anchoredPosition + new Vector2(0f, _drinkGlass.rect.height * 0.34f);

        /// <summary>
        /// The lap's instrument (2026-08-26, the author: "tuz ve sekeri bardagin etrafina
        /// surdugumuz mini oyun gelistirilsin, gorsel olarak hic estetik ve iyi degil").
        ///
        /// It was fourteen 5×13 rectangles on a circle, half-lit, and nothing else: no
        /// centre, no reading, no sense of a lap being RUN — the author's word for it was
        /// "boxes", and that is what it was. Four things now, and each earns its place:
        ///
        ///   the SEAT   a dim ring of the same fourteen marks, so the circle you are being
        ///              asked to run is visible before you start running it
        ///   the CRUST  the marks behind the sweep, in the dish's own colour and TALLER —
        ///              a crust builds up, so the mark grows as it takes
        ///   the HEAD   the mark under the cursor burns brighter and stands proudest, which
        ///              is the one thing that says "this is where you are"
        ///   the COUNT  the lap's own percentage in the middle of the glass's mouth, in the
        ///              house's display face, so a half-run rim is a number and not a guess
        /// </summary>
        private void ShowRimRing(bool on)
        {
            if (_rimRing == null)
            {
                if (!on) return;
                _rimRing = NewRect("RimRing", (RectTransform)_prepCarry.parent);
                _rimRing.anchorMin = _rimRing.anchorMax = _rimRing.pivot = new Vector2(0.5f, 0.5f);
                _rimRing.sizeDelta = Vector2.zero;
                for (int i = 0; i < RimSegments; i++)
                {
                    var tick = NewRect("T" + i, _rimRing);
                    tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                    tick.sizeDelta = new Vector2(6f, 12f);
                    var img = tick.gameObject.AddComponent<Image>();
                    img.sprite = ChromeArt.Card();
                    img.type = Image.Type.Sliced;
                    img.raycastTarget = false;
                    _rimTicks.Add(img);
                }
                _rimCount = NewText("Lap", _rimRing, _display, 8, TextAnchor.MiddleCenter,
                                    UITheme.Cream[4]);
                Place(_rimCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120, 16),
                      Vector2.zero);
                _rimCount.raycastTarget = false;
                var edge = _rimCount.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
                edge.effectDistance = new Vector2(1f, -1f);
            }
            if (_rimRing.gameObject.activeSelf != on) _rimRing.gameObject.SetActive(on);
            if (on) _rimRing.SetAsLastSibling();
        }

        private Text _rimCount;

        /// <summary>Stands the ring on the drink's mouth and colours it by how far the lap
        /// has run — tick colour, no arc: the bench's own reading, at the bench's own
        /// fourteen segments, so the two are one gesture with one picture.</summary>
        private void PlaceRimRing(Vector2 mouth, PrepProp prop)
        {
            if (_rimRing == null) return;
            _rimRing.anchoredPosition = mouth;
            _rimSwept.TryGetValue(prop.Id, out float swept);
            float ran = Mathf.Clamp01(swept / RimLap);
            var lit = prop.Id == "sugar_rim" ? UITheme.Amber[4] : UITheme.Cream[4];
            var seat = new Color(1f, 1f, 1f, 0.15f);
            // The head is where the HAND is, not where the fill ends: the lap counts a
            // swept ANGLE, so the cursor may be anywhere on the circle while the crust
            // fills from the start. The mark under the cursor is the one that burns.
            int head = _rimAngleKnown
                ? Mathf.RoundToInt(Mathf.Repeat(_rimAngle / RimLap, 1f) * RimSegments) % RimSegments
                : -1;
            for (int i = 0; i < _rimTicks.Count; i++)
                {
                float a = (i / (float)RimSegments) * RimLap;
                bool crusted = (i / (float)RimSegments) < ran;
                bool burning = i == head;
                // A crust BUILDS: the mark grows as it takes, and the one being laid now
                // stands proudest of all.
                float len = burning ? 20f : crusted ? 15f : 10f;
                float wide = burning ? 8f : crusted ? 7f : 5f;
                var rt = _rimTicks[i].rectTransform;
                rt.sizeDelta = new Vector2(wide, len);
                rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RimRingRadius;
                rt.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
                _rimTicks[i].color = burning ? Color.white : crusted ? lit : seat;
            }
            if (_rimCount != null)
            {
                _rimCount.text = Mathf.RoundToInt(ran * 100f) + "%";
                _rimCount.color = ran >= 1f ? UITheme.Lime[3] : lit;
            }
        }

        /// <summary>How far out the ring stands from the mouth. Inside RimFar and outside
        /// RimNear, so the marks sit in the band the sweep is actually counted in — a ring
        /// drawn where the hand is not counted is a ring that lies.</summary>
        private const float RimRingRadius = 62f;

        /// <summary>
        /// The rail, every frame: out whenever the bar is open, dimmed where a piece has
        /// already been used, and carrying whatever is in the hand.
        ///
        /// ALWAYS OUT (2026-08-26). It used to appear only beside a finished drink, which
        /// made it read as a prompt; a bar's garnish tray does not come and go, and the
        /// player is meant to know where these things live before they need them. What
        /// changes with the drink is whether a piece can be USED, and that is said by the
        /// dimming and by the drop refusing — not by the tray vanishing.
        /// </summary>
        private void StepMiniPreps(TycoonRun run)
        {
            if (_prepRail == null) return;
            // THE RAIL DOES NOT VANISH WHEN THE CELLAR OPENS (2026-08-26, the author:
            // "garnishler kapak acmak icin bastigimizda yok oluyorlar"). It used to be
            // switched off with the drawer; the dishes are standing on the bar, the bar
            // rises with the room, and a tray that disappears the moment you reach behind
            // it reads as a bug. It rides CounterLift up instead — and its props stop
            // ANSWERING the pointer while the drawer is open, because the cellar's own
            // doors are under them and a click meant for a bottle must reach the bottle.
            bool on = run != null && run.Phase == TycoonPhase.DayOpen
                      && (_flow == null || !_flow.IsOpen);
            if (_prepRail.gameObject.activeSelf != on)
                _prepRail.gameObject.SetActive(on);
            if (!on)
            {
                if (_prepHeld != null) DropPrep(false);
                return;
            }
            _prepRail.anchoredPosition = new Vector2(0, CounterLift);
            if (_coasterRt != null)
            {
                if (!_coasterRt.gameObject.activeSelf) _coasterRt.gameObject.SetActive(true);
                // ON the foot line, under the drink, riding the bar like the dishes beside
                // it — one line, so the coaster, the glass and the six dishes all touch the
                // same counter.
                _coasterRt.anchoredPosition = new Vector2(GlassHomeX, CounterFootY + CounterLift);
            }
            bool reachable = !CellarOpen;
            if (!reachable && _prepHeld != null) DropPrep(false);

            bool glass = run.DrinkReady && _glassShown && !_glassServing && !_glassReturning;
            // WHAT THE BAR OWNS, AND NOTHING ELSE (2026-08-26, the author: "bazilari
            // ileriki seviyelerde acilacakti"). Ice, the twist and the two rims are house
            // basics and always out. The olive and the mint are STOCK, and base_bar.json
            // has always priced them behind three and four stars — the gate existed in the
            // economy and the rail was not reading it. A jar the bar has not bought, or has
            // emptied tonight, is not on the counter; buying one puts it there.
            int slot = 0;
            foreach (var prop in _prepProps)
            {
                bool stocked = prop.Style == null || GarnishOnTheShelf(run, prop.Style) != null;
                if (prop.Rt.gameObject.activeSelf != stocked)
                    prop.Rt.gameObject.SetActive(stocked);
                if (!stocked) continue;
                // Laid out by VISIBLE index, so an unbought garnish leaves no hole in the
                // row — the rail closes up and still ends where the coaster begins.
                prop.Rt.anchoredPosition = new Vector2(PrepRailX0 + slot * PrepRailGap, PrepRailY);
                slot++;

                // Spent, or nothing to spend it on. The bucket is never spent — ice is
                // counted, not applied — which is the one exception the glass already makes.
                bool done = glass && prop.Prep != null && prop.Id != "ice"
                            && run.ServingGlass.HasPreparation(prop.Id);
                var baseCol = prop.Img.sprite != null ? Color.white : UITheme.Cyan[3];
                float a = !glass ? 0.55f : done ? 0.4f : 1f;
                prop.Img.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
                prop.Img.raycastTarget = reachable;
            }
            StepPrepCarry(run);
            StepGrains();
            // THE RIM'S GRIND, decided once (2026-08-27). The flag is set by StepRimLap
            // while the lap is actually turning and cleared here after it is read, so a
            // cursor that leaves the band, a dish that is put down, or a stage that opens
            // over the room all stop the sound by simply not asking for it again.
            Sfx.HoldLoop(_rimLoopWanted ? "rim_turn" : null, 0.7f);
            _rimLoopWanted = false;
        }

        // ── the drink you carry (GDD 24 §3, 2026-07-22) ──────────────────────────

        private void BuildDrinkGlass(RectTransform root)
        {
            // BETWEEN THE LAST DISH AND THE DRIP MAT (2026-08-26, the author: "son
            // garnish ile bar mati arasinda tezgahin ustunde durmali"). The rail runs to
            // stage 380 at its longest and the mat starts at 480; the drink stands at 430,
            // in the gap, and every drag off a dish travels right into it.

            // THE COASTER IS ALWAYS THERE (same note: "tam bardagin koyulacagi yere bir
            // bardak altligi olmali sahnede her zaman"). It is the drink's PLACE, so it is
            // drawn whether or not there is a drink on it — an empty coaster is what tells
            // the player where the next one will land, and it is why the glass no longer
            // looks like it is floating on a strip of counter. Built before the glass, so
            // the drink stands ON it.
            // DRAWN, at the proportion the counter needs (2026-08-26): a generated one
            // shipped first and stood 38 units deep under a 92-unit glass, which is a bowl,
            // with its lower half over the counter's front edge. See BackBarArt.Coaster.
            var coaster = NewRect("Coaster", root);
            coaster.anchorMin = coaster.anchorMax = coaster.pivot = new Vector2(0.5f, 0.5f);
            coaster.sizeDelta = new Vector2(112f, 36f);
            _coasterRt = coaster;
            var coasterImg = coaster.gameObject.AddComponent<Image>();
            coasterImg.sprite = BackBarArt.Coaster();
            coasterImg.raycastTarget = false;
            coaster.gameObject.SetActive(false);
            // (The bin used to be built here, before the glass, so the carried drink passed
            //  over it. It went on 2026-08-26 and the sink took the verb — see TycoonHud's
            //  own headstone for it, and OnDrainClicked below.)

            // The drink you carry to a seat is the real glass now (v5 P14 / C9): the same
            // drawing the serve stage stands on the counter, with its interior filled to the
            // level the drink is actually at. It used to be a translucent box with a cyan bar
            // for a rim, which said "a drink" and nothing about WHICH drink.
            _drinkGlass = NewRect("DrinkGlass", root);
            _drinkGlass.anchorMin = _drinkGlass.anchorMax = _drinkGlass.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlass.sizeDelta = new Vector2(78, CarriedGlassHeight);
            _drinkGlass.anchoredPosition = GlassHome;

            // THE GLASS IS PICKED UP AGAIN (2026-08-11, the author: back to dragging
            // instead of clicking). Clicking a customer to serve them was the wrong verb for
            // the one moment in the loop that is physical: you have made a drink, and what
            // you do with a drink is carry it to somebody. The whole rect takes the press —
            // a glass is a narrow silhouette, and asking for the glass itself would be a
            // precision test nobody signed up for.
            var body = _drinkGlass.gameObject.AddComponent<Image>();
            body.color = new Color(0f, 0f, 0f, 0.004f);
            body.raycastTarget = true;
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(_ =>
            {
                var run = Run;
                if (run == null || run.Phase != TycoonPhase.DayOpen) return;
                if (_flow != null && _flow.IsOpen) return;
                if (!_glassShown || _glassServing || _glassReturning || !run.DrinkReady) return;
                _glassGrabbed = true;
                _glassVel = Vector2.zero;
                Sfx.Play("click", 0.5f);
            });
            _drinkGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);

            // The layer architecture (the author, 2026-08-02): BACK face and base first,
            // the liquid over it, the FRONT face — interior fully clear — on top.
            var backRt = NewRect("Back", _drinkGlass);
            Stretch(backRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassBack = backRt.gameObject.AddComponent<Image>();
            _drinkGlassBack.preserveAspect = true;
            _drinkGlassBack.raycastTarget = false;
            _drinkGlassBack.enabled = false;

            var liquid = NewRect("Liquid", _drinkGlass);
            Stretch(liquid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassLiquid = liquid.gameObject.AddComponent<Image>();
            _drinkGlassLiquid.raycastTarget = false;
            _drinkGlassLiquid.type = Image.Type.Filled;
            _drinkGlassLiquid.fillMethod = Image.FillMethod.Vertical;
            _drinkGlassLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            _drinkGlassLiquid.preserveAspect = true;

            // THE TOP OF THE DRINK IS AN ELLIPSE (2026-08-11, the author: the glass is 3D, so
            // what is in it has to be). A vertical fillAmount cuts the interior with a straight
            // edge — right for the body, wrong for the surface, which is the one place the
            // drink shows the player it is a cylinder and not a picture of one. It goes over
            // the liquid and under the front face, so the glass's own wall still crosses it.
            var surf = NewRect("Surface", _drinkGlass);
            surf.anchorMin = surf.anchorMax = surf.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlassSurface = surf.gameObject.AddComponent<Image>();
            _drinkGlassSurface.sprite = GlassArt.SurfaceDisc();
            _drinkGlassSurface.raycastTarget = false;
            _drinkGlassSurface.enabled = false;

            var art = NewRect("Art", _drinkGlass);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassArt = art.gameObject.AddComponent<Image>();
            _drinkGlassArt.raycastTarget = false;
            _drinkGlassArt.preserveAspect = true;

            var hint = NewText("Hint", _drinkGlass, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
            Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(190, 18), new Vector2(0, 24));
            hint.text = "CLICK A CUSTOMER TO SERVE";
            hint.raycastTarget = false;

            _drinkGlass.gameObject.SetActive(false);
        }

        /// <summary>
        /// Puts the drink's top face where the drink's top is, at the width the glass has
        /// there.
        ///
        /// The art is letterboxed inside its rect by preserveAspect, so the sprite's own box
        /// is worked out first: everything the level and the profile say is in SPRITE
        /// fractions, and placing them against the rect instead would float the surface off
        /// the liquid on any glass whose drawing is not exactly the rect's shape.
        /// </summary>
        private void PlaceDrinkSurface(GlassArt.Piece piece, float fraction)
        {
            if (_drinkGlassSurface == null) return;
            if (piece.Fill == null || fraction <= 0f || piece.Aspect <= 0f)
            {
                _drinkGlassSurface.enabled = false;
                return;
            }

            Vector2 rect = _drinkGlass.rect.size;
            float drawnH = Mathf.Min(rect.y, rect.x / piece.Aspect);
            float drawnW = drawnH * piece.Aspect;

            float level = piece.FillAmount(fraction);          // 0..1 up the sprite
            float width = piece.InteriorWidthAt(level) * drawnW;
            if (width <= 1f) { _drinkGlassSurface.enabled = false; return; }

            var rt = _drinkGlassSurface.rectTransform;
            rt.sizeDelta = new Vector2(width, width * GlassArt.SurfaceSquash);
            rt.anchoredPosition = new Vector2(0f, (level - 0.5f) * drawnH);
            // A shade lighter than the body: the top face catches the room, and without the
            // lift it reads as a hole in the drink rather than the top of it.
            var body = DrinkColor();
            _drinkGlassSurface.color = new Color(
                Mathf.Lerp(body.r, 1f, 0.24f), Mathf.Lerp(body.g, 1f, 0.24f),
                Mathf.Lerp(body.b, 1f, 0.24f), body.a);
            _drinkGlassSurface.enabled = true;
        }

        /// <summary>
        /// The SINK's click: pours the ready drink away, and pays for it (2026-08-26). Inert
        /// with nothing to pour — an empty counter never nags, and the basin is scenery the
        /// rest of the night.
        ///
        /// What it COSTS is Core's answer, not this one's: the steel basin the bar opens with
        /// writes the goods off, the brass one it can fit later does not
        /// (TycoonRun.WasteIsFree), and the toast reports whichever came back. That is the
        /// whole of the upgrade, and the first piece of dressing that changes what the bar
        /// can afford to do.
        /// </summary>
        private void OnDrainClicked()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (!_glassShown || _glassServing || _glassReturning || !run.DrinkReady) return;
            int fee = run.DiscardGlass();
            Sfx.Play("drain", 0.9f);
            Toast(fee > 0 ? $"POURED AWAY · -${fee}" : "POURED AWAY");
            if (fee > 0)
                LogService($"<color=#F27D8A>POURED AWAY</color> a built drink · -${fee}");
            _drinkGlass.gameObject.SetActive(false);
            _glassShown = false;
        }

        /// <summary>The finished drink sits on the counter and is dragged onto a customer to
        /// serve (GDD 24 §3). Heavy, springy carry with a lean into the motion (AAA feel).</summary>
        private void UpdateDrinkGlass()
        {
            var run = Run;
            // The glass on the counter is the SERVING glass and nothing else. A drink still in
            // the shaker is a half-finished build, not something you can pick up and carry — it
            // used to appear here, which is how an unpoured drink reached a customer (2026-07-28).
            bool ready = run != null && run.Phase == TycoonPhase.DayOpen
                && (_flow == null || !_flow.IsOpen)
                && run.DrinkReady;

            if (!ready)
            {
                if (_glassShown)
                {
                    _drinkGlass.gameObject.SetActive(false);
                    _glassShown = false;
                    _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
                }
                return;
            }

            if (!_glassShown)
            {
                _glassShown = true;
                Sfx.Play("glass_down", 0.7f);
                _drinkGlass.gameObject.SetActive(true);
                _drinkGlass.anchoredPosition = GlassHome;
                _glassAngle = 0f;
                _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
            }
            // The glass shows the drink as it was actually built: the vessel it chose, its
            // blended colour and its real fill level — no fixed glass, colour or amount.
            int drinkTier = run.GlassTier(run.ServingGlassware?.Id);
            var piece = GlassArt.For(run.ServingGlassware, drinkTier);
            if (!ReferenceEquals(_drinkGlassware, run.ServingGlassware) || drinkTier != _drinkGlassTier
                || _drinkGlassArt.sprite == null)
            {
                _drinkGlassware = run.ServingGlassware;
                _drinkGlassTier = drinkTier;
                // Front face over the liquid when the set is modular; the composite
                // sprite carries a run without the generated art.
                _drinkGlassArt.sprite = piece.Front != null ? piece.Front : piece.Sprite;
                _drinkGlassBack.sprite = piece.Back;
                _drinkGlassBack.enabled = piece.Back != null;
                _drinkGlassLiquid.sprite = piece.Fill;
                _drinkGlass.sizeDelta = new Vector2(CarriedGlassHeight * piece.Aspect, CarriedGlassHeight);
            }
            _drinkGlassLiquid.color = DrinkColor();
            _drinkGlassLiquid.fillAmount = piece.FillAmount((float)run.ServingGlass.FillFraction);
            PlaceDrinkSurface(piece, (float)run.ServingGlass.FillFraction);
            // The finishing touches ride the carried glass too (P14): the customer is handed
            // the drink that was actually finished, salt and wedge and all.
            GlassDecor.Sync(_drinkGlass, piece, run.ServingGlass, run);

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var mouse = Mouse.current;

            // THE CARRY (2026-08-11). While it is held the glass springs after the cursor,
            // stiff and slightly under-damped, and leans into whichever way it is travelling
            // — the weight is the whole reason this is a drag and not a click. Letting go
            // over a customer hands it to them; letting go anywhere else sends it home,
            // which is the same slide the refusal already used.
            if (_glassGrabbed)
            {
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _glassGrabbed = false;
                    // THE SINK IS A PLACE YOU CARRY IT TO (2026-08-26, the author: "bardağı
                    // çöpe atmak için ana sahnede bardağı lavaboya sürüklemek gerekir").
                    // It answered a click for one round, which made throwing a drink away
                    // cheaper and easier than serving it — the wrong shape for the one verb
                    // that costs money. It is the same carry as the serve now, and it is
                    // asked FIRST: the sink is at the far end of the bar from every stool,
                    // so a drop that is over the basin was never also over a drinker.
                    if (stage != null && mouse != null
                        && stage.PointerOverDrain(mouse.position.ReadValue()))
                    {
                        OnDrainClicked();
                        if (!_glassShown) return;
                    }
                    int seat = SeatUnderPointer(mouse);
                    bool served = false, saidWhy = false;
                    if (seat >= 0)
                    {
                        try { served = ServeSeat(seat); }
                        catch (InvalidOperationException e3)
                        { Toast(e3.Message.ToUpperInvariant()); saidWhy = true; }
                    }
                    if (served)
                    {
                        _drinkGlass.gameObject.SetActive(false);
                        _glassShown = false;
                        return;
                    }
                    if (seat >= 0 && !saidWhy) Toast("READ THEIR ID FIRST");
                    // Home it goes, along the counter, by the road it already knows.
                    _glassServeFrom = GlassHome;
                    _glassServeTo = _drinkGlass.anchoredPosition;
                    _glassServeDur = Mathf.Min(GlassSlideMax,
                        0.08f + (_glassServeTo - _glassServeFrom).magnitude / 4200f);
                    _glassServeT = 0f;
                    _glassReturning = true;
                    return;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_drinkGlass.parent, mouse.position.ReadValue(), null,
                        out Vector2 want))
                {
                    var before = _drinkGlass.anchoredPosition;
                    _glassVel += (want - before) * (GlassCarryStiffness * dt);
                    _glassVel *= Mathf.Exp(-GlassCarryDamping * dt);
                    _drinkGlass.anchoredPosition = before + _glassVel * dt;
                    float carry = Mathf.Clamp(-_glassVel.x * 0.012f, -16f, 16f);
                    _glassAngle = Mathf.Lerp(_glassAngle, carry, 1f - Mathf.Exp(-18f * dt));
                    _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
                }
                return;
            }

            // THE SLIDE (2026-08-11): the glass travels the counter on its own timer; the
            // serve fires on arrival, after a re-validation, because the seat can empty
            // and the patience can run out while the glass is in flight.
            if (_glassServing || _glassReturning)
            {
                _glassServeT += dt;
                float k = _glassServeDur <= 0f ? 1f : Mathf.Clamp01(_glassServeT / _glassServeDur);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);   // lands soft
                var from = _glassServing ? _glassServeFrom : _glassServeTo;
                var to = _glassServing ? _glassServeTo : _glassServeFrom;
                var before = _drinkGlass.anchoredPosition;
                _drinkGlass.anchoredPosition = Vector2.Lerp(from, to, e);
                // lean into the travel, upright at both ends
                float lean = Mathf.Clamp((_drinkGlass.anchoredPosition.x - before.x) / dt * -0.012f, -18f, 18f);
                _glassAngle = Mathf.Lerp(_glassAngle, lean * Mathf.Sin(k * Mathf.PI), 0.5f);
                _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
                if (k < 1f) return;

                if (_glassReturning)
                {
                    _glassReturning = false;
                    _drinkGlass.anchoredPosition = GlassHome;
                    _drinkGlass.localRotation = Quaternion.identity;
                    return;
                }
                _glassServing = false;
                int seat = _glassServeSeat;
                _glassServeSeat = -1;
                bool served = false, saidWhy = false;
                try { served = seat >= 0 && ServeSeat(seat); }
                catch (InvalidOperationException e2)
                { Toast(e2.Message.ToUpperInvariant()); saidWhy = true; }
                if (served)
                {
                    _drinkGlass.gameObject.SetActive(false);   // handed over; a new drink re-shows it
                    _glassShown = false;
                }
                else
                {
                    // Refused at the stool: the drink comes back. The player keeps it.
                    if (!saidWhy) Toast("THEY LEFT — YOU GET THE DRINK BACK");
                    _glassServeT = 0f;
                    _glassReturning = true;
                }
                return;
            }

            // At rest: home, upright. (The bin's hover tint lived here; the sink answers the
            // pointer with HoverGlow, like every other prop standing in the room.)
            _drinkGlass.anchoredPosition = GlassHome;
            _drinkGlass.localRotation = Quaternion.identity;
        }

        /// <summary>The carried drink's colour: its ingredients' true liquid colours, blended by
        /// share in linear space (2026-07-23) — clear spirits read pale, and a mix stays clean.</summary>
        private Color DrinkColor() => UITheme.DrinkColor(Run?.Shelf, Run?.ServingGlass);

        /// <summary>How a served customer reacts (GDD 24 §4, §10): a word for the read/serve
        /// and the payment, rising from the seat with a little pop. Green when they're pleased,
        /// red when it's the wrong drink; a gold call when they order another round.</summary>
        private System.Collections.IEnumerator ServeReaction(int seatIndex, ServiceVerdict verdict)
        {
            var seat = _seats[seatIndex].Root;
            bool wrong = verdict.Match == OrderMatch.Wrong;
            Color tone = verdict.OrdersAgain ? UITheme.Amber[3]
                : wrong ? UITheme.ViceRed[3] : UITheme.Lime[3];

            // Only the FACE answers at the serve (2026-07-31): they can see the drink, so the
            // reaction line is honest — but the bill is not on the table yet. The money and
            // the stars float up when they finish and get up (TabFloat), which is when a
            // customer actually pays.
            string line = verdict.OrdersAgain ? "ANOTHER ROUND!"
                : verdict.Match == OrderMatch.Exact ? "PERFECT!"
                : verdict.Match == OrderMatch.Close ? "THANKS."
                : "NOT WHAT I ASKED";
            if (verdict.OrdersAgain) Sfx.Play("another_round", 0.85f);

            var text = NewText("React", seat.parent, _display, 14, TextAnchor.LowerCenter, tone);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = line;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 60);
            var start = seat.anchoredPosition + new Vector2(-89f, 118f);   // centred over the seat
            // ...and it rides the counter, like everything else over the bar. Only the LIFT
            // is followed and not the seat itself: the word belongs to the stool it was said
            // at, so it must not walk out of the room with a customer who is leaving.
            float liftAtStart = CounterLift;

            const float duration = 1.35f;
            float tt = 0f;
            while (tt < duration && text != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                // A quick pop on the way in, then a slow rise and fade.
                float pop = 1f + 0.3f * Mathf.Clamp01(1f - k * 6f) - 0.05f * k;
                rt.localScale = new Vector3(pop, pop, 1f);
                rt.anchoredPosition = start
                    + new Vector2(0, 58f * k + CounterLift - liftAtStart);
                text.color = new Color(tone.r, tone.g, tone.b, 1f - k * k);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        // ── what they thought of it (2026-09-04) ────────────────────────────────
        //
        // The author: "müşteriler içkilerini içtikten sonra tepkilerini emoji efektleriyle
        // verecek … partikül sayısı kötüden/mükemmele göre artacak … mükemmelde 20 adet".
        // The face is the mood, the COUNT is the grade: four motes for a drink that missed,
        // twenty for one that landed. ReactionMotes does the flying.

        /// <summary>Where the mouth turns down, and where it turns up.</summary>
        private const double ReactionSour = 0.35, ReactionSweet = 0.7;

        /// <summary>How far under the crown the motes leave: they come up from BEHIND the
        /// drinker, so they start at the SHOULDERS and clear the head on the way — measured
        /// in play, where a chin-line start read as a puff off the top of the hair.</summary>
        private const float MotesBelowCrown = 74f;

        /// <summary>
        /// A PERFECT POUR IS ITS OWN RUNG (2026-09-04, the author: "eğer perfect ise kusursuz
        /// olduğunu belirtsin ... partiküller abartılsın"). The three bands below grade
        /// SATISFACTION, which a perfect pour shares with any merely good drink served
        /// promptly — so the rarest thing in the game arrived looking exactly like the
        /// common one. It gets a count no other serve can reach, gold instead of green, and
        /// an answer thrown from the player's own side of the counter.
        /// </summary>
        private const int PerfectMotes = 32, PerfectBackMotes = 20;

        /// <summary>The face, its ink and how many of them one serve is worth.</summary>
        private static (string Face, Color Tint, int Count) ReactionFor(double satisfaction, bool perfect)
        {
            if (perfect) return ("good", UITheme.Amber[4], PerfectMotes);
            double s = System.Math.Max(0.0, System.Math.Min(1.0, satisfaction));
            if (s < ReactionSour)
                return ("bad", UITheme.ViceRed[3], 4 + (int)System.Math.Round(s / ReactionSour * 3));
            if (s < ReactionSweet)
                return ("fair", UITheme.Amber[3],
                    8 + (int)System.Math.Round((s - ReactionSour) / (ReactionSweet - ReactionSour) * 5));
            return ("good", UITheme.Lime[3],
                14 + (int)System.Math.Round((s - ReactionSweet) / (1.0 - ReactionSweet) * 6));
        }

        /// <summary>Throws the motes from behind one drinker.</summary>
        private void ReactionBurst(SeatView view, double satisfaction, bool follow,
            bool perfect = false)
        {
            if (stage == null || view == null || view.Body == null) return;
            if (!view.Body.gameObject.activeSelf) return;
            var (faceName, tint, count) = ReactionFor(satisfaction, perfect);
            var face = ChromeArt.Face(faceName);
            if (face == null) return;
            // The body's own position is the middle of the rig canvas; the crown sits
            // HeadTop above the stool, which is CharSize/2 - CharFootDrop above that middle.
            float headHud = view.Look != null ? view.Look.HeadTop : CharSize * 0.5f;
            float upStage = (headHud - (CharSize * 0.5f - CharFootDrop) - MotesBelowCrown) / StageToHud;
            var at = view.Body.transform.position + new Vector3(0f, upStage, 0f);
            ReactionMotes.Burst(stage, at, view.Body, follow, face, tint, count);
            if (!perfect) return;
            // AND THE BAR ANSWERS. A second burst from below the counter's line, in the
            // room's magenta, so a perfect pour is a thing the two sides of the bar do
            // TOGETHER rather than a thing that happens to a customer. It is pinned to the
            // stool and never follows anybody: the player does not walk out.
            ReactionMotes.Burst(stage, at + new Vector3(0f, -MotesBelowCrown / StageToHud, 0f),
                view.Body, false, face, UITheme.Magenta[3], PerfectBackMotes);
        }

        /// <summary>
        /// The bill, paid on the way out (2026-07-31): what the whole visit came to — every
        /// round of it — and the stars this customer leaves behind. Fired by the departure
        /// hook, which is the same moment Core settles the tab into the till.
        ///
        /// The stars are DRAWN, never typed (2026-08-11): the first cut set them in U+2605
        /// and U+2606, which PressStart2P does not carry, so Unity drew the missing-glyph
        /// box five times over and the author read the tofu as "black and white frames
        /// around the figures". They ride the `StarRow` ruler now, like every other star in
        /// the game — and the money sits on a whole multiple of the face's 8px design size,
        /// which is the rest of what made it soft.
        ///
        /// THREE MARKS, NOT ONE SLIP (2026-08-25). It used to be one host carrying the stars,
        /// the total and the tip stacked on each other, so they arrived together, drifted
        /// together and left together — a receipt floating off a stool. They are three now,
        /// counted out a third of a second apart, each on its own host with its own phase,
        /// its own climb and its own lean. Nothing about the movement is shared.
        ///
        /// The START is shared, on purpose. The stool is walking out from under them — a
        /// leaving drinker's rect is being lerped across the room — so all three are fired
        /// from where the stool stood when the tab settled, not from wherever it has got to
        /// by the time a mark's turn comes round.
        /// </summary>
        private void TabFloat(int seatIndex, CustomerVisit visit)
        {
            var seat = _seats[seatIndex].Root;
            var start = seat.anchoredPosition + new Vector2(0f, 96f);
            int tip = visit.Paid - visit.PaidBase;

            // THE SCORE CAME OFF THE STOOL (2026-09-04, the author: "müşterilerin verdikleri
            // ücretle beraber gözüken puanları gizlensin"). A leaving drinker threw three
            // marks — a five-star row, the money, the tip — and the first of them was a
            // GRADE: a number about a drink already drunk, printed at the one moment the
            // player can do nothing about it.
            //
            // What it said is not lost, it moved to where it is useful. The motes off their
            // shoulders carry how it went, and the note in the bubble carries what to change
            // — both while the glass is still in their hand. The bar's own standing still
            // counts every one of these stars; it is read on the night's slip, where the
            // night is what is being judged rather than the customer walking out.
            //
            // The remaining two marks keep their lanes and their stagger. Lane 0 is simply
            // empty now, which is what leaves the money climbing highest (TabLaneClimb).

            // THE MONEY, AND THE FIGURE IS THE EVENT (2026-08-25, the author: "daha
            // belirgin ve dikkat çekici"). 24 — the next legal step up, a whole 3x of the
            // face's 8px grid — and ringed the way the till's change is, so it holds its
            // shape over a lit wall or a dark one. Amber[3] and not the ramp's palest step:
            // the figure crosses a sunset window on its way up, and 0xF5C97B against that is
            // cream on cream (measured).
            StartCoroutine(TabMark(seat.parent, start, seatIndex, 1, TabStagger, host =>
            {
                var paid = NewText("Paid", host, _display, 24, TextAnchor.LowerCenter,
                    UITheme.Amber[3]);
                Place(paid.rectTransform, new Vector2(0.5f, 1), new Vector2(200, 28),
                    Vector2.zero);
                paid.rectTransform.pivot = new Vector2(0.5f, 1);
                paid.horizontalOverflow = HorizontalWrapMode.Overflow;
                paid.verticalOverflow = VerticalWrapMode.Overflow;
                paid.text = "+$" + visit.Paid;
                Ring(paid);
            }));

            // AND LAST THE TIP, which is the part worth its own colour and is short enough
            // to say what it is without a line explaining itself. It comes last because it
            // is the part that is not owed — a bar earns it after the bill is already paid.
            if (tip > 0)
                StartCoroutine(TabMark(seat.parent, start, seatIndex, 2, TabStagger * 2f,
                    host =>
                    {
                        var tipText = NewText("Tip", host, _display, 16, TextAnchor.UpperCenter,
                            UITheme.Lime[4]);
                        Place(tipText.rectTransform, new Vector2(0.5f, 1), new Vector2(200, 18),
                            Vector2.zero);
                        tipText.rectTransform.pivot = new Vector2(0.5f, 1);
                        tipText.horizontalOverflow = HorizontalWrapMode.Overflow;
                        tipText.verticalOverflow = VerticalWrapMode.Overflow;
                        tipText.text = "+$" + tip + " TIP";
                        Ring(tipText);
                    }));
        }

        /// <summary>
        /// One of the three: built, held back its share of a second, then carried up off the
        /// stool on its own air.
        ///
        /// PIVOTED IN THE MIDDLE OF ITS OWN FOOT, so the lean turns it about the point it is
        /// rising from rather than swinging it about a corner off to the left.
        ///
        /// EVERY NUMBER IN THE MOVEMENT COMES FROM (seat, lane) and none of it is rolled:
        /// nothing in this game is random by accident (the determinism rule), so a wander
        /// that reproduces is one less thing that can differ between two runs of the same
        /// seed — and three marks off one stool still take three different paths, because
        /// the lane is in the phase.
        /// </summary>
        private System.Collections.IEnumerator TabMark(Transform parent, Vector2 start,
            int seatIndex, int lane, float delay, Action<RectTransform> dress)
        {
            // Counted the moment it is PROMISED and not the moment it appears: the night's
            // books wait on this (FloorIsClear), and a tip still holding its breath is money
            // the day has not finished paying.
            _tabFloats++;

            var host = NewRect(TabLaneName[lane], parent);
            host.anchorMin = host.anchorMax = new Vector2(0, 0);
            host.pivot = new Vector2(0.5f, 0f);
            host.sizeDelta = new Vector2(200, 40);
            host.anchoredPosition = start + new Vector2(TabLaneX[lane], 0f);
            var group = host.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;              // not here yet; the wait below is its cue
            dress(host);

            for (float wait = delay; wait > 0f && host != null; wait -= Time.deltaTime)
                yield return null;

            float phase = seatIndex * 1.7f + lane * 2.3f;
            float climb = TabClimb + TabLaneClimb[lane];
            float tt = 0f;
            while (tt < TabLife && host != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / TabLife);
                float rise = 1f - (1f - k) * (1f - k);        // fast off the stool, then easing
                float wander = Mathf.Sin(phase + k * Mathf.PI * 1.6f);
                host.anchoredPosition = start + new Vector2(
                    TabLaneX[lane] + TabSway * wander * k, climb * rise);
                // It leans the way it is being carried: the lean is the drift's own slope,
                // which is why it reads as one movement rather than as a spin.
                host.localRotation = Quaternion.Euler(0, 0,
                    -TabLean * Mathf.Cos(phase + k * Mathf.PI * 1.6f) * k);
                // A pop out of the stool, then a slow settle — the punch is what makes it
                // arrive rather than appear.
                float pop = 1f + 0.35f * Mathf.Clamp01(1f - k * 9f) - 0.06f * k;
                host.localScale = new Vector3(pop, pop, 1f);
                // It holds its ink for two thirds of the life and only then goes. The old
                // curve (1 - k²) was already fading on the first frame, which is most of
                // why it read as "gone at once".
                group.alpha = k < 0.62f ? 1f : 1f - (k - 0.62f) / 0.38f;
                yield return null;
            }
            _tabFloats--;
            if (host != null) Destroy(host.gameObject);
        }

        /// <summary>
        /// A dark halo round a label, so a figure thrown over the room keeps its shape on
        /// any wall it crosses — and the till's own sunset window is the wall it crosses.
        ///
        /// TWO BLACKS, NOT THE TILL'S WHITE-THEN-BLACK (measured, 2026-08-25). The white
        /// ring works on the register, which stands in shadow. Over the window it does not:
        /// `Outline` draws offset copies BEHIND the glyph, and a pixel face at 24 is a
        /// three-unit stroke with a unit of anti-aliasing down each side — so the white
        /// shows straight through the soft edges and a gold "+$4" comes out cream. Two dark
        /// rings at different distances give the same read on a dark wall and leave the
        /// gold gold.
        /// </summary>
        private static void Ring(Text label)
        {
            var near = label.gameObject.AddComponent<Outline>();
            near.effectColor = new Color(0f, 0f, 0f, 0.92f);
            near.effectDistance = new Vector2(2f, -2f);
            var far = label.gameObject.AddComponent<Outline>();
            far.effectColor = new Color(0f, 0f, 0f, 0.62f);
            far.effectDistance = new Vector2(3.5f, -3.5f);
        }

        private void RefreshSeats()
        {
            var run = Run;
            var seated = run.Floor.Seated;

            // A patron whose patience ran out storms off (GDD 24 §4). IT IS SAID ON THE
            // STOOL, NOT IN A BANNER (2026-09-04, the author: "'A customer stormed off'
            // yazısı kalkacak"). A red line across the top of the screen was the bar
            // telling you about its own room; the walk-out is now what everyone else
            // gives back — a handful of sour motes over the stool they got up from, thrown
            // by the departure branch below, where the seat that did it is known.

            // The licence is only good while its holder is at the bar.
            if (_idVisit != null && (_idVisit.State != VisitState.Waiting || !seated.Contains(_idVisit)))
                CloseId();

            bool drinkReady = run.Phase == TycoonPhase.DayOpen && run.DrinkReady &&
                (_flow == null || !_flow.IsOpen);

            // Stools are stable (2026-07-22): a customer keeps their seat until they leave, so
            // busts never shift or morph when the queue compacts. Reconcile the positional
            // Seated list against the fixed stools each frame.
            // 1) Departures — a stool whose patron is no longer seated starts a leave animation.
            for (int i = 0; i < _seats.Count; i++)
            {
                var v = _seats[i];
                if (v.Visit != null && !v.Exiting && !seated.Contains(v.Visit))
                {
                    v.Exiting = true;
                    v.ExitT = 0f;
                    // THE STORY'S GUEST LEAVES A LINE, NOT A SCENE (GDD 26 §3). Their clock
                    // running out is a beat that did not land, not a customer storming out of
                    // a bad bar — they walk, they do not slam, and the night's log does not
                    // book them as a walk-out because they were never on its books at all.
                    v.ExitStorm = !v.Visit.OnTheHouse && v.Visit.State == VisitState.StormedOff;
                    // WHAT THEY THOUGHT, AT THE BOTTOM OF THE GLASS (2026-09-04, the author:
                    // "verilen emoji tepkileri içkiyi bitirdikten sonra verilmeli"). It used to
                    // be thrown a sip after the serve, which is a verdict on a drink they had
                    // barely tasted; it belongs here, where they set the empty down and get up.
                    // A walk-out gets the same sentence in the same language — the worst face
                    // there is, as few as they come. Both are pinned to the stool rather than
                    // following them out: a cloud chasing a leaver reads as a comet. The
                    // guest of the house is left out, as they are left out of every other
                    // ledger (GDD 26 §3).
                    // A PERFECT POUR IS REMEMBERED PAST THE SIP that earned it: the note
                    // taken at the serve is still on the seat when they set the glass down,
                    // and it is the only thing here that knows the pour was exact. A
                    // storm-off never reaches it — there was no glass.
                    if (!v.Visit.OnTheHouse)
                        ReactionBurst(v, v.ExitStorm ? 0.0 : v.Visit.Satisfaction, follow: false,
                            perfect: !v.ExitStorm && v.Note.Flawless);
                    if (v.Visit.OnTheHouse) { }
                    else if (v.ExitStorm)
                        LogService($"<color=#F27D8A>STORM-OFF</color> " +
                            (v.Visit.IdInspected ? v.Visit.Order.Wanted.Name.ToUpperInvariant() : "?") +
                            " · patience ran out · $0 · " + LogStars(0));
                    else if (v.Visit.Paid > 0)
                        LogService($"<color=#F5C97B>TAB</color> settled ${v.Visit.Paid}" +
                            (v.Visit.SnacksTaken > 0 ? $" (+{v.Visit.SnacksTaken} snack)" : "") +
                            $" · leaves {LogStars(v.Visit.Satisfaction)}");
                    // The bussing beat (D2): a drinker leaves the empty glass on this stool.
                    // Core created the DirtyGlass in the same tick that freed the seat; this
                    // view claims the first one no other stool has claimed.
                    if (v.Visit.Served != null)
                        foreach (var g in run.Floor.Dirty)
                        {
                            bool claimed = false;
                            foreach (var other in _seats) if (other.Dirty == g) { claimed = true; break; }
                            if (!claimed) { v.Dirty = g; break; }
                        }
                    // The tab settles as they go: what they paid and the stars they leave
                    // behind float over the emptying stool. The serve only earned the face.
                    if (v.Visit.Paid > 0) TabFloat(i, v.Visit);
                    if (v.Visit.Paid > 0) Sfx.Play("cash");
                    Sfx.Play(!v.ExitStorm && v.Visit.Satisfaction >= 0.55 ? "cheer_sfx" : "upset_sfx", 0.6f);
                    // And the body answers before it leaves (P15/D5): a cheer or a slump on
                    // the stool. This is where the emotional tell lives now the stat rows
                    // left the card — skipped cleanly while the clips have no frames yet.
                    v.ReactClip = !v.ExitStorm && v.Visit.Satisfaction >= 0.55
                        ? PatronClip.Cheer : PatronClip.Upset;
                    var reactLook = v.Look ?? (_looks.Count > 0 ? _looks[0] : null);
                    v.ReactLeft = reactLook != null
                        && reactLook.Clips.TryGetValue(v.ReactClip, out var rf) && rf.Length > 0
                        ? ReactSeconds : 0f;
                }
            }
            // 1b) THE GUEST WEARS THE FACE THE BEAT NAMES, whatever order the frame ran in.
            //
            // Measured, 2026-08-13: the story's guest kept turning up in a stranger's body
            // while the plate showed the right person — the stool had been given a rolled
            // look, and a stool KEEPS its look by design (a face that changes under the
            // player is worse than a wrong one). Rather than chase which frame won the race,
            // the written face is simply reasserted here, once a frame, idempotently: for
            // this one visit the beat is the authority, not the seat.
            var houseGuest = run.LastCustomer;
            if (houseGuest != null)
            {
                var written = LookForStory(run.LastCallBeat?.Who);
                if (written != null)
                    foreach (var v in _seats)
                        if (v.Visit == houseGuest && v.Look != written)
                        {
                            v.Look = written;
                            v.Tag.anchoredPosition = new Vector2(0, written.HeadTop + TagLift);
                            if (v.Gauge != null)
                                v.Gauge.anchoredPosition = new Vector2(0, written.HeadTop + 6f);
                        }
            }

            // 2) Arrivals — a seated customer with no stool takes the first free one and walks in.
            foreach (var visit in seated)
            {
                bool assigned = false;
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Visit == visit) { assigned = true; break; }
                if (assigned) continue;
                // THE GUEST SITS WHERE THEY CAN BE TALKED TO (GDD 26 §3): the stool nearest
                // the till, which is the end of the row the bar is worked from. Everyone else
                // takes the first free stool, as they always have — "first" meaning first in
                // the order the ROOM fills (SeatFillOrder), which is no longer the same thing
                // as first along the counter.
                var order = SeatOrderFor(run);
                int owned = Math.Min(run.Seats, order.Length);
                bool nearTheTill = visit.OnTheHouse;
                for (int n = 0; n < owned; n++)
                {
                    int i = nearTheTill ? TillEndward(order, owned, n) : order[n];
                    if (i < 0) break;
                    var v = _seats[i];
                    if (v.Visit == null && !v.Exiting)
                    {
                        v.Visit = visit;
                        v.WalkT = 0f;
                        v.Note = default;      // the last drinker's line is not this one's
                        HushSeat(v);           // …and neither is what they said about it
                        // Who walked in, and how tall they are. The ticket and the gauge
                        // hang off THEIR head: the cast runs from 135 to 166 pixels of
                        // figure, which is 60 HUD units of difference, and a fixed window
                        // would leave the short ones with their paperwork floating.
                        v.Look = LookFor(visit);
                        if (v.Look != null)
                        {
                            v.Tag.anchoredPosition = new Vector2(0, v.Look.HeadTop + TagLift);
                            if (v.Gauge != null)
                                v.Gauge.anchoredPosition = new Vector2(0, v.Look.HeadTop + 6f);
                        }
                        v.Root.gameObject.SetActive(true);
                        Sfx.Play("door", 0.5f);   // someone through the door (P17)
                        break;
                    }
                }
            }

            RefreshSnackRow(run);
            RefreshDirtyGlasses(run);
            // The bar bed (P17): always on, muffled while a stage or the licence is open.
            Sfx.Ambience(ducked: (_flow != null && _flow.IsOpen) ||
                                 (_idRoot != null && _idRoot.gameObject.activeSelf));

            // 3) Render each stool from its assigned patron.
            for (int i = 0; i < _seats.Count; i++)
            {
                var view = _seats[i];

                if (view.Exiting)
                {
                    // The ticket comes down the moment they get up (2026-08-19, the author:
                    // "içtikten sonra baloncuk kalkabilir") — a leaving customer is done
                    // talking, and a balloon walking out with them reads as unfinished
                    // business. The patience bar goes with it (2026-08-20): their clock
                    // stopped when they left the stool, and a gauge crossing the room is a
                    // countdown on somebody who is no longer waiting for anything.
                    if (view.Tag.gameObject.activeSelf) view.Tag.gameObject.SetActive(false);
                    if (view.Gauge != null && view.Gauge.gameObject.activeSelf)
                        view.Gauge.gameObject.SetActive(false);
                    AdvanceExit(view);
                    continue;
                }

                if (view.Visit == null)
                {
                    if (view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(false);
                    SyncPatronBody(view);
                    continue;
                }

                AdvanceWalkIn(view);

                var visit = view.Visit;
                bool deciding = !visit.HasOrdered;                    // reading the menu (2026-07-23)
                bool drinking = visit.State == VisitState.Drinking;   // served, nursing the drink

                // The bubble only knows what the PLAYER knows (v5 C3): until the ID card has
                // been read, Core refuses to hand the order over at all. Stripped to three
                // beats (the author, 2026-08-02): it does not exist until they SIT and have
                // an order to give; unread it says only that they are ready — not who they
                // are, not what they want; read, it says only the name and the order.
                bool known = visit.IdInspected;
                bool atTheStool = view.WalkT >= 1f;
                // THE BUBBLE IS UP THE WHOLE TIME THEY ARE ON THE STOOL (2026-08-19). It used
                // to be hidden while they read the menu, so a customer deciding and a customer
                // who had not arrived yet looked exactly alike — the player had nothing to
                // wait ON. It says "..." instead, which is a customer visibly thinking.
                // …AND DOWN WHILE THE CELLAR IS (2026-08-22, the author: "Backbar
                // açıldığında müşterilerin kafasının üstündeki barlar gitmeli"). The drinkers
                // ride the room up with the drawer, and their tickets and clocks ride with
                // them — straight into the shelves you are trying to read. Nothing is lost by
                // taking them down: the cellar is a place you are looking AWAY from the room
                // to work in, and the clocks are still running underneath.
                // THE BALLOON'S OWN CLOCK, read before the ticket's, because the ticket
                // stands down while somebody is talking.
                bool saying = view.Say != null && view.Say.gameObject.activeSelf;
                if (saying && Time.unscaledTime >= view.SayUntil) { HushSeat(view); saying = false; }
                if (saying && (!atTheStool || CellarOpen)) { HushSeat(view); saying = false; }

                // ONE THING OVER ONE HEAD (2026-09-04, the author: "kafalarının üstündeki
                // kutucuk yerine konuşma baloncukları gözükmeli"). The ticket is a standing
                // readout of an OPEN order; speech is what happens once the drink is in
                // their hand. So the ticket goes down while a line is being said — and stays
                // down for the rest of the savour, because a customer who has been served
                // has no order left to read and a plate over them saying so was the loading
                // sign this beat replaced.
                //
                // An extra round brings it straight back: Core never puts those visits into
                // Drinking (CustomerVisit.Resolve refreshes the order and stays Waiting), so
                // the moment the balloon retires the ticket is up again with the new drink
                // on it — which is the author's "ikinci siparişte tekrardan görüntüleyebilmeliyiz".
                bool showBubble = atTheStool && !CellarOpen && !saying && !drinking;
                if (view.Tag.gameObject.activeSelf != showBubble)
                    view.Tag.gameObject.SetActive(showBubble);

                if (showBubble)
                {
                    // A regular ordering again after a perfect serve gets a gold star and the
                    // round count (GDD 24 §4) — the reward for reading them right, made
                    // visible. The name is part of what the card teaches: it waits for the read.
                    // "x3", not a star from the font: no pixel face here carries one, so it
                    // arrived as a fallback glyph at the wrong weight beside a name set in ours.
                    string star = visit.ExtraOrdersTaken > 0
                        ? $"<color=#8F5A1E>x{visit.ExtraOrdersTaken + 1} </color>" : "";
                    view.Name.supportRichText = true;
                    // The name off their PAPERS, which is the name their licence prints —
                    // see NameOn. The ticket is where the card is remembered once it is shut.
                    view.Name.text = known && !deciding
                        ? star + NameOn(visit, view.Look).ToUpperInvariant() : "";

                    // THE ORDER ARRIVES AS SPEECH (2026-08-19, the author: "yazılar konuşma
                    // metni gibi harf harf gelecek"). The clock starts on the EDGE of the
                    // licence being read, not on the frame it is read in, so the ticket cannot
                    // restart its sentence every time the pointer moves. Reduced motion is
                    // handed the whole line at once — a typewriter is exactly the sort of
                    // thing that setting exists to switch off.
                    if (known && !view.WasKnown)
                    {
                        view.WasKnown = true;
                        view.SpeakFrom = Time.unscaledTime;
                    }
                    string wanted = known ? visit.Order.Wanted.Name.ToUpperInvariant() : "";
                    int said = Motion.Reduced ? wanted.Length
                        : Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - view.SpeakFrom) * SpeakCps),
                                      0, wanted.Length);
                    view.Spoken = said >= wanted.Length;

                    if (drinking)
                    {
                        // Served, mid-animation, off-limits. The ticket barely gets to say
                        // this any more — a drinker's plate stands down for the whole savour
                        // (see showTag) — but the branch stays honest for the frames between
                        // a serve and the balloon coming up.
                        view.Wants.text = "DRINKING" + (Motion.Reduced ? "..."
                            : new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime / DotBeat) % 3));
                        view.Wants.color = UITheme.ClubBlue[1];
                        view.Order.text = "";
                        view.Spoken = true;
                    }
                    else if (deciding)
                    {
                        // Reading the menu. One, two, three dots and round again — the beat is
                        // the only thing on the ticket, so the ticket is the size of it.
                        view.Wants.text = Motion.Reduced ? "..."
                            : new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime / DotBeat) % 3);
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                        view.Spoken = false;
                    }
                    else if (!known)
                    {
                        // Ready, unread: the one line the author asked for, and nothing else.
                        view.Wants.text = "READY TO ORDER";
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                    }
                    else if (visit.OnTheHouse)
                    {
                        // THE STORY'S GUEST NAMES ONE DRINK AT A TIME, and the post-it is
                        // where it is named (GDD 26 §4). Their licence is open from the
                        // moment they sit — they introduced themselves — so the ticket would
                        // otherwise print the ask over their head and hand the player the
                        // whole trial in advance, which is the one thing the reveal is for.
                        view.Wants.text = "TALK TO THEM";
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                        view.Spoken = false;
                    }
                    else
                    {
                        // Read: the name above, the order below — the card said the rest.
                        view.Wants.text = "";
                        view.Order.text = wanted.Substring(0, said);
                    }

                    // THE ICON ROW comes up only once the order has finished being SAID. The
                    // pictures are the fastest thing on the ticket to read, so showing them
                    // while the letters are still arriving would answer the question before
                    // the sentence asks it — and the typing would be decoration.
                    float iconW = LayOutOrderIcons(view, visit,
                        known && view.Spoken && !deciding && !drinking);

                    // The ticket FITS its lines and its WIDEST line (the author, 2026-08-02:
                    // "yazı hiçbir zaman taşmamalı"). SEX ON THE BEACH ran off both ends of
                    // a fixed card. The card takes the width of the longest thing it says,
                    // up to a cap; past the cap the order wraps to a second row and the card
                    // grows downward instead. Nothing is ever clipped, and nothing floats in
                    // an empty box.
                    //
                    // BOTH AXES ANSWER THE CONTENT NOW (2026-08-19). The height used to be
                    // the rows of TYPE only, so the icon row hung off the bottom of the plate;
                    // and the width had a 156 floor, which drew a poster round three dots.
                    float widest = Mathf.Max(view.Name.preferredWidth,
                        Mathf.Max(view.Wants.preferredWidth,
                            Mathf.Max(view.Order.preferredWidth, iconW)));
                    float cardW = Mathf.Clamp(widest + TagPad * 2f, TagMinW, TagMaxW);
                    float textW = cardW - TagPad * 2f;

                    // The order is the line that runs long, so it is the one allowed to wrap.
                    int orderLines = view.Order.text.Length == 0 ? 0
                        : Mathf.Max(1, Mathf.CeilToInt(view.Order.preferredWidth / Mathf.Max(1f, textW)));
                    view.Order.horizontalOverflow = orderLines > 1
                        ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;

                    // EACH ROW IS AS TALL AS ITS OWN FONT SAYS (2026-08-19). One constant used
                    // to stand in for three different line boxes, so the plate was always a
                    // few units taller than what it held and the type rode high in it.
                    float rowTop = -TagPad;
                    view.Name.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Name.text.Length > 0) rowTop -= view.NameLineH;
                    view.Wants.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Wants.text.Length > 0) rowTop -= view.WantsLineH;
                    view.Order.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    rowTop -= view.OrderLineH * orderLines;
                    if (view.IconRow != null && view.IconRow.gameObject.activeSelf)
                    {
                        view.IconRow.anchoredPosition = new Vector2(0, rowTop);
                        rowTop -= IconRowH;
                    }
                    // The bottom pays the foot row as well as the padding, so what the type is
                    // centred in is the WHITE FIELD and not the sprite: the plate's top edge
                    // is two units of colour and its bottom is two plus the foot's one.
                    view.Tag.sizeDelta = new Vector2(cardW, -rowTop + TagPad + TagFoot);
                }

                // (The drink icon used to dock against the order text's measured width, on
                // whatever row the order landed on. It lives on its own row now, beside the
                // serving spec, and LayOutOrderIcons places the whole row.)

                // TWO clocks now (the author, 2026-08-02): the wait to be ASKED, then a fresh
                // wait for the drink. The gauge draws whichever is live — Core says which, and
                // Core also says which THIRD of it they are in, so the colour over the head is
                // the same reading the till pays by. (The asking wait used to draw magenta;
                // the three bands speak for both clocks now, and the bubble says which one is
                // running far louder than a hue ever did.)
                float patience = (deciding || drinking) ? 1f : (float)visit.PatienceFraction;
                // THE BAR IS ONLY UP WHILE IT IS EMPTYING (2026-08-20, the author: "herhangi
                // bir sabır barı azalmıyorken kafasının üstünde bar gözükmesin ... içki
                // içerken odadan çıkarken vs"). It used to stand over every seated customer
                // and simply hold FULL through the beats where no clock runs — thinking,
                // drinking, walking out — which is a gauge that means nothing three times a
                // visit, and a room of them says the night is under pressure when it is not.
                //
                // The condition is Core's own, not a list of screens: patience ticks in
                // CustomerVisit.Tick only while the visit is WAITING, is not held, and has
                // finished deciding. Anything else — a mind being made up, a drink being
                // nursed, a guest who is being talked to (GDD 26 §4 keeps their clock on the
                // POST-IT anyway), somebody already off the stool — has no clock to draw.
                bool clockRunning = !visit.OnTheHouse && !visit.ClockHeld
                    && !deciding && !drinking && visit.State == VisitState.Waiting
                    && !CellarOpen;          // see the bubble, above
                if (view.Gauge != null && view.Gauge.gameObject.activeSelf != clockRunning)
                    view.Gauge.gameObject.SetActive(clockRunning);
                var band = visit.Band;
                var lit = band == ServiceBand.Green ? UITheme.Lime[3]
                    : band == ServiceBand.Amber ? UITheme.Amber[3] : UITheme.ViceRed[3];
                // The last third breathes. Nothing else on the head moves, so a bar that is
                // about to lose somebody is visible from across the screen without a word.
                float pulse = band == ServiceBand.Red && !Motion.Reduced
                    ? 0.74f + 0.26f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.2f))
                    : 1f;
                view.PatienceFill.fillAmount = patience;
                view.PatienceFill.color = new Color(lit.r, lit.g, lit.b, pulse);
                if (view.PatienceNeon != null)
                    view.PatienceNeon.color = new Color(lit.r, lit.g, lit.b, 0.42f * pulse);

                // Drive the animated customer (2026-07-23): walk-in, the sit-and-breathe idle,
                // a one-shot "placing the order" beat, then nursing the drink. Facing and frame
                // are chosen from the visit state; the body below the waist is clipped by the bar.
                UpdateSeatAnimation(view, visit);

                // The tag lights when a drink is built and this customer can actually take
                // it — and "can take" includes the READ: only taken orders are click-servable,
                // so the lit set and the clickable set are one set.
                //
                // It used to be a TINT over a flat rectangle, and a tint is no use to a
                // drawing: multiplying a white plate by cyan drags the magenta edge to a
                // muddy teal and the whole balloon changes colour to say one thing. The lit
                // ticket is a second SPRITE instead — the same 11x11 geometry with its edge
                // walked onto the information ramp, so only the edge moves and the plate is
                // still recognisably the same object (16 §5: light says state).
                bool canTake = drinkReady && !deciding && !drinking && visit.IdInspected;
                // A THIRD tone joined the two (2026-08-19, the author: "içecek içiyorsa pembe
                // rengi vice mavisi olsun"): while they drink, the edge walks onto the club's
                // blue — the customer is mid-animation and cannot be interacted with, and the
                // plate says so the same way the dots on it do.
                var tone = drinking ? ChromeArt.BubbleTone.Drink
                    : canTake ? ChromeArt.BubbleTone.Take
                    : ChromeArt.BubbleTone.Order;
                view.TagBg.sprite = ChromeArt.Bubble(tone);
                if (view.Tail != null) view.Tail.sprite = ChromeArt.BubbleTail(tone);
                if (view.IconRule != null)
                    view.IconRule.color = canTake ? UITheme.Cyan[0] : UITheme.Magenta[1];
            }
        }

        /// <summary>Walks a newly-seated customer in from the right along the counter and fades
        /// them up (2026-07-23): the west-facing walk cycle carries them left into their stool.</summary>
        private void AdvanceWalkIn(SeatView view)
        {
            // In from the screen's real right edge (not a few frames off the stool) at a steady
            // pace, so a far stool is a longer walk (2026-07-23).
            float entryX = _hudRoot.rect.width + OffscreenMargin;
            if (view.WalkT < 1f)
            {
                float dist = Mathf.Max(1f, entryX - view.SeatX);
                // ARRIVING SLOWS DOWN (2026-08-19, the author: "karakterler koltuğuna
                // yaklaşınca yürüme hızı biraz yavaşlamalı"). An ease-out lived here once and
                // was removed for a good reason, written down at the time: the ground slid
                // fast under slow feet, because only the FLOOR was easing. So the ease is
                // back with the missing half — WalkPace scales the walk cycle by exactly the
                // same factor it scales the speed, and the feet stay on the floor at every
                // pace. Nothing about the cycle is retimed; it is simply played slower.
                float left = (1f - view.WalkT) * dist;
                view.WalkPace = Mathf.Lerp(ArrivalPace, 1f, Mathf.Clamp01(left / ArrivalEase));
                bool stillWalking = view.WalkT < 1f;
                view.WalkT = Mathf.Min(1f,
                    view.WalkT + Time.deltaTime * WalkSpeed * view.WalkPace / dist);
                if (stillWalking && view.WalkT >= 1f)
                {
                    Sfx.Play("stool_take", 0.7f);
                }
                view.Root.anchoredPosition =
                    new Vector2(Mathf.Lerp(entryX, view.SeatX, view.WalkT), SeatLineY);
                view.Group.alpha = Mathf.Clamp01(view.WalkT * 4f);
            }
            else
            {
                view.Root.anchoredPosition = new Vector2(view.SeatX, SeatLineY);
                view.Group.alpha = 1f;
            }
        }

        /// <summary>Plays a customer leaving (2026-07-23): they get up and walk back out to
        /// the right the way they came — and since 2026-08-19 it IS the way they came (the
        /// author: "çıkış animasyonu giriş animasyonu ile aynı hızda aynı şekilde"): the
        /// entrance mirrored, same WalkSpeed, same near-stool ease, same fade. One pace for
        /// everybody — the storm-off's shake and its 1.5× hurry are gone; anger is carried by
        /// the Upset reaction beat and the toast, not by the walk.</summary>
        private void AdvanceExit(SeatView view)
        {
            // The reaction beat first: they stay on the stool and the drink answers — a fist
            // up or a slow head-shake — before they get up. One shot, then the walk.
            if (view.ReactLeft > 0f)
            {
                view.ReactLeft -= Time.deltaTime;
                // ON THE STOOL means on the stool AS IT IS RIGHT NOW (2026-08-25, the author:
                // "müşteriler tepki animasyonu verirlerse tezgah açılıp kapandığında havada
                // asılı kalıyorlar"). This branch used to return without touching the rect,
                // which is fine for a room that is standing still and is a person left
                // hanging in mid-air the moment the cellar lifts the one they are leaning on.
                // Every other beat — walking in, walking out, sitting — re-reads SeatLineY
                // each frame; this is simply the one that did not.
                view.Root.anchoredPosition = new Vector2(view.SeatX, SeatLineY);
                UpdatePatronFrame(view, view.ReactClip, ReactSeconds - view.ReactLeft, facing: 1);
                return;
            }

            // The entrance run backwards: slow at the stool, full pace by ArrivalEase out —
            // and the cycle is scaled by the same factor as the floor, so the feet grip at
            // every step exactly as they do on the way in (see AdvanceWalkIn).
            float exitX = _hudRoot.rect.width + OffscreenMargin;
            float dist = Mathf.Max(1f, exitX - view.SeatX);
            float gone = view.ExitT * dist;
            float pace = Mathf.Lerp(ArrivalPace, 1f, Mathf.Clamp01(gone / ArrivalEase));
            view.ExitT = Mathf.Min(1f,
                view.ExitT + Time.deltaTime * WalkSpeed * pace / dist);
            view.Root.anchoredPosition = new Vector2(
                Mathf.Lerp(view.SeatX, exitX, view.ExitT), SeatLineY);
            // The entrance fades up over its first quarter; leaving fades down over the last.
            view.Group.alpha = Mathf.Clamp01((1f - view.ExitT) * 4f);

            // Mirror the walk so they face the way they are leaving (to the right).
            UpdatePatronFrame(view, PatronClip.Walk, view.AnimClock, facing: -1);
            view.AnimClock += Time.deltaTime * Mathf.Max(0.05f, pace);

            if (view.ExitT >= 1f)
            {
                view.Exiting = false;
                // They are through the door: book the visit and the stars against the FACE
                // that walked out, which is the last moment both are still in hand.
                RecordDeparture(view.Look, view.Visit);
                view.Visit = null;
                // AN EMPTY STOOL HAS NO FACE (2026-08-25). This line is the whole reason the
                // bar looked like four people on a loop. The arrival that reuses this stool
                // sets `v.Visit` FIRST and asks LookFor SECOND, and LookFor's first act is to
                // honour a stool that already holds a look - which this stool still did,
                // belonging to whoever just walked out. So every customer after the first on
                // any stool inherited the last one's face: four stools, four faces, all
                // night, every night, and a licence that read "3rd visit" with a full row of
                // stars on the bar's opening hour. Measured on 2026-08-25: seven different
                // people through the door, four faces drawn.
                view.Look = null;
                // The next customer on this stool speaks their own order from the beginning.
                view.WasKnown = false;
                view.Spoken = false;
                view.Group.alpha = 1f;
                if (view.Body != null) view.Body.flipX = false;   // reset the mirror
                view.Root.gameObject.SetActive(false);
            }
        }

        // ── the animated customer (2026-07-23) ───────────────────────────────────

        /// <summary>Chooses the clip and frame for a seated customer from their state and drives
        /// the character image: the sit-and-breathe idle while they wait, a one-shot "placing the
        /// order" beat the moment they decide, and the drink once served. (An impatience flush
        /// used to tint the body here; removed 2026-08-19, the author: "kızınca kızarmasın
        /// kararmasın" — running out of patience is the gauge's job, not the skin's.)</summary>
        private void UpdateSeatAnimation(SeatView view, CustomerVisit visit)
        {
            bool ordered = visit.HasOrdered;
            bool seated = view.WalkT >= 1f;
            bool drinking = visit.State == VisitState.Drinking;

            if (ordered && !view.WasOrdered && seated)
            {
                view.OrderAnimLeft = OrderAnimSeconds;
                Sfx.Play("order_ready", 0.6f);
            }
            view.WasOrdered = ordered;

            if (drinking) view.DrinkT += Time.deltaTime; else view.DrinkT = 0f;

            PatronClip clip; float t;
            if (!seated)                      { clip = PatronClip.Walk;  t = view.AnimClock; }   // faces left, walking in
            else if (drinking)                { clip = PatronClip.Drink; t = view.DrinkT; }
            else if (view.OrderAnimLeft > 0f) { clip = PatronClip.Order; t = OrderAnimSeconds - view.OrderAnimLeft;
                                                view.OrderAnimLeft -= Time.deltaTime; }
            else                              { clip = PatronClip.Idle;  t = view.AnimClock; }
            // The clock the walk is played on runs at the pace the figure is moving, so an
            // arriving customer's feet slow with the floor instead of skating on it.
            view.AnimClock += Time.deltaTime * (seated ? 1f : Mathf.Max(0.05f, view.WalkPace));

            // A seated customer's idle is a STILL frame, so the life in it comes from where
            // they are looking. This runs only in the idle branch — somebody speaking their
            // order or lifting a glass has better things to do with their head.
            int exact = -1;
            if (clip == PatronClip.Idle) SeatedGlance(view, ref clip, ref exact);
            UpdatePatronFrame(view, clip, t, facing: 1, exactFrame: exact);
        }

        /// <summary>
        /// Turns the idle into a look. Nobody beside them: they hold still and glance a
        /// little to one side every few seconds, alternating. Somebody on one side: they
        /// turn to that person and HOLD it while they are there. Somebody on both: they
        /// look between them, on the same slow clock.
        ///
        /// The clock is the seat's own animation time plus a phase off its index — six
        /// customers glancing in unison would read as a machine, and a phase is free where
        /// a random number would have to be plumbed through RunRng to stay deterministic.
        /// </summary>
        private void SeatedGlance(SeatView view, ref PatronClip clip, ref int frame)
        {
            var look = view.Look;
            if (look == null) return;
            bool right = Occupied(view.Index + 1), left = Occupied(view.Index - 1);

            // Somebody SITTING DOWN is the event, not somebody being there: the glance is a
            // one-shot on the rising edge, and it ends by coming back (2026-08-19, the
            // author: "bakma animasyonu bittikten sonra normal pozisyona geri dönmeli").
            // Held forever, it stopped being a glance and became a pose.
            if (right && !view.SawRight) { view.Greeting = true; view.GreetRight = true;  view.GreetT = 0f; }
            else if (left && !view.SawLeft) { view.Greeting = true; view.GreetRight = false; view.GreetT = 0f; }
            view.SawRight = right; view.SawLeft = left;

            bool useRight = view.Greeting ? view.GreetRight
                          : Mathf.FloorToInt((view.AnimClock + view.Index * 1.7f) / GlanceEvery) % 2 == 0;
            var want = useRight ? PatronClip.LookRight : PatronClip.LookLeft;
            if (!look.Clips.TryGetValue(want, out var frames) || frames.Length == 0)
            {
                view.Greeting = false;
                return;
            }
            int hold = Mathf.Clamp(useRight ? look.HoldRight : look.HoldLeft, 1, frames.Length - 1);

            if (view.Greeting)
            {
                view.GreetT += Time.deltaTime;
                // The head turns at the house rate too, not at a rate of its own: the turn
                // takes as long as its own frames take, which is what keeps a glance the
                // same speed as the walk beside it.
                float outT = hold / PatronFps, backAt = outT + GreetHoldSeconds;
                if (view.GreetT >= backAt + outT) { view.Greeting = false; return; }
                float u = view.GreetT < outT ? view.GreetT / outT
                        : view.GreetT < backAt ? 1f
                        : 1f - (view.GreetT - backAt) / outT;
                // Held at the MEASURED far end rather than the clip's last frame: some of
                // these clips swing back to the front on their own, and holding their end
                // would hold a face looking straight ahead.
                frame = Mathf.Clamp(Mathf.RoundToInt(u * hold), 0, hold);
                clip = want;
                return;
            }

            // Nobody just arrived: still for most of a slow cycle, then a small look out and
            // back. The clock carries a phase off the seat index — six customers glancing in
            // unison would read as a machine, and a phase is free where a random number
            // would have to be plumbed through RunRng to stay deterministic.
            float t = Mathf.Repeat(view.AnimClock + view.Index * 1.7f, GlanceEvery);
            if (t >= GlanceLookSeconds) return;                 // the still frame, which is idle
            int small = Mathf.Min(GlanceSmallFrame, frames.Length - 1);
            float g = t / GlanceLookSeconds;
            int f = g < 0.28f ? Mathf.FloorToInt(g / 0.28f * (small + 1))
                  : g > 0.72f ? Mathf.FloorToInt((1f - g) / 0.28f * (small + 1))
                  : small;
            frame = Mathf.Clamp(f, 0, small);
            clip = want;
        }

        /// <summary>Is there somebody sitting on that stool? Off the end of the bar counts
        /// as nobody, which is why the two end seats only ever glance one way.</summary>
        private bool Occupied(int index) =>
            index >= 0 && index < _seats.Count && _seats[index].Visit != null
            && _seats[index].WalkT >= 1f && !_seats[index].Exiting;

        /// <summary>Sets the character image to the right frame of <paramref name="clip"/> at time
        /// <paramref name="t"/>, mirrored when <paramref name="facing"/> is -1 (leaving right).
        /// <paramref name="exactFrame"/> overrides the clip's own timing when the caller has
        /// already decided which frame it wants (the glances, which are driven by who is
        /// sitting where rather than by a clock).</summary>
        private void UpdatePatronFrame(SeatView view, PatronClip clip, float t, int facing,
            int exactFrame = -1)
        {
            var look = view.Look ?? (_looks.Count > 0 ? _looks[0] : null);
            if (look == null || !look.Clips.TryGetValue(clip, out var frames) || frames.Length == 0) return;
            if (view.Body == null) return;
            view.Body.sprite = frames[exactFrame >= 0
                ? Mathf.Clamp(exactFrame, 0, frames.Length - 1)
                : PatronFrameIndex(clip, t, frames.Length)];
            // A touch wider than tall (CharWiden). The mirror is flipX rather than a negative
            // scale: a negative scale on a lit sprite inverts its winding and the 2D renderer
            // drops it, so a leaving customer would simply vanish.
            view.Body.flipX = facing < 0;
            SyncPatronBody(view);
        }

        /// <summary>The frame index for a clip at time t. Most clips loop at a fixed rate; the
        /// drink raises and lowers the glass over a sip window then holds it at rest, so it reads
        /// as a real sip every few seconds instead of a gulp every frame (2026-07-23).</summary>
        private static int PatronFrameIndex(PatronClip clip, float t, int n)
        {
            if (n <= 1) return 0;
            // STRAIGHT THROUGH, and hold the last frame. Every one-shot is now drawn in two
            // halves - out to the middle of the action, then INTERPOLATED back to the idle
            // pose - so its last frame is the idle pose and the return is drawn rather than
            // reversed. That is what the halves bought: a clip that ends where the idle
            // stands, at twice the frames, with nothing mirrored.
            if (clip == PatronClip.Walk) return Mathf.FloorToInt(t * PatronFps) % n;
            if (clip == PatronClip.Drink)
            {
                // A sip, then a pause standing as they were, then another sip - the
                // "1. yudum, 2. yudum" the author asked for, out of one clip that ends
                // where it began. The clip is not played flat: see DrinkTicks.
                float u = Mathf.Repeat(t, DrinkCycleSeconds) * PatronFps;   // in ticks
                int acc = 0;
                for (int i = 0; i < n; i++)
                {
                    acc += DrinkTicks(i, n);
                    if (u < acc) return i;
                }
                return n - 1;   // the rest: standing with the glass down, as the clip left them
            }
            return Mathf.Min(n - 1, Mathf.FloorToInt(t * PatronFps));
        }

        /// <summary>
        /// THE DRINK'S TIMING CHART — how many ticks of 1/PatronFps each frame is held for.
        /// One rate still (the walk's), with HOLDS on it, which is how an animator slows a
        /// beat without slowing the film: "FPS tüm animasyonlarda aynı olmalı" is untouched.
        ///
        /// Everything hangs off ONE fact about the art: the clip is two halves joined —
        /// out to the glass at the mouth, then interpolated back to the idle pose — so the
        /// SIP IS THE MIDDLE FRAME. That is measured, not assumed, and it holds across the
        /// live cast's two clip lengths: 17 frames puts the glass at the lips on 7-8-9
        /// (afrowoman, clubgirl, silverbob) and 16 puts it on 6-7-8 (heavyset). The
        /// remaining cast still stands on the old rig and does not load; when they are
        /// redrawn they arrive at 17 like everyone else.
        ///
        ///   middle ±1   the swallow            5 ticks   0.42s each
        ///   ±2 … ±4     the arm's travel       2 ticks   the lift takes ~0.5s, and so does
        ///                                                the lower, where both were 0.25s
        ///   further     standing, glass down   1 tick    dead frames at either end
        ///
        /// A clip long enough for the chart to outrun DrinkCycleSeconds would be cut at the
        /// rest rather than dropping frames; at 35 ticks (2.9s) against a 4.4s cycle the
        /// longest clip in the cast has a second and a half of room.
        /// </summary>
        private static int DrinkTicks(int frame, int n)
        {
            int off = Mathf.Abs(frame - (n - 1) / 2);
            if (off <= 1) return DrinkSipTicks;
            return off <= 4 ? 2 : 1;
        }

        private void LoadPatronFrames()
        {
            _looks.Clear();
            foreach (var entry in PatronCast)
            {
                var clips = new Dictionary<PatronClip, Sprite[]>
                {
                    [PatronClip.Idle]  = LoadPatronClip(entry.Slug, "idle"),
                    [PatronClip.Order] = LoadPatronClip(entry.Slug, "order"),
                    [PatronClip.Drink] = LoadPatronClip(entry.Slug, "drink"),
                    [PatronClip.Walk]  = LoadPatronClip(entry.Slug, "walk"),
                    [PatronClip.Cheer] = LoadPatronClip(entry.Slug, "cheer"),
                    [PatronClip.Upset] = LoadPatronClip(entry.Slug, "upset"),
                    [PatronClip.LookRight] = LoadPatronClip(entry.Slug, "look_right"),
                    [PatronClip.LookLeft]  = LoadPatronClip(entry.Slug, "look_left"),
                };
                // A look with no idle has no art on disk. Skip it instead of seating a
                // customer who renders as nothing.
                if (clips[PatronClip.Idle].Length == 0) continue;
                var face = Resources.Load<Sprite>($"Patron/{entry.Slug}/face");
                _looks.Add(new PatronLook
                { Slug = entry.Slug, Clips = clips, HeadY = entry.HeadY, Face = face,
                  Stars = entry.Stars,
                  HoldRight = entry.HoldRight, HoldLeft = entry.HoldLeft });
            }
        }

        /// <summary>All frames of one clip, ordered by name. Everyone in the cast lives
        /// under their own slug. The very first patron used to sit loose at Patron/&lt;clip&gt;
        /// with no slug of their own, and this read that too; that art was deleted in the
        /// 2026-08-20 sweep along with the rest of the old rig, so the branch is gone.</summary>
        private static Sprite[] LoadPatronClip(string slug, string clip)
        {
            var sprites = Resources.LoadAll<Sprite>($"Patron/{slug}/{clip}");
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        private SeededRng FaceRng => _faceRng ?? (_faceRng =
            new RunRng((_bootstrap != null ? _bootstrap.CurrentSeed : null) ?? "").GetStream("faces"));

        /// <summary>
        /// Which face sits down. The same person is the same face every time they come back —
        /// recognising them across visits is the whole point of remembering anybody — and a
        /// person nobody has met yet is given whichever face has been off the floor longest.
        /// </summary>
        private PatronLook LookFor(CustomerVisit visit)
        {
            if (_looks.Count == 0) return null;
            // A seat that already holds a look keeps it: this is asked again every time
            // the licence is opened, and a face that changed under the player would undo
            // the whole point of having twenty-two of them.
            foreach (var seat in _seats)
                if (seat.Visit == visit && seat.Look != null) return seat.Look;

            // THE STORY'S GUEST IS NOT ROLLED (GDD 26 §8): the beat names the face, and it
            // is the same face every night of the run. Rolling one instead would put the
            // rent collector in a different body each time he came back — which is the
            // one thing a recurring character cannot survive.
            var run = Run;
            if (visit != null && run != null && ReferenceEquals(visit, run.LastCustomer))
            {
                var written = LookForStory(run.LastCallBeat?.Who);
                if (written != null) return written;
            }
            if (visit == null) return _looks[0];

            // Asked again after they have walked out — the night's invoice staples a
            // polaroid of its two witnesses to the takings. A second answer here would put
            // a stranger's photograph on the receipt.
            if (_faceOfVisit.TryGetValue(visit, out var booked)) return booked;

            // Only the people this bar has earned. Someone is always available — the
            // 0-star set never empties — so this cannot starve.
            float standing = run != null ? (float)run.Rating.Average : 0f;
            var open = new List<PatronLook>();
            foreach (var look in _looks)
                if (look.Stars <= standing + 0.001f) open.Add(look);
            if (open.Count == 0) open.Add(_looks[0]);

            // NO TWO OF THE SAME DRAWING IN THE ROOM (the author, 2026-08-10). Each drawing
            // IS a character, so the same face on two stools reads as a bug rather than as
            // a coincidence — and the registry can hand back somebody who is still sitting
            // there, which is the one case that produces it.
            var free = new List<PatronLook>();
            foreach (var look in open)
            {
                bool taken = false;
                foreach (var seat in _seats)
                    if (seat.Visit != null && seat.Visit != visit && seat.Look == look)
                    { taken = true; break; }
                if (!taken) free.Add(look);
            }
            bool doubling = free.Count == 0;
            if (doubling) free = open;      // more stools than faces: somebody has to double up

            string person = visit.Regular != null ? visit.Regular.Id : null;
            PatronLook theirs = null;
            bool met = person != null && _faceOfPerson.TryGetValue(person, out theirs);
            if (met && (doubling || free.Contains(theirs)))
                return BookFace(visit, person, theirs);

            // The longest-unseen face, and a coin toss between those tied at the back of the
            // queue — which on the opening night is every face in the building.
            int oldest = int.MaxValue;
            foreach (var look in free)
                oldest = Math.Min(oldest, _faceLastSeen.TryGetValue(look, out var t) ? t : 0);
            var queue = new List<PatronLook>();
            foreach (var look in free)
                if ((_faceLastSeen.TryGetValue(look, out var seen) ? seen : 0) == oldest)
                    queue.Add(look);

            // A face they are only BORROWING: somebody the bar has already met, whose own
            // face is on another stool this minute, is drawn as a free one for this visit
            // alone and keeps their real face for the next time they come in.
            return BookFace(visit, met ? null : person, queue[FaceRng.NextInt(queue.Count)]);
        }

        private static string CrowdName(WealthTier tier) =>
            tier == WealthTier.HighRoller ? "HIGH ROLLERS" : tier == WealthTier.Broke ? "BROKE" : "REGULARS";

        private PatronRecord LogFor(PatronLook look)
        {
            string key = look != null && !string.IsNullOrEmpty(look.Slug) ? look.Slug : "patron";
            PatronRecord rec;
            if (!_patronLog.TryGetValue(key, out rec))
            {
                rec = new PatronRecord();
                _patronLog[key] = rec;
            }
            return rec;
        }

        /// <summary>One person has walked back out. Books the visit and the stars they left.</summary>
        private void RecordDeparture(PatronLook look, CustomerVisit visit)
        {
            if (visit == null) return;
            var rec = LogFor(look);
            rec.Visits++;
            // THE SAME NUMBER THE BAR'S OWN STANDING IS BUILT FROM (BarRating.Record books
            // exactly this for every finished visit), so the licence and the top bar cannot
            // disagree about how a night went.
            rec.Stars += BarRating.ExactStarsFor(visit.Satisfaction);
            rec.Ratings++;
        }

        /// <summary>
        /// Stands the world body where its seat says, at the size and the alpha the seat is
        /// wearing. The body is a PASSENGER of the stool: every line that already moved,
        /// faded or hid a seat keeps working untouched, and this reads the result once a
        /// frame rather than each of them having to learn about the room.
        ///
        /// The conversion is the stage's own contract — one world unit is one stage unit,
        /// the HUD is drawn at twice that (StageToHud), and the stage's origin is the middle
        /// of its 640x360. The character's FEET sit CharFootDrop below the stool's line,
        /// which is what the counter then covers.
        /// </summary>
        private void SyncPatronBody(SeatView view)
        {
            if (view.Body == null) return;
            bool on = view.Root != null && view.Root.gameObject.activeSelf && view.Body.sprite != null;
            if (view.Body.gameObject.activeSelf != on) view.Body.gameObject.SetActive(on);
            if (!on) return;

            // WHO IS IN FRONT (2026-08-10, the author: a walker crossed in front of the
            // people already at the bar). Every body sat at one sorting order, so their
            // relative depth was whatever the renderer felt like. Somebody still walking
            // in or out is BEHIND everyone seated — they are further into the room — and
            // among the seated, the nearer stool draws in front, which is just perspective.
            bool walking = view.Exiting || view.WalkT < 1f;
            view.Body.sortingOrder = walking ? 22 : 25;

            float drawnH = CharSize / StageToHud;                       // stage units tall
            float k = drawnH / Mathf.Max(0.0001f, view.Body.sprite.bounds.size.y);
            view.Body.transform.localScale = new Vector3(k * CharWiden, k, 1f);

            var p = view.Root.anchoredPosition;
            float footY = (p.y - CharFootDrop) / StageToHud;            // stage units
            view.Body.transform.position = new Vector3(
                p.x / StageToHud - StageRef.x * 0.5f,
                footY + drawnH * 0.5f - StageRef.y * 0.5f, 0f);

            var c = view.Body.color;
            c.a = view.Group != null ? view.Group.alpha : 1f;
            view.Body.color = c;
        }

        private PatronLook LookNamed(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return null;
            foreach (var look in _looks) if (look.Slug == slug) return look;
            return null;
        }

        /// <summary>The face this beat's person wears — their own if it has been drawn, the
        /// one they borrow until then (GDD 26 §1b). Null once neither exists, which is a
        /// content error the loader already refuses.</summary>
        private PatronLook LookForStory(StoryCharacter who) =>
            who == null ? null : LookNamed(who.Look) ?? LookNamed(who.PlaceholderLook);
    }
}
