# NOTE: needs the PixelLab MCP helper (pixellab.py) on sys.path - the session
# driver carries it; see memory pixellab-mcp-constraints for auth and caps.
# -*- coding: utf-8 -*-
"""The full set: 24 bottles x 2 takes through the frozen brief. Queue, fetch, gate."""
import base64, contextlib, io, json, os, sys, time
from PIL import Image
import pixellab   # noqa: see note above

sys.path.insert(0, r'c:\My project (2)\Tools')
import v3_brief

HERE = os.path.dirname(os.path.abspath(__file__))
STATE = os.environ.get('V3_STATE') or os.path.join(HERE, 'v3_state.json')
RAW = os.environ.get('V3_RAW') or os.path.join(HERE, 'v3_raw')
TAKES = 2


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def queue():
    st = load()
    for bid in v3_brief.BOTTLES:
        args = v3_brief.call_args(bid)
        for i in range(TAKES):
            key = '%s__%d' % (bid, i)
            if st.get(key):
                continue
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                pixellab.call(v3_brief.TOOL, args, timeout=900)
            out = buf.getvalue()
            oid = None
            for line in out.splitlines():
                if line.strip().startswith('id:'):
                    oid = line.split('id:')[1].strip()
            st[key] = oid
            save(st)
            print('%-26s -> %s' % (key, oid if oid else out[:120].replace('\n', ' ')))
            time.sleep(0.6)


def fetch():
    os.makedirs(RAW, exist_ok=True)
    st = load()
    pending = {k: v for k, v in st.items()
               if v and not os.path.exists(os.path.join(RAW, k + '.png'))}
    for wave in range(60):
        if not pending:
            break
        got_any = False
        for key, oid in sorted(pending.items()):
            _, body = pixellab.post(
                {"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                 "params": {"name": "get_map_object", "arguments": {"object_id": oid}}},
                pixellab._sid(), timeout=900)
            ims = []
            for m in pixellab.sse(body):
                for c in ((m.get('result') or {}).get('content') or []):
                    if c.get('type') == 'image':
                        ims.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
            if ims:
                ims[0].save(os.path.join(RAW, key + '.png'))
                print('fetched', key)
                got_any = True
        pending = {k: v for k, v in pending.items()
                   if not os.path.exists(os.path.join(RAW, k + '.png'))}
        if pending and not got_any:
            print(' %d pending...' % len(pending))
            time.sleep(25)
    print('missing:', sorted(pending) if pending else 'none')


def luma(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def gate(path):
    """liquid rows / ratio / dress presence, per take. Returns a score tuple."""
    im = Image.open(path).convert('RGBA')
    bb = im.getbbox()
    if bb is None:
        return (-9, 0, 0), 'EMPTY IMAGE'
    im = im.crop(bb)
    W, H = im.size
    px = im.load()
    solid = [[px[x, y][3] > 40 for y in range(H)] for x in range(W)]
    widths = [sum(1 for x in range(W) if solid[x][y]) for y in range(H)]
    widest = max(widths)
    shoulder = next(y for y in range(H) if widths[y] >= widest * 0.8)

    def row_avg(y):
        cs = [px[x, y][:3] for x in range(W) if px[x, y][3] > 200]
        if len(cs) < 6:
            return None
        n = len(cs)
        return (sum(c[0] for c in cs) / n, sum(c[1] for c in cs) / n,
                sum(c[2] for c in cs) / n)

    body = [b for b in (row_avg(y) for y in range(shoulder + 2, shoulder + 18)) if b]
    if not body:
        return (-9, 0, 0), 'no body'
    glum = sum(luma(b) for b in body) / len(body)
    gbr = sum(max(b) - min(b) for b in body) / len(body)
    # any pooled liquid, whatever its hue: more saturated than the glass and darker
    # (the blue-only rule let six amber-filled whiskies through, 2026-08-05)
    liquid = sum(1 for y in range(shoulder, H - 4)
                 if (lambda a: a and (max(a) - min(a)) > gbr + 16 and luma(a) < glum - 20)(row_avg(y)))
    ratio = H / W
    ratio_ok = 1.6 <= ratio <= 3.0
    # print/dress: rows carrying pixels far from the glass in luma or chroma
    def printish(x, y):
        c = px[x, y][:3]
        return px[x, y][3] > 200 and (abs(luma(c) - glum) > 30
                                      or (max(c) - min(c)) - gbr > 35)
    dress = sum(1 for y in range(shoulder, H)
                if sum(1 for x in range(W) if printish(x, y)) >= 10)
    score = ((1 if (liquid == 0 and ratio_ok and dress >= 12) else 0),
             -liquid, dress)
    return score, '%3dx%-3d r %.1f liq %2d dress %3d' % (W, H, ratio, liquid, dress)


def pick():
    best = {}
    for f in sorted(os.listdir(RAW)):
        bid, i = f[:-4].rsplit('__', 1)
        score, desc = gate(os.path.join(RAW, f))
        mark = ''
        if bid not in best or score > best[bid][0]:
            best[bid] = (score, f)
            mark = ' *'
        print('%-26s %s%s' % (f[:-4], desc, mark))
    picks = {bid: f for bid, (s, f) in best.items()}
    io.open(os.path.join(HERE, 'v3_picks.json'), 'w', encoding='utf-8').write(
        json.dumps(picks, indent=1))
    passing = sum(1 for s, f in best.values() if s[0] == 1)
    print('picked %d bottles, %d pass all gates' % (len(picks), passing))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'queue'
    if cmd == 'queue':
        queue()
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'pick':
        pick()
