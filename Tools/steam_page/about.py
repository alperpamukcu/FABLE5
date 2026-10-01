# -*- coding: utf-8 -*-
"""The store page's ABOUT THIS GAME column: neon section banners and animated GIFs, all 616 px wide (the
column's width), all built from the game's own sprites, clips and lines - Roxy's words are tour.json's.

    python3 Tools/steam_page/about.py      ->  Tools/steam_page/out/about/*.gif, *.png
"""
import os, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import scene as S
import compose as K

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'about')
W = 640          # the picture inside a kit panel: native 320 on a 2 px grid (the kit's 360 frame, at half its scale)
FPS = 12
ARCADE = os.path.join(S.ROOT, 'Assets', 'Fonts', 'MalibuArcade-Regular.ttf')


def tour_line(i, j=0):
    d = json.load(open(os.path.join(S.RES, 'Data', 'tour.json'), encoding='utf-8'))
    out = []

    def walk(o):
        if isinstance(o, dict):
            for k, v in o.items():
                if k == 'say':
                    out.append(v)
                else:
                    walk(v)
        elif isinstance(o, list):
            for x in o:
                walk(x)
    walk(d)
    return out[i][j]


# ── pixel text ───────────────────────────────────────────────────────────────────────────────────────
def mask_text(s, font, size):
    f = ImageFont.truetype(font, size)
    l, t, r, b = f.getbbox(s)
    m = Image.new('L', (r - l + 2, b - t + 2), 0)
    ImageDraw.Draw(m).text((-l, -t), s, font=f, fill=255)
    return np.asarray(m.point(lambda v: 255 if v > 127 else 0)) > 0


def neon_text(s, ramp='Magenta', scale=3, size=16, font=ARCADE):
    """A word as a lit tube: body ramp[3], core Cream[4] on the inner texels, the title sign's four bands."""
    m = mask_text(s, font, size)
    pad = 8
    m = np.pad(m, pad)
    a = np.zeros(m.shape + (4,), np.uint8)
    inner = m.copy()
    inner[1:] &= m[:-1]; inner[:-1] &= m[1:]; inner[:, 1:] &= m[:, :-1]; inner[:, :-1] &= m[:, 1:]
    a[m] = S.C('%s[3]' % ramp)
    a[inner] = S.C('Cream[4]') if ramp != 'Cyan' else S.C('Cream[4]')
    img = Image.fromarray(a, 'RGBA')
    halo = S.glow(m, S.C('%s[3]' % ramp), step=1)
    halo.alpha_composite(img)
    return S.up(halo.crop(halo.getbbox()), scale)


def wrap(s, font, size, width):
    f = ImageFont.truetype(font, size)
    words, lines, cur = s.split(), [], ''
    for w_ in words:
        t = (cur + ' ' + w_).strip()
        if f.getlength(t) <= width:
            cur = t
        else:
            lines.append(cur); cur = w_
    lines.append(cur)
    return lines


def bubble(s, shown, width=150, s_=2):
    """Roxy's plate: Cream[4] paper, Night[0] rim, Silkscreen 8px typed up to `shown` characters."""
    lines = wrap(s, S.FONT, 8, width - 10)
    h = 8 + 10 * len(lines)
    img = Image.new('RGBA', (width, h + 6), (0, 0, 0, 0))
    a = np.zeros((h + 6, width, 4), np.uint8)
    a[1:h - 1, 0:width] = S.C('Night[0]')
    a[0:h, 1:width - 1] = S.C('Night[0]')
    a[2:h - 2, 1:width - 1] = S.C('Cream[4]')
    a[1:h - 1, 2:width - 2] = S.C('Cream[4]')
    a[h - 2, 2:width - 2] = S.C('Cream[3]')
    # tail, bottom-right
    for k in range(6):
        a[h - 1 + k, width - 24 + k: width - 16] = S.C('Night[0]')
        a[h - 1 + k, width - 23 + k: width - 17] = S.C('Cream[4]') if k < 5 else S.C('Night[0]')
    img = Image.fromarray(a, 'RGBA')
    left = shown
    for i, ln in enumerate(lines):
        if left <= 0:
            break
        S.text(img, (6, 5 + i * 10), ln[:left], S.C('Night[2]'))
        left -= len(ln) + 1
    return S.up(img, s_)


