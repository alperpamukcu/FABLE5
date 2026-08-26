using System;
using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The shaker stage (GDD 24 §2), rebuilt 2026-08-13: the bench is the tin, the bottle
    /// in hand, and THE RAIL — one shelf across the back carrying every bottle the bar
    /// stocks, so a three-ingredient build is three clicks on one bench instead of three
    /// trips through the menu. Tip the bottle into the open tin, stir with the spoon OR
    /// cap it and shake, then exit right to the glass. The liquid is a real particle
    /// body, so it pours, pools and sloshes.
    ///
    /// What LEFT the bench in the rebuild: the prep table (it carried nothing since the
    /// four preps moved to the glass on 2026-08-10 — dead furniture the cap and spoon
    /// were drawn across), and the dormant prep-drag machinery with it.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        private RectTransform _pourBottle;    // the grabbable bottle
        private RectTransform _pourVessel;    // the bottle itself inside it, sized to its art
        private Image _pourBottleBody;
        private BottleFill _pourFill;         // what is left in it, behind the glass
        /// <summary>Where this bottle's CAP is, as an offset from the grip — measured off the
        /// art (VesselArt) when the stage refreshes, swung with the bottle when it tips.</summary>
        private Vector2 _pourMouth;
        private MetaballFluid _shakerFluid;   // the metaball liquid: pour stream + pooled body
        private ShakerSolids _shakerSolids;   // ice / lemon afloat inside the shaker
        private float _slosh;                 // running slosh phase for the shaker surface
        private Vector2 _bottleRest;
        private bool _bottleGrabbed;
        private bool _pouring;
        private const float LiftRange = 200f;  // px of lift for a full tilt
        private const float MaxTilt = 118f;    // degrees the bottle leans at full lift
        // 230 → 300 (the author, 2026-08-05: "shakera dökme sahnesinde tüm alkol
        // şişelerinin boyutunu büyüt") — the v3 masters are slimmer than the old art,
        // and at 230 a 3.7:1 bottle read as a wand. The mouth offset and the tilt
        // maths all derive from this, so the pour arc scales with it.
        private const float BottleH = 300f;
        // The pour fills slower than the raw bottle rate so the stream reads as a real pour
        // (GDD 24 §2, 2026-07-22 — "doluş hızı çok hızlı"). Only the drawn volume slows; the
        // floor's patience clock runs on its own tick, untouched.
        private const float PourTimeScale = 0.45f;

        // NO DRINKS STAND ON THIS BENCH (2026-08-13, the author: "shakerin doldurulduğu
        // sahnede de içecekler olmayacak, oyuncu içecek seçmek için back bar sahnesine
        // gidecek"). A speed rail along the back wall was built and taken out again: the
        // bar has ONE place where a drink is chosen, and it is the back bar. The bench is
        // what your hands are on — the bottle you came in with, the tin, the lid and the
        // spoon — and nothing else.

        // The shake (GDD 24 §2.5, 2026-07-22): grab the shaker itself and throw it around —
        // it springs after the cursor with overshoot (loose and lively), the liquid sloshes,
        // and how far the cursor travels builds the shake energy.
        private bool _shaking;
        /// <summary>Which held-action sound this frame wants; the stage frame plays it once,
        /// so the pour and the shake cannot silence each other (P17).</summary>
        private string _shakerLoopWanted;
        private double _shakeEnergy;
        private Vector2 _lastShakeMouse;
        private Vector2 _shakerVel;      // the shaker's spring velocity while thrown about
        private Vector2 _shakerHome;     // its rest position

        // What the mat used to do: say that the tin and the bottle are ON something. Two
        // contact shadows on the counter line, each following its prop's x and thinning as
        // it is lifted away — a shaken tin is in the air, and its shadow should know.
        private RectTransform _tinShadow, _bottleShadow;

        // The STIR (GDD 21 §14, 2026-08-11): the mandatory mix made Preparations.Stirred
        // load-bearing, so the bench grew a bar spoon. Stir and shake are told apart by the
        // CAP — the spoon only works an OPEN tin, the shake only a capped one — so the two
        // mixing verbs can never fight over one gesture.
        private RectTransform _spoonRt;
        private Vector2 _spoonRest;
        private bool _spoonHeld;
        private double _stirEnergy;
        private float _stirPrevAngle;
        private bool _stirHasPrev;
        /// <summary>Radians of circling over the tin for a 100% stir — about five laps.</summary>
        private const float StirFullRadians = 5f * 2f * Mathf.PI;

        // The way OUT of the bench (the author's loop rework): once the tin is capped and
        // the mix rule is satisfied, the drink moves ON to the glass instead of back
        // through the menu. Gated on Core's own CanPourOut, so the key can never walk
        // the player into the refusal.
        private Button _toGlassBtn;
        private Button _lidOffKey;
        private CanvasGroup _lidOffGroup;
        private Text _shakerHint;

        /// <summary>The standing line under the bench's title: what this drink asks for, or
        /// the pair of choices while the tin holds nothing the book can name.</summary>
        private static string ShakerHintFor(PrepMethod? method, bool bottleInReach = true)
        {
            string named =
                method == PrepMethod.Shaken ? "THIS ONE IS SHAKEN"
                : method == PrepMethod.Stirred ? "THIS ONE IS STIRRED"
                : method == PrepMethod.Built ? "THIS ONE IS BUILT — DO NOT WORK IT"
                : null;
            if (!bottleInReach) return named ?? "CAPPED — SHAKE IT, OR TAKE IT OVER";
            return named == null
                ? "GRAB THE BOTTLE TO POUR · STIR IT, OR CAP IT AND SHAKE"
                : "GRAB THE BOTTLE TO POUR · " + named;
        }
        private CanvasGroup _toGlassGroup;
        private Text _toGlassLabel;
        private bool _toGlassWasOn;
        private float _toGlassPulse;

        /// <summary>The work meter's track. The fill derives from it, so the bar can
        /// actually reach its own end at 100%.</summary>
        private const float ShakeMeterW = 260f, MeterH = 22f;
        /// <summary>Where the tube's mark stands: the point past which the tin is worked
        /// enough for the drink to be worth pouring. Measured against the same 0..1 both
        /// verbs report, so one mark reads for the shake and for the stir.</summary>
        private const float EnoughMark = 0.72f;
        private RectTransform _shakeMeterRig, _shakeMeterMark;
        private Image _shakeMeterFill;
        private Text _shakeMeterText;

        /// <summary>
        /// Puts a reading on the work meter and brings it out. AT REST IT IS NOT THERE:
        /// the old bar sat on the counter empty and black whenever nobody was shaking, which
        /// is a gauge reporting on nothing. StepWorkMeter takes it away again.
        /// </summary>
        private void ShowWorkMeter(float amount, Color tone, string caption)
        {
            if (_shakeMeterRig == null) return;
            _meterHeldThisFrame = true;
            if (!_shakeMeterRig.gameObject.activeSelf) _shakeMeterRig.gameObject.SetActive(true);
            _shakeMeterFill.fillAmount = Mathf.Clamp01(amount);
            // Past the mark it goes green, and that is the whole reading: the colour says
            // "enough" at the same instant the fill crosses the tick that says where enough is.
            _shakeMeterFill.color = amount >= EnoughMark ? UITheme.Lime[3] : tone;
            if (_shakeMeterText != null) _shakeMeterText.text = caption;
            if (_shakeMeterMark != null)
                _shakeMeterMark.gameObject.SetActive(amount < EnoughMark);
        }

        /// <summary>Takes the meter away on the first frame nothing claimed it.</summary>
        private void StepWorkMeter()
        {
            if (_shakeMeterRig == null) return;
            if (!_meterHeldThisFrame && _shakeMeterRig.gameObject.activeSelf)
                _shakeMeterRig.gameObject.SetActive(false);
            _meterHeldThisFrame = false;
        }

        private bool _meterHeldThisFrame;
        private const float ShakeFullTravel = 4000f;   // px of cursor travel for a full shake
        // The tin keeps MORE give than the other two on purpose: the whole verb is
        // throwing a heavy thing about, and a tin welded to the cursor cannot be shaken.
        // But 105/6 was a balloon on a string; 210/17 is a full tin with a wrist behind it.
        private const float ShakeStiffness = 210f;      // follows hard, still swings
        private const float ShakeDamping = 17f;

        // The pour gauge (2026-07-31, the author's note): WHILE pouring, a bar shows each
        // ingredient's share in its own liquid colour with the percentage inked on it — the
        // number the recipe bands grade, live, where the pouring happens.
        private RectTransform _shakerMixBar;
        private string _mixBarSig = "";

        private void RefreshShakerMixBar(TycoonRun run)
        {
            if (_shakerMixBar == null) return;
            var glass = run.Glass;
            var sig = new StringBuilder();
            foreach (var id in glass.Ingredients)
                sig.Append(id).Append((int)(glass.RatioOf(id) * 100)).Append(';');
            sig.Append((int)(glass.FillFraction * 100));
            string signature = sig.ToString();
            if (signature == _mixBarSig) return;
            _mixBarSig = signature;

            // The tin's own column, captioned in the air beside it (labels to the LEFT:
            // the track hugs the right wall, and the TO THE GLASS key is past it).
            FillGauge(_shakerMixBar, glass, run, labelsLeft: true);
        }

        // ── the bench's steps, in order (2026-08-14) ─────────────────────────────
        //
        // The author asked for the top-left corner to say what to do, in order, with icons.
        // It is not a tooltip and not a tutorial that fires once: it is a CHECKLIST that
        // reads the bench's live state, so the step you are on is lit, the ones behind you
        // are ticked, and the ones ahead are dim. A player who already knows the bench sees
        // where they are at a glance; one who does not is told what to do next, in order,
        // without a word of prose.

        private readonly List<(Image icon, Text label, Image tick)> _stepRows =
            new List<(Image, Text, Image)>();

        private void BuildStepCard(RectTransform panel)
        {
            string[] marks = { "pour", "cap", "mix", "toglass" };
            string[] words = { "FILL THE TIN", "CAP IT", "SHAKE OR STIR", "TO THE GLASS" };
            BuildStepCard(panel, "THE BENCH", marks, words, _stepRows, new Vector2(20, -18));
        }

        /// <summary>
        /// The card itself, for whichever station asks for one (2026-08-14, the author:
        /// "Shake veya karıştırma yapması gerektiği sahnede sol üstte belirtilsin, aynı
        /// öğreticiyi bardağa koyma sahnesi içinde oluştur"). Same corner, same 8px rows,
        /// same tick — a player who learns to read it at the bench can read it at the
        /// counter without being taught twice.
        /// </summary>
        /// <summary>How far below the field's own top edge anything on a bench may start.
        /// The room's fascia is 54 units of instrument and the flow draws OVER it, so a
        /// card pinned to the top pinned itself across the clock (2026-08-26, the author:
        /// "üst barın üstüne denk geliyor").</summary>
        private const float BenchTopClear = 74f;

        private void BuildStepCard(RectTransform panel, string head, string[] marks,
            string[] words, List<(Image icon, Text label, Image tick)> rows, Vector2 at)
        {
            const float CardW = 246f, RowH = 26f, HeadH = 26f;
            var card = NewRect("Steps", panel);
            Place(card, new Vector2(0, 1), new Vector2(CardW, HeadH + 10f + marks.Length * RowH),
                new Vector2(at.x, at.y - BenchTopClear));
            // A PLATE, not a wash of black (2026-08-26). It used to be a 72%-opaque
            // rectangle, which over a lit wall is a smudge; it is the house's own card now,
            // with a capped head — the same grammar the fascia's wells and the market's
            // tiles are built in, so the bench stops looking like a different game.
            var bg = card.gameObject.AddComponent<Image>();
            bg.sprite = ChromeArt.Card();
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Night[1];
            bg.raycastTarget = false;

            var cap = NewRect("Cap", card);
            cap.anchorMin = new Vector2(0, 1); cap.anchorMax = Vector2.one;
            cap.pivot = new Vector2(0.5f, 1);
            cap.offsetMin = new Vector2(3, -HeadH); cap.offsetMax = new Vector2(-3, -3);
            var capImg = cap.gameObject.AddComponent<Image>();
            capImg.color = UITheme.Cyan[0];
            capImg.raycastTarget = false;

            var title = NewText("H", cap, _body, 8, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
            Stretch(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(9, 0), Vector2.zero);
            title.text = head;

            for (int i = 0; i < marks.Length; i++)
            {
                float y = -HeadH - 4f - i * RowH;
                var row = NewRect("Step" + i, card);
                Place(row, new Vector2(0, 1), new Vector2(CardW - 16f, RowH - 2f),
                      new Vector2(8, y));

                // NUMBERED, NOT PICTURED (2026-08-26, the author: "oluşturulan iconlar
                // anlaşılır değil"). Four 16px silhouettes were asked to say "fill the tin",
                // "cap it", "shake or stir" and "take it over"; at that size a shaker and a
                // lid are the same blob, and a picture that has to be decoded is worse than
                // no picture at all beside the words that already say it. The mark is the
                // STEP NUMBER now, in a socket — which is the one thing about a checklist
                // that a glance actually needs: where you are in it.
                var mark = NewRect("I", row);
                Place(mark, new Vector2(0, 0.5f), new Vector2(18, 18), new Vector2(2, 0));
                var mimg = mark.gameObject.AddComponent<Image>();
                mimg.sprite = ChromeArt.Card();
                mimg.type = Image.Type.Sliced;
                mimg.color = UITheme.Night[3];
                mimg.raycastTarget = false;
                var num = NewText("N", mark, _display, 8, TextAnchor.MiddleCenter,
                                  UITheme.TextSecondary);
                Stretch(num.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                num.text = (i + 1).ToString();
                num.raycastTarget = false;

                var text = NewText("L", row, _body, 8, TextAnchor.MiddleLeft, UITheme.TextSecondary);
                Place(text.rectTransform, new Vector2(0, 0.5f), new Vector2(178, 14),
                      new Vector2(28, 0));
                text.text = words[i];

                var tick = NewRect("T", row);
                Place(tick, new Vector2(1, 0.5f), new Vector2(12, 12), new Vector2(-4, 0));
                var timg = tick.gameObject.AddComponent<Image>();
                timg.sprite = ChromeArt.Mark("tick");
                timg.preserveAspect = true;
                timg.raycastTarget = false;
                timg.enabled = false;

                rows.Add((mimg, text, timg));
            }
        }

        /// <summary>
        /// WHETHER THE TIN STILL HAS WORK IN IT (2026-08-14, the author's bug: picking a
        /// soda after the spirits jumped straight to the glass, leaving a tin that was
        /// never capped and could no longer be reached). One reading of "done", asked by
        /// every door out of the bench, so no route can disagree with the step card: there
        /// is something in the tin, and it is not both capped and mixed the way Core asks.
        /// </summary>
        private bool BenchUnfinished(TycoonRun run) =>
            run != null && !run.Glass.IsEmpty && !(_capped && run.CanPourOut);

        /// <summary>What the tin is still owed, in the player's words — used by the doors
        /// that turn a player back, so being refused always says why.</summary>
        private string BenchOwed(TycoonRun run) =>
            !_capped ? "cap the tin first" : "shake or stir it first";

        /// <summary>
        /// Which step the bench is on, read off the same state the keys and Core read — the
        /// card cannot disagree with the bar, because it is not told anything the bar does
        /// not already know.
        /// </summary>
        private void UpdateStepCard(TycoonRun run)
        {
            if (_stepRows.Count == 0) return;
            bool filled = !run.Glass.IsEmpty;
            bool mixed = run.IsMixed;
            int at = !filled ? 0 : !_capped ? 1 : !mixed && run.MixRequired ? 2 : !mixed ? 2 : 3;

            // THE CARD NAMES THE METHOD (2026-08-14, the author: "tariflerin hangilerinin
            // çalkalanması gerektiği hangisinin karıştırması gerektiği belirtilsin önemli").
            // The third row stops being a menu of two and becomes the instruction the tin's
            // own contents ask for. A Built drink's row is ticked on sight: there is nothing
            // to do to it, and a step with nothing to do is a step already taken.
            var method = filled ? run.TinMethod : null;
            // With the lid on there is no bottle to grab, so the line drops that half.
            if (_shakerHint != null)
                _shakerHint.text = _capped ? ShakerHintFor(method, false) : ShakerHintFor(method, true);
            int optional = -1;
            bool optionalDone = false;
            if (_stepRows.Count > 2)
            {
                _stepRows[2].label.text =
                    method == PrepMethod.Shaken ? "SHAKE IT"
                    : method == PrepMethod.Stirred ? "STIR IT"
                    : method == PrepMethod.Built ? "BUILT, NO MIXING"
                    : "SHAKE OR STIR";
                if (method == PrepMethod.Built) { optional = 2; optionalDone = true; }
            }
            PaintSteps(_stepRows, at, optional, optionalDone);
        }

        /// <summary>
        /// The card's ink: everything before the cursor is green and ticked, the cursor
        /// itself is amber, everything after it is dim. One optional row may be named — it
        /// is never the cursor and only ticks if it was actually done, because a step you
        /// may skip must not read as a step you are being blocked on.
        /// </summary>
        private void PaintSteps(List<(Image icon, Text label, Image tick)> rows,
            int at, int optional, bool optionalDone)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var (icon, label, tick) = rows[i];
                bool opt = i == optional;
                bool done = opt ? optionalDone : i < at;
                bool here = !opt && i == at;
                var ink = here ? UITheme.Amber[4] : done ? UITheme.Lime[3] : UITheme.TextSecondary;
                label.color = ink;
                icon.color = new Color(ink.r, ink.g, ink.b, here ? 1f : done ? 0.75f : 0.45f);
                tick.enabled = done;
                if (tick.enabled) tick.color = UITheme.Lime[3];
            }
        }

        /// <summary>
        /// THE LABELS STAND BESIDE THE COLUMN, NOT INSIDE IT (2026-08-14, the author's
        /// screenshot: "68% EMPTY" and "32% SMIRKOFF" running out of both sides of a 44-unit
        /// bar). A gauge this narrow can carry a colour and nothing else; the words go in the
        /// clear air next to it, each one level with the band it names, and the band keeps the
        /// hairline that ties them together. Nothing is clipped and nothing overlaps, at any
        /// mix, because the text was never asked to fit somewhere it cannot.
        ///
        /// One drawing for both benches: the tin's gauge and the glass's are the same object
        /// with different contents, and they were the same forty lines twice.
        /// </summary>
        private void FillGauge(RectTransform bar, GlassContents glass, TycoonRun run, bool labelsLeft)
        {
            foreach (Transform child in bar) Destroy(child.gameObject);
            float h = bar.rect.height, y = 0f;
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                float share = (float)(glass.RatioOf(id) * glass.FillFraction);   // of the VESSEL
                float segH = share * h;
                var seg = GaugeBand(bar, $"S_{id}", segH, y,
                    UITheme.LiquidColor(card?.Info?.Style, card?.Type ?? IngredientType.Spirit));
                GaugeLabel(seg, segH, labelsLeft, UITheme.TextPrimary,
                    $"{share:P0} {(card?.Name ?? id).ToUpperInvariant().Split(' ')[0]}");
                y += segH;
            }

            float free = Mathf.Max(0f, 1f - (float)glass.FillFraction);
            if (free <= 0.001f) return;
            var room = GaugeBand(bar, "S_empty", free * h, y, new Color(1f, 1f, 1f, 0.05f));
            GaugeLabel(room, free * h, labelsLeft, UITheme.TextSecondary, $"{free:P0} EMPTY");
        }

        private RectTransform GaugeBand(RectTransform bar, string name, float height, float y, Color fill)
        {
            var seg = NewRect(name, bar);
            seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(1, 0);
            seg.pivot = new Vector2(0.5f, 0);
            seg.sizeDelta = new Vector2(-2, height);
            seg.anchoredPosition = new Vector2(0, y);
            var img = seg.gameObject.AddComponent<Image>();
            img.color = fill;
            img.raycastTarget = false;
            return seg;
        }

        /// <summary>A band's caption, out in the air beside it, with a hairline back to it.
        /// Bands too thin to hold a line of type get no caption — a 2% splash is a colour.</summary>
        private void GaugeLabel(RectTransform seg, float segH, bool onLeft, Color ink, string text)
        {
            if (segH < 11f) return;
            float side = onLeft ? 0f : 1f;

            var tick = NewRect("Tick", seg);
            tick.anchorMin = tick.anchorMax = new Vector2(side, 0.5f);
            tick.pivot = new Vector2(onLeft ? 1f : 0f, 0.5f);
            tick.sizeDelta = new Vector2(8, 1);
            tick.anchoredPosition = new Vector2(onLeft ? -1f : 1f, 0f);
            var timg = tick.gameObject.AddComponent<Image>();
            timg.color = new Color(ink.r, ink.g, ink.b, 0.45f);
            timg.raycastTarget = false;

            var label = NewText("L", seg, _body, 8,
                onLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, ink);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(side, 0.5f);
            rt.pivot = new Vector2(onLeft ? 1f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(170, 12);
            rt.anchoredPosition = new Vector2(onLeft ? -11f : 11f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = text;
        }

        /// <summary>The 1px neon frame both pour gauges wear.</summary>
        private void GaugeEdge(RectTransform host, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                var r = NewRect("E" + i, host);
                if (i < 2)
                {
                    r.anchorMin = new Vector2(0, i); r.anchorMax = new Vector2(1, i);
                    r.pivot = new Vector2(0.5f, i);
                    r.sizeDelta = new Vector2(0, 1);
                }
                else
                {
                    float ax = i == 2 ? 0f : 1f;
                    r.anchorMin = new Vector2(ax, 0); r.anchorMax = new Vector2(ax, 1);
                    r.pivot = new Vector2(ax, 0.5f);
                    r.sizeDelta = new Vector2(1, 0);
                }
                r.anchoredPosition = Vector2.zero;
                var img = r.gameObject.AddComponent<Image>();
                img.color = c; img.raycastTarget = false;
            }
        }

        private string ShakerLine(TycoonRun run)
        {
            if (run.Glass.IsEmpty) return "shaker empty — tip the bottle over the tin";
            var sb = new StringBuilder();
            sb.Append($"SHAKER {run.Glass.FillFraction:P0} — ");
            var parts = new List<string>();
            foreach (var id in run.Glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                parts.Add($"{(card?.Name ?? id).ToUpperInvariant()} {run.Glass.RatioOf(id):P0}");
            }
            sb.Append(string.Join(", ", parts));
            return sb.ToString();
        }

        /// <summary>The readout's ordinary voice — and it clears any warning colour left on it.</summary>
        private void SayShaker(string line)
        {
            _shakerReadout.text = line;
            _shakerReadout.color = UITheme.TextSecondary;
            _saidThisFrame = true;
        }

        // ── being sent back ───────────────────────────────────────────────────────
        //
        // A door that turns you around has to say why, and say it where you land. The
        // readout is rewritten by every stage method each frame, so a line posted at the
        // moment of the turn would be gone before the stage had finished sliding in. This
        // holds it for a beat and claims the readout while it does.

        private string _benchDemandLine;
        private float _benchDemandT;

        /// <summary>Send the player to the bench to finish the tin, with the reason held on
        /// the readout long enough to be read.</summary>
        private void DemandBench(string line)
        {
            _benchDemandLine = line;
            _benchDemandT = 2.6f;
            GoTo(Stage.Shaker);
        }

        private void StepBenchDemand()
        {
            if (_benchDemandT <= 0f) return;
            _benchDemandT -= Time.unscaledDeltaTime;
            _shakerReadout.text = _benchDemandLine;
            _shakerReadout.color = UITheme.ViceRed[3];
            _saidThisFrame = true;
        }

        /// <summary>The tin is at the brim and is refusing things. Said in red, because it is the
        /// reason nothing is happening (2026-07-28).</summary>
        private void ShowShakerFull()
        {
            _shakerReadout.text = "THE TIN IS FULL — PUT THE LID ON AND SHAKE, OR EMPTY IT";
            _shakerReadout.color = UITheme.ViceRed[3];
            _saidThisFrame = true;
        }

        /// <summary>
        /// Whether the readout has already been given something to say this frame.
        ///
        /// The stage methods run in one order every frame and the LAST one wins the readout —
        /// which was UpdateCap, whose closing nudge fires whenever the tin holds anything and
        /// is not yet capped. That is most of the stage's life, so it silently stomped the
        /// live mix line, the red "THE TIN IS FULL" refusal and the fizz refusal, every frame,
        /// before any of them could be seen. A nudge is the thing you say when there is
        /// nothing else to say; this is what lets it be that.
        /// </summary>
        private bool _saidThisFrame;

        /// <summary>Said only if nothing louder was said this frame.</summary>
        private void NudgeShaker(string line)
        {
            if (_saidThisFrame) return;
            _shakerReadout.text = line;
            _shakerReadout.color = UITheme.TextSecondary;
        }

        /// <summary>The bottle in hand, drawn: open art, style tint fallback, sized and
        /// stood by its own drawing with the mouth measured off it (VesselArt).</summary>
        private void PushFocusBottleArt(TycoonRun run)
        {
            var colour = UITheme.StyleColor(_focusBottle.Info?.Style, _focusBottle.Type);
            _shakerTitle.text = _focusBottle.Name.ToUpperInvariant();
            // In the hand it stands OPEN (the author, 2026-08-01): the pour scene uses the
            // capless variant when one exists. Same canvas as the closed art, so the liquid
            // mask and the mouth line all stay put; styles missing an open shot fall back.
            var bottleSprite = ItemArt.BottleOpen(_focusBottle);
            _pourBottleBody.sprite = bottleSprite;
            _pourBottleBody.color = bottleSprite != null ? Color.white : colour;
            // It stands on the bench at the size its own drawing asks for, and it is measured
            // against its CLOSED art: an open bottle is the same bottle with the cap off, so
            // it must not grow to fill the space the cap left (VesselArt).
            _pourMouth = VesselArt.StandOn(_pourVessel, new Vector2(0.5f, 0f), bottleSprite,
                BottleH, Vector2.zero, ItemArt.Bottle(_focusBottle));
            PushPourFill(run);
        }

        // ── the shaker focus stage: the tilt-pour ────────────────────────────────

        private void RefreshShaker()
        {
            var run = Run;
            if (run == null || _focusBottle == null) return;
            PushFocusBottleArt(run);
            SayShaker(ShakerLine(run));
            _pourBottle.anchoredPosition = _bottleRest;
            _pourBottle.localRotation = Quaternion.identity;
            _shakerFluid.Clear();
            _shakerFluid.ClearStreamColor();      // a new visit pours nothing yet
            _shakerFluid.SetColor(DrinkColor(run.Glass));
            _shakerVessel.anchoredPosition = _shakerHome;
            _shakerVessel.localRotation = Quaternion.identity;
            _capped = false; _capGrabbed = false; _capT = 0f;
            _spoonHeld = false; _stirEnergy = 0; _stirHasPrev = false;
            _toGlassWasOn = false; _toGlassPulse = 0f;
            if (_spoonRt != null)
            {
                _spoonRt.anchoredPosition = _spoonRest;
                _spoonRt.localRotation = Quaternion.identity;
            }
            if (_shakerOpenSize != Vector2.zero) _shakerVessel.sizeDelta = _shakerOpenSize;
            _capPos = _capRest;
            if (_shakerTop != null) { _shakerTop.anchoredPosition = _capRest; _shakerTop.localRotation = Quaternion.identity; }
            foreach (var g in _benchProps) if (g != null) g.alpha = 1f;
            PushShakerPool(run, 0f);
            _mixBarSig = "!";                 // force a redraw on stage entry
            RefreshShakerMixBar(run);
            // Stage entry: the meter reports on nothing until a hand claims it, and a tin
            // that ARRIVED shaken says so once rather than standing an empty bar on the bar.
            _shakeMeterFill.fillAmount = 0f;
            if (run.Glass.HasPreparation("shaken"))
                ShowWorkMeter((float)run.ShakeEnergy, UITheme.Amber[3],
                              $"SHAKEN  {run.ShakeEnergy:P0}");
        }

        /// <summary>
        /// One frame of the tilt-pour. The bottle follows the mouse while grabbed; the
        /// higher it is lifted the further it leans toward the shaker (GDD 24 §2). Liquid
        /// runs from the mouth only when it is tilted over the shaker's opening.
        /// </summary>
        private void UpdateTiltPour(TycoonRun run)
        {
            if (Mouse.current == null || _focusBottle == null) return;

            // A grab already in flight must not survive the lid going on either.
            if (_capped) { _bottleGrabbed = false; return; }

            // Release when the button comes up, wherever the cursor is.
            if (_bottleGrabbed && !Mouse.current.leftButton.isPressed)
                _bottleGrabbed = false;

            bool pourNow = false;
            if (_bottleGrabbed &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 local))
            {
                // Keep the bottle on the surface.
                float halfW = _pourSurface.rect.width * 0.5f;
                float halfH = _pourSurface.rect.height * 0.5f;
                local.x = Mathf.Clamp(local.x, -halfW + 30f, halfW - 30f);
                local.y = Mathf.Clamp(local.y, -halfH + 20f, halfH - 20f);
                _pourBottle.anchoredPosition = local;

                float lift = Mathf.Clamp01((local.y - _bottleRest.y) / LiftRange);
                float tilt = lift * MaxTilt;                       // degrees, counter-clockwise = leans left
                _pourBottle.localRotation = Quaternion.Euler(0, 0, tilt);

                // Where the mouth ends up: the bottle's CAP, swung around its grip. It used to
                // be the top centre of the grab plate, which is the cap only for art that
                // fills its sheet — the juice cartons poured from a point some 80px above
                // their own spout (the author, 2026-08-11: "sıvının çıkış yerini kapak olarak
                // ayarla"). VesselArt reads it off the drawing instead.
                Vector2 mouth = local + VesselArt.Swing(_pourMouth, tilt);

                var opening = _shakerVessel.anchoredPosition + new Vector2(0, _shakerVessel.rect.height * 0.5f);
                bool over = Mathf.Abs(mouth.x - opening.x) < 78f && mouth.y > opening.y - 30f;
                // A full tin takes nothing more, so the stream stops with it: liquid pouring into
                // a glass that cannot accept it read as an overflow the rules do not have
                // (GDD 21 §3, 2026-07-28). The bottle stays in hand — only the pour ends.
                bool full = run.Glass.IsFull;
                // THE FIZZ GUARD IS GONE (2026-08-14, the author: "soda shakera dökülmüyor").
                // It stood here to echo Core's old refusal — and outlived it by a day, which
                // is exactly the failure its own comment warned about, pointed the other way:
                // a guard kept for safety after the rule it guards has been overturned is a
                // rule of its own that nobody wrote down. Core takes fizz in the tin now, so
                // the hand pours it.
                pourNow = tilt > 42f && over && !full;
                if (full && tilt > 42f && over) ShowShakerFull();

                if (pourNow)
                {
                    // A stream of merging droplets falls from the mouth toward the opening; the
                    // metaball field fuses them into one liquid column and melts them into the
                    // pool where they land (GDD 24 §3.5).
                    // The LIQUID's colour, on the STREAM: StyleColor is the shelf tag's identity
                    // hue (amaro navy, gin green) and pouring with it drew a drink no bottle
                    // contains, which then snapped to the true colour on the next refresh.
                    _shakerFluid.SetStreamColor(
                        UITheme.LiquidColor(_focusBottle.Info?.Style, _focusBottle.Type));
                    var streamVel = new Vector2((opening.x - mouth.x) * 1.8f, -225f);
                    _shakerFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow)
            {
                if (run.PouringId == null) run.BeginPour(_focusBottle.Id);
                run.PourTick(Time.deltaTime * PourTimeScale);   // slower, deliberate pour
                // The tin's own colour, every frame it changes. RefreshShaker sets this once on
                // the way in, and on the way in the tin is EMPTY — so without this the body kept
                // DrinkColor's empty-glass cream while the stream poured pink into it (the
                // author's screenshot, 2026-08-03: a tin the gauge called 80% House Syrup drawn
                // the colour of nothing). The serve stage has always had the twin of this line.
                _shakerFluid.SetColor(DrinkColor(run.Glass));
                SayShaker(ShakerLine(run));
            }
            else if (run.PouringId != null)
            {
                run.EndPour();
                // Whatever is still falling belongs to the tin now, not to the next bottle
                // the player picks up.
                _shakerFluid.ClearStreamColor();
            }

            if (pourNow) _shakerLoopWanted = "pour_loop";   // the stage frame drives the source
            if (pourNow) RefreshShakerMixBar(run);          // the gauge follows the stream
            _pouring = pourNow;

            // Every frame, not only the pouring ones: the bottle in hand is the same bottle
            // that stands on the rail, and it drains while you hold it over the tin. Setting
            // it once on the way in would show the level it had when you picked it up.
            PushPourFill(run);
        }

        /// <summary>How full the bottle in hand is, read off the shelf it came from.</summary>
        private void PushPourFill(TycoonRun run)
        {
            if (_pourFill == null) return;
            if (_focusBottle == null) { _pourFill.Hide(); return; }
            var stock = run?.Shelf?.Find(_focusBottle.Id);
            _pourFill.Show(_pourBottleBody.sprite,
                UITheme.LiquidColor(_focusBottle.Info?.Style, _focusBottle.Type),
                stock != null && stock.Capacity > 0 ? stock.Remaining / stock.Capacity : 0.0);
        }

        /// <summary>
        /// Places the drink and steps it, once every vessel has finished moving for the frame.
        /// It used to run inside the tilt-pour, which is BEFORE the cap animation eases the tin
        /// across the bench and grows it — so the liquid was placed against last frame's tin and
        /// trailed it visibly wherever it moved (2026-07-28). It also sat behind that method's
        /// early return, which meant a stage with no mouse present simply froze the drink.
        /// </summary>
        private void StepShakerFluid(TycoonRun run)
        {
            // A gentle vertical heave on the pool top; the height-field carries the real waves.
            float energy = _shaking ? 1f + 3f * (float)_shakeEnergy : (_pouring ? 1.2f : 0.3f);
            _slosh += Time.deltaTime * (4f + 6f * energy);
            PushShakerPool(run, Mathf.Sin(_slosh) * 1.0f * energy);

            _shakerFluid.Step(Time.deltaTime);
            _shakerSolids.Step(Time.deltaTime);

            // The shadows, once everything has finished moving for the frame — the same
            // reason the drink is placed here rather than in the tilt-pour.
            // The tin never leaves the bench (capping only slides and grows it); the bottle
            // fades out with the rest of the bench props, so its shadow goes with it. Each
            // holds ITS OWN foot line: the two stand at different depths on the counter, and
            // one shared line put the bottle's shadow seventy pixels under its base.
            PushPropShadow(_tinShadow, _shakerVessel, _shakerHome.y, TinFootY, 158f, 1f);
            PushPropShadow(_bottleShadow, _pourBottle, _bottleRest.y, BottleFootY, 128f, 1f - _capT);
        }

        /// <summary>Where each bench prop's base sits when it is standing: the tin's rect is
        /// centre-pivoted, the bottle's is gripped low at 0.22 of its height.</summary>
        private float TinFootY => _shakerHome.y - 358f * 0.5f + 14f;
        private float BottleFootY => _bottleRest.y - BottleH * 0.22f + 6f;

        /// <summary>Keeps one contact shadow under its prop: it holds that prop's own foot
        /// line, follows its x, and shrinks and fades as the prop is lifted off it. The
        /// <paramref name="alpha"/> is the prop's own visibility, so a faded bench prop
        /// does not leave a shadow standing on the counter without it.</summary>
        private void PushPropShadow(RectTransform shadow, RectTransform prop, float restY,
                                    float floorY, float width, float alpha)
        {
            if (shadow == null || prop == null) return;
            float lift = Mathf.Max(0f, prop.anchoredPosition.y - restY);
            float k = Mathf.Clamp01(1f - lift / 200f);
            shadow.anchoredPosition = new Vector2(prop.anchoredPosition.x, floorY);
            float w = width * (0.62f + 0.38f * k);
            shadow.sizeDelta = new Vector2(w, Mathf.Max(10f, w * 0.22f));
            var img = shadow.GetComponent<Image>();
            if (img != null && img.sprite != null)
                img.color = new Color(0f, 0f, 0f, 0.55f * k * Mathf.Clamp01(alpha));
        }

        /// <summary>Places the shaker's pooled liquid from the glass interior and its live fill,
        /// plus a vertical slosh <paramref name="bob"/> on the surface (all surface-local px).</summary>
        private void PushShakerPool(TycoonRun run, float bob)
        {
            if (run.Glass.IsEmpty) { _shakerFluid.ClearPool(); return; }
            // Read the vessel live so the pool travels with the shaker when it is thrown about.
            // Fill the glass INTERIOR (inset from the walls) so the liquid pools inside the
            // clear shaker instead of a box around it (2026-07-23).
            var c = _shakerVessel.anchoredPosition;
            float halfW = _shakerVessel.rect.width * 0.5f;
            float iw = halfW * 0.50f;   // measured: the tin's cavity is 50% of the sprite width
            float h = _shakerVessel.rect.height;
            float innerH = h * (CavityRim - CavityFloor);   // measured: that floor → rim

            // The cavity's centre sits well BELOW the tin's own pivot — the drinkable part runs
            // from 0.09 to 0.61 of the sprite, so its middle is about a seventh of the height
            // down from the middle of the art. The sprite turns about its pivot and the pool
            // turns about its own centre, so unless that centre is carried round the pivot by
            // hand the two swing apart the moment the tin leans: at the 24° a shake reaches,
            // by nearly twenty pixels — the liquid visibly leaving the steel (2026-07-28).
            // The tap already does this for the leaning pint; the shaker never did.
            float rad = _shakerVessel.localEulerAngles.z * Mathf.Deg2Rad;
            if (rad > Mathf.PI) rad -= 2f * Mathf.PI;
            var centre = RotateAbout(new Vector2(c.x, c.y - h * 0.5f + h * CavityFloor + innerH * 0.5f), c, rad);
            float minX = centre.x - iw;
            float maxX = centre.x + iw;
            float bottomY = centre.y - innerH * 0.5f;   // measured: above the rounded base
            // A full tin draws full. The ninth this used to shave off was a fudge for the
            // solver's particle-count estimate, and it made a glass the rules called 100% read
            // as nine-tenths — the one number the player checks against the vessel (2026-07-28).
            // The estimate is fixed where it belongs now, in the solver itself; measured after:
            // a tin the rules call 100% draws to 100% of its cavity.
            float fill = (float)run.Glass.FillFraction;
            float rimY = bottomY + innerH;
            float topY = bottomY + innerH * fill + bob;
            // The particle fluid collides with the tin's rotated interior, so it sloshes with it.
            _shakerFluid.SetPool(minX, maxX, bottomY, rimY, fill, rad);
            // The cap's placement belongs to UpdateCap now — it rests on the bench until
            // you drop it on the tin, so it must not be glued to the vessel here.
            // The solids float on the liquid line and bounce off these same walls.
            _shakerSolids.SetBounds(minX, maxX, bottomY, topY);
        }

        /// <summary>
        /// The cap (2026-07-24). While the tin is open you build the drink in it; drag the lid
        /// over its mouth and it snaps on. Capping hands the stage over to shaking: the bottle,
        /// the spoon and the rail fade away and the tin eases into the middle and grows, so
        /// nothing is left on the bench but the thing you are about to shake.
        /// </summary>
        private void UpdateCap(TycoonRun run)
        {
            if (_shakerTop == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var mouse = Mouse.current;

            if (_capGrabbed)
            {
                // The cap's art lives in the top of its canvas, so centre THAT on the cursor —
                // grabbing it used to pin the mouse to the empty space beneath the lid.
                float lift = _shakerTop.rect.height * CapArtOffset;
                if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _pourSurface, mouse.position.ReadValue(), null, out Vector2 local))
                    _capPos = Vector2.Lerp(_capPos, local - new Vector2(0, lift), 1f - Mathf.Exp(-30f * dt));
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _capGrabbed = false;
                    // Anywhere over the tin will do — you should not have to thread the mouth.
                    var tin = _shakerVessel;
                    var d = _capPos + new Vector2(0, lift) - tin.anchoredPosition;
                    bool onTin = Mathf.Abs(d.x) < tin.rect.width * 0.75f
                              && Mathf.Abs(d.y) < tin.rect.height * 0.75f;
                    if (onTin && !run.Glass.IsEmpty) { _capped = true; Sfx.Play("glass_down"); }
                    else _capPos = _capRest;
                }
            }

            _capT = Mathf.MoveTowards(_capT, _capped ? 1f : 0f, dt / 0.45f);
            float e = _capT * _capT * (3f - 2f * _capT);   // smoothstep

            if (!_shaking)
                _shakerVessel.anchoredPosition = Vector2.Lerp(
                    _shakerVessel.anchoredPosition,
                    Vector2.Lerp(_shakerHome, new Vector2(CapCentreX, _shakerHome.y), e),
                    1f - Mathf.Exp(-9f * dt));
            _shakerVessel.sizeDelta = Vector2.Lerp(_shakerOpenSize, _shakerOpenSize * CapGrowth, e);

            foreach (var g in _benchProps) if (g != null) g.alpha = 1f - e;

            if (_capT > 0f)
            {
                _shakerTop.sizeDelta = _shakerVessel.sizeDelta;
                _shakerTop.anchoredPosition = Vector2.Lerp(_capPos, _shakerVessel.anchoredPosition, e);
                _shakerTop.localRotation = _shakerVessel.localRotation;
            }
            else
            {
                _shakerTop.sizeDelta = _shakerOpenSize;
                _shakerTop.anchoredPosition = _capPos;
            }
            _shakerTop.SetAsLastSibling();
            var capImg = _shakerTop.GetComponent<Image>();
            if (capImg != null) capImg.raycastTarget = !_capped;   // capped: grab the tin, not the lid

            // THE LID COMES OFF AGAIN (2026-08-14, the author: "kapağı kapatıldıktan sonra
            // isterse kapağı çıkarabilecek karıştırmayı unutursa diye"). Its own key rather
            // than a drag: the capped tin is grabbed to SHAKE it, so a grabbable lid resting
            // on top would steal the gesture the stage exists for. It shows only while the
            // lid is on and the tin is still.
            if (_lidOffKey != null)
            {
                bool offer = _capped && !_shaking && !run.Glass.IsEmpty;
                if (_lidOffGroup != null)
                {
                    _lidOffGroup.alpha = Mathf.MoveTowards(_lidOffGroup.alpha, offer ? 1f : 0f, dt / 0.18f);
                    _lidOffGroup.blocksRaycasts = offer;
                }
                _lidOffKey.interactable = offer;
            }

            if (!_capped && !run.Glass.IsEmpty && !_capGrabbed && !_spoonHeld)
            {
                // The method the tin itself asks for speaks BEFORE the lid does: a drink
                // with a decision to make (spoon or lid) is steered past the spoon by
                // "close it" alone. Built says so out loud now that the highballs come
                // through the tin — "cap it and take it over" is an instruction, not a
                // silence.
                switch (run.TinMethod)
                {
                    case PrepMethod.Stirred:
                        NudgeShaker("this one is STIRRED — work the spoon over the open tin");
                        break;
                    case PrepMethod.Shaken:
                        NudgeShaker("this one is SHAKEN — cap the tin, then shake it");
                        break;
                    case PrepMethod.Built:
                        NudgeShaker("this one is BUILT — no shaking; cap it and take it over");
                        break;
                    default:
                        if (run.MixRequired && !run.IsMixed)
                            NudgeShaker("two spirits — stir it with the spoon, or cap it and shake");
                        else
                            NudgeShaker("drag the lid onto the tin to close it");
                        break;
                }
            }
        }

        // ── the tin bursts ────────────────────────────────────────────────────────
        //
        // Shaking fizz (2026-08-14, the author: "gazlı içecekler çalkalandığında patlayabilir
        // shaker boşalsın ve patlama animasyonu olsun ardından sanki çöpe atılmış gibi
        // tekrardan en baştan başlayabilir"). Core has already emptied the tin and written
        // the goods off by the time this runs — this is the bang, and the walk back to a
        // clean bench. The lid is thrown, the drink is thrown, the room shakes, and the
        // readout says what happened for as long as it takes to read it.

        private float _blowT;                 // counts down while the bang plays
        private Vector2 _blowLidVel;
        private Vector2 _blowHome;            // where the tin stood when it went off
        private float _blowLidSpin;
        private const float BlowHold = 1.9f;

        private void BlowTheTin()
        {
            _blowT = BlowHold;
            _shaking = false;
            _shakeEnergy = 0;

            // The drink leaves the tin as a burst of its own colour, thrown up and out.
            var at = _shakerVessel.anchoredPosition;
            for (int i = 0; i < 7; i++)
                _shakerFluid.Splash(at + new Vector2(UnityEngine.Random.Range(-40f, 40f), 20f), 1f);
            _shakerFluid.ClearPool();

            // The lid goes with it, and keeps going until the bench is rebuilt.
            _capped = false;
            _capGrabbed = false;
            _blowLidVel = new Vector2(UnityEngine.Random.Range(-260f, 260f), 520f);
            _blowLidSpin = UnityEngine.Random.Range(-620f, 620f);

            _blowHome = _shakerVessel.anchoredPosition;
            Sfx.Play("bottle_open", 1f);
            Sfx.Play("upset_sfx", 0.6f);
            _shakerReadout.text = "IT BLEW UP — NEVER SHAKE A FIZZY DRINK";
            _shakerReadout.color = UITheme.ViceRed[3];
            _saidThisFrame = true;
        }

        /// <summary>One frame of the bang: the lid flies, and when it is over the bench is
        /// rebuilt from a Core that is already empty — which is the loop back to the start.</summary>
        private void StepBlowout()
        {
            if (_blowT <= 0f) return;
            float dt = Time.unscaledDeltaTime;
            _blowT -= dt;
            if (_shakerTop != null)
            {
                _blowLidVel.y -= 1400f * dt;
                _shakerTop.anchoredPosition += _blowLidVel * dt;
                _shakerTop.localRotation *= Quaternion.Euler(0, 0, _blowLidSpin * dt);
            }
            // The tin rings with it — a decaying rattle around where it stood, so the bang
            // is felt on the object that made it rather than announced by a line of text.
            if (_shakerVessel != null)
            {
                float ring = Mathf.Max(0f, _blowT - (BlowHold - 0.45f)) / 0.45f;
                _shakerVessel.anchoredPosition = _blowHome + new Vector2(
                    UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * (14f * ring);
            }
            _shakerReadout.text = "IT BLEW UP — NEVER SHAKE A FIZZY DRINK";
            _shakerReadout.color = UITheme.ViceRed[3];
            _saidThisFrame = true;
            if (_blowT <= 0f) RefreshShaker();   // a clean bench, exactly as after the bin
        }

        /// <summary>
        /// Take the lid back off. The bench returns to the state it was in before the cap —
        /// the props come back, the tin walks home — and nothing about the DRINK changes: a
        /// shake that already happened stays in the glass's preparations, because it did
        /// happen. This is the way back from a lid closed before the spoon was picked up.
        /// </summary>
        private void UncapTin()
        {
            if (!_capped || _shaking) return;
            _capped = false;
            _capGrabbed = false;
            _capPos = _capRest;
            Sfx.Play("bottle_open", 0.7f);
            SayShaker("lid off — the tin is open again");
        }

        /// <summary>
        /// The stir (GDD 21 §14): pick the spoon up while the tin is OPEN and work circles
        /// over its mouth. Energy is the swept ANGLE around the tin's centre — a straight
        /// rattle sweeps nothing, so the shake's gesture cannot fake a stir. Release with
        /// anything behind it and the stir commits at that thoroughness.
        /// </summary>
        private void UpdateStir(TycoonRun run)
        {
            if (_spoonRt == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            if (!_spoonHeld)
            {
                _spoonRt.anchoredPosition = Vector2.Lerp(
                    _spoonRt.anchoredPosition, _spoonRest, 1f - Mathf.Exp(-12f * dt));
                _spoonRt.localRotation = Quaternion.Lerp(
                    _spoonRt.localRotation, Quaternion.identity, 1f - Mathf.Exp(-12f * dt));
                return;
            }

            // Capping mid-stir puts the spoon down: the two verbs never share a tin state.
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || _capped)
            {
                if (!_capped && !run.Glass.IsEmpty && _stirEnergy > 0.05)
                {
                    run.Stir(_stirEnergy);
                    SayShaker($"STIRRED · {_stirEnergy:P0} · {ShakerLine(run)}");
                }
                _spoonHeld = false;
                _stirEnergy = 0;
                _stirHasPrev = false;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, mouse.position.ReadValue(), null, out Vector2 local))
                return;

            // 30 -> 60: the spoon is a light thing held in the fingers, so it is the one
            // that should sit nearest the cursor of the three.
            _spoonRt.anchoredPosition = Vector2.Lerp(
                _spoonRt.anchoredPosition, local, 1f - Mathf.Exp(-60f * dt));

            // The swept angle, taken about the tin's centre and only while the spoon is
            // actually over the tin — circling the bench does not stir the drink.
            var tin = _shakerVessel;
            Vector2 arm = local - tin.anchoredPosition;
            bool overTin = Mathf.Abs(arm.x) < tin.rect.width * 0.9f
                        && Mathf.Abs(arm.y) < tin.rect.height * 0.9f
                        && arm.magnitude > 8f;
            if (overTin)
            {
                float angle = Mathf.Atan2(arm.y, arm.x);
                if (_stirHasPrev)
                {
                    float swept = Mathf.Abs(Mathf.DeltaAngle(
                        _stirPrevAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    _stirEnergy = Mathf.Clamp01((float)_stirEnergy + swept / StirFullRadians);
                    if (swept > 0.01f) _shakerLoopWanted = "stir_loop";
                }
                _stirPrevAngle = angle;
                _stirHasPrev = true;
                // the spoon leans into the work, the way the drag pieces swing
                _spoonRt.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Sin(Time.unscaledTime * 9f) * 9f);
            }
            else _stirHasPrev = false;

            ShowWorkMeter((float)_stirEnergy, UITheme.Cyan[3],
                          $"STIR  {_stirEnergy:P0}");
            NudgeShaker(overTin ? "work circles over the tin" : "bring the spoon over the tin");
        }

        /// <summary>
        /// The right-edge key out of the bench: lit only when the tin is capped and Core
        /// itself would let the drink leave (<see cref="TycoonRun.CanPourOut"/>). It pulses
        /// once the moment it first comes alive, so the way forward announces itself.
        /// </summary>
        private void UpdateToGlass(TycoonRun run)
        {
            if (_toGlassBtn == null) return;
            bool on = _capped && !run.Glass.IsEmpty && run.CanPourOut;
            _toGlassBtn.interactable = on;
            if (_toGlassGroup != null)
                _toGlassGroup.alpha = on ? 1f : 0.4f;
            if (on && !_toGlassWasOn) _toGlassPulse = 1f;
            _toGlassWasOn = on;
            if (_toGlassPulse > 0f)
            {
                _toGlassPulse = Mathf.MoveTowards(_toGlassPulse, 0f, Time.unscaledDeltaTime / 0.35f);
                float k = 1f + 0.10f * Mathf.Sin(_toGlassPulse * Mathf.PI);
                ((RectTransform)_toGlassBtn.transform).localScale = new Vector3(k, k, 1f);
            }
            if (_capped && !run.Glass.IsEmpty && !run.CanPourOut)
                NudgeShaker("it wants a mix — shake it, or bin it and start again");
        }

        /// <summary>
        /// The mouse-energy shake (GDD 24 §2.5): while the pad is held, cursor travel builds
        /// the shake energy and the shaker jitters; releasing applies the shake at whatever
        /// energy was reached.
        /// </summary>
        private void UpdateShake(TycoonRun run)
        {
            if (_shaking) _shakerLoopWanted = "shake_loop";
            if (!_shaking) return;
            var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            {
                // Released: commit the shake if there's a drink and any energy behind it.
                if (!run.Glass.IsEmpty && _shakeEnergy > 0.05)
                {
                    // Asked BEFORE the shake, because after it the tin is empty and there is
                    // nothing left to ask (Core's own note on ShakeBlowsTheTin).
                    bool blows = run.ShakeBlowsTheTin;
                    run.Shake(_shakeEnergy);
                    if (blows) BlowTheTin();
                    else SayShaker($"SHAKEN · {_shakeEnergy:P0} · {ShakerLine(run)}");
                }
                _shaking = false;
                _shakeEnergy = 0;
                _shakerVessel.localRotation = Quaternion.identity;
                // Leave the shaker wherever it was set down — no teleport home (2026-07-22).
                _shakerVel = Vector2.zero;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // Cursor travel builds the shake energy.
            float travel = (mouse - _lastShakeMouse).magnitude;
            _lastShakeMouse = mouse;
            _shakeEnergy = Mathf.Clamp01((float)_shakeEnergy + travel / ShakeFullTravel);

            // The shaker springs loosely after the cursor and overshoots — throw it around.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, mouse, null, out Vector2 local))
            {
                _shakerVel += (local - _shakerVessel.anchoredPosition) * (ShakeStiffness * dt);
                _shakerVel *= Mathf.Exp(-ShakeDamping * dt);
                _shakerVessel.anchoredPosition += _shakerVel * dt;
                _shakerVessel.localRotation =
                    Quaternion.Euler(0, 0, Mathf.Clamp(-_shakerVel.x * 0.02f, -24f, 24f));

                // The slosh comes from the fluid feeling the tin's acceleration (MetaballFluid
                // reads the vessel's motion itself). The old Disturb/Ripple pokes that used to
                // fake it are gone: they injected a one-way velocity into every particle on
                // every frame, on top of the real inertia, and that compounded — the drink was
                // driven into the wall and packed tighter and tighter until a full tin read as
                // a puddle (measured: 100% -> 35% of its area over 16s of shaking). Ripple was
                // also being handed a surface-space x while it now expects the tin's own frame.
            }

            ShowWorkMeter((float)_shakeEnergy, UITheme.Amber[3],
                          $"SHAKE  {_shakeEnergy:P0}");
        }

        /// <summary>
        /// The bar's own wall, hung behind a bench from <paramref name="fromY"/> (a
        /// fraction of the panel) to the top. Both service benches wear it, so the two
        /// halves of building one drink are shot on one set — the same argument that put
        /// the prep table on both of them in 2026-08-04, now that the furniture between
        /// them is gone.
        /// </summary>
        /// <summary>
        /// The soft ellipse that PINS A THING TO THE COUNTER (BackBarArt, the same one the
        /// back bar wall's bottles stand on). With the mat and the prep table gone the
        /// counter is a plain field, and a prop drawn on a plain field is a prop floating
        /// over it — the contact shadow is what the drawn surfaces were doing for free.
        /// Added as the first child of its parent so whatever stands on it draws over it.
        /// </summary>
        private RectTransform AddContactShadow(RectTransform parent, float width, Vector2 at)
        {
            var rt = NewRect("Shadow", parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, Mathf.Max(10f, width * 0.22f));
            rt.anchoredPosition = at;
            rt.SetAsFirstSibling();
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = BackBarArt.BottleShadow();
            img.raycastTarget = false;
            img.color = new Color(0f, 0f, 0f, img.sprite != null ? 0.55f : 0f);
            return rt;
        }

        /// <summary>
        /// The bar top the bench's props stand on. THE PAINTED WALL THAT USED TO COME WITH IT
        /// IS GONE (2026-08-22): the bench opens onto the room now, so the wall behind it is
        /// the room's own. The COUNTER stayed, and deliberately — see below, it is what keeps
        /// a black contact shadow from being drawn on black.
        /// </summary>
        /// <summary>Every bar top drawn on a bench, so AlignBenchCounters can put them all
        /// on the room's own counter line when a stage opens.</summary>
        private readonly List<RectTransform> _benchCounters = new List<RectTransform>();

        /// <summary>
        /// Puts every bench's bar top on the line the room's counter is actually on. Called
        /// when a stage opens rather than baked at build: the shaker and the glass are always
        /// entered with the cellar open, the DRAUGHT station usually is not, and the counter
        /// sits 121 px apart between those two states.
        /// </summary>
        private void AlignBenchCounters()
        {
            var room = GetComponent<TycoonHud>()?.Room;
            if (room == null) return;
            float fromY = room.BenchSurfaceFraction;
            foreach (var band in _benchCounters)
            {
                if (band == null) continue;
                band.anchorMin = Vector2.zero;
                band.anchorMax = new Vector2(1f, fromY);
                band.offsetMin = Vector2.zero;
                band.offsetMax = Vector2.zero;
            }
            // ...and the wall stands ON that line (2026-08-26). It is hung from the bar top
            // rather than stretched between two edges, so the counter can move between the
            // open and shut cellar without the backdrop changing its pixel size.
            if (_benchWall != null)
            {
                _benchWall.anchorMin = new Vector2(0f, fromY);
                _benchWall.anchorMax = new Vector2(1f, fromY);
                _benchWall.offsetMin = new Vector2(0f, _benchWall.offsetMin.y);
                _benchWall.anchoredPosition = new Vector2(0f, 0f);
                _benchWall.sizeDelta = new Vector2(0f, _benchWall.sizeDelta.y);
            }
        }

        /// <summary>The room's counter, zoomed (2026-08-25, the author: "ekran çok boş
        /// gözüküyor, mevcut tezgahın görseline zoom yapılmış gibi gözükmeli"). Every
        /// colour below is SAMPLED from Assets/Art/Backgrounds/counter.png — the slab, its
        /// ridge, and the magenta neon rail that runs the counter's far edge — so the bench
        /// is the same object the room draws, four times closer. Bands, not a texture:
        /// chrome is procedural (14 §3), and a zoomed pixel surface IS flat runs of colour.</summary>
        private static readonly Color BenchSlab = Hex(0x1F1924);
        private static readonly Color BenchSlabSheen = Hex(0x292630);
        private static readonly Color BenchRidge = Hex(0x312E3A);
        private static readonly Color BenchSeam = Hex(0x17141C);
        private static readonly Color[] BenchRail =
            { Hex(0xD77BBA), Hex(0xB7699F), Hex(0x975885), Hex(0x77476B), Hex(0x573650), Hex(0x372536) };

        private static Color Hex(int v) =>
            new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);

        private void AddBenchCounter(RectTransform panel, float fromY)
        {
            // It goes in BEHIND EVERYTHING on the panel: a band added after the title is a
            // band drawn over the title, and the panel's own background is a component, so
            // first CHILD is as far back as a child can go.
            var top = NewRect("CounterTop", panel);
            _benchCounters.Add(top);
            Stretch(top, Vector2.zero, new Vector2(1f, fromY), Vector2.zero, Vector2.zero);
            top.SetAsFirstSibling();
            var timg = top.gameObject.AddComponent<Image>();
            timg.color = BenchSlab;
            timg.raycastTarget = false;

            // The counter's FAR EDGE, zoomed: the ridge that catches the room, the seam,
            // and the neon rail the room's own counter wears — the six sampled rows drawn
            // at 5 units each, which is the x4-and-a-bit the whole bench stands at.
            float y = 0f;
            void Band(string name, float h, Color c)
            {
                var band = NewRect(name, top);
                band.anchorMin = new Vector2(0f, 1f); band.anchorMax = Vector2.one;
                band.pivot = new Vector2(0.5f, 1f);
                band.offsetMin = new Vector2(0, -y - h);
                band.offsetMax = new Vector2(0, -y);
                var img = band.gameObject.AddComponent<Image>();
                img.color = c; img.raycastTarget = false;
                y += h;
            }
            Band("Ridge", 8f, BenchRidge);
            Band("Seam", 5f, BenchSeam);
            for (int i = 0; i < BenchRail.Length; i++)
                Band("Rail" + i, 5f, BenchRail[i]);
            Band("Seam2", 5f, BenchSeam);

            // And the slab keeps one soft sheen band a hand's width in — the zoom of the
            // sheen the room's slab carries — so the big field reads as a surface and not
            // as a fill.
            var sheen = NewRect("Sheen", top);
            sheen.anchorMin = new Vector2(0f, 1f); sheen.anchorMax = Vector2.one;
            sheen.pivot = new Vector2(0.5f, 1f);
            sheen.offsetMin = new Vector2(0, -y - 74f);
            sheen.offsetMax = new Vector2(0, -y - 62f);
            var simg = sheen.gameObject.AddComponent<Image>();
            simg.color = BenchSlabSheen;
            simg.raycastTarget = false;
        }

        private void BuildShakerPanel()
        {
            _benchProps.Clear();

            // The whole screen (P14 v2, the serve stage's recipe): the stage is the counter
            // you are standing at, not a dialog floating on it.
            _shakerPanel = NewRect("ShakerPanel", _field);
            Stretch(_shakerPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // NO DARKENING (2026-08-22, the author: "Shaker ve pour sahnelerinde karartma
            // olmasın"). This was an opaque page, then a scrim over the room; it is neither
            // now. The room behind the bench is the bar you are standing in and it keeps its
            // own light. The plate is still HERE and still raycasts — that is the whole job
            // it has left: you can see past the bench, you cannot reach past it.
            var block = _shakerPanel.gameObject.AddComponent<Image>();
            block.color = new Color(0f, 0f, 0f, 0f);
            Swallow(_shakerPanel);

            // 16, not 18: the pixel faces only rasterise cleanly at whole multiples of their
            // 8px design size (CLAUDE.md), and the serve stage's twin title is 16 in
            // PrimaryAction.
            // THE NAME IS ON THE BAR (2026-08-26, the author: "alkolün ismini de tezgahın
            // üstüne göm"). It used to hang at the top of the field in gold, over the room's
            // own fascia — a caption floating on nothing, and the loudest thing on a screen
            // whose subject is a tin. It sits on the counter's back edge now, on a plate cut
            // into the bar, where a bartender's own name rail would be: below the wall, above
            // the slab, centred on the work.
            var namePlate = NewRect("NamePlate", _shakerPanel);
            Place(namePlate, new Vector2(1f, 0f), new Vector2(330, 30), new Vector2(-40, 236));
            var npImg = namePlate.gameObject.AddComponent<Image>();
            npImg.sprite = ChromeArt.Card();
            npImg.type = Image.Type.Sliced;
            npImg.color = UITheme.Night[0];
            npImg.raycastTarget = false;
            _shakerTitle = NewText("Title", namePlate, _display, 8, TextAnchor.MiddleCenter,
                                   UITheme.Amber[4]);
            Stretch(_shakerTitle.rectTransform, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);
            _shakerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            _shakerTitle.raycastTarget = false;

            // The standing line under the title. It named both methods as a menu of two,
            // which stopped being true when the recipe started naming one (2026-08-14) —
            // UpdateStepCard rewrites it with the method the tin actually asks for.
            _shakerHint = NewText("Hint", _shakerPanel, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            // Under the fascia (2026-08-26): the flow draws OVER the room's instruments.
            Stretch(_shakerHint.rectTransform, new Vector2(0, 1), Vector2.one,
                    new Vector2(0, -BenchTopClear - 12f), new Vector2(0, -BenchTopClear));
            _shakerHint.text = ShakerHintFor(null);

            // NO PAINTED WALL ANY MORE. It was here because "a bench standing in a void
            // reads as a diagram of a bench" — and the void is gone: the real room stands
            // behind the scrim, which is the corner of the bar this wall was imitating. The
            // COUNTER band stays: the props' contact shadows are black, and black on the
            // scrimmed room reads as cut-out-and-pasted exactly as it did on the old panel.

            // The play surface — a COORDINATE SPACE, not a thing you can see: where the
            // tin, the bottle and the spoon are placed and where the pointer is read. The
            // faint slab it used to wear is gone with the mat, for the same reason (below).
            _pourSurface = NewRect("PourSurface", _shakerPanel);
            Stretch(_pourSurface, Vector2.zero, Vector2.one, new Vector2(16, StageBottom), new Vector2(-16, -StageTop));

            // NO MAT, NO TABLE, NO SLAB (2026-08-13, the author: the panel IS the counter,
            // on this bench as on the glass one). The bar mat was a picture of a surface
            // laid over the surface — a lit rectangle with the wall behind it and the real
            // counter all around it, which read as a tray the bench had been put down on.
            // The tin and the bottle stand on the counter itself now.

            // The shaker vessel: a tapered tin, opening at the top, left of centre. Grab it to
            // shake — it becomes the toy you throw around.
            _shakerHome = new Vector2(-210, -44);
            _bottleRest = new Vector2(330, -70);   // the bottle's own rest, needed by its foot line
            // The two contact shadows, built BEFORE the props so they draw under them.
            // Each is placed on its own prop's foot line every frame (PushPropShadow).
            _tinShadow = AddContactShadow(_pourSurface, 158f, new Vector2(_shakerHome.x, TinFootY));
            _bottleShadow = AddContactShadow(_pourSurface, 128f, new Vector2(_bottleRest.x, BottleFootY));
            _shakerVessel = NewRect("Shaker", _pourSurface);
            Place(_shakerVessel, new Vector2(0.5f, 0.5f), new Vector2(200, 358), _shakerHome);
            var shakerImg = _shakerVessel.gameObject.AddComponent<Image>();
            // The real steel shaker (2026-07-23). It sits in front of the fluid so the metal
            // reads solid — the falling stream shows above the mouth then vanishes into the tin.
            var tinSprite = ItemArt.Load("tin_open") ?? ItemArt.Shaker;
            if (tinSprite != null) { shakerImg.sprite = tinSprite; shakerImg.preserveAspect = true; shakerImg.color = Color.white; }
            else
            {
                shakerImg.color = UITheme.Cream[2];
                var tin = NewRect("Tin", _shakerVessel);
                Stretch(tin, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -22));
                tin.gameObject.AddComponent<Image>().color = UITheme.Night[3];
                var lip = NewRect("Lip", _shakerVessel);
                Place(lip, new Vector2(0.5f, 1), new Vector2(128, 16), new Vector2(0, 0));
                lip.gameObject.AddComponent<Image>().color = UITheme.Cream[3];
            }

            // Grabbing the shaker (once it holds a drink) starts a free, loose shake.
            var shakeGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            shakeGrab.callback.AddListener(_ =>
            {
                if (Run == null || Run.Glass.IsEmpty) { SayShaker("pour something to shake"); return; }
                if (!_capped) { SayShaker("cap it first — drag the lid onto the tin"); return; }
                _shaking = true;
                _shakeEnergy = Run.ShakeEnergy;   // continue from what's been shaken, don't reset
                _shakerVel = Vector2.zero;
                _lastShakeMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            });
            _shakerVessel.gameObject.AddComponent<EventTrigger>().triggers.Add(shakeGrab);

            // The metaball fluid draws over the vessel (pool); the solids float on top of it;
            // the bottle is created after, so it sits in front of the liquid.
            _shakerFluid = new MetaballFluid(_pourSurface);
            // The tin's silhouette (bottom → rim): a full body that draws in to the neck, so the
            // drink takes the shaker's shape instead of filling an invisible box (2026-07-24).
            _shakerFluid.SetProfile(new[] {
                // The tin's cavity from just above its rounded base up to the rim. The pinched
                // base rows are deliberately left out of the simulated interior — they are a
                // slot barely wider than a particle, which only squeezed the drink and fired it
                // back out; the floor sits above them instead.
                0.690f, 0.707f, 0.724f, 0.741f, 0.759f, 0.776f, 0.793f, 0.810f, 0.828f, 0.828f,
                0.828f, 0.862f, 0.862f, 0.879f, 0.897f, 0.914f, 0.914f, 0.931f, 0.931f, 0.948f,
                0.966f, 0.966f, 0.966f, 0.983f, 0.983f, 1.000f, 1.000f, 1.000f });
            // The tin's rim, dome and cap ride ABOVE the liquid (2026-07-24): the fluid draws
            // over the open body to show the level, but it must never cover the cap.
            _shakerOpenSize = _shakerVessel.sizeDelta;
            _capRest = new Vector2(-350, -150);   // bottom-left of the tin
            _shakerTop = NewRect("ShakerCap", _pourSurface);
            _shakerTop.anchorMin = _shakerTop.anchorMax = _shakerTop.pivot = new Vector2(0.5f, 0.5f);
            _shakerTop.sizeDelta = _shakerOpenSize;
            _capPos = _capRest;
            _shakerTop.anchoredPosition = _capRest;
            var topImg = _shakerTop.gameObject.AddComponent<Image>();
            topImg.sprite = ItemArt.Load("shaker_cap");
            topImg.preserveAspect = true; topImg.raycastTarget = true;

            var capGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            capGrab.callback.AddListener(_ => { if (!_capped) _capGrabbed = true; });
            _shakerTop.gameObject.AddComponent<EventTrigger>().triggers.Add(capGrab);
            _shakerTop.gameObject.SetActive(topImg.sprite != null);

            _shakerSolids = new ShakerSolids(_pourSurface);
            // The metal shaker is opaque, so the fluid draws OVER it (2026-07-24): you see the
            // drink inside the tin as a cutaway, which is the point — a metal shaker you can
            // still read the level in. (A clear vessel would sit in front instead.)
            _shakerVessel.SetAsFirstSibling();

            // The grabbable bottle, resting lower-right (_bottleRest set with the shadows
            // above, which measure their foot lines off it). The grip pivot sits low so
            // lifting swings the mouth in a big arc.
            _pourBottle = NewRect("Bottle", _pourSurface);
            _pourBottle.pivot = new Vector2(0.5f, 0.22f);
            _pourBottle.sizeDelta = new Vector2(180, BottleH);
            _pourBottle.anchoredPosition = _bottleRest;
            // The art is a CHILD of the grab rect, which is itself an invisible hit plate:
            // a bottle is a narrow silhouette and the grab has to be the whole slot.
            //
            // The drink rides BEHIND the art, cut out by it (2026-08-11, the author:
            // "hepsinde ne kadar miktarı kaldıysa o kadar doluluk olmalı"). The bar of
            // colour that used to run down either side of this bottle is exactly what the
            // stencil removes: the grab rect is a fixed 180 wide and the art is
            // letterboxed inside it, so a plain rectangle of drink had nothing to stop it
            // at the glass. See BottleFill.
            var hitBottle = _pourBottle.gameObject.AddComponent<Image>();
            hitBottle.color = new Color(0, 0, 0, 0.001f);   // invisible, still grabbable

            // The BOTTLE inside the grab plate. The plate is a fixed slot because a hand needs
            // one; the vessel is sized per bottle by VesselArt when the stage refreshes, so a
            // carton stands as a carton and a slim bottle as a slim bottle, both with their
            // feet on the plate's floor line and their caps where the art puts them.
            _pourVessel = NewRect("Vessel", _pourBottle);
            _pourFill = BottleFill.Under(_pourVessel);

            var pourArt = NewRect("Body", _pourVessel);
            Stretch(pourArt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pourBottleBody = pourArt.gameObject.AddComponent<Image>();
            _pourBottleBody.preserveAspect = true;    // the real bottle art, set per focus in RefreshShaker
            _pourBottleBody.color = UITheme.Cyan[3];
            _pourBottleBody.raycastTarget = false;
            if (ItemArt.Bottle("vodka") == null)      // no art available → keep a procedural neck
            {
                var neck = NewRect("Neck", _pourBottle);
                Place(neck, new Vector2(0.5f, 1), new Vector2(20, 34), new Vector2(0, 0));
                neck.gameObject.AddComponent<Image>().color = UITheme.Cream[3];
            }
            // Pointer-down anywhere on the bottle grabs it.
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(_ =>
            {
                // Capping puts the bench away by fading the props — but a CanvasGroup's alpha
                // does not stop raycasts, so the faded bottle stayed fully clickable and a
                // sealed, shaken tin could be topped up from a bottle nobody could see. The
                // rail's stands guard the same way.
                if (_capped) return;
                if (_focusBottle != null && Run != null && Run.Phase == TycoonPhase.DayOpen)
                    _bottleGrabbed = true;
            });
            _pourBottle.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);
            _benchProps.Add(_pourBottle.gameObject.AddComponent<CanvasGroup>());

            // 13 → 16: pinned to the pixel faces' 8px grid (CLAUDE.md), like every other
            // size in the rebuild.
            _shakerReadout = NewText("Readout", _shakerPanel, _body, 16, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(_shakerReadout.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 92), new Vector2(-16, 118));

            // The pour gauge: a slim standing column, cyan-edged, filled bottom-up with the
            // TIN's contents as shares of the whole vessel — 5% of vodka reads 5% VODKA and
            // the room above it reads EMPTY. Against the right wall, left of the TO THE
            // GLASS key with clear air on both sides (at 520 its labels ran under the key;
            // at -340, in its first life, it hung over the prep table this rebuild removed).
            var mixTrack = NewRect("MixTrack", _shakerPanel);
            Place(mixTrack, new Vector2(0.5f, 0.5f), new Vector2(44, 330), new Vector2(490, -24));
            var trackBg = mixTrack.gameObject.AddComponent<Image>();
            trackBg.color = new Color(0.05f, 0.05f, 0.09f, 0.88f);
            trackBg.raycastTarget = false;
            GaugeEdge(mixTrack, new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.7f));
            _shakerMixBar = NewRect("MixSegs", mixTrack);
            Stretch(_shakerMixBar, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            // THE WORK METER (2026-08-26, the author: "doluluk barlarını tamamen tekrardan
            // tasarla, çok amatörce duruyor").
            //
            // It was a 220x14 rectangle of flat Night[0] with a second flat rectangle
            // growing inside it and its caption floating in the air above — no tube, no
            // glass, no marks, and on a bench where nothing is being shaken it is simply a
            // black bar sitting on the counter with nothing in it. That is exactly what the
            // author was looking at.
            //
            // THE HOUSE ALREADY OWNS A FINISHED GAUGE and no bench was using it: the
            // day-end standing track is GaugeTube + a Solid-sprited Image.Type.Filled +
            // GaugeGlass over the top, and it is the same instrument this needs. So the
            // meter is that gauge, with three things added that a WORK bar wants and a
            // standing bar does not: it is only there while there is work (the whole rig
            // hides at rest instead of sitting empty), its caption is set INTO the tube
            // rather than hung over it, and it carries a mark at the point where the work
            // is enough — the one number a player shaking a tin actually wants to see
            // coming.
            var meterRig = _shakeMeterRig = NewRect("WorkMeter", _shakerPanel);
            Place(meterRig, new Vector2(0.5f, 0), new Vector2(ShakeMeterW, MeterH),
                  new Vector2(0, 74));
            meterRig.pivot = new Vector2(0.5f, 0);
            var tube = meterRig.gameObject.AddComponent<Image>();
            tube.sprite = ChromeArt.GaugeTube((int)ShakeMeterW, (int)MeterH);
            tube.color = UITheme.Night[2];
            tube.raycastTarget = false;

            var meterInner = NewRect("Inner", meterRig);
            Stretch(meterInner, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            var fill = NewRect("Fill", meterInner);
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _shakeMeterFill = fill.gameObject.AddComponent<Image>();
            // WITH A SPRITE, or Type.Filled is ignored and the gauge reads full at nought
            // (the day-end track paid for that lesson; see ChromeArt.Solid).
            _shakeMeterFill.sprite = ChromeArt.Solid();
            _shakeMeterFill.raycastTarget = false;
            _shakeMeterFill.type = Image.Type.Filled;
            _shakeMeterFill.fillMethod = Image.FillMethod.Horizontal;
            _shakeMeterFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _shakeMeterFill.fillAmount = 0f;

            var meterGlass = NewRect("Glass", meterRig);
            Stretch(meterGlass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mg = meterGlass.gameObject.AddComponent<Image>();
            mg.sprite = ChromeArt.GaugeGlass((int)ShakeMeterW, (int)MeterH, 5);
            mg.raycastTarget = false;

            // WHERE ENOUGH IS. A tin is worked until the drink is mixed, not until the bar
            // is full, and the bar was saying nothing about which point that was.
            _shakeMeterMark = NewRect("Enough", meterRig);
            Place(_shakeMeterMark, new Vector2(0, 0), new Vector2(2, MeterH + 8f),
                  new Vector2(EnoughMark * (ShakeMeterW - 4f) + 2f, -4f));
            _shakeMeterMark.pivot = new Vector2(0.5f, 0);
            var mkImg = _shakeMeterMark.gameObject.AddComponent<Image>();
            mkImg.color = UITheme.Cream[4];
            mkImg.raycastTarget = false;

            // The caption is INSIDE the tube — a gauge whose reading floats above it is a
            // gauge and a label, which is two objects doing one job.
            _shakeMeterText = NewText("ShakeText", meterRig, _body, 8, TextAnchor.MiddleCenter,
                                      UITheme.TextPrimary);
            Stretch(_shakeMeterText.rectTransform, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);
            _shakeMeterText.raycastTarget = false;
            var edge = _shakeMeterText.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.85f);
            edge.effectDistance = new Vector2(1f, -1f);
            meterRig.gameObject.SetActive(false);

            // THE BAR SPOON (GDD 21 §14, 2026-08-11): the stir's instrument, resting by the
            // tin. Drawn, not generated — it is an instrument the pointer works, and at this
            // size a rod and a bowl in the bench's own steel read truer than any take.
            // BESIDE THE TIN AND CLEAR OF THE LID (2026-08-14, the author: the spoon and the
            // shaker were drawn over each other). Measured rather than nudged: the tin stands
            // at x −310..−110 and the lid rests across −450..−250, so the only clear air on
            // this side is the corridor between the BACK key (which ends at −550) and the
            // lid. The spoon leans there, in reach, touching nothing.
            _spoonRest = new Vector2(-500f, -104f);
            _spoonRt = NewRect("BarSpoon", _pourSurface);
            _spoonRt.pivot = new Vector2(0.5f, 1f);        // held by the grip, bowl hangs down
            _spoonRt.sizeDelta = new Vector2(26, 118);
            _spoonRt.anchoredPosition = _spoonRest;
            var spoonHit = _spoonRt.gameObject.AddComponent<Image>();
            spoonHit.color = new Color(0, 0, 0, 0.001f);   // the whole slot answers the hand
            // A DRAWN spoon at last (2026-08-25, the author: "kaşık için uygun bir görsel
            // üretilecek"): the twisted-stem bar spoon from Tools/bench_props_gen.py, bowl
            // down — shipped flipped, because the drawing came bowl-up and a spoon stirs
            // with its bowl in the drink. Its 32×128 art stands at a whole 2×; the three
            // grey rectangles it replaces stay below as the no-art fallback.
            var spoonArt = ItemArt.Load("bench_spoon");
            if (spoonArt != null)
            {
                _spoonRt.sizeDelta = new Vector2(64, 256);
                var sImg = NewRect("Art", _spoonRt);
                Stretch(sImg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var si = sImg.gameObject.AddComponent<Image>();
                si.sprite = spoonArt; si.preserveAspect = true; si.raycastTarget = false;
            }
            else
            {
                var rod = NewRect("Rod", _spoonRt);
                rod.anchorMin = rod.anchorMax = new Vector2(0.5f, 1f);
                rod.pivot = new Vector2(0.5f, 1f);
                rod.sizeDelta = new Vector2(5, 96);
                rod.anchoredPosition = Vector2.zero;
                var rodImg = rod.gameObject.AddComponent<Image>();
                rodImg.color = new Color(0.72f, 0.75f, 0.80f, 1f);
                rodImg.raycastTarget = false;
                var twist = NewRect("Twist", _spoonRt);        // the twisted shaft's glint
                twist.anchorMin = twist.anchorMax = new Vector2(0.5f, 1f);
                twist.pivot = new Vector2(0.5f, 1f);
                twist.sizeDelta = new Vector2(2, 84);
                twist.anchoredPosition = new Vector2(-1, -6);
                var twistImg = twist.gameObject.AddComponent<Image>();
                twistImg.color = new Color(0.92f, 0.94f, 0.97f, 0.85f);
                twistImg.raycastTarget = false;
                var bowl = NewRect("Bowl", _spoonRt);
                bowl.anchorMin = bowl.anchorMax = new Vector2(0.5f, 0f);
                bowl.pivot = new Vector2(0.5f, 0f);
                bowl.sizeDelta = new Vector2(16, 24);
                bowl.anchoredPosition = new Vector2(0, 0);
                var bowlImg = bowl.gameObject.AddComponent<Image>();
                bowlImg.color = new Color(0.62f, 0.66f, 0.72f, 1f);
                bowlImg.raycastTarget = false;
            }
            var spoonGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            spoonGrab.callback.AddListener(_ =>
            {
                // The spoon works an OPEN tin only — the cap hands the stage to the shake.
                if (!_capped && Run != null && Run.Phase == TycoonPhase.DayOpen)
                { _spoonHeld = true; _stirHasPrev = false; }
            });
            _spoonRt.gameObject.AddComponent<EventTrigger>().triggers.Add(spoonGrab);
            _benchProps.Add(_spoonRt.gameObject.AddComponent<CanvasGroup>());

            // THE WAY FORWARD (the author's loop rework): the drink moves ON to the glass
            // from here. Right edge centre — the mirror of where the back key stands —
            // and lit only when Core itself would let the drink leave.
            var toGlass = NewRect("ToGlass", _shakerPanel);
            Place(toGlass, new Vector2(1f, 0f), new Vector2(216, KeyStripH),
                  new Vector2(-30, KeyStripY));
            _toGlassBtn = toGlass.gameObject.AddComponent<Button>();
            _toGlassBtn.onClick.AddListener(() => GoTo(Stage.Serve));
            _toGlassGroup = toGlass.gameObject.AddComponent<CanvasGroup>();
            var tgFace = NewRect("Face", toGlass);
            Stretch(tgFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(toGlass, UITheme.PrimaryAction, _toGlassBtn, tgFace);   // GDD 16 §2
            _toGlassLabel = NewText("L", tgFace, _body, 8, TextAnchor.MiddleCenter, Color.black);
            Stretch(_toGlassLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 4 + KeyPlate.Throw), new Vector2(-4, -4));
            // ONE LINE, and the arrow says which way (2026-08-26). Three words stacked in a
            // 76-wide column is a column of words, not a key.
            _toGlassLabel.text = "TO THE GLASS  ▶";

            // THE WAY BACK OUT OF A LID CLOSED TOO EARLY (2026-08-14). It stands under the
            // tin, on the side the spoon rests on, and only ever appears once the lid is
            // actually on — the bench props have faded by then, so it is not one more thing
            // to read while the drink is being built.
            var lidOff = NewRect("LidOff", _shakerPanel);
            Place(lidOff, new Vector2(0f, 0f), new Vector2(160, 52), new Vector2(120, 132));
            _lidOffKey = lidOff.gameObject.AddComponent<Button>();
            var lidFace = NewRect("Face", lidOff);
            Stretch(lidFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(lidOff, UITheme.Night[3], _lidOffKey, lidFace);
            var lidLabel = NewText("L", lidFace, _body, 8, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(lidLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 4 + KeyPlate.Throw), new Vector2(-4, -4));
            lidLabel.text = "TAKE THE LID OFF";
            _lidOffGroup = lidOff.gameObject.AddComponent<CanvasGroup>();
            _lidOffGroup.alpha = 0f;
            _lidOffGroup.blocksRaycasts = false;
            _lidOffKey.onClick.AddListener(UncapTin);

            // What to do, in order, in the corner nothing else uses.
            BuildStepCard(_shakerPanel);

            // The way back wears the LEFT edge (the loop rework): one key, one place,
            // every station.
            AddEdgeBack(_shakerPanel);
            // THE BIN COMES TO THE BENCH (2026-08-22). It used to stand on the back-bar page,
            // which is the page the cellar replaced — and the room's own bin refuses while a
            // bench is open (OnBinClicked), by design, so without this a botched build could
            // not be thrown away at all until you had walked out of the room you botched it in.
            AddBinButton(_shakerPanel);
        }

    }
}
