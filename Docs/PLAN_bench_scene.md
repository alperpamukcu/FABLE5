# PLAN — Tezgâh sahnesi (built ekranı): araştırma, ilkeler, yerleşim, fikirler

_2026-09-16. Yazarın isteği: "bu sahnenin tasarımı çok önemli oyuncu çok fazla bu sahneyi görecek, bu tarz
yemek/içecek built etmeli oyunlardaki sahne tasarımlarını incele detayları öğren ve ona göre bu sahnede her nesneyi
yerleştir, bu öğrenim sırasında built için yeni geliştirmeler fikirler öğrenirsen değerlendirebiliriz." Uygulanan
düzen GDD_MEVCUT §9.73'te; bu dosya araştırmayı, ilkeleri ve henüz yapılmamış fikirleri taşır._

## 1 · İncelenen oyunlar

| Oyun | Sahnede ne var | Bize dersi |
|---|---|---|
| **VA-11 Hall-A** | Beş malzeme tuşu ve sayaçları, shaker, yan panelde tarif; buz / yaşlandır / karıştır. Karıştırma bir zamanlama: shaker yavaşken durdurursan "mixed", 5 sn geçince "blended". İçki bir diyalog seçeneği — doğru içki müşterinin başka hikâyesini açar. | Karıştırmanın bir **karar anı** olması; tarifin yapım sırasında yanda durması; asıl geri bildirimin müşteriden gelmesi. |
| **Coffee Talk** | Skeuomorfik dolap + makine; malzeme dolaptan makineye taşınır; süre yok; hata ucuz (reset); tarif önce baristanın kendi cümlesi, sonra gizlenir; pasif tuşlar sönük; hover'da tek zıplama. | "Verimli menü" yerine hissi koruyan araç; yönergenin mekânın/karakterin ağzından gelmesi; eğitim tekerleklerinin zamanla kalkması. |
| **Papa's (Freezeria Mix Station)** | İbre yeşil bölgeye girince durdurulan zamanlama barı; hedef miktar sipariş fişinde; hızlandırıcı ve alarm yükseltmeleri. | Tek bakışta okunan "yeterli" bölgesi; yükseltmenin aleti hızlandırması (bizde `WorkSpeed`). |
| **Potion Craft** | Kepçe, körük, havan fiziksel araç; fare jestiyle karıştırma; ilerleme haritadaki işaretçi; yazı yok denecek kadar az. | Araç = fiziksel jest; ilerleme yazıyla değil nesneyle söylenir. |
| **BarSim** | Gerçek bar jestleri; miktar, teknik ve süre puanlanıp eleştirilir. | Süre ve tekniği de değerlendirmek (fazla çalkalama = sulanma). |
| **Bartender: The Right Mix** | Basılı tut = dök; shake tuşu; fazla çalkalarsan patlama. | Aşırının komik, görünür cezası (gazlı içkinin patlaması bizde var). |
| **Yo, Bartender (alt.ctrl GDC)** | Gerçek şişelerle dökme; tarif kitabı okumak oynamaktan uzun sürünce tarifler sadeleştirildi, zorluk katmanları. | Şişeyi bardağın üstünde tutmak zorunda olmak gerçek hissettirir; tarif okuma yükü düşük tutulmalı. |

Kaynaklar: Game Developer — "Brewing meaningful UX in Coffee Talk", "Deep dive: cozy experience in Coffee Talk",
"A breakdown of the drink choice mechanic in VA-11 HALL-A", "Alt.Ctrl.GDC: Yo, Bartender"; Steam topluluğu — VA-11
mix/blend, Papa's Freezeria rehberi; Potion Craft wiki (Crafting); barsim.com; crazygamesonline Bartender rehberi.

## 2 · Çıkarılan ilkeler → bu tezgâhta karşılığı

| İlke | Tezgâhta |
|---|---|
| Sahne bir çalışma yüzeyidir, panel değil | Yönergeler taşa oyulu plakada; bar nesnesi yok; her yazı tezgâhın parçası. |
| Bölgeler soldan sağa iş akışını verir | Yönerge (plaka) → aletler (kapak, kaşık/peçete) → iş (shaker) → kaynak (şişe) → ölçü. |
| İlerleme nesnede okunur | Buzlanan shaker, ölçekte karışan katmanlar, yanan merdiven basamakları. |
| "Yeterli" tek bakışta | Plakada "ENOUGH AT 72%", geçince yeşil; ölçekte tek renk. |
| Yönerge kısa ve diegetik | Adım şeridi + tek cümle; merdivenin altındaki "LIFT HIGHER · POURS FASTER". |
| Hata ucuz, ceza görünür | ÇÖP tuşu ve ücreti; gazlı içkinin patlaması. |
| Hover/tutuş dili tutarlı | Her prop aynı parlama/kaldırma dilinde (HoverGlow, değişmedi). |
| Üst barla çakışma yok | Musluk tezgâhının başlığı plakaya indi. |

