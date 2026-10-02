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
