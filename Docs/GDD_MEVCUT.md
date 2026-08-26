# LAST CALL — MEVCUT OYUN GDD'Sİ (as-built)

**Tarih:** 2026-08-07 (§9 servis sahneleri 2026-08-13'te yeniden kuruldu) · **Kaynak:** koddan çıkarıldı (8 kollu denetim, dosya:satır kanıtlı) · **Durum:** oyunun *bugün gerçekte olduğu hali* — tasarım niyeti değil, çalışan kural.

Bu belge, tarihi GDD modüllerinden (00–13) ve kod gerisinde kalmış plan maddelerinden arındırılmış tek referanstır. Çelişki listesi için `Docs/GELISTIRME_RAPORU.md` §6.

---

## 1 · Oyun tek cümlede

Barmen-tycoon: her gece kapı açılır, müşteriler oturur, **kimlik kartına bakmadan siparişi göremezsin**; içkiyi gerçek fizikle (dökme, çalkalama, musluk) yapar, servis eder, bahşişle ayakta kalır, kirayı ödeyemezsen batarsın. Beş yıldız, son oyun hedefi.

## 2 · Gün döngüsü

```
DayOpen (gece, 95 sn)  →  kapanış saati: kapı kapanır, oturanlar bitirir
  → gün tamam: KİRA otomatik düşer, market yeniden atılır → DayEnd
DayEnd (hesap + market) → ContinueToNextDay(): puanlama, defter, iflas kontrolü → yeni gün
```

| Kural | Değer | Yer |
|---|---|---|
| Gece süresi | 95 sn (gösterim 18:00→02:00) | TycoonConfig.cs:95-99 |
| Menü açıkken zaman | ×0.3 yavaşlar | TycoonConfig.cs:31 |
| Kira (tek borçlandıran) | `12 + 2g + g²/9` (24. gün $136, 30. gün $184) | TycoonConfig.cs:175 |
| İflas | üst üste **3 gün** kasa < 0 ile kapanış; bir temiz gün sayacı sıfırlar | DayLedger.cs:106-121 |
| Kazanma | açık uç; 5★ itibar hedef | BarRating.cs başlık |

## 3 · Müşteri

- **Geliş:** aralık `max(6, 12 − 0.5×gün) × yıldız çarpanı × (1±0.30)`; ≥3 bekleyen varsa gelen **vazgeçer** (balk). Ayrılanın kirli bardağı taburesini 7 sn kilitler (tıkla = topla).
- **İki saat:** (1) *sorulma sabrı* `max(14, 30−1.6g)` — dolarsa fırtına gibi gider; (2) *içki sabrı* `max(22, 50−2.5g)` — **kimlik okununca** başlar.
- **Kimlik kartı (gizli bilgi):** `CustomerVisit.Order` `InspectId()` çağrılana dek **throw eder**; gerçek siparişi yalnız Core görür (`OrderTruth`). Kartı açmak siparişi almaktır — geri dönüşü yok. Kör servis yasal: yargıç gerçekle karşılaştırır.
- **Sipariş havuzu:** açık menüden, en düşük ranktan `3+gün` tarif; stok bakılmaz (kuru şişe = `DeclineOrder`).
- **Servis tercihi (spec):** ~%50 sade; değilse 1–2 garnitür {buz, limon, tuz, şeker}. Draught'a garnitür yazılmaz. Beklenen doluluk 0.80 (tepeleme isteği 2026-08-02'de emekli). **"Sert çalkala" 2026-08-11'de emekli:** yöntem müşterinin hevesi değil TARİFİN talebi — hakem artık `Prep`'i notluyor (aşağıda).
- **Ekstra tur:** Exact + zanaat tam + dönen müşteri + bekleme <%90 → en fazla 2 ek sipariş, sabır %80'e tazelenir.
- **Müdavimler opt-in:** kayıt (registry) verilmezse anonim kalabalık. Müdavim: isim/yaş/şehir/arketip/ziyaret/ilişki taşır; duygu katmanı 2026-08-02'de söküldü — kokteyle verilen tepki tek gerçek.
- **Açılış gecesi kimseyi tanımaz (2026-08-25):** `RegularsRegistry.RollNext` artık `allowReturns` alıyor, `TycoonRun` gün 1'de `false` geçiyor — bar dün yoktu, o yüzden gece birde içen herkes YENİ. Dönüş zarı yine atılır (kapı akışa bir çekiş borçlanmasın diye), sadece onurlandırılmaz; gün 2'den itibaren %55 dönüş şansı geri gelir. Yazarın şikâyeti buydu: ilk gün kimlikler "2. ziyaret" ve dolu yıldız satırı basıyordu. **ÖLÇÜLEN BEDEL:** ekstra tur "dönen müşteri" şartı taşır, gece bir artık dönen müşteri barındırmadığından o gece ekstra tur YOK — 200 koşuluk simde iflas %3.0 → **%7.0**, medyan kasa $194 → **$145**, bar itibarı 2.67★ → 2.59★. A/B izole edildi: kapı kapatılınca eski rapor birebir yeniden üretiliyor, yani kayma tamamen bu kuralın. Kural yazarındır; telafi kolu (başlangıç parası, gün 1 kirası, ya da ekstra turun "dönen" şartı) ayrı bir karar.
- **Yüz KİŞİYE aittir, isme değil (2026-08-25):** `TycoonHud.LookFor` eskiden arketip havuzundan gelen İSMİ hash'liyordu — kırk isim on çizime çöküyor, aynı isim daima aynı yüzü açıyor, oda her gece dört-beş surattan ibaret görünüyordu ("müşteriler rastgele gelmeli hergün"). Artık yüz `RegularState.Id`'ye bağlanır ve tanınmayan biri EN UZUN SÜREDİR sahnede olmayan yüzü alır: kadro tükenmeden kimse tekrarlanmaz, açılış gecesi (~8 içen, 9 yüz) baştan sona yabancıdır. Kendi yüzü o an başka taburede olan bir müdavim, o ziyaret için boş bir yüz ödünç alır ve kendi yüzünü bir sonrakine saklar. Kendi üreticinde (koşunun tohumundan türeyen ayrı "faces" akışı) — Core'un akışlarına dokunmaz, hiçbir şeye karar vermez.
- **BOŞ TABURENİN YÜZÜ YOKTUR (2026-08-25) — "aynı müşteriler geliyor"un ASIL sebebi buydu.** Gelen müşteri taburede önce `v.Visit`e yazılıyor, yüz SONRA soruluyordu; `LookFor`'un ilk işi ise "zaten yüzü olan tabureye dokunma" — ve o tabure hâlâ az önce çıkan kişinin yüzünü taşıyordu, çünkü `view.Look` ayrılışta hiç temizlenmiyordu. Sonuç: her taburede yalnız İLK müşteri gerçek bir yüz alıyor, sonrakilerin hepsi onu miras alıyordu — dört tabure, koşu boyunca **dört yüz**, ve misafir defteri (yüz başına tutulur) barın açılış saatinde "3. ziyaret" + dolu yıldız satırı basıyordu. Play'de ölçüldü: kapıdan yedi ayrı kişi girmişken dört yüz çiziliyordu; tek satırlık `view.Look = null` sonrası yedi kişi = yedi yüz, hepsi 1 ziyaret.
- **Misafir defteri koşuyla sıfırlanır (2026-08-25):** `_patronLog` (yüz başına ziyaret + bırakılan yıldız) hiç temizlenmiyordu; HUD bir kez kurulduğu için NEW RUN, yüzleri önceki koşunun sayaçlarıyla açıyordu. Yüz atamaları da aynı yerde sıfırlanır.
- **Kimlik evrakı canlı kadroyu da kapsıyor (2026-08-25):** `customers/papers.json` 2026-08-19 rig'inin dokuz yüzünü de taşıyor. O güne dek CANLI kadronun tek satırı yoktu: isim arketip havuzuna düşüyor, "citizen of" alanına ülke yerine ŞEHİR basılıyor, bayrak hiç çizilmiyordu — okunması istenen tek kartta, sessizce. `PapersTests` dokuzunu tek tek çitliyor.
- **Son müşteri = evin misafiri + sınav (2026-08-13 rework, Core'da var, henüz sessiz — GDD 26 §3-4):** hikâye opt-in; `StoryArc` verilmemiş koşu bugünküyle birebir aynı. Verilmişse: kapı kapandıktan **ve** oda boşaldıktan sonra o gecenin beat'inin misafiri `BarDay.SeatGuest` ile oturur. **Defterlerin dışında:** kimlik yok (kendini tanıtır — gizli bilgi kuralının TEK yazılı istisnası, CLAUDE.md'de çitli), hesap yok, bahşiş yok, puan yok, fişte satır yok (`OnTheHouse`; gecenin sayan listesi `BarDay.FinishedCounted()`). **Sınav:** birkaç içki, TEK saat, post-it'te teker teker; standart = tam tarif + tam zanaat + tam yöntem, tek af doluluk ≥0.90; yanlış içki hata sayar ve istek YERİNDE kalır; `allowedMistakes` aşılınca veya saat bitince gece yanar, beat kendi gecesinde `returnsAfterWeeks` hafta sonra döner. Diyalog saati tutar (`ClockHeld`): konuşurken hiçbir şey işlemez, `BeginLastCallTrial()` başlatır, 120 sn `TalkingGrace` emniyeti gece rehin kalmasın diye. Ekstra tur yolu bilerek dokunulmadı (ödül sabrı tazeler; talep tazelemez). Veri bağlantısı ve diyalog kabuğu S3/S5'te.
- **Takvim artık kural (2026-08-13, `BarCalendar` — GDD 26 §2b; hafta 2026-08-14'te yeniden kesildi):** hafta altı açık gece, **Pazartesi→Cumartesi, PAZAR kapalı** (gün 1 = Pazartesi; takvim Pazar'ı kepenk olarak çizer). Plakadaki `WEEK 2 · FRIDAY` yazısı haftalardır oradaydı ama hiçbir şey ifade etmiyordu; hikâye misafiri artık **yalnız Cumartesi** gelir (`VipNight`, "her cumartesi bir hikaye müşterisi gelecek") ve sessiz geceler "eksik olanı gidip alma" geceleri. (Bu satır bir süre 2026-08-13 kesimini — Salı→Pazar — anlattı; kod her zaman kazanır.) Ev halkı misafir değil: yalnız `role: host` sessiz gece çalışabilir (Ece'nin açılış Salısı). Takvim `TycoonHud`'dan Core'a taşındı, yazı değişmedi.

## 4 · İçki yapımı — üç yol, tek yasa

**Brim kanunu:** hiçbir kap taşmaz; `Add` kabul ettiğini döndürür, fazlası hiç girmez. Dökülme = bilinçli israf sayaçları (bin, SpilledBeer).

| Yol | Kapı | Reddettikleri |
|---|---|---|
| **Shaker** (`BeginPour/PourTick/PourMeasure/PourGarnish/Shake/Stir`) | HER içeceğin inşası — gazlı dahil (2026-08-14) | yalnız bira (keg işi) |
| **Bardakta inşa** (`PourAtGlass`) | sim ve cam-beyanı yolu; artık duvarın açtığı bir kapı değil | bira |
| **Musluk** (`BeginPull/PourTilted/SettleHead`) | yalnız bira | bira olmayan id; shaker doluyken pull |

- **Servis dökümü zorunlu:** içki shaker'da servis edilemez; `PourIntoServingGlass(hacim, isabet)` — isabet dışı kısım dökülür, oranlar bozulmadan (TransferInto brim'e kadar, hazırlıklar bardağa taşınır).
- **Zorunlu karıştırma (GDD 21 §14; tarife bağlandı 2026-08-14):** önce **tarif** konuşur — `TinMethod`, tin'in kendi içeriğinin eşleştiği tarifin `prepMethod`'u: `Shaken`/`Stirred` çalışmayı zorunlu kılar, `Built` asla. Kitap içeceği adlandıramıyorsa eski yapısal kural devreye girer: tin'de ≥%3 payla **2+ alkollü** içerik (kategori testi — likörler sayılır, ABV asla kural beslemez) varsa dışa döküm `Shake` ya da `Stir` ister; red `PourIntoServingGlass`'ta, UI `CanPourOut` okur. Bardakta inşa muaf (kural tin hakkında); bin her zaman açık; Info'suz test kartları bilerek muaf. `Stir(enerji)` = `Shake`'in aynası (`IsStirred/StirEnergy`, tek yuva son kazanır). Hakem yöntemi aynı gün öğrendi (§6 zanaat): Martini'yi çalkalamak hâlâ YASAL (kapı "karışsın" der, "doğru karışsın" demez) ama bahşişten öder.
- **Bardak otomatiği:** eşleşen tarifin `GlassId`'si ilk dışa dökümde seçilir; sıvı varken kap değişmez. Kapasiteler: pint 1.6 · highball 1.0 (varsayılan) · rocks 0.7 · martini 0.6 · coupe 0.55.
- **Bira fiziği (TapPour):** akış 0.42/sn; 45° ideal, >60° döker, 88° tamamı ziyan; dik tutuş köpük %78 → yatık %4; köpük bandı **0.08–0.20** (ideal 0.14); çökme `0.16/sn`, çöken köpüğün %35'i sıvıya döner. `Preparations.Draught` damgası yargıca "köpüğü puanla" der.
- **Hazırlıklar:** shaken/stirred (tek yuva, sonuncu kazanır), ice, lemon_twist, salt_rim, sugar_rim, draught.

## 5 · Tarifler ve eşleme

- **54 tarif** (`recipes.json` ↔ `RecipeCatalog` parite testli). 4'ü canlı başlar (draught, neat_pour, vodka_soda, gin_sour); 49'u satın alınarak açılır.
- **Bantlar:** her kokteyl **stil bantlı** (cin ≠ votka); yalnız draught + neat_pour tip bantlı (marka-bağımsız). Stil+tip karışımı kurucuda reddedilir.
- **Eşleme (`RatioRecipeMatcher`, 2026-08-20 mükemmel-döküm respeci):** MinFill kapısı (yalnız draught 0.75) → her adlı pay **mükemmel değerin 20'lik KUTUSUNDA** olmalı (kutular alt-sınır-dahil: tam 40 → 40–60 kutusu) VE ≥ **%5** (tutam malzeme sayılmaz — nanesiz Smash, Sour'dur) → adsız pay ≤ **0.15**. En yüksek rank kazanır. El yazması min/max bantlar tarifin TANIMI olarak kaldı (mükemmel onların içine oturur) ama kabul testi değil — oyuncu bandı göremez, kutuyu görür.
- **Mükemmel döküm (`PerfectPour`, GDD 21 §9a):** `IdealPour` + ızgara kenarı koruması (kenardaki değer, tarif kimliğinin FNV hash'iyle 2–5 puan kutu içine itilir; yedi 40/60 highball'un her biri kendi mükemmelini taşır: 36.6/63.4, 42.5/57.5…). Katalog geneli test: toplam=1, her değer kenardan ≥2 puan içeride, ≥%6, ve her tarifin mükemmel dökümü TÜM kitaba karşı kendisi olarak okunur (0 rank çakışması, 52 tarif).
- **Öğrenme durumu (`TycoonRun`, koşu ömürlü):** Exact servis en iyi yapımı yazar (`BestMakeFor`: doğruluk + dökülen paylar); her malzeme mükemmele **±2.5 puan** içinde inerse sayfa **mükemmellenir** (`IsPerfected`) ve `ExactPourFor` kesin sayıları verir — o âna kadar FIRLATIR (InspectId deseninin aynısı; iade sayfayı geri alır, öğrenilen geceyi almaz).
- **MinTier (kalite bandı):** martinez (cin≥T2), boulevardier (viski≥T2), rosita (tekila≥T2), el_presidente (rom≥T2), **vesper (cin≥T3 + votka≥T2)**. Ucuz şişe bandı doldurmaz — hata mesajı yok, içki "daha azı" okunur.
- **Rank kademeleri:** 1–8 başlangıç (kapısız) · 9–14 → 2.0★ · 15–21 → 3.0★ · 22+ → 4.0★. Fiyat `max(9, 5+5(rank−2)/2)`. Alım kilitli stok stillerini kataloğa salar.

## 6 · Ekonomi

**Gelir** (ayrılışta tahsil, serviste değil):

| Kalem | Formül |
|---|---|
| Taban fiyat | `3 + (rank+1)/2` (bilerek düşük — $4–17) × **(0.10 + 0.90 × doğruluk)** (2026-08-20): doğruluk = mükemmel oranlara yakınlık, pay-ağırlıklı; doğru kutu her zaman BİR ŞEY kazandırır (taban $1 tabanı) |
| Stok primi | seçkin Spirit/Bitter bandı başına `(rafın en iyi tier−1) × $2` |
| Kalabalık çarpanı | HighRoller ×1.25 · Regular ×1.0 · Broke ×0.75 |
| **Bahşiş (asıl kazanç)** | `ödenen taban × kalite` (yalnız Exact, 2026-08-20); kalite = **0.35 hız + 0.25 zanaat + 0.20 doğruluk + 0.20 doluluk**. Zanaat (2026-08-11): kokteylde `0.6 × garnitür-spec + 0.4 × YÖNTEM` — yöntem, SİPARİŞ EDİLEN tarifin `Prep`'ine karşı (Shaken çalkala ister, Stirred kaşık ister; yanlış karıştırma = hiç karıştırmama, çalkalanmış Martini berelidir; Built umursamaz). Draught'ta zanaat = köpük. Ekstra tur artık doğru yöntemi de ister. Broke/Yanlış/0 taban → bahşiş yok; Close bahşiş almaz — kasada ödeme yok (2026-08-20) |
| **Yakın (Close)** | **istenen içki, kutusunun dışında** (2026-08-20): tarifin adını andığı her bant bardakta (≥%5), yabancı pay %15 içinde, ama bir pay KUTUSUNU kaçırmış → **$0, bahşiş yok** ("tamamen yanlış" — kutu menüde herkesin okuyabildiği yerde). Memnuniyet 0.30: kendi içkisinin bozulmuşu, yabancı içkiden az küstürür (0.05'e karşı). Tier hâlâ affedilir: kuyu ciniyle Vesper buraya düşer. *Aynı aileden başka bir içki* Yanlış'tır. Bantsız sipariş (bira, sek) Exact ya da hiç. (2026-08-14 yarım-bahşiş hâli, kutu görünür olunca kaldırıldı: okunabilir uçurum tuzak değil hedeftir) |
| Yanlış içki | *teslim edilenin* taban fiyatı × kendi doğruluğu (tanımsızsa $0) |
| Ret (doluluk <0.35) | $0, memnuniyet 0.02 · Decline: $0, 0.15 |
| Atıştırmalık | tabına fiyat (bahşişsiz); sabah geri alım `fiyat−1` → kâse başına net $1/birim |

**Gider:** kira (tek eksiye düşüren) · dolum `eksik×$3` · marka `Info.Price` yoksa `8+6×tier(+6 spirit)` (yıldız kapılı `min(4, tier)`) · tarif · tabure `$30/$50` (4→6) · bardak kademesi (hat başına 5 fiyat, json) · tezgah `40×tier` (yalnız Ambience) · çöp `hacim×$2`.

**Memnuniyet:** `(Exact .75 | Close .50 | Wrong .05) + 0.20(zanaat−.5) + 0.12(doluluk−.5) − 0.30×bekleme + Ambience` (0–1).

### 6.1 · Musluk merdiveni ve fıçı kilidi (2026-08-19)

Yazarın kuralı: *"3 seviye musluk olacak, marketten musluğu geliştirmeden bir üst
seviye fıçı bira alınmamalı."* Üç kule TEK yuvada (`taps`, tezgâh üstü x192,
y=`CounterRestY`) duran tek istasyonun üç yaşı — `taps_one/two/three`, `tapLevel`
1/2/3. Odada aynı anda **yalnız en yükseği** çizilir (`TycoonRun.StandingTap()`);
alttakiler satılmış değil, üstü kapatılmış sayılır.

- **Basamak atlanmaz:** `BuyFixture` yalnız `TapLevel + 1` olan kuleyi satar
  (`CanBuyTap`). Mağaza kutucuğu sebebini yazar ("2 LINE TOWER FIRST"), yıldız değil.
- **İade sırası:** aynı gece iki basamak alınabildiği için üstteki kule dururken
  alttakini iade etmek reddedilir — üstten geri verilir.
- **Fıçı kilidi:** her keg `tapLevel` taşır (`beer_kestrel` 1, `beer_collier` 2,
  `beer_marigold` 3) ve `UnlockCondition.Tap(n)` ile kilitlenir — mağazanın dördüncü
  kilit türü, ilki odaya bakan. Yıldıza bağlı DEĞİL: üç keg de T1, dolayısıyla merdiven
  sıfır der ve tek ayıran kule. Tutulan keg `StarsWanted = NaN` döndürür, böylece
  koridorun "n★'da açılır" ipucunu hiçbir yıldızın açmayacağı bir basamağa çekmez.
- **Veri:** kule seviyesi `fixtures.json`'da `tapLevel`, keg kilidi `base_bar.json`'da
  `tapLevel`. Yükleyici iki içerik hatasını kapıda reddeder: bir merdivende aynı
  basamağın iki kez bulunması ve 1,2,3 dizisinde delik olması (satın alınamaz kule).

### 6.2 · Merdiven biradan çıktı; paspas da bir parça değil (2026-08-25)

`tapLevel`'in yanına 2026-08-24'te gelen genel `level` alanı, §6.1'deki bütün kuralları
(tek yuvada birden çok parça, yalnız en yükseği durur, basamak atlanmaz, üsttekinin
altındaki iade edilmez) biradan bağımsız hâle getirdi. O gün duvar lambaları bunu
kullanan ilk merdivendi; **2026-08-25'te lavabo ikincisi oldu**: `counter_sink` basamak 1
(odayla gelir), `sink_brass` basamak 2 — aynı siluet, pirinç. Merdiven kodu tek: bir
üçüncüsü yalnız `fixtures.json` ister.

- **Market kutucuğu artık yuvayı okuyor.** Lamba merdiveninin satırı sabit yazılmıştı
  ("the back wall · both lamps, one fitting") ve pirinç lavabo onu miras alıp tezgâh
  üstündeki bir tekne için duvarı söylüyordu. `RungPlace` yuvanın `OnCounter` ve
  `PairSpreadPx` alanlarından cümleyi kuruyor; dördüncü merdiven kod istemez.
- **Duvar lambaları yazarın kendi çizimleriyle değişti** (mark 1 cam tüp: camgöbeği
  tepe, pembe gövde; mark 2 mercan çerçeveli krem panel; mark 3 palmiye aynı kaldı).
  Üç mark da 40×40 tuvalde ve mürekkebi tuvalin ORTASINDA — sahne duvar parçasını
  çizimine göre değil tuvaline göre astığı için, kayan bir mark yükseltilince duvarda
  zıplar. `Tools/room_dressing_gen.py` bunu kapıda ölçer.
- **Yeni bir yuva bayrağı: `flat`.** Halı tahtaların, bira paspası tezgâhın üstünde
  DÜZ yatar ve o yüzeyi paylaşan parçalar (masalar, bira kulesi) onların ÜSTÜNDE
  durmalıdır. `flat` parçayı bir sıralama bandı aşağı indirir (tezgâhta 35 yerine 34,
  zeminde 20 yerine 16) ve temas gölgesini kapatır — bütün yüzüyle yere değen bir şeyin
  altındaki leke gölge değil kirdir. `onCounter`'dan bağımsız: iki yüzeyin de dizilişi var.
- **Odayla gelen yeni parçalar:** `floor_rug` (Tide Rug, x320 y106) ve `beer_mat`
  (Drip Mat, x540 y74 — kulenin ayağı üstünde). İkisi de `startsInTheRoom`, yani
  markette OURS görünür, satılmaz, iade edilmez.

## 7 · Yıldız / itibar omurgası

- `BarRating`: 0★ başlar; gece yıldızı `1+4×memnuniyet`, **iki tavanla** kırpılır; ilerleme ataletli (+0.10 çıkış, −0.20 iniş, gecelik en çok +0.25). Fırtına gidenler de puan yazar.
- **Tavanlar döngüyü zorlar:** `UpgradeStarCap = 2.0 + bardak adımları (hat başına 0.60'a dek) + 0.25×(tabure−3)`; `MenuStarCap` gece servis edilen en iyi Exact ranka göre 2.0→5.0.
- **ODA ORTADAN DOLAR (2026-08-25, yazar: "başlangıçtaki koltuklar 2-3-4-5 sırası olacak geliştirme ile alınan koltuklar 1 ve 6 olmalı"):** tezgâh boyunca altı tabure çizilir, yeni bar dördüne sahiptir — eskiden İLK dördüne, yani açılış gecesinin bütün kalabalığı sol duvara yaslanıyor ve kasayla arasında iki tabure boşluk kalıyordu (yeni açılan bir bar terk edilmiş gibi okunuyordu), üstelik yükseltme kimsenin oturmadığı sıranın UZAK ucuna bir tabure daha ekliyordu. Şimdi sahip olunan dördü ORTADAKİ dört (2-3-4-5), yükseltmenin aldığı ikisi ise iki UÇ: önce kasa tarafı (6), sonra uzak duvar (1). Sıra `SeatFillOrder(slots, StartingSeats)` ile TÜRETİLİR (açılış bloğu satırın ortasına yerleşir, artanı kasa ucundan geri doğru eklenir), yani başka bir tabure sayısıyla açılan bir bar da ortalanır. Evin misafiri hâlâ kasaya en yakın taburede oturur ama artık `TillEndward` ile — sırayı TERS gezmek yanlış cevabı verir, çünkü yükseltmenin aldığı SON tabure uzak duvardakidir.
- Kalabalık yarını seçer: ortalama ≥4.2 HighRoller · ≥1.5 Regular · altı Broke. Ambience: bardak+tezgahtan en çok +0.21 düz bonus.

## 8 · İçerik envanteri

| Küme | Sayı | Not |
|---|---|---|
| Şişe kartı | **41** (30 canlı / 11 kilitli) | T1 26 · T2 5 · T3 5 · T4 5; markalar parodi (Smirkoff, John Wanderer, Maliboo…) |
| Başlangıç rafı | 6 | vodka_astra, gin_boothby, soda_klara, lemon_fresh, syrup_house, beer_kestrel (+bootstrap'ta sabit) |
| Gazlı bayrağı | **5** | cola, tonic, energy + **soda_klara, ginger_kicker (2026-08-11'de çevrildi** — §12 borcu kapandı). **2026-08-13:** gazlılar arka bar duvarına GERİ döndü; Serve dolabı kaldırıldı (aşağı) |
| Tarif | **54** | Built 19 · Shaken 22 · Stirred 13; pint 1 / rocks 14 / highball 22 / coupe 10 / martini 7. **2026-08-15:** black_russian (rank 8, 0★) ve mint_julep (rank 21) Built→**Stirred** — kaşık artık ilk basamakta öğreniliyor; en erken karıştırılan tarif rank 22 (4★) idi ve `MixRequired` yöntemi okuduğundan tezgâhın yarısı görünmüyordu |
| Bardak | 5 | 6'şar kademe (T1 + 5 satın alım) |
| Atıştırmalık | 4 | asla yalnız satılmaz (Core reddi) |
| Arketip | 8 | ağırlık toplamı 24, Easygoing/Particular 12–12 dengeli |
| Çizili müşteri | **9** | 2026-08-19 rig'i; hepsinin yıldız kapısı 0 — yani *şimdiki kadronun tamamı başlangıç müşterisi*, kilit açma ileride eklenecekler için. **spanishsuit 2026-08-25'te kesildi** (yazar: görseli ve animasyonları bozuk); 200 karesi silindi, üretim kaydı `Tools/patron_trial_state.json`'da kaldı ve o kareler yeniden gönderilmemeli |

## 9 · Ekranlar ve fiiller

| Ekran | Oyuncu ne yapar |
|---|---|
| **Zemin (HUD)** | tabureye içki sürükle=servis · çöpe sürükle=at (ücretli) · kirli bardak tıkla=topla · kâse tıkla=atıştırmalık taşı · kasa/ayarlar. **KİTAP TUŞU DA NESNE OLDU (2026-08-25, yazar: "Book butonu ise tezgahın üstüne sabitlensin"):** 84×40'lık gri BOOK tuşu gitti; yerine tezgâhın üstünde KAPALI DURAN kitabın kendisi var (`Items/book_closed`, 28×55 sanat px, 1 sanat px = 2 HUD birimi — odanın kendi grenı; ayağı `CounterLineY - 36`'da, yani tezgâhın ÇİZİLİ yüzeyinde, ve çekmece açılınca odayla birlikte yükselir). Çizim TÜRETİLDİ, yeniden çizilmedi: `Tools/book_closed_gen.py` `menu_booklet.png`'in kendi renklerini okur (kapak Amber[0], gövde Malt[0], yaprak Cream[4], yaldız Amber[2], şerit ViceRed[1]) — tezgâhta duran şeyin AÇILAN şey olması gerekir, ve bu proje "ikinci çekim başka bir nesne çıkarır" dersini üç kez ödedi. Haber rozeti kitabın sağ üst köşesinde. **KİTAP ODANIN IŞIĞINA GİRDİ (2026-08-25, yazar: "menü en ekranın ortasında kalmış biraz daha tezgaha dahil hissi verdirilmeli gölgelendirmelerden etkilenmiyor kasa gibi etkilenmeli, ve seçilebilir olduğu anlaşılması için parlamalı ve mouse ile üstüne gelindiğinde menüyü aç yazmalı"):** üç ayrı kusur, tek kök. **Kök:** kitap bir CANVAS propu ve Unity'de hiçbir ışık canvas'a ulaşmıyor — odadaki her fikstür `WorldSprite`, yani URP 2D ışıkları onları boyuyor, kitap ise gecenin her saatinde kendi gündüz renklerini giyiyordu. "Yapıştırılmış duruyor" şikâyeti buydu: nerede durduğu değil, barın üstünde akşamın dokunmadığı tek şey olması. Çözüm `DiegeticStage.RoomWashLight` — oda kendi cevabını zaten hesaplıyordu ve **hiçbir yerde okunmuyordu**, çünkü tek tüketicisi 2026-08-22'de silinen back bar sayfasıydı; artık tüketicisi kitap (`DressBookProp`, her kare). **Parlama zaten vardı ve görülemiyordu:** kitabın sanatı barın en parlak şeyi ve parlağın 1.22 katı yine parlak — ışıkla koyulaşınca aynı 1.22 okunur oldu, yani parlama için ayrı bir sayı gerekmedi ve odanın tek dili (`HoverGlow.Gain`) korundu. **`HoverGlow.Retint()` bu yüzden var:** dinlenme rengini DIŞARIDAN alıyor. Her kare `Image.color` yazan bir ışıkla, girişte rengi yakalayıp her kare yazan bir parlama aynı alanı sırayla eziyordu; artık ışık rengi değil DİNLENME rengini yazıyor, imleç uzaktayken ikisi aynı şey, üstündeyken parlama hâlâ odayla birlikte kayan bir zeminden parlıyor. **İpucu:** kitabın üstünde "OPEN THE MENU" plakası (`ChromeArt.Card()`, Night[1] üzerine Amber[4], 8 px), kepengin kendi 0.14 sn'siyle açılıp kapanıyor, kitap AÇIKKEN çıkmıyor. **Yer:** `BookPropX` −196 → **−336** (sahne x 222 → 152). 222 sayılarla soldaydı ama gözle tam ortaydı ve iki yanında boş tezgâh vardı — bir propu "yüzeye KONMUŞ" gösteren şey budur, "yüzeye AİT" değil. Barın çalışan nesnelerinin hepsi uçlarda (lavabo 140, fıçı 540, kasa 604); kitap da lavabonun yanına, elle alınan şeylerin olduğu uca geçti ve barın ortası içenlere kaldı. **HESAP TABUREDEN KALKAR (2026-08-25):** müşteri içkisini bitirip kalkarken bıraktığı yıldızlar (aynı `StarRow` cetveli, kesirli), ödediği para (display-24, beyaz+siyah çift kontur) ve varsa BAHŞİŞ satırı 3.2 sn boyunca (eski 1.6) yükselir — düz değil: yavaş bir sinüsle sağa sola savrulur (`TabSway` 26) ve savrulduğu yöne yatar (`TabLean` 7°), tabureden çıkarken bir pop yapar, ömrünün %62'sine kadar mürekkebini korur. Faz taburenin indeksinden gelir (rastgele DEĞİL). Havada hesap varken gün sonu gelmez. **ÜÇ İZ, TEK FİŞ DEĞİL (2026-08-25, yazar: "verdiği yıldız, para ve tip, arka arkaya çıksın ve birbirinden bağımsız hareket etsinler"):** üçü tek host'ta üst üste dizili değil artık; `TabFloat` üç ayrı `TabMark` başlatıyor — yıldızlar önce, para yarım saniye (`TabStagger` 0.5) sonra, bahşiş bir saniye sonra — her biri kendi fazı (`seat*1.7 + lane*2.3`), kendi tırmanışı ve kendi yatışıyla. **ÖNCE ÇIKAN EN YÜKSEĞE ÇIKAR** (`TabLaneClimb` {30, 0, −34}): ilk kesimde para yıldızlardan 14 birim DAHA yükseğe tırmanıyordu, bir saniye içinde onlara yetişti ve yıldız sırası "+$17"in üstüne oturdu (oyunda ölçüldü). Kural: her iz içeriğini host'unun ÜST kenarından astığı için iki iz ancak aralarındaki yükseklik farkı ÜSTTEKİNİN KENDİ BOYUNU aştığı sürece birbirinden temiz kalır — yıldız sırası 14, rakam 28. Ortak olan tek şey ÇIKIŞ NOKTASI: tabure kalkan müşteriyle birlikte odayı geçtiği için üçü de hesabın kapandığı andaki yerden fırlatılır, sırası gelince taburenin gittiği yerden değil. Hepsi `_tabFloats`'a VAAT edildikleri anda sayılır, göründükleri anda değil. Hiyerarşideki adları `TabStars` / `TabPaid` / `TabTip` — "Tab0..2" kitabın kendi sekmelerine çarpıyordu. **BALONUN BEYAZI ŞEFFAF (2026-08-25):** `ChromeArt.BubbleFill` alfası 0xDB (%86) — yalnız iç dolgu; kenar, ayak ve borunun eğimleri opak kalır. Borunun ETEK üç satırı `BubbleSolid` (opak): o satırların işi plakanın alt bandını SİLMEK, şeffaf silgi silmez (magenta kenar balonun ağzından görünürdü). **PERFECT BİLDİRİMİ (2026-08-25):** bir tarif İLK KEZ perfect oranda yapılıp servis edilince (`ServeSeat` Core'a servis öncesi/sonrası sorar; Core olay değil küme tutuyor) üç iz bırakır — platin renkli bildirim ("PERFECT POUR · <ad> — IN THE BOOK NOW", 3.4 sn, cheer_sfx), KİTAP tuşunda sayaçlı rozet, ve kitabın giriş sayfasında basılabilir satır (ad + folyo; basınca o sayfayı açar ve haberi okundu sayar). Bildirim kanalı artık renk+süre alıyor (varsayılan yine reddin kırmızısı) . **MENÜ TUŞU KALKTI (2026-08-25):** kepengin üstündeki plaka iki yazı taşıyordu ve kapalı hâli "MENU — MAKE A DRINK" diyordu — back bar sayfası 2026-08-22'de silindiği beri var olmayan bir menü, üstelik zaten kapının kendisi olan bir merdanenin üzerinde duran bir plakada. Yerine merdanenin ÜSTÜNE yazılmış **Open bar** ve altında aşağı ok geldi (`Items/sign_open`, `sign_open_arrow`; `Tools/open_sign_gen.py` çiziyor): italik duvar yazısı, üç kat — en dış magenta kontur, içinde beyazdan koyu pembeye geçen astar, en içte pembe gövde — ve yazı toplam **34 px** (yazarın tavanı). **YAZI YENİDEN YAZILDI (2026-08-25, yazar: "open yazısını değiştir istersen yazanı da değiştir"):** dördüncü el `wall` artık varsayılan — daha az eğim, daha geniş harf aralığı, daha yuvarlak kâseler; tavan yükseklikte olduğu için genişlik hiç harcanmamıştı (merdane 592 px, yazı 90'dı), o yüzden ikinci kelime bedava geldi ve okun zaten söylediğini tekrar etmeyen bir şey söylüyor. **KALEM DE DEĞİŞTİ (2026-08-25, yazar: "daha miami vice fontunu andıran bir font ile Open bar yazsın"):** `wall` de geri gönderildi, çünkü yukarıdaki dört elin dördü de AYNI eldi — yuvarlak uçlu bir keçeli kalemin sürüklenmesi, yani el yazısı. İstenen şey o elin beşinci sürümü değil, öbür tür harf: dizilmiş harf. Yeni el `vice` varsayılan ve kalem kullanmıyor — harfleri DOLU ŞEKİLLERDEN kuruyor (dikdörtgen, halka, kama), o yüzden her gövde paralel kenarlı ve her uç DÜZ kesik; eğim 0.21 (afişin ölçülü italiği), ve harfler BÜYÜK. Büyük harf, yukarıdaki dört elin kaybettiği kavgayı da bitiriyor: 34 px tavanda kâseleri ilk yiyen şey katlardır, küçük harf ise boyunu x-yüksekliği, kâmet ve alt uzantı arasında bölüştürür — büyük harf hepsini tek banda harcar, yani O'nun içi işaret bir piksel bile büyümeden yarı yarıya daha açık. İKİ KALINLIK var ve ikincisi zorunlu: dikeyler 5 px, yataylar 3 px. Tek kalınlıkta B kapanıyor — bir kâmet içine yığılmış iki kâsenin her birine astar ve kontur iki yandan 2 px giriyor, 29 satırda o kadar yer yok. Yataylar incelince kâseler 8 satır kalıyor ve dördü hayatta kalıyor; bu bir kaçamak değil, afişin kendi modülasyonu. Üç kat aynen duruyor (brief odur); `vice_cyan` aynı harflerin astarı beyaz yerine CYAN olan hâli, seçilmedi, kontak föyünde duruyor (`py -3 Tools/open_sign_gen.py --takes`). Yazı 181×33 (tavan 34). Yazı BUTON DEĞİL: merdane zaten bir çarpma plakası taşıyor, mahzenin tuvali kapalıyken raycast almıyor, tıklama oraya düşüyor. Yazı merdaneyle birlikte AŞAĞI İNER ve inerken solar. **SHUT IT DE KALKTI (aynı gün):** mahzeni kapatan şey artık mahzen olmayan HER YER — tam ekran görünmez bir yakalayıcı (`CellarCatcher`, mahzen tuvalinin İLK çocuğu, dolayısıyla ray'de en son sorulan) kapağı kapatır; rafların yüzü (`ShelfGuard`, tezgâh sanatının 65..241 satırlarından ölçülü) onun ÜSTÜNDE oturur ve oraya düşen tıklamayı yutar, böylece iki şişe arasını ıskalamak bir şişeye mal olur, odanın tamamına değil. Kitap ve kimlik yıllardır bu "dışarı tıkla" kalıbını kullanıyordu; mahzen de artık onu kullanıyor. **KAPAK NEFES ALIR (aynı gün, yazar: "mouse ile gelindiğinde sadece kapak biraz yukarıdan aralanır ve aralanan yerden ışık çıkar"):** imleç merdanenin üstüne gelince kepenk kendi yönünde — AŞAĞI — 7 birim kayar ve açılan yarıktan `Items/light_spill` (592×16, uçları sönen sıcak tungsten, düz alfa bantları) sızar; yarık 0.14 sn'de açılıp kapanır, çekmece açılmaya başlayınca harcanır (`1 - _drawerT`), Motion.Reduced'da anlıktır. **YAZI DA İNER (2026-08-25, yazar: "open tuşu da sanki kapağın üstündeymiş gibi aşağı inmeli"):** yazı slatların ÜSTÜNE boyanmış, o yüzden kepenk eğildiğinde onunla eğilir — ama PLAKA eğilmez: imleci hisseden dikdörtgen imlecin altından kayarsa üst 7 birim girip çıkar ve merdane 0.14 sn'de bir titrer. Plaka merdanenin dinlenme yerine çakılı, yalnız iki tabela biniyor. **AÇIKKEN KEPENK RAY BIRAKIR (aynı gün, yazar: "aşağıdan çok az kapak gözüküyor bu gözükmeyi arttırıp gözüken kısıma üst ok görseli koyalım ... mouse ile üstüne gelindiğinde biraz daha kapansın"):** açık çerçevenin sillde bıraktığı şerit 6 birimden **16**'ya çıktı (`ShutterRail`; `ShutterTravel` artık ondan TÜRETİLİYOR — 120+2−65+121−16 = 162). Tavanı raf koyuyor: çekmece kalkıkken alt rafın tahtası ekran tabanından 15, üstündeki şişelerin ayağı 10 birim yukarıda, dolayısıyla daha yüksek bir ray "önde park etmiş alt profil" olmaktan çıkıp "açılamamış kepenk" olur. Şeride merdanenin kendi üç katındaki AYNA çevrilmiş chevron basılı (`Items/sign_shut_arrow`, `arrow(up=True)` — Unity'de negatif ölçekle çevrilmiş sprite piksel ızgarasından düşer, o yüzden ayna Python'da vuruluyor) ve `_drawerT` ile açılıp `1 - _drawerT` ile solan Open yazısının tam tersi biçimde belirir. Şerit KENDİ çarpma plakasını taşıyor (`ShutterRail`, mahzen tuvalinin SON çocuğu — `CellarCatcher` ve `ShelfGuard`'ın üstünde, çünkü ShutterDoor tuvali (6) mahzeninkinin (7) altında sıralanıyor ve açıkken oraya düşen her ray yakalayıcıya gidiyordu); kapanınca deaktive olur ve `_shutterHovered` elle temizlenir (deaktif olan plaka OnPointerExit almaz). Nefes artık İKİ yönlü: kapalıyken aşağı, açıkken YUKARI — tıklamanın götüreceği yön hangisiyse kepenk oraya yaslanır |
| **Üst şerit (2026-08-19 redesign, aynı akşam ÜÇ tur)** | Kiriş kenardan kenara; üstünde iki YUVA (ChromeArt.Well — kirişe gömülü oyuk: üst kenar karanlık, alt dudak ışıklı, taban = ekran camı) ve serbest duran yıldızlar: (1) SAAT — yuvada elle çizilmiş 11×14 piksel maske rakamlar 2×'te (SegmentClock; tasarım+kanıt Tools/clock_digits.py; hayalet 8 + halo + kolon); (2) HAFTA — aynı yuvada başta WEEK sayacı (display-16 cyan), sonra 7 gece: **kelime lambadır** — bu gecenin adı amber yanar ve altında minyatür neon boru (hikâye gecesiyse magenta), CMT'nin işareti her hafta magenta yıldız, PAZAR kepenk, geçmiş günler sönük cam, ilerisi Cream[3]; ampul sırası ve tel emekli; (3) YILDIZLAR — kutusuz beş **3D altın yıldız** Items/star3d.png (32px @1×, PixelLab, luma-sıralı Amber/Malt eşleme) + koyu cam soket, dolgu maskesi okumadır, SAYI YOK; kalabalık başlığı üstte; çark tuşu −16'da. PixelLab takvim plakası tek build yaşadı ve geri alındı; ViceFade dolgusu da. Neon boru durum ışığı (amber→magenta). |
| **Kimlik kartı** | tabure tıkla → `InspectId()` (kapı!); sipariş satırı hover=**kutu kartı** (2026-08-20: beş kutulu bar, yalnız mükemmelin kutusu yanık; kesin sayı ancak sayfa mükemmellenince) |
| **Tarif kitabı** | **AÇIK KİTAPÇIK (2026-08-24):** `menu_booklet.png` tam 2× (740×708 HUD), iki dik sayfa (167×326 sanat px); altın sayfa takımı (`menu_page_frame`) yazıyla AYNI kapta yaşar, katta birlikte kırpılır. Sayfa çevirme ÇİZİLMİŞ 16 kare (`menu_page_00..15`, soyulma modeli, 40ms/kare; geri = aynı kareler tersten; Reduced anlık): ön baskı katta KIRPILIR, arka yüz TAM SAYI kaydırılır, hiçbir şey ölçeklenmez (cetvel: `Tools/menu_booklet.py`). **YEMEK KİTABI DÜZENİ (aynı akşam):** ilk forma = başlık plakası + İÇİNDEKİLER (bölüm satırı tıklanır → o sayfaya atlar); sonrası TARİF BAŞINA TAM SAYFA — tier künyesi, ad, hazırlık·bardak, içki ikonu, gösterge LEJANDI (bar neyi ölçer + hangi renk hangi %20'lik dilim), tam genişlik doz satırları (`BkGaugeW` 102×14), en altta içkinin tarihçesi + köken·fiyat satırı (`Resources/Data/recipes_lore.json` ↔ `RecipeLore`, katalogla iki yönlü test altında). Kilitli tarif: sayfası soluk + kapı plakası; dökülemeyen şişe adının ALTINDA "LOCKED · NOT IN THE WELL" der. **YILDIZ KAPILARI ÇİZİLİR (2026-08-25):** tek yardımcı `StarRow` (beş yuva, per-star `Image.Type.Filled`, yarım rung yarım yıldız) üç yerde — mağazanın mühürlü etiketi (kasa/şişe/fikstür/koridor kapılarının hepsi bu tek çiziciden geçer), kitabın kapı plakası (aynı cetvelde iki satır: OPENS AT ne ister, YOU HAVE bar nerede) ve indeksin kilitli satırları. Yıldızla ilgisi olmayan kilit (kule basamağı, kişi beat'i) cümlesini korur. PERFECT sayfa: gösterge yerine KESİN SAYI + PERFECT etiketi, platin çift çerçeve, sağ üst köşede −45° "PERFECT RECIPE" kurdelesi. **İÇİNDEKİLER BİR TARAYICI (2026-08-25):** üstünde arama kutusu (ada göre, 15 sonuç; yazarken ok tuşları sayfayı çevirmez), bölüm satırına tıklayınca AYNI SAYFADA o bölümün tüm tarifleri açılır (ad + folyo + tıkla-git, kilitlinin yanında LOCKED, "< ALL CHAPTERS" ile geri); her tıklanabilir satır hover'da amber yanar. İndeks satırları `&` yerine AND basar (gövde yazı tipinin ampersandı 16'da `$` okunuyor). Başlık sayfasının kokteyli `menu_cover_drink.png` — `Tools/menu_cover_drink_gen.py` ile 64 sanat px üretilip 40 renge quantize edildi. Çevirme: alt dış köşeler + görünür `<` `<<` `>` kâğıt tuşları (ilk/son formada saklanır) + ←/→; kurdele formayı tutar. Kitap açıkken saat `BookTimeScale` 0.05 (servis menüleri 0.3 kalır). ARAMA VE FİLTRELER PANOYLA EMEKLİ; `menu_board` silindi. Diğer üç pencere (kimlik hover, market spec, sipariş balonu) `DrawRecipeSpec`'ten çizmeye devam eder; kitap sayfası kendi çizerini kullanır ama AÇIĞA ÇIKARMA KAPISI aynıdır: kesin sayı yalnız `RecipeSpecRows`→Core (`IsPerfected`/`ExactPourFor`) söylerse basılır |
| **Gün sonu** | **GECE RAPORU (2026-08-25 yeniden tasarım):** ekranın ortasında hesap fişi (para), İKİ YANINDA gecenin iki ALETİ. **Gün, oda boşalmadan gelmez:** Core zaten son taburenin boşalmasını bekliyordu, ama ÇIKIŞ YÜRÜYÜŞÜ HUD'ın — perde eskiden son müşterinin tepki anını ve kapıya yürüyüşünü örtüyordu; faz dönüşü artık kitapları yalnız SİLAHLANDIRIR (`_dayEndDue`), gerçek açılış `FloorIsClear()` (ekranda kimse yok + havada sayılan hesap yok) veya 9 sn emniyet süresi. **SOL — THE WEEK:** haftanın altı gecesi + PAZAR; oynanmış geceler `Ledger.History`'den yıldızı (aynı `StarRow` cetveli) ve NET parasıyla, bu gece amber plakada yanar, ilerideki geceler beş BOŞ yuva, CMT'de her hafta magenta VIP yıldızı, PAZAR kepenk + CLOSED; altta haftanın toplamı. **SAĞ — AFTER TONIGHT:** barın merdiveni — beş 40px yıldız + iki haneli sayı, altında 0→5 ölçekli gösterge (ChromeArt.GaugeTube/GaugeGlass): amber dolgu barın durduğu yer, SOLGUN bant kazanılan/kaybedilen dilim, beyaz çentik gecenin başındaki duruş, cyan çentik bir sonraki basamak; yanında WAS x.xx ve ok'lu delta çipi (+0.12 / −0.60 / HELD). Altında üç okuma: TONIGHT (gecenin kapalı yıldızı), CEILING (fikstür+menü tavanı; oda tavanı aştıysa kırmızı ve "buy the fittings"), TOMORROW (yarının kalabalığı). **Sayılar KURALDAN gelir, ekran hesaplamaz:** `BarRating.StandingAfter` (CloseNight'ın aynı üç satırı), `TycoonRun.TonightStars/StarCeiling/StandingAfterTonight/CrowdTomorrow` — hepsi kitaplar kapanmadan sorulur, `NightReportTests` sor-sonra-kapat diye pinler. Beat sırası: 1 çağrı → 2 kâğıt beslenir (aletler kendi kenarlarından girer) → 3 yıldızlar fişe düşer + damga → **4 duruş tırmanır** (1.1 sn, sayı+yıldız+gösterge birlikte) → ancak o zaman GO TO THE ORDER. Fişin başlığı 8 birim aşağı indi (DISGRACE damgası tarih satırını kırpıyordu) ve fişten "BAR x.x" kalktı — o okuma artık sağdaki aletin işi. Sonra market (4 sekme: DOLUM/ŞİŞELER/TARİFLER/YÜKSELTMELER + bu gece alınanlar iade) **ODA ÖNCE TEMİZLENİR (2026-08-25, yazar: "oyun sonu ekranı gelmeden önce açık olan tüm pencereler kapanır ana sahneye dönülür ... aynı şekilde gün başlarken de ekran ana ekran haline gelir ve temizlenir"):** `CloseEverySheet()` — kitap (SERT kapanır: kaydırma ve sayfa çevirme coroutine'leri durdurulur, panel anında gider; inen scrim'in altında yolculuğunu sürdüren bir sayfa tam da önlenmek istenen şeydi), ayarlar, geliştirici tezgâhı, rehber, defter, kimlik, servis akışı ve mahzen kapağı (anında). **Gece BİTERKEN çağrılır, kitaplar gelirken değil (2026-08-25 ikinci tur):** faz dönüşünün kendisinde — yani son müşteri hâlâ kapıya yürürken oda çoktan çıplaktır; `ShowDayEnd()` bir kez daha çağırır (araya girip bir şey açan olursa) ve `OnOpenTomorrow()` da, yani ertesi gece de temiz bir odada açılır. Gece artık yarım okunmuş bir tarifin, açık bir kimliğin ya da tin'inde içki kalmış bir tezgâhın ÜSTÜNDE sayılmıyor. |
| **Gün başı (perde)** | **GÜN GEÇME SAHNESİ (2026-08-25, yazar: "güneşin doğudan çıkıp battığını ve şu anki saate geldiğini gösteren bir gün geçme animasyonu, saati de tam 18:00'a saran — KCD2'deki uyku ekranı gibi").** Eskiden 6 sn siyah + hafta/gün kartıydı; artık barın KAPALI OLDUĞU on altı saat oynanıyor: 02:00 → 18:00. Kart 700×520; en üstte 640×220 GÖKYÜZÜ PANELİ (RectMask2D — güneş ufkun ARKASINDAN doğar ve halesi kartı basmaz), altında saat, gün adı ve marki. **Gökyüzü BANTLI:** 20 düz satır, tepe ile ufuk arasında `k^1.6` ile karışır (sıcak uç ufka yapışsın diye); renkler yalnız palet token'ları ve yedi saat anahtarı — 02 gece Night[0/2], 05 ilk ışık ClubBlue[1], 06:30 şafak Amber[3], 08 ve 13 gündüz ClubBlue[3/4]+Cyan[4], 16 ikindi Amber[4], **18 altın saat Magenta[2]+Amber[3] = odanın penceresinin zaten taşıdığı renk.** **Şehir, güneş ve ay ÜRETİLMİŞ SANAT (aynı gün, yazar: "kullanılan mevcut görsel profesyonelce durmuyor, gerekirse görsel ve animasyonu üret"):** ilk kesimin prosedürel kutu-kuleleri programcı sanatı okundu ve kesildi. `Tools/day_sky_gen.py` üç parçayı PixelLab'dan üretip 40 renge quantize eder — `Scene/curtain_city.png` (320×96: körfezin karşısından Miami silüeti, yanık pencereler ve iki palmiye gömülü; panelde tam 2×), `curtain_sun.png` (32) ve `curtain_moon.png` (24, hilal — eski iki-disk ısırma numarası emekli). GÜNEŞ VE AY ŞEHRİN ARKASINDA çizilir: kulelerin arkasından doğar, arkasına batar. Silüetin tintı parlak saatlerde 1'in ÜSTÜNE itilir (Image tint yalnız çarpabilir; sanat bilerek koyu üretildi) — öğle göğünün altında zifiri bir şehir resimde delik gibi dururdu. YILDIZLAR ilk ışıkta söner (her biri kendi fazında titrer); haleler oyunun kendi LampGlow'u, ALFAYLA açılır, boyutla değil. **Saat oyunun kendi `SegmentClock`'u**, kirişteki hâlinin iki katı (yani sanatın 4×'i — tam kat) ve beşer dakikada okur. Hepsi TEK saatten sürülür: kendi zamanlayıcısında geçen bir güneş ile ayrı sarılan bir saat, aynı anda oynayan iki animasyon olurdu. **Ritim (7.0 sn):** 0.45 kart gelir → **3.60 gün geçer** (gün adı devri bu fazın ilk yarısında) → 1.25 saat 18:00'da durur → 1.70 kart çıkar, oda açılır. `Motion.Reduced` doğrudan 18:00'a oturur. Bir gün geçme sahnesi bundan uzun olursa dinlenme olmaktan çıkıp bekleme olur. **KARTIN TAKVİMİ ARTIK KİRİŞİN ALETİ (2026-08-25, yazar: "Gün başlangıç ekranındaki takvim göstergesini beğenmiyorum bunu geliştir, ana sahnedeki üst bardaki takvim göstergesine benzer yapabilirsin"):** kartın altındaki marki — tel, yedi sap, her gecede bir ampul — üst şeridin ÜÇÜNCÜ kesimde zaten "bunting gibi duruyor" diye attığı resmin ta kendisiydi. Aynı yedi gece olduğu için artık aynı alet: `BuildWeekStrip` ikiye ayrıldı — `BuildWeekGlass` camı, başlığı ve yedi yuvayı kuruyor, `LightWeekCells` onu yakıyor; kiriş 1× ölçekte, kart 1.4×'te (454 birim cam → 636, kartın 700'üne değmeden) `CurtainWeekY` −452'de asıyor. Yakma TEK sayı ile iki montajı da taşıyor: kiriş `over`=1 ve `leaving`=−1 geçer (tek gece, tam yanık), kart ise DEVİR yapar — dün gece, gün adlarının yer değiştirdiği eğrinin (`e`) tam aynısında söner, bu gece aynı eğride yanar. Kartın tepesindeki ayrı "WEEK 3" satırı kalktı: alet kendi başlığının altında kendi sayacını basıyor, hafta iki kez söylenmiyor. |
| **Back bar (menü)** | **İÇECEK SEÇMENİN TEK YERİ (2026-08-13).** Duvar garnitür VE BİRA dışında her şeyi taşır — gazlılar dahil. **Bira duvarı terk etti (2026-08-15):** fıçı satırı kaldırıldı, draught'un tek kapısı tezgâhtaki bira musluğu (aşağı). Şişe hover=bilgi kartı, tıkla=rota (garnitür anında tin'e tutam; gazlı→Serve eline; kalan→Shaker eline). Kapalı şişe kendi kabına bakar: gazlı SERVİS BARDAĞI dolu diye kapanır, kalanı tin dolu diye. Sahne geçişleri KAYAR (ileri sağdan, geri soldan; açılış fade, kapanış anlık); her istasyonda sol kenar BACK TO BAR |
| **Shaker** | Elde tek şişe, tin, kapak, kaşık — **tezgâhta içecek rafı YOK (2026-08-13)**; başka şişe için back bar'a dönülür. Şişeyi kaldır-yatır dök (akış şişenin ÖLÇÜLEN kapağından çıkar, 2026-08-11); AÇIK tin'de kaşıkla daire=karıştır; kapağı tak; tin'i savur=çalkala; kapalı+karışık → sağ kenar TO THE GLASS. **Kaşık ÇİZİM artık (2026-08-25):** `bench_spoon` — burgu saplı bar kaşığı, 32×128 sanat tam 2×'te, kâse aşağı (üretim kâse-yukarı geldi, sevkte çevrildi); üç gri dikdörtgen sanat yoksa yedek olarak durur |
| **Serve** | shaker'ı NİŞANLA dök (kaçırırsan döker); **dolap/raf YOK (2026-08-13)** — buradaki tek şişe back bar'ın elimize verdiği gazlıdır (Core tin'de reddettiği için bardak onun tek kapısı), düğme basılı gelmediğinden **elde DURUR**, basınca kavranır; hazırlık kapları tezgâhın sol ucunda; SERVE tuşu bardak boşken sönük. **HAZIRLIK REWORK'U (2026-08-25):** (1) **TUZ/ŞEKER BİR BECERİ:** tabağa bas-tut, imleçle bardağın AĞZININ ETRAFINDA tam bir tur çiz (kaşığın işaretli-süpürme aritmetiği, doksan derece döndürülmüş; yön fark etmez; ağzın 34–190 birim bandı dışına çıkmak turu duraklatır, dökmez) — ağız çevresinde 14 dilimli halka turu gösterir, yarım kalan tur rafta "SALT %60" diye bekler, tur tamamlanınca `AddPreparationAtGlass` aynı Core fiiliyle işler. (2) **BUZ SAYILIR:** kova hiç 'bitti'ye dönmez, her sürükle-bırak bir küp ekler (`GlassContents.IceCubes` — adım listesi tekilliğini korur, hakem yine 'buz var mı' diye bakar; küpler `TransferInto` ile içkiyle taşınır) ve küpler bardağın İÇİNDE sıvı çizgisinde yığın olarak çizilir (GlassDecor, 7 çizim tavanı, el dizilimi tablosu — kaynayan buz olmasın diye sabit). (3) **SERVİSTE TEZGÂH SIFIRLANIR:** SERVE veya sahneden çıkış `ResetServeHand` — eldeki tabak, sürüklenen parça, halka ve yarım turlar temizlenir. **Kaplar üretilmiş sanat (2026-08-25, tek take):** `bench_dish_salt/sugar` (tur atılan sığ tabaklar), `bench_bucket_ice` (küpleri görünen açık kova), `bench_bowl_lemon` — `Tools/bench_props_gen.py`, quantize zinciri; eskiler yedek olarak duruyor |

**TEZGÂH ODANIN TEZGÂHININ ZOOM'U (2026-08-25, yazar: "ekran çok boş gözüküyor, mevcut tezgahın görseline zoom yapılmış gibi gözükmeli"):** üç tezgâhın bandı artık `counter.png`'den ÖRNEKLENMİŞ renklerle çizilir — slab #1F1924, sırt #312E3A, dikiş #17141C ve uzak kenarda odanın magenta neon rayı (#D77BBA→#372536 altı basamak, 5'er birim = ~4× zoom) + slab'da tek sheen bandı. Bantlar prosedürel (14 §3), zoom'lu piksel yüzeyi zaten düz renk koşularıdır. **ANA TEZGÂHTA MİNİ İSTASYONLAR (2026-08-25, yazar: "servis et dedikten sonra buz limon şeker koymayı unutursa diye"):** yapılmış içki ana tezgâhta dururken sağında dört istasyon belirir (`bench_mini_*` 32px sanat 2×'te, tezgâh boyuna oranlı): tıkla=uygula — burada beceri YOK, tezgâh af kapısıdır; buz yine sayılır, diğerleri uygulanınca söner; sıra `CounterLift`'e biner (çekmece açılınca havada kalmaz) ve içki eldeyken/serviste/akış açıkken gizlenir. **Her iki tezgâhın seti (2026-08-13):** ekranda mobilya assetı yok — `prep_table` ve `bar_mat` kaldırıldı. Panelin kendisi tezgâhtır: arkada barın kendi duvarı (`BackBarArt.LuxeWall`, gölgede), önünde bir ton açık tezgâh bandı ve buluştukları yerde aydınlık ön kenar; üstünde duran her şey `BackBarArt.BottleShadow` ile temas gölgesi taşır (tin ve şişeninki her kare kendi tabanını takip eder, kaldırınca söner). Yüzeyin kendisi çizilmez — `PourSurface`/`ServeSurface` sadece koordinat uzayıdır.
| **Tap** | **KAPISI ODADAKİ MUSLUK (2026-08-15).** Tezgâhta duran bira musluğu fikstürüne tıklamak doğrudan bu sahneyi açar (`DiegeticStage` plakası → `TycoonServiceFlow.OpenTap`); 1. seviye musluk (`taps_one`) bar ilk geceden **zaten sahibi** — mağazada OURS yazar, satılmaz, geri verilmez. **Musluk artık üç basamaklı bir MERDİVEN (2026-08-19, §6.1).** Kimse fıçı seçmeden gelindiği için mahzen kendisi bağlar: raf sırasında ilk dolu fıçı. **Font odanın kendi musluğunun BÜYÜMÜŞÜ (2026-08-25):** `bench_tap_big` 120×240 sanat tam 2×'te (240×480) — art deco pirinç kolon, krom musluk; gömülü kol sevkte silindi (tek rig iki kol taşımaz), animasyonlu `tap_handle` ölçülen yuvaya (−16,+82) monte; musluk ağzı sanattan ölçüldü (−79,+66), tezgâh çizgisi 30 indi (−170) ve fıçılar onunla (−345). Bardağı yatır-doldur, dikleştir-köpük; verdikt satırı canlı; **tezgâh altı gerçek mahzen (2026-08-13)**: hatta bağlı fıçı + stoktaki diğer fıçılar kendi gözlerinde, birine tıkla=onu hatta bağla (Core `CanPull` reddederse hiçbir şey değişmez ve nedeni yazılır); dökerken pour_loop sesi; SERVE tuşu bardak boşken sönük |

Teknik: sahne 640×360 (PixelPerfect), HUD 1280×720; tüm UI kodla kurulur, prefab yok; yalnız yeni Input System (`Mouse.current`).

**TIKLANABİLİR HER ŞEY İMLECE CEVAP VERİR (2026-08-25, yazar: "etkileşime girilebilir her buton veya nesne mouse ile üstüne gelince hafif parlamalı").** Üç lehçe var, üçü de kasıtlı: **tuşlar** `PressSink` ile kalkar-şişer-ısınır (`KeyPlate.Dress`'ten geçen her şey); **market** `HoverWarm` ile yalnız ısınır (döşemesi her yeniden kurulumda değiştiğinden rect oynatan bir bileşen kodla kavga ederdi); **odadaki nesneler** yeni `HoverGlow` ile kendi ışığını yakar. Üçüncüsü bu tarihte yazıldı: tıklanabilir şeylerin yarısı tuş değil — mahzendeki şişe, üstünde biri olan tabure, garnitür kavanozu, kasa, bira musluğu, tezgâhtaki kitap — ve her biri ŞEFFAF bir çarpma plakasının altındaki bir ÇİZİM: kaldırılacak yüz, ısıtılacak plaka yok. `HoverGlow` plakadan hedefin KENDİ rengini 1.22× parlatır (musluğun 2026-08-15'te elle yazdığı sayı, artık tek kaynak), SpriteRenderer'a da Graphic'e de ulaşır (dünyadaki gövdeler bu yüzden), dinlenme rengini KURULUŞTA değil İMLEÇ GELİNCE okur (nesneler ışıklandırılıyor), ve ALFAYA hiç dokunmaz — solmakta olan bir müşteriyi hover'lamak onu yarı saydam dondururdu. **BİTKİLER İKİ MERDİVEN OLDU (2026-08-25, yazar: "bitkiler güzel alternatiflerini de üret farklı vazo ve bitki çeşitlerini üret aynı tarzda ... mevcut yeni üretilen bitkileri upgrade kısmına koy eskilerini kaldır"):** oda iki bitki taşıyordu ve ikisi de yükseltme DEĞİLDİ — solda eğrelti, sağda monstera, birer basamak, bir kez alınıp bir daha iyileştirilemiyordu. Beş yeni bitki iki yuvayı da merdivene çevirdi (lavabonun iki, duvar lambalarının üç basamağı gibi): `plant_left` **palmiye $20 → kemanyaprağı $55 → sarmaşık $95**, `plant_right` **paşakılıcı $25 → agav $70**. Bölüşüm keyfî değil: sol yuva (x 20) pencere yanındaki derin köşe, sağ yuva (x 616) kasanın dibindeki bar ucu — uzun bitki köşede okunur, kasanın önünde engel olur, o yüzden üç DİK bitki sola, iki alçak ve geniş olan sağa. Beşi de `create_image_pro` ile üretildi ve beşinin de renk referansı **odanın kendi monsterası** oldu: ilk turda prompt yaprağı üç yeşil rampa adıyla istediği hâlde dört adayın dördü magenta-turkuaz geldi, çünkü `palette_miami.png` içinde Lime yok ve **plaka metni yeniyor**. Ortak plaka genişletilmedi (sonraki her sahne çağrısını sessizce yeniden renklendirirdi); varlığa özel referans verildi (`vice_room_gen.PALETTE_OVERRIDE`). Eski sanat SİLİNMEDİ, yalnız listeden çıkarıldı — `fx_monstera.png` bu beşinin üretildiği renk çıpası, silmek onları yapan aracı bozardı. **İTHALAT KURALI DA BÜYÜDÜ:** `LastCallImporter` yalnız `Assets/Art/` ve `Resources/Scene/` kapsıyordu; `Resources/Fixtures/` KAPSAM DIŞIYDI ve oradaki her .meta elle ayarlanmıştı, yani .meta'sız inen beş yeni PNG bilinear PPU 100 olurdu — odanın sanat pikseliyle ölçülen dünyasında yüz kat küçük ve bulanık. Kural artık o klasörü de kapsıyor; `Resources/Items/` bilerek dışarıda, orası canvas sanatı ve PPU 100. **MAHZEN ŞİŞEYİ KÜÇÜLTMEZ, ARALARINI KISAR (2026-08-25, yazar: "raftaki alkolleri sığdırmak için boyutları değişmemeli gerekirse aralarında 1 pixel kalıcak kadar yakınlaşsınlar ama boyutları değişmesin"):** eskiden raf, EN GENİŞ şişe kendi eşit yuvasına sığana kadar bütün rafın boyunu düşürüyordu — yani geniş omuzlu bir rom satın almak bardaki DİĞER her şişeyi sessizce küçültüyordu, ve 31. şişeden itibaren herkes 62'den 58'e iniyordu. Artık boy `CellarBottleH` = **62 sabiti**, her zaman. Esneyen şey ARALIK: yuvalar eşit pay olmaktan çıkıp şişenin KENDİ çizili genişliği oldu (kataloğun en geniş şişesi en darının iki katı — eşit yuva, şişman bir şişenin yerini ince birine harcayıp faturayı bütün rafa kesiyordu), göz artan havayı eşit dağıtıyor ve doluyken **1 px**'e kadar iniyor, daha aşağı değil. Altı bölme (iki tahta × üç göz) kalanı EŞİT paylaşıyor, sığmayan bir bölme fazlasını bir sonrakine devrediyor, hiçbir şişe gözünün dışına taşmıyor. Oyunda ölçüldü: 29 şişe (bütün katalog) hepsi 62 boyunda, en dar aralık 10.4 px; 42 şişede yine hepsi 62, en dar aralık 2.8 px, taşan 0 — eski kod 42'de 36'da kesip kalanı hiç çizmiyordu. `CellarSlots` 36 → **48**. Kalan çıplak butonlar bilerek çıplak: scrim'ler, yutucular, kitabın görünmez sayfa köşeleri (görünür `<` `>` kâğıt tuşları artık parlıyor) ve zaten her kare parlayan çöp kovası.

**Kaplar sayfadan değil ÇİZİMDEN ölçülür (2026-08-11, `VesselArt`; GDD 15 §8):** şişe/karton
sahnenin verdiği boyda, kendi çiziminin ölçüsüyle durur — ayakları tezgâhın/rafın çizgisinde,
ortası işaretinde; kendi yüksekliğinin 0.44'ünden geniş olan kap ENİNDEN sığdırılır (karton
şişenin yanında karton kalır). Döküm ağzı da ölçülür: kapaklı ve kapaksız çekim aynı sayfadaysa
kapak, iki çekimin AYRILDIĞI piksellerdir (kartonun ağzı düz çatıya oturan bir güdük, siluetin
tepesi değil). Şişede kalan sıvı çizimin kutusuna göre doldurulur; opak kap (karton, kutu)
seviyesini doğası gereği göstermez — sayı hover kartında ve market kutucuğunda.

