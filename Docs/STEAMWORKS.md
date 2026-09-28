# STEAMWORKS — başarımlar, istatistikler, sıralama, zaman çizelgesi, bulut

*2026-09-28. Yazar: "Oyuna steam etkileşimleri koyalım ... sık başarım kazanılsın ... ilerleme hissi verilsin."
Aynı gün: "Steam başarımları için gerekenleri sağlayalım ve tamamlayalım" — App ID, desteklenen özellikler,
ikonlar, şirket adı, rütbe unvanlarının çevirisi. Kurallar `Docs/GDD_MEVCUT.md` §9.133 ve §9.136'te; bu belge
Steamworks sitesinde yapılacak işlerin sırası ve oyunun o tarafla nasıl konuştuğu.*

**Uygulama:** Malibu Club: Cocktail Bar Simulator — **App ID 5336380** (`StoreLink.SteamAppId`, proje kökünde
`steam_appid.txt`). **Şirket:** LasGen Interactive (ProjectSettings `companyName`).

## 1. Oyunda ne çalışıyor

- **48 başarım, 35 istatistik** — `Assets/Resources/Data/achievements.json` (veri). Kurallar Core'da
  (`AchievementTracker`, `TycoonRun.Feats`), saklama Game'de (`Achievements`), çizim UI'da
  (`TycoonHud.Achievements`). Oyun kendi kaydını tutar (`persistentDataPath/achievements.json`); Steam
  açıldığında ikisi iki yönlü birleşir. Dev fiilleri başarım kazandırmaz; test takımları hiçbir şey kaydetmez.
- **Sıralama listeleri (3):** `LONGEST_RUN` (bir barın açık kaldığı gece), `BIGGEST_TILL` (bir gecenin
  kapanışında kasa), `BEST_NIGHT_TIPS` (tek gecenin bahşişi). Kişisel en iyiler; Steam her oyuncunun en iyisini
  tutar. Oyun listeyi ilk puanda kendisi oluşturur.
- **Zaman çizelgesi (Steam Timeline):** her gece bir oyun aşaması (kapılar açılınca başlar, şafakta kapanır);
  kayıt çubuğunun altında "Gece 12 · 3★" / defter / ön kapı; işaretli anlar: başarım, beş yıldızlı gece, yeni
  rütbe, bir tarifin ilk kusursuz dökümü, barın kapanması. Metinler oyuncunun dilinde, ikonlar Steam'in kendi
  `steam_*` seti — sitede ayar gerekmez.
- **Durum satırı (rich presence):** menüde / "Gece 12 · 3★" / defter kapanışı, 29 dil.
- **Dil:** oyuncunun kayıtlı seçimi → Steam kütüphanesinde bu oyun için seçilen dil → işletim sistemi.
- **Overlay:** Shift+Tab açık bir geceyi duraklatır.
- **Steam yokken:** oyun "[store] Steam is not running for this app" der ve kaydını diskte tutar (ölçüldü,
  r259). Steam'den başlatılmayan bir build (steam_appid.txt yanında değilse) kendini Steam üzerinden yeniden açar.

## 2. Steamworks'te, sırayla

1. **Kit'i üret:** `py -3 -X utf8 Tools/steamworks/achievements_kit.py` → `Tools/steamworks/out/SHEET.md`
   (bütün tablolar), `achievements_loc.vdf`, `rich_presence/*.vdf`.
2. **İstatistikler:** SHEET §1 — **8'i zorunlu** (ilerleme çubuğunu çizenler); kalan 27 isteğe bağlı (yalnız
   başka bilgisayara taşınan sayılar için).
3. **Başarımlar:** SHEET §2, **tablodaki sırayla** (Steam token'ları ekleme sırasına göre numaralıyor). Gizli işareti
   yalnız iki sırrın. İlerleme çubuğu olanlara Progress Stat (min 0, max tablodaki).
4. **İkonlar (yazar onayladı, 2026-09-28):** `py -3 -X utf8 Tools/steamworks/achievement_icons.py` →
   `out/icons/256/<API>_achieved.jpg` ve `_unachieved.jpg` (256×256; Steam kendisi küçültür). Açılmamış hal ikonun
   siyah-beyazı, iki sırrınki kilit; oyun içindeki liste ve kart aynı resimleri kullanır (GDD §9.137).
5. **Çeviriler:** Achievement Localization → Import `out/achievements_loc.vdf` (29 dil). Sıra karıştıysa önce Export,
   sonra `achievements_kit.py --from <export.vdf>`.
6. **Sıralama listeleri:** oyun ilk puanda oluşturur; sitede her birine Community Name ver (SHEET §3).
7. **Durum satırı:** Community → Rich Presence Localization: `out/rich_presence/<dil>.vdf`.
8. **Steam Cloud (Auto-Cloud):** SHEET §4 — kök WinAppDataLocalLow, `LasGen Interactive/Malibu Club/saves`
   (`*.json`) ve `LasGen Interactive/Malibu Club` (`achievements.json`); kota 10 MB / 50 dosya.
9. **Publish:** Stats & Achievements değişiklikleri App Admin'de yayınlanmadan oyuna görünmez.
10. **Desteklenen özellikler:** SHEET §5 — işaretle: Başarımlar, Steam Cloud, İstatistikler, Sıralama Listeleri,
    Zaman Çizelgesi. İşaretleme: Remote Play (telefon/tablet/TV — kumanda desteği yok), Remote Play Together
    (tek oyunculu), satın alma / atölye / bölüm düzenleyici / bildirimler / HDR / yorum / Source (oyunda yok),
    altyazı (seslendirme yok). Her kutu, özellik yayınlanıp denendikten sonra.
11. **Dene:** Steam açık, hesap uygulamaya sahip → editörde Play → konsolda `[store] Steam is up` → ilk doğru servis
    "First Round"u açmalı; Shift+Tab geceyi durdurmalı; Steam'in kayıt çubuğunda gece aşaması görünmeli. Sıfırlamak
    için: `steam://open/console` → `reset_all_stats 5336380`.

## 3. Yeni başarım eklemek

1. `achievements.json`'a satır (yalnız `Stats.cs`'teki istatistiklerden biri; yeni bir sayı gerekirse önce Core'da
   raporlanmalı — `TycoonRun.Feats`). DataLoader yanlış adı, katmanı, gizli/sır uyuşmazlığını reddeder.
2. `Tools/loc/achievement_keys.py` → `merge_fragments.py --update --write` → 28 dilin
   `Tools/loc/translations/<dil>/achievements.json` parçası → `assemble_table.py` → `check_tables.py`.
3. `LastCall → Achievement Pacing` ile nereye düştüğünü ölç.
4. `achievement_icons.py`'nin PICKS tablosuna işaretini ekle; Steamworks'te **listenin sonuna** ekle, kit'i yeniden
   üret, çevirileri ve ikonları yükle, Publish. Oyuncunun zaten yetmiş olduğu bir başarım ilk okumada sessizce
   kazanılır (`Reconcile`).