def plate(s, colour='Amber[3]', ink='Night[0]', s_=2, size=8, bold=True):
    m = mask_text(s, S.FONT_BOLD if bold else S.FONT, size)
    h, w = m.shape
    a = np.zeros((h + 6, w + 10, 4), np.uint8)
    a[1:-1, :] = S.C(colour); a[:, 1:-1] = S.C(colour)
    a[3:3 + h, 5:5 + w][m] = S.C(ink)
    return S.up(Image.fromarray(a, 'RGBA'), s_)


def label(img, xy, s, colour, scale=2, bold=True):
    """Silkscreen at 8px, enlarged by a whole number - the HUD's own lettering size."""
    layer = Image.new('RGBA', (len(s) * 8 + 8, 12), (0, 0, 0, 0))
    S.text(layer, (0, 0), s, colour, bold=bold)
    layer = S.up(layer.crop(layer.getbbox() or (0, 0, 1, 1)), scale)
    x, y = xy
    if x == 'c':
        x = (img.width - layer.width) // 2
    img.alpha_composite(layer, (int(x), int(y)))
    return layer.width


# ── GIF writing ──────────────────────────────────────────────────────────────────────────────────────
def save_gif(frames, name, fps=FPS, holds=None):
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name + '.gif')
    import kit
    frames = [kit.panel_px(f.convert('RGB'), 2) for f in frames]
    # ONE palette for the whole clip, cut from a strip of sample frames: the painted backdrop then maps to the same
    # indices every frame, so the encoder only stores what moved (per-frame palettes made a still wall "change").
    picks = frames[::max(1, len(frames) // 8)][:8]
    sheet = Image.new('RGB', (frames[0].width, frames[0].height * len(picks)))
    for i, f in enumerate(picks):
        sheet.paste(f.convert('RGB'), (0, i * frames[0].height))
    master = sheet.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    pal = [f.convert('RGB').quantize(palette=master, dither=Image.Dither.NONE) for f in frames]
    durs = [int(1000 / fps)] * len(frames)
    if holds:
        for i, ms in holds.items():
            durs[i] = ms
    pal[0].save(p, save_all=True, append_images=pal[1:], duration=durs, loop=0, optimize=True, disposal=1)
    print('%-26s %dx%d  %3d frames  %.2f MB' % (os.path.basename(p), frames[0].width, frames[0].height,
                                                 len(frames), os.path.getsize(p) / 1e6))


def save_png(img, name):
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name + '.png')
    img.convert('RGB').save(p, optimize=True)
    print('%-26s %dx%d' % (os.path.basename(p), img.width, img.height))


# ── backdrops ────────────────────────────────────────────────────────────────────────────────────────
# The master composition's own bar (plate.py + keyart.counter): the steam_kit sunset behind the counter with the
# magenta neon lip. Patrons stand behind it, so the counter's strip is laid again in front of them.
_front = {}


def bar_bg(H, counter_y=0.84, sun_x=0.5, sun_r=None, horizon=0.60, patrons=None, patron_x=0.0, props_x=None,
           props_step=70, seed=3, night=0.30, sun_drop=0, dusk=0.0):
    import plate as PL
    import keyart as KA
    w, h = W // 2, H // 2
    img = PL.render(w, h, horizon=horizon, night=night, sun_x=sun_x, sun_r=sun_r or max(8, int(h * 0.16)),
                    skyline_spans=((0.0, 0.30), (0.74, 1.0)), skyline_h=max(10, int(h * horizon * 0.40)),
                    palms=(('left', 0.0, 1.0), ('right', 1.0, 1.0)), seed=seed, sun_drop=sun_drop)
    if dusk:
        img = S.tint(img, S.C('Night[0]'), dusk)
    cyn = int(round(h * counter_y))
    ctr, top = KA.counter(w, h - cyn)
    img.alpha_composite(ctr, (0, cyn))
    img.alpha_composite(KA.lip_glow(w, h, cyn + top))
    big = img.resize((w * 2, h * 2), Image.NEAREST)
    cy = cyn * 2
    _front[H] = big.crop((0, cy + 2 * top, W, H))
    _front[(H, 'y')] = cy + 2 * top
    if props_x is not None:
        from build_capsules import props
        props(big, cy, 2, props_x, props_step)
    return big, cy


def front(img, cy):
    """The counter's front face back over whatever stands behind it (its top and neon lip stay behind them)."""
    img.alpha_composite(_front[img.height], (0, _front[(img.height, 'y')]))


# ── 1 · Roxy walks in and says hello ─────────────────────────────────────────────────────────────────
def gif_meet_roxy():
    H = 360
    bg, cy = bar_bg(H, sun_x=0.3, patrons=None, props_x=24, props_step=64)
    walk = S.frames('hostess', 'walk')
    arrive = S.frames('hostess', 'arrive')
    talk = S.frames('hostess', 'talk')
    line = tour_line(0)
    frames = []
    x_end = 330
    x = W + 10
    def put(img, f, x):
        r = S.up(S.rim_light(f.crop((40, 0, 180, 216)), S.C('Magenta[4]'), 'left', 170), 2)
        K.paste_glow(img, r, x - r.width // 2, 14, 'Magenta[3]', 3)
    k = 0
    while x > x_end:
        img = bg.copy(); put(img, walk[k % len(walk)], x); frames.append(img); x -= 9; k += 1
    for f in arrive:
        img = bg.copy(); put(img, f, x_end); frames.append(img)
    n = len(line)
    t = 0
    shown = 0
    while shown < n + 30:
        img = bg.copy(); put(img, talk[(t // 2) % len(talk)] if shown < n else talk[0], x_end)
        b = bubble(line, shown, 150)
        img.alpha_composite(b, (x_end - b.width - 30, 40))
        frames.append(img); shown += 3; t += 1
    holds = {len(frames) - 1: 1600}
    save_gif(frames, '01_meet_roxy', holds=holds)


# ── 2 · read the card: the order lives behind it; the kid goes out the door ─────────────────────────
def gif_read_the_card():
    H = 360
    bg, cy = bar_bg(H, sun_x=0.78, patrons=None, counter_y=0.84)
    frames = []
    guests = [('clubgirl', S.id_card('clubgirl', 'Sabrina Voss', 24, 'us', 'United States'), 'GIN SOUR', False),
              ('harajuku', S.id_card('harajuku', 'Yui Kobayashi', 19, 'jp', 'Japan'), 'MOJITO', True)]
    for who, card, order, kid in guests:
        stand = S.frames(who, 'order') or S.frames(who, 'idle')
        arrive = S.frames(who, 'arrive')
        px = 120
        def guest(img, f):
            g = S.up(f.crop((40, 0, 180, 220)), 2)
            img.alpha_composite(S.tint(g, S.C('Night[2]'), 0.08), (px - 20, cy - int(g.height * 0.55)))
            front(img, cy)
        for f in arrive:
            img = bg.copy(); guest(img, f); frames.append(img)
        # the card rises
        c2 = S.up(card, 2) if False else card
        c2 = S.up(c2.crop((0, 0, c2.width, c2.height)), 1)
        cx, cy_card = 300, 70
        for k in range(8):
            img = bg.copy(); guest(img, stand[0])
            y = H - (H - cy_card) * (k + 1) // 8
            K.paste_glow(img, S.up(card, 1), cx, y, 'Amber[3]', 2)
            frames.append(img)
        # the order is read off the card
        for k in range(len(order) + 14):
            img = bg.copy(); guest(img, stand[(k // 1) % len(stand)])
            c = card.copy()
            S.text(c, (130, 62), 'WANTS', S.C('Magenta[1]'))
            S.text(c, (130, 74), order[:k], S.C('Magenta[2]'), bold=True)
            K.paste_glow(img, S.up(c, 1), cx, cy_card, 'Amber[3]', 2)
            if k > len(order) + 3:
                pl = plate('ORDER TAKEN', 'Cyan[4]') if not kid else plate('UNDER 20  -  KICK', 'Magenta[3]', 'Cream[4]')
                img.alpha_composite(pl, (cx + 128 - pl.width // 2, cy_card + 172))
            frames.append(img)
        frames += [frames[-1]] * 10
        if kid:
            leave = S.frames(who, 'upset')
            for f in leave:
                img = bg.copy(); guest(img, f)
                pl = plate('UNDER 20  -  KICK', 'Magenta[3]', 'Cream[4]')
                img.alpha_composite(pl, (cx + 128 - pl.width // 2, cy_card + 172))
                frames.append(img)
        else:
            for f in S.frames(who, 'cheer'):
                img = bg.copy(); guest(img, f)
                pl = plate('ORDER TAKEN', 'Cyan[4]')
                img.alpha_composite(pl, (cx + 128 - pl.width // 2, cy_card + 172))
                frames.append(img)
    save_gif(frames, '02_read_the_card')


# ── 3 · shake it, pour it, perfect ───────────────────────────────────────────────────────────────────
def gif_shake_and_pour():
    H = 360
    bg, cy = bar_bg(H, sun_x=0.2, counter_y=0.86, props_x=None)
    shaker = S.load('Items/shaker.png'); cap = S.load('Items/shaker_cap.png')
    tin = shaker.copy(); tin.alpha_composite(cap)
    tin = tin.crop(tin.getbbox())
    frames = []
    base_x, base_y = 230, cy - tin.height * 1 - 10
    mixbar = 0
    for k in range(36):
        img = bg.copy()
        dx = int(26 * np.sin(k * 1.3)); dy = int(10 * np.cos(k * 1.3))
        K.paste_glow(img, tin, base_x + dx, base_y + dy, 'Cyan[3]', 2)
        mixbar = min(100, k * 3.4)
        bar = Image.new('RGBA', (24, 150), S.C('Night[0]'))
        bar.alpha_composite(Image.new('RGBA', (20, 146), S.C('Night[3]')), (2, 2))
        fill = Image.new('RGBA', (20, int(146 * mixbar / 100)), S.C('Cyan[3]' if mixbar < 100 else 'Cyan[4]'))
        bar.alpha_composite(fill, (2, 148 - fill.height))
        K.paste_glow(img, bar, 440, base_y + 40, 'Cyan[3]', 2)
        label(img, (434, base_y + 14), 'MIX', S.C('Cream[4]'))
        if mixbar >= 100:
            pl = plate('SHAKEN', 'Cyan[4]')
            img.alpha_composite(pl, (404, base_y + 200))
        frames.append(img)
    # pour into the coupe
    for k in range(26):
        img = bg.copy()
        g = S.up(S.glass('coupe', 'Magenta[3]', min(0.85, k / 25 * 0.85) + 0.001), 2)
        gx, gy = 330, cy + 12 - g.height
        img.alpha_composite(g, (gx, gy))
        t = S.up(tin.rotate(-120, resample=Image.NEAREST, expand=True), 1)
        img.alpha_composite(t, (gx - t.width + 70, gy - t.height + 30))
        # the stream
        a = np.asarray(img).copy()
        sx = gx + g.width // 2 - 2
        a[gy - 40:gy + 20, sx:sx + 4] = S.C('Magenta[3]')
        img = Image.fromarray(a, 'RGBA')
        frames.append(img)
    # perfect: gold and magenta motes, the plate
    rng = np.random.RandomState(4)
    motes = [(rng.uniform(-1, 1), rng.uniform(-1.6, -0.4), rng.choice(['Amber[4]', 'Amber[3]', 'Magenta[4]'])) for _ in range(52)]
    for k in range(30):
        img = bg.copy()
        g = S.up(S.glass('coupe', 'Magenta[3]', 0.85, rim='sugar'), 2)
        gx, gy = 330, cy + 12 - g.height
        K.paste_glow(img, g, gx, gy, 'Amber[3]', 2)
        a = np.asarray(img).copy()
        for vx, vy, col in motes:
            x = int(gx + g.width / 2 + vx * k * 9)
            y = int(gy + 30 + vy * k * 7 + 0.35 * k * k)
            if 0 <= x < W - 4 and 0 <= y < H - 4:
                a[y:y + 4, x:x + 4] = S.C(col)
        img = Image.fromarray(a, 'RGBA')
        pl = plate('PERFECT', 'Amber[3]')
        img.alpha_composite(pl, (gx + g.width // 2 - pl.width // 2, 24))
        frames.append(img)
    save_gif(frames, '03_shake_and_pour', holds={len(frames) - 1: 1200})


# ── 4 · the tap: a pint is poured by its angle, its craft is the head ───────────────────────────────
def pint(liquid, head):
    g = S.load('Items/glass3d_pint.png')
    f = np.asarray(S.load('Items/glass3d_pint_fill.png'))[..., 3] > 0
    ys = np.nonzero(f.any(1))[0]
    top, bot = ys.min(), ys.max()
    span = bot - top
    lv = bot - int(span * liquid)
    hv = lv - int(span * head)
    a = np.zeros(f.shape + (4,), np.uint8)
    k = f.copy(); k[:lv] = False
    a[k] = S.C('Amber[2]')
    a[k & np.roll(~k, 1, 1)] = S.C('Amber[3]')
    kh = f.copy(); kh[:max(top, hv)] = False; kh[lv:] = False
    a[kh] = S.C('Cream[4]')
    a[kh & np.roll(~kh, -1, 0)] = S.C('Cream[3]')
    out = Image.fromarray(a, 'RGBA')
    out.alpha_composite(g)
    return out


def gif_pint():
    H = 360
    bg, cy = bar_bg(H, sun_x=0.75, counter_y=0.86)
    tap = S.load('Items/tap.png'); handle = S.load('Items/tap_handle.png')
    t = tap.copy(); t.alpha_composite(handle); t = t.crop(t.getbbox())
    frames = []
    N = 40
    for k in range(N + 22):
        img = bg.copy()
        q = min(1.0, k / N)
        p = pint(0.82 * q, 0.14 * min(1, q * 1.4) if q > 0.3 else 0.0)
        p2 = S.up(p.crop(p.getbbox()), 2)
        tx = 330
        img.alpha_composite(t, (tx, cy + 10 - t.height))
        gx = tx - p2.width - 24
        # the spout: a chrome arm from the tower out over the glass
        sy = cy + 10 - p2.height - 22
        arm = np.asarray(img).copy()
        sx = gx + p2.width // 2
        arm[sy:sy + 8, sx - 4:tx + 6] = S.C('Graphite[3]')
        arm[sy + 1:sy + 3, sx - 4:tx + 6] = S.C('Cream[3]')
        arm[sy:sy + 16, sx - 5:sx + 3] = S.C('Graphite[3]')
        arm[sy + 14:sy + 16, sx - 5:sx + 3] = S.C('Graphite[2]')
        img = Image.fromarray(arm, 'RGBA')
        img.alpha_composite(p2, (gx, cy + 10 - p2.height))
        if k < N:
            a = np.asarray(img).copy()
            a[sy + 16:cy + 10 - int(p2.height * 0.82 * q), gx + p2.width // 2 - 3:gx + p2.width // 2 + 1] = S.C('Amber[3]')
            img = Image.fromarray(a, 'RGBA')
        else:
            pl = plate('GOOD PINT', 'Cyan[4]')
            img.alpha_composite(pl, (gx + p2.width // 2 - pl.width // 2, 30))
            label(img, ('c', 76), 'HEAD 14%  -  100% FULL', S.C('Cream[4]'), bold=False)
        frames.append(img)
    save_gif(frames, '04_pull_a_pint', holds={len(frames) - 1: 1000})


# ── 5 · the crowd: every face reacts to the drink it got ─────────────────────────────────────────────
def gif_crowd():
    H = 360
    bg, cy = bar_bg(H, sun_x=0.5, counter_y=0.84)
    cast = [('kdance', 'cheer'), ('guard', 'drink'), ('gallerist', 'upset'), ('leopard', 'cheer'), ('bristol', 'drink')]
    clips = {w: S.frames(w, c) for w, c in cast}
    n = max(len(v) for v in clips.values())
    frames = []
    for loop in range(2):
        for k in range(n):
            img = bg.copy()
            for i, (w_, c) in enumerate(cast):
                f = clips[w_][(k + i * 3) % len(clips[w_])]
                g = S.up(f.crop((50, 0, 170, 220)), 1)
                img.alpha_composite(g, (12 + i * 120, cy - int(g.height * 0.72)))
            front(img, cy)
            for i, (w_, c) in enumerate(cast):
                em = {'cheer': '+$14', 'drink': '+$9', 'upset': 'TOO SWEET'}[c]
                col = {'cheer': 'Amber[3]', 'drink': 'Cream[3]', 'upset': 'Magenta[3]'}[c]
                if k > n // 3:
                    pl = plate(em, col, 'Night[0]' if c != 'upset' else 'Cream[4]')
                    img.alpha_composite(pl, (12 + i * 120 + 60 - pl.width // 2, 14))
            frames.append(img)
    save_gif(frames, '05_the_crowd', fps=14)


# ── 6 · a night: six till two, the sun goes, the till fills, the stars are filed ─────────────────────
def gif_night():
    H = 360
    frames = []
    N = 48
    for k in range(N + 24):
        q = min(1.0, k / N)
        h = H // 2
        r = max(8, int(h * 0.16))
        big, cy = bar_bg(H, sun_x=0.5, sun_r=r, sun_drop=int(q * (r + 1)), dusk=0.42 * q, night=0.30 + 0.25 * q)
        mins = int(18 * 60 + q * 8 * 60)
        hh, mm = (mins // 60) % 24, mins % 60 // 10 * 10
        clock = neon_text('%02d:%02d' % (hh, mm), 'Cyan', 2, 16)
        big.alpha_composite(clock, (18, 18))
        till = neon_text('$%d' % int(q * 312), 'Amber', 2, 16)
        big.alpha_composite(till, (W - till.width - 18, 18))
        if k >= N:
            lit = min(5, (k - N) // 3)
            for i in range(5):
                st = S.load('Items/star3d.png' if i < lit else 'Items/star3d_socket.png')
                st = S.up(st.crop(st.getbbox()), 2)
                big.alpha_composite(st, (W // 2 - 2 * st.width - st.width // 2 - 8 + i * (st.width + 4), 120))
        frames.append(big)
    save_gif(frames, '06_one_night', holds={len(frames) - 1: 1600})


# ── 7 · build the house: fittings land one by one, COMFORT's diamonds fill ──────────────────────────
def house_wall(H):
    w, h = W // 2, H // 2
    a = np.zeros((h, w, 4), np.uint8)
    a[:] = S.C('Night[2]')
    for x in range(0, w, 6):
        a[:, x] = S.C('Night[3]')
    wy = int(h * 0.62)
    a[wy:wy + 2] = S.C('Magenta[2]')
    a[wy + 2:int(h * 0.80)] = S.C('Magenta[0]')
    for x in range(0, w, 12):
        a[wy + 4:int(h * 0.80) - 2, x] = S.C('Magenta[1]')
    fy = int(h * 0.80)
    a[fy:] = S.C('Night[1]')
    for y in range(fy + 2, h, 4):
        a[y, ::2] = S.C('Night[0]')
    img = S.up(Image.fromarray(a, 'RGBA'), 2)
    img = S.neon_bar(img, 6)
    return img, fy * 2


def gif_build():
    H = 360
    wall, floor = house_wall(H)
    # (fitting, x, y or None for the floor, scale, diamonds lit once it is in)
    steps = [('fx_neon_martini', 40, 46, 2, 1), ('fx_picS_trio', 110, 40, 2, 2), ('fx_plant_palm', 6, None, 2, 2),
             ('fx_table_t2', 150, None, 2, 3), ('fx_plant_pothos', 420, None, 2, 4), ('fx_post5_car', 510, 112, 2, 5)]
    placed = []
    frames = []

    def draw(extra=None, lit=0, word=None):
        img = wall.copy()
        for im, x, y in placed:
            img.alpha_composite(im, (x, y))
        if extra:
            img.alpha_composite(extra[0], (extra[1], extra[2]))
        label(img, (W - 196, 16), 'COMFORT', S.C('Cyan[4]'))
        for i in range(5):
            d = S.load('Items/diamond_lit_32.png' if i < lit else 'Items/diamond_socket_32.png')
            img.alpha_composite(d, (W - 200 + i * 38, 36))
        if word:
            pl = plate(word, 'Amber[3]')
            img.alpha_composite(pl, (W - 200, 76))
        return img

    before = 0
    for n, (name, x, y, sc, lit) in enumerate(steps):
        im = S.load('Fixtures/%s.png' % name)
        im = S.up(im.crop(im.getbbox()), sc)
        ty = y if y is not None else floor + 6 - im.height
        for k in range(6):
            yy = int(-im.height + (ty + im.height) * (k + 1) / 6)
            frames.append(draw((im, x, yy), before))
        before = lit
        placed.append((im, x, ty))
        for k in range(7):
            frames.append(draw(None, lit, 'INSTALLED'))
    frames += [draw(None, 5)] * 14
    save_gif(frames, '07_build_the_house', holds={len(frames) - 1: 1600})


# ── section banners ──────────────────────────────────────────────────────────────────────────────────
# section headers: the author's steam_kit header, re-lettered (kit.header2), one icon from the game each
BANNERS = [('meet_roxy', 'MEET ROXY', 'heart3d'), ('read_the_card', 'READ THE CARD', 'card'),
           ('shake_stir_pour', 'SHAKE STIR POUR', 'mk_tab_liquor'), ('the_tap', 'THE TAP', 'ib_kegs'),
           ('the_crowd', 'THE CROWD', 'ib_arrivals'), ('one_night', 'SIX TILL TWO', 'ib_lateness'),
           ('build_the_house', 'BUILD THE HOUSE', 'up_wall_art'), ('features', 'ON THE MENU', 'ib_round')]


def save_header(key, word, icon):
    import kit
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, 'banner_' + key + '.gif')
    kit.save_header(kit.header2(word, icon, seed=11 + len(word)), p)
    print('%-26s 1440x176  6 frames  %.2f MB' % (os.path.basename(p), os.path.getsize(p) / 1e6))


def main():
    for key, word, icon in BANNERS:
        save_header(key, word, icon)
    gif_meet_roxy()
    gif_read_the_card()
    gif_shake_and_pour()
    gif_pint()
    gif_crowd()
    gif_night()
    gif_build()


if __name__ == '__main__':
    import sys
    if len(sys.argv) > 1:
        globals()[sys.argv[1]]()
    else:
        main()
