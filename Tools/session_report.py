# -*- coding: utf-8 -*-
"""NELER YAPILDI - the 2026-08-25 vice round, all four parts on one page.

This is the OTHER kind of report. `vice_room_gen.py report` is a chooser: it lays every
generated take out with its measurements so one can be picked. This one answers a
different question - what changed, what shipped, what is still waiting on a decision -
and it covers the two parts that never went near PixelLab at all (the sign is struck by
hand, the shelf is code).

Every picture here already exists on disk and was made by a named tool; nothing is
re-generated, so this page is cheap to rebuild and cannot drift from what shipped.

    py -3 Tools/session_report.py      # writes Tools/vice_session_report.html
"""
import base64
import io
import os
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'vice_session_report.html')


def b64(path):
    with io.open(path, 'rb') as f:
        return base64.b64encode(f.read()).decode('ascii')


def fig(path, caption, cls='', width=None):
    """One picture. Missing files are announced rather than silently skipped - a preview
    that quietly drops a panel is how a reader concludes the work was not done."""
    p = path if os.path.isabs(path) else os.path.join(ROOT, path)
    if not os.path.exists(p):
        return ('<figure class="%s"><div class="shot missing">eksik: <code>%s</code>'
                '</div></figure>' % (cls, path))
    style = ' style="width:%dpx"' % width if width else ''
    return ('<figure class="%s"><div class="shot"><img alt="%s"%s '
            'src="data:image/png;base64,%s"></div><figcaption>%s</figcaption></figure>'
            % (cls, caption, style, b64(p), caption))


CSS = """
:root{
  --ink:#F2E8D5; --ink-dim:#C9BCA8; --ink-faint:#9C8F80;
  --ground:#1A1023; --panel:#241830; --panel-hi:#362447;
  --line:#4A3160; --rose:#E84DA6; --petrol:#3BC8BE; --brass:#E8A33D; --lime:#6FCC4B;
}
*{box-sizing:border-box}
body{margin:0; background:var(--ground); color:var(--ink);
  font-family:"IBM Plex Sans","Segoe UI",system-ui,sans-serif; font-size:15px;
  line-height:1.65; -webkit-font-smoothing:antialiased}
.wrap{max-width:1060px; margin:0 auto; padding:56px 26px 96px}
.eyebrow{margin:0; font-family:Silkscreen,"IBM Plex Mono",monospace; font-size:11px;
  letter-spacing:.18em; text-transform:uppercase; color:var(--rose)}
h1{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400;
  font-size:clamp(22px,3.6vw,34px); line-height:1.3; margin:16px 0 0; text-wrap:balance}
h2{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400; font-size:20px;
  margin:66px 0 0; padding-top:26px; border-top:2px solid var(--rose); color:var(--rose)}
h3{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400; font-size:15px;
  margin:34px 0 0; color:var(--ink)}
p{max-width:70ch}
.lede{color:var(--ink-dim); margin:18px 0 0}
b{color:var(--ink)}
code{font-family:"IBM Plex Mono",monospace; font-size:.88em; color:var(--brass)}

table.state{width:100%; border-collapse:collapse; margin:26px 0 0;
  font-family:"IBM Plex Mono",monospace; font-size:12.5px}
table.state th{text-align:left; font-weight:400; letter-spacing:.08em;
  text-transform:uppercase; font-size:10.5px; color:var(--ink-faint);
  border-bottom:1px solid var(--line); padding:8px 10px}
table.state td{border-bottom:1px solid var(--line); padding:10px; vertical-align:top;
  color:var(--ink-dim)}
table.state td:first-child{color:var(--ink)}
.pill{display:inline-block; padding:2px 8px; border:1px solid; font-size:10.5px;
  letter-spacing:.08em; text-transform:uppercase; white-space:nowrap}
.pill.done{color:var(--lime); border-color:#2A5926; background:#16331B}
.pill.wait{color:var(--brass); border-color:#8F5A1E; background:#3A2410}
.pill.block{color:var(--rose); border-color:#5C1B45; background:#2E0F22}

figure{margin:22px 0 0; display:grid; gap:8px}
.shot{background:var(--panel); border:1px solid var(--line); padding:10px; overflow-x:auto}
.shot img{display:block; max-width:100%; height:auto; image-rendering:pixelated}
.shot.missing{font-family:"IBM Plex Mono",monospace; font-size:12px; color:var(--rose)}
.alpha .shot{background:
  repeating-conic-gradient(var(--panel-hi) 0 25%, var(--panel) 0 50%) 0 0/16px 16px}
figcaption{font-family:"IBM Plex Mono",monospace; font-size:11.5px; color:var(--ink-faint);
  max-width:78ch}
.pair{display:grid; grid-template-columns:repeat(auto-fit,minmax(320px,1fr)); gap:18px}

dl.nums{display:grid; grid-template-columns:repeat(auto-fit,minmax(170px,1fr));
  gap:12px 22px; margin:22px 0 0; padding:14px 16px; background:var(--panel);
  border:1px solid var(--line)}
dl.nums > div{display:grid; gap:3px}
dt{font-family:"IBM Plex Mono",monospace; font-size:10.5px; letter-spacing:.1em;
  text-transform:uppercase; color:var(--ink-faint)}
dd{margin:0; font-family:"IBM Plex Mono",monospace; font-size:13px;
  font-variant-numeric:tabular-nums; color:var(--ink)}
dd.good{color:var(--petrol)} dd.bad{color:var(--rose)}
.note{border-left:2px solid var(--brass); padding:2px 0 2px 16px; margin:24px 0 0;
  color:var(--ink-dim)}
.note.stop{border-color:var(--rose)}
footer{margin-top:60px; padding-top:24px; border-top:1px solid var(--line);
  color:var(--ink-faint); font-size:13px}
"""


