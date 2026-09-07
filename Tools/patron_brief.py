# -*- coding: utf-8 -*-
"""THE ROSTER WRITES THE BRIEF (2026-09-07).

Assets/Data/customers/roster.json is where a person is decided; patron_prompts.FIGURE_OPTIONS
is where create_character is told about them. Keeping the two in step by hand is how they
drift, so this generates the second from the first: one entry per person, the roster's own
`look` sentence plus the room and the house rules the prompt book already appends.

    py -3 -X utf8 Tools/patron_brief.py            rewrite the generated block
    py -3 -X utf8 Tools/patron_brief.py show slug  print one person's full prompt

Only the block between the two markers is touched; the hand-written figures above it (the
cast that was briefed before the roster existed) are left exactly as they are.
"""
import io
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ROSTER = os.path.join(ROOT, 'Assets', 'Data', 'customers', 'roster.json')
PROMPTS = os.path.join(HERE, 'patron_prompts.py')
OPEN = '    # ==== GENERATED FROM THE ROSTER - do not edit by hand (patron_brief.py) ===='
CLOSE = '    # ==== end generated ===='

# What each register means in the room, said in the words the model draws from. The author's
# vocabulary: hoodies and sweatshirts and street style, smart casual, and club combinations.
ROOM = {
    'club': 'a customer in a Miami night club, dressed up for the night',
    'street casual': 'a customer in a Miami bar, in relaxed street clothes',
    'smart casual': 'a customer in a Miami bar, in smart casual clothes after work',
}


def people():
    return json.load(io.open(ROSTER, encoding='utf-8'))['people']


# What each pattern says in the prompt. The roster names one per person at most (see
# `garment`), because a leopard jacket over a striped top is where a 220px figure turns to
# noise - the reason the brief said "no pattern" at all until 2026-09-07.
PATTERN = {
    'plain': 'in plain colours, no pattern',
    'leopard': 'the top is a leopard print in tan and brown',
    'stripe': 'the top is boldly striped in two colours',
    'dot': 'the top is a polka dot print',
    'palm': 'the shirt is a palm leaf print in green and cream',
    'floral': 'the top is a bright floral print',
    'shine': 'the top is a shiny satin that catches the light',
    'glitter': 'the top is covered in fine glitter, sparkling',
    'sequin': 'the top is sequinned and reflective',
    'colourblock': 'the top is colour-blocked in three bright colours',
    'tiedye': 'the top is a soft tie-dye in two colours',
    'check': 'the shirt is a small check pattern',
    'geo': 'the shirt is a bold geometric print',
}


def entry(p):
    look = p['look'].rstrip('. ')
    fabric = PATTERN.get(p.get('garment', 'plain'), PATTERN['plain'])
    return ('    "%s": (\n        "%s, %s, "\n'
            '        "%s, no logo, no bag, no hat, nothing black"),\n'
            % (p['slug'], look, ROOM.get(p['dress'], ROOM['street casual']), fabric))


def write():
    s = io.open(PROMPTS, encoding='utf-8').read()
    rows = people()
    block = (OPEN + '\n'
             '    # One entry per person in Assets/Data/customers/roster.json, in that file\'s\n'
             '    # order. The roster is the decision - name, age, country, dress, look - and this\n'
             '    # is only its sentence for create_character. Re-run patron_brief.py after editing\n'
             '    # the roster; anything above the marker is hand written and is left alone.\n'
             + ''.join(entry(p) for p in rows) + CLOSE + '\n')

    if OPEN in s:
        i, j = s.index(OPEN), s.index(CLOSE) + len(CLOSE) + 1
        s = s[:i] + block + s[j:]
    else:
        # first run: the generated block goes at the end of FIGURE_OPTIONS
        i = s.index('FIGURE_OPTIONS = {')
        j = s.index('\n}\n', i)
        s = s[:j + 1] + block + s[j + 1:]
    io.open(PROMPTS, 'w', encoding='utf-8', newline='\n').write(s)
    print('briefed %d people from the roster' % len(rows))


def show(slug):
    sys.path.insert(0, HERE)
    import patron_prompts as brief
    if slug not in brief.FIGURE_OPTIONS:
        raise SystemExit('%s is not in the prompt book - run patron_brief.py first' % slug)
    print(brief.character_prompt(brief.FIGURE_OPTIONS[slug], brief.NEUTRAL_POSE, brief.PIVOT_LANGUAGE))


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'show':
        show(sys.argv[2])
    else:
        write()
