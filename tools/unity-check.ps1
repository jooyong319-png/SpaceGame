# unity-check.ps1 — Unity 배치 모드로 컴파일 검사
#
# 사람이 에디터를 열지 않고도 "스크립트가 컴파일되는가"를 확인한다.
# ⚠️ Unity 에디터가 켜져 있으면 실패한다 — 같은 프로젝트를 두 인스턴스가 못 연다.
#
# 사용법:  powershell -ExecutionPolicy Bypass -File tools\unity-check.ps1

$ErrorActionPreference = "Stop"

$Editor  = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
$Project = Split-Path -Parent $PSScriptRoot
$GameDir = Join-Path $Project "game"
$LogDir  = Join-Path $Project "tools\logs"
$Log     = Join-Path $LogDir "compile.log"

if (-not (Test-Path $Editor)) { Write-Host "[X] Unity를 못 찾음: $Editor"; exit 1 }
if (-not (Test-Path $GameDir)) { Write-Host "[X] 프로젝트를 못 찾음: $GameDir"; exit 1 }

$running = Get-Process Unity -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[X] Unity 에디터가 실행 중이다. 닫고 다시 실행할 것 (PID: $($running.Id -join ', '))"
    exit 2
}

# 🔴 동시 실행 방지.
# Unity 프로세스 확인만으로는 '기동 중'인 다른 실행을 못 잡는다 —
# 실제로 두 배치가 겹쳐 Unity가 0xC000013A로 죽은 적이 있다(2026-08-19).
$Lock = Join-Path $env:TEMP "salvagerun-unity.lock"
if (Test-Path $Lock) {
    $age = (Get-Date) - (Get-Item $Lock).LastWriteTime
    if ($age.TotalMinutes -lt 30) {
        Write-Host "[X] 다른 Unity 배치가 실행 중이다 ($([math]::Round($age.TotalMinutes,1))분 전 시작). 끝나면 다시 실행할 것."
        exit 3
    }
    Remove-Item $Lock -Force   # 30분 넘은 것은 죽은 락으로 본다
}
New-Item -ItemType File -Path $Lock -Force | Out-Null

# 🔴 본문을 함수로 감싼다. 스크립트 최상위에서 `return`을 쓰면 **스크립트가 통째로 끝나서**
#    아래 `exit $code`가 안 돌고, 컴파일이 깨졌는데도 종료 코드 0이 나간다.
#    그러면 `unity-test.ps1`이 **깨진 코드로 테스트를 돌린다.**
function Invoke-CompileCheck {

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
if (Test-Path $Log) { Remove-Item $Log -Force }

Write-Host "[*] 컴파일 검사 시작... (첫 실행은 에셋 임포트로 몇 분 걸릴 수 있다)"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# 🔴 `-Wait`를 쓰면 **안 된다.** `Start-Process -Wait`는 프로세스 하나가 아니라
#    **자식까지 전부** 기다린다. 유니티는 배치 도중 Hub·Bee 같은 자식을 띄우는데,
#    그중 하나라도 살아남으면 유니티가 끝났는데도 **영원히 기다린다.**
#    2026-08-23에 실제로 걸렸다 — 컴파일 로그에는 "Exiting batchmode successfully"가
#    찍혀 있는데 스크립트만 10분 넘게 멈춰 있었다.
#
#    그래서 `unity-test.ps1`과 같은 방식으로 **유니티가 뜨는 것을 확인하고,
#    사라질 때까지** 기다린다.
$proc = Start-Process -FilePath $Editor -PassThru -NoNewWindow -ArgumentList @(
    "-batchmode", "-quit", "-nographics",
    "-projectPath", $GameDir,
    "-logFile", $Log
)

$appeared = $false
for ($i = 0; $i -lt 60; $i++) {
    if (Get-Process Unity -ErrorAction SilentlyContinue) { $appeared = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $appeared) { Write-Host "[!] 유니티가 뜨지 않았다" }
while (Get-Process Unity -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 2 }

$sw.Stop()
$exitCode = if ($proc.HasExited) { $proc.ExitCode } else { "?" }
Write-Host "[*] 종료 코드 $exitCode · $([math]::Round($sw.Elapsed.TotalSeconds,1))초"

if (-not (Test-Path $Log)) { Write-Host "[X] 로그가 없다"; return 1 }

# 컴파일 에러만 추려낸다
$errors = Select-String -Path $Log -Pattern "error CS\d+" -AllMatches |
          ForEach-Object { $_.Line.Trim() } | Select-Object -Unique

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "===== 컴파일 에러 $($errors.Count)건 ====="
    $errors | ForEach-Object { Write-Host $_ }
    return 1
}

$warns = Select-String -Path $Log -Pattern "warning CS\d+" -AllMatches |
         ForEach-Object { $_.Line.Trim() } | Select-Object -Unique

Write-Host ""
Write-Host "===== 컴파일 통과 ====="
if ($warns.Count -gt 0) {
    Write-Host "경고 $($warns.Count)건:"
    $warns | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
}
Write-Host "전체 로그: $Log"
return 0
}

$code = 1
try { $code = Invoke-CompileCheck }
finally { if (Test-Path $Lock) { Remove-Item $Lock -Force -ErrorAction SilentlyContinue } }

# 🔴 `exit`는 try 안에서 쓰면 finally를 건너뛴다 — 그래서 컴파일이 깨질 때마다
#    락 파일이 남아 다음 실행이 "다른 배치가 실행 중"으로 30분간 막혔다 (2026-08-23).
#    코드만 담아 두고 여기서 한 번만 나간다.
exit $code
