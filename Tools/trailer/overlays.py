# -*- coding: utf-8 -*-
"""ANIMATED OVERLAYS for the trailer (2026-10-02, the author: "Roxy fragmanda bir anlatıcı gibi ara ara girip konuşacak
... ekrana yazan yazılar animasyonlu olsun"). Each overlay is a PNG sequence with alpha at the film's frame rate,
drawn only from the game's own art and faces, every pixel on a whole-number scale:

  narrator(text, seconds)  Roxy's plate, the way the game draws her: the plum box with the pink tube border and her
                           name tab, her bust in the well talking (her own talk frames, mouth moving while the words
                           type), the line typed out letter by letter in the game's dialogue face (Jersey 15), and the
                           box sliding up in and down out. Returns the times a voice blip should sound.
  title(text, seconds)     a caption in the display face (Malibu Arcade): the letters drop in one after another, the
                           amber rule wipes under them, the whole line lifts away at the end.

    seq_dir, (x, y), blips = narrator('You read the card. Every time.', 3.2, out_dir, W, H)
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ARCADE = os.path.join(ROOT, 'Assets', 'Fonts', 'MalibuArcade-Regular.ttf')
JERSEY = os.path.join(ROOT, 'Assets', 'Resources', 'Fonts', 'Jersey15-Regular.ttf')
SILK = os.path.join(ROOT, 'Assets', 'Fonts', 'Silkscreen-Regular.ttf')
ROXY = os.path.join(ROOT, 'Assets', 'Resources', 'Patron', 'hostess')
FPS = 60

PLUM, PLUM_DEEP, PINK, PINK_HOT, PINK_DEEP = (26, 16, 35), (13, 8, 19), (232, 77, 166), (255, 125, 198), (143, 36, 100)
CREAM, AMBER, SHADOW = (242, 232, 213), (232, 163, 61), (13, 8, 19)


def ease_out(u):
    u = min(1.0, max(0.0, u))
    return 1 - (1 - u) ** 3


def hard(img):
    """No anti-aliasing: every pixel either the face's or nothing."""
    a = np.asarray(img).copy()
    a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    return Image.fromarray(a, 'RGBA')


def glyphs(text, font, ink, shadow=True):
    """One image per character at the face's design size, with its advance."""
    out = []
    for ch in text:
        box = font.getbbox(ch) if ch.strip() else (0, 0, 0, 0)
        adv = font.getlength(ch)
        asc, desc = font.getmetrics()
        im = Image.new('RGBA', (int(math.ceil(adv)) + 2, asc + desc + 2), (0, 0, 0, 0))
        if ch.strip():
            d = ImageDraw.Draw(im)
            if shadow:
                d.text((1, 1), ch, font=font, fill=SHADOW + (255,))
            d.text((0, 0), ch, font=font, fill=ink + (255,))
            im = hard(im)
        out.append((im, adv))
    return out


# ── Roxy's bust, talking ─────────────────────────────────────────────────────────────────────────────

def roxy_busts(scale):
    """Her talk and idle frames cropped to head and shoulders, scaled whole."""
    frames = []
    for folder in ('talk', 'talk_1', 'talk_2'):
        d = os.path.join(ROXY, folder)
        if not os.path.isdir(d):
            continue
        for f in sorted(x for x in os.listdir(d) if x.endswith('.png')):
            frames.append(Image.open(os.path.join(d, f)).convert('RGBA'))
    idle = Image.open(os.path.join(ROXY, 'idle', sorted(x for x in os.listdir(os.path.join(ROXY, 'idle'))
                                                       if x.endswith('.png'))[0])).convert('RGBA')
    a = np.asarray(idle)[..., 3]
    ys, xs = np.nonzero(a > 0)
    cx = (xs.min() + xs.max()) // 2
    top = ys.min()
    box = (cx - 36, top - 2, cx + 36, top + 70)          # 72x72: hair, face, shoulders
    crop = lambda im: im.crop(box).resize((72 * scale, 72 * scale), Image.NEAREST)
    return [crop(f) for f in frames] or [crop(idle)], crop(idle)


# ── the narrator's plate ─────────────────────────────────────────────────────────────────────────────

