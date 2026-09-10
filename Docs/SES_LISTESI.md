# LAST CALL — Ses Envanteri ve İhtiyaç Listesi

*2026-09-09'da koddan çıkarıldı (yazarın isteği: "oyunda kullandığımız ses dosyalarının ve
ihtiyacımız olan ses efekti müzik vs. listesini detaylıca çıkar").*

---

## 0 · Sistem nasıl çalışıyor

| | |
|---|---|
| **Klasör** | `Assets/Resources/Audio/` — başka yere konan dosya yüklenmez |
| **Çağırma** | `Sfx.Play("dosya_adi")` — uzantısız, isimle. Dosya yoksa **sessizlik** (hata değil), yani ses ses eklenebilir |
| **Format** | 44.1 kHz, **mono**, 16-bit WAV. Stereo/48k da çalışır ama bank tek düzende |
| **Kanal sayısı** | 6 sesli one-shot havuzu (round-robin) + 1 **ortam** (loop) + 1 **eylem loop'u** (dökme/çalkalama) |
| **Ses seviyesi** | `Sound.Volume` (PlayerPrefs, varsayılan 0.8) × çağrıdaki `volume` |
| **Perde oynatması** | Deterministik sayaç (rastgele değil — evin kuralı) |
| **Şu anki kaynak** | 73 klibin **tamamı sentetik** (`Tools/sfx_bank.py` + `sfx_dsp.py` ile üretildi). Yani hepsi yer tutucu; gerçekleriyle değiştirilebilir |
| **v2 seslendirme** | 2026-09-10'da banka **yeniden basıldı** (yazar: "sesleri de tekrarda sen üret ... cozy seslere yakın"). Üç şey değişti ve üçü de ÖLÇÜMLE seçildi — bkz. `sfx_dsp` §THE ROOM: **(1) oda** — her klip artık barın içinde çalıyor (sentetik dürtü yanıtı + evrişim), ve nerede çaldığı klip başına yazılı (`sfx_bank.SPACE`): UI parmağın altında, KURU; bardak tezgahta; kapı odanın karşısında. Ölçü: kuyruk enerjisi medyanı 0.159 → 0.194, `glass_down` 0.03 → 0.16, `door` 0.01 → 0.15. **(2) vurulan nesnenin fiziği** — modal partial'lar artık sıfırıncı örnekte tam genlikte başlamıyor (temas enerjiyi devrediyor, yükseğe daha hızlı) ve tizler önce ölüyor; ikisi de zil ile nesne arasındaki fark. **(3) ton** — 3 kHz'de −2.2 dB (sertlik bandı), 320 Hz'de +2 dB gövde, 6.5 kHz üstü −3.5 dB hava. Centroid medyanı 1626 → 1447 Hz. Banka 6.5 → 7.0 MB. Demo: `Tools/sfx_demo.py` |
| **Değiştirme** | Aynı isimle WAV'ı klasöre koymak yeter, kod değişmez |

**Uzunluk kuralı:** UI tıklamaları 40–80 ms, nesne sesleri 150–400 ms, olay sesleri
0.5–1.5 sn, loop'lar dikişsiz (0.3–1.0 sn), ortam yatağı 30 sn+.
Klipler **başı-sonu sıfırda** olmalı (tık/pop olmasın).

---

## 1 · Şu an oyunda olan 73 klip

### 1.1 Oda ve gece döngüsü

| Dosya | Süre | Nerede çalıyor | İstenen karakter |
|---|---|---|---|
| `ambience_loop` | 31.9 s | Sürekli; bench açıkken kısılıyor | Bar yatağı: uğultu, uzak konuşma, cam şıngırtısı. **Gerçek bir kalabalık kaydı en çok bunu iyileştirir** |
| `day_open` | 0.85 s | Perde kalkarken, gece başlarken | Kapıyı açma + neon uyanma |
| `day_close` | 0.90 s | Gece biterken | Kepenk, ışıkların sönmesi |
| `bar_closed` | 1.60 s | Kapanış anı | Ağır kapı + son nefes |
| `last_call_bell` | 1.50 s | Son sipariş çanı | Pirinç bar zili, tek vuruş |
| `curtain` | 0.85 s | Perde geçişi | Kadife sürtünme |
| `synth_swell` | 2.40 s | Sahne vurgusu (DiegeticStage) | Yükselen synth pad |
| `door` | 0.55 s | Müşteri kapıdan girince | Kapı + sokak sızıntısı |

