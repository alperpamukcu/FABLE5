# The 2026-08-25 room batch, from the author's own art: three new wall-lamp marks, a
# brass sink to upgrade the steel one to, a rug for the floor and a drip mat for the bar.
#
# Sources are the author's Desktop "konsept art" drawings, copied into
# Tools/AssetPipeline/sources/konsept_art/ so the repo carries what shipped. Every piece
# is already drawn at its final size, so this ships them 1:1 rather than cropping —
# sink_fixture_gen.py had to crop because that one was drawn small on a 1024x576 canvas.
#
# WHAT IS CHECKED HERE, AND WHY (all three have already cost a placement):
#   * a lamp mark's ink must stay CENTRED on its 40x40 canvas. The stage stands a wall
#     piece by its canvas, not by its drawing, so a mark whose ink drifts down the sheet
#     hangs lower on the wall than the mark below it — the fitting appears to jump when
#     it is upgraded.
#   * the two sink rungs must share a silhouette to the pixel, or the upgrade slides in
#     its hole in the counter.
#   * nothing may change size without this script being read again: the rug and the mat
#     are placed against slot coordinates measured off these exact drawings.
#
# Re-running is idempotent: same sources, same bytes.

from PIL import Image
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Tools", "AssetPipeline", "sources", "konsept_art")
OUT = os.path.join(ROOT, "Assets", "Resources", "Fixtures")

#            source                  shipped as          expected size
PIECES = [
    ("wall_lamp_mark1.png", "fx_wall_lamp_lv0.png", (40, 40)),
    ("wall_lamp_mark2.png", "fx_wall_lamp_lv1.png", (40, 40)),
    ("wall_lamp_mark3.png", "fx_wall_lamp_lv2.png", (40, 40)),
    ("sink_gold.png",       "fx_sink_gold.png",     (82, 35)),
    ("floor_rug.png",       "fx_floor_rug.png",     (251, 30)),
    ("beer_mat.png",        "fx_beer_mat.png",      (119, 13)),
]


def centred(img):
    """The ink's centre on the canvas, in canvas units — 20.0/20.0 is dead centre of 40x40."""
    l, t, r, b = img.getbbox()
    return ((l + r) / 2.0, (t + b) / 2.0)


def main():
    shipped = {}
    for src_name, out_name, size in PIECES:
        img = Image.open(os.path.join(SRC, src_name)).convert("RGBA")
        if img.size != size:
            raise SystemExit(f"{src_name} is {img.size}, not the {size} the room is laid out "
                             f"against — re-read Tools/room_dressing_gen.py before shipping it.")
        # WRITTEN ONLY WHEN THE PIXELS ACTUALLY DIFFER. Pillow re-encodes on every save,
        # so an unchanged drawing still lands as new BYTES — and mark 3's palm, which this
        # batch did not touch, showed up in the diff as if it had been redrawn. A pipeline
        # that dirties files it did not change is a pipeline nobody can read a diff from.
        dest = os.path.join(OUT, out_name)
        old = Image.open(dest).convert("RGBA") if os.path.exists(dest) else None
        if old is None or old.size != img.size or old.tobytes() != img.tobytes():
            img.save(dest)
            print(f"{src_name:24s} -> {out_name:24s} {img.size}  ink {img.getbbox()}")
        else:
            print(f"{src_name:24s} == {out_name:24s} {img.size}  unchanged, not rewritten")
        shipped[out_name] = img

    # The wall marks hang off one bracket, so they must be drawn about one point.
    for name in ("fx_wall_lamp_lv0.png", "fx_wall_lamp_lv1.png", "fx_wall_lamp_lv2.png"):
        cx, cy = centred(shipped[name])
        if abs(cx - 20.0) > 1.0 or abs(cy - 20.0) > 1.0:
            raise SystemExit(f"{name}'s ink centres at ({cx}, {cy}) and not on the canvas "
                             "centre — this mark would hang off the bracket the others use.")
        print(f"{name:24s} ink centre ({cx:.1f}, {cy:.1f}) — on the bracket")

    # The two sink rungs sit in one hole in the counter.
    steel = Image.open(os.path.join(OUT, "fx_sink.png")).convert("RGBA")
    gold = shipped["fx_sink_gold.png"]
    if steel.size != gold.size or list(steel.getchannel("A").getdata()) \
            != list(gold.getchannel("A").getdata()):
        raise SystemExit("the brass sink does not share the steel one's silhouette — the "
                         "upgrade would move in its hole.")
    print("fx_sink_gold.png         shares fx_sink.png's silhouette to the pixel")


if __name__ == "__main__":
    main()
