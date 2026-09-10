# SESLERİ NEREDEN ALACAĞIZ (2026-09-10)

Yazar: *"Mevcut oyun seslerini beğenmedim, sesleri daha profesyonel nereye ürettirebiliriz?
Hangi yapay zeka bu konuda bize yardımcı olabilir."*

**Beğenmemenin sebebi belli ve `Docs/SES_LISTESI.md` zaten yazıyor:** oyundaki 73 klibin
**tamamı sentetik** — `Tools/sfx_bank.py` + `sfx_dsp.py` ile osilatörden üretildi. Hiçbiri
kayıt değil. Yani sorun karıştırma ya da seviye değil; ortada gerçek ses yok.

**İyi haber:** sistem tam da bunun için kurulmuş. `Sfx.Play("dosya_adi")` dosyayı **adıyla**
arar, `Assets/Resources/Audio/` altında bulamazsa **sessiz geçer, hata vermez**. Yani
**tek bir satır kod değiştirmeden** klasöre aynı adla WAV atarak hepsini değiştirebiliriz.
Tek tek de değiştirilebilir — 73'ünü birden bulmak zorunda değiliz.

Format (bankın düzeni): **44.1 kHz, mono, 16-bit WAV**, başı ve sonu sıfırda (tık olmasın).

---

## 1 · Kısa cevap

Bir bar oyununu gerçek yapan sesler **fizikseldir**: cam, buz, sıvı, ahşap, kasa, kalabalık
uğultusu. Bu tür sesleri yapay zekâ bugün *idare eder* seviyede üretiyor; **gerçek kayıt
kütüphaneleri hâlâ belirgin şekilde daha iyi.** Müzikte ise durum tersine döndü — yapay zekâ
müzik artık fazlasıyla yeterli.

Önerim üçü birlikte:

| Katman | Kaynak | Neden |
|---|---|---|
| **Efektlerin gövdesi** (cam, sıvı, buz, kapı, kasa) | **Sonniss GDC Bundle** — ücretsiz, telifsiz, sonsuza kadar ticari kullanım | Yüzlerce GB profesyonel saha kaydı; her yıl GDC'de bedava dağıtılıyor. Bu oyunun ihtiyacı olan foley'in çoğu içinde |
| **Boşlukları doldurma** (çok özel, bulunamayan sesler) | **ElevenLabs Sound Effects** (metinden efekt) | Bugün en iyi metin→efekt motoru. "ice cubes dropped into a rocks glass" yazıp saniyeler içinde alıyorsun. Ücretli planlarda ticari lisans |
| **Müzik** (bar yatağı, gün sonu, kapanış) | **Suno** ya da **ElevenLabs Music** | Yapay zekâ müziği bu iş için yeterli. Suno'da ticari kullanım **Pro/Premier** planlarında; ücretsiz planda YOK — plana dikkat |

---

## 2 · Kaynakların tamamı, artı ve eksileriyle

### Yapay zekâ — efekt

| Araç | Durum |
|---|---|
| **ElevenLabs Sound Effects** | Metinden efekt. En olgunu. Ticari lisans ücretli planda. **Önerilen** |
| **Stable Audio / Stable Audio Open** | Açık ağırlıklı, kendi makinende çalıştırılabilir, ücretsiz. Kalite ElevenLabs'in bir tık altında ama lisans sorunu hiç yok |
| **Meta AudioGen / AudioCraft** | Araştırma modeli, kurulum işi; kalite değişken. Ancak tamamen ücretsiz |

### Yapay zekâ — müzik

| Araç | Durum |
|---|---|
| **Suno** | En kolayı, en iyisi. **Ticari kullanım Pro/Premier'de** |
| **Udio** | Suno'ya çok yakın; lisans şartlarını üretmeden önce okumak gerek |
| **ElevenLabs Music** | Efektlerle aynı hesap, aynı fatura — tek yerden yürütmek istersen avantaj |

### Gerçek kayıt kütüphaneleri (yapay zekâ değil, ama bu oyun için çoğu zaman daha iyi)

| Kaynak | Durum |
|---|---|
| **Sonniss GDC Bundle** | **Ücretsiz, telifsiz, ticari.** Profesyonel saha kaydı. En yüksek kalite/bedel oranı |
| **freesound.org** | Devasa; **CC0 filtresi** ile süz, o zaman atıf bile gerekmez |
| **Kenney.nl** | CC0 oyun ses paketleri; UI tıkları ve arayüz sesleri için hazır |
| **ZapSplat / Soundly** | Abonelikli, çok geniş, aranabilir |

### Retro katman (isteğe bağlı)

Oyun piksel; bazı UI sesleri **bilerek** çip sesi olabilir. **jsfxr / Bfxr / ChipTone** —
tarayıcıda, ücretsiz, saniyeler içinde. Şu anki sentetik banka en yakın akraba bunlar; ama
seçilerek kullanılırsa "ucuz" değil "üslup" olur.

---

## 3 · Nasıl ilerleriz

1. `Docs/SES_LISTESI.md` §1'deki 73 klip ve §2'deki eksikler listesi zaten hazır — alışveriş
   listesi o.
2. En çok duyulan 10–15 sesle başla; oyunun hissi %80 onlarda: bardak koyma, sıvı dökme,
   buz, shaker çalkalama, kasa, kapı zili, müşteri oturma, gün sonu.
3. Ne bulursan `Assets/Resources/Audio/` altına **aynı adla** at. Kod değişmez, test
   değişmez, sessizlik hata değil — yani yarısını bugün, yarısını sonra koyabilirsin.
4. Karıştırma (seviye dengesi) sonra bir turda yapılır; `Sound.Volume` ve çağrı başına
   `volume` zaten var.

---

*Bu bir öneri belgesidir; hiçbir ses dosyası değiştirilmedi.*