## 3 · Yerleşim (1280×720, ölçüldü)

Plaka x 16–480, y 349–413 (raydan asılı) · kapak (çizim) x 45–215 · kaşık/peçete x 240–360 · shaker x 464–696
(kapaklıyken ortada 524–756) · şişe x 850–1030 · merdiven x 850–1030, y 560–630 · ölçü x 1057–1193 · BARA DÖN
sol alt · ÇÖP sağ alt · SERVİS ET (bardak tezgâhı) ÇÖP'ün solu. Hiçbir prop plakayı ya da merdiveni kesmiyor;
kapağın dinlenme dikdörtgeninin merkezi BARA DÖN'ün sütununun dışında (test bunu yakaladı: tuşun üstüne düşen bir
kapak merkezi kapağı tutulamaz yapıyor).

## 4 · Göstergede değerlendirilen üç yön

- **Buz + karışım (seçilen).** Gerçek shaker davranışı; bar nesnesi yok; iki fiil için tek dil.
- **Kadran.** Tezgâh önüne gömülü yarım kadran (manometre gibi), ibre yeşil bölgeye. Okunaklı, ama ortaya gelen
  kapaklı shaker'dan hep uzakta boş bir yer ister.
- **Neon ray.** Arka kenardaki macenta ray enerjiyle soldan sağa yanar. Çok diegetik; şişe boynu ve kapaklı shaker
  rayı kesiyor. Mekanik tek noktadan besleniyor (`ShowWorkMeter`), üçü de bir günlük iş.

## 5 · Oynanışa katılabilecek fikirler (yapılmadı — yazarın kararına)

