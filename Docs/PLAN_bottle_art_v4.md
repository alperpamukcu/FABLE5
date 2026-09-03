# PLAN — İçecek Sanatı v4: tek el, tek kamera, üç plaka, iki boyut

**Durum: CANLI PLAN (2026-08-27).** Yazarın briefi: *"Tüm içecek assetleri aynı sanata ve
uyumluluğa ait olmalı, hepsi tekrardan üretilecek."* Kapsam: `base_bar.json`'daki 41 kartın
36'sı (22 cam şişe, 3 bira, 12 meşrubat/karton/teneke — nane ve zeytin kap değildir, dışarıda).
GDD 25'in kamera, palet, parodi-giyim ve kimlik bölümleri bağlayıcı kalır; bu belge 25'i
**boyut, katmanlama, etiket ve üretici** konularında geçersiz kılar. Uygulama günlüğü
`GDD_MEVCUT §9`'a işlenir.

Önce **neyi neden yaptığımız**, sonra nasıl.

---

## 0 · Teşhis: bugünkü 36 kap neden tek el değil

Ölçüldü (2026-08-27, kontak sayfası):

| Kusur | Kanıt |
|---|---|
| Ortak tuval yok | 29 v3 şişenin **29'u farklı boyutta** (35×144 … 64×196); 7 kartonun 4 boyutu var |
| Kamera yok | Kola tenekesi düz bakış, kızılcık kartonu 3/4 sağ, lime kartonu 3/4 sol |
| Kontur tutarsız | JW ve baklava-şişe kalın siyah; Cumpari ve Gibbon kontursuz |
| Sıvı pişmiş | Mason's krem, Grand Mariner amber, Kicker bira sprite'ın İÇİNDE — "boş şişe" kanunu ihlali; doluluk gösterilemez |
| Ekranda küçültülüyor | Mahzen 62 sahne birimi; 80×160 master 62'ye sığdırılınca **0.39 art-px/birim** — pixel grid'i bozan tam olarak bu |
| Metin bozuk | "laca cola" ayna, "cranberry" kırık — üretici yazı yazamıyor (hafıza: pixellab-mcp-constraints) |
| Sürüklenme | v3'ün aracı `create_map_object` **seed de stil referansı da almıyor** (canlı şema, 2026-08-27) — her şişe kendi elini uyduruyor |

Bu yedi kusurun hepsi tek bir şeyin eksikliği: **yazılı bir dil ve onu zorlayan bir boru hattı.**

---

## 1 · On karar (özet)

1. **Master tuval 96×192**, tek üretim, `create_image_pro` ile; her şişe PİLOTA çıpalı
   (`style_image` + `reference_images` + `seed`). Üretim NATİF boyutta (küçültme yok — hafıza
   art-direction-rules 2026-08-18).
2. **Mahzen sprite'ı = master ÷ 3 = 32×64**, TÜRETİLİR, asla ikinci kez üretilmez. 1 art px =
   1 sahne birimi → ekranda tam 2× (720p). `CellarBottleH` 62 → **64**.
3. **El şişesi = master'ın kendisi**, ekranda 2× → `BottleH` 300 → **384** canvas birimi.
   İki sahnede **aynı çizim, aynı piksel boyu, üç kat çözünürlük** — "yakından bakış" tam bu.
4. **Gövde BOŞ ve ETİKETSİZ üretilir.** Etiket boru hattında yapılır: üretilmiş küçük amblem
   + piksel-font marka adı + prosedürel etiket zemini. Böylece kavite (sıvının yeri)
   geometrik olarak türer — v3'ü kıran etiket/kavite ayrıştırma sezgisi ortadan kalkar.
5. **Üç plaka sandviç**: `_back` (kavite, cam tonu %45) → **sıvı çalışma zamanında**
   (kavite maskesi × `UITheme.LiquidColor` × doluluk) → `_front` (cam filmi %30 alfa;
   etiket/kapak/kontur %100). Etiket sıvının ÖNÜNDE, arka cam sıvının ARKASINDA — yazarın
   istediği tam bu ve mimari olarak garantili.
