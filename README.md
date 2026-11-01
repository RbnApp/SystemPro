<p align="center">
  <img src="docs/assets/logo.png" width="128" alt="RbnSystemPro">
</p>

<h1 align="center">RbnSystemPro</h1>

<p align="center">
  <b>Yeni kurulmuş Windows'u tek pencerede toparlayan bakım aracı.</b><br>
  Donanım ve güvenlik özeti, sürücü durumu, <code>winget</code> ile program kurulumu, temizlik,
  ağ araçları ve canlı performans.
</p>

<p align="center">
  <a href="https://github.com/RbnApp/SystemPro/releases/latest"><img alt="Son sürüm" src="https://img.shields.io/github/v/release/RbnApp/SystemPro?style=for-the-badge&amp;label=SON%20S%C3%9CR%C3%9CM"></a>
  <a href="#-gereksinimler"><img alt="Windows 10 ve 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=for-the-badge&amp;logo=windows"></a>
  <a href="LICENSE"><img alt="MIT lisansı" src="https://img.shields.io/badge/License-MIT-3DA639?style=for-the-badge"></a>
  <a href="https://github.com/RbnApp/SystemPro/issues/new"><img alt="Geri bildirim" src="https://img.shields.io/badge/GER%C4%B0%20B%C4%B0LD%C4%B0R%C4%B0M-issues-2ea44f?style=for-the-badge&amp;logo=github"></a>
  <a href="https://dotnet.microsoft.com/"><img alt=".NET 10 / WPF" src="https://img.shields.io/badge/.NET-10%20%7C%20WPF-512BD4?style=for-the-badge&amp;logo=dotnet"></a>
</p>

---

## 🧭 Nedir?

RbnSystemPro, Windows kurulumundan sonra sırayla yapılan işleri tek uygulamada toplar:
sistemin özetini çıkarır, sürücülerin durumunu gösterir, gerekli programları `winget` ile
kurar, geçici dosyaları temizler, ağ ayarlarına erişir ve makinenin anlık durumunu izler.

Uygulama **yönetici haklarıyla** çalışır (`app.manifest:19` → `requireAdministrator`); bu
yüzden açılışta UAC onayı istenir ve sistem değiştiren işlemler sorunsuz çalışır.

> Arayüz **İngilizce**. Aşağıdaki başlıklarda ekranda gördüğünüz sayfa adları kullanılır.

## ✨ Özellikler

### 🏠 Dashboard — genel bakış
- Donanım özeti: CPU (çekirdek / iş parçacığı), RAM (yuva yuva), anakart, GPU ve VRAM, BIOS sürümü ve tarihi.
- Güvenlik durumu: Microsoft Defender, güvenlik duvarı, UAC, Secure Boot, TPM.
- Windows sürümü ve etkinleştirme bilgisi; **Activation** kartı Windows'un etkinleştirme sayfasını açar (`ms-settings:activation`).
- Kartlardan ilgili sayfaya geçilir; alttaki **System Activity Timeline** son işlemleri listeler.

### 📦 Programs — Software Manager
- Kategorilere ayrılmış hazır liste (16 program): tarayıcılar (Chrome, Firefox, Brave, Opera),
  arşiv ve ekran görüntüsü (WinRAR, 7-Zip, Lightshot), disk araçları (CrystalDiskInfo, Hard Disk Sentinel,
  FurMark), medya (VLC, K-Lite), geliştirme (Notepad++), oyun (Discord, Steam, Epic Games).
- Kurulum `winget install -e --id <paket> --silent` ile yapılır; kurulu olanlar **INSTALLED** olarak işaretlenir.
- Kaldırma `winget uninstall` ile yapılır. Ardından yalnız **tam eşleşen** kaldırma kaydı temizlenir;
  hiçbir klasör silinmez (bkz. `Services/UninstallCleanup.cs`).
- `winget` yoksa **ENABLE NOW** düğmesi App Installer paketini yeniden kaydeder ve sonucu doğrular.

### 🎮 GPU Drivers
- Ekran kartının üreticisini addan tanır (NVIDIA / AMD / Intel) ve kurulu sürücü sürümünü gösterir.
- Sürücü **elle indirilir**: resmî indirme sayfası açılır. NVIDIA, AMD ve Intel sürücülerinin
  `winget` paketi yoktur; liste bu yüzden kısayol olarak çalışır.
- NVIDIA için Game Ready ve Studio ayrımı vardır.

