# ONAYLANAN GÖRSELLER — OYUNA GİRMEYİ BEKLEYENLER

*Son güncelleme 2026-09-10.*

Ev kuralı: yazar seçmeden hiçbir üretim `Assets`'e girmez (bkz. hafıza `bottle-art-v3-respec`).
Aşağıdakiler **seçildi ama hâlâ girmedi** — hepsi `Tools/room_variants*/` altında duruyor.
Bu dosya seçimlerin kaybolmaması için var; bir satır oyuna indiği gün buradan silinir.

**Hepsini odada görmek için:** `Tools/upgrade_tree/` — barın bütün merdivenlerini ağaç hâlinde
gösteren, tıklayınca odayı kuran sayfa (aşağıdaki bekleyenler "aday" rozetiyle orada duruyor).
Üretimi: `LastCall → Export Room Layers` (play modunda, oda ekranda) + `py Tools/upgrade_tree/build.py`.
Poster sorusu (sağ duvar mı sol mu) orada denenerek cevaplanır: sol poster pencereye biniyor,
sağ poster televizyonun yerini kaplıyor.

---

## Seçilenler

| Tur | Dosya | Nereye | Not |
|---|---|---|---|
| 1 | `art_city` | `wall_center` merdiveni | 170×80, üçlü tablo |
| 1 | `table_v1`, `table_v2` | `table_left` / `table_right` merdiveni | 132×78 |
| 2 | `plant_monstera`, `plant_bird` | `plant_left` / `plant_right` merdiveni | 56×92, 56×96 |
| 3 | `picS_flamingo`, `picS_cherry`, `picS_lips` | `wall_center` (küçük boy) | 48×56, 48×48, 56×44 |
| 3 | `picR_pelican` | **yeni yuva** `wall_right_art` | sağ duvar perspektifi, `hang_right` ile basılmış |
| 3 | `fx_ceil_beams`, `fx_ceil_stucco`, `fx_ceil_deco`, `fx_ceil_palm` | **yeni yuva** `ceiling` | 640×360 plaka, odanın krem yamuğuna kesilmiş |
| 4 | `fx_floor_check`, `fx_floor_carpet`, `fx_floor_marble` | **yeni yuva** `floor` | 640×360 plaka, zemin düzlemi projeksiyonu |
| 4 | `post_malibu`, `post_surf`, `post_pier`, `post_coast` | **yeni yuva** (poster) | **AÇIK SORU:** sağ duvar mı, sol mu, ikisi de? Her ikisi de basıldı (`fx_R_*`, `fx_L_*`) |

## Beşinci tur — değerlendirmede (2026-09-12)

Yazarın istediği yeni adaylar (`Tools/room_variants5_gen.py`, 39 üretim): üçer tavan, zemin,
**bar üstü tablası** (mahzen rafları elleniyor değil), halı, paspas dokusu (her dokudan hem damla
hem garnitür paspası), sağ duvar posteri, neon, televizyon, masa (mat, yansımasız), bitki, bira
musluğu; artı iki yeni üçlü tablo (altı küçük tablo, çerçeveleri prosedürel çizildi — üretici
kesimi çerçeveyi alıyor). Hepsi upgrade ağacında "aday" olarak duruyor; seçilenler buraya
"Seçilenler" tablosuna taşınır.

## Altıncı tur — değerlendirmede (2026-09-12)

80'ler Miami desenleri, üçer adet (`Tools/room_variants6_gen.py`): halı (memphis, gün batımı
bantları, palmiye), zemin (neon ızgara, terrazzo, dalga karo), tavan (rozetli panel, ışın,
yıldız), arka duvar (chevron, neon kemerler, palmiye duvar kâğıdı), **sağ duvar** (flamingo
şeritler, turkuaz baklava, ışın), duvar lambası (tarak, neon çubuk, deniz kabuğu), sağ duvar
neonu (gün batımı, kadeh, pelikan) ve sağ duvar tablosu (okyanus, sahil yolu, memphis).
Hepsi ağaçta "aday"; perspektifleri odanın kendi haritalamalarıyla verildi.

## Reddedilenler (tekrar üretilmeyecek, arşiv)

Tur 2: `art_flamingo_neon`, `art_palms`, `table_v3`, `counter_v1/v2/v3`, `pic_*` ilk çekimler,
`plant_cactus`, `plant_orchid`, `plant_fern`, `ceil_tin`, `ceil_neon`, `ceil_sky`, `ceil_mirror`.
Tur 4: bütün karşı duvarlar (`back_*`), bütün pencereler (`win_*`), bütün müzik sistemleri
(`hifi_*`), ve `floor_parquet` / `floor_oak` / `floor_terrazzo`.

## Girmesi için gereken kod işi

1. `Assets/Data/fixtures/fixtures.json` — üç yeni slot: `ceiling`, `floor`, `wall_right_art`
   (+ posterlerin yuvası, yazarın kararına göre). `ceiling` ve `floor` `backdrop` gibi
   davranır (640×360 tam plaka); `wall_right_art` `hangs`.
2. `DiegeticStage` — yeni slotların sıralaması. `ceiling` duvarın ÖNÜNDE (krem alanı
   kapatıyor), `floor` tahtaların önünde, ikisi de mobilyanın arkasında.
3. Her fikstür için `price` / `comfort` / `level` / `stars` — GDD 27 §3.1'in konfor ölçeğine
   göre; merdiven başına sıra `CoreCornersTests.Every_ladder_in_the_catalogue_sells_exactly_the_next_rung`
   tarafından denetleniyor, yani basamaklar boşluksuz olmalı.
4. PNG'ler `Assets/Resources/Fixtures/`'a; yeni dosyalar postprocessor kuralı yüzünden
   **zorla yeniden import** ister (hafıza `urp-2d-lighting-stage`).
