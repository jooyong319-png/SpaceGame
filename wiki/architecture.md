# architecture — 구조 (rev.10)

> ⚠️ 이 문서는 rev.4(뱀서라이크) 때 쓰였고 **부분적으로만 갱신됐다.**
> 클래스 구조와 데이터 흐름은 지금도 맞지만, **게임 규칙 설명은 옛 것이 섞여 있다.**
> 규칙은 [[index]]와 [[design-lineage]]가 정본이다.

> 2026-08-20 기준. [[SCHEMA]] 규칙: **문서와 코드가 다르면 코드가 맞다.**
>
> ⚠️ rev.3(층 순차 하강 · 환생 · 테크트리)은 **전부 폐기**됐다. 이유는 [[decisions]] 2026-08-20 항목들.
> 지금 게임은 **"우주를 청소하는 느낌의 뱀서라이크"**다.

---

## 한 판의 흐름

```
맵 선택 (클리어한 만큼 해금)
   └─ 출항
        웨이브 1 → 2 → … → N          (50초마다, 갈수록 거세짐)
          · 쓰레기가 화면 밖에서 배를 향해 몰려온다
          · 무기가 자동으로 갈아버린다 → 파편이 터진다
          · 파편 흡수 = 크레딧 + 경험치
          · 레벨업 → 카드 3장 중 1장 (무기 획득/강화 · 패시브)
        마지막 웨이브 종료 → 보스(부위 4개) → 전부 부수면 **맵 클리어 → 다음 맵 해금**
        연료 0 → 격침 → 결과
```

- 🔴 **rev.10에서 뒤집혔다 — 연료는 이제 오직 이동 비용이다.**
  맞아서 줄지 않는다. 우주선은 **두 대**에 부서진다 (배리어 1 + 본체 1).
  그리고 진짜 HP는 **기지 연료**다 — 계속 닳고, 쓰레기를 먹여야 차고, 0이면 패배.
  (옛 설명: "연료 = HP, 이동 무료, 닿을 때만 감소" — rev.4~9)
- 성적 = 생존 시간 · 파편 수 · 크레딧

---

## 폴더

```
SalvageRun/
├── docs/        기준 문서 (project-brief · content-design · success-state · coldstart-distribution · design-review-packet) + archive/
├── wiki/        이 위키
├── tools/       unity-check.ps1 · unity-test.ps1 · logs/
└── game/Assets/_Project/
    ├── Data/    GameContent.asset · RunConfig.asset  ← 밸런스 정본
    ├── Editor/  SalvageRun 메뉴
    ├── Tests/   BalanceSim.cs.disabled  ← 되살리려면 새 API에 맞춰야 함
    └── Scripts/
        ├── Core/  InputReader
        ├── Data/  GameData · RunConfig · ContentDefaults
        ├── Meta/  MetaSave · TechSystem(RunStats + 카드 적용)
        ├── Run/   ShipController · StageField · JunkPiece · Fragment ·
        │          WeaponRig · RunDirector · CameraFollow · Juice · ShipVisual · AimCursor
        └── UI/    GameHud (OnGUI, 임시)
```

---

## 데이터 흐름

```
GameContent.asset (쓰레기22 · 맵6+보스 · 카드22)     RunConfig.asset (배·무기 기본값)
        │                                                    │
        └────────► TechSystem.BuildStats() ◄─────────────────┘
                            │  (시작 무기 = 회전 절단날 Lv1)
                            ▼
                        RunStats ◄──── 레벨업 카드가 계속 더해진다 (ApplyCard)
                            │
          ┌─────────────────┼──────────────────┐
          ▼                 ▼                  ▼
   ShipController      WeaponRig          RunDirector
                            │                  │
                     JunkPiece.Chip()          │
                            └── 파편 ─────► Absorb() → 크레딧 + XP → 레벨업 → 카드
```

🔴 **RunStats는 런 시작마다 새로 만든다.** 카드는 그 위에만 쌓이므로 런이 끝나면 자동으로 사라진다.

---

## 핵심 규칙이 코드 어디에 있는가

