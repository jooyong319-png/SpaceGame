# unity-webgl.ps1 — WebGL 빌드를 배치 모드로 굽는다 (에디터를 열지 않고)
#
# 🔴 첫 빌드는 IL2CPP 변환 때문에 10~25분 걸린다. 두 번째부터는 캐시로 훨씬 빠르다.
# ⚠️ Unity 에디터가 켜져 있으면 실패한다.
# ⚠️ .ps1은 BOM 없이 저장하면 PowerShell 5.1이 ANSI로 읽어 한글이 깨진다 — 이 파일은 BOM 포함.

$ErrorActionPreference = "Stop"

$Editor  = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
$Project = Split-Path -Parent $PSScriptRoot
$GameDir = Join-Path $Project "game"
$LogDir  = Join-Path $Project "tools\logs"
$Log     = Join-Path $LogDir "webgl-build.log"
$OutDir  = Join-Path $Project "build\webgl"
$Status  = Join-Path $LogDir "webgl-status.txt"

if (-not (Test-Path $Editor)) { Write-Host "[X] Unity를 못 찾음: $Editor"; exit 1 }

$running = Get-Process Unity -ErrorAction SilentlyContinue
if ($running) { Write-Host "[X] Unity 에디터가 실행 중이다. 닫고 다시 실행할 것"; exit 2 }

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
if (Test-Path $Log) { Remove-Item $Log -Force }

# 🔴 상태 파일을 남긴다. 빌드가 10분 넘게 걸려서
#    하나의 턴 안에 끝나지 않는다 — 끝났는지를 **파일로** 확인해야
#    세션이 바뀔어도 결과를 잃지 않는다.
Set-Content -Path $Status -Value "RUNNING" -Encoding utf8

Write-Host "[*] WebGL 빌드 시작... (첫 빌드는 IL2CPP 때문에 10~25분 걸린다)"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# 🔴 `& $Editor ...`로 부르면 **기다리지 않고 바로 빠져나온다.**
#    그러면 빌드가 멀쩡히 도는 중인데 스크립트는 "실패"라고 보고한다 (2026-08-22에 겪음).
#    `Start-Process -Wait`로 확실히 기다린다 — unity-check.ps1과 같은 방식.
$proc = Start-Process -FilePath $Editor -PassThru -Wait -NoNewWindow -ArgumentList @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $GameDir,
    "-executeMethod", "SalvageRun.EditorTools.WebGLBuild.Build",
    "-logFile", $Log
)
$code = $proc.ExitCode

$sw.Stop()
Write-Host ("[*] 종료 코드 {0} · {1:N1}분" -f $code, $sw.Elapsed.TotalMinutes)

if (Test-Path $Log) {
    $errors = Select-String -Path $Log -Pattern "error CS|BuildFailedException|Build failed" -ErrorAction SilentlyContinue
    if ($errors) {
        Write-Host "`n===== 빌드 에러 ====="
        $errors | Select-Object -First 15 | ForEach-Object { Write-Host ("  " + $_.Line.Trim()) }
    }
}

if ($code -eq 0 -and (Test-Path (Join-Path $OutDir "index.html"))) {
    $size = (Get-ChildItem $OutDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "`n===== 빌드 성공 ====="
    Write-Host ("  위치: {0}" -f $OutDir)
    Write-Host ("  크기: {0:N1} MB" -f $size)
    Write-Host ""
    # 🔴 zip까지 여기서 만든다. 손으로 압축하면 Windows가 경로를 역슬래시로 써서
    #    itch에서 Build 폴더가 안 풀린다 (2026-08-22에 겪음). 자세한 건 make-zip.py
    Write-Host ""
    Write-Host "[*] zip 만드는 중..."
    & python (Join-Path $PSScriptRoot "make-zip.py")
    if ($LASTEXITCODE -ne 0) { Set-Content -Path $Status -Value "ZIPFAIL" -Encoding utf8; Write-Host "[X] zip 실패"; exit 1 }
    Set-Content -Path $Status -Value "OK" -Encoding utf8

} else {
    Set-Content -Path $Status -Value "FAIL" -Encoding utf8
    Write-Host "`n[X] 빌드 실패. 전체 로그: $Log"
    exit 1
}
