# -*- coding: utf-8 -*-
"""The eighth round's picks page (2026-09-29): the five room layouts photographed in the real room (probe r269, the
screen-space HUD hidden, time frozen), every candidate of every piece beside them, and the deco lamp's four colours.
Writes Docs/reports/room_layouts8/index.html and its img/ folder; nothing here touches Assets.

  py -3 -X utf8 Tools/room_variants8_page.py <folder of r269 shots>
"""
import html
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'room_variants8')
OUT = os.path.join(ROOT, 'Docs', 'reports', 'room_layouts8')
IMG = os.path.join(OUT, 'img')

ROOM_CROP = (0, 0, 1280, 548)          # the room over the counter; the hostess's plate starts at y 565

LAYOUTS = [
    ('L1', 'Köşe meyhanesi', 'Barın açıldığı gece: ucuz, yorgun, dürüst.',
     {'ceil': 0, 'back': 0, 'floor': 0, 'rwall': 0, 'pic': 2}, {},
     'Mint tuğla, koyu tahta tavan, damalı karo. Birinci basamak için doğru yoksulluk; lekeler ve dökülen sıva hikâyeyi taşıyor.'),
    ('L2', 'Plaj barakası', 'Hurda tahta ve güneşle toparlanmış.',
     {'ceil': 0, 'back': 0, 'floor': 2, 'rwall': 0, 'pic': 0}, {'floor': 0},
     'Tavan ve palmiye yapraklı sağ duvar iyi. Zemin 2 odada kum gibi soluk kaldı; zemin 0 (palmiyeli tahta) daha okunur. '
     'Resmin çerçevesi yok, arka duvarın muralına karışıyor; seçilirse çerçeveyi kodla çizerim.'),
    ('L3', 'Tropik salon', 'Birisi para harcamış.',
     {'ceil': 0, 'back': 0, 'floor': 0, 'rwall': 0, 'pic': 0}, {'rwall': 3},
     'Mercan duvar ve alttaki yaprak bandı güçlü. Sağ duvar 0 odada çok kalabalık; 3 (çapraz hasır) daha sakin. '
     'Resim burada da çerçevesiz, duvarda asılı değil yüzüyor gibi.'),
    ('L4', 'Art Deco Miami', 'Ev sahibesinin hatırladığı Ocean Drive.',
     {'ceil': 0, 'back': 0, 'floor': 1, 'rwall': 0, 'pic': 0}, {},
     'En bütünlüklü oda. Güneş ışınlı altın çerçeve, sütunlar ve tavanın ışın deseni aynı dili konuşuyor. Olduğu gibi alınabilir.'),
    ('L5', 'Kulüp', 'Beşinci yıldızın aldığı oda.',
     {'ceil': 1, 'back': 0, 'floor': 0, 'rwall': 0, 'pic': 0}, {},
     'Zümrüt kapitone duvar, mürdüm yıldızlı tavan, pirinç çizgiler. Merdivenin tepesi gibi duruyor; olduğu gibi alınabilir.'),
]
PIECES = [('ceil', 'Tavan'), ('back', 'Arka duvar'), ('floor', 'Zemin'), ('rwall', 'Sağ duvar'), ('pic', 'Orta resim')]
LAMPS = [(0, 'Yeşil pirinç, mavi cam'), (1, 'Bakır, buzlu cam'), (2, 'Krom, füme cam'), (3, 'Kırmızı lake, sarı cam')]
LAMP_PICK = 1


def copy_shots(shots):
    os.makedirs(IMG, exist_ok=True)
    for name in ['now'] + [l[0] for l in LAYOUTS]:
        Image.open(os.path.join(shots, name + '.png')).convert('RGB').crop(ROOM_CROP).save(os.path.join(IMG, 'room_%s.png' % name))
    Image.open(os.path.join(shots, 'L3_hud.png')).convert('RGB').save(os.path.join(IMG, 'room_L3_hud.png'))
    # the lamps hang in the L5 room (the probe swapped the lamps last, over the club)
    lamp_shots = {0: 'L5', 1: 'L4_lamp1', 2: 'L4_lamp2', 3: 'L4_lamp3'}
    for i, shot in lamp_shots.items():
        im = Image.open(os.path.join(shots, shot + '.png')).convert('RGB')
        im.crop((520, 0, 760, 240)).save(os.path.join(IMG, 'lamp_room_%d.png' % i))
        shutil.copy(os.path.join(SRC, 'lamp_deco_%d.png' % i), os.path.join(IMG, 'lamp_%d.png' % i))
    for lay in LAYOUTS:
        for key, _ in PIECES:
            for n in range(4):
                p = os.path.join(SRC, '%s_%s_%d.png' % (lay[0], key, n))
                if os.path.exists(p):
                    shutil.copy(p, os.path.join(IMG, os.path.basename(p)))


