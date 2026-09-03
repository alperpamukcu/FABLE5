# -*- coding: utf-8 -*-
"""The proof gate: staging/ -> one self-contained HTML the author picks from.

  py -3 Tools/v4_bottles/report.py vodka_astra [gin_boothby ...]   -> Tools/v4_bottles/report.html

Nothing enters the game from here. Every take is shown as the game would DRAW it — back,
liquid, front — at the two sizes it will actually be seen at (hand 2×, cellar 2×), at three
fills (25 / 60 / 95%), with the open state, the cellar copy, and the audit line under it.
Images are data-URIs with image-rendering: pixelated (memory bottle-art-v3-respec: the
author picks with one word per card — "astra: s23").
"""
import base64
import io
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import process                                   # noqa: E402
import palette                                   # noqa: E402

STAGING = process.STAGING
OUT = os.path.join(HERE, 'report.html')

LIQUID = {'vodka': (230, 236, 242), 'gin': (226, 236, 232), 'rum': (232, 196, 128), 'whiskey': (201, 130, 43),
          'tequila': (240, 226, 190), 'liqueur': (166, 43, 68), 'mixer': (220, 232, 240)}


def data_uri(im, scale=1):
    if scale != 1:
        im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    b = io.BytesIO(); im.save(b, 'PNG')
    return 'data:image/png;base64,' + base64.b64encode(b.getvalue()).decode('ascii')


def img(im, scale, title=''):
    return '<img src="%s" title="%s" style="image-rendering:pixelated;image-rendering:crisp-edges">' % (data_uri(im, scale), title)


def card_html(card_id, take_dir):
    import brief
    fam = brief.family(card_id)
    a = json.load(io.open(os.path.join(take_dir, 'audit.json'), encoding='utf-8'))
    name = os.path.basename(take_dir)
    if 'rejected' in a:
        return '<div class="take rej"><h3>%s</h3><p>REDDEDİLDİ: %s</p></div>' % (name, a['rejected'])
    cells = []
    if fam in brief.SEALED:
        sp = Image.open(os.path.join(take_dir, 'v4_%s.png' % card_id)).convert('RGBA')
        cs = Image.open(os.path.join(take_dir, 'v4_%s_c.png' % card_id)).convert('RGBA')
        cells.append('<div><div class="lbl">el · 2×</div>%s</div>' % img(sp, 2))
        cells.append('<div><div class="lbl">mahzen · 2× (÷3 türetilmiş)</div>%s</div>' % img(cs, 2))
        cells.append('<div><div class="lbl">1×</div>%s %s</div>' % (img(sp, 1), img(cs, 1)))
    else:
        back = Image.open(os.path.join(take_dir, 'v4_%s_back.png' % card_id)).convert('RGBA')
        mask = Image.open(os.path.join(take_dir, 'v4_%s_mask.png' % card_id)).convert('RGBA')
        front = Image.open(os.path.join(take_dir, 'v4_%s_front.png' % card_id)).convert('RGBA')
        backc = Image.open(os.path.join(take_dir, 'v4_%s_back_c.png' % card_id)).convert('RGBA')
        maskc = Image.open(os.path.join(take_dir, 'v4_%s_mask_c.png' % card_id)).convert('RGBA')
        frontc = Image.open(os.path.join(take_dir, 'v4_%s_front_c.png' % card_id)).convert('RGBA')
        liq = LIQUID.get(fam, (220, 220, 220))
        for f in (0.25, 0.60, 0.95):
            cells.append('<div><div class="lbl">el · %d%% dolu</div>%s</div>'
                         % (int(f * 100), img(process.composite(back, mask, front, liq, f), 2)))
        cells.append('<div><div class="lbl">boş (yalnız plakalar)</div>%s</div>'
                     % img(process.composite(back, mask, front, liq, 0.0), 2))
        for f in (0.25, 0.60, 0.95):
            cells.append('<div><div class="lbl">mahzen · %d%%</div>%s</div>'
                         % (int(f * 100), img(process.composite(backc, maskc, frontc, liq, f), 2)))
        cells.append('<div><div class="lbl">mahzen 60%% · 6× · kontur 2px</div>%s</div>'
                     % img(process.composite(backc, maskc, frontc, liq, 0.6), 6))
        f1 = os.path.join(take_dir, 'v4_%s_front_c1.png' % card_id)
        if os.path.exists(f1):
            frontc1 = Image.open(f1).convert('RGBA')
            cells.append('<div><div class="lbl">mahzen 60%% · 6× · kontur 1px</div>%s</div>'
                         % img(process.composite(backc, maskc, frontc1, liq, 0.6), 6))
        cells.append('<div><div class="lbl">plakalar 1×: back · mask · front</div>%s %s %s</div>'
                     % (img(back, 1), img(mask, 1), img(front, 1)))
    m = a.get('measure', {})
    line = ('oran %s · kapak %s px / gövde %s px · taban bombe %s satır (%s) · kavite %s satır · '
            'sıvı-satır %s · palet-dışı %s · kanıt etiket=%s kavite=%s%s'
            % (m.get('ratio'), m.get('cap_w'), m.get('body_w'), m.get('base_bow_rows'), m.get('bow_ratio'),
               a.get('cavity_rows', '-'), a.get('liquid_rows', '-'), a.get('off_palette'),
               a.get('proof_label_pixels_unchanged', '-'), a.get('proof_cavity_pixels_showing_liquid', '-'),
               (' · AÇIK: ' + a['open_state']) if 'open_state' in a else ''))
    return ('<div class="take"><h3>%s</h3><div class="row">%s</div><p class="audit">%s</p></div>'
            % (name, ''.join(cells), line))


