# -*- coding: utf-8 -*-
"""Derive the three certificate stocks from the ONE good PixelLab take (2026-09-26).

The pro takes proved the alpha cutter eats near-white sheets (worn came back 4%% opaque,
clean 0%%); only the ivory sheet survived, and its interior is one flat tone - which is
the bill's own family (flat stock + a deckled edge). So, per the recipe "the generator
paints STOCK, code cuts the SILHOUETTE", the ivory take is the DONOR: its teeth and its
edge shading are re-laid onto a 512-wide canvas (mirror-repeat, so the teeth keep their
drawn scale at the sheet's exact 2x), and the three stocks are recoloured from it:

  cert_paper_ivory  - the donor's own pale ivory, plus faint laid lines (the premium sheet)
  cert_paper_clean  - multiplied to the slip's warm white, sparse fibre flecks
  cert_paper_worn   - multiplied to a yellowed tone, the worn take's REAL foxing specks
                      blended in near the edges, and the edge itself browned

Binary alpha everywhere; the deckle band lives in the outer 12px so the sprite slices.
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "raw")
OUT = os.path.join(HERE, "out")
W, BAND = 512, 12

IVORY_FLAT = (251, 248, 229)          # the donor's interior
CLEAN_MUL = (0.980, 0.972, 0.987)     # -> ~#F6F1E2, the slip's warm white
WORN_MUL = (0.916, 0.887, 0.838)      # -> ~#E6DCC0, yellowed


def _seq():
    s = [0x2545F491]
    def r():
        s[0] ^= (s[0] << 13) & 0xFFFFFFFF
        s[0] ^= s[0] >> 17
        s[0] ^= (s[0] << 5) & 0xFFFFFFFF
        return s[0] / 0xFFFFFFFF
    return r


def donor():
    im = Image.open(os.path.join(RAW, "cert_paper_ivory_0.png")).convert("RGBA")
    im.putalpha(im.getchannel("A").point(lambda v: 255 if v >= 128 else 0))
    return im.crop(im.getbbox())


def widen(sheet):
    """The donor's teeth, mirror-repeated onto a 512-wide canvas at their drawn scale."""
    dw, dh = sheet.size
    sp = sheet.load()
    out = Image.new("RGBA", (W, dh), (0, 0, 0, 0))
    op = out.load()
    inner = dw - 2 * BAND
    for x in range(W):
        if x < BAND:
            sx = x
        elif x >= W - BAND:
            sx = dw - (W - x)
        else:
            m = (x - BAND) % (2 * inner)
            sx = BAND + (m if m < inner else 2 * inner - 1 - m)
        for y in range(dh):
            op[x, y] = sp[sx, y]
    return out


def mul(im, f):
    out = im.copy()
    px = out.load()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (min(255, round(r * f[0])), min(255, round(g * f[1])),
                            min(255, round(b * f[2])), 255)
    return out


def edge_dist_leq(px, w, h, x, y, d):
    for dy in range(-d, d + 1):
        for dx in range(-d, d + 1):
            nx, ny = x + dx, y + dy
            if nx < 0 or ny < 0 or nx >= w or ny >= h or px[nx, ny][3] == 0:
                return True
    return False


def _speck_palette():
    """The worn take's own foxing inks, by frequency - the one thing that take gave us."""
    src = Image.open(os.path.join(RAW, "cert_paper_worn_0.png")).convert("RGBA").crop((136, 8, 376, 300))
    counts = {}
    for r, g, b, a in src.getdata():
        if a >= 128:
            counts[(r, g, b)] = counts.get((r, g, b), 0) + 1
    return [c for c, _ in sorted(counts.items(), key=lambda kv: -kv[1])[:6]]


