using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 콘텐츠 초기값. 에셋(`_Project/Data/GameContent.asset`)이 정본이고 여기는 그 씨앗이다.
    /// 메뉴 `SalvageRun > 데이터 에셋 생성`으로 에셋을 뽑고, 이후 밸런스는 인스펙터에서 만진다.
    ///
    /// 🔴 테크트리 총비용이 이 게임의 플레이타임을 결정한다. 근거는 docs/content-design.md.
    /// </summary>
    public static class ContentDefaults
    {
        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        public static void Fill(GameContent c)
        {
            FillJunk(c);
            WeaponDefaults.FillWeapons(c);
            WeaponDefaults.FillCombos(c);
            TechTreeDefaults.Fill(c);
            ShipDefaults.Fill(c);
            FillStages(c);
            FillCards(c);
        }

        // ---------------------------------------------------------------- 쓰레기 22종
        //  🔴 종류를 가르는 건 숫자가 아니라 **이동 패턴**이다.
        //     2026-08-20에 16종이 전부 같은 방식으로 움직여서 통째로 1종처럼 느껴졌다.
        //     Chase / Drift / Zigzag / Charger / Orbiter + 분열 + 무리 + 위험물.
        static JunkType J(string name, int tier, int value, float size, float speed,
                          float homing, MoveKind move, float movePower,
                          float hp, float contactDamage, int fragments, Color color,
                          int weight = 10, int groupSize = 1, string splitInto = null, int splitCount = 0,
                          bool hazard = false, float fuelBonus = 0f, float fuelPenalty = 0f,
                          bool anchor = false, JunkShape shape = JunkShape.Debris)
        {
            return new JunkType
            {
                displayName = name, tier = tier, value = value, size = size,
                driftSpeed = speed, homing = homing, move = move, movePower = movePower,
                hp = hp, contactDamage = contactDamage, fragments = fragments, color = color,
                spawnWeight = weight, groupSize = groupSize, splitInto = splitInto,
                splitCount = splitCount > 0 ? splitCount : 2,
                isHazard = hazard, fuelBonus = fuelBonus, fuelPenalty = fuelPenalty,
                isAnchor = anchor, shape = shape
            };
        }

        static void FillJunk(GameContent c)
        {
            // 🔴 속도 기준: 배의 실제 순항 속도는 약 **16**이다
            //    (thrustForce 42 ÷ linearDamping 2.6 — maxSpeed 26에는 도달하지 않는다).
            //    2026-08-21에 배가 빠르다는 피드백으로 52→42로 내리면서
            //    **쓰레기도 같은 비율(×0.82)로 같이 내렸다** — 배만 늦추면
            //    상대 속도가 올라가서 직전에 고친 "너무 빠르다"가 그대로 되돌아온다.
            //    피하면서 싸우려면 다수가 그 **절반 아래**여야 한다. 2026-08-21에
            //    잡몹이 11(55%)이었고 그게 화면의 60%를 차지해서 "너무 빠르다"가 나왔다.
            //
            //    weight = 화면에 뜨는 **개체 수 비율**. 무리로 나오는 건 알아서 보정된다.
            // 🔴 크기를 1.55배로 올렸다 (2026-08-22 플레이 피드백: "크기가 너무 작고").
            //    화면에 200개가 뜨는 게임이라 개당 크기를 작게 잡았는데,
            //    작으면 **무엇을 부수고 있는지 안 보여서** 청소하는 실감이 사라진다.
            // 🔴 접촉 피해를 1.6배로 올렸다 (2026-08-22).
            //    "쓰레기가 약해서 잘 안 죽는다" — 연료가 접촉으로만 닳는 설계인데
            //    한 대가 너무 안 아파서 피할 이유가 없었다.
            //    웨이브마다 추가로 1.35배씩 더 붙는다 (RunDirector.CheckContact).
            c.junk = new[]
            {
                // ==================================================================
                //  🔴 **쓰레기는 실제 사물이다** (2026-08-26 사장님 지시).
                //
                //     위성 · 작은 우주선 · 전함 · 거대 우주선 · (외계 우주선 = 보스)
                //
                //     전에는 "볼트 다발 · 폐배선 · 파쇄 패널" 같은 **조각**이었다.
                //     조각은 크기와 색만 다를 뿐 **무엇을 부수고 있는지가 안 읽힌다** —
                //     사물이어야 "저건 전함이다, 단단하겠다"가 보자마자 온다.
                //
                //  🔴 다섯 범주가 **한눈에 갈리는 축**을 하나씩 갖는다:
                //     · 위성      = 작고 흔하다. 고철
                //     · 작은 우주선 = 중간. 빠르고 수가 많다. 회로
                //     · 전함      = 크고 단단하다. 느리다. 코어
                //     · 거대 우주선 = 아주 크다. 부수면 **조각이 여럿** 나온다
                // ==================================================================

                // ---- 위성 (티어 0) : 잡몹. "파바바박"의 주력 ----
                //      HP가 낮아 닿는 족족 터진다. 단단한 것만 있으면 리듬이 죽는다
                J("소형 위성",     0,   6, 0.62f, 2.2f, 0.9f, MoveKind.Chase,  1f,  10f, 2.4f, 1, C(150,160,175), weight: 24, groupSize: 4, shape: JunkShape.Satellite),
                J("통신 위성",     0,  10, 0.78f, 1.8f, 0.7f, MoveKind.Orbiter, 0.8f, 26f, 3.2f, 1, C(190,190,220), weight: 16, groupSize: 2, shape: JunkShape.Satellite),
                J("기상 위성",     0,  14, 0.86f, 1.6f, 0.6f, MoveKind.Drift,  1f,  42f, 4.0f, 2, C(170,200,210), weight: 12, shape: JunkShape.Satellite),
                J("정찰 위성",     0,  18, 0.92f, 2.6f, 1.2f, MoveKind.Zigzag, 1.2f, 55f, 4.0f, 2, C(160,200,255), weight: 10, shape: JunkShape.Satellite),

                // ---- 작은 우주선 (티어 1) : 중형. 빠르고 수가 많다 ----
                J("구조정",        1,  30, 1.10f, 2.4f, 1.0f, MoveKind.Chase,   1f,  95f, 6.4f, 3, C(200,170,110), weight: 14, groupSize: 2, shape: JunkShape.Vessel),
                J("정찰선",        1,  36, 1.04f, 3.0f, 1.4f, MoveKind.Zigzag,  1.3f, 85f, 6.4f, 3, C(160,210,255), weight: 12, shape: JunkShape.Vessel),
                J("화물선",        1,  48, 1.30f, 1.7f, 0.7f, MoveKind.Drift,   1f, 150f, 8.0f, 4, C(210,180,120), weight: 11,
                  splitInto: "소형 위성", splitCount: 2, shape: JunkShape.Vessel),
                J("채굴선",        1,  54, 1.24f, 2.0f, 0.9f, MoveKind.Charger, 1.1f, 140f, 8.0f, 4, C(220,140,100), weight:  9, shape: JunkShape.Vessel),

                // ---- 전함 (티어 2) : 크고 단단하다. 느리다 ----
                J("호위함",        2,  90, 1.55f, 1.5f, 0.7f, MoveKind.Chase,   1f, 300f, 11.2f, 5, C(150,180,200), weight: 12,
                  splitInto: "구조정", splitCount: 2, shape: JunkShape.Warship),
                J("구축함",        2, 120, 1.72f, 1.3f, 0.6f, MoveKind.Drift,   1f, 380f, 12.8f, 5, C(140,150,170), weight: 10,
                  splitInto: "정찰선", splitCount: 3, shape: JunkShape.Warship),
                J("포격함",        2, 145, 1.66f, 1.7f, 0.9f, MoveKind.Charger, 1.0f, 340f, 14.4f, 6, C(210,110,110), weight:  8,
                  splitInto: "소형 위성", splitCount: 4, shape: JunkShape.Warship),

                // ---- 거대 우주선 (티어 2) : 아주 크다. 부수면 조각이 여럿 ----
                //      🔴 이게 화면에 하나 뜨면 **그쪽으로 갈 이유**가 된다.
                //         느리고 단단해서 시간을 들여야 하고, 그만큼 쏟아진다
                J("수송 모함",     2, 240, 2.20f, 0.9f, 0.5f, MoveKind.Drift,   1f, 620f, 16.0f, 8, C(190,175,140), weight:  6,
                  splitInto: "화물선", splitCount: 3, shape: JunkShape.Hulk),
                J("난파 순양함",   2, 300, 2.45f, 0.8f, 0.4f, MoveKind.Drift,   1f, 780f, 16.0f, 9, C(160,170,190), weight:  5,
                  splitInto: "호위함", splitCount: 2, shape: JunkShape.Hulk),

                // ---- 위험물 : 크레딧 0 ----
                //      ⬜ 지금은 **닿아도 아프지 않다** (플레이어 무적, 2026-08-23).
                //         `fuelPenalty`는 아무 데서도 안 읽는다.
                //         남겨 둔 이유: 보스 투사체와 함께 위협을 되살릴 때 쓸 자리다.
                J("냉각수 유출",   1,   0, 1.15f, 2.0f, 1.0f, MoveKind.Chase,   1f,  70f, 22.4f, 0, C(120,255,200), weight:  8, hazard: true, fuelPenalty: 10f),
                J("방사성 폐기물", 2,   0, 1.28f, 1.7f, 0.9f, MoveKind.Zigzag,  1.2f, 100f, 28.8f, 0, C(180,255,110), weight:  7, hazard: true, fuelPenalty: 14f),

                // ---- 계류 장치 (rev.10 최종 지역) ----
                //  🔴 스폰 풀에 들어가지 않는다 (`spawnWeight = 0`).
                //     최종 지역 도착 시 `StageField.PlantAnchors()`가 직접 심는다.
                //
                //  🔴 **HP를 하나씩 다르게** 주려고 4종으로 나눴다.
                //     첫 닻이 쉽게 부서져야 *"할 만하다"*를 먼저 배운다 —
                //     넷 다 똑같이 단단하면 첫 닻에서 포기한다.
                //     이름도 순서를 말해 준다 (1번 → 4번).
                J("계류 장치 1번", 2, 260, 2.6f, 0f, 0f, MoveKind.Drift, 0f,  900f, 30f, 22, C(255,120,140), weight: 0, anchor: true),
                J("계류 장치 2번", 2, 320, 2.8f, 0f, 0f, MoveKind.Drift, 0f, 1400f, 34f, 26, C(255,110,120), weight: 0, anchor: true),
                J("계류 장치 3번", 2, 380, 3.0f, 0f, 0f, MoveKind.Drift, 0f, 2000f, 38f, 30, C(250, 90,110), weight: 0, anchor: true),
                J("계류 장치 4번", 2, 460, 3.3f, 0f, 0f, MoveKind.Drift, 0f, 2700f, 42f, 36, C(240, 70,100), weight: 0, anchor: true),

                // ---- 파손 로봇 (rev.9) ----
                //  🔴 2026-08-21 요청: *"쓰레기 무리 주변에 파손된 로봇 같은 게 있어서 플레이어를 공격"*
                //
                //  쓰레기를 '밭'으로 바꾸면서 **위협이 통째로 사라졌다.** 이것들이 그 자리를 메운다.
                //  🔴 핵심은 **쓰레기와 위협을 분리한 것**이다 —
                //     "캐고 싶은 것"과 "무서운 것"이 다른 물건이 되면서,
                //     좋은 밭일수록 위험하다는 관계를 수치가 아니라 **배치**로 만들 수 있다.
                //
                //  · 파편을 거의 안 남긴다 — 이건 수확물이 아니라 **치워야 하는 것**이다
                //  · 다만 값은 높다. 위험을 감수하고 잡을 이유는 있어야 한다
                //  · 붉은 계열로 통일 — 밭(회색·청록)과 **색으로 즉시 구분**되어야 한다
                //  🔴 **행동을 넷 다 다르게** 준다 (2026-08-23 사장님: *"적은 왜 돌진..만 있어?"*).
                //     돌진만 있으면 대응이 하나뿐이다 — 피하거나 죽이거나.
                //     행동이 갈리면 **"쫓아갈까 / 무시하고 캘까 / 먼저 치울까"**가 생긴다.
                //
                //     그리고 넷이 드릴과 부딪히는 방식이 전부 다르다:
                //       추격 유닛 — 캐는 중에 들이받는다 (묶인 걸 처벌)
                //       저격 포탑 — 캐는 중에 쏜다 (도망칠 수도 없다)
                //       매복 기뢰 — 밭에 들어가는 순간을 노린다 (캐기 전에)
                //       선회 감시기 — 조준을 방해한다 (한 놈만 무는 드릴에 성가시다)
                J("추격 유닛",     0, 18, 0.72f, 10.4f, 2.4f, MoveKind.Hunter,   1f,  18f, 18f, 1, C(255,120,110), weight: 10, groupSize: 2),
                J("저격 포탑",     1, 34, 0.98f,  5.6f, 1.2f, MoveKind.Sniper,   1.2f, 40f, 24f, 2, C(255,180, 90), weight:  8),
                J("매복 기뢰",     1, 28, 0.86f, 11.0f, 2.0f, MoveKind.Ambusher, 1f,  26f, 30f, 1, C(230, 90,150), weight:  7),
                J("선회 감시기",   2, 44, 1.14f,  7.8f, 1.6f, MoveKind.Circler,  1f,  62f, 26f, 2, C(220, 70, 90), weight:  6),
            };
        }

        // ---------------------------------------------------------------- 패시브 카드 17장
        //  🔴 **무기 카드는 여기 없다.** RunDirector가 `content.weapons`에서 직접 만든다 —
        //     무기를 추가할 때마다 카드도 같이 써야 하면 반드시 어긋난다.
        //     여기 있는 건 무기와 무관한 상시 효과뿐이다.
        static CardDef P(string title, string desc, CardEffect eff, float value, int weight,
                         CardRarity rarity = CardRarity.Common)
        {
            return new CardDef
            {
                title = title, description = desc, effect = eff,
                value = value, weight = weight, rarity = rarity,
                color = Cards.ColorOf(rarity)
            };
        }

        static void FillCards(GameContent c)
        {
            c.cards = new[]
            {
                // ---- 무기 공통 강화 ----
                P("출력 증폭",   "전 무기 피해 +25%",   CardEffect.ToolPower,   0.25f, 16),
                P("고출력 회로", "전 무기 피해 +45%",   CardEffect.ToolPower,   0.45f,  7, CardRarity.Rare),
                P("임계 반응로", "전 무기 피해 +80%",   CardEffect.ToolPower,   0.80f,  2, CardRarity.Legend),

                P("확장 코일",   "전 무기 사거리 +25%", CardEffect.ToolRange,   0.25f, 15),
                P("광역 증폭기", "전 무기 사거리 +45%", CardEffect.ToolRange,   0.45f,  6, CardRarity.Rare),

                P("냉각 개선",   "전 무기 쿨다운 -18%", CardEffect.Cooldown,    0.18f, 14),
                P("초전도 냉각", "전 무기 쿨다운 -32%", CardEffect.Cooldown,    0.32f,  6, CardRarity.Rare),
                P("영점 냉각",   "전 무기 쿨다운 -50%", CardEffect.Cooldown,    0.50f,  2, CardRarity.Epic),

                // ---- 무기 패턴별 (2026-08-22 피드백: "절단날 회전 속도·크기 카드 등 다양함이 필요") ----
                //  🔴 무기 **이름**이 아니라 **패턴**에 붙인다. 무기가 늘어도 카드를 다시 안 쓴다.
                P("추가 궤도",   "궤도체(절단날·방벽) +1개", CardEffect.OrbitCount, 1f,  9, CardRarity.Rare),
                P("고속 회전",   "궤도 회전 속도 +30%",      CardEffect.OrbitSpin,  0.30f, 12),
                P("확장 날",     "궤도체 궤도 반경 +25%",    CardEffect.OrbitRadius,0.25f, 12),
                P("연장 탄창",   "발사체(작살·원반) +1발",   CardEffect.ProjectileCount, 1f, 9, CardRarity.Rare),
                P("관통 탄두",   "관통 +2",                  CardEffect.PierceBonus, 2f, 11),
                P("추가 탄두",   "폭발물(폭탄·지뢰) +1개",   CardEffect.BlastCount, 1f,  9, CardRarity.Rare),
                P("분기 회로",   "연쇄 대상 +2",             CardEffect.ChainTargets, 2f, 11),

                // ---- 단발성 (2026-08-22 요청) ----
                //  🔴 카드는 원래 영구 성장인데 이것만 몇 초짜리다.
                //     그래서 수치를 아주 크게 잡는다 — 어중간하면 "고르면 손해인 카드"가 되고,
                //     손해인 선택지는 선택지가 아니다.
                //     가중치도 낮게 둬서 **가끔 나오는 도박**으로 만든다.
                P("과부하 주입", "10초 동안 피해 +500%",      CardEffect.BurstPower, 10f, 5, CardRarity.Legend),
                P("공진 확장",   "10초 동안 무기 범위 +500%", CardEffect.BurstSize,  10f, 5, CardRarity.Legend),
                P("냉각 폭주",   "12초 동안 쿨다운 -75%",     CardEffect.BurstHaste, 12f, 5, CardRarity.Legend),

                // ---- 수집 ----
                P("자기 수집기", "파편 흡수 반경 +35%", CardEffect.IntakeRadius,0.35f, 14),
                P("광역 회수기", "파편 흡수 반경 +60%", CardEffect.IntakeRadius,0.60f,  6, CardRarity.Rare),
                P("자동 분류기", "파편 가치 +25%",      CardEffect.ValueMul,    0.25f, 13),
                P("암거래 회로", "파편 가치 +55%",      CardEffect.ValueMul,    0.55f,  4, CardRarity.Epic),
                P("분석 모듈",   "경험치 획득 +30%",    CardEffect.XpGain,      0.30f, 12),
                P("연료 정제기", "파편마다 연료 +0.3",  CardEffect.RefineOnCollect, 0.3f, 11),

                // ---- 생존 · 기동 ----
                P("보조 추진기", "이동 속도 +15%",      CardEffect.MoveSpeed,   0.15f, 14),
                P("관성 제어기", "이동 속도 +28%",      CardEffect.MoveSpeed,   0.28f,  6, CardRarity.Rare),
                P("차폐 도장",   "충돌 피해 -22%",      CardEffect.ContactResist,0.22f, 14),
                P("중장갑 판재", "충돌 피해 -35%",      CardEffect.ContactResist,0.35f,  6, CardRarity.Rare),
                P("불침 격벽",   "충돌 피해 -55%",      CardEffect.ContactResist,0.55f,  2, CardRarity.Epic),
                P("보조 탱크",   "최대 연료 +60",       CardEffect.FuelMax,     60f,   13),
                P("대형 탱크",   "최대 연료 +120",      CardEffect.FuelMax,     120f,   6, CardRarity.Rare),

                // ---- 기지 (2026-08-21 요청: "레벨업 보상으로 기지 무기 강화") ----
                //  🔴 rev.7에서 지는 조건은 기지 상실인데, 정작 기지를 키울 방법이 없었다.
                //     '방어 포탑'을 먹기 전에는 기지가 **아무것도 안 쏜다** —
                //     스스로 싸우는 기지는 처음부터 주는 게 아니라 **보상**이어야 한다.
                //     그래야 초반의 "혼자 다 막아야 한다"는 긴장이 살아 있다.
                //
                //  🔴 가중치를 높게(16) 준 이유: 이 카드가 안 나오면 후반에 기지가 그냥 무너진다.
                //     "가끔 나오면 좋은 것"이 아니라 **후반의 필수 축**이다.
                P("방어 포탑",   "기지가 스스로 쓰레기를 쏜다",  CardEffect.BaseTurretLevel, 1f, 16),
                P("포탑 증설",   "기지 포탑 레벨 +2",           CardEffect.BaseTurretLevel, 2f,  8, CardRarity.Rare),
                P("포신 증설",   "기지 포탑이 목표 +1개 동시",   CardEffect.BaseTurretCount, 1f,  7, CardRarity.Rare),
                P("포탑 증폭기", "기지 포탑 피해 +45%",         CardEffect.BaseTurretPower, 0.45f, 12),
                P("장거리 조준", "기지 포탑 사거리 +40%",       CardEffect.BaseTurretRange, 0.40f, 11),
                P("속사 장전기", "기지 포탑 쿨다운 -30%",       CardEffect.BaseTurretHaste, 0.30f, 10),
                P("포탑 관제소", "기지 포탑 피해 +90%",          CardEffect.BaseTurretPower, 0.90f, 3, CardRarity.Epic),
                // 🔴 rev.8: 기지에 체력이 없다. 대신 **가동 시간을 줄인다** —
                //    무방비로 서 있어야 하는 시간이 곧 위험이므로, 그걸 깎는 게 보상이다.
                P("가동 촉진기", "기지 가동 시간 -8초",         CardEffect.BaseHpMax,       8f,  12),
                P("과부하 기동", "기지 가동 시간 -18초",        CardEffect.BaseHpMax,      18f,   5, CardRarity.Epic),
            };
        }

        static void FillStages(GameContent c)
        {
            // 🔴 런은 1층부터 순차로 내려간다(2026-08-20). 지역 선택 메뉴는 없다.
            //    깊어질수록 유입·가치·위험이 커지고 워프 비용도 비싸진다 —
            //    "여기서 더 긁을까, 내려갈까"가 런 안의 유일하고 가장 중요한 선택이다.
            c.stages = new[]
            {
                new StageDef {
                    displayName="기지 궤도", rank=1, mapHalfSize=new Vector2(52f,34f), waveCount=6,  waveSeconds=7f, description="모선 바로 아래. 위성 파편이 천천히 돈다.",
                    junkCount=110, initialFill=16, spawnPerSecond=2.5f, hazardRatio=0f,
                    baseDrainPerSecond=3.5f, travelFuelCost=180f,
                    minTier=0, maxTier=0,
                    ambient=C(11,12,18),
                    boss=new BossDef { displayName="버려진 위성", kind=BossKind.Inert,
                        integrity=90f,  reward=300,  fragments=12, size=3.0f, color=C(170,180,195) } },

                new StageDef {
                    displayName="폐선 항로", rank=2, mapHalfSize=new Vector2(58f,38f), waveCount=7,  waveSeconds=7f, description="버려진 항로. 빠른 파편이 섞인다.",
                    unlockScrap=800,
                    junkCount=150, initialFill=20, spawnPerSecond=3.2f, hazardRatio=0.10f,
                    baseDrainPerSecond=5.0f, travelFuelCost=260f,
                    minTier=0, maxTier=1,
                    ambient=C(9,19,27),
                    boss=new BossDef { displayName="폐선 견인로봇", kind=BossKind.Repulsor,
                        integrity=170f, reward=800,  fragments=16, size=3.4f, color=C(120,190,220), interferePower=13f } },

                new StageDef {
                    displayName="잔해장", rank=3, mapHalfSize=new Vector2(64f,42f), waveCount=8,  waveSeconds=7f, description="함대가 침몰한 자리. 값나가는 것이 많다.",
                    unlockScrap=2400, unlockCircuit=20,
                    junkCount=180, initialFill=22, spawnPerSecond=7.5f, hazardRatio=0.12f,
                    baseDrainPerSecond=6.5f, travelFuelCost=340f,
                    minTier=1, maxTier=1,
                    ambient=C(22,14,30),
                    boss=new BossDef { displayName="침몰 화물선", kind=BossKind.Spewer,
                        integrity=280f, reward=1800, fragments=20, size=4.0f, color=C(230,150,110), interferePower=1.6f } },

                new StageDef {
                    displayName="파괴된 정거장", rank=4, mapHalfSize=new Vector2(70f,46f), waveCount=9,  waveSeconds=7f, description="거대 구조물의 잔해. 절단 없이는 손대지 못하는 것들.",
                    unlockScrap=6000, unlockCircuit=60, unlockCore=4,
                    junkCount=210, initialFill=24, spawnPerSecond=5.0f,  hazardRatio=0.13f,
                    baseDrainPerSecond=8.0f, travelFuelCost=430f,
                    minTier=1, maxTier=2,
                    ambient=C(29,12,20),
                    boss=new BossDef { displayName="정거장 코어", kind=BossKind.Emp,
                        integrity=430f, reward=4200, fragments=24, size=4.4f, color=C(140,170,255), interferePower=4.5f } },

                new StageDef {
                    displayName="심연", rank=5, mapHalfSize=new Vector2(76f,50f), waveCount=10, waveSeconds=7f, description="아무도 회수하러 오지 않는 곳.",
                    unlockScrap=14000, unlockCircuit=140, unlockCore=14,
                    junkCount=240, initialFill=26, spawnPerSecond=6.0f,  hazardRatio=0.14f,
                    baseDrainPerSecond=9.5f, travelFuelCost=520f,
                    minTier=2, maxTier=2,
                    ambient=C(27,8,12),
                    boss=new BossDef { displayName="심연 포식체", kind=BossKind.Devourer,
                        integrity=640f, reward=9000, fragments=30, size=4.8f, color=C(200,90,140), interferePower=7f } },

                new StageDef {
                    displayName="균열", rank=6, mapHalfSize=new Vector2(84f,56f), waveCount=12, waveSeconds=7f, description="여기까지 온 우주선은 거의 없다.",
                    unlockScrap=32000, unlockCircuit=320, unlockCore=40,
                    junkCount=270, initialFill=28, spawnPerSecond=4.0f,  hazardRatio=0.16f,
                    baseDrainPerSecond=11.0f, travelFuelCost=620f,
                    minTier=2, maxTier=2,
                    ambient=C(34,6,26),
                    boss=new BossDef { displayName="균열", kind=BossKind.Rift,
                        integrity=950f, reward=20000, fragments=40, size=5.4f, color=C(215,110,255), interferePower=2.4f } },
            };
        }

    }
}
