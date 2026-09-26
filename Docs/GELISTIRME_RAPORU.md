# LAST CALL — GELİŞTİRME RAPORU

**Denetim tarihi:** 2026-08-07 · **Günlük son güncelleme:** 2026-09-26 (eksik listesi §0.0)
**Yöntem:** 8 kollu kod denetimi (dosya:satır kanıtlı) + sim raporu + doküman-kod karşılaştırması
**Eş belge:** `Docs/GDD_MEVCUT.md` (oyunun bugünkü kuralları)

> Bu belge iki şey taşıyor: **§0 geliştirme günlüğü** (ne yapıldı, neden, hangi ölçümle) ve
> **§1–8 duran denetim** (sistem sağlığı + öncelikli borç listesi). Denetim 2026-08-07'de
> yazıldı; günlük ondan sonrasını sürüyor ve denetimin bayatlayan satırlarını §0.5'te
> tek tek işaretliyor. Bir sayı iki yerde çelişirse **günlük doğrudur** — o ölçülmüş,
> denetim hatırlanmış olabilir.

---

## 0.0 · EKSİK LİSTESİ — 2026-09-26 (yazar: "teknik, donanım ve içerik olarak tamamlayalım")

*İki okuma ajanının dosya:satır kanıtlı taramasından (HEAD `32d2bf56`). Eş belge:
`Docs/PROJE_HARITASI.md` (dosya yollarıyla proje düzeni). Sıralama kabaca demo yoluna göre;
"KARAR" = yazarın seçimi gerekiyor. Aşağıdaki 08-07 denetiminin bayatlamış satırları için bu
bölüm kazanır (483 test → bugün 789+15; kayıt sistemi §9.123 ile geldi).*

### A · TEKNİK

1. **Derleme hattı yok:** `BuildPipeline` kullanımı yok, `.github/` yok, derleme betiği yok.
   Demo öncesi: Windows build betiği (Editor menüsü ya da `-batchmode`), sürüm damgası, çıktı klasörü.
2. **Demo yapılandırması yok:** demo define'ı, gece sınırı, teşekkür/istek-listesi ekranı yok
   (plan: `Docs/marketing/LAUNCH_READINESS.md:35-50`). `FirstWeek` yalnız içerik (`FirstWeek.cs:43`).
