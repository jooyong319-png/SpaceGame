using System;
using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 🔴 **이 게임이 뱀서와 다른 지점.**
    ///
    /// 뱀서는 무기를 6개까지 넓게 모은다. 여기는 **딱 2개**만 갖고 깊게 판다
    /// (우주선이 준 무기 1 + 런 중에 얻은 무기 1).
    /// 그래서 "무엇을 고를까"가 판마다 달라지고, 두 무기의 **계열 조합**마다 히든 능력이 붙는다.
    ///
    /// 🔴 조합을 무기 쌍이 아니라 **태그 쌍**으로 정의한 이유:
    ///    무기 12종이면 쌍이 66개다. 손으로 못 쓰고, 써도 대부분 아무도 못 본다.
    ///    태그 6종이면 21가지로 끝나고 **무기를 20종으로 늘려도 조합표는 그대로**다.
    ///    같은 태그끼리 짝지으면 '특화' 조합이 되는 것도 자연스럽다.
    /// </summary>
    public enum ComboEffect
    {
        None = 0,

        // ---- 같은 계열끼리 = 특화 (6) ----
        CutCut,             // 난도질    — 절삭 피해가 크게 오르고 타격마다 깎임이 누적된다
        PierceP,            // 관통 정렬 — 관통 수 대폭 증가, 일직선상 전부
        ShockShock,         // 과부하    — 연쇄가 두 배로 뻗는다
        BlastBlast,         // 연쇄 폭발 — 폭발이 한 번 더 터진다
        FieldField,         // 영구 장판 — 장판이 넓어지고 남는다
        GravGrav,           // 사건의 지평 — 끌어당김이 압도적으로 강해진다

        // ---- 다른 계열끼리 (15) ----
        CutPierce,          // 절개      — 관통이 지나간 자리에 절삭 흔적이 남는다
        CutShock,           // 전도 날   — 절삭이 닿은 곳에서 방전이 튄다
        CutBlast,           // 파편 폭풍 — 절삭이 지나간 자리가 터진다
        CutField,           // 분쇄 장판 — 절삭 궤도가 장판 끝까지 넓어진다
        CutGravity,         // 견인 분쇄 — 끌려온 것이 절삭에 갈린다

        PierceShock,        // 번개 관통 — 관통한 대상마다 방전
        PierceBlast,        // 작렬 관통 — 관통이 멈춘 자리에서 폭발
        PierceField,        // 회수 관통 — 관통이 멈춘 자리에 장판이 남는다
        PierceGravity,      // 견인 관통 — 관통이 맞힌 것을 끌어당긴다

        ShockBlast,         // 감전 폭탄 — 폭발한 자리에서 방전이 퍼진다
        ShockField,         // 대전 장판 — 장판 피해가 오르고 밖으로 방전한다
        ShockGravity,       // 자기 폭풍 — 모인 것들이 서로 감전된다

        BlastField,         // 압축 붕괴 — 장판이 주기적으로 스스로 터진다
        BlastGravity,       // 중력 폭탄 — 폭발 전에 빨아들인다

        FieldGravity        // 포획장    — 장판이 대상을 붙잡아 둔다
    }

    [Serializable]
    public class ComboDef
    {
        public WeaponTag a;
        public WeaponTag b;
        public ComboEffect effect;
        public string title;
        [TextArea] public string description;
        public Color color = new Color(1f, 0.85f, 0.4f);

        public bool Matches(WeaponTag x, WeaponTag y)
            => (a == x && b == y) || (a == y && b == x);
    }
}
