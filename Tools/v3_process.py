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
    # A POOL lives low. A flagged band that starts in the upper half of the body is
    # not a pool — it is tinted glass outvoted by a clear neck, and "repairing" it
    # would tile base-glass over the body, dress included (the round-three tell:
    # five liq-0-gated bottles suddenly "repaired" 55-87 rows).
    body_h = (H - 4) - shoulder
    if body_h > 0 and (lo - shoulder) / body_h < 0.40:
        print('  liquid-like band %d-%d starts high: tinted glass, left alone' % (lo, hi))
        return im, 0
    # and the dress is never repainted: print pixels (far from the glass tone in
    # luma or chroma against their own row) stay.
    src = min(H - 1, hi + 2)
    glass_rows = [row_avg(y) for y in range(shoulder + 1, lo - 1)]
    glass_rows = [g for g in glass_rows if g]
    fixed = 0
    for y in range(lo, hi + 1):
        a = row_avg(y)
        for x in range(W):
            if px[x, y][3] <= 200 or px[x, src][3] <= 200:
                continue
            c = px[x, y][:3]
            if a and (abs(luma(c) - luma(a)) > 46
                      or abs((max(c) - min(c)) - (max(a) - min(a))) > 46):
                continue                      # print on the glass: not the pool's
            px[x, y] = px[x, src + ((y - lo) % 3) - 1]
            fixed += 1
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
    for y in range(mouth + 2, H - 2):
        xs = [x for x in range(W) if solid[x][y]]
        if len(xs) < 2 * WALL + 3:
            continue
        l, r = min(xs) + WALL, max(xs) - WALL
        for x in range(l, r + 1):
            if solid[x][y]:
                mask[x][y] = True
                n += 1
        # Transparent gaps inside the span: BELOW the shoulder they are see-through
        # glass windows and the drink lives behind them (the 1810 fix); at the NECK
        # they are handle openings and nothing may be drawn across them (Krakatoa's
        # ears wore liquid, the author's screenshot 2026-08-05) — unless the gap is
        # a few pixels of glass thinness.
        x = l
        while x <= r:
            if solid[x][y]:
                x += 1
                continue
            g0 = x
            while x <= r and not solid[x][y]:
                x += 1
            if y >= shoulder or (x - g0) <= 4:
                for gx in range(g0, x):
                    mask[gx][y] = True
                    n += 1
    if n == 0:
        return None

    tones = sorted((px[x, y][:3] for x in range(W) for y in range(H)
                    if mask[x][y] and px[x, y][3] > 200), key=luma)
    if not tones:
        return None
    glass = tones[len(tones) // 2]
    gl, gc = luma(glass), max(glass) - min(glass)

    # PRINT IS A REGION, not a pixel (the author, 2026-08-05: liquid colour was
    # bleeding through the LABELS). Per-pixel distance failed both ways — a white
    # plate on pale glass sits ~25 luma from it and went translucent; letter strokes
    # are narrow runs and were killed as streaks. So the label is found the way the
    # pilot found it: rows whose MARKED pixels form a long run qualify, contiguous
    # qualifying rows form a band, and each band's bounding box is opaque WHOLESALE —
    # letters, background, emblem and all. A label is printed matter; it does not
    # come in translucent parts.
    def marky_vs(c, bl, bc):
        lum = luma(c)
        ch = max(c) - min(c)
        return ((lum > bl + 18 and ch < 60)          # a plate brighter than the glass
                or abs(ch - bc) > 30                 # chromatic print (bands, seals)
                or lum < bl - 35)                    # dark print (letters, black labels)

    def row_base(y):
        """The row's OWN glass, read at its wall margins — a tinted bottle shades
        darker down its body, and against one global median those shading rows all
        read as 'print' (seven bottles went 2-16% film on the second proof pass).
        The margins move with the shading; a full-width label defeats them, and
        such a row falls back to the global tone, where a plate still reads."""
        xs = [x for x in range(W) if mask[x][y] and px[x, y][3] > 200]
        if len(xs) < 8:
            return gl, gc
        edge = xs[:3] + xs[-3:]
        cs = [px[x, y][:3] for x in edge]
        m = tuple(sorted(c[i] for c in cs)[len(cs) // 2] for i in range(3))
        if marky_vs(m, gl, gc):
            return gl, gc          # the margins are print themselves: full-width label
        return luma(m), max(m) - min(m)

    def marky(c):
        return marky_vs(c, gl, gc)

    # Each row contributes only its LONGEST marked run — a wall shadow or a lone
    # bright pixel can no longer stretch a box to the silhouette (that greed opaqued
    # seven bottles wall-to-wall on the first proof pass). And the bottom 6 rows are
    # BASE, never label: dark base glass kept dragging boxes to the floor, which is
    # exactly the "taban sıvının önünde" fault.
    runs = {}
    for y in range(mouth, H - 6):
        bl, bc = row_base(y)
        best_len, best_l, cur_l, run = 0, 0, 0, 0
        for x in range(W + 1):
            ok = (x < W and mask[x][y] and px[x, y][3] > 200
                  and marky_vs(px[x, y][:3], bl, bc))
            if ok:
                if run == 0:
                    cur_l = x
                run += 1
            else:
                if run > best_len:
                    best_len, best_l = run, cur_l
                run = 0
        if best_len >= 12:
            runs[y] = (best_l, best_l + best_len - 1)
    qualifying = sorted(runs)
    boxes = []
    if qualifying:
        band = [qualifying[0]]
        for y in qualifying[1:]:
            if y <= band[-1] + 3:
                band.append(y)
            else:
                boxes.append(band)
                band = [y]
        boxes.append(band)
        # a band under 4 rows is a shading artefact, not a label
        boxes = [b for b in boxes if b[-1] - b[0] + 1 >= 4]
    rects = []
    for b in boxes:
        bl_, br_ = (min(runs[y][0] for y in b), max(runs[y][1] for y in b))
        # A label must leave glass at the SIDES or the level has nowhere to read
        # (the pilot's 76% law, now the chain's): the box keeps its centre 78%.
        spans = [(min(xs), max(xs)) for xs in
                 ([x for x in range(W) if mask[x][y]] for y in b) if xs]
        if spans:
            sl = sum(s[0] for s in spans) / len(spans)
            sr = sum(s[1] for s in spans) / len(spans)
            margin = (sr - sl + 1) * 0.11
            bl_ = max(bl_, int(sl + margin))
            br_ = min(br_, int(sr - margin))
        if br_ > bl_:
            rects.append((bl_, b[0], br_, b[-1]))
    print('  label boxes:', rects)

    def printed(x, y):
        return any(l <= x <= r and t <= y <= bb for l, t, r, bb in rects)

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
                continue          # inside a label box: printed matter stays whole
            c = px[x, y][:3]
            # film 70 -> 44 (the author, 2026-08-05: "cam bu kadar buğu yapmamalı") —
            # the glass keeps its speculars but stops frosting the drink behind it.
            # Bright STREAKS outside the label boxes are reflections by construction
            # and ride at 150 in their own colour.
            fp[x, y] = c + ((150,) if luma(c) > gl + 20 else (44,))
            film += 1
    return back, front, fill, n, film, glass


def open_front(front, master, seam, strip_wax=False):
    W, H = front.size
    out = front.copy()
    op = out.load()
    px = master.load()
    solid, widths, widest, shoulder, top = anatomy(master)
    for y in range(seam):
        for x in range(W):
            op[x, y] = (0, 0, 0, 0)
    if strip_wax:
        # A wax seal DRIPS below its seam, and the drips belong to the cap (the
        # author, 2026-08-05: "o uzantı kapağa dahil"). Everything under the seam
        # that wears the cap's own colours goes with it; the holes show the back
        # plate, which is the glass the wax was covering.
        caps = [px[x, y][:3] for y in range(top, seam)
                for x in range(W) if solid[x][y]]
        step = max(1, len(caps) // 400)
        caps = caps[::step]
        stripped = 0
        for y in range(seam, min(H, seam + int(H * 0.22))):
            for x in range(W):
                if not solid[x][y]:
                    continue
                c = px[x, y][:3]
                if any(sum(abs(c[i] - k[i]) for i in range(3)) < 60 for k in caps):
                    op[x, y] = (0, 0, 0, 0)
                    stripped += 1
        print('  wax stripped below the seam: %d px' % stripped)
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

# Wax-sealed closures whose drips must leave WITH the cap in the open state.
WAX_STRIP = {'bourbon_ashfall'}


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
        fopen = (open_front(front, im, seam, strip_wax=bid in WAX_STRIP)
                 if seam else None)
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