def html():
    p, a = [], None
    p = []
    a = p.append
    a('<title>Neler yapildi &mdash; vice turu</title>')
    a('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
      'family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:wght@400;600&'
      'family=Silkscreen&display=swap">')
    a('<style>%s</style>' % CSS)
    a('<div class="wrap">')
    a('<p class="eyebrow">Last Call &middot; %s</p>' % time.strftime('%Y-%m-%d'))
    a('<h1>Neler yapildi</h1>')
    a('<p class="lede">Dort istegin dordu de yapildi. <b>Ikisi oyuna girdi</b> (yazi ve '
      'raf &mdash; ikisi de kod/cizim, uretim degil), <b>ikisi seni bekliyor</b> (kasa ve '
      'oda parcalari &mdash; uretilmis sanat, ve bu projede uretilmis sanat once '
      'gosterilir sonra oyuna girer). Asagida her biri icin ONCE/SONRA var.</p>')

    a('<table class="state">')
    a('<tr><th>istek</th><th>durum</th><th>nerede</th></tr>')
    for what, pill, cls, where in (
        ('1 &middot; Kasa gorseli (2.5D, 30&deg;, vice)',
         '12 aday uretildi', 'wait',
         'Uc makine &times; dort aday. <b>Oyuna konmadi</b> &mdash; secmeni bekliyor. '
         '<code>staging/vice_room/till_*.png</code>'),
        ('2 &middot; Open bar yazisi (miami vice font)',
         'oyunda', 'done',
         'Yeni <code>vice</code> eli varsayilan. <code>Items/sign_open.png</code> 181&times;33'),
        ('3 &middot; Raftaki alkoller kuculmesin',
         'oyunda + test', 'done',
         'Boy sabit 62, aralik 1 px&rsquo;e kadar iniyor. EditMode 369/369, PlayMode 7/7'),
        ('4 &middot; Odaya eklenebilecek gorseller',
         '18 aday uretildi', 'wait',
         'Iki oda, uc tezgah, dort dosheme parcasi. <b>Hicbiri oyuna konmadi</b>'),
        ('&nbsp;&nbsp;&nbsp;&nbsp;&rdsh; tezgahlar',
         'engel var', 'block',
         'Ucunun de tablasi ince &rarr; kepenk sanati artik acikligi ortmuyor. Asagida'),
    ):
        a('<tr><td>%s</td><td><span class="pill %s">%s</span></td><td>%s</td></tr>'
          % (what, cls, pill, where))
    a('</table>')

    # ── 1 · the till ────────────────────────────────────────────────────────
    a('<h2>1 &middot; Kasa &mdash; evet, uretildi</h2>')
    a('<p class="lede">Uc makine, her birinden dort aday: <b>12 gorsel</b>. Acinin '
      'prompt&rsquo;taki cumlesi ucunde de <b>harfi harfine ayni</b> &mdash; degismesi '
      'gereken sey makinenin kendisiydi, bakildigi yer degil. Tezgahin kendisi de '
      '&ldquo;sadece kamera acisi ve goz yuksekligi&rdquo; etiketiyle referans olarak '
      'gonderildi; bu, senin &ldquo;tezgahi referans al&rdquo; talimatinin modelin yanlis '
      'okuyamayacagi hali.</p>')
    a(fig('Tools/vice_till_v2.png',
          'Ikinci tur: 12 yeni aday, 3&times;. Her adin yanindaki sayi <b>yan kenar '
          'kaymasi</b> &mdash; ne kadar kucukse o kadar karsiya bakiyor. Sagda '
          'karsilastirma icin oyundaki kasa', 'alpha'))
    a('<p class="note">Ilk tur %49&ndash;68 arasindaydi ve yazar hakliydi: '
      '&ldquo;30 derece&rdquo; kameranin ne kadar YUKARIDA oldugunu soyluyor, nesnenin ne '
      'kadar DONDUGUNU degil &mdash; ve jeneratorun bosluga koydugu varsayilan cevap tam '
      'izometri oldu. Ikisi artik ayri yaziliyor ve yanlis cevap adiyla yasaklandi: '
      'yukseklik yerinde kaldi, donus neredeyse kareye dondu. Sonuc: <b>neon %25&ndash;26</b> '
      '(oyundaki kasa %20), <b>brass %39&ndash;40</b>, <b>marble %58&ndash;67</b> &mdash; '
      'marble hala fazla donuk, brief’i tutan neon ve brass.</p>')
    a('<h3>Ayni yerde, eski ve yeni</h3>')
    a('<p class="lede">Ustte oyundaki kasa, altta yeni aday &mdash; ikisi de gercek '
      '<b>57 birimlik</b> ayak izinde, ayni tezgahin ayni noktasinda. Eskisi tezgaha '
      'yapistirilmis duz bir kutu; yenisinin ust yuzeyi var, o yuzden tezgahin '
      '<i>uzerinde duruyor</i>.</p>')
    a(fig('Tools/till_in_place.png',
          'Ust: <code>register2.png</code> (oyunda). Alt: <code>till_brass #1</code>. '
          '3&times;, sahnenin kendi kompozisyonundan kirpildi'))
    a('<dl class="nums">')
    a('<div><dt>uretilen</dt><dd>12 aday</dd></div>')
    a('<div><dt>olcu</dt><dd>112&times;100 &rarr; ~90&times;93</dd></div>')
    a('<div><dt>yan kenar kaymasi (eski)</dt><dd class="bad">%20</dd></div>')
    a('<div><dt>yan kenar kaymasi (yeni)</dt><dd class="good">%49&ndash;68</dd></div>')
    a('</dl>')
    a('<p class="note">Bu sayi <b>olculdu, bakilmadi</b>: siluetin en sol pikseli asagi '
      'inerken yana ne kadar yuruyor. Duz cizilmis bir kutuda yan kenar diktir. Ilk '
      'yazdigim metrik yanlisti &mdash; &ldquo;ustten daralma&rdquo; olcuyordu ve eski '
      'kasa da daraliyor (ekran kafasi dar), yani iki durumu ayirt edemiyordu; sana '
      'ulasmadan degistirdim.</p>')

    # ── 2 · the sign ────────────────────────────────────────────────────────
    a('<h2>2 &middot; Open bar &mdash; oyunda</h2>')
    a('<p class="lede">Onceki dort el de <b>ayni eldi</b>: yuvarlak uclu keceli bir '
      'kalemin surtulmesi, yani el yazisi. Istenen o elin besinci surumu degil, obur tur '
      'harfti. Yeni el <code>vice</code> <b>kalem kullanmiyor</b> &mdash; harfleri dolu '
      'sekillerden kuruyor (dikdortgen, halka, kama), o yuzden her govde paralel kenarli '
      've her uc <b>duz kesik</b>.</p>')
    a(fig('Tools/sign_before_after.png',
          'Merdanenin kendi grisinde, 5&times;. Ucuncu satir <code>vice_cyan</code>: '
          'secilmedi, kontak foyunde duruyor'))
    a('<p class="note">Iki karar sayidan cikti, zevkten degil. <b>Buyuk harf:</b> 34 px '
      'tavanda kaseleri ilk yiyen sey katlardir; kucuk harf boyunu x-yuksekligi, kamet ve '
      'alt uzanti arasinda bolusturur, buyuk harf hepsini tek banda harcar &mdash; yani '
      'O&rsquo;nun ici, isaret bir piksel bile buyumeden yari yariya daha acik. '
      '<b>Iki kalinlik</b> (dikey 5, yatay 3): tek kalinlikta B kapaniyordu, cunku bir '
      'kamet icine yigilmis iki kasenin her birine astar ve kontur iki yandan 2 px '
      'giriyor ve 29 satirda o kadar yer yok. Yataylar incelince kase 8 satir kaliyor, '
      'dordu hayatta kaliyor.</p>')

    # ── 3 · the shelf ───────────────────────────────────────────────────────
    a('<h2>3 &middot; Raf &mdash; oyunda, olculdu</h2>')
    a('<p class="lede">Eski kod <b>en genis sise esit yuvasina sigana kadar butun rafin '
      'boyunu dusuruyordu</b> &mdash; yani genis omuzlu bir rom satin almak bardaki diger '
      'her siseyi sessizce kucultuyordu. Artik boy sabit; esneyen sey <b>aralik</b>, '
      'doluyken 1 px&rsquo;e kadar.</p>')
    a(fig('Tools/shelf_before_after.png',
          'Ayni 42 sise, iki algoritma, oyundaki tezgahin uzerine cizildi. 2&times;. '
          'Bu bir DIYAGRAM: yeninin gercek ekran goruntusu asagida, eskinin olabilmesi '
          'icin kodu geri almak gerekirdi, o yuzden iki taraf da ayni sekilde cizildi'))
    a('<dl class="nums">')
    a('<div><dt>sise boyu (once)</dt><dd class="bad">57.9 px</dd></div>')
    a('<div><dt>sise boyu (sonra)</dt><dd class="good">62.0 px &mdash; sabit</dd></div>')
    a('<div><dt>cizilmeyen (once)</dt><dd class="bad">6 sise</dd></div>')
    a('<div><dt>cizilmeyen (sonra)</dt><dd class="good">0</dd></div>')
    a('<div><dt>yuva sayisi</dt><dd>36 &rarr; 48</dd></div>')
    a('<div><dt>testler</dt><dd class="good">369/369 &middot; 7/7</dd></div>')
    a('</dl>')
    a('<h3>Ve oyunun kendisinde</h3>')
    a('<p class="lede">Diyagram degil: calisan sahnede mahzen 42 siseyle dolduruldu ve '
      'geri okundu &mdash; <b>hepsi 62.00</b>, en dar aralik <b>2.75 px</b>, gozunun '
      'disina tasan <b>0</b>.</p>')
    a(fig('Tools/cellar_in_play.png',
          'Play modunda, 1280&times;720. 42 sise, gozde yediser, boylari ayni'))

    # ── 4 · the room ────────────────────────────────────────────────────────
    a('<h2>4 &middot; Oda parcalari &mdash; 18 aday, hicbiri oyunda degil</h2>')
    a('<p class="lede">Iki oda plakasi, uc tezgah, dort dosheme parcasi (dordunden '
      'dorder aday). Hepsinin detayli olcumu ve prompt&rsquo;u ayri raporda: '
      '<code>Tools/vice_room_report.html</code>. Burada sadece ne cikti ve neyin engeli '
      'var.</p>')
    a('<h3>Dosheme</h3>')
    a(fig('Tools/vice_dressing_contact.png',
          'Neon flamingo, saksi palmiye, deco ayna, jukebox &mdash; 3&times;. Ucu '
          '<code>fixtures.json</code>&rsquo;da ZATEN VAR OLAN bir yuvaya giriyor; '
          'jukebox&rsquo;in yuvasi yok, secilirse acilmasi gerek', 'alpha'))
    a('<p class="note">Palmiyeler <b>ikinci cekim</b>. Ilk turda prompt yaprak icin uc '
      'yesil rampa adiyla yesil istedi, dort adayin dordu de magenta-turkuaz geldi: '
      '<code>palette_miami.png</code> icinde Lime yok, cunku o plaka &ldquo;Miami '
      'tonlari&rdquo; icin kesilmisti &mdash; ve <b>plaka metni yeniyor</b>. Ortak '
      'plakayi genisletmek yanlis olurdu (ondan sonraki her sahne cagrisini sessizce '
      'yeniden renklendirirdi), o yuzden bu tek varliga kendi renk referansini verdim: '
      'odada zaten duran <code>fx_monstera</code>.</p>')
    a(fig('Tools/vice_plants_all.png',
          'Bes bitki, bes kap, 3&times;: palmiye (terracotta saksi), kemanyapragi (krem '
          'vazo), pasakilici (koyu kare saksi), sarmasik (pirinc ayakli), agav (genis '
          'kase). Hepsi ayni renk referansiyla &mdash; odanin kendi monsterasi', 'alpha'))
    a(fig('Tools/plants_in_room.png',
          'Secilen beslisi odada, <code>fixtures.json</code>’daki kendi yuva '
          'koordinatlarinda. <b>Bunlar oyuna girdi</b>: plant_left uc basamak '
          '(palmiye &rarr; kemanyapragi &rarr; sarmasik), plant_right iki '
          '(pasakilici &rarr; agav). Eski fern ve monstera listeden kaldirildi'))
    a(fig('Tools/vice_palm_contact.png',
          'Yeniden cekilen dort palmiye + sagda odada duran iki bitki, 4&times;. '
          'Yeni yesil monstera&rsquo;nin yesilinden parlak, saksi da odanin koyu '
          'saksilarina karsi terracotta &mdash; &ldquo;taze bitki&rdquo; mi '
          '&ldquo;uyumsuz&rdquo; mu, senin karar', 'alpha'))
    a('<h3>Tezgahlar &mdash; ve neden hicbiri dogrudan giremiyor</h3>')
    a(fig('Tools/vice_counters_in_scene.png',
          'Uc tezgah, sahnede, oyundaki oda ve kasayla. Ucunun de TABLASI ince: raf '
          'acikligi hemen tezgah ustunde basliyor'))
    a('<table class="state">')
    a('<tr><th></th><th>acilis satiri</th><th>aciklik yuksekligi</th><th>gozler</th>'
      '<th>en dar goz</th></tr>')
    for name, top, oh, cent, wd, cls in (
        ('oyundaki', '65', '<b>176</b>', '119 / 319 / 517', '173', ''),
        ('counter_vice', '32', '209', '114 / 319 / 525', '171', 'bad'),
        ('counter_sunset', '38', '203', '103 / 317 / 538', '143', 'bad'),
        ('counter_chrome', '44', '197', '102 / 319 / 536', '182', 'bad'),
    ):
        a('<tr><td>%s</td><td>%s</td><td>%s</td><td>%s</td><td>%s</td></tr>'
          % (name, top, oh, cent, wd))
    a('</table>')
    a('<p class="note stop"><b>Engel bu:</b> <code>counter_shutter.png</code> '
      '592&times;176 ve tam olarak oyundaki 65..241 araligini kaplasin diye cizilmis. '
      'Uc take&rsquo;in de tablasi daha ince oldugu icin aciklik 197&ndash;209 satir '
      '&mdash; kepenk artik ortmuyor. Bu bir sabit degisikligi degil, <b>yeni sanat</b>. '
      'Ayrica chrome&rsquo;un plakasi 16 satir kisa geldi, alt rafi kirpmanin disinda '
      'kaldi. <b>Onerim:</b> tezgahi secmeyelim &mdash; tabla kalinligini satir olarak '
      'yazip bir tur daha cekelim (40 generation). Kasa ve dosheme secimlerin bunu '
      'beklemek zorunda degil.</p>')
    a('<p class="lede">Olcen kodun kendisi <b>oyundaki tezgahin uzerinde dogrulandi</b>: '
      'ayni yontem oradan 119/319/517 ve acilis 65 okuyor, yani kodun kendi 120/319/517 '
      've 65&rsquo;ini. Bir olcum aracinin, hic olculmemis sanat hakkinda inanilmayi '
      'boyle hak ediyor. Her take&rsquo;in altinda, secilirse gereken '
      '<code>DiegeticStage</code> sabit blogu yapistirilacak halde duruyor.</p>')

    a('<footer>Uretim <code>Tools/vice_room_gen.py</code> &middot; yazi '
      '<code>Tools/open_sign_gen.py</code> &middot; raf diyagrami '
      '<code>Tools/shelf_compare.py</code> &middot; bu sayfa '
      '<code>Tools/session_report.py</code>. Her PixelLab cagrisi prompt&rsquo;u ve '
      'seed&rsquo;i ile <code>Tools/AssetPipeline/generation_log.jsonl</code> icine '
      'yazildi. Uretilmis hicbir gorsel <code>Assets/</code> altina kopyalanmadi.'
      '</footer>')
    a('</div>')
    return '\n'.join(p)


if __name__ == '__main__':
    io.open(OUT, 'w', encoding='utf-8').write(html())
    print('%s  %d KB' % (os.path.relpath(OUT, ROOT), os.path.getsize(OUT) // 1024))
