# architecture — 코드 어디에 뭐가 있나 (궤도 청소부)

> **2026-09-24 새로 썼다.** 게임 규칙은 [[game]]이 정본이고, 여기는 코드 지도 · 도구 · 함정만.
> 옛 살비지런 코드 지도는 [[archive/salvagerun-0902/architecture]]. **문서와 코드가 다르면 코드가 맞다.**

## 폴더

```
game/Assets/_Project/
  Scripts/Orbit/
    Sim/SweepSim.cs      🔴 규칙 전부 (순수 C#, 유니티 없음) — 트리 · 행성 · 청구서 · 무기 · 연쇄 · 파산 · 가게 · 복권 · 무한 궤도
    Sim/Market.cs        증권 — 종목 8 · 봉 · 뉴스가 주가를 민다
    Sim/Parts.cs         부품 22종
    SweepGame.cs         화면 — 카메라 · 도트 격자 · 스프라이트 · 연출(사건 → 효과) · 포대 · 입력
    SweepHud.cs          OnGUI 본체 — 출동 HUD · 결산 · 정비고 트리 · 설정 · 엔딩 · 크레딧 (partial)
    SweepHudRooms.cs     조종실 소품 · 방 넘기기(Strip) · 증권 방 · 홀로그램 · 파산 단추 · 막 카드
    SweepHudParts.cs     부품 가게 방
    SweepHudStockFx.cs   증권 연출 · 속보 앵커 · 내 주식 칩
    SweepHudLobby.cs     로비
    OrbitSfx.cs          소리 합성 (파일 없음)
    PlanetArt.cs · OrbitArt.cs · KNum.cs
  Resources/             ship · junk · att · anim · planet_anim · bgparts · news (픽셀랩 그림) · ArtUnused/
  Editor/PaceBotTmp.cs   ⚠️ 봇 돌릴 때만 잠깐 — 커밋 금지, 끝나면 지운다
tools/pacing/Program.cs  페이스 봇 (dotnet · 또는 에디터 안에서)
```

## 데이터 흐름

`SweepSim.Tick()` → `Events` 큐(`SwEv`) → `SweepGame.Consume()`이 연출 · 소리로 바꾼다. 화면은 sim을 **읽기만** 한다.
저장은 PlayerPrefs `sweep.state` · `sweep.meta` (JSON). 새 필드는 기본값으로 채워진다.
방 번호 `hud.flow`: 0 출동 · 1 결산 · 2 조종실 · 3 정비고 · 4 증권 · 5 부품 가게. `hud.lobby`가 켜져 있으면 로비.

## 도구

### 유니티 MCP (`unity-orbit` · Unity_RunCommand)

- 🔴 **먼저 `EditorApplication.isPlaying` 확인** — 사장님이 플레이 중이면 멈추고 말씀드린다. 유니티 끄지 않기.
- **컴파일**: `AssetDatabase.Refresh(ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();` → `%LOCALAPPDATA%/Unity/Editor/Editor.log`의 「Domain Reload Profiling」 줄 수가 늘 때까지 기다림 → 그다음 명령은 따로.
- **플레이 캡처**: `sweep.state/meta`를 `sweep.backup.*`에 복사 → Play → `EditorApplication.update`로 단계 진행 → `ScreenCapture.CaptureScreenshot` → 끝나면 되돌림. 🔴 **찍는 틱과 화면을 바꾸는 틱을 나눌 것** (같은 틱이면 바뀐 첫 프레임이 찍힌다).
- 시험용 훅: `hud.TestLotto` · `TestEnd` · `testTip/testTipId` · `testShopHot`(가게 깜빡일 칸) · `Deny/WhyNot` · `game.TestAim`.
- RunCommand 안에서 `System.Reflection` 네임스페이스는 막혀 있다 (`Type.GetMethod`는 된다).

### 페이스 봇

- `tools/pacing/Program.cs`를 `sed`로 `public static class PaceBotTmp` · `public static void Main`으로 바꿔 `Assets/_Project/Editor/PaceBotTmp.cs`에 복사 → 컴파일 → 스레드에서 `Type.GetType("PaceBotTmp, SalvageRun.Editor").GetMethod("Main")` 실행, `Console.SetOut`으로 파일에.
- 🔴 끝나면 `Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush=true})`로 되돌린다 — 안 하면 다음 컴파일이 `ObjectDisposedException`.
- 재는 것: 끝나는 분 · 파산 수 · 행성 허가 시각. 재미 · 난이도는 안 잰다.

### 🧪 헤드리스 테스트 (유니티 없이 · 09-25)

