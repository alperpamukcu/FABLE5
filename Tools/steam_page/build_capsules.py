# -*- coding: utf-8 -*-
"""Every store and library picture Steam asks for, at its exact size, from the same room, cast and sign.

    python3 Tools/steam_page/build_capsules.py      ->  Tools/steam_page/out/capsules/*.png (+ .jpg where Steam wants one)

Sizes (Steamworks "Store Graphical Assets" / "Library Assets", 2024 spec): header 920x430, small 462x174,
main 1232x706, vertical 748x896, page background 1438x810, library capsule 600x900, library hero 3840x1240
(no logo - Steam lays the library logo over it), library logo 1280x720 (transparent), library header 920x430,
event cover 800x450, event header 1920x622, community icon 184x184, client icon 256x256.
"""
import os
import numpy as np
from PIL import Image
import scene as S
import compose as K

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'capsules')
CARD = ('clubgirl', 'Sabrina Voss', 24, 'us', 'United States')
CAST = ['clubgirl', 'guard', 'harajuku', 'leopard', 'gallerist', 'kdance', 'bristol', 'driftgirl']


def save(img, name, jpg=False):
    os.makedirs(OUT, exist_ok=True)
    img = img.convert('RGB') if jpg or img.mode != 'RGBA' else img
    p = os.path.join(OUT, name + ('.jpg' if jpg else '.png'))
    if jpg:
        img.save(p, quality=95, subsampling=0)
    else:
        img.save(p, optimize=True)
    print('%-28s %dx%d' % (os.path.basename(p), img.width, img.height))
    return img


def props(img, cy, s, x0, step):
    kinds = [('glass', 'highball', 'Cyan[3]'), ('bottle', 'tequila_sol_viejo', 0.6), ('glass', 'rocks', 'Amber[2]'),
             ('bottle', 'gin_boothby', 0.6), ('glass', 'coupe', 'Amber[3]', 'salt'), ('bottle', 'rum_cane_coral', 0.5)]
    xs = [x0 + i * step for i in range(len(kinds))]
    return K.place_props(img, cy, s, xs, kinds)


def key_scene(W, H, roxy_s, roxy_x, roxy_y, logo_w=None, logo_xy=(0, 0), patrons=None, patron_x=0.0,
              counter_y=0.76, sun_x=0.73, sun_r=0.27, horizon=0.56, card_xy=None, props_x=None, props_step=70,
              s=2, glass=True, dim=0.0, vig=0.45):
    img, cy = K.room(W, H, s=s, horizon=horizon, sun_x=sun_x, sun_r=sun_r, patrons=patrons, patron_x=patron_x,
                     counter_y=counter_y)
    if props_x is not None:
        props(img, cy, s, props_x, props_step)
    if dim:
        img = S.tint(img, S.C('Night[0]'), dim)
    img = S.vignette(img, vig)
    if card_xy:
        K.paste_glow(img, S.id_card(*CARD), card_xy[0], cy + card_xy[1], 'Amber[3]', 2)
    if roxy_s:
        r = K.roxy(roxy_s, rim='Magenta[4]', rim_side='right', glass=glass)
        K.paste_glow(img, r, int(W * roxy_x) - r.width // 2, roxy_y, 'Magenta[3]', max(3, roxy_s + 2))
    if logo_w:
        lg = K.logo_fit(logo_w)
        x, y = logo_xy
        img.alpha_composite(lg, (int(x) if x >= 0 else (W - lg.width) // 2, y))
    return img


def main():
    # ── store ────────────────────────────────────────────────────────────────────────────────────────
    save(key_scene(1232, 706, 4, 0.80, -10, 640, (24, 22), CAST[:3], 0.0, card_xy=(24, -6), props_x=160), 'main_capsule_1232x706')
    header = key_scene(920, 430, 3, 0.79, -14, 560, (18, 40), None, counter_y=0.80, sun_x=0.78, sun_r=0.30,
                       props_x=40, props_step=78)
    save(header, 'header_capsule_920x430', jpg=True)
    save(key_scene(462, 174, 2, 0.86, -18, 330, (8, 22), None, counter_y=0.86, sun_x=0.86, sun_r=0.40, horizon=0.66,
                   glass=False, vig=0.3), 'small_capsule_462x174', jpg=True)
    save(key_scene(748, 896, 4, 0.56, 300, 700, (-1, 18), CAST[2:5], 0.0, counter_y=0.80, sun_x=0.55, sun_r=0.30,
                   horizon=0.62, props_x=40, props_step=90), 'vertical_capsule_748x896', jpg=True)
    bg = key_scene(1438, 810, 0, 0, 0, None, patrons=CAST[3:7], patron_x=0.08, counter_y=0.78, sun_x=0.5, sun_r=0.3,
                   props_x=200, props_step=170, dim=0.55, vig=0.6)
    save(bg, 'page_background_1438x810', jpg=True)
    # ── library ──────────────────────────────────────────────────────────────────────────────────────
    save(key_scene(600, 900, 4, 0.50, 262, 580, (-1, 20), CAST[2:4], 0.0, counter_y=0.80, sun_x=0.5, sun_r=0.34,
                   horizon=0.6, props_x=30, props_step=70), 'library_capsule_600x900', jpg=True)
    hero = key_scene(3840, 1240, 7, 0.76, -40, None, patrons=CAST[:6], patron_x=0.02, counter_y=0.8, sun_x=0.76,
                     sun_r=0.30, s=4, props_x=None, vig=0.35)
    save(hero, 'library_hero_3840x1240', jpg=True)
    logo = Image.new('RGBA', (1280, 720), (0, 0, 0, 0))
    lg = K.logo_fit(1280)
    logo.alpha_composite(lg, (0, (720 - lg.height) // 2))
    save(logo, 'library_logo_1280x720')
    save(header, 'library_header_920x430', jpg=True)
    # ── events / community ───────────────────────────────────────────────────────────────────────────
    save(key_scene(800, 450, 3, 0.78, -6, 470, (16, 26), None, 0.0, counter_y=0.80, sun_x=0.78, sun_r=0.30,
                   props_x=30, props_step=72), 'event_cover_800x450', jpg=True)
    save(key_scene(1920, 622, 4, 0.82, -30, 860, (60, 40), None, 0.0, counter_y=0.80, sun_x=0.80, sun_r=0.32,
                   props_x=80, props_step=110), 'event_header_1920x622', jpg=True)
    face = S.load('Patron/hostess/face.png')
    for size, s, name in ((184, 3, 'community_icon_184x184'), (256, 4, 'client_icon_256x256')):
        ic = S.banded(size // s + 1, size // s + 1, ['Magenta[1]', 'Magenta[2]', 'Amber[2]', 'Amber[3]'])
        ic.alpha_composite(S.rim_light(face, S.C('Magenta[4]'), 'right'), ((size // s + 1 - 64) // 2, 2))
        ic = S.up(ic, s).crop((0, 0, size, size))
        save(ic, name, jpg=name.startswith('community'))


if __name__ == '__main__':
    main()
