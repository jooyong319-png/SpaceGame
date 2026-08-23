# SALVAGE RUN (임시명)

**우주를 청소하는 느낌의 뱀서라이크.**
사방에서 몰려드는 우주 쓰레기를 무기가 자동으로 갈아버리고, 파편을 흡수해 성장한다.

- 엔진: **Unity 6.3 LTS (6000.3.22f1) · URP 2D** · 플랫폼: PC(Windows) + WebGL 데모
- 유통: **itch.io → 스팀**
- 시작: 2026-08-19 · 상태(2026-08-20): **플레이 가능한 그레이박스. 아트 0, 메타 0.**

---

## 🔴 판단이 갈리면 여기로

**`docs/project-brief.md`(rev.4)가 이 프로젝트의 기준 문서다.**
구현 판단이 갈리면 그 문서의 §9 설계 원칙과 §10 MVP 범위로 결정한다.

새 작업 세션(사람이든 LLM이든)은 이 순서로 읽는다:

```
1. wiki/SCHEMA.md        ← 위키 사용법 + 네 원칙 (제0~제3)
2. wiki/index.md         ← 페이지 카탈로그
3. docs/project-brief.md ← 기준 문서
4. wiki/todo.md          ← 다음에 뭘 하는가
```

⚠️ **브리프를 전면 개정하기 전에 [[SCHEMA]] 제0원칙을 먼저 읽을 것.**
rev.1~3은 한 번도 플레이하지 않은 채 24시간 안에 세 번 갈아엎었고, 문서만 2,500줄 나왔다.

## 폴더

| 경로 | 내용 |
|---|---|
| `docs/` | 🔴 기준 문서 (브리프 · 성공 상태 · 콘텐츠 설계 · 콜드스타트 · 리뷰 패킷) |
| `docs/archive/` | 폐기된 rev.2·rev.3 브리프. **참고용, 따르지 말 것** |
| `wiki/` | 프로젝트 위키 (옵시디언 볼트로 열면 `[[링크]]`가 동작) |
| `art/src/` | 도트 **원본** 파일(.aseprite 등). Unity Assets 밖에 둔다 |
| `game/` | Unity 프로젝트 루트 |
| `tools/` | 헤드리스 컴파일 체크 / 테스트 스크립트 + 로그 |

상위 통합 위키: `d:/Gcalen/wiki/` — Unity·웹·배포 등 **어느 프로젝트든 재사용되는 기술 지식**.
저장소 밖 로컬 폴더라 원격 세션은 못 읽는다 → `wiki/unified-wiki-inbox.md` 경유.

---

## 코드 지도

```
game/Assets/_Project/
├── Data/     GameContent.asset · RunConfig.asset   ← 🔴 밸런스 정본
├── Editor/   GreyboxMenu (SalvageRun 메뉴)
├── Tests/    BalanceSim.cs.disabled                ← rev.3 API 참조, 되살려야 함
└── Scripts/
    ├── Core/ InputReader          입력 (Input System / 레거시 양쪽 대응)
    ├── Data/ GameData · RunConfig · ContentDefaults
    ├── Meta/ MetaSave · TechSystem (RunStats + 카드 적용)
    ├── Run/  ShipController · StageField · JunkPiece · Fragment ·
    │         WeaponRig · RunDirector · CameraFollow · Juice · ShipVisual · AimCursor
    └── UI/   GameHud (OnGUI, 임시)
```

자세한 건 `wiki/architecture.md`의 **"핵심 규칙이 코드 어디에 있는가"** 표.

## 설계 원칙 ↔ 구현 위치

| 원칙 | 어디서 강제되는가 |
|---|---|
| 밸런스 수치는 한 곳에만 | `Data/*.asset` (ScriptableObject). 코드에 매직넘버 금지 |
| 이동에 비용이 없다 | `ShipController.FixedUpdate` — 추진 소모 코드가 **없다** |
| 연료는 닿을 때만 준다 | `RunDirector.CheckContact` + `JunkPiece.TryContact` (0.45초 쿨다운) |
| 무기는 전부 자동 | `WeaponRig.Update` — 보유한 것만 각각 실행 |
| 런 성장은 런이 끝나면 사라진다 | `RunStats`를 런 시작마다 새로 만든다. 카드는 그 위에만 쌓인다 |
| 그레이박스 먼저, 도트는 나중 | `wiki/todo.md` 순서 |

---

## 실행 방법

