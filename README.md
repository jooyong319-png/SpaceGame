# SALVAGE RUN (임시명)

**우주 쓰레기를 견인해 오는 인크리멘탈 채굴 게임.**
무기는 알아서 부순다. 플레이어가 정하는 것은 **어디에 서고, 무엇을 싣고 돌아올지**다.
많이 매달수록 느려진다 — 그 저울이 이 게임의 특색이다.

- 엔진: **Unity 6.3 LTS (6000.3.22f1) · URP 2D** · 플랫폼: WebGL 데모 + PC(Windows)
- 유통: **itch.io → 스팀**
- 시작 2026-08-19 · 상태(2026-09-02): **플레이 가능. 아트는 코드로 찍은 임시 도트.**
  🔴 **숫자는 맞는데 재미가 없다는 것이 지금의 문제다** — `wiki/playtests.md` 맨 아래

---

## 🔴 판단이 갈리면 여기로

**문서는 `wiki/` 하나로 모았다** (2026-09-02 리셋 — 옛 `docs/`는 `wiki/archive/docs-rev5/`).
새 작업 세션(사람이든 LLM이든)은 **이 순서로 세 장만** 읽으면 된다:

```
1. wiki/game.md       ← 🔴 정본. 지금 게임이 무엇인가 (코드에서 직접 읽어 씀)
2. wiki/playtests.md  ← 사람이 해보고 뭐라 했나 (맨 아래부터)
3. wiki/todo.md       ← 다음에 뭘 하나 · 사장님이 정하실 것
```

카탈로그는 `wiki/index.md`, 위키 쓰는 법은 `wiki/SCHEMA.md`.
⛔ `wiki/archive/`는 **전부 폐기된 설계**다. 지금의 근거로 쓰지 말 것.

## 폴더

| 경로 | 내용 |
|---|---|
| `wiki/` | 🔴 문서 전부 (옵시디언 볼트로 열면 `[[링크]]`가 동작) |
| `wiki/archive/` | ⛔ 폐기된 설계 · 옛 측정 · 옛 `docs/`. **따르지 말 것** |
| `game/` | Unity 프로젝트 루트 |
| `tools/` | 헤드리스 컴파일 / 테스트 / 빌드 스크립트 + 로그 |
| `build/` | WebGL 산출물 (`SalvageRun-webgl.zip`) |
| `art/src/` | 도트 **원본**(.aseprite 등). Unity Assets 밖에 둔다 |

상위 통합 위키: `d:/Gcalen/wiki/` — Unity·웹·배포 등 **어느 프로젝트든 재사용되는 기술 지식**.
저장소 밖 로컬 폴더라 원격 세션은 못 읽는다 → `wiki/unified-wiki-inbox.md` 경유.

코드 지도와 "규칙이 어디에 있는가" 표는 **`wiki/architecture.md`**에 있다.

---

## 실행 방법

```
Unity에서 씬을 열고 →  메뉴 SalvageRun > 데이터 에셋 생성
                    →  메뉴 SalvageRun > 그레이박스 준비
                    →  Play
```

### 조작 — 🔴 키보드뿐이다 (마우스 안 쓴다)

| 키 | 동작 |
|---|---|
| `WASD` / 방향키 | 이동 (관성이 있다) |
| `Shift` | 대시 |
| `Space` | 줍기 · 메뉴 확인 |
| `W`/`S` + `Enter` | 메뉴 이동/확인 |
| `T` | 정비소(테크트리) · `E` 출발 |
| `Esc` | 뒤로 |
| `K` | 🛠 조절 패널 (플레이 중 수치를 돌려 본다) |
| `B` / `G` | 🛠 봇 토글 / 크레딧 치트 |

## 에디터 없이 검사하기

```
tools/unity-check.ps1                  컴파일만 (1~2분)
tools/unity-test.ps1 -Only SmokeTest   스모크 (수 분)
tools/unity-test.ps1                   밸런스 시뮬 포함 (~12분, CPU를 꽉 쓴다)
tools/unity-webgl-bg.ps1               WebGL 빌드
```

⚠️ **Unity 에디터가 켜져 있으면 실행되지 않는다** (락 파일로 막아 뒀다).
두 Unity 인스턴스가 같은 프로젝트를 열면 `0xC000013A`로 죽는데 원인이 전혀 안 보인다.

---

## ⚠️ 손대기 전에 알아야 할 것

- **제목과 URL은 공개 후 바꾸지 않는다.** 스팀 스토어 주소·itch 주소가 제목에서 나온다.
  지금은 임시명이라 **공개 전에 확정**해야 한다.
- **PPU(Pixels Per Unit)를 정한 뒤엔 못 바꾼다.** 바꾸면 전 스프라이트 재작업이다.
  아트 시작 직전에 정한다.
