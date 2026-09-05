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

- **Geliş:** aralık `max(6, 12 − 0.5×gün) × yıldız çarpanı × (1±0.30)`; ≥3 bekleyen varsa gelen **vazgeçer** (balk). Ayrılanın (içki SERVİS EDİLENİN) boş bardağı tezgâhta kalır ve toplanana dek taburesini kilitler — kendiliğinden temizlenmez (7 sn'lik saat 2026-09-05'te emekli, §9.23); tıkla = topla (elde birikir, lavaboda yıkanır).
- **Tek saat (2026-09-04, §9.22):** sabır `max(22, 50−2.5g)` sn, müşteri kararını verdiği an işlemeye başlar ve içki gelene dek işler; sorulmayı beklemek de aynı barı harcar (dolarsa fırtına gibi gider). **Kimlik okumak** barı sıfırlamaz, kalanın üstüne üç kutudan birini (`PatienceMax/3`) ekler, tavan dolu bar. (İki ayrı saat 2026-08-02 → 2026-09-04 arasında vardı.)
- **Kimlik kartı (gizli bilgi):** `CustomerVisit.Order` `InspectId()` çağrılana dek **throw eder**; gerçek siparişi yalnız Core görür (`OrderTruth`). Kartı açmak siparişi almaktır — geri dönüşü yok. Kör servis yasal: yargıç gerçekle karşılaştırır. **Kartın ikinci işi (2026-09-05, §9.24):** ikinci geceden itibaren gelenlerin bir kısmı 20 yaş altı (yarısı ödünç kartla); `CustomerVisit.Papers` kart okunana dek throw eder, `TycoonRun.Kick(visit)` yalnız okunmuş kartla çalışır — doğru kick defter dışı + $5 teşekkür, yanlış kick walk-out, servis edilen reşit olmayan kalkarken `$20 + $20×⌊itibar⌋` ceza. **Kartın ikinci işi (2026-09-05, §9.24):** ikinci geceden itibaren gelenlerin bir kısmı 20 yaş altı (yarısı ödünç kartla); `CustomerVisit.Papers` kart okunana dek throw eder, `TycoonRun.Kick(visit)` yalnız okunmuş kartla çalışır — doğru kick defter dışı + $5 teşekkür, yanlış kick walk-out, servis edilen reşit olmayan kalkarken `$20 + $20×⌊itibar⌋` ceza.
- **Sipariş havuzu:** açık menüden, en düşük ranktan `3+gün` tarif; stok bakılmaz (kuru şişe = `DeclineOrder`).
- **Servis tercihi (spec):** ~%50 sade; değilse 1–2 garnitür {buz, limon, tuz, şeker}. Draught'a garnitür yazılmaz. Beklenen doluluk 0.80 (tepeleme isteği 2026-08-02'de emekli). **"Sert çalkala" 2026-08-11'de emekli:** yöntem müşterinin hevesi değil TARİFİN talebi — hakem artık `Prep`'i notluyor (aşağıda).
- **Ekstra tur:** Exact + zanaat tam + dönen müşteri + bekleme <%90 → en fazla 2 ek sipariş, sabır %80'e tazelenir.
- **Müdavimler opt-in:** kayıt (registry) verilmezse anonim kalabalık. Müdavim: isim/yaş/şehir/arketip/ziyaret/ilişki taşır; duygu katmanı 2026-08-02'de söküldü — kokteyle verilen tepki tek gerçek.
- **Açılış gecesi kimseyi tanımaz (2026-08-25):** `RegularsRegistry.RollNext` artık `allowReturns` alıyor, `TycoonRun` gün 1'de `false` geçiyor — bar dün yoktu, o yüzden gece birde içen herkes YENİ. Dönüş zarı yine atılır (kapı akışa bir çekiş borçlanmasın diye), sadece onurlandırılmaz; gün 2'den itibaren %55 dönüş şansı geri gelir. Yazarın şikâyeti buydu: ilk gün kimlikler "2. ziyaret" ve dolu yıldız satırı basıyordu. **ÖLÇÜLEN BEDEL:** ekstra tur "dönen müşteri" şartı taşır, gece bir artık dönen müşteri barındırmadığından o gece ekstra tur YOK — 200 koşuluk simde iflas %3.0 → **%7.0**, medyan kasa $194 → **$145**, bar itibarı 2.67★ → 2.59★. A/B izole edildi: kapı kapatılınca eski rapor birebir yeniden üretiliyor, yani kayma tamamen bu kuralın. Kural yazarındır; telafi kolu (başlangıç parası, gün 1 kirası, ya da ekstra turun "dönen" şartı) ayrı bir karar.
- **Yüz KİŞİYE aittir, isme değil (2026-08-25):** `TycoonHud.LookFor` eskiden arketip havuzundan gelen İSMİ hash'liyordu — kırk isim on çizime çöküyor, aynı isim daima aynı yüzü açıyor, oda her gece dört-beş surattan ibaret görünüyordu ("müşteriler rastgele gelmeli hergün"). Artık yüz `RegularState.Id`'ye bağlanır ve tanınmayan biri EN UZUN SÜREDİR sahnede olmayan yüzü alır: kadro tükenmeden kimse tekrarlanmaz, açılış gecesi (~8 içen, 9 yüz) baştan sona yabancıdır. Kendi yüzü o an başka taburede olan bir müdavim, o ziyaret için boş bir yüz ödünç alır ve kendi yüzünü bir sonrakine saklar. Kendi üreticinde (koşunun tohumundan türeyen ayrı "faces" akışı) — Core'un akışlarına dokunmaz, hiçbir şeye karar vermez.
- **BOŞ TABURENİN YÜZÜ YOKTUR (2026-08-25) — "aynı müşteriler geliyor"un ASIL sebebi buydu.** Gelen müşteri taburede önce `v.Visit`e yazılıyor, yüz SONRA soruluyordu; `LookFor`'un ilk işi ise "zaten yüzü olan tabureye dokunma" — ve o tabure hâlâ az önce çıkan kişinin yüzünü taşıyordu, çünkü `view.Look` ayrılışta hiç temizlenmiyordu. Sonuç: her taburede yalnız İLK müşteri gerçek bir yüz alıyor, sonrakilerin hepsi onu miras alıyordu — dört tabure, koşu boyunca **dört yüz**, ve misafir defteri (yüz başına tutulur) barın açılış saatinde "3. ziyaret" + dolu yıldız satırı basıyordu. Play'de ölçüldü: kapıdan yedi ayrı kişi girmişken dört yüz çiziliyordu; tek satırlık `view.Look = null` sonrası yedi kişi = yedi yüz, hepsi 1 ziyaret.
- **Misafir defteri koşuyla sıfırlanır (2026-08-25):** `_patronLog` (yüz başına ziyaret + bırakılan yıldız) hiç temizlenmiyordu; HUD bir kez kurulduğu için NEW RUN, yüzleri önceki koşunun sayaçlarıyla açıyordu. Yüz atamaları da aynı yerde sıfırlanır.
- **Kimlik evrakı canlı kadroyu da kapsıyor (2026-08-25):** `customers/papers.json` 2026-08-19 rig'inin dokuz yüzünü de taşıyor. O güne dek CANLI kadronun tek satırı yoktu: isim arketip havuzuna düşüyor, "citizen of" alanına ülke yerine ŞEHİR basılıyor, bayrak hiç çizilmiyordu — okunması istenen tek kartta, sessizce. `PapersTests` dokuzunu tek tek çitliyor.
- **Son müşteri = evin misafiri + sınav (2026-08-13 rework, Core'da var, henüz sessiz — GDD 26 §3-4):** hikâye opt-in; `StoryArc` verilmemiş koşu bugünküyle birebir aynı. Verilmişse: kapı kapandıktan **ve** oda boşaldıktan sonra o gecenin beat'inin misafiri `BarDay.SeatGuest` ile oturur. **Defterlerin dışında:** kimlik yok (kendini tanıtır — gizli bilgi kuralının TEK yazılı istisnası, CLAUDE.md'de çitli), hesap yok, bahşiş yok, puan yok, fişte satır yok (`OnTheHouse`; gecenin sayan listesi `BarDay.FinishedCounted()`). **Sınav:** birkaç içki, TEK saat, post-it'te teker teker; standart = tam tarif + tam zanaat + tam yöntem, tek af doluluk ≥0.90; yanlış içki hata sayar ve istek YERİNDE kalır; `allowedMistakes` aşılınca veya saat bitince gece yanar, beat kendi gecesinde `returnsAfterWeeks` hafta sonra döner. Diyalog saati tutar (`ClockHeld`): konuşurken hiçbir şey işlemez, `BeginLastCallTrial()` başlatır, 120 sn `TalkingGrace` emniyeti gece rehin kalmasın diye. Ekstra tur yolu bilerek dokunulmadı (ödül sabrı tazeler; talep tazelemez). Veri bağlantısı ve diyalog kabuğu S3'te geldi; **ev sahibinin dersleri ve kitaptaki açık hesap 2026-09-05'te (§9.25).**
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

### 5.1 · İçenin notu, sesi ve partikülleri (2026-09-04)

**KONUŞMA SESİ YOK (yazar: "konuşma sesi olmayacak").** `SpeakSeat` ve dört mırıltı klibi
(`voice_greet/order/happy/upset`) silindi — selamlama, sipariş ve çıkış artık sessiz.
Söyledikleri YAZILI: baloncuk siparişi, düşünme ritmini ve içkinin notunu taşıyor, ve
okunabilir bir satırın altındaki mırıltı aynı bilgiyi ikinci kez, göz atarak okunamayan tek
kanalda söylüyordu. Tabure, kasa ve odanın bütün sesleri duruyor; yalnız ağızlar sustu.

**İÇERKEN İPUCU VERİR** (yazar: "her müşteri içerken içecekte mükemmel oranda neyin yanlış
olduğunu küçük bir cümle ile ipucu versin"). Yeni saf kural `Core/Tycoon/PourAdvice.cs`:
teslim edilen bardağı mükemmel dökümle karşılaştırır ve **tek cümle** döndürür — mutlak
sapması en büyük bant (oyuncunun döktüğü şey hacim, o yüzden bardaktaki en büyük düzeltme),
sapmanın işareti (fazlaysa "less", azsa "more") ve büyüklüğü havuz sisteminin kendi 20
puanlık kutusunda: 2.5 puana kadar **mükemmel** (`ServiceJudge.PerfectWindow` ile AYNI
pencere, bir test ikisini birbirine çiviliyor), 6'ya kadar "A touch", 12'ye kadar "A little",
üstü "A lot". İki bantlı içkide iki isim tek hatadır (fazla cin = az tonik), beraberliği
büyük bant kazanır. **SAYI CORE'DAN ÇIKMAZ:** `RecipeDefinition.Perfect` `internal` ve tek
kapısı `TycoonRun.ExactPourFor`; kural o duvarın içinde yaşayıp dışarı kelime verir, ve bir
test her tarifi altı sapmada dolaşıp hiçbir cümlede rakam olmadığını doğrular. Bira/sek gibi
bantları TÜRETİLMİŞ tarifler sessizdir — öğrenilecek oranı olmayan içkiye ders verilmez.

**Nerede görünür:** kafanın üstündeki baloncukta, siparişin geldiği aynı daktiloyla harf harf
(`view.Note`, serviste bir kez alınır ve saklanır — içerken altından değişmez). Baloncuk
zaten en uzun satırına göre genişleyip aşağı büyüyordu; artık DURUM satırı da sarıyor
(`wantsLines`), çünkü bir cümle iki kelimelik bir etiket değil. Mükemmelse yazı amber, değilse
kulübün mavisi. Notu olmayan içkide eski "DRINKING…" noktaları geri gelir.

**PUANLAR TABUREDEN KALKTI** (yazar: "müşterilerin verdikleri ücretle beraber gözüken puanları
gizlensin"): `TabFloat`'ın üç izinden yıldız sırası silindi; para ve bahşiş duruyor. Yıldızlar
barın duruşunu hâlâ besliyor, gecenin fişinde okunuyor — orada yargılanan gece, kapıdan çıkan
müşteri değil.

**MÜKEMMELİN KENDİ BASAMAĞI VAR** (yazar: "perfect ise ... partiküller abartılsın"): üç
memnuniyet bandı (4-7 / 8-13 / 14-20) mükemmel dökümü sıradan iyi bir içkiden ayıramıyordu.
`PerfectMotes` **32** adet altın partikül, üstüne tezgâhın KENDİ tarafından `PerfectBackMotes`
**20** magenta partikül — mükemmel döküm barın iki yakasının birlikte yaptığı bir şey. Notun
`Flawless` bayrağı taşır, yani çıkışta hâlâ bilinir.

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

**Gider:** kira (eksiye düşüren — 2026-09-05'ten beri reşit olmayana servis cezası da, §9.24) · dolum `eksik×$3` · marka `Info.Price` yoksa `8+6×tier(+6 spirit)` (yıldız kapılı `min(4, tier)`) — **MEŞRUBAT MERDİVENDEN ÇIKTI (2026-09-04, yazar: "meşrubat fiyatları daha uygun olmalı ... hacimleri daha az"):** kategorisi `mixer`/`juice` olan her şey json'da **$2–4** (kola/tonik/zencefil/nar 3, soda ve şurup 2, meyve suları 3–4) ve fiyatsız kalanı `Market.SoftDrinkPrice` $3 yakalar — eskiden soda listesizdi ve merdivenden **$14** çıkıyordu, yani kuyu romundan pahalı. **Rafta da yarım şişe:** `ShelfBottle.MixerCapacity` **3.0** ölçü (spirit 6.0, keg 24.0) — 70cl'lik bir kola şişesi diye bir şey yok; döküm hızı değişmedi, yalnız daha erken biter, dolumu da o kadar ucuzdur. 200 koşuluk sim: iflas %7.0 → **%2.0**, medyan kasa $145 → **$193**, karşılanamayan sipariş 1335 → **490** · tarif · tabure `$30/$50` (4→6) · bardak kademesi (hat başına 5 fiyat, json) · tezgah `40×tier` (yalnız Ambience) · çöp `hacim×$2`.

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

- `BarRating`: 0★ başlar; gece yıldızı `5×memnuniyet` (2026-08-11'den beri; `1+4×` eski ölçek), **iki tavanla** kırpılır; ilerleme ataletli (+0.10 çıkış, −0.20 iniş, gecelik en çok +0.25). Fırtına gidenler de puan yazar.
- **İKİ PUAN, ORTAK YILDIZ (2026-09-05, GDD 27; §9.23):** gecenin yıldızı `min(servis, konfor)`. **Servis** = `min(5×ortalama memnuniyet, MenuStarCap)`; **konfor** = `ComfortBase − 1.0 × (1 − temizlik)`, `ComfortBase = 2.0 + Σ fikstür `comfort` (yalnız ayakta duran basamak) + 0.5 × bardak adımı tavanı + 0.25 × ek tabure` (eski `UpgradeStarCap` bu tabana dönüştü; `MenuStarCap` gece servis edilen en iyi Exact ranka göre 2.0→5.0 aynen). Yarının kalabalığı SERVİS tarafını okur (kir tek başına kalabalığı yoksullaştıramaz).
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
| **Gün sonu** | **GECE RAPORU (2026-08-25 yeniden tasarım):** ekranın ortasında hesap fişi (para), İKİ YANINDA gecenin iki ALETİ. **Gün, oda boşalmadan gelmez:** Core zaten son taburenin boşalmasını bekliyordu, ama ÇIKIŞ YÜRÜYÜŞÜ HUD'ın — perde eskiden son müşterinin tepki anını ve kapıya yürüyüşünü örtüyordu; faz dönüşü artık kitapları yalnız SİLAHLANDIRIR (`_dayEndDue`), gerçek açılış `FloorIsClear()` (ekranda kimse yok + havada sayılan hesap yok) veya 9 sn emniyet süresi. **SOL — THE WEEK:** haftanın altı gecesi + PAZAR; oynanmış geceler `Ledger.History`'den yıldızı (aynı `StarRow` cetveli) ve NET parasıyla, bu gece amber plakada yanar, ilerideki geceler beş BOŞ yuva, CMT'de her hafta magenta VIP yıldızı, PAZAR kepenk + CLOSED; altta haftanın toplamı. **SAĞ — AFTER TONIGHT:** barın merdiveni — beş 40px yıldız + iki haneli sayı, altında 0→5 ölçekli gösterge (ChromeArt.GaugeTube/GaugeGlass): amber dolgu barın durduğu yer, SOLGUN bant kazanılan/kaybedilen dilim, beyaz çentik gecenin başındaki duruş, cyan çentik bir sonraki basamak; yanında WAS x.xx ve ok'lu delta çipi (+0.12 / −0.60 / HELD). Altında üç okuma: TONIGHT (gecenin kapalı yıldızı), CEILING (fikstür+menü tavanı; oda tavanı aştıysa kırmızı ve "buy the fittings"), TOMORROW (yarının kalabalığı). **Sayılar KURALDAN gelir, ekran hesaplamaz:** `BarRating.StandingAfter` (CloseNight'ın aynı üç satırı), `TycoonRun.TonightStars/StarCeiling/StandingAfterTonight/CrowdTomorrow` — hepsi kitaplar kapanmadan sorulur, `NightReportTests` sor-sonra-kapat diye pinler. Beat sırası: 1 çağrı → 2 kâğıt beslenir (aletler kendi kenarlarından girer) → 3 yıldızlar fişe düşer + damga → **4 duruş tırmanır** (1.1 sn, sayı+yıldız+gösterge birlikte) → ancak o zaman GO TO THE ORDER. Fişin başlığı 8 birim aşağı indi (DISGRACE damgası tarih satırını kırpıyordu) ve fişten "BAR x.x" kalktı — o okuma artık sağdaki aletin işi. Sonra market (**5 sekme:** DOLUM/İÇKİ/MEŞRUBAT/TARİFLER/YÜKSELTMELER + bu gece alınanlar iade). **AYAKTA TEK TUŞ VAR (2026-09-04, yazar: "satın al butonu ve güne geç butonu yerine ... 2 butonu 1 buton yapıyoruz"):** sepetin başlık bandındaki PLACE ORDER ile sağ alttaki OPEN TOMORROW aynı tuş oldu (`_marketKey`, ayak sağı **216×128**, altyazı **24 punto**; ayak toplamı 8+800+8+216+8=1040) ve hangi işi yaptığını SEPETTEN okur — sepette bir şey varsa **YEŞİL** (Lime 4 yüz, Lime 1 mürekkep) **PLACE ORDER** ve basınca `Checkout()`, boşsa **MAGENTA** (Magenta 4, beyaz mürekkep) **OPEN TOMORROW / START TUESDAY** ve basınca `OnDayEndAdvance()`; sipariş indikten sonra 3 sn gri **ORDERED** ve tıklanmaz. Renkler ve boy 2026-09-04'te ikinci turda ayarlandı (yazar: "daha dikkat çekici olmalı ve satın alma seçeneğinde rengi yeşil olmalı"): amber PARADIR (16 §5) ve gecenin sonu para harcamaz, o yüzden çıkış ambere veda etti; büyütmek bir tuşu gürültüsüz yükseltmenin tek dürüst yolu, çünkü harcanacak şey yokken atan bir lamba dekorasyondur. Sepet 880'den 800'e indi ve hâlâ on beş çip alıyor. Lamba (yalnız sepet doluyken nefes alan `LampGlow`) artık bu tuşun arkasında duruyor. Eski çift, ancak biri anlamlıyken ikisi birden duran bir çiftti: boş sepette sipariş tuşu NOTHING PICKED diyordu, dolu sepette çıkış tuşu seçilenleri sessizce çöpe atıp "emin misin" diye soruyordu. Sepeti boşaltmak (çipe tıkla) artık geçmenin yolu; **Escape hâlâ eski kapıdan** yürür, yani sepet uyarısı `ClosingWorry()` ile ayakta. `ServiceSmokeTests.The_markets_one_key_buys_first_and_opens_tomorrow_after` koridordan bir şişe alıp tuşa basarak ikisini de pinler; `Baselines~/basket.png` bu yüzden yeniden kutsandı. **BOŞ ELLE ÇIKIŞ SORULUR** — `ClosingWorry()` hâlâ ayakta ve tek tuş üstünden de çalışıyor (`Leaving_the_market_having_bought_nothing_asks_first`). **SEPET KALAN BAKİYEYİ DE YAZAR (2026-09-04):** başlık bandı sağdan sola TOTAL ve **LEFT IN THE TILL** (`Money − CartTotal`, sıfırda kırmızı) — üst bar kasanın NE TUTTUĞUNU, sepet siparişin NE ETTİĞİNİ söylüyordu ve çıkarmayı oyuncu yapıyordu. **DOLUM KOLİSİ ARTIK KALAN (2026-09-04, yazar: "hem ayrı olarak alkolleri restocklayıp hem de ayrıyeten tam fiyatına restock satın alınıyor"):** "Restock the Whole Well" rafın TÜM açığını değil, `WholeWellPrice()` = tüm açık − sepetteki tek tek şişe satırları kadarını ister; sepetteki her çip fiyatı anında düşürür, sıfıra inince koli satılmaz (**IN**, raf zaten doluysa **FULL**) ve sepette duran bir koli her yeniden kurulumda `RepriceWholeWell()` ile güncellenir, sıfırlanınca sepetten düşer. Eski çözüm koliyi seçince tek tek satırları sepetten ATIYORDU — oyuncunun verdiği siparişi sessizce düzenleyen bir satır — ve koli yine tam fiyat yazıyordu. Core tarafında bir şey değişmedi: `RefillShelf()` çalıştığı ANDA rafı okur, tek tek satırlar sepette ondan önce geldiği için tam olarak kalanı tahsil eder, yani sepetin aritmetiği ile kasanınki aynı aritmetik. `The_restock_aisle_never_bills_the_same_measure_twice` bunu kasa üstünden pinler. **AÇIK ÜRÜNLER DE RÜTBESİNİ GÖSTERİR (2026-09-04, yazar: "markette açık olan her ürünün kutusunun bir tarafında kaç yıldız gerekiyorsa yıldız iconu ile gösterilsin"):** `TileSpec.RungStars` + `StarLadder` — kutunun SOL kenarında, sanat bandının boyunca, aşağıdan yukarı beş yuvalı dikey yıldız merdiveni (istenen rung kadarı amber, yarım rung yarım yıldız). Sağ kenarı stok göstergesi tuttuğu için tek boş sütun orası; mühürlü sandık rütbesini zaten kilidin etiketinde yazdığından ikisi asla birlikte çizilmez **ODA ÖNCE TEMİZLENİR (2026-08-25, yazar: "oyun sonu ekranı gelmeden önce açık olan tüm pencereler kapanır ana sahneye dönülür ... aynı şekilde gün başlarken de ekran ana ekran haline gelir ve temizlenir"):** `CloseEverySheet()` — kitap (SERT kapanır: kaydırma ve sayfa çevirme coroutine'leri durdurulur, panel anında gider; inen scrim'in altında yolculuğunu sürdüren bir sayfa tam da önlenmek istenen şeydi), ayarlar, geliştirici tezgâhı, rehber, defter, kimlik, servis akışı ve mahzen kapağı (anında). **Gece BİTERKEN çağrılır, kitaplar gelirken değil (2026-08-25 ikinci tur):** faz dönüşünün kendisinde — yani son müşteri hâlâ kapıya yürürken oda çoktan çıplaktır; `ShowDayEnd()` bir kez daha çağırır (araya girip bir şey açan olursa) ve `OnOpenTomorrow()` da, yani ertesi gece de temiz bir odada açılır. Gece artık yarım okunmuş bir tarifin, açık bir kimliğin ya da tin'inde içki kalmış bir tezgâhın ÜSTÜNDE sayılmıyor. |
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

### 9.10 · Bitmiş işin süpürülmesi (2026-08-27)

**3931 SATIR GİTTİ, 15 SATIR GELDİ.** Altı kollu bir denetim (kod, sanat, ses, doküman,
araçlar, veri) projeyi taradı; her aday **silinmeden ÖNCE** adıyla VE **GUID**'iyle
doğrulandı — sahneye sürüklenmiş bir sprite ada değil GUID'e bağlanır, ve "grep bulamadı"
bu evde silme gerekçesi değildir (sanat `"v3_"+id+"_flat"` gibi TÜRETİLMİŞ adlarla
yüklenir). İki süit ilk denemede yeşil: 380/380 ve 7/7.

**BARDAK TEZGÂHININ BİTİRME MASASI (452 satır).** `AddFinishTub`/`AddGarnishChip`'in
çağıranı yoktu; onlar tek yazar olduğu için `_servePrep` ve `_rimPrep` **asla** null
olmaktan çıkamıyordu — yani `UpdateServePrepDrag` ve `UpdateRimLap` her kare çağrılıp
ilk satırda geri dönüyordu. Kanıtlı no-op. Onlarla birlikte: `TableStand`, `StandNear/Far`,
`FinishProps`, `MixerMeasure`, `RailLabel`, rim takımının tezgâh kopyası (`ShowRimRing`,
`PlaceRimRing`, on bir alan), sürükleme yayı ve `_serveGarnishRow` — artık hiçbir şey
ebeveyni olmayan, her tazelemede boş döngülenen bir kap. Tur mekaniği YAŞIYOR:
kopyası `TycoonHud.Seats`'te, odanın tezgâhında.

**BOŞ FİZİK TERTİBATI.** `ShakerSolids.Add`'in çağıranı yoktu; tertibat her kare BOŞ bir
gövde listesini adımlıyor ve onun için sınır hesaplıyordu. `Pendulum` da yalnız ölü
sürüklemedeydi — `DrinkPhysics.cs` bütün olarak gitti.

**YİKİLMİŞ SAYFANIN MOBİLYASI.** Back-bar sayfası 2026-08-22'de yıkıldı; duvarı (`LuxeWall`),
altındaki raf (`Ledge`), üzerindeki isim plakası (`NamePlate`) ve bilgi balonu
(`InfoPlate`+`InfoTail`) kaldı. **`KegCrown` DOKUNULMADI** — kendi belgesinde yazılı bir
saklama kararı taşıyor ("hand-drawn art, not logic"); bu beşi ondan ayıran şey, fıçının
yeniden çizilebilecek olmasına karşılık bunların artık var olmayan bir sayfanın mobilyası
olması.

**22 YETİM GÖRSEL.** Kart devrinden (`sh_k_*`, `sh_mark`, `sh_strip_seal`, `btn_close*`,
`plate*`), yıkılan sayfadan (`Scene/backbar`), ve kesilen koddan yeni yetim kalanlar:
`ItemArt.Bucket`'in sekiz kovası/kasesi (tek çağıranı `AddFinishTub`'dı) ile
`ItemArt.Prep`'in artık ulaşılamayan `salt`/`sugar` dalları — rim artık ağıza ÇİZİLEN bir
kabuk (`GlassDecor.Speckles`), tepsiden alınan bir parça değil. **SAĞDAN ÇIKANLAR:**
`tap.png` (Tap.cs:198'de canlı yedek), `shaker.png` (üç çağıran), `register2.png`
(Main.unity'de GUID'le bağlı), `fx_monstera` (§9.9'un yazılı kaydı: beş bitkinin renk
çıpası, silmek üreten aracı bozar), `bench_mini_*` ve `garnish_*` (rafın canlı yedekleri).

**KALAN BORÇ (silinmedi, rapor edildi):** üç ses AD'ı klipsiz çalınıyor — `stir_loop`,
`whoosh`, ve yeni bulunan `page_turn` (TycoonHud.Book.cs:488,553). `Sfx` eksik klibi
sessizce yutuyor, yani bunlar hata vermiyor; on üç klibin hepsi canlı, yetim klip YOK.

### 9.11 · Barın sesi (2026-08-27)

**ÖNCE TEŞHİS: SES SİSTEMİ BOZUK DEĞİLDİ, KAPALIYDI.** Yazar "oyunda sesler mevcut
değil" dedi; oyunda ölçüldü ve `Sound.Effective` **0.00** çıktı — `PlayerPrefs`'te
`lastcall.muted=1`, ses 0.2'ye düşmüş. Mute'u değiştiren tek yer üst bardaki ayar
satırı (`TycoonHud.Chrome.cs:722`) ve ayar **yeniden başlatmayı aşmak üzere tasarlanmış**,
yani tek bir yanlış tıklama oyunu kalıcı olarak susturuyor. Bu bir kusur değil ama
**görünürlük borçlu**: mute'un tek göstergesi o panelin içinde.

**SONRA ÖLÇÜM: ON ÜÇ KLİBİN YEDİSİ PATLIYORDU.** Dalga formu sıfırdan uzakta bitiyordu —
`click.wav` tam ölçeğin **%45**'inde kesiliyor (her basışta sert bir çat), `ambience_loop`
her 5.75 saniyede bir sarım başında çatlıyor. Hepsi 22 kHz (yarım Nyquist), birkaçında DC
kayması. Yazarın yasağı ("patlamalar ... kesinlikle olmamalı") tam da bunu tarif ediyordu.

**43 KLİPLİK BANKA SENTEZLENDİ** (`Tools/sfx_dsp.py` + `Tools/sfx_bank.py`). İndirmek yerine
üretmenin sebebi: hazır paketler yükleyenin bıraktığı seviye, oran ve kırpımla gelir — ki
değiştirilen kusur tam olarak buydu. Burada her klip **TEK KAPIDAN** çıkıyor (`render`):
DC süzülür, `tanh` ile yumuşak limitlenir (sert kırpma = patlama), seviyesi merdivenden
atanır, sonra kenarları yükseltilmiş-kosinüsle sıfıra çekilir ve **uç örnekler sıfır mı diye
IDDIA EDİLİR**. Patlama artık ihraç edİlemez. Döngüler `loopify` ile kuyruğu başına
çapraz-solduruyor: sarım noktası ek yeri değil, çapraz geçiş.

**SESİN KENDİSİ FİZİKSEL MODELLENDİ** — hiçbir şey saf sinüs değil. Nesneleri ayıran şey hangi
parcıalların çınladığı ve ne hızla söndükleri: **cam** yüksek/inharmonik/yavaş (1:2.76:5.40:8.93),
**ahşap** alçak ve çok hızlı, **metal** inharmonik ve uzun, **kâğıt** perdesiz kısa çıtırtılar,
**sıvı** band-sınırlı genışliği nefes alan gürültü + kabarcık. Her şey 8 kHz altına
alçak-geçirilmiş: sabah 2'deki bir bar parlak bir oda değildir, ve süzsüz gürültü yazarın
yasakladığı "kulak rahatsız eden" sesin ta kendisidir. Zar atılmıyor: her gürültü klip ADIYLA
tohumlanıyor, yani banka her makinede bayt-bayt aynı çıkıyor (ev kuralı sese de işliyor).

**SEVİYE MERDİVENİ KASITLI** ("farklı yüksekliklerde sesler"): hover −30 dB → tick −24 →
light −18 → body −13 → weight −9 → moment −6. Ölçüldü: 0.032'den 0.501'e, **24 dB'lik
yayılım**. Bir arayüz tıkı kasanın altında kalmazsa her basış hizmet ettiği ana ile kavga eder.

**EMEK ARTİK DUYULUYOR.** `Sfx.HoldLoop` yalnız ad+seviye alıyordu, yani `_shakeEnergy` ve
`_stirEnergy` her kare gerçek imleç yolundan hesaplanıp **ses katmanında çöpe atılıyordu**:
tin'i deli gibi çalkalayan da hafifçe sallayan da tıpatip aynı döngüyü duyuyordu. Enerji
(0..1) artık **hem seviyeyi hem perdeyi** sürüyor — gerçek bir çabanın yaptığı budur, yalnız
birini oynatmak ses düğmesi gibi okunur. İkisi de **yumuşatılıyor** (perde seviyenin yarı
hızında): zıplayan bir seviye zipper gürültüsü, zıplayan bir perde warble'dır, ve ikisi de
tam oyuncu en çok çalışırken gelirdi. Oyunda ölçüldü: enerji 0 → `vol .396 pitch .920`,
enerji 1 → `vol .720 pitch 1.100` (**5.2 dB ve ~3 yarım ses**).

**İKİ `Sfx` NESNESİ BİRİKİYORDU.** Oyunda 16 AudioSource ölçüldü: `_instance` statiği domain
reload'da sıfırlanıyor ama `DontDestroyOnLoad` nesnesi sağ kalıyor, yani her yeniden derleme
bir kopya daha bırakıyor — ve öksüz olan kendi ambience yatağını çalmaya devam ediyor. İki
yatak üst üste faz yıkanmasıdır. `Instance` artık ÖNCE var olanı arıyor, `Awake` ikinciyi
kendini yıkıyor, ve reload'dan sağ çıkanın serialize edilmeyen ses dizisi boşsa yeniden
kuruluyor (yoksa yeniden kullanım ilk tıklamada NullReference olurdu).

**MALZEME EŞLEŞMELERİ DÜZELTİLDİ:** `bottle_open` dört iş birden yapıyordu (mahzenden şişe,
fıçı bağlama, tin'in kapağı, ve tin'in PATLAMASI) ve tin'i kapatmak `glass_down` çalıyordu —
ahşap üzerinde cam sesi, iki parça çelik için. Artık `cap_on` (metal), `blowout` (mührün
bırakması + kapak + gaz + dökülen içki, tek olay tek klip), ve bira `tap_pull` (daha dolgun,
daha alçak, daha gazlı — GDD 21 §10 duyulabilir hale geldi). Sessiz olanlara ses verildi:
ehliyet okuma (oyunun MERKEZİ hareketi, sessizdi), lavabo, rim'in kapanması, şişe kaldırma,
kapak alma, tin kavrama, kaşık, ve musluk kolu (kol her kare çağrılan bir yerde, o yüzden
YALNIZ durum değişince — aksi hâlde 60 Hz'de makineli tüfek olurdu).

### 9.12 · Barın sesi tamamlandı: foley ve sentez (2026-08-27)

**AYRIM TEK CÜMLE: BAR FOLEY'DİR, OYUN SENTEZDİR.** Barmenin elinin dokunduğu her şey — cam,
ahşap, metal, kâğıt, sıvı — fiziksel nesnesi olarak modellendi, çünkü oyuncunun bir tezgâhın
arkasında olduğuna inanması gerekiyor. SİSTEMİN söylediği her şey — yıldız, seviye, hükum,
gecenin açılışı, kofinin sonu — **1980'ler polisentezi**, çünkü orada konuşan oda değil oyun,
ve bu bar Miami'de neonla aydınlanıyor. İki ses, asla karıştırılmadan: oyuncu her an barın mı
yoksa oyunun mu konuştuğunu biliyor.

**`analog()` dönemi tek fonksiyona koydu** (`Tools/sfx_dsp.py`): chiptune bir konsol çipinin
kare dalgasıdır; bu oda ise bir polisentezdir. Üç şey onu "beep" olmaktan çıkarıyor —
**DETUNE** (birkaç sent aralıklı sesler birbiriyle vuruşur, dönemin bütün sıcaklığı budur),
**HAREKETLİ FİLTRE** (nota sönerken parlaklığın düşmesi sese şekil verir), ve **DRIFT**
(analog osilatörler asla sabit durmaz; kusursuz sabit perde her zaman dijital duyulur).
Banka **67 klip**: 43'ü ilk turdan, 24'ü bu turdan.

**HER KARE ÇALIŞAN YERLERİN HEPSİ KORUNDU** — bu turun asıl riski buydu. `RefreshTapText`
her kare koşuyor, yani hüküm dallarına konacak düz bir `Play` saniyede altmış kez ateş
ederdi: yasaklanan "bozuk ses"in ta kendisi, üstelik en gürültülü anda. Üç dala üç ekleme
yerine **zincirin sonunda tek kapı** (`SpeakVerdict`), ve metin değişimi TEK BAŞINA yetmiyor:
`score` bira girerken 1.0'ın etrafında salınıyor ve iyi bir bardak yolda "TOO MUCH HEAD"in
içinden geçiyor, o yüzden kapı hem metnin değişmesini hem MUSLUĞUN KAPANMASINI istiyor.
Hüküm biten bir dökümün yargısıdır, koşarken yapılan yorum değil.

Aynı dikkatle: dolu-bardak kesintisi ile köpük oturması **aynı kenarda birbirini dışlıyor**
(biri doluluktan durdu, diğeri elin bırakmasından — ikisini birden çalmak en önemli kenarda
çift vuruş olurdu); oturma sesi yürüyüşün iki yanından okunan kenarla; sipariş sesi zaten
kenar olan `!view.WasOrdered` koşulunun içinde; ve seviye atlama `_lastFixtureCount`'un
**−1'den başladığını** hesaba katarak — aksi halde oyun başlar başlamaz var olduğu için
oyuncuyu tebrik ederdi, ki o zaman bir şey aldığında tebrik etmesinin bir anlamı kalmazdı.

**RIM TURU DÖNGÜSÜ TEZGÂHIN KURALINI ALDI:** oyunda TEK döngü kanalı var, o yüzden karede tek
karar veren olmalı. `StepRimLap` yalnız **istiyor** (`_rimLoopWanted`), rafın adımı okuyup
temizliyor — imleç ağzın çevresindeki bandan çıktığında tur durakladığı için, doğrudan
başlatılsaydı duraklamış bir tur öğütüp durur, yani takılı bir ses olurdu.

**SESSİZLİKLERİN İTİRAFI:** marketın **beş reddi de** sessizdi — dükkân yalnız yazıyla hayır
diyordu, ellerine bakan bir oyuncu hiçbir şey olmadığını görüyordu; ödemede altı kalemlik
sepete tek `cash` çalıyordu, oysa fiş zaten kalem başı bir satır düşürüyordu (artık satırın
kendi gecikmesiyle sikke); gece sonunun damgası bankanın EN KÜÇÜK sesini çalıyordu; ve
bütün yapımı bitiren tek basış olan **SERVE IT tamamen sessizdi**.

### 9.13 · Odanın kulakları yoktu, ve uğultu müziğe döndü (2026-08-27)

**ÇEKİRDEK HATA: SAHNEDE HİÇ `AudioListener` YOKTU.** Yazar "oyun içi sesleri play modda
duyamıyorum" dedi. Ölçüm zinciri BAŞTAN SONA sağlıklı görünüyordu — Game view'ın mute'u
kapalı, `AudioListener.volume` 1, PlayerPrefs temiz, kaynaklar doğru seviyelerde
GERÇEKTEN çalıyor — çünkü bunların hepsi **GÖNDEREN** taraf. Unity dinleyicisiz hiçbir ses
render etmez ve sahnede tam olarak sıfır tane vardı (`LISTENERS=0`, Main Camera'da da yok).
Koca bir ses bankası mikrofonsuz bir odaya çalıyormuş. **DERS:** alıcı tarafı doğrula,
göndereni değil. Çözüm iki katmanlı: `DebugSceneCreator` kameraya koyuyor (konvansiyonel
yer), `Sfx.EnsureListener` çalışma anında **yoksa** ekliyor (her sahnede ağ). Kanıt:
`AudioListener.GetOutputData` tepe değeri 0.000 → **0.088**.

**UĞULTU KALDIRILDI, YERİNE MÜZİK KONDU** (yazar: "oyunda uğultu sesi var bu gerçekçi ve iyi
değil ... arka planda ortama uygun alttan müzik çalmalı"). Haklıydı ve kusur benimdi:
eski yatak oda tonuna ek olarak neon trafosunu taklit eden **100 ve 120 Hz'de iki sinüs**
taşıyordu. Sabit alçak sinüs bir DRONE'dur — başlangıcı, hareketi ve sebebi yoktur, ve bir
gece boyunca atmosfer olmaktan çıkıp tınnitusa dönüşür. Yerine **müzik**: A minörde
i–VI–III–VII, akor başına sekiz saniye, toplam **32 saniyelik** döngü (bir müşterinin
ziyareti içinde tekrar etmiyor), altında bas notası ve çok altında oda tonu. **−26 dBFS**,
yani eski yataktan DAHA SESSİZ: fark edilen bir yatak fazla yüksektir.

**DÖKME ÜÇ KAPA AYRILDI** (yazar: "suyun bardağa dökülmesi shakere dökülmesi yere
dökülmesi hepsi gerçektiki gibi farklı olmalı"). Fiziksel gerekçe: dökarken duyduğun şey
sıvı değil, **KAPTIR**. `pour_glass` sert, açık, ~700 Hz'de berrak çınlayan bir tüp;
`pour_tin` çelik — daha alçak, çok daha hızlı sönen, madeni parlaklıklı, dar ağız olduğu
için daha az kabarcık; `pour_floor` **hıç rezonanssız** — düz yüzeyin hava sütunu yoktur, o
yüzden geniş, ıslak, sıçramalı ve ÖLÜ. "Çok aşamalı"nın ikinci yarısı çağrı noktasında:
döngünün **perdesi doluluk oranıyla yükseliyor**, çünkü sıvının üstündeki hava sütunu
kısalıyor — bir kabın dolduğunu anlatan tek en tanıdık şey budur. Bira taşarken **döngüyü
dökülme KAZANIYOR**: oyuncunun en çok duyması gereken ve hâlâ düzeltebileceği tek şey o.

**DAMGA DÖRT PARÇA OLDU** (yazar: "damga tam vurulduğunda hissi vermeli"). Tatmin
yükseklikten değil **SIRADAN** gelir: (1) inerken hava, (2) VURUŞ — mürekkep yastığının
kâğıda değmesi, (3) altındaki tezgâhın darbeyi alması, (4) **kalkarken lastik sıyrılması**.
Dördüncüsü kimsenin aklına gelmeyen ve işi BİTİREN parça: kalktığını duymadığın damga hâlâ
sayfaya basılı duruyordur.

**MÜŞTERİLERİN DİLİ — yazar sordu, cevap:** Simlish DEĞİL. Simlish seslendirilmiştir,
sentezlenemez; yerine geçen kırpılmış cıvıltı ise bu oyunun kütüğüyle kavga eder — mekaniği
İNSAN OKUMAK olan sabah 2 Miami barının müşterileri cıvıldayamaz. Onun yerine **MIRILTI**:
birüç formant biçimli hece, alçak ve sıcak, saniyenin üçte birinde biten — ve yalnız
insanın gerçekten bir şey söylediği anlarda (sipariş, tepki, oturma), sürekli değil.
Formant sentezi ses etkisini veren şey: darbe dizisi + üç rezonans = sesli harf, ve
heceler arası rezonans değişimi tutulan notayı içinde kelime olan bir şeye çevirir.
**PERDEYİ TABURE BELİRLİYOR**, yani dört klip altı farklı ses veriyor ve 2 numaralı
taburedeki içici her gelişinde aynı insan gibi duyuluyor. Yayılım bilerek dar
(0.86–1.16): daha genişi alt tabureleri deve, üst tabureleri çizgi filme çevirirdi.
Üst üste konuşma yok — havada ses varsa yenisi beklir, çünkü iki mırıltı birden gevezeliktir.

### 9.14 · İçecek sanatı v4 planı ve pilotu (2026-08-27)

Yazar: *"Tüm içecek assetleri aynı sanata ve uyumluluğa ait olmalı, hepsi tekrardan
üretilecek"* + beş madde (doluluk görünsün; etiket önde/cam arkada; kısa isimler tür
kelimesiyle bitsin; ince siyah kontur; iki sahne iki boyut ama tek kimlik). **Plan
`Docs/PLAN_bottle_art_v4.md`** — on karar, boyut matematiği (96×192 master, mahzen ÷3 =
32×64, ikisi de ekranda 2×), etiketsiz üretim + boru hattında basılan etiket, üç plaka
sandviç, isim tablosu, kanıt kapıları. Boru hattı `Tools/v4_bottles/` (brief/gen/process/
report/palette/fontpx). Teşhis ölçüldü: 29 v3 şişenin 29'u farklı boyutta; v3'ün aracı
`create_map_object` seed de stil referansı da almıyor (sürüklenmenin mekanik sebebi) —
v4 `create_image_pro` (style_image + reference_images + seed, ASENKRON job/get_image).
**Pilot Smirkoff üç seed:** boş (sıvı satırı 0), palet-içi (0), taban bombesi 0.135–0.163,
sıvı kanıtı geçti (kırmızı/mavi kompozitte etiket pikselleri birebir, kavite farklı).
Pilot raporu `Tools/v4_bottles/report.html`; yazar take + amblem + kontur seçecek, seçilen
take **çıpa** olacak. Çalışma zamanı (BottleArt sandviçi, mahzen SpriteMask, BottleH 384,
CellarBottleH 64) pilot onayından SONRA.

### 9.15 · v4 şişeler oyunda: sandviç iki sahnede, votka ailesi çıpalı (2026-09-04)

**Çalışma zamanı kuruldu (PLAN v4 §4c + §12 kademe 1).** `BottleArt` yeniden yazıldı: arka plaka
→ `Clip` (Mask = kavite maskesi) → `Level` (her kare **−tilt** ile ters döndürülen, dünya-hizalı
rect) → içki + yüzey bandı → ön plaka. Sıvı çizgisi şişe eğilince **yatay kalır** — sektörün
standart deseni, shader'sız. Doluluk **hacim-doğru**: maskenin texel'leri eğim kovası başına
(36 × 5°) dünya-yukarıya izdüşümle sıralanıp `fraction`'ıncı texel yüzey oluyor; eğik şişe
dolu miktarını değiştirmiyor. `BottleFill` yalnız v4 plakası olmayan kartların yedeği.
**Mahzen:** slot başına üç `SpriteRenderer` — arka (30), `SpriteMask` altında düz renk 1×1 quad
(31, kaviteye ölçekli, satıra kuantize), ön (32; 31'di, içki etiketi örtüyordu). Doluluk HUD'dan
(`SetCellarPlates/SetCellarFills/SetCellarTones`) — sahne çalıştırmayı okumaz. `CellarBottleH`
62 → **64**, el `BottleH` 300 → **384**, `VesselArt.StandOn(fixedScale)` ile v4 masterı **tam 2×**
(ölçüldü: sabit ölçek olmadan 2.19× duruyordu). `ItemArt.Plates(card, cellar)`; `Bottle` → kapaklı
mahzen kopyası, `BottleOpen` → açık master.

**Bir yan hata bulundu:** `BuildOpenSign` kapısı retire edilmiş `sign_open.png`'yi yüklüyordu;
dosya diskten gitmişti (yazarın çalışma ağacı silmesi), editörün Resources önbelleği tükenince
tabela — ve PlayMode'un bastığı `OpenSignArrow` — sessizce kurulmaz oldu. Kapı artık çizilen
ok. Silme commit'lendi.

**Sanat:** Smirkoff s23 çıpa; Absolve, Gander, Whale (votka ailesi) ona `style_image` +
`reference_images` + seed ile üretildi — tek el (kontak sayfası). Amblemler tek çağrı, indeks 0.
`Tools/v4_bottles/ship.py` yalnız `picks.json`'daki seçimleri `Assets/Resources/Items/v4_*` olarak
gönderir. Tezgâh baseline'ı elde v4 şişeyle yeniden kutsandı.

### 9.16 · Sadeleştirme: etiket üreticiden, kart başına tek seed, önizleme yok (2026-09-04)

Yazar kotayı görünce (1.567 kalan / 10.000; döngü 18 Eylül) sadeleştirdi: *"şekil ve tarzı boş
ver … etiket yazı marka logo her neyi varsa … tek katman … her alkolden 1 alternatif … ön izleme
yapma direkt üret."* Uygulanan: `brief.LABEL` (marka adıyla etiket ve küçük logo üreticiden),
`STYLE` mat/az parlama, `SEEDS = (23,)`, boru hattında etiket basma kapalı (`GENERATED_LABEL`),
filmde baskı pikselleri opak (cam tonundan luma > 46 ya da kroma > +34 uzaklık). Çin partisi
durduruldu (eski brief); etiketsiz votka/cin ham takes `raw/_labelless_v1/`'e arşivlendi; çıpa
hâlâ Smirkoff s23 (stil için; etiket stile girmez). **PixelLab aynı anda 20 iş koşturuyor** —
36'lık kuyruk 20'den sonrasını "rate limit exceeded (20/20 jobs)" ile reddetti; `refill.py`
pencereyi dolu tutuyor. `finish_all` → process → picks → ship, rapor yok.

### 9.17 · Altıncı tur: sıvı kenara değer, boyun eğince dolar, mahzen kopyası yeniden çizilir (2026-09-04)

Yazar oyunda baktı: *"sınırları tam doğru değil, bazı yerler tam kenarına temas etmiyor sıvı;
sıvıyı çevirdiğinde ağza da dolması gerekiyor. Küçük boyutlar çok kötü, etiketler gözükmüyor,
çok kalın kontrasları var — sadece 1 pixel siyah kontras olmalı."* Üç düzeltme, hepsi ölçülerek:

- **Kenar teması** — `process.py` `WALL = 0`: sıvı maskesi cam duvarını artık içeri çekmiyor,
  içki mürekkep halkasına değiyor.
- **Boyun dolumu** — `liquid_mask` boynu dahil BÜTÜN iç boşluğu veriyor; "dolu = omuz" kuralı
  maskeden çıkıp HACİM oldu: `BottleArt.EnsureLut` omuz satırını (medyan gövde genişliğinin
  %88'i kuralı) bulup `_shoulderFrac`'ı (omuz altındaki doku payı) hesaplıyor, `SetLevel`
  oranı onunla çarpıyor. Dik dururken 1.0 omuza kadar; eğince aynı hacim boyna akıyor.
- **Mahzen kopyası YENİDEN ÇİZİLİYOR, örneklenmiyor** — `cellar_render()`: silüet master'ın
  alfasından alan kapsamasıyla (9'da ≥5), iç boşluklar kenardan flood-fill ile doldurulur
  (kapalı kaplarda ince üst elipsin bıraktığı delikler halka geçişinde siyah leke oluyordu:
  cola_marlow 70 halka hücresine 152 mürekkep); cam düz kendi tonu + film + arka gradyan;
  etiket master'da ölçülüp (`label_block`: cam tonundan luma > 46 / kroma > +34 uzak baskı
  pikselleri, en yoğun yatay bant) temiz blok olarak çizilir (kâğıt cam tonuna 34 luma'dan
  yakınsa %80 koyulaşır, ≥5 satır, tek satır mürekkep işareti); kapak yalnız cama çizilir;
  halka tam bir piksel (`peel_and_ring(front, 1, cut=1, peel=False)`).
- **Üreticinin sildiği gövde geri verildi** — `restore_body()`: PixelLab'ın `no_background`'ı
  arka planı renkle keyliyor ve üç kapalı kabın KOYU ön yüzünü de silmişti (cola_marlow gövdesi
  bbox'unun %22'si opak; orange_grove ve cranberry_north ön yüzleri): oda içlerinden görünüyor,
  koyu mahzen zemininde "siyah teneke" sanılıyordu. Kanvas kenarından ulaşılamayan her saydam
  piksel kabın içidir; brief'in istediği renkle (kartın `label_ramp`'i, orta ton, sağ üçte
  birde bir kademe koyu) doldurulur: 7.780 / 6.633 / 6.237 piksel. Cam şişelerde sıfır delik.

38 kart yeniden işlendi ve `ship.py` ile gönderildi (188 plaka). Bu turun mahzen kopyası §9.18'de
değişti; doğrulama ve testler §9.19'un sonunda (EditMode 383/383, PlayMode 7/7, bench baseline
yeniden kutsandı).

### 9.18 · Yedinci tur: mahzen kopyası = master'ın alan ortalaması, cilalı (2026-09-04)

Yazar oyunda 9.17'nin yeniden çizimini gördü: *"şişeler yamık ve kaliteleri çok düşük,
üstlerinde etiket yok veya 1 pixel çizgi halinde var. Büyük halleri güzel."* İki yol denendi,
ölçülerek: (1) **üretim** — master'ın 1/3'ü `init_image` olarak `create_image_pixflux`'a
(32×64, güç 200/300, 55 palet zorunlu; 4 kart × 2 = 8 üretim, çağrı başına 1 kota): gürültü ve
sapma ekledi; (2) **init'in kendisi** — gövdesi onarılmış master'ın alan-ortalamalı (box) 1/3'ü —
sayfadaki en sadık şeydi. Üretim yolu bırakıldı (`cellar_gen.py` pilot olarak duruyor).

`cellar_box()` (`cellar_render`'ın yerine): üreticinin kenar halkası önce soyulur (kenarı
karartmasın), opak hücre = ≥ yarım kapsama, iç delikler komşu ortalamasıyla dolar, her renk
55'e kilitlenir, **etiket** master'da bulunup (`label_region`: gövde rengi alt gövdenin modu,
baskı = gövdeden ≥55 luma koyu YA DA açık pikseller, kapak bölgesi olan üst %30 hariç, 4 px
genişletmeyle harfler birleşir, en büyük blob; kâğıt = bbox'taki baskın renk, işaret = baskın
baskı rengi) küçük kopyada iki renge kilitlenir ve cam ailelerde 1 px koyu çerçeve alır;
cam kapak çizilir; halka tam bir piksel. `label_block`'un cam tonuna göre ölçümü krem gövdede
bütün şişeyi etiket sayıyordu (votka 46×163); luma 34 eşiği de parlama şeridini yakalıyordu —
iki kutuplu 55 eşiği bunları çözdü.

**Kimlik hataları:** brief'te `gin_juniper_crow` ve `tequila_cielo_rojo` yazıyordu; veri
`gin_juniper_crown` / `tequila_cielo_roto` — bu yüzden ikisi oyunda eski sanata düşüyordu.
Düzeltildi, plakalar doğru adla gönderildi, yanlış adlılar silindi. `grenadine_rubis`'in kartı
hiç yoktu; brief'e eklendi, master'ı üretildi (1 çağrı). 39 kart / 194 plaka. Kota: 9.529 / 10.000.

### 9.19 · Yedinci turun denetimi: dört mercekli çapraz sorgu ve kapatılanlar (2026-09-04)

Plakalar, `process.py`, çalışma zamanı ve import ayarları dört bağımsız ajanla tarandı (doğrulama
ajanlarının çoğu oturum limitine takıldı; bulgular elle ölçülerek karara bağlandı). Kapatılanlar:

- **Koyu camda saydam halka** — `plates()` mürekkep halkasını cam tonuna 46 luma yakın bulup
  filme (alfa 77) çeviriyordu (liqueur_kafa 311, rum_windward 348 kenar pikseli). Halka ve
  silüet kenarı artık hiç filmlenmiyor.
- **Mahzende ayak halkası yok (33/39)** — `centre()` ayağı 189. satıra koyuyordu, 189//3 = 63 son
  satır. Ayak H−3'te (188 → mahzen 62, 63 halkaya). Kapak artık ağzın ÜSTÜNE değil ağzın
  üzerine çiziliyor (bir satır üstte, üç satır ağızda): kopya master'ın oranını korur, tam
  kanvas take'lerde (hollow_oak) bile sığar; tek kalan hollow_oak'ın alt halkası (191 satırlık take).
- **Köşelerde çift mürekkep** — silüet yanlara iki hücre atladığında 4-bağlı halka L'nin iç
  köşesini dolduruyordu; `thin_ring` havaya değmeyen halka hücresini gövde pikseline çevirir
  (halka çapraz bağlı kalır; hücreyi saydam bırakmak her omuzda bir iğne deliği açıyordu).
- **Etiket ayakta ölçüldü (sol_viejo)** — `label_region` alt %10'u da dışlar.
- **Maske saydam ön pikseli örtüyor (redline 64,61)** — `cavity()` aralığı master alfasıyla
  keser; `plates()` arkayı maskeyle birebir boyar.
- **`BottleArt` hacim tablosu ±90°** — dökme 118°'ye yatar; yatayı geçince yatay kova okunuyor,
  ağız tarafındaki köşe kuru çiziliyordu (22 doku pikseline kadar). Tablo 72 kova ile tam daire.
  **Yukarı vektörünün işareti tersti** (−tilt): her kova aynalıydı, simetrik kaplarda görünmedi.
  Düzeltildi. Okunamayan/atlas dokular için `textureRect` + uyarı.
- **Mahzen "dolu = omuz"u bilmiyordu** — düz yükseklik oranı boynu dolduruyor, elden 5–11 satır
  yüksek çiziyordu. Omuz tablosu `BottleArt.Upright` olarak paylaşıldı; `SetCellarFills` satırı
  oradan alır. Ofset `localPosition` ile (ölçekli sahnede kaymasın).
- **Mahzen paketleme kanvasla ölçüyordu** — 32 px kanvas × 5 = 30 yuva, 36 marka; opak
  genişlikle paketleniyor (`CellarDrawnWidth`), sprite çizimin merkezine kaydırılıyor
  (`CellarCentreShift`), kapılar da ona göre.
- **Kare başına `Resources.Load`** — `PushPourFill` her karede `ItemArt.Plates` çözüyordu (kapalı
  kaplarda ıska önbelleklenmez); `PourPlates()` kart başına bir kez. `PushCellarFills` yalnız bir
  seviye değişince sahneye yazar.
- **`SetCellar` maskeyi kapatmıyordu**; ölü kod (`origin`, `_surface.enabled = … ? true : true`) silindi.
- **`process.py`'deki ikinci `BRAND_WORD`** (eski kimlikli) silindi, `brief.BRAND_WORD` tek tablo;
  `gen_state.json`'daki bayat anahtarlar temizlendi.
- **Yeni test `V4PlateImportTests`**: her v4 plaka okunabilir, Point, PPU 100, mip yok, kanvas
  96×192 / 32×64, her kartın seti tam — postprocessor derlenmeden inen PNG artık sessizce boş
  şişe çizemez.

Tasarım gereği bırakılanlar: maskedeki #FFFFFF (stencil, çizilmez), 13 el önünde alfa 200
(parlama şeridi), mahzen maskesinin halkaya değmesi (yazarın "sıvı kenara temas etsin" kuralı).

### 9.20 · Tepki artık bir yazı değil, arkadan yükselen emoji zerreleri (2026-09-04)

Yazar: *"'A customer stormed off' yazısı kalkacak, bunun yerine müşteriler içkilerini içtikten
sonra tepkilerini emoji efektleriyle verecek. Kötü, fena değil, güzel/mükemmel için 3 adet
emoji/icon … müşterinin assetinin arkasından küçük küçük partiküller olarak yukarı gidecek …
mükemmelde 20 adet."* Ekranın tepesindeki kırmızı bant kaldırıldı (`_lastStormedCount` sayacıyla
birlikte); sabrı biten müşterinin tepkisi de artık herkesinkiyle aynı dilde: kalktığı taburenin
üstünde birkaç ekşi surat.

- **Üç yüz, `ChromeArt.Face`** (prosedürel, ev kuralı: UI chrome üretilmez): 14×14 kanvasta
  ortak bir disk, sadece AĞIZ değişir — düşük (bad), düz (fair), yukarı (good) — ve çağıran
  taraf ViceRed / Amber / Lime ile boyar. **Mürekkep içeride:** ilk kesim gözü ve ağzı DELİK
  bırakıyordu (yukarıdaki `Mark` ailesi gibi); oyunda bakınca gün batımı duvarında kırmızı
  surat hem hatlarını hem kenarını kaybediyordu, çünkü delikten duvarın kendisi görünüyor.
  Artık her yüzün kendi 1 px halkası ve koyu hatları var — tıpkı yazının iki kez halkalanması
  gibi, aynı sebeple.
- **`ReactionMotes`** (Behaviours): dünya sprite'ları, müşterinin sorting order'ının BİR ALTINDA
  her karede — oturan gövde 25, çıkan 22, zerreler hep arkada. Her zerre kendi anında çıkar,
  kendi yüksekliğine (58–104 birim) kendi salınımıyla tırmanır, omuzdan uzağa yatar ve kendi
  hızında söner; hepsi tek saatte olsaydı perde açılışı olurdu, alkış değil. Sanat 14 px ve bir
  piksel bir sahne birimi çizilir (720p'de iki ekran pikseli), asla ölçeklenmez.
- **Sayı, notun kendisi**: `ReactionFor` memnuniyeti üç banda böler (0.35 / 0.70) ve içinde
  doğrusal sayar — 4–7 kötü, 8–13 fena değil, 14–20 güzel; tam memnuniyet tam 20 eder.
- **An**: `TasteMotes`, servisten **0.9 sn** sonra. Ölçüm: içme klibi iki yarımın birleşimi, yudum
  ORTA kare, `DrinkTicks` ondan önce 10 tik tutuyor (12 fps → 0.83 sn). Tekrar ısmarlayan müşteri
  (OrdersAgain) klibi hiç oynatmaz, aynı vuruş orada da okunur. Sabrı bitende burst kalkış
  dalında, tabureye çivili (takip etmez: giden birini kovalayan bulut kuyruklu yıldıza benzer).
- Servisteki söz balonu ("PERFECT!" / "THANKS." / "NOT WHAT I ASKED") duruyor: emoji ne kadar
  beğendiğini söyler, söz neyin yanlış gittiğini.

Kaldırılan bantla birlikte `patience_warn` klibi de bağlantısız kaldı — bilerek: kalkış dalı
zaten `upset_sfx` + `voice_upset` çalıyor, üçüncü ses yığın olurdu. Klip bankada duruyor ve asıl
işi için (sabır bitmeden UYARI) hazır bekliyor.

EditMode 383/383, PlayMode 8/8.

### 9.21 · Sabır üç banda bölündü, saat bahşişin çarpanı oldu (2026-09-04)

Yazar: *"Sabır barını 3'e böleceğiz. Kırmızı, sarı, yeşil — böylece hızlı servis etmenin de
önemi artacak, bahşişi arttıracak. … Sabır barı için profesyonel bir ui üret, temaya ve
renklere uyan, miami 80s'lere uygun."*

**Kural (Core).** `ServiceBand {Green, Amber, Red}` ve eşikler `ServiceJudge.GreenBand = 1/3`,
`AmberBand = 2/3` — beklemenin HARCANAN payına göre. `SpeedScore` artık düz `1 − bekleme`
değil, band kenarlarında kırılan sürekli bir eğri: yeşilin dibinde **0.75**, sarının dibinde
**0.30**, sonunda 0. `CustomerVisit.Band` bu bandı verir, böylece kafanın üstündeki bar ile
kasa aynı üçlemeyi okur. (Bu bölüm yazıldığında iki ayrı saat vardı ve band "hangisi
işliyorsa" ona bakıyordu; **§9.22 ikisini tek bara indirdi**.)

**Saat toplamdan çıktı, çarpan oldu.** Ölçüm: hız 0.35 ağırlıklı bir terimken, diğer üç terim
doluyken müşteri kalkarken verilen içki hâlâ anında verilenin **%65'ini** bahşiş alıyordu (10$
içkide 6$ karşı 10$). Ağırlığı 0.45'e çıkarmak da yetmedi — ağırlıklı bir terim "çok geç"
diyemez, ancak bir çarpan diyebilir. Şimdi: `earned = 0.40 craft + 0.30 accuracy + 0.30 fill`
(toplamı 1) ve `quality = earned × (ClockFloor + (1 − ClockFloor) × speed)`.
`TipCeiling 1.0 → 1.15` (anında servis eskisinden İYİ öder) ve `ClockFloor = 0.35`.

**Taban ölçüyle kondu.** Tabansız ilk hâl (saf çarpan) 200 koşuda iflası %2 → **%100** yaptı,
bot 21. günde ölüyordu (serve başına bahşiş 4.65$ → 2.60$, gelir 134$ → 85$/gün): geç içki de
içkidir, birileri onu yaptı. Tabanla: **iflas %1.0**, medyan kasa $136, gelir $131.7/gün,
bahşiş serve başına $4.42 — yani hızlı bara eskisinden fazla, ağır bara belirgin az.
(Rapor dosyası bu turda yazarın kendi meşrubat fiyat çalışmasıyla birlikte koştu; sayılar
ikisinin toplamı, commit'e girmedi.)

**Gauge (UI).** Aynı evin aleti: `ChromeArt.GaugeTube` gövde + `GaugeGlass(w, h, 3)` cam —
üç adım istendiği için camdaki iki çizik tam band sınırlarına düşüyor. Boş şerit üç bandı
kendi koyu tonlarıyla taşıyor (sol kırmızı, orta sarı, sağ yeşil), dolgu canlı band rengi,
altında bandın rengini alan bir neon şerit (tezgâhın kendi numarası), kırmızı bandda hafif
nabız (Motion.Reduced'da yok). Sipariş-alınma saatinin magenta rengi kalktı: üç band bekleyişin
tamamı için konuşuyor, hangi aşamada olunduğunu balon zaten söylüyor.

**Tepki içki BİTİNCE veriliyor.** *"Verilen emoji tepkileri içkiyi bitirdikten sonra
verilmeli."* `TasteMotes` (servisten 0.9 sn sonra) kaldırıldı; burst kalkış dalında, boş bardağı
bırakıp kalktıkları anda atılıyor — sabrı bitenle aynı yerde, aynı dilde.

**Tek yıldız, tek kalp.** *"Bundan sonra oyunda kalp ve yıldız iconu olarak her yerde bunları
kullanacaksın."* Oyun üç ayrı yıldız sayıyordu (yazarın gölgeli `star3d`'i, düz beyaz
`Items/star`, `ChromeArt.Mark("star")`). Artık `ItemArt.Star(lit, px)` ve `ItemArt.Heart(lit, px)`
— iki durum (yanık / yuva), iki boyut (16 ve 32; 32'lik ikon 14 px kareye sıkışınca çamur olur,
şişe dersi) ve **kendi rengini taşırlar**: çağıran yalnız alfa ile karartabilir. Kalp yoktu,
`Tools/heart_icon.py` yıldızın kuruluşuyla çizdi (iki lob + uç, 1 px mürekkep, üç ton, aynı
parıltı); `Tools/icon_sizes.py` 16'lıkları master'dan türetir (halkayı soy → alan ortalaması →
palete kilitle → 1 px halka). Kalbin ilk işi: ehliyette ilişki rütbesi üç kalple
(`Relationships.ForSatisfiedVisits`: Stranger 0 … Confidant 3).

**Backbar 10 px yukarı.** `DrawerTravel 121 → 131` (odanın kendi pikseli, ekranda 20).
Bench look baseline'ı bu yüzden yeniden kutsandı.

EditMode 389/389 (6 yeni band testi), PlayMode 10/10.

### 9.22 · İki saat tek bara indi; sipariş almak barı sıfırlamıyor, bir kutu ödüyor (2026-09-04)

Yazar: *"Mevcut sabır barı 3 kutudan oluşuyor, sipariş almak barı 0lamaz +1 kutu daha ekler."*

**Neydi.** 2026-08-02'de bekleyiş ikiye bölünmüştü: `OrderPatienceSeconds` (asked olmayı
bekleme, gün 1'de ~30 sn) ve `PatienceSeconds` (içkiyi bekleme, ~50 sn). `InspectId()` birinciyi
bitirip ikinciyi **tepeden** başlatıyordu. Ekranda bunun anlamı, tabureye gidildiği anda barın
ağzına kadar dolmasıydı — yani gösterge "bekleyiş henüz başlamadı" diyordu, oysa müşteri
oturalı yarım dakika olmuştu. Bahşişin hız çarpanı da aynı yerden sıfırlanıyordu.

**Kural (Core).** Tek saat. `PatienceLeft` müşteri kararını verdiği an işlemeye başlar ve içki
gelene kadar işler; sipariş alınmaması da aynı barı harcar ve barı biten müşteri, içkisi
gelmeyen müşteriyle aynı şekilde çekip gider. `InspectId()` artık şunu yapar:

```
PatienceLeft = Min(PatienceMax, PatienceLeft + PatienceMax × OrderTakenPatienceBonus)
```

`OrderTakenPatienceBonus = 1/3` — göstergenin üç kutusundan tam biri. `Min` tavanı yüzünden
ödül **geç kalınan taburede gerçek, hemen gidilen taburede görünmez**; bar hiçbir zaman dördüncü
bir kutu göstermez, çünkü kasa tam üç bandın üçte birleriyle ödüyor. Fazladan tur (`Resolve`'un
`ExtraOrderPatienceRefill = 0.8` dolumu) bu kutuyu almaz: o içki bar boyunca istenir, kimsenin
yürüyüp sorması gerekmez. `OrderPatienceSeconds` / `RollOrderPatience` / `OrderPatienceMax` /
`OrderPatienceLeft` silindi; `AwaitingOrderTaking` kaldı ama artık yalnızca balonun hangi
cümleyi göstereceğini söyler, saat seçmez.

**Ölçüm (200 tohumlu koşu, tek taburede sırayla çalışan meşgul bot).** Eski iki saat → yeni tek
saat: storm-off **%28.4 → %7.4**, servis anında harcanmış bekleme **%8.2 → %34.8**, servis
bandları yeşil/sarı/kırmızı **51791/2188/14 → 29638/18167/9847**, serve başına bahşiş
**$3.46 → $2.93**. Yani üç band ilk kez gerçekten kullanılıyor: eskiden 54 bin serviste
**14 tanesi** kırmızıydı, çünkü gösterge sipariş alındığında doluyordu — §9.21'in yazdığı band
sistemi fiilen dekoratifti. Kaybedilen müşterinin çoğu da içkiyi beklerken değil, kimse
gelmediği için gidiyordu.

**`PatienceSeconds` bilerek değişmedi** (50 − 2.5·gün, taban 22). Tek saat toplam olarak eski
asking-saatinden uzun olduğu için gece belirgin şekilde daha af edici; bunu geri almak ayrı bir
denge kararı ve kendi ölçümünü hak ediyor, bu değişikliğe sessizce binmemeli. Yazarın kararı:
süre kalsın.

### 9.23 · Odanın kendi puanı: konfor, tezgâhın gecesi, merdiven (2026-09-05)

Yazar: *"Oyuncular hem alkolü puanlar hem mekanı, 2 ayrı metrik olacak … bu ikisi ayrı metrikler
olacak fakat ortak yıldızlar olacak. … Tezgahta müşterilerin bıraktığı bardakları toplaman
gerekecek … bardaklar toplanmadıysa, tezgah silinmediyse bu konfor puanını düşürecek."*
Tasarım `GDD/27`, faz günlüğü `PLAN_house_and_law.md` (H1b: Core kablolandı; H4 bez, lavabo
suyu ve ekran sonra).

**İki puan, ortak yıldız (Core).** `ServiceTonight = min(5×ortalama memnuniyet, MenuStarCap)`;
`ComfortTonight = clamp(ComfortBase − 0.75 × (1 − temizlik), 0, 5)`; gecenin yıldızı
`min(servis, konfor)` — `StarCeiling` artık `min(ComfortTonight, MenuStarCap)`, eski
`UpgradeStarCap` `ComfortBase`'e dönüştü: `2.0 + Σ fikstür comfort (yalnız ayakta duran basamak)
+ 0.5 × bardak adımı tavanı + 0.25 × ek tabure`. Fikstürün `comfort`u VERİ (`fixtures.json`,
`FixtureDefinition.Comfort`; odayla gelenler 0 taşır, üstü örtülen basamak sayılmaz). Yarının
kalabalığı SERVİS tarafını okur (`CrowdStarsTonight`), kir tek başına kalabalığı yoksullaştıramaz.
`DayDetail.ServiceStars/ComfortStars` fişe ve deftere yazılır (sor-sonra-kapat testleri).

**Tezgâhın gecesi (`Housekeeping`, `BarDay.House`).** İçki SERVİS EDİLEN ayrılan tezgâhta bir
`CounterMess` bırakır: boş bardak (toplanana dek tabureyi tutar) + leke (silinene dek). Yedi
saniyelik `BusSeconds` kendini-temizleme EMEKLİ; hiçbir şey kendiliğinden gitmez. Sinyal
`CustomerVisit.DrinkServed` (yalnız `ServeTo` kurar): fırtına giden, reddedilen sipariş (eskiden
görünmez bir bardak bırakıp tabureyi 7 sn kilitliyordu — C6 hatası kapandı) ve evin misafiri hiçbir
şey bırakmaz; eşleşmeyen döküm yine bardak bırakır. Fiiller `TycoonRun.CollectGlass(mess)` (bardak
ele, tabure anında boş), `Wipe(mess)` (bardağın altı silinmez — önce topla), `WashGlasses()`
(lavabo `1.5 + 0.5×n` sn çalışır, meşgulken ikinci yıkama bekler), hepsi `DayOpen` kapılı.
**Tolerans 10 sn**: bir pislik bu süreden sonra her saniye koltuk-saniye yazar;
`Cleanliness = clamp(1 − koltuk-saniye / (tabure × Floor.Elapsed), 0, 1)`. Kapanış bloğu
(`Floor.IsComplete`) önce `House.CloseNight()` çağırır: eldeki ve lavabodaki bardaklar bedava
yıkanır, tezgâhta kalan zaten ödenmiştir. `ComfortNow` canlı okuma (toleransı geçmiş nokta / tabure).

**Sahne (H4, aynı gün).** `ForTheScene` lekeleri AÇIK geçirir — sahne artık sim ve testlerle aynı
kuralın tamamını öder. Boş bardak TUTULUR (basılınca Core `CollectGlass`, tabure o an boşalır;
bardak eli izler — lavabonun üstünde bırakılırsa yıkanır, başka yerde elde kalır ve lavabonun
üstündeki şerit "n IN HAND · CLICK THE SINK" yazar); altındaki leke çizili bir iz (`ChromeArt.Smudge`,
tabure başına) ve BEZ (`ChromeArt.Cloth`, tezgâhın sol ucunda x60) alınıp üstünden geçirilince siler
(bardağın altını Core reddeder, ret bir kez toast); lavabo tıklaması eldekileri yıkar
(`WashGlasses`; "NOTHING TO WASH" / "THE TAP IS RUNNING"), su `WashSecondsFor(n)` boyunca kabın
üstünde kare-sayfa olarak akar (`fx_sink_water`, `Tools/sink_water_gen.py` lavabonun siluetinden;
hücre boyu `fixtures.json`'daki `cellW/cellH` — TV'nin kesicisi de artık hücreyi veriden okur) ve
`tap_water` döngüsü çalar (rim döngüsüyle aynı kanal, rim öncelikli). Eşleşmeyen dökümün bardağı da
sahnede duruyor.

**İki sembol (H5, aynı gün).** Üst şeritte yıldız bloğunun solunda iki beşli şerit: **kalp** =
gecenin servisi (`ServiceTonight`), **madalyon** = odanın o anki konforu (`ComfortNow`, tezgâhta
bardak dururken düşen tek okuma); sayı yok, dolgu okumadır (C11 korundu: yıldızın altına bir şey
girmedi). Fişte puan satırının altında ev satırı (`BillHouse`: SERVICE ♥ n.n · COMFORT ◉ n.n,
düşük olan puanın mürekkebiyle — gece o olarak dosyalandı). Ayakta duran tahtada TONIGHT'ın
üstünde SERVICE ve COMFORT satırları kendi sembolleriyle (`StandRow` birim sprite alır); iki tahta
420 → 460. Yükseltme kartları "Mark n of N · +0.4 comfort to the room" der. `ItemArt.Medal`
yıldız/kalbin yanında (tek çizim, iki hâl, iki boy, boyanmaz).

**Veri.** 25 parçaya `comfort`; üç masa yuvası üçer basamaklı merdiven (`table_{left,mid,right}_{1,2,3}`,
rustik/pirinç/çelik, aynı sanat); `plant_monstera` yetim `fx_monstera` ile `plant_right` 3. basamak.

**Ölçüm (`LastCall → Simulate Tycoon 200 Runs`, `LastCall → Measure Housekeeping`).** Bot
`fixtures.json` yüklüyor, tezgâhı anında topluyor/siliyor/yıkıyor ve gecede bir kez dolar başına en
çok konfor veren açık basamağı alıyor (musluk hariç). İlk ölçüm (v0: ceza 1.0, tolerans 6 sn,
fikstür değerleri yarısı, bot en ucuzu alıyor): 20 sn'de pisliğe ulaşan el yarım yıldız ve iflas
%4→%13, 30 sn'de %41; en ucuz basamağı alan bot %0→%4 iflasla DÜŞEN itibar — fikstürler dolar
başına bardak adımının 2–4 katı pahalıydı. v1 (ceza 0.75, tolerans 10 sn, değerler ×2, değere göre
alım) ile 200 koşu: iflas HEAD raporunda 2 (1.0%) → **3 (1.5%)**; kasa medyanı
$84 / $136 / $199 → **$64 / $76 / $87**; gelir/gider $131.7 / $127.5 → $129.9 / $127.9; itibar 2.71 stars →
2.66 stars; servis / konfor gece ortalaması **2.94 / 2.99**; temizlik 100%; konforun geceyi
tuttuğu geceler 2784 (46.5%); yoksul kalabalık çekilen gece 0 (0.0%); konfor tabanı 10/20/30. gün
medyanı 2.50 / 3.35 / 3.83; 2.5★'a ulaşan 196 (98.0%) → 196 (98.0%) (gün p25/p50/p75 20 / 21 / 22 →
21 / 22 / 23); 3.0★ 24 (12.0%) → 8 (4.0%). Dört şekil (100 koşu, aynı tohumlar):

| 1 · instant, no dressing | 0.0% | $134 | 10.2 | 2.96 | 2.85 | 100% | 64.1% | 0.0% | 2.65 | 100.0% | 2.0% |
| 2 · instant, buys dressing | 1.0% | $76 | 10.2 | 2.94 | 3.05 | 100% | 42.1% | 0.0% | 2.68 | 100.0% | 6.0% |
| 3 · never wipes or washes | 6.0% | $64 | 10.2 | 2.90 | 2.67 | 53% | 71.0% | 0.0% | 2.44 | 84.0% | 0.0% |
| 4a · 10 s to the mess | 1.0% | $73 | 10.2 | 2.94 | 3.05 | 100% | 41.3% | 0.0% | 2.69 | 99.0% | 8.0% |
| 4b · 20 s to the mess | 1.0% | $76 | 10.0 | 2.97 | 2.96 | 91% | 53.5% | 0.0% | 2.66 | 99.0% | 6.0% |
| 4c · 30 s to the mess | 8.0% | $69 | 9.4 | 2.98 | 2.73 | 82% | 72.3% | 0.0% | 2.51 | 83.0% | 7.0% |

Okuma: 1 = bardak payının yarıya inmesinin bedeli; 2 = yeni taban; 3 = çürüme (konfor tabanın
altında, itibar durur, yoksul gece ARTMAZ); 4 = insan eli, `DirtPenalty`/`DirtGrace` bu satırdan
seçildi. EditMode 452/452 yeşil.

### 9.25 · Ev sahibi konuşur: dersler ve kitaptaki açık hesap (2026-09-05)

PLAN_last_call S5'in Ece'ye kalan iki yarısı (`5948a965`). Ödül satırı GDD 26 §12.3 ile
(2026-08-14) çoktan ters çevrilmişti — beat ödemez, kazandırdığı şeyler onu ADLARIYLA kilit
yapar (`unlockBeat`) — ama `story.json`'daki yedi ders ayrıştırılıp **hiçbir yerde söylenmiyordu**
ve kitap açık hesabı göstermiyordu.

- **Dersler (Core):** `StoryCue` koşullarını Core gözler — ilk kapı (ctor ve `ContinueToNextDay`),
  kimsenin kartı okunmamışken bekleyen biri ve tin'de karışmamış iki alkol (`Tick`'te),
  ilk fıçı (`BeginPull`), ilk market ve kira altında kapanan gece (kapanışta), ilk ekstra tur
  (`ServeTo`), ve **bu hafta** gelen misafirin rafta olmayan stili (kapıda; ark boyunca ilk
  `needStyle` taşıyan beat'e bakar — silahlı beat Ece'nin sessiz Pazartesi'si olabilir).
  `StoryProgress.Learn(cue)` koşu başına bir kez; `TycoonRun.LessonDue` sırayla, `HeardLesson()`
  düşürür; ders yazılmamış cue sessizce harcanır; hikâyesiz koşuda hiçbiri yok. Rastgelelik yok,
  bot ders okumaz (kuyruk en çok sekiz).
- **Dersler (UI):** açık gecede diyalog plakası — Ece'nin adı ve yüzü, satır başına GO ON, sonda
  GOT IT, SAY NO yok; beat oynuyorsa beat kazanır ve ders kuyrukta bekler. Kapanışın iki dersi
  (ilk market, kırmızı gece) markette 98 mesaj kutusu (`BuildHostNote`, kapanış sorusunun
  penceresi), Escape aynı tuş. Ece'nin yüzü kadroda olmadığından plaka şimdilik adıyla ve boş
  kuyuyla konuşur (kadroya alınıyor: `Tools/patron_prompts.py` "ece").
- **Açık hesap (GDD 26 §5):** `StoryProgress.CurrentAsked` (kaçırma/geri çevrilme ile açılır,
  beat tutulunca kapanır). Kitabın başlık sayfasında haberlerin üstünde OPEN TAB: sorulmuşsa
  "<AD> WANTS <İÇKİ> · <GECE>", ilk ziyaretten önce ise `needStyle` varken ve gecesi bu hafta
  ya da gelecek haftaysa "GET <STİL> IN · <AD> COMES <GECE>".
- **Doğrulama:** `StoryLessonTests` 13 test (473/473), PlayMode 10/10, oyunda fotoğraf
  (plaka, market notu, kitap sekmesi).

### 9.24 · Kapı: 20 yaş, ödünç kimlik, kick, ceza, teşekkür (2026-09-05)

Yazar: *"20 yaş altı kişiler alkol alamayacak … kimliğin üstündeki butondan 'kick'leyebileceksin,
aynı zamanda sahte kimlik de işin içerisine eklenecek. Sahte kimlikli birisine alkol vermenin
büyük para cezası olacak … gelişmişlik seviyesine göre … doğru şekilde kovması ise gün sonunda
küçük bonus paralar verecek."* Tasarım `GDD/28`, faz günlüğü `PLAN_house_and_law.md` H2b
(Core) ve H3 (ekran, aynı gün — aşağıda).

**Evrak kişinindir ve gizlidir (Core).** Her yeni gelen `NextArrival`'da bir kez, `"papers"`
akışında `IdPapers.Roll(gün, kayıt yaşı)` alır (`RegularState.Papers`, Core'a ÖZEL; dışarıdan
tek kapı `CustomerVisit.Papers`, kart okunana dek **throw eder** — siparişin kuralı). Dürüst
yetişkin kayıt yaşını taşır; `MinorChance(gün) = gün<2 ? 0 : min(0.12, 0.03+0.01·gün)` ile
gelen reşit olmayanların yarısı (`ForgedShare`) ÖDÜNÇ kart (21–27 basar, gerçek 18–19), yarısı
kendi yaşını basar. `LooksYoung` odanın görebildiği tek gerçek: her reşit olmayan genç görünür,
dürüst yetişkinlerin %25'i de — yüz şüphe sebebi, hüküm değil. Regüler olmayan bir koşuda
(`archetypes` yok) evrak yok, reşit olmayan yok.

**Kick (`TycoonRun.Kick(visit)`).** Beş kapı: gün açık; evin misafiri asla (throw); oturmuş ve
bekliyor; **kart okunmuş** (kör kick throw); hiç servis edilmemiş (`Paid == 0` — "kart senin
anındı"). Doğru kick (reşit değil ya da sahte): `VisitState.Kicked` + `OffTheBooks` — `BarDay.
FinishedCounted`/`AverageSatisfaction` misafir gibi atlar (ne SERVED ne WALKED, not yok), kişi
`Barred` (kayıt bir daha göndermez, çekiliş harcanır), `RightKicks++`. Yanlış kick (dürüst
yetişkin): 0 memnuniyetle defterde walk-out, regüler 0'ı hatırlar, `WrongKicks++`, ceza yok.
Kick edilen tezgâhta hiçbir şey bırakmaz (`DrinkServed` false).

**Servis edilen reşit olmayan.** Servis geçer, öder ve bahşiş verir; `ServeTo` `FineOwed =
20 + 20 × floor(itibar)` yazar (`IdPapers.FineFor`), fazladan tur vermez (hüküm `OrdersAgain=false`
ile yeniden basılır), `MinorsServed++`. Ceza **kalkarken**, hesaptan SONRA, bir kez
(`SettleDepartures`, `visit.Fined`); kasa eksiye düşebilir (kira gibi ikinci ev sahibi), kırmızı
sayılır. `DayFines` gidere, `DayBonus` gelire girer: `DayIncome = satış + bahşiş + teşekkür`,
`DayExpenses = kira + stok + dükkân + ceza`.

**Devletin teşekkürü.** Doğru kick başına `KickBonus = $5` (bir kuyu içki), kapanış bloğunda
kirayla birlikte ve ondan önce ödenir (`Floor.IsComplete`, fiş kasayı doğru basar), gecede bir
kez; `DevJumpToNight` gece oynamaz, ödemez. Beş sayaç iki sıfırlama noktasında da sıfırlanır
(`DayFines/DayBonus/RightKicks/WrongKicks/MinorsServed/MinorsMet`); `DayDetail`/`DayResult` hepsini
taşır.

**Bot (`TycoonSimulator`).** Her kartı okur okumaz `KickIfDue`: reşit olmayanı/sahteyi kapı
gösterir; `Hands.MisreadId` (kendi `"door"` akışından, ziyaret başına bir çekiliş) kartı kaçıran
eli ölçer (taban bot 0). **200 koşu:** oturanların 3404 / 3404 / 0 (5.4% of seats); yanlış kick / kaçırılan kart 0 / 0;
ceza $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 · 3★ $0.00; teşekkür $17020 · 2.1%. Kapıdan önce → sonra: iflas 3 (1.5%) → 1 (0.5%); kasa $64 / $76 / $87 → $65 / $77 / $87;
itibar 2.66 stars → 2.71 stars; gece başına müşteri 10.2 → 10.0; gelir/gider $129.9 / $127.9 → $133.4 / $131.2.
EditMode 460/460 yeşil.

**Ekran (H3).** `papers.json`'da dört yüz `"young": true` (clubgirl, pastelman, eastasianman,
leopard); `LookFor` `LooksYoung` bir ziyareti o havuzdan çizer (reşit olmayanlar da, genç görünen
yetişkinler de). **Ödünç kart** kartta BAŞKASININ evrakını basar — `LenderFor` kişi başına bir kez,
kimlik id'sinin kararlı hash'inden (kendi yüzü, evraksız yüz ve o an başka taburede oturan yüz
hariç): fotoğraf, ad, yaş, ülke, bayrak ödünç verenin; kart okununca başın üstündeki fiş ve günlük
de kartın adını basar (ad ikinci bir ipucu olmasın diye); dürüst reşit olmayanın kartı gerçek yaşı
(18–19) basar; ziyaret sayısı ve bağ KİŞİNİN. **KICK tuşu** kartın üst bandında bayrağın solunda
(tezgâhın kırmızı `KeyCap`'i), misafirde gizli; ziyareti yerel değişkene alır, `Run.Kick` çağırır,
kartı kapatır, `SHOWN THE DOOR · UNDER AGE / BORROWED CARD / THEY WERE OF AGE` yazar (ret Core'un
kendi sözüyle). Kick edilen fırtına yoluyla çıkar, tepki yok, günlükte sebep. **Fiş:** `THANKS · n
SHOWN OUT` gelirde, `FINES · UNDER AGE / BORROWED CARD / UNREAD CARD` giderde (yalnız oluştuğunda,
kendi işaretleriyle), toplamlar `DayIncome`/`DayExpenses`; kapı gösterilen ne SERVED ne WALKED.
**Defter:** satırda `thanks $n`, `fines $n`, `n shown the door`.

**İkinci ipucu — DEĞİŞTİRİLMİŞ kart (H6, aynı gün).** Sahte kartların yarısı (`AlteredShare` 0.5)
artık ödünç değil değiştirilmiş: kendi yüzü, kendi adı, yılı 21–24'e çekilmiş — ve ülkesinin
OLMAYAN bir bayrak (`WrongFlagFor`, kişi id'sinden kararlı; kadronun çizili bayrakları arasından).
`"papers"` akışında yalnız sahte kartlı reşit olmayan için bir çekiliş daha. Günlük ve fiş
`altered card` / `ALTERED CARD` der; bot ayırt etmeden kapı gösterir.

## 10 · Teknik omurga

- **6 asmdef:** Core (saf C#, motor erişimi imkânsız) ← Game ← UI ← Editor; Tests → Core+Game; PlayTests (2026-08-12) sanal fareyle gerçek sahneyi oynar — UI'ın içine değil, ekrana ve Core durumuna bakar.
- **Determinizm:** `RunRng` (FNV-1a→PCG32) adlı akışlar: arrivals, orders, patience, decide, customer, read. `System.Random`/`UnityEngine.Random` yasak.
- **Veri:** 6 JSON, `JsonUtility` + gürültülü doğrulama; tarifler çift kaynak (json+katalog) parite testli. **`story/story.json` 2026-08-13'te yüklenir oldu** (`DataLoader.ParseStory`): kadro + tarif kataloğuna karşı kurulur; bilinmeyen look/tarif/gece, sessiz geceye yazılmış misafir, iki host, kimsenin izlemediği ders adı, hiçbir yere çıkmayan beat yüklemede patlar. Yazım kuralı da orada: `needStyle` isteyen beat, o stili `hostWarning` satırında **adıyla** söylemek zorunda. Bootstrap boot'ta ayrıştırır ama koşuya henüz vermez (`storyInPlay` kapalı — diyalog plakası S3'te).
- **Araçlar:** LastCall menüsü — Create Debug Scene · Simulate Tycoon 200 Runs · Measure Service Speed Response.
- **Doğrulama:** 281 EditMode testi (12 dosya) + 7 PlayMode testi (4 duman + 3 piksel taban resmi, `Baselines~`); sim botu gerçek oyuncu fiilleriyle 200 koşu, `Docs/tycoon_sim_report.md`.
