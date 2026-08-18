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
- **Son müşteri = evin misafiri + sınav (2026-08-13 rework, Core'da var, henüz sessiz — GDD 26 §3-4):** hikâye opt-in; `StoryArc` verilmemiş koşu bugünküyle birebir aynı. Verilmişse: kapı kapandıktan **ve** oda boşaldıktan sonra o gecenin beat'inin misafiri `BarDay.SeatGuest` ile oturur. **Defterlerin dışında:** kimlik yok (kendini tanıtır — gizli bilgi kuralının TEK yazılı istisnası, CLAUDE.md'de çitli), hesap yok, bahşiş yok, puan yok, fişte satır yok (`OnTheHouse`; gecenin sayan listesi `BarDay.FinishedCounted()`). **Sınav:** birkaç içki, TEK saat, post-it'te teker teker; standart = tam tarif + tam zanaat + tam yöntem, tek af doluluk ≥0.90; yanlış içki hata sayar ve istek YERİNDE kalır; `allowedMistakes` aşılınca veya saat bitince gece yanar, beat kendi gecesinde `returnsAfterWeeks` hafta sonra döner. Diyalog saati tutar (`ClockHeld`): konuşurken hiçbir şey işlemez, `BeginLastCallTrial()` başlatır, 120 sn `TalkingGrace` emniyeti gece rehin kalmasın diye. Ekstra tur yolu bilerek dokunulmadı (ödül sabrı tazeler; talep tazelemez). Veri bağlantısı ve diyalog kabuğu S3/S5'te.
- **Takvim artık kural (2026-08-13, `BarCalendar` — GDD 26 §2b):** hafta altı açık gece, Salı→Pazar, Pazartesi bar karanlık (gün 1 = Salı, gün 4 = Cuma, gün 10 = 2. hafta Cuma). Plakadaki `WEEK 2 · FRIDAY` yazısı haftalardır oradaydı ama hiçbir şey ifade etmiyordu; hikâye misafiri **yalnız Cuma-Cumartesi** getirdiği için sessiz geceler artık "eksik olanı gidip alma" geceleri. Ev halkı misafir değil: yalnız `role: host` sessiz gece çalışabilir (Ece'nin açılış Salısı). Takvim `TycoonHud`'dan Core'a taşındı, yazı değişmedi.

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

- **53 tarif** (`recipes.json` ↔ `RecipeCatalog` parite testli). 4'ü canlı başlar (draught, neat_pour, vodka_soda, gin_sour); 49'u satın alınarak açılır.
- **Bantlar:** her kokteyl **stil bantlı** (cin ≠ votka); yalnız draught + neat_pour tip bantlı (marka-bağımsız). Stil+tip karışımı kurucuda reddedilir.
- **Eşleme (`RatioRecipeMatcher`):** MinFill kapısı (yalnız draught 0.75) → her bant payı kabul etmeli (sınırlar dahil) → adsız pay ≤ **0.15**. En yüksek rank kazanır; tarif bonus, kapı değil.
- **MinTier (kalite bandı):** martinez (cin≥T2), boulevardier (viski≥T2), rosita (tekila≥T2), el_presidente (rom≥T2), **vesper (cin≥T3 + votka≥T2)**. Ucuz şişe bandı doldurmaz — hata mesajı yok, içki "daha azı" okunur.
- **Rank kademeleri:** 1–8 başlangıç (kapısız) · 9–14 → 2.0★ · 15–21 → 3.0★ · 22+ → 4.0★. Fiyat `max(9, 5+5(rank−2)/2)`. Alım kilitli stok stillerini kataloğa salar.

## 6 · Ekonomi

**Gelir** (ayrılışta tahsil, serviste değil):

| Kalem | Formül |
|---|---|
| Taban fiyat | `3 + (rank+1)/2` (bilerek düşük — $4–17) |
| Stok primi | seçkin Spirit/Bitter bandı başına `(rafın en iyi tier−1) × $2` |
| Kalabalık çarpanı | HighRoller ×1.25 · Regular ×1.0 · Broke ×0.75 |
| **Bahşiş (asıl kazanç)** | `taban × kalite`; kalite = 0.45 hız + 0.35 zanaat + 0.20 doluluk. Zanaat (2026-08-11): kokteylde `0.6 × garnitür-spec + 0.4 × YÖNTEM` — yöntem, SİPARİŞ EDİLEN tarifin `Prep`'ine karşı (Shaken çalkala ister, Stirred kaşık ister; yanlış karıştırma = hiç karıştırmama, çalkalanmış Martini berelidir; Built umursamaz). Draught'ta zanaat = köpük. Ekstra tur artık doğru yöntemi de ister. Broke/Yanlış/0 taban → bahşiş yok; **Close → bahşişin yarısı** (`CloseTipShare`, 2026-08-14) |
| **Yakın (Close)** | **istenen içki, yanlış oranda** (2026-08-14): tarifin adını andığı her bant bardakta (≥%5), yabancı pay eşleştiricinin kendi %15'i içinde, ama paylar bandı kaçırmış → menü fiyatı, **bahşişin yarısı**. Tier de affedilir: kuyu ciniyle kurulan Vesper buraya düşer. *Aynı aileden başka bir içki* Close değil, Yanlış'tır. Bantsız sipariş (bira, sek) Exact ya da hiç. Eski kural ("baskın TİP eşleşirse") stil bantları yüzünden **hiç ateşlenemiyordu** |
| Yanlış içki | *teslim edilenin* taban fiyatı (tanımsızsa $0) |
| Ret (doluluk <0.35) | $0, memnuniyet 0.02 · Decline: $0, 0.15 |
| Atıştırmalık | tabına fiyat (bahşişsiz); sabah geri alım `fiyat−1` → kâse başına net $1/birim |

**Gider:** kira (tek eksiye düşüren) · dolum `eksik×$3` · marka `Info.Price` yoksa `8+6×tier(+6 spirit)` (yıldız kapılı `min(4, tier)`) · tarif · tabure `$30/$50` (4→6) · bardak kademesi (hat başına 5 fiyat, json) · tezgah `40×tier` (yalnız Ambience) · çöp `hacim×$2`.

**Memnuniyet:** `(Exact .75 | Close .50 | Wrong .05) + 0.20(zanaat−.5) + 0.12(doluluk−.5) − 0.30×bekleme + Ambience` (0–1).

## 7 · Yıldız / itibar omurgası

- `BarRating`: 0★ başlar; gece yıldızı `1+4×memnuniyet`, **iki tavanla** kırpılır; ilerleme ataletli (+0.10 çıkış, −0.20 iniş, gecelik en çok +0.25). Fırtına gidenler de puan yazar.
- **Tavanlar döngüyü zorlar:** `UpgradeStarCap = 2.0 + bardak adımları (hat başına 0.60'a dek) + 0.25×(tabure−3)`; `MenuStarCap` gece servis edilen en iyi Exact ranka göre 2.0→5.0.
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

## 9 · Ekranlar ve fiiller

| Ekran | Oyuncu ne yapar |
|---|---|
| **Zemin (HUD)** | tabureye içki sürükle=servis · çöpe sürükle=at (ücretli) · kirli bardak tıkla=topla · kâse tıkla=atıştırmalık taşı · MENÜ/KİTAP/kasa/ayarlar |
| **Kimlik kartı** | tabure tıkla → `InspectId()` (kapı!); sipariş satırı hover=ideal oran kartı |
| **Tarif kitabı** | ara, TIER/PREP/ŞİŞE filtreleri; kilitliler "n★'DA AÇILIR" |
| **Gün sonu** | hesap fişi → market (4 sekme: DOLUM/ŞİŞELER/TARİFLER/YÜKSELTMELER + bu gece alınanlar iade) |
| **Back bar (menü)** | **İÇECEK SEÇMENİN TEK YERİ (2026-08-13).** Duvar garnitür VE BİRA dışında her şeyi taşır — gazlılar dahil. **Bira duvarı terk etti (2026-08-15):** fıçı satırı kaldırıldı, draught'un tek kapısı tezgâhtaki bira musluğu (aşağı). Şişe hover=bilgi kartı, tıkla=rota (garnitür anında tin'e tutam; gazlı→Serve eline; kalan→Shaker eline). Kapalı şişe kendi kabına bakar: gazlı SERVİS BARDAĞI dolu diye kapanır, kalanı tin dolu diye. Sahne geçişleri KAYAR (ileri sağdan, geri soldan; açılış fade, kapanış anlık); her istasyonda sol kenar BACK TO BAR |
| **Shaker** | Elde tek şişe, tin, kapak, kaşık — **tezgâhta içecek rafı YOK (2026-08-13)**; başka şişe için back bar'a dönülür. Şişeyi kaldır-yatır dök (akış şişenin ÖLÇÜLEN kapağından çıkar, 2026-08-11); AÇIK tin'de kaşıkla daire=karıştır; kapağı tak; tin'i savur=çalkala; kapalı+karışık → sağ kenar TO THE GLASS |
| **Serve** | shaker'ı NİŞANLA dök (kaçırırsan döker); **dolap/raf YOK (2026-08-13)** — buradaki tek şişe back bar'ın elimize verdiği gazlıdır (Core tin'de reddettiği için bardak onun tek kapısı), düğme basılı gelmediğinden **elde DURUR**, basınca kavranır; hazırlık kapları (buz/limon/tuz/şeker + garnitür kavanozları) tezgâhın sol ucunda; SERVE tuşu bardak boşken sönük |

**Her iki tezgâhın seti (2026-08-13):** ekranda mobilya assetı yok — `prep_table` ve `bar_mat` kaldırıldı. Panelin kendisi tezgâhtır: arkada barın kendi duvarı (`BackBarArt.LuxeWall`, gölgede), önünde bir ton açık tezgâh bandı ve buluştukları yerde aydınlık ön kenar; üstünde duran her şey `BackBarArt.BottleShadow` ile temas gölgesi taşır (tin ve şişeninki her kare kendi tabanını takip eder, kaldırınca söner). Yüzeyin kendisi çizilmez — `PourSurface`/`ServeSurface` sadece koordinat uzayıdır.
| **Tap** | **KAPISI ODADAKİ MUSLUK (2026-08-15).** Tezgâhta duran bira musluğu fikstürüne tıklamak doğrudan bu sahneyi açar (`DiegeticStage` plakası → `TycoonServiceFlow.OpenTap`); 1. seviye musluk (`taps_one`) bar ilk geceden **zaten sahibi** — mağazada OURS yazar, satılmaz, geri verilmez. Kimse fıçı seçmeden gelindiği için mahzen kendisi bağlar: raf sırasında ilk dolu fıçı. Bardağı yatır-doldur, dikleştir-köpük; verdikt satırı canlı; **tezgâh altı gerçek mahzen (2026-08-13)**: hatta bağlı fıçı + stoktaki diğer fıçılar kendi gözlerinde, birine tıkla=onu hatta bağla (Core `CanPull` reddederse hiçbir şey değişmez ve nedeni yazılır); dökerken pour_loop sesi; SERVE tuşu bardak boşken sönük |

Teknik: sahne 640×360 (PixelPerfect), HUD 1280×720; tüm UI kodla kurulur, prefab yok; yalnız yeni Input System (`Mouse.current`).

**Kaplar sayfadan değil ÇİZİMDEN ölçülür (2026-08-11, `VesselArt`; GDD 15 §8):** şişe/karton
sahnenin verdiği boyda, kendi çiziminin ölçüsüyle durur — ayakları tezgâhın/rafın çizgisinde,
ortası işaretinde; kendi yüksekliğinin 0.44'ünden geniş olan kap ENİNDEN sığdırılır (karton
şişenin yanında karton kalır). Döküm ağzı da ölçülür: kapaklı ve kapaksız çekim aynı sayfadaysa
kapak, iki çekimin AYRILDIĞI piksellerdir (kartonun ağzı düz çatıya oturan bir güdük, siluetin
tepesi değil). Şişede kalan sıvı çizimin kutusuna göre doldurulur; opak kap (karton, kutu)
seviyesini doğası gereği göstermez — sayı hover kartında ve market kutucuğunda.

## 10 · Teknik omurga

- **6 asmdef:** Core (saf C#, motor erişimi imkânsız) ← Game ← UI ← Editor; Tests → Core+Game; PlayTests (2026-08-12) sanal fareyle gerçek sahneyi oynar — UI'ın içine değil, ekrana ve Core durumuna bakar.
- **Determinizm:** `RunRng` (FNV-1a→PCG32) adlı akışlar: arrivals, orders, patience, decide, customer, read. `System.Random`/`UnityEngine.Random` yasak.
- **Veri:** 6 JSON, `JsonUtility` + gürültülü doğrulama; tarifler çift kaynak (json+katalog) parite testli. **`story/story.json` 2026-08-13'te yüklenir oldu** (`DataLoader.ParseStory`): kadro + tarif kataloğuna karşı kurulur; bilinmeyen look/tarif/gece, sessiz geceye yazılmış misafir, iki host, kimsenin izlemediği ders adı, hiçbir yere çıkmayan beat yüklemede patlar. Yazım kuralı da orada: `needStyle` isteyen beat, o stili `hostWarning` satırında **adıyla** söylemek zorunda. Bootstrap boot'ta ayrıştırır ama koşuya henüz vermez (`storyInPlay` kapalı — diyalog plakası S3'te).
- **Araçlar:** LastCall menüsü — Create Debug Scene · Simulate Tycoon 200 Runs · Measure Service Speed Response.
- **Doğrulama:** 281 EditMode testi (12 dosya) + 7 PlayMode testi (4 duman + 3 piksel taban resmi, `Baselines~`); sim botu gerçek oyuncu fiilleriyle 200 koşu, `Docs/tycoon_sim_report.md`.
