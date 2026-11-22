$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$toolRoot = Join-Path $repoRoot '.tools'
New-Item -ItemType Directory -Force $toolRoot | Out-Null
New-Item -ItemType File -Force (Join-Path $toolRoot '.gdignore') | Out-Null
$archive = Join-Path $toolRoot 'comfy-portable.7z'
$url = 'https://github.com/Comfy-Org/ComfyUI/releases/download/v0.46.0/ComfyUI_windows_portable_nvidia.7z'
$expectedHash = '6a5255c7ec01fdc54bdbd8e4507073cf6acda2fbeea74ae1829a50681af5fac6'
if (-not (Test-Path $archive)) {
    & curl.exe -fL --retry 4 -C - -o "$archive.part" $url
    if ($LASTEXITCODE -ne 0) { throw 'Portable download failed; rerun to resume.' }
    if ((Get-FileHash "$archive.part" -Algorithm SHA256).Hash.ToLower() -ne $expectedHash) { throw 'Portable checksum mismatch.' }
    Move-Item -LiteralPath "$archive.part" -Destination $archive
}
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLower() -ne $expectedHash) { throw 'Portable checksum mismatch.' }
if (-not (Test-Path (Join-Path $toolRoot 'ComfyUI_windows_portable/python_embeded/python.exe'))) {
    & tar.exe -xf $archive -C $toolRoot
    if ($LASTEXITCODE -ne 0) { throw 'Portable extraction failed.' }
}
& (Join-Path $toolRoot 'ComfyUI_windows_portable/python_embeded/python.exe') (Join-Path $PSScriptRoot 'download_models.py')
if ($LASTEXITCODE -ne 0) { throw 'Model installation failed.' }
Write-Output 'Portable ComfyUI and model checksums verified. Run start_comfy.ps1 next.'
