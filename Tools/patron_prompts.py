# -*- coding: utf-8 -*-
"""What a LAST CALL customer IS, in the words we send PixelLab (2026-08-19).

Until today the cast's descriptions lived in the MCP call and died with the session:
generation_log.jsonl kept the character_id and nothing else, so a shipped patron could
be downloaded again but never re-derived. The bottles have had their briefs in the repo
since the v3 era (v3_brief.py, tier_prompts.py, carton_prompts.py); this is the same
thing for people, and it is the file to change when the answer to "what were these
generated from" changes.

THE SPEC, decided with the author on 2026-08-19 in one sitting. Every line here is a
decision, not a preference, and the reason is written beside it.

  NO LIGHT, NO REFLECTION. First line of every prompt and the reason the flat plates
  read at all: the room is lit in URP by 2D lights, so a highlight baked into a drawing
  is a second sun that does not move with the first. The author has now said this three
  times across the scene, the bottles and the cast - it is the house rule, not a note.

  FULL BODY, CUT AT THE BAR. Generated whole (the walk-in needs legs) and cropped in
  play, because the counter takes everything below its own rest line.

  THE CUT LINE IS THE WRIST LINE. "karakterlerin elleri masanin uzerinde gozukuyor gibi
  olmali" (2026-08-19). A body cut hard at the bar's edge cannot also rest its hands ON
  the bar - the hands would be in the discarded half. So every seated clip is posed with
  the forearms forward and the WRISTS ON THE CUT ROW: palms and fingers stay above it,
  lying on the wood. This is a constraint on the drawing, which is why it is in the
  prompt rather than in the layout code.

  THE GLASS IS DRAWN IN THE HAND - reversed the same day it was decided, and both
  halves are kept here because the reasoning still holds for whoever revisits it.
  The original rule was that the customer is drawn with an EMPTY grip and the game pins
  the served vessel into it, so that a customer drinks whatever was actually poured;
  three grip classes (short / long / stemmed) and a per-frame hand-anchor table were the
  machinery for it, and all of it was built. The author then chose the simpler thing
  ("eski tarza geri donelim sadece 1 tarz drinking olsun, ayri ayri uretme, bardak
  elinde olsun normal su bardagi gibi"): ONE drink clip, a plain glass drawn in the hand.
  The trade, stated plainly: every customer now drinks from the same glass whatever the
  recipe said. If that ever grates, the empty-hand road is still open and the anchor note
  below is what it needs.

  MEDIUM COLOUR BUDGET, 14-20. The palette doctrine (14 art bible) puts the chroma on
  the ACTORS and keeps the architecture quiet, so a customer may carry one saturated
  garment; the room may not.

  VIEW: side, which is PixelLab's word for EYE LEVEL. The first trial was drawn high
  top-down to match the room's 30-degree floor, and the author looked at it and said no:
  "karakteri tam karsidan goruyor olmaliyiz, yukaridan degil" (2026-08-19). The customer
  is the thing the player reads faces on, and a face seen from above is mostly scalp -
  the room may be looked down on, the person is looked AT. The seam this opens (a figure
  at eye level standing on a floor drawn at 30 degrees) is paid for by the counter: the
  bar cuts the body before any ground contact is visible, so there is no floor plane
  under a customer to disagree with.

  LINE LANGUAGE: selective outline, chosen off the trial - silhouette outlined, inner
  shapes separated by colour.

  PROPORTION PIVOT: the trial's selective figure, 196px of body on a 220px canvas, is
  the ruler. "Kafa boyu vucut boyu hep bu pivota benzer olmali" - so the size stays 220
  and every new customer is read against PIVOT_BODY_PX before it is accepted. A cast
  whose heads drift is a cast that cannot share one rig.

SCALE is deliberately NOT fixed here. "Her karakter boyu farkli olacagindan orana gore
belirleyecegiz" - so a person's height is a RATIO of the reference adult, and the
reference is chosen against the counter, whose rest line is the one measured constant.
REFERENCE_CANDIDATES carries the choices being compared in the room right now.
"""

# ── the standing constraints, prepended to every character prompt ────────────
# Kept as one block so a change here reaches the whole cast, and so the diff shows
# exactly what the cast was told.
HOUSE_RULES = (
    "flat local colour only, absolutely no baked lighting, no highlights, no specular "
    "gleam, no reflections, no glow, no cast shadow on the ground; "
    "even matte tone with simple hand-placed shade steps, "
    # PROPORTION, spelled out since 2026-08-19. The author put a new pair beside heavyset
    # and said they did not match him on body proportion or detail, and the measurement
    # agreed: the cast runs a head 0.125-0.152 of total height and silverbob came back at
    # 0.183 - a head a third too big, which is the road to chibi. The model has no
    # proportions dial in v3 mode, so the only place to say it is here.
    "realistic adult human proportions, a small head about one seventh of the total "
    "height, long legs, natural adult build, not chibi, not stylised, "
    # DETAIL, for the same reason: shaved came back on 27 colours where the cast runs
    # 37-57, and read flat beside them.
    "detailed shading with fabric folds and clear facial features, "
    "full body from head to feet, standing upright, feet apart on the ground, "
    "transparent background, no floor, no furniture, no props, no text"
)