- `cd tools/pacing && dotnet run -c Release -- test [씨앗 수]` — 기본 12. 유니티가 켜져 있어도 된다 (Sim 폴더를 그대로 컴파일).
- ⚠️ 윈도우 「애플리케이션 제어」가 `bin/` 의 새 dll 을 막을 때가 있다 (0x800711C7) → `dotnet build -c Release -o <스크래치 폴더>` 뒤 `dotnet <폴더>/pacing.dll test 100`.
- ① 봇 N판 끝까지(분 분포 · 파산) ② 무작위 손 퍼징(아무 칸 · 대출 · 복권 · 가게 · 파산 · 행성 · 수동 사격 — 걸음마다 돈 · 빚 · 칸 레벨 · 가격 · 구역 검사) ③ 밀린 저장 옮기기 ④ 구역이 끝까지 차례로 열리는가 ⑤ 무한 궤도 ⑥ 파산 10연속 + 다시 불러오기 ⑦ 무한 궤도 60층 ⑧ 가게 소모품 ⑨ 복권 칸 ⑩ 수동 사격 ⑪ 트리 배치(같은 자리에 칸 둘 · 한 자리에 쌓인 단계).
- 통과하면 끝 줄 「✅ 모두 통과」, 실패하면 씨앗 · 걸음을 찍고 종료 코드 1. 첫 실행에 무한 궤도 → 청산 출동 버그를 잡았다.
- 🔴 규칙(Sim)을 고치면 커밋 전에 한 번 돌린다.

### 🎚 소리 크기 재기 (유니티 플레이 · 09-25)

- 못 들으니 숫자로 — `AudioListener.GetOutputData`로 매 프레임 출력을 받아 평균(RMS) · 최고치 · 찢어짐(>0.99) 비율.
- 곡마다 음악만(`OrbitMusic.Force`, 효과음 0) 4초 · 출동 효과음만 6초 · 둘 다 6초. 측정 동안 전체 소리 100%, 끝나면 되돌린다.
- 지금 기준: 음악 평균 0.044~0.062 · 출동 효과음 0.144 · 최고 0.88 · 찢어짐 0. 곡 목표는 `OrbitMusic.Target`, 효과음 전체는 `OrbitSfx.SfxBase`(0.6).

### 픽셀랩 (PixelLab MCP)

- 사장님 구독 Tier 1 (월 2000 생성). 키는 `C:/Make_Game/.mcp.json` — **커밋 금지**.
- `create_image_pro_flash` (5~9 생성, 품질 좋음) · `create_image_pixen/pixflux` (1) · `animate_image` (64² 8장 = 1). 받기: `https://api.pixellab.ai/mcp/images/{job}/download?index=N`.
- 가져오기: Sprite · Point · 무압축 · 밉맵 없음. 두 장이 조금씩 다르게 나오면(입 벌린 앵커) **바뀐 부분만 오려 붙인다** — 통째로 바꾸면 깜빡인다.
- 애니메이션을 뽑을 땐 사장님께 먼저 여쭌다 (자리 비우실 때 「마음대로」 허락은 그때만).

## 도트 격자

카메라가 `pixRT`(모니터 높이 ÷ 정수 ≈ 540줄)에 그리고 OnGUI가 `ViewRect`에 키워 그린다. 마우스 ↔ 월드는 `ScreenToWorld` · `WorldToScreen`을 거친다. 시안 좌표는 1280×720(`TW(mx,my)`), HUD는 높이 600 기준(`RefH`).

## 🔴 반복해서 밟은 함정

1. **python 패치로 줄 가운데에 `//` 주석을 넣지 말 것** — 뒤 코드를 삼켜 컴파일이 깨지고, 봇이 옛 코드를 돌렸다 (두 번).
2. **python 문자열 안의 `\n`** — C# 문자열에 실제 줄바꿈으로 들어가 깨진다. `chr(92)+'n'`로.
3. **모달 뒤 버튼** — OnGUI는 먼저 그린 버튼이 클릭을 가져간다. 모달이 뜨면 뒤를 `GUI.enabled=false`로 (뉴스 닫기가 뒤 전광판에 먹혀 다시 열렸다).
4. **시뮬이 조준점을 잠깐 바꾸면**(FireAt) 포구 위치 비교가 빗나간다 — 연출 쪽은 「지금 쏘는 무기」(`curW`)로 판단.
5. **구역 「다 찍기」 조건에 봇이 안 사는 칸이 들어가면** 봇이 끝나지 않는다 (906분).
6. PlayerPrefs 되돌리기를 빼먹으면 사장님 저장이 망가진다. 🔴 **`isPlaying=false` 한 틱 안에서 되돌리면 안 된다** — 게임이 꺼지면서 한 번 더 저장해 덮어쓴다(09-25). 플레이가 완전히 멈춘 뒤 되돌리고 `sweep.state == sweep.backup.state` 확인.
7. 🔴 **새 트리 칸은 `Nodes` 맨 뒤에만** — 저장은 칸 번호로 레벨을 든다. 09-24 밤 중간(106번)에 12칸을 끼웠다가 뒤 칸 12개가 밀렸다. `SweepState.layout`(지금 2)으로 밀린 저장(126 · 130칸)을 id 기준으로 옮겨 고쳤다.

8. 🔴 **트리 칸 자리(`Layout`)** — 단계가 여럿인 칸은 `dx`/`dy`가 0이면 Ⅰ·Ⅱ·Ⅲ이 한 자리에 쌓여 「같은 곳을 두 번 눌러야」 한다. 새 칸은 빈자리 확인 필수. 09-24 밤 붙인 28칸 중 26곳이 겹쳐 있었다 → 테스트 11 「트리 배치」가 잡는다.

## 태그

#orbitsweeper #architecture #unity #tools