### 1.2 Müşteri

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `stool_take` | 0.70 s | Tabureye oturma | Tabure gıcırtısı + ağırlık |
| `order_ready` | 0.34 s | Sipariş balonu çıkınca | Kısa, nazik bildirim |
| `another_round` | 1.20 s | "Bir tur daha" | Neşeli iki nota |
| `cheer_sfx` | 0.70 s | Memnun müşteri (memnuniyet ≥ 0.55) | Küçük alkış/sevinç |
| `upset_sfx` | 0.60 s | Küs müşteri çıkışı | Homurdanma / sandalye itme |
| `serve_clink` | 0.55 s | Bardak müşteriye gidince | Cam tokuşması |
| `patience_warn` | 0.22 s | **Bağlanmadı** — sabır bitmek üzereyken çalmalı | Tik / huzursuz vuruş |

### 1.3 Kimlik ve kapı (GDD 28)

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `id_card` | 0.62 s | Kimlik açılırken | Kart çekme, laminat |
| `id_card_away` | 0.20 s | Kimlik kapanırken | Kartı bırakma |
| `stamp` | 0.55 s | Damga (market + gün sonu) | Kauçuk damga, tok |
| `deny` | 0.22 s | Reddedilen her işlem (7 yer) | Kısa, kuru "hayır" |

### 1.4 Mahzen ve şişeler

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `cellar_open` | 1.30 s | Mahzen çekmecesi açılırken | Ahşap ray, sürtünme |
| `cellar_close` | 1.15 s | Kapanırken | Aynısının kapanışı |
| `bottle_open` | 0.30 s | Şişe kapağı açılınca | Vidalı kapak / mantar |
| `bottle_set` | 0.26 s | Şişe tezgaha konunca | Cam + tahta |
| `cap_on` | 0.34 s | Tin kapağı kapanınca (4 yer) | Metal oturması |

### 1.5 Shaker tezgahı

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `pour_tin` | 1.14 s | **Loop** — tine dökerken | Sıvı akışı, doluluğa göre perde |
| `shake_loop` | 0.34 s | **Loop** — çalkalarken | Buz + metal, enerjiye göre hız |
| `stir_loop` | 0.56 s | **Loop** — karıştırırken | Bar kaşığı, cam içi |
| `stir_commit` | 0.28 s | Karıştırma bitince | Kaşığı çekme |
| `tin_tip` | 0.20 s | Tin eğilirken (4 yer) | Metal kayma |
| `tin_set_down` | 0.40 s | Tin bırakılırken | Metal tezgah |
| `blowout` | 0.90 s | Aşırı çalkalama / patlama | Kapak fırlaması |
| `ice_drop` | 0.34 s | Buz eklerken | Küpler cam/metale |
| `tap_handle` | 0.18 s | Kol/kapak (2 yer) | Küçük mekanik klik |

### 1.6 Bardak tezgahı ve servis

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `pour_glass` | 1.14 s | **Loop** — bardağa dökerken | Sıvı, daha ince |
| `glass_pickup` | 0.18 s | Bardağı alırken | Cam kaldırma |
| `glass_down` | 0.30 s | Bardağı bırakırken | Cam + tahta |
| `serve_it` | 0.30 s | Servis onayı | Kısa olumlu |
| `drain` | 0.75 s | Lavaboya dökerken (4 yer) | Gider |