| 규칙 | 위치 |
|---|---|
| 연료 = 이동 비용 (rev.10) | `ShipController.FixedUpdate` — `thrustFuelPerSecond` 소모 |
| 닿으면 연료 감소 | `RunDirector.CheckContact` + `JunkPiece.TryContact`(0.45초 쿨다운) |
| 쓰레기가 배를 향해 온다 | `StageField.SpawnFromEdge`(배 주변 링) + `JunkPiece.ApplyMovePattern` |
| 이동 패턴 5종 | `JunkPiece.ApplyMovePattern` — Chase/Drift/Zigzag/Charger/Orbiter |
| 분열 · 무리 | `StageField.BreakJunk`(splitInto) · `SpawnFromEdge`(groupSize) |
| 무기가 동시에 돈다 | `WeaponRig.Update` — 보유한 것만 각각 실행 |
| 파편만 수집 대상 | `Fragment` — 쓰레기 본체는 흡수 불가 |
| 웨이브 곡선 | `RunDirector.UpdateWave` — 유입 ×1.45^(웨이브-1), 상한 25+35×(웨이브-1), 최대 300 |
| 맵 클리어 → 해금 | `RunDirector.Clear` → `MetaSave.UnlockNextMap` |
| 카드 고를 때 세계가 멈춘다 | `RunDirector.WorldPaused` — StageField·JunkPiece·Fragment·ShipController가 각자 본다 |
| 흔들림 상한 | `Juice.ShakeCap`(0.22) · `Juice.ShakeScale`(F3로 0/1 토글) |
| 파편이 쓰레기와 구분된다 | `Fragment.ColorFor(value)` + 반짝임 · `JunkPiece.Update`의 채도 낮추기 |
| 밸런스는 한 곳에만 | `GameContent` / `RunConfig` 에셋 |

---

## 무기 · 영구 성장

자세한 건 [[weapons]]와 [[meta-progression]]. 여기엔 **코드 구조만** 적는다.

```
WeaponDef (데이터 13종) ──► WeaponPattern 12가지 ──► Hit() 한 곳
                              Orbit/Boomerang/          특성·조합이
                              Projectile/Beam/Chain/    전부 여기서 붙는다
                              PeriodicAoe/Nova/Mine/
                              Aura/Well/Companion
```

🔴 **`WeaponRig`에 무기별 전용 코드가 없다.** 새 무기 = 데이터 한 줄, 새 특성 = `Hit()` 한 곳.
무기 13종을 각각 손으로 짜면 20종이 될 때 손댈 수 없게 된다.

| 규칙 | 위치 |
|---|---|
| 무기는 한 판에 2개 | `RunConfig.maxWeapons` · `RunDirector.PickingSecondWeapon` |
| 두 번째 무기 고르는 판은 무기만 | `RunDirector.BuildWeaponOffers` |
| 무기 카드는 데이터가 아니다 | `RunDirector.WeaponCard` — `content.weapons`에서 생성 |
| 조합은 **계열 쌍** | `GameContent.FindCombo(WeaponTag, WeaponTag)` · `RunDirector.CheckCombo` |
| 영구 강화가 런에 얹히는 지점 | `TechSystem.ApplyTechTree` (카드보다 **먼저**) |
| 재화 드랍 | `StageField.RollMaterials` — 티어가 높을수록 좋은 것 |
| 노드 구매 | `MetaSave.CanBuy` / `MetaSave.Buy` |
| 우주선이 시작 무기를 정한다 | `TechSystem.BuildStats` → `MetaSave.CurrentShip` |
| 우주선 스탯 적용 순서 | 테크(**더하기**) → 우주선(**곱하기**). 반대면 곡선이 이상해진다 |
| 우주선 해금·선택 | `MetaSave.BuyShip` / `SelectShip` · HUD `DrawShipPicker` |
| 배 외형 | `ShipVisual.ApplyShip` — 색과 크기 |

## 아트 (임시)

`PixelArt.cs` — **코드로 찍은 도트.** 잔해·파편·배·결정·고리·글로우·날.

🔴 `ToSprite`가 PPU를 **텍스처 가로폭으로** 잡아 스프라이트가 항상 가로 1유닛이 된다.
고정 PPU를 쓰면 도트를 넣는 순간 기존 `localScale` 계산이 전부 어긋난다.

⚠️ 진짜 아트가 오면 **이 파일만 지우면 된다** — 밖으로 안 새게 짰다.
그때는 PPU를 하나로 고정하고 크기를 다시 잡아야 한다.

## 맵과 카메라

- **맵은 화면보다 크다** (52×34 ~ 84×56 유닛). 카메라가 배를 따라가고 맵 경계에서 멈춘다
- 이전의 "아레나 = 화면 하나"는 폐기했다 — 도망칠 공간이 없으면 뱀서의 핵심 동사가 성립하지 않는다
- 쓰레기는 **배 주변 화면 밖 링**에서 생성되고, 배에서 멀어지면 사라진다

