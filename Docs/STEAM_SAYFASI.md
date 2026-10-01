# STEAM SAYFASI — Malibu Club: Cocktail Bar Simulator

*2026-10-01. Yazar: "Oyunum için en baştan steam sayfası elementleri ve görselleri oluşturmanı istiyorum. Ana
karakterimiz roxy olacak. ... pazar araştırması tasarım araştırması yap ... detaylı görsel ve giflerden yararlanılmış
steam about kısmı ve steam mağaza ve kütüphane görselleri hazırla. Oyunun Malibu Club yazı logosu aynen kalacak."*

Bu belge mağaza sayfasının tamamını tek yerde tutar: araştırma ve konumlandırma, metinler, etiketler, görsellerin
listesi ve Steamworks'e nasıl yükleneceği. Steamworks'teki başarım/istatistik işleri `Docs/STEAMWORKS.md`'de.

**Üretim (2026-10-01, üçüncü tur, geçerli):** yazar: "Görseller bütün bir kompozisyon olmalı, pixel art olarak
düşün ... logo ve yazının bulunacağı konum, görsellerin bulunacağı konum, tüm kütüphane ve mağaza elementlerinin
konumları ... benzer türdeki oyunların tasarım kurallarından ders al ... Vice ve Miami sunset temasında." Yazar
ayrıca kendi `steam_kit`'inden örnekler verdi (Steam arka planı, iki çerçeveli panel, ABOUT THE GAME başlığı, neon
ayraç; kopyaları `Tools/steam_page/out/refs/`, git dışında) ve "Steam backgroundunu tekrardan yapmana gerek yok" dedi.

Bütün mağaza ve kütüphane görselleri artık **tek bir katmanlı ana kompozisyondan** dizilir (`Tools/steam_page/`):

| Katman | Kaynak | Ne yapar |
|---|---|---|
| Plaka | `plate.py` | Yazarın Steam arka planının kendisi: ana resmi (237×133, 8 px ızgara) ölçüldü — bantlar ve iki satırlık dikişleri, güneş, yansımalar, deniz, palmiyeler; şehir aynı elle yeniden çizilir. Her görselin kendi ızgarasında yeniden çizilir, logo için üstüne koyu gece bantları eklenir. |
| Roxy | `roxy_layer.py` | Tek kesit, her görselde aynı: Nano Banana Pro adayı `pixelsnap.py` ile gerçek ızgarasına oturtuldu (479×266, 75 mürekkep), GrabCut ile ayrıldı, raf artıkları, sivri pikseller ve gözlerdeki gri gürültü temizlendi, yaka bir ton açıldı. Eli tezgâhın mermerinde. |
| Tezgâh | `keyart.counter` | `steam_kit` panelindeki lacivert-siyah mermer (ClubBlue damarlar), panellerin magenta neon kenarı; uzun önlerde 12 texel MiMo yivleri ve kitin neon çizgisi. |
| Nesneler | `props.py` | En çok üç tür işareti: tezgâh, tequila sunrise highball, parmak uçlarının altında hasta kimliği (oyunun kancası). |
| Logo | `logo_render.py`, `logo_native.py` | **Asla yeniden örneklenmez.** Gönderilen set 1:1 (çoğunda x1, 560 px); küçük kapsül için tabelanın kendi kurucusu (`Tools/title_sign/build_logo.py`) x4 haritasından türetilmiş ana çizimlerle 388 px'te **yeniden çizer** — aynı 7 alfa düzeyi, aynı 14 mürekkep. |

Her görsel kendi ana tuvalinde çizilir (çıktı / yoğunluk, tam sayı), Roxy'nin gün batımına değen dış kenarına tek
koyu texel konur (harmanlama yok), tuval tek palete indirgenir (ΔE<4 eşleri birleşir), NEAREST ile büyütülür; logo en
son, tam boyda konur. Yoğunluk 2 (ya da 4): Steam'in yarım boy kopyaları tam piksele düşer; ana kapsül ve hero 3,
Roxy büyük dursun diye.

```
python3 Tools/steam_page/keyart.py      # 13 görsel + client_icon.ico + yerleşim paftası -> out/keyart/
python3 Tools/steam_page/about.py       # About GIF'leri + steam_kit başlıkları          -> out/about/
```