### 9.3 · Oda temizlendi, lavabo iş aldı (2026-08-26)

Yazarın altı maddesi, tek turda.

- **KASA VE PARA ANA SAHNEDEN ÇIKTI** ("kasa ve parayı ana sahneden kaldır"). Register
  iki kendi tuvaliyle (−7 ve 6), üstündeki altın bakiye, çekmeceden kalkan +$/−$ süzülmesi
  ve tıklandığında açtığı defter — dördü birlikte gitti; bir sayı taşımayan makine dekordur,
  altında makine olmayan sayı ise bu makinenin yerine geçtiği fasya göstergesidir.
  `DiegeticStage`'den `BuildRegister/SetMoney/SetMoneyInDebt/FloatMoney/SetRegisterHandler`,
  `registerSprite`, `RegisterX/RegisterBaseY` ve ekran penceresi kesirleri silindi;
  `DebugSceneCreator`'daki sprite ataması aynı gün gitti (var olmayan alana `FindProperty`
  null döner ve patlar). **Vardiya boyunca hiçbir yerde bakiye yazmaz** — gecenin hesabı
  fişte, harcarken de market tabletinde okunur. **Borç yine görünür:** fasyanın neonu artık
  ÜÇ hâl söylüyor (vardiya amber · son sipariş magenta · eksideysen vice kırmızısı) ve
  eksi, son siparişi yener; tek yazar, tek önbellek (`_beamState`) — kule rengini saatin
  kendi değişim kontrolünün içinde boyamak, ikinci bir yazarla birlikte "hangisi son
  oynadıysa o" demek olurdu. **Defterin kapısı** çarkın arkasına taşındı (ayarlarda
  "TONIGHT'S BOOK"): bara başka bir biçimde geri konmadı, çünkü kaldırmanın bütün amacı
  servis ederken kimsenin sana para saymaması.
