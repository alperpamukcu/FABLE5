# FONT PLANI — "POPÜLER TÜM DİLLER" (2026-09-10)

Yazar: *"Popüler tüm dilleri ekleyeceğiz ona göre fontlar seçmen lazım."*

Bu belge **yalnızca yazı tipi** kararını veriyor. Çeviri katmanı (string tablosu, dil
seçici, `Assets/Data/*.json` metinleri) AYRI ve çok daha büyük bir iş — §6'da ölçüsü var.
Aşağıdaki her satır ölçüldü; tahmin yok.

---

## 1 · Elimizde ne var (2026-09-10 ölçümü)

`fontTools` ile dört dosyanın `cmap`'i tarandı:

| Font | Glif | ASCII | Türkçe (ğıİşÇÖÜ) | Latin-ext (PL/CZ/HU/RO/VN) | Kiril | Yunan | Arapça | CJK |
|---|---|---|---|---|---|---|---|---|
| **PressStart2P-Regular** | 689 | ✔ | **✔ TAM** | 24/25 (`ș` yok) | **✔ TAM** | **✔ TAM** | ✘ | ✘ |
| **Silkscreen-Regular** | 226 | ✔ | ✘ (6 eksik) | 4/25 | ✘ | ✘ | ✘ | ✘ |
| **SilkscreenBold** | 226 | ✔ | ✘ (6 eksik) | 4/25 | ✘ | ✘ | ✘ | ✘ |
| **Jersey15-Regular** | 332 | ✔ | **✔ TAM** | **✔ TAM** | ✘ | ✘ | ✘ | ✘ |

Sürpriz olan: **Press Start 2P zaten Kiril ve Yunan taşıyor.** Yani Rusça, Ukraynaca,
Bulgarca, Sırpça ve Yunanca için YENİ FONT ALMAMIZA GEREK YOK — başlık/rakam yuvası
hazır. Kırılan yer gövde yuvası: **Silkscreen Türkçeyi bile taşımıyor** (ğ Ğ ı İ ş Ş yok).

Yuvalar (`DebugSceneCreator.cs:29-32`):

- `displayFont` → PressStart2P (başlıklar, rakamlar) — DiegeticStage + TycoonHud + Flow
- `bodyFont` → Silkscreen (gövde, açıklama, konuşma) — TycoonHud + Flow
- `shopFont` → SilkscreenBold (market)
- `Jersey15` → `Resources/Fonts/`, kod tarafında ayrı yükleniyor

---

## 2 · Karar: Silkscreen gövde yuvasından çıkıyor

**Bugün, tek dil oyunda bile bir hata var:** İngilizce metinde sorun yok ama oyun
Türkçeye çevrildiği an gövde yazısında ğ/ı/ş yerine kutu çıkar. Bu tek başına Silkscreen'i
emekli etmek için yeterli.

Yerine **Jersey 15** (zaten repoda, OFL, Türkçe + tüm Latin-ext TAM). Aynı yerde duran,
lisansı temiz, indirilmesi gerekmeyen tek aday.

> Uyarı — piksel ızgarası: ev kuralı "piksel yüzleri yalnız 8px tasarım boyunun tam
> katlarında temiz rasterize olur, punto 8/16/24'e sabitlenir" (CLAUDE.md). **Jersey 15'in
> tasarım boyu 15'tir, 8 değil.** Yani gövde yuvasını Jersey'e çevirirken puntolar
> 15/30 (ya da ölçülerek en yakın temiz kat) olmalı; bugünkü 8/16/24 ile bırakılırsa
> gövde bulanıklaşır. Bu, geçişin ölçülmesi gereken tek yeri.

---

## 3 · Diller üç kuşağa ayrılıyor

### Kuşak A — bugün açılabilir, YENİ FONT GEREKMEZ

Latin + Kiril + Yunan: **İngilizce, Türkçe, Almanca, Fransızca, İspanyolca, Portekizce,
İtalyanca, Hollandaca, Lehçe, Çekçe, Macarca, Romence, Endonezce, Vietnamca(*),
Rusça, Ukraynaca, Yunanca.**

