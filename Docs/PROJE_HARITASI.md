# PROJE HARİTASI — dosya yollarıyla (2026-09-26, HEAD 32d2bf56)

*Yazarın "tüm projeyi dosya yollarıyla beraber düzenle" isteğiyle çıkarıldı. Bu belge elle
tutulur; bir klasör taşınırsa buradaki satırı da taşı. Kural: **Assets/Resources altındaki her
yol YÜK TAŞIR** (koddan adıyla yüklenir) — oradaki hiçbir şey, yükleyen kod satırı bilinmeden
taşınmaz/silinmez. Aşağıda her canlı klasörün yükleyicisi yazılıdır.*

## 1. Kök dizin

| Yol | Ne | Boyut | Git |
|---|---|---|---|
| `Assets/` | Oyun | 153 MB, ~11.7k dosya | izlenir |
| `Docs/` | Tasarım + planlar + raporlar | 12.5 MB | çoğu izlenir; `marketing/` ve `steam/` **İZLENMİYOR** (bkz. §6.1) |
| `Tools/` | Sanat/ses/loc/mağaza boru hatları | **1.17 GB** | 1.870 izlenir, ~1.700 izlenmiyor, kalanı ignore |
| `Library/`, `Temp/`, `Logs/`, `UserSettings/` | Unity önbelleği | 4.3 GB | ignore |
| `ProjectSettings/`, `Packages/` | Ayarlar + paketler | küçük | izlenir |
| `CLAUDE.md`, `AGENTS.md`, `README.md` | Kural + tanıtım | — | izlenir |
| `Art/pilot/` | Pilot dönemi taslakları | 6.6 MB | ignore |
| `LastCall.*.csproj`, `*.slnx` | IDE dosyaları (üretilir) | — | ignore |
| `Assembly-CSharp*.csproj`, `LastCall.DebugUI.csproj` | **ÖLÜ** (2026-07-28'de kalkan derlemeler) — silinebilir | — | ignore |
| `dev/null/` | Yanlış yere yazılmış LFS hook'ları — silinebilir | — | ignore |

## 2. Assets — derleme sınırları (asmdef)

```
LastCall.Core       Assets/Scripts/Core/      hiçbir şey (noEngineReferences) — TÜM kurallar
LastCall.Game       Assets/Scripts/Game/      Core
LastCall.UI         Assets/Scripts/UI/        Core, Game, uGUI, InputSystem, URP+2D
LastCall.Editor     Assets/Scripts/Editor/    hepsi (yalnız editör)
LastCall.Tests      Assets/Tests/EditMode/    Core, Game (UI YOK; ama PerfectPourTests:278
                                              UI klasörünü METİN olarak tarar — UI dosyaları
                                              Assets/Scripts/UI altından çıkamaz)
LastCall.PlayTests  Assets/Tests/PlayMode/    Core, Game, uGUI, InputSystem(+TestFramework)
```

Büyük dosyalar (bölmek istersen aynı klasörde partial olarak böl): `DiegeticStage.cs` 315 KB,
`TycoonHud.Seats.cs` 310 KB, `ChromeArt.cs` 216 KB, `TycoonServiceFlow.Shaker.cs` 216 KB,
`TycoonRun.cs` 208 KB (+ `.Door/.House/.Lessons/.Save` partial'ları), `TycoonHud.Chrome.cs` 196 KB.

## 3. Assets/Data — sahneye GUID ile, araçlara YOL ile bağlı

Taşıma `.meta` kalırsa sahneyi kırmaz ama YOL okuyanları kırar (testler, BalanceGuide,
EconomyReport, DebugSceneCreator, `Tools/upgrade_tree/ship.py`).

| Dosya | İçerik | Not |
|---|---|---|
| `bottles/base_bar.json` | 41 kart ("starting" ×9) | parite: `RecipeCatalog` ↔ `recipes.json` |
| `recipes/recipes.json` | 54 tarif | |
| `glassware/glassware.json` | 5 bardak (+tierComfort) | |
| `fixtures/fixtures.json` | 110 satır | `ship.py` UYARILI: konfor/fiyat elle, üstüne yazma |
| `customers/archetypes.json` | 8 arketip | |
| `customers/papers.json` | 51 ehliyet | |
| `customers/roster.json` | 100 kişilik kadro | **oyun OKUMAZ** — yalnız 4 Tools betiği okur |
| `story/story.json` | Ece'nin arkı (1 beat, 7 ders) | |

## 4. Assets/Resources — sevkiyata giren her şey (~139 MB) ve yükleyicileri

| Klasör | Boyut | Yükleyen |
|---|---|---|
| `Audio/` (103 klip) | 56.6 MB | `Sfx.cs:215/286/374/562` (`Audio/{ad}`, `_far`, `music_{mood}_{n}`) |
| `Patron/` (41 yüz × 10 klip, 4.384 PNG) | 39.1 MB | `TycoonHud.Seats.cs:4831/4875` — HEPSİ açılışta yüklenir |
| `Fonts/` (8 TTF) | 35.0 MB | `LanguageFonts.cs:30/155` — dil başına yüklenir ama hepsi sevk edilir |
| `Data/loc/` (29 dil) | ~7 MB | `Localization.cs:20/93/117` |
| `Items/` (483 PNG) | — | `ItemArt.cs:25` — 22 dosyada 98 çağrı, adlar çoğu zaman çalışma anında kurulur (`v4_{id}_back…`) |
| `Fixtures/` (143 PNG + 2 lavabo animasyonu) | — | `DiegeticStage.cs:407/839/3711/4541`, `TycoonHud.Ladder.cs:1343` — adlar `fixtures.json`'dan |
| `Menu/` (30) | — | `MenuPack.cs:44/248` |
| `Keys/` (58) | — | `KeyCaps.cs:37` |
| `Scene/` (15) | — | `DiegeticStage`, `TycoonHud.Curtain`, `WindowSky.cs:80` (`window_city.bytes`) |
| `Emotes/` (41) | — | `TycoonHud.Seats.cs:3661+` (`sign_*` canlı; `em_*` ×30 bilerek duruyor) |
| `Strangers/` (12) | — | `TycoonHud.IdCard.cs:1133` |
| `Data/` (4 json) | — | `GameBootstrap.cs:172/174`, `RecipeLore.cs:57`, `SkyClock.cs:143` |
| `Fluid/MetaballLiquid.mat` | — | `MetaballFluid.cs:23` |

Resources DIŞI: `Scenes/Main.unity` (tek sahne), `Settings/` (URP: `PC_RPAsset`+`Renderer2D`
canlı; `PC_Renderer/Mobile_Renderer/SampleSceneProfile` ŞABLON ARTIĞI, referanssız),
`Shaders/MetaballLiquid.shader`, `Fonts/` (3 yüz + OFL lisansları), `Art/Backgrounds/` (tezgâh),
`Tests/PlayMode/Baselines~/` (bench/menu/basket — Unity almaz, git tutar),
`InputSystem_Actions.inputactions` (ŞABLON, kod kullanmıyor ama project-wide actions kayıtlı).

## 5. Tools ve Docs — boru hatları

Canlı hatlar (izlenir): `loc/` (parçalar+çeviriler → `Data/loc`), `v4_bottles/` (şişeler;
`palette.py`'yi steam_kit/menu_art/shaker da kullanır), `menu_art_gen.py` (menüler; `frame`
komutu dahil), `upgrade_tree/` (oda merdiveni → Fixtures + fixtures.json), `music_synth.py`,
`sfx_bank/dsp/ingest`, patron_* zinciri, `heart_icon/icon_sizes` (tek yıldız/kalp kuralı).

Docs canlıları: `GDD_MEVCUT.md` (kural kazanır), `GDD/` 14–16, 21–28, `_CHANGELOG`,
`PLAN_service_depth/last_call/house_and_law/bottle_art_v4/localization_L1/rank_ladder`,
üretilenler: `BALANCE.md`, `ECONOMY_*.md` (EconomyReport SON adlıyı yeniden yazar — taşınmaz),
`tycoon_sim_report.md` + 4 sim raporu, `SES_KAYNAKLARI.md`. `story_guests_drafted.json`
**YÜK TAŞIR** (`StoryDataTests.cs:71`). Tarihî olanlar (arşivlenebilir, yalnız doc-doc bağlantı):
`SAHNE_DUZENLEME`, `ONAYLANAN_GORSELLER`, `PLAN_audio_sourcing/fonts_i18n/pour_v2/bench_scene/second_look`, `GDD/18`, `GDD/25`.

## 6. DÜZENLEME BULGULARI (sıralı; "yap/yapma" kararları yazarın)

1. **Steam + pazarlama hattı GIT'TE DEĞİL.** `Tools/steam_kit/` (1.03 GB; ~320 MB'ı ignore bile
   değil: `deliver/` 166 + `out/` 107 + `fonts/` 43), `Tools/social/`, `Docs/steam/`,
   `Docs/marketing/`, `KeyArtCapture.cs`. İzlenen kod onlara bağımlı (`Languages.cs:13` →
   `steam_kit/i18n.py`; `Tools/loc/check_tables.py:27` → `steam_kit/fonts`). Kayıp riski
   yüksek; `deliver/` büyük ölçüde `out/` kopyası. Öneri: betikler+refs+i18n+fonts commit,
   `out/`+`deliver/` ignore. O hat başka oturumun aktif sahası — kararı sahibiyle ver.
2. **Reddedilen adaylar `??` olarak sızıyor** (~1.300 dosya): `room_variants2` (tamamı),
   `room_variants7` (tamamı — ama `room7_ship.py:41,104` SEVK EDİLMİŞ sanatın girdisi olarak
   4'ünü okuyor: bunlar izlenmeli), `cert_paper/{out,raw}`, `money_icon_out/`, `sfx_v2/`,
   `tv_ads2/state.json`… Öneri: sevk girdilerini izle, kalanına alt-klasör ignore kuralı.
3. **Sevkiyata binen ölü sanat** (GUID taraması yapıldı, güvenli): `Fixtures/{gold,silver}_sink_animation/`
   (28 PNG — `Tools/AssetPipeline/sources/sink` ile bayt-aynı), `Scene/window_cycle.png` (432 KB),
   `Items/{key_back,key_next,sh_wifi,licence_shell2,licence_shell3,glass3d_martini_t5_Front1,
   sign_open_arrow,sign_shut_arrow,bench_tap_arch,bench_tap_single,bench_tap_tee}`,
   `Fixtures/{fx_candle,fx_tap_single,fx_tap_arch,fx_tap_tee,fx_plant_fiddle,fx_plant_snake}`.
   DOKUNMA: `Items/bubble_white_2/10` ve `Items/cog3d` (Tools okuyor), `star3d/heart3d/medal3d/
   coin3d/dollar` aileleri (ItemArt yedek zinciri), `Emotes/em_*` (bilerek duruyor).
4. **Belge-kod çelişkileri:** CLAUDE.md'nin v3 yedeği satırı, test sayıları ve README'nin eski
   MENÜ hızlı başlangıcı bayattı (bu commit'te düzeltildi). Hâlâ bayat: `GDD/26` başlığı
   ("design, not built"), `GDD/25` başlığı (v4'e yenildi), LAUNCH_READINESS/EARLY_ACCESS'in
   "kayıt yok / DEV TOOLS görünür / display ayarı yok" satırları (pazarlama oturumunun dosyaları),
   HANDOFF §6 (Enter Play Mode 1/1 okur, belge 0/0 der — bu commit'te düzeltildi).
5. **İzlenen betiklerin yerel-tek girdileri:** `AssetPipeline/sources/patron_trial/` (82.5 MB,
   ignore) olmadan patronlar başka makinede yeniden sevk edilemez → HANDOFF'a not düşüldü.
6. **Editörün gördüğü çöp** (güvenle silinir): `Assets/_Recovery/` (11 sahne),
   `Assets/InitTestScene*.unity` ×7, kökteki ölü csproj'lar, `dev/null/`,
   `Tools/patron_sheet_frames/` (20.5 MB kopya).
7. **LFS dışı ikili:** `Tools/AssetPipeline/sources/pixellab_user/room_ref.jfif` (2.58 MB;
   `.jfif` LFS kalıbında yok). `Scene/window_city.bytes` YERİNDE KALIR (`WindowSky.cs:80`).
8. **Şablon artıkları** (düşük öncelik, Unity içinden sil): `PC_Renderer/Mobile_Renderer/
   SampleSceneProfile.asset`, Mobile kalite seviyesi + `Mobile_RPAsset`,
   `InputSystem_Actions.inputactions` (önce Project Settings → Input System'den çöz),
   `applicationIdentifier` şablon kimliği, `com.unity.collab-proxy` paketi.
9. **Ad birliği yok (7 ad):** depo `FABLE5`, klasör/projectName "My project (2)", derlemeler
   `LastCall.*`, belgeler "LAST CALL", productName "Malibu Club", mağaza "Malibu Club: Cocktail
   Bar Simulator", stüdyo "LasGen Interactive" (STUDIO_BRAND) — `companyName` hâlâ
   "DefaultCompany". **companyName'i EA'den önce BİR KEZ değiştir** (kayıt klasörü + PlayerPrefs
   taşınır; bugünkü productName düzeltmesi bunu bir kez zaten yaptı).
10. **Standalone iskelet ayarları:** scripting backend MONO (IL2CPP ayarlı değil — `stripEngineCode`
    bu yüzden etkisiz), grafik API otomatik, `defaultScreenWidth 1024×768` (4:3!), Unity splash
    açık, exe ikonu boş (`m_BuildTargetIcons: []` — ikonlar `Tools/steam_kit/deliver/icons/`te),
    `enableCrashReportAPI: 0`. Demo derlemesinden önce hepsi elden geçmeli (GELISTIRME_RAPORU
    2026-09-26 bölümü).
