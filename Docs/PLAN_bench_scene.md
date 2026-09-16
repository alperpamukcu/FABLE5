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
