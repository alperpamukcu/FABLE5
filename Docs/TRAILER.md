# Fragman (2026-10-02)

Fragmanı oyun kendisi çeker, kurgu bir betikle kesilir. Elle kayıt yok, kurgu programı yok. Oyun değiştiğinde ikisi
de yeniden çalıştırılır ve aynı fragman güncel haliyle çıkar.

## 1. Çekim: Unity'de bir menü

**LastCall → Trailer → Record All Shots**

- Editör oynatma modunda olmamalı. Çekimler oynatma moduna kendileri girer.
- Game görünümü çekim boyunca 1920×1080'e sabitlenir, bitince eski boyutuna döner.
- Her sahne test çalıştırıcısında bir test olarak oynar. Takımın normal PlayMode testlerinde bu testler kendini
  atlar (`TrailerSwitch`), çünkü yalnız bu menü onları açar.
- Çıktı `Recordings/Trailer/` klasörüne yazılır (git dışı):
  - `<sahne>.mp4`: 1080p60, H.264.
  - `<sahne>.json`: sahnenin işaretleri, saniye cinsinden (`card open`, `first drop`, `shake`, `served`, `kicked`…).
- Tek bir sahneyi yeniden çekmek için: **Record One Shot...**
- Türkçe arayüzle çekmek için: **Record All Shots (Turkish)**

| Sahne | Dekor | Ne çekilir |
|---|---|---|
| `S00_roxy` | İlk gece, devralınan eski bar | Ana menü, NEW RUN. Roxy yürüyüp girer ve açılış turunun ilk satırlarını söyler. İlk işi verir, çıkıp gider. |
| `S01_roxy_serve` | Tropik (palmiye tavan, dalga duvar, flamingo neon) | Gece kamera dışında kapanır. Hikâyenin "evin konuğu" olarak Roxy tabureye oturur, ilk içki onun. |
| `S02_serve_N` | 2★ kulüp (şerit, şevron) | Müşteri başına bir çekim (en çok 8): kart, içki (döküş, çalkalama, karıştırma, fıçı), servis, mutlu yüz. |
| `S03_kick` | 2.5★ dalga odası | Yalan söyleyen kart ve KICK. |
| `S04_close` | 3★ deco | Kamera dışında kusursuz servisle dolu bir gece. Ardından kârlı gece fişi, pazar, alışveriş, ertesi gece. |
| `S05_rush` | 1.5★ stucco | Bütün tabureler dolu; el karttan karta gezer. |
| `S06_bottles` | 4★ raf | Mahzen açılır, şişeler tek tek tezgâha gelir (en çok 10). |
| `S07_rooms` | Boştan lükse | Sabit kadraj; oda her 1,6 saniyede bir basamak giyinir (`DevFit`, 6 adım). |

### Nasıl çekiyor

- **El, testlerin eli değil.** Her hareket oyun saatine göre yumuşatılmış bir kaydırma.
- **Döküş koordinatla değil, Core'a bakarak.** El, kap dolmaya başlayana kadar yükselir. Birkaç adım daha çıkıp
  akıntıyı görünür yapar, ölçüye gelince bırakır.
- **Sahne hazırlığı kamera kapalıyken yapılır:** `DevPresetStars` ile o yıldız seviyesindeki bar kurulur, saat doğru
  müşteri içeri girene kadar ileri sarılır. Kamera müşteri yürürken başlar.
- **Saat kameranındır.** `TrailerCamera`, `Time.captureDeltaTime` ile her kareyi tam 1/60 saniye ilerletir. Böylece
  yavaş bir yakalama takılma olarak görünmez.
  - Arayüz unscaled time ile oynar. Bu editörde o saatin adımı izleyip izlemediğini ilk 30 karede ölçer.
  - İzlemiyorsa duvar saatine geçer. Bu, `.json`'daki `clock` alanına ve konsola yazılır.
- **İmleç sonradan çizilir.** Oyunun imleci donanım imleci, ekran yakalaması onu görmez. Kamera oyunun kendi el
  çizimini sanal farenin olduğu yere basar.
- **Ses yok.** Ses motoru duvar saatiyle çalıştığı için kayma yapardı. Müzik kurguda eklenir.
- **Kayıtlar hiçbir yere işlemez:** kayıt dosyası, başarımlar ve Steam kapalı.

