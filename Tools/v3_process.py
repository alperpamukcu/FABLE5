# -*- coding: utf-8 -*-
"""Every picked take -> the four v3 plates, through one generic chain.

Per bottle: trim -> clean_liquid -> assert_contour -> sandwich -> open state at the
cap seam -> audits. No dress step: the label and its wordmark are generated now.
The print test is luma-OR-chroma distance from the glass, which keeps dark letters
on bright plates AND bright plates on dark bottles opaque.

Bottles whose seam cannot be found ship without an open front — the runtime falls
back to the closed one, which is safe (MeasureV3 falls through), and the report
names them for a manual pass.
"""
import json, math, io, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.environ.get('V3_RAW') or os.path.join(HERE, 'v3_raw')
DEST = r'c:\My project (2)\Assets\Resources\Items'
PREVIEW = r'c:\My project (2)\Art\pilot'

WALL = 2                      # at 160 grain the walls are thinner than at 280
SHOULDER = 0.80
RING = (12, 10, 16, 255)


def luma(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def solid_map(im, a=40):
    px = im.load()
    W, H = im.size
    return [[px[x, y][3] > a for y in range(H)] for x in range(W)]


def anatomy(im):
    solid = solid_map(im)
    W, H = im.size
    widths = [sum(1 for x in range(W) if solid[x][y]) for y in range(H)]
    widest = max(widths)
    shoulder = next(y for y in range(H) if widths[y] >= widest * SHOULDER)
    top = next(y for y in range(H) if widths[y] > 0)
    return solid, widths, widest, shoulder, top


def clean_liquid(im):
    W, H = im.size
    px = im.load()
    _, widths, widest, shoulder, top = anatomy(im)

    def row_avg(y):
        cs = [px[x, y][:3] for x in range(W) if px[x, y][3] > 200]
        if len(cs) < 6:
            return None
        n = len(cs)
        return (sum(c[0] for c in cs) / n, sum(c[1] for c in cs) / n,
                sum(c[2] for c in cs) / n)

    # The glass baseline comes from the NECK, not the upper body: a bottle drawn
    # FULL poisons a body baseline — the sampled "glass" rows are the liquid, so
    # nothing differs and nothing is found (Van Wrinkle, 2026-08-05). The neck is
    # above any drawn fill; a genuinely tinted-glass bottle tints its neck too,
    # which keeps the comparison honest for dark bottles.
    seam0 = cap_seam(im) or (top + 4)
    neck = [b for b in (row_avg(y) for y in range(seam0 + 2, shoulder - 1)) if b]
    if len(neck) < 4:
        neck = [b for b in (row_avg(y) for y in range(shoulder + 2, shoulder + 18)) if b]
    if not neck:
        return im, 0
    glum = sum(luma(b) for b in neck) / len(neck)
    gchr = sum(max(b) - min(b) for b in neck) / len(neck)

    def liquid_row(y):
        # ANY pooled liquid, not just blue: more saturated than the glass and darker.
        # The blue-only rule shipped six whiskies with amber liquid in them (the
        # author's review, 2026-08-05) because amber is b<r and the test read b-r.
        a = row_avg(y)
        return (a is not None and (max(a) - min(a)) > gchr + 16
                and luma(a) < glum - 20)

    rows = [y for y in range(shoulder, H - 4) if liquid_row(y)]
    if not rows:
        return im, 0
    lo, hi = min(rows), max(rows)
    src = min(H - 1, hi + 2)
    for y in range(lo, hi + 1):
        for x in range(W):
            if px[x, y][3] > 200 and px[x, src][3] > 200:
                px[x, y] = px[x, src + ((y - lo) % 3) - 1]
    return im, hi - lo + 1


def assert_contour(im):
    W, H = im.size
    px = im.load()
    solid = solid_map(im)
    edge = []
    for x in range(W):
        for y in range(H):
            if not solid[x][y]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < W and 0 <= ny < H) or not solid[nx][ny]:
                    edge.append((x, y))
                    break
    dark = sorted((px[x, y][:3] for x, y in edge if luma(px[x, y][:3]) < 95), key=luma)
    if not dark:
        return im, 0
    ink = dark[len(dark) // 2]
    fixed = 0
    for x, y in edge:
        if luma(px[x, y][:3]) > 95:
            px[x, y] = ink + (255,)
            fixed += 1
    return im, fixed


def cap_seam(im, prefer_high=False):
    """Where the cap sits on the glass. Two signals: the darkest row in the top
    reach (the seam's ink), and the first row where the silhouette NARROWS off the
    cap's width (a cap oversails its neck). The default keeps the ink row; the
    author flagged three bottles cut too LOW, and those take the higher signal."""
    W, H = im.size
    px = im.load()
    solid, widths, widest, shoulder, top = anatomy(im)
    best, dark_seam = 1e9, None
    for y in range(top + 3, min(shoulder - 2, top + int(H * 0.22))):
        cs = [px[x, y][:3] for x in range(W) if solid[x][y]]
        if len(cs) < 5:
            continue
        avg = sum(luma(c) for c in cs) / len(cs)
        if avg < best:
            best, dark_seam = avg, y
    narrow_seam = None
    capw = max(widths[top:top + 4]) if top + 4 <= H else widths[top]
    run_max = capw
    for y in range(top + 3, min(shoulder - 2, top + int(H * 0.22))):
        run_max = max(run_max, widths[y])
        if widths[y] <= run_max * 0.80:
            narrow_seam = y
            break
    # tone break: the closure is one block of colour from the top; the first row
    # whose average leaves that block is where the glass starts (a gold cap on a
    # green bottle breaks hard here even when no row is dark and no width narrows)
    tone_seam = None
    def avg_of(y):
        cs = [px[x, y][:3] for x in range(W) if solid[x][y]]
        return (tuple(sum(c[i] for c in cs) / len(cs) for i in range(3))
                if len(cs) >= 4 else None)
    caps = [avg_of(y) for y in range(top + 3, top + 8)]
    caps = [c for c in caps if c]
    if caps:
        cap_avg = tuple(sum(c[i] for c in caps) / len(caps) for i in range(3))
        for y in range(top + 8, min(shoulder - 2, top + int(H * 0.22))):
            a1, a2 = avg_of(y), avg_of(y + 1)
            if (a1 and a2
                    and sum(abs(a1[i] - cap_avg[i]) for i in range(3)) > 130
                    and sum(abs(a2[i] - cap_avg[i]) for i in range(3)) > 130):
                tone_seam = y
                break
    if prefer_high:
        cands = [s for s in (dark_seam, narrow_seam, tone_seam) if s]
        return min(cands) if cands else dark_seam
    return dark_seam


def sandwich(master, mouth):
    W, H = master.size
    px = master.load()
    solid, widths, widest, shoulder, top = anatomy(master)

    # SPAN-based cavity: each row's interior is the run between its walls, inset —
    # not "where the art is opaque". The 1810 take drew genuinely transparent glass
    # (alpha holes in the body) and the opacity rule found a 630px cavity in a
    # 58x106 bottle, so its drink had nowhere to be drawn (the author, 2026-08-05).
    # Through real holes the back plate shows anyway, which is exactly right.
    mask = [[False] * H for _ in range(W)]
    n = 0
    for y in range(mouth + 2, H - 4):
        xs = [x for x in range(W) if solid[x][y]]
        if len(xs) < 2 * WALL + 3:
            continue
        for x in range(min(xs) + WALL, max(xs) - WALL + 1):
            mask[x][y] = True
            n += 1
    if n == 0:
        return None

    tones = sorted((px[x, y][:3] for x in range(W) for y in range(H)
                    if mask[x][y] and px[x, y][3] > 200), key=luma)
    if not tones:
        return None
    glass = tones[len(tones) // 2]
    gl, gc = luma(glass), max(glass) - min(glass)

    def printed(x, y):
        c = px[x, y][:3]
        return abs(luma(c) - gl) > 26 or abs((max(c) - min(c)) - gc) > 26

    cool = tuple(int(c * 0.75 + t * 0.25) for c, t in zip(glass, (150, 200, 235)))
    inner_light = tuple(min(255, int(c * 0.92)) for c in cool)
    inner_dark = tuple(int(c * 0.62) for c in cool)

    back = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    bp = back.load()
    front = master.copy()
    fp = front.load()
    fill = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    flp = fill.load()
    film = 0
    for y in range(H):
        xs = [x for x in range(W) if mask[x][y]]
        if not xs:
            continue
        l, r = min(xs), max(xs)
        cx = (l + r) / 2.0
        w = r - l + 1
        for x in xs:
            flp[x, y] = (255, 255, 255, 255)
            t = min(1.0, abs(x - cx) / max(1.0, w / 2.0)) ** 1.5
            bp[x, y] = tuple(int(inner_light[i] + (inner_dark[i] - inner_light[i]) * t)
                             for i in range(3)) + (255,)
            if px[x, y][3] <= 200:
                continue          # a real hole in the glass: the back shows through it
            if printed(x, y):
                continue
            c = px[x, y][:3]
            fp[x, y] = c + ((166,) if luma(c) > gl + 20 else (70,))
            film += 1
    return back, front, fill, n, film, glass


def open_front(front, master, seam):
    W, H = front.size
    out = front.copy()
    op = out.load()
    px = master.load()
    solid, widths, widest, shoulder, top = anatomy(master)
    for y in range(seam):
        for x in range(W):
            op[x, y] = (0, 0, 0, 0)
    xs = [x for x in range(W) if solid[x][min(H - 1, seam + 1)]]
    if not xs:
        return None
    l, r = min(xs), max(xs)
    w = r - l + 1
    h = max(3, round(w * 0.30))
    cx, cy = (l + r) / 2.0, seam + h / 2.0
    body = sorted((px[x, y][:3] for x in range(W) for y in range(shoulder, H)
                   if solid[x][y]), key=luma)
    glass = body[len(body) // 2]
    dark = tuple(int(c * 0.35) for c in glass)
    lip = tuple(min(255, int(c * 1.30)) for c in glass)
    for y in range(seam, min(H, seam + h + 1)):
        for x in range(l, r + 1):
            nx, ny = (x - cx) / (w / 2.0), (y - cy) / (h / 2.0)
            if nx * nx + ny * ny <= 1.0:
                op[x, y] = dark + (255,)
    for x in range(l, r + 1):
        nx = (x - cx) / (w / 2.0)
        if abs(nx) > 0.985:
            continue
        yr = int(round(cy - (h / 2.0) * math.sqrt(max(0.0, 1 - nx * nx))))
        if 0 <= yr < H:
            op[x, yr] = lip + (255,)
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


# The author's per-bottle seam verdicts (2026-08-05): these three were cut too LOW
# on their open state; they take the higher of the seam signals.
PREFER_HIGH = {'vermouth_velvet', 'gin_veilcrest', 'bourbon_hollow_oak'}

# And where the signals disagree with the author's eye, the eye wins by number:
# an explicit seam row, consulted before any detector. One line per verdict.
SEAM_AT = {
    'bourbon_hollow_oak': 9,     # the gold foil ends here; the dark-row said 15
    'vermouth_velvet': 20,       # gold cap on green glass defeats all three signals
}


def run(only=None):
    picks = json.load(io.open(os.path.join(HERE, 'v3_picks.json'), encoding='utf-8'))
    report, no_open = [], []
    for bid, fname in sorted(picks.items()):
        if only and bid not in only:
            continue
        raw = Image.open(os.path.join(RAW, fname)).convert('RGBA')
        im = raw.crop(raw.getbbox())
        im, liq = clean_liquid(im)
        im, edges = assert_contour(im)
        seam = SEAM_AT.get(bid) or cap_seam(im, prefer_high=bid in PREFER_HIGH)
        mouth = seam if seam else int(im.size[1] * 0.06)
        sw = sandwich(im, mouth)
        if sw is None:
            report.append('%-22s SKIPPED: no cavity' % bid)
            continue
        back, front, fill, cav, film, glass = sw
        fopen = open_front(front, im, seam) if seam else None
        back.save(os.path.join(DEST, 'v3_%s_back.png' % bid))
        fill.save(os.path.join(DEST, 'v3_%s_fill.png' % bid))
        front.save(os.path.join(DEST, 'v3_%s_front.png' % bid))
        op = os.path.join(DEST, 'v3_%s_front_open.png' % bid)
        if fopen is not None:
            fopen.save(op)
        else:
            no_open.append(bid)
            if os.path.exists(op):
                os.remove(op)
        comps = components(front)
        report.append('%-22s %3dx%-3d cav %5d film %5d seam %s comps %d%s'
                      % (bid, im.size[0], im.size[1], cav, film,
                         seam if seam else '-', comps,
                         ' liq!%d' % liq if liq else ''))
    print('\n'.join(report))
    print('no open state:', no_open if no_open else 'none')

    # one contact sheet of every shipped master, for the author
    ims = []
    for bid in sorted(picks):
        p = os.path.join(RAW, picks[bid])
        im = Image.open(p).convert('RGBA')
        ims.append(im.crop(im.getbbox()))
    pad = 8
    W = sum(i.width for i in ims) + pad * (len(ims) + 1)
    H = max(i.height for i in ims) + pad * 2
    sheet = Image.new('RGBA', (W, H), (26, 16, 35, 255))
    x = pad
    for i in ims:
        sheet.paste(i, (x, pad + (H - 2 * pad - i.height) // 2), i)
        x += i.width + pad
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(
        os.path.join(PREVIEW, 'set_v3.png'))
    print('sheet -> Art/pilot/set_v3.png')


if __name__ == '__main__':
    run(set(sys.argv[1:]) if len(sys.argv) > 1 else None)
