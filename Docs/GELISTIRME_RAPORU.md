# LAST CALL — GELİŞTİRME RAPORU

**Denetim tarihi:** 2026-08-07 · **Günlük son güncelleme:** 2026-08-10
**Yöntem:** 8 kollu kod denetimi (dosya:satır kanıtlı) + sim raporu + doküman-kod karşılaştırması
**Eş belge:** `Docs/GDD_MEVCUT.md` (oyunun bugünkü kuralları)

> Bu belge iki şey taşıyor: **§0 geliştirme günlüğü** (ne yapıldı, neden, hangi ölçümle) ve
> **§1–8 duran denetim** (sistem sağlığı + öncelikli borç listesi). Denetim 2026-08-07'de
> yazıldı; günlük ondan sonrasını sürüyor ve denetimin bayatlayan satırlarını §0.5'te
> tek tek işaretliyor. Bir sayı iki yerde çelişirse **günlük doğrudur** — o ölçülmüş,
> denetim hatırlanmış olabilir.

---

## 0 · Geliştirme günlüğü

### 0.1 · Kapsam ve ölçüm

2026-08-09 süpürmesinden (`8add99e`) bu yana **40 commit**. Bugünün ölçülmüş hâli:

| Ölçü | Değer | Not |
|---|---|---|
| EditMode testi | **186** (11 dosya, 3.621 satır) | denetim günü 175'ti; +11'i yeni içerik kuralları |
| Core | 30 dosya, **5.181 satır** | saf C#, `noEngineReferences` |
| UI | 25 dosya, **14.791 satır** | hâlâ sıfır otomatik test (§4 borcu duruyor) |
| `TycoonHud.cs` | **5.833 satır** | denetimdeki "3.4k" iki kat bayat — tek sınıf borcu **büyüdü** |
| `Resources/Items` | 308 PNG | market kiti + şişe plakaları |
| `Resources/Patron` | 30 karakter klasörü | her biri 6 klip + vesikalık |
| `Resources/Fixtures` | 7 PNG | yeni: modüler sahne parçaları |

### 0.2 · İş kolu — kimlik kartı ve kadro (2026-08-10)

Oyunun gizli-bilgi mekaniği kartın kendisinde yaşıyor, o yüzden kart bir kozmetik değil
bir okuma yüzeyi. On commit'lik bir tur:

| Ne | Neden | Kanıt |
|---|---|---|
| **31 karakterlik kadro** (`db10df2`, `6efdbdb`, `9e891b6`) | tek arketip portresi 31 kişiyi temsil edemiyordu | her karakter 6 klip; `patron_casting.html` ile klipler oyunun kendi hızında oynatılarak seçildi |
| **Yıldız kapıları** | "kim ne zaman gelir" bir ilerleme ekseni olsun | kapılar 0 / 1.5 / 2.5 / 3.5; rehber bu sırada listeler |
| **Vesikalıklar 1:1** (`5328758`) | 31 yüzün 26'sı **kesirli** oranda büyütülüyordu (1.014×–1.241×) | NEAREST kesirli oranda bazı satırları ikiler, hepsini değil — "sünmüş" his buydu |
| **Tepe payı bestelenerek** (`b0a853a`) | 4 karakterin tacı kaynak satır 0'da; kırpma pay üretemez | figür plakaya tam piksel kaydırılıyor: herkeste tam 12 px |
| **Burun hizası** (`7734c60`) | gövde kutusunun merkezi omuzdur, yüz değil | ten bandının medyanı **kötüleştirdi** (10 yüz kaydı); bandı alın–burun arasına daraltmak çözdü |
| **Sürücü belgesi yapısı** (`5328758`, `8b4a42b`) | belge hissi numaralı alan ızgarasından gelir | 1–5 numaralı alanlar, veri kutuları, onay hücreleri |
| **Kağıdın gerçek sınırı** (`8b4a42b`) | üretici kartı **opak beyaz zemine** çizmiş; 256×160 tuvalde stok 228×138 | çizgiler kağıttan taşıyordu; her bölge kremden ölçüldü |

**Ders (yazıya geçti):** rect ölçümü "kart dikdörtgeninin içinde" der ve geçirir; kağıdın
kendi sınırı ayrı bir testtir. `licard.py` artık dikdörtgene değil kreme bakıyor, ayrıca
mürekkep-üstüne-mürekkep çakışmasını da ölçüyor (puan kendi üçüncü yıldızının üstüne
basıyordu, her metin kendi kutusuna sığdığı için tüm eski testler geçmişti).

### 0.3 · İş kolu — 2D dinamik ışık ve modüler sahne (2026-08-10)

Dört fazlı geçiş; her fazın kendi kanıtı var.

| Faz | Commit | Ne yapıldı | Kanıt |
|---|---|---|---|
| **A** | `b1d67c3` | URP 3D forward yolundan **2D Renderer**'a; bloom-only volume; URP `PixelPerfectCamera`, post açık | ekran görüntüsü **birebir aynı** — kasıtlı no-op kontrol noktası |
| **B** | `32b2d11` | sahne overlay canvas'tan **world-space** `SpriteRenderer`'a; 6 `Light2D` | `ShelfCell` eski sayıların aynısını verdi (hücre 0 → cx −280, taban 26, h 51, ölçek 1.000) |
| **C** | `4535234` | `fixtures.json` + `ParseFixtures` + Core `BuyFixture`/iade/kapı | 9 yeni test |
| **D** | `5918360` | 7 sahne slotu + 7 sprite + market **DRESSING** bölümü | 7 parça ayakta, 5'i ışıklı, SHOP −$180 defterde |
| **E** | `671805b` | müşteriler world-space'e; maske yerine tezgah kırpması | küresel ışık 0.85→0.15'te gövde (88,48,45)→(36,19,17) **%59 karardı**, HUD rafı kıpırdamadı |

