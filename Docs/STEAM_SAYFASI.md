# STEAM SAYFASI — Malibu Club: Cocktail Bar Simulator

*2026-10-01. Yazar: "Oyunum için en baştan steam sayfası elementleri ve görselleri oluşturmanı istiyorum. Ana
karakterimiz roxy olacak. ... pazar araştırması tasarım araştırması yap ... detaylı görsel ve giflerden yararlanılmış
steam about kısmı ve steam mağaza ve kütüphane görselleri hazırla. Oyunun Malibu Club yazı logosu aynen kalacak."*

Bu belge mağaza sayfasının tamamını tek yerde tutar: araştırma ve konumlandırma, metinler, etiketler, görsellerin
listesi ve Steamworks'e nasıl yükleneceği. Steamworks'teki başarım/istatistik işleri `Docs/STEAMWORKS.md`'de.

**Üretim (2026-10-01, ikinci tur):** yazar ilk turun koddan çizilmiş kapsüllerini kaliteli bulmadı ("nano banana ile
ya da farklı şeylerle üretebilirsin"). Kapsüllerin sahnesi artık Google'ın Gemini görsel modeliyle boyanıyor
(`Tools/steam_page/gen_nano.py`; Nano Banana Pro = `gemini-3-pro-image-preview`, 2K, hero 4K). Roxy'nin `idle` karesi
modele karakter referansı olarak gider. **Logo modele hiç çizdirilmez**, oyunun logo haritalarından birebir boyanıp
üstüne konur. Her görsel için birkaç aday üretilir, gözle seçilir, `ship_picks.py` seçilenleri Steam adlarına
yerleştirir. About GIF'lerinin zemini de modelin boyadığı boş bar (`gif_backdrop`); üstündeki her figür, kart, bardak
ve shaker oyunun kendi sprite'ı. Anahtar `GEMINI_API_KEY` ortam değişkeninden okunur, hiçbir dosyaya yazılmaz. Modelin
ham çıktıları (`out/nano/`) git dışında; seçim tablosu `ship_picks.py`'de.

```
GEMINI_API_KEY=... GEMINI_MODEL=gemini-3-pro-image-preview GEMINI_IMAGE_SIZE=2K python3 Tools/steam_page/gen_nano.py main_capsule --takes 2 --tag _pro
python3 Tools/steam_page/ship_picks.py      # seçilen adaylar -> out/capsules/
python3 Tools/steam_page/about.py           # GIF'ler (gif_backdrop_2 zemininde) + başlıklar -> out/about/
```

İlk turun koddan çizen üreticisi (`build_capsules.py`) yerinde duruyor; adaylar yokken yedek olarak çalışır.
Yeniden üretmek için:

```
python3 Tools/steam_page/build_capsules.py   # mağaza + kütüphane + etkinlik + ikonlar  -> out/capsules/
python3 Tools/steam_page/about.py            # About GIF'leri + bölüm başlıkları       -> out/about/
```

Bağımlılık: `pip install pillow numpy`. Bütün PNG'lerin LFS'ten inmiş olması gerekir (`git lfs pull --include="Assets/**"`).
İki komut da aynı girdiden bayt bayt aynı çıktıyı üretir. Rastgelelik yok; sabit tohumlar kullanılıyor.

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
| Kızıl saç ve bordo tulum mor zeminde kaybolur | Roxy'ye gün batımı tarafından Magenta[4] kenar ışığı ve logonun dört bantlı halesi verildi. Arkasında da güneş var. |
| Jenerik synthwave (ızgara, güneş, palmiye) "müzik klibi" gibi okunur | Karede her zaman **tezgâh, kokteyl ve kimlik kartı** var; tür ilk bakışta okunuyor. |
| Logo 120×45'te okunmalı | Küçük kapsülde logo genişliğin %71'i; tüp halesi zemini ayırıyor. |
| Kapsülde yalnız sanat, ad ve resmî alt başlık | Hiçbir kapsülde slogan, puan ya da ödül yazısı yok. "COCKTAIL BAR SIMULATOR" logonun parçası. |
| Piksel sanat yalnızca tam katlarla ve NEAREST ile büyütülür | Oda ×2 (oyunun 640×360 → 1280×720 ızgarası), Roxy ×3–7, hero ×4. Hiçbir sprite yeniden örneklenmedi. |
| Palet disiplini | Her renk `UITheme` rampalarından. Geçişler Bayer titreşimli bantlar, ışıltılar tabelanın 150/78/34/12 alfa bantları. |

**Logo:** `Assets/Resources/Logo/logo_*_map.bytes` haritalarından, oyunun `TitleSignLight` "Lit" kıyafetiyle **birebir**
boyanır (`Tools/steam_page/logo_render.py`). Harfler hiç yeniden çizilmedi. Gereken genişliğin üstündeki en yakın set
seçilip BOX ile **küçültülür**, asla büyütülmez.

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

| Dosya | Boyut | İçerik |
|---|---|---|
| `01_meet_roxy.gif` | 616×360 | Roxy yürüyerek girer, döner ve turun ilk sözünü yazar. |
| `02_read_the_card.gif` | 616×360 | Kart yükselir, sipariş okunur (ORDER TAKEN). İkinci misafir 19 yaşında: UNDER 20 – KICK. |
| `03_shake_and_pour.gif` | 616×340 | Shaker çalkalanır, MIX dolar, coupe'a dökülür. PERFECT: altın ve magenta parçacıklar. |
| `04_pull_a_pint.gif` | 616×340 | Bira dolar, köpük banda oturur: GOOD PINT, HEAD 14%. |
| `05_the_crowd.gif` | 616×300 | Beş misafir kendi klipleriyle içer, sevinir, bozulur. |
| `06_one_night.gif` | 616×300 | 18:00 → 02:00 arasında güneş batar, kasa dolar, beş yıldız yanar. |
| `07_build_the_house.gif` | 616×320 | Dekor tek tek yerine oturur, COMFORT elmasları dolar. |
| `banner_*.png` | 616×84 | Sekiz neon bölüm başlığı (MalibuArcade, tek ölçek). |

En büyük GIF ~1 MB, toplam ~3.8 MB. Valve'ın ~15 MB'lık sayfa sınırının çok altında.

**Steam'e yükleme:** Steamworks → Store Page Admin → Description → *Upload images*. Dosyalar
`{STEAM_APP_IMAGE}/extras/<ad>` olur; BBCode bu yolları kullanıyor, dosya adlarını değiştirme.

## 6 · Mağaza ve kütüphane görselleri (`Tools/steam_page/out/capsules/`)

| Steam yuvası | Dosya | Boyut |
|---|---|---|
| Header capsule | `header_capsule_920x430.jpg` | 920×430 |
| Small capsule | `small_capsule_462x174.jpg` | 462×174 |
| Main capsule | `main_capsule_1232x706.png` | 1232×706 |
| Vertical capsule | `vertical_capsule_748x896.jpg` | 748×896 |
| Page background | `page_background_1438x810.jpg` | 1438×810 (karartılmış; Roxy ve logo yok) |
| Library capsule | `library_capsule_600x900.jpg` | 600×900 |
| Library hero | `library_hero_3840x1240.jpg` | 3840×1240 (logo YOK; Steam kütüphane logosunu üstüne koyar) |
| Library logo | `library_logo_1280x720.png` | 1280×720, saydam |
| Library header | `library_header_920x430.jpg` | 920×430 |
| Event cover | `event_cover_800x450.jpg` | 800×450 |
| Event header | `event_header_1920x622.jpg` | 1920×622 |
| Community icon | `community_icon_184x184.jpg` | 184×184 |
| Client / shortcut icon | `client_icon_256x256.png` | 256×256 |

Boyutlar Steamworks'ün 2024 spesifikasyonu (üçüncü taraf kopyalarla doğrulandı; yüklerken Steamworks arayüzündeki
sayılarla bir kez karşılaştır).

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
