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

  THE GLASS IS NEVER DRAWN. The customer is drawn with an EMPTY grip and the game puts
  the served vessel in it - otherwise every customer drinks the same glass whatever was
  poured. What makes that work is the rig's anchor table (see HAND_ANCHOR_NOTE): per
  drink frame, an (x, y, angle) the vessel is pinned to.

  Three grips, not one (the author, 2026-08-19): short (shot/rocks), long (highball,
  pint), stemmed (cocktail, wine - fingers on the STEM, so a coupe is not held in a
  fist). The vessel draws BEHIND the hand, single layer: the hand is the near thing.

  Each customer drinks in their OWN way (the author's call, cost stated and accepted):
  three drink clips per person and three anchor measurements, so a new face is not free.

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
    "14 to 20 colours total, "
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
PIVOT_CANVAS_PX = 160
PIVOT_BODY_PX = 145
PIVOT_LANGUAGE = 'selective'
PIVOT_VIEW = 'side'          # PixelLab's name for eye level

# ── the three customers being compared (2026-08-19, round two) ───────────────
# The line language and the ruler are settled, so the only thing left open is WHO walks
# in. Three deliberately different bodies, because a proportion pivot that only holds
# for one build is not a pivot: a slight young woman, a heavy man in middle age, and a
# tall older regular. All three are dressed for the Miami club room - the chroma is on
# the person, the room stays concrete and plum (14 art bible).
FIGURE_OPTIONS = {
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
    "oldregular": (
        "a tall lean man in his sixties, bar regular, "
        "grey hair combed back, deep lines on the face, "
        "a faded olive work jacket over a plain white shirt, worn brown trousers, "
        "no logo, no pattern, no hat"),
}

# ── the scale candidates the room is being asked about ───────────────────────
# (label, reference adult height in art px, fraction of that height BELOW the counter's
#  rest line). The counter rest line is DiegeticStage.CounterRestY; the shipped cast sits
#  at 150px with only 44% of the body under the bar, which is thigh height - the reason
#  they read as giants leaning over the counter rather than people standing at it.
REFERENCE_CANDIDATES = [
    ("belly",  220, 0.62),   # a real bar top: navel / lower ribs
    ("belly_s", 180, 0.62),  # same cut, smaller people, bigger-looking room
    ("waist",  190, 0.55),   # more shoulder and arm above the wood, less true
]

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
