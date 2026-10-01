# -*- coding: utf-8 -*-
"""The About section for every language the game ships: header strips drawn in that language and BBCode ready to
paste into Steamworks (Store Page Admin -> Description, one language tab at a time).

    python3 build_about_i18n.py      ->  out/about_i18n/images/*.gif  (upload all once: Description -> Upload images)
                                         out/about_i18n/bbcode/about_<code>.txt  (paste into that language's tab)

Text lives in i18n/about_<code>.json (written from i18n/about_en.json; Roxy's line and the KICK button are the
game's own translations, i18n/glossary.json). The two content strips carry no text and are shared.
"""
import json, os, shutil
import kit
import strips

HERE = os.path.dirname(os.path.abspath(__file__))
I18N = os.path.join(HERE, 'i18n')
OUT = os.path.join(HERE, 'out', 'about_i18n')
IMG = os.path.join(OUT, 'images')
REF_HEADER = os.path.join(HERE, 'out', 'refs', 'header_about.gif')

# the game's 29 languages (Assets/Scripts/Core/Text/Languages.cs): code -> Steamworks language name
LANGS = [('en', 'English'), ('zh-CN', 'Simplified Chinese'), ('ru', 'Russian'), ('de', 'German'),
         ('pt-BR', 'Portuguese - Brazil'), ('es', 'Spanish - Spain'), ('fr', 'French'), ('tr', 'Turkish'),
         ('pl', 'Polish'), ('ko', 'Korean'), ('ja', 'Japanese'), ('uk', 'Ukrainian'),
         ('zh-TW', 'Traditional Chinese'), ('it', 'Italian'), ('es-419', 'Spanish - Latin America'),
         ('pt', 'Portuguese - Portugal'), ('cs', 'Czech'), ('hu', 'Hungarian'), ('ro', 'Romanian'), ('nl', 'Dutch'),
         ('sv', 'Swedish'), ('da', 'Danish'), ('no', 'Norwegian'), ('fi', 'Finnish'), ('el', 'Greek'),
         ('bg', 'Bulgarian'), ('id', 'Indonesian'), ('ms', 'Malay'), ('vi', 'Vietnamese')]

# section -> header icon (the game's own sprites); 'keep' = the author's coupe from their header
ICONS = [('about', 'keep'), ('meet_roxy', 'heart3d'), ('read_the_card', 'card'), ('shake_stir_pour', 'mk_tab_liquor'),
         ('the_tap', 'ib_kegs'), ('the_crowd', 'ib_arrivals'), ('one_night', 'ib_lateness'),
         ('build_the_house', 'ib_room'), ('features', 'ib_round')]

QUOTES = {'de': ('„', '“'), 'cs': ('„', '“'), 'pl': ('„', '”'), 'hu': ('„', '”'), 'ro': ('„', '”'), 'bg': ('„', '“'),
          'ru': ('«', '»'), 'uk': ('«', '»'), 'fr': ('« ', ' »'), 'el': ('«', '»'), 'no': ('«', '»'),
          'ja': ('「', '」'), 'zh-CN': ('“', '”'), 'zh-TW': ('「', '」'), 'da': ('»', '«'), 'sv': ('”', '”'),
          'fi': ('”', '”')}


def img(name):
    return '[img]{STEAM_APP_IMAGE}/extras/%s[/img]' % name


def bbcode(code, t):
    q0, q1 = QUOTES.get(code, ('“', '”'))
    h = lambda k: img('header_%s_%s.gif' % (k, code))
    L = [h('about'), t['hook'], '',
         h('meet_roxy'), '[b]%s%s%s[/b]' % (q0, t['roxy_quote'], q1), '', t['roxy'], '',
         h('read_the_card'), t['card_1'], '', t['card_2'], '',
         h('shake_stir_pour'), '[list]'] + ['[*][b]%s[/b]%s' % (b, r) for b, r in t['shake']] + ['[/list]',
         img('strip_bottles.gif'), t['bottles'], '',
         h('the_tap'), t['tap'], '',
         h('the_crowd'), img('strip_guests.gif'), t['crowd'], '',
         h('one_night'), t['night'], '',
         h('build_the_house'), t['house'], '',
         h('features'), '[list]'] + ['[*]%s' % f for f in t['features']] + ['[/list]']
    return '\n'.join(L) + '\n'


def main(codes=None):
    os.makedirs(IMG, exist_ok=True)
    os.makedirs(os.path.join(OUT, 'bbcode'), exist_ok=True)
    for n in ('strip_bottles', 'strip_guests'):
        src = os.path.join(HERE, 'out', 'about', n + '.gif')
        if not os.path.exists(src):
            strips.main()
        shutil.copy(src, os.path.join(IMG, n + '.gif'))
    done = []
    for code, steam in LANGS:
        if codes and code not in codes:
            continue
        p = os.path.join(I18N, 'about_%s.json' % code)
        if not os.path.exists(p):
            print('missing text:', code)
            continue
        t = json.load(open(p, encoding='utf-8'))
        for key, icon in ICONS:
            out = os.path.join(IMG, 'header_%s_%s.gif' % (key, code))
            if code == 'en' and key == 'about' and os.path.exists(REF_HEADER):
                shutil.copy(REF_HEADER, out)             # the author's own header, as it is
                continue
            kit.save_header(kit.header_lang(t['titles'][key], icon, code, seed=11 + len(key)), out)
        open(os.path.join(OUT, 'bbcode', 'about_%s.txt' % code), 'w', encoding='utf-8').write(bbcode(code, t))
        done.append((code, steam))
        print('%-6s %-24s ok' % (code, steam))
    return done


if __name__ == '__main__':
    import sys
    main(sys.argv[1:] or None)
