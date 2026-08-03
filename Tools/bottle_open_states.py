# -*- coding: utf-8 -*-
"""
Open-mouth variants, derived from the APPROVED closed bottles.

The set shipped with its open states cut rather than drawn: five of them were the
closed sprite with the top rows erased and nothing put back, and six vessels had no
open state at all. This takes the closure off at the seam where the glass starts and
draws a mouth in the bottle's own palette — a lip, the bore behind it, and the far
rim catching the light — so an open bottle is a bottle that has been opened rather
than a bottle that has been cropped.

Nothing is rescaled and the canvas is never resized: the open sprite has to line up
with the closed one pixel for pixel, or a bottle jumps when it is uncorked.
"""
from PIL import Image
import json, io, os, sys

D = 'Assets/Resources/Items'
KEGS = {'lager', 'pale_ale', 'stout'}          # a keg has no cap to take off
# Nothing here has a closure worth taking off: a carton and a can have no mouth to
# draw, and the house syrup already stands with a pour spout in its neck. The pour
# stage falls back to the closed shot, which for these IS the open one.
SEALED = {'orange', 'lemon', 'lime', 'pineapple', 'cranberry', 'energy', 'cola',
          'soda', 'syrup'}


def styles():
    d = json.load(io.open('Assets/Data/bottles/base_bar.json', encoding='utf-8'))
    return sorted({c.get('style') for c in d['cards'] if c.get('style')} - KEGS - SEALED)


def brands():
    """The tier bottles, bot_{brand}.png. Their open shots were generated separately at
    first, and the packs drew a DIFFERENT bottle open than shut often enough that the
    pour stage did not match the shelf (the author, 2026-08-03: 'raftaki versiyonları
    ile içki koyma sahnesindeki görseller bir değil'). Deriving the open state from the
    shut art is what makes them the same bottle by construction."""
    return sorted(f[:-4] for f in os.listdir(D)
                  if f.startswith('bot_') and f.endswith('.png')
                  and not f.endswith('_open.png'))


def vessel_mask(im):
    W, H = im.size
    px = im.load()
    ok = [[px[x, y][3] >= 128 for y in range(H)] for x in range(W)]
    seen = [[False] * H for _ in range(W)]
    best = []
    for sx in range(W):
        for sy in range(H):
            if not ok[sx][sy] or seen[sx][sy]:
                continue
            st = [(sx, sy)]
            seen[sx][sy] = True
            comp = []
            while st:
                x, y = st.pop()
                comp.append((x, y))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and ok[nx][ny] and not seen[nx][ny]:
                        seen[nx][ny] = True
                        st.append((nx, ny))
            if len(comp) > len(best):
                best = comp
    m = [[False] * H for _ in range(W)]
    for x, y in best:
        m[x][y] = True
    return m


def attached(im, mask):
    """The vessel and whatever is stuck to it, corners included. The closure has to go
    by this rather than by the vessel alone — a cap meeting the neck only at its
    corners was left behind as a floating bar — but a sprig of olive leaves resting
    beside the jar touches nothing and stays where the artist put it."""
    W, H = im.size
    px = im.load()
    ok = [[px[x, y][3] >= 128 for y in range(H)] for x in range(W)]
    seen = [[False] * H for _ in range(W)]
    st = [(x, y) for x in range(W) for y in range(H) if mask[x][y]]
    for x, y in st:
        seen[x][y] = True
    while st:
        x, y = st.pop()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and ok[nx][ny] and not seen[nx][ny]:
                    seen[nx][ny] = True
                    st.append((nx, ny))
    return seen


def glass_colours(im, mask):
    """The tones the body is made of. A tone that reaches most of the body is glass;
    the cap's tones never appear down there, which is what gives away where it ends."""
    W, H = im.size
    px = im.load()
    rw = [sum(1 for x in range(W) if mask[x][y]) for y in range(H)]
    widest = max(rw)
    lo = min(y for y in range(H) if rw[y] >= widest * 0.8)
    bot = max(y for y in range(H) if rw[y] > 0)
    seen, top, low = {}, {}, {}
    for x in range(W):
        for y in range(lo, bot + 1):
            if not mask[x][y]:
                continue
            c = px[x, y][:3]
            seen[c] = seen.get(c, 0) + 1
            top[c] = min(top.get(c, y), y)
            low[c] = max(low.get(c, y), y)
    body = bot - lo + 1
    lum = lambda c: 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
    # The outline is not glass. It runs the whole body, so it passes the reach test, and
    # every closure is outlined too — leaving it in made the first row of every cap look
    # like glass and the caps all survived being taken off.
    return {c for c, n in seen.items()
            if n >= 20 and lum(c) >= 60 and (low[c] - top[c] + 1) > body * 0.72}, lo, bot


