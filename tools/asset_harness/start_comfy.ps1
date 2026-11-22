$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$config = Get-Content (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
$portable = Join-Path $repoRoot $config.comfy_portable
try {
    $status = Invoke-RestMethod "$($config.comfy_url)/system_stats" -TimeoutSec 2
    Write-Output "ComfyUI is already responding at $($config.comfy_url)."
    exit 0
} catch { }
$process = Start-Process -FilePath (Join-Path $portable 'python_embeded/python.exe') -WorkingDirectory $portable -ArgumentList @('-s', 'ComfyUI/main.py', '--windows-standalone-build', '--disable-auto-launch', '--listen', '127.0.0.1', '--port', '8188', '--lowvram', '--reserve-vram', '1.5') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $repoRoot '.tools/comfy.stdout.log') -RedirectStandardError (Join-Path $repoRoot '.tools/comfy.stderr.log')
$process.Id | Set-Content (Join-Path $repoRoot '.tools/comfy.pid')
Write-Output "Started local ComfyUI process $($process.Id). Logs: .tools/comfy.stderr.log"