def esc(s):
    return html.escape(s, quote=True)


CSS = r'''
:root {
  color-scheme: dark;
  --ground: #16121c; --panel: #201a28; --panel-2: #2a2233; --line: #3b3046;
  --ink: #f1e8dc; --muted: #ab9fb6; --brass: #d9a44b; --teal: #5cc0b3; --pink: #ef80a2;
  --wall: #3a3242;
  --display: "Big Shoulders Display", "Arial Narrow", sans-serif;
  --body: "IBM Plex Sans", "Segoe UI", system-ui, sans-serif;
  --mono: "IBM Plex Mono", ui-monospace, Consolas, monospace;
}
* { box-sizing: border-box; }
body { background: var(--ground); color: var(--ink); font: 16px/1.55 var(--body); margin: 0; padding-inline: 20px; padding-block: 0 64px; }
.wrap { max-width: 1280px; margin: 0 auto; }
img.px { image-rendering: pixelated; image-rendering: crisp-edges; display: block; max-width: 100%; height: auto; }
header { padding-block: 40px 20px; display: grid; gap: 12px; }
.eyebrow { font: 500 12px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; color: var(--brass); margin: 0; }
h1 { font: 800 clamp(40px, 7vw, 76px)/.95 var(--display); letter-spacing: .01em; text-transform: uppercase; margin: 0; text-wrap: balance; }
h2 { font: 800 clamp(30px, 4vw, 44px)/1 var(--display); text-transform: uppercase; margin: 0; text-wrap: balance; }
h3 { font: 700 20px/1.1 var(--display); text-transform: uppercase; letter-spacing: .04em; margin: 0; color: var(--muted); }
.lede { max-width: 68ch; color: var(--muted); margin: 0; }
.lede b { color: var(--ink); font-weight: 600; }
nav.jump { position: sticky; top: env(safe-area-inset-top, 0px); z-index: 5; background: color-mix(in srgb, var(--ground) 92%, transparent);
  backdrop-filter: blur(6px); padding-block: 10px; border-bottom: 1px solid var(--line); display: flex; flex-wrap: wrap; gap: 8px; }
nav.jump a { font: 500 13px/1 var(--mono); color: var(--ink); text-decoration: none; padding: 8px 12px; border: 1px solid var(--line); border-radius: 2px; }
nav.jump a:hover, nav.jump a:focus-visible { border-color: var(--brass); color: var(--brass); outline: none; }
.ladder { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 8px; margin-block: 24px 8px; }
.ladder a { text-decoration: none; color: var(--ink); display: grid; gap: 6px; }
.ladder a:focus-visible { outline: 2px solid var(--brass); outline-offset: 3px; }
.ladder span { font: 500 12px/1.2 var(--mono); color: var(--muted); }
.ladder span b { color: var(--ink); font-weight: 600; }
section.layout { padding-block: 48px 8px; border-top: 1px solid var(--line); margin-top: 40px; display: grid; gap: 18px; scroll-margin-top: 60px; }
.head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px 16px; }
.tag { font: 600 14px/1 var(--mono); color: var(--ground); background: var(--brass); padding: 6px 8px; border-radius: 2px; }
.sub { color: var(--muted); margin: 0; }
.shot { border: 1px solid var(--line); background: #000; }
.note { max-width: 72ch; margin: 0; }
.pieces { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 14px; }
@media (min-width: 1180px) { .pieces { grid-template-columns: repeat(5, minmax(0, 1fr)); } }
.piece { background: var(--panel); border: 1px solid var(--line); padding: 14px; display: grid; gap: 10px; align-content: start; }
.cands { display: flex; flex-wrap: wrap; gap: 10px; }
figure { margin: 0; display: grid; gap: 6px; justify-items: start; }
figure .tile { background: var(--wall); padding: 4px; border: 2px solid transparent; }
figure.in .tile { border-color: var(--teal); }
figure.rec .tile { border-color: var(--pink); }
figcaption { font: 500 12px/1.2 var(--mono); color: var(--muted); display: flex; gap: 6px; flex-wrap: wrap; align-items: center; }
.chip { font: 600 10px/1 var(--mono); letter-spacing: .08em; text-transform: uppercase; padding: 3px 5px; border-radius: 2px; }
.chip.in { background: var(--teal); color: var(--ground); }
.chip.rec { background: var(--pink); color: var(--ground); }
.tile img.sq { width: 88px; }
.tile img.pic, .tile img.back { width: 190px; max-width: 100%; }
.lamps { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.lamp { background: var(--panel); border: 1px solid var(--line); padding: 12px; display: grid; gap: 10px; }
.lamp.rec { border-color: var(--pink); }
.lamp .pair { display: grid; grid-template-columns: 1fr 2fr; gap: 10px; align-items: end; }
.lamp .solo { background: var(--wall); padding: 6px; }
.lamp p { margin: 0; font-size: 14px; }
.ask { background: var(--panel-2); border: 1px solid var(--line); padding: 20px; display: grid; gap: 12px; max-width: 80ch; }
.ask ol { margin: 0; padding-left: 20px; display: grid; gap: 8px; }
code { font: 500 14px/1 var(--mono); color: var(--brass); }
.legend { display: flex; flex-wrap: wrap; gap: 14px; font: 500 12px/1 var(--mono); color: var(--muted); }
@media (max-width: 900px) { .ladder { grid-template-columns: repeat(3, minmax(0, 1fr)); } .lamps { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 460px) { .ladder { grid-template-columns: repeat(2, minmax(0, 1fr)); } .lamps { grid-template-columns: 1fr; } }
@media (prefers-reduced-motion: reduce) { * { scroll-behavior: auto !important; } }
'''