### Tohum

`TrailerSwitch.Seed` varsayılan olarak `"MALIBU-TRAILER"`. `S03_kick` 12 denemede sahte kart bulamazsa
**Inconclusive** olur; tohumu değiştirip yeniden çek.

## 2. Kurgu: bir Python betiği

```
python3 Tools/trailer/edit.py hook_60              # 16:9 (Steam) ve 9:16 (Shorts/TikTok/Reels)
python3 Tools/trailer/edit.py hook_60 --lang tr    # Türkçe yazılar
python3 Tools/trailer/edit.py hook_60 --wide       # yalnız 16:9
```

**`cuts/hook_60.json`, yazarın brief'i (2026-10-02):** en fazla bir dakika, kısa dikey videolar gibi kesilecek. Hızlı
geçişler, sürekli hareket, ilk saniyede kanca.

- **Akış beş perde:**
  1. Kanca: döküş, kart, "NOBODY TELLS YOU / WHAT THEY WANT".
  2. El işi: her kesmede bir fiil, her fiile bir kelime (POUR, SHAKE, STIR, PULL A PINT).
  3. Bükülme: yalan söyleyen kart, KICK.
  4. Büyüme: gece fişi, pazar, ertesi gece, dolu salon.
  5. Çağrı: logo ve istek listesi.
- **Ritim:** her bölüm 1–4 vuruş (~0,5–2 sn), kesmeler müziğin kendi temposuna oturur (`music_night_7`, ~118 BPM;
  tempo ve ilk vuruş parçadan ölçülür).
- **Bölüm alanları:**
  - Hareket: `zoom` bölüm boyunca itme, `punch` ilk karelerde vuruş zoomu, `flash` sert kesmede beyazdan açılış.
  - `speed`: elin beklediği yerleri hızlandırır.
  - `focus`: zoomun (ve 9:16 kırpmanın) merkezi.
  - `optional`: o işareti taşıyan çekim yoksa bölüm atlanır (örneğin karıştırılan bir içki sipariş edilmediyse).
- **Çekimler:** `S02_serve_*` o sahnenin her çekimi demek. `pick`, aynı işarete sahip kaçıncı çekimin kullanılacağını
  seçer; böylece arka arkaya farklı müşteriler görünür.
- **Ön kurgu:** bir sahne henüz çekilmemişse bölüm `still` resmini hareketle gösterir. Çekimden önce animatik
  izlenebilir.
- **Kendi kayıtların:** OBS/ShadowPlay kayıtları da `Recordings/Trailer/<ad>.mp4` olarak konup `"at": <saniye>`
  ile kullanılabilir.
- **`captions.json`:** ekrandaki yazılar, dil başına bir blok, `\n` iki satır yapar. Malibu Arcade ile sığan en büyük
  tam sayı ölçekte çizilir ve "pop" ile gelir.
- **Kapanış kartı:** anahtar görselin 16:9 / 9:16 hali (`keyart.py` → `trailer_end`, `trailer_end_tall`), altında
  "WISHLIST ON STEAM".
- **Çıktı:**
  - `Tools/trailer/out/hook_60.mp4`: 1920×1080, 60 fps, H.264 High CRF 16, AAC 320k, -14 LUFS, faststart. Steam'in
    istediği biçim (16:9, 1080p, 60 fps, H.264 + AAC, 5000+ kbps).
  - `hook_60_tall.mp4`: aynı kurgu 1080×1920.

## 3. Hikâye kurgusu (v3): `Tools/trailer/story.py`, `cuts/story_60.json`

Yazarın isteği (2026-10-02): rastgele kamera olmasın, hikâye olsun, Roxy ara ara anlatıcı olarak girsin, müzik
değişsin, yazılar animasyonlu olsun.

Kurallar araştırmadan geliyor. Kaynakları aşağıda: Derek Lieu'nun yazıları, Steam'in sessiz otomatik oynatması ve
türün kendi fragmanları.

- **Sıra:** oynanış ilk saniyede gelir, logo müziğin drop'unda yanar, çağrı ve Roxy'nin "button" satırı en sonda.
- **"Tell, show":** Roxy'nin bir satırı bir sütunu açar, altındaki görüntü onu kanıtlar. Satırlar en çok 8 kelime,
  ekranda aynı anda tek yazı olur.