def worn_from(base):
    im = mul(base, WORN_MUL)
    px = im.load()
    w, h = im.size
    rnd = _seq()
    # mottling first: soft seeded blotches, thicker toward the edges, a step darker
    for _ in range(60):
        cx, cy = rnd() * w, rnd() * h
        edge = min(cx, w - cx, cy, h - cy) / min(w, h)
        if rnd() < edge * 2.2:
            continue
        rad = 3 + rnd() * 11
        for y in range(max(0, int(cy - rad)), min(h, int(cy + rad + 1))):
            for x in range(max(0, int(cx - rad)), min(w, int(cx + rad + 1))):
                dx, dy = x - cx, y - cy
                if dx * dx + dy * dy > rad * rad:
                    continue
                r, g, b, a = px[x, y]
                if a:
                    px[x, y] = (round(r * 0.965), round(g * 0.96), round(b * 0.945), 255)
    # foxing in the take's own inks: specks gathered toward the edges, never a stripe
    inks = _speck_palette()
    for _ in range(340):
        fx, fy = rnd(), rnd()
        edge = min(fx, 1 - fx, fy, 1 - fy)
        if rnd() < edge * 3.0:
            continue
        x, y = int(fx * w), int(fy * h)
        ink = inks[int(rnd() * len(inks)) % len(inks)]
        for dx, dy, k in ((0, 0, 0.62), (1, 0, 0.4), (0, 1, 0.32)):
            if rnd() < (0.4 if dx or dy else 1.0):
                nx, ny = x + dx, y + dy
                if nx < w and ny < h:
                    r, g, b, a = px[nx, ny]
                    if a:
                        px[nx, ny] = (round(r * (1 - k) + ink[0] * k),
                                      round(g * (1 - k) + ink[1] * k),
                                      round(b * (1 - k) + ink[2] * k), 255)
    # the browned edge: every opaque pixel within a step or two of the outside
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if not a:
                continue
            if edge_dist_leq(px, w, h, x, y, 1):
                px[x, y] = (round(r * 0.82), round(g * 0.80), round(b * 0.74), 255)
            elif edge_dist_leq(px, w, h, x, y, 3):
                px[x, y] = (round(r * 0.92), round(g * 0.91), round(b * 0.88), 255)
    return im


def clean_from(base):
    im = mul(base, CLEAN_MUL)
    px = im.load()
    w, h = im.size
    rnd = _seq()
    flat = tuple(min(255, round(c * f)) for c, f in zip(IVORY_FLAT, CLEAN_MUL))
    for y in range(BAND, h - BAND):
        for x in range(BAND, w - BAND):
            if px[x, y][:3] != flat:
                continue
            v = rnd()
            if v < 0.004:
                px[x, y] = (239, 230, 208, 255)   # a dark fibre fleck
            elif v < 0.007:
                px[x, y] = (254, 252, 244, 255)   # a light one
    return im


def ivory_from(base):
    im = base.copy()
    px = im.load()
    w, h = im.size
    for y in range(BAND, h - BAND):
        if y % 7 != 3:
            continue
        for x in range(BAND, w - BAND):
            if px[x, y][:3] == IVORY_FLAT:
                px[x, y] = (254, 252, 238, 255)   # the laid line, one tone up
    return im


def posterize(im, tones):
    """The blends back onto a short ink list, the way every shipped Items sheet is."""
    rgb = im.convert("RGB").quantize(colors=tones, method=Image.MEDIANCUT,
                                     dither=Image.Dither.NONE).convert("RGB")
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    op, qp, ap = out.load(), rgb.load(), im.load()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            if ap[x, y][3]:
                op[x, y] = qp[x, y] + (255,)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    base = widen(donor())
    for name, im in (("cert_paper_worn", posterize(worn_from(base), 24)),
                     ("cert_paper_clean", clean_from(base)),
                     ("cert_paper_ivory", ivory_from(base))):
        assert im.size[0] == W
        alphas = set(p[3] for p in im.getdata())
        assert alphas <= {0, 255}, (name, sorted(alphas)[:5])
        im.save(os.path.join(OUT, name + ".png"))
        n = len([c for c in im.getcolors(maxcolors=100000) if c[1][3]])
        print(name, im.size, "tones:", n)


if __name__ == "__main__":
    main()