def neck_seam(im, mask, glass, shoulder):
    """
    Where the closure ends and the bottle begins.

    Read off the glass itself: a cap, a cork, a crown or a wax seal is made of tones
    that appear nowhere in the body, so the seam is the first row from which the glass
    runs UNBROKEN. Every row of the run has to be glass — testing the run on average
    let a single bright row inside a black cap through, and the mouth was then drawn in
    the cap's own colours.

    A clear glass stopper defeats colour, being made of the same glass as the bottle.
    Those give themselves away by shape instead: a bulb sitting above a waist.
    """
    W, H = im.size
    px = im.load()

    def glassy(y):
        xs = [x for x in range(W) if mask[x][y]]
        if len(xs) < 6:
            return None
        inner = xs[2:-2]
        if not inner:
            return None
        return sum(1 for x in inner if px[x, y][:3] in glass) / len(inner)

    y0 = None
    for y in range(shoulder):
        run = [glassy(k) for k in range(y, min(y + 6, shoulder + 1))]
        run = [v for v in run if v is not None]
        if len(run) >= 3 and min(run) >= 0.65:
            y0 = y
            break

    widths = [sum(1 for x in range(W) if mask[x][y]) for y in range(H)]
    if y0 is None or y0 <= 3:
        # the waist: the first place the vessel pinches in below its top, with the
        # bulb above it wider by a clear margin
        waist = None
        for y in range(2, max(3, shoulder - 1)):
            if widths[y] <= widths[y - 1] and widths[y] <= widths[y + 1]                     and max(widths[:y]) - widths[y] >= 3:
                waist = y
                break
        if waist is not None:
            y0 = waist
    if y0 is None:
        # last resort — a lid that is neither glass-toned nor perched on a waist still
        # sits on its own dark seating line (the olive jar)
        lum = lambda c: 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
        for y in range(1, shoulder):
            xs = [x for x in range(W) if mask[x][y]]
            if len(xs) >= 6 and sum(1 for x in xs if lum(px[x, y][:3]) < 70) >= len(xs) * 0.5:
                y0 = y + 1
                break
    if y0 is None:
        return None
    probe = min(y0 + 2, H - 1)
    row = [x for x in range(W) if mask[x][probe]] or [x for x in range(W) if mask[x][y0]]
    if not row:
        return None
    return y0, min(row), max(row)


def tones(im, mask, y, xl, xr):
    """Outline, face and highlight, taken from the neck itself so the mouth is drawn
    in the bottle's own palette rather than a palette of mine."""
    px = im.load()
    row = [px[x, y][:3] for x in range(xl, xr + 1) if mask[x][y]]
    if len(row) < 3:
        return None
    lum = lambda c: 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
    dark = min(row, key=lum)
    inner = row[1:-1] or row
    face = max(set(inner), key=lambda c: (inner.count(c), lum(c)))
    light = max(inner, key=lum)
    bore = tuple(int(face[i] * 0.42 + dark[i] * 0.58) for i in range(3))
    return dark, face, light, bore