- **Roxy'nin satırları** (`captions.json` → `roxy_*`):
  - "There you are, sugar. The bar's yours."
  - "The order's on the card, sugar. Read it."
  - "Fake ID? Show 'em the door."
  - "Stars pay the rent. Earn every one."
  - "Dress this place up. Make Miami jealous."
  - "It's your bar now. Don't blow it."
  - "Last call, honey."
- **Kamera kayması yok.** Bir plan ya **WIDE** (oyun 1:1) ya da **DETAIL** (aynı görüntü tam 2x, nearest). Detaya
  kesmeyle geçilir, zoom ile girilmez. Kart, fiş ya da Roxy'nin kutusu ekrandayken kamera kıpırdamaz. 9:16'da
  görüntü 2x'te kırpılır.
- **Ses:** oyunun kendi sesleri açık. Filmdeki her işaret kendi sesini getirir (döküş, çalkalama, kimlik, KICK,
  damga, şişeler). Roxy'nin yazısına kelime başına bir tuş "blip"'i eşlik eder.
- **Müzik:** `music_night_4`, 12,3 sn'den başlar. Parçanın drop'u fragmanın 6. saniyesine, neon tabelanın yandığı
  ana düşer. Kesmeler vuruşa oturur (~103 BPM).
- **Roxy'nin kutusu** (`overlays.narrator`):
  - Oyunun panosu: mor kutu, pembe tüp çerçeve, "ROXY VALE" sekmesi.
  - Kuyuda Roxy'nin kendi talk karelerinden büstü; yazı akarken ağzı oynar.
  - Jersey 15 ile daktilo efekti.
  - Kutu aşağıdan kayarak girer, aşağı inerek çıkar. Kart gibi okunacak bir şey varsa yukarıya alınır (`"box": "top"`).
- **Yazılar** (`overlays.title`): Malibu Arcade, harfler sırayla düşer, altına amber çizgi silinerek çizilir,
  arkasında mor bant var.
- **Tabela** (`overlays.sign_on`): logo neon gibi titreyerek yanar.
- **Kapanış:** `S08_endcard` karesi kullanılır. Bu kare 4★ salon, gece bitmiş, son müşteri olarak lamba altında
  tek başına oturan Roxy; üst bar ve pano gizli, el kadrajda yok.
  - Üstüne tabela yanar, "WISHLIST ON STEAM" ve "COMING SOON" gelir, sonra Roxy: "Last call, honey."
  - Yayın tarihi kesinleşince `when` değiştirilir.
- **Eksik çekim:** bir çekim yoksa o bölüm atlanır (`take` listesindeki yedekler sırayla denenir).

```
TRAILER_FILMS=<klasör> python3 Tools/trailer/story.py            # 16:9 + 9:16
python3 Tools/trailer/story.py --lang tr --wide
```

