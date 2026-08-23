# unity-webgl-bg.ps1 — WebGL 빌드를 **세션에서 떼어내** 굽는다.
#
# 🔴 왜 필요한가 (2026-08-22~23에 세 번 겪음):
#    WebGL 빌드는 10분이 넘는다. 그런데 이걸 Claude 세션의 자식 프로세스로 돌리면
#    **세션이 정리될 때 빌드도 같이 죽는다.** 매번 `Link_WebGL_wasm` 부근에서
#    끊긴 것이 그 때문이었다 — 링커가 실패한 게 아니라 **부모가 사라진 것**이다.
#    (RAM 20GB 여유 · 디스크 1.6TB 여유였으므로 자원 문제가 아니었다)
#
# 🔴 그래서 `Start-Process`로 **완전히 독립된 프로세스**를 띄우고 즉시 빠져나온다.
#    진행 상황은 `tools/logs/webgl-status.txt`(RUNNING/OK/FAIL/ZIPFAIL)로 확인한다.
#
# 사용법:
#    tools\unity-webgl-bg.ps1          — 빌드 시작하고 바로 반환
#    Get-Content tools\logs\webgl-status.txt   — 상태 확인

$ErrorActionPreference = "Stop"

$Project = Split-Path -Parent $PSScriptRoot
$LogDir  = Join-Path $Project "tools\logs"
$Status  = Join-Path $LogDir "webgl-status.txt"
$Inner   = Join-Path $PSScriptRoot "unity-webgl.ps1"
$OutLog  = Join-Path $LogDir "webgl-bg.out.txt"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$running = Get-Process Unity -ErrorAction SilentlyContinue
if ($running) { Write-Host "[X] Unity가 이미 실행 중이다. 끝나길 기다릴 것"; exit 2 }

Set-Content -Path $Status -Value "RUNNING" -Encoding utf8

# -WindowStyle Hidden + 부모와 끊긴 새 프로세스. 이 스크립트가 끝나도 계속 돈다.
Start-Process -FilePath "powershell.exe" -WindowStyle Hidden -ArgumentList @(
    "-ExecutionPolicy", "Bypass",
    "-NoProfile",
    "-File", $Inner
) -RedirectStandardOutput $OutLog -RedirectStandardError (Join-Path $LogDir "webgl-bg.err.txt")

Write-Host "[*] 빌드를 백그라운드로 띄웠다 (세션과 분리됨)."
Write-Host "    상태:  $Status"
Write-Host "    출력:  $OutLog"
Write-Host "    로그:  $(Join-Path $LogDir 'webgl-build.log')"