1. **Sulanma: çalkalamayı karar yap.** Yeterli çizgisinin (72%) üstünde bir "fazla" bandı; orada ısrar sulu bir
   içki verir, müşteri "sulu" der (Papa's'ın yeşil bölgesi + BarSim'in süre eleştirisi). Buzla anlam kazanır.
2. **Buz gerçek malzeme.** Buzsuz shaker buzlanmaz ve ılık kalır; buzlu soğur ve sulanır; bardaktaki buz erir,
   bekleyen içki bozulur. Buz kovası tezgâhta zaten var.
3. **Jigger (ölçek) yükseltmesi.** Önce jigger'a dök (tam 25/50 ml), sonra tenekeye — acemi için kesinlik; ustalık
   serbest dökümde (merdiven).
4. **Rayda tarif fişi.** Sayfası alınmış tarifin oranları plakanın yanında bir fişte, yapım boyunca göz önünde;
   kısa olmalı (Yo Bartender dersi).
5. **Eğitim tekerlekleri kalkar.** Aynı içki N kez yapıldıktan sonra adım şeridi söner, okuma satırı kalır.
6. **İyi çalkalamanın görünür ödülü.** Buzlu shaker'dan servis edilen içkiye bahşiş katsayısı.
7. **Ritim.** Çalkalama enerjisi ham yoldan değil düzenli gidiş-gelişten; deneme ister.

## 6 · Kaşık gibi araç kilitleri — yıldız merdiveninde oynanış (yapılmadı — yazarın kararına)

_2026-09-16. Yazar: "kaşık gibi oynanışı geliştirecek neler eklenebilir içerik değil oynanış için." Kaşık artık
ilk yıldızın dersi (GDD_MEVCUT §9.75): karıştırılan ilk sayfa alınınca tezgâha gelir. Aynı kalıp — **bir sayfa bir
araç getirir, araç yeni bir el hareketi ve yeni bir hata türü açar** — her basamağa uygulanabilir. Her satırda:
ne getirir, hangi el hareketi, hangi hata, hangi sayfa/basamak, mevcut koda nasıl bağlanır._

| ★ | Araç | El hareketi (tezgâhta) | Yeni hata / ustalık | Getiren sayfa | Bağlantı |
|---|---|---|---|---|---|
| 0 | Shaker + kapak (var) | Kapat, çalkala, dök | Az çalkalama (72% altı), fizzli tenekeyi patlatma | Gin Sour (rank 3) | `IsShaken`, `ShakeBlowsTheTin` |
| 1 | **Bar kaşığı (yapıldı)** | Açık tenekede daireler | Yanlış yöntem (çalkalanmış Martini = 0 ustalık) | Black Russian (9) | `SpoonUnlocked`, `MethodScore` |
| 1 | **Jigger (ölçek)** | Şişeyi jigger'a, jigger'ı tenekeye — tam 25/50 ml; serbest döküm hâlâ mümkün ama merdiven yok | Jigger kesinlik verir, zaman alır; müşteri sabrı düşer. Ustalık = serbest dökümde bantı tutturmak | Gimlet (12) ya da sayfa yerine **tezgâh yükseltmesi** (Rating §27) | Yeni prop; `PourMeasure(id, share)` zaten ölçülü döküyor |
| 2 | **Süzgeç (Hawthorne)** | Kapaklı tenekeden bardağa dökerken süzgeç takılı mı? Takılı değilse buz/posa geçer | "Buz parçalı" içki: sour'larda temiz olmalı, Julep'te olmalı. İki süzgeç (ince) → çift süzme | Daiquiri (16) ya da Whiskey Sour (15) | `Preparations` slotuna `strained`; `ServiceJudge` garnish gibi puanlar |
| 2 | **Buz kovası gerçek** | Tenekeye buz koy (bir tık), çalkalama süresi = sulanma | Buzsuz çalkalama ılık ve puansız; fazla çalkalama sulu ("sulu" tepkisi) | Zaten tezgâhta; ilk 2★ sayfası açar | `ShakeEnergy`/süre → `Dilution`; §5 madde 1–2 |
| 3 | **Muddler (ezici)** | Bardakta nane/limon ez — birkaç basış, çok ezersen acı | Az ezme → aroma yok; çok ezme → "acı" tepkisi | Mint Julep (21) ya da Mojito (kitapta yoksa yeni içerik olur — kaçınılır) | Garnish sistemine `muddled` hazırlık; bardak sahnesinde basış sayacı |
| 3 | **Alev / kabuk (zest) sıkma** | Portakal kabuğunu alev üstünden bardağa sık — zamanlama | Erken/geç → duman kokusu yok | Old Fashioned (26) / Negroni (25) | `Preparations` `flamed_zest`; bardak sahnesinde tek zamanlama tuşu |
| 4 | **Katmanlama kaşığı (pousse-café)** | Kaşığın sırtından yavaş döküm; hız merdiveni ters çalışır (yavaş = temiz katman) | Hızlı dökersen katman bozulur, bir daha olmaz | Bir 4★ katmanlı sayfa (B-52 tipi, kitapta yok — içerik ister) | Dökme kadranı zaten hızı gösteriyor; katman = `LiftShare` düşük tutulur |
| 4 | **Karıştırma bardağı (mixing glass) + Julep süzgeci** | Stirred içki tenekede değil cam bardakta karışır; kaşık dairesi görünür, "yeterli" çizgisi sulanmayla yarışır | Uzun karıştırma sulu; kısa karıştırma sıcak | Dry Martini (22) | Teneke propunun cam varyantı; `StirEnergy` zaten var |

**Öneri (sıra):** (1) buz gerçek malzeme + sulanma — §5'in 1–2'si, en çok "karar anı" katanı, yeni prop istemez;
(2) süzgeç — bir tık, iki hata türü, sour'ları farklı kılar; (3) jigger — acemi/usta ayrımı; (4) gerisi içerik
sayfasıyla birlikte. Hepsi kaşığın kalıbını izler: **sayfa alınır → araç tezgâhta belirir → adım şeridi o araca göre
değişir → yargıç yeni hazırlığı puanlar.**

**Masalara servis (yazarın sorusu, ayrı başlık):** oturma animasyonu yerine masada **silüet** (koltuk hizasında koyu
figür + bardak); servis = içkiyi tepsiye koy, masaya tıkla (yürüyüş yok, tezgâhtan masaya "kayan" tepsi); masadaki
müşteri barın önündeki gibi sipariş kartı taşır ama **sabrı daha uzun, bahşişi daha yüksek, tabak/bardak toplanması**
(wipe/collect verb'leri §27 zaten var) gerekir. Yeni oynanış: masalar aynı anda 2–3 sipariş biriktirir → sıra
yönetimi. Kod: `TableSeat` = `Stool`'un uzak türevi; `TycoonHud.Seats` içinde masalar için ikinci yerleşim.
