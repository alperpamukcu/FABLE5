# -*- coding: utf-8 -*-
"""Generate v4 vessel takes on PixelLab — through the frozen brief, anchored on the pilot.

  py -3 Tools/v4_bottles/gen.py pilot                  Smirkoff, 3 seeds, no anchor yet
  py -3 Tools/v4_bottles/gen.py take vodka_vor gin_*    anchored takes for cards
  py -3 Tools/v4_bottles/gen.py emblem vodka_astra      the label emblem candidates
  py -3 Tools/v4_bottles/gen.py balance

Raw takes land in Tools/v4_bottles/raw/<id>/s<seed>[_cN].png and NEVER in Assets: the
pipeline's process.py derives the plates into staging/, report.py shows them, the author
picks, and only ship.py touches the game (memory bottle-art-v3-respec — the author's rule).

WHY create_image_pro. The v3 tool (create_map_object) takes neither a seed nor a style
reference — the live schema was read on 2026-08-27 — which is the mechanical reason 29
bottles came back as 29 hands. pro takes style_image + style_copy + up to four labelled
reference_images + seed; every card after the pilot is anchored on the pilot's picked take,
and the camera is taught by a reference rather than begged in prose (GDD 25 §1's lesson:
prose pitch produced flat cut-outs twice).

pro is ASYNC: the call returns a job id, get_image(job_id) returns progress until the
candidates are ready. Candidate count is by canvas: 64 at <=42px, 16 at <=85px, 4 at
<=170px, 1 above — a 96x192 bottle is one candidate per seed, a 32x32 emblem is sixty-four.
"""
import base64
import contextlib
import io
import json
import os
import re
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)
import pixellab                      # noqa: E402  Tools/pixellab.py
import brief                         # noqa: E402

RAW = os.path.join(HERE, 'raw')
STATE = os.path.join(HERE, 'gen_state.json')
ANCHOR = os.path.join(HERE, 'anchor.png')          # the picked pilot, once there is one
CAMERA_REF = os.path.join(HERE, 'camera_ref.png')  # box + cylinder at the house pitch

JOB_RE = re.compile(r'job[_ ]?id\s*[:=]?\s*["\']?([A-Za-z0-9_-]{6,})', re.I)
URL_RE = re.compile(r'https?://[^\s"\'<>)]+')


def _state():
    if os.path.exists(STATE):
        return json.load(io.open(STATE, encoding='utf-8'))
    return {'jobs': {}}


def _save(st):
    # Two generators run at once (takes and emblems); merge over the file so neither
    # overwrites the other's job records.
    on_disk = _state()
    on_disk['jobs'].update(st['jobs'])
    st['jobs'] = on_disk['jobs']
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(st, indent=1))


def _b64(path):
    return base64.b64encode(io.open(path, 'rb').read()).decode('ascii')


def _call(tool, args, timeout=900):
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        msgs = pixellab.call(tool, args, timeout=timeout)
    return buf.getvalue(), msgs


def _texts(msgs):
    for m in msgs or []:
        r = m.get('result') if isinstance(m, dict) else None
        if not r:
            continue
        for c in r.get('content', []):
            if c.get('type') == 'text':
                yield c.get('text', '')


def _job_id(text, msgs):
    for t in list(_texts(msgs)) + [text]:
        mm = JOB_RE.search(t)
        if mm:
            return mm.group(1)
    return None


def _images_from(text, msgs):
    """Every candidate in a get_image reply: inline image parts first, else PNG URLs."""
    out = []
    for m in msgs or []:
        r = m.get('result') if isinstance(m, dict) else None
        if not r:
            continue
        for c in r.get('content', []):
            if c.get('type') == 'image' and c.get('data'):
                out.append(base64.b64decode(c['data']))
    # A multi-candidate job inlines only the first few and says "frames: N" with a
    # download URL that takes ?index=i. Sixty-four emblems are sixty-four fetches, and
    # the whole point of a 32x32 pro call is that they cost nothing extra.
    fm = re.search(r'frames:\s*(\d+)', text)
    dm = re.search(r'download:\s*(https?://\S+?)\?index=\d+', text)
    if fm and dm:
        n, base = int(fm.group(1)), dm.group(1)
        if n > len(out):
            out = []
            for i in range(n):
                try:
                    out.append(pixellab.fetch_url('%s?index=%d' % (base, i)))
                except Exception as e:
                    print('  frame %d fetch failed: %s' % (i, e))
            return out
    if out:
        return out
    for tok in URL_RE.findall(text):
        if '.png' in tok.lower():
            try:
                out.append(pixellab.fetch_url(tok))
            except Exception:
                pass
    return out


