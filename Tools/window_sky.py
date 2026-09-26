# -*- coding: utf-8 -*-
"""THE VIEW OUT OF THE WINDOW, REBUILT AS A CLOCK (2026-09-17).

The author: "gunes batmiyor yok oluyor, hava renk degisimi daha smooth olmali, gunes asagi
dogru batmali, sehir isiklari ona gore yanmali. arada gokyuzunde M seklinde ucan kuslar."

The 31 PixelLab frames in Scene/window_cycle.png did three things at once - the sky, the
sun and the city - and could only step between whole pictures, so the sun faded out where
it stood and the sky changed in 31 clunks. From here on the view is COMPOSED: the SKY is
drawn by the stage every frame from a small model (palette bands, ordered dither, a sun
disc that sinks, stars that come out, clouds that drift) and the CITY is the author's own
skyline: since 2026-09-22 ONE drawing (the sheet's night frame) that this tool walks through
the evening - the golden frame's purples at opening, the night's navy by the small hours, its
windows coming on one by one - as thirty-one frames the stage steps and blends between.
Nothing in the sky is a bitmap.

This file is the tool AND the twin: `derive` cuts the city layers, and `preview` renders
the very same model the stage renders (WindowSky.cs is a port of `render_sky` below) so the
look can be judged on a contact sheet before the editor ever compiles it. When the two
disagree, the C# is wrong - the constants are read from the same json.

  py -3 Tools/window_sky.py derive      window_cycle.png -> Resources/Scene/window_city.bytes (+ preview)
  py -3 Tools/window_sky.py preview     the evening at ten hours -> Tools/window_sky_preview.png
"""
import io
import json
import math
import os
import struct
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SHEET = os.path.join(ROOT, 'Tools', 'AssetPipeline', 'sources', 'window_cycle.png')   # moved out of the build 2026-09-26 (the weight pass)
CITY_OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'window_city.bytes')
MODEL = os.path.join(ROOT, 'Assets', 'Resources', 'Data', 'sky_cycle.json')
PREVIEW = os.path.join(HERE, 'window_sky_preview.png')
CITY_PREVIEW = os.path.join(HERE, 'window_city_preview.png')

# The sheet's cells are the room's window hole at 2x of the source panorama: 141x274 of
# glass showing 71x137 source pixels (Tools/window_cycle.py flat()). Everything here works
# at SOURCE resolution; the stage doubles it, the way the sheet already did.
CELL_W, CELL_H = 141, 274
SRC_W, SRC_H = 71, 137
ZOOM = 2
FRAME_COUNT = 31

# The 55 colours, by ramp (UITheme, GDD 14 v3 s3). Every pixel the sky puts up is one of these.
RAMPS = {
    'Night': (0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160),
    'Magenta': (0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6),
    'Cyan': (0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3),
    'Amber': (0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B),
    'ViceRed': (0x3D1220, 0x6E1B32, 0xA62B44, 0xD9455C, 0xF27D8A),
    'ClubBlue': (0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0),
    'Lime': (0x16331B, 0x2A5926, 0x479938, 0x6FCC4B, 0xA8F077),
    'Cream': (0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5),
    'Malt': (0x3A2410, 0x6B4416, 0x9E6A1D, 0xC98F2B, 0xE6B959),
    'Graphite': (0x14161A, 0x24272D, 0x383D45, 0x545A64, 0x808893),
    'Brick': (0x38161A, 0x5C2226, 0x7E3130, 0x9C4740, 0xB96253),
}


def rgb(hexv):
    return np.array([(hexv >> 16) & 255, (hexv >> 8) & 255, hexv & 255], float)


def token(name):
    """'Magenta3' -> the ramp's fourth step, as float RGB."""
    ramp, idx = name.rstrip('0123456789'), int(name[len(name.rstrip('0123456789')):])
    return rgb(RAMPS[ramp][idx])


# 4x4 Bayer, the pattern every dithered surface in the game already wears.
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], float) / 16.0


def load_model():
    with open(MODEL, encoding='utf-8') as f:
        return json.load(f)


# ── the city, cut from the author's frames ──────────────────────────────────

