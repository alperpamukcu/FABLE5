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
- **İKİ PUAN, ORTAK YILDIZ (2026-09-05, GDD 27; §9.23):** gecenin yıldızı `min(servis, konfor)`. **Servis** = `min(5×ortalama memnuniyet, MenuStarCap)`; **konfor** = `ComfortBase − 1.0 × (1 − temizlik)`, `ComfortBase = 0 + Σ fikstür `comfort` (yalnız ayakta duran basamak) + 0.5 × bardak adımı tavanı + 0.25 × ek tabure` (**taban 2026-09-06'da 2.0'dan 0'a indi — çıplak oda hiçbir şey etmez, gece 0 yıldız dosyalar; §9.35**; eski `UpgradeStarCap` bu tabana dönüştü; `MenuStarCap` gece servis edilen en iyi Exact ranka göre 2.0→5.0 aynen). Yarının kalabalığı SERVİS tarafını okur (kir tek başına kalabalığı yoksullaştıramaz).
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

### 9.26 · Duvar bir merdiven: yazarın dört oda plakası (2026-09-06)

Yazar: *"Seninle paylaştığım görseller tüm arkaplanı değiştirerek oyunda duvar geliştirmesi
olarak sunulacak. club_room4 başlangıç barı olacak. 1-2-3-4 diye gidiyor."* (`2608fd6e`)

- **Veri:** `fixtures.json`'a `walls` yuvası (`backdrop: true` — parçası çengele dikilmez, sprite'ı
  odanın arka plakasının YERİNE geçer; x/y hükümsüz) ve dört basamak: `walls_1` Cracked Plaster
  (bizim, $40, 0 konfor), `walls_2` Fresh Plaster $70 · +0.3 (1.0★), `walls_3` Panelled Wall
  $130 · +0.6 (2.0★), `walls_4` Harlequin Paper $200 · +1.0 (3.0★). Plakalar
  `Resources/Fixtures/fx_walls_1..4.png` (640×360; yazarın club_room4 → 1'i, o sırayla). Market
  karosu için her basamağın KENDİ duvarından kesilmiş 64×48 `fx_walls_swatch_N`
  (`FixtureDefinition.Swatch`, satırdaki `swatch`): odayı pula küçültmek bulanıktı, pencere 1×
  keskin. Merdiven kuralları aynen (`CanBuyRung`, rung n+2 gizli, yalnız en üst basamak çizilir).
- **Sahne:** `StageSlot.Backdrop`; `DiegeticStage.SyncFixtures` backdrop yuvasındaki (en üst)
  basamağı `_backgroundSr.sprite`'a yazar. Sahnenin serileştirilmiş plakası artık `fx_walls_1`
  (Create Debug Scene ile yeniden kuruldu), `Assets/Art/Backgrounds/club_room.png` silindi. Kapıdaki
  tabela dört plakada da **+20 ONLY** — GDD 28'in yaşı.
- **Konfor bütçesi** 8.9 → 9.9 (tavan 5.0). **Ev simi A/B** (aynı build, aynı tohumlar, 100 koşu;
  merdivensiz → merdivenli), basamak alan bot (şekil 2): konfor gece ort. 3.13 → **3.13**, konforun geceyi tuttuğu geceler 40.5% → **40.5%**, itibar 2.73 → **2.73**, 3.0★'a ulaşan 18.0% → **18.0%**, iflas 0.0% → 0.0%, kasa medyanı $75 → $75. Alışveriş yapmayan bot
  (şekil 1, konfor/itibar/3.0★): 2.88 / 2.69 / 5.0% → 2.88 / 2.69 / 5.0% — merdiven yalnız alanı taşır. 200 koşu raporu
  bayt-aynı (zemin botu alışveriş yapmaz).
- **Testler:** `FixtureTests` +2 (backdrop/swatch ayrıştırma; gerçek dosyada dört basamak, çatlak
  açılış, plakalar ve örnekler Resources'ta); oyunda üç fotoğraf (açılış odası, UPGRADES rafı,
  2. basamak alındıktan sonraki gece).

### 9.27 · Market bir geliştirme ekranı: raflar, gizli alınanlar, ikonlar; oda çıplak açılır (2026-09-06)

Yazar: *"mevcut tablo mevcut duvar lambası, halı ve televizyon bunların hepsi upgrade olmalı, upgrade
kısmını güzelce gruplandır karışık gözükmesin, satın alınan eşyalar gözükmemeli, barmat upgrade değil.
Upgrade görsellerinde ürünün resmi yerine geliştirme iconu gibi bir görsel üretilsin ... duvar iconu
üstünde yukarı yeşil ok, mobilya geliştirmelerinde sandalye iconu üzerinde yeşil ok gibi."* (`f37db211`)

- **Oda çıplak açılır:** `flamingo_triptych` ($45 · +0.2, resim merdiveninin 1. basamağı), `wall_lamps_one`
  ($30 · +0.1), `floor_rug` ($35 · +0.2), `wall_tv` ($70 · +0.2) artık `startsInTheRoom` değil — satın alınır ve
  konfor taşır. Gece birde odada yalnız paspas, tek musluk, çelik lavabo ve çatlak duvar var (FreeBase — 2026-09-06'dan beri 0; oda hiçbir şey etmez, §9.35);
  paspas geliştirme değil, hep evin. Duvar lambası alınmadan oda gece cam ışığı ve genel yıkamayla loş — yazarın
  kararı, harabe barın kendisi.
- **Raflar:** her fikstür `group` adlandırır (walls/light/furniture/greenery/counter — `FixtureDefinition.Group`);
  UPGRADES rafı SEATS & BAR, GLASSWARE, THE WALLS, THE LIGHT, FURNITURE & FLOOR, GREENERY, THE COUNTER sırasıyla
  kurulur (kararlı sıralama, raf içinde dosya sırası). **Alınan gözükmez:** sahip olunan parça (döşenmiş basamak,
  alınmış tekil, evin paspası) rafta yok; MAX tabure/tezgâh/bardak hattı da yok; sıradaki basamak ve alınmamış
  tekiller kalır. Rafta hiçbir şey kalmazsa tek levha: THE ROOM IS FITTED — NOTHING LEFT TO RAISE.
- **İkonlar:** karoda ürün resmi yerine grubun geliştirme ikonu — piktogram + yeşil yukarı ok
  (`Items/up_{walls,light,furniture,greenery,counter,seats,bar,glass}.png`, 24×24, karoda tam 4×;
  `Tools/upgrade_icons.py`, UI chrome, paletin rampaları). Ürünün kendisi (duvar örneği, bardak, tezgâh) bilgi
  kartında (`TileSpec.CardArt`); sepet/iade karosu da ikonu gösterir.
- **Core:** `TycoonRun.DevFit(id)` — geliştirici fiili, kataloğu parçayı bedava ve fazsız döşer (kapanış ışığı
  testi lambaları böyle takıyor; bilinmeyen id reddedilir).
- **Denge:** konfor bütçesi 9.9 → 10.6 (tavan 5.0). Ev simi A/B (aynı build, yalnız veri): basamak alan bot (şekil 2) konfor 3.13 → 3.14, konforun tuttuğu geceler %40,5 → %39,3, itibar 2.73 → 2.74, 3.0★'a ulaşan %18 → %19, kasa $75 → $77; alışveriş yapmayan bot bayt-aynı; duvar merdivenini bot yine almıyor.
- **Testler:** `FixtureTests` +1 (oda çıplak açılır, dördü alınır, her parça raf adlandırır; group ayrıştırma),
  kapanış ışığı testi `DevFit` ile; oyunda dört fotoğraf (çıplak oda, rafın başı/ortası/sonu).

### 9.28 · Shaker yazarın: tezgâhta duran tin, tezgâha dönüş kapısı, altın basamak (2026-09-06)

Yazar iki shaker çizdi (gümüş ve altın) ve şunu istedi: *"ana sahnede bardak altlığının üstünde
gözükecek ... shaker aşamasında iken ana sahnedeki shakera basıp o sahneye geri dönebilir. Altın
shaker upgrade olacak."* (`5a8bdd1d`)

- **Plakalar (`Tools/shaker_ship.py`):** yazarın dört plakası (bütün, uçsuz, kapaksız tin, ayrı kapak)
  tier başına tek 208×208 tuvalde geldi; araç hepsinden AYNI 116×208 pencereyi kesiyor — iki tezgâhın
  paylaştığı sayfa bu, kapak tin'in boynuna ancak öyle oturuyor (VesselArt'ın kendi notu). **Kapağın
  yeri ölçüldü:** yazarın ayrı kapak dosyası (61,14)'te bütün çizimle **3883/3883** birebir tutuyor.
  **Dökme kapağı türetildi:** uçsuz çizimin, oturan kapağın kapladığı satırları (0–83) — yani ucu
  kalkmış, süzgeci açık kubbe; dört plaka katman değil ayrı çizimler (ölçüldü: açık tin'in gövdesi
  kapalı olanınki değil), o yüzden hiçbir şey çıkarılmadı. Çıktı: `Items/{shaker,tin_open,shaker_cap,
  shaker_cap_pour}[_t2].png` + tezgâhın küçük tini `Items/shaker_prop[_t2].png` (48×48, yazarın kendi
  çizimi). Kaynaklar `Tools/AssetPipeline/sources/shaker/`.
- **Tezgâhta duran tin (HUD):** `DrinkWaitingInShaker` (tin dolu, servis bardağı boş) iken içkinin KENDİ
  altlığında duruyor — ikisi birbirini dışlıyor, o yüzden aynı yeri paylaşabiliyorlar. Ayak çizgisi
  `DishRestY` ile ölçülüyor (garnitürlerin cetveli), altlığın kaldırması ve tezgâhın kayması ekleniyor;
  akış açıkken ya da mahzen kalkıkken yok. Tıklayınca `TycoonServiceFlow.OpenShaker()` (kurallar orada
  soruluyor), imleç üstündeyken kitabın/merdanenin diliyle "BACK TO THE TIN" plakası. Çarpma testi
  sanatın alfasında (`alphaHitTestMinimumThreshold`), kutuda değil.
- **Altın basamak:** `fixtures.json` `shaker` yuvası **`carried: true`** — oda onu bir çengele DİKMEZ;
  `DiegeticStage.SyncFixtures` taşınan yuvayı atlıyor, market rungu her merdiven gibi satıyor
  (`shaker_steel` evin, $35 · 0 konfor, level 1; `shaker_gold` $140 · +0.4 · 2.0★, level 2; grup
  `counter`). Karonun resmi için `FixtureArt` artık `Fixtures/` bulamazsa `Items/`'e düşüyor — taşınan
  parçanın çizimi aletlerin yanında durur. İki tezgâh da sahip olunan tini giyiyor (`DressShakerArt`,
  sahneye girişte).
- **Testler:** `FixtureTests` +1 (taşınan yuva, iki basamak, her iki tier'ın dört plakası 116×208
  paylaşılan sayfada), oda-çıplak testine `shaker_steel` eklendi; `CoreCornersTests`'in merdiven testi
  yeni merdiveni kendiliğinden kapsıyor. Tezgâhın piksel baseline'ı yeni tinle yeniden kutsandı (resme
  bakılarak).

