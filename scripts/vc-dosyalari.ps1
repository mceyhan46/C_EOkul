# Visual C++ calisma zamani dosyalarini Visual Studio'nun redist klasorunden redist/x64 altina kopyalar.
# Dosyalar depoya eklenmez. Dagitim Visual Studio lisans kosullarina (REDIST listesi) baglidir.
$ErrorActionPreference = 'Stop'
$names = 'msvcp140.dll', 'msvcp140_1.dll', 'vcruntime140.dll', 'vcruntime140_1.dll'

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw 'Visual Studio bulunamadi (vswhere.exe yok).' }
$vs = & $vswhere -latest -products * -property installationPath
$version = (Get-Content (Join-Path $vs 'VC\Auxiliary\Build\Microsoft.VCRedistVersion.default.txt') -Raw).Trim()
$source = Get-ChildItem (Join-Path $vs "VC\Redist\MSVC\$version\x64") -Directory -Filter 'Microsoft.VC*.CRT' |
    Select-Object -First 1
if (-not $source) { throw "CRT redist klasoru bulunamadi: $version" }

$target = Join-Path $PSScriptRoot '..\redist\x64'
New-Item -ItemType Directory -Force $target | Out-Null
foreach ($name in $names) {
    $file = Join-Path $source.FullName $name
    $signature = Get-AuthenticodeSignature $file
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "Imza dogrulanamadi: $file ($($signature.Status))"
    }
    Copy-Item $file $target -Force
    Write-Host "$name  surum $((Get-Item $file).VersionInfo.FileVersion)  kaynak $($source.Name)"
}
