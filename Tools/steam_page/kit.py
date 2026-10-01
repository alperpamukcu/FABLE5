# -*- coding: utf-8 -*-
"""THE STEAM KIT'S FRAMES: section headers and picture panels in the author's steam_kit style.

The author's About section (Tools/steam_kit, theirs; samples in out/refs) is built on a 4 px grid:

  header (1440x176, native 360x44, 6 frames at 420 ms): an icon box on the left, a dimmed sunset strip with the
  title in the game's display face (Malibu Arcade, 16 px, cream, one-texel dark shadow), an amber rule under it,
  and the magenta neon rule along the bottom. The strip's windows twinkle between frames.

  panel (1440 wide, native 360): 17 texels of frame, a dark-amber-dark border, the picture, the border again,
  14 texels of dark, then the neon rule (#C03382 / #FF7CC7 / #C03382) and a last row.

header() re-letters the author's own header frame by frame (the old title and its rule are painted out with the
row's band ink, the icon box is cleared), so every new header is that header's sibling, twinkle included.
panel() frames any picture with the same geometry at any whole-number scale.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REFS = os.path.join(HERE, 'out', 'refs')
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ARCADE = os.path.join(ROOT, 'Assets', 'Fonts', 'MalibuArcade-Regular.ttf')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

CREAM = (242, 232, 213)
SHADOW = (16, 5, 27)
AMBER = (232, 163, 61)
BOX = (13, 8, 19)
BOX_RIM = (36, 24, 48)


def hx(s):
    s = s.lstrip('#'); return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


# ── header ───────────────────────────────────────────────────────────────────────────────────────────
def _template_frames():
    g = Image.open(os.path.join(REFS, 'header_about.gif'))
    out = []
    for i in range(g.n_frames):
        g.seek(i)
        out.append(np.asarray(g.convert('RGB').resize((360, 44), Image.NEAREST)).astype(np.int16))
    return out


def _erase_title(a):
    """Paint the old title, its shadow and its amber rule out of one native frame, row by row: each such texel
    takes the nearest clean texel's ink in its own row (the strip is horizontal bands, palms and towers)."""
    a = a.copy()
    region = (slice(8, 37), slice(44, 344))
    sub = a[region]
    cream = (sub[..., 0] > 200) & (sub[..., 1] > 190) & (sub[..., 2] > 160)
    amber = (sub[..., 0] > 200) & (sub[..., 1] > 130) & (sub[..., 1] < 190) & (sub[..., 2] < 90)
    dirty = cream | amber
    # the shadow: dark texels touching the letters from the right or below
    sh = np.zeros_like(dirty)
    sh[1:, :] |= cream[:-1, :]; sh[:, 1:] |= cream[:, :-1]; sh[1:, 1:] |= cream[:-1, :-1]
    lum = sub @ np.array([.299, .587, .114])
    dirty |= sh & (lum < 40)
    for y in range(sub.shape[0]):
        row = sub[y]
        bad = dirty[y]
        if not bad.any():
            continue
        good = np.nonzero(~bad)[0]
        if not len(good):
            continue
        for x in np.nonzero(bad)[0]:
            j = good[np.argmin(np.abs(good - x))]
            row[x] = row[j]
    a[region] = sub
    # the icon box: cleared to its own ink
    a[1:37, 1:40] = BOX
    return a


def _text_mask(text, size=16, tracking=4):
    f = ImageFont.truetype(ARCADE, size)
    w = sum(int(f.getlength(c)) for c in text) + tracking * (len(text) - 1) + 4
    m = Image.new('L', (w, size + 6), 0)
    d = ImageDraw.Draw(m)
    x = 0
    for c in text:
        d.text((x, 0), c, font=f, fill=255)
        x += int(f.getlength(c)) + tracking
    a = np.asarray(m) > 127
    ys, xs = np.nonzero(a)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def icon_card():
    """A 24x16 ID card in the game's licence colours, for READ THE CARD."""
    a = np.zeros((16, 24, 4), np.uint8)
    a[1:15, 0:24] = hx('#C9BCA8') + (255,)
    a[0:16, 1:23] = hx('#C9BCA8') + (255,)
    a[2:14, 1:23] = hx('#F2E8D5') + (255,)
    a[1:4, 1:23] = hx('#1F2E66') + (255,)
    a[5:12, 3:10] = hx('#9C8F80') + (255,)
    a[6:9, 5:8] = hx('#E8A33D') + (255,)
    for y in (6, 8, 10):
        a[y, 12:21] = hx('#6E6459') + (255,)
    return Image.fromarray(a, 'RGBA')


