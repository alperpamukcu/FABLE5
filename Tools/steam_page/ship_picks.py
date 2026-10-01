# -*- coding: utf-8 -*-
"""Copy the Nano Banana takes picked by eye (2026-10-01) to Steam's file names in out/capsules.

    python3 Tools/steam_page/ship_picks.py

The library logo stays the exact title sign (build_capsules.py); the library header is the header capsule; the
client icon is the community icon's take, reduced to 256 from the model's own picture.
"""
import os
from PIL import Image
import compose as K

HERE = os.path.dirname(os.path.abspath(__file__))
NANO = os.path.join(HERE, 'out', 'nano')
OUT = os.path.join(HERE, 'out', 'capsules')

PICKS = {   # take -> Steam file(s)
    # _pro = Nano Banana Pro (gemini-3-pro-image-preview, 2K; the hero 4K); the rest the flash model
    'main_capsule_pro_1': ['main_capsule_1232x706.png'],
    'header_capsule_pro_2': ['header_capsule_920x430.jpg', 'library_header_920x430.jpg'],
    'small_capsule_pro_2': ['small_capsule_462x174.jpg'],
    'vertical_capsule_1': ['vertical_capsule_748x896.jpg'],      # both Pro takes put her head under the title
    'library_capsule_1': ['library_capsule_600x900.jpg'],        # same
    'library_hero_pro_1': ['library_hero_3840x1240.jpg'],
    'page_background_pro_2': ['page_background_1438x810.jpg'],
    'event_header_pro_2': ['event_header_1920x622.jpg'],
    'event_cover_2': ['event_cover_800x450.jpg'],
    'community_icon_1': ['community_icon_184x184.jpg'],
}


def save(img, name):
    p = os.path.join(OUT, name)
    if name.endswith('.jpg'):
        img.convert('RGB').save(p, quality=93, subsampling=0, optimize=True)
    else:
        img.save(p, optimize=True)
    print('%-30s %dx%d  %.0f KB' % (name, img.width, img.height, os.path.getsize(p) / 1024))


def main():
    os.makedirs(OUT, exist_ok=True)
    for take, names in PICKS.items():
        img = Image.open(os.path.join(NANO, take + '.png'))
        for n in names:
            save(img, n)
    raw = Image.open(os.path.join(NANO, 'raw', 'community_icon_1.png')).convert('RGB')
    save(raw.resize((256, 256), Image.LANCZOS), 'client_icon_256x256.png')
    logo = Image.new('RGBA', (1280, 720), (0, 0, 0, 0))
    lg = K.logo_fit(1280)
    logo.alpha_composite(lg, (0, (720 - lg.height) // 2))
    save(logo, 'library_logo_1280x720.png')


if __name__ == '__main__':
    main()
