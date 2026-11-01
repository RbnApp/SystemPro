<#
.SYNOPSIS
    RbnSystemPro kurulum paketi üretimi (tek komut).

.DESCRIPTION
    Sıra: 1) sürümü Helpers\GlobalConfig.cs'den oku (tek kaynak)
          2) dotnet publish (Release, win-x64, self-contained=false)
          3) Inno Setup 6 (ISCC) ile sihirbazı derle
          4) çıktı yolu + boyut + SHA-256 yazdır

    Çıktı: bin\setup\RbnSystemPro-Setup-<sürüm>.exe  (bin/ .gitignore'da, commit edilmez)
    Sır/anahtar kullanılmaz; imzalama bu betiğin işi değildir.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Setup\derle.ps1
#>
[CmdletBinding()]
param(
    [switch]$NoPublish,
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'

$setupDir         = $PSScriptRoot
$repoRoot         = Split-Path -Parent $setupDir
$projectPath      = Join-Path $repoRoot 'RbnSystemPro.csproj'
$globalConfigPath = Join-Path $repoRoot 'Helpers\GlobalConfig.cs'
$issPath          = Join-Path $setupDir 'RbnSystemPro.iss'
$publishDir       = Join-Path $repoRoot 'bin\setup\publish'
$outputDir        = Join-Path $repoRoot 'bin\setup'

# ---------------------------------------------------------------- 1) Sürüm
if (-not (Test-Path -LiteralPath $globalConfigPath)) {
    throw "GlobalConfig.cs bulunamadi: $globalConfigPath"
}
$configText = Get-Content -LiteralPath $globalConfigPath -Raw
$versionMatch = [regex]::Match($configText, 'public\s+const\s+string\s+AppVersion\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"')
if (-not $versionMatch.Success) {
    throw "AppVersion okunamadi ($globalConfigPath). Beklenen: public const string AppVersion = ""ANA.ARA.YAMA"";"
}
$appVersion = $versionMatch.Groups[1].Value
Write-Host "Surum (GlobalConfig.AppVersion): $appVersion"

# ---------------------------------------------------------------- 2) ISCC
if (-not $IsccPath) {
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe') }
    if ($env:ProgramFiles)        { $candidates += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe') }
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw "Inno Setup 6 (ISCC.exe) bulunamadi. Beklenen: C:\Program Files (x86)\Inno Setup 6\ISCC.exe. Kurulumu kendimiz yapmayiz; Lena'ya bildirin."
}
Write-Host "ISCC: $IsccPath"

# ---------------------------------------------------------------- 3) Publish
$appExe = Join-Path $publishDir 'RbnSystemPro.exe'
if (-not $NoPublish) {
    if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
    Write-Host "dotnet publish: $projectPath ($Configuration / $RuntimeIdentifier / self-contained=false)"
    & dotnet publish $projectPath -c $Configuration -r $RuntimeIdentifier --self-contained false -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish basarisiz (cikis kodu $LASTEXITCODE)" }
}

if (-not (Test-Path -LiteralPath $appExe)) {
    throw "Publish ciktisinda RbnSystemPro.exe yok: $appExe"
}
$exeProductVersion = (Get-Item -LiteralPath $appExe).VersionInfo.ProductVersion
if ($exeProductVersion -ne $appVersion) {
    throw "Publish edilen exe'nin ProductVersion degeri ($exeProductVersion) GlobalConfig surumuyle ($appVersion) uyusmuyor."
}
Write-Host "Publish edilen exe ProductVersion: $exeProductVersion (uyumlu)"

# ---------------------------------------------------------------- 4) ISCC
Write-Host "ISCC derleniyor: $issPath"
& $IsccPath "/DAppVersion=$appVersion" "/O$outputDir" $issPath
if ($LASTEXITCODE -ne 0) { throw "ISCC basarisiz (cikis kodu $LASTEXITCODE)" }

$setupExe = Join-Path $outputDir "RbnSystemPro-Setup-$appVersion.exe"
if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "Kurulum paketi uretilmedi: $setupExe"
}

$setupItem = Get-Item -LiteralPath $setupExe
$sha256    = (Get-FileHash -LiteralPath $setupExe -Algorithm SHA256).Hash
$sizeMb    = [math]::Round($setupItem.Length / 1MB, 2)
$sizeBytes = $setupItem.Length

Write-Host ''
Write-Host '=== KURULUM PAKETI HAZIR ==='
Write-Host ("Yol     : {0}" -f $setupItem.FullName)
Write-Host ("Boyut   : {0} MB ({1} bayt)" -f $sizeMb, $sizeBytes)
Write-Host ("SHA-256 : {0}" -f $sha256)

[pscustomobject]@{
    Version  = $appVersion
    Path     = $setupItem.FullName
    SizeMB   = $sizeMb
    Bytes    = $sizeBytes
    Sha256   = $sha256
    Iscc     = $IsccPath
    Publish  = $publishDir
}