def load_icon(name):
    if name == 'card':
        return icon_card()
    im = Image.open(os.path.join(ITEMS, name + '.png')).convert('RGBA')
    return im.crop(im.getbbox())


def header(text, icon, tracking=4, scale=4):
    """The steam_kit header with a new title and icon: a list of (RGB image, duration ms) frames."""
    frames = []
    m = _text_mask(text, 16, tracking)
    if m.shape[1] > 286:
        m = _text_mask(text, 16, max(0, tracking - (m.shape[1] - 286) // max(1, len(text) - 1) - 1))
    ic = load_icon(icon)
    for a in _template_frames():
        a = _erase_title(a)
        x0, y0 = 52, 12
        sh = np.zeros_like(m)
        sh_a = a[y0 + 1:y0 + 1 + m.shape[0], x0 + 1:x0 + 1 + m.shape[1]]
        sh_a[m] = SHADOW
        txt = a[y0:y0 + m.shape[0], x0:x0 + m.shape[1]]
        txt[m] = CREAM
        a[y0 + m.shape[0] + 3, x0:x0 + m.shape[1]] = AMBER
        img = Image.fromarray(a.astype(np.uint8), 'RGB').convert('RGBA')
        img.alpha_composite(ic, (1 + (39 - ic.width) // 2, 1 + (36 - ic.height) // 2))
        frames.append(img.convert('RGB').resize((360 * scale, 44 * scale), Image.NEAREST))
    return frames


def save_header(frames, path, ms=420):
    pal = [f.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames]
    master = pal[0]
    pal = [f.quantize(palette=master, dither=Image.Dither.NONE) for f in frames]
    pal[0].save(path, save_all=True, append_images=pal[1:], duration=ms, loop=0, optimize=True, disposal=1)


# ── panel ────────────────────────────────────────────────────────────────────────────────────────────
FRAME_TL = hx('#241830')
FRAME_BR = hx('#0E0812')
EDGE = hx('#0F0716')
GOLD = hx('#C8822D')
RULE = [hx('#C03382'), hx('#FF7CC7'), hx('#C03382')]
LAST = hx('#251834')


def panel_native(w, h):
    """(canvas size, art offset) of a native panel around a w x h picture."""
    W = 17 + 3 + w + 3 + 17
    H = 17 + 3 + h + 3 + 14 + 3 + 1
    return (W, H), (20, 20)


def panel(art, scale=1):
    """Frame a native picture (RGB/RGBA, its own native pixels) and enlarge the whole panel by `scale`."""
    w, h = art.size
    (W, H), (ox, oy) = panel_native(w, h)
    a = np.zeros((H, W, 3), np.uint8)
    a[:] = hx('#191023')                       # the kit's gutter: one ink, a 1-texel lip round it
    a[0, :] = FRAME_TL; a[:, 0] = FRAME_TL
    a[H - 1, :] = hx('#231A2F'); a[:, W - 1] = hx('#231A2F')
    # dark / gold / dark border
    for k, c in ((0, EDGE), (1, GOLD), (2, EDGE)):
        a[17 + k:oy + h + 3 - k, 17 + k] = c
        a[17 + k:oy + h + 3 - k, W - 18 - k] = c
        a[17 + k, 17 + k:W - 17 - k] = c
        a[oy + h + 2 - k, 17 + k:W - 17 - k] = c
    ry = oy + h + 3 + 14
    for k, c in enumerate(RULE):
        a[ry + k, 6:W - 6] = c
    a[H - 1, :] = LAST
    img = Image.fromarray(a, 'RGB')
    img.paste(art.convert('RGB'), (ox, oy))
    return img.resize((W * scale, H * scale), Image.NEAREST) if scale != 1 else img


def divider(width=1440):
    """The kit's neon rule on its own (1440x32 in the kit: 4 px grid, 8 native rows)."""
    a = np.zeros((8, width // 4, 3), np.uint8)
    a[:] = hx('#1A1023')
    a[2] = hx('#C03382'); a[3] = hx('#FF7CC7'); a[4] = hx('#FF7CC7'); a[5] = hx('#C03382')
    a[:, 0] = a[:, -1] = hx('#0D0813')
    return Image.fromarray(a, 'RGB').resize((width, 32), Image.NEAREST)


# ── header, drawn from the master plate ──────────────────────────────────────────────────────────────
def _dim(a):
    """The kit header's strip is the page sunset seen through smoked glass: about 0.62 of each ink, the green
    pulled down a little more (measured on the author's header: pink -> #8F1C58, orange -> #9F5134,
    yellow -> #A16834)."""
    f = a[..., :3].astype(np.float32) * 0.62
    f[..., 1] = f[..., 1] * 0.95 - 8
    out = a.copy()
    out[..., :3] = np.clip(f, 0, 255).astype(np.uint8)
    return out


def strip(w=318, h=37, seed=11, dim=True):
    import plate as PL
    sky = PL.SKY[6:]                       # from the pink band down: the kit header shows the low sky only
    img = PL.render(w, h + 1, horizon=(h + 1) / (h + 1) - 0.0001, sun_x=0.52, sun_r=10, sky=sky,
                    skyline_spans=((0.70, 0.93),), skyline_h=26,
                    palms=(('left', 0.06, 1.0, -0.12), ('right', 1.05, 1.0, -0.05)), seed=seed, reflect=False)
    a = np.asarray(img.convert('RGB')).copy()[:h]
    return _dim(a) if dim else a


def header2(text, icon, tracking=4, scale=4, frames=6, seed=11):
    """A steam_kit header drawn new: the author's frame, box and neon rule, a strip from the master plate,
    the title in Malibu Arcade 16 with its one-texel shadow and amber rule; windows twinkle across frames."""
    bases = [f.astype(np.uint8) for f in _template_frames()]
    raw = strip(seed=seed, dim=False)
    st = _dim(raw)
    m = _text_mask(text, 16, tracking)
    if m.shape[1] > 286:
        m = _text_mask(text, 16, max(0, tracking - (m.shape[1] - 286) // max(1, len(text) - 1) - 1))
    ic = load_icon(icon)
    rng = np.random.RandomState(seed)
    win = np.zeros(raw.shape[:2], bool)                    # lit windows, found on the undimmed strip
    for c in ('#7DF0E3', '#FEB555', '#EB4BA1'):
        win |= np.all(raw == np.array(hx(c)), axis=-1)
    win[:, :2] = False
    out = []
    for k in range(frames):
        a = bases[k % len(bases)].copy()
        a[1:37, 1:40] = BOX
        s2 = st.copy()
        # twinkle: a few windows go dark each frame
        ys, xs = np.nonzero(win)
        if len(ys):
            off = rng.rand(len(ys)) < 0.35
            s2[ys[off], xs[off]] = (s2[ys[off], xs[off]].astype(int) * 0.35).astype(np.uint8)
        a[1:38, 41:359] = s2[:37, :318]
        x0, y0 = 52, 12
        a[y0 + 1:y0 + 1 + m.shape[0], x0 + 1:x0 + 1 + m.shape[1]][m] = SHADOW
        a[y0:y0 + m.shape[0], x0:x0 + m.shape[1]][m] = CREAM
        a[y0 + m.shape[0] + 3, x0:x0 + m.shape[1]] = AMBER
        img = Image.fromarray(a, 'RGB').convert('RGBA')
        if ic is not None:
            img.alpha_composite(ic, (1 + (39 - ic.width) // 2, 1 + (36 - ic.height) // 2))
        out.append(img.convert('RGB').resize((360 * scale, 44 * scale), Image.NEAREST))
    return out


def panel_px(art, unit=2):
    """The panel frame drawn at `unit` px per frame texel around a picture already at output size (its own
    sprites may sit on a finer grid than the frame, as the game's do)."""
    w, h = art.size
    fw, fh = w // unit, h // unit
    frame = panel(Image.new('RGB', (fw, fh), (0, 0, 0)), unit)
    frame.paste(art.convert('RGB'), (20 * unit, 20 * unit))
    return frame


# ── localized headers ─────────────────────────────────────────────────────────────────────────────────
FONTS_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'Fonts')
# the game's own heading faces per language (Assets/Scripts/UI/Text/LanguageFonts.cs): Malibu Arcade carries
# Latin, Cyrillic and Greek capitals; Vietnamese and Korean head in Galmuri7; Japanese and Chinese in Fusion Pixel 12
CJK = {'ja': 'FusionPixel12-ja.ttf', 'zh-CN': 'FusionPixel12-zh_hans.ttf', 'zh-TW': 'FusionPixel12-zh_hant.ttf'}


def heading_face(code, text):
    """(font path, size, top row) for a header title: the game's heading face for that language, at a whole
    multiple of its grid; the house face when it draws every letter, otherwise Galmuri11 (draws them all)."""
    from fontTools.ttLib import TTFont
    if code in CJK:
        return os.path.join(FONTS_DIR, CJK[code]), 24, 8
    if code in ('vi', 'ko'):                                   # Galmuri7 heads these in the game; at strip size the
        return os.path.join(FONTS_DIR, 'Galmuri11.ttf'), 24, 8  # 12-grid sister reads as boldly as the house face
    cm = TTFont(ARCADE).getBestCmap()
    if all(ord(c) in cm for c in text if not c.isspace()):
        return ARCADE, 16, 12
    return os.path.join(FONTS_DIR, 'Galmuri11.ttf'), 24, 8


def _text_mask_font(text, font, size, tracking):
    """The title's mask, cropped so that row 0 is the CAP LINE (the top of 'H' / the font's ascent box); marks
    above the cap (Ü, Ć, Ő...) are kept in negative rows - returned as (mask, rows above the cap line)."""
    f = ImageFont.truetype(font, size)
    w = int(sum(f.getlength(c) for c in text)) + tracking * (len(text) - 1) + 8
    pad = size
    m = Image.new('L', (w, size * 3), 0)
    d = ImageDraw.Draw(m)
    x = 0
    for c in text:
        d.text((x, pad), c, font=f, fill=255)
        x += f.getlength(c) + tracking
    a = np.asarray(m) > 127
    ref = Image.new('L', (size * 2, size * 3), 0)
    ImageDraw.Draw(ref).text((0, pad), 'H' if font.endswith(('Arcade-Regular.ttf', 'Galmuri7.ttf', 'Galmuri11.ttf'))
                             else '国', font=f, fill=255)
    cap = int(np.nonzero(np.asarray(ref).any(1))[0].min())
    ys, xs = np.nonzero(a)
    top = ys.min()
    return a[top:ys.max() + 1, xs.min():xs.max() + 1], cap - top


def header_lang(text, icon, code, scale=4, frames=6, seed=11):
    """header2 for any language: the same frame, strip, twinkle and amber rule; the title in that language's
    heading face. The cap line sits at the template's row 12 (row 8 for the 12-grid faces at 24)."""
    font, size, top = heading_face(code, text)
    room = 300                                            # native px from x 52 to the strip's right end
    tracking = 0 if font == ARCADE else 1
    m, above = _text_mask_font(text, font, size, tracking)
    while m.shape[1] > room and size > 8:                 # a long title steps down one grid size
        grid = 12 if size % 12 == 0 else 8
        size -= grid
        m, above = _text_mask_font(text, font, size, tracking)
    if m.shape[1] > room:
        raise ValueError('title too wide for the strip: %r (%s)' % (text, code))
    bases = [f.astype(np.uint8) for f in _template_frames()]
    raw = strip(seed=seed, dim=False)
    st = _dim(raw)
    win = np.zeros(raw.shape[:2], bool)
    for c in ('#7DF0E3', '#FEB555', '#EB4BA1'):
        win |= np.all(raw == np.array(hx(c)), axis=-1)
    win[:, :2] = False
    ic = None if icon == 'keep' else load_icon(icon)
    rng = np.random.RandomState(seed)
    out = []
    for k in range(frames):
        a = bases[k % len(bases)].copy()
        if icon != 'keep':                                # 'keep': the author's own coupe stays in the box
            a[1:37, 1:40] = BOX
        s2 = st.copy()
        ys_, xs_ = np.nonzero(win)
        if len(ys_):
            off = rng.rand(len(ys_)) < 0.35
            s2[ys_[off], xs_[off]] = (s2[ys_[off], xs_[off]].astype(int) * 0.35).astype(np.uint8)
        a[1:38, 41:359] = s2[:37, :318]
        x0 = 52
        y0 = max(2, top - max(0, above))                  # the cap line on `top`; marks above it rise into the strip
        h = min(m.shape[0], 35 - y0)
        mm = m[:h]
        a[y0 + 1:y0 + 1 + h, x0 + 1:x0 + 1 + mm.shape[1]][mm] = SHADOW
        a[y0:y0 + h, x0:x0 + mm.shape[1]][mm] = CREAM
        ry = min(35, y0 + h + 2)
        a[ry, x0:x0 + mm.shape[1]] = AMBER
        img = Image.fromarray(a, 'RGB').convert('RGBA')
        if ic is not None:
            img.alpha_composite(ic, (1 + (39 - ic.width) // 2, 1 + (36 - ic.height) // 2))
        out.append(img.convert('RGB').resize((360 * scale, 44 * scale), Image.NEAREST))
    return out
