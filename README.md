# Snake Block Jam

Oreon Studios Unity case çalışması. Portre ekranda bir yılanın herhangi bir gövde hücresine dokunarak onu başının baktığı yönde hareket ettirirsiniz. Amaç tahtayı boşaltmaktır. Köprüler iki katmanda geçiş sağlar, tüneller kaçış noktasıdır, oklar başın yönünü değiştirir. İpucu kaçabilecek bir yılanı gösterir. Kürek seçtiğiniz yılanı kaldırır.

## Çalıştırma

1. Projeyi Unity Hub üzerinden **6000.3.28f1** ile açın. Android için aynı sürümün Android Build Support, SDK, NDK ve OpenJDK modüllerini kurun.
2. `Assets/_Project/Scenes/Game.unity` sahnesini açıp Play'e basın.
3. Sahne üreticisini değiştirdiyseniz Play modundan çıkın. **Snake Block Jam → Setup → Build Game Scene** seçip sahneyi kaydedin.
4. APK üretmek için **Snake Block Jam → Build → Android APK (Release)** seçin. Çıktı `Builds/Android/SnakeBlockJam.apk` altındadır. Paket adı `com.oreoncase.snakeblockjam`, hedef ARM64, betik altyapısı IL2CPP'dir.

İlk beş seviyede hamle sayacı görünmez. Sonraki bölümlerde çarpma da bir hamle harcar. İlk dört seviyede kazanma bir sonraki bölümü açar. L5'ten itibaren Devam, kayıpta Tekrar Dene kullanılır.

Bu case için oluşturulan release APK varsayılan Android Debug sertifikasıyla imzalandı. Geliştirme derlemesi seçeneği kapalıdır. İmza ve ARM64 paket içeriği Android SDK araçlarıyla doğrulandı.

## Mimari

```text
LevelData / LevelCatalog ──► GameController ◄── InputHandler
                                  │
                             MoveSimulator
                                  │ MoveResult
                              BoardState
                                  │
                        BoardView / SnakeView
                        HUD / sonuç / ses
```

Core saf C# ile hamleyi hesaplar. GameController sonucu tahtaya uygular ve aynı hareketi görünümde oynatır. Bu sayede kaçan bir yılanın boşalttığı hücrelere animasyon bitmeden yeni hamle yapılabilir. Çarpma dönüşünde giriş kilidi kullanılır. Level Editor oyunda kullanılan LevelData dosyalarını düzenler.

## Level Editor

**Snake Block Jam → Level Editor** menüsünü açın. Üstteki alandan bölüm seçin. **Kopyala** ile bir örnekten başlayabilir veya **Yeni** ile boş tahta oluşturabilirsiniz. Yılan ve köprü araçları sürüklenerek çizilir. Tünel ve yön oku hücreye yerleştirilir. Köprüde yüzeyi **Zemin / Üst** seçimi belirler.

Doğrulama hatalarını düzeltin, **Kaydet** ve yeni bölümse **Kataloğa ekle** seçin. **▶ Oyna** doğrudan seçili seviyeyi başlatır. Bölüm denemeleri için bu editör akışı kullanılır. Cihaz sürümünde ayrıca seviye atlama paneli bulunmuyor.

![Level Editor'de L19 köprü hücrelerinin ayrıntısı](Media/level-editor-L19.png)

## Doğrulama

Unity Test Runner, EditMode ve PlayMode paketlerini çalıştırır. 19 Kasım 2026 görsel turunda 374 EditMode, 44 kalıcı PlayMode testi geçti. Aynadaki dört ek kontrol tanıtım ve sonuç ekranlarını, kayıtlı oynanış akışını, L1–L20 geçişlerini ve ok kayma klibinin PCM çıktısını doğruladı. Test koşusu Mobile kalite ayarında 720 × 1616 görsel kanıt üretti. Oynanış videosu Unity PlayMode'dan 24 kare/sn olarak kaydedildi. Bu kayıt cihaz FPS ölçümü değildir.

Android 15 Pixel 8 emülatöründe 720 × 1616 çözünürlükte L1–L20 ekran dokunuşlarıyla geçildi. Her bölümde uygulama günlüğünde Won durumu denetlendi. İlk açılış denemesinde Android düşük bellek nedeniyle uygulamayı sonlandırdı. 4,5 GB RAM ve host GPU yapılandırmasıyla tam tur geçti.

![Release APK ekranları](Media/android-release.png)

## Araçlar ve kapsam

- Prosedürel tahta geometrisi, yılan parçaları ve oyun sesleri proje içinden üretilir. URP çizimi ortak ağları ve renge göre paylaşılan malzemeleri kullanır.
- Lilita One Regular, Juan Montoreano. OFL lisansı ve font `Assets/_Project/Art/Fonts/` altında dağıtılır.
- Codex, uygulama, test yürütme ve referans karelerin karşılaştırılmasında kullanıldı. ImageGen ile üretilen özgün kupa, Hard ikonu, köprü, tünel ve ok tanıtımları kullanıcı onayıyla eklendi. FFmpeg, Python, Pillow ve NumPy inceleme, görsel hazırlama ve kayıt kodlamasında kullanıldı.
- Katalog 20 bölüm içerir. Son bölümde Devam ilk bölüme döner. Coin, reklam, mağaza, kalp ve ek hamle satışı kapsam dışıdır.
- Referanstaki bekleme ifadelerinin kesin periyotları ölçülmedi. Çarpan yılanın göz spirali tahtada kaldığı sürece sürer. Fiziksel Android cihazında FPS ve GC ölçümü henüz alınmadı.
