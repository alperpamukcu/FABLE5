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
        private BottleFill _pourFill;         // what is left in it, behind the glass (pre-v4 art)
        private BottleArt _pourArt;           // the v4 sandwich: back, level drink, glass front
        private ItemArt.BottlePlates _pourPlates;   // what the sandwich shows, resolved once per card
        private object _pourPlatesFor;        // ...and the card it was resolved for
        /// <summary>Where this bottle's CAP is, as an offset from the grip — measured off the
        /// art (VesselArt) when the stage refreshes, swung with the bottle when it tips.</summary>
        private Vector2 _pourMouth;
        private MetaballFluid _shakerFluid;   // the metaball liquid: pour stream + pooled body
        private RectTransform _tinFrontRt;    // the tin's front body, over the fluid (2026-09-08)
        private Image _tinFrontImg;
        private float _slosh;                 // running slosh phase for the shaker surface
        private Vector2 _bottleRest;
        private bool _bottleGrabbed;
        // THE HAND KEEPS ITS GRIP (2026-09-06, the author: "nesneler tutulurken veya
        // sürüklenirken hep mouseun ortasına hizalanıyor bunun olmamasını istiyorum"): where
        // on the bottle or the lid the finger landed is where it stays for the carry.
        private Vector2 _capGrabOffset;
        /// <summary>The hand on the bottle (PourHand, 2026-09-11) — the neck grip, the weight and
        /// the walk home. The bottle used to snap to the cursor and was left hanging, tipped,
        /// wherever it was let go.</summary>
        private readonly PourHand _bottleHand = new PourHand();
        private Image _tinSurface;               // the brimful drink's oval in the tin's mouth (2026-09-14)
        private HoverGlow _pourGlow;             // the bottle's glow, stilled while the hand holds it
        private const float BottleGripDepth = 60f;
        private bool _pouring;
        // 320 (2026-09-14, the author: "şişeyi kaldırma aralığını genişletip büyütebiliriz"): the tin went to the bottom
        // of the screen, so the room over it grew and the lift takes more of it (200, then 260, before).
        private const float LiftRange = 320f;
        // 180, NECK STRAIGHT DOWN (2026-09-13): the pour runs past level and is fullest with the
        // vessel on its neck (BottlePour), so the hand has to be able to get it there. The first
        // part of the lift lays it level (PourHand.Lean). Was 118.
        private const float MaxTilt = 180f;    // degrees the vessel leans at full lift
        // 230 → 300 (the author, 2026-08-05: "shakera dökme sahnesinde tüm alkol
        // şişelerinin boyutunu büyüt") — the v3 masters are slimmer than the old art,
        // and at 230 a 3.7:1 bottle read as a wand. The mouth offset and the tilt
        // maths all derive from this, so the pour arc scales with it.
        // 384 (2026-09-04, PLAN_bottle_art_v4 §3): the v4 master is 96x192 and draws at exactly
        // 2x, the same pixel size the cellar's 32x64 copy draws at — one drawing, three times
        // the resolution when it is in the hand. Was 300, which put a 192-tall master at 1.56x.
        private const float BottleH = 384f;
        // The pour fills slower than the raw bottle rate so the stream reads as a real pour
        // (GDD 24 §2, 2026-07-22 — "doluş hızı çok hızlı"). Only the drawn volume slows; the
        // floor's patience clock runs on its own tick, untouched.
        /// <summary>The share of full flow the bottle in hand is giving this frame (Core's
        /// BottlePour, read — never recomputed here), for the stream to be drawn at.</summary>
        private float _bottleShare;

        /// <summary>
        /// WHERE THE BOTTLE IS TAKEN: ON ITS OWN PIXELS (2026-09-13, the author: "bir şeyi
        /// dökerken nesnenin üzerinden tutmamız gereksin"). The grab used to be the whole
        /// 180-wide slot the bottle stands in, so a press in the air beside a slim bottle picked
        /// it up. This invisible copy of the drawing answers the pointer only where the drawing
        /// is opaque; the slot itself no longer takes a raycast.
        /// </summary>
        private Image _pourGrip;
        private Image _pourSlotHit;

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
        // ...and since 2026-09-16 the light on the work and the two mirrors (StepBenchLight).
        private RectTransform _benchLight, _tinMirror, _bottleMirror;

        /// <summary>A faint, squashed, upside-down copy of a prop on the stone under its foot: what a wiped counter
        /// gives back. Hung from its top edge, flipped by its scale; its picture is set each frame.</summary>
        private RectTransform AddMirror(string name, float w, float h)
        {
            var rt = NewRect(name, _pourSurface);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // the pivot at the FOOT: flipped by its scale, a rect grows the other way from its pivot, so a foot pivot
            // is what hangs the mirror under the line rather than over it (seen in play: the first cut hid behind the bottle)
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = new Vector3(1f, -1f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.14f);
            img.raycastTarget = false;
            img.preserveAspect = false;
            rt.SetSiblingIndex(1);
            return rt;
        }

        /// <summary>Every frame: the light follows the work, the mirrors stand under their props.</summary>
        private void StepBenchLight()
        {
            if (_benchLight == null || _shakerVessel == null) return;
            Vector2 want = _capped ? _shakerVessel.anchoredPosition + new Vector2(0f, 40f)
                : _bottleGrabbed && _bottleHand.Held ? _bottleHand.GripPoint
                : (_shakerVessel.anchoredPosition + _bottleRest) * 0.5f + new Vector2(0f, 20f);
            _benchLight.anchoredPosition = Vector2.Lerp(_benchLight.anchoredPosition, want,
                1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            if (_tinMirror != null)
            {
                var img = _tinMirror.GetComponent<Image>();
                var art = _shakerBodyImg != null ? _shakerBodyImg.sprite : null;
                bool show = art != null && !_shaking && _blowT <= 0f;
                if (_tinMirror.gameObject.activeSelf != show) _tinMirror.gameObject.SetActive(show);
                if (show)
                {
                    if (img.sprite != art) img.sprite = art;
                    _tinMirror.anchoredPosition = new Vector2(_shakerVessel.anchoredPosition.x, TinFootY + 2f);
                }
            }
            if (_bottleMirror != null)
            {
                var img = _bottleMirror.GetComponent<Image>();
                var art = _pourPlates != null && _pourPlates.Front != null ? _pourPlates.Front
                    : _pourBottleBody != null ? _pourBottleBody.sprite : null;
                bool show = art != null && _pourBottle != null && _pourBottle.gameObject.activeSelf && _bottleHand.AtRest && !_capped;
                if (_bottleMirror.gameObject.activeSelf != show) _bottleMirror.gameObject.SetActive(show);
                if (show)
                {
                    if (img.sprite != art) img.sprite = art;
                    _bottleMirror.anchoredPosition = new Vector2(_bottleRest.x, BottleFootY + 2f);
                }
            }
        }

        // The STIR (GDD 21 §14, 2026-08-11): the mandatory mix made Preparations.Stirred
        // load-bearing, so the bench grew a bar spoon. Stir and shake are told apart by the
        // CAP — the spoon only works an OPEN tin, the shake only a capped one — so the two
        // mixing verbs can never fight over one gesture.
        private RectTransform _spoonRt;
        private RectTransform _napkinRt;     // the towel under the spoon — comes and goes with it
        private Vector2 _spoonRest;
        /// <summary>Where the spoon stands (2026-09-16): between the lid and the tin, its foot 70 under the bench
        /// line so the towel under it clears the plaque above (measured; the BACK key is in another column).</summary>
        private const float SpoonX = -340f, SpoonFootY = BenchFootY - 70f;
        private RectTransform _shakerPlaque;
        private bool _spoonHeld;
        private Vector2 _spoonGrabOffset;   // the hand keeps its grip (2026-09-06, the seventh list)
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
            // One key per whole line (L1): the pour half and the method half are one sentence.
            if (!bottleInReach)
                return method == PrepMethod.Shaken ? UIText.T("bench.shaker.hint.shaken")
                    : method == PrepMethod.Stirred ? UIText.T("bench.shaker.hint.stirred")
                    : method == PrepMethod.Built ? UIText.T("bench.shaker.hint.built")
                    : UIText.T("bench.shaker.hint.capped");
            return method == PrepMethod.Shaken ? UIText.T("bench.shaker.hint.pour_shaken")
                : method == PrepMethod.Stirred ? UIText.T("bench.shaker.hint.pour_stirred")
                : method == PrepMethod.Built ? UIText.T("bench.shaker.hint.pour_built")
                : UIText.T("bench.shaker.hint.pour");
        }
        private CanvasGroup _toGlassGroup;
        private Text _toGlassLabel;
        private bool _toGlassWasOn;
        private float _toGlassPulse;

        /// <summary>The point past which the tin is worked enough for the drink to be worth pouring. Measured
        /// against the same 0..1 both verbs report, so one mark reads for the shake and for the stir.</summary>
        private const float EnoughMark = 0.72f;

        // THE WORK SHOWS ON THE DRINK (2026-09-16, the author: "karıştırma ve çalkalama sırasındaki barın
        // tasarımını değiştir"). The tube that hung over the room is gone. A shaken tin FROSTS from its base up
        // to the height the shake has reached — a white gradient cut to the tin's own front plate, on the thing
        // the hand is throwing about; the measure's bands BLEND toward the drink's one colour as either verb works,
        // layers while unmixed and one colour once mixed, which is what Core says of it; and the counter's plaque
        // carries the figure and where enough is. Nothing floats over the room any more.
        private RectTransform _tinFrostRt;
        private float _workLevel;          // the last reading; the frost keeps it after the hand lets go
        private readonly Dictionary<RectTransform, List<(Image img, Color tone)>> _gaugeBands =
            new Dictionary<RectTransform, List<(Image, Color)>>();

        // ...AND BESIDE THE MEASURE, A COLUMN (2026-09-16, the author: "Çalkalama/karıştırma doluluk barı ölçü
        // barının yanında gözükebilir"): the house gauge stood on end at the measure's left, MIX engraved under it,
        // the enough mark scratched across it, its fill amber until the mark and lime past it. It reads what the
        // frost reads; the frost stays on the tin, where the eye is while shaking.
        private Image _workFill;
        private const float WorkColW = 24f, WorkColGap = 20f;

        private void BuildWorkColumn(RectTransform panel)
        {
            var rig = NewRect("WorkColumn", panel);
            Place(rig, new Vector2(0.5f, 0.5f), new Vector2(WorkColW, MeasureSize.y),
                  new Vector2(MeasureAt.x - MeasureSize.x * 0.5f - WorkColGap - WorkColW * 0.5f, MeasureAt.y));
            var tube = rig.gameObject.AddComponent<Image>();
            tube.sprite = ChromeArt.GaugeTube((int)WorkColW, (int)MeasureSize.y);
            tube.color = UITheme.Night[2];
            tube.raycastTarget = false;
            var inner = NewRect("Inner", rig);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            var fill = NewRect("Fill", inner);
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _workFill = fill.gameObject.AddComponent<Image>();
            _workFill.sprite = ChromeArt.Solid();          // a fill needs something to fill (ChromeArt.Solid)
            _workFill.type = Image.Type.Filled;
            _workFill.fillMethod = Image.FillMethod.Vertical;
            _workFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _workFill.fillAmount = 0f;
            _workFill.raycastTarget = false;
            var glass = NewRect("Glass", rig);
            Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = glass.gameObject.AddComponent<Image>();
            gi.sprite = ChromeArt.GaugeGlass((int)WorkColW, (int)MeasureSize.y, 5);
            gi.raycastTarget = false;
            // where enough is: a line scratched across the tube, a little wider than it
            var mark = NewRect("Enough", rig);
            Place(mark, new Vector2(0.5f, 0f), new Vector2(WorkColW + 8f, 2f), new Vector2(0f, 2f + EnoughMark * (MeasureSize.y - 4f)));
            mark.pivot = new Vector2(0.5f, 0.5f);
            var mi = mark.gameObject.AddComponent<Image>();
            mi.color = UITheme.Cream[4];
            mi.raycastTarget = false;
            var word = NewText("Head", rig, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            word.rectTransform.anchorMin = new Vector2(0, 0); word.rectTransform.anchorMax = new Vector2(1, 0);
            word.rectTransform.pivot = new Vector2(0.5f, 1);
            word.rectTransform.offsetMin = new Vector2(-20, -16); word.rectTransform.offsetMax = new Vector2(20, -4);
            word.text = UIText.T("bench.shaker.work.head");
            word.raycastTarget = false;
            Engraved(word);
        }

        /// <summary>Puts a reading on the drink: the frost, the blend, the column, and the plaque's figure with the
        /// mark it is working toward — green once past it.</summary>
        private void ShowWorkMeter(float amount, Color tone, string caption)
        {
            _meterHeldThisFrame = true;
            _workLevel = Mathf.Clamp01(amount);
            SetFrost(_workLevel);
            BlendGauge(_shakerMixBar, _workLevel);
            bool enough = _workLevel >= EnoughMark;
            SayShaker(enough ? caption
                : caption + "  ·  " + UIText.T("bench.shaker.work.enough", ("mark", EnoughMark.ToString("P0"))));
            if (enough) _shakerReadout.color = UITheme.Lime[3];
        }

        /// <summary>At rest the frost stays where the shake left it, and the bands blend by what Core says.</summary>
        private void StepWorkMeter()
        {
            if (!_meterHeldThisFrame && Run != null) BlendGauge(_shakerMixBar, Run.IsMixed ? 1f : 0f);
            _meterHeldThisFrame = false;
        }

        /// <summary>The frost on the tin's body: <paramref name="level"/> of the front plate's height, from its base.</summary>
        private void SetFrost(float level)
        {
            if (_workFill != null)
            {
                _workFill.fillAmount = Mathf.Clamp01(level);
                _workFill.color = level >= EnoughMark ? UITheme.Lime[3] : CounterFinish.Current.Accent;
            }
            if (_tinFrostRt == null) return;
            bool on = level > 0.02f;
            if (_tinFrostRt.gameObject.activeSelf != on) _tinFrostRt.gameObject.SetActive(on);
            _tinFrostRt.anchorMin = Vector2.zero;
            _tinFrostRt.anchorMax = new Vector2(1f, Mathf.Clamp01(level));
            _tinFrostRt.offsetMin = _tinFrostRt.offsetMax = Vector2.zero;
        }

        /// <summary>The measure's bands, each from its own liquid's colour toward the drink's one colour by
        /// <paramref name="level"/>: a tin half worked is half one drink.</summary>
        private void BlendGauge(RectTransform bar, float level)
        {
            if (bar == null || Run == null || !_gaugeBands.TryGetValue(bar, out var bands)) return;
            var mixed = UITheme.DrinkColor(Run.Shelf, Run.Glass);
            mixed.a = 1f;
            foreach (var (img, tone) in bands)
                if (img != null) img.color = Color.Lerp(tone, mixed, level);
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

        // ── which tin the bar owns (2026-09-06) ──────────────────────────────────
        //
        // The steel shaker came with the room; the gold one is a rung on the `shaker`
        // ladder, and the two benches wear whichever the bar has fitted. The plates are
        // the author's own, drawn on one canvas per tier and landed on the sheet these
        // rects already work (Tools/shaker_ship.py), so the tier is a NAME and nothing
        // here has to know what a gold shaker looks like.

        private Image _shakerBodyImg, _shakerCapImg;
        private HoverGlow _capGlow;     // off once the lid is on: the shaker is one thing

        /// <summary>The tier's suffix on a shaker plate: "" for the steel one the bar opened
        /// with, "_t2" once the gold rung is fitted. Read off Core's own ladder.</summary>
        // The tin the bar WEARS (2026-09-13): a player who climbed to gold may still pick the
        // steel one's look, and keeps the gold one's speed (TycoonRun.WorkSpeed).
        private string ShakerTier => Run != null && (Run.WornRung("shaker")?.Level ?? 0) >= 2 ? "_t2" : "";

        /// <summary>Puts the owned tin on both benches. Called on the way into either one, so
        /// a rung bought at the market is on the counter the next time the tin is opened.</summary>
        /// <summary>The tin's front body for its tier, or nothing where the author drew none.</summary>
        private void DressTinFront()
        {
            if (_tinFrontImg == null) return;
            string t = ShakerTier;
            var front = ItemArt.Load("tin_open" + t + "_Front") ?? ItemArt.Load("tin_open_Front");
            _tinFrontImg.sprite = front;
            _tinFrontImg.enabled = front != null;
            // SEATED WHERE IT WAS CUT FROM (2026-09-14): matched on the tin's own sheet, per tier. The
            // hand-typed anchors were tin_open's, and stretched the t2 crop's 123 rows over 124.
            var tinSheet = ItemArt.Load("tin_open" + t) ?? ItemArt.Load("tin_open");
            var at = GlassArt.FrontOffset(tinSheet, front);
            if (front != null && tinSheet != null && at.x >= 0f)
            {
                float sheetW = tinSheet.rect.width, sheetH = tinSheet.rect.height;
                var artRt = _tinFrontImg.rectTransform;
                artRt.anchorMin = new Vector2(at.x / sheetW, (sheetH - at.y - front.rect.height) / sheetH);
                artRt.anchorMax = new Vector2((at.x + front.rect.width) / sheetW, (sheetH - at.y) / sheetH);
                artRt.offsetMin = artRt.offsetMax = Vector2.zero;
            }
        }

        /// <summary>The front rides the tin: same position, rotation and scale, every frame
        /// the shaker stage is stepped.</summary>
        private void FollowTinFront()
        {
            if (_tinFrontRt == null || _shakerVessel == null) return;
            bool on = _shakerVessel.gameObject.activeInHierarchy && _tinFrontImg != null && _tinFrontImg.sprite != null;
            if (_tinFrontRt.gameObject.activeSelf != on) _tinFrontRt.gameObject.SetActive(on);
            if (!on) return;
            _tinFrontRt.anchoredPosition = _shakerVessel.anchoredPosition;
            _tinFrontRt.localRotation = _shakerVessel.localRotation;
            _tinFrontRt.localScale = _shakerVessel.localScale;
            _tinFrontRt.sizeDelta = _shakerVessel.sizeDelta;
        }

        private void DressShakerArt()
        {
            string t = ShakerTier;
            var tin = ItemArt.Load("tin_open" + t) ?? ItemArt.Load("tin_open");
            if (_shakerBodyImg != null && tin != null) _shakerBodyImg.sprite = tin;
            DressTinFront();
            if (_serveShakerBody != null && tin != null) _serveShakerBody.sprite = tin;
            var cap = ItemArt.Load("shaker_cap" + t) ?? ItemArt.Load("shaker_cap");
            if (_shakerCapImg != null && cap != null) _shakerCapImg.sprite = cap;
            // The pouring lid is the same lid with its tip off, and it falls back the same
            // way the serve bench always did: a missing plate costs the look, never the pour.
            var pour = ItemArt.Load("shaker_cap_pour" + t) ?? ItemArt.Load("shaker_cap_pour") ?? cap;
            if (_serveCapImg != null && pour != null) _serveCapImg.sprite = pour;
        }

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
            // The bottle's name used to ride the card's head; the strip has no head, so the
            // name goes to a quiet text the bench still writes to (see BuildStepStrip).
            _shakerTitle = BuildStepStrip(_shakerPlaque,
                new[] { UIText.T("bench.shaker.step.fill"), UIText.T("bench.shaker.step.cap"),
                        UIText.T("bench.shaker.step.mix"), UIText.T("bench.shaker.step.to_glass") },
                new[] { "step_fill", "step_cap", "step_shake", "step_glass" }, _stepRows);
        }

        /// <summary>A dark edge under the letters, the way a cut in stone catches shadow: the strip's ink, for
        /// every word engraved on a plaque.</summary>
        private static void Engraved(Text t)
        {
            var cut = t.gameObject.AddComponent<Shadow>();
            cut.effectColor = new Color(0f, 0f, 0f, 0.85f);
            cut.effectDistance = new Vector2(1f, -1f);
        }

        // ── the pour dial ─────────────────────────────────────────────────────────
        //
        // SECOND TAKE (2026-09-16, the author of the nine rungs: "Dökülme hızı göstergesini beğenmedim daha
        // profesyonel olsun arka plana yedirilme işini beğendim"): the same reading as an INSTRUMENT — a half-round
        // dial cut into the counter under the bottle's rest, a brass arc, nine ticks for Core's nine steps of flow
        // (BottlePour, 1·1·2·3·5·8·13·21·34), and an amber needle that swings from the left as the bottle is
        // lifted. It is the instrument family the plaque belongs to: the same recess, the same brass, the same
        // engraved captions.

        private RectTransform _pourNeedle;
        private const float DialPlaqueW = 180f, DialPlaqueH = 110f, DialW = 176f, DialH = 88f, NeedleH = 66f;

        private void BuildLiftLadder()
        {
            var plaque = NewRect("PourDial", _pourSurface);
            Place(plaque, new Vector2(0.5f, 0.5f), new Vector2(DialPlaqueW, DialPlaqueH),
                  new Vector2(_bottleRest.x, BottleFootY - 22f - DialPlaqueH * 0.5f));
            var img = plaque.gameObject.AddComponent<Image>();
            img.sprite = CounterFinish.Recess();
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            // Under the bottle's shadow and the bottle, but OVER the mirrors (the bottle's reflection lay across the
            // face in play): just after the last mirror in the surface's order.
            plaque.SetSiblingIndex(_bottleMirror != null ? _bottleMirror.GetSiblingIndex() + 1 : 0);

            var face = NewRect("Face", plaque);
            Place(face, new Vector2(0.5f, 0f), new Vector2(DialW, DialH), new Vector2(0f, 20f));
            var fi = face.gameObject.AddComponent<Image>();
            fi.sprite = ChromeArt.DialFace((int)(DialW / 2f), (int)(DialH / 2f), BottlePour.StepCount,
                CounterFinish.Current.Rim, CounterFinish.Current.Id);   // drawn at half, shown at 2x; the arc in the finish's rim
            fi.raycastTarget = false;

            // The needle turns about the hub, which the face draws two texels (four units) over its foot.
            _pourNeedle = NewRect("Needle", plaque);
            _pourNeedle.anchorMin = _pourNeedle.anchorMax = new Vector2(0.5f, 0f);
            _pourNeedle.pivot = new Vector2(0.5f, 0f);
            _pourNeedle.sizeDelta = new Vector2(4f, NeedleH);
            _pourNeedle.anchoredPosition = new Vector2(0f, 24f);
            var ni = _pourNeedle.gameObject.AddComponent<Image>();
            ni.color = CounterFinish.Current.Accent;
            ni.raycastTarget = false;

            var word = NewText("Hint", plaque, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(word.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(4f, 4f), new Vector2(-4f, 16f));
            word.text = UIText.T("bench.shaker.lift_hint");
            Engraved(word);
            LightLiftLadder(0);
        }

        /// <summary>The needle on <paramref name="step"/>'s tick (1..9), or lying at the left for none.</summary>
        private void LightLiftLadder(int step)
        {
            if (_pourNeedle == null) return;
            int n = BottlePour.StepCount;
            float a = step <= 0 ? 180f : 180f * (1f - (step - 0.5f) / n);   // degrees from the right, left round
            _pourNeedle.localRotation = Quaternion.Euler(0f, 0f, a - 90f);
        }

        // ── THE STEPS ARE CUT INTO THE COUNTER (2026-09-13) ─────────────────────
        //
        // The author: "Built sahnesinin düzeni tekrardan tasarlansın, doluluk barları ve
        // yönergeler sanki arkaplana dahilmiş gibi gözüksün." The checklist was a dark card
        // standing up from the counter over the room — over the very drinkers the night is
        // about — which is a panel, whatever it is inlaid to look like. It is a line of the
        // counter's own front now: a numbered socket, the words engraved beside it, a tick
        // when the step is done, side by side under the props' feet and above the keys. No
        // plate, no box: the same ink rules as ever (PaintSteps), on the stone itself.

        /// <summary>The height of one step on the strip, and the room its figure takes before the picture.</summary>
        private const float StepStripH = 20f, StepNumberW = 10f;

        // ── THE PLAQUE (2026-09-16, the author: "yazıları yönergeleri arkaplana göm ... yerleşimleri mevcut
        // arkaplana göre yap") ───────────────────────────────────────────────────────────────────────────────────
        //
        // A recess cut into the counter under its rail, at the bench's left, that the bench's words are engraved
        // in: the step strip on its upper row, the readout (the tin's) or the aim line (the glass's) on its lower.
        // It is HUNG FROM THE RAIL (AlignBenchCounters), so it stands the same distance under the counter's far
        // edge whichever line the room puts the counter on. Its width stops short of the tin's column — measured:
        // the tin at rest spans x 464..696, the plaque ends at 438 — and the tools stand under it, not in it.

        /// <summary>The plaque's place and size on a bench: from the panel's left edge, under the rail's foot. 464
        /// wide: the tin's rect starts at 464 but its steel at ~500, and the capped tin stands at the centre.</summary>
        private const float PlaqueX = 16f, PlaqueW = 464f, PlaqueH = 64f, PlaquePad = 8f, PlaqueUnderRail = 6f;
        /// <summary>The strip's and the readout's bottoms, from the plaque's own; the readout has two lines' room.</summary>
        private const float PlaqueStepsY = 42f, PlaqueLineY = 6f, PlaqueLineH = 32f;
        /// <summary>The counter's far edge: ridge, seam, six rails and a seam (AddBenchCounter's bands).</summary>
        private const float RailBandH = 8f + 5f + 5f * 6f + 5f;

        private readonly List<(RectTransform rt, float dy)> _railHung = new List<(RectTransform, float)>();

        /// <summary>A recess in the counter for a bench's words, hung <paramref name="dyFromRailFoot"/> under the
        /// far edge's foot. Its children ride it.</summary>
        private RectTransform AddCounterPlaque(RectTransform panel, float w, float h, float dyFromRailFoot = PlaqueUnderRail)
        {
            var rt = NewRect("Plaque", panel);
            Place(rt, new Vector2(0f, 0f), new Vector2(w, h), new Vector2(PlaqueX, 0f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CounterFinish.Recess();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
            _railHung.Add((rt, RailBandH + dyFromRailFoot + h));
            return rt;
        }

        private Text BuildStepStrip(RectTransform plaque, string[] words, string[] marks,
            List<(Image icon, Text label, Image tick)> rows)
        {
            // ON THE PLAQUE (2026-09-16), left to right from its edge — the strip used to be centred across the
            // bench, where the capped tin stands and stood in front of it.
            var strip = NewRect("Steps", plaque);
            Place(strip, new Vector2(0f, 0f), new Vector2(plaque.sizeDelta.x - PlaquePad * 2f, StepStripH),
                  new Vector2(PlaquePad, PlaqueStepsY));
            for (int i = 0; i < words.Length; i++)
            {
                var cell = NewRect("Step" + i, strip);
                cell.anchorMin = cell.anchorMax = new Vector2(0f, 0.5f);
                cell.pivot = new Vector2(0f, 0.5f);
                cell.sizeDelta = new Vector2(10f, StepStripH);

                // THE NUMBER, THEN THE PICTURE (2026-09-16, the author: "1-2-3-4 daha profesyonel bir şekilde
                // belirtilmeli görsellerle ne yapması gerektiği"): the step's figure in the small face, then a 16 px
                // drawing of the thing to do (ChromeArt's step_* marks — the bottle over the tin, the lid, the shaken
                // tin, the spoon, the glass), then the word. The socket the number sat in is gone.
                var num = NewText("N", cell, _display, 8, TextAnchor.MiddleLeft, UITheme.TextSecondary);
                Place(num.rectTransform, new Vector2(0, 0.5f), new Vector2(10, 14), Vector2.zero);
                num.rectTransform.pivot = new Vector2(0, 0.5f);
                num.text = (i + 1).ToString();
                num.raycastTarget = false;
                Engraved(num);
                var mark = NewRect("I", cell);
                Place(mark, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(StepNumberW, 0));
                mark.pivot = new Vector2(0, 0.5f);
                var mimg = mark.gameObject.AddComponent<Image>();
                mimg.sprite = ChromeArt.Mark(marks[i]);
                mimg.preserveAspect = true;
                mimg.raycastTarget = false;

                var text = NewText("L", cell, _body, 8, TextAnchor.MiddleLeft, UITheme.TextSecondary);
                Place(text.rectTransform, new Vector2(0, 0.5f), new Vector2(240, 14), new Vector2(StepNumberW + 16f + 6f, 0));
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.text = words[i];
                // ENGRAVED: a dark edge under the letters, the way a cut in stone catches shadow.
                var cut = text.gameObject.AddComponent<Shadow>();
                cut.effectColor = new Color(0f, 0f, 0f, 0.85f);
                cut.effectDistance = new Vector2(1f, -1f);

                var tick = NewRect("T", cell);
                Place(tick, new Vector2(0, 0.5f), new Vector2(10, 10), Vector2.zero);
                var timg = tick.gameObject.AddComponent<Image>();
                timg.sprite = ChromeArt.Mark("tick");
                timg.preserveAspect = true;
                timg.raycastTarget = false;
                timg.enabled = false;

                rows.Add((mimg, text, timg));
            }
            LayOutStrip(rows);
            // What the bench still writes the bottle's name to. Not drawn: the measuring glass
            // names every bottle that went in, in its own band.
            var title = NewText("H", strip, _body, 8, TextAnchor.MiddleCenter, CounterFinish.Current.Accent);
            title.gameObject.SetActive(false);
            return title;
        }

        /// <summary>Stands the strip's steps side by side and centred, each as wide as its own
        /// words. Called again whenever a step's words change (the third names the method).</summary>
        private static void LayOutStrip(List<(Image icon, Text label, Image tick)> rows)
        {
            const float Socket = StepNumberW + 16f, Gap = 6f, TickGap = 4f, TickW = 10f, Between = 28f;
            var widths = new float[rows.Count];
            float total = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                widths[i] = Socket + Gap + rows[i].label.preferredWidth + TickGap + TickW;
                total += widths[i] + (i > 0 ? Between : 0f);
            }
            float x = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                var cell = (RectTransform)rows[i].label.transform.parent;
                cell.anchoredPosition = new Vector2(x, 0f);
                cell.sizeDelta = new Vector2(widths[i], cell.sizeDelta.y);
                var tick = (RectTransform)rows[i].tick.transform;
                tick.anchoredPosition = new Vector2(Socket + Gap + rows[i].label.preferredWidth + TickGap, 0f);
                x += widths[i] + Between;
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
            !_capped ? UIText.T("bench.shaker.owed.cap") : UIText.T("bench.shaker.owed.mix");

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
                string wasThird = _stepRows[2].label.text;
                _stepRows[2].label.text =
                    method == PrepMethod.Shaken ? UIText.T("bench.shaker.step.shake")
                    : method == PrepMethod.Stirred ? UIText.T("bench.shaker.step.stir")
                    : method == PrepMethod.Built ? UIText.T("bench.shaker.step.built")
                    : UIText.T("bench.shaker.step.mix");
                if (_stepRows[2].label.text != wasThird) LayOutStrip(_stepRows);
                // ...and its picture (2026-09-16): the thrown tin, the spoon, or the lid for a drink built as it is.
                var pic = ChromeArt.Mark(method == PrepMethod.Stirred ? "step_stir" : method == PrepMethod.Built ? "step_cap" : "step_shake");
                if (_stepRows[2].icon.sprite != pic) _stepRows[2].icon.sprite = pic;
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
        /// THE MEASURE'S CONTENTS (2026-09-13, the author: "doluluk barı büyütülsün ve hangi
        /// alkol olduğu ismi ve küçük görseliyle gözüksün, % gösteren sayı doluluk barının
        /// üstünde olsun ve doluluk miktarına göre sayısı o barın üstünde büyüsün, o sayının
        /// tipi siyah kenarlıklı içi beyaz olacak").
        ///
        /// Each bottle that went in is a band of its own liquid colour, CUT TO THE TIN'S
        /// SILHOUETTE — and it is named BESIDE the tin at its band's height, with the bottle's
        /// own small picture, on the rig's Labels rect outside the silhouette's mask, where the
        /// taper cannot cut a word in half. The vessel's reading rides the liquid: the figure
        /// stands on the surface, white ringed in black, and grows a size as the vessel fills
        /// (16, 24, 32 — the faces' own multiples).
        ///
        /// One drawing for both benches: the tin's measure and the glass's are the same object
        /// with different contents.
        /// </summary>
        private void FillGauge(RectTransform bar, GlassContents glass, TycoonRun run, bool labelsLeft)
        {
            foreach (Transform child in bar) Destroy(child.gameObject);
            // bore → Cavity (the mask) → the rig, which carries the labels and the reading.
            var rig = bar.parent != null ? bar.parent.parent as RectTransform : null;
            var host = rig != null ? rig.Find("Labels") as RectTransform : null;
            if (host != null) foreach (Transform child in host) Destroy(child.gameObject);
            float h = bar.rect.height, y = 0f;
            var bands = new List<(Image img, Color tone)>();   // for BlendGauge: the bands and their own colours
            _gaugeBands[bar] = bands;
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                float share = (float)(glass.RatioOf(id) * glass.FillFraction);   // of the VESSEL
                float segH = share * h;
                var tone = UITheme.LiquidColor(card?.Info?.Style, card?.Type ?? IngredientType.Spirit);
                var seg = GaugeBand(bar, $"S_{id}", segH, y, tone);
                bands.Add((seg.GetComponent<Image>(), tone));
                // THE MENISCUS: one lit row at the top of each measure.
                if (segH >= 3f)
                {
                    var top = NewRect("M", seg);
                    top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one;
                    top.pivot = new Vector2(0.5f, 1);
                    top.sizeDelta = new Vector2(0, 2);
                    top.anchoredPosition = Vector2.zero;
                    var mimg = top.gameObject.AddComponent<Image>();
                    mimg.color = Color.Lerp(tone, UITheme.Cream[4], 0.55f);
                    mimg.raycastTarget = false;
                }
                // WHAT IT IS, beside it: a band too thin to hold a line of type is a colour.
                if (host != null && card != null && segH >= 11f)
                    GaugeLabel(host, y, segH, labelsLeft, card,
                        UIText.T("bench.shaker.gauge.band",
                            ("name", UIText.Caps(UIText.Data("bottle", card.Id, "name", card.Name ?? id)).Split(' ')[0]),
                            ("share", share.ToString("P0"))));
                y += segH;
            }

            // THE READING, ON THE SURFACE.
            var totalRt = rig != null ? rig.Find("Total") as RectTransform : null;
            var total = totalRt != null ? totalRt.GetComponent<Text>() : null;
            if (total != null)
            {
                float fill = Mathf.Clamp01((float)glass.FillFraction);
                total.fontSize = LanguageFonts.Size(total.font, fill < 0.34f ? 16 : fill < 0.67f ? 24 : 32);
                total.text = Mathf.RoundToInt(fill * 100f) + "%";
                float boreFoot = rig.rect.height * (1f - ChromeArt.ShakerGaugeCavity.y);
                totalRt.anchoredPosition = new Vector2(0f, boreFoot + y + 4f);
            }
            // Layers while unmixed, one colour once mixed (2026-09-16): what Core says of the tin, on both benches.
            BlendGauge(bar, run.IsMixed ? 1f : 0f);
        }

        private RectTransform GaugeBand(RectTransform bar, string name, float height, float y, Color fill)
        {
            var seg = NewRect(name, bar);
            seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(1, 0);
            seg.pivot = new Vector2(0.5f, 0);
            seg.sizeDelta = new Vector2(0, height);
            seg.anchoredPosition = new Vector2(0, y);
            var img = seg.gameObject.AddComponent<Image>();
            img.color = fill;
            img.raycastTarget = false;
            return seg;
        }

        /// <summary>A band's name, out in the air beside the tin level with the band: the
        /// bottle's small picture against the tin, the outlined name and share past it.</summary>
        private void GaugeLabel(RectTransform host, float y, float segH, bool onLeft,
                                IngredientCard card, string text)
        {
            float side = onLeft ? 0f : 1f, sign = onLeft ? -1f : 1f;
            float mid = y + segH * 0.5f;
            float iconH = Mathf.Min(22f, segH), iconW = 12f;

            var icon = NewRect("Icon", host);
            icon.anchorMin = icon.anchorMax = new Vector2(side, 0f);
            icon.pivot = new Vector2(onLeft ? 1f : 0f, 0.5f);
            icon.sizeDelta = new Vector2(iconW, iconH);
            icon.anchoredPosition = new Vector2(sign * GaugeLabelGap, mid);
            var iimg = icon.gameObject.AddComponent<Image>();
            iimg.sprite = ItemArt.Bottle(card);
            iimg.preserveAspect = true;
            iimg.raycastTarget = false;
            iimg.enabled = iimg.sprite != null;

            var label = NewText("L", host, _body, 8,
                onLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Color.white);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(side, 0f);
            rt.pivot = new Vector2(onLeft ? 1f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(170f, 12f);
            rt.anchoredPosition = new Vector2(sign * (GaugeLabelGap + iconW + 4f), mid);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            Outlined(label, 1f);
            label.text = text;
        }

        // ── THE MEASURE IS THE TIN AGAIN (2026-09-13, second pass) ──────────────
        //
        // The author: "Yeni doluluk göstergesini beğenmedim, önceki shaker silüetinde olmasını
        // istiyordum." The measuring glass of the morning is gone and the silhouette traced off
        // the tin's own art is back (ChromeArt.ShakerOutline / ShakerSolid) — at the size the
        // measure grew to, with the contents it gained: bands cut to the tin, each named beside
        // it with its bottle's picture, and the growing outlined figure on the surface.

        /// <summary>Where the measure stands on both benches (panel-centred), right of the work.</summary>
        private static readonly Vector2 MeasureAt = new Vector2(485f, -109f);   // -87 until the props came down
        /// <summary>136x300: the shaker's own 82:181, at the height the measure grew to.</summary>
        private static readonly Vector2 MeasureSize = new Vector2(136f, 300f);

        /// <summary>
        /// The standing measure: the shaker in outline with the drink cut to its silhouette.
        /// Returns the BORE — the rect the contents are stacked in, the tin's collar to its
        /// floor — so <see cref="FillGauge"/> never has to know how the instrument is built.
        /// </summary>
        private RectTransform BuildStandingGauge(RectTransform panel, Vector2 at,
                                                 Vector2 size, string head)
        {
            var rig = NewRect("MixTrack", panel);
            Place(rig, new Vector2(0.5f, 0.5f), size, at);
            // It takes a press, so a click on the measure is not a click off the bench.
            var catcher = rig.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0.001f);
            rig.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // The contents first and the outline over them, CUT TO THE SILHOUETTE: the mask is
            // the outline's own silhouette, so the two never disagree about where the wall is.
            var maskRt = NewRect("Cavity", rig);
            Stretch(maskRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mimg = maskRt.gameObject.AddComponent<Image>();
            mimg.sprite = ItemArt.Load("gauge_tin_solid") ?? ChromeArt.ShakerSolid((int)size.x, (int)size.y);
            mimg.preserveAspect = true;
            mimg.raycastTarget = false;
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            // The tin's dark inside, so an empty measure reads as an empty tin and not as a hole.
            var inside = NewRect("Inside", maskRt);
            Stretch(inside, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var inImg = inside.gameObject.AddComponent<Image>();
            inImg.color = new Color(0.03f, 0.02f, 0.05f, 0.55f);
            inImg.raycastTarget = false;

            var cavity = ChromeArt.ShakerGaugeCavity;    // top / bottom, as fractions from the top
            var bore = NewRect("MixSegs", maskRt);
            Stretch(bore, new Vector2(0, 1f - cavity.y), new Vector2(1, 1f - cavity.x),
                    Vector2.zero, Vector2.zero);
            // The names live OUTSIDE the mask, on a rect with the bore's anchors.
            var labels = NewRect("Labels", rig);
            Stretch(labels, new Vector2(0, 1f - cavity.y), new Vector2(1, 1f - cavity.x),
                    Vector2.zero, Vector2.zero);

            var shell = NewRect("Outline", rig);
            Stretch(shell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var simg = shell.gameObject.AddComponent<Image>();
            simg.sprite = ItemArt.Load("gauge_tin") ?? ChromeArt.ShakerOutline((int)size.x, (int)size.y);
            simg.preserveAspect = true;
            simg.raycastTarget = false;

            // The reading: white, ringed in black, standing on the surface (FillGauge moves it).
            var total = NewText("Total", rig, _display, 16, TextAnchor.LowerCenter, Color.white);
            total.rectTransform.anchorMin = total.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            total.rectTransform.pivot = new Vector2(0.5f, 0f);
            total.rectTransform.sizeDelta = new Vector2(size.x + 90f, 40f);
            total.rectTransform.anchoredPosition = new Vector2(0f, size.y * (1f - cavity.y) + 4f);
            total.horizontalOverflow = HorizontalWrapMode.Overflow;
            total.verticalOverflow = VerticalWrapMode.Overflow;
            total.raycastTarget = false;
            Outlined(total, 2f);
            total.text = "0%";

            // Which vessel it measures, under the tin's foot.
            var label = NewText("Head", rig, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            label.rectTransform.anchorMin = new Vector2(0, 0);
            label.rectTransform.anchorMax = new Vector2(1, 0);
            label.rectTransform.pivot = new Vector2(0.5f, 1);
            label.rectTransform.offsetMin = new Vector2(0, -16);
            label.rectTransform.offsetMax = new Vector2(0, -4);
            label.text = head;
            label.raycastTarget = false;
            var cut = label.gameObject.AddComponent<Shadow>();
            cut.effectColor = new Color(0f, 0f, 0f, 0.85f);
            cut.effectDistance = new Vector2(1f, -1f);
            return bore;
        }

        private string ShakerLine(TycoonRun run)
        {
            if (run.Glass.IsEmpty) return UIText.T("bench.shaker.line.empty");
            var parts = new List<string>();
            foreach (var id in run.Glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                parts.Add(UIText.T("bench.shaker.line.part",
                    ("name", UIText.Caps(UIText.Data("bottle", id, "name", card?.Name ?? id))),
                    ("share", run.Glass.RatioOf(id).ToString("P0"))));
            }
            // The list separator is a symbol and stays here (PLAN_localization_L1 §4).
            return UIText.T("bench.shaker.line.contents",
                ("fill", run.Glass.FillFraction.ToString("P0")), ("parts", string.Join(", ", parts)));
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
            _shakerReadout.text = UIText.T("bench.shaker.tin_full");
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
            _shakerTitle.text = UIText.Caps(UIText.Data("bottle", _focusBottle.Id, "name", _focusBottle.Name));
            // In the hand it stands OPEN (the author, 2026-08-01): the pour scene uses the
            // capless variant when one exists. Same canvas as the closed art, so the liquid
            // mask and the mouth line all stay put; styles missing an open shot fall back.
            var bottleSprite = ItemArt.BottleOpen(_focusBottle);
            // v4: the sandwich draws the bottle — back, drink, front — and the flat body
            // image stands down; pre-v4 cards keep the flat body and the stencil fill.
            var plates = PourPlates();
            _pourArt?.Show(plates);   // null across a domain reload mid-play; rebuilt with the UI
            _pourBottleBody.enabled = plates == null;
            _pourBottleBody.sprite = bottleSprite;
            _pourBottleBody.color = bottleSprite != null ? Color.white : colour;
            // It stands on the bench at the size its own drawing asks for, and it is measured
            // against its CLOSED art: an open bottle is the same bottle with the cap off, so
            // it must not grow to fill the space the cap left (VesselArt).
            // A v4 bottle is gauged against ITSELF: its open master is the whole canvas, there
            // is no separate closed sheet to measure the cap against.
            // A SEALED CONTAINER IS A v4 MASTER TOO (2026-09-06, the author: "limonata ve diğer
            // meyve suları görseldeki gibi gözüküyor hem boyutu yanlış hem görsel yok sadece
            // aydınlatması var"). Cartons and cans get ONE sprite by design (brief.SEALED: no
            // cavity, so no back/mask/front sandwich) — and the bench read "no sandwich" as
            // "pre-v4", fell through to the CELLAR thumbnail and stretched a 32x64 picture over
            // 180x384. What decides the scale is the CANVAS the drawing came on, not whether it
            // arrived in three plates: a 96x192 master stands at exactly 2x either way.
            bool master = bottleSprite != null && Mathf.Approximately(bottleSprite.rect.height, 192f);
            _pourMouth = VesselArt.StandOn(_pourVessel, new Vector2(0.5f, 0f), bottleSprite,
                BottleH, Vector2.zero,
                plates != null || master ? bottleSprite : ItemArt.Bottle(_focusBottle),
                fixedScale: plates != null || master ? BottleH / 192f : 0f);
            // The bottle is taken where it is drawn: the open master (or the sandwich's front
            // plate), hit-tested on its alpha. A card with no drawing at all keeps the slot.
            var gripArt = bottleSprite ?? (plates != null ? plates.Front ?? plates.Back : null);
            bool readable = gripArt != null && gripArt.texture != null && gripArt.texture.isReadable;
            if (_pourGrip != null)
            {
                _pourGrip.sprite = readable ? gripArt : null;
                _pourGrip.alphaHitTestMinimumThreshold = readable ? 0.1f : 0f;
                _pourGrip.enabled = readable;
            }
            if (_pourSlotHit != null) _pourSlotHit.raycastTarget = !readable;
            PushPourFill(run);
        }

        // ── the shaker focus stage: the tilt-pour ────────────────────────────────

        private void RefreshShaker()
        {
            var run = Run;
            if (run == null) return;
            // THE BENCH CAN BE WALKED INTO EMPTY-HANDED (2026-09-06). It used to be
            // reachable only by carrying a bottle here, so this returned early without a
            // focus and the bench was left half-dressed — the bottle prop still wearing the
            // placeholder tint it is built with, which drew as a flat teal box beside the
            // tin. The tin standing on the counter is a door now, and a player who walks
            // back in to shake what is already in the tin carries nothing: the bottle prop
            // stands DOWN, and everything else about the bench is set up as it always was.
            bool inHand = _focusBottle != null;
            if (_pourBottle != null && _pourBottle.gameObject.activeSelf != inHand)
                _pourBottle.gameObject.SetActive(inHand);
            // THE SPOON IS EARNED (2026-09-16, the author: "Kaşık isteyen ilk tarif alındıktan
            // sonra kaşık otomatik olarak sahneye eklenir"). Core says whether there is one
            // behind the bar (TycoonRun.SpoonUnlocked: the first stirred page on the menu
            // brings it); until then the towel and the spoon are simply not on the bench,
            // and the tin is shaken. The bench asks every time it opens, so the night the
            // page is bought the spoon is standing there in the morning.
            bool spoon = run.SpoonUnlocked;
            if (_spoonRt != null && _spoonRt.gameObject.activeSelf != spoon) _spoonRt.gameObject.SetActive(spoon);
            if (_napkinRt != null && _napkinRt.gameObject.activeSelf != spoon) _napkinRt.gameObject.SetActive(spoon);
            if (!spoon) _spoonHeld = false;
            if (_bottleShadow != null && _bottleShadow.gameObject.activeSelf != inHand)
                _bottleShadow.gameObject.SetActive(inHand);
            if (inHand) PushFocusBottleArt(run);
            else if (_shakerTitle != null) _shakerTitle.text = UIText.T("bench.shaker.title.tin");
            SayShaker(ShakerLine(run));
            ConfigureBottleHand();
            _shakerFluid.Clear();
            _shakerFluid.ClearStreamColor();      // a new visit pours nothing yet
            _shakerFluid.SetColor(DrinkColor(run.Glass));
            _shakerVessel.anchoredPosition = _shakerHome;
            _shakerVessel.localRotation = Quaternion.identity;
            _tinCatchX = _shakerHome.x; _tinCatchV = 0f; _tinAx = 0f; _tinSway = 0f; _tinSwayV = 0f; _tinCatchLast = _shakerHome.x;
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
            // Stage entry: a tin that ARRIVED shaken wears its frost; the bands blend by what Core says of the mix.
            _workLevel = run.Glass.HasPreparation("shaken") ? (float)run.ShakeEnergy : 0f;
            SetFrost(_workLevel);
            BlendGauge(_shakerMixBar, run.IsMixed ? 1f : 0f);
            LightLiftLadder(0);
        }

        /// <summary>
        /// One frame of the tilt-pour. The bottle follows the mouse while grabbed; the
        /// higher it is lifted the further it leans toward the shaker (GDD 24 §2). Liquid
        /// runs from the mouth only when it is tilted over the shaker's opening.
        /// </summary>
        private void UpdateTiltPour(TycoonRun run)
        {
            if (Mouse.current == null || _focusBottle == null) return;

            // A grab already in flight must not survive the lid going on either — the bottle
            // still walks home behind the faded bench rather than freezing where it was.
            if (_capped && _bottleGrabbed) { _bottleGrabbed = false; _bottleHand.Release(); }

            // Release when the button comes up, wherever the cursor is.
            if (_bottleGrabbed && !Mouse.current.leftButton.isPressed)
            {
                _bottleGrabbed = false;
                _bottleHand.Release();
            }
            // HELD, THE GLOW LETS GO (2026-09-13): its rise, sway and grow moved the drawing under
            // a hand trying to hold it still, and a tilt that carried the steel off the pointer
            // dropped the glow mid-pour. It eases back when the bottle is let go.
            if (_pourGlow != null)
            {
                _pourGlow.Rise = _bottleGrabbed ? 0f : 4f;
                _pourGlow.Sway = _bottleGrabbed ? 0f : 1.4f;
                _pourGlow.Grow = _bottleGrabbed ? 1f : 1.05f;
            }

            // The hand moves every frame, held or not: let go and the bottle goes home.
            Vector2? pointer = null;
            if (_bottleGrabbed && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 ptr))
                pointer = ptr;
            {
                float halfW = _pourSurface.rect.width * 0.5f;
                float halfH = _pourSurface.rect.height * 0.5f;
                // THE MOUTH STAYS WHERE THE TIN CAN GO (2026-09-14, second pass): a bottle tipped left carries its mouth
                // up to its hold's lever left of the hand, so the hand stops that far in from the left edge.
                float leftmost = -halfW + TinCatchInset - 70f + _bottleHand.HoldBelowSpout;
                _bottleHand.Step(Time.deltaTime, pointer,
                    Rect.MinMaxRect(Mathf.Max(-halfW + 30f, leftmost), -halfH + 20f, halfW - 30f, halfH + HandAbove));
                _bottleHand.Apply(_pourBottle);
                // The dial under the rest shows the step the lift is on (2026-09-16).
                LightLiftLadder(_bottleGrabbed && _bottleHand.Held ? BottlePour.LiftStep(_bottleHand.PourLift01) : 0);
            }
            StepBenchLight();

            bool pourNow = false;
            if (_bottleGrabbed && _bottleHand.Held)
            {
                float tilt = _bottleHand.Tilt;                     // degrees, counter-clockwise = leans left

                // Where the mouth ends up: the bottle's CAP, turned about the hand's grip on the
                // neck. It used to be the top centre of the grab plate, which is the cap only for
                // art that fills its sheet — the juice cartons poured from a point some 80px
                // above their own spout (the author, 2026-08-11: "sıvının çıkış yerini kapak
                // olarak ayarla"). VesselArt reads it off the drawing instead.
                Vector2 mouth = _bottleHand.SpoutNow;

                var (opening, mouthHalf) = TinMouth();
                // THE TIN CATCHES (2026-09-14): it slides under the stream wherever the bottle is tipped
                // (StepTinCatch), so there is no mouth to aim at any more; a stream out of its reach bends to it.
                bool over = true;
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
                // THE LIP DECIDES, not a fixed 42 degrees (2026-09-11): Core says at what lean
                // THIS bottle, at its own level, starts to run, and how much it gives past that.
                var held = run.Shelf.Find(_focusBottle.Id);
                double level = held != null && held.Capacity > 0 ? held.Remaining / held.Capacity : 0;
                // BY THE LIFT, IN STEPS (2026-09-14, BottlePour.LiftShare): 1, 1, 2, 3, 5, 8, 13, 21, 34 over the lift past level.
                _bottleShare = tilt >= PourFromTilt ? (float)BottlePour.LiftShare(_bottleHand.PourLift01, level) : 0f;
                bool running = _bottleShare > 0f;
                pourNow = running && over && !full;
                if (full && running && over) ShowShakerFull();

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
                    // STRAIGHT DOWN (2026-09-14, second pass): the stream used to be bent toward the tin, and every drop
                    // kept the bend it left with while the tin moved — the curve of beads that missed. It falls where the
                    // mouth is, and the tin goes there (StepTinCatch follows the liquid).
                    var streamVel = new Vector2(0f, -225f);
                    // A rope as thick as the pour is heavy: the lip's trickle is a thread, a
                    // bottle tipped right over a full rope (the girth P3 will take from Core's
                    // own delivered volume).
                    // ...and a THREAD when little is running (2026-09-13, the author: "az
                    // dökülüyorken şişenin ucundan dökülen sıvı az gözükmeli").
                    _shakerFluid.EmitStream(mouth, streamVel, Time.deltaTime, 0.2f + 1.3f * _bottleShare);
                    // What the pour sounds like (2026-09-15): how far it drops onto the drink, and where on the bench.
                    _pourFall01 = FallOf(mouth.y, TinDrinkTopY(run));
                    _pourPan = PanOf(mouth.x, _pourSurface);
                }
            }

            if (pourNow)
            {
                if (run.PouringId == null) run.BeginPour(_focusBottle.Id);
                run.PourTickLift(Time.deltaTime, _bottleHand.PourLift01);   // Core's steps, Core's rate
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

            if (pourNow) _shakerLoopWanted = "pour_tin";    // the stage frame drives the source
            if (pourNow) RefreshShakerMixBar(run);          // the gauge follows the stream
            _pouring = pourNow;

            // THE TIN FOLLOWS THE LIQUID (2026-09-14, second pass): where the next drop in the air comes down at its
            // mouth; before anything is falling, the mouth of a bottle well on its way over; home otherwise — and a pour
            // let go of mid-stream is still caught.
            float catchTo = _shakerHome.x;
            if (!_capped && _capT <= 0f)
            {
                float rimY = TinMouth().Centre.y;
                if (_shakerFluid.NextLandingX(rimY, out float falling)) catchTo = falling;
                else if (_bottleGrabbed && _bottleHand.Held && _bottleHand.Tilt > 40f) catchTo = _bottleHand.SpoutNow.x;
            }
            StepTinCatch(catchTo);

            // Every frame, not only the pouring ones: the bottle in hand is the same bottle
            // that stands on the rail, and it drains while you hold it over the tin. Setting
            // it once on the way in would show the level it had when you picked it up.
            PushPourFill(run);
        }

        private float _tinCatchX = -120f, _tinCatchV, _tinAx, _tinSway, _tinSwayV, _tinCatchLast = -120f;
        private float _pourFall01, _pourPan;   // the bottle's pour, heard: how far it drops, where on the bench (2026-09-15)

        /// <summary>Where a stream into the open tin lands, surface-local: the top of the drink in its cavity, the
        /// cavity's floor when it is empty — read off the tin's rect the way PushShakerPool reads it, because an empty
        /// tin has no pool to ask (2026-09-15).</summary>
        private float TinDrinkTopY(TycoonRun run)
        {
            var c = _shakerVessel.anchoredPosition;
            float h = _shakerVessel.rect.height;
            return c.y - h * 0.5f + h * (CavityFloor + (CavityRim - CavityFloor) * (float)run.Glass.FillFraction);
        }
        /// <summary>How far in from the surface's edges the catching tin stops (its centre, surface-local): the whole
        /// bench (2026-09-14, second pass) — it passes behind the lid, the spoon and the measure rather than leave a
        /// stream it cannot reach. The hand keeps the mouth inside it (UpdateTiltPour).</summary>
        private const float TinCatchInset = 92f;
        /// <summary>How far under the drawn mouth the stream is swallowed: past the front plate's lip (26 sheet rows,
        /// 52 units, at the middle), so the stream is seen going in — over the tin's back, under its front (2026-09-14,
        /// the author: "şişelerden dökülen sıvı shaker.png nin önünde shaker_Front.png nin arkasında olacak").</summary>
        private const float TinStreamDepth = 56f;

        private void StepTinCatch(float targetX)
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;
            float reach = _pourSurface.rect.width * 0.5f - TinCatchInset;
            StepCatch(ref _tinCatchX, ref _tinCatchV, ref _tinAx, ref _tinSway, ref _tinSwayV, ref _tinCatchLast,
                Mathf.Clamp(targetX, -reach, reach), dt);
        }

        /// <summary>The focus bottle's v4 plates, resolved once per card: PushPourFill runs
        /// every frame of the pour, and ItemArt never caches a miss, so a sealed vessel in the
        /// hand was a Resources.Load per frame (2026-09-04 audit).</summary>
        private ItemArt.BottlePlates PourPlates()
        {
            if (!ReferenceEquals(_pourPlatesFor, _focusBottle))
            {
                _pourPlates = _focusBottle != null ? ItemArt.Plates(_focusBottle) : null;
                _pourPlatesFor = _focusBottle;
            }
            return _pourPlates;
        }

        /// <summary>How full the bottle in hand is, read off the shelf it came from.</summary>
        private void PushPourFill(TycoonRun run)
        {
            if (_pourFill == null) return;
            if (_focusBottle == null) { _pourFill.Hide(); _pourArt?.Hide(); return; }
            var stock = run?.Shelf?.Find(_focusBottle.Id);
            double fraction = stock != null && stock.Capacity > 0 ? stock.Remaining / stock.Capacity : 0.0;
            var tone = UITheme.LiquidColor(_focusBottle.Info?.Style, _focusBottle.Type);
            if (_pourArt != null && PourPlates() != null)
            {
                // THE SURFACE STAYS LEVEL (PLAN §12): the tilt is the grab plate's z rotation,
                // and the drink counter-rotates by it inside the glass.
                _pourFill.Hide();
                _pourArt.SetLevel(tone, fraction, _pourBottle.localRotation.eulerAngles.z);
                return;
            }
            _pourFill.Show(_pourBottleBody.sprite, tone, fraction);
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
        private float TinFootY => _shakerHome.y - TinH * 0.5f + 14f * (TinH / 358f);

        /// <summary>The brimful drink's oval, in tin sheet pixels in from the drawing's opaque bounds: two
        /// inside the opening's dark inside (measured on tin_open, 2026-09-14).</summary>
        private const float TinMouthInsetX = 5f, TinMouthTopInset = 5f, TinMouthBottomInset = 28f;

        /// <summary>How full the tin has to be before any of it shows at the mouth.</summary>
        private const float BrimFill = 0.97f;
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

        /// <summary>
        /// WHERE THE TIN IS ACTUALLY OPEN, measured off the drawing rather than taken as the
        /// top of the rect it is drawn in (2026-09-06). The two used to be near enough the
        /// same thing; growing the tin to the glass's proportion pulled them 118 units apart,
        /// and the aim went with the box — you had to hold the bottle a hand above the rim
        /// before anything poured, which is how the pour smoke test found it. Returns the rim
        /// in the surface's own coordinates, and how far either side of it counts as "in".
        /// </summary>
        /// <summary>
        /// Gives the hand this bottle: its rest, its measured cap, and a lift range fitted to the
        /// room above it. A bottle's neck stands high on this bench, so the lift a full tilt takes
        /// is whatever is left under the top of the surface (between 90 and the old 200) — and the
        /// grip depth is kept inside what that lift allows, so raising the hand can never lower
        /// the mouth (PourHand.Configure guards the same inequality).
        /// </summary>
        private void ConfigureBottleHand()
        {
            if (_pourBottle == null) return;
            // THE LIFT IS THE POINTER'S NOW (2026-09-13, PourHand): the room over a bottle's neck
            // no longer squeezes it to 90, so every bottle gets the whole LiftRange, fitted under
            // the surface's top when it is taken.
            // Held by its MIDDLE from level on (2026-09-14): half way from the drawn cap to the foot.
            float footBelowPivot = _bottleRest.y - BottleFootY;
            _bottleHand.Configure(_bottleRest, _pourMouth, BottleGripDepth, LiftRange, MaxTilt,
                (_pourMouth.y + footBelowPivot) * 0.5f);
            _bottleHand.Apply(_pourBottle);
        }

        private (Vector2 Centre, float Half) TinMouth()
        {
            var rt = _shakerVessel;
            float top = rt.anchoredPosition.y + rt.rect.height * 0.5f;
            var sp = _shakerBodyImg != null ? _shakerBodyImg.sprite : null;
            if (sp == null || sp.rect.height < 1f)
                return (new Vector2(rt.anchoredPosition.x, top), 78f);
            var ob = ItemArt.OpaqueBounds(sp);
            float k = rt.rect.height / sp.rect.height;              // the drawing fills the rect
            float drop = (sp.rect.height - (ob.y + ob.height)) * k; // empty canvas over the rim
            // The mouth's own width plus a little slack: threading a rim is not the game.
            // TURNED WITH THE TIN (2026-09-14): a tin rocking as it catches carries its mouth round its centre.
            var up = rt.localRotation * new Vector3(0f, rt.rect.height * 0.5f - drop, 0f);
            return (rt.anchoredPosition + (Vector2)up, ob.width * k * 0.5f + 8f);
        }

        /// <summary>Places the shaker's pooled liquid from the glass interior and its live fill,
        /// plus a vertical slosh <paramref name="bob"/> on the surface (all surface-local px).</summary>
        private void PushShakerPool(TycoonRun run, float bob)
        {
            // The tin's mouth is always a hole the stream goes into (2026-09-11): below the brim
            // the steel shows no drink, and with nothing to land on the stream used to fall
            // through the tin and out under it — the "drips to the floor" the author saw.
            var (mouthAt, mouthHalfW) = TinMouth();
            _shakerFluid.SetSink(mouthAt + new Vector2(0f, -TinStreamDepth), mouthHalfW);
            if (_tinSurface != null) _tinSurface.enabled = false;   // shown below, only at the brim
            // The drink at the brim and the stream into it are drawn in the tin's own pixels.
            var tinArt = _shakerBodyImg != null ? _shakerBodyImg.sprite : null;
            if (tinArt != null && tinArt.rect.height >= 1f)
            {
                var tr = _shakerVessel;
                _shakerFluid.SetPixelGrid(tr.rect.height / tinArt.rect.height, new Vector2(
                    tr.anchoredPosition.x - tr.rect.width * tr.pivot.x,
                    tr.anchoredPosition.y - tr.rect.height * tr.pivot.y));
            }
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
            // THE TIN IS NOT A GLASS (2026-09-06, the author: "pour sahnesinde shaker
            // içerisinde koyduğumuz sıvı gözükmesin sadece tamamı dolduktan sonra ucundan
            // gözükebilir, artık shakerimiz şeffaf değil"). Steel is opaque and this one was
            // being drawn as a cutaway — the level showing through the metal — which was a
            // 2026-07-24 decision the author has now overturned. What is left is the one
            // thing you would really see: a tin filled to the brim shows the drink at its
            // mouth. Everything below the brim is simply not visible, and the readout above
            // the bench is where the level is read instead.
            if (fill < BrimFill) { _shakerFluid.ClearPool(); return; }
            // IN THE MOUTH (2026-09-08, the author's tin_open_Front: "sıvının önünü ekle,
            // böylece sıvı şişenin içerisinde gibi gözükecek"). The tin's front wall is now
            // a plate drawn OVER the fluid, its top edge at the front lip (0.615 of the
            // sprite, the cavity rim), and the mouth's interior — 0.615 to 0.73, measured
            // on the master — is what it leaves open. The brimful pool used to sit a band
            // under the rim, which put the whole of it behind the plate; it now stands in
            // the mouth, from just over the front lip to under the back rim, so a brimful
            // tin shows its drink through the opening and nowhere else. The plate's ring
            // hides the metaball's proud edge on the front side; MouthTop keeps it under
            // the back rim.
            //
            // AN OVAL IN THE MOUTH (2026-09-14, the author: "shakerın içindeki sıvı shakera tam oturmuyor ve
            // 2.5d perspektifi de düşününce sıvının tepesinin oval olması gerekiyor"). That pool was a box
            // from the front lip to under the back rim, and read as a flat trapezoid laid over the rim. The
            // drink's top is the ellipse inside the opening, a few pixels in from the drawing ("sıvı dolum
            // hizası ... en dışındaki pixelden birkaç pixel içeride"): measured on tin_open (116x208) the
            // opening's dark inside runs x 20..95 and rows 58..84 — three pixels in from the drawing's
            // sides and five under its top — and the drink stands two pixels inside that.
            _shakerFluid.ClearPool();
            if (_tinSurface != null && tinArt != null && tinArt.rect.height >= 1f)
            {
                var ob = ItemArt.OpaqueBounds(tinArt);            // sheet px from the bottom-left
                float k = _shakerVessel.rect.height / tinArt.rect.height;
                float drawnTop = ob.y + ob.height;
                float x0 = ob.x + TinMouthInsetX, x1 = ob.x + ob.width - TinMouthInsetX;
                float y1 = drawnTop - TinMouthTopInset, y0 = drawnTop - TinMouthBottomInset;
                var srt = _tinSurface.rectTransform;
                srt.sizeDelta = new Vector2((x1 - x0) * k, (y1 - y0) * k);
                srt.anchoredPosition = new Vector2(((x0 + x1) * 0.5f - tinArt.rect.width * 0.5f) * k,
                                                   ((y0 + y1) * 0.5f - tinArt.rect.height * 0.5f) * k + bob);
                var body = DrinkColor(run.Glass);
                _tinSurface.color = new Color(Mathf.Lerp(body.r, 1f, 0.24f), Mathf.Lerp(body.g, 1f, 0.24f),
                                              Mathf.Lerp(body.b, 1f, 0.24f), 1f);
                _tinSurface.enabled = true;
            }
            // The cap's placement belongs to UpdateCap now — it rests on the bench until
            // you drop it on the tin, so it must not be glued to the vessel here.
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
            // The tin answers the hand only once the lid is on it (see BuildShakerPanel).
            if (_tinGlow != null && _tinGlow.enabled != _capped) _tinGlow.enabled = _capped;
            // ...and the lid stops being its own object the moment it is on (2026-09-09).
            if (_capGlow != null && _capGlow.enabled == _capped) _capGlow.enabled = !_capped;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var mouse = Mouse.current;

            if (_capGrabbed)
            {
                // The cap's art lives in the top of its canvas, so centre THAT on the cursor —
                // grabbing it used to pin the mouse to the empty space beneath the lid.
                float lift = _shakerTop.rect.height * CapArtOffset;
                if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _pourSurface, mouse.position.ReadValue(), null, out Vector2 local))
                    _capPos = Vector2.Lerp(_capPos, local - new Vector2(0, lift) + _capGrabOffset, 1f - Mathf.Exp(-30f * dt));
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _capGrabbed = false;
                    // Anywhere over the tin will do — you should not have to thread the mouth.
                    var tin = _shakerVessel;
                    var d = _capPos + new Vector2(0, lift) - tin.anchoredPosition;
                    bool onTin = Mathf.Abs(d.x) < tin.rect.width * 0.75f
                              && Mathf.Abs(d.y) < tin.rect.height * 0.75f;
                    if (onTin && !run.Glass.IsEmpty) { _capped = true; Sfx.Play("cap_on"); }
                    else _capPos = _capRest;
                }
            }

            _capT = Mathf.MoveTowards(_capT, _capped ? 1f : 0f, dt / 0.45f);
            float e = _capT * _capT * (3f - 2f * _capT);   // smoothstep

            if (!_shaking)
            {
                if (e <= 0.0001f)
                {
                    // OPEN, IT CATCHES (2026-09-14): where StepTinCatch has it, rocked on its foot.
                    var rock = Quaternion.Euler(0f, 0f, _tinSway);
                    _shakerVessel.anchoredPosition = new Vector2(_tinCatchX, _shakerHome.y - TinH * 0.5f)
                        + (Vector2)(rock * new Vector3(0f, TinH * 0.5f, 0f));
                    _shakerVessel.localRotation = rock;
                }
                else
                {
                    // Capped, it eases up to the middle of the old counter line, where the lid and a shaking hand
                    // have room, and stands upright.
                    _shakerVessel.localRotation = Quaternion.Slerp(_shakerVessel.localRotation, Quaternion.identity,
                        1f - Mathf.Exp(-12f * dt));
                    _shakerVessel.anchoredPosition = Vector2.Lerp(
                        _shakerVessel.anchoredPosition,
                        Vector2.Lerp(new Vector2(_tinCatchX, _shakerHome.y), new Vector2(CapCentreX, BenchFootY + TinH * 0.5f), e),
                        1f - Mathf.Exp(-9f * dt));
                }
            }
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
                        NudgeShaker(UIText.T("bench.shaker.nudge.stirred"));
                        break;
                    case PrepMethod.Shaken:
                        NudgeShaker(UIText.T("bench.shaker.nudge.shaken"));
                        break;
                    case PrepMethod.Built:
                        NudgeShaker(UIText.T("bench.shaker.nudge.built"));
                        break;
                    default:
                        if (run.MixRequired && !run.IsMixed)
                            NudgeShaker(UIText.T("bench.shaker.nudge.two_spirits"));
                        else
                            NudgeShaker(UIText.T("bench.shaker.nudge.cap"));
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
            Sfx.Play("blowout", 1f);
            _shakerReadout.text = UIText.T("bench.shaker.blew_up");
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
            _shakerReadout.text = UIText.T("bench.shaker.blew_up");
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
            Sfx.Play("cap_on", 0.55f);
            SayShaker(UIText.T("bench.shaker.lid_off"));
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
                    Sfx.Play("stir_commit", 0.6f);
                    SayShaker(UIText.T("bench.shaker.stirred_line",
                        ("pct", _stirEnergy.ToString("P0")), ("line", ShakerLine(run))));
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
                _spoonRt.anchoredPosition, local + _spoonGrabOffset, 1f - Mathf.Exp(-60f * dt));

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
                          UIText.T("bench.shaker.meter.stir", ("pct", _stirEnergy.ToString("P0"))));
            NudgeShaker(overTin ? UIText.T("bench.shaker.nudge.circles") : UIText.T("bench.shaker.nudge.spoon_over"));
        }

        /// <summary>
        /// The right-edge key out of the bench: lit only when the tin is capped and Core
        /// itself would let the drink leave (<see cref="TycoonRun.CanPourOut"/>). It pulses
        /// once the moment it first comes alive, so the way forward announces itself.
        /// </summary>
        /// <summary>
        /// THE GLASS COMES TO YOU (2026-08-26, the author: "bardaga koyma asamasina artik
        /// ayri bir sahne istemiyorum, shaker kapagi kapatildiktan sonra ekrana otomatik
        /// bardak gelsin").
        ///
        /// There was a key here — TO THE GLASS, lit when Core would let the drink leave the
        /// tin, pulsing once the moment it came alive. It is gone. Capping the tin is the
        /// player SAYING the build is finished, so nothing more should have to be pressed:
        /// the moment the tin is closed AND pourable the glass slides in by itself, on the
        /// same counter, with the same background standing still behind it.
        ///
        /// WHY IT WAITS FOR CanPourOut RATHER THAN FOR THE CAP. A tin holding two spirits
        /// may not leave the shaker unmixed (GDD 21 §14), so a cap is not always the end of
        /// the work — sometimes the shake is. Firing on the cap alone would carry a drink
        /// that Core is about to refuse onto the next bench and strand it there; firing on
        /// "the drink may now be poured" is the same instant for a built drink and the
        /// right one for a shaken one. The bench still says so out loud while it waits.
        ///
        /// ONE BEAT of delay, because a screen that changes on the same frame as the lid
        /// lands reads as the lid having done something else.
        /// </summary>
        private void UpdateToGlass(TycoonRun run)
        {
            bool ready = _capped && !run.Glass.IsEmpty && run.CanPourOut;
            if (!ready)
            {
                _toGlassWait = 0f;
                if (_capped && !run.Glass.IsEmpty)
                    NudgeShaker(UIText.T("bench.shaker.nudge.wants_mix"));
                return;
            }
            // Not while a hand is still on something: a cap released over the tin and a
            // spoon still circling are both "the player is working", and the bench does
            // not move out from under a working hand.
            if (_capGrabbed || _shaking || _spoonHeld || _bottleGrabbed) { _toGlassWait = 0f; return; }
            _toGlassWait += Time.unscaledDeltaTime;
            if (_toGlassWait >= ToGlassBeat) { _toGlassWait = 0f; GoTo(Stage.Serve); }
        }

        /// <summary>How long the closed tin is left standing before the glass arrives.</summary>
        private const float ToGlassBeat = 0.45f;
        private float _toGlassWait;

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
                    else SayShaker(UIText.T("bench.shaker.shaken_line",
                        ("pct", _shakeEnergy.ToString("P0")), ("line", ShakerLine(run))));
                }
                _shaking = false;
                _shakeEnergy = 0;
                _shakerVessel.localRotation = Quaternion.identity;
                Sfx.Play("tin_set_down", 0.7f);
                // Leave the shaker wherever it was set down — no teleport home (2026-07-22).
                _shakerVel = Vector2.zero;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // Cursor travel builds the shake energy.
            float travel = (mouse - _lastShakeMouse).magnitude;
            _lastShakeMouse = mouse;
            // A better tin takes less arm (2026-09-13): the shaker ladder's WorkSpeed.
            _shakeEnergy = Mathf.Clamp01((float)_shakeEnergy
                + travel * (float)run.WorkSpeed("shaker") / ShakeFullTravel);

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

            ShowWorkMeter((float)_shakeEnergy, CounterFinish.Current.Accent,
                          UIText.T("bench.shaker.meter.shake", ("pct", _shakeEnergy.ToString("P0"))));
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
            if (Mathf.Approximately(fromY, _benchCounterTop)) return;
            _benchCounterTop = fromY;
            foreach (var band in _benchCounters)
            {
                if (band == null) continue;
                band.anchorMin = Vector2.zero;
                band.anchorMax = new Vector2(1f, fromY);
                band.offsetMin = Vector2.zero;
                band.offsetMax = Vector2.zero;
            }
            // ...and everything hung from the rail goes with it (the plaques, 2026-09-16).
            float railTop = fromY * _field.rect.height;
            foreach (var (rt, dy) in _railHung)
                if (rt != null) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, railTop - dy);
        }

        /// <summary>The room's counter, zoomed (2026-08-25, the author: "ekran çok boş
        /// gözüküyor, mevcut tezgahın görseline zoom yapılmış gibi gözükmeli"). Every
        /// colour below is SAMPLED from Assets/Art/Backgrounds/counter.png — the slab, its
        /// ridge, and the magenta neon rail that runs the counter's far edge — so the bench
        /// is the same object the room draws, four times closer. Bands, not a texture:
        /// chrome is procedural (14 §3), and a zoomed pixel surface IS flat runs of colour.</summary>
        private static readonly Color BenchSlab = Hex(0x1F1924);
        private static readonly Color BenchRidge = Hex(0x312E3A);

        /// <summary>Which grain the bench tops wear. One line, because "quieter" and
        /// "busier" is a taste call made by eye — see ChromeArt.CounterGrain.</summary>
        private const ChromeArt.CounterGrain BenchGrain = ChromeArt.CounterGrain.Brushed;   // Slate until 2026-09-16 (ChromeArt.CounterGrain)

        /// <summary>How far a gauge caption stands off the band it names. Enough to clear
        /// the shaker's widest point, so no tick starts inside the steel.</summary>
        private const float GaugeLabelGap = 8f;

        /// <summary>The least air allowed between any two drawn edges on the bench. The
        /// props are placed against this rather than by eye, because "nothing overlaps"
        /// is a thing you can check and taste is not.</summary>
        private const float BenchClear = 26f;
        private static readonly Color BenchSeam = Hex(0x17141C);
        private static readonly Color[] BenchRail =
            { Hex(0xD77BBA), Hex(0xB7699F), Hex(0x975885), Hex(0x77476B), Hex(0x573650), Hex(0x372536) };

        private static Color Hex(int v) =>
            new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);

        // ── the counter's dressing (2026-09-16, the author: "bar tezgahının arkasına bir de bar matı koyulabilir
        // yanda dilimlenmiş limonlar ve su lekesi olabilir ... tezgaha ışıklandırma ve yansıma gerekiyor") ────────
        //
        // On the shared stage, under every bench: the room's light lying on the stone under the rail, a rubber bar
        // mat along the back edge, and two lemon wheels in the far corner. All hung from the rail, so they stay on
        // the counter's own line. The water ring and the mirrors are the shaker bench's (BuildShakerPanel).

        private void BuildBenchDressing()
        {
            var sheen = NewRect("Sheen", _benchStage);
            Place(sheen, new Vector2(0.5f, 0f), new Vector2(1180f, 240f), new Vector2(0f, 0f));
            var si = sheen.gameObject.AddComponent<Image>();
            si.sprite = ChromeArt.Halo();
            si.color = new Color(UITheme.Cream[4].r, UITheme.Cream[4].g, UITheme.Cream[4].b, 0.07f);   // Cream[4] at 7%: light, not paint
            si.raycastTarget = false;
            _railHung.Add((sheen, RailBandH - 30f + 240f));

            var mat = NewRect("BarMat", _benchStage);
            Place(mat, new Vector2(0f, 0f), new Vector2(360f, 48f), new Vector2(500f, 0f));
            var mi = mat.gameObject.AddComponent<Image>();
            mi.sprite = ChromeArt.BarMat();
            mi.type = Image.Type.Tiled;          // the rim from the border, the ribs REPEATED across the middle
            mi.pixelsPerUnitMultiplier = 0.5f;   // the 16x12 tile at 2x
            mi.raycastTarget = false;
            _railHung.Add((mat, RailBandH + 8f + 48f));

            var lemon = ItemArt.Load("counter_lemon");
            for (int i = 0; i < 2; i++)
            {
                var wheel = NewRect("Lemon" + i, _benchStage);
                Place(wheel, new Vector2(1f, 0f), new Vector2(64f, 64f), new Vector2(-16f - i * 28f, 0f));
                wheel.pivot = new Vector2(1f, 0f);
                var li = wheel.gameObject.AddComponent<Image>();
                li.sprite = lemon; li.preserveAspect = true; li.raycastTarget = false;
                li.enabled = lemon != null;
                _railHung.Add((wheel, RailBandH + 6f + 64f));
                wheel.SetAsLastSibling();
            }
        }

        private void AddBenchCounter(RectTransform panel, float fromY)
        {
            // It goes in BEHIND EVERYTHING on the panel: a band added after the title is a
            // band drawn over the title, and the panel's own background is a component, so
            // first CHILD is as far back as a child can go.
            var top = NewRect("CounterTop", panel);
            _benchCounters.Add(top);
            RegisterFixed(panel, top);   // the slab is the same slab on both benches
            Stretch(top, Vector2.zero, new Vector2(1f, fromY), Vector2.zero, Vector2.zero);
            top.SetAsFirstSibling();
            var timg = top.gameObject.AddComponent<Image>();
            // GRAIN, NOT A FILL. The slab was one flat colour over a third of the screen
            // — which is what a zoom of a flat sprite honestly is, and it read as one.
            //
            // Marble was the first answer and the author sent it back: what a counter
            // wants is a grain that is one flat colour at a glance and only turns into
            // detail up close, not veining, which is made of big shapes that read from
            // across the room. SLATE is that — the top is laid in panels with a hairline
            // seam where they meet, so leaning in finds structure rather than noise. See
            // ChromeArt.CounterGrain for the rest of the set.
            //
            // TILED, never stretched: this band's height changes by 121 units between a
            // bench standing over an open cellar and one that is not (AlignBenchCounters),
            // and a stretched pixel surface stops having pixels. At a quarter of its
            // pixels-per-unit one art pixel lands as four, which is the same 4× the
            // bench's own zoom stands at, and a 320-wide tile covers the design width in
            // one go — so there is no repeat to see either.
            // THE AUTHOR'S OWN COUNTER, when there is one (2026-09-16: "Built ekranında kullanılan tezgahı
            // aseprite'da editleyeceğim"): Items/bench_counter.png, tiled at the same 4x, its own far edge and all —
            // Tools/bench_export.py hands them the drawing at the art's scale. Until it exists, the code's grain.
            var own = ItemArt.Load("bench_counter");
            // ...IN THE COUNTER'S FINISH (2026-09-16): the author's drawing with its slab family recoloured, or the
            // brushed tile cut in the finish's slab tone (CounterFinish.Current, set by the HUD before the benches build).
            timg.sprite = own != null ? CounterFinish.RecolourSlab(own, CounterFinish.Current.Id)
                                      : ChromeArt.Counter(320, 96, BenchGrain, CounterFinish.Current.Counter);
            timg.type = Image.Type.Tiled;
            timg.pixelsPerUnitMultiplier = 0.25f;
            timg.color = timg.sprite != null ? Color.white : BenchSlab;
            timg.raycastTarget = false;
            if (own != null) return;   // the drawing carries its own edge and sheen

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
            // A LIFT, not a band, now that there is stone under it: the sheen used to be
            // a flat #292630 rectangle, which over marble is a strip with the veining
            // wiped out of it. Half a step of white leaves the stone showing through.
            simg.color = new Color(1f, 1f, 1f, 0.045f);
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

            // (The bottle's name lives in the step card's own teal cap since 2026-08-26
            //  — see BuildStepCard. Its plate here lasted one build: a second plate on a
            //  full band, standing in the mix gauge's column at every width.)

            // The standing line under the title. It named both methods as a menu of two,
            // which stopped being true when the recipe started naming one (2026-08-14) —
            // UpdateStepCard rewrites it with the method the tin actually asks for.
            _shakerHint = NewText("Hint", _shakerPanel, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            // On the band with everything else (2026-08-26): the quiet line over the
            // readout, where the eye already is when it wants to be told what next.
            Stretch(_shakerHint.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(16, 114), new Vector2(-16, 128));
            _shakerHint.text = ShakerHintFor(null);
            // Off since 2026-09-13: the strip cut into the counter names the method on its
            // third step, and a line under it saying it again was the card's double.
            _shakerHint.gameObject.SetActive(false);

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
            // Foot on the bench line: the tin is 358 tall about its centre.
            // THE MIDDLE IS SHARED (2026-09-04, the author: "Shaker ve alkol şişesi
            // sahnenin ortasını paylaşmalı"). The tin stood at -210 and the bottle away
            // at +330, which left the middle of the bench empty and the two things the
            // stage is actually about at opposite ends of it. They flank the centre now.
            //
            // EVERY POSITION HERE IS ARITHMETIC, not taste ("Hiçbir görsel üst üste
            // binmemeli"). Each prop's drawn half-width is measured off its own art —
            // tin 66, cap 71, bottle 86, napkin 54 — and the numbers below leave at least
            // BenchClear between every pair of drawn edges, inside the 1149-wide working
            // area. Change one and re-check the others; the gaps are the contract.
            // -60 since 2026-09-16 (was -120): the plaque under the rail takes the bench's left, and the tin stands
            // clear of it — its left edge at 464, the plaque's right at 438.
            _shakerHome = new Vector2(-60, CatchFootY + TinH * 0.5f);   // at the bottom: the tin catches (2026-09-14)
            // 300, not 150 (2026-09-14): the strip and the readout moved up under the rail, and at 150 the bottle stood
            // in their band; right of them it stands clear, and the tin travels under it when the bottle is in hand.
            _bottleRest = new Vector2(300, -90);
            // The two contact shadows, built BEFORE the props so they draw under them.
            // Each is placed on its own prop's foot line every frame (PushPropShadow).
            _tinShadow = AddContactShadow(_pourSurface, 158f * (TinW / 200f), new Vector2(_shakerHome.x, TinFootY));
            _bottleShadow = AddContactShadow(_pourSurface, 128f, new Vector2(_bottleRest.x, BottleFootY));
            // LIGHT ON THE WORK, AND THE STONE ANSWERING (2026-09-16, the author: "tezgaha ışıklandırma ve yansıma
            // gerekiyor ... odak şişe ve shakerda olmalı dökerken, şişe dolduktan sonra odak kapağa geçmeli"): a
            // soft bloom behind whatever the hand is on — the bottle while it pours, the tin once the lid is on,
            // both at rest — and a faint mirror of the tin and the bottle on the counter under their feet
            // (StepBenchLight). Built before the props, so they draw over it; the water ring with them.
            _benchLight = NewRect("Light", _pourSurface);
            Place(_benchLight, new Vector2(0.5f, 0.5f), new Vector2(760f, 460f), new Vector2(120f, -60f));
            var lightImg = _benchLight.gameObject.AddComponent<Image>();
            lightImg.sprite = ChromeArt.Halo();
            lightImg.color = new Color(UITheme.Cream[4].r, UITheme.Cream[4].g, UITheme.Cream[4].b, 0.16f);   // Cream[4] at 16%: light
            lightImg.raycastTarget = false;
            _benchLight.SetAsFirstSibling();
            _tinMirror = AddMirror("TinMirror", TinW, TinH * 0.3f);
            _bottleMirror = AddMirror("BottleMirror", 180f, BottleH * 0.3f);
            var ring = NewRect("WaterRing", _pourSurface);
            Place(ring, new Vector2(0.5f, 0.5f), new Vector2(96f, 36f), new Vector2(130f, -292f));   // between the tin and the dial, on the counter
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = ChromeArt.Smudge(11);
            ringImg.color = new Color(1f, 1f, 1f, 0.38f);
            ringImg.raycastTarget = false;
            ring.SetSiblingIndex(1);
            _shakerVessel = NewRect("Shaker", _pourSurface);
            Place(_shakerVessel, new Vector2(0.5f, 0.5f), new Vector2(TinW, TinH), _shakerHome);
            var shakerImg = _shakerVessel.gameObject.AddComponent<Image>();
            // The real steel shaker (2026-07-23). It sits in front of the fluid so the metal
            // reads solid — the falling stream shows above the mouth then vanishes into the tin.
            var tinSprite = ItemArt.Load("tin_open") ?? ItemArt.Shaker;
            if (tinSprite != null) { shakerImg.sprite = tinSprite; shakerImg.preserveAspect = true; shakerImg.color = Color.white; _shakerBodyImg = shakerImg; }
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

            // THE BRIMFUL DRINK'S TOP (2026-09-14): an oval in the tin's mouth, placed by PushShakerPool. A
            // child of the tin so it turns with it; drawn before the fluid's stream, and under the front
            // plate, which hides its near part behind the lip.
            var tinSurfaceRt = NewRect("TinSurface", _shakerVessel);
            tinSurfaceRt.anchorMin = tinSurfaceRt.anchorMax = tinSurfaceRt.pivot = new Vector2(0.5f, 0.5f);
            _tinSurface = tinSurfaceRt.gameObject.AddComponent<Image>();
            _tinSurface.sprite = GlassArt.SurfaceDisc();
            _tinSurface.raycastTarget = false;
            _tinSurface.enabled = false;

            // Grabbing the shaker (once it holds a drink) starts a free, loose shake.
            var shakeGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            shakeGrab.callback.AddListener(_ =>
            {
                if (Run == null || Run.Glass.IsEmpty) { SayShaker(UIText.T("bench.shaker.nothing_to_shake")); return; }
                if (!_capped) { SayShaker(UIText.T("bench.shaker.cap_first")); return; }
                _shaking = true;
            Sfx.Play("tin_tip", 0.7f);
                _shakeEnergy = Run.ShakeEnergy;   // continue from what's been shaken, don't reset
                _shakerVel = Vector2.zero;
                _lastShakeMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            });
            _shakerVessel.gameObject.AddComponent<EventTrigger>().triggers.Add(shakeGrab);
            // THE BENCH ANSWERS THE POINTER (2026-09-06, the author asked for the same
            // language here as in the room). The tin is the biggest thing on this bench
            // and it is held rather than picked up, so it takes the light and a breath
            // of sway — no rise, because a tin that lifts off the bench is a spill.
            var tinGlow = _tinGlow = _shakerVessel.gameObject.AddComponent<HoverGlow>();
            tinGlow.Graphics = new Graphic[] { shakerImg };   // the lid joins it below, once it exists
            tinGlow.Rise = 0f; tinGlow.Sway = 1.2f; tinGlow.Grow = 1.03f; tinGlow.Halo = 1.3f;
            // NOT UNTIL THERE IS SOMETHING TO DO WITH IT (2026-09-06, the author: "shakera
            // koyma sahnesinde shaker kapatılmadan önceki shakerin gövdesi ile mousela
            // etkileşime giremiyoruz ondan dolayı onun mouse ile üstüne gelince parlamasına
            // gerek yok, çalkalama aşamasında var"). An open tin is a target for the bottle,
            // not for the hand; a light on it promises a grab that is refused. It comes on
            // with the lid, which is when the tin becomes the thing you shake.
            tinGlow.enabled = false;

            // The metaball fluid draws over the vessel (pool); the solids float on top of it;
            // the bottle is created after, so it sits in front of the liquid.
            _shakerFluid = new MetaballFluid(_pourSurface);
            // THE TIN'S FRONT, OVER THE LIQUID (2026-09-08, the author: "shakerın içerisi
            // eskisi gibi dolsun, sen kesme, sadece sıvının önünü tin_open_Front ... ekle
            // böylece sıvı şişenin içerisinde gibi gözükecek"). The fluid is a RawImage
            // created after the tin on this surface, so it already draws over the tin — the
            // fill the author wants kept. This is the third layer: the tin's front body,
            // drawn 82x124 of the 116x208 tin from x 17, bottom-aligned at row 195, placed
            // on a rect that copies the tin's transform every frame so it shakes and pours
            // with it.
            _tinFrontRt = NewRect("TinFront", _pourSurface);
            Place(_tinFrontRt, new Vector2(0.5f, 0.5f), new Vector2(TinW, TinH), _shakerHome);
            var tinFrontArt = NewRect("Art", _tinFrontRt);
            tinFrontArt.anchorMin = new Vector2(17f / 116f, (208f - 195f) / 208f);
            tinFrontArt.anchorMax = new Vector2((17f + 82f) / 116f, (208f - 195f + 124f) / 208f);
            tinFrontArt.offsetMin = tinFrontArt.offsetMax = Vector2.zero;
            _tinFrontImg = tinFrontArt.gameObject.AddComponent<Image>();
            _tinFrontImg.raycastTarget = false;
            _tinFrontImg.preserveAspect = false;
            DressTinFront();
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
            // Under the card, on the counter between it and the tin (2026-08-26: at -150
            // the dome stood half-hidden behind the step card's lower rows). The rect is
            // mostly empty air — the lid art rides CapArtOffset above its centre — so the
            // rest is derived from where the DOME should sit, not from the rect.
            // Left of the tin with 73 units of air, and clear of the napkin by 65.
            // -510 since 2026-09-16 (was -330): the lid takes the far left, the spoon stands between it and the tin.
            // AND ITS DOME AT -140, not -185: the lid's rect is mostly air under the dome, and at the far left its
            // centre fell on the BACK key's column — a press on the lid's middle landed on the key instead (the
            // tall-glass test caught it: "the lid went on and the bench never moved to the glass"). Measured: the
            // rect's centre is at 636 on screen now, the key starts at 672.
            _capRest = new Vector2(-510, -140f - CapArtOffset * TinH);
            _shakerTop = NewRect("ShakerCap", _pourSurface);
            _shakerTop.anchorMin = _shakerTop.anchorMax = _shakerTop.pivot = new Vector2(0.5f, 0.5f);
            _shakerTop.sizeDelta = _shakerOpenSize;
            _capPos = _capRest;
            _shakerTop.anchoredPosition = _capRest;
            var topImg = _shakerTop.gameObject.AddComponent<Image>();
            topImg.sprite = ItemArt.Load("shaker_cap");
            _shakerCapImg = topImg;
            topImg.preserveAspect = true; topImg.raycastTarget = true;

            var capGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            capGrab.callback.AddListener(_ =>
            {
                if (_capped) return;
                _capGrabbed = true;
                _capGrabOffset = Vector2.zero;
                if (Mouse.current != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 held))
                    _capGrabOffset = _capPos - (held - new Vector2(0, _shakerTop.rect.height * CapArtOffset));
                Sfx.Play("cap_on", 0.35f);
            });
            _shakerTop.gameObject.AddComponent<EventTrigger>().triggers.Add(capGrab);
            // THE LID IS ITS OWN THING ONLY WHILE IT IS OFF (2026-09-09, the author:
            // "kapağı shakera takınca artık shaker bir olmalı, ayrı ayrı kapak ve gövde
            // seçilmemeli"). A capped tin that lights its lid on one hover and its body on
            // the next is two objects wearing one silhouette. Once the lid is on, this glow
            // is switched off and the TIN's glow lights both drawings and moves both
            // transforms — one shaker, one light, one sway.
            var capGlow = _capGlow = _shakerTop.gameObject.AddComponent<HoverGlow>();
            capGlow.Graphics = new Graphic[] { topImg };
            capGlow.Rise = 4f; capGlow.Sway = 1.4f; capGlow.Grow = 1.06f;
            _shakerTop.gameObject.SetActive(topImg.sprite != null);
            // ONE SHAKER (2026-09-09): the tin's own light takes the lid's drawing and the
            // lid's transform, so a capped tin lights and sways as one object.
            if (_tinGlow != null)
            {
                _tinGlow.Graphics = new Graphic[] { shakerImg, topImg };
                _tinGlow.Movers = new Transform[] { _shakerVessel, _shakerTop };
            }

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
            var hitBottle = _pourSlotHit = _pourBottle.gameObject.AddComponent<Image>();
            hitBottle.color = new Color(0, 0, 0, 0.001f);   // invisible; the fallback grab for a bottle with no art

            // The BOTTLE inside the grab plate. The plate is a fixed slot because a hand needs
            // one; the vessel is sized per bottle by VesselArt when the stage refreshes, so a
            // carton stands as a carton and a slim bottle as a slim bottle, both with their
            // feet on the plate's floor line and their caps where the art puts them.
            _pourVessel = NewRect("Vessel", _pourBottle);
            _pourFill = BottleFill.Under(_pourVessel);
            _pourArt = BottleArt.Under(_pourVessel);

            var pourArt = NewRect("Body", _pourVessel);
            Stretch(pourArt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pourBottleBody = pourArt.gameObject.AddComponent<Image>();
            _pourBottleBody.preserveAspect = true;    // the real bottle art, set per focus in RefreshShaker
            _pourBottleBody.color = UITheme.Cyan[3];
            _pourBottleBody.raycastTarget = false;
            // The grip: the drawing again, over everything, invisible, hit-tested on its alpha.
            var grip = NewRect("Grip", _pourVessel);
            Stretch(grip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pourGrip = grip.gameObject.AddComponent<Image>();
            _pourGrip.color = new Color(1f, 1f, 1f, 0.001f);
            _pourGrip.raycastTarget = true;
            _pourGrip.enabled = false;
            // The house vodka's plate stands for the whole set: no v4 art, no bottles.
            if (ItemArt.Load("v4_vodka_astra_front") == null)      // no art available → keep a procedural neck
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
                {
                    _bottleGrabbed = true;
                    // The out parameter is ZERO when the point cannot be mapped, so the fallback
                    // is chosen after the call, not before it.
                    if (Mouse.current == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 held))
                        held = _bottleHand.GripPoint;
                    _bottleHand.Press(held, _pourSurface.rect.height * 0.5f + HandAbove, TinMouth().Centre.y + ClearOverRim);
                }
            Sfx.Play("bottle_set", 0.45f);   // lifted off the wood
            });
            _pourBottle.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);
            var bottleGlow = _pourBottle.gameObject.AddComponent<HoverGlow>();
            bottleGlow.Graphics = new Graphic[] { _pourBottleBody };
            bottleGlow.Riser = _pourVessel;      // the drawing, not the grab plate
            bottleGlow.Rise = 4f; bottleGlow.Sway = 1.4f; bottleGlow.Grow = 1.05f;
            _pourGlow = bottleGlow;
            _benchProps.Add(_pourBottle.gameObject.AddComponent<CanvasGroup>());

            // THE PLAQUE (2026-09-16): the recess under the rail that carries the bench's words — the steps on its
            // upper row (BuildStepCard, below), the readout on its lower. 13 → 16: pinned to the pixel faces' 8px
            // grid (CLAUDE.md), like every other size in the rebuild. Engraved, the way the strip's words are.
            _shakerPlaque = AddCounterPlaque(_shakerPanel, PlaqueW, PlaqueH);
            _shakerReadout = NewText("Readout", _shakerPlaque, _body, 16, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_shakerReadout.rectTransform, new Vector2(0f, 0f), new Vector2(PlaqueW - PlaquePad * 2f, PlaqueLineH),
                  new Vector2(PlaquePad, PlaqueLineY));
            _shakerReadout.horizontalOverflow = HorizontalWrapMode.Wrap;   // a long line takes the plaque's second row
            Engraved(_shakerReadout);

            // The pour gauge: a slim standing column, cyan-edged, filled bottom-up with the
            // TIN's contents as shares of the whole vessel — 5% of vodka reads 5% VODKA and
            // the room above it reads EMPTY. Against the right wall, left of the TO THE
            // GLASS key with clear air on both sides (at 520 its labels ran under the key;
            // at -340, in its first life, it hung over the prep table this rebuild removed).
            // 300 at -60, not 330 at -24: the taller hang poked 34 units over the counter
            // rail into the room, and the band owns every instrument now (2026-08-26).
            // 96x212 at (462,-52): the shaker's own 82:181, standing where the 44-wide
            // column did but wide enough to BE a shaker. Its right edge lands at 510,
            // inside the 1149-wide working area the serve bench measures against, and
            // its captions still have their air to the left.
            _shakerMixBar = BuildStandingGauge(_shakerPanel, MeasureAt, MeasureSize, UIText.T("bench.shaker.gauge.head"));
            BuildWorkColumn(_shakerPanel);

            // THE WORK METER, FOURTH TAKE (2026-09-16): it was a flat bar (2026-08-26, "çok amatörce duruyor"),
            // then the house's tube gauge, and the author sent that back too ("karıştırma ve çalkalama sırasındaki
            // barın tasarımını değiştir"). It hung over the ROOM, the one place on this screen the bench does not
            // own. There is no bar now: the tin frosts as it is shaken (below, on its front plate), the measure's
            // bands blend as the drink mixes, and the plaque says the figure (ShowWorkMeter).
            // The frost rides the tin's front plate, cut to it: a white gradient standing on the plate's foot,
            // as tall as the shake has come. The plate is a child rect that copies the tin's transform every frame,
            // so the frost turns and travels with the steel.
            _tinFrostRt = NewRect("Frost", tinFrontArt);
            var frost = _tinFrostRt.gameObject.AddComponent<Image>();
            frost.sprite = ChromeArt.FrostGradient();
            frost.type = Image.Type.Simple;
            frost.color = new Color(1f, 1f, 1f, 0.62f);
            frost.raycastTarget = false;
            tinFrontArt.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // the frost stays on the steel
            SetFrost(0f);

            // THE LIFT LADDER (2026-09-16, the author: "şişeyi kaldırdıkça dolum hızının arttığı gibi detayları
            // oyuncuya belirt"): under the bottle's rest, a recess with nine rungs that grow the way the pour's
            // steps grow (BottlePour.Steps, 1·1·2·3·5·8·13·21·34) and the words that say it. The rung the hand is
            // on lights as the bottle is lifted (LightLiftLadder), so the ladder is the instruction and the reading.
            BuildLiftLadder();

            // THE BAR SPOON (GDD 21 §14, 2026-08-11): the stir's instrument, resting by the
            // tin. Drawn, not generated — it is an instrument the pointer works, and at this
            // size a rod and a bowl in the bench's own steel read truer than any take.
            // BESIDE THE TIN AND CLEAR OF THE LID (2026-08-14, the author: the spoon and the
            // shaker were drawn over each other). Measured rather than nudged: the tin stands
            // at x −310..−110 and the lid rests across −450..−250, so the only clear air on
            // this side is the corridor between the BACK key (which ends at −550) and the
            // lid. The spoon leans there, in reach, touching nothing.
            // Against the left edge, in the margin the card leaves (2026-08-26): the
            // spoon is the one prop that lives in the instrument column, standing like a
            // tool on its rack, and its foot is on the bench's own line. y is the foot
            // plus the drawn spoon's full height, because the slot hangs from its grip.
            // THE NAPKIN GOES DOWN FIRST, so the spoon lies on it rather than through it.
            // It is parented to the surface and not to the spoon: the spoon gets picked up
            // and thrown about by the stir, and a napkin that travelled with it would be a
            // napkin stuck to the tool. It stays on the counter where it was set.
            var napkin = NewRect("Napkin", _pourSurface);
            // THE NAPKIN COVERS THE SPOON (2026-09-16, the author: "kaşığın arkasındaki peçetenin boyutu kaşığı
            // kaplasın"): a folded bar towel the whole spoon lies on, 140x240 about the spoon's own middle — the
            // spoon's slot is 64x256 — laid a few degrees off square. It stood under the bowl alone before.
            // The spoon moved to x -340, between the lid at the far left and the tin, and its foot 70 under the
            // bench line, so the towel's top clears the plaque under the rail and its foot stands clear of the
            // BACK key's column.
            Place(napkin, new Vector2(0.5f, 0.5f), new Vector2(140, 240),
                  new Vector2(SpoonX, SpoonFootY + 128f));
            var nimg = napkin.gameObject.AddComponent<Image>();
            nimg.sprite = ChromeArt.Napkin(56, 96);
            nimg.raycastTarget = false;
            napkin.localRotation = Quaternion.Euler(0, 0, -4f);   // set down by hand, not laid square
            _napkinRt = napkin;

            _spoonRest = new Vector2(SpoonX, SpoonFootY + 256f);
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
                // ...AND IT ANSWERS THE POINTER (2026-09-06, the author: "kaşık parlamıyor").
                // The glow rides the SLOT, which is what the stir moves, but it lights and
                // measures the drawing, because the slot is a transparent hit rectangle.
                var spoonGlow = _spoonRt.gameObject.AddComponent<HoverGlow>();
                spoonGlow.Graphics = new Graphic[] { si };
                spoonGlow.Rise = 4f; spoonGlow.Sway = 1.6f; spoonGlow.Grow = 1.05f;
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
                {
                    _spoonHeld = true; _stirHasPrev = false;
                    // Held where it was taken (the author: "kaşık neresinden tutulursa
                    // ordan tutulsun"): the spoon's pivot is its grip, so grabbing the bowl
                    // used to snap the grip to the finger.
                    _spoonGrabOffset = Vector2.zero;
                    if (Mouse.current != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 held))
                        _spoonGrabOffset = _spoonRt.anchoredPosition - held;
                    Sfx.Play("tap_handle", 0.5f);
                }
            });
            _spoonRt.gameObject.AddComponent<EventTrigger>().triggers.Add(spoonGrab);
            _benchProps.Add(_spoonRt.gameObject.AddComponent<CanvasGroup>());

            // (The TO THE GLASS key retired on 2026-08-26 — the glass arrives on its own
            //  now; see UpdateToGlass. Its 216 units of the key strip went back to the bar,
            //  which is most of why the band stopped looking crowded.)

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
            lidLabel.text = UIText.T("bench.shaker.lid_off_key");
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