### 1.7 Fıçı / tap

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `tap_pull` | 1.34 s | **Loop** — bira akarken | Basınçlı akış + köpük |
| `tap_water` | 0.54 s | **Loop** — lavabo suyu | İnce su |
| `pour_floor` | 1.04 s | **Loop** — yere dökülürken | Splash, dağınık |
| `pour_cutoff` | 0.26 s | Akış kesilince | Musluk kapanışı |
| `head_settle` | 0.70 s | Köpük oturunca | Kabarcık çıtırtısı |
| `verdict_good` | 0.55 s | İyi pint | Olumlu üç nota |
| `verdict_flat` | 0.48 s | Köpüksüz | Nötr |
| `verdict_bad` | 0.50 s | Kötü pint | Olumsuz |

### 1.8 Garnish, rim, temizlik

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `garnish` | 0.16 s | Garnish eklerken | Küçük yaprak/dilim |
| `grain_pinch` | 0.14 s | Tuz/şeker tutamı | Kum serpme |
| `rim_turn` | 0.84 s | **Loop** — bardak kenarı çevirirken | Cam üzerinde tane |
| `rim_done` | 0.30 s | Rim bitince (2 yer) | Kısa onay |
| `bowl_down` | 0.26 s | Kase bırakma | Seramik |
| `dish_down` | 0.18 s | Tabak bırakma (2 yer) | Küçük seramik |

### 1.9 Menü kitabı

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `book_open` | 0.42 s | Kitap açılınca | Deri + kağıt |
| `book_close` | 0.32 s | Kapanınca | Tok kapanış |
| `page_turn` | 0.46 s | Sayfa çevirme (2 yer) | Tek kağıt |

### 1.10 Para, market, gün sonu

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `cash` | 0.80 s | Kasa (3 yer) | Çekmece + zil |
| `coin` | 0.45 s | Bozuk para | Metal tıngırtı |
| `buy` | 0.42 s | Markette satın alma | Onay + kasa |
| `bill_slip` | 0.65 s | Fatura fişi | Kağıt çekme |
| `printer_feed` | 0.49 s | Yazıcı besleme | Tırtıklı ilerleme |
| `debt_alarm` | 1.10 s | Borç uyarısı | Alçak alarm |
| `star_earn` | 0.75 s | Yıldız kazanma | Parlak yükseliş |
| `level_up` | 0.95 s | Seviye/rütbe | Daha büyük yükseliş |
| `screen_on` | 0.30 s | Ekran açılışı (2 yer) | CRT uyanma |
| `screen_off` | 0.28 s | Ekran kapanışı | CRT sönme |

### 1.11 Arayüz

| Dosya | Süre | Nerede | Karakter |
|---|---|---|---|
| `click` | 0.04 s | Her tuş (11 yer) | Çok kısa, tok |
| `key_press` | 0.08 s | Fiziksel tuş plakaları (6 yer) | Mekanik klavye |
| `hover` | 0.06 s | Üstüne gelme | Neredeyse duyulmayan |
| `whoosh` | 0.34 s | Sahne kayması | Hava geçişi |

### 1.12 Bağlanmamış (dosya var, kod çağırmıyor)

`voice_greet` (0.25 s), `voice_order` (0.44 s), `voice_happy` (0.44 s),
`voice_upset` (0.35 s), `patience_warn` (0.22 s).

Bunlar müşteri konuşma balonlarına takılmak üzere üretilmiş ama hiçbir yerden
çağrılmıyor. **Karar gerekiyor:** karakter başına kısa "mırıldanma" (Animal Crossing /
Undertale usulü) istiyor muyuz? İstiyorsak kişi başına 3–4 heceli sete ihtiyaç var,
istemiyorsak bu beş dosya silinir.

---

## 2 · İhtiyaç listesi — bulunacaklar

### 2.1 MÜZİK (oyunda hiç yok — en büyük eksik)

