"""
Measures the back-bar plate and prints the twelve numbers TycoonServiceFlow.Menu holds.

The bottles are laid into the cabinet's own niches, so those numbers are ART-BOUND: a
new plate moves every one of them and nothing in the game notices — a bottle simply
stands on a shelf board or half inside a pilaster. This is how they are re-taken.

    python Tools/backbar_measure.py [plate.png]

It works by colour, not by eye: the cabinet's frame is a small, flat set of tones, so
a column that is mostly frame is a pilaster and a row that is mostly frame is a shelf
board. The shelf a bottle stands on is that board's TOP edge, and a niche is the gap
between the board above it and the board below.
"""
import os, sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLATE = os.path.join(ROOT, "Assets", "Resources", "Scene", "backbar.png")
# The 2026-08-19 blue cabinet's frame: lit edge, face, shade, and its dark outline.
FRAME = ((110, 170, 215), (60, 130, 180), (47, 110, 160), (35, 82, 120),
         (30, 72, 110), (18, 42, 67))
TOL = 45


def runs(mask, join=3):
    """Solid runs, with near neighbours joined.

    The join is not tidying: a shelf board is drawn as a lit plank face and a shaded
    lip with a single dark seam between them, so scanning for solid runs finds each
    board TWICE and the seam once. Un-joined, the second half of every board reads as
    its own shelf and the game stands bottles on six of them."""
    out, start = [], None
    for i, on in enumerate(mask):
        if on and start is None:
            start = i
        elif not on and start is not None:
            out.append((start, i - 1)); start = None
    if start is not None:
        out.append((start, len(mask) - 1))
    merged = []
    for r in out:
        if merged and r[0] - merged[-1][1] <= join:
            merged[-1] = (merged[-1][0], r[1])
        else:
            merged.append(r)
    return [r for r in merged if r[1] - r[0] >= 2]


def measure(path=PLATE):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    px = im.load()

    def frame(x, y):
        r, g, b = px[x, y]
        return any(abs(r - c[0]) + abs(g - c[1]) + abs(b - c[2]) < TOL for c in FRAME)

    # A pilaster is a column that is frame nearly all the way down; a shelf board is a
    # row that is frame nearly all the way across. Sampled on a stride - this is a
    # structure scan, not a checksum.
    cols = [sum(frame(x, y) for y in range(0, h, 3)) for x in range(w)]
    rows = [sum(frame(x, y) for x in range(0, w, 3)) for y in range(h)]
    pil = runs([c > 0.60 * len(range(0, h, 3)) for c in cols])
    brd = runs([r > 0.60 * len(range(0, w, 3)) for r in rows])
    if len(pil) < 2 or len(brd) < 2:
        raise SystemExit("no cabinet found in " + path + " - is FRAME still its colour?")

    spans = [(pil[i][1], pil[i + 1][0]) for i in range(len(pil) - 1)]
    # The cornice is the first board; every board after it carries a shelf.
    stands = [b[0] for b in brd[1:]]
    tops = [brd[i][1] for i in range(len(brd) - 1)]

    print(f"plate {w}x{h}   pilasters {pil}   boards {brd}")
    print("NicheStandY = { " + ", ".join(f"{(h - s) * 2:.0f}f" for s in stands) + " };")
    print("NicheHeight = { " + ", ".join(f"{(s - t) * 2:.0f}f"
                                         for s, t in zip(stands, tops)) + " };")
    print("NicheSpanX  = { " + ", ".join(f"new Vector2({a * 2:.0f}f, {b * 2:.0f}f)"
                                         for a, b in spans) + " };")


if __name__ == "__main__":
    measure(sys.argv[1] if len(sys.argv) > 1 else PLATE)
