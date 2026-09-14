# -*- coding: utf-8 -*-
"""Checks the in-game string tables against English (2026-09-13, localization L1/L4).

Mirrors what Assets/Tests/EditMode/StringTableFileTests.cs enforces in the editor, plus what only a
tool can see, so a translation can be proven before Unity ever loads it:

  errors (exit 1)   a key en.json does not have; placeholders that differ from English; a counted
                    line missing a plural form its language needs (CLDR, mirrors Core PluralRules);
                    key/placeholder syntax StringTable refuses; a file named differently from its code;
                    a character the language's planned body face cannot draw
  warnings          a stale entry (its `src` is not today's English); a plural form the language
                    never picks; a translation identical to English (fine for names, suspicious for
                    sentences); untranslated keys (they fall back down the chain, see Languages.cs)

    py -3 -X utf8 Tools/loc/check_tables.py                 # every table present
    py -3 -X utf8 Tools/loc/check_tables.py de fr           # some
    py -3 -X utf8 Tools/loc/check_tables.py de --list-untranslated
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOC = os.path.join(ROOT, 'Assets', 'Resources', 'Data', 'loc')
FONTS = os.path.join(ROOT, 'Tools', 'steam_kit', 'fonts')

SHIPPED = ['en', 'zh-CN', 'ru', 'de', 'pt-BR', 'es', 'fr', 'tr', 'pl', 'ko', 'ja', 'uk', 'zh-TW', 'it',
           'es-419', 'pt', 'cs', 'hu', 'ro', 'nl', 'sv', 'da', 'no', 'fi', 'el', 'bg', 'id', 'ms', 'vi']
SUFFIXES = {'zero', 'one', 'two', 'few', 'many', 'other'}


def required_forms(code):   # mirrors LastCall.Core.PluralRules.Required
    if code in ('zh-CN', 'zh-TW', 'ja', 'ko', 'vi', 'id', 'ms'):
        return {'other'}
    if code in ('ru', 'uk', 'pl'):
        return {'one', 'few', 'many'}
    if code in ('cs', 'ro'):
        return {'one', 'few', 'other'}
    return {'one', 'other'}


def possible_forms(code):   # forms PluralRules.For can return for whole numbers
    if code in ('fr', 'pt-BR', 'es', 'es-419', 'it', 'pt'):
        return {'one', 'many', 'other'}
    return required_forms(code) | {'other'}


# the planned body face per language (LOCALIZATION_PLAN §5): Galmuri11 everywhere but Chinese
def face_for(code):
    if code == 'zh-CN':
        return os.path.join(FONTS, 'fusion-pixel-12px-proportional-zh_hans.ttf')
    if code == 'zh-TW':
        return os.path.join(FONTS, 'fusion-pixel-12px-proportional-zh_hant.ttf')
    if code == 'ja':
        return os.path.join(FONTS, 'fusion-pixel-12px-proportional-ja.ttf')
    return os.path.join(FONTS, 'Galmuri11.ttf')


PLACEHOLDER = re.compile(r'\{([^{}]*)\}')
TAG = re.compile(r'<[^<>]*>')


def base(key):
    return key.split('#', 1)[0]


def placeholders(text):
    return set(PLACEHOLDER.findall(text))


def key_problem(key):
    if not key or any(c.isspace() for c in key):
        return 'empty key or whitespace in key'
    if '#' in key:
        head, _, suffix = key.partition('#')
        if not head or suffix not in SUFFIXES:
            return 'plural suffix must be one of ' + ', '.join(sorted(SUFFIXES))
    return None


def text_problem(text):
    depth = 0
    start = 0
    for i, c in enumerate(text):
        if c == '{':
            if depth:
                return "a '{' opens inside another placeholder"
            depth, start = 1, i + 1
        elif c == '}':
            if not depth:
                return "a '}' closes nothing"
            depth = 0
            name = text[start:i]
            if not name or not re.fullmatch(r'[a-z0-9_]+', name):
                return "placeholder '{%s}' is not lower_snake_case" % name
    return "a '{' never closes" if depth else None


def load(code):
    path = os.path.join(LOC, code + '.json')
    with open(path, encoding='utf-8') as fh:
        doc = json.load(fh)
    return doc, {e['key']: e for e in doc.get('strings', [])}


_cmaps = {}


def cmap(path):
    if path not in _cmaps:
        from fontTools.ttLib import TTFont
        _cmaps[path] = TTFont(path).getBestCmap()
    return _cmaps[path]


def check(code, en, list_untranslated=False):
    errors, warnings = [], []
    doc, table = load(code)
    if doc.get('code') != code:
        errors.append('file is %s.json but says code "%s"' % (code, doc.get('code')))
    en_bases = {}
    for k, e in en.items():
        en_bases.setdefault(base(k), set()).update(placeholders(e['text']))
    en_counted = {base(k) for k in en if '#' in k}
    face = cmap(face_for(code))
    missing_glyphs = {}
    for k, e in table.items():
        text = e.get('text')
        if text is None:
            errors.append('%s: no text' % k)
            continue
        p = key_problem(k) or text_problem(text)
        if p:
            errors.append('%s: %s' % (k, p))
        b = base(k)
        if b not in en_bases:
            errors.append('%s: not in en.json' % k)
            continue
        if placeholders(text) != en_bases[b]:
            errors.append('%s: placeholders {%s}, English has {%s}' % (
                k, ','.join(sorted(placeholders(text))), ','.join(sorted(en_bases[b]))))
        if '#' in k and k.split('#', 1)[1] not in possible_forms(code):
            warnings.append('%s: %s never picks the "%s" form' % (k, code, k.split('#', 1)[1]))
        english = en.get(k) or en.get(b + '#other') or en.get(b)
        if english is not None and e.get('src') is not None and e['src'] != english['text']:
            warnings.append('%s: stale (translated from "%s", English is now "%s")' % (k, e['src'], english['text']))
        if english is not None and text == english['text'] and not k.startswith('data.') and len(text) > 12 and ' ' in text:
            warnings.append('%s: identical to English' % k)
        for ch in set(TAG.sub('', PLACEHOLDER.sub('', text))):
            if ch in '\n\t' or ord(ch) < 32:
                continue
            if ord(ch) not in face:
                missing_glyphs.setdefault(ch, []).append(k)
    for b in sorted({base(k) for k in table if '#' in k} | ({base(k) for k in table} & en_counted)):
        for form in sorted(required_forms(code)):
            if b + '#' + form not in table:
                errors.append('%s: counted line has no #%s form (%s needs %s)' % (
                    b, form, code, '/'.join(sorted(required_forms(code)))))
    for ch, keys in sorted(missing_glyphs.items()):
        errors.append('glyph %r (U+%04X) not in %s: %s%s' % (
            ch, ord(ch), os.path.basename(face_for(code)), ', '.join(keys[:3]), ' …' if len(keys) > 3 else ''))
    en_needed = {base(k) for k in en}
    have = {base(k) for k in table}
    untranslated = sorted(en_needed - have)
    return errors, warnings, untranslated


def main(argv):
    list_untranslated = '--list-untranslated' in argv
    codes = [a for a in argv if not a.startswith('--')]
    _, en = load('en')
    for k, e in en.items():
        p = key_problem(k) or text_problem(e['text'])
        if p:
            print('en: %s: %s' % (k, p))
    present = [c for c in SHIPPED if c != 'en' and os.path.exists(os.path.join(LOC, c + '.json'))]
    for f in glob.glob(os.path.join(LOC, '*.json')):
        name = os.path.splitext(os.path.basename(f))[0]
        if name not in SHIPPED:
            print('ERROR %s.json is not a shipped language' % name)
    codes = codes or present
    failed = False
    print('%-6s %6s %6s %6s %6s' % ('code', 'keys', 'errors', 'warn', 'untr.'))
    en_total = len({base(k) for k in en})
    for code in codes:
        if not os.path.exists(os.path.join(LOC, code + '.json')):
            print('%-6s missing file' % code)
            continue
        errors, warnings, untranslated = check(code, en, list_untranslated)
        _, table = load(code)
        print('%-6s %6d %6d %6d %6d / %d' % (code, len(table), len(errors), len(warnings), len(untranslated), en_total))
        for e in errors[:40]:
            print('   ERROR', e)
        if len(errors) > 40:
            print('   ... %d more errors' % (len(errors) - 40))
        for w in warnings[:15]:
            print('   warn ', w)
        if len(warnings) > 15:
            print('   ... %d more warnings' % (len(warnings) - 15))
        if list_untranslated:
            for k in untranslated:
                print('   untranslated', k)
        failed = failed or bool(errors)
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