# The three line languages being compared. PixelLab honours `outline` in v3 mode; the
# words are its own enum, so they are quoted rather than paraphrased.
LINE_LANGUAGES = {
    "inked":     ("single color black outline", "a dark outline all the way around the figure"),
    "selective": ("selective outline",          "outline on the silhouette only, inner shapes separated by colour"),
    "lineless":  ("lineless",                   "no outline at all, shapes read by value and hue alone"),
}

# The pose that the RIG is measured from. Neutral on purpose: the foot line, the head
# row and the shoulder width all have to come off a frame where nothing is foreshortened.
NEUTRAL_POSE = (
    "standing straight and relaxed facing the viewer, arms hanging down at the sides, "
    "hands open and empty, weight on both feet"
)

# The pose the game actually shows for ~90% of a visit, and the one the author asked to
# see before choosing: at the bar, hands on the wood.
BAR_POSE = (
    "standing at a bar leaning slightly forward, both forearms reaching forward and "
    "both hands resting flat and open on an unseen bar top at waist height, "
    "wrists level with the waist, hands empty"
)

# The trial figure: no identity on purpose (the author: "notr bir prova figuru"), so the
# choice is made on the LINE LANGUAGE and not on whether the outfit is likeable.
TRIAL_FIGURE = (
    "a plain young adult bar customer, average build, short dark hair, "
    "plain grey t-shirt, plain dark blue trousers, plain shoes, "
    "no logo, no pattern, no jewellery, no hat, no bag"
)

# ── the settled rig ruler ────────────────────────────────────────────────────
# Measured off trial selective_neutral.png: 196px of body inside a 220px canvas, 30
# colours. Every customer generated from here on is checked against it before it is
# accepted; a figure more than a few px off has different head-to-body proportions and
# cannot share the cast's anchors.
# RULED BY THE COUNTER, not by the canvas (2026-08-19, the author: "musterilerin
# boyutunu masaya gore dogru belirle"). The first pivot was picked to fill a canvas and
# it made giants: measured against the bar, a 203px customer is 260cm tall.
#
# The measurement, off counter.png: rows 0-66 are the bar's TOP SURFACE seen at 30
# degrees, rows 67-149 are its FRONT FACE - so the bar's height is 83 art px. A bar top
# is 110cm, which puts the room at 0.755 art px per cm, and a 170cm adult at 128px, a
# 180cm one at 136px. The shipped cast, which has always sat right in this room, draws
# at ~145 art px equivalent; the room's own door and ceiling agree to within a tenth.
# So the ruler is ~145 for a tall adult, and the canvas follows the body rather than the
# body following the canvas.
# BACK TO 220, and the counter's ruler stands as a fact beside it rather than instead of
# it. The measurement is still true - a 200px customer at a 110cm bar is 265cm - but the
# author kept the approved drawings, and a cast has to be consistent with ITSELF before it
# is consistent with the furniture: a bar of people at two different scales is wrong in a
# way anybody can see, while a bar of people uniformly a little large is a stylisation.
# So every customer generated from here on matches the two already in the game.
PIVOT_CANVAS_PX = 220
PIVOT_BODY_PX = 200
# ── the rig the game loads (2026-08-19) ──────────────────────────────────────
# The drawings arrive on a 220 canvas and the game draws that canvas at one art pixel per
# stage unit, so the canvas IS the rig: TycoonHud.CharSize = RIG_CANVAS_PX x StageToHud,
# and a frame standing anywhere but the foot line stands anywhere but the floor.
# Ten pixels of air under the shoes, as the 2026-08-09 rig had.
RIG_CANVAS_PX = 220
RIG_FOOT_Y = 210
# How far the canvas's BOTTOM sits below the counter's far edge, in stage units: the
# bar's front face measures 83 art px (see the note above), and the feet stand 10 px above
# the canvas bottom, so 93. TycoonHud.CharFootDrop is this x StageToHud.
RIG_FOOT_DROP = 93

# 'lineless', not 'selective' - and the two words mean less than they look. PixelLab
# treats `outline` as soft guidance: 'selective' drew clubgirl and heavyset at 54-57% dark
# silhouette, and the same setting drew the next pair at 99-100%, a full black keyline the
# author rejected on sight ("siyah koyu kontur olmamali, trial lineless_neutral gibi
# olmali"). Asking for 'lineless' is a push in the right direction, not a guarantee, which
# is why the MEASUREMENT is the real gate (patron_trial_gen.edge_darkness).
PIVOT_LANGUAGE = 'lineless'
PIVOT_VIEW = 'side'          # PixelLab's name for eye level

