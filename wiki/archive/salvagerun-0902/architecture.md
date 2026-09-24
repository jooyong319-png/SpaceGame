# architecture — 코드 어디에 뭐가 있나

> **2026-09-02에 다시 썼다.** 옛 문서는 rev.4(뱀서라이크) 시절 설명이 섞여 있어
> 통째로 갈았다. 게임 규칙은 여기 안 적는다 — 그건 [[game]]이 정본이다.
> **문서와 코드가 다르면 코드가 맞다.**

---

## 폴더

```
SalvageRun/
├── README.md          셋업 · 빌드 · 읽는 순서
├── wiki/              문서는 전부 여기 하나로 모았다 (2026-09-02)
│   └── archive/       ⛔ 폐기된 설계와 옛 측정. docs-rev5/가 옛 docs/ 통째로
├── tools/             unity-check.ps1 · unity-test.ps1 · unity-webgl*.ps1 · logs/
├── build/             SalvageRun-webgl.zip · webgl/
└── game/Assets/_Project/
    ├── Data/          GameContent.asset · RunConfig.asset   ← 🔴 밸런스 정본
    ├── Editor/        GreyboxMenu · WebGLBuild
    ├── Resources/     폰트(Galmuri) 등
    ├── Scenes/        Greybox
    ├── Tests/         SmokeTest.cs · BalanceSim.cs
    └── Scripts/
        ├── Core/      InputReader                 입력 한 곳 (Input System + 레거시 폴백)
        ├── Data/      GameData · RunConfig · ContentDefaults · TechTree(Defaults)
        │              WeaponData(Defaults) · ShipData · ComboData
        ├── Meta/      MetaSave(저장·해금) · TechSystem(RunStats 조립)
        ├── Run/       RunDirector(한 판의 지휘자) · StageField(쓰레기 밭)
        │              ShipController · WeaponRig · BossBehaviour · Fragment · Fx …
        └── UI/        GameHud · TechTreeScreen    ← 전부 OnGUI (임시)
```

## 규칙이 어디에 있나

| 알고 싶은 것 | 파일 |
|---|---|
| 화면 전환 · 한 판의 진행 · 정산 | `Run/RunDirector.cs` — **여기가 지휘자다** |
| 웨이브 · 보스 등장 시각 | `RunDirector.UpdateWave` |
| 쓰레기 생성 · 재화 드롭 | `Run/StageField.cs` (`RollMaterials`) |
| 조준 · 발사 · 연쇄 · 폭발 | `Run/WeaponRig.cs` |
| 구역·무기·보스 수치 | `Data/ContentDefaults.cs` · `Data/WeaponDefaults.cs` |
| 테크트리 113노드 | `Data/TechTreeDefaults.cs` |
| 저장 · 해금 · 구역 구매 | `Meta/MetaSave.cs` |
| 노드 효과가 스탯이 되는 곳 | `Meta/TechSystem.cs` → `RunStats` |
| 보스 부위 HP | `RunDirector.BossPartHp` 🔴 **정본은 여기 하나뿐.** 검사가 식을 복사하면 안 된다 |

## 데이터 흐름

```
MetaSave(저장) ─┐
                ├─> TechSystem ─> RunStats ─> RunDirector ─> ShipController/WeaponRig/StageField
GameContent ────┘                                  │
                                                   └─> 정산 ─> MetaSave.AddMaterial
```

**한 판이 시작할 때 `RunStats`를 새로 조립한다.** 판 도중에는 안 바뀐다
(레벨업·카드가 없으므로 — [[game]]).

---

## 도구 (`tools/`)

| 명령 | 무엇 | 걸리는 시간 |
|---|---|---|
| `unity-check.ps1` | 컴파일만 | 1~2분 |
| `unity-test.ps1 -Only SmokeTest` | 스모크 | 수 분 |
| `unity-test.ps1` | **밸런스 시뮬 포함** (CPU를 꽉 쓴다) | ~12분 |
| `unity-webgl-bg.ps1` | WebGL 빌드 | 오래 |

🔴 **유니티 에디터가 켜져 있으면 셋 다 못 돈다.** `Get-Process Unity`로 확인할 것.
🔴 **에디터를 닫지 말 것** — 사장님이 켜 두신 것이다.
🔴 **컴파일은 사장님이 주무실 때 맡기신 일에만 돌린다.** 그 외엔 고치고 알려드리고 끝낸다.

---

## 🔴 재사용할 수 있는 지식 — 시뮬 결정론 6조건

봇 시뮬로 밸런스를 재려면 **같은 입력이 같은 결과를 내야 한다.**
여기서 흔들림 44.6% → 0%까지 가는 데 조건을 여섯 개 찾았다.
**어느 프로젝트에서든 그대로 쓸 수 있다** (→ [[unified-wiki-inbox]]):

1. `Time.captureDeltaTime` 고정
2. 절대 시각(`Time.time`) 금지
3. 워밍업 프레임 버리기
4. `fixedDeltaTime`을 프레임에 맞추기
5. **물리를 수동으로 돌린다** — `Physics2D.Simulate`를 프레임당 한 번
6. **배를 제자리로 돌린 *다음에* 밭을 짓는다** ← 순서가 틀리면 44.6% 흔들린다

⚠️ 넷까지 맞춰 놓고도 흔들려서 네 번을 헛짚었다.
**결과가 이상하면 게임이 아니라 계측기를 먼저 의심할 것.**

## 반복해서 밟은 함정

| | |
|---|---|
| **객체 풀** | `FreePiece`/`FreeFragment`/`FreeShot` — 순서 버그의 단골. 반납 전에 상태를 지울 것 |
| **재귀** | `Explode → HitAround → Hit → ProcExplode → Explode` 무한재귀로 StackOverflow. `procDepth`로 막았다. **확률을 낮춰서는 못 고친다** |
| **enum 개수** | 손으로 센 상수는 enum이 늘 때 조용히 어긋난다. `Enum.GetValues`로 뽑을 것 |
| **저장 호환** | `MatKind` 정수값이 세이브에 물려 있다. **순서를 바꾸지 말고 뒤에 붙일 것** |
| **인덱스 편집** | 코드 수정은 **문자열 매칭으로만.** 인덱스 기반 편집으로 파일을 여러 번 깨뜨렸다 |