Kaynaklar (araştırma, 2026-10-02):
- [Derek Lieu, "No logo"](https://www.derek-lieu.com/blog/2019/1/19/no-logo)
- [Derek Lieu, "Tell, show, repeat"](https://www.derek-lieu.com/blog/2023/4/9/tell-show-repeat-the-2nd-easiest-game-trailer-to-make)
- [Derek Lieu, "Intercutting dialogue with gameplay"](https://www.derek-lieu.com/blog/2022/9/26/intercutting-dialogue-with-gameplay)
- [Derek Lieu, "Why your game trailer needs sound effects on"](https://www.derek-lieu.com/blog/2020/1/10/why-your-game-trailer-needs-sound-effects-on)
- [Derek Lieu, "The game trailer call to action end slate"](https://www.derek-lieu.com/blog/2021/4/25/the-game-trailer-call-to-action-end-slate)
- [Derek Lieu, trailer review: Papers, Please](https://www.derek-lieu.com/blog/2019/3/3/trailer-review-papers-please)
- [Derek Lieu, trailer review: Coffee Talk](https://www.derek-lieu.com/blog/2020/2/2/trailer-review-coffee-talk)
- [Steamworks: trailers](https://partner.steamgames.com/doc/store/trailer)
- [Chris Zukowski, 60 mistakes (PDF)](https://howtomarketagame.com/wp-content/uploads/2023/05/Zukowski_60MistakesEbookV1.pdf)

## 4. Kamera kurgusu (v4/v5): `Tools/trailer/fx.py`

Yazarın v3 notu: "kamera titremesi, oynaması, zoom, odak, kayma olmadan çok yavan". O yüzden §3'teki "kamera kayması
yok" kuralı v4'te kalktı. Her kare Python'da kuruluyor:

- **Kamera:** bölüm başına `cam` anahtar kareleri (`[u, zoom, cx, cy]`, yumuşatılmış), ya da filmin `focus <ad> x y`
  işaretini takip eden `track`.
- **Titreme:** işaretlere bağlı (`SHAKE_ON`: kick, damga, tam akış, oda değişimi...) ya da bölümün kendi `shake`'i.
- **Işık:** `spot` (sabit nokta ya da işaretli özne), `spot_r`, `dark`, `blur` (odak dışı bulanık).
- **Geçiş:** `in` alanı `whip` (yatay sürüklenme + whoosh), `flash`, `punch` ya da `cut` olabilir.
- **Yazılar:**
  - `say`: Roxy'nin kutusu. `say_at` ve `say_sec` zamanlamasını ayarlar.
  - `title`: kinetik yazı. `title_sec` ile birkaç bölüme yayılabilir, `title_y` konumunu verir.
  - `type_blips`: oyunun kendi panosu yazarken daktilo sesi.
- **Kapanış:** `end` alanı. `sign_y`, `cta_y`, `when_y`, `cta_at` ve `when_at` ile yerleşir.
- **Müzik:** kesitte `beat` verilirse ölçülen tempo yerine bestenin vuruşu kullanılır; ölçüm bazı parçalarda kayıyor.
  `music_fade` ile sonda uzun çıkış yapılır.
- **Görüntü:** lanczos ile ölçeklenir, zoom 1,2'yi geçince hafif keskinleştirilir. Çıktı H.264 High, CRF 12, 1080p60.

```
TRAILER_FILMS=<klasör> python3 Tools/trailer/fx.py v5_60 --wide     # yalnız 16:9
```

### v5 (2026-10-03): eldeki görüntüden 16:9

Yazar "şimdilik sadece yatay yeter" dedi. `cuts/v5_60.json` yalnızca `trailer-footage` dalındaki çekimlerden kuruldu
(S00, S02, S03, S06, S07, S10, S11).

Önce her film kare kare haritalandı: özne kutuları, temiz pencereler ve kusurlar. Kesit yalnız temiz pencereleri
kullanıyor. Haritanın yakaladıkları:

- S02'de servis hiç oturmuyor: misafirin sabrı bitiyor, para değişmiyor.
- S06'da hiçbir şişe dökülmüyor. Bunlar eski kodun çekimleri; v4 kodu ikisini de düzeltti, ama yeniden çekim gerekiyor.
- S07'nin 6. odasında 3. kademe lamba var (Deco Lantern). Kesit 8,0 sn'den önce bitiyor.
- S00'da el, Roxy'nin göğsünde duruyordu. Çekim artık eli NEW RUN'dan sonra köşeye çekiyor.
- S11'de kitap kapanmıyordu. Açık kitap kendi tuşuna (B) cevap vermiyor, çünkü açık bir sayfa varken `UpdateHotkeys`
  susuyor. Çekim artık Escape ile kapatıyor.
- eastasianman dönerken sprite'ı büyüyüp yükseliyor, bu yüzden oyuncu kadrosundan çıktı. pastelman temiz döndüğü
  için kadroya girdi.

Müzik `music_night_8`, 13,846 sn'den (6. ölçü), vuruş 0,5769 sn, kazanç 0,38. Parçanın drop'u 4,615. saniyede tabelaya,
nakarat (saksafon ve brass) 41,54. saniyede dekor değişimine düşer. Kütüphane ölçülerek seçildi (ses, enerji
eğrisi, tempo): night_1 ve night_5, reddedilen night_4 ile aynı reçeteden; night_2, night_7 ve dayend_3 ilk kareden yüksek ve
yükselmiyor.

Eksik olan ve yeni çekim bekleyenler: fatura ekranı ve gün sonu (S04), tepki dizisi (S09), sahte kart ve KICK (S03),
yoğun salon (S05), kapanış karesi (S08), Roxy'nin servisi (S01).