### 9.29 · Tezgâhın eksikleri, odanın imleç cevabı, lavabonun kuyruğu (2026-09-06)

Yazarın altı maddesi, üç commit (`8168efb6`, `8db73970`, `fb09cf3c`).

- **İki kâse geri geldi:** garnitür rayı altı tabak istiyor, dördü çizilmişti; `counter_ice`
  ve `counter_lemon` hiç yoktu, o yüzden ray 2026-08-26'dan beri iki turkuaz yer tutucu kutu
  çiziyordu. Yazarın kendi minileri (`bench_mini_ice/lemon`, 09-05 süpürmesinde gitmişti —
  kimse yüklemiyordu) rayın istediği adlarla döndü.
- **İki paspas yazarın:** `bira_paspasi` (54×13) musluk altındaki damlalığın yerine (eskisi
  119 genişti, tezgâhın yarısını kaplıyordu), `cerez_paspasi` → `prep_mat`, garnitür
  tabaklarının altına. Paspas RAYDAN ölçülüyor: kavanoz stokta yoksa sıra kapanıyor,
  alınınca açılıyor, paspas o kareyi ölçüp ortalanıyor. İkisi de 6 px uçlarla dokuz-dilim ve
  TILED çiziliyor — uzayan ray dokuyu sündürmüyor, tekrarlıyor (tezgâhın kendi yasası).
- **Tezgâhtaki tin mahzen açılınca kaybolmuyor** (çekmece bütün tezgâhı kaldırıyor, üstündeki
  her şey onunla çıkıyor) ve **tezgâha tezgâhtan girilince oda kayıyor** (`OpenShaker` artık
  çekmeceyi açıyor — eskiden tezgâha yalnız mahzenden gelinirdi, oda hep zaten kalkıktı).
- **Tezgâha elsiz girilebiliyor:** `RefreshShaker` şişe yokken erken dönüyordu, bench yarım
  giyinik kalıyor ve şişe propu kuruluşundaki yer tutucu rengiyle (turkuaz kutu) duruyordu.
  Prop iniyor, kart "THE TIN" diyor.
- **Şişe dökülebileceği yere gidiyor:** kapalı tin üzerine seçilen şişe tezgâha (bardağa)
  gönderiliyordu — o bench'in dolabı 2026-08-22'de kaldırıldığından şişe duracak yeri
  olmayan bir odaya varıyordu: görünmez, dökülemez, sessizce düşüyordu. Artık tin'e gidiyor,
  kapak kapalıysa bench "TAKE THE LID OFF TO ADD IT" diyor. **Bu, yazarın kendi kapak
  kuralını şişeler için geçersiz kılıyor; tek satırla geri alınır.**
- **İmleç cevabı büyüdü (GDD 16):** yükselme + büyüme + salınım + arkadan hale; her kare
  geri alınıp yeniden uygulanıyor, sahibiyle kavga etmiyor. Mahzenin şişeleri yalnız yanıyor
  (dört ayrı renderer), müşteriler yalnız parlıyor (insan prop değil).
- **Lavabo kuyruğu bir koltuk kuyruğu oldu:** kirli bardak tabureyi TOPLANANA kadar değil,
  YIKANANA kadar tutuyor (`Housekeeping.GlassesOut` → `BarDay.FreeStools`). Barın bardağı
  sınırlı; tezgâhı dolduran bar müşteri kaçırıyor. Şerit "N STOOLS HELD" yazıyor.
  **Su yazarın:** iki lavabo için 14'er kare, tek tabakaya diziliyor
  (`Tools/sink_water_ship.py`; `fx_sink_water` çelik, `fx_sink_gold_water` pirinç) — kare
  leğenin kendisini de taşıdığı için duran lavabonun üstünü örtüyor, dikiş yok.
  200 koşu: kasa medyanı $80 → $77, fırtına %15.4 → %15.5 (bot anında yıkıyor).
- **Testler:** `HouseTests` +2 (bardak nerede olursa olsun sayılır; ikinci bardak akan
  lavaboyu bekler), EditMode 486/486.

### 9.30 · İmleç cevabının düzeltmeleri, tin'in lavabosu, tek beden tin, yavaş geçiş (2026-09-06)

Yazarın ikinci listesi, iki commit (`<H>`, `<B>`).

- **Konumlar:** çerez paspasının yüksekliği aynı; bira paspası 4, bira fıçısı 5 birim aşağı;
  peçete tezgâhın sağ ucuna; menü, lavabo ve garnitürler 30 birim sola; menü 3 birim aşağı,
  odanın ışığından muaf (beyaza sabit) ve daha önde. Birimler ODANIN kendi pikselleri
  (1 oda px = 2 HUD birimi).
