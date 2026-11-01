; ============================================================================
;  RbnSystemPro — Windows kurulum sihirbazı (Inno Setup 6)
;
;  SÜRÜM TEK KAYNAKTAN GELİR: Helpers\GlobalConfig.cs -> AppVersion.
;  Bu dosyayı doğrudan ISCC ile derlemeyin; Setup\derle.ps1 sürümü okuyup
;  /DAppVersion=<sürüm> olarak geçirir. (Sürüm numarası burada YAZILMAZ.)
;
;  Derleme:  powershell -File Setup\derle.ps1
;  Çıktı:    bin\setup\RbnSystemPro-Setup-<sürüm>.exe  (bin/ .gitignore'da)
; ============================================================================

#ifndef AppVersion
  #error AppVersion tanımlı değil. Setup\derle.ps1 kullanın (ISCC'ye /DAppVersion=<sürüm> geçirir).
#endif

#define AppName "RbnSystemPro"
#define AppExeName "RbnSystemPro.exe"
#define AppPublisher "rbnbilisim"
#define AppUrl "https://github.com/RbnApp/SystemPro"
#define AppCopyright "© 2026 rbnbilisim"

[Setup]
; AppId KALICI ve BENZERSİZ: değiştirilirse Windows kurulumu yeni ürün sayar,
; yükseltme/kaldırma kaydı kopar. Bu GUID sabit tutulur.
AppId={{F81E6206-1D3E-4108-8628-D90F36632588}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
AppCopyright={#AppCopyright}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} {#AppVersion} kurulum sihirbazı
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoCopyright={#AppCopyright}
VersionInfoOriginalFileName={#AppName}-Setup-{#AppVersion}.exe

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Lisans: depo kökündeki MIT metni (CAPP-215)
LicenseFile=..\LICENSE

OutputDir=..\bin\setup
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
; Simge: tek kaynak Assets\app_icon.ico (16,24,32,48,64,128,256 px; DAPP-36).
; Ürün simgesi "A — nabız halkası" (DAPP-34); eski roket görseli ve yazısı kaldırıldı.
SetupIconFile=..\Assets\app_icon.ico
; Kaldırma kaydındaki simge kurulan uygulamanın kendisinden gelir; exe'ye aynı ico gömülür.
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}

; Sihirbaz görselleri (DAPP-36): yeni simgeden üretildi, Inno varsayılan/eski görsel yok.
; Boyutlar Inno 6.6+ belgesine göre (ISetup.chm -> WizardImageFile / WizardSmallImageFile):
; büyük görsel 164:314 oranı, "202x386'dan büyük önerilir" -> 202x386; küçük görsel kare,
; "58x58'den büyük önerilir" -> 110x110 (125% DPI'daki 77x77 alanı da örter).
WizardImageFile=sihirbaz-buyuk.bmp
WizardSmallImageFile=sihirbaz-kucuk.bmp
; 32-bit BMP'lerde alfa kanalı ön çarpımsız tanımlı (simgenin yuvarlatılmış köşeleri şeffaf).
WizardImageAlphaFormat=defined

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Uygulama x64 ve tek dosya (win-x64 publish); .NET 10 Desktop Runtime hedefte kurulu olmalı.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Uygulama manifesti requireAdministrator (app.manifest) -> sihirbaz da yönetici ister.
PrivilegesRequired=admin
; Yalnızca AÇIK komut satırı bayrağıyla (/CURRENTUSER | /ALLUSERS) yönetici olmayan
; kuruluma izin verilir; çift tıklayan kullanıcı her zaman yönetici (UAC) yolundan geçer.
PrivilegesRequiredOverridesAllowed=commandline

; Çalışan uygulama: mutex adı App.xaml.cs ile AYNI olmalı
; (Global\RbnSystemPro_Mutex_Unique_App_ID). Kurulum/kaldırma sırasında uygulama
; açıksa Inno kullanıcıya kapatma seçeneği sunar; kapatılan uygulama sonra yeniden açılmaz.
AppMutex=Global\RbnSystemPro_Mutex_Unique_App_ID
CloseApplications=yes
RestartApplications=no

[Languages]
; Inno Setup 6 Türkçe dil dosyası (C:\Program Files (x86)\Inno Setup 6\Languages\Turkish.isl)
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Kaynak: Setup\derle.ps1'in dotnet publish çıktısı (bin\setup\publish).
; Sembol dosyaları pakete girmez.
Source: "..\bin\setup\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Kurulum sonunda "RbnSystemPro'yu başlat" onay kutusu (sessiz kurulumda atlanır).
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; [UninstallDelete] BİLİNÇLİ OLARAK YOK.
; Kaldırma, yalnızca sihirbazın kurduğu dosyaları siler. Uygulamanın çalışırken
; {app}\Logs altında oluşturduğu günlük ve ayar dosyaları (SettingsService/LoggerService,
; AppDomain.BaseDirectory\Logs) KALIR — kullanıcı verisi silinmez, ekranda soru sorulmaz.
; Bu yüzden kaldırma sonrası {app} klasörü boş değilse Inno onu silmez; veri korunur.