6. **Açık hal türetilir** (`bottle_open_states.py`, kapak dikişinden), üretilmez.
7. **Kontur 1 px, Night[0] `#0D0813`** — ince, palet-içi siyah. (GDD 14 §3 "kendi rampasının
   en koyusu" der; şişelerde tek renk kontur tutarlılık için tercih edildi — pilotta 1 px /
   2 px / kontursuz üç varyant yan yana gösterilir, yazar gözle seçer.)
8. **55 renk paleti** (GDD 14 §3), zincirde kuantize; `pixflux`'ta `color_image` ile ZORLANIR.
9. **İsimler ≤ 14 karakter, tür kelimesiyle biter** (§6 tablosu; sert tavan 15).
10. **Hiçbir asset rapor görülmeden oyuna girmez** (hafıza bottle-art-v3-respec, yazar
    2026-08-05): 3× büyütülmüş HTML kontak sayfası, şişe başına tek kelime tercih.

---

## 2 · Kamera (GDD 25 §1, değişmedi)

Göz nesnenin hafif üstünde, **~17° pitch**: her dairesel kesit üstte genişliğinin %30'u
kadar yüksek bir elips çizer (40 px kapak → 12 px elips); taban kenarı elipsin yarısı kadar
(genişliğin %15'i) AŞAĞI bombeli, asla düz çizgi. Yuvarlak şeylerde yaw yok: düz bakış,
sol-sağ simetrik. Kutu/kartonlarda ön yüz gerçek dikdörtgen, üst yüz sığ bant, SAĞ yan
ön genişliğin ~%12'si; aynı pitch. **Boyut değişir, açı asla.**

v3'te kamera araçtan geliyordu (`map_object` "high top-down"). v4'te `create_image_pro`'nun
`view` parametresi yok; kamera **referans görselden** öğrenilir: `reference_images[0]` =
pilot şişe (kamerası kabul edilmiş), `reference_images[1]` = kamera referansı (kutu +
silindir, GDD 25). Kabul testi aynen: kapak elipsi %30±5, taban bombesi ~%15, düz kesit yok —
take başına ölçülür (`process.py --measure`).

---

## 3 · Boyut matematiği — "iki boyut, tek kimlik" sorununun cevabı

Yazarın endişesi: *"2 adet üretim yaparsak aynı şişe için AI çeşitli sapmalar yapabilir."*
Doğru; bu proje o tuzağı üç kez yaşadı (hafıza open-states-derive). Cevap **üretmemek**:

```
master   96 × 192   art px   → el şişesi, ekranda 2×  = 192 × 384 px  (BottleH 384)
mahzen   32 × 64    art px   = master ÷ 3, TÜRETİLMİŞ → ekranda 2×  =  64 × 128 px  (CellarBottleH 64)
```

- 96 ve 192 ikisi de **4'e bölünür** (PixelLab şartı) ve **3'e bölünür** (türetme şartı).
- İki sahnede de ekran ölçeği **tam 2×** — aynı piksel iriliği, üç kat çözünürlük. Bir
  şişeye yaklaşınca olan tam olarak bu: pikseller büyümez, sayıları artar.
- ÷3 türetmesi **palet-koruyan mod-örnekleme**dir (3×3 bloğun EN SIK rengi; ortalama değil —
  ortalama bulanıklaştırır ve paletten çıkar), ardından silüet temizliği (en büyük blob,
  iğne delikleri kapat), yeniden 1 px kontur ve palet kilidi. Etiket 32×64'te zaten okunmaz;
  tasarım gereği **renk bloğu** olur (zemin + amblemin baskın rengi) — bu bir kayıp değil,
  pixel-art'ın uzak-plan dilidir.
- **Yedek yol**, pilotta yan yana gösterilecek: ÷3 türetmesini `pixflux` img2img'e
  `init_image_strength` yüksek (~700) + `color_image` = palet vererek "temizletmek".
  Sürüklenme riski var; o yüzden YEDEK, ve yalnız yazar türetilmişi beğenmezse.

Mahzen bölmesi 78 satır derin − 16 hava = 62'ydi; 64 için hava 14'e iner (ihmal edilir).
Tezgâhta tin 358 birim; 384'lük şişe tin'den uzun — gerçek bir cin şişesi de öyledir.

---

## 4 · Katmanlama — etiket önde, cam arkada, sıvı ortada

### 4a · Neden gövde ETİKETSİZ üretiliyor
v3 sandviçi (`v3_process.py`) etiketi ve kaviteyi üretilmiş resimden **sezgiyle** ayırmaya
çalıştı (kroma/luma eşikleri). Koyu camda bütün gövdeyi "baskı" saydı, amber sıvıyı görmedi,
üç kök sebeple kırıldı (hafıza v3-front-plates-baked-liquid). Etiket üretime hiç girmezse
ayrıştırılacak bir şey kalmaz:

- **kavite** = silüet içinde, omuz altında, duvar kalınlığı (2 px) içeri çekilmiş satır
  aralıkları — saf geometri, her şişede aynı kural;
- **etiket** = boru hattının SONRADAN, bilinen bir dikdörtgene bastığı katman — tanım
  gereği opak ve tanım gereği önde.

### 4b · Plakalar (master'dan türetilir; mahzen için ÷3 kopyaları ayrıca)
| Plaka | İçerik | Alfa |
|---|---|---|
| `v4_{id}_back` | Yalnız kavite; cam tonunun ~%45 değeri; ortada açık, duvarlarda koyu yatay gradyan (v3 formülü: `cool = cam×0.75 + (150,200,235)×0.25`) | opak |
| *(sıvı)* | Oyun çizer: kavite maskesi, `UITheme.LiquidColor(style,type)`, doluluk = `Remaining/Capacity`, **kavite yüksekliğinin yüzdesi** ("%30 ise %30") | opak |
| `v4_{id}_front` | Kavite pikselleri **%30 alfa** (cam filmi), sol duvar spekülar çizgisi ≥%75, **etiket + kapak + kontur %100** | karışık |
| `v4_{id}_front_open` | Aynı, kapak dikişinden türetilmiş açık boyun | karışık |
| `v4_{id}_mask` | Kavite maskesi (beyaz/şeffaf) — sıvının Filled-Image sprite'ı | binary |

Koyu cam: film alfası %30 SABİT kalır, filmin RENGİ camın tonundan alınır — v3'ün "koyu
camda film inmiyor" hatası eşik değil kural olur.

### 4c · Çalışma zamanı
- **El (uGUI, tezgâh):** `BottleArt` yeniden kurulur (2026-08-07'de silinen sınıfın minimal
  hali): back `Image` → liquid `Image` (`type=Filled, Vertical, Bottom`, sprite=mask,
  color=LiquidColor, `fillAmount`=doluluk) → front `Image`. `BottleFill` (stencil hilesi)
  emekli olur — artık gerçek film var.
- **Mahzen (world-space `SpriteRenderer`):** back SR → `SpriteMask`(mask sprite) altında
  **1×1 beyaz quad** SR (kavite dikdörtgeni × doluluk kadar ölçekli; düz renk olduğu için
  ölçekleme sanatı bozmaz), `Sprite-Lit-Default` ile IŞIK ALIR → front SR. Shader yok.
- Yükleme: `ItemArt.Bottle` → `v4_{id}_front` (flat yerine sandviç); `bot_{id}` mühürlüler
  tek sprite kalır. Sıvı yalnız `Sealed` kümesi dışında.

---

## 5 · Etiket sistemi

Etiket üç katmandan yapılır ve **hepsi deterministik**:
1. **Zemin**: etiket dikdörtgeni gövdenin en geniş bandında, genişliğin %62'si × gövde
   boyunun %26'sı (aile başına ayar §7); kesik köşe; markanın iki paleti (zemin + şerit);
   1 px kenar = zemin rampasının [0]'ı.
2. **Amblem**: `create_image_pro` 32×32, `no text`, pilota stil-çıpalı, marka başına
   ("stylised crane bird", "a hat", "a bat", "a gibbon face"…); 55'e kuantize; zeminde üst
   yarıda ortalı. (≤170 px'te pro **çok aday** döndürür — ucuz ve seçilebilir.)
3. **Marka adı**: kendi **3×5 piksel-font**umuz (`fontpx.py`, A-Z 0-9 ' &), sığıyorsa 2×
   (glyph 6×10), sığmıyorsa 1×; renk = şerit rampasının [4]'ü; **yalnız marka kelimesi**
   ("SMIRKOFF") — tür kelimesi raf etiketinde/hover kartında. Üretici yazı yazamadığı için
   yazı hiçbir zaman üreticiden istenmez (hafıza).

Mahzen (÷3) etiketinde amblem ve yazı bloklaşır; zemin ve şerit rengi kalır. İstenen bu.

---

## 6 · İsim tablosu (kural: ≤14, tür kelimesiyle biter; sert tavan 15)

Tür kelimesi haritası: vodka→Vodka, gin→Gin, rum→Rum, **bourbon→Whiskey** (oyuncunun
tanıdığı kelime; kod `styleWord` haritasıyla test eder), tequila→Tequila, amaro→Amaro,
vermouth→Vermouth, triple_sec/coffee_liqueur→Liqueur, lager→Lager, stout→Stout, pale_ale→Ale,
energy→Energy, cola→Cola, soda→Soda, tonic→Tonic, syrup→Syrup, ginger→Ginger.
Meyve suları için tür = içeceğin kendisi (Lemonade, Limeade…) — yazarın onayına açık.

| id | Bugün | Yeni | n |
|---|---|---|---|
| vodka_astra | Smirkoff Vodka | Smirkoff Vodka | 14 |
| vodka_vor | Absolve Vodka | Absolve Vodka | 13 |
| vodka_leonid | Grey Gander Vodka | **Gander Vodka** | 12 |
| vodka_okhta | White Whale Vodka | **Whale Vodka** | 11 |
| gin_boothby | Garden's Gin | Garden's Gin | 12 |
| gin_juniper_crow | Leafeater Gin | Leafeater Gin | 13 |
| gin_thornwood | Hendrake's Gin | Hendrake's Gin | 14 |
| gin_veilcrest | Gibbon 48 Gin | Gibbon 48 Gin | 13 |
| rum_cane_coral | White Bat Rum | White Bat Rum | 13 |
| rum_tidewater | Admiral Morgan Rum | **Admiral Rum** | 11 |
| rum_windward | Krakatoa Rum | Krakatoa Rum | 12 |
| rum_reina_del_mar | Maliboo Rum | Maliboo Rum | 11 |
| bourbon_redline | John Wanderer Whiskey | **Walker Whiskey** | 14 |
| bourbon_old_harrow | Jack Spaniel's Whiskey | **Spaniel Whiskey** | 15 |
| bourbon_ashfall | Mason's Mark Whiskey | **Mason's Whiskey** | 15 |
| bourbon_hollow_oak | Van Wrinkle 23 Whiskey | **Wrinkle Whiskey** | 15 |
| tequila_sonora | Jose Cuerdo Tequila | **Cuerdo Tequila** | 14 |
| tequila_alta_luna | 1810 Tequila | 1810 Tequila | 12 |
| tequila_sol_viejo | Don Julep Añejo Tequila | **Julep Tequila** | 13 |
| tequila_cielo_rojo | Azulejo Tequila | Azulejo Tequila | 15 |
| amaro_notte | Cumpari Amaro | Cumpari Amaro | 13 |
| vermouth_velvet | Canzone Vermouth | **Velvet Vermouth** | 15 |
| liqueur_delia | Grand Mariner Triple Sec | **Mariner Liqueur** | 15 |
| liqueur_kafa | Koala Coffee Liqueur | **Koala Liqueur** | 13 |
| energy_volt | Blue Ox | **Blue Ox Energy** | 14 |
| beer_marigold | Brass Pale Ale | **Brass Ale** | 9 |
| beer_collier | Goodness Stout | Goodness Stout | 14 |
| beer_kestrel | Krona Lager | Krona Lager | 11 |
| cranberry_north | Cranberry | **Cranberry Juice** | 15 |
| pineapple_isla | Isla Piña | **Isla Pineapple** | 14 |
| *(diğer meşrubat)* | Loca Cola, Klara Soda, Quinn's Tonic, House Syrup, Kicker Ginger, Lemonade, Limeade, Orange Juice | değişmez | ≤13 |

Bir EditMode testi (`BottleNameRuleTests`) kuralı sabitler: uzunluk ≤15 ve son kelime =
`styleWord[style]` (meyve suları muaf listede).

---

## 7 · Silüet aileleri (kimlik silüette; GDD 25 §4'ten, oran sayıyla)

| Aile | Oran (boy/en) | Silüet | Kapak | Etiket bandı |
|---|---|---|---|---|
| vodka | 2.5 | uzun düz omuz, dar boyun | vidalı, gümüş/mavi | orta, dikey dar |
| gin | 2.1 | bodur, geniş omuz (London dry) | mantar+kapsül | geniş, büyük |
| rum | 2.2 | yuvarlak omuz, hafif şişkin gövde | mantar | orta |
| whiskey | 1.9–2.0 | kare/geniş gövde, kısa boyun; **T3 mum damlası (renk/kesim değişik)** | mantar+mum | alçak-geniş |
| tequila | 2.3 | uzun boyun, dar taban / T4 el yapımı cam | mantar, ip | dar, alçak |
| amaro/vermouth/likör | 2.4 | ince, yüksek omuz | vidalı | dikey ince |
| bira | 2.6 | uzun boyun kahverengi cam, mühürlü (sıvı YOK) | taç kapak | boyun bandı |
| teneke (kola, enerji) | 2.0 | silindir, üst halka elipsi | çekme halka | tam sarma |
| karton (meyve suyu) | 2.0 | kutu, üst üçgen çatı, sağ yan %12 | vidalı ağız | ön yüz |
| şişe-meşrubat (tonik, soda, zencefil, şurup) | 2.4 | cam, ince | vidalı | orta |

Tier merdiveni bir ailede aynı silüeti korur; **T1→T4 farkı giyimdir** (etiket zenginliği,
kapak malzemesi, cam tonu) — silüet değil. Böylece aynı raf aynı elden okunur.

---

## 8 · Meşrubat / karton / teneke hattı (ayrı boru, aynı dil)

Yazar: *"onlar için ayrı pipeline üretebiliriz ama aynı sanat dilinde olmalılar."*
Aynı: kamera, tuval (96×192 içinde kendi oranında), kontur, palet, etiket sistemi, pilot çıpası,
rapor kapısı. Farklı: **mühürlü** — kavite yok, plaka yok, tek sprite `bot_{id}` + türetilmiş
`_open` (dökme deliği: teneke halkası kalkık / karton ağzı açık; `carton_open_states.py`).
Sıvı gösterilmez (hafıza: iç plaka verilirse gri blok görünür). Hover kartı doluluğu sayıyla
söyler, bugünkü gibi.

---

## 9 · Boru hattı — `Tools/v4_bottles/` (dondurulmuş)

```
brief.py     dondurulmuş sözcükler; per-kart LOOK cümlesi + oran; EMPTY & NO LABEL blokları
             tam güçte; build() dışından üretim YOK (GDD 25 §5a dersi: elle brief üç kez kırdı)
gen.py       create_image_pro 96×192, seed×3, style_image=pilot, reference_images=[pilot, kamera
             referansı], style_copy=[color_palette, outline, detail, shading]; amblemler 32×32;
             get_balance ÖNCE; kuyruk+poll; ham takes → Tools/v4_bottles/raw/{id}/
process.py   trim → 55'e kuantize → 1px Night[0] kontur (uniform_outline, idempotent) →
             ölçüm (elips/bombe) → kavite → back/mask/front → etiket bas → open (dikiş) →
             ÷3 mahzen plakaları → audit (bkz. kapılar) → staging/ (oyuna DEĞİL)
report.py    HTML: kart başına adaylar 1×/3×, her aday %25/%60/%95 doluluk ile, el ve mahzen
             boyutunda, kontur varyantları; yazar tek kelime seçer → picks.json
ship.py      picks.json → Assets/Resources/Items/ (v4_*.png + bot_*.png) + eski v3/bot sil
fontpx.py    3×5 piksel-font; palette.py 55 renk + color_image PNG
```

**Kanıt kapıları** (bir take geçemezse rapora "reddedildi: <sebep>" olarak girer):
- boş: kavitede sıvı satırı yok (kroma > cam+16 ∧ luma < cam−20 satır sayısı = 0)
- etiketsiz: gövde bandında baskı yok (kavite dışı opak piksel yalnız duvar+kapak)
- kamera: kapak elipsi %30±5, taban bombesi ≥%10
- kontur: tek halka, 1 px, tamamı Night[0]
- palet: 55 dışı piksel 0; alfa binary
- boyut: tam 96×192 / 32×64 (yeniden ölçekleme YASAK — sığmayan reddedilir)
- idempotens: process iki kez → hash aynı (hafıza sprite-pipeline-idempotence)
- **sıvı kanıtı**: kırmızı ve mavi sıvıyla kompozit alınır; etiket pikselleri iki kompozitte
  birebir aynı olmalı (etiket sıvının önünde), kavite pikselleri farklı olmalı (sıvı görünüyor)

---

## 10 · Çalışma zamanı değişiklikleri (pilot onayından SONRA)

1. `BottleArt` (uGUI sandviç) geri gelir — minimal, `Filled` sıvı; `BottleFill` emekli.
2. `DiegeticStage`: `CellarBottleH` 64; mahzen şişesi 3 SR + SpriteMask quad; doluluk
   `TycoonRun` stoktan (`Remaining/Capacity`), gün içinde canlı.
3. `TycoonServiceFlow.Shaker`: `BottleH` 384; `_pourBottleBody` yerine sandviç.
4. `ItemArt.Bottle/BottleOpen`: `v4_{id}_front(_open)` + plaka erişimcileri; `bot_` aynı.
5. `base_bar.json` isimler (§6) + `BottleNameRuleTests`.
6. Look-test baseline'ları yeniden kutsanır (mahzen ve tezgâh resmi değişiyor).

---

## 11 · Sıra

1. **Pilot = Smirkoff Vodka** (GDD 25 §5 kararı): 3 seed × pro + 1 map_object kontrol adayı;
   kontur 1/2/0 varyantları; ÷3 türetilmiş vs pixflux-temizlenmiş mahzen yan yana;
   %25/60/95 doluluk kompozitleri. → HTML → **yazar seçer** ve düzeltme notu verir.
2. Seçilen pilot = **çıpa**. Merdiven: vodka×3 → gin×4 → rum×4 → whiskey×4 → tequila×4 →
   tekiller×4 → meşrubat×12 → bira×3. Her parti rapor, her parti tercih.
3. Çalışma zamanı (§10) pilot onayıyla paralel kurulur; ilk parti oyuna girerken hazırdır.
4. Eski `v3_*_flat` ve `bot_*` sprite'ları, yerine geleni oyunda ölçülünce silinir.

Maliyet: pro ≈ 20–40 üretim/çağrı; 36 kap × 3 seed ≈ 3.2–4.3k + amblemler ≈ 0.5k. Kota
10.000 (2026-09-18'de yenilenir). Her partiden önce `get_balance`.
