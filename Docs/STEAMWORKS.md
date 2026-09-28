# STEAMWORKS — başarımlar, istatistikler, durum satırı, bulut

*2026-09-28. Yazar: "Oyuna steam etkileşimleri koyalım. Steam başarımı vs. bu entegrasyonu oyuna iyi bir
şekilde yap her saniye başarım da kazanılmasın çok zor başarımlarda olsun ama sık başarım kazanılsın
oyuncuya ilerleme hissi verilsin." Kurallar `Docs/GDD_MEVCUT.md` §9'daki başarım kaydında; bu belge
Steamworks sitesinde yapılacak işlerin sırası ve oyunun o tarafla nasıl konuştuğu.*

## 1. Bugün ne çalışıyor (App ID olmadan da)

- **48 başarım, 35 istatistik** — `Assets/Resources/Data/achievements.json` (veri; yeni başarım = yeni satır).
  Kurallar Core'da (`AchievementTracker`, `TycoonRun.Feats`), saklama Game'de (`Achievements`), çizim UI'da
  (`TycoonHud.Achievements`).
- **Oyun kendi kaydını tutar:** `persistentDataPath/achievements.json` (bütün barlar boyunca, atomik yazım,
  okunamayan dosya ezilmez, kenara alınır). Steam yokken, editörde, App ID gelmeden kazanılan her şey burada
  durur ve Steam ilk açıldığında ona verilir.
- **Oyun içinde:** kazanılınca üst bildirim satırında altın kupa + ses; uzun sayaçlar çeyreklerde /
  yarıda camgöbeği "12/25" notu; ana menüde **ACHIEVEMENTS** listesi (kazanılanlar yanık, sayaçların çubuğu,
  gizliler "?" ile).
- **Dev fiilleri başarım kazandırmaz:** preset, gece atlama, bedava fikstür, rütbe tırmandırma koşuyu
  kalıcı olarak işaretler (`DevTouched`, kayda da yazılır). Testler (SaveStore kapalı oturum) hiçbir şey
  kaydetmez, göstermez.
- **Tempo ölçüldü:** `LastCall → Achievement Pacing (60 Runs)` → `Docs/achievement_pacing.md`. Taban bot
  ilk gece 3, ilk 12 gecede ~19 başarım alıyor (gecede ~1,5); en uzun boş aralık ortanca 4 gece. Orta/geç
  katmanlar (2★+, 500 servis, 5★ oda...) botun ulaşamadığı yerde — insan için ekonomi projeksiyonu 2★'ı
  ~17., 5★'ı ~53–76. geceye koyuyor.

## 2. Steam tarafı nasıl bağlı

- **Paket:** Steamworks.NET 2025.164.1 (SDK 1.64, MIT) — `Packages/manifest.json`.
- **`Assets/Scripts/Steam/`** (`LastCall.Steam`): yalnız paket kuruluyken derlenir (asmdef version define),
  ve **`StoreLink.SteamAppId` 0 iken Steam'i hiç başlatmaz** — editör "Spacewar oynuyor" görünmez.
  App ID girilince: oyun Steam dışından açılırsa Steam üzerinden yeniden başlatır (`RestartAppIfNecessary`,
  yalnız build), `SteamAPI.Init`, her kare callback, oyun kapanırken `Shutdown`.
- **Birleştirme:** bir oturumda Steam ilk kez açıldığında iki yön birden — Steam'in bildiği büyük sayılar ve
  kazanılmışlar sessizce alınır (başka bilgisayar), buradakiler Steam'e verilir.
- **Dil:** oyuncunun kayıtlı seçimi → **Steam kütüphanesinde bu oyun için seçtiği dil** → işletim sistemi.
- **Overlay:** Shift+Tab açık bir geceyi duraklatır (odak kaybı gibi).
- **Durum satırı (rich presence):** menüde "At the front door", gecede "Night 12 · 3★", kapanışta
  "Closing the books on night 12" — 29 dilde.

## 3. App oluşunca, sırayla

1. **App ID:** `Assets/Scripts/Game/StoreLink.cs` → `SteamAppId = <id>`; aynı sayıyı proje kökündeki
   `steam_appid.txt`'ye yaz (yalnız editör testleri için; `.gitignore`'dan çıkar ve commit'le). Aynı gün
   `StoreLink.Page` adresi de girilir (menüdeki STEAM tuşu onunla görünür).
2. **Kit'i üret:** `py -3 -X utf8 Tools/steamworks/achievements_kit.py` → `Tools/steamworks/out/`.
3. **İstatistikler:** Stats & Achievements → Stats: `out/SHEET.md` §1'deki 35 istatistik (INT, Client;
   *sum* olanlara Increment Only).
4. **Başarımlar:** aynı sayfada Achievements: `SHEET.md` §2'deki 48 başarım, **tablodaki sırayla** (Steam
   token'ları ekleme sırasına göre numaralıyor). Gizli işareti yalnız iki sırrın. İlerleme çubuğu olanlara
   Progress Stat (min 0, max tablodaki).
5. **Çeviriler:** Achievement Localization → Import `out/achievements_loc.vdf` (29 dil). Sıra karıştıysa:
   önce Export al, sonra `achievements_kit.py --from <export.vdf>` → `out/achievements_loc_from_export.vdf`.
6. **İkonlar:** her başarım için 64×64 JPG, yanık + gri. **Henüz seçilmedi** — aday sayfası yazar onayına
   gidecek (yeni sanat önce rapor, sonra oyuna).
7. **Durum satırı:** Community → Rich Presence Localization: `out/rich_presence/<dil>.vdf` dosyaları.
8. **Steam Cloud (Auto-Cloud):** kök *WinAppDataLocalLow*, alt klasör `<şirket>/Malibu Club`:
   `saves/*.json` ve `achievements.json`. **Önce `companyName`'i kesinleştir** (bugün `DefaultCompany`;
   GELISTIRME §0.0 madde 8 "LasGen Interactive" diyor) — değişirse yol da değişir.
9. **Publish:** Stats & Achievements değişiklikleri App Admin'de yayınlanmadan oyuna görünmez.
10. **Dene:** Steam açık, hesap uygulamaya sahip → editörde Play → konsolda `[store] Steam is up` → ilk
    doğru servis "First Round"u açmalı, Shift+Tab geceyi durdurmalı. Sıfırlamak için Steam konsolu:
    `steam://open/console` → `reset_all_stats <appid>`.

## 4. Yeni başarım eklemek

1. `achievements.json`'a satır (yalnız `Stats.cs`'teki istatistiklerden biri; yeni bir sayı gerekirse önce
   Core'da raporlanmalı — `TycoonRun.Feats`). DataLoader yanlış adı, yanlış katmanı, gizli/sır uyuşmazlığını
   yüksek sesle reddeder.
2. `py -3 -X utf8 Tools/loc/achievement_keys.py` → `merge_fragments.py --update --write` → 28 dilin
   `Tools/loc/translations/<dil>/achievements.json` parçası → `assemble_table.py` → `check_tables.py`.
3. `LastCall → Achievement Pacing` ile nereye düştüğünü ölç.
4. Steamworks'te **listenin sonuna** ekle, kit'i yeniden üret, çevirileri yükle, ikonu koy, Publish.
   Oyuncunun zaten yetmiş olduğu bir başarım, dosya ilk okunduğunda sessizce kazanılır (`Reconcile`).