- Başlık/rakam: **PressStart2P** (olduğu gibi)
- Gövde/market: **Jersey 15** (Silkscreen'in yerine)
- Tek gerçek eksik: Romence `ș` (U+0219) PressStart2P'de yok. İki satırlık çözüm —
  o dilde `ș`→`ş` (U+015F) normalizasyonu; Romence okurları bu ikameyi zaten görüyor.
- (*) Vietnamca Jersey'de tam ama PressStart2P'de değil; başlıklar Vietnamca'da
  Jersey'e düşmeli.

**Bu kuşak, dünya oyuncu tabanının büyük çoğunluğu ve bize hiçbir font maliyeti yok.**

### Kuşak B — CJK, yeni font + teknik iş gerektirir

**Basitleştirilmiş Çince, Geleneksel Çince, Japonca, Korece.**

Piksel estetiğini koruyan, lisansı temiz iki aile:

| Aile | Kapsam | Lisans | Tasarım boyu |
|---|---|---|---|
| **Zpix (最像素)** | SC + TC + Kana + Kanji + Hangul + Latin + Kiril | OFL | 12 px → punto 12/24 |
| **Galmuri9 / 11 / 14** | Hangul + Kana + Kanji + Latin + Kiril | OFL | 9 / 11 / 14 px |

Öneri: **tek dosyayla Zpix**. Dört dili birden taşır, punto 12/24 ile piksel ızgarasına
oturur, ve Latin'i de taşıdığı için o dillerde karışık cümlelerde yüz değişmez.

Teknik bedel — küçümsenmemeli:

- CJK'da **büyük harf yoktur.** Kodda 97 yerde `ToUpperInvariant` var
  (`grep -c ToUpper` → 97, biri hariç hepsi Invariant). CJK'da bunlar zararsız ama
  Türkçede yanlış: `ToUpperInvariant("i")` → `"I"`, doğrusu `"İ"`. Yani ya
  `ToUpper(tr-TR)` ya da — daha temiz — **dile göre büyük-harfleştirmeyi kapatan tek bir
  yardımcı** (`UIText.Caps(s)`), çünkü Yunanca'da da büyütünce aksan düşer.
- Legacy uGUI `Text` + dinamik `Font` CJK'yı atlasa sığdırırken zorlanır; ekranda aynı
  anda birkaç yüz farklı glif olacaksa **TextMeshPro'ya geçiş** gerekir (TMP'nin dinamik
  SDF atlası ve `fallbackFontAssets` zinciri var; legacy'de eşdeğeri yok). Bu, UI'nin
  tamamına dokunan ayrı bir faz.

### Kuşak C — ŞİMDİLİK KAPSAM DIŞI ÖNERİLİYOR

**Arapça, Farsça, İbranice, Tayca, Hintçe.**

Sebep font değil, **şekillendirme**: Arapça/Farsça harfleri kelime içindeki yerine göre
biçim değiştirir ve satır sağdan sola akar; Tayca/Devanagari birleşik glif ister. Unity'nin
ne legacy `Text`'i ne de TMP'si bunu kendiliğinden yapar — harici bir shaper (HarfBuzz
sarmalayıcı ya da RTL eklentisi) ve tüm hizalama/ikon-metin düzeninin aynalanması gerekir.
Ayrıca piksel estetiğinde lisansı temiz Arapça yüz neredeyse yok.

Karar önerisi: bu beş dil **v1'de yok**. İstenirse ayrı bir faz olarak planlanır.

---

## 4 · Uygulama sırası (öneri)

| # | İş | Boyut |
|---|---|---|
| F1 | `bodyFont` + `shopFont` → Jersey 15; puntoları 15/30 ızgarasına oturt, oyunu baştan sona bak | küçük |
| F2 | `UIText.Caps(s)` — dile duyarlı büyük harf; 97 `ToUpperInvariant` çağrısı buradan geçsin | küçük |
| F3 | String tablosu + dil seçici (§6) | **büyük** |
| F4 | Kuşak A dilleri açılır | orta (çeviri işi) |
| F5 | TMP geçişi + Zpix → Kuşak B | **büyük** |
| F6 | (isteğe bağlı) shaper + RTL → Kuşak C | **büyük** |

F1 ve F2 bugün yapılabilir ve tek dilde bile oyunu iyileştirir.

---

## 5 · Fontları nereden alacağız

Üçü de **SIL Open Font License** — ticari oyunda bedava, tek şart telif satırının
korunması. Yeni gelecek tek dosya Zpix.

| Font | Kaynak | Durum |
|---|---|---|
| Press Start 2P | Google Fonts | **repoda** `Assets/Fonts/` |
| Jersey 15 | Google Fonts | **repoda** `Assets/Resources/Fonts/` |
| Zpix | github SolidZORO/zpix-pixel-font | indirilecek |
| Galmuri (yedek) | github quiple/galmuri | indirilecek |

---

## 6 · Çeviri katmanının ölçüsü (font işi DEĞİL, ama bilinmesi gerek)

- UI kodunda **266 ayrı büyük-harf metin sabiti** (`Assets/Scripts/UI`, tek tırnak taraması).
- Veri tarafında **~59 KB metin**: `roster.json` 44.6 KB, `papers.json` 8.4 KB,
  `story.json` 3.8 KB, `archetypes.json` 2.6 KB.
- Kodda **hiç lokalizasyon katmanı yok** — `Localization`, `I18n`, `LocaleKey` araması boş.

Yani her metin bugün doğduğu yerde yazılı. Diller açılmadan önce F3 şart.

---

*Bu belge bir plandır; hiçbir font değişimi henüz yapılmadı.*