def open_variant(im, force=False):
    W, H = im.size
    mask = vessel_mask(im)
    glass, shoulder, _ = glass_colours(im, mask)
    seam = neck_seam(im, mask, glass, shoulder)
    top = min(y for y in range(H) if any(mask[x][y] for x in range(W)))
    if seam is not None and seam[0] <= top + 3:
        seam = None
    if seam is None:
        # Nothing was found to take off. On a jar whose lid is drawn in the same greys
        # as its glass it is better to leave the vessel shut than to hang a mouth in
        # the air above a lid that is still on — that is the styles-mode answer. A tier
        # bottle, though, ALWAYS has a closure, and the one that defeats every test is
        # the cap drawn in the bottle's own colour with no waist under it (Thornwood's
        # green-on-green cylinder, 2026-08-03, found via the author's screenshots). A
        # same-colour cap already reads as the neck, so the mouth is drawn IN PLACE:
        # the top rows become the open ellipse and the silhouette stays the shelf's.
        if not force:
            return None
        y0 = top + 2
        row = [x for x in range(W) if mask[x][y0]]
        if len(row) < 6:
            return None
        xl, xr = min(row), max(row)
    else:
        y0, xl, xr = seam
    t = tones(im, mask, min(y0 + 3, H - 1), xl, xr)
    if t is None:
        return None
    dark, face, light, bore = t

    out = im.copy()
    op = out.load()
    ip = im.load()

    # Which tones belong to the closure: the ones that live above the seam and hardly
    # anywhere else. A wax seal drips PAST the seam and a foil skirt hangs below it, and
    # leaving those behind is how the bourbon kept red streaks running down an open neck.
    # A rope wrapped round a bottle's neck is mostly below the seam, so it stays.
    above, below = {}, {}
    for x in range(W):
        for y in range(H):
            if not mask[x][y]:
                continue
            c = ip[x, y][:3]
            (above if y < y0 else below)[c] = (above if y < y0 else below).get(c, 0) + 1
    closure = {c for c, n in above.items()
               if c not in glass and n >= 6 and n >= (n + below.get(c, 0)) * 0.7}

    # The closure comes off — everything above the seam, not just what is 4-connected to
    # the body. A cap that met the neck only at its corners was left behind as a bar
    # floating over the open bottle.
    reach = attached(im, mask)
    for x in range(W):
        for y in range(y0):
            if reach[x][y]:
                op[x, y] = (0, 0, 0, 0)

    for y in range(y0, min(y0 + 16, H)):
        run = [x for x in range(W) if mask[x][y]]
        if not run:
            continue
        edge = {min(run), max(run), min(run) + 1, max(run) - 1}
        for x in run:
            if ip[x, y][:3] in closure:
                op[x, y] = (dark if x in edge else face) + (255,)

    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H:
            op[x, y] = (c[0], c[1], c[2], 255)

    # The mouth is an ELLIPSE, because the shelf is drawn from slightly above — the
    # same reason the glasses got elliptical bottoms. Its depth follows its width, so a
    # wide-necked vodka opens into a wide oval and a slim tonic into a slit; drawing
    # every mouth two pixels deep made the wide ones read as another flat cap.
    neck_w = xr - xl + 1
    rx = neck_w / 2.0 + 1.0                       # the lip flares a pixel past the neck
    # deep enough to read as an opening, but a preserving jar's mouth is not a well:
    ry = min(max(1.5, rx * 0.42), 6.0)
    wall = max(1.0, rx * 0.17)                    # how thick the glass reads at the lip
    cx = (xl + xr) / 2.0
    depth = int(round(ry * 2))
    if y0 - depth < 0:
        depth = y0
        ry = depth / 2.0
    cy = y0 - ry

    def ellipse(x, y, ax, ay, oy=0.0):
        if ax <= 0 or ay <= 0:
            return 9.9
        dx = (x + 0.5 - cx) / ax
        dy = (y + 0.5 - (cy + oy)) / ay
        return dx * dx + dy * dy

    for y in range(max(0, y0 - depth), y0):
        for x in range(int(cx - rx) - 2, int(cx + rx) + 3):
            out_e = ellipse(x, y, rx, ry)
            if out_e > 1.55:
                continue
            # the bore sits a touch high, so the near lip is thicker than the far rim —
            # that asymmetry is what makes an oval read as an opening and not a disc
            bore_e = ellipse(x, y, rx - wall, max(0.8, ry - wall * 0.55), -0.35)
            if out_e > 1.0:
                put(x, y, dark)
            elif bore_e <= 1.0:
                put(x, y, bore)
            else:
                # the far half of the rim is turned towards the light, the near half away
                put(x, y, light if y + 0.5 < cy else face)
    return out


if __name__ == '__main__':
    # python Tools/bottle_open_states.py write                    the shelf styles
    # python Tools/bottle_open_states.py write bots               the tier bottles
    # python Tools/bottle_open_states.py write named soda cola    exactly these, SEALED
    #   or not - soda and cola moved to generated-full art (2026-08-03), and a full
    #   bottle derives an open state as readily as an empty one: the closure comes
    #   off, the mouth is drawn, the water stays where the artist painted it.
    write = len(sys.argv) > 1 and sys.argv[1] == 'write'
    which = sys.argv[2] if len(sys.argv) > 2 else 'styles'
    targets = (brands() if which == 'bots'
               else sys.argv[3:] if which == 'named'
               else styles())
    made, failed = [], []
    for s in targets:
        im = Image.open(f'{D}/{s}.png').convert('RGBA')
        # a tier bottle always has a closure, so its derivation may not give up
        o = open_variant(im, force=which == 'bots')
        if o is None:
            failed.append(s)
            continue
        made.append(s)
        if write:
            o.save(f'{D}/{s}_open.png')
    print(f'{len(made)} open variants' + (' written' if write else ' (dry run)'))
    if failed:
        print('no closure found on:', ', '.join(failed))