def page():
    out = ['<title>Beş Oda Düzeni</title>',
           '<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>',
           '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Big+Shoulders+Display:wght@700;800'
           '&family=IBM+Plex+Mono:wght@500;600&family=IBM+Plex+Sans:wght@400;600&display=swap">',
           '<style>' + CSS + '</style>', '<div class="wrap">']
    out.append('<header><p class="eyebrow">LAST CALL · oda merdiveni · sekizinci tur · 29 Eylül 2026</p>'
               '<h1>Beş oda düzeni</h1>'
               '<p class="lede">Birinci basamaktan beşinciye beş bütün oda. Her biri ayrı katmanlardan kurulu: <b>tavan, arka duvar, '
               'zemin, sağ duvar ve orta resim</b>. İsterseniz bir düzeni bütün olarak, isterseniz parça parça seçebilirsiniz. '
               'Fotoğraflar gerçek odadan alındı: oyunun kendi ışığı açık, üst bar ve tezgâh kapakları gizli, saat durduruldu. '
               'Oyun verisine hiçbir şey eklenmedi; sprite\'lar çalışma anında değiştirildi. Tavandaki lambalar da yeni '
               'Art Deco sarkıt; aşağıda dört rengi var.</p></header>')
    out.append('<nav class="jump" aria-label="Bölümler">' + ''.join('<a href="#%s">%s · %s</a>' % (l[0], l[0], esc(l[1])) for l in LAYOUTS)
               + '<a href="#lamba">Lamba</a><a href="#secim">Seçim</a></nav>')
    out.append('<div class="ladder">')
    out.append('<a href="#simdi"><img class="px" src="img/room_now.png" alt="Şu anki oda" loading="lazy"><span><b>Şimdi</b> · bugünkü ilk basamak</span></a>')
    for lay in LAYOUTS:
        out.append('<a href="#%s"><img class="px" src="img/room_%s.png" alt="%s" loading="lazy"><span><b>%s</b> · %s</span></a>'
                   % (lay[0], lay[0], esc(lay[1]), lay[0], esc(lay[1])))
    out.append('</div>')
    out.append('<div class="legend"><span><span class="chip in">odada</span> fotoğraftaki aday</span>'
               '<span><span class="chip rec">öneri</span> fotoğraftakinin yerine önerdiğim</span></div>')

    out.append('<section class="layout" id="simdi"><div class="head"><span class="tag">ŞİMDİ</span><h2>Bugünkü oda</h2></div>'
               '<p class="sub">Yeni bir oyunun ilk basamakları: kirişli tahta tavan, terrakota zemin, çatlak sıva, şehir resmi, pirinç sarkıtlar. Karşılaştırma için.</p>'
               '<img class="px shot" src="img/room_now.png" alt="Bugünkü oda" loading="lazy"></section>')

    for code, title, sub, used, rec, note in LAYOUTS:
        out.append('<section class="layout" id="%s"><div class="head"><span class="tag">%s</span><h2>%s</h2></div>' % (code, code, esc(title)))
        out.append('<p class="sub">%s</p>' % esc(sub))
        out.append('<img class="px shot" src="img/room_%s.png" alt="%s odada" loading="lazy">' % (code, esc(title)))
        out.append('<p class="note">%s</p>' % esc(note))
        out.append('<div class="pieces">')
        for key, label in PIECES:
            out.append('<div class="piece"><h3>%s</h3><div class="cands">' % label)
            for n in range(4):
                fn = '%s_%s_%d.png' % (code, key, n)
                if not os.path.exists(os.path.join(SRC, fn)):
                    continue
                cls = []
                chips = ''
                if used[key] == n:
                    cls.append('in'); chips += '<span class="chip in">odada</span>'
                if rec.get(key) == n:
                    cls.append('rec'); chips += '<span class="chip rec">öneri</span>'
                kind = 'pic' if key == 'pic' else 'back' if key == 'back' else 'sq'
                out.append('<figure class="%s"><div class="tile"><img class="px %s" src="img/%s" alt="%s %s %d" loading="lazy"></div>'
                           '<figcaption>%s · %s %d %s</figcaption></figure>'
                           % (' '.join(cls), kind, fn, code, esc(label), n, code, esc(label.lower()), n, chips))
            out.append('</div></div>')
        out.append('</div></section>')

    out.append('<section class="layout" id="lamba"><div class="head"><span class="tag">LAMBA</span><h2>Yeni tezgâh lambası</h2></div>'
               '<p class="sub">Globe sarkıtın yerine: kademeli pirinç halkalar, oluklu cam, ince çubuk. Işığı çizime gömülü değil; odanın kendi ışığı '
               'camın ölçülen yerinden çıkıyor. Hepsi kulüp odasında (L5) asılı.</p><div class="lamps">')
    for n, label in LAMPS:
        out.append('<div class="lamp%s"><div class="pair"><div class="solo"><img class="px" src="img/lamp_%d.png" alt="Lamba %d" loading="lazy" style="width:100%%"></div>'
                   '<img class="px" src="img/lamp_room_%d.png" alt="Lamba %d odada" loading="lazy"></div>'
                   '<p><code>lamba %d</code> · %s%s</p></div>'
                   % (' rec' if n == LAMP_PICK else '', n, n, n, n, n, esc(label),
                      ' <span class="chip rec">öneri</span>' if n == LAMP_PICK else ''))
    out.append('</div><p class="note">Önerim <b>lamba 1</b>: tarifteki “pirinç halkalar, buzlu cam” sözüne en yakını ve beş odanın hepsinde sıcak kalıyor. '
               'Sarı cam (3) kulüpte güzel ama tropik odalarda turuncuya kayıyor.</p></section>')

    out.append('<section class="layout" id="secim"><div class="head"><span class="tag">SEÇİM</span><h2>Nasıl seçilir</h2></div><div class="ask">'
               '<p>Cevabı kısa yazmanız yeterli. Örnek: <code>L1 hepsi · L2 zemin 0 · L3 sağ duvar 3 · L4 hepsi · L5 hepsi · lamba 1</code></p>'
               '<p>Sizden bir karar daha gerekiyor: bu beş oda bugünkü merdivene nasıl girecek?</p><ol>'
               '<li><b>Yerine koy.</b> Beş düzen her yuvanın beş basamağı olur, bugünkü parçalar (yedinci tura kadar seçilenler dahil) satıştan kalkar.</li>'
               '<li><b>Araya ekle.</b> Bugünkü basamaklar kalır, yeni parçalar fiyatlarına göre merdivenin içine dizilir. Merdiven uzar, odanın havası karışık kalır.</li>'
               '</ol><p>Seçilen parçalar odaya girmeden önce son bir iş var. L2 ve L3 resimlerine çerçeve çizmek, ve tavan, zemin ve sağ duvar '
               'döşemelerindeki 1–5 piksellik kenar çizgisini kırpmak (fotoğraflar bu kırpılmış hâliyle çekildi).</p></div></section>')
    out.append('</div>')
    return '\n'.join(out)


if __name__ == '__main__':
    shots = sys.argv[1] if len(sys.argv) > 1 else None
    if not shots:
        raise SystemExit(__doc__)
    copy_shots(shots)
    with open(os.path.join(OUT, 'index.html'), 'w', encoding='utf-8') as f:
        f.write(page())
    print('wrote', os.path.join(OUT, 'index.html'), len(os.listdir(IMG)), 'images')
