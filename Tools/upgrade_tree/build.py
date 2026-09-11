# -*- coding: utf-8 -*-
"""THE UPGRADE TREE (2026-09-12, the author: "tüm geliştirmeleri ağaç şeklinde html olarak
görmeliyim, aynı zamanda kendim tıklayarak istediğim kombinasyonu yapıp görebilmeliyim böylece
tier belirleriz, bu sistemi ilerideki eklenecek upgradelerde de kullanırız").

One self-contained page: every ladder of the room — the game's own fixtures and the approved
art that has not shipped yet — as a tree, and the room itself, stacked from the pieces the
player would own. Click a rung and it stands in the room; move rungs up and down to set the
tiers; save a combination. The page keeps its choices in the artifact's database so the next
session can read them back.

Inputs
  Assets/Data/fixtures/fixtures.json          the ladders, prices, comfort, stars
  Tools/upgrade_tree/layers/                  the room, taken apart — LastCall → Export Room
                                              Layers (play mode, room on screen)
  Assets/Resources/Fixtures/                  the backdrops (wall plates, the right wall)
  Tools/upgrade_tree/candidates.json          approved art not in the game yet
Out
  Tools/upgrade_tree/upgrade_tree.html

A NEW UPGRADE is new data: add it to fixtures.json (and ship its art) or to candidates.json,
re-export the layers if it stands in the room, and run this again.

  py -3 -X utf8 Tools/upgrade_tree/build.py
"""
import base64
import io
import json
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
LAYERS = os.path.join(HERE, 'layers')
W, H = 640, 360

GROUPS = [
    ('walls', 'Duvarlar', 'THE WALLS'),
    ('wall_art', 'Duvarda', 'ON THE WALL'),
    ('light', 'Işık', 'THE LIGHT'),
    ('furniture', 'Mobilya ve zemin', 'FURNITURE & FLOOR'),
    ('greenery', 'Bitkiler', 'GREENERY'),
    ('counter', 'Tezgâh', 'THE COUNTER'),
]
SLOT_TITLES = {
    'walls': 'Arka duvar', 'walls_right': 'Sağ duvar', 'ceiling': 'Tavan', 'floor': 'Zemin',
    'wall_center': 'Orta duvar · tablo', 'wall_right': 'Sağ duvar · neon', 'wall_tv': 'Televizyon',
    'wall_right_art': 'Sağ duvar · tablo', 'poster_left': 'Poster · sol duvar',
    'poster_right': 'Poster · sağ duvar', 'wall_lamps': 'Duvar lambaları (çift)',
    'table_left': 'Sol masa', 'table_right': 'Sağ masa', 'floor_rug': 'Halı',
    'plant_left': 'Sol bitki', 'plant_right': 'Sağ bitki', 'taps': 'Bira musluğu',
    'sink': 'Lavabo', 'beer_mat': 'Damla paspası', 'prep_mat': 'Garnitür paspası',
    'shaker': 'Shaker',
}


def png_uri(im):
    buf = io.BytesIO()
    im.save(buf, 'PNG', optimize=True)
    return 'data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode('ascii')


def cropped(im, order):
    """A 640x360 layer as the page stacks it: the drawn part only, and where it goes."""
    im = im.convert('RGBA')
    box = im.getbbox()
    if box is None:
        return None
    x0, y0, x1, y1 = box
    return {'src': png_uri(im.crop(box)), 'x': x0, 'y': y0, 'w': x1 - x0, 'h': y1 - y0,
            'order': order, 'foot': y1}