### 🔌 Essential Drivers
- Temel sürücü listesi: Realtek Audio, Intel Chipset, Intel Management Engine, Realtek Ethernet, Intel Wireless.
- Kurulum sırası: `pnputil /scan-devices` → hazır `winget` paketi varsa onunla kurulum → sonuç yeniden denetlenir.
- Realtek ses ve ağ sürücüsünün `winget` paketi yoktur; bu satırlarda cihaz taraması yapılır ve sonuç doğrulanır.

### 🧹 System Cleaner
- Geçici dosyalar: `%TEMP%`, `C:\Windows\Temp` ve `%LOCALAPPDATA%\Temp` (tekrarlanan yollar ayıklanır).
- Kayıt defteri: yalnız kullanıcıya ait **MuiCache** girdilerinden, dosyası artık bulunmayanlar silinir.
  Bu, geniş kapsamlı bir "registry cleaner" değildir.
- Tarayıcı verisi: Chrome, Edge, Firefox, Brave, Opera ve Opera GX ayrı ayrı seçilebilir.
- **Run Cleaner** önce neyin silineceğini söyleyip onay ister; onay yoksa hiçbir şey silinmez.

### 🌐 Network Optimizer
- DNS: tek tıkla Google (8.8.8.8 / 8.8.4.4) ya da Cloudflare (1.1.1.1 / 1.0.0.1); açık olan bütün
  ağ bağdaştırıcılarına uygulanır.
- DNS önbelleğini boşaltma (`ipconfig /flushdns`) ve TCP/IP optimizasyonu.
- **Hard Reset:** Winsock ve TCP/IP sıfırlanır, IP yenilenir. Bağlantı birkaç saniye kesilir;
  bu yüzden işlem onay ister.
- **WiFi Networks:** çevredeki ağları listeler, seçilen ağa bağlanır, kayıtlı ağın parolasını gösterir.

### 📈 Performance
- Canlı CPU, GPU ve RAM kullanımı; çalışma süresi ve etkin güç planı.
- Diskler: model, bağlama noktası, boş alan, sıcaklık ve SMART hata durumu.
- Okunamayan değer **uydurulmaz**; ekranda "—" görünür.
- Ayarlardaki **Hardware Monitoring** anahtarı derin WMI sensör taramasını açar ya da kapatır.

### 🛡️ Update & Security
- Defender, güvenlik duvarı, Secure Boot, UAC ve TPM durumu tek ekranda.
- Son başarılı Windows güncellemesinin tarihi.
- **Check Windows Update** düğmesi Windows Update ayarlarını açar (`ms-settings:windowsupdate`).

### 🕘 Activity Timeline
- Yapılan işlemlerin kaydı: kurulum, sürücü, temizlik, optimizasyon — tarih ve saatle.
- Kayıt Ayarlar'daki **Clear Logs** düğmesiyle temizlenir.

### ⚙️ Settings
- Sürüm bilgisi ve **Check for Updates**: GitHub'daki `version.json` ile karşılaştırır; yeni sürüm
  varsa indirme sayfasını tarayıcıda açar.
- GitHub deposu bağlantısı.
- **Geri bildirim gönder**: tarayıcıda GitHub'ın "yeni issue" sayfasını açar. Önceden doldurulan
  tek şey sürüm ve Windows sürümüdür.
- Aktivite kaydını temizleme ve donanım izleme anahtarı.

## 🚨 Windows Deploy — bizim yazdığımız bir optimizasyon değil