```
Unity에서 씬을 열고 →  메뉴 SalvageRun > 데이터 에셋 생성
                    →  메뉴 SalvageRun > 그레이박스 준비
                    →  Play
```

### 조작 / 디버그 키

| 키 | 동작 |
|---|---|
| 좌클릭 홀드 | 커서 쪽으로 추진 |
| Shift / 우클릭 | 대시 |
| 커서 | 조준 (작살 방향) |
| Esc | 일시정지 / 뒤로 |
| F1 | 디버그: 크레딧 지급 |
| F2 | 디버그: 런 즉시 종료 |

## 에디터 없이 검사하기

```
tools/unity-check.ps1    컴파일만 (배치 모드)
tools/unity-test.ps1     컴파일 → PlayMode 테스트
```

⚠️ **Unity 에디터가 켜져 있으면 실행되지 않는다** (락 파일로 막아 뒀다).
두 Unity 인스턴스가 같은 프로젝트를 열면 `0xC000013A`로 죽는데 원인이 전혀 안 보인다.

---

## ⚠️ 손대기 전에 알아야 할 것

- **제목과 URL은 공개 후 바꾸지 않는다.** 스팀 스토어 주소·itch 주소가 제목에서 나온다.
  지금은 임시명이라 **공개 전에 확정**해야 한다 (`docs/project-brief.md` §12).
- **PPU(Pixels Per Unit)를 정한 뒤엔 못 바꾼다.** 바꾸면 전 스프라이트 재작업이다.
  그레이박스 단계에서는 의미가 없으므로 아트 시작 직전에 정한다.
- **세이브 JSON에 `version` 필드를 처음부터 넣는다.** 나중에 넣으면 마이그레이션 경로가 없다.
  🟡 메타(우주선·특성)를 추가할 때 `version`을 올릴 것.
- **WebGL 제약을 처음부터 지킨다** — 스레드 의존 코드 금지, `System.IO` 직접 파일 접근 금지.
  마지막에 확인하면 전면 수정이 된다. 🔴 `GameHud`가 아직 OnGUI인 것도 여기서 문제가 된다.
- **절대 시각(`Time.time`, `DateTime.Now`)을 게임 코드에 쓰지 않는다.**
  헤드리스 시뮬의 결정론이 조용히 깨지는데, 표만 보면 정상처럼 보인다.

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
  `Juice`는 오프셋만 계산하고 `CameraFollow`가 마지막에 더한다. 조준은 `BasePosition` 기준.
- **2026-08-20** — 큰 파일(`ContentDefaults.cs`)의 한 구획을 통째로 갈아끼우다가
  **바로 위 메서드(`FillJunk`, 쓰레기 22종)가 같이 사라졌다.** 호출부만 남아서
  `CS0103: The name 'FillJunk' does not exist`로 나왔다.
  → 구획 치환 뒤에는 **컴파일 검사를 미루지 말 것.** 에디터가 켜져 있어 미룬 사이에
    다른 변경이 얹혀 원인이 흐려진다.
- **2026-08-20** — `unity-check.ps1`의 락은 **앞선 배치가 끝날 때까지** 잡혀 있다.
  `[X] 다른 Unity 배치가 실행 중이다`가 뜨면 에러가 아니라 **순서를 기다리라는 뜻**이다
  (exit 3). 앞 실행이 끝난 뒤 다시 돌린다.
- **2026-08-21** — **Unity가 끝난 뒤에도 스크립트 요약이 한참 안 나올 수 있다.**
  Unity 프로세스는 사라졌는데 락과 "실행 중" 표시만 남아 보여서, 죽은 줄 알고
  락을 지우고 다시 돌리기 쉽다 — 그러면 **배치 두 개가 겹쳐 `0xC000013A`로 죽는다.**
  → 성급히 락을 지우지 말고 `tools/logs/compile.log`로 먼저 판정한다:
    `grep -c "error CS"`가 0이고 끝이 `Exiting batchmode successfully now!`면 **이미 통과**다.
    정말 멈춘 게 맞다면(로그가 몇 분째 그대로) 그때 `%TEMP%\salvagerun-unity.lock`을 지운다.

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
          ⚠️ 초기부터 주기적으로 빌드가 통과하는지 확인할 것
          🔴 화면에 200개가 뜨는 장르다. 성능을 실측할 것
```

## 운영 (배포 후)

- itch.io 페이지 GIF·커버 갱신
- 플레이 피드백은 `wiki/playtests.md`에, 밸런스 변경 이유는 `wiki/balance-log.md`에