def thumb(im, box=None):
    """A small picture of a rung for its row: its drawn part, scaled by whole pixels."""
    im = im.convert('RGBA')
    box = box or im.getbbox()
    if box is None:
        return None
    part = im.crop(box)
    w, h = part.size
    k = max(1, min(72 // max(w, 1), 48 // max(h, 1)))
    if w > 72 or h > 48:
        f = min(72 / w, 48 / h)
        part = part.resize((max(1, int(w * f)), max(1, int(h * f))), Image.NEAREST)
    elif k > 1:
        part = part.resize((w * k, h * k), Image.NEAREST)
    return png_uri(part)


def main():
    fx = json.load(open(os.path.join(ROOT, 'Assets', 'Data', 'fixtures', 'fixtures.json'), encoding='utf-8'))
    exp = json.load(open(os.path.join(LAYERS, 'layers.json'), encoding='utf-8'))
    cand = json.load(open(os.path.join(HERE, 'candidates.json'), encoding='utf-8'))
    exported = {l['id']: l for l in exp['layers']}

    slots = {s['id']: s for s in fx['slots']}
    ladders, rungs, order_of_slots = {}, {}, []

    def ladder(slot_id, group, title, backdrop=False, candidate_slot=False):
        if slot_id not in ladders:
            ladders[slot_id] = {'slot': slot_id, 'group': group, 'title': title,
                                'backdrop': backdrop, 'candidateSlot': candidate_slot,
                                'rungs': []}
            order_of_slots.append(slot_id)
        return ladders[slot_id]

    slot_group = {}
    for f in fx['fixtures']:
        if f.get('group'):
            slot_group.setdefault(f['slot'], f['group'].lower())

    # ── the game's own fixtures ──────────────────────────────────────────────
    for f in fx['fixtures']:
        s = slots[f['slot']]
        group = (f.get('group') or slot_group.get(f['slot']) or 'counter').lower()
        lad = ladder(f['slot'], group, SLOT_TITLES.get(f['slot'], f['slot']), backdrop=bool(s.get('backdrop')))
        layers, th = [], None
        if s.get('backdrop'):
            art = Image.open(os.path.join(FIXT, f['sprite'] + '.png'))
            c = cropped(art, 12 if s.get('overlay') else 10)
            if c:
                layers.append(c)
            sw = f.get('swatch')
            th = thumb(Image.open(os.path.join(FIXT, sw + '.png'))) if sw else thumb(art)
        elif not s.get('carried'):
            for key in ('fx_' + f['id'], 'fx_' + f['id'] + '__hi'):
                if key in exported:
                    art = Image.open(os.path.join(LAYERS, key + '.png'))
                    c = cropped(art, exported[key]['orderMin'])
                    if c:
                        layers.append(c)
                        if th is None:
                            th = thumb(art)
            if not layers:
                print('  (no exported layer for %s — re-export the room)' % f['id'])
        else:
            spr = os.path.join(ROOT, 'Assets', 'Resources', 'Items', f['sprite'] + '.png')
            if os.path.exists(spr):
                th = thumb(Image.open(spr))
        level = f.get('level') or f.get('tapLevel') or 0
        rungs[f['id']] = {
            'id': f['id'], 'slot': f['slot'], 'name': f['name'], 'level': level,
            'price': f.get('price'), 'comfort': f.get('comfort', 0), 'stars': f.get('stars', 0),
            'starts': bool(f.get('startsInTheRoom')), 'candidate': False,
            'carried': bool(s.get('carried')), 'flavor': f.get('flavor', ''),
            'layers': layers, 'thumb': th,
        }
        lad['rungs'].append(f['id'])

    # ── approved art that has not shipped ────────────────────────────────────
    for cs in cand['slots']:
        ladder(cs['id'], cs['group'], cs['title'], candidate_slot=True)['order'] = cs['order']
    cand_order = {cs['id']: cs['order'] for cs in cand['slots']}
    for c in cand['items']:
        lad = ladders.get(c['slot'])
        if lad is None:
            raise SystemExit('candidate %s names slot %s, which is nowhere' % (c['id'], c['slot']))
        layers = []
        if 'layer' in c:
            art = Image.open(os.path.join(ROOT, c['layer'])).convert('RGBA')
            lay = cropped(art, cand_order.get(c['slot'], 15))
            th = thumb(art)
        else:
            spr = Image.open(os.path.join(ROOT, c['sprite'])).convert('RGBA')
            spr = spr.crop(spr.getbbox())
            like = exported.get('fx_' + c['like'])
            ref = Image.open(os.path.join(LAYERS, 'fx_' + c['like'] + '.png')).getbbox()
            hangs = bool(slots.get(c['slot'], {}).get('hangs'))
            cx = (ref[0] + ref[2]) / 2
            if hangs:   # a picture hangs by its middle, where the one it replaces hung
                x0 = int(round(cx - spr.width / 2))
                y0 = int(round((ref[1] + ref[3]) / 2 - spr.height / 2))
            else:       # a thing stands on its feet, where the one it replaces stood
                x0 = int(round(cx - spr.width / 2))
                y0 = ref[3] - spr.height
            canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
            canvas.alpha_composite(spr, (max(0, x0), max(0, y0)))
            lay = cropped(canvas, like['orderMin'] if like else 20)
            th = thumb(spr)
        if lay:
            layers.append(lay)
        rungs[c['id']] = {
            'id': c['id'], 'slot': c['slot'], 'name': c['name'], 'level': 0, 'price': None,
            'comfort': None, 'stars': None, 'starts': False, 'candidate': True,
            'carried': False, 'flavor': 'Round %s · approved, not in the game yet.' % c.get('round', '?'),
            'layers': layers, 'thumb': th,
        }
        lad['rungs'].append(c['id'])

    # The game's rungs in their tier order, candidates after them in the file's order.
    for lad in ladders.values():
        lad['rungs'].sort(key=lambda rid: (rungs[rid]['candidate'], rungs[rid]['level'] or 99))

    base = []
    for key in ('base_under', 'base_glass', 'base_mid', 'base_front'):
        if key in exported:
            c = cropped(Image.open(os.path.join(LAYERS, key + '.png')), exported[key]['orderMin'])
            if c:
                base.append(c)

    groups = []
    for gid, tr, en in GROUPS:
        ids = [sid for sid in order_of_slots if ladders[sid]['group'] == gid]
        if ids:
            groups.append({'id': gid, 'title': tr, 'shelf': en, 'ladders': ids})
    loose = [sid for sid in order_of_slots if ladders[sid]['group'] not in dict((g[0], 1) for g in GROUPS)]
    if loose:
        groups.append({'id': 'room', 'title': 'Oda', 'shelf': 'THE ROOM', 'ladders': loose})

    data = {'size': [W, H], 'groups': groups, 'ladders': ladders, 'rungs': rungs, 'base': base}
    page = open(os.path.join(HERE, 'page.html'), encoding='utf-8').read()
    page = page.replace('/*DATA*/null', json.dumps(data, ensure_ascii=False, separators=(',', ':')))
    out = os.path.join(HERE, 'upgrade_tree.html')
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write(page)
    n_c = sum(1 for r in rungs.values() if r['candidate'])
    print('%d ladders, %d rungs (%d approved, not in the game) -> %s  (%d KB)'
          % (len(ladders), len(rungs), n_c, out, len(page) // 1024))


if __name__ == '__main__':
    main()
