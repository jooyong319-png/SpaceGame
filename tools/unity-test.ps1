# unity-test.ps1 — Unity 배치 모드로 PlayMode 테스트(밸런스 시뮬레이션) 실행
#
# ⚠️ Unity 에디터가 켜져 있으면 실패한다.
# ⚠️ .ps1은 BOM 없이 저장하면 PowerShell 5.1이 ANSI로 읽어 한글이 깨진다 — 이 파일은 BOM 포함.
#
# 사용법:  powershell -ExecutionPolicy Bypass -File tools\unity-test.ps1

# 🔴 -Only 로 테스트 하나만 돌린다. 전체는 7분간 CPU를 꽉 써서 팬이 시끄럽다 —
#    한 가지만 확인할 때 전체를 도는 건 난비다 (2026-08-21).
#    예:  powershell -File tools/unity-test.ps1 -Only VerifyDeterminism
param([string]$Only = "", [switch]$Full)

$ErrorActionPreference = "Stop"

$Editor  = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
$Project = Split-Path -Parent $PSScriptRoot
$GameDir = Join-Path $Project "game"
$LogDir  = Join-Path $Project "tools\logs"
$Log     = Join-Path $LogDir "test.log"
$Results = Join-Path $LogDir "test-results.xml"

if (-not (Test-Path $Editor)) { Write-Host "[X] Unity를 못 찾음: $Editor"; exit 1 }

$running = Get-Process Unity -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[X] Unity 에디터가 실행 중이다. 닫고 다시 실행할 것"
    exit 2
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
foreach ($f in @($Log, $Results)) { if (Test-Path $f) { Remove-Item $f -Force } }

# 🔴 컴파일 검사를 먼저 한다.
# 컴파일이 깨진 채로 PlayMode 테스트를 시키면 Unity가 셧다운 중 비정상 종료(0xC000013A)해서
# 원인이 "크래시"로 보인다 — 실제로는 그냥 컴파일 에러다. (2026-08-19에 한 번 헛짚음)
Write-Host "[*] 1/2 컴파일 검사..."
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "unity-check.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host "[X] 컴파일 단계에서 실패. 테스트를 건너뛴다."
    exit 1
}

Write-Host "[*] 2/2 PlayMode 테스트 실행... (수 분 걸릴 수 있다)"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# -runTests는 자동 종료하므로 -quit을 붙이지 않는다.
# PlayMode 테스트는 렌더링이 필요할 수 있어 -nographics를 쓰지 않는다.
# ⚠️ 배열을 먼저 변수로 만든다. `-ArgumentList @(...) + @(...)` 는
#    PowerShell이 `+` 를 새 위치 인자로 읽어서 파싱 에러가 난다.
$unityArgs = @(
    "-batchmode",
    "-projectPath", $GameDir,
    "-runTests",
    "-testPlatform", "PlayMode",
    "-testResults", $Results,
    "-logFile", $Log
)
if ($Only) { $unityArgs += @("-testFilter", $Only) }

# 🔴 **조용하게 돌린다** (2026-08-23 사장님 요청: *"시뮬 돌릴땐 소리 안 나오게"*).
#
#    유니티를 그냥 돌리면 6코어를 꽉 써서 팬이 풀로 돌아간다.
#    시뮬은 급한 일이 아니므로 **우선순위를 낮추고 코어를 절반만** 쓴다.
#    느려지지만 그 대신 조용하고, 사장님이 다른 일을 하셔도 안 느려진다.
#
#    `-Full`로 끄고 전력을 다 쓸 수 있다 (자리 비울 때용).
$proc = Start-Process -FilePath $Editor -PassThru -NoNewWindow -ArgumentList $unityArgs

if (-not $Full) {
    try {
        Start-Sleep -Milliseconds 900          # 프로세스가 자리잡을 틈을 준다
        $proc.PriorityClass = "BelowNormal"

        $cores = [Environment]::ProcessorCount
        $use   = [Math]::Max(2, [Math]::Floor($cores / 2))
        $mask  = 0
        for ($i = 0; $i -lt $use; $i++) { $mask = $mask -bor (1 -shl $i) }
        $proc.ProcessorAffinity = [IntPtr]$mask

        Write-Host ("[*] 조용 모드 — 우선순위 BelowNormal · 코어 {0}/{1} 사용 (-Full로 해제)" -f $use, $cores)
    } catch {
        Write-Host "[!] 조용 모드 설정 실패 — 그냥 돌린다: $($_.Exception.Message)"
    }
}