def narrator(text, seconds, out_dir, W, H, cps=34.0, top=False):
    """Roxy's plate as a PNG sequence. Returns (dir, (x, y) to overlay at, blip times in seconds)."""
    os.makedirs(out_dir, exist_ok=True)
    for f in os.listdir(out_dir):
        os.remove(os.path.join(out_dir, f))
    s = 3 if H >= 1080 else 2                                # one design pixel, on screen
    tall = H > W
    bw = (min(W - 80, 1520) if not tall else W - 60) // s
    bh = 78
    font = ImageFont.truetype(JERSEY, 27)
    name_font = ImageFont.truetype(SILK, 8)
    talk, idle = roxy_busts(1)

    # the static plate at design size
    plate = Image.new('RGBA', (bw, bh), (0, 0, 0, 0))
    d = ImageDraw.Draw(plate)
    d.rectangle([0, 6, bw - 1, bh - 1], fill=PLUM + (238,))
    d.rectangle([0, 6, bw - 1, bh - 1], outline=PINK_DEEP + (255,))
    d.rectangle([1, 7, bw - 2, bh - 2], outline=PINK + (255,))
    d.rectangle([2, 8, bw - 3, bh - 3], outline=PINK_DEEP + (255,))
    # the well her bust sits in
    d.rectangle([6, 12, 6 + 62, bh - 7], fill=PLUM_DEEP + (255,), outline=PINK_DEEP + (255,))
    # the name tab
    tab_text = 'ROXY VALE'
    tw = int(name_font.getlength(tab_text))
    d.rectangle([8, 0, 8 + tw + 8, 11], fill=PINK + (255,))
    d.text((12, 2), tab_text, font=name_font, fill=PLUM_DEEP + (255,))
    plate = hard(plate)

    # the line, wrapped to the box
    gl = glyphs(text, font, CREAM)
    text_x0, text_w = 78, bw - 78 - 10
    lines, line, width = [], [], 0.0
    for word in text.split(' '):
        ww = font.getlength(word + ' ')
        if line and width + ww > text_w:
            lines.append(line)
            line, width = [], 0.0
        line.append(word)
        width += ww
    if line:
        lines.append(line)
    # characters in order with their positions
    placed, y = [], 16
    asc, desc = font.getmetrics()
    line_h = asc + desc - 6
    for ln in lines:
        x = text_x0
        for k, word in enumerate(ln):
            for ch in word + (' ' if k < len(ln) - 1 else ''):
                im, adv = glyphs(ch, font, CREAM)[0]
                placed.append((ch, im, x, y))
                x += adv
        y += line_h
    n_chars = len(placed)
    type_t = n_chars / cps

    slide = 0.18
    frames = int(round(seconds * FPS))
    blips = []
    last_word = -1
    big_w, big_h = bw * s, (bh + 20) * s
    for fi in range(frames):
        t = fi / FPS
        u_in = ease_out(t / slide)
        u_out = ease_out((t - (seconds - slide)) / slide) if t > seconds - slide else 0.0
        off = int(round((1 - u_in) * 20 + u_out * 20))          # design pixels below its rest
        alpha = (u_in if t < slide else 1.0) * (1 - u_out)
        canvas = Image.new('RGBA', (bw, bh + 20), (0, 0, 0, 0))
        canvas.alpha_composite(plate, (0, off))
        typed = int(max(0.0, t - 0.12) * cps)
        talking = 0.12 < t < 0.12 + type_t + 0.1
        bust = talk[int(t * 10) % len(talk)] if talking else idle
        canvas.alpha_composite(bust.crop((5, 5, 67, 67)), (6, 12 + off))
        for k, (ch, im, x, yy) in enumerate(placed[:typed]):
            canvas.alpha_composite(im, (int(x), yy + off))
        # a blip on every word that starts typing
        words_typed = sum(1 for k in range(min(typed, n_chars)) if placed[k][0] == ' ')
        if talking and typed > 0 and words_typed != last_word:
            blips.append(t)
            last_word = words_typed
        if typed < n_chars and talking and (fi // 15) % 2 == 0 and typed > 0:
            ch, im, x, yy = placed[min(typed, n_chars - 1)]
            ImageDraw.Draw(canvas).rectangle([int(x), yy + 4 + off, int(x) + 1, yy + 20 + off], fill=PINK_HOT + (255,))
        big = canvas.resize((big_w, big_h), Image.NEAREST)
        if alpha < 1.0:
            a = np.asarray(big).copy()
            a[..., 3] = (a[..., 3] * alpha).astype(np.uint8)
            big = Image.fromarray(a, 'RGBA')
        big.save(os.path.join(out_dir, '%04d.png' % fi))
    x = (W - big_w) // 2
    y = (40 if not tall else 220) if top else H - big_h - (40 if not tall else 260)
    return out_dir, (x, y), blips


# ── titles ───────────────────────────────────────────────────────────────────────────────────────────

def title(text, seconds, out_dir, W, H, lang_face=None, y_frac=0.16, max_scale=5, band=False):
    """A kinetic caption: letters drop in one by one, the amber rule wipes under, the line lifts away.
    Returns (dir, (x, y))."""
    os.makedirs(out_dir, exist_ok=True)
    for f in os.listdir(out_dir):
        os.remove(os.path.join(out_dir, f))
    font_path, size = lang_face or (ARCADE, 16)
    font = ImageFont.truetype(font_path, size)
    lines = text.split('\n')
    gls = [glyphs(l, font, CREAM) for l in lines]
    widths = [sum(adv for _, adv in g) for g in gls]
    asc, desc = font.getmetrics()
    lh = asc + desc + 6
    dw, dh = int(max(widths)) + 8, lh * len(lines) + 8
    s = max(1, min(max_scale, int((W * 0.82) // dw)))
    frames = int(round(seconds * FPS))
    stagger, drop_t = 0.028, 0.16
    n = sum(len(g) for g in gls)
    for fi in range(frames):
        t = fi / FPS
        out_u = ease_out((t - (seconds - 0.2)) / 0.2) if t > seconds - 0.2 else 0.0
        canvas = Image.new('RGBA', (dw, dh + 12), (0, 0, 0, 0))
        if band:                                             # a plum band, so the words read over any room
            ba = int(200 * ease_out(t / 0.15) * (1 - out_u))
            ImageDraw.Draw(canvas).rectangle([0, 2, dw - 1, dh + 9], fill=PLUM + (ba,))
        k = 0
        for li, g in enumerate(gls):
            x = (dw - widths[li]) / 2
            for im, adv in g:
                u = ease_out((t - k * stagger) / drop_t)
                if u > 0:
                    dy = int(round((1 - u) * -8 - out_u * 10))
                    if u < 1:
                        a = np.asarray(im).copy()
                        a[..., 3] = np.where(a[..., 3] > 0, int(255 * u), 0).astype(np.uint8)
                        im = Image.fromarray(a, 'RGBA')
                    canvas.alpha_composite(im, (int(x), 4 + li * lh + dy + 6))
                x += adv
                k += 1
        # the rule wipes under the last line once the letters are in
        ru = ease_out((t - n * stagger) / 0.25)
        if ru > 0:
            ry = 4 + len(lines) * lh + 2
            x0 = (dw - widths[-1]) / 2
            d = ImageDraw.Draw(canvas)
            d.line([(int(x0), ry), (int(x0 + widths[-1] * ru), ry)], fill=AMBER + (255,))
            d.line([(int(x0) + 1, ry + 1), (int(x0 + widths[-1] * ru) + 1, ry + 1)], fill=SHADOW + (255,))
        big = canvas.resize((canvas.width * s, canvas.height * s), Image.NEAREST)
        if out_u > 0:
            a = np.asarray(big).copy()
            a[..., 3] = (a[..., 3] * (1 - out_u)).astype(np.uint8)
            big = Image.fromarray(a, 'RGBA')
        big.save(os.path.join(out_dir, '%04d.png' % fi))
    return out_dir, ((W - dw * s) // 2, int(H * y_frac))


# ── the sign, switching on ───────────────────────────────────────────────────────────────────────────

def sign_on(sign, seconds, out_dir, flicker=((0.00, 0), (0.06, 1), (0.10, 0), (0.16, 1), (0.20, 0.35), (0.26, 1))):
    """The neon sign (an RGBA image, drawn at its final size) lighting the way a tube does: a stutter of on and off,
    then held. Returns the directory."""
    os.makedirs(out_dir, exist_ok=True)
    for f in os.listdir(out_dir):
        os.remove(os.path.join(out_dir, f))
    base = np.asarray(sign).copy()
    frames = int(round(seconds * FPS))
    for fi in range(frames):
        t = fi / FPS
        level = 1.0
        for at, lv in flicker:
            if t >= at:
                level = lv
        a = base.copy()
        a[..., 3] = (a[..., 3] * level).astype(np.uint8)
        Image.fromarray(a, 'RGBA').save(os.path.join(out_dir, '%04d.png' % fi))
    return out_dir
