# models/models.json icindeki modelleri indirir ve SHA-256 ile dogrular.
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\models'
$items = Get-Content (Join-Path $dir 'models.json') -Raw -Encoding utf8 | ConvertFrom-Json
foreach ($m in $items) {
    $path = Join-Path $dir $m.file
    if (-not (Test-Path $path)) {
        Write-Host "Indiriliyor: $($m.file)"
        Invoke-WebRequest -Uri $m.url -OutFile "$path.tmp" -MaximumRetryCount 3 -RetryIntervalSec 5
        Move-Item "$path.tmp" $path -Force
    }
    $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $m.sha256) {
        Remove-Item $path -Force
        throw "SHA-256 uyusmuyor: $($m.file) beklenen=$($m.sha256) gelen=$hash"
    }
    Write-Host "Dogrulandi: $($m.file)"
}