# 🔴 `Start-Process -PassThru`로 받은 객체는 `WaitForExit()`가
#    **즉시 돌아오는 경우가 있다** (핸들을 안 잡은 채로 넘어온다).
#    2026-08-23에 조용 모드를 넣으면서 `-Wait`를 뺀 후 이 증상이 났다 —
#    유니티는 돌고 있는데 **스크립트만 먼저 끝나** 결과를 못 읽었다.
#    `Wait-Process`는 PID로 기다리므로 확실하다.
# 🔴 `Wait-Process -Id`도 믿을 수 없다 — 유니티가 자기를 다시 띄우면
#    처음 받은 PID는 먼저 끝나고, 실제 작업은 다른 프로세스가 계속한다.
#    그러면 스크립트만 먼저 끝나 **결과를 못 읽는다** (2026-08-23에 두 번 겪음).
#    그래서 **유니티가 하나도 안 남을 때까지** 직접 확인한다.
# 1) 먼저 **뜨는 것을 확인**한다. 유니티가 뜨기까지 몇 초 걸리는데,
#    그 전에 폴링하면 **빈 상태를 보고 끝난 줄 안다** (2026-08-23에 그러했다).
$appeared = $false
for ($i = 0; $i -lt 60; $i++) {
    if (Get-Process Unity -ErrorAction SilentlyContinue) { $appeared = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $appeared) { Write-Host "[!] 유니티가 뜨지 않았다" }

# 2) 그리고 **사라질 때까지** 기다린다
while (Get-Process Unity -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 2 }

$sw.Stop()
Write-Host "[*] 끝남 · $([math]::Round($sw.Elapsed.TotalSeconds,1))초"

if (Test-Path $Log) {
    $errors = Select-String -Path $Log -Pattern "error CS\d+" -AllMatches |
              ForEach-Object { $_.Line.Trim() } | Select-Object -Unique
    if ($errors.Count -gt 0) {
        Write-Host ""
        Write-Host "===== 컴파일 에러 $($errors.Count)건 ====="
        $errors | ForEach-Object { Write-Host $_ }
        exit 1
    }
}

# 시뮬레이션 출력 뽑아내기
if (Test-Path $Log) {
    $lines = Get-Content $Log
    $start = ($lines | Select-String -Pattern "\[SIM\]" | Select-Object -First 1).LineNumber
    if ($start) {
        Write-Host ""
        $lines[($start-1)..([Math]::Min($start+40, $lines.Count-1))] | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "[!] [SIM] 출력이 없다. 테스트가 실행되지 않았을 수 있다."
    }
}

if (Test-Path $Results) {
    [xml]$xml = Get-Content $Results
    $r = $xml.SelectSingleNode("//test-run")
    if ($r) {
        Write-Host ""
        Write-Host "===== 테스트 결과 ====="
        Write-Host "총 $($r.total) · 통과 $($r.passed) · 실패 $($r.failed) · 스킵 $($r.skipped)"
        $script:failedCount = [int]$r.failed
    }
    $failed = $xml.SelectNodes("//test-case[@result='Failed']")
    foreach ($f in $failed) {
        Write-Host ""
        Write-Host "[실패] $($f.fullname)"
        Write-Host $f.failure.message.InnerText
        Write-Host $f.failure.'stack-trace'.InnerText
    }
} else {
    Write-Host "[!] 결과 XML이 없다: $Results"
}

Write-Host ""
Write-Host "전체 로그: $Log"
# 🔴 종료 코드는 **결과 XML**로 정한다.
#    유니티가 자기를 다시 띄우면 `$proc.ExitCode`는 실제 결과가 아니다.
if ($script:failedCount -gt 0) { exit 2 }
exit 0
