"""
The room, the bar and the props that stand on it, composited exactly the way
DiegeticStage lays them out at 16:9 - so a prop can be judged AT SIZE without
entering play mode.

It is a measuring stick, not a renderer: no lights, no patrons, no HUD. What it is
faithful about is the geometry, because that is what the props are being judged for
- whether the tower stands ON the bar rather than in it, and whether the till reads
at 57 units wide.

    python Tools/stage_preview.py [out.png]

The three numbers below are the same three DiegeticStage keeps, and they are read
from it rather than copied by hand wherever the file makes that possible.
"""
import os, re, sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STAGE = os.path.join(ROOT, "Assets", "Scripts", "UI", "Hud", "DiegeticStage.cs")
W, H = 640, 360


def const(name, default):
    """Reads a float const straight out of DiegeticStage, so this preview cannot drift
    from the stage it claims to be previewing."""
    src = open(STAGE, encoding="utf-8-sig").read()
    m = re.search(r"const\s+float\s+" + name + r"\s*=\s*(-?[\d.]+)f", src)
    return float(m.group(1)) if m else default


def slots():
    """The fixture hooks, out of the same file the game loads them from."""
    import json
    path = os.path.join(ROOT, "Assets", "Data", "fixtures", "fixtures.json")
    data = json.load(open(path, encoding="utf-8-sig"))
    return {s["id"]: s for s in data["slots"]}, {f["id"]: f for f in data["fixtures"]}


def compose(tower_ids=None, out=None):
    rest = const("CounterRestY", 116.0)
    inset = const("CounterSurfaceInset", 2.0)
    reg_x, reg_y = const("RegisterX", 604.0), const("RegisterBaseY", 104.0)

    frame = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    room = Image.open(os.path.join(ROOT, "Assets/Art/Backgrounds/club_room.png")).convert("RGBA")
    frame.alpha_composite(room, (0, 0))

    counter = Image.open(os.path.join(ROOT, "Assets/Art/Backgrounds/counter.png")).convert("RGBA")
    frame.alpha_composite(counter, (0, int(H - (rest + inset))))

    slot_by_id, fixture_by_id = slots()
    # The tower the room would actually stand: the LOWEST rung, which is the one the bar
    # opens owning. Pass ids to look at any other.
    towers = sorted((f for f in fixture_by_id.values() if f.get("tapLevel", 0) > 0),
                    key=lambda f: f["tapLevel"])
    for fid in (tower_ids or [t["id"] for t in towers[:1]]):
        f = fixture_by_id[fid]
        s = slot_by_id[f["slot"]]
        art = Image.open(os.path.join(ROOT, "Assets/Resources/Fixtures", f["sprite"] + ".png"))
        frame.alpha_composite(art.convert("RGBA"),
                              (int(s["x"] - art.width / 2), int(H - s["y"] - art.height)))

    till = Image.open(os.path.join(ROOT, "Assets/Art/Props/register2.png")).convert("RGBA")
    # The till is a CANVAS prop pinned to 57 units wide, so it is resampled here the
    # way the canvas resamples it rather than pasted at its own size.
    tw = 57
    th = int(round(tw * till.height / till.width))
    till = till.resize((tw, th), Image.NEAREST)
    frame.alpha_composite(till, (int(reg_x - tw / 2), int(H - reg_y - th)))

    out = out or os.path.join(ROOT, "Temp", "stage_preview.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    frame.convert("RGB").save(out)
    return out


if __name__ == "__main__":
    print(compose(out=sys.argv[1] if len(sys.argv) > 1 else None))