⚠️ **화면 흔들림은 `CameraFollow`가 마지막에 더한다.** `Juice`는 오프셋만 계산한다.
Juice가 카메라를 직접 움직이면 추적과 싸우고, 조준까지 흔들려 배가 튄다 — 실제로 겪었다.
조준은 `CameraFollow.BasePosition`(흔들리기 전)을 기준으로 계산한다.

---

## 타격감 (Juice)

> 🔴 **흔들림 수치는 다른 장르에서 가져오면 안 된다.** 파괴가 초당 수십 번 일어나므로
> 한 번의 값이 작아도 겹쳐서 상한에 붙는다. 2026-08-21에 파괴 0.32 → **0.030**으로 내렸고,
> 상한도 1.2 → **0.22**로 낮췄다. 멀미는 취향이 아니라 접근성 문제라 **F3로 끄는 길도 뒀다.**

**오디오 파일이 없다.** 파형을 런타임에 생성한다 — 깎는 틱, 부서지는 노이즈, 흡수 블립, 레벨업.
화면 흔들림과 피격 섬광(`hitFlash`)도 여기서 나온다. 아트 단계에서 진짜 소리로 교체한다.

> 🔴 첫 플레이가 밋밋했던 원인의 절반이 소리 부재였다. 그레이박스에서도 최소한의 피드백은 필요하다.

---

## 화면에서 읽혀야 하는 것

| 무엇 | 어떻게 |
|---|---|
| **파편 = 재화** | 값에 따라 청록(기본)/금색(18+)/보라(45+). **반짝인다.** sortingOrder 8 |
| **쓰레기 = 잔해** | 종류 색의 채도를 45%로 낮춘다. 깎일수록 어두워진다. sortingOrder 5 |
| **위험물** | 예외 — 채도를 안 낮춘다. 피해야 하는 건 선명해야 한다 |

🔴 파편이 쓰레기 색을 물려받던 시절엔 **둘을 구분할 수 없었다** (2026-08-21).
색만으로는 부족하다 — 화면에 200개가 떠 있으면 **움직임이 다른 것**이 먼저 읽힌다.

## 저장 (MetaSave)

- JSON 1개, `persistentDataPath/meta.json`, `version` 필드
- 크레딧 · **해금된 맵 수**(`unlockedMaps`) · 최고 기록 · 통계
- 🟡 우주선 종류 · 특성은 아직 없다 — 추가할 때 `version`을 올릴 것
- ⚠️ WebGL은 IndexedDB — flush 필요 여부는 첫 WebGL 빌드에서 실측

---

## 계측 (헤드리스 시뮬)

`tools/unity-check.ps1`(컴파일만) · `tools/unity-test.ps1`(컴파일 → PlayMode 테스트)

⚠️ `Tests/BalanceSim.cs.disabled` — rev.3 API를 참조해서 꺼 뒀다.
되살리려면 `StartRun(map)` · 카드 선택 · 무기 조합으로 다시 써야 한다.
**결정론 확보 방법은 그대로 유효하다** (하나라도 빠지면 조용히 흔들린다):

1. `Time.timeScale`이 아니라 **`Time.captureDeltaTime`**
2. **절대 시각 금지** (`Time.time`, `DateTime.Now`) — 객체마다 자기 수명 타이머
3. **워밍업 런 하나를 버린다**

+ 난수는 전부 시드 고정 (`WeaponRig.Rand`, `RunDirector.NextRandom`, `Juice.Random01`).
+ 배치 실행은 락으로 겹치지 않게 한다 (겹치면 `0xC000013A`로 죽는데 원인이 안 보인다).

---

## 임시인 것 (아트 단계에서 버린다)

- `GreyboxBootstrap` — 씬을 코드로 조립
- `GameHud` — OnGUI. **WebGL 비용이 크므로 UGUI로 교체 필요**
- `GameHud.DrawDiagnostics` — 입력 진단 줄. 마우스 문제 확정 후 삭제
- 모든 스프라이트가 런타임 생성 1×1 흰 사각형
- `Juice`의 절차적 사운드

## 죽은 코드 정리 (2026-08-21 완료)

rev.2~3의 잔재 `ToolKind` · `ToolDef` · 구 `TechNode` · `TechEffect` · `TechBranch` ·
`GameContent.tools/tech` · `techCostMultiplier`는 **전부 걷어냈다.**
새 테크트리를 넣을 때 이름이 충돌해서 컴파일이 깨진 덕에 발견했다.

🟡 아직 남은 것: `StageDef.quota/warpCost/stayDrainRamp/returnSeconds`, `BossDef.kind`(방해 미구현).
