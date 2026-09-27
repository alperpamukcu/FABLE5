# KREDİLER — oyunda kullanılan üçüncü taraf varlıklar

*Oyunun kendi çizimi, kodu ve bestesi dışında build'e giren her şeyin kaynağı, sahibi ve lisansı.
Jenerik ekranı / Steam sayfasının "Credits" bölümü / basın kiti buradan yazılır. Yeni bir üçüncü
taraf varlık oyuna girdiği gün buraya satırı düşer — lisansı okunmadan hiçbir şey sevk edilmez.*

## Görsel

| Oyunda | Kaynak | Sahibi | Lisans | Atıf |
|---|---|---|---|---|
| Buff / debuff işaretleri `Assets/Resources/Items/ib_*.png` (20 ikon, `Tools/icons_1bit_ship.py`) | [1-bit Pixel Icons](https://nikoichu.itch.io/pixel-icons) | Nikoichu | CC0 1.0 — "you have absolutely no restrictions on how you can use them" | Zorunlu değil; sahibi rica ediyor, veriyoruz |

Kaynak paket proje kökünde, `1-bit_Pixel_Icons/` (Assets dışında; yalnız seçilen ikonlar sevk edilir,
palete oturtularak: beyaz → `Cream[4]`, siyah → `Night[0]`). Paketin kendisi git'e girmez (`.gitignore`,
yazar 2026-09-28): repoda yalnız kullanılan ikonlar durur; yeni seçim için paket itch.io'dan köke indirilir.

**Jeneriğe girecek satır:**
> Icons: "1-bit Pixel Icons" by Nikoichu — https://nikoichu.itch.io/pixel-icons (CC0)

## Yazı tipleri (SIL Open Font License 1.1)

OFL, yazı tipi yazılımla birlikte dağıtılırken **lisans metninin ve telif notunun da dağıtılmasını**
şart koşar. Lisans metinleri `Assets/Fonts/OFL-*.txt`'de duruyor ama oyunun derlemesine girmiyor —
bir jenerik / "Lisanslar" ekranı ya da derleme klasörüne kopyalanan bir `LICENSES` dosyası gerekir
(GELISTIRME_RAPORU §0.0'a eklendi) — **2026-09-27'de karşılandı:** ana menünün CREDITS ekranı her
fontun telif notunu ve SIL OFL 1.1'in tam metnini gösteriyor (`Tools/credits_build.py` →
`Resources/Data/credits.json` + `licenses.txt`).

| Yazı tipi | Lisans dosyası |
|---|---|
| Silkscreen | `Assets/Fonts/OFL-Silkscreen.txt` |
| Press Start 2P | `Assets/Fonts/OFL-PressStart2P.txt` |
| Jersey 15 | `Assets/Fonts/OFL-Jersey15.txt` |
| Indie Flower | `Assets/Fonts/OFL-IndieFlower.txt` |
| Galmuri (ko) | `Assets/Fonts/OFL-Galmuri.txt` |
| Fusion Pixel 12 (ja / zh) | `Assets/Fonts/OFL-FusionPixel.txt` |

## Ses

Bütün kayıtlar CC0 / kamu malı; dosya dosya kaynakları `Docs/SES_KAYNAKLARI.md`'de
(`Tools/sfx_ingest.py --ledger` yazar). Şarkılar ve sentetik klipler oyunun kendi üretimi.