**Yerleşim** (`keyart.LAYOUTS`; kutular çıktı tuvalinin yüzdesi, pafta: `out/keyart/_layout/LAYOUT_SHEET.png`):

| Görsel | Boyut | Yoğ. | Logo | Roxy | Bar üstü |
|---|---|---|---|---|---|
| `main_capsule` | 1232×706 | 3× | x 7–53% · y 4–34% (560 px) | x 41–89% · y 2–88% | y 86% |
| `header_capsule` | 920×430 | 2× | x 3–64% · y 4–53% (560 px) | x 57–99% · y 0–93% | y 91% |
| `small_capsule` | 462×174 | 2× | x 8–92% · y 2–86% (388 px) | — (yalnızca logo) | — |
| `vertical_capsule` | 748×896 | 2× | x 13–87% · y 5–29% (560 px) | x 22–75% · y 35–80% | y 79% |
| `library_capsule` | 600×900 | 2× | x 3–97% · y 4–28% (560 px) | x 22–87% · y 36–82% | y 80% |
| `library_hero` | 3840×1240 | 3× | — (Steam kendi logosunu koyar) | x 42–57% · y 29–78% | y 78% |
| `event_cover` | 800×450 | 2× | — (Steam adı ve ikonu yanına koyar) | x 1–50% · y 5–96% | y 95% |
| `event_header` | 1920×622 | 2× | x 3–32% · y 6–40% (560 px) | x 63–84% · y 31–96% | y 95% |