- **ÇÖP KUTUSU GİTTİ, YERİNE LAVABO** ("çöp kutusunu da kaldır, çöp kutusu yerine lavabo
  kullanılacak"). Tezgâhın sağ ucunda yarısı kadrajın altında duran çelik kuyu, odanın
  ZATEN sahip olduğu (ve marketin iki basamağını sattığı) bir fikstürün işini yapan
  uydurma bir nesneydi. Fiil değişmedi — yapılmış içki tıklanarak dökülür — yalnız neye
  tıklandığı: `fixtures.json`'daki **`drain: true`** bayrağını taşıyan parçaya. Bayrak
  DATA: `DiegeticStage` musluğun çarpma plakasını hangi kuralla asıyorsa lavabonunkini de
  o kuralla asıyor (`BuildPropDoor`, eski `BuildTapDoor`), affordans propun kendi
  `HoverGlow`'u. `bin_well.png` ve `BinW/BinH/IsOverBin` silindi.
- **ÜST SEVİYE LAVABO ZARARI SIFIRLAR** ("üst seviye lavabo alındığında dökülen
  içkilerden zarar elde edilmeyecek, başlangıç lavabosunda içkiyi çöpe attığında para
  yiyeceksin"). `sink_brass` **`drainsFree: true`** taşır; `TycoonRun.WasteIsFree` sahip
  olunan katalogda böyle bir parça var mı diye sorar ve `WriteOffVessels` yazmayı atlar.
  Kural basamak NUMARASINA değil PARÇAYA bağlı — üçüncü bir tekne ya da başka bir yuvadaki
  bir gider içerik olur, kod olmaz. Fikstürsüz kurulan koşu (bütün tezgâh kurulumları ve
  eski süitlerin çoğu) ücreti ödemeye devam eder; `DrainTests` bu sınırı çiviler. Bu, barın
  NE YAPABİLECEĞİNİ değiştiren ilk döşeme parçası.
- **MERDİVEN BİR BASAMAK İLERİSİNİ GÖSTERİR** ("3. seviyeye geçmek istiyorsan önce 2.
  seviyeyi açmalısın ve 3. seviye 2. seviyeyi açmadıysan gözükmemeli"). Kural zaten Core'daydı
  (`CanBuyRung`, tek basamak); değişen VİTRİN: mağazanın DRESSING koridoru her merdivenin
  her basamağını aynı anda diziyor, ulaşılamayanları "LOWER MARK FIRST" mührüyle
  gösteriyordu — üç bitki, üç kule, üç lamba, hepsi bu gece alınamaz. Artık sahip olunanlar
  ve alınabilecek TEK basamak görünür; gerisi merdiven tırmandıkça gelir. Basamaksız
  parçalar (tek yuvalı döşeme) değişmedi.
- **MENÜ LAVABONUN SOL OMZUNA** ("menüyü lavabonun sol yanına getir"). Kitap sahne x
  152'de duruyordu, yani teknenin AYAK İZİNİN İÇİNDE (lavabo x 140 merkezli 82 px = 99…181);
  `BookPropX` −336 → **−482** (sahne x 79), teknenin sol kenarıyla arasında 6 birim hava.
- **YÜRÜYÜŞ GERÇEKTEN YAVAŞLAR** ("yürüme animasyonunun sonunda yavaşlarken animasyonun
  yavaşlaması gerekmez mi"). Kablolama zaten doğruydu — `WalkPace` hem zemini hem çevrimi
  aynı katsayıyla ölçekliyor — eksik olan OKUNURLUKTU: 260 birimde 0.45 demek son adımların
  saniyede 5.5 kare ve üçte bir saniye sürmesi demek, yani ölçülebilen ama görülemeyen bir
  yavaşlama. **300 birimde 0.30**: varış 3.5 kare/sn, yavaşlama üçte iki saniye daha uzun.
  Seçilmeden önce ölçüldü — vardiya 95 saniye ve yürüyüş oradan harcanıyor: yaklaşma 0.45 sn
  pahalandı; eğrili yumuşatma (u² yerine u) önce denendi ve 1.7 sn tuttu, yani bir müşteri.

### 9.4 · Fatura sadeleşti (2026-08-26)

Yazar: "gün sonu fatura ekranı karmaşık ve çok yazılı duruyor". Fiş **on üç** basılı satır
koşuyordu: iki blok başlığı, beş rakam, gözün zaten yapabildiği iki ara toplam — ve beşin
üçü rutin olarak SIFIRDI (hiçbir şey almayan bar da her gece STOCK $0 ve SHOP $0 basıyordu).
Bloklar ve ara toplamlar (`BillSub`) gitti; gelir tek satır (**TAKINGS**), giderler yalnız
gerçekten ödenenler, ayıran şey kırmızı mürekkep ve eksi işareti — 2026-08-11'in "gider ve
kalan daha açık belli edilsin" notunun iş gören yarısı buydu. RENT her zaman basar (barın
üstüne kapandığı fatura odur). Yıldız sırasının altındaki "TONIGHT 3.5" de gitti: yıldızlar
zaten o okumadır; kalan satır odada kimin olduğunu söyler (n SERVED · n WALKED).

### 9.5 · Tezgâh tek oda oldu (2026-08-26)

**ÜÇ KUSUR, TEK KÖK — VE DUVAR TEK BİR YAPIM DAYANDI.** Tezgâhların arkaplanı yoktu; bir tur boyunca üretilmiş bir arka duvar asıldı ve aynı gün geri söküldü (yazar: "arkadaki bu planı kaldıralım müşteriler gözüksün") — duvar "tezgâh boş" şikayetine barı tahtayla kapatarak cevap veriyordu: oda ve İÇEN müşteriler, bir lambri resminin arkasında tamamen çizili duruyordu. Sahne artık yalnız BAR ÜSTÜnü sahiplenir (`BuildBenchStage`, bir kez); ray çizgisinin üstünde canlı oda görünür ve sahneler arası kayan yalnız tezgâhın üstündekilerdir. Bütün kontroller yazarın 1149×426'lık çalışma alanında yaşar: kart barda ayakta (sol kolon), dikey karışım sütunları rayın altında ve sağ marjın içinde, alt raflar ölçülü bir istif (tuşlar 26..72, okuma 84..110, ipucu 114..128, iş göstergesi 134..156).

**ÇEKLİST OKUNUR OLDU.** Sol üst köşeye sabitlenmişti ve akış fasyanın ÜSTÜNE çizdiği için
saatin üzerine biniyordu; dört 16 px işaretinden "tin'i doldur", "kapa", "çalkala ya da
karıştır" ve "bardağa götür"ün okunması bekleniyordu — o boyutta dördü de aynı lekedir.
`BenchTopClear` = 74 birim aşağıda, kapaklı başlıklı ev kartı, ve işaret artık **ADIM
NUMARASI**. Alkolün adı alanın tepesinde altın renkte asılıydı; artık tezgâhın arka kenarına
kesilmiş bir plakada, SAĞ uçta — ortada dururken tin ve bardağın arkasına düşüyordu.

**TUŞLAR BARA İNDİ.** BACK ve TO THE GLASS, duvarın yarısında asılı 76×150'lik, kelimeleri alt
alta dizilmiş sütunlardı — bir barda hiçbir şey omuz hizasında duvardan kullanılmaz. Tezgâhın
ön kenarında tek satırlık şerit (`KeyStripY/KeyStripH`), her birinde ok; çöp o sıradan uzakta.

**İŞ GÖSTERGESİ:** çalkalama/karıştırma çubuğu 220×14'lük düz Night[0] dikdörtgeni ve içinde
büyüyen ikinci bir düz dikdörtgendi; başlığı havada asılıydı. Kimse çalkalamazken tezgâhın
üstünde duran boş siyah bir bar. **Evin bitmiş göstergesi zaten vardı ve hiçbir tezgâh onu
kullanmıyordu:** `ChromeArt.GaugeTube` + `Solid` sprite'lı `Image.Type.Filled` + `GaugeGlass`.
Bar artık o alettir, üç farkla: **iş yokken hiç yoktur** (`StepWorkMeter`, hiçbir el talep
etmediği ilk karede çekilir), başlığı tüpÜN İÇİNDEdir, ve **yeterin nerede olduğunu**
söyleyen bir çentik taşır (`EnoughMark` 0.72; dolgu çentiği geçince yeşile döner).

### 9.6 · Tezgâhın garnitür rafı ve turu (2026-08-26)

Bitmiş içkinin yanında beliren dört istasyon artık gece boyu barda duran **altılı bir RAF**,
ve **SÜRÜKLENİYORLAR** — içkiyi tabureye taşımakla aynı fiil, aynı ağırlık. Hep açıktır,
çünkü bir barın garnitür tepsisi gelip gitmez; içkiye göre değişen şey KULLANILABİLİRLİKtır,
onu da sönme ve bırakmanın reddi söyler.

**RAF İKİ TÜR PARÇA TAŞIR ve her biri kendi Core fiilinden geçer.** Buz, limon, tuz ve şeker
`PreparationDefinition` — hacimsiz işaretler, `AddPreparationAtGlass`. **Zeytin ve nane ise
İÇERİKtİr**: stoktur, raftan gelir, biter, ve `recipes.json`'un "olive"/"mint" stil bantları
onlara göre notlanır — `PourAtGlass(id, ServingGlass.Capacity × 0.05)` ile düşerler (tin'in
kendi `GarnishClickFraction`'ı, ama hedef kapta ölçülür). Barın stoklamadığı ya da bitirdiği
bir garnitür hiç kurulmaz.

**TUR TEZGÂHA TAŞINDI, SİLİNMEDİ.** Kaplar bardak tezgâhından kalkınca tuz/şekeri tek
bırakmayla uygulamak, sekiz gün önce açıkça istenmiş bir beceriyi ("tuz artık bardağın
etrafında çevirerek ... ufak bir skill oyunu") kimse geri istemeden silmek olurdu. Aritmetik
tezgâha BÜTÜN taşındı (`StepRimLap`): kabı içkinin üstünde tut ve imleçle AĞZININ etrafında
tam bir tur at. Sayılar tezgâhın kendi sayıları — süpürmenin saydığı bant, turun üçte
birinden büyük tek kare sıçramalarının atılması, yarım kalan turun kabına yazılması — böylece
orada öğrenen oyuncu burada yeniden öğrenmez. **Bir tur asla bırakmayla uygulanmaz.**

**İMLEÇ ETİKETLERİ:** yazarın kuralı menüye değil bu TÜR etkileşime dair — tek plaka
(`_propTip`), hangi rect'in üzerinde duracağı söylenir ve EKRAN üzerinden çevirir, böylece
sahnenin kendi tuvalindeki bir prop (lavabo, bira musluğu) için de HUD'ınki için olduğu gibi
çalışır. Kirli bardak "CLEAR THE GLASS", atıştırmalık "TAKE THE <ad>", lavabo "POUR IT AWAY",
musluk "PULL A PINT". **Bardak lavaboya SÜRÜKLENİR**; drenaj artık hiç tıklama almaz, plakası
yalnız bırakılma noktasını sınamak ve imlece ne olduğunu söylemek içindir.

**BORÇ:** bardak tezgâhının eski bitirme masasından kalan `AddGarnishChip`, `AddFinishTub`,
`TableStand` ve o tezgâhın kendi tur makinesi (`UpdateRimLap`, `ShowRimRing`, `PlaceRimRing`)
artık çağrılmıyor — çökmezler ama ölüdürler, bir sonraki temizlik turunda gitmeliler.

### 9.7 · On birinci tur: plaka dilimlendi, tin birleşti, kepenk sustu (2026-08-26)

- **`board_plate` 9-DİLİMLİ** (`ItemArt.BoardPlate`): kenarlar çizimden ölçüldü (başlık 30,
  yanlar 12, taban 14 satır), `pixelsPerUnitMultiplier 0.5` ile çerçeve her boyda tam 2×.
  Panolar 420'ye döndü (içerik alttan taşmıyor), teal kapaklardaki yazı gece mürekkebi,
  MON şeridi rayların içinde. Aynı plaka tezgâhın adım kartının plakası; tin tezgâhının
  kart başlığı ŞİŞENİN ADIni taşır (`RefreshShaker` yazar), bardak tezgâhının kartı iki
  adıma indi (TIP THE TIN · SERVE IT) — buz ve garnitür o tezgâhtan odaya taşınalı beri
  üçüncü satır, gitmiş bir istasyonun tarifiydi.
- **İKİ TEZGÂHTA TEK TİN:** bardak sahnesi `ItemArt.Shaker` çiziyordu — başka bir kap,
  üçte iki boyda. Artık tin tezgâhının gövdesi + OTURMUŞ kapağı, aynı 200×358; ağız
  matematiği yüksekliğe bağlı olduğundan döküm onunla taşındı (`ServeVesselH` 358).
- **SERVE IT ▶** tek yüksek satır (display-16), tuş şeridinde; ve **kepengi kapatarak
  çıkar** — yalnız bu kapı: BACK TO THE BAR mahzeni açık bırakır, çünkü geri dönüş başka
  bir şişe almak içindir.
- **KEPENKTE YAZI YOK:** STOCK tek yapım dayandı; 3× büyütülmüş şevron tek başına, kelimenin
  durduğu yerde. Süitler artık oka basar (`OpenSignArrow`).
- **RAF SOLA KAYDI:** lavabo 181 … kaplar 195..380 … bardak 405 … mat 480 — tezgâh, gecenin
  akış sırasıyla okunur: lavabo, malzemeler, içki, musluk.

### 9.8 · Raf barın sahip olduklarını gösterir; içki kendi altlığında durur (2026-08-26)

- **KİLİT EKONOMİDEYDİ, RAF OKUMUYORDU** (yazar: "bazıları ileriki seviyelerde
  açılacaktı"). Buz, limon kıvrımı ve iki rım ev temelidir, hep durur. **Zeytin ve nane
  STOKtur** — `base_bar.json` onları başından beri 3.0 ve 4.0 yıldızın arkasına fiyatlamış;
  raf artık bunu okuyor. Barın almadığı ya da bu gece bitirdiği bir kavanoz tezgâhta
  durmaz; alınca durur. **Sıra GÖRÜNÜR indekse göre dizilir**, böylece alınmamış bir
  garnitür sırada delik bırakmaz ve raf hep altlığın başladığı yerde biter.
- **KAPTAN İÇİNDEKİ ÇIKAR** (yazar: "buz kovasından buz alırsın buz kovası değil"). El artık
  koveyi değil KÜPü, kaseyi değil DİLİMİ, kavanozu değil ŞİŞİ kaldırıyor — hem de bardağa
  düştüğünde yüzen sprite'ın ta kendisini: seçme, taşıma ve yüzme tek nesne. **İki rım
  İSTİSNA ve bu bir unutkanlık değil:** fiil bardağı tuzun İÇİNDE çevirmek olduğu için elde
  duran şey kabın kendisidir.
- **KEPENK AÇILINCA KAYBOLMAZ** (yazar: "kapak açmak için bastığımızda yok oluyorlar").
  Önce çekmeceyle birlikte kapatılıyordu; kaplar barın üstünde duruyor, bar odayla
  yükseliyor, ve arkasına uzandığın anda yok olan bir tepsi hata gibi okunur. Artık
  `CounterLift` ile YUKARI biner; yalnız **imlece cevap vermeyi keser**, çünkü altında
  mahzenin kendi kapıları var ve şişeye giden tıklama şişeye ulaşmalıdır.
- **İÇKİNİN BİR YERİ VAR:** bitmiş içki son garnitür (sahne 380) ile bira matı (480)
  arasında, sahne 430'da durur; boyu 116 → **92** (o boyda bardın en uzun nesnesiydi ve
  önplan propu gibi okunuyordu). Altında **her zaman** bir altlık çizilir
  (`counter_coaster`, tam 2×) — içki olsun olmasın: boş altlık, bir sonrakinin nereye
  konacağını söyleyen şeydir. İkisi de tek sabitten (`GlassHomeX`) yerleşir.

### 9.9 · On üçüncü tur: sahne bir kompozisyon, içki bir şey oldu (2026-08-26)

**KOMPOZİSYON TEK KURALLA ÇÖZÜLDÜ: PROPLAR DİYEJETİK, KROM DEĞİL.** Tin, şişe, kaşık ve
bardak tezgâhta DURAN şeylerdir ve tezgâhtaki nesne arkasındaki duvardan yakındır — uzun
bir tin'in ray çizgisini aşıp odanın önüne çizilmesi perspektiftir, çakışma değil.
Okunan her şey — kart, göstergeler, tuşlar, yazı — alettir ve rayın altındaki bantta
kalır. Bant üç kolon: solda aletler (kart x 130, kaşık sol kenarda ayakta), ortada iş
(tin, sonra tin+bardak), sağda ölçüler (karışım sütunu, çöp). Her propun AYAĞI tek
çizgide (`BenchFootY`, ekran 585), iki kartın ÜST kenarı tek çizgide (`CardSeat`).

**KAPAK KAPANINCA BARDAK KENDİ GELİR** (yazar: "bardağa koyma aşamasına artık ayrı bir
sahne istemiyorum"). TO THE GLASS tuşu emekli; tin kapalı VE dökülebilir olunca
(`CanPourOut` — karışmamış iki alkollü tin kapıda durur) bardak 0.45 sn sonra kayarak
gelir. El bir şeyin üstündeyken asla: çalışan elin altından sahne çekilmez.

**PLAKA ÇİZİLDİ** (`ChromeArt.Instrument`): üretilmiş `board_plate` çerçevesi tek
dikdörtgende üç farklı raydı — solda kesik magenta, sağda düz teal — ve 9-dilim gürültüyü
esnetiyordu. Krom prosedüreldir (14 §3); çizimin BEĞENİLEN görünüşü (lacivert yüz, teal
kapak, pirinç çizgi, dört perçin) 48×48'lik gride yeniden çizildi; panolar ve adım kartı
onu giyer. `board_plate.png` silindi.

**İÇKİ BİR ŞEY OLDU:**
- **Buz YÜZER:** küpler sıvı çizgisinde kendi yavaş salınımına biner (faz = küp indeksi,
  zar yok), birkaç derece yalpalar, bardak boşaldıkça oturur. Nane/zeytin yarı güçle sallanır.
- **LİMON CAMA OTURUR:** `glass_lemon_rim` yarığıyla kenara geçer, yarısı içeride hissi;
  dekorun çocuğu olduğundan bardak nereye giderse onunla gider.
- **KABUK AĞIZDA VE GÖRÜNÜR:** eski şerit iç genışlikte ve 7 birimdi — ağzın içinde
  yüzen kutucuklar. Şimdi ağız genişliğinde, iki kat derin, üç parça: koyu oturak,
  benek, ışık alan üst dudağı.
- **RIMLER YUMAK TAŞIR** (`carry_salt/sugar`): kap değil tutam; taşırken YOL BAŞINA tane
  döker (`ShedGrain`, 26 birimde bir, sapma tane sayısından yürütülür, zar yok).
- **TUR ALETİ:** on dört kutu yerine dört okuma — sönük oturak halkası, kabın renginde
  BÜYÜYEN kabuk işaretleri, imlecin altında yanan baş, ve ağzın ortasında yüzde.
- **ALTLIK ÇİZİLDİ** (`BackBarArt.Coaster`): üretilen iki deneme de oran tutturamadı;
  altlık tam ölçü isteyen bir elipstir — mantar, aşınmış halka, pirinç kenar, 56×18.

## 10 · Teknik omurga

- **6 asmdef:** Core (saf C#, motor erişimi imkânsız) ← Game ← UI ← Editor; Tests → Core+Game; PlayTests (2026-08-12) sanal fareyle gerçek sahneyi oynar — UI'ın içine değil, ekrana ve Core durumuna bakar.
- **Determinizm:** `RunRng` (FNV-1a→PCG32) adlı akışlar: arrivals, orders, patience, decide, customer, read. `System.Random`/`UnityEngine.Random` yasak.
- **Veri:** 6 JSON, `JsonUtility` + gürültülü doğrulama; tarifler çift kaynak (json+katalog) parite testli. **`story/story.json` 2026-08-13'te yüklenir oldu** (`DataLoader.ParseStory`): kadro + tarif kataloğuna karşı kurulur; bilinmeyen look/tarif/gece, sessiz geceye yazılmış misafir, iki host, kimsenin izlemediği ders adı, hiçbir yere çıkmayan beat yüklemede patlar. Yazım kuralı da orada: `needStyle` isteyen beat, o stili `hostWarning` satırında **adıyla** söylemek zorunda. Bootstrap boot'ta ayrıştırır ama koşuya henüz vermez (`storyInPlay` kapalı — diyalog plakası S3'te).
- **Araçlar:** LastCall menüsü — Create Debug Scene · Simulate Tycoon 200 Runs · Measure Service Speed Response.
- **Doğrulama:** 281 EditMode testi (12 dosya) + 7 PlayMode testi (4 duman + 3 piksel taban resmi, `Baselines~`); sim botu gerçek oyuncu fiilleriyle 200 koşu, `Docs/tycoon_sim_report.md`.