def frame(i):
    """Frame i of the sheet at SOURCE resolution (every second cell pixel)."""
    sheet = Image.open(SHEET).convert('RGBA')
    cols = sheet.size[0] // CELL_W
    r, c = divmod(i, cols)
    a = np.asarray(sheet.crop((c * CELL_W, r * CELL_H, (c + 1) * CELL_W, (r + 1) * CELL_H)))
    return a[::ZOOM, ::ZOOM, :3].astype(int)


def bright(px):
    """A lit window: warm and clearly above the wall around it, or plainly white."""
    s = px.sum(axis=-1)
    warm = px[..., 0] > px[..., 2] + 30
    return ((s > 210) & warm) | (s > 330)


def inpaint(img, holes):
    """Fill the lit windows with the dark of the wall around them: each hole takes the
    median of the non-hole pixels in a growing square, so a window switched off leaves
    building behind it and not a dark dot of nothing."""
    out = img.copy()
    h, w = holes.shape
    ys, xs = np.where(holes)
    for y, x in zip(ys, xs):
        for rad in (1, 2, 3, 5):
            y0, y1 = max(0, y - rad), min(h, y + rad + 1)
            x0, x1 = max(0, x - rad), min(w, x + rad + 1)
            block = img[y0:y1, x0:x1].reshape(-1, 3)
            ok = ~holes[y0:y1, x0:x1].reshape(-1)
            if ok.sum() >= 3:
                out[y, x] = np.median(block[ok], axis=0)
                break
    return out


def lum(a):
    return (a[..., 0] * 299 + a[..., 1] * 587 + a[..., 2] * 114) // 1000


def silhouette(night):
    """The towers off the NIGHT frame: the darkest thing in the view, below the upper sky. Every
    column is city from its first tower pixel down, and a one-pixel spike of sky between two
    towers is a mullion's worth of nothing."""
    rows = np.arange(SRC_H)[:, None]
    tower = (lum(night) < 20) & (rows >= 40)
    skyline = np.full(SRC_W, SRC_H, int)
    for x in range(SRC_W):
        hit = np.where(tower[:, x])[0]
        if len(hit):
            skyline[x] = int(hit.min())
    for x in range(1, SRC_W - 1):
        skyline[x] = min(skyline[x], max(skyline[x - 1], skyline[x + 1]) + 6)
    city = np.zeros((SRC_H, SRC_W), bool)
    for x in range(SRC_W):
        city[skyline[x]:, x] = True
    return city, skyline


def dusk_silhouette(day):
    """The golden frame's own city, for its colours only (the old cut)."""
    R, B = day[:, :, 0], day[:, :, 2]
    rows = np.arange(SRC_H)[:, None]
    purple = (B > R + 10) & (R < 170) & (rows >= 40)
    city = np.zeros((SRC_H, SRC_W), bool)
    for x in range(SRC_W):
        hit = np.where(purple[:, x])[0]
        if len(hit):
            city[int(hit.min()):, x] = True
    return city


def by_rank(src, src_mask, ref, ref_mask):
    """Each src pixel takes the ref colour at its own luminance rank: the dusk frame's palette laid
    over the night frame's drawing, dark for dark and light for light, whole colours only."""
    out = src.copy()
    ys, xs = np.where(src_mask)
    if not len(ys):
        return out
    ref_px = ref[ref_mask]
    ref_px = ref_px[np.argsort(lum(ref_px), kind='stable')]
    order = np.argsort(lum(src[ys, xs]), kind='stable')
    n, m = len(order), len(ref_px)
    for rank, k in enumerate(order):
        out[ys[k], xs[k]] = ref_px[min(m - 1, int(rank * m / n))]
    return out


def clusters(lit):
    """4-connected runs of lit pixels: a window is one light, so it comes on as one."""
    label = np.full(lit.shape, -1, int)
    n = 0
    for y, x in zip(*np.where(lit)):
        if label[y, x] >= 0:
            continue
        stack = [(y, x)]
        label[y, x] = n
        while stack:
            cy, cx = stack.pop()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < SRC_H and 0 <= nx < SRC_W and lit[ny, nx] and label[ny, nx] < 0:
                    label[ny, nx] = n
                    stack.append((ny, nx))
        n += 1
    return label, n


