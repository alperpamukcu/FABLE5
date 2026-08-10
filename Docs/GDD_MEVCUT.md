# LAST CALL — MEVCUT OYUN GDD'Sİ (as-built)

**Tarih:** 2026-08-07 · **Kaynak:** koddan çıkarıldı (8 kollu denetim, dosya:satır kanıtlı) · **Durum:** oyunun *bugün gerçekte olduğu hali* — tasarım niyeti değil, çalışan kural.

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
- **Servis tercihi (spec):** ~%50 sade; değilse 1–2 garnitür {buz, limon, tuz, şeker}; Shaken tariflerde %25 "sert çalkala" (enerji ≥0.6). Draught'a garnitür/çalkalama yazılmaz. Beklenen doluluk 0.80 (tepeleme isteği 2026-08-02'de emekli).
- **Ekstra tur:** Exact + zanaat tam + dönen müşteri + bekleme <%90 → en fazla 2 ek sipariş, sabır %80'e tazelenir.
- **Müdavimler opt-in:** kayıt (registry) verilmezse anonim kalabalık. Müdavim: isim/yaş/şehir/arketip/ziyaret/ilişki taşır; duygu katmanı 2026-08-02'de söküldü — kokteyle verilen tepki tek gerçek.

## 4 · İçki yapımı — üç yol, tek yasa

**Brim kanunu:** hiçbir kap taşmaz; `Add` kabul ettiğini döndürür, fazlası hiç girmez. Dökülme = bilinçli israf sayaçları (bin, SpilledBeer).

| Yol | Kapı | Reddettikleri |
|---|---|---|
| **Shaker** (`BeginPour/PourTick/PourMeasure/PourGarnish/Shake`) | kokteyl inşası | bira (keg işi), gazlı (`Carbonated`) |
| **Bardakta inşa** (`PourAtGlass`) | gazlının TEK kapısı + Built tarifler | bira |
| **Musluk** (`BeginPull/PourTilted/SettleHead`) | yalnız bira | bira olmayan id; shaker doluyken pull |

- **Servis dökümü zorunlu:** içki shaker'da servis edilemez; `PourIntoServingGlass(hacim, isabet)` — isabet dışı kısım dökülür, oranlar bozulmadan (TransferInto brim'e kadar, hazırlıklar bardağa taşınır).
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
| **Bahşiş (asıl kazanç)** | `taban × kalite`; kalite = 0.45 hız + 0.35 zanaat + 0.20 doluluk. Broke/Yanlış/0 taban → bahşiş yok |
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
| Gazlı bayrağı | 3 | cola, tonic, energy (soda ve ginger Bubbly ama bayraksız — bilinçli veri gerçeği) |
| Tarif | **53** | Built 19 · Shaken 24 · Stirred 10; pint 1 / rocks 13 / highball 22 / coupe 10 / martini 7 |
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
| **Back bar (menü)** | şişe hover=bilgi kartı, tıkla=rota (garnitür anında; bira→Tap; gazlı→Serve eline; kalan→Shaker) |
| **Shaker** | şişeyi kaldır-yatır dök; hazırlık sürükle; kapağı tak; tin'i savur=çalkala |
| **Serve** | shaker'ı NİŞANLA dök (kaçırırsan döker); dolap şişesi elde `PourAtGlass`; bardakta bitir |
| **Tap** | bardağı yatır-doldur, dikleştir-köpük; verdikt satırı canlı |

Teknik: sahne 640×360 (PixelPerfect), HUD 1280×720; tüm UI kodla kurulur, prefab yok; yalnız yeni Input System (`Mouse.current`).

## 10 · Teknik omurga

- **5 asmdef:** Core (saf C#, motor erişimi imkânsız) ← Game ← UI ← Editor; Tests → Core+Game (UI *yapısal olarak* test edilemez).
- **Determinizm:** `RunRng` (FNV-1a→PCG32) adlı akışlar: arrivals, orders, patience, decide, customer, read. `System.Random`/`UnityEngine.Random` yasak.
- **Veri:** 5 JSON, `JsonUtility` + gürültülü doğrulama; tarifler çift kaynak (json+katalog) parite testli.
- **Araçlar:** LastCall menüsü — Create Debug Scene · Simulate Tycoon 200 Runs · Measure Service Speed Response.
- **Doğrulama:** 175 EditMode testi (9 dosya); sim botu gerçek oyuncu fiilleriyle 200 koşu, `Docs/tycoon_sim_report.md`.