Dashboard'daki **Windows Deploy** kartı RbnSystemPro'nun kendi optimizasyon motoru **değildir**.
[Raphire/Win11Debloat](https://github.com/Raphire/Win11Debloat) projesinin betiğini internetten
indirip yönetici yetkisiyle çalıştırır.

| | |
|---|---|
| **Ne yapar** | Betik önceden kurulu uygulamaları kaldırır, telemetri ve tanılama verilerini kapatır, Windows ayarlarını değiştirir (görev çubuğu, başlat menüsü, dosya gezgini, güncellemeler, yapay zekâ özellikleri ve daha fazlası). |
| **Kaynak** | `https://debloat.raphi.re/` → GitHub'da [Raphire/Win11Debloat](https://github.com/Raphire/Win11Debloat) |
| **Lisans** | MIT — üçüncü taraf, RbnSystemPro'ya ait değil |
| **Nasıl çalışır** | Her çalıştırmada adresten indirilir (`irm`) ve PowerShell'de **yönetici** olarak koşar (`Services/DeployService.cs:32-41`). İndirilen betik saklanmaz; özeti ya da imzası RbnSystemPro tarafından doğrulanmaz. |
| **Onay** | Çalıştırmadan önce onay penceresi çıkar: kaynak adresi, yönetici yetkisi ve işlemin geri alınamaz olduğu yazar (`Views/DashboardPage.xaml.cs:25-32`). |
| **Geri alma** | RbnSystemPro'nun geri alma düğmesi yoktur. Projenin kendi belgesi değişikliklerin geri alınabildiğini, kaldırılan uygulamaların çoğunun Microsoft Store'dan yeniden kurulabildiğini söyler; geri alma Windows tarafında elle yapılır. |

Bu adımı atlamak uygulamanın diğer özelliklerini etkilemez. Windows Deploy'dan önce bir sistem
geri yükleme noktası almanızı öneririz.

## 🚀 Kurulum

Son yayın: **[RbnApp/SystemPro · Releases](https://github.com/RbnApp/SystemPro/releases/latest)**.
İndirilecek dosya `RbnSystemPro-Setup-<sürüm>.exe`; aşağıdaki örnekler güncel sürüm olan `0.1.3` ile yazıldı.

1. Dosyayı indirip çalıştırın. Windows yönetici onayı (UAC) ister; uygulama `Program Files\RbnSystemPro`
   altına kurulur, Başlat menüsüne kısayol eklenir. Kaldırmak için **Ayarlar → Uygulamalar**.
   Kaldırmada uygulamanın `Logs` klasöründeki günlük ve ayarlarınız silinmez.
2. Makinede **.NET 10 Desktop Runtime** kurulu olmalıdır; yoksa kurulumdan sonra `.exe` açılmaz
   (bkz. [Gereksinimler](#-gereksinimler)).

Kurulum dosyası **Windows kod imzasıyla imzalı değildir**. SmartScreen "Yine de çalıştır" diyebilir; bu
beklenen durumdur. Dosyayı yalnız bu depodan (`RbnApp/SystemPro`) alın.

### 🔎 Dosyayı doğrula

İndirdiğiniz dosyanın değişmediğini SHA-256 özetiyle kontrol edebilirsiniz. Aynı sürümün yayın sayfasında
duran `RbnSystemPro-Setup-<sürüm>.exe.sha256` dosyası beklenen özeti taşır. İki dosyayı aynı klasöre indirip
PowerShell'de:

```powershell
Get-Content .\RbnSystemPro-Setup-0.1.3.exe.sha256             # beklenen: <özet>  <dosya adı>
Get-FileHash .\RbnSystemPro-Setup-0.1.3.exe -Algorithm SHA256  # sizin dosyanızın özeti
```

İki özet büyük/küçük harf farkı gözetmeden aynıysa dosya değişmemiştir. Farklıysa çalıştırmayın, yeniden
indirin. Yayın sayfasındaki `.sig` dosyası ayrıca **bütünlük imzasıdır** (Ed25519, rbnbilisim imza anahtarı);
dosyanın bize ait olduğunu ve değişmediğini gösterir, geliştirici tarafında kontrol edilir. Windows'un
"doğrulanmış yayıncı" imzası değildir; SmartScreen uyarısını kaldırmaz.

Kurulum paketini kullanmadan denemek isteyenler için: .NET 10 SDK kurulu makinede `dotnet run -c Debug`
(UAC onayı ister). Kendi paketinizi üretmek için aşağıdaki **Geliştirme** bölümüne bakın.

## 📋 Gereksinimler

| Gereksinim | Neden |
|---|---|
| Windows 10 veya Windows 11, x64 | Hedef çatı `net10.0-windows`, çalışma ortamı `win-x64` (`RbnSystemPro.csproj:5,30`); manifest yalnız Windows 10/11 desteği bildirir (`app.manifest:31`). Windows 10'da 2004 (20H1) ve üzeri önerilir. |
| .NET 10 Desktop Runtime | Yayın `self-contained` değildir (`RbnSystemPro.csproj:31`); runtime yoksa `.exe` açılmaz. |
| `winget` (App Installer) | Program kurulumu ve sürücü sorguları için. Yoksa Programs sayfasındaki **ENABLE NOW** yeniden kaydetmeyi dener. |
| Yönetici hakları | Manifest `requireAdministrator` (`app.manifest:19`); açılışta UAC onayı istenir. |
| İnternet | `winget` kurulumları, güncelleme denetimi ve Windows Deploy betiği için. |

## 💬 Geri bildirim

Hata ve öneriler GitHub Issues'ta toplanır: [RbnApp/SystemPro · Issues](https://github.com/RbnApp/SystemPro/issues/new).
Uygulamadaki **Settings → Geri bildirim gönder** düğmesi bu sayfayı tarayıcıda açar ve gövdeye
yalnız sürüm ile Windows sürümünü yazar. Kimlik, makine adı, kullanıcı adı, dosya yolu ve günlük gönderilmez.

## 🔢 Sürüm

Biçim **ANA.ARA.YAMA**'dır ve başında "v" yoktur (şu an: `0.1.3`). Tek kaynak
`Helpers/GlobalConfig.cs:8` içindeki `AppVersion` sabitidir; derlemedeki `FileVersion`/`AssemblyVersion`
değeri de buradan okunur (`RbnSystemPro.csproj:17-27`). Kural: YAMA artar, 9'dan sonra ARA'ya,
ARA 9'dan sonra ANA'ya devreder (0.4.9 → 0.5.0, 0.9.9 → 1.0.0). Sürüm numarası yalnız yeni bir
yapı yayımlanırken artırılır.

Rozet bandındaki **SON SÜRÜM** rozeti dinamiktir: GitHub'daki en son Release etiketini gösterir, tıklanınca
`releases/latest` sayfası açılır. Etiket ile buradaki `AppVersion` aynı sürümü taşır; sürüm yalnız yeni bir
yapı yayımlanırken artırıldığı için ikisi ayrı düşmez.

## 📄 Lisans

MIT — ayrıntılar için [LICENSE](LICENSE). Telif: © 2026 rbnbilisim.

## 🛠️ Geliştirme

```powershell
dotnet build -c Debug            # 0 hata / 0 uyarı beklenir
dotnet run -c Debug              # UAC onayı ister (yönetici)
dotnet publish -c Release -r win-x64 --self-contained false
```

`Publish/` klasöründeki eski `.exe` güncel değildir; yeni paket yukarıdaki `publish` komutuyla üretilir.

### Kurulum paketi üretimi

Windows kurulum sihirbazı Inno Setup 6 ile üretilir; sürüm yine `Helpers/GlobalConfig.cs` → `AppVersion`'dan okunur
(ISS dosyasına elle yazılmaz). Tek komut:

```powershell
powershell -ExecutionPolicy Bypass -File Setup\derle.ps1
```

Betik sırayla `dotnet publish` (Release, win-x64, self-contained=false) → `ISCC` çalıştırır ve çıktının yolunu,
boyutunu, SHA-256'sını yazar. Çıktı `bin\setup\RbnSystemPro-Setup-<sürüm>.exe`; `bin/` dizini `.gitignore`'da
olduğu için commit edilmez. Sihirbaz `Program Files\RbnSystemPro`'ya kurar (yönetici ister), kaldırmada kullanıcı
günlüğü/ayarı (`{app}\Logs`) silinmez. Paket Windows kod imzasıyla (Authenticode) imzalı değildir; yayımlanan
`.sig` dosyası yalnız bütünlük imzasıdır.

| Yol | İçerik |
|---|---|
| `App.xaml(.cs)` | Uygulama girişi, DI kapsayıcısı |
| `Views/` | Sayfalar: Dashboard, Performance, Programs, Cleaner, Network, WifiNetworks, Drivers, GpuDrivers, UpdateSecurity, Timeline, Settings |
| `ViewModels/` | MVVM durum ve komutlar |
| `Services/` | İş mantığı: donanım, performans, yazılım, temizlik, ağ, sürücü, deploy, günlük |
| `Models/` | Veri modelleri |
| `Helpers/` | `GlobalConfig` (sürüm ve adresler), donanım sabitleri, yardımcılar |
| `docs/` | Mimari, servis kataloğu, geliştirme rehberi, şablonlar |
| `Logs/` | Çalışma zamanı günlüğü ve aktivite kaydı (uygulama oluşturur) |

## 📚 Belgeler

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — katmanlar ve servisler
- [docs/SERVICES_CATALOG.md](docs/SERVICES_CATALOG.md) — servis envanteri
- [docs/DEVELOPMENT_GUIDE.md](docs/DEVELOPMENT_GUIDE.md) — geliştirme akışı

## 🚫 Neleri yapmaz

Eski tanıtım metinlerinde geçen ama kodda karşılığı olmayan işler:

- Otomatik Windows format USB'si (`autounattend.xml`) üretmez, Windows kurmaz.
- ISO düzenlemez, ISO'ya sürücü gömmez.
- Ayrı bir "Killer LAN" sürücüsü listesi yoktur; ağ sürücüsü olarak Realtek Ethernet listelenir.
- Telemetriyi kapatan kendi özelliği yoktur; bunu yalnız Windows Deploy kartı, üçüncü taraf betikle yapar.
- ".NET 8/9" değil, **.NET 10** üzerinde çalışır (`RbnSystemPro.csproj:5`).
- Kendi kendini güncellemez; yeni sürümü indirme sayfasından elle kurarsınız.

---

<p align="center"><sub>© 2026 rbnbilisim · RbnSystemPro</sub></p>
