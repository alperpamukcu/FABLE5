# Sahneyi editörden düzenleme (level design)

**Tarih:** 2026-08-07 · Bu belge, oyunu Unity editöründen elle düzenlemenin kurallarını
anlatır. Kod tarafı: `Assets/Scripts/Editor/DebugSceneCreator.cs`.

---

## 1 · Neden özel bir kurulum gerekti

Bu projede sahne kodla kuruluyor: HUD, menü, shaker, servis, tap — hepsi oyun açılırken
`TycoonHud` ve `TycoonServiceFlow` tarafından yaratılıyor, prefab yok. Editörde sürükleyip
bırakacağın bir nesne yoktu; play modunda gördüğün hiyerarşi de oyun kapanınca uçuyordu.

Çözüm: **kodun asla dokunmadığı bir katman** — `StageDressing`.

## 2 · StageDressing — senin katmanın

| Özellik | Değer |
|---|---|
| Nerede | `Assets/Scenes/Main.unity` kökünde, `StageDressing` adlı Canvas |
| Çizim sırası | `sortingOrder = -5` → oda resminin (−10) **üstünde**, HUD'ın (5) **altında** |
| Birim | Odanın kendi ölçüsü: 640×360 referans, yüksekliğe göre ölçekli |
| Runtime | **Hiçbir kod bu katmanı okumaz veya yazmaz** — tamamen senin |

**Create Debug Scene artık güvenli:** araç yalnız kendi iki kökünü (`Main Camera`, `Game`)
siler ve yeniden kurar. Sahnedeki diğer her kök — StageDressing ve elle eklediğin her şey —
her yeniden üretimde **aynen kalır**.

## 3 · Nasıl çalışırsın

1. `Assets/Scenes/Main.unity`'yi aç.
2. Project penceresinde bir veya birkaç PNG/sprite seç
   (`Assets/Resources/Items/`, `Assets/Art/` — hepsi olur).
3. Menüden **LastCall → Add Selected Sprites To Dressing**.
   Her sprite, kendi gerçek piksel boyutunda, StageDressing altına bir `Image` olarak düşer
   (üst üste binmesin diye 12'şer piksel kaydırılarak).
4. Scene görünümünde tut ve **sürükle**. Taşı, ölçekle, döndür, çoğalt (Ctrl+D), sil.
   Hierarchy'de yukarı/aşağı taşımak çizim sırasını değiştirir (üstteki arkada kalır).
5. **Ctrl+S** ile kaydet. Düzenin kalıcı.

Sprite'lar `raycastTarget = false` ile gelir: dekor asla oyunun tıklamasını yemez.

## 4 · Piksel kuralları (görüntü net kalsın)

- **Pozisyonlar tam sayı olsun.** Inspector'da `231.7` değil `232`. Yarım piksel = bulanık kenar.
- **Ölçek 1 kalsın** ya da tam katına çıksın (2, 3). `1.4` gibi değerler piksel sanatını ezer.
- **Boyutu elle değiştirme**, ölçekle büyüt: Width/Height alanları sprite'ın kendi
  boyutundan gelir; onları bozmak resmi gerer.
- Yeni sprite'ları `Assets/Resources/Items/` altına koyarsan import ayarları otomatik doğru
  gelir (point filter, sıkıştırmasız, mipmap yok — `PatronArtPostprocessor` halleder).

## 5 · Sınır: dekor evet, oyun nesneleri hayır

Bu katman **dekor** içindir. Oyunun kendi kurduğu şeylerin (fıçılar, kasa, oturak sırası,
raflar, bardak rafı) yerini buradan değiştiremezsin — onlar her açılışta koddaki
koordinatlardan yeniden kurulur.

**Onları da elle yerleştirmek istersen** bir sonraki adım hazır: sahneye boş "çapa"
nesneleri konur (`KegRow`, `TillSpot`, `SeatLine`, `GlassRack`…), kod pozisyonu sabit
sayıdan değil o çapadan okur. Sen çapayı sürüklersin, oyun ona uyar. İstediğinde açılır.

## 6 · Sorun giderme

| Belirti | Sebep / çözüm |
|---|---|
| Eklediğim sprite oyunda görünmüyor | Sahneyi kaydettin mi? Play, kaydedilmiş sahneyi açar |
| Dekor HUD'ın üstünde çiziliyor | Canvas'ın `sortingOrder`'ı −5 olmalı; elle değiştirilmiş olabilir |
| Kenarlar bulanık | Pozisyon veya ölçek kesirli — tam sayıya yuvarla |
| StageDressing kayboldu | **Create Debug Scene**'i çalıştır: yoksa yeniden yaratır (içi boş olarak) |
| Menü öğesi sprite eklemiyor | Seçim PNG/Sprite değil (klasör veya başka tip seçilmiş olabilir) |