3. **Dev yüzeyleri Development Build'de görünür:** DEV TOOLS + dev tezgâh `#if UNITY_EDITOR ||
   DEVELOPMENT_BUILD` (`TycoonHud.Settings.cs:314`, `TycoonHud.Chrome.cs:2192`); `Dev*` fiilleri
   Core API'sinde kalıyor (preset/skip/jump/forcelastcall/fit). İnceleme/demoya dev build verme.
4. **Çökme/telemetri kancası yok** (`enableCrashReportAPI: 0`, log yakalayıcı yok).
5. **Steam entegrasyonu yok:** Steamworks paketi yok, `steam_appid.txt` yok, başarım yok, Steam
   Cloud işareti yok; dil OS'ten (`Languages.FromSteam` beslenmiyor, `Localization.cs:44`).
   Kayıt `persistentDataPath/saves/run.json` — Auto-Cloud yolu EA'den önce yazılmalı.
6. **Yazılı son müşteri sahnede KAPALI:** `TycoonConfig.ForTheScene` `lastCall: false`
   (`TycoonConfig.cs:51-58`); ark yalnız `DevForceLastCall` ile oynuyor. `storyInPlay` alanı
   "S3 ile sil" notuna rağmen duruyor (`GameBootstrap.cs:35-44`). KARAR: demoda açık mı?
7. **Denge:** 200-koşu simi hâlâ 200/200 iflas (medyan 22. gün, `tycoon_sim_report.md:13-19`);
   projeksiyonda öğrenen bar 1.7★'da takılı; oda fiyatı ×0.35 kolu KARAR bekliyor
   (`GDD_MEVCUT` 9.122). P18'in dört kutusu açık (`PLAN_service_depth.md:756-813`), döküm
   hassasiyeti neredeyse fark yaratmıyor (%18 hata → %99.9 Exact, `imperfect_hands_report.md`).
8. **Kayıt sınırları (bilerek):** tek yuva, yalnız şafakta; sürüm/veri kayması TÜM kaydı reddeder
   (göç yok); `companyName` hâlâ DefaultCompany — EA'den önce BİR KEZ "LasGen Interactive"e
   çevrilmeli (kayıt klasörü + PlayerPrefs taşınır).
9. **PlayMode ilk-koşu yalancı kırmızısı** hâlâ teşhissiz (§0.5); UI 59k satıra 15 PlayMode testi.
10. **Depo hijyeni:** mağaza/pazarlama hattı git dışı (PROJE_HARITASI §6.1 — kayıp riski),
    ~1.300 aday/çıktı `??` sızıntısı (§6.2), sevk girdisi 4 dosya izlenmiyor (`room7_ship.py`).

### B · DONANIM / PLATFORM

1. **Gamepad/kumanda kodu SIFIR:** 14 dosyada 46 `Mouse.current/Keyboard.current`; şablon
   `InputSystem_Actions.inputactions` kayıtlı ama kullanılmıyor. Steam Deck incelemesi kumanda
   ister (`steamworks-rules_2026-09-26.json:136`); EA cevabı "planlanıyor" diyor.
2. **Steam Deck:** 1280×800'de en küçük yazı ~8.9 px (sınır 9); doğrulama koşusu yok.
3. **En-boy:** 16:10/ultrawide/Deck ölçülmedi (kırpma kuralı `DesignFrame.cs:21-31`);
   1366×768 masaüstü HİÇBİR pencere boyu alamıyor (`DisplayOptions.cs:40-55`); yalnız borderless.
4. **Standalone iskeleti:** backend MONO (IL2CPP yok → stripEngineCode etkisiz), grafik API
   otomatik, `defaultScreenWidth 1024×768`, exe ikonu boş, Unity splash açık, Mac ikonu TODO.
5. **Min-spec ölçülmedi:** sayfa taslağı "Win10 64-bit, 4 GB, DX11" — ölçümle doğrulanacak
   (`PAGE_SETUP.md:26`). Düşük donanım testi kaydı yok.
6. **Erişilebilirlik — var:** MOTION, FLASHES, POINTER (2x), COLOUR CUES, PAUSE WHEN AWAY,
   INVERT POUR, 7 tuş yeniden bağlama, ses×3. **Yok (KARAR):** CARRYING (tıkla-taşı),
   SMALL PRINT (8→16), LEFT-HANDED; UI ölçeği; bas-tut hareketlerine alternatif.

### C · İÇERİK

1. **Kadro:** 41 yüz çizili / kadroda 90 (49 "planned"); EA metni "yüz kişi" diyor. Yüz kilidi
   ilerlemesi yok (her yıldız kapısı 0). Eski rig listesi yeniden çizim bekliyor.
2. **Hikâye:** Ece'nin kendi yüzü yok (`placeholderLook: silkwoman`); S6'nın üç konuğu yüzsüz
   (~90 üretim/yüz, `PLAN_last_call.md:376-387`); yazılı arkın ilk `unlockBeat`'i yazılmadı.
3. **Oda sanatı:** lavabo 3. basamak (mermer reddedildi) yeni çizim bekliyor; "+20 ONLY" tabelası
   sağ duvar örtüsünün altında; kola/votka gri-mor okunuyor; TV reklam 2. seti üretilmedi
   (`tv_ads2/` boş); ana menüde logo yok (yalnız yazı — logo `steam_kit/deliver/logos/`ta).
4. **Ses:** 14 şarkı var; ana menü teması YOK; 5 sfx eksik (`SES_LISTESI.md:217`: till_open,
   neon_buzz, fixture_install, paper_stamp_fail, ui_error); OST albümü bestelenmedi
   (master ~13 Eki, `DLC_PLAN.md:52`).
5. **Lokalizasyon:** dil başına 224 çevrilmemiş anahtar (tr: 39) — en büyük öbekler rank (51),
   id (35), data (27), book.trait (26); L2 taşma taraması (9 Eki), L3 font, L4 ekran kontrolü
   (16 Eki) açık; "host→bartender" mağaza düzeltmesi 8 dilde bekliyor; ölü anahtarlar
   (`data.snack.*` ×4 + 2 kural + `chrome.pause.save`) 29 tabloda taşınıyor.
6. **Mağaza varlıkları:** kapsüller/ikonlar/29 dil metni HAZIR; **oyun içi ekran görüntüsü (≥5),
   fragman, GIF'ler, demo kapsülleri YOK** — mağaza sayfası incelemesinin blokajı
   (`PAGE_SETUP.md:81`). Content Survey + AI beyanı YALNIZ YAZAR.
7. **Takvim** (`LAUNCH_READINESS.md:95-108`): 26-28 Eyl app oluştur + INDIE Live Expo başvurusu
   (paket hazır: `marketing/events/INDIE_LIVE_EXPO.md`, son gün 29 Eyl); 29 Eyl-3 Eki ekran
   görüntüleri; ~3-6 Eki sayfa incelemeye; 9 Eki Kapı 1; 22 Eki Kapı 2 (sürüm+demo+fragman);
   5 Kas EA.

### D · OPTİMİZASYON / GEREKSİZ YÜK (demo öncesi "yük atma" listesi)

*2026-09-26 akşamı inenler (GDD §9.124): kadro tembel yüklemeye geçti (4.384 kare → 41 +
ısınma; açılış 36 ms), 3'teki ölü sanat silindi (steam_kit'in okuduğu üçlü hariç),
window_cycle Tools'a taşındı, 6'daki şablon artıkları + collab-proxy söküldü, pencere
varsayılanı 1280×720. Kalanlar: fontlar/loc bölme, isReadable taraması, unity-mcp'nin
demo derlemesinden çıkarılması, ambience/story ses kararları.*

1. Resources ~139 MB'ın tamamı sevk ediliyor: Audio 56.6 + Patron 39.1 (4.384 gevşek PNG,
   SpriteAtlas yok, açılışta HEPSİ yüklenir) + Fonts 35 (CJK 3×7 MB her sürüme biner) + loc 7.
2. 5.193 PNG'de `isReadable: 1` (CPU kopyası) — patronlarda bilerek (alfa isabeti), kalanında gözden geçir.
3. Ölü sanat adayları (GUID taramalı liste: PROJE_HARITASI §6.3) ≈ 600 KB + 39 dosya.
4. Paketler: `com.coplaydev.unity-mcp` OYUNCU derlemesine runtime asm sokuyor; `collab-proxy`
   gereksiz; modül seti (terrain/vehicles/xr…) Mono'da traşlanmıyor.
5. Nadir/ölü ses: `ambience_loop.wav` (2.8 MB, yalnız yedek), `music_story_1.ogg` (lastCall
   kapalıyken erişilmez).
6. Şablon artıkları + `_Recovery`/`InitTestScene*` + kök ölü csproj'lar + `dev/null/` +
   `patron_sheet_frames/` (20.5 MB kopya).

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

### 0.10 · Tek oturuma dönüş — yarım kalanların kapanışı ve süpürme (2026-09-05)

Yazar: *"Tek oturumdan devam edeceğiz çok karmaşa yaşandı. Yarım kalan işleri tespit et, yarım
kalan işleri bitir. Mevcut projede gereksiz, eski, hatalı sürümleri kaldıralım."* Dört paralel
oturumun ağaçta bıraktığı 215 dosyalık kirli çalışma alanı hunk hunk sınıflandırıldı (kim, ne,
bitmiş mi) ve dokuz commit hâlinde `main`'e indi — her biri aynı doğrulanmış ağaçtan, geçici
index'le (paylaşılan index'e dokunmadan) dilimlenerek:

| Commit | Ne | Sahibi (oturum) |
|---|---|---|
| `6cbe1f7b` | tek sabır saati; sipariş almak bir kutu öder (§9.22) | 0c7527bb (bitmiş, commit'siz) |
| `b136a9c8` | kokteyl yapılırken musluk kilitli | 6c |
| `6673cb47` | **H1b** — konfor puanı ve tezgâhın gecesi kablolandı (§9.23) | bu oturum |
| `ae732239` | tezgâhın tek ayak çizgisi, gövdeli bardak altlığı, `ItemArt.OpaqueBounds` | cdfbf9b7 + db |
| `3d48d683` | fişte çizili $ ve yıldız, haftanın cirosu | e97395d4 |
| `7ba2f53a` | 25 PNG — odanın sanatı olduğu gibi | yazar + eski turlar |
| `2378c708` | tezgâh yeniden: kayrak, shaker-gösterge, peçete, ÇÖP tuşu, kapağı açık döküm | db (yazarın yönetiminde) |
| `d468216d` | süpürme (aşağıda) | bu oturum |
| `6c40b5cb` | **H2b** — kapı kablolandı: evrak, kick, ceza, teşekkür (§9.24) | bu oturum |
| `5820f8e3` | **H3** — KICK tuşu kartta, ödünç yüz, fişte CEZA/TEŞEKKÜR | bu oturum |
| `ce9365b0` | **H4** — bardak lavaboya taşınır, bez lekeyi siler, musluk suyu; sahne lekeleri öder | bu oturum |
| `479b301c` | **H5** — kalp/madalyon şeritleri, fişte ev satırı, tahtada SERVICE/COMFORT | bu oturum |
| `9b3bd33b` | **H6 (kod)** — değiştirilmiş kart: yanlış bayrak; sanat yazarın seçimini bekliyor | bu oturum |
| `e0744f16` | süpürmenin ikinci yarısı — 190 dosya (yazar manifesti çalıştırdı) | yazar + bu oturum |
| `dd7a7fdf` | yazarın sanat turu — arka duvar, musluk kulesi, kutu etiketi | yazar |
| `5948a965` | **S5** — ev sahibinin dersleri konuşur, kitapta açık hesap (§9.25) | bu oturum |
| `7c188737` | yazarın ikinci v4 turu — 38 plaka | yazar |
| `2608fd6e` | **duvar merdiveni** — yazarın dört oda plakası, `backdrop` yuvası, +20 tabela (§9.26) | yazar + bu oturum |
| `f37db211` | **geliştirme ekranı** — raflar, alınanlar gizli, ikonlar; oda çıplak açılır, dört eşya satın alınır (§9.27) | bu oturum |
| `5a8bdd1d` | **shaker** — yazarın iki tini, tezgâhta duran shaker ve tezgâha dönüş, altın basamak (§9.28) | yazar + bu oturum |
| `8168efb6` | tezgâhın eksikleri — iki kâse, iki paspas, mahzen/oda kayması, elsiz bench, şişe yolu (§9.29) | yazar + bu oturum |
| `8db73970` | **imleç cevabı** — yükselme, büyüme, salınım, hale (§9.29, GDD 16) | bu oturum |
| `fb09cf3c` | **lavabo kuyruğu** — bardak yıkanana kadar tabureyi tutar; yazarın iki su animasyonu (§9.29) | yazar + bu oturum |
| `a4deabd9` | **imleç cevabı düzeltmesi** — çizimin sınırında hale, öne çıkma, sallanma, mahzenin şişeleri (§9.30) | bu oturum |
| `b51dc655` | **tin'in lavabosu, tek beden tin, yavaş geçiş** — taşınan tin, 232×416, 0.42s fren (§9.30) | yazar + bu oturum |
| `6dc61d6c` | **ışığın şekli, çağıran lavabo, ovulan kir** — silüetten büyüyen parlama, Beckon, piksel piksel silinen işaret (§9.31) | yazar + bu oturum |
| `1283ad2a` | **kenar ışığı, çelik tin, bardak altlıkları, ovulan kir, orantılı bardaklar** (§9.32) | yazar + bu oturum |
| `0eb68ba0` | bardak altlığına oturdu, altlıklar kaldırıldı, lavabo 5 sn + saat (§9.33) | yazar + bu oturum |
| `a397bef8` | **üç yudum, bulut baloncuk** — sıralı cümleler, daktilo, bulut şekli (GDD 24) | bu oturum |
| `d05b6d75` | **Ece = haftalık görev** — üç tür, ödül, LOG şeridi, ikonlu bildirim (§9.34) | yazar + bu oturum |
| `3016fc2f` | **tarif sayfası** — beş nokta, büyük görsel, öne çıkan ücret, 16px (GDD 16 §0e) | yazar + bu oturum |
| `d2c5728f` | kartonlar bench'e döndü, çizgiroman baloncuğu, lavabo sessiz, neon/TV taşındı | yazar + bu oturum |
| `f85fd727` | **kimlik yeniden** — çizilen kâğıt, tek ızgara, %20 daha kompakt (GDD 28) | yazar + bu oturum |
| `b09e7fef` | **altıncı liste** — pasta bekleme, korunan tutuş, ışıklı mat, **konfor 0'dan** (duvarlar +1.25/+2.25/+3.25, $40'lık ilk basamak), kimlik v3, kiriş + ipuçları, ayar penceresi, Türkçe tezgâh, kalın bardak seti (§9.35) | yazar + bu oturum |
| `2b0fc656` | **yedinci liste** — tutuş/ağız/lavaboya giren bardak, mat fikstür, altın madalyon, kirişte segment rakamı + sayılı ipuçları, kimlik v3.1, yuvarlak balon + ayrılma, görev plakası, market tek boy + iki duvar rafı + madalyonlu meta + sandık, **yıldız ekonomisi** (iflas %34→%9), **PixelLab 3D bardak seti** + oyuk kesimi + jant tanesi/yarım dilim (§9.36) | yazar + bu oturum |
| `ec254241` | **sekizinci liste** — mahzen kartı + sallanan sıvı, maske dışı gösterge etiketleri (+ yazarın PNG kancası), kitap satırı, rayla uzayan mat, elips oyuk + büyük bardak, **kimlik v4 2:1** (zımba/yıldız şeridi) (§9.37) | yazar + bu oturum |
| `e9bc1d20` | **dokuzuncu liste** — Malibu Club kimliği (plaj şeridi, yuvarlak köşe), gecenin kirişi, mahzen kartı v2, **tek tin = tek porsiyon** (nişan dökmez), 13 sesli müşteri defteri, ortalanmış bardak + yay taban + akış hissi, 6 basamaklı bardak giydirmesi, raydaki havlu, mürekkepli kir/halka, kitap satırı (§9.38) | yazar + bu oturum |
| `c18334ec` | **onuncu liste** — on yeni düşük profilli müşteri (11'er klip, kişi başına farklı sinirlenme, siyah kontursuz `patron_ink`), leopar yeniden mürekkeplendi, market kartı yeniden, koridor yerinde, balonlar yığılır/kaymaz, konuşma 12 kps, mahzen aile etiketleri, şeffaf havlu, canlı sıvılar (§9.39) | yazar + bu oturum |
| `54927186` | **on birinci liste** — beş genç Japon müşteri (Tokyo Drift kızı dahil, pastelman dili, `japanese` sesi, `fl_jp`), kapı sırası oturum başına rastgele, mat Full Rect, çıkış balonu ölçüldü (§9.40) | yazar + bu oturum |

**Süpürme, ölçerek.** `Assets/Resources` altındaki her PNG oyunun kendi yükleme zincirine karşı
ada, türetilmiş öneke VE sahne GUID'ine göre sınandı (`art_reach.py`): 41 kartın 39'unda tam v4
sandviç var (iki garnitür kartı hariç — onların resmi tezgâhtaki kâse oldu); `bot_*` meşrubat
çekimleri, v2 stil şişeleri (`vodka.png` …), `ice/prep_lemon/pint`, `bench_mini_*`, `btn_bin`,
`sign_open`, sekiz arketip portresi (her yüzün kendi fotoğrafı var), `Pending~` klasörü ve
`register2` gitti; `ItemArt.Bottle` v4 → garnitür kâsesi → null'a indi, `Bottle(style)` ve
`ItemArt.Prep` silindi, kitap ve tarif kartları raftaki olmayan stili katalogdan çiziyor
(`ItemArt.StyleBottle`). Sahne koddan yeniden kuruldu (portre alanı düştü). **İkinci yarı (`e0744f16`,
yazar manifesti çalıştırdı):** 57 `v3_*_flat` plakası, `lager/pale_ale/stout.png`, `fx_fern`, dört kâse
yedeği, 37 emekli Tools betiği ve durum dosyası, Antigravity dönemi `Tools/AssetPipeline` sunucusu,
`Docs/PLAN_tycoon_pivot.md` (PLAN_service_depth'e emilmişti), `_Recovery`/`InitTestScene*`
kalıntıları — 190 dosya. İki düzeltme: `Tools/patron_trial_gen.py` manifestteydi ve KALDI (kadro
üretiminin roll/adopt/judge aracı, `patron_ship.py` onu import ediyor); clubgirl/heavyset altındaki
boş `drink_long/short/stem` klasörleri metalarıyla gitti. Silmeden sonra EditMode 460/460, PlayMode 10/10.

**Ağaçtaki PNG'ler (`dd7a7fdf`):** yazar `v4_energy_volt.png`'nin arka planını kendi temizledi ve
etiketi yeniden yazdı; arka duvarı (harlequin kâğıt, lambri, kapıda +21 ONLY), tezgâh musluk kulesini
ve dört mahzen plakasını elden geçirdi — on resim yazarın sanat turu olarak commit edildi. Beş PNG
piksel-özdeş yeniden kayıttı, HEAD'e döndürüldü (piksel doğrulamasıyla, `git checkout` kullanmadan).

**Doğrulama:** EditMode 452/452 (kapı ile 460/460), PlayMode 10/10 (tezgâh baseline'ı yeni
görünümle yeniden kutsandı — resme bakılarak), 200 koşu sim + dört ev şekli (§9.23), kapı simi
(§9.24). Her faz oyun içinde fotoğraflandı (kart + KICK, bardak/leke/su/silme, şeritler ve tahtalar).

**Aynı gün kapanan plan fazları:** PLAN_house_and_law H1b, H2b, H3, H4, H5 ve H6'nın kod yarısı —
GDD 27 ve 28 artık oyunda. **Kalan:** H6'nın sanatı (resim basamakları 2–3, mermer lavabo —
yazarın rapordan seçimi), PLAN_last_call S6 (kadro içeriği — üç misafirin yüzü heavyset rig'inde çizilince; Ece'nin yüzü
kadroya alınıyor, üç aday yazarın seçiminde), PLAN_service_depth P18 (ekonomi/tutorial/kayıt).

### 0.10 · Simülasyon çöktü: merdiven + gün bazlı talep = 21. günde iflas (2026-09-21)

`LastCall → Simulate Tycoon 200 Runs` 2026-09-06'dan beri ilk kez koşuldu (`Docs/tycoon_sim_report.md`). Bot bir
TABAN'dır (kartı okur, kural ile alışveriş yapar); mutlak sayı değil ŞEKİL okunur — ve şekil bozuk:

| | 2026-09-06 | 2026-09-21 |
|---|---|---|
| İflas | 18 (%9) | **200 (%100)**, medyan 21. gün |
| Gün başına gelir / gider | $232 / $224 | $102 / $114 |
| Servis başına taban | $10,85 | $5,46 |
| Reddedilen / geri çevrilen sipariş | 4 / 736 | 156 / 4476 |
| Ortalama gece yıldızı | 2,13 | 1,29 |
| Konfor (gece ort.) | 2,44 | 1,36; gecelerin %97'si konfora bağlı |
| Basamak: 1,0★'a ulaşan | — | %99,5 (medyan 15. gün); 1,5★ **%3**; 2,0★ ve üstü **hiç** |

Okuma: merdiven (4a4c560e, 26e33ae1) şişeleri, tarifleri ve dekor basamaklarını yıldıza bağladı; gece
`min(servis, konfor)` dosyalıyor; bot yalnız `walls_2`'yi alabiliyor (200 koşuda 200 duvar, başka basamak yok),
konfor 1,5'te kalıyor, yıldız 1,0'da takılıyor, 1★ rafının içkileri ucuz ($5,46) — ve "alışverişten önce kırmızı"
sütunu 11. günden itibaren tırmanıyor (%0 → 19. günde %90): kalabalığın istekleri GÜNLE büyürken raf YILDIZLA
kilitli, "rafın cevaplayamadığı kademe talebi" 63'ün 26'sı, geri çevrilen sipariş altı kat. Yani talep botun
ALAMAYACAĞI şişeyi çekiyor: ölü bir çekiş.

Bu bir tasarım kararı, yazarın: (a) kalabalık yalnız barın basamağında AÇIK olanı istesin (`DrinkOrder.Roll`
havuzunu `Market.GateOf ≤ ShopStars` ile süz; garnitür süzgeci zaten var) — talep merdiveni izler; ya da (b)
kademe talebi günle değil yıldızla yükselsin; ya da (c) ilk basamakların eşiği/fiyatı düşsün (1,0★'da $130'luk
`walls_3` botun kasasının üstünde). (a) en küçük ve Core'da. Oyuncu botun iki katı kazanır (bahşiş, okuma), ama
1,5★'a 200 koşudan 6'sının ulaşması, 1,5★'a yazılmış konuğun oyuncuya da geç geleceğini söylüyor.

## 1 · Yönetici özeti

*(2026-09-06 yenilemesi.)* Oyunun **çekirdeği sağlam ve derin**: kural katmanı saf, deterministik (altın vektörlerle pinli), **483 EditMode testiyle** korunuyor; içki fiziği (dökme/çalkalama/musluk) gerçek; gizli-bilgi mekaniği (kimlik kartı) kodda hakikaten kilitli; ev (iki puan, temizlik, merdivenler — duvar dahil) ve kapı (20 yaş, ödünç/değiştirilmiş kart, kick) oyunda; hikâyenin ev sahibi konuşuyor. Kalan borç: **(a) ekonomi 30 günde yaşıyor ama uzun ufukta kira gelir tavanını 31. gecede kesiyor** (GDD 26 §12.2 — sonlu koşu mu, büyüme mi: yazarın kararı, GDD 23), **(b) UI 28k satır, 10 PlayMode testi bir tabandır, kapsam değil**, **(c) hikâye kadrosu yüz bekliyor** (PixelLab kredisi, S6).

## 2 · Sistem sağlık tablosu

| Sistem | Durum | Kanıt | Risk |
|---|---|---|---|
| Core tycoon döngüsü | 🟢 Sağlam | 483 EditMode testi; faz kapıları her fiilde; altın vektörler (`CoreCornersTests`) | düşük |
| İçki yapımı (3 yol) | 🟢 Sağlam | brim/bira/gazlı/karışım redleri Core'da | düşük |
| Tarif eşleme | 🟢 Sağlam | parite testi + her tarif için IdealPour testi | düşük |
| Ekonomi dengesi | 🟡 30 günde yaşıyor | iflas %1.0, kasa medyanı $80, gelir/gider $133.6/$131.4 (200 koşu, 2026-09-06); uzun ufukta 31. gece duvarı (GDD 26 §12.2) | orta |
| Yıldız/itibar | 🟡 Çalışıyor | 2.72★ ortalama, memnuniyet %60, fırtına %15.4; servis/konfor 2.98/3.11 | orta |
| UI (9 ekran) | 🟡 Taban var | 10 PlayMode testi (sanal fare + üç piksel baseline); 28k satırın gerisi gözle | orta |
| Sanat | 🟢 Yazarın elinde | v4 şişe sandviçi, dört oda plakası, tezgâh — sahne/prop sanatı yazarın; benim payım UI + karakter/animasyon | düşük |
| Dokümantasyon | 🟢 Tek gerçek GDD_MEVCUT | §6 tablosu 2026-09-06'da kapandı; PLAN kutuları kapatıldı | düşük |
| Araçlar/sim | 🟢 Güçlü | 200 koşu + ev/kapı/kusurlu-el ölçümleri; bot alışveriş yapıyor | düşük |

## 3 · Denge bulguları (sim: 200 koşu × 30 gün)

| Metrik | Değer | Yorum |
|---|---|---|
*(2026-09-06, `Docs/tycoon_sim_report.md` — 200 koşu × 30 gün, bot alışveriş yapıyor.)*

| İflas | %1.0 | taban — bot kusursuz döküyor ama kural kadar alıyor |
| Gün sonu kasa (p25/med/p75) | **$68 / $80 / $88** | yaşanır ama dar; kira 30. günde $136 |
| Gelir vs gider (gün ort.) | $133.6 / $131.4 | net +$2 |
| Fırtına gidenler | %15.4 | P18 hedefi <%15'in kıyısında |
| Draught payı / köpük bandı | %8.2 / %100 | leaned-then-straightened çekiş |
| Verdikt dağılımı | Exact %100, Close 29, Wrong 0 | kusurlu el ayrı raporda (`imperfect_hands_report.md`) |
| Servis / konfor (gece ort.) | 2.98 / 3.11 | konforun geceyi tuttuğu geceler %40.6 |
| Basamak alımı (yuvaya göre) | counter_end 198 · plant_left 171 · table_left 124 · wall_lamps 199 | duvar merdivenini bot hiç almıyor (§9.26) |

**Ana bulgu:** 30 günlük ufukta ekonomi dengede; asıl soru uzun ufuk — gelir ~$176/gece tavanına dayanıp kira onu 31. gecede geçiyor (GDD 26 §12.2). Sonlu koşu mu, büyüme mi: GDD 23'ün, yani yazarın kararı; P18'in ekonomi turu o karardan sonra.

## 4 · Kalite boşlukları

| Boşluk | Ayrıntı |
|---|---|
| UI testsiz | ~28k satır; Tests asmdef'i UI'ı referans bile almıyor. **Kısmen kapandı:** PlayMode süiti (10 test) sanal fareyle gerçek sahneyi oynuyor — taban, kapsam değil |
| ~~PlayMode/input testi yok~~ ✅ | **kapandı 2026-08-12** — `LastCall.PlayTests`: bar açılır, tabure tıklanır, şişe tezgâha iner, tezgâh döker; ayrıca `LookTests` üç ekranı piksel piksel karşılaştırır |
| ~~Determinizm~~ ✅ | **kapandı 2026-09-06** — `CoreCornersTests`: üç tohum × üç akış altın vektörleri, `NextDouble` ham sözcüğü, ve tohumlu bir gecenin ilk altı oturuşu (saniye + sabır) pinli |
| ~~Kültür pini~~ ✅ | **kapandı 2026-09-06** — `RunCulture` nokta ve `n%` yazıyor, `Pin()` tr-TR'yi alt ediyor (testli) |
| ~~Sim başlığı bayat~~ ✅ | **kapandı 2026-09-06** — 200 koşu raporunun başlığı zaten güncel; yıldız pistinin "never shops" notu düzeltildi |
| Kapsamsız Core köşeleri | ~~Relationships eşikleri (1/3/6)~~ ✅ (testli 2026-09-06), ~~RunCulture~~ ✅; GameBootstrap hâlâ yalnız PlayMode'un dolaylı kapsamında |

## 5 · Ölü kod / temizlik envanteri

| Alan | Boyut | Not |
|---|---|---|
| ~~DiegeticStage emekli döngü~~ ✅ | ~~700 satır~~ | **süpürüldü** (2026-08-07 ve 2026-08-27 turları) |
| ~~Menu.cs ölü aile~~ ✅ | ~~250 satır~~ | **dosya bütün olarak silindi** 2026-08-22'de back-bar sayfasıyla birlikte |
| ~~Yetim PNG (Items)~~ ✅ | ~~14 dosya~~ → gerçekte **22** | **silindi 2026-08-27** (`2c8fb8d8`); her aday adla VE GUID'le doğrulandı, `register2.png` yalnız GUID'le bağlı çıkıp kurtuldu |
| ~~Gölgelenmiş sanat~~ ✅ | ~~30 bot_* + 20 stil `_open`~~ **silindi 2026-09-05** (`d468216d`); 57 `v3_*_flat` plakası **silindi 2026-09-05** (`e0744f16`) | yükleme zinciri v4 → kâse → null; v3 dalı koddan çıktı |
| ~~Assets/Art fiilen ölü~~ ✅ | ~~21 şişe + vip_patron + pour_nick(+mask) + club_bg~~ | **bitti** — 2026-09-05'te `Pending~`, arketip portreleri ve `register2` de gitti; `Assets/Art` yalnız üç arka plan |
| ~~DTO ölü alanlar~~ ✅ | ~~charges/bands/chargeMultiplier~~ | **yok** (2026-09-06 doğrulandı: `DataLoader`'da bu alanlar kalmamış; `bands` yaşayan stil bantlarıdır) |
| ~~Tekrarlar~~ ✅ | ~~iki mix-bar ikizi~~ (tek `FillGauge`/`BuildStandingGauge` paylaşılıyor, 2026-09-06 doğrulandı); ~~TycoonHud 3.4k satır tek sınıf~~ (2026-08-25'te dokuz partial'a bölündü); NewRect/NewText ×4 sınıf **bilerek kalıyor** — her biri 7 satırlık yerel yardımcı, ortak sınıf çağrı yerlerini değiştirmeden bir şey kazandırmaz | kapandı |
| ~~Veri tuhaflıkları~~ ✅ | ~~glassware.json yorumu "3 kademe"~~ (yorum altı kademe diyor), ~~`weight≤0→1`~~ (kodda yok) | tequila tek kilitli-T1 hattı bilinçli: tier ladder öyle yazıldı |

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

**Kapanış (2026-09-06):** on ikisi de kapandı — 1/2/3/5/6/9/11/12 önceki turlarda (GDD 19 silindi, GDD 23 ve CLAUDE.md düzeltildi, GDD 25 başlığı, bellek), 4 (GDD 24 §5 "filled to the top" çıkarıldı), 7/8 (PLAN P14/P15/P16'nın ◐ kutuları tarih ve işaretçiyle kapatıldı), 10 (PLAN'daki `FillPreference` hücresi emekli diye işaretli). `GDD_MEVCUT.md` tek gerçek; CLAUDE.md öyle diyor.

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
| ~~P0~~ ✅ | ~~Bota kusurlu-oyun modu (isabet/oran gürültüsü, gecikme)~~ | **kapandı 2026-09-05** — `LastCall → Measure Imperfect Hands` (`Hands.MisreadId`, kusurlu döküm), `Docs/imperfect_hands_report.md` |
| ~~P0~~ ✅ | ~~`BottleArt.cs` bayrağını commit et~~ | **kapandı** — bayrak çoktan girmiş; 2026-09-05'te ağaç dokuz commit'le temizlendi (§0.10) |
| ~~P0~~ ✅ | ~~CLAUDE.md onarımı (UI satır sayısı, modül işaretçileri)~~ | **kapandı 2026-08-27** — `.Menu` parçası (2026-08-22'de silinmişti) mimari bölümünden çıktı, içki alma yeri tezgâhın mahzeni olarak yazıldı, UI satır sayısı 17.5k → 28k |
| **P1** | Ekonomi dengeleme turu (P18) — yeni sim verisiyle | kasa medyanı $7'den yaşanır aralığa |
| ~~P1~~ ✅ | ~~Doküman borcu tek geçiş (§6 tablosu) + `GDD_MEVCUT` tek-gerçek ilanı~~ | **kapandı 2026-09-06** — §6 |
| ~~P1~~ ✅ | ~~Ölü kod süpürmesi (DiegeticStage rayı, Menu ailesi, 14 yetim PNG)~~ | **kapandı 2026-08-27** (`2c8fb8d8`) — 3931 satır çıktı, 15 girdi; 22 yetim PNG (14 değil), bitirme masasının 452 satırı, boş `ShakerSolids` tertibatı, yıkılmış sayfanın beş mobilyası. Her aday adla VE GUID'le doğrulandı — `register2.png` yalnız GUID'le bağlıydı, ad taraması onu yetim sanardı |
| ~~P1~~ ✅ | ~~Determinizm altın vektörleri + kültür pini testi~~ | **kapandı 2026-09-06** — `CoreCornersTests` |
| ~~P2~~ ✅ | ~~UI test dikişi (en az PlayMode duman testi)~~ | **kapandı 2026-08-12** — `LastCall.PlayTests` (10 test) |
| ~~P2~~ ✅ | ~~TycoonHud'u parçalara böl (Flow'un partial deseni)~~ | **kapandı 2026-08-25** — dokuz partial |
| **P2** | Sanat programına dönüş: İncil + tercihler Docs'a, M2 yeniden girişi, M1 konsepti | askıdaki hat kapanır |
| **P2** | ~~Tutorial/FTUE~~ (ev sahibinin dersleri, 2026-09-05, §9.25) + kayıt sistemi (P18 devri) | oturum sürekliliği hâlâ yok — GDD 23 §6 "tam sıfırlama, roguelite" diyor; kayıt yazılmadı |
| ~~P1~~ ✅ | ~~Bankada duran ama çalmayan klipleri bağla~~ | **kapandı 2026-08-27** — 20'sinin 19'u bağlandı, banka **66/66 bağlı**. `rent_line` EMEKLİ EDİLDİ: fatura satırı diye bir an yok, `RebuildDayEnd` üç maliyet satırını tek sessiz geçişte kuruyor — ona ev vermek stagger'ı İNŞA ETMEK olurdu, ki o özellik, ses turu değil |
| **P2** | PlayMode'un ilk-koşu sahte kırmızısını teşhis et | Süit her oturumda 1-2 kez kırmızı verip tekrarda yeşil dönüyor. **2026-09-06 gözlemi:** yazar play'den çıktıktan hemen sonraki ilk koşuda iki test aynı şekilde düştü (`under=[Seat1]`, tıklama işlenmedi), aynı build'de yeniden koşunca 10/10; `TestResults.xml` gerçeğin kaynağı. **BU HAYALET GİRDİ DEĞİL:** iki tanılama da işaretçinin hedefe ULAŞTIĞINI gösteriyor (`under=[Seat1]`, `under=[BillNext@22]`, `key active True`) — tıklama iletiliyor ama işlenmiyor. Şüphe: soğuk ilk koşuda `WaitForSecondsRealtime` geçiyor ama çok az KARE dönüyor (import/shader ısınması kareyi ~1ms olmaktan çıkarıyor), yani `Update`'le sürülen animasyon ilerlemiyor. Ölçülmeden dokunulmamalı |
| — | ~~Teardown'a otomatik hayalet-girdi temizliği~~ | **ÖNERİLMEZ:** `GhostInputGuard` bilerek menü öğesi ve gerekçesi kendi belgesinde ölçülü (2026-08-13): editörde gerçek fare de non-native görünüyor, her play'de ateşlenen bir süpürme oyuncunun kendi imlecini alır. Ayrıca süitin `TearDown`'ı zaten kendi sanal faresini açıkça kaldırıyor |

### 8.1 · Işık/sahne turundan çıkan yeni öneriler (2026-08-10)

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| **P1** | `ShadowCaster2D`: tezgah + fikstürler | ışık şu an her şeyin içinden geçiyor; gölge, sistemi "dekor" olmaktan çıkaran adım |
| ~~P1~~ ✅ | ~~Fikstür slotlarını `fixtures.json`'a taşı~~ | **kapandı 2026-08-10** — `slots` veride (`StageSlot`) |
| ~~P1~~ ✅ | ~~PlayMode duman testi: sahneyi kur, menüyü aç, bir gün oynat~~ | **kapandı 2026-08-12** |
| **P2** | `StageDressing` katmanını world-space'e al | elle yerleştirilen dekor da ışık alsın; şu an odanın tek ışıksız parçası |
| **P2** | Fikstür sanatının PixelLab turu (rapor-önce) | placeholder'lar dosya-adı birebir değiştirilebilir; kod dokunulmaz |
| ~~P2~~ ✅ | ~~Bota fikstür alımı öğret~~ | **kapandı 2026-09-05** (`6673cb47`) — bot `fixtures.json` yüklüyor, gecede bir açık basamağı dolar başına konfora göre alıyor; §9.23 |

### 8.2 · Son müşteri planından çıkan bulgular (2026-08-12)

`GDD 26` + `PLAN_last_call.md` yazılırken veriye bakınca çıkan, hikâyeden bağımsız üç iş:

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| ~~P0~~ ✅ | ~~Kadro evrakını `customers/papers.json`'a taşı~~ | **kapandı 2026-08-12** (PLAN S0) |
| ~~P1~~ ✅ | ~~Tarif merdivenine **erken bir stirred içki** ekle~~ | **kapandı 2026-08-15** — Black Russian (rank 8, 0★) ve Mint Julep (rank 21) Built→Stirred; yeni bant testi `TheFirstRung_TeachesEveryVerbTheBenchAsksFor` geri düşmeyi engelliyor |
| ~~P2~~ ✅ | ~~Gecenin bitişi tek cümlede kalsın~~ | **kapandı 2026-08-13** (S1) — `IsComplete` değişmedi; misafir taburedeyken zaten yanlış |

### 8.3 · Ev ve kapı planından çıkan işler (2026-09-04)

`GDD 27` (mekân: iki puan, temizlik, merdiven) + `GDD 28` (kapı: 20 yaş, ödünç kimlik, kick) +
`PLAN_house_and_law.md` yazılırken koda bakınca çıkan, planın kendisinden bağımsız işler:

| Öncelik | İş | Neden / çıktı |
|---|---|---|
| ~~P0~~ ✅ | ~~Paylaşılan ağaçtaki sahipsiz Core değişiklikleri commit'lensin~~ | **kapandı 2026-09-05** — tek saat `6cbe1f7b`, musluk kilidi `b136a9c8`, TV/meşrubat 9c ve 6c'nin kendi commit'leri; §0.10 |
| ~~P0~~ ✅ | ~~`PlayDayServingEveryone` test yardımcısı temizlik yapsın~~ | **kapandı** (`6673cb47`) — `TestNight.Clean` iki yardımcıda da |
| ~~P1~~ ✅ | ~~Reddedilen sipariş görünmez bardak bırakıyor~~ | **kapandı** (`6673cb47`, C6) — sinyal `DrinkServed`, yalnız `ServeTo` kurar |
| ~~P1~~ ✅ | ~~Fiş ham oda yıldızı, hafta tahtası kırpılmış gece, defter `NightStars` — üç yüzey üç sayı~~ | **kapandı 2026-09-05** (H5) — tahta SERVICE/COMFORT/TONIGHT satırlarıyla min'i açıklıyor, fişte ev satırı; §9.23 |
| ~~P1~~ ✅ | ~~Dressing koridorunun "bir basamak ileri" kuralı Core'da/testte pinsiz~~ | **kapandı 2026-09-06** — `CoreCornersTests.Every_ladder_in_the_catalogue_sells_exactly_the_next_rung` (gerçek dosya, `DevFit` ile tırmanarak) |
| ~~P2~~ ✅ | ~~Sim botu fikstür almıyor~~ | **kapandı** (`6673cb47`) — bkz. §8.1 |
| ~~P2~~ ✅ | ~~`BALANCE.md` eski~~ | **kapandı 2026-09-06** — üretece oda (GDD 27) ve kapı (GDD 28) sayfaları eklendi, `LastCall → Write Balance Guide` ile yeniden üretildi |
| ~~P2~~ ✅ | ~~`DiegeticStage.LoadScreenFrames` hücre boyu TV'ye sabit~~ | **kapandı 2026-09-05** (H4) — `cellW`/`cellH` fikstürün kendi satırında; lavabo suyu ikinci sayfa |