def streaks():
    """Thin clouds for the upper sky: long, one or two rows, tapered - the flat stratus a Miami
    sunset lays over the sea. The golden frame's own cloud pixels, cut out and drifted, read as
    dark glyphs once the sky under them went smooth (2026-09-22, eighth list)."""
    cloud = np.zeros((SRC_H, SRC_W), bool)
    for i, (row, x0, length, thick) in enumerate(((9, 4, 26, 2), (17, 38, 22, 1), (24, 12, 30, 2),
                                                  (31, 44, 18, 1), (38, 0, 20, 1), (44, 30, 24, 1))):
        for dx in range(length):
            x = (x0 + dx) % SRC_W
            end = min(dx, length - 1 - dx)
            t = thick if end >= 3 else 1
            for dy in range(t):
                if end >= 1 or hash01(x, row, 71 + i) > 0.5:
                    cloud[row + dy, x] = True
    return cloud


def derive():
    """ONE CITY, ALL NIGHT (2026-09-22, the author's eighth list: "Şehir silüetini geliştirelim şu an
    karman çorman pixellere benziyor önceki gibi bir şehir görüntüsü olmalı").

    The thirty-one frames were each drawn by PixelLab on its own, so their towers do not stand in the
    same places: frame 20's skyline is not frame 0's. The first cut kept one mask (the golden frame's)
    and showed every frame's pixels through it, so from the blue hour on the towers were full of that
    frame's sky and the sky of that frame's towers - the jumble the author saw, and worse once the
    frames were cross-faded. Now the city is ONE drawing, the night frame's (the one with the most in
    it: the towers' lit grids and the town's lamps), and the evening is done TO it:

      the bodies  walk from the golden frame's purples (laid on by luminance rank, zone by zone, so a
                  tower stays a tower and a roof a roof) to the night frame's own navy over the dusk;
      the lights  come on window by window, each 4-connected run of lit pixels as one light at its own
                  hour through the blue hour, and a fifth of them go dark again in the small hours.

    Thirty-one frames of that one drawing ship in the old LCS2 layout, so the stage is unchanged and its
    cross-fade between neighbours is now a fade of the same towers rather than a ghost of two skylines.
    """
    day, night = frame(0), frame(30)
    city, skyline = silhouette(night)
    town_top = int(np.median(skyline[skyline < SRC_H])) + 14      # below the towers' feet: the town
    rows = np.arange(SRC_H)[:, None]

    lit = city & (lum(night) >= 26)
    unlit = inpaint(night, lit)
    day_city = dusk_silhouette(day) & ~bright(day)
    # THE DUSK IS THE NIGHT DRAWING IN THE GOLDEN FRAME'S LIGHT: zone by zone, the unlit night city is
    # moved to the golden frame's own mean colour there and its contrast kept (a little more for the
    # towers, which the sun behind them outlines), so every edge of the drawing survives the evening.
    # (Laying the golden palette on by luminance rank was tried first: the towers went flat and the town
    # came out in blotches, because the two frames' tones are not spread the same way.)
    looks = []
    for zone, gain in ((rows < town_top, 1.25), (rows >= town_top, 1.0)):
        here = city & np.broadcast_to(zone, city.shape)
        mine = unlit[here & ~lit].mean(axis=0)
        theirs = day[day_city & np.broadcast_to(zone, city.shape)].mean(axis=0)
        looks.append((unlit.astype(float) - mine) * gain + theirs)
    # the towers' feet run into the town over eight rows rather than along a ruled line
    wt = np.clip((rows - (town_top - 4)) / 8.0, 0.0, 1.0)[:, :, None]
    dusk = np.clip(looks[0] * (1.0 - wt) + looks[1] * wt, 0, 255)

    label, n = clusters(lit)
    on_at = np.array([0.10 + 0.52 * hash01(k, 3, 17) for k in range(n)])
    off_at = np.array([0.80 + 0.18 * hash01(k, 5, 23) if hash01(k, 7, 29) < 0.2 else 9.0 for k in range(n)])

    frames = []
    for i in range(FRAME_COUNT):
        h = i / float(FRAME_COUNT - 1)
        w = smoothstep(0.08, 0.56, h)
        body = dusk * (1.0 - w) + unlit * w
        img = body.copy()
        ys, xs = np.where(lit)
        for y, x in zip(ys, xs):
            k = label[y, x]
            on = min(1.0, max(0.0, (h - on_at[k]) / 0.05)) * (1.0 - min(1.0, max(0.0, (h - off_at[k]) / 0.05)))
            img[y, x] = body[y, x] * (1.0 - on) + night[y, x] * on
        frames.append(np.clip(np.round(img), 0, 255).astype(np.uint8))

    mask = city.astype(np.uint8)
    cloud = streaks() & ~city
    with open(CITY_OUT, 'wb') as f:
        f.write(b'LCS2')
        f.write(struct.pack('<HHH', SRC_W, SRC_H, FRAME_COUNT))
        f.write(mask.tobytes())
        f.write(cloud.astype(np.uint8).tobytes())
        f.write(np.minimum(skyline, 255).astype(np.uint8).tobytes())
        for fr in frames:
            f.write(fr[city].astype(np.uint8).tobytes())      # mask order: row-major over city px
    print('wrote %s: %d frames x %d city px, %d lights in %d windows, %d cloud px, skyline rows %d..%d'
          % (os.path.relpath(CITY_OUT, ROOT), FRAME_COUNT, int(city.sum()), int(lit.sum()), n,
             int(cloud.sum()), int(skyline.min()), int(skyline[skyline < SRC_H].max())))

    # A contact sheet of what was made: the city at six hours over a flat sky of its hour.
    tiles = []
    for i in (0, 6, 12, 18, 24, 30):
        t = np.zeros((SRC_H, SRC_W, 4), np.uint8)
        t[:, :, :3] = np.array([60, 30, 70]) if i < 12 else np.array([30, 24, 60])
        t[:, :, 3] = 255
        t[city, :3] = frames[i][city]
        t[cloud, :3] = (t[cloud, :3] * 0.8).astype(np.uint8)
        tiles.append(t)
    sheet = Image.new('RGBA', (SRC_W * 4 * len(tiles) + 8 * (len(tiles) - 1), SRC_H * 4), (20, 20, 20, 255))
    for i, t in enumerate(tiles):
        sheet.paste(Image.fromarray(t).resize((SRC_W * 4, SRC_H * 4), Image.NEAREST), (i * (SRC_W * 4 + 8), 0))
    sheet.save(CITY_PREVIEW)
    print('preview', os.path.relpath(CITY_PREVIEW, ROOT))