def emblem_grid(card_id):
    d = os.path.join(process.RAW, card_id, 'emblem')
    if not os.path.isdir(d):
        return ''
    files = sorted(f for f in os.listdir(d) if f.endswith('.png') and f != 'pick.png')
    def idx(f):
        return 0 if '_c' not in f else int(f.split('_c')[1][:-4])
    files.sort(key=idx)
    cells = ''.join('<div class="em"><div class="lbl">%d</div>%s</div>' % (idx(f), img(Image.open(os.path.join(d, f)).convert('RGBA'), 4))
                    for f in files)
    return '<div class="take"><h3>amblem adayları — "%s: amblem 17" gibi seç</h3><div class="row">%s</div></div>' % (card_id, cells)


def build(cards):
    parts = []
    for cid in cards:
        d = os.path.join(STAGING, cid)
        if not os.path.isdir(d):
            parts.append('<h2>%s — staging yok</h2>' % cid); continue
        takes = sorted(os.listdir(d))
        parts.append('<h2>%s <small>%d take</small></h2>' % (cid, len(takes)))
        for t in takes:
            parts.append(card_html(cid, os.path.join(d, t)))
    pal = ''.join('<i style="background:#%02x%02x%02x"></i>' % c for c in palette.COLOURS)
    html = '''<!doctype html><meta charset="utf-8"><title>v4 bottles — pick</title>
<style>
body{background:#0D0813;color:#F2E8D5;font:14px/1.45 system-ui,sans-serif;margin:24px}
h2{color:#E84DA6;margin:36px 0 8px;border-bottom:1px solid #362447}
h2 small{color:#9C8F80;font-weight:normal;margin-left:10px}
.take{background:#1A1023;border:1px solid #362447;border-radius:6px;padding:12px 14px;margin:10px 0}
.take.rej{border-color:#A62B44;color:#F27D8A}
h3{margin:0 0 8px;color:#7DF0E3;font-size:15px}
.row{display:flex;flex-wrap:wrap;gap:18px;align-items:flex-end}
.row>div{text-align:center}
.em{width:140px}
.lbl{color:#9C8F80;font-size:11px;letter-spacing:.06em;text-transform:uppercase;margin-bottom:4px}
img{background:#241830;border:1px solid #362447;vertical-align:bottom}
.audit{color:#C9BCA8;font-size:12px;margin:10px 0 0}
.pal i{display:inline-block;width:14px;height:14px;margin:1px}
p.how{color:#C9BCA8;max-width:80ch}
</style>
<h1>v4 içecek sanatı — pilot raporu</h1>
<p class="how">Her take, oyunun <b>çizeceği gibi</b> gösterilir: arka plaka → sıvı (maske × renk × doluluk) → ön plaka.
El boyutu 2× (tezgâh), mahzen 2× (÷3 türetilmiş kopya). Tercihi tek kelimeyle ver — <b>"astra: s23"</b> — ve
düzeltme notu ekle. Seçilmeyen hiçbir şey oyuna girmez.</p>
<div class="pal">%s</div>
%s''' % (pal, ''.join(parts))
    io.open(OUT, 'w', encoding='utf-8').write(html)
    print('report ->', OUT, '(%d KB)' % (len(html) // 1024))


if __name__ == '__main__':
    build(sys.argv[1:] or ['vodka_astra'])
