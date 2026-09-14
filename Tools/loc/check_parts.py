# -*- coding: utf-8 -*-
"""Checks one language's translation parts without writing its table (2026-09-14, localization L4).

assemble_table.py writes into Assets/, which the open editor imports — not something to do while a
test run is going. This joins Tools/loc/translations/<code>/*.json over the current table IN MEMORY
and runs check_tables.check on the result, so a translator can prove their parts at any time.

    py -3 -X utf8 Tools/loc/check_parts.py de              # errors, warnings, untranslated count
    py -3 -X utf8 Tools/loc/check_parts.py de --list-untranslated
"""
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import check_tables as ct  # noqa: E402

PARTS = os.path.join(ct.ROOT, 'Tools', 'loc', 'translations')


def merged_table(code):
    merged, problems = {}, []
    try:
        _, table = ct.load(code)
        merged.update(table)
    except FileNotFoundError:
        pass
    seen = {}
    for part in sorted(glob.glob(os.path.join(PARTS, code, '*.json'))):
        name = os.path.basename(part)
        try:
            with open(part, encoding='utf-8') as fh:
                entries = json.load(fh).get('strings', [])
        except ValueError as e:
            problems.append('%s: not valid JSON (%s)' % (name, e))
            continue
        for e in entries:
            k = e.get('key')
            if not k or 'text' not in e:
                problems.append('%s: entry without key or text: %r' % (name, e))
                continue
            if k in seen and seen[k][1] != e['text']:
                problems.append('%s: in %s and %s with different text' % (k, seen[k][0], name))
                continue
            seen[k] = (name, e['text'])
            merged[k] = {'key': k, 'text': e['text'], 'src': e.get('src')}
    return merged, problems


def main(argv):
    codes = [a for a in argv if not a.startswith('--')]
    if len(codes) != 1:
        sys.exit('usage: check_parts.py <code> [--list-untranslated]')
    code = codes[0]
    merged, problems = merged_table(code)
    real_load = ct.load
    ct.load = lambda c: ({'code': code, 'strings': list(merged.values())}, merged) if c == code else real_load(c)
    _, en = real_load('en')
    errors, warnings, untranslated = ct.check(code, en)
    errors = problems + errors
    print('%s: %d keys, %d errors, %d warnings, %d untranslated of %d' % (
        code, len(merged), len(errors), len(warnings), len(untranslated), len({ct.base(k) for k in en})))
    for e in errors[:60]:
        print('   ERROR', e)
    if len(errors) > 60:
        print('   ... %d more errors' % (len(errors) - 60))
    for w in warnings[:25]:
        print('   warn ', w)
    if len(warnings) > 25:
        print('   ... %d more warnings' % (len(warnings) - 25))
    if '--list-untranslated' in argv:
        for k in untranslated:
            print('   untranslated', k)
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