def read_city():
    with open(CITY_OUT, 'rb') as f:
        assert f.read(4) == b'LCS2'
        w, h, count = struct.unpack('<HHH', f.read(6))
        n = w * h
        mask = np.frombuffer(f.read(n), np.uint8).reshape(h, w)
        cloud = np.frombuffer(f.read(n), np.uint8).reshape(h, w).astype(bool)
        skyline = np.frombuffer(f.read(w), np.uint8).astype(int)
        c = int((mask == 1).sum())
        frames = [np.frombuffer(f.read(c * 3), np.uint8).reshape(c, 3).copy() for _ in range(count)]
    return frames, mask, cloud, skyline


# ── the model ────────────────────────────────────────────────────────────────

def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def hash01(x, y, salt):
    """A stable per-pixel number in [0,1) — the same one WindowSky.cs computes."""
    h = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    h ^= h >> 16
    return (h & 0xFFFF) / 65536.0


class Sky:
    """The evening as a function of tau (0 at 18:00, 1 at 02:00) - see sky_cycle.json."""

    def __init__(self, model):
        self.keys = model['keys']
        self.stops_v = np.array(model['stopsV'], float)
        self.sun = model['sun']
        self.stars = model['stars']
        self.city = model['city']
        self.palette = np.array([token(n) for n in model['skyPalette']])

    def key_colours(self, k):
        return np.array([token(n) for n in k['stops']])

    def bands(self, tau):
        """The continuous top->horizon ramp at this hour: (5,3) RGB stops."""
        keys = self.keys
        if tau <= keys[0]['t']:
            return self.key_colours(keys[0])
        for i in range(len(keys) - 1):
            a, b = keys[i], keys[i + 1]
            if a['t'] <= tau <= b['t']:
                f = smoothstep(a['t'], b['t'], tau)
                return self.key_colours(a) * (1 - f) + self.key_colours(b) * f
        return self.key_colours(keys[-1])

    def sun_row(self, tau):
        s = self.sun
        return s['rowStart'] + (s['rowEnd'] - s['rowStart']) * min(1.0, tau / s['setBy'])

    def sun_colours(self, tau):
        s = self.sun
        f = smoothstep(0.0, s['setBy'], tau)
        core = token(s['coreHigh']) * (1 - f) + token(s['coreLow']) * f
        rim = token(s['rimHigh']) * (1 - f) + token(s['rimLow']) * f
        return core, rim

    def render(self, tau, clock, city, motion=True):
        frames, mask, cloud, skyline = city
        H, W = SRC_H, SRC_W
        stops = self.bands(tau)
        horizon = float(self.sun['horizonRow'])
        # The continuous sky, row by row: v runs 0 at the top to 1 at the skyline's base.
        ys = np.arange(H, dtype=float)
        v = np.clip(ys / horizon, 0, 1)
        img = np.zeros((H, W, 3), float)
        for c in range(3):
            img[:, :, c] = np.interp(v, self.stops_v, stops[:, c])[:, None]

        # THE SUN GOES DOWN. A disc with a rim, sinking on a straight line, and a halo that
        # warms the sky around it before the quantiser sees any of it - which is what turns
        # the halo into dithered rings rather than a painted glow.
        s = self.sun
        srow, scol, rad = self.sun_row(tau), float(s['col']), float(s['radius'])
        core, rim = self.sun_colours(tau)
        xs = np.arange(W, dtype=float)[None, :]
        d = np.sqrt((xs - scol) ** 2 + (ys[:, None] - srow) ** 2)
        halo = float(s['haloHigh']) * (1 - smoothstep(0.0, s['setBy'], tau)) + float(s['haloLow']) * smoothstep(0.0, s['setBy'], tau)
        fall = np.clip(1 - d / float(s['haloRadius']), 0, 1) ** 2 * halo
        halo_col = token(s['halo']) if s.get('halo') else core
        img = img * (1 - fall[:, :, None]) + halo_col[None, None, :] * fall[:, :, None]
        disc = d <= rad
        rimmask = disc & (d > rad - 1.6)
        img[disc] = core
        img[rimmask] = rim

        # Clouds: a step darker than the sky they stand in, and they drift.
        drift = int((clock * float(self.city['cloudDrift'])) % (2 * W)) if motion else 0
        wide = np.concatenate([cloud, cloud[:, ::-1]], axis=1)
        shifted = np.roll(wide, drift, axis=1)[:, :W]
        img[shifted] *= 0.80

        # Quantise with the ordered pattern: nudge by the cell's threshold, take the nearest.
        thr = np.tile(BAYER, (H // 4 + 1, W // 4 + 1))[:H, :W]
        spread = float(self.city['ditherSpread'])
        nudged = img + ((thr - 0.5) * spread)[:, :, None]
        dist = ((nudged[:, :, None, :] - self.palette[None, None, :, :]) ** 2 * np.array([0.9, 1.2, 0.8])).sum(-1)
        idx = dist.argmin(-1)
        out = self.palette[idx].copy()

        # Stars come out where the sky has gone dark enough to show them.
        st = self.stars
        luma = (img * np.array([0.299, 0.587, 0.114])).sum(-1) / 255.0
        for i in range(int(st['count'])):
            sx = int(hash01(i, 1, 11) * W)
            sy = int(hash01(i, 2, 11) * float(st['rowMax']))
            birth = float(st['from']) + hash01(i, 3, 11) * (float(st['to']) - float(st['from']))
            if tau < birth or luma[sy, sx] > float(st['lumaMax']) or shifted[sy, sx]:
                continue
            phase = hash01(i, 4, 11) * 6.2832
            period = 2.2 + hash01(i, 5, 11) * 2.0
            tw = 0.5 + 0.5 * math.sin(clock * 6.2832 / period + phase) if motion else 1.0
            bright_star = hash01(i, 6, 11) > 0.6
            if tw > 0.55:
                out[sy, sx] = token('Cream4' if bright_star else 'Cream3')
            elif tw > 0.22:
                out[sy, sx] = token('Cream3' if bright_star else 'Cream2')

        # THE CITY, from the frame the hour is nearest to - the author's own progression,
        # a little behind the sun (frameLag) so the windows start as the disc touches the towers.
        cm = self.city
        lag = float(cm.get('frameLag', 0.0))
        idx = int(round(max(0.0, tau - lag) / max(1e-4, 1.0 - lag) * (len(frames) - 1)))
        idx = min(len(frames) - 1, max(0, idx))
        out[mask == 1] = frames[idx]
        return out.astype(np.uint8)


def birds_on(img, tau, clock, model):
    """A flock for the preview only: the stage flies them as sprites."""
    if tau > float(model['birds']['until']):
        return img
    ink = token('Night1').astype(np.uint8)
    frames = [
        ["X.....X", ".X...X.", "..X.X..", "...X..."],
        [".X...X.", "X.X.X.X", "...X...", "......."],
        ["..X.X..", ".X.X.X.", "X.....X", "......."],
    ]
    out = img.copy()
    for i in range(7):
        bx = int(50 - (clock * 9.0) % 90 + i * 6 - (i % 2) * 3)
        by = int(14 + i * 2 + 2 * math.sin(clock * 3 + i))
        f = frames[int((clock * 6 + i) % 3)]
        for yy, row in enumerate(f):
            for xx, ch in enumerate(row):
                if ch == 'X' and 0 <= by + yy < SRC_H and 0 <= bx + xx < SRC_W:
                    out[by + yy, bx + xx] = ink
    return out


def moon_on(img, tau, model):
    """The curtain's crescent, where the stage hangs it (12 source px of a 24 px sprite)."""
    m = model.get('moon')
    if not m or tau < float(m['from']):
        return img
    art = np.asarray(Image.open(os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'curtain_moon.png')).convert('RGBA'))
    p = min(1.0, (tau - float(m['from'])) / max(1e-4, 1.0 - float(m['from'])))
    x = float(m['xStart']) + (float(m['xEnd']) - float(m['xStart'])) * p
    y = float(m['yStart']) + (float(m['yEnd']) - float(m['yStart'])) * p - float(m['arc']) * math.sin(p * math.pi)
    a = min(1.0, (tau - float(m['from'])) / max(1e-4, float(m['fade'])))
    out = img.copy()
    h, w = art.shape[:2]
    for yy in range(0, h, ZOOM):
        for xx in range(0, w, ZOOM):
            px = art[yy, xx]
            if px[3] < 128 or a < 0.5:
                continue
            ty, tx = int(y - h / (2 * ZOOM) + yy / ZOOM), int(x - w / (2 * ZOOM) + xx / ZOOM)
            if 0 <= ty < SRC_H and 0 <= tx < SRC_W:
                out[ty, tx] = px[:3]
    return out


def preview():
    model = load_model()
    sky = Sky(model)
    city = read_city()
    hours = [0.0, 0.06, 0.12, 0.17, 0.22, 0.28, 0.36, 0.46, 0.62, 0.85, 1.0]
    pad = 6
    sheet = Image.new('RGBA', ((CELL_W + pad) * len(hours), CELL_H + 40), (12, 8, 16, 255))
    from PIL import ImageDraw
    draw = ImageDraw.Draw(sheet)
    for i, tau in enumerate(hours):
        img = sky.render(tau, 3.0 + i * 1.7, city)
        img = birds_on(img, tau, 3.0 + i * 1.7, model)
        img = moon_on(img, tau, model)
        tile = Image.fromarray(img).resize((CELL_W, CELL_H), Image.NEAREST)
        sheet.paste(tile, (i * (CELL_W + pad), 0))
        h = 18 + tau * 8
        draw.text((i * (CELL_W + pad) + 4, CELL_H + 6), '%02d:%02d' % (int(h) % 24, int((h % 1) * 60)), fill=(230, 220, 200, 255))
    sheet.save(PREVIEW)
    print('preview', os.path.relpath(PREVIEW, ROOT))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'preview'
    if cmd == 'derive':
        derive()
    elif cmd == 'preview':
        preview()
    else:
        raise SystemExit(__doc__)
