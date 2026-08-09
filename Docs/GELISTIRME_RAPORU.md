# LAST CALL — GELİŞTİRME RAPORU

**Tarih:** 2026-08-07 · **Yöntem:** 8 kollu kod denetimi (dosya:satır kanıtlı) + sim raporu + doküman-kod karşılaştırması · **Eş belge:** `Docs/GDD_MEVCUT.md` (oyunun bugünkü kuralları)

---

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
| UI testsiz | 23 dosya, ~14.2k satır (CLAUDE.md "~6k" diyor — **2 kat bayat**); Tests asmdef'i UI'ı referans bile almıyor |
| PlayMode/input testi yok | "ölü tap kolu" sınıfı hataların ağı yok |
| Determinizm | yalnız öz-tutarlılık testli; **altın vektör yok** — platform sapması sessiz geçer |
| Kültür pini | `tycoon_speed_response.md` tr-TR formatında işlenmiş ("11,6") — pin kanıtsız |
| Sim başlığı bayat | "marka almaz / bant orta noktası" yazıyor; bot IdealPour kullanıyor ve marka+tarif alıyor |
| Kapsamsız Core köşeleri | Relationships eşikleri (1/3/6), GameBootstrap, RunCulture |

## 5 · Ölü kod / temizlik envanteri

| Alan | Boyut | Not |
|---|---|---|
| DiegeticStage emekli döngü | ~**700 satır** | ray koreografisi, eski kimlik kartı, mood göstergesi — sıfır çağıran |
| Menu.cs ölü aile | ~250 satır | BuildGroupPage/AddItemBox/MixBar/sayfa-çevirme (tek girişi daima gizli) |
| Yetim PNG (Items) | **14 dosya** | backwall kiti, ivy, tablolar, sign_lastcall, gauge_frame… |
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
| **P0** | Sim tablosunu 30 güne aç + yeniden koştur | geç-oyun ekonomisi ilk kez görünür; 1 satırlık düzeltme |
| **P0** | Bota kusurlu-oyun modu (isabet/oran gürültüsü, gecikme) | Close/Wrong/Refused ekonomisi ilk kez ölçülür |
| **P0** | `BottleArt.cs` bayrağını commit et (test yeşiliyle) | çalışma ağacı temizlenir |
| **P0** | CLAUDE.md onarımı (UI satır sayısı, modül işaretçileri) | yanlış pusula düzelir |
| **P1** | Ekonomi dengeleme turu (P18) — yeni sim verisiyle | kasa medyanı $7'den yaşanır aralığa |
| **P1** | Doküman borcu tek geçiş (§6 tablosu) + `GDD_MEVCUT` tek-gerçek ilanı | makas kapanır |
| **P1** | Ölü kod süpürmesi (DiegeticStage rayı, Menu ailesi, 14 yetim PNG) | ~1.000+ satır ve 60+ dosya gürültüsü gider |
| **P1** | Determinizm altın vektörleri + kültür pini testi | platform güvencesi gerçek olur |
| **P2** | UI test dikişi (en az PlayMode duman testi: sahne kur, bir gün oynat, input yolu) | "ölü kol" sınıfına ağ |
| **P2** | TycoonHud'u parçalara böl (Flow'un partial deseni) | 3.4k satırlık tek sınıf dağılır |
| **P2** | Sanat programına dönüş: İncil + tercihler Docs'a, M2 yeniden girişi, M1 konsepti | askıdaki hat kapanır |
| **P2** | Tutorial/FTUE + kayıt sistemi (P18 devri) | yeni oyuncu ve oturum sürekliliği |