Kurallar (araştırma + dört yargıçlı eleştiri turu, 2026-10-01): logo en koyu sakin bantta, arkasından hiçbir kule,
güneş ya da palmiye geçmez; güneş Roxy'nin arkasında; Roxy izleyiciye bakar, kadeh yukarıda; küçük kapsül yalnızca
logo (120×45'te "MALIBU CLUB" okunur); dikeyler üst üste dizilir (logo, Roxy, bar); kütüphane kapsülünün alt %10'u
istemci bindirmelerine bırakılır; hero'da yüz 860×380 güvenli alanın içinde, sol alt Steam logosu için sakin, alt
~%18 oynat çubuğunun altında düz; ana kapsülün dış %5'i karusel kenarına bırakılır. **Kütüphane logosu:** Steamworks'te
BottomLeft, genişlik ≤%38, yükseklik ≤%30 olarak sabitle (Roxy'nin koluna değmesin, oynat çubuğunun üstünde kalsın).
**Sayfa arka planı:** yazarın kendi dosyası olduğu gibi kalır (Steam 1438×810 ister; yazarın asıl dosyası `steam_kit`'te).

**Önceki turlar** (yerinde duran ama artık kullanılmayan araçlar): koddan çizilen ilk tur (`build_capsules.py`,
`scene.py`, `compose.py`) ve Nano Banana'nın doğrudan kapsül boyadığı ikinci tur (`gen_nano.py`, `ship_picks.py`).
`gen_nano.py` hâlâ Roxy kaynağını üretmek için gerekli; anahtar `GEMINI_API_KEY` ortam değişkeninden okunur, hiçbir
dosyaya yazılmaz.

Bağımlılık: `pip install pillow numpy opencv-python-headless`. Bütün PNG'lerin LFS'ten inmiş olması gerekir.

---

## 1 · Pazar araştırması (özet)

| Oyun | Neden kıyas | Not |
|---|---|---|
| VA-11 Hall-A | Piksel bar, içki karıştırma ve sohbet | $14.99, ~38.7k yorumun %97'si olumlu. Alt başlık türü söylüyor. |
| Papers, Please | Kimlik kontrolü, sahte belge, kapıdan çevirme | %97, ~39.7k yorum. KICK mekaniğimizin atası. |
| Coffee Talk | Piksel "demle ve konuş" | $12.99, ~%95 olumlu. |
| Tavern Talk / Tavern Master | Hafif VN + içki / 2D işletme | $17.99 / %87–91 olumlu. |
| Brewpub Simulator | 3D bar işletmesi | %57 (karışık). Kancası olmayan bar simülatörü kayboluyor. |
| Mixed Spirits, Barman Simulator, Open Bar | 2025–26 bar simülatörleri | Neredeyse hepsi 3D birinci şahıs. **2D piksel neon bar boşluğu açık.** |
| Supermarket Sim, TCG Card Shop Sim | Simülatör dalgası | Yayıncı (streamer) kaynaklı büyüme. |
| Dave the Diver, Potion Craft | Piksel işletme | Büyük karakter + sade arka plan + temiz logo alanı. |

- **Pazar (Zukowski, 2025 özeti):** Simülasyon ikinci büyük tür (1.048 çıkışın 43'ü 1.000+ yorum, %4.1). Bu türü
  Supermarket Simulator kalıbındaki 3D "iş simülatörleri" ele geçirmiş durumda. Anlatı türü birinci (51). Önerilen
  konum: **anlatı + simülatör melezi**. VA-11 Hall-A ve Papers, Please okuyucusu bu tür oyunlar için geliyor.
- **İstek listesi motorları:**
  - **Next Fest:** ~2k istek listesinin altında etkisi zayıf. Fest istek listelerinin %68–88'i demoyu hiç oynamayan
    kişilerden geliyor, yani kapsülü ve fragmanı görenlerden.
  - **Yayıncılar:** kimlik kontrolü, KICK ve sahte kartlar kısa klip için ideal anlar.
- **Fiyat bandı:** $14.99–17.99.
- **Kaynaklar** (WebFetch proxy'de engelliydi; rakamlar arama özetlerinden alındı, yaklaşıktır):
  - howtomarketagame.com: "What the hell happened in 2025"; Next Fest kıyasları
  - partner.steamgames.com/doc/store/assets (+ /rules, /eventassets, /community)
  - presskit.gg kapsül ve açıklama rehberleri; steampageanalyzer.com
  - gamedeveloper.com: kapsül kuralları, GIF desteği

## 2 · Tasarım araştırması → kararlar

| İlke (araştırma) | Bu sayfada nasıl uygulandı |
|---|---|
| Tek odak, göz teması: kapsüllerin yalnız ~%20'sinde okunabilir bir yüz var, o yüzden yüz öne çıkarır | Her kapsülde Roxy önde ve izleyiciye bakıyor (oyunun `idle` karesi). |
| Kızıl saç ve bordo tulum mor zeminde kaybolur | Güneş her zaman Roxy'nin arkasında; gün batımına değen dış kenarında tek koyu Magenta[0] texel, gece bandına değen saçta Magenta[1]. |
| Jenerik synthwave (ızgara, güneş, palmiye) "müzik klibi" gibi okunur | Karede her zaman **tezgâh, kokteyl ve kimlik kartı** var; tür ilk bakışta okunuyor. |
| Logo 120×45'te okunmalı | Küçük kapsülde logo genişliğin %84'ü, tamamen gece bandında; gün batımı yalnızca altta ince bir şerit. |
| Kapsülde yalnız sanat, ad ve resmî alt başlık | Hiçbir kapsülde slogan, puan ya da ödül yazısı yok. "COCKTAIL BAR SIMULATOR" logonun parçası. |
| Piksel sanat yalnızca tam katlarla ve NEAREST ile büyütülür | Her görselde tek ızgara: plaka, tezgâh, Roxy ve nesneler aynı yoğunlukta (2 ya da 3). Logo hiç yeniden örneklenmez. |
| Palet disiplini | Plaka yazarın arka planının mürekkepleri, tezgâh ve nesneler `UITheme` rampaları; her görsel son adımda tek palete indirgenir. |

**Logo:** `Assets/Resources/Logo/logo_*_map.bytes` haritalarından, oyunun `TitleSignLight` "Lit" kıyafetiyle **birebir**
boyanır (`Tools/steam_page/logo_render.py`). Harfler hiç yeniden çizilmedi ve hiçbir set yeniden örneklenmez (yukarıda).

## 3 · Kısa açıklama (≤300 karakter)

**EN (birincil):**
> A neon cocktail-bar tycoon on a Miami beach. Nobody tells you what they want: read the ID, pour it right, shake,
> stir and pull a proper pint, and spot the kid with a borrowed licence before you serve them. Roxy hands you the bar.
> Keep it alive.

**TR:**
> Miami sahilinde neon bir kokteyl bar işletmesi. Kimse ne istediğini söylemez: kimliği oku, doğru dök, çalkala,
> karıştır, düzgün bir bira çek ve ödünç kimlikli çocuğu servis etmeden yakala. Roxy barı sana bırakıyor. Ayakta tut.

## 4 · Etiketler (öncelik sırasıyla; ilk 5 en çok ağırlık taşır)

1. Simulation
2. Management
3. Pixel Graphics
4. Crafting
5. Time Management
6. 2D
7. Retro
8. Cozy
9. Economy
10. Casual
11. Colorful
12. Atmospheric
13. Singleplayer
14. Cute
15. Stylized
16. Choices Matter
17. Narrative
18. Resource Management
19. Building
20. Relaxing

"Bartender" Steam'de etiket olarak yoksa yerine Crafting kullanılır. Etiket sihirbazında adları teyit et.
Kaçınılacaklar:
- **Cyberpunk:** yanıltıcı.
- **Female Protagonist:** Roxy oyuncu karakteri değil.
- **Story Rich / Visual Novel:** hikâye misafiri bugün sahnede açılmadıkça (`GameBootstrap.storyInPlay`) iddia edilmesin.

## 5 · About bölümü

Metin: `Tools/steam_page/copy/about_en.bbcode` (birincil) ve `about_tr.bbcode` (Türkçe sayfa için). Akış:
**Roxy → kart → kokteyl → musluk → kalabalık → gece → ev → özellik listesi**. Her bölüm bir neon başlık bandı, bir
GIF ve en fazla iki kısa paragraftan oluşur. Roxy'nin balonundaki söz oyunun kendi turundan (`tour.json`).

**Metindeki her sayı veriden doğrulandı:** 54 tarif (`recipes.json`), 41 şişe kartı (`base_bar.json`), 42 yüz
(`Resources/Patron`), 22 yuvada 91 dekor (`fixtures.json`), 18 iş (`quests.json`), 48 başarım
(`achievements.json`), 29 dil (28 çeviri + İngilizce).

Her GIF yazarın `steam_kit` paneline oturur (17 texel oluk, koyu-altın-koyu kenar, kitin neon çizgisi; 720×442),
zemini key art'ın kendi plakası ve tezgâhıdır; bütün figürler 2 px ızgarada. Bölüm başlıkları yazarın "ABOUT THE
GAME" başlığının kardeşleri: aynı çerçeve, ikon kutusu ve neon çizgi, şerit aynı sahneden, Malibu Arcade 16 px, altı
kare 420 ms, pencereler yanıp söner (`kit.header2`). Sayfa yazarın kendi `header_about.gif`'iyle açılır.

| Dosya | Boyut | İçerik |
|---|---|---|
| `header_about.gif` | 1440×176 | Yazarın kendi başlığı (`steam_kit`), olduğu gibi. |
| `banner_*.gif` | 1440×176 | Sekiz bölüm başlığı, 6 kare, her biri ~20 KB. |
| `01_meet_roxy.gif` | 720×442 | Roxy yürüyerek girer, döner ve turun ilk sözünü yazar. |
| `02_read_the_card.gif` | 720×442 | Kart yükselir, sipariş okunur (ORDER TAKEN). İkinci misafir 19 yaşında: UNDER 20 – KICK. |
| `03_shake_and_pour.gif` | 720×442 | Shaker çalkalanır, MIX dolar, coupe'a dökülür. PERFECT: altın ve magenta parçacıklar. |
| `04_pull_a_pint.gif` | 720×442 | Bira dolar, köpük banda oturur: GOOD PINT, HEAD 14%. |
| `05_the_crowd.gif` | 720×442 | Beş misafir kendi klipleriyle içer, sevinir, bozulur. |
| `06_one_night.gif` | 720×442 | 18:00 → 02:00 arasında plakanın güneşi batar, kasa dolar, beş yıldız yanar. |
| `07_build_the_house.gif` | 720×442 | Dekor tek tek yerine oturur, COMFORT elmasları dolar. |

En büyük GIF ~0.7 MB, toplam ~3 MB. Valve'ın ~15 MB'lık sayfa sınırının çok altında. Steam açıklama sütunu GIF'leri
sütun genişliğine sığdırır; 720 piksellik panel 1200 piksellik yeni sayfada küçültülmeden de sığar.

**Steam'e yükleme:** Steamworks → Store Page Admin → Description → *Upload images*. Dosyalar
`{STEAM_APP_IMAGE}/extras/<ad>` olur; BBCode bu yolları kullanıyor, dosya adlarını değiştirme.

## 6 · Mağaza ve kütüphane görselleri (`Tools/steam_page/out/keyart/`)

| Steam yuvası | Dosya | Boyut |
|---|---|---|
| Header capsule | `header_capsule.png` | 920×430 |
| Small capsule | `small_capsule.png` | 462×174 |
| Main capsule | `main_capsule.png` | 1232×706 |
| Vertical capsule | `vertical_capsule.png` | 748×896 |
| Page background | yazarın `steam_kit` dosyası | 1438×810 |
| Library capsule | `library_capsule.png` | 600×900 |
| Library hero | `library_hero.png` | 3840×1240 (logo YOK) |
| Library logo | `library_logo.png` | 1280×428, saydam, x2 seti 1:1 + 8 px kenar |
| Library header | `library_header.png` | 920×430 (header ile aynı) |
| Event cover | `event_cover.png` | 800×450 (sağ yarı etkinlik yazısına boş) |
| Event header | `event_header.png` | 1920×622 |
| Community icon | `community_icon.png` | 184×184 (JPG'ye q≥95 ile çevrilerek yüklenir) |
| Client / shortcut icon | `client_icon.png`, `client_icon.ico` | 256×256; .ico 16/24/32/48/256 |

PNG olarak teslim edilir: piksel sanatı JPG'de bozulur. Steam'in JPG istediği yuvalara (community icon) yüklemeden
hemen önce en yüksek kaliteyle çevrilir.

## 7 · Ekran görüntüleri: oyunun içinden çekilmeli

Steam ekran görüntülerinin **gerçek oynanış** olmasını şart koşar. Bu konteynerde Unity yok, o yüzden çekilemedi.
`Docs/readme/*.png` yakalamaları 1280×720 ve 2026-09-25 tarihli; arayüz o günden beri değişti, kullanma.
Unity'de 1920×1080 Game view ile çekilecek liste (en az 5, hedef 8–10; ilk görüntü varsayılan olarak öne çıkar):

1. Dolu bir gece, gün batımı: dört taburede misafir, baloncuklar, üst bar görünür.
2. Kimlik kartı açık, sipariş ve tarif iğnelenmiş.
3. Sahte kart: yanlış bayrak + KICK tuşu.
4. Tezgâh: shaker dolu, MIX sütunu, tarif listesi tik almış.
5. Musluk: bira yatık, köpük bandı yeşil (GOOD PINT).
6. Kusursuz döküm: altın ve magenta parçacıklar.
7. Roxy'nin turu: plaka ve gösterdiği şeyin aydınlatıldığı an.
8. Gecenin fişi: yıldızlar, kalp ve elmas satırları.
9. Market: şişeler, sayfalar ve dekor.
10. Yükseltilmiş salon: tablolar, neon, masa ve bitkiler.

Fragman notu: ilk 3 saniyede oynanış (kart → döküm → misafirin yüzü). 1920×1080, 30/60 fps, .mp4.

## 8 · Riskler ve açık sorular

- **Ad riski:** "Malibu" bir rom markası (Pernod Ricard). "Malibu Club" ise GTA: Vice City'deki ünlü kulübün adı;
  topluluk modları da bu adı kullanıyor. Steam'de aynı adlı bir oyun bulunamadı, ama bir **marka araştırması**
  yaptırılmalı. Logo değişmedi, karar yazarın.
- **Hikâye misafiri:** `storyInPlay` sahnede kapalıysa metinde hikâye iddiası olmamalı (şu an yok).
- **Dekor sanatı:** H6'nın resim basamakları 2–3 ve mermer lavabo yazarın seçimini bekliyor. Metin bu yüzden
  "daha iyi bir lavabo" diyor, "mermer" demiyor.
