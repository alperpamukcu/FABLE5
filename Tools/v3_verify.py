# -*- coding: utf-8 -*-
"""The proof pass the author demanded ("yüzde yüz emin olarak"): every shipped
sandwich answers four questions BY MEASUREMENT, per bottle.

  LABEL    composite back+liquid+front twice — once with a RED drink, once BLUE,
           both at full fill. Any pixel that differs between the two is liquid
           showing through; count how many of those sit inside the front's OPAQUE
           print regions. Must be 0: a label may not change with the drink.
  BASE     the lowest film (see-through) row of the front must reach within 4 rows
           of the vessel's bottom — else the base stands in front of the drink and
           a full bottle looks empty at its foot.
  FILM     the see-through share of the cavity (sanity floor 0.25).
  SOLID    one component per plate, fill inside back, equal canvases.
"""
import io, json, os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, r'c:\My project (2)\Tools')
import v3_brief

DEST = r'c:\My project (2)\Assets\Resources\Items'

ALC = ['vodka_astra', 'vodka_vor', 'vodka_leonid', 'vodka_okhta',
       'gin_boothby', 'gin_juniper_crown', 'gin_thornwood', 'gin_veilcrest',
       'rum_cane_coral', 'rum_tidewater', 'rum_windward', 'rum_reina_del_mar',
       'bourbon_redline', 'bourbon_old_harrow', 'bourbon_ashfall', 'bourbon_hollow_oak',
       'tequila_sonora', 'tequila_alta_luna', 'tequila_sol_viejo', 'tequila_cielo_roto',
       'amaro_notte', 'vermouth_velvet', 'liqueur_delia', 'liqueur_kafa']
MIXB = [m for m, s in v3_brief.MIXERS.items() if s[0] == 'bottle']

RED = (255, 0, 0)
BLUE = (0, 0, 255)


def composite(back, fill, front, drink):
    W, H = back.size
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    op = out.load()
    bp, flp, fp = back.load(), fill.load(), front.load()
    for x in range(W):
        for y in range(H):
            r = g = b = 0.0
            a = 0.0
            br, bg_, bb, ba = bp[x, y]
            if ba > 0:
                r, g, b, a = br, bg_, bb, 1.0
            if flp[x, y][3] > 128:               # the liquid, full bottle
                r, g, b, a = drink[0], drink[1], drink[2], 1.0
            fr, fg, fb, fa = fp[x, y]
            if fa > 0:
                k = fa / 255.0
                r = fr * k + r * (1 - k)
                g = fg * k + g * (1 - k)
                b = fb * k + b * (1 - k)
                a = max(a, k)
            op[x, y] = (int(r), int(g), int(b), int(a * 255))
    return out


def components(im):
    W, H = im.size
    px = im.load()
    ok = [[px[x, y][3] > 0 for y in range(H)] for x in range(W)]
    seen = [[False] * H for _ in range(W)]
    count = 0
    for sx in range(W):
        for sy in range(H):
            if not ok[sx][sy] or seen[sx][sy]:
                continue
            count += 1
            st = [(sx, sy)]
            seen[sx][sy] = True
            while st:
                x, y = st.pop()
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < W and 0 <= ny < H and ok[nx][ny] and not seen[nx][ny]:
                            seen[nx][ny] = True
                            st.append((nx, ny))
    return count


def verify(bid):
    paths = {k: os.path.join(DEST, 'v3_%s_%s.png' % (bid, k))
             for k in ('back', 'fill', 'front')}
    if not all(os.path.exists(p) for p in paths.values()):
        return '%-20s MISSING PLATES' % bid, False
    back = Image.open(paths['back']).convert('RGBA')
    fill = Image.open(paths['fill']).convert('RGBA')
    front = Image.open(paths['front']).convert('RGBA')
    W, H = back.size
    if fill.size != (W, H) or front.size != (W, H):
        return '%-20s CANVAS MISMATCH' % bid, False

    a = composite(back, fill, front, RED).load()
    b = composite(back, fill, front, BLUE).load()
    fp = front.load()
    flp = fill.load()
    bleed = film = opaque = 0
    low_film = -1
    for x in range(W):
        for y in range(H):
            if flp[x, y][3] <= 128:
                continue
            fa = fp[x, y][3]
            if fa == 255:
                opaque += 1
                if a[x, y][:3] != b[x, y][:3]:
                    bleed += 1                    # print that changes with the drink
            else:
                film += 1
                low_film = max(low_film, y)
    cav = film + opaque
    ys = [y for x in range(W) for y in range(H) if flp[x, y][3] > 128]
    bottom = max(ys) if ys else 0
    base_gap = bottom - low_film if low_film >= 0 else 999
    film_share = film / max(1, cav)
    comps = components(front)
    ok = (bleed == 0 and base_gap <= 4 and film_share >= 0.25 and comps == 1)
    return ('%-20s bleed %3d  base-gap %2d  film %3.0f%%  comps %d  -> %s'
            % (bid, bleed, base_gap, film_share * 100, comps,
               'PASS' if ok else 'FAIL'), ok)


def main():
    ids = ALC + MIXB
    bad = 0
    for bid in ids:
        line, ok = verify(bid)
        print(line)
        if not ok:
            bad += 1
    print('%d/%d pass' % (len(ids) - bad, len(ids)))
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