# ── the three customers being compared (2026-08-19, round two) ───────────────
# The line language and the ruler are settled, so the only thing left open is WHO walks
# in. Three deliberately different bodies, because a proportion pivot that only holds
# for one build is not a pivot: a slight young woman, a heavy man in middle age, and a
# tall older regular. All three are dressed for the Miami club room - the chroma is on
# the person, the room stays concrete and plum (14 art bible).
FIGURE_OPTIONS = {
    # ── the cast in the game ────────────────────────────────────────────────
    "clubgirl": (
        "a young woman in her twenties, slim and short, bar customer in a Miami club, "
        "dark brown hair in a high ponytail, hoop earrings, "
        "a cropped magenta halter top, high-waisted black trousers, "
        "no logo, no pattern, no bag"),
    "heavyset": (
        "a heavyset man in his forties, broad and thick-set, bar customer in a Miami "
        "club, receding black hair, short beard, "
        "an open cream linen shirt over a teal t-shirt, dark trousers, "
        "no logo, no pattern, no hat"),

    # ── the 2026-08-19 pair, drawn to stand beside them ─────────────────────
    # Different from the two in the game on every axis that reads at this size - age,
    # build, hair silhouette, and which colour they carry - because a cast is told apart
    # by its shapes long before its faces. Both belong to the same Miami room: the chroma
    # is on the person and the architecture stays quiet (14 art bible).
    "silkwoman": (
        "a tall woman in her thirties, slim and elegant, bar customer in a Miami club, "
        "black hair loose to the shoulders with a centre parting, gold hoop earrings, "
        "a turquoise satin slip dress with thin straps, "
        "no logo, no pattern, no bag, no hat"),
    "pastelman": (
        "a slim young man in his twenties, tall and narrow, bar customer in a Miami club, "
        "short curly dark hair, clean shaven, a thin gold chain, "
        "an open pale coral short sleeved shirt over a white vest, cream trousers, "
        "no logo, no pattern, no hat"),

    # ── the second pair of the 2026-08-19 casting ───────────────────────────
    # The four before them are: a slim young woman with a high ponytail, a broad man in
    # his forties, a tall woman with long loose hair, a narrow young man with curls. So
    # these two take the silhouettes still missing - a SHORT bob and a SHAVED head - and
    # the ages at the ends of the range. A cast is read by its outlines first.
    # The suit was BLACK the first time and the author refused it on sight: at this size a
    # near-black garment cannot be told from a keyline, and the house draws no keylines.
    # Nothing else about her changes - the silver bob is the silhouette she was cast for.
    # NO JACKET, third attempt. Black first (refused on sight), then ivory - and the ivory
    # one came back with a dark keyline round every lapel, pocket and hem. A TAILORED
    # GARMENT IS DRAWN WITH LINES: that is how the model knows a blazer. So the blazer goes
    # and the silhouette she was cast for - the silver bob - stays.
    "silverbob": (
        "a woman in her fifties, upright and composed, bar customer in a Miami club, "
        "short silver hair in a sharp bob, large gold earrings, "
        "a lilac silk blouse and cream trousers, no jacket, "
        "no logo, no pattern, no bag, no hat, nothing black"),
    "shaved": (
        "a stocky man in his thirties, heavy shoulders and thick arms, bar customer in a "
        "Miami club, shaved head, dark stubble, "
        "a plain white sleeveless vest, dark grey trousers, "
        "no logo, no pattern, no hat, no tattoo"),

    # ── the third pair (2026-08-19): "farkli etniklerde 2 karakter daha" ────
    # A bar in Miami is Cuban, Haitian, Colombian, Venezuelan before it is anything else,
    # and the six drawn so far do not show that. Named plainly in the prompt, because the
    # model draws what it is told and a vague description defaults to the same face.
    # Everything else follows the same rules as the rest of the cast: nothing near-black
    # over the whole figure, one saturated garment at most, the chroma on the person.
    "afrowoman": (
        "a Black woman in her forties, warm dark brown skin, tall and full-figured, "
        "bar customer in a Miami club, a short natural afro, large gold hoop earrings, "
        "a burnt orange wrap blouse, cream wide-legged trousers, "
        "no logo, no pattern, no bag, no hat"),
    # The first take drew a black rim round the shirt's shoulders and sleeves. A buttoned
    # shirt invites lines the same way a blazer does, so the collar and placket go too.
    "eastasianman": (
        "an East Asian man in his thirties, slim and neat, bar customer in a Miami club, "
        "straight black hair swept back, clean shaven, thin silver-framed glasses, "
        "a pale mint short sleeved t-shirt, light grey trousers, "
        "no collar, no buttons, no logo, no pattern, no hat"),
}

HAND_ANCHOR_NOTE = """\
The drink clips carry a table, not a drawing: for each frame, (x, y, angle) in canvas
pixels for the hand that holds the glass, plus which grip class it is. The served
vessel's own sprite is pinned there, base to the anchor, and tilts by angle on the sip
frames. Measured off the frames once per character per grip - never guessed, the same
law HeadY lives under.
"""


def character_prompt(figure, pose, language):
    """The full text for one create_character call - identity, pose, house rules."""
    _, line_words = LINE_LANGUAGES[language]
    return "%s, %s, %s, %s" % (figure, pose, line_words, HOUSE_RULES)


def outline_hint(language):
    return LINE_LANGUAGES[language][0]


if __name__ == "__main__":
    for name in LINE_LANGUAGES:
        print("== %s\n%s\n" % (name, character_prompt(TRIAL_FIGURE, NEUTRAL_POSE, name)))
