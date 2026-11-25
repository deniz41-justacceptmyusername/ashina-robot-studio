# Çevrimiçi maç

Bir oyuncu oda açar; diğerleri davet koduyla katılır. Oda sahibi de oynayabilir: toplam **6 robot, 3 mavi + 3 kırmızı**. Klavye/kumanda ve WPILib kullanan oyuncular aynı sahada bulunabilir. Uygulama masaüstünde çalışır.

## Oda aç ve katıl

1. Robot projenizi açın. Ana ekrandan **Çevrimiçi maça gir** seçin. Çalışma alanında aynı giriş **Saha → Çevrimiçi maç** altında bulunur.
2. Oyuncu adını ve ittifakı seçin. Farklı ağlarda oynuyorsanız oda sahibi **İnternet odası aç** düğmesine basar. Aynı ağda **Aynı Wi-Fi / yerel ağ → Yerel oda aç** seçilebilir.
3. Davet hazır olunca **Daveti kopyala** düğmesine basın ve diğer oyunculara gönderin.
4. Diğer oyuncular kendi robotlarını açıp kodu yapıştırır ve **Davet koduyla katıl** seçer. Oda sahibinin sezonu kullanılır; uyumsuz saha dosyalarıyla katılım engellenir.
5. Herkes **Hazırım** seçer. Oda sahibi **Maçı başlat** düğmesine basar. Altı kişi zorunlu değildir; birkaç robotla da antrenman yapılabilir.

Geri sayımın ardından otonom, devre dışı ara ve teleop birlikte başlar. REBUILT varsayılanı 20 saniye otonom, 3 saniye ara ve 140 saniye teleoptur. Diğer sezonlarda 15/3/135 kullanılır. Süreler oda panelinden değiştirilebilir. Oda sahibi lobiye döndüğünde saha, stok ve sayaçlar yenilenir.

**Esc / Kendi robotumu durdur** yalnız senin robotunu durdurur. Oda sahibi bağlantıyı kapatırsa istemciler ve bağlı WPILib robotları devre dışı kalır. Odadan ayrılmak, normal robot projesinin online öncesi ayarlarını geri getirir.

## İnternetten erişim

İnternet odası, Windows paketindeki araçla geçici bir tünel açar. Uygulama erişebildiği adresi doğruladıktan sonra daveti gösterir. Daveti **Davet koduyla katıl** alanına yapıştırın. Oda sahibi uygulamayı kapattığında link geçersiz olur. Yeni oturumda yeni bir davet paylaşılır. Geçici hizmetin erişilebilirliği ağınıza ve hizmet sağlayıcısına bağlıdır. [Cloudflare Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/)

Yerel ağ daveti `adres:port/oda-anahtarı` biçimindedir. Doğrudan barındırılan bir oda için erişilebilir sunucu veya VPN adresi de kullanılabilir. WPILib bağlantısı her oyuncunun kendi bilgisayarında kalır.

## WPILib robotu ile oyna

Her takım kendi WPILib simülasyonunu yerel bilgisayarında başlatır. Uygulamada kendi NT sunucusuna bağlanır. Online sürüş **hız isteği / Unity fiziği** kullanır. Konum izleme tek başına ortak çarpışmaları robot koduna geri besleyemez.

- `integration/java/AshinaSimLink.java` güncel sürümünü kullanın. `simulationPeriodic()` içindeki `update()` kumandayı, enabled/autonomous, ittifak, istasyon ve maç süresini WPILib'e aktarır.
- Sürüş kodunun istediği robot yönündeki `[vx, vy, omega]` değerini her 20 ms'de `/AshinaRobot/Drive/request` konusuna yayınlayın. Birimler m/s, m/s, rad/s; pozitif Y sol, pozitif dönüş saat yönünün tersidir.
- Mevcut odometri/poz kestirimine simüle teker mesafesi ve gyro verisi vermek için `AshinaSwerveSim` kullanılabilir. `updateSensors()`, `modulePositions()` ve `heading()` normal odometri girişlerine bağlanır.
- `AshinaSimLink.takeResetPose()` geldiğinde teker/gyro simülasyonu ve odometri başlangıcını birlikte sıfırlayın. Bu, yeni maçın başlangıç konumunu eşler.
- Intake ve atış izinlerini, hareketli parça açılarını ve çıkış hızını genel NT eşlemeleriyle bağlayın. Topu doğuran ve stoğu azaltan taraf oda sahibidir.
- Kamera adı, montajı, görüş alanı ve gecikmesini tanımlayın. Limelight/PhotonVision verileri kendi robotunuzun ortak sahadaki konumundan üretilir.

Yayınlanmış bir WPILib `SendableChooser` varsa seçenekleri online panelde **Otonom** düğmesiyle değiştirebilirsiniz. Seçim kendi kodunuza gider. Klavye/kumandayla giren robotlar otonomda bekler, teleopta sürülür. [IO kurulum rehberi](networktables.md).

## Ayrı sunucu

Windows paketindeki aynı EXE görüntü çizmeden oda sunucusu olarak da çalışabilir. Sunucu oyuncu yeri tüketmez; altı oyuncunun tamamı uzaktan bağlanır:

```powershell
.\RobotStudio.exe -batchmode -nographics --match-server 5820 --season 2026 --min-players 6 --public-address match.example.org --invite-file invite.txt -logFile match.log
```

Alan adını/IP'yi kendi sunucunuzunkiyle değiştirin. Üretilen davet `invite.txt` dosyasına yazılır. Belirtilen sayıda oyuncu hazır olunca maç başlar; bitişten 8 saniye sonra lobiye döner. `--min-players 1` ile tek robotun bağlantısı da denenebilir.

İsteğe bağlı ayarlar: `--auto-seconds`, `--teleop-seconds`, `--countdown-seconds`, `--gap-seconds`. `--room-key` 32 onaltılık karakterle sabit anahtar belirler; verilmezse her açılışta yeni anahtar üretilir. Hazır paket Windows içindir; Linux sunucu derlemesi sağlanmaz.

## Kapsam

Ortak şase çarpışmaları, oyun eşyaları, tek sahipli stok, mekanizma hareketleri ve REEFSCAPE'in sallanan kafesleri oda sahibinden paylaşılır. Nokta/bölge hedefleri, normal atış açısı, maksimum hız ve saçılma online atışlarda da uygulanır. Modeller ve simülasyon ayarları oyunculara aktarılır; yerel kod projesi, NT adresi ve Blockly içeriği aktarılmaz. Online model sınırı 32 MB'tır.

Skor kapsamı REBUILT HUB giriş toplamıdır. Resmi aktif/pasif HUB dönemleri, climb puanları, coral puanlama ve cezalar eklenmemiştir. Maç sürerken katılım reddedilir; lobiye dönüldükten sonra yeniden katılın. Sunucu göçü ve kaldığı yerden otomatik yeniden katılım bulunmaz.

[Doğrulama](validation.md): altı oyunculu TCP oturumları, iki ayrı Unity uygulaması, gerçek WPILib NT4/sensör IO'su ve eklenmiş 80 ms gidiş-dönüş gecikmesi. Farklı internet sağlayıcıları arasında gerçek WAN ve altı fiziksel bilgisayar testi yapılmadı.