| İhtiyaç | Uzunluk | Nerede | Not |
|---|---|---|---|
| **Ana tema / menü** | 60–120 s loop | Açılış, ayarlar | Synthwave, 80'ler bar, orta tempo |
| **Gece yatağı A** (sakin) | 90–180 s loop | Gece başı, az müşteri | Ritim hafif, bas yürüyüşü |
| **Gece yatağı B** (yoğun) | 90–180 s loop | Kalabalık saatler | Aynı tondan, daha ritmik — A ile aynı tempoda olsun ki geçiş dikişsiz olsun |
| **Son sipariş** | 60–90 s loop | Last call sonrası | Daha az enstrüman, yalnız |
| **Gün sonu / hesap** | 30–60 s | Fatura ekranı | Sakin, sayılara eşlik eden |
| **Hikâye anı** (26. modül) | 45–90 s | Son müşteri sahnesi | Tek enstrüman, duygusal |
| **Zafer/rütbe atlama stinger'ı** | 3–5 s | Yıldız/seviye | Tek seferlik |

> Teknik: müzik için ayrı bir kanal gerekiyor (şu an sadece `ambience_loop` var).
> Klipler `Assets/Resources/Audio/` içine `music_*` adıyla konursa kanalı bağlarım.

### 2.2 Ortam yatakları (mevcut tek yatağın yerine/yanına)

| İhtiyaç | Uzunluk | Not |
|---|---|---|
| `ambience_loop` **gerçeği** | 60 s+ | Kalabalık uğultusu, bardak, uzak kahkaha — şu anki sentetik |
| Sokak/yağmur (pencere) | 60 s+ | Arka plan camdan sızan şehir |
| Boş bar (kapanış) | 30 s+ | Buzdolabı uğultusu, neon vızıltısı |

### 2.3 Kalite yükseltme önceliği (mevcut sentetikler yerine gerçek kayıt)

En çok duyulan 10 klip — **önce bunlar** değişsin:

1. `click` (11 çağrı, her tıklama)
2. `ambience_loop` (sürekli)
3. `pour_tin` / `pour_glass` (her içki)
4. `shake_loop`
5. `cash` (her satış)
6. `serve_clink`
7. `bottle_set` / `glass_down`
8. `id_card`
9. `stool_take`
10. `cellar_open` / `cellar_close`

### 2.4 Henüz sesi olmayan eylemler (yeni dosya gerekiyor)

| Önerilen ad | Nerede olacak | Karakter |
|---|---|---|
| `bin_drop` | Çöpe atma | Metal kapak + sıvı |
| `cloth_wipe` | Bez ile silme | Kumaş sürtünme, 2–3 varyant |
| `sink_fill` | Lavabo dolarken | Su birikmesi |
| `fixture_install` | Market'ten alınan mobilya yerleşince | Montaj, tok |
| `kick_out` | Müşteri kovulunca | Sert kapı |
| `paper_stamp_fail` | Sahte kimlik yakalanınca | Kuru, olumsuz damga |
| `snack_crunch` | Atıştırmalık | Çıtırtı |
| `neon_buzz` | Neon tabela (döngü) | Elektrik vızıltısı |
| `till_open` | Kasa çekmecesi ayrı | `cash`ten ayrılırsa daha iyi |
| `ui_error` | Yasak işlem | `deny`den daha yumuşak bir varyant |

### 2.5 Varyant ihtiyacı (tekdüzelik kırmak için)

Aynı sesin 3 varyantı olursa oyun çok daha az "yapay" duyulur. Öncelik:
`click`, `bottle_set`, `glass_down`, `serve_clink`, `dish_down`, `page_turn`.
Dosya adı `click_1/2/3` olursa rastgele değil **sırayla** çalacak şekilde bağlarım
(evin determinizm kuralı).

---

## 3 · Nasıl teslim edilir

1. WAV dosyalarını `Assets/Resources/Audio/` içine, **yukarıdaki adlarla** koy.
2. Yeni ad (müzik, varyant, yeni eylem) getirirsen bana söyle — kodda bağlarım.
3. Lisans: ticari kullanıma uygun olsun (CC0 en rahatı). Kaynağı bu dosyaya not düşerim.
