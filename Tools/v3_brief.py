# -*- coding: utf-8 -*-
"""THE fixed generation brief for v3 bottles (GDD 25 §5a) — one source of truth.

The author, 2026-08-05: "Görseller bozuk üretiliyor. Şu sabit promptları
kararlaştırıp pixellab'den ürettir." Every round before this one wrote its brief by
hand and each rewrite reopened a solved failure: liquid pooled back in, the label
went blank, the pitch went flat. These blocks are frozen; a bottle brief is built
ONLY by build(), and the per-bottle part is the LOOK sentence and nothing else.

Every block exists because a generation actually failed without it:
  EMPTY   — the high_0 take pooled 20 rows of blue at the base (2026-08-05)
  BUILD   — round one came back wonky and off-centre (2026-08-04)
  NO_TEXT — the generator writes mangled glyphs when allowed any text
  CHECKER — clear empty glass is where the transparency chequerboard appears
"""

# Tool config — create_map_object, and the knobs the approved take was made with
# (the aday_high pick was generated on these defaults; they are now explicit law).
TOOL = 'create_map_object'
VIEW = 'high top-down'          # the author's camera, picked 2026-08-04
CANVAS = {'width': 120, 'height': 280}
KNOBS = {'outline': 'single color outline',
         'shading': 'medium shading',
         'detail': 'medium detail'}

EMPTY = ("the bottle is COMPLETELY EMPTY with absolutely no liquid inside - no fill "
         "level, no coloured contents, no pool at the bottom, the inside shows only "
         "pale empty glass all the way down to the base, ")

CHECKER = ("never draw a transparency checkerboard pattern inside the glass, the "
           "glass is a solid pale colour, ")

BUILD = ("perfectly symmetrical left-right silhouette with straight vertical walls, "
         "standing upright and centred, filling the frame from top to bottom, "
         "clean pixel art, no anti-aliasing, transparent background, ")

NO_TEXT = ("absolutely no text anywhere on the bottle - no letters, no words, no "
           "numbers, no writing of any kind, label areas are blank geometry only")


def build(look, ratio):
    """The one way a v3 bottle brief is made: the LOOK sentence, the ratio as a
    number, then the frozen blocks in fixed order."""
    return ("%s, the bottle is about %.1f times as TALL as it is WIDE, %s%s%s%s"
            % (look, ratio, EMPTY, CHECKER, BUILD, NO_TEXT))


def call_args(look, ratio):
    args = {'description': build(look, ratio), 'view': VIEW}
    args.update(CANVAS)
    args.update(KNOBS)
    return args


# ── the per-bottle LOOK lines, written as each bottle reaches its step ──────────
# (GDD 25 §4: the silhouette family may echo the famous bottle; registered
# distinctive elements shift; the dress geometry is named but never lettered.)
LOOKS = {
    'vodka_astra': (
        "a tall clear glass vodka bottle, straight cylindrical body with rounded "
        "shoulders and a medium neck, a silver metal cap, a white rectangular label "
        "on the body with one bold red horizontal banner band across its upper part "
        "and a tall plain white panel below it", 3.5),
}
