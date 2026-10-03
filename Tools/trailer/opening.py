# -*- coding: utf-8 -*-
"""THE OPENING (2026-10-03, the author: "başlangıç görseli güzel değil ... oyundaki şehir görüntüsü olmalı, aynı
zamanda gün batımı güneş"). The game's own skyline (Scene/curtain_city, the city the curtain shows between nights)
in front of a synthwave sunset: a striped sun going down behind the towers, the sea taking its light, the windows
coming on. Drawn at the game's native 480x270 and blown up 4x, so every pixel is a game pixel.

    python3 Tools/trailer/opening.py <films dir>     -> <films dir>/I00_city.mp4 (+ .json, no marks)
"""
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
W, H, S = 480, 270, 4
FPS, SECS = 60, 3.0
HORIZON = 196
SUN_X, SUN_R = 150, 62

SKY = [(26, 14, 44), (44, 20, 70), (74, 26, 98), (118, 34, 116), (168, 44, 120), (214, 70, 112), (240, 112, 92),
       (250, 156, 84)]
SUN_TOP, SUN_BOT = np.array([255, 226, 120]), np.array([246, 84, 128])
SEA = (20, 14, 46)


def sky(t):
    img = np.zeros((H, W, 3), np.uint8)
    bands = len(SKY)
    for y in range(HORIZON):
        u = y / HORIZON
        k = u * (bands - 1)
        i = int(k)
        f = k - i
        # dithered band edges, the game's way of doing a gradient
        for x in range(W):
            pick = i + (1 if f > 0.5 + 0.25 * (((x + y) & 1) - 0.5) and i + 1 < bands else 0)
            img[y, x] = SKY[pick]
    return img


def stars(img, t, rng):
    pts = rng.integers(0, [W, 90], size=(60, 2))
    tw = rng.random(60)
    for (x, y), p in zip(pts, tw):
        on = 0.5 + 0.5 * np.sin(t * 5 + p * 20)
        if on > 0.35:
            c = int(150 + 105 * on)
            img[y, x] = (c, c, min(255, c + 20))


def sun(img, t):
    cx, r = SUN_X, SUN_R
    cy = HORIZON - 92 + int(round(t * 7))          # going down, behind the low towers
    for y in range(max(0, cy - r), min(HORIZON, cy + r)):
        for x in range(cx - r, cx + r):
            if (x - cx) ** 2 + (y - cy) ** 2 > r * r:
                continue
            u = (y - (cy - r)) / (2 * r)
            # the synthwave cuts: thicker gaps toward the bottom, drifting down
            if u > 0.45:
                band = (y + int(t * 8)) % 9
                if band < 1 + int((u - 0.45) * 8):
                    continue
            img[y, x] = (SUN_TOP * (1 - u) + SUN_BOT * u).astype(np.uint8)
    return cy


def sea(img, t, cy, rng):
    img[HORIZON:] = SEA
    for y in range(HORIZON, H):
        d = (y - HORIZON) / (H - HORIZON)
        # the sun's road on the water: broken streaks that shimmer
        half = int(40 + 70 * d)
        for x in range(max(0, SUN_X - half), min(W, SUN_X + half), 1):
            n = np.sin(x * 0.35 + y * 1.7 + t * 9) + np.sin(x * 0.11 - t * 5)
            if n > 1.1 - d * 0.6 and (y % 2 == 0):
                k = 1 - abs(x - SUN_X) / half
                c = (SUN_BOT * (1 - d) + np.array([120, 40, 120]) * d) * (0.5 + 0.5 * k)
                img[y, x] = c.astype(np.uint8)
        if y % 3 == 0:
            img[y, ::7] = np.minimum(255, img[y, ::7].astype(int) + 10)


def city_layer():
    c = Image.open(os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'curtain_city.png')).convert('RGBA')
    c = c.resize((480, 144), Image.NEAREST)
    return np.asarray(c).copy()


def frame(t, base_sky, city, rng_seed=7):
    rng = np.random.default_rng(rng_seed)
    img = base_sky.copy()
    stars(img, t, rng)
    cy = sun(img, t)
    sea(img, t, cy, rng)
    # the skyline on the horizon, its windows coming on one by one
    top = HORIZON + 6 - city.shape[0]
    a = city[..., 3:4] / 255.0
    rgb = city[..., :3].astype(np.float32)
    lit = (rgb[..., 0] > 150) & (rgb[..., 1] > 100)                 # the warm window pixels
    wrng = np.random.default_rng(3)
    on_at = wrng.random(lit.shape) * 2.2
    dark = rgb.copy()
    dark[lit] = (40, 26, 56)
    shown = np.where((lit & (on_at < t))[..., None], rgb, dark)
    region = img[top:top + city.shape[0]].astype(np.float32)
    img[top:top + city.shape[0]] = (region * (1 - a) + shown * a).astype(np.uint8)
    return img


def main(films):
    os.makedirs(films, exist_ok=True)
    out = os.path.join(films, 'I00_city.mp4')
    base = sky(0)
    city = city_layer()
    enc = subprocess.Popen(['ffmpeg', '-y', '-v', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W * S, H * S),
                            '-r', str(FPS), '-i', '-', '-c:v', 'libx264', '-crf', '10', '-preset', 'medium',
                            '-pix_fmt', 'yuv420p', out], stdin=subprocess.PIPE)
    for i in range(int(SECS * FPS)):
        img = frame(i / FPS, base, city)
        enc.stdin.write(Image.fromarray(img).resize((W * S, H * S), Image.NEAREST).tobytes())
    enc.stdin.close()
    enc.wait()
    json.dump({'film': 'I00_city.mp4', 'fps': FPS, 'seconds': SECS, 'marks': []},
              open(os.path.join(films, 'I00_city.json'), 'w'))
    Image.fromarray(frame(1.5, base, city)).resize((W * S, H * S), Image.NEAREST).save(os.path.join(HERE, 'out', 'opening_preview.png'))
    print('->', out)


if __name__ == '__main__':
    main(sys.argv[1])
