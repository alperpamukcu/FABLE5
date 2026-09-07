# -*- coding: utf-8 -*-
"""THE CUSTOMER DIRECTORY, READ BACK (2026-09-07). Assets/Data/customers/roster.json is the
hundred-strong list the author asked for; this reports on it and checks it against the three
places identity actually lives, so a row that says "drawn" and a folder that is empty cannot
both be true for long.

    py -3 -X utf8 Tools/patron_roster.py              the population, by state and by style
    py -3 -X utf8 Tools/patron_roster.py todo         who is waiting, in production order
    py -3 -X utf8 Tools/patron_roster.py check        every disagreement with the game
    py -3 -X utf8 Tools/patron_roster.py sync         rewrite the art/inGame columns from disk
    py -3 -X utf8 Tools/patron_roster.py md           a table for the report
"""
import collections
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ROSTER = os.path.join(ROOT, 'Assets', 'Data', 'customers', 'roster.json')
PAPERS = os.path.join(ROOT, 'Assets', 'Data', 'customers', 'papers.json')
VOICES = os.path.join(ROOT, 'Assets', 'Resources', 'Data', 'voices.json')
PATRON = os.path.join(ROOT, 'Assets', 'Resources', 'Patron')
HUD = os.path.join(ROOT, 'Assets', 'Scripts', 'UI', 'Hud', 'TycoonHud.cs')
PROMPTS = os.path.join(HERE, 'patron_prompts.py')
CLIPS = ('idle', 'order', 'drink', 'cheer', 'upset', 'walk', 'look_right', 'look_left')


def load():
    return json.load(io.open(ROSTER, encoding='utf-8'))


def game():
    """What the game itself says, read from the files rather than from the roster."""
    hud = io.open(HUD, encoding='utf-8').read()
    i = hud.index('PatronCast =')
    cast = set(re.findall(r'\("([a-zA-Z]+)", [\d.]+f', hud[i:hud.index('};', i)]))
    papers = {p['slug'] for p in json.load(io.open(PAPERS, encoding='utf-8'))['papers'] if p['slug']}
    voices = json.load(io.open(VOICES, encoding='utf-8'))['voices']
    spoken = {slug for v in voices for slug in v.get('people', [])}
    known_voices = {v['id'] for v in voices}
    # DRAWN MEANS PLAYABLE, not "a folder exists": a patron shipped for its idle alone has a
    # folder from the first minute and no clips for another twenty, and the roster is read to
    # answer "who can the bar seat tonight". The bar needs the reaction clips, so that is the
    # test (2026-09-07).
    drawn = set()
    for d in os.listdir(PATRON):
        if not os.path.isdir(os.path.join(PATRON, d)):
            continue
        got = [c for c in ('order', 'drink', 'cheer', 'upset', 'walk')
               if os.path.isdir(os.path.join(PATRON, d, c))
               and any(n.endswith('.png') for n in os.listdir(os.path.join(PATRON, d, c)))]
        if len(got) == 5:
            drawn.add(d)
    # "briefed" used to mean "has a figure in patron_prompts", which stopped saying anything
    # the day patron_brief.py started generating one for everybody in the roster. It means
    # ORDERED now: a take has been asked of PixelLab and is either cooking or landed.
    state_path = os.path.join(HERE, 'patron_trial_state.json')
    state = json.load(io.open(state_path, encoding='utf-8')) if os.path.exists(state_path) else {}
    briefed = {k for k, v in state.items()
               if '__' not in k and (v.get('character_id') or v.get('png'))}
    return cast, papers, spoken, known_voices, drawn, briefed


def frames(slug):
    """How many frames each clip has on disk, for the ones that are drawn."""
    out = {}
    for clip in CLIPS:
        d = os.path.join(PATRON, slug, clip)
        out[clip] = len([n for n in os.listdir(d) if n.endswith('.png')]) if os.path.isdir(d) else 0
    return out


def report():
    people = load()['people']
    by_art = collections.Counter(p['art'] for p in people)
    print('%d people: %s' % (len(people), ', '.join('%d %s' % (n, k) for k, n in by_art.most_common())))
    print('  in the game (cast table): %d' % sum(1 for p in people if p['inGame']))
    print()
    print('cheer:   ' + ', '.join('%s %d' % (k, n) for k, n in collections.Counter(p.get('cheer', '-') for p in people).most_common()))
    print('upset:   ' + ', '.join('%s %d' % (k, n) for k, n in collections.Counter(p.get('upset', '-') for p in people).most_common()))
    print('order:   ' + ', '.join('%s %d' % (k, n) for k, n in collections.Counter(p.get('order', '-') for p in people).most_common()))
    print('dress:   ' + ', '.join('%s %d' % (k, n) for k, n in collections.Counter(p['dress'] for p in people).most_common()))
    print('voice:   ' + ', '.join('%s %d' % (k, n) for k, n in collections.Counter(p['voice'] for p in people).most_common()))
    print('country: ' + ', '.join('%s %d' % (k, n) for k, n in collections.Counter(p['country'] for p in people).most_common(8)))
    ages = [p['age'] for p in people]
    print('age:     %d-%d, %d over fifty' % (min(ages), max(ages), sum(1 for a in ages if a > 50)))


