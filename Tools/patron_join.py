# -*- coding: utf-8 -*-
"""JOIN A SHIPPED PATRON TO THE BAR (2026-09-07). After patron_ship.py has stood a character's
clips under Resources/Patron/<slug>/ and logged its measured head row, three things still have
to be written by hand - and used to be: the cast row in TycoonHud.PatronCast (slug, HeadY,
Stars, HoldRight, HoldLeft), the papers row (name, age, country, flag) and the voice they
speak in. This does the three, idempotently: a slug already in a table is left as it is.

    py -3 -X utf8 Tools/patron_join.py <slug> --name "Osvaldo Reyes" --age 64 \\
        --country "United States" --iso us --voice miami [--stars 0]
"""
import argparse
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
HUD = os.path.join(ROOT, 'Assets', 'Scripts', 'UI', 'Hud', 'TycoonHud.cs')
PAPERS = os.path.join(ROOT, 'Assets', 'Data', 'customers', 'papers.json')
VOICES = os.path.join(ROOT, 'Assets', 'Resources', 'Data', 'voices.json')


def shipped(slug):
    """The last 'shipped' record for the slug: head_y and the two glance holds."""
    rec = None
    for line in io.open(LOG, encoding='utf-8'):
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r.get('asset') == 'patron/' + slug and r.get('event') == 'shipped':
            rec = r
    if rec is None or rec.get('head_y') is None:
        raise SystemExit('no shipped record with a head_y for %s - run patron_ship.py first' % slug)
    return rec


def join_cast(slug, head_y, stars, hold_r, hold_l):
    src = io.open(HUD, encoding='utf-8').read()
    if re.search(r'\("%s", ' % re.escape(slug), src):
        print('  cast: %s already in PatronCast' % slug)
        return
    anchor = '            ("leopard", 4f, 0f, 4, 6),\n'
    assert src.count(anchor) == 1, 'the cast table anchor moved'
    row = '            ("%s", %sf, %sf, %d, %d),   // the tenth list, 2026-09-07\n' % (
        slug, head_y, stars, hold_r, hold_l)
    src = src.replace(anchor, anchor + row)
    io.open(HUD, 'w', encoding='utf-8', newline='\n').write(src)
    print('  cast: %s' % row.strip())


def join_papers(slug, name, age, country, iso):
    src = io.open(PAPERS, encoding='utf-8').read()
    if '"slug": "%s"' % slug in src:
        print('  papers: %s already there' % slug)
        return
    row = '{ "slug": "%s", "name": "%s", "age": %d, "country": "%s", "iso": "%s" }' % (
        slug, name, age, country, iso)
    tail = src.rstrip()
    assert tail.endswith(']\n}') or tail.endswith(']}') or tail.endswith('}'), 'papers.json ends oddly'
    i = src.rfind(']')
    j = src.rfind('}', 0, i)                      # the last row's closing brace
    src = src[:j + 1] + ',\n    ' + row + src[j + 1:]
    io.open(PAPERS, 'w', encoding='utf-8', newline='\n').write(src)
    json.loads(src)
    print('  papers: %s' % row)


def join_voice(slug, voice):
    src = io.open(VOICES, encoding='utf-8').read()
    data = json.loads(src)
    for v in data['voices']:
        if slug in v.get('people', []):
            print('  voice: %s already speaks %s' % (slug, v['id']))
            return
    m = re.search(r'"id": "%s",\n\s+"name": "[^"]*",\n\s+"isos": \[[^\]]*\],\n\s+"people": \[([^\]]*)\]' % re.escape(voice), src)
    assert m, 'no voice called %s' % voice
    people = m.group(1)
    new = people + (', ' if people.strip() else '') + '"%s"' % slug
    src = src[:m.start(1)] + new + src[m.end(1):]
    json.loads(src)
    io.open(VOICES, 'w', encoding='utf-8', newline='\n').write(src)
    print('  voice: %s -> %s' % (slug, voice))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('slug')
    ap.add_argument('--name', required=True)
    ap.add_argument('--age', type=int, required=True)
    ap.add_argument('--country', required=True)
    ap.add_argument('--iso', required=True)
    ap.add_argument('--voice', required=True)
    ap.add_argument('--stars', type=float, default=0.0)
    a = ap.parse_args()
    rec = shipped(a.slug)
    holds = rec.get('holds') or {}
    hold_r = int(holds.get('look_right', 5))
    hold_l = int(holds.get('look_left', 5))
    join_cast(a.slug, rec['head_y'], a.stars, hold_r, hold_l)
    join_papers(a.slug, a.name, a.age, a.country, a.iso)
    join_voice(a.slug, a.voice)


if __name__ == '__main__':
    main()