def _poll(job_id, timeout=1200, every=8):
    t0 = time.time()
    while time.time() - t0 < timeout:
        text, msgs = _call('get_image', {'job_id': job_id}, timeout=120)
        low = text.lower()
        imgs = _images_from(text, msgs)
        if imgs:
            return imgs, text
        if 'failed' in low or ('error' in low and 'progress' not in low):
            return [], text
        time.sleep(every)
    return [], 'timeout'


def balance():
    text, _ = _call('get_balance', {})
    print(text.strip())


def references(anchored):
    refs = []
    if anchored and os.path.exists(ANCHOR):
        refs.append({'base64': _b64(ANCHOR),
                     'usage': 'house style: match this exact pixel-art style, outline, shading, palette and camera pitch'})
    if os.path.exists(CAMERA_REF):
        refs.append({'base64': _b64(CAMERA_REF),
                     'usage': 'camera only: seen from slightly above, top ellipses, base edge bowed downward'})
    return refs


def _run(tag, args, out_first):
    """Submit one pro job, poll it, save every candidate. Returns saved paths."""
    st = _state()
    t0 = time.time()
    text, msgs = _call(brief.TOOL, args, timeout=300)
    jid = _job_id(text, msgs)
    st['jobs'][tag] = {'job': jid, 'submit': text[:400]}
    _save(st)
    if not jid:
        print('  !! %s: no job id in reply:\n%s' % (tag, text[:500]))
        return []
    imgs, ptext = _poll(jid)
    st['jobs'][tag].update({'n': len(imgs), 'secs': round(time.time() - t0, 1), 'poll': ptext[:300]})
    _save(st)
    if not imgs:
        print('  !! %s: job %s gave no image: %s' % (tag, jid, ptext[:300]))
        return []
    saved = []
    for i, png in enumerate(imgs):
        o = out_first if i == 0 else out_first.replace('.png', '_c%d.png' % i)
        io.open(o, 'wb').write(png)
        saved.append(o)
    print('  -> %s: %d candidate(s) in %.0fs' % (tag, len(imgs), time.time() - t0))
    return saved


def take(card_id, seeds=brief.SEEDS, anchored=True, size=brief.CANVAS):
    """One card, N seeds. Anchored on the pilot unless this IS the pilot."""
    os.makedirs(os.path.join(RAW, card_id), exist_ok=True)
    desc = brief.build(card_id)
    got = []
    for seed in seeds:
        out = os.path.join(RAW, card_id, 's%d.png' % seed)
        if os.path.exists(out):
            print('  have', out); got.append(out); continue
        args = {'description': desc, 'width': size['width'], 'height': size['height'],
                'no_background': True, 'seed': seed}
        refs = references(anchored)
        if refs:
            args['reference_images'] = json.dumps(refs)
        if anchored and os.path.exists(ANCHOR):
            args['style_image_base64'] = _b64(ANCHOR)
            args['style_copy'] = json.dumps(['color_palette', 'outline', 'detail', 'shading'])
        print('  gen %s seed %d ...' % (card_id, seed))
        got += _run('%s:%d' % (card_id, seed), args, out)
    return got


def emblem(card_id, seeds=(5,)):
    """The label medallion: 32x32, no text. pro returns sixty-four candidates at this size."""
    p = brief.emblem_prompt(card_id)
    if not p:
        print('  no emblem for', card_id); return []
    d = os.path.join(RAW, card_id, 'emblem')
    os.makedirs(d, exist_ok=True)
    got = []
    for seed in seeds:
        out = os.path.join(d, 'e%d.png' % seed)
        if os.path.exists(out):
            got.append(out); continue
        args = {'description': p, 'width': 32, 'height': 32, 'no_background': True, 'seed': seed}
        if os.path.exists(ANCHOR):
            args['style_image_base64'] = _b64(ANCHOR)
            args['style_copy'] = json.dumps(['color_palette', 'outline'])
        got += _run('%s:emblem:%d' % (card_id, seed), args, out)
    return got


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'balance'
    if cmd == 'balance':
        balance()
    elif cmd == 'pilot':
        balance()
        take('vodka_astra', anchored=False)
    elif cmd == 'take':
        balance()
        import fnmatch
        ids = [c for pat in sys.argv[2:] for c in brief.CARDS if fnmatch.fnmatch(c, pat)]
        for cid in ids:
            take(cid)
    elif cmd == 'emblem':
        for cid in sys.argv[2:]:
            emblem(cid)
    else:
        print(__doc__)