def todo():
    people = load()['people']
    waiting = [p for p in people if p['art'] != 'drawn']
    print('%d waiting on art:' % len(waiting))
    for state in ('briefed', 'planned'):   # briefed = a take is ordered, planned = not asked for
        rows = [p for p in waiting if p['art'] == state]
        if not rows:
            continue
        print('\n  %s (%d)' % (state.upper(), len(rows)))
        for p in rows:
            print('    %-14s %-22s %2d  %-16s %-14s %-9s %s/%s/%s'
                  % (p['slug'], p['name'], p['age'], p['country'], p['dress'], p['voice'],
                     p.get('cheer', '-'), p.get('upset', '-'), p.get('order', '-')))


def check():
    people = load()['people']
    cast, papers, spoken, known_voices, drawn, briefed = game()
    slugs = {p['slug'] for p in people}
    bad = 0

    def say(line):
        nonlocal bad
        bad += 1
        print('  ' + line)

    print('roster vs the game:')
    for p in people:
        s = p['slug']
        if p['art'] == 'drawn' and s not in drawn:
            say('%-14s says drawn, no folder under Resources/Patron' % s)
        if s in drawn and p['art'] != 'drawn':
            say('%-14s has frames on disk but the roster says %s' % (s, p['art']))
        if p['inGame'] and s not in cast:
            say('%-14s says inGame, not in PatronCast' % s)
        if s in cast and not p['inGame']:
            say('%-14s is in PatronCast, roster says otherwise' % s)
        if p['art'] == 'drawn' and s not in papers:
            say('%-14s is drawn with no papers row' % s)
        if p['voice'] not in known_voices:
            say('%-14s speaks "%s", which is not a voice' % (s, p['voice']))
        if p['art'] == 'drawn' and s not in spoken and p['voice'] != 'default':
            say('%-14s is drawn and voiced %s, but voices.json does not list them' % (s, p['voice']))
    for s in drawn - slugs:
        say('%-14s is drawn and not in the roster at all' % s)
    # The host and the fallback row are cast, not crowd: Ece is written in story.json and the
    # empty slug is the licence's fallback, so neither belongs in a directory of customers.
    for s in papers - slugs - {'', 'ece'}:
        say('%-14s has papers and is not in the roster' % s)

    # THE STORY'S FACES TOO (2026-09-07). Trimming papers.json to the roster took the row the
    # host's placeholder was borrowing, and DataLoader refuses a story character whose face has
    # no papers - the scene came up on a FormatException and the bar did not open. papers.json
    # answers to story.json as well as to the roster, so the check asks both.
    story_path = os.path.join(ROOT, 'Assets', 'Data', 'story', 'story.json')
    if os.path.exists(story_path):
        story = json.load(io.open(story_path, encoding='utf-8'))
        for c in story.get('characters', []):
            for key in ('look', 'placeholderLook'):
                face = c.get(key)
                if face and face not in papers:
                    say('story %s.%s is "%s", which has no papers row' % (c['id'], key, face))
                if key == 'placeholderLook' and face and face not in drawn:
                    say('story %s stands in as "%s", which has no art' % (c['id'], face))

    print('\nframe counts (drawn only, the walk should be 17 since 2026-09-07):')
    for p in people:
        if p['art'] != 'drawn':
            continue
        f = frames(p['slug'])
        short = [c for c in CLIPS if c != 'idle' and f[c] < 17]
        if short:
            print('  %-14s %s' % (p['slug'], ', '.join('%s:%d' % (c, f[c]) for c in short)))
    print('\n%d disagreement%s' % (bad, '' if bad == 1 else 's'))
    return bad


def sync():
    """The columns the disk owns — art and inGame — rewritten from what is actually there."""
    doc = load()
    cast, papers, spoken, known_voices, drawn, briefed = game()
    changed = 0
    for p in doc['people']:
        art = 'drawn' if p['slug'] in drawn else 'briefed' if p['slug'] in briefed else 'planned'
        in_game = p['slug'] in cast
        if p['art'] != art or p['inGame'] != in_game:
            changed += 1
            print('  %-14s %s -> %s, inGame %s -> %s' % (p['slug'], p['art'], art, p['inGame'], in_game))
        p['art'] = art
        p['inGame'] = in_game
    json.dump(doc, io.open(ROSTER, 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=1)
    print('%d row%s updated' % (changed, '' if changed == 1 else 's'))


def md():
    people = load()['people']
    print('| # | slug | name | age | country | dress | voice | art | in game |')
    print('|---|------|------|-----|---------|-------|-------|-----|---------|')
    for i, p in enumerate(people, 1):
        print('| %d | `%s` | %s | %d | %s | %s | %s | %s | %s |'
              % (i, p['slug'], p['name'], p['age'], p['country'], p['dress'], p['voice'],
                 p['art'], 'yes' if p['inGame'] else 'no'))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'report'
    if cmd == 'todo':
        todo()
    elif cmd == 'check':
        sys.exit(1 if check() else 0)
    elif cmd == 'sync':
        sync()
    elif cmd == 'md':
        md()
    else:
        report()
