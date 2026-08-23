using System.Collections.Generic;
using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 영구 강화 테크트리의 초기값.
    ///
    /// 🔴 격자에 놓고 선행 관계로 잇는다. 선은 데이터로 두지 않는다 —
    ///    `requires`에서 자동으로 그리므로 그림과 규칙이 어긋날 수 없다.
    ///
    /// 🔴 배치 규칙: 중앙 (0,0)이 뿌리. 네 방향으로 계열이 뻗고,
    ///    무기 계열은 아래쪽 바깥, 특수(조합·시작 상태)는 위쪽 바깥.
    ///
    ///    (-x, +y) 기동 ┃ 화력 (+x, +y)
    ///    ───────── 뿌리 ─────────
    ///    (-x, -y) 선체 ┃ 수집 (+x, -y)
    ///
    /// 🔴 비용은 2026-08-22 실측으로 내렸다 (고철 ×0.40 · 회로 ×0.55 · 코어 ×0.70).
    ///    그전엔 전체 완주에 195시간이 나왔다 — 수입과 비용이 두 자릿수 배로 어긋나 있었다.
    ///
    /// 🔴 무기 노드는 **무기 종류가 아니라 패턴**에 붙인다.
    ///    "절단날 +1"이 아니라 "궤도체 +1" — 그래야 무기를 20종으로 늘려도
    ///    노드를 다시 쓰지 않는다.
    /// </summary>
    public static class TechTreeDefaults
    {
        static readonly List<TechNodeDef> buf = new List<TechNodeDef>();

        static TechNodeDef N(string id, string title, string desc, TechBranch branch,
                             int x, int y, TechEffect effect, float value,
                             int scrap, int circuit = 0, int core = 0,
                             int maxRank = 1, float growth = 1.55f, params string[] requires)
        {
            var n = new TechNodeDef
            {
                id = id, title = title, description = desc, branch = branch,
                cell = new Vector2Int(x, y), effect = effect, value = value,
                costScrap = scrap, costCircuit = circuit, costCore = core,
                maxRank = maxRank, costGrowth = growth,
                requires = requires
            };
            buf.Add(n);
            return n;
        }

        public static void Fill(GameContent c)
        {
            buf.Clear();

            // ==========================================================================
            //  뿌리
            // ==========================================================================
            N("root", "예비 동력로", "모든 것이 여기서 갈라진다. 최대 연료 +20",
              TechBranch.Core, 0, 0, TechEffect.FuelMax, 20f, 0);

            // ==========================================================================
            //  선체 — 버티는 쪽 (좌하)
            // ==========================================================================
            N("hull1", "보강 판재", "최대 연료 +25", TechBranch.Hull, -1, -1,
              TechEffect.FuelMax, 25f, 16, 0, 0, 5, 1.5f, "root");

            N("hull2", "충격 흡수재", "충돌 피해 -4%", TechBranch.Hull, -2, -1,
              TechEffect.ContactResist, 0.04f, 28, 0, 0, 5, 1.6f, "hull1");

            N("hull3", "예열 연료", "시작 연료 +8%p", TechBranch.Hull, -2, -2,
              TechEffect.StartFuel, 0.08f, 36, 1, 0, 3, 1.7f, "hull1");

            N("hull4", "이중 격벽", "최대 연료 +60", TechBranch.Hull, -3, -1,
              TechEffect.FuelMax, 60f, 64, 1, 0, 4, 1.6f, "hull2");

            N("hull5", "복합 장갑", "충돌 피해 -7%", TechBranch.Hull, -3, -2,
              TechEffect.ContactResist, 0.07f, 88, 2, 0, 3, 1.7f, "hull2", "hull3");

            N("hull6", "비상 전개 장치", "격침될 때 한 번 부활한다 (연료 절반)",
              TechBranch.Hull, -4, -2, TechEffect.Revive, 1f, 240, 7, 2, 1, 1f, "hull5");

            N("hull7", "정제 회수기", "파편마다 연료 +0.15", TechBranch.Hull, -4, -1,
              TechEffect.RefineOnCollect, 0.15f, 104, 2, 0, 4, 1.6f, "hull4");

            // ==========================================================================
            //  기동 — 움직이는 쪽 (좌상)
            // ==========================================================================
            N("drv1", "보조 추진기", "이동 속도 +3%", TechBranch.Drive, -1, 1,
              TechEffect.MoveSpeed, 0.03f, 16, 0, 0, 6, 1.5f, "root");

            N("drv2", "출력 증대", "추진력 +2", TechBranch.Drive, -2, 1,
              TechEffect.Thrust, 2f, 32, 1, 0, 5, 1.55f, "drv1");

            N("drv3", "자세 제어", "감쇠 +0.12 (더 잘 멈춘다)", TechBranch.Drive, -2, 2,
              TechEffect.Handling, 0.12f, 36, 1, 0, 4, 1.6f, "drv1");

            N("drv4", "냉각 개선", "대시 쿨다운 -6%", TechBranch.Drive, -3, 1,
              TechEffect.DashCooldown, 0.06f, 60, 1, 0, 5, 1.6f, "drv2");

            N("drv5", "관성 제어기", "이동 속도 +6%", TechBranch.Drive, -3, 2,
              TechEffect.MoveSpeed, 0.06f, 96, 2, 0, 4, 1.7f, "drv2", "drv3");

            N("drv6", "초전도 코일", "대시 쿨다운 -12%", TechBranch.Drive, -4, 1,
              TechEffect.DashCooldown, 0.12f, 168, 4, 1, 3, 1.7f, "drv4");

            N("drv7", "영점 조정", "추진력 +6", TechBranch.Drive, -4, 2,
              TechEffect.Thrust, 6f, 160, 4, 1, 3, 1.7f, "drv5");

            // ==========================================================================
            //  화력 — 때리는 쪽 (우상)
            // ==========================================================================
            N("pow1", "출력 증폭", "전 무기 피해 +4%", TechBranch.Power, 1, 1,
              TechEffect.WeaponPower, 0.04f, 18, 0, 0, 8, 1.5f, "root");

            N("pow2", "확장 코일", "전 무기 사거리 +4%", TechBranch.Power, 2, 1,
              TechEffect.WeaponRange, 0.04f, 32, 1, 0, 6, 1.55f, "pow1");

            N("pow3", "냉각 회로", "전 무기 쿨다운 -3%", TechBranch.Power, 2, 2,
              TechEffect.WeaponCooldown, 0.03f, 44, 1, 0, 6, 1.6f, "pow1");

            N("pow4", "고출력 회로", "전 무기 피해 +9%", TechBranch.Power, 3, 1,
              TechEffect.WeaponPower, 0.09f, 104, 2, 0, 5, 1.65f, "pow2");

            N("pow5", "초전도 냉각", "전 무기 쿨다운 -7%", TechBranch.Power, 3, 2,
              TechEffect.WeaponCooldown, 0.07f, 120, 3, 0, 4, 1.7f, "pow3");

            N("pow6", "해체 프로토콜", "보스에게 주는 피해 +12%", TechBranch.Power, 4, 1,
              TechEffect.BossDamage, 0.12f, 200, 5, 1, 4, 1.65f, "pow4");

            N("pow7", "광역 증폭기", "전 무기 사거리 +10%", TechBranch.Power, 4, 2,
              TechEffect.WeaponRange, 0.10f, 184, 4, 1, 4, 1.7f, "pow2", "pow5");

            N("pow8", "임계 출력", "전 무기 피해 +18%", TechBranch.Power, 5, 1,
              TechEffect.WeaponPower, 0.18f, 360, 9, 3, 3, 1.8f, "pow4", "pow6");

            // ==========================================================================
            //  수집 — 버는 쪽 (우하)
            // ==========================================================================
            N("sal1", "자기 수집기", "파편 흡수 반경 +6%", TechBranch.Salvage, 1, -1,
              TechEffect.IntakeRadius, 0.06f, 16, 0, 0, 6, 1.5f, "root");

            N("sal2", "자동 분류기", "크레딧 +5%", TechBranch.Salvage, 2, -1,
              TechEffect.ValueMul, 0.05f, 30, 1, 0, 6, 1.55f, "sal1");

            N("sal3", "분석 모듈", "경험치 +5%", TechBranch.Salvage, 2, -2,
              TechEffect.XpMul, 0.05f, 34, 1, 0, 6, 1.55f, "sal1");

            N("sal4", "광역 회수기", "파편 흡수 반경 +14%", TechBranch.Salvage, 3, -1,
              TechEffect.IntakeRadius, 0.14f, 88, 2, 0, 4, 1.65f, "sal2");

            N("sal5", "감정 회로", "크레딧 +12%", TechBranch.Salvage, 3, -2,
              TechEffect.ValueMul, 0.12f, 112, 3, 0, 5, 1.7f, "sal2", "sal3");

            N("sal6", "학습 코어", "경험치 +12%", TechBranch.Salvage, 4, -2,
              TechEffect.XpMul, 0.12f, 128, 3, 1, 4, 1.7f, "sal3");

            N("sal7", "보급 신호", "아이템 드랍률 +0.6%p", TechBranch.Salvage, 4, -1,
              TechEffect.ItemDropChance, 0.006f, 152, 4, 1, 5, 1.7f, "sal4");

            N("sal8", "연료 농축", "연료 아이템 회복량 +20%", TechBranch.Salvage, 5, -1,
              TechEffect.FuelPickupBonus, 0.20f, 168, 4, 1, 3, 1.7f, "sal7");

            // ---- 재화 발견 (수집 계열의 끝) ----
            N("mat1", "선별 자석", "고철 드랍률 +12%", TechBranch.Salvage, 5, -2,
              TechEffect.ScrapFind, 0.12f, 120, 2, 0, 5, 1.6f, "sal5");

            N("mat2", "회로 감지기", "회로 드랍률 +12%", TechBranch.Salvage, 6, -2,
              TechEffect.CircuitFind, 0.12f, 240, 6, 1, 5, 1.7f, "mat1");

            N("mat3", "코어 공명기", "코어 드랍률 +12%", TechBranch.Salvage, 6, -1,
              TechEffect.CoreFind, 0.12f, 360, 11, 2, 5, 1.8f, "mat2");

            N("mat4", "전방위 정제", "모든 재화 드랍률 +10%", TechBranch.Salvage, 7, -2,
              TechEffect.MatFindAll, 0.10f, 640, 18, 4, 4, 1.85f, "mat3");

            // ==========================================================================
            //  무기 특색 — 패턴별 (아래쪽 바깥)
            //  🔴 무기 이름이 아니라 **패턴**에 붙인다. 무기가 늘어도 여기는 그대로다.
            // ==========================================================================
            N("wp_orbit1", "추가 궤도", "궤도체(절단날·방벽) +1", TechBranch.Weapon, -2, -4,
              TechEffect.BladeCount, 1f, 140, 3, 0, 3, 1.9f, "hull2");

            N("wp_orbit2", "고속 회전", "궤도 회전 속도 +12%", TechBranch.Weapon, -3, -4,
              TechEffect.BladeSpin, 0.12f, 104, 2, 0, 4, 1.6f, "wp_orbit1");

            N("wp_proj1", "추가 발사", "발사체(작살·원반) +1", TechBranch.Weapon, -1, -4,
              TechEffect.HarpoonCount, 1f, 160, 4, 1, 3, 1.9f, "root");

            N("wp_proj2", "강화 미늘", "관통 +1", TechBranch.Weapon, 0, -4,
              TechEffect.HarpoonPierce, 1f, 120, 3, 0, 4, 1.75f, "wp_proj1");

            N("wp_blast1", "추가 탄두", "폭발물(폭탄·지뢰) +1", TechBranch.Weapon, 1, -4,
              TechEffect.BombCount, 1f, 168, 4, 1, 3, 1.9f, "sal1");

            N("wp_blast2", "고폭 장약", "폭발 반경 +10%", TechBranch.Weapon, 2, -4,
              TechEffect.BombRadius, 0.10f, 112, 3, 0, 4, 1.65f, "wp_blast1");

            N("wp_chain1", "분기 회로", "연쇄 대상 +1", TechBranch.Weapon, 3, -4,
              TechEffect.ArcTargets, 1f, 152, 4, 1, 4, 1.8f, "wp_blast1");

            N("wp_chain2", "전도 확장", "연쇄 사거리 +12%", TechBranch.Weapon, 4, -4,
              TechEffect.ArcRange, 0.12f, 120, 3, 0, 4, 1.65f, "wp_chain1");

            N("wp_field1", "장판 확장", "장판 반경 +10%", TechBranch.Weapon, -4, -4,
              TechEffect.VortexRadius, 0.10f, 120, 3, 0, 4, 1.65f, "wp_orbit2");

            N("wp_field2", "고밀도 장판", "장판 피해 +15%", TechBranch.Weapon, -5, -4,
              TechEffect.VortexDamage, 0.15f, 168, 4, 1, 4, 1.7f, "wp_field1");

            // ==========================================================================
            //  특수 — 런을 시작하는 조건 자체를 바꾼다 (위쪽 바깥)
            //  🔴 여기가 제일 비싸다. **판이 시작되는 모양**을 바꾸는 것이라
            //     숫자 노드 수십 개보다 체감이 크다.
            // ==========================================================================
            N("sp_card1", "예비 설계도", "레벨업 카드 선택지 +1", TechBranch.Special, 0, 3,
              TechEffect.CardChoices, 1f, 280, 8, 1, 2, 2.2f, "pow1", "drv1");

            N("sp_lv1", "사전 조율", "런을 레벨 +1로 시작한다", TechBranch.Special, -1, 3,
              TechEffect.StartLevel, 1f, 320, 9, 1, 3, 2.1f, "sp_card1");

            N("sp_wlv1", "숙련 정비", "시작 무기 레벨 +1", TechBranch.Special, 1, 3,
              TechEffect.StartWeaponLevel, 1f, 360, 10, 2, 3, 2.1f, "sp_card1");

            N("sp_combo1", "계열 공명", "조합 발동에 필요한 레벨 -1", TechBranch.Special, 0, 4,
              TechEffect.ComboLevelDown, 1f, 560, 15, 4, 3, 2.3f, "sp_lv1", "sp_wlv1");

            // ---- 계열 끝 노드 (코어를 크게 요구한다) ----
            N("sp_power", "과부하 정비", "전 무기 피해 +25%", TechBranch.Special, 2, 3,
              TechEffect.WeaponPower, 0.25f, 800, 22, 6, 2, 2.4f, "pow8");

            N("sp_hull", "불침 설계", "최대 연료 +150", TechBranch.Special, -2, 3,
              TechEffect.FuelMax, 150f, 720, 20, 5, 2, 2.4f, "hull6");

            N("sp_value", "암거래망", "크레딧 +30%", TechBranch.Special, 3, 3,
              TechEffect.ValueMul, 0.30f, 880, 24, 6, 2, 2.4f, "mat4");

            N("sp_speed", "곡예 기동", "이동 속도 +15%", TechBranch.Special, -3, 3,
              TechEffect.MoveSpeed, 0.15f, 760, 21, 5, 2, 2.4f, "drv7");

            c.techTree = buf.ToArray();
            buf.Clear();
        }
    }
}