**Işık planı** (tahmin değil, `club_room.png`'den ölçüldü): dört tavan lambasının ampulleri
sanat x 65 / 237 / 406 / 579, y 84. Küresel yıkama 0.85 (hafif soğuk), lamba havuzları 0.55,
neon spill `NeonBlink`'in **aynı** programında.

**Sıralama defteri (dünya):** zemin 0 · oda 10 · duvar dekoru 20 · **içenler 25** · tezgah 30 ·
tezgah üstü 35. Canvas'lar: RegisterBack −7 · SignCanvas −9 · dressing −5 · HUD 5 · kasa 6 ·
servis akışı 12 · kimlik 20 · market 22 · rehber 24.

**Fikstür zinciri:** `Assets/Data/fixtures/fixtures.json` (7 parça, 5'i ışıklı, kapılar 0/1.5/2.5)
→ `DataLoader.ParseFixtures` (slot başına tek parça, yüklemede patlar) → `TycoonRun.BuyFixture`
(kozmetik: yıldız kapılı, aynı gece iadeli, **gecelik fitting harcamaz**) → HUD `WatchFixtures`
(sayaçla değişim izler) → `DiegeticStage.SyncFixtures` (slotlara diker, ışığı kurar).

### 0.3b · İş kolu — yüzeyin dürüstlüğü (2026-08-11)

Sekiz maddelik bir tur; hepsi "ekranda yanlış duruyor" ile başladı, hiçbiri orada bitmedi.
Ortak kural: **UI mobilyası üretilmez, çizilir** (`ChromeArt.cs`, yeni) — ve **hiçbir şey
bir yazı tipinden resim istemez**.

| Ne | Neden | Kanıt / ölçüm |
|---|---|---|
| **Şişe içi sıvı katmanı kaldırıldı** (`BottleFluid` silindi) | dökme sahnesinde sıvı şişenin sağından solundan taşıyordu | katmanın kendi ölçümü zaten söylüyordu: düz-dönem sprite'larının gövdesi **alfa 255** — arkasına çizilen içki doğru olduğu her yerde görünmez, yalnız siluetin **dışına** taştığı yerde görünürdü. Kavrama rect'i sabit 180, sanat `preserveAspect` ile kutulanmış: taşan tam da o fark. İçki artık şişenin kendi sanatının rengi; kalan miktar hover kartında (2026-08-07 süpürmesinin koyduğu yer) |
| **Raf kendi genişliğine göre diziliyor** | "şişeler daha büyük ve birbirine daha yakın dursun" | eskiden plank `perRow` eşit yuvaya bölünüyordu; şimdi yükseklik rafın, genişlik her siluetin kendi oranından, aralık sabit 10. Ölçüldü: sanat 33×128 → **37×144**, komşu aralığı ~129 → **10** |
| **Raf tabelası markayı değil STİLİ taşıyor** | sıkı dizilişte "SMIRKOFF VODKA" komşusunun tabelasına basıyordu (aynı hata üçüncü kez) | tabela ~45 birim; 8 puntoda ölçülen genişlik 5,4/karakter. Stil hem sığıyor hem tariflerin dili: VODKA · GIN · SYRUP · LEMON · SODA. Dört karakter sığmıyorsa tabela hiç çizilmiyor |
| **Fıçılar rafların önünde** | "en alttaki rafın altında kalıyor" | UGUI kardeş sırasına göre çizer; keg satırı ledge'den sonra ama raflardan **önce** kuruluyordu. `SetAsLastSibling` |
| **Fatura puntosu bir kademe büyük** | belgenin tamamı 8'de dizilmişti (ipucu boyutu), oysa günün okunduğu yer burası | 8→16, 16→24; satır 22→30, kritik satırı 44→64. Rakamlar `_display` yerine `_shop`: PressStart2P 24'te karakter başına 24 birim, "-$1240" tek başına sağ sütunun 146 biriminin 144'ünü yiyordu |
| **Fatura işaretleri elle çizildi** | üretilen yedi ikon 16 pikselde çamurdu | `ChromeArt` maskeleri: tek siluet, **16'da çizilip 16'da basılıyor** (1,25× ölçek piksel kenarlarını ekranın kendi ızgarasının arasına düşürüyordu). Yıldız 16 maskesinin tam 2 katında |
| **Market kartı ve ADD tuşu çizildi** | "AI slop olduğu belli oluyor" / "çok yapay duruyor" | kart gri tonlarda, durumun kağıdıyla boyanıyor: cetveli ve oturma gölgesi listenin kendi renginin tonları. Tuşun **atması** var (altında iki koyu satır) — düğme ile içinde yazı olan renkli dikdörtgen arasındaki fark bu |
| **Kitapta her tarif kutuda** | "açıkta olunca karmaşıklık oluşuyor" | ince cetvelli satırlar bir form için doğru, katalog için değil: bir spec kartı beş sıra ölçü demek, alt alta beş tanesi tek uzun sayı sütunu okunuyordu |
| **Font ikonları temizlendi** | "oyunda fontlara dahil icon emoji kullanmayalım" | ★ → ◆ ✖ ⚙ ❧ ✓ ▸ — 17 çağrı yeri. Piksel yüzler bu glifleri taşımıyor, sistem yedeğinden başka bir ağırlıkta geliyorlardı. Gerekli iki yerde (ayar dişlisi, hazır garnitür tiki) **çizilmiş sprite**, kalanında kelime |

Doğrulama: 188/188 test, ve beş yüzey play'de ölçülüp resmedildi (raf, tezgah, kitap,
fatura, market) — derlenmesine güvenilmedi.

### 0.3c · İş kolu — üç kollu denetim (2026-08-11 gece)

Yazarın emri: "veri, kural ve mantık hatalarını analiz et; gereksiz kodu kaldır;
dosya düzenini profesyonelleştir." Üç paralel denetçi + üç commit:

**Kural kolu (`21f312f`)** — beş gerçek BUG, hepsi test pinli:
| Bulgu | Sonuç |
|---|---|
| Her Built içki yanlış bardakta | fizz yalnız bardakta girebildiği için kap YARIM içkiye göre seçiliyordu — her Vodka Soda rocks'ta, kendi highball'u ölü veri. Kural: **içki bardakta kendini ilan eder** — bardak-yanı döküm bir tarifi adlandırınca içki kendi kabına aktarılır (taşırmaz, eski brim'in kestiğini tamamlar) |
| Mix kapısı yandan geçilebiliyordu | erken bir çalkalama tüm inşayı "karışık" damgalıyordu; bayat `shaken` bardağa binip hakemden tam yöntem puanı alıyordu. Kural: **karışmış tin'e dökülen her şey onu karıştırılmamış yapar** |
| 51 tarifte fiyat primi yanlış | stil bantları `band.Type` varsayılanıyla hep Spirit okunuyordu — Vodka Soda'yı raftaki T4 viski pahalandırıyordu. Prim artık bandın ADLANDIRDIĞI şişeden, stil başına |
| 9 fiyatsız şişe | $8+6/tier fallback'e düşüyordu: kuyu romu $20, kendi T2 üstü $7 — ve market en-ucuz-önce kuralıyla kötü alımı ZORLUYORDU. Dokuzuna veri fiyatı |
| Shelf.PourInto köpüğü saymıyordu | kendi yorumunun yasakladığı buharlaşma; `Glass.Headroom` |

Artı borçlar: taze bar 2.25★ tavan (bedava çeyrek yıldız), iade edilen marka gece boyu alınamaz kalıyordu, `CanMake` MinTier bilmiyordu (Vesper kuyu cinle "yapılır" görünüyordu), eşleşmeyen içki kirli bardak bırakmıyordu (en kötü servise bussing indirimi), iki yalancı red metni, kararsız sıralama, üç yerde bayat yıldız-kapısı tablosu. Sim: medyan $176→$169, gerisi düz.

**Ölü kod kolu (`48c39db`)** — ~850KB + ~300 satır: Splasher bütünüyle (her kare kurulup beslenmeyen parçacık sistemi), AddPrepSource (70 satır, tezgâhı 08-10'da terk eden dört küvetin kurucusu), Quality/QualityTier kavramı, SpriteKey (hiç okunmadı, json değerleri var olmayan PNG'leri adlandırıyordu), InstanceId, Tweening'in kullanılmayan coroutine'leri, 43 öksüz PNG (üç eski market kiti), hiç rol almamış walrus karakteri (790KB), TutorialInfo, kaza-kurtarma sahneleri, `dev/null` klasörü. Ders yeniden ödendi: iki blok kesiği yapıya güvenip komşu metod yedi — derleme yakaladı, brace-sayımıyla yeniden (source-edit-safety).

**Düzen kolu (bu commit)** — `Assets/Scripts/UI` 28 düz dosyadan altı alt klasöre (Flow/Hud/Art/Fluid/Behaviours/Layout; asmdef özyinelemeli, namespace'ler değişmedi, git mv .meta'larla); iki editor kökü tek `Scripts/Editor`'da (LastCallImporter Assembly-CSharp-Editor→LastCall.Editor); `Tools/*_raw` gitignore'a. Bilerek DOKUNULMAYAN: `Resources/Items` düz kalır (87 çağrı yeri + json'dan türeyen adlar yol-yüklü — klasörleme her yükü kırar), Data'nın tekli klasörleri (maliyetsiz), Tools betikleri (scratchpad importları kırılır).

### 0.4 · Bu turda yakalanan hatalar

Hepsi ölçümle bulundu; hiçbiri "bakınca fark edildi" değil:

| Hata | Nasıl bulundu | Neden önemli |
|---|---|---|
| **İçki menüsü hiç açılmıyordu** (`e9ca821`) | play'de `IsOpen=False`, konsolda `MissingComponentException` | `AddComponent` **taban sınıf** `RequireComponent`'ını takip etmiyor; `BottleFluid` CanvasRenderer'sız doğup menü inşasını öldürüyordu. Ekranda "sıralama hatası" gibi görünüyor, değil |
| **Kağıt zemini** (`8b4a42b`) | PNG'nin renk profili | ancak ekran görüntüsü gösterebildi; rect testleri geçmişti |
| **Mum tezgahın arkasında** (`5918360`) | ilk kanıt turu | sorting 20 < tezgah 30; tezgah üstü slotlar 35'e alındı |
| **Fikstür PNG'leri düz doku** (`5918360`) | `Resources.Load` null döndü | postprocessor kuralı derlenmeden önce inen dosyalar eski ayarla kalıyor; force reimport gerek |
| **Eşit sıralı iki canvas** (`3000314`) | inceleme turu | plaket ve fallback oda ikisi de −10; eşit sırada çizim düzeni **tanımsız** |
| **Ayna negatif ölçekle** (`671805b`) | geçiş sırasında öngörüldü | ışıklı sprite'ta negatif ölçek sarımı ters çevirir, renderer eler — çıkan müşteri kaybolurdu |

### 0.5 · Denetimin bayatlayan satırları

Aşağıdaki §1–8 satırları bu günlükle **çelişiyor**; düzeltilmeden okunmasın:

| §  | Bayat ifade | Bugünkü gerçek |
|---|---|---|
| §1, §2 | "175 test" | **186** |
| §2 | "Sanat: şişeler düz-sprite; sıvı dış sanatçıda" | denetim **doğru**: sıvı katmanı 2026-08-10'da bir günlüğüne geri geldi (`BottleFluid`, `bb42753`) ve 2026-08-11'de kaldırıldı — düz-dönem sprite'ları opak, arkasına çizilen içki yalnız siluetin dışına taşarak görünüyordu. Şişenin rengi kendi sanatının |
| §4 | "UI ~14.2k satır" | **14.791**; `TycoonHud` 3.4k değil **5.833** |
| §5 | "DiegeticStage emekli döngü ~700 satır" | süpürüldü; dosya yeniden yazıldı (world-space) |
| §7 | "M1 (ana sahne) entegre değil" | **ana sahne artık world-space ve ışıklı**; modüler parça sistemi kurulu |
| §8 P0 | "`BottleArt.cs` bayrağını commit et" | çalışma ağacı temiz |

### 0.6 · Bu turda **kapanmayan** boşluklar

Dürüst liste — hiçbiri "sonra bakarız" diye gizlenmedi:

| Boşluk | Etki | Ölçü |
|---|---|---|
| **Gölge yok** | ışık her şeyin içinden geçiyor; mum tezgahta gölge düşürmüyor | `ShadowCaster2D` sayısı: **0** |
| **Slotlar kodda sabit** | yeni yerleşim noktası kod değişikliği ister — "içerik veridir" kuralıyla çelişir | `DiegeticStage.FixtureSlots`: 7 sabit `Vector2` |
| **Fikstür sanatı placeholder** | prosedürel; PixelLab geçişi dosya-adı birebir yapılabilir | 7 PNG |
| **Sim botu fikstür almıyor** | satın alma yolu botla sınanmıyor (kozmetik oldukları için tabanı bozmuyor) | `TycoonSimulator`'da `BuyFixture`: **0** |
| **UI testsiz** | denetimin §4 borcu; bu tur menü regresyonuyla **bedelini gösterdi** | 14.791 satır, 0 test |
| **Elle yerleştirilen dekor ışık almıyor** | `StageDressing` overlay canvas'ta (−5) | sürükle-bırak katmanı world'e taşınmadı |

### 0.7 · İş kolu — kesilen tablo ve görünmeyen fiil (2026-08-15)

İki P0/P1 kalemi kapandı ve biri beklenmedik bir cevap verdi.

**Tablo 30 güne açıldı** (`TycoonSimulator` `.Take(15)` düştü) ve ikinci bir sütun kazandı:
bir gece iki türlü kırmızı biter — ya hasılat kirayı ve stoğu karşılamamıştır, ya karşılamış
ve bar alışverişe çıkmıştır. Ayrımı `DayResult` zaten taşıyordu (`Rent`/`Stock`/`Upgrades`),
rapor sormuyordu.

**Bulgu — geç oyunda ekonomik sıkışma YOK.** 200 koşu × 30 gün, alışveriş hariç kırmızı gün
sayısı **her gün için 0.0%**. Kırmızı eğrisinin tepeleri (g21 %82, g28 %70) tamamen botun
kendi alışverişi. Denetimin "geç oyun kötüleşiyor" endişesi, ölçülebilir hâle geldiğinde
kendini doğrulamadı: kira bu ufukta hiç ısırmıyor, bar masrafını her gece çıkarıyor, kaybetmenin
tek yolu harcamak. **Uyarı:** bu kusursuz oyun (Exact %100). Hata ekonomisi hâlâ ölçülmedi —
`LastCall → Measure Imperfect Hands` sıradaki P0.

**Kaşık dört yıldıza kadar oyunda yoktu.** Tin kapandığından beri (2026-08-13) yöntemi
**tarif** söylüyor (`MixRequired` → `TinMethod`), en erken `Stirred` tarif ise rank 22'ydi:
her stirred klasik vermut ya da amaro ister, ikisi de 4★'da açılıyor. Yani oyuncu barın
ömrünün çoğunu tezgâhın yarısını hiç görmeden geçirebiliyordu. Black Russian (rank 8, 0★
— iki ağır sıvı, gaz yok, çalkalamak kahve likörünü köpürtür) ve Mint Julep (rank 21) gerçek
yöntemlerine döndü; yeni şişe, yeni fiyat, yeni sayfa yok. Sim çıktısı **birebir aynı** kaldı
(bot yöntemi zaten tariften okuyor), yani bu bir denge değişikliği değil, bir öğretme
değişikliği.

---

### 0.8 · İş kolu — barın sesi (2026-08-27)

**ÖLÇÜM ÖNCE, TEDAVİ SONRA.** Yazar "oyunda sesler mevcut değil" dedi. Oyunda ölçüldü:
`Sound.Effective` **0.00** — sistem çalışıyordu, `PlayerPrefs`'teki mute bayrağı susturuyordu.
Sonra kliplerin kendisi ölçüldü: on üçün **yedisi patlıyordu** (dalga formu sıfırdan uzakta
bitiyor; `click.wav` tam ölçeğin %45'inde), hepsi 22 kHz, birkaçında DC kayması.

**ÇIKTI:** 67 kliplik sentezlenmiş banka (`Tools/sfx_dsp.py` + `sfx_bank.py`), tek mastering
kapısından (`render`) geçiyor — DC süzülür, `tanh` limitlenir, seviye merdivenden atanır,
uçlar sıfıra çekilir ve **sıfır oldukları iddia edilir**. Patlama ihraç edilemez.

**İKİ GERÇEK HATA yol üstünde çıktı:** (1) `Sfx.HoldLoop` ad+seviye alıyordu, yani `_shakeEnergy`
ve `_stirEnergy` her kare hesaplanıp ses katmanında çöpe atılıyordu — emek duyulmuyordu;
(2) `_instance` statiği domain reload'da sıfırlanıp `DontDestroyOnLoad` nesnesi sağ kaldığı
için her yeniden derleme bir `Sfx` kopyası daha bırakıyordu (oyunda 16 AudioSource ölçüldü),
ve öksüz olan kendi ambience yatağını çalmaya devam ediyordu.

**AÇIK KALAN:** envanterdeki 177 aksiyonun ~50'si bağlandı. Bankada duran ama hiçbir yerde
çalmayan klipler için §8'e P1 satırı eklendi.

### 0.9 · Bankanın tamamlanması (2026-08-27, ikinci ses turu)

**66 KLİBİN 66'SI BAĞLI.** Kalan yirmi klip dört kollu bir çapa taramasıyla yerine oturdu.
Ajanların yakaladığı en değerli şey bir ÇAKIŞMAYDI: yıldız iniş satırında duran
`Sfx.Play("key_press")` — aynı günün jenerik-tık süpürmesinden kalmıştı — `star_earn`'ün
YERİNE geçmeliydi, yanına değil; yoksa her yıldız çift vururdu.

**KENAR KORUMALARI, tek tek:** `day_open` perdenin kendi `_curtainT >= CurtainTotal`
kapısıyla zaten bir kez; `day_close` mevcut `_lastPhase` kenarıyla; `last_call_bell`
`_clockWasLast` İKİ YÖNDE de ateşlediği için `if (last)` ile (yoksa ertesi gecenin açılış
karesinde de çalardı); `beer_spill` için yeni bir `_spilledLast` alanı (`SpilledBeer` yalnız
büyüyor, okunacak kenar yok) ve **epsilon dekorasyon değil** — dökülme her kare biraz
artıyor, çıplak bir `>` saniyede altmış kez tetiklerdi; `synth_swell` ve `bar_closed` kendi
yükselen-kenar bayraklarıyla; `id_card_away` kartın gerçekten açık olup olmadığıyla
(`CloseId` on iki yerden koşulsuz çağrılıyor, her servis dahil).

**`hover` İÇİN FREN ODAYA AİT, PROPA DEĞİL.** Odadaki her nesne `HoverGlow` taşıyor, yani
imleci arka bar boyunca süpermek saniyede bir düzine şişe kesiyor. Nesne başına soğuma
süresi bunu çözmezdi (on iki farklı nesne = on iki ses); soğuma **statik**, yani oda bir
bütün olarak ancak bu sıklıkta konuşabiliyor. Klip zaten bankanın en sessizi (−30 dBFS).

**BAĞLANMAYAN TEK KLİP EMEKLİ EDİLDİ:** `rent_line` fatura satırı başına bir vuruş istiyordu
ve öyle bir an yok. Tarifi `sfx_bank.py`'de duruyor (fatura bir gün satır satır yazarsa
bedava geri gelir), wav silindi — yüklenmeyen sanat borçtur, iki gün önce kendi koyduğumuz
kural.

## 1 · Yönetici özeti

Oyunun **çekirdeği sağlam ve derin**: kural katmanı saf, deterministik, 175 testle korunuyor; içki fiziği (dökme/çalkalama/musluk) gerçek; gizli-bilgi mekaniği (kimlik kartı) kodda hakikaten kilitli. Üç gerçek borç alanı var: **(a) ekonomi jilet sırtında ve geç-oyun şekli görünmez** (sim tablosu tam kötüleştiği günde kesiliyor), **(b) UI ~13–14k satır ve sıfır otomatik test**, **(c) doküman-kod makası açılmış** (12 doğrulanmış çelişki) ve sanat programı yarım kararlarla askıda.

## 2 · Sistem sağlık tablosu

| Sistem | Durum | Kanıt | Risk |
|---|---|---|---|
| Core tycoon döngüsü | 🟢 Sağlam | 34 test; faz kapıları her fiilde | düşük |
| İçki yapımı (3 yol) | 🟢 Sağlam | 32+36 test; brim/bira/gazlı redleri Core'da | düşük |
| Tarif eşleme | 🟢 Sağlam | parite testi + her tarif için IdealPour testi | düşük |
| Ekonomi dengesi | 🟠 Kırılgan | net gün ort. **−$0.1**; kasa medyanı $7, p25 −$5 | **yüksek** |
| Yıldız/itibar | 🟡 Çalışıyor | 2.76★ ortalama, memnuniyet %47, fırtına %17 | orta |
| UI (9 ekran) | 🟡 Çalışıyor ama çıplak | 0 test, yapısal olarak test edilemez (asmdef) | **yüksek** |
| Sanat | 🟡 Askıda | şişeler düz-sprite döneminde; sıvı işi dış sanatçıya devredildi; M1/M2 konseptleri geri alındı | orta |
| Dokümantasyon | 🔴 Makas açık | 12 doğrulanmış çelişki (§6) | orta |
| Araçlar/sim | 🟢 Güçlü, kör noktalı | 200 koşu gerçek fiillerle; ama yalnız kusursuz oyun | orta |

## 3 · Denge bulguları (sim: 200 koşu × 30 gün)

| Metrik | Değer | Yorum |
|---|---|---|
| İflas | %0.5 | tavan değil taban — bot kusursuz oynuyor |
| Gün sonu kasa (p25/med/p75) | **−$5 / $7 / $34** | jilet sırtı; bahşiş marjına yaslanmış |
| Gelir vs gider (gün ort.) | $136.3 / $136.4 | **net negatif** |
| Kırmızı gün eğrisi | g2–4 ~%40 → g8–11 ~%0 → g13 %11.5 → g14 %25.5 → **g15 %59.5** | ikinci tepe tırmanırken… |
| **Tablo kesilmesi** | `Report()` `.Take(15)` — ufuk 30 gün | **g16–30 hesaplanıyor ama yazılmıyor**; geç oyun kör |
| Fırtına gidenler | %17.0 | P18 hedefi <%15, hâlâ üstünde |
| Draught payı / köpük bandı | %9.2 / %100 | tek senaryolu çekiş profili — band hassasiyeti ölçülmemiş |
| Verdikt dağılımı | Exact %100, Close 0, Wrong 6/70k, Refused 0 | kusursuz oyun → hata ekonomisi **doğrulanmamış** |

**Ana bulgu:** ekonomi değerlendirmesi yapılamadan önce iki ucuz düzeltme şart — (1) rapor tablosunu 30 güne aç, (2) bota kusurlu-oyun modu ekle (isabet/oran gürültüsü). Mevcut veriler "geç oyun kötüleşiyor" diyor ama kanıt penceresi tam orada kapanıyor.

## 4 · Kalite boşlukları

| Boşluk | Ayrıntı |
|---|---|
| UI testsiz | ~28k satır; Tests asmdef'i UI'ı referans bile almıyor. **Kısmen kapandı:** PlayMode süiti (7 test) sanal fareyle gerçek sahneyi oynuyor — taban, kapsam değil |
| ~~PlayMode/input testi yok~~ ✅ | **kapandı 2026-08-12** — `LastCall.PlayTests`: bar açılır, tabure tıklanır, şişe tezgâha iner, tezgâh döker; ayrıca `LookTests` üç ekranı piksel piksel karşılaştırır |
| Determinizm | yalnız öz-tutarlılık testli; **altın vektör yok** — platform sapması sessiz geçer |
| Kültür pini | `tycoon_speed_response.md` tr-TR formatında işlenmiş ("11,6") — pin kanıtsız |
| Sim başlığı bayat | "marka almaz / bant orta noktası" yazıyor; bot IdealPour kullanıyor ve marka+tarif alıyor |
| Kapsamsız Core köşeleri | Relationships eşikleri (1/3/6), GameBootstrap, RunCulture |

## 5 · Ölü kod / temizlik envanteri

| Alan | Boyut | Not |
|---|---|---|
| ~~DiegeticStage emekli döngü~~ ✅ | ~~700 satır~~ | **süpürüldü** (2026-08-07 ve 2026-08-27 turları) |
| ~~Menu.cs ölü aile~~ ✅ | ~~250 satır~~ | **dosya bütün olarak silindi** 2026-08-22'de back-bar sayfasıyla birlikte |
| ~~Yetim PNG (Items)~~ ✅ | ~~14 dosya~~ → gerçekte **22** | **silindi 2026-08-27** (`2c8fb8d8`); her aday adla VE GUID'le doğrulandı, `register2.png` yalnız GUID'le bağlı çıkıp kurtuldu |
| Gölgelenmiş sanat | 116 v3 plaka (bilinçli rezerv) + 30 bot_* + 20 stil `_open` | yükleme zinciri asla ulaşmıyor |
| Assets/Art fiilen ölü | 21 şişe + vip_patron + pour_nick(+mask) + club_bg | sahneye bağlı ama gizli/ölü yolda |
| DTO ölü alanlar | charges/bands/chargeMultiplier | sökülen duygu katmanının kalıntısı |
| Tekrarlar | NewRect/NewText ×4 sınıf; iki mix-bar ikizi (~55 satır ×2); TycoonHud 3.4k satır tek sınıf | bölünmemiş |
| Veri tuhaflıkları | glassware.json yorumu "3 kademe" der, kod 5 ister; `weight≤0→1` sessiz düzeltme; tequila tek kilitli-T1 hattı | bilinçli mi belgelenmeli |

## 6 · Doküman borcu (doğrulanmış çelişkiler)

| # | Çelişki | Gerçek |
|---|---|---|
| 1 | GDD 19 başlığı "CURRENT", PLAN D1 "duygu motoru gizli sürücü" | duygu katmanı **yok** (2026-08-02 söküldü); memnuniyet doğrudan ServiceJudge |
| 2 | GDD 23: ekstra tur "mood tip" ister | kodda mood tip terimi yok |
| 3 | GDD 23 "26 tarif" | **53 tarif** |
| 4 | "Tepeleme doldur" spec'i GDD23/PLAN'da yaşıyor | `Roll` asla üretmiyor (emekli 2026-08-02) |
| 5 | GDD 24 "bütün-set sanat kuralı" | C10 ile emekli, gerçek akış sahne-başına |
| 6 | GDD 25: 120×280 · yazı yasak · sandviç zorunlu | kod: **80×160 · yazı serbest · düz-sprite dönemi** (`ShowBottleLevels=false`) |
| 7 | PLAN P14 "☐ bardak 3 kademe" | aynı dosyanın eki + kod: **6 kademe, gemide** |
| 8 | PLAN P16 back-bar "◐ sahne sırada" | sahne **kurulu** |
| 9 | PLAN'da 3 kira eğrisi | yalnız `12+2g+g²/9` canlı |
| 10 | `FillPreference` referansları | tip hiç yok |
| 11 | CLAUDE.md "12 (reduced motion)" ve "13 (determinism)" işaretçileri | 12'de içerik yok; 13 aslında 10_technical içi §13 |
| 12 | Bellek "UI chrome asla AI" | yazar yasağı 2026-07-31'de kaldırdı (PLAN kayıtlı) |

**Öneri:** `GDD_MEVCUT.md` yaşayan tek gerçek ilan edilsin; 19/23/25 başlıklarına ve PLAN'ın bayat kutularına tek geçişlik düzeltme yapılsın; CLAUDE.md işaretçileri onarılsın.

## 7 · Sanat programı — askıdaki kararlar

| Konu | Durum |
|---|---|
| Şişeler | Düz-sprite (seçilmiş ham alımlar); dolum göstergesi bayrakla kapalı (**tek uncommitted değişiklik**); katmanlı sıvı işi dış sanatçıya devredilecek |
| Sanat İncili v2 + tercih kayıtları | `Art/pilot/` ve scratchpad'de — **ikisi de git dışında**; kaybolma riski → Docs'a taşınmalı |
| M1 (ana sahne) | konsept alımları üretildi; entegre değil |
| M2 (back bar) | tam entegrasyon yazıldı, **yazar kararıyla bugün geri alındı**; ham alımlar + kod bilgisi duruyor, yeniden giriş ucuz |
| Kamera/stil kilidi | "back bar A" stili + sabit açı yazarca onaylı — sonraki üretimlerin zemini |

## 8 · Önceliklendirilmiş öneriler

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| ~~P0~~ ✅ | ~~Sim tablosunu 30 güne aç + yeniden koştur~~ | **kapandı 2026-08-15** — §0.7; geç oyun görünür ve cevap "sıkışma yok" |
| **P0** | Bota kusurlu-oyun modu (isabet/oran gürültüsü, gecikme) | Close/Wrong/Refused ekonomisi ilk kez ölçülür |
| **P0** | `BottleArt.cs` bayrağını commit et (test yeşiliyle) | çalışma ağacı temizlenir |
| ~~P0~~ ✅ | ~~CLAUDE.md onarımı (UI satır sayısı, modül işaretçileri)~~ | **kapandı 2026-08-27** — `.Menu` parçası (2026-08-22'de silinmişti) mimari bölümünden çıktı, içki alma yeri tezgâhın mahzeni olarak yazıldı, UI satır sayısı 17.5k → 28k |
| **P1** | Ekonomi dengeleme turu (P18) — yeni sim verisiyle | kasa medyanı $7'den yaşanır aralığa |
| **P1** | Doküman borcu tek geçiş (§6 tablosu) + `GDD_MEVCUT` tek-gerçek ilanı | makas kapanır |
| ~~P1~~ ✅ | ~~Ölü kod süpürmesi (DiegeticStage rayı, Menu ailesi, 14 yetim PNG)~~ | **kapandı 2026-08-27** (`2c8fb8d8`) — 3931 satır çıktı, 15 girdi; 22 yetim PNG (14 değil), bitirme masasının 452 satırı, boş `ShakerSolids` tertibatı, yıkılmış sayfanın beş mobilyası. Her aday adla VE GUID'le doğrulandı — `register2.png` yalnız GUID'le bağlıydı, ad taraması onu yetim sanardı |
| **P1** | Determinizm altın vektörleri + kültür pini testi | platform güvencesi gerçek olur |
| **P2** | UI test dikişi (en az PlayMode duman testi: sahne kur, bir gün oynat, input yolu) | "ölü kol" sınıfına ağ |
| **P2** | TycoonHud'u parçalara böl (Flow'un partial deseni) | 3.4k satırlık tek sınıf dağılır |
| **P2** | Sanat programına dönüş: İncil + tercihler Docs'a, M2 yeniden girişi, M1 konsepti | askıdaki hat kapanır |
| **P2** | Tutorial/FTUE + kayıt sistemi (P18 devri) | yeni oyuncu ve oturum sürekliliği |
| ~~P1~~ ✅ | ~~Bankada duran ama çalmayan klipleri bağla~~ | **kapandı 2026-08-27** — 20'sinin 19'u bağlandı, banka **66/66 bağlı**. `rent_line` EMEKLİ EDİLDİ: fatura satırı diye bir an yok, `RebuildDayEnd` üç maliyet satırını tek sessiz geçişte kuruyor — ona ev vermek stagger'ı İNŞA ETMEK olurdu, ki o özellik, ses turu değil |
| **P2** | PlayMode'un ilk-koşu sahte kırmızısını teşhis et | Süit her oturumda 1-2 kez kırmızı verip tekrarda yeşil dönüyor. **BU HAYALET GİRDİ DEĞİL:** iki tanılama da işaretçinin hedefe ULAŞTIĞINI gösteriyor (`under=[Seat1]`, `under=[BillNext@22]`, `key active True`) — tıklama iletiliyor ama işlenmiyor. Şüphe: soğuk ilk koşuda `WaitForSecondsRealtime` geçiyor ama çok az KARE dönüyor (import/shader ısınması kareyi ~1ms olmaktan çıkarıyor), yani `Update`'le sürülen animasyon ilerlemiyor. Ölçülmeden dokunulmamalı |
| — | ~~Teardown'a otomatik hayalet-girdi temizliği~~ | **ÖNERİLMEZ:** `GhostInputGuard` bilerek menü öğesi ve gerekçesi kendi belgesinde ölçülü (2026-08-13): editörde gerçek fare de non-native görünüyor, her play'de ateşlenen bir süpürme oyuncunun kendi imlecini alır. Ayrıca süitin `TearDown`'ı zaten kendi sanal faresini açıkça kaldırıyor |

### 8.1 · Işık/sahne turundan çıkan yeni öneriler (2026-08-10)

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| **P1** | `ShadowCaster2D`: tezgah + fikstürler | ışık şu an her şeyin içinden geçiyor; gölge, sistemi "dekor" olmaktan çıkaran adım |
| **P1** | Fikstür slotlarını `fixtures.json`'a taşı | yeni yerleşim noktası kod değil içerik olur; §5'teki "içerik veridir" kuralına döner |
| **P1** | PlayMode duman testi: sahneyi kur, menüyü aç, bir gün oynat | menü regresyonu (`e9ca821`) tam olarak bu ağın yokluğunda geçti — testler yeşildi |
| **P2** | `StageDressing` katmanını world-space'e al | elle yerleştirilen dekor da ışık alsın; şu an odanın tek ışıksız parçası |
| **P2** | Fikstür sanatının PixelLab turu (rapor-önce) | placeholder'lar dosya-adı birebir değiştirilebilir; kod dokunulmaz |
| **P2** | Bota fikstür alımı öğret | kozmetik oldukları için tabanı bozmaz ama satın alma yolu sınanır |

### 8.2 · Son müşteri planından çıkan bulgular (2026-08-12)

`GDD 26` + `PLAN_last_call.md` yazılırken veriye bakınca çıkan, hikâyeden bağımsız üç iş:

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| **P0** | Kadro evrakını (`PatronPapers`, 30 satır) `TycoonHud`'dan `customers/papers.json`'a taşı | "içerik veridir" kuralını ihlal ediyor; hikâye karakterleri aynı tabloyu paylaşacak, yazarın C#'a dokunması gerekmemeli (PLAN S0) |
| ~~P1~~ ✅ | ~~Tarif merdivenine **erken bir stirred içki** ekle~~ | **kapandı 2026-08-15** — Black Russian (rank 8, 0★) ve Mint Julep (rank 21) Built→Stirred; yeni bant testi `TheFirstRung_TeachesEveryVerbTheBenchAsksFor` geri düşmeyi engelliyor |
| **P2** | Gecenin bitişi tek cümlede kalsın | `Floor.IsComplete` bugün TEK yerden okunuyor (`TycoonRun:688`) — son müşteri kapanıştan SONRA oturacağı için bu koşul "misafir de gittiyse" hâline gelmeli; ikinci bir okuyucu doğarsa beat sessizce kaybolur |

### 8.3 · Ev ve kapı planından çıkan işler (2026-09-04)

`GDD 27` (mekân: iki puan, temizlik, merdiven) + `GDD 28` (kapı: 20 yaş, ödünç kimlik, kick) +
`PLAN_house_and_law.md` yazılırken koda bakınca çıkan, planın kendisinden bağımsız işler:

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| **P0** | Paylaşılan ağaçtaki sahipsiz Core değişiklikleri (tek saat sabrı, meşrubat fiyatı, TV: `CustomerVisit`, `TycoonConfig`, `DataLoader`, `FixtureDefinition`, `fixtures.json`) commit'lensin | H1b/H2b kablolaması bu dosyalara girer; sahipsiz yarım iş üstüne hunk stage etmek "test edemediğin parçayı commit'lemek" demek (PLAN §çalışma koşulları) |
| **P0** | `PlayDayServingEveryone` test yardımcısı temizlik yapsın (topla → sil → yıka) | kirli bardak artık kendini temizlemiyor; yardımcı temizlemezse her çivili yıldız sayısı kirli barın sayısı olur |
| **P1** | Reddedilen sipariş (`DeclineOrder`) GÖRÜNMEZ bir Core bardağı bırakıyor ve tabureyi 7 sn kilitliyor (`BarDay.Tick`, `State != StormedOff`) | hata; H1b'de "dökülmeyen içki iz bırakmaz" ile kapanır (PLAN C6) |
| **P1** | Fiş ham oda yıldızı, hafta tahtası KIRPILMIŞ gece, defter `NightStars` basıyor — üç yüzey üç sayı | GDD 27 D7 bilinçli tutuyor; ayakta duran tahta min'i açıklamalı, yoksa oyuncu "4.9 yazdı, 2.0 girdi" der |
| **P1** | `TycoonHud.DayEnd` dressing koridoru "bir basamak ileri" kuralını yalnız HUD'da taşıyor, Core'da/testte pin yok | H1b'de `VisibleRung(slot)` sorgusu ya da katalog testi |
| **P2** | Sim botu fikstür almıyor (§8.1 P2 hâlâ açık) ve `fixtures.json`'ı hiç yüklemiyor | konfor tabanı fikstürden gelince bot sonsuza dek taban konforda kalır; H1b botu döşeme alıp temizlik yapmayı öğreniyor |
| **P2** | `GDD 23 §7/§8`, `GDD_MEVCUT §7`, `BALANCE.md` düzyazısı hâlâ `1 + 4x` ve silinmiş "arka duvar / müzisyen" satırlarını taşıyor | kod 5x (2026-08-11); H1b'nin doküman geçişinde silinir |
| **P2** | `DiegeticStage.LoadScreenFrames` hücre boyu TV'ye sabit (45×45) | lavabo suyu ikinci kare-sayfası; kesici json'dan hücre okumalı (H4) |