- **세이브 JSON에 `version` 필드를 처음부터 넣는다.** 나중에 넣으면 마이그레이션 경로가 없다.
  🔴 `MatKind`의 정수값이 세이브에 물려 있다 — **순서를 바꾸지 말고 뒤에 붙일 것.**
- **WebGL 제약을 처음부터 지킨다** — 스레드 의존 코드 금지, `System.IO` 직접 파일 접근 금지.
  🔴 UI가 아직 전부 `OnGUI`인 것도 여기서 비용이 된다 (`wiki/todo.md` A항).
- **절대 시각(`Time.time`, `DateTime.Now`)을 게임 코드에 쓰지 않는다.**
  헤드리스 시뮬의 결정론이 조용히 깨지는데, 표만 보면 정상처럼 보인다.
- **컴파일 통과 ≠ 동작.** 스모크에 **찍힌 숫자**를 볼 것.

## ⚠️ 알려진 함정

_(실제로 겪은 것만 날짜와 함께 적는다. 추측은 적지 않는다.)_

- **2026-08-19** — Unity Hub + Unity 6 LTS 설치가 첫 작업이며, **WebGL Build Support 모듈을
  설치 시점에 같이 체크**해야 한다(나중에 추가하면 에디터 재설치급 다운로드).
- **2026-08-19** — `.ps1`을 BOM 없는 UTF-8로 저장하면 PowerShell 5.1이 ANSI로 읽어
  **한글 주석에서 파싱이 실패**한다. 엉뚱한 에러가 뜬다 → **UTF-8 with BOM**으로 저장.
- **2026-08-19** — Unity 6 URP 17에서 `Light2D`는 `Unity.RenderPipelines.Universal.**2D**.Runtime`에
  있다. asmdef를 나누는 순간 참조가 터진다.
- **2026-08-19** — URP 2D에서 씬에 `Light2D`가 하나도 없으면 **코드는 정상인데 화면만 새까맣다.**
- **2026-08-20** — Unity 배치 모드에서 `Time.timeScale`을 올려도 게임 시간이 거의 안 간다.
  시뮬은 **`Time.captureDeltaTime`**을 써야 한다.
- **2026-08-20** — 화면 흔들림이 카메라를 직접 움직이면 **조준까지 흔들려서 배가 튄다.**
  `Juice`는 오프셋만 계산하고 `CameraFollow`가 마지막에 더한다.
- **2026-08-20** — 큰 파일(`ContentDefaults.cs`)의 한 구획을 통째로 갈아끼우다가
  **바로 위 메서드(`FillJunk`)가 같이 사라졌다.** 호출부만 남아 `CS0103`으로 나왔다.
  → 🔴 **코드 수정은 문자열 매칭으로만.** 구획 치환 뒤 컴파일 검사를 미루지 말 것.
- **2026-08-20** — `unity-check.ps1`의 락은 **앞선 배치가 끝날 때까지** 잡혀 있다.
  `[X] 다른 Unity 배치가 실행 중이다`는 에러가 아니라 **순서를 기다리라는 뜻**이다(exit 3).
- **2026-08-21** — **Unity가 끝난 뒤에도 스크립트 요약이 한참 안 나올 수 있다.**
  죽은 줄 알고 락을 지우고 다시 돌리면 **배치 두 개가 겹쳐 `0xC000013A`로 죽는다.**
  → 락을 성급히 지우지 말고 `tools/logs/compile.log`로 판정한다:
    `grep -c "error CS"`가 0이고 끝이 `Exiting batchmode successfully now!`면 **이미 통과**다.
- **2026-08-27** — `Explode → HitAround → Hit → ProcExplode → Explode` **무한재귀**로
  StackOverflow. `procDepth`로 막았다. 🔴 **확률을 낮춰서는 못 고친다.**
- **2026-08-27** — 헤드리스 시뮬 결정론은 조건이 **여섯**이다. 넷만 맞추면 44.6% 흔들린다.
  전체 목록은 `wiki/architecture.md`.

---

## 셋업 (새 PC)

```
1. Unity Hub 설치 → Unity 6.3 LTS 설치
   모듈: WebGL Build Support(필수), Windows Build Support(IL2CPP)
2. game/ 폴더를 Unity Hub에서 열기
3. Edit > Project Settings > Player > Active Input Handling = Both
```

## 빌드

```
Windows : File > Build Settings > Windows, IL2CPP
WebGL   : File > Build Settings > WebGL, 압축 Brotli/Gzip
          또는 tools/unity-webgl-bg.ps1
          🔴 화면에 200개가 뜨는 장르다. 성능을 실측할 것
```

## 운영 (배포 후)

- itch.io 페이지 GIF·커버 갱신
- 플레이 피드백은 `wiki/playtests.md`에
- 친구에게 보낼 글은 `wiki/playtest-questions.md`에 그대로 있다
