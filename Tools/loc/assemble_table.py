# -*- coding: utf-8 -*-
"""Builds one language's in-game table from translation parts (2026-09-13, localization L4).

A whole table is too long to write in one go, so translators write parts:
    Tools/loc/translations/<code>/<anything>.json    {"strings": [{"key", "text", "src"}, ...]}
and this joins them into Assets/Resources/Data/loc/<code>.json (sorted by key, UTF-8, the format
DataLoader.ParseStringTable reads). A key in two parts is an error unless both say the same thing.
Entries already in the table and absent from every part are kept.

    py -3 -X utf8 Tools/loc/assemble_table.py de        # then: py -3 -X utf8 Tools/loc/check_tables.py de
"""
import glob
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOC = os.path.join(ROOT, 'Assets', 'Resources', 'Data', 'loc')
PARTS = os.path.join(ROOT, 'Tools', 'loc', 'translations')


def main(code):
    target = os.path.join(LOC, code + '.json')
    merged = {}
    if os.path.exists(target):
        with open(target, encoding='utf-8') as fh:
            for e in json.load(fh).get('strings', []):
                merged[e['key']] = e
    seen = {}
    errors = []
    parts = sorted(glob.glob(os.path.join(PARTS, code, '*.json')))
    if not parts:
        sys.exit('no parts in %s' % os.path.join(PARTS, code))
    for part in parts:
        try:
            with open(part, encoding='utf-8') as fh:
                entries = json.load(fh).get('strings', [])
        except ValueError as e:
            errors.append('%s: not valid JSON (%s)' % (os.path.basename(part), e))
            continue
        for e in entries:
            k = e.get('key')
            if not k or 'text' not in e:
                errors.append('%s: entry without key or text: %r' % (os.path.basename(part), e))
                continue
            if k in seen and (seen[k][1]['text'] != e['text']):
                errors.append('%s: in %s and %s with different text' % (k, seen[k][0], os.path.basename(part)))
                continue
            seen[k] = (os.path.basename(part), e)
            entry = {'key': k, 'text': e['text']}
            if e.get('src') is not None:
                entry['src'] = e['src']
            merged[k] = entry
    if errors:
        for e in errors:
            print('ERROR', e)
        sys.exit(1)
    lines = ['{', '  "code": "%s",' % code, '  "strings": [']
    items = [merged[k] for k in sorted(merged)]
    for i, e in enumerate(items):
        lines.append('    ' + json.dumps(e, ensure_ascii=False) + (',' if i < len(items) - 1 else ''))
    lines += ['  ]', '}']
    text = '\n'.join(lines) + '\n'
    json.loads(text)
    with open(target, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(text)
    print('wrote %s: %d keys from %d parts' % (os.path.relpath(target, ROOT), len(items), len(parts)))


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit('usage: assemble_table.py <code>')
    main(sys.argv[1])