- **İmleç cevabı düzeltildi (GDD 16 §0b):** hale artık RECT'in değil ÇİZİMİN sınırlarında
  (`DrawnSize`, spritein opak kutusu); imlecin altındaki nesne hiyerarşide en üste çıkıyor,
  ışığı bir altında (canvas'ta kardeş sırası, odada sorting order, çıkışta ikisi de geri);
  "sağ sol" bir KAYMA değil ±2°'lik SALLANMA. Panel kapanırken sıra hiç ellenmiyor —
  Unity ebeveyn (de)aktive olurken kardeş taşımayı reddediyor (PlayMode bunu yakaladı).
- **Mahzenin şişeleri de yükseliyor, büyüyor, sallanıyor:** şişe dört transform (ön, arka,
  içki, maske) — dördü birden `Movers` olarak veriliyor ve her biri KENDİ pivotu değil
  GÖVDENİN merkezi etrafında dönüp büyüyor, yoksa içki camın dışına savruluyor.
- **Tin lavaboya taşınıyor:** tezgâhtaki tin basılıp sürüklenince elde kalkıyor (10 px
  eşiği; eşiği geçmeyen basış hâlâ bench'in kapısı), lavaboya bırakılınca içindekiler
  gidiyor ve musluk akıyor. Çöp zaten bench'in ÇÖP tuşu.
- **Lavabo her kullanımda meşgul:** `RunTheTap()` + `PourAwayAtSink()`; bardağı da dökmek
  artık musluğu başlatıyor. Para cezası çöple aynı (tek muhasebe), fark ZAMAN.
- **Tek beden tin (GDD 21):** iki bench de 232×416 — 116×208 sayfanın TAM 2 katı. Kapalı
  tin 348, highball 244 → 1.43 (gerçekte ~1.53). Nişan artık kutunun tepesine değil
  KENARA (`TinMouth`, sprite'ın opak kutusundan) — 137 birimlik fark PlayMode dökme
  testini kırmıştı. El şişesi hâlâ 2× (384) ve tin'in yanında bir tık kısa duruyor;
  bir sonraki tam kat 576, yazarın kararı.
- **Tek yavaş geçiş (GDD 24):** shaker→bardak 0.42s; ortak krom (tezgâh, geri, çöp) yerinde
  duruyor, eğri sabit hızla gidip son %14'te FRENLİYOR, duruşta çalışma yüzeyi (bardak,
  tin, gölgeler, içki) 22 birimlik sönümlü bir sarsıntı alıyor.
- **Testler:** EditMode 488/488 (+2: musluk boş çalışıyor, dökme çöpün ücretini alıp
  musluğu başlatıyor), PlayMode 10/10, bench temel görüntüsü yeniden kutsandı.

### 9.31 · Işığın şekli, çağıran lavabo, ovulan kir (2026-09-06)

Yazarın üçüncü listesi, tek commit (`<C>`).

- **Parlama artık nesnenin şekli:** tek elips her nesneye uymuyordu; `ChromeArt.Glow`
  ışığı spritein KENDİ alfasından büyütüyor (iki geçişli chamfer mesafe dönüşümü, sonra
  mesafeye göre sönüm). Tuval, çizimin tuvali + her yandan erişim kadar, bu yüzden propun
  kendi ölçeğinde ortalanınca kendiliğinden hizalanıyor. Sprite başına önbellek; prop
  çizimini değiştirince ışık yeniden kesiliyor. `Halo` artık BOYUT değil ERİŞİM çarpanı.
- **Hareket eden prop, çarpma plakası değil:** musluk ve lavabo odada çizilip canvas
  plakasından tıklanıyor; `Riser` verilmediği için glow görünmez plakayı kaldırıyordu —
  yükselme hiç görülmemiş, üstelik plaka propundan 2 birim kayınca bırakma hedefi elin
  altından kaçıyordu (tin taşıma testi bunu düşerek yakaladı).
- **Lavabo eli çağırıyor:** `HoverGlow.Beckon` + `DiegeticStage.CallTheDrain`; bardak ya
  da tin taşınırken lavabo imlecin altındaymış gibi yanıyor ve öne çıkıyor.
- **Kir ovularak çıkıyor:** işaret 28×9 → 48×18 (halka, iç yıkama, sürtme kuyruğu, üç
  sıçrama); her leke sanatın bir KOPYASINA sahip ve bez geçtiği teksellerden mürekkep
  alıyor. Core'un `Wipe`'ı ancak %7'nin altına inince çağrılıyor. Ölçüm: aynı noktada
  altı ovuş %5, ortadan tek düz geçiş %83; köşeler için geri dönmek gerekiyor.
  Alfalar 96/34 → 150/72: eski işaret koyu tezgâhta neredeyse görünmüyordu.
- **Menü tahtasının altına bir şey konmuyor:** en soldaki tabure tahtanın altında kalıyor
  (tahta 67..129, taburenin bıraktıkları 101..170), bardak ve işaret tahtanın sağına
  itiliyor. **Yerleşimin kendisi yazarın kararı — tahtayı 30 birim sola alan hamle bu
  çakışmayı büyüttü; bir satırla geri alınır.**
- **Testler:** EditMode 488/488, PlayMode 11/11. Sepet resmi testi artık kapanış sorusu
  ekrandaysa onu yanıtlıyor: OpenUntil'in ikinci basışı "siparişi kapat?" kartını açıyor
  ve kartın perdesi altındaki her şeyi üçte bir karartıyordu (140k piksel fark).

### 9.32 · Kenar ışığı, çelik tin, bardak altlıkları, ovulan kir, orantılı bardaklar (2026-09-06)

Yazarın dördüncü listesi, tek commit (`<C>`).

- **Işık artık sadece KENAR:** çizimin opak olduğu yere hiçbir şey yazılmıyor, yalnız
  siluetin dışına sönümlü bir hale çıkıyor. Şeffaf propların (bardak) içi ışıkla dolmuyor.
- **Işık pivotta değil ÇİZİMİN merkezinde:** bench propları ayağından asılı (şişe 0.22,
  kaşık sapından), pivot merkezli hale 384 birimlik şişenin yüz birim altında kalıyordu.
- **Işık komşuların önüne geçmiyor:** prop öne çıkıyor, ışığı yerinde kalıyor.
- **Eksik parlamalar tamamlandı:** kaşık, servis bench'indeki tin, tezgâhtaki hazır bardak
  ve toplanacak boşlar. Boş bardağın üstüne gelince LAVABO da yanıyor. Dolum bench'indeki
  tin kapak takılana kadar yanmıyor (o hâlde ele gelmiyor, şişenin hedefi).
- **Tin çelik:** %97'nin altında içi hiç çizilmiyor; ağzına kadar doluysa ağzında bir bant
  görünüyor. Seviye bench'in kendi göstergesinden okunuyor.
- **Tek paspas:** çerez paspası dikeyde iki kez tekrarlanıyordu (rect 2×, döşeme 1×);
  `pixelsPerUnitMultiplier` yarıya inince tek sıra kaldı, yükseklik aynı.
- **Bardak altlığı:** sipariş alındıktan sonra müşterinin önüne kare altlık iniyor
  (`coaster_a..d`, PixelLab 32×32); hangisi ve ne kadar yamuk durduğu tabure + ziyaret
  hash'inden. Tıklanamaz, içki hakkında hiçbir şey söylemez.
- **Kir:** her müşteri bardağını bırakıyor + 0-3 iz (kendi RNG akışı; %25/%40/%25/%10).
  İzler ayrı birer mess, yani ayrı ayrı konfor eksiltiyor ve ayrı ayrı siliniyor.
  200 koşu: yalnız silme sayısı 44.850 → 46.337; hayatta kalma ve kasa aynı.
- **Silme bezin kendi ayak iziyle:** 52×32'lik dikdörtgen, işaretin kendi pikselleri
  üzerinde; değdiği piksel tamamen gidiyor. 96×36'lık işaretin ortasından tek geçiş
  yarısını alıyor. İşaret artık blok değil TANE (96×36, 1 birim = 1 piksel); tuz/şeker
  gerdanlığı da öyle (`GlassDecor.Speckles` üç kat çözünürlük).
- **Bardak boyları orantılı:** kurulu takım her hat için AYRI KIRPILMIŞ sayfa
  (highball 32×63, rocks 46×56), sabit kutuya sığdırınca hepsi aynı boy çiziliyordu.
  `GlassArt.BoxFor` yüksekliği ORANTI TABLOSUNDAN alıyor (`Shapes`): rocks artık
  highball'ın %63'ü (52'ye karşı 32), gerçek camda olduğu gibi.
- **Tıklamak bardağı toplamıyor:** boşa basılıp SÜRÜKLENMESİ gerekiyor (tin'in kuralı).
- **Testler:** EditMode 488/488 (dört test yeni kurala göre yazıldı), PlayMode 11/11.

### 9.33 · Beşinci listenin ilk yarısı (2026-09-06)

Commit `<C>`.

- **Yeni bardak altlıkları kaldırıldı** (yazarın isteği): dört PixelLab altlığı ve onları
  koyan kod gitti; üretici `Tools/coaster_gen.py` duruyor, istenirse yeni take alınır.
- **Servis bardağı altlığına oturdu:** her hat kendi boyunda çizildiğinden (BoxFor) sabit
  `CarriedGlassHeight/2` ile hesaplanan ev, kısa bardağı havada bırakıyordu; ev artık
  bardağın KENDİ yarı boyunu kullanıyor.
- **Boş bardak sadece lavaboya gider:** basış bir RESİM kaldırıyor, Core ancak bırakma
  lavabonun üstündeyse haberdar oluyor (topla + yıka tek harekette). Başka yere bırakınca
  hiçbir şey olmamış oluyor.
- **Lavabo beş saniye:** yığın ne olursa olsun tek süre (`Housekeeping.WashSeconds`),
  pirinç lavabo `washSeconds: 2.5` ile yarıya indiriyor (yalnız drain diyebilir).
  Musluk akarken leğenin üstünde eski tip bir saat duruyor, akrebi tüm süreyi süpürüyor.
- **Sallanma sahibinin dönüşüne EKLENİYOR:** mutlak açı, bardağa dökülürken tin'i dik
  tutuyordu; artık bench'in eğimi korunuyor.
- **Panel perdeyi indirince ışık bırakıyor:** `blocksRaycasts` düşen bir CanvasGroup çıkış
  olayı göndermiyor, bu yüzden musluk "seçili" kalıyordu.
- **Tezgâhtaki içki tıklanınca kendi bench'ini açıyor** (`OpenServe`), sürüklenince servis;
  üstüne gelince "CLICK TO EDIT · DRAG TO SERVE" ipucu çıkıyor.
- **Ölçüm:** meşrubatın bench'e ulaştığı doğrulandı (`OpenBottle(soda_klara)` → prop aktif,
  `v4_soda_klara_front`, dökülebilir); mahzende beş kapının beşi de açık. Yazarın raporu
  bu haliyle tekrar edilemedi — bkz. oturum notu.
- **Testler:** EditMode 488/488, PlayMode 11/11.

### 9.34 · Üç yudum, üç görev türü, ödeyen hafta (2026-09-06)

Commit `<C>`.

- **Ece artık gün sonu müşterisi değil:** yazılı son çağrı sahnenin konfigüründe kapalı
  (`TycoonConfig.LastCall = false`, `ForTheScene`); beat silinmedi, kendi süiti hâlâ
  oynatıyor ve dev tezgâhı `DevForceLastCall` ile çağırıyor.
- **Haftalık görev üç türlü:** `Serve` (bir içkiden N adet), `Perfect` (N kusursuz pour),
  `Clean` (N gece hatasız). İlk hafta hep Serve; sonra sırayla. Hedefler haftayla
  büyüyor (3→7 / 1→4 / 1→3), hiçbiri imkansız değil.
- **Görev ödüyor:** $12'den $36'ya, zor türlerde +$8. Gecenin BONUS satırına yazılıyor,
  oda parayla birlikte "ECE PAYS UP · +$X" diyor (ikon: madeni para), log satırı kalıyor.
  200 koşu: iflas 2 → 0, kasa medyanı $77 → $85, günlük gelir $133.8 → $137.6.
  Ayar tek yerde: `WeeklyJobs.RewardFor`.
- **Görev şeridi LOG'un yanında:** ikon + satır, üstüne bir şey gelince %35'e soluyor
  (yok olmuyor), mouse gelince tam netleşiyor.
- **Bildirimler ikon taşıyabiliyor:** `Toast(mesaj, renk, süre, ikon)`.
- **Ekonomi süitleri görevsiz koşuyor** (`weeklyJobs: false`), çünkü onlar barın SATIŞTAN
  kazandığını ölçüyor; sim ve oyun açık koşuyor.

## 10 · Teknik omurga

- **6 asmdef:** Core (saf C#, motor erişimi imkânsız) ← Game ← UI ← Editor; Tests → Core+Game; PlayTests (2026-08-12) sanal fareyle gerçek sahneyi oynar — UI'ın içine değil, ekrana ve Core durumuna bakar.
- **Determinizm:** `RunRng` (FNV-1a→PCG32) adlı akışlar: arrivals, orders, patience, decide, customer, read. `System.Random`/`UnityEngine.Random` yasak.
- **Veri:** 6 JSON, `JsonUtility` + gürültülü doğrulama; tarifler çift kaynak (json+katalog) parite testli. **`story/story.json` 2026-08-13'te yüklenir oldu** (`DataLoader.ParseStory`): kadro + tarif kataloğuna karşı kurulur; bilinmeyen look/tarif/gece, sessiz geceye yazılmış misafir, iki host, kimsenin izlemediği ders adı, hiçbir yere çıkmayan beat yüklemede patlar. Yazım kuralı da orada: `needStyle` isteyen beat, o stili `hostWarning` satırında **adıyla** söylemek zorunda. Bootstrap boot'ta ayrıştırır ama koşuya henüz vermez (`storyInPlay` kapalı — diyalog plakası S3'te).
- **Araçlar:** LastCall menüsü — Create Debug Scene · Simulate Tycoon 200 Runs · Measure Service Speed Response.
- **Doğrulama:** 281 EditMode testi (12 dosya) + 7 PlayMode testi (4 duman + 3 piksel taban resmi, `Baselines~`); sim botu gerçek oyuncu fiilleriyle 200 koşu, `Docs/tycoon_sim_report.md`.

### 9.35 · Altıncı liste: peynir dilimi, tutuş, ışıklı mat, sıfır konfor, dar kimlik, kiriş, menü, tezgâh, kalın bardak (2026-09-06)

Yazarın altıncı düzeltme listesi. Hepsi kodda ve fotoğrafla doğrulandı.

- **Lavabo bekleme süresi bir pasta:** saat kadranı ve ibre gitti; `ChromeArt.PieRing` üstünde `Image.Type.Filled / Radial360`
  bir disk (`PieDisc`), tepeden saat yönünde dolar, dolan dilim geçen sürenin tam payı (`_sinkPie.fillAmount = 1 − WashLeft/SinkSeconds`).
- **El tutuşunu korur:** taşınan her şey (bitmiş içki, boş bardak, tin, bez, çerez tutamı, tezgâh şişesi, kapak, bardak sahnesindeki tin)
  basıldığı noktayı kaydeder ve o ofsetle taşınır — pivot artık imlece zıplamaz. Çerez tutamında ofset kap/küp oranında
  ölçeklenir (parmak kabın neresine bastıysa küpün orasında kalır). Musluk bardağı değişmedi (ağzı musluğun altında kalmalı).
- **Çerez matı odanın ışığında:** `_prepMatImg.color = stage.RoomWashLight` her kare; arka barın giydiği yıkama.
- **Konfor sıfırdan başlar:** `VenueComfort.FreeBase = 0`. Çıplak oda hiçbir şey etmez; gece `min(servis, konfor)` dosyaladığı için
  kusursuz servis bile çıplak barda **0 yıldız** yazar (`NightReportTests.ABareRoom_FilesNothing_WhateverTheDrinks`). Kalabalık
  SERVİS tarafını okuduğundan müşteri ve para akışı değişmez; yıldız kapıları duvara kadar kapalıdır — huni budur.
  **Duvarlar odayı taşır:** `walls_2` **$45** · **+1.25 · 0★** (kapısız — bir gecelik hasılat, ikinci gece alınır), `walls_3` $130 · **+2.25 · 1★**, `walls_4` $200 · **+3.25 · 2★**
  (eski $70/$130/$200, +0.3/+0.6/+1.0 ve 1/2/3★). **Sim (200 koşu):** bot bir çıplak odada önce sıvayı alır (marketin tabelasını okur); taban 0% iflastan **%34'e** çıktı ($45; $70'te %68, $40'ta %27 — merdiven pini $40'ı yasaklar, 3–4. basamağı ucuzlatmak hiçbir şey değiştirmedi). Bağlayıcı kısıt fiyat değil, puanın sıfırdan tırmanışı (gecede 0.125, 1★ 7. gün yerine 11. gün) ve kapının sıfır yıldız varış aralığı (GDD 23 §7) — açılışın bu kadar sert kalması yazarın kararı.. Her basamağın kapısı bir önceki basamağın verdiği konforun altında, ilk basamak tek başına ilk yıldıza
  ulaşır. Markette duvar rafı ilk raf; 2. basamak alınana kadar tabelası amber "START HERE ▸ THE WALLS — THE ROOM'S COMFORT"
  (`ShopSection(title, hot: true)`), konfor taşıyan her karo "COMFORT +1.25" yazar (eski "Fitting · mark 2" yerine). Test pinleri:
  `HouseTests` (0 taban), `ComfortWiringTests`/`TycoonRunTests`/`NightReportTests` kir ve puanı ölçen geceleri `ARoomWorthTwo()`
  (baştan sahip olunan +2.0 lamba) ile oynar. 492/492.
- **Kimlik v3:** 196×148 art px (588×444) — ızgara basılana göre ölçüldü (348 birim; 20 harflik içki 320). Bayrak 16×11'in 6 katı,
  22 px'lik diske kesilmiş (`ChromeArt.Roundel` + `Mask`, üstünde `RoundelRing`), bandın alt kenarına madalyon gibi asılı.
  KICK anahtarı çizildiği yükseklikte (100×52): dilimlenmiş kapağın 24 birimlik kenarı 30'luk anahtara 16 px kelime için 6 birim yüz
  bırakıyordu. Yazı bandın solunda üst üste (otorite / sınıf). Onay çipleri beşinci satırın İÇİNDE, başlığın sağında.
- **Kiriş (üst bar):** saat kuyusu · kasa kuyusu (2× coin, cyan rakam, eksi bakiyede kırmızı; `RunTheTill` kirişi de sürer) · hafta ·
  COMFORT/SERVICE kelimeleri şeritlerin yanında · yıldızlar · 42'lik ayar anahtarı (çark tam 2×, amber). Üç okuma ve kasa ve anahtar
  imleç altında ikonlu iki satırlık ipucu verir (`ShowPropTip(over, word, icon, detail)`); ekranın tepesindeki nesnelerde ipucu ALTA asılır.
- **Ayarlar bir pencere:** scrim + ortada kart (band, çark, neon), AUDIO / DISPLAY / THE RUN grupları, satır = ad + sağda kontrol;
  ses beş bloklu ölçek ve −/+; SOUND ON/OFF; MOTION FULL/REDUCED; TONIGHT'S BOOK → OPEN; START OVER → NEW RUN (tuğla rengi);
  altta DEV TOOLS (küçük) ve CLOSE (amber). Sıralama 23.
- **Geliştirici tezgâhı Türkçe ve düz yazı tipinde:** `LegacyRuntime.ttf` (Arial), dört gerçek sütun (FİYAT / AD / NASIL YAPILIR / NE İSTER),
  basamak başlıkları "★ 1.0 · 10 SATIR — KİLİTLİ"; fiiller YENİ KOŞU / ORTA OYUN / SON OYUN / GÜN SONUNA ATLA / SON SİPARİŞE ATLA / ODA.
  Oyunun kendi tostları İngilizce kaldı (piksel yüzlerde Türkçe glif yok).
- **Bardak seti v2 (çizili):** `GlassArt.PreferDrawn = true` — üretilmiş `glass3d_*` plakaları kurulu ve bir bayrak uzakta. 128×176
  tuval, duvar 3→5 px, taban 6→10, duvar silindir gibi taranmış (uzak kenar koyu, ışık çizgisi, gövde, iç kenar gölge), tüm boşluğun
  üstüne %26 cam tonu ("daha az şeffaf"): içki camın İÇİNDEN görünür. Boyutlar aynı (bardak sahnesi 260, tezgâh 92, boşlar 52);
  kalın duvar 92'de bile ~3 birim kalır, eski 3 px 1.5'e iniyordu.

### 9.36 · Yedinci liste: tutuş, ağız, lavaboya giren bardak, mat fikstür, altın madalyon, kiriş rakamı, kimlik v3.1, yuvarlak balon, görev plakası, market rafı, yıldız ekonomisi (2026-09-06)

Yazarın yedinci listesi. Kodda, fotoğraflandı, ölçüldü.

- **Kaşık tutulduğu yerden tutulur** (`_spoonGrabOffset`); **dolu tin** artık sıvıyı ağzının içinde gösterir
  (`BrimInset = 7`); **müşterinin bardağı lavaboya BARDAKLA gider**: bırakma testi imleç VEYA taşınan resmin merkezi
  (`ScreenOf`), ve lavaboya giren şey yok olmaz, 0.28 s'de aşağı süzülüp solar (`SinkFade`, tin dâhil; Reduced'da anında).
- **Çerez matı odanın fikstürü:** `prep_mat` (slot 258/69, tezgâhın üstünde düz, `startsInTheRoom`), odanın ışığıyla
  aydınlanır; HUD'daki kopya ve elle boyama gitti. `FixtureTests` verilenler listesi altı parça.
- **Madalyon altın** (Amber disk, Malt jant; `Tools/medallion_icon.py`). **Yıldız/kalp/madalyon ipuçları sayı söyler**
  (her kare okunur): SERVİS x/5, ODA x/5, YILDIZ "gecede bir adım, ikisinin küçüğüne: SERVİS a · KONFOR b = GECE min".
- **Kasa rakamı saatin elinde:** `SegmentFigure` — altı hücre (işaret, dolar, dört rakam), sağa yaslı, boş hücreler
  hayalet; coin ve yazı gitti.
- **Kimlik v3.1:** satırlar 24 (27'den), band 22 (24'ten), hücreler 24 (26'dan), kart 144 (148'den); mühür bandın tam
  ortasında; KICK ev anahtarı (88×36, ViceRed). NEW ARDEN = kurgusal şehir/yargı alanı (yalnız bir ad; `BuildIdCard`).
- **Balonlar:** gerçek yuvarlak dikdörtgen (yarıçap 5.5, 2 px mürekkep), gövde yüzü 16 normal ağırlık, sola yaslı;
  `SeparateSays` her kare başların üstünden başlar, çakışanları yarı yarıya iter, satırı ekranda tutar, kuyruğu konuşanın
  üstüne geri koyar.
- **Ece'nin görevi plakada:** ev kartı satıra göre kesilir, magenta pip, "ECE · 2/5 · PERFECT POURS", tam opak; görev yoksa
  satır hiç yok.
- **Market:** ürünler kutunun boyuna sığar (ölçek 3x altında tabanlanmaz); "+1.25 COMFORT" başında madalyon
  (`TileSpec.MetaIcon`); sandık yeniden çizildi (`Tools/restock_icon.py`); THE WALLS (sıva merdiveni) ve ON THE WALL
  (triptik, ekran — `wall_art` grubu) iki raf; ON THE WALL'un ikonu `up_wall_art`.
- **Yıldız ekonomisi (GDD 23):** `StarEconomy.TierMultiplier = 1 + ⌊yıldız⌋`. Tarif kapısının, fikstür gereksiniminin,
  şişe basamağının kademesi fiyatı çarpar (`RecipePrice`, `FixturePrice`, `Market.RungPrice`); barın kademesi içki
  bedelini VE kirayı çarpar (`PriceOf`, `Rent`). Duvar merdiveni $45 · $260 · $600 — katalogdaki en pahalı şey.
  **Sim (200 koşu):** iflas %34 → **%9**, gelir/gider $111/$113 → **$232/$224**, kasa medyanı $10 → **$261**, puan 1.82 →
  **2.13**, 2.0★'a %89, 2.5★'a %59 ulaşıyor (önce %50 / %0). Açılış (kademe 0) tanım gereği aynı. `DevPreset` kasası da kademeyle ölçekli (orta oyun 160→480, son oyun 600→3600) — aksi hâlde 12. gün ön ayarı tek kira sonrası iki şişeyi dolduramıyordu (PlayMode restock testi bulmuştu). 497 EditMode (StarEconomyTests dâhil).
- **3D bardak seti (PixelLab, `Tools/glass3d_gen.py` → `glass3d_ship.py`):** beş boş, kalın duvarlı, buzlu cam; oyuk
  (`_fill`) elle ölçülen üç sayıyla kesilir ve duvar yarı saydam bırakılır (GLASS_ALPHA 118) — bkz. GDD 21 ve bu turun
  kapanış notu.

### 9.37 · Sekizinci liste: mahzen kartı ve sallanan sıvı, etiketli gösterge, kitap satırı, uzayan mat, elips oyuk ve büyük bardak, 2:1 kimlik (2026-09-06)

- **Mahzen:** şişenin üstüne gelince kart (isim, hangi ev içkilerinde — `MenuDrinksUsingStyle` — ve gazlıysa `NoShake` işareti)
  `ShowPropTip` ile şişenin üstünde; sıvı ve maske artık şişeyle birlikte sallanıyor (`RefreshCellarMovers`: kapılar plakalardan
  ÖNCE kurulduğu için takipçi listesi yalnız şişeydi).
- **Göstergeler:** "40% VODKA" etiketleri tinin maskesinin dışına (`Labels` rect) taşındı; `Items/gauge_tin.png` (48×106, 2×) ve
  `gauge_tin_solid.png` konursa çizili tin yerine kullanılır. Konumlar: shaker sahnesi (462, −74), bardak sahnesi (522, −74),
  1280×720 panelde merkez çapa, 96×212.
- **Kitap:** kilitli malzeme etiketi 8 px tek satır.
- **Mat:** `SetPrepMatSpan(centreHud, widthHud)` — HUD ayakta duran kap sayısı değişince odaya söyler; fikstür `Tiled` çizilir,
  yeniden ortalanır (ölçüldü: 4 kap → 143×13 stage px, merkez 220.5).
- **Bardak:** oyuk ağzın uzak yayı ile tabanın yakın yayı arasında (elips), duvar içeri; bardak sahnesi 340, tezgâh 116.
- **Kimlik v4:** 200×100 (2:1). Band: otorite / "PATRON LICENCE · NA …", KICK ev anahtarı 84×30, mühür Ø14 (bayrak 4×).
  Ray: fotoğraf 48 + damga şeridi (ziyaret zımbaları + bağ kalpleri; 16 px yıldız + ortalama). Izgara 134 genişlik, 19 px adım.
- **Ölçülemeyen:** "hazır bardağa tıklanınca arkaplan kalkmıyor" — bardağın kendi PointerDown'u ile denendi, çekmece 0→1
  kalkıyor; yazardan hangi durumda olduğunu istedik.
- Arka plan görselleri: `Assets/Art/Backgrounds/counter.png` 638×250 (pivot orta, PPU 1, sol 217 / sağ 218 px kenar, orta 203 px
  yatay tekrar; yüzey çizgisi alttan 96, dinlenme çizgisi 120) ve `counter_shutter.png` 592×186 (kepenk).

### 9.38 · Dokuzuncu liste: Malibu Club kimliği, gecenin şeridi, mahzen kartı, tek porsiyon, sesler, raydaki havlu, bardak seviyeleri (2026-09-07)

- **Kimlik:** NEW ARDEN → MALIBU CLUB / "MIAMI · PATRON LICENCE · No"; şerit 16 px, `Items/licence_band.png` (200×16, PixelLab plaj
  panoraması) + soldan sağa açılan karartma; iki satır şeridin ortasında; kâğıt köşeleri r=4 yuvarlak (`LicencePaper`), kart `Mask`.
- **Üst bar:** hafta aleti kirişten indi (gün kartında duruyor); saatin yanında GECE kuyusu (sayı + gün adı + kalabalık; hafta hover'da);
  kasa kirişin sağ yarısına, okumaların soluna (−470); yıldız/kalp/madalyon tek merkez çizgisinde; ayarlar anahtarında üretilmiş
  `Items/cog3d.png`.
- **Mahzen kartı:** tek satırlık başlık yerine kart (`BuildCellarCard`): ad, stil · kademe · şişe fiyatı (`Market.StockPrice`), kalan,
  "IN THE BOOK" + 4 tarif (+N more), gazlıysa NEVER SHAKEN satırı.
- **Tek porsiyon (Core):** `TransferInto(..., scale)`; `PourIntoServingGlass` ölçek = bardak/tin kapasitesi; nişan artık dökmüyor
  (`AimGate` 0.35 ile tezgâh akıtmıyor). Testler: `AFullTin_FillsWhateverGlassComesDown…`, `HalfATin_IsHalfAGlass`, PourSystem ölçek
  testleri; `One_confident_measure_of_a_fizzy_drink_comes_out_wrong` kendi notuna göre kaldırıldı ("the trap is gone").
- **Sesler:** `Resources/Data/voices.json` → `VoiceBook` (Core); 13 ses, 9 ipucu; `TycoonRun.VoiceStream` ("voice" akışı); balon
  yazıldığı gibi (cümle düzeni), servis pop'u büyük harf. Sipariş satırı yalnız kart okunduktan sonra.
- **Bardak:** kanvas oyuğa ortalandı (`glass3d_ship.py` crop), taban yayı `FloorArc` (`MetaballFluid.SetFloorArc`), bardak tezgâh
  çizgisinde; akış kalınlığı eğime bağlı, damla çizili yüzeye iniyor ve hızıyla sıçrıyor.
- **Bardak seviyeleri:** `glass3d_{id}_t2..t6.png` — PixelLab `edit_image` ile taban üstüne giydirme, alfa taban siluetine kesik
  (`Tools/ninth_art_gen.py`; sayfa: `Tools/glass3d_tiers_preview.png`).
- **Havlu:** `Items/towel_on_bar.png` rayda (`CounterArtPoint(599, 57)`), elde `towel.png`, kuyruk hareket yönüne bakar, silme kutusu
  havlunun kendi rect'i. Kir ve tuz/şeker halkasında koyu kontur.

### 9.39 · Onuncu liste: on yeni müşteri, market kartı, balon yığını, mahzen grupları, canlı sıvılar, mürekkep (2026-09-07)

- **Yeni kadro:** `Tools/patron_prompts.FIGURE_OPTIONS`'a on düşük profilli Miami müşterisi (guayabera, nurseaide, linecook,
  busdriver, fisherman, abuelita, rider, cleaner, retiree, guard); `patron_trial_gen.TRYING` bu on. Sinirlenme klibi kişi başına
  farklı (`UPSET_STYLES` / `UPSET_OF`: shake, arms, sigh, glare, wave). Klipler iki yarım × 8–9 kare (sunucu tavanı 16), hattın
  bildiği yol: still → `patron_ship` (idle) → A yarıları → `pull` → B yarıları → `pull` → `peaks` → `patron_ship` → kadro satırı.
  Oyundaki on: guayabera (Osvaldo Reyes, miami), linecook (Ramiro Cortés, miami), abuelita (Carmen Ortiz, miami), nurseaide
  (Marie Joseph, miami), busdriver (Leroy Banks, southern), fisherman (Desmond Clarke, default), rider (Yeferson Mora, miami),
  cleaner (Rosa Mendieta, miami), retiree (Walt Petersen, midwest), guard (Andrés Vargas, miami) — hepsi 0★, kimlikleri ABD;
  `Tools/patron_join.py` kadro satırını, kimlik kaydını ve sesi tek seferde yazar. Kadro 19 yüz (PapersTests 51 kişi).
- **Siyah kontur yok:** `Tools/patron_ink.py <slug>` — saf siyah pikseller komşu renklerin koyultulmuş tonuna çevrilir (leopar
  yeniden mürekkeplendi; 96 karede 97 bin piksel). Modelin "lineless" isteğine rağmen çizdiği konturlar bu geçitten geçer.
- **Market kartı:** beş bant (resim girintisi, yıldız satırı, isim plakası, gerçek satırı — stok yatay çubuk —, durum + ayak);
  sepet değişince koridor piksel ofsetiyle yerinde kalıyor (`_shopScrollPx`).
- **Balonlar:** set/boyut imzasıyla bir kez çözülür (`_saysSig`); 48 birime kadar kayar, sonra bir kat yukarı çıkar, kuyruk uzar.
  Konuşma hızı 12 karakter/sn.
- **Mahzen:** stok aileye göre sıralı, raf altında aile adı (`StepCellarLabels`); havlu çekmece açıkken %20 alfa, isabet kutusu
  resmin alfası. **Sıvılar:** `UITheme.Vivid` (kroma ×1.35) — mahzen dolulukları ve kokteyl renkleri birlikte.

### 9.40 · On birinci liste: beş Japon müşteri, rastgele kapı sırası, balon kontrolü (2026-09-07)

- **Beş yeni yüz (pastelman dili, hepsi 50 altı):** driftgirl (Aiko Tanaka, 23 — Tokyo Drift görselindeki gibi: lila askılı üst,
  kot, uzun açık kahve saç), racerboy (Kenji Sato, 26), salaryman (Hiroshi Nakamura, 34), harajuku (Yui Kobayashi, 22), mechanic
  (Takeshi Mori, 47). Kimlikleri Japonya (`Items/fl_jp.png` 16×11 çizildi), sesleri yeni `japanese` sesi (`voices.json`, 14 ses).
  Sinirlenme tarifleri: glare / wave / sigh / shake / arms. Aynı hat: still → judge → adopt → ship → 11 klip → ink → join.
- **Kapı sırası:** `FaceRng` artık koşu tohumuna saat damgası ekliyor (`|faces|ticks`) — hangi çizimin hangi sırayla girdiği oturumdan
  oturuma değişir; kim olduğu, ne istediği, ne zaman geldiği hâlâ koşunun tohumlu akışlarından.
- **Balonlar gidince kalıyor mu:** oyunda ölçüldü — sabrı biten müşteri kalkarken balon kökle birlikte yürüyor (child), 4 sn içinde
  iniyor (`SayUntil` + çıkış bitiminde `HushSeat`). Yeniden görülürse durum istenir.

### 9.41 · Kadro önizlemesi (2026-09-07)

Yazar: *"tüm karakterleri ve animasyonlarını inceleyebileceğim bir önizleme kur"*. İki kapı, aynı zamanlama:

- **Oyun içi:** `LastCall → Patron Preview` (`Assets/Scripts/Editor/PatronPreview.cs`). Solda kadro (yüz + kaç klip var), sağda
  seçili klibi oyunun kendi hızında oynatan sahne, altında her karenin şeridi (kareye tıkla = durdur ve o kareye git), üstte
  klip/zoom/döngü ve "Whole cast" kontak modu. "Check ink" seçili kişinin idle karesindeki siyah piksel sayısını sayar —
  `Tools/patron_ink.py` geçidinin editördeki karşılığı. Rig'in ayak çizgisi (220 tuvalde 210) sahnede çizili.
- **Unity'siz:** `py -3 -X utf8 Tools/patron_sheet.py` → `Tools/patron_sheet.html` (116 KB sayfa) + `Tools/patron_sheet_frames/`
  (21 MB kopya kare, ikisi de .gitignore'da). Tarayıcıda aynı kontroller; gönderilebilir.

Zamanlama iki yerde de oyunun kanunu: 12 fps, yürüyüş döngü, tek atışlar bir kez oynayıp son karede (idle pozu) durur, içme
`DrinkTicks` tablosuyla (orta kare = yudum, 5 tık) 4.4 sn'lik çevrimde. Bu iki sayı `TycoonHud`'da yaşıyor ve editör derlemesi
`LastCall.UI`'ye bakamadığı için önizlemede yeniden yazıldı — kaydıkları gün ikisini de düzelt.

### 9.42 · On ikinci liste: müşteri rehberi, gece kulübü kadrosu, gerçek tepkiler, kapanan yürüyüş (2026-09-07)

- **Rehber (`Assets/Data/customers/roster.json`, 100 kişi):** bir satır bir kişi — kimlik (ad/yaş/ülke/bayrak), ses, giyim kaydı
  (club / street casual / smart casual), promptun kurulduğu `look` cümlesi, üç tepki stili (cheer / upset / order) ve üretim durumu
  (`art`: drawn/briefed/planned, `inGame`). `Tools/patron_roster.py` okur: `report`, `todo`, `check` (oyunla her tutarsızlık),
  `sync`, `md`. `Tools/patron_brief.py` rehberden prompt defterini üretir — kişi bir yerde karar verilir, iki yerde yazılmaz.
- **Kadro gece kulübüne çekildi (yazar: "ben normal sivil gece kulübü / pub / bar insanı oluşturmanı istiyorum"):** iş üniformalı
  ve yaşlı 11 kişi emekli edildi (abuelita, busdriver, cleaner, linecook, mechanic, nurseaide, retiree, guayabera, heavyset,
  silverbob, fisherman) — kadro satırı, kimlik kaydı, ses kaydı ve kareleri silindi; sebepleri rehberin `retired` listesinde.
  guard ve rider yazarın çağrısıyla kaldı, gece kıyafetine geçtiler. Kalan 13 çizim, 87 kişi sırada.
- **Ülkeler oyuncu tabanına göre:** ABD 34 (çoğu göçmen topluluk), Japonya 6, İtalya/Türkiye/Almanya 5'er, Çin/Kore/İngiltere/
  İspanya/Avustralya 4'er, Arap dünyası 5, Afrika 4, Güney/Doğu Asya 5, Kuzey Avrupa 5. Altı yeni ses: `chinese`, `korean`,
  `arabic`, `african`, `desi`, `aussie` (toplam 21).
- **Tepkiler gerçek insan tepkisi (yazar: "daha gerçekçi ... biraz daha tiyatral"):** `CHEER_STYLES` beş hâl — başparmak, iki el
  havada, parmakla gösterip başını sallama, gülümseme, kahkaha; `UPSET_BIG` beş hâl daha büyük yazıldı; `ORDER_STYLES` iki hâl,
  üçte biri parmak kaldırarak sipariş veriyor. Her kişiye rehberden atanmış. `CALM` yalnız yürüyüş ve bakışlarda kaldı; tepkiler
  `BIG` altında yazılıyor.
- **Yürüyüş kapanıyor:** ölçüldü — komşu kareler ~5000 piksel, son→ilk ~7300, yani çevrim değil tek adım. Klip artık iki yarım
  (`walk_a` sağ adım, `walk_b` sol adım + başlangıç pozuna interpolasyon), 17 kare; `keep_first_frame:false` da 8 isteyip 9 gelen
  kopya kareyi kaldırıyor. `Tools/patron_walk_fix.py check` dikişi ölçer.
- **Havlu:** rect artık çizimin tam ölçüsünde (`FitCloth`) — alfa isabet testi rect üzerinden örneklediği için `preserveAspect`'in
  bıraktığı boşluk hitbox'ı şişiriyordu. **Sipariş balonu kaldırıldı:** balon yalnız içki teslim edildikten sonra, her yudumda.

### 9.43 · On üçüncü liste: Miami gardırobu, desenler, on Avrupalı (2026-09-07)

- **Mekân Miami (yazar: "ceket ve mont giymemeleri gerekiyor, yazlık gömlek tshirt crop bluz, atlet"):** rehberdeki 43 kişinin
  üstü yazlığa çevrildi (mont/ceket/hırka/kapüşonlu/triko yok), ev kurallarına da "sıcak Miami gecesi, ceket yok, kollar açık"
  maddesi eklendi — model söylenmediği yerde herkese mont giydiriyor, ilk yüzün 41'i montlu geldi.
- **Desenler (yazar: "karakteristik desenli kıyafetler de olsun ... çok düz olmasınlar"):** rehber `garment` sütunu kazandı —
  plain ya da tek bir desenli parça (leopar, çizgi, puantiye, palmiye, çiçek, parlak saten, simli, payet, renk bloğu, batik,
  ekose, geometrik). Beşte iki oranında dağıtıldı; çizilmiş herkes `plain` kalıyor çünkü sanatları zaten var. Tek parça, çünkü
  iki desen 220 pikselde gürültü oluyor — brief'in başından beri "no pattern" demesinin sebebi buydu.
- **On Avrupalı bitti** (atelier, barista, archivist, gallerist, trainee, skater, florist, junior, busker, couriereu): 11'er klip,
  15 kare, yürüyüş dikişi 0. Hepsi mürekkep geçidinden geçti, kadro satırları yaz çekiminden yeniden ölçüldü. Kadro 23 kişi.
- **Kimlik dosyası rehbere eşitlendi:** eski rig'den kalan 23 kayıt silindi (kimse çizmiyor), Ece ve boş yedek satır kaldı;
  `patron_roster.py check` artık sıfır uyuşmazlık veriyor.

### 9.49 · On dokuzuncu liste: yazarın kartı, parmak ucu, para, yazı tipi, market kartı, paneller, oda (2026-09-08)

- **Mahzen şişe kartı yazarın sanatında** (`bottle_card.png`, 147×84: 3 px macenta çerçeve, erik zemin, solda yuva + ayraç).
  İki parça (`card_slot` 42, `card_body` 105), **6 px'ten dilimli** — çerçeve 3 px ama köşeler 6 px yuvarlak (`#+++#`); 3'ten
  dilimlenince çapraz kısmı uzayan kenar şeridine düşüp her panelin ayağında 9 px'lik merdiven yaptı (ölçüldü). 2x çizim
  (`pixelsPerUnitMultiplier` 0.5). Yuvada şişenin kendisi: mahzen plakaları `BottleArt` sandviçinde, kalan miktar kadar dolu.
  Kutuda: ad Silkscreen Bold 24 açık mürekkep (yazarın "Aseprite fontu" elde yok; evin kalın yüzü), aile · TIER, rung yıldızları
  (`RungOf`), `$` ikonu + kehribar fiyat, stok satırı + çubuk, "IN THE BOOK" altında kokteyllerin **menü ikonları** (`DrinkIcon`, 32)
  ve adları (en çok 6, kalanı sayı). Kart içeriğe göre büyür (`LayOutCellarCard`: en geniş satır + 12 pad, en az 220; boy
  satırların toplamı, en az şişe boyu). Kendi kanvası 26 (kitap prop'u 8'de kartın önüne geçiyordu), ekranda dikey kelepçe.
- **Garnish kartı aynı kart:** rayın kâseleri `ShowGarnishCard` — yuvada kâse, adı, "GARNISH · söz", fiyat ya da ON THE HOUSE,
  raftaki stok, amaç cümlesi (`GarnishPurpose`). Tarifler garnish adı taşımadığı için "kullanıldığı kokteyller" listesi yok.
- **İmleç:** yazarın üç çizimi (`Tools/cursor_src/{pointer,click,grab}.png`, 10x) 2x'te 32×32; parmak sol-üste bakıyor, sıcak nokta
  **(3, 1)**. `ForgivingRaycaster` (tüm GraphicRaycaster'ların yerine): tam nokta boş dönerse 3 ve 6 px'lik iki halkada sekizer
  nokta daha denenir — "parmağın etrafı tıklarken seçebilmeli".
- **Kitap prop'u tıklanmıyordu:** `Reach` 110 birimlik prop'un ayağından 60 birim yükseliyordu; üst yarı deliklikti ve tık
  pencereye düşüyordu. Reach artık prop'un tamamı + 6/8 birim hava. Ölçüldü: raycast prop merkezinde Reach'i buluyor.
- **Para ikonu her yerde:** `ItemArt.Coin` → yazarın `dollar` (32), `dollar_24` (19×23 glif 24'e ortalı, ölçeksiz), `dollar_16`
  (2:1 çoğunluk kesimi). Eski coin3d dosyaları duruyor ama çağrılmıyor. Panodaki para artık **birimle** (24) ölçülüyor — ekran
  pikseliyle ölçülünce küçük pencerede altmış birimlik para pano üstünde yüzüyordu.
- **Konuşma yazı tipi:** Silkscreen'de küçük harf yok, ğ ı İ ş yok. On altı aday 8–48 arası her boyda basıldı, anti-alias oranı
  ölçüldü: **Jersey 15** (OFL) gerçek küçük harfli, tam Türkçe, 27'de keskin (%0.1 yumuşak piksel) ve Silkscreen 16'dan geniş
  değil. Tiny5 (16'da keskin) önce girdi, yazar "kötü" dedi — 5 px yüz 2x'te ince. `_speech` Resources'tan yüklenir
  (`Fonts/Jersey15-Regular`), balon ve fişin üç satırı 27'de; adlar ve içki adları artık büyük harfe çevrilmiyor,
  konuşma `Sentence()` ile cümle düzeninde. `TagPad` 7 → **10**.
- **Market kartı eski dile döndü:** pencere 148 (koyu, `Night[2]`), ayak açık plaka; şişeler **dolu** (`TileSpec.Card` →
  `TileBottle`: mahzen plakaları 2x, seviye 1.0), ad iki satır, ince stok şeridi, `$` + rakam, tuş. Açıklama satırı ve durum
  satırı gitti — durum pencerenin sağ üstünde damga. Rung yıldızları pencerenin sol altında. **NEW!** bandı: `Rating.PreviousStanding`
  (kapanıştan önceki durum) ile `Average` arasında kalan kapı dün gece açılmıştır (`TycoonRun.OpenedLastNight`), pencerenin sol
  üstünde 18° macenta bant; `NewArrivalTests` bunu pinliyor. Upgrade piktogramları **48** (24 ızgarası 2x + kutulara bevel),
  tile'da 2x.
- **Paneller tek sanat:** `ChromeArt.Panel()` (kartın çerçeveli yuvası, 6 px dilim) — gün sonu panoları (cyan kapaklı enstrüman
  gitti; başlık macenta, okuma kehribar), ayarlar plakası (Card + Frame yerine), perde tarih kartı. Dev ve rehber panelleri
  dokunulmadı.
- **Perfect sayfa:** baskının altında soluk platin kâğıt (`BkPerfectPaper`, 8 birim içeri), ad Silkscreen Bold 24 koyu erik,
  kaş platin; sayfa düzeni aynı.
- **Oda:** `lamp_left`/`lamp_right`/`table_mid`/`counter_end` yuvaları ve fener, kâğıt fener, tezgah mumu, orta masa (3 basamak),
  monstera fikstürleri kesildi (yazar: "mum lamba aydınlatmaları kalksın, duvar lambaları kalsın; en fazla 2 masa; bitki kötü").
  Masalar 128/516 → **224/416**, y 129 → **126**. Katalog 39 → 32.
- **Kart şişenin ARKASINDA oluşur** (yazarın notu, aynı gün): yuva rafta duran şişenin kendi yerine ve boyuna oturur (kopya
  gerçek şişenin üstünde, kimse yerinden oynamaz), kutu yanına uzanır; ekranın sağ üçte birindeki şişelerde kart döner —
  aynı sanatın aynalanmış kesimi (`card_slot_l`/`card_body_l`), kutu sola. `StepCellarCard` kapı plakasının HUD-birimli
  köşelerinden merkez ve boyu okur; garnish kâseleri için aynı. Kart zemini yazarın dosyasında 226/255 alfa — yarı saydamlık
  sanatın kendisi. Gün sonu perdesi 0.88 → **0.965**. Market kartlarındaki `$` ikonu yerine düz yeşil `$` (display face,
  `Lime[2]`); çizili dolar panolarda kaldı.
- **Doğrulama:** EditMode 504/504, PlayMode 11/11. Fotoğraflar: mahzen kartı (Krakatoa Rum, sağ elli Quinn's Tonic), garnish
  kartı (lemon twist), Jersey 15 balon/fişler, market likör/geliştirme sekmeleri, gün sonu panoları, ayarlar, perfect sayfa.

### 9.48 · On sekizinci liste: tek balon, parmak ucu, emoji demeti, içindekiler satırı, duran oda (2026-09-08)

- **Sipariş/isim fişi aynı balon:** `SeatView.TagBg`/`Tail` artık `ChromeArt.SpeechBox`/`SpeechTail` (yazarın beyaz
  gövdesi + çizili kuyruk, 20×12, y=2). Kenar rengiyle söylenen durum (Order/Take/Drink) fişten kalktı; durum ikonların
  altındaki `IconRule` renginde ve noktalarda yaşıyor.
- **İmleç parmak ucundan hedefliyor:** Windows donanım imleci 32×32'dir; 48×45'lik çizimi `CursorMode.Auto` küçültüp sıcak
  noktayı ortaya kaydırıyordu. Denendi: `ForceSoftware` boyutu korudu ama eli her ekran görüntüsüne çizdi — look testleri
  48×45'lik bir bölgede kırmızıya döndü. Kalıcı çözüm: üç kare **2x, 32×32** (`Tools/ui_art_2026_09_08.py`, kaynak
  `Tools/cursor_hand_3x.png`), sıcak nokta **(19, 0)** — parmağın üst satırı x 16..21. Ve `StepCursor` yalnız
  `Mouse.current.native` iken çalışıyor: PlayMode süitinin sanal faresi altında imleç değiştirmek tıklama düşürdü (her
  koşuda başka test: makara, market, lavabo; tek başına geçiyordu).
- **Emojiler demet hâlinde, arkadan:** HUD'daki tek yüz gitti; `ReactionMotes.Burst` yeni aşırı yüklemesiyle (`Sprite[]
  faces, units`) sahne motları olarak, omuzların arkasından yükselip sallanarak sönüyor. Vuruş başına adet: PERFECT/
  FLAWLESS/BOND 4, PATIENCE/CLOSE 2, diğerleri 3; yüzler havuzdan **birbirinden farklı** (`EmotesFor`, ilk seçim voice
  akışından, gerisi havuzu dolaşır). Boyut **16 sahne birimi** (1 birim = 1 sanat pikseli); 32 denendi, başın genişliğinde
  bulanık yüzler çıktı.
- **İçindekiler satırı 50 → 72, adım 56 → 78;** sayım metni 36 yüksek (iki satır) — "11 POURS · 1 PERFECT · 7 LOCKED"
  16'da sütuna sığmayıp alttan yukarı büyüyerek başlığa biniyordu.
- **Kitap açıkken oda da duruyor:** `DiegeticStage.AmbientScale` (HUD her kare `clock` veriyor: kitap 0, tezgâh menüleri
  0.3, aksi 1) ve `_ambientClock`; pencere rüzgârı (`Time.unscaledTime` yerine bu saat), musluk suyu kareleri ve ışık
  titremesi ondan akıyor. Ölçüldü: kitap açıkken 35.324 → 35.324 (2 sn), kapanınca yürüdü. Kapıya bağlı değil — kapalı
  barın penceresinde hava sürer.
- **Koruma:** `RefreshSeats` koltuk atarken `i >= _seats.Count` ise durur (koltukları kurulmamış bara karşı başlatılan
  koşuda her kare `ArgumentOutOfRange` atıyordu; yansıma probunda görüldü, oyunda yol yok ama sınır ayrı).
- **Doğrulama:** EditMode 503/503, PlayMode 11/11 (yazılım imleciyle 5 kırmızı, donanım 32×32 + native kapısıyla temiz).
  UnityMCP'nin `TestJobManager` sınıfı `internal`; sıkışan işi yansımayla `ClearStuckJob` temizledi.

### 9.47 · Yazarın 2026-09-08 sanatı: bardak dudakları, tin ön plakası, el imleci, yıldız/kalp, balon, otuz emoji (2026-09-08)

- **Bardak dudakları:** `Items/glass3d_<bardak>_t<N>_Front` (t2–t6; t1'in ön parçası yok) bardağın sıvının ÖNÜNDE kalan
  kısmıdır — birkaç satırlık rim şeridi (coupe 41×6, highball 26×4, rocks 36×5). `GlassArt.Piece.Lip` + `LipPlacement(box,
  out size, out topCentre)`; `LipRowTable` {coupe 13/88, highball 14/96, rocks 13/72, martini 11/88} şeridin ana çizimdeki
  satırını söyler. Servis tezgahında `_serveGlassLipRt` her kare `FollowServeGlassLip()` ile bardağı izler (eğilen bardakla
  döner); koltuktaki bardakta `_drinkGlassLip`. Eski `_front` araması `_frontplate` oldu — yeni dosyalarla ad çakışıyordu.
- **Tin ön plakası:** `tin_open_Front` / `tin_open_t2_Front` (82×124, 116×208'lik ana çizimin x 17..99, alttan y 13..137'si)
  sıvının önüne `_tinFrontRt` olarak biner ve `FollowTinFront()` ile fırlatılan tin'i izler (`StepBlowout`'tan sonra çağrılır;
  o adım tin'in yerini son belirleyen). Plaka opak çelik: ağız boşluğu ana çizimde alttan **0.615–0.73** bandı. Dolu tin'in
  havuzu eskiden rim'in 7+22 px altındaydı, yani tümü plakanın arkasına düştü; havuz artık **ağzın içinde** (`rimY + 2` →
  `MouthTop` 0.70). Yazarın 09-06 kuralı duruyor: dolmadan sıvı görünmez, dolunca ağızdan görünür.
- **El imleci:** `CursorSkin` (yeni dosya, statik) üç kare: `cursor_hand` (yazarın Hand3, 16×15'in 3x'i = 48×45),
  `cursor_hand_pressed` (bir hücre aşağı, parmak bir hücre kısa), `cursor_hand_grab` (işaret parmağı yumruğa katlanmış) —
  ikisi `Tools/ui_art_2026_09_08.py` ile aynı çizimin hücre ızgarasından türetildi, elle çizilmedi. Sıcak nokta parmak ucu
  (28,1). `TycoonHud.StepCursor()` her kare: `TycoonServiceFlow.IsHolding` (şişe/kaşık/bardak elde) ya da taşınan bardak/bez
  → Grab; sol tuş basılı → Pressed; yoksa Idle. `Cursor.SetCursor` yalnız durum değişince (sürücü çağrısı). Play'den çıkışta
  `CursorSkin.Reset()`. Ekran görüntüsünde görünmez (OS imleci); durum `_shown` alanından okundu: Idle.
- **Yıldız ve kalp:** `ItemArt.Star(lit, px)` → `star_small(_socket)` 14×12 / `star_big(_socket)` 18×17; `Heart` → `heart_lit` /
  `heart_socket` 12×12, `HeartHalf()` → `heart_half`. Üst bar, kitap rozeti, kimlik şeridi, market rung'ları hepsi bu iki
  erişimciden geçtiği için tek değişiklikle yenilendi.
- **Balon:** `ChromeArt.SpeechBox` yazarın `bubble_white_10`'u (32×32, 8/8/8/8 dilim); kuyruk `bubble_white_2`'den kesilen
  `speech_tail` (10×6, üst satırı gövdenin alt çizgisi), 20×12'de y=2'ye biner. **Kuyruk artık tırmanışla uzamıyor** — 12 +
  dy'lik eski kural çizilmiş kuyruğu yüz piksellik beyaz bir sivri uca çeviriyordu (ölçüldü: ortadaki balonun kuyruğu ~100 px);
  yükselen balon sahibinin üstünde durur, kuyruk başın üstünde kalır.
- **Otuz emoji, on vuruş** (`Resources/Emotes/em_<n>`, 16×16, oyunda 2x = 32; `PatronArtPostprocessor` klasörü sprite kuralına
  aldı — ilk import Default doku olarak indi, `Resources.Load<Sprite>` null döndü, force reimport gerekti). Tablo
  `TycoonHud.Seats.EmoteTable`: PERFECT 85·6·50·51·19, FLAWLESS (ilk kusursuz yapım) 19·85·21, ANOTHER 49·53·42·115, CLOSE
  27·67·51, WRONG 35·1·10, AWFUL (yanlış VE tatmin < `ReactionSour`) 65·71·15, PATIENCE 17·81·9, STORM (servis edilmeden
  gitti) 73·9·23, KICKED 23·3·33, BOND (müdavimin kusursuz içkisi, `Relationship ≥ Regular`) 118·86. Kancalar: `ServeReaction`
  (hüküm; `SeatView.HeldEmote` varsa — servis anında seçilen FLAWLESS/BOND — o kazanır, tek yüz), kalkış (yalnız storm ve
  kick; sakin giden zaten söyledi), sabır `< 0.34` bir ziyarette **bir kez** (`SeatView.Nagged`, taburede yeni ziyaretle
  sıfırlanır). Seçim `run.VoiceStream` üzerinden — tohumun, karenin değil. Gösterim `Emote()`/`EmotePop`: koltuk kökünde,
  başın 12 altında 0.6 ölçekle başlar, OutBack (evde yoktu; `EmoteEase`) ile 0.22 sn'de başın 10 üstüne çıkar, 1.1 sn durur,
  0.35 sn'de 14 birim süzülüp söner; `Motion.Reduced` anında. Vuruşsuz kalanlar (12 tek gözyaşı, 18 salya, 37 baş dönmesi)
  bilerek bağlanmadı — vuruşu olmayan yüz hiç yüzden kötü.
- **PixelLab:** 3777/10000 kullanım; yazar "5000 dolunca yeni karakter yaratmaya ara" dedi — karakter yaratma o eşikte durur,
  var olanların klipleri devam eder (klip karakter yaratmaz). istanbul/kadikoy/izmir/ankara/madrid'in look/order/drink
  klipleri çekildi; cheer_a/upset_a/walk_a kuyrukta (15 iş).
- **Doğrulama:** Roslyn üç derleme temiz; Unity 0 hata; EditMode/PlayMode aşağıda. Fotoğraflar: üst barın yeni yıldız/kalpleri,
  balonun yeni gövdesi, servis tezgahında rocks t6 dudağı (ilk çekimde (0,0)'da asılıydı — `FollowServeGlassLip()` tanımlı ama
  ÇAĞRILMAMIŞTI; `FollowTinFront()` da öyle, plaka `Place` sayesinde yerindeydi ama fırlatmayı izlemiyordu — ikisi de bağlandı).
  Hayalet girdi bir kez daha: bu sefer klavye olaylarının fareye düştüğü "State format KEYS ... MOUS" — bench kendi kendine
  açılıp Smirkoff döktü; Clear Ghost Input sonrası cihaz listesi BOŞ kaldı (yerli fare/klavye yeniden keşfedilmedi) — editörü
  yeniden başlatmak gerekir, yansımayla çekim yapmaya engel değil.

### 9.46 · On yedinci liste: gece tezgah temizlenince biter, menü sayfası ve market kartı yeniden (2026-09-08)

- **Gece tezgah temizlenmeden bitmiyor.** `TycoonRun.Tick` gün kapatma bloğuna yalnız `Floor.IsComplete && Floor.House.CounterClear`
  iken giriyor. Kapı KOŞUDA, katta değil: `BarDay.IsComplete` "kapanış saati ve tabure boş" olarak kaldı (`FloorEmpty` aynı şeyin
  adı), çünkü kat testleri servis edip kimse temizlemeden katın boşalmasını bekliyor — bez katın değil koşunun. İki fiil
  süpürerek kapatır (`Housekeeping.SweepForClosing`): `DevSkipToDayEnd` ve simülatörün "never wipes" elleri (çürümenin
  bedeli zaten `CloseNight`'ta okunmuştur, süpürmek geri vermez). El şeridi kat boşken ve tezgah kirliyken "LAST CALL · CLEAR
  THE COUNTER TO CLOSE" yazıyor.
- **Sürüklenen bardağın silüeti tezgahta kalmıyor:** `RefreshDirtyGlasses` taşınan koltuğun prop'unu taşıma süresince
  çizmiyor (Core bardağı lavabo alana kadar mess'te tutuyor, prop her kare mess'ten çiziliyordu).
- **Kitap:** chapter satırı çerçevenin iç çizgisinin (5. sanat satırı = 10 birim) altına indi (-8 → -18, `BkContentTop` 80 → 90);
  `RatioDots` iki uçta birer piksel pay aldı (son dot'un merkezi 60, kenarı 64.4, doku 64'tü); satır aralığı sayfaya göre ölçülüyor
  (`rowPitch = clamp((BkStoryTop − y) / satır, 32, 48)`, öykü ayağı alttan 142 = üstten 510) ve şişe kutusu ona uyuyor — Long
  Island 7 satırla sığıyor; perfect kurdelesi gitti (170×22'lik 45° kurdele uzun adı örtüyordu: "SEX ON THE BEA"), söz chapter
  satırına platin mürekkeple ("HOUSE PRIDE · PERFECT RECIPE"); içindekilerde mükemmel sayfa PERFECT etiketi, chapter satırı
  "N PERFECT" sayımı taşıyor; rozet kare-rakam yerine platin yıldız + sayı; kitap prop'u kendi kanvasında **8**'de (kepengin
  isabet plakası 6'da odanın enini kaplıyor, bez gibi), açık kitap 15 → **27**.
- **Mahzen plakaları:** aynı rafta ardışık iki plaka çakışırsa ikincisi bir plaka boyu iner (balonların merdiveni, plaka ölçeğinde).
- **Market kartı baştan:** 176×256, beş sütun (5×176 + 4×12 = 928 / 1004). Resim penceresi tam genişlik, rung yıldızları
  pencerenin sol altında; **ad kâğıdın üstünde** (lacivert plaka gitti — sayfadaki kâğıt olmayan tek nesneydi ve "SMIRNOFF VODKA"yı
  "SM / VO" diye kesiyordu), Silkscreen Bold 16 büyük harf, en çok iki satır; bir satır OLGU (stok çubuğu + yüzde, ya da meta);
  DURUM satırı; ayakta **madeni para + rakam** (kehribar etiket, chrome'da yazılı `$`ın kaldığı son yerdi) ve tuş. Ölçüldü:
  display face 16'da 156 sütuna 9 karakter sığıyor, "RESTOCK THE WHOLE WELL" üç satıra bölünüp son kelimeyi kaybediyordu.
- **Doğrulama:** EditMode 503/503, PlayMode 11/11 (editör yeniden başlatıldıktan sonra; öncesinde `InputSystem.devices` boştu ve
  süit koşamıyordu). Ara kırmızılar — iki market kartı testi — eski kartla da düştü ve aynı kodla sonra geçti: sebep play modunda
  eklediğim probe sanal faresiydi, kart değil (hafıza: playmode-red-is-usually-ghost-input, beşinci sebep).

### 9.45 · On beşinci liste: kimlik açılıyor, vesikalıklar tek çerçevede, fatura panoları dizildi (2026-09-07)

- **Kimlik açılıyor:** kart 0.86'dan tam boya `IdOpenSeconds` (0.14 sn) içinde, ölçeklenmemiş saatte; `Motion.Reduced`'da anında.
- **Vesikalık kuyusu banttan ayrıldı:** `LicPortrait` y 18 → **24 sanat pikseli** (bant `LicPad/2`'den başlayıp 19'da bittiği için
  18'de başlayan fotoğraf bir piksel bindiriyordu, ikisi kaynamış tek blok gibi okunuyordu).
- **Damga şeridi iki satır, iki sütun.** Okunur 16'da `VISITS` 58, `RATES US` 89 birim mürekkep; ray 144, yani yan yana 13 birim
  bindirip `VISITSRATES US` basıyordu. Denendi ve ölçülüp atıldı: her başlığa kendi satırı (dört satır = 46 px, kart 100 px ve
  şerit 76'da başlıyor — kendi kartından 22 px taşıyor). Kalan düzen: sol sütun delikler (12'lik, 2x), sağ sütun yıldızlar
  (`LicStampCol` 82, iki grubun arasında 6 birim hava), başlıklar üstlerinde, üç kalp başlık satırının sağ ucunda. Yıldızların
  yanındaki ortalama sayısı **kaldırıldı** — yıldızlar zaten o sayı, ve sığmıyordu (sütun 68, yıldızlar 65).
- **Bayrak mührü kendi bayrağını tutuyor:** roundel 42 birimken içindeki bayrak 64 genişti; maske bayrağı kesiyor, taşan kısmı
  KICK tuşunun üstünde bozuk bir çizim gibi duruyordu. Mühür **56**, bayrak **48×33** (çizildiği boy, 1:1 — tam kat, çünkü 1.3x
  bayrak bazı şeritleri iki kat kalın yapar).
- **Vesikalıklar tek çerçeve:** her yüz **64×64**, baş karenin %52'si, tepe sabit mesafede — kesim omuzdan, geometri gereği.
  Eskiden kare figüre göre ölçülüyordu (omuz genişliği × 0.78), yani 42–75 px arası (1.79 kat) çıkıyor ve hepsi aynı pencereye
  `preserveAspect` ile çizildiği için o fark **büyütme farkına** dönüşüyordu. Boyun, başın kendi merkez sütununda aranıyor —
  tüm siluette en dar satır **bel** çıkıyor, çünkü bu rigler kolları gövdeden ayrık duruyor.
- **Gece panoları dizildi:** her okuma satırında kelimenin bittiği yerden rakamın başladığı yere **kılavuz nokta dizisi**
  (ölçüm: başlık mürekkebi x95'te bitiyor, rakam x247'de başlıyordu — beş satır boyunca 150 boş piksel). Rakamlar tek sütunda,
  birim ikonu kendi 26 birimlik oluğunda. Hafta panosunda gecenin **faturaları ve CEZALARI** kendi kuralının altında (fiş
  zaten basıyordu, `DayFines` zaten `DayExpenses` içindeydi — eksik olan, gecenin ne tuttuğuna bakılan yerde yazmasıydı).
  Merdiven 36'ya indi ki ayak plakanın içinde kalsın.
- **Para ikonu:** `Tools/coin_icon.py` — yıldız/kalp dilinde, iç Lime, siluetin içinde tek kontür. Yazılan `$` yerine geçiyor:
  kasa, pano toplamları, sepet. Üç ders: ikon **ekran pikseliyle** ölçülür (gün sonu paneli 0.45 ölçekte, 16 birimlik ikon 7
  piksel çıkıyordu); 16'lık usta **elle yazılır** (okunur bir dolar 11 satır ister, 16'lık madeni parada 10 var — üretilen her
  sürüm kendi gözlerini kapattı); ve **8'lik yok** — yıldızla kalp o boyu siluetiyle taşıyor, madeni paranın anlamı ise doğası
  gereği iç detay. Sığmadığı yerde (gecenin üst üste iki rakamı, fişin basılı mürekkebi, marketin kehribar etiketi) rakam
  kendi yazısını koruyor.
- **Yürüyüş ve konuşma:** girişte/çıkışta **fade yok**, tam hızda ekranın dışına; ayrılırken söylenen söz balonun kendi
  saatiyle inmiyor, sahneden çıkana kadar başlarının üstünde. Diyalog **Silkscreen Bold**'da (aynı metrik, çift vuruş).

### 9.44 · On dördüncü liste: bez çivide, kimlik kısaldı, garnish kartları, mahzen etiketleri (2026-09-07)

- **Temizlik bezi:** kendi kanvasına alındı (sıralama 8) — kepenğin isabet plakası 6'da ve odanın enini kaplıyordu, rayın
  boyunca bezin tıklamalarını yiyordu. Elde artık **çiviye asılı**: `_clothGrabOffset` sıfır, rect'in pivotu üst ortası, yani
  imleç nereden tutarsa tutsun bez üst orta noktasından sarkıyor. Ve **sallanıyor**: imlecin yatay hızı bir yaya giriyor, bez
  çivinin etrafında dönüyor (`ClothSwingPerSpeed/Max/Stiffness/Damping`), el durunca dikleşiyor; `Motion.Reduced`'da sabit.
- **Kimlik:** kart 200 → **176 sanat pikseli** (sağdan 24 px kısaldı; belge numarası v4'te banda taşındığından sağ sütun boş
  hava taşıyordu). Şerit artık kâğıdın içinde — tam genişlik yerine `LicPad` payıyla ve en üstten başlıyor; eskiden 6 birim
  aşağıdan başlayıp yuvarlak köşelere dayandığı için taşıyordu.
- **Garnishler tek dilde:** fiş (`LayOutOrderIcons`), kimlik (`PrefChip`) ve tezgâhtaki kaseler artık aynı çizimi kullanıyor —
  `PrefArt.ForPreparation`, yoksa `ChromeArt.Mark` yedeği. Kimlikte yazı kalktı, yalnız ikon; üstüne gelince adı çıkıyor.
  Tezgâhtaki kaseler hover'da kart veriyor: ikon, ad ve ne işe yaradığı (`GarnishPurpose`, ya da hazırlığın kendi açıklaması).
- **Mahzen etiketleri okunuyor:** aile adları artık **plakada** — koyu zemin, altında pembe raf kenarı çizgisi, üstünde krem
  yazı, genişlik yazıya göre ölçülüyor. Ölçüm: rafın üstünde krem yazı 5.6:1, plakada 15.8:1 (metin 4.5 ister).
