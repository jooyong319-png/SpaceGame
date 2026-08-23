using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Data;
using SalvageRun.Meta;
using SalvageRun.Run;

namespace SalvageRun.UI
{
    /// <summary>
    /// 수직 슬라이스용 HUD. Canvas 조립을 건너뛰려고 OnGUI로 그린다.
    /// ⚠️ 임시다. 손맛이 확인되면 UGUI로 교체한다 — OnGUI는 WebGL에서 비용이 크다.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public GameContent content;
        public RunConfig config;
        public RunDirector director;
        public ShipController ship;
        public WeaponRig arms;
        public Camera cam;
        public TechTreeScreen tech;

        Texture2D px;
        GUIStyle label, small, big, huge, center, popup, centerSmall, rightSmall;

        void Awake()
        {
            px = new Texture2D(1, 1);
            px.SetPixel(0, 0, Color.white);
            px.Apply();
        }

        /// <summary>
        /// 🔴 한글 폰트. **없으면 WebGL에서 한글이 전부 빈칸이 된다.**
        ///    OnGUI 기본 폰트에는 한글 글리프가 없고, 에디터(Windows)에서만
        ///    시스템 폰트로 대체돼 보인다 — 2026-08-22 첫 itch 빌드에서 그대로 겪었다.
        ///    `Resources`에 두고 런타임에 불러온다 (씬 참조에 의존하지 않게).
        /// </summary>
        static Font korFont;

        static Font KoreanFont
        {
            get
            {
                if (korFont == null) korFont = Resources.Load<Font>("Galmuri11");
                return korFont;
            }
        }

        void Styles(float s)
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label);
                small = new GUIStyle(GUI.skin.label);
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                huge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                popup = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                centerSmall = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
                rightSmall = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };
            }
            label.fontSize = Mathf.RoundToInt(15 * s);
            small.fontSize = Mathf.RoundToInt(12 * s);
            big.fontSize = Mathf.RoundToInt(26 * s);
            huge.fontSize = Mathf.RoundToInt(38 * s);
            center.fontSize = Mathf.RoundToInt(15 * s);
            popup.fontSize = Mathf.RoundToInt(17 * s);
            centerSmall.fontSize = Mathf.RoundToInt(11 * s);
            rightSmall.fontSize = Mathf.RoundToInt(12 * s);
            label.normal.textColor = big.normal.textColor = center.normal.textColor = Color.white;

            var f = KoreanFont;
            if (f != null)
            {
                label.font = small.font = big.font = huge.font = f;
                center.font = popup.font = centerSmall.font = rightSmall.font = f;
            }
        }

        void Box(float x, float y, float w, float h, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), px);
            GUI.color = Color.white;
        }

        void Box(Rect r, Color c) => Box(r.x, r.y, r.width, r.height, c);

        void Update()
        {
            if (tech == null || director == null) return;

            // 🔴 정비소는 **런 밖에서만** 연다. 판 도중에 영구 강화를 사면
            //    "죽기 직전에 사서 버틴다"가 되어 런의 긴장이 사라진다.
            if (director.State == GameState.Field || director.State == GameState.Drafting)
            {
                tech.Close();
                return;
            }

            if (SalvageRun.Core.InputReader.ToggleTechPressed) tech.Toggle();
            if (tech.Open && SalvageRun.Core.InputReader.EscapePressed) tech.Close();
        }

        void OnGUI()
        {
            if (director == null || content == null) return;
            if (tech != null && tech.Open) return;   // 정비소가 열려 있으면 HUD는 쉰다
            float s = Mathf.Max(0.75f, Screen.height / 720f);
            Styles(s);

            switch (director.State)
            {
                case GameState.Title: DrawTitle(s); break;
                case GameState.Ready: DrawReady(s); break;   // ⬜ rev.12: 더 이상 안 쓴다
                case GameState.Field: DrawField(s); break;
                case GameState.Drafting: DrawField(s); DrawDraft(s); break;
                case GameState.Result: DrawResult(s); break;
            }
            DrawDiagnostics(s);
            DrawBotBanner(s);
            DrawComboFlash(s);
            DrawBossIntro(s);
            DrawBurstTimer(s);
            DrawBossArrow(s);
            DrawBaseHp(s);
            DrawRespawn(s);
            DrawCargo(s);
            DrawDockTally(s);
            DrawFullLoadBanner(s);
            DrawBaseArrow(s);
            DrawIntro(s);
            DrawVoyage(s);
            DrawRot(s);
            DrawAnchorArrows(s);
            DrawAnchorStatus(s);
            DrawFinalIntro(s);
            DrawTunePanel(s);
        }

        /// <summary>
        /// 🔴 **조절 패널** (`K`). rev.7~10을 한 판도 안 해보고 만들었으므로
        ///    수치가 전부 추측이다. 내가 더 찍는 대신 **플레이하면서 직접 돌리게** 한다.
        ///
        ///    전부 **배수**라 맵 데이터의 관계(2번 맵이 1번보다 빡세다)는 유지된 채
        ///    전체만 움직인다.
        ///
        ///    맨 아래 한 줄 요약은 **그대로 읽어서 알려 주시라고** 있는 것이다.
        /// </summary>
        void DrawTunePanel(float s)
        {
            if (Core.InputReader.ToggleTunePressed) Tuning.PanelOpen = !Tuning.PanelOpen;
            if (!Tuning.PanelOpen) return;

            float w = 380f * s, rowH = 30f * s;
            var r = new Rect(14f * s, 74f * s, w, rowH * 18f);

            Box(r, new Color(0.04f, 0.06f, 0.10f, 0.92f));
            Frame(r, new Color(0.45f, 0.85f, 1f, 0.7f), 1.5f * s);

            float y = r.y + 8f * s;
            GUI.color = Accent;
            GUI.Label(new Rect(r.x + 10f * s, y, w, 22f * s), "조절 — 돌려보고 값을 알려주세요  [K]", small);
            GUI.color = Color.white;
            y += 26f * s;

            Tuning.DrillDrag       = Row(s, r.x, ref y, w, "드릴 묶임",  Tuning.DrillDrag,       0.05f, 1f);
            Tuning.DrillPower      = Row(s, r.x, ref y, w, "드릴 피해",  Tuning.DrillPower,      0.25f, 4f);
            Tuning.HunterRatio     = Row(s, r.x, ref y, w, "로봇 비율",  Tuning.HunterRatio,     0f,    0.6f);
            Tuning.BaseDrainMul    = Row(s, r.x, ref y, w, "기지 감소",  Tuning.BaseDrainMul,    0f,    3f);
            Tuning.FuelPerCargoMul = Row(s, r.x, ref y, w, "화물 회복",  Tuning.FuelPerCargoMul, 0.2f,  5f);
            Tuning.ShipFuelMul     = Row(s, r.x, ref y, w, "배 연료",    Tuning.ShipFuelMul,     0.3f,  4f);
            Tuning.ThrustFuelMul   = Row(s, r.x, ref y, w, "추진 소모",  Tuning.ThrustFuelMul,   0f,    3f);
            Tuning.JunkSize        = Row(s, r.x, ref y, w, "쓰레기 크기", Tuning.JunkSize,        0.5f,  3f);
            Tuning.JunkDensity     = Row(s, r.x, ref y, w, "쓰레기 밀도", Tuning.JunkDensity,     0.3f,  3f);
            Tuning.TowWeightMul    = Row(s, r.x, ref y, w, "견인 무게",   Tuning.TowWeightMul,    0.2f,  4f);
            Tuning.LegSecondsMul   = Row(s, r.x, ref y, w, "항행 길이",   Tuning.LegSecondsMul,   0.3f,  3f);
            Tuning.IncomingCostMul = Row(s, r.x, ref y, w, "충돌 손실",   Tuning.IncomingCostMul, 0f,    3f);
            Tuning.IncomingRateMul = Row(s, r.x, ref y, w, "잔해 양",     Tuning.IncomingRateMul, 0.2f,  3f);
            Tuning.TurretPowerMul  = Row(s, r.x, ref y, w, "포탑 화력",   Tuning.TurretPowerMul,  0.2f,  5f);

            y += 4f * s;
            if (Btn(new Rect(r.x + 10f * s, y, 110f * s, 24f * s), "기본값", s, true, Warm))
                Tuning.Reset();

            GUI.color = TextDim;
            GUI.Label(new Rect(r.x + 10f * s, y + 26f * s, w - 20f * s, 34f * s), Tuning.Summary, small);
            GUI.color = Color.white;
        }

        /// <summary>손잡이 한 줄. 이름 · 슬라이더 · 현재 값.</summary>
        float Row(float s, float x, ref float y, float w, string label, float v, float lo, float hi)
        {
            GUI.color = new Color(0.82f, 0.88f, 0.95f);
            GUI.Label(new Rect(x + 10f * s, y, 92f * s, 22f * s), label, small);
            GUI.color = Color.white;

            float sliderW = w - 180f * s;
            v = GUI.HorizontalSlider(new Rect(x + 104f * s, y + 8f * s, sliderW, 18f * s), v, lo, hi);

            GUI.color = Accent;
            GUI.Label(new Rect(x + w - 68f * s, y, 58f * s, 22f * s), $"{v:0.00}", small);
            GUI.color = Color.white;

            y += 30f * s;
            return v;
        }

        // ==============================================================================
        //  🔴 색 체계 — 유니티 기본 스킨 그대로면 "만들다 만 것"으로 보인다.
        //     쓰는 색을 여기 모아 두고 화면마다 다른 색을 쓰지 않는다.
        // ==============================================================================

        static readonly Color BgDeep   = new Color(0.028f, 0.034f, 0.055f, 1f);
        static readonly Color Panel    = new Color(0.075f, 0.090f, 0.130f, 0.94f);
        static readonly Color PanelHi  = new Color(0.120f, 0.145f, 0.200f, 0.96f);
        static readonly Color Edge     = new Color(0.30f, 0.36f, 0.48f, 0.85f);
        static readonly Color Accent   = new Color(0.45f, 0.85f, 1.00f, 1f);
        static readonly Color Warm     = new Color(1.00f, 0.82f, 0.42f, 1f);
        static readonly Color Danger   = new Color(1.00f, 0.42f, 0.38f, 1f);
        static readonly Color TextDim  = new Color(0.58f, 0.64f, 0.76f, 1f);

        /// <summary>직접 그리는 버튼. 유니티 기본 버튼은 이 게임과 안 어울린다.</summary>
        bool Btn(Rect r, string text, float s, bool enabled = true, Color? tint = null)
        {
            var mouse = Event.current.mousePosition;
            bool hot = enabled && r.Contains(mouse);
            Color c = tint ?? Accent;

            Box(r, enabled ? (hot ? PanelHi : Panel) : new Color(0.05f, 0.06f, 0.09f, 0.9f));
            Frame(r, enabled ? (hot ? c : new Color(c.r, c.g, c.b, 0.45f))
                             : new Color(0.22f, 0.25f, 0.32f, 0.7f),
                  hot ? 2f * s : 1.4f * s);

            GUI.color = enabled ? (hot ? Color.white : new Color(0.86f, 0.90f, 0.97f)) : TextDim;
            GUI.Label(new Rect(r.x, r.y + (r.height - 20f * s) * 0.5f, r.width, 20f * s), text, center);
            GUI.color = Color.white;

            return enabled && Event.current.type == EventType.MouseDown && r.Contains(mouse);
        }

        // ---------------------------------------------------------------- 타이틀

        float titleClock;

        /// <summary>
        /// 🔴 타이틀 화면. itch에서는 로딩이 끝난 직후 **뭘 봐야 할지 모르는 몇 초**가
        ///    가장 큰 이탈 구간이다. 이름과 버튼 하나만 있는 화면이 그 몇 초를 잡아 준다.
        /// </summary>
        /// <summary>
        /// 🔴 **타이틀** (rev.12). 화면에 **결정 하나만** 둔다.
        ///
        ///    예전엔 준비 화면 하나에 임무 설명 5줄 · 재화 · 정비소 · 조작 · 우주선 6척 ·
        ///    맵 6개를 다 쏟아부었다. **아직 한 판도 안 해본 사람에게 배와 맵을 고르라고**
        ///    하니 고를 근거가 없어 그냥 첫 번째를 눌렀다 — 그건 선택이 아니라 관문이다.
        ///
        ///    이제 배 선택도 맵 선택도 없다. 누르면 바로 임무가 시작된다.
        /// </summary>
        void DrawTitle(float s)
        {
            titleClock += Time.unscaledDeltaTime;

            Box(0, 0, Screen.width, Screen.height, BgDeep);
            DrawTitleStars(s);

            float cx = Screen.width * 0.5f;
            float y = Screen.height * 0.26f;

            float pulse = 0.86f + 0.14f * Mathf.Sin(titleClock * 1.6f);
            GUI.color = new Color(Accent.r * pulse, Accent.g * pulse, Accent.b * pulse);
            GUI.Label(new Rect(0, y, Screen.width, 60f * s), "SALVAGE  RUN", huge);
            GUI.color = Color.white;
            y += 58f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(0, y, Screen.width, 24f * s), "우주를 건너는 마지막 방법", center);
            GUI.color = Color.white;
            y += 50f * s;

            float bw = 280f * s, bh = 42f * s;
            bool hasSave = false;      // ⬜ 이어하기 저장은 아직 없다 (rev.12에서 붙인다)

            if (hasSave)
            {
                if (Btn(new Rect(cx - bw * 0.5f, y, bw, bh), "이어하기", s, true, Accent))
                    director.LeaveTitle();
                y += bh + 8f * s;

                if (Btn(new Rect(cx - bw * 0.5f, y, bw, bh), "새로 시작", s, true, Warm))
                    director.StartNewMission();
                y += bh + 8f * s;
            }
            else
            {
                if (Btn(new Rect(cx - bw * 0.5f, y, bw, bh), "임무 시작", s, true, Accent))
                    director.StartNewMission();
                y += bh + 8f * s;
            }

            // 🔴 **조작 방식은 첫 화면에 있어야 한다.**
            //    랩톱 터치패드로 시작했다가 못 움직이면 그대로 닫는다.
            //    시작한 뒤에 설정을 찾게 하면 늦다.
            bool kb = Core.InputReader.UsingKeyboard;
            float half = bw * 0.5f;

            if (Btn(new Rect(cx - bw * 0.5f, y, half - 3f * s, bh * 0.8f),
                    "마우스 이동", s, true, kb ? TextDim : Accent))
                Core.InputReader.Control = Core.InputReader.Scheme.Mouse;

            if (Btn(new Rect(cx + 3f * s, y, half - 3f * s, bh * 0.8f),
                    "키보드 이동", s, true, kb ? Accent : TextDim))
                Core.InputReader.Control = Core.InputReader.Scheme.Keyboard;

            y += bh * 0.8f + 22f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(0, y, Screen.width, 20f * s),
                kb ? "WASD 이동 · Shift 대시 · Q 화물 버리기 · E 출발"
                   : "좌클릭 홀드 이동 · Shift 대시 · Q 화물 버리기 · E 출발", center);
            GUI.color = Color.white;
        }

        /// <summary>타이틀 배경의 별. 흐르는 게 있어야 화면이 죽어 보이지 않는다.</summary>
        void DrawTitleStars(float s)
        {
            const int Count = 90;
            for (int i = 0; i < Count; i++)
            {
                // 시드 대신 인덱스로 고정 배치 — 매 프레임 흔들리면 안 된다
                float fx = Frac(i * 0.6180339f);
                float fy = Frac(i * 0.7548776f);

                float speed = 6f + (i % 5) * 7f;
                float x = Frac(fx + titleClock * speed / Screen.width) * Screen.width;
                float y = fy * Screen.height;

                float size = (i % 7 == 0 ? 2.6f : 1.5f) * s;
                float a = 0.20f + 0.55f * Frac(i * 0.3141592f);

                Box(x, y, size, size, new Color(0.75f, 0.85f, 1f, a));
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        // ---------------------------------------------------------------- 대기

        /// <summary>
        /// 🔴 첫 화면. 위에서 아래로 **커서 하나로 쌓는다.**
        ///    화면 비율(0.30f · 0.375f …)로 위치를 잡았더니 해상도가 바뀌거나
        ///    항목이 늘 때마다 서로 겹쳤다 — 2026-08-22 피드백: *"첫 화면 UI도 너무 이상하게 되어있어"*.
        ///    쌓아 올리면 무엇을 추가해도 안 겹친다.
        /// </summary>
        void DrawReady(float s)
        {
            Box(0, 0, Screen.width, Screen.height, BgDeep);
            DrawTitleStars(s);

            float y = Screen.height * 0.06f;
            float cx = Screen.width * 0.5f;

            // ---- 제목 ----
            GUI.color = Accent;
            GUI.Label(new Rect(0, y, Screen.width, 44f * s), "SALVAGE RUN", big);
            GUI.color = Color.white;
            y += 46f * s;

            // 🔴 **이야기가 규칙을 설명한다** (rev.11).
            //
            //    지금까지는 *"기지를 왜 지키지?"*에 답이 없어서 규칙이 겉돌았다.
            //    임무 한 줄이 들어가자 **연료가 곧 거리**가 되고, 모든 규칙이 거기서 나온다.
            //    그래서 첫 화면은 규칙 나열이 아니라 **임무 브리핑**이어야 한다.
            GUI.color = Warm;
            GUI.Label(new Rect(0, y, Screen.width, 22f * s),
                "지구가 이 기지를 좌표까지 보냈다. 연료 수단은 전부 파괴됐다", center);
            y += 22f * s;

            GUI.color = new Color(1f, 0.9f, 0.6f);
            GUI.Label(new Rect(0, y, Screen.width, 22f * s),
                "남은 방법은 하나 — 우주 쓰레기를 연료로 바꾼다", center);
            y += 26f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(0, y, Screen.width, 22f * s),
                "정박: 나가서 캔다 · 끌수록 무거워진다 (Q로 버림)", center);
            y += 22f * s;
            GUI.Label(new Rect(0, y, Screen.width, 22f * s),
                "항행: 기지가 나아간다. 커서로 조준해 잔해를 막아라", center);
            y += 22f * s;
            GUI.Label(new Rect(0, y, Screen.width, 22f * s),
                "연료가 0이면 표류 — 좌표에 닿으면 임무 완수", center);
            y += 22f * s;
            GUI.Label(new Rect(0, y, Screen.width, 22f * s),
                (Core.InputReader.UsingKeyboard ? "WASD = 이동" : "좌클릭 홀드 = 이동")
                + " · Shift = 대시 · E = 출발 · K = 조절", center);
            GUI.color = Color.white;
            y += 34f * s;

            // ---- 재화 · 정비소 ----
            var md = MetaSave.Data;
            float bw = 340f * s;

            // 재화는 색을 각자 준다 — 어느 게 귀한 건지 색으로 배우게 한다
            float chipW = bw / 3f;
            DrawMatChip(cx - bw * 0.5f,               y, chipW, s, MatKind.Scrap,   md.scrap);
            DrawMatChip(cx - bw * 0.5f + chipW,       y, chipW, s, MatKind.Circuit, md.circuit);
            DrawMatChip(cx - bw * 0.5f + chipW * 2,   y, chipW, s, MatKind.Core,    md.core);
            y += 26f * s;

            if (Btn(new Rect(cx - bw * 0.5f, y, bw, 34f * s), "정비소 — 영구 강화  [T]", s, true, Warm))
                tech?.Toggle();
            y += 38f * s;

            // 🔴 **조작 방식 선택** (2026-08-21 요청: "키보드랑 마우스 선택 할 수 있게").
            //    마우스 추종이 이 장르의 표준이지만 모두에게 맞지는 않는다 —
            //    특히 랩톱 터치패드에서는 키보드가 훨씬 낫다.
            //    준비 화면에 둔 이유: 판이 시작된 뒤에 바꾸려고 메뉴를 뒤지게 하면 안 된다.
            bool kb = Core.InputReader.UsingKeyboard;
            float half = bw * 0.5f;

            if (Btn(new Rect(cx - bw * 0.5f, y, half - 2f * s, 30f * s),
                    "마우스 이동", s, true, kb ? TextDim : Accent))
                Core.InputReader.Control = Core.InputReader.Scheme.Mouse;

            if (Btn(new Rect(cx + 2f * s, y, half - 2f * s, 30f * s),
                    "키보드 이동", s, true, kb ? Accent : TextDim))
                Core.InputReader.Control = Core.InputReader.Scheme.Keyboard;

            y += 32f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(cx - bw * 0.5f, y, bw, 18f * s),
                kb ? "WASD · 방향키로 이동 · 조준은 커서" : "좌클릭 홀드로 커서 쪽 이동",
                centerSmall);
            GUI.color = Color.white;
            y += 26f * s;

            // ---- 우주선 ----
            SectionLabel(ref y, s, "우주선 — 시작 무기가 정해진다");
            DrawShipPicker(s, ref y);
            y += 14f * s;

            // ---- 맵 ----
            SectionLabel(ref y, s, "맵 — 클리어하면 다음이 열린다");

            int open = Mathf.Clamp(md.unlockedMaps, 1, content.StageCount);
            float mw = 340f * s, mh = 30f * s;

            for (int i = 0; i < content.StageCount; i++)
            {
                var st = content.Stage(i);
                var r = new Rect(cx - mw * 0.5f, y, mw, mh);
                bool unlocked = i < open;

                string t = unlocked
                    ? $"{st.displayName}   ·   웨이브 {st.waveCount}"
                    : "??? — 앞 맵을 클리어하면 열린다";

                if (Btn(r, t, s, unlocked)) director.StartRun(i);
                y += mh + 5f * s;
            }
        }

        void DrawMatChip(float x, float y, float w, float s, MatKind m, int amount)
        {
            GUI.color = Mats.ColorOf(m);
            GUI.Label(new Rect(x, y, w, 22f * s), $"{Mats.Name(m)} {amount}", centerSmall);
            GUI.color = Color.white;
        }

        void SectionLabel(ref float y, float s, string text)
        {
            GUI.color = TextDim;
            GUI.Label(new Rect(0, y, Screen.width, 20f * s), text, center);
            GUI.color = Color.white;
            y += 22f * s;

            // 얇은 구분선 — 구역이 나뉘어 보인다
            Box(Screen.width * 0.30f, y - 6f * s, Screen.width * 0.40f, 1f, new Color(Edge.r, Edge.g, Edge.b, 0.35f));
        }

        /// <summary>
        /// 🔴 우주선 선택. rev.5에서 배는 단순한 스탯 묶음이 아니라
        ///    **시작 무기를 정하므로 조합의 절반을 미리 결정한다.**
        ///    그래서 시작 무기를 항상 같이 보여준다 — 그게 배의 정체다.
        /// </summary>
        void DrawShipPicker(float s, ref float y)
        {
            if (content.ships == null || content.ships.Length == 0) return;

            int n = content.ships.Length;
            float gap = 6f * s;
            float w = Mathf.Min(150f * s, (Screen.width * 0.92f - gap * (n - 1)) / n);
            float h = 68f * s;
            float total = n * w + (n - 1) * gap;
            float x0 = Screen.width * 0.5f - total * 0.5f;

            var cur = MetaSave.CurrentShip(content);

            for (int i = 0; i < n; i++)
            {
                var def = content.ships[i];
                var r = new Rect(x0 + i * (w + gap), y, w, h);

                bool owned = MetaSave.ShipUnlocked(def);
                bool selected = cur != null && cur.id == def.id;

                Box(r, selected ? new Color(def.color.r * 0.32f, def.color.g * 0.32f, def.color.b * 0.32f, 0.95f)
                       : owned ? new Color(0.13f, 0.15f, 0.20f, 0.92f)
                       : new Color(0.08f, 0.09f, 0.12f, 0.92f));

                Frame(r, selected ? Color.white
                        : owned ? new Color(def.color.r, def.color.g, def.color.b, 0.55f)
                        : new Color(0.28f, 0.31f, 0.38f, 0.7f),
                      selected ? 2.5f * s : 1.5f * s);

                float ty = r.y + 5f * s;

                GUI.color = owned ? def.color : new Color(0.45f, 0.47f, 0.55f);
                GUI.Label(new Rect(r.x + 3f * s, ty, r.width - 6f * s, 30f * s), def.displayName, centerSmall);
                GUI.color = Color.white;
                ty += 30f * s;

                GUI.color = owned ? new Color(0.85f, 0.90f, 1f) : new Color(0.40f, 0.42f, 0.50f);
                GUI.Label(new Rect(r.x + 3f * s, ty, r.width - 6f * s, 16f * s),
                    Weapons.Name(def.startingWeapon), centerSmall);
                GUI.color = Color.white;

                if (owned)
                {
                    GUI.color = selected ? new Color(0.7f, 1f, 0.85f) : new Color(0.60f, 0.65f, 0.75f);
                    GUI.Label(new Rect(r.x + 3f * s, r.yMax - 18f * s, r.width - 6f * s, 16f * s),
                        selected ? "선택됨" : "선택", centerSmall);
                    GUI.color = Color.white;

                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) MetaSave.SelectShip(def);
                }
                else
                {
                    bool can = MetaSave.CanBuyShip(def, out _);
                    GUI.color = can ? new Color(1f, 0.9f, 0.5f) : new Color(0.62f, 0.45f, 0.45f);
                    GUI.Label(new Rect(r.x + 2f * s, r.yMax - 30f * s, r.width - 4f * s, 28f * s),
                        ShipCostText(def), centerSmall);
                    GUI.color = Color.white;

                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) MetaSave.BuyShip(def);
                }
            }

            y += h + 4f * s;

            if (cur != null)
            {
                GUI.color = new Color(0.70f, 0.74f, 0.84f);
                GUI.Label(new Rect(0, y, Screen.width, 20f * s), cur.description, center);
                GUI.color = Color.white;
                y += 20f * s;
            }
        }

        static string ShipCostText(ShipDef d)
        {
            string t = "";
            if (d.costScrap > 0) t += $"고철 {d.costScrap}";
            if (d.costCircuit > 0) t += (t.Length > 0 ? "\n" : "") + $"회로 {d.costCircuit}";
            if (d.costCore > 0) t += (t.Length > 0 ? " · " : "") + $"코어 {d.costCore}";
            return t;
        }

        void Frame(Rect r, Color c, float t)
        {
            Box(r.x, r.y, r.width, t, c);
            Box(r.x, r.yMax - t, r.width, t, c);
            Box(r.x, r.y, t, r.height, c);
            Box(r.xMax - t, r.y, t, r.height, c);
        }

        // ---------------------------------------------------------------- 필드

        void DrawField(float s)
        {
            float pad = 18f * s;

            // 연료
            float barW = Mathf.Min(Screen.width * 0.32f, 380f * s);
            float barH = 20f * s;
            float fuel01 = ship.FuelMax > 0f ? Mathf.Clamp01(ship.Fuel / ship.FuelMax) : 0f;

            Box(pad, pad, barW, barH, new Color(0f, 0f, 0f, 0.55f));
            Color fc = fuel01 > 0.35f ? new Color(0.35f, 0.85f, 0.6f)
                     : (fuel01 > 0.15f ? new Color(0.95f, 0.75f, 0.3f) : new Color(1f, 0.4f, 0.35f));
            Box(pad, pad, barW * fuel01, barH, fc);
            GUI.Label(new Rect(pad, pad + barH + 2f * s, barW * 2f, 22f * s),
                $"연료 {ship.Fuel:0} / {ship.FuelMax:0}" +
                (ship.ThrottleNow > 0.01f ? $"   ▲ 출력 {(ship.ThrottleNow * 100f):0}%" : "   ● 관성"), small);

            // 경험치 · 레벨
            float xy = pad + barH + 24f * s;
            Box(pad, xy, barW, 10f * s, new Color(0f, 0f, 0f, 0.5f));
            Box(pad, xy, barW * director.XpRatio, 10f * s, new Color(0.55f, 0.8f, 1f));
            GUI.Label(new Rect(pad, xy + 10f * s, barW * 2f, 22f * s),
                $"Lv.{director.Level}   ·   무기 {director.Stats.OwnedWeaponCount}/{director.MaxWeapons}", small);

            // 보유 무기 목록 — 늘어나는 게 눈에 보여야 성장이 체감된다
            string[] wn = { "절단날", "작살", "소용돌이", "폭탄", "방전" };
            float wy = xy + 30f * s;
            for (int i = 0; i < wn.Length; i++)
            {
                int lv = director.Stats.weaponLevel[i];
                if (lv <= 0) continue;
                GUI.color = new Color(0.7f, 0.95f, 1f);
                GUI.Label(new Rect(pad, wy, 220f * s, 20f * s), $"{wn[i]}  Lv.{lv}", small);
                GUI.color = Color.white;
                wy += 18f * s;
            }

            // 🔴 조합은 이 게임의 차별점이다. 열렸으면 반드시 보여야 한다.
            if (director.ActiveCombo != null)
            {
                GUI.color = director.ActiveCombo.color;
                GUI.Label(new Rect(pad, wy, 320f * s, 20f * s), $"★ {director.ActiveCombo.title}", small);
                GUI.color = new Color(0.75f, 0.75f, 0.8f);
                GUI.Label(new Rect(pad, wy + 17f * s, 340f * s, 20f * s), director.ActiveCombo.description, small);
                GUI.color = Color.white;
                wy += 38f * s;
            }
            else if (director.Stats.OwnedWeaponCount >= 2)
            {
                // 조건만 알려준다. 무엇이 열리는지는 알려주지 않는다 — 발견이 보상이다
                GUI.color = new Color(0.55f, 0.55f, 0.62f);
                GUI.Label(new Rect(pad, wy, 340f * s, 20f * s),
                    $"두 무기를 Lv.{director.ComboLevel}까지 올리면 ???", small);
                GUI.color = Color.white;
                wy += 18f * s;
            }


            // 수익
            float rx = Screen.width - pad - 260f * s;
            GUI.color = new Color(1f, 0.93f, 0.6f);
            GUI.Label(new Rect(rx, pad - 6f * s, 260f * s, 46f * s), $"{director.RunValue}", huge);
            GUI.color = Color.white;
            GUI.Label(new Rect(rx, pad + 38f * s, 260f * s, 20f * s),
                $"파편 {director.RunCollected}개   ·   생존 {director.RunTime:0}초", small);

            // 웨이브 / 보스
            if (director.Phase == FloorPhase.Collecting)
            {
                GUI.Label(new Rect(rx, pad + 58f * s, 260f * s, 20f * s),
                    $"{director.Stage.displayName}   ·   웨이브 {director.Wave}/{director.Stage.waveCount}   ·   보스 {director.NextBossIn:0}초", small);
            }
            else
            {
                GUI.color = new Color(1f, 0.7f, 0.5f);
                GUI.Label(new Rect(rx, pad + 58f * s, 260f * s, 22f * s),
                    $"대형 잔해 — 부위 {director.BossPartsLeft}", label);
                GUI.color = Color.white;
            }

            if (director.ContactHits > 0)
            {
                GUI.color = new Color(1f, 0.5f, 0.45f);
                GUI.Label(new Rect(rx, pad + 80f * s, 260f * s, 20f * s),
                    $"충돌 {director.ContactHits}회 · 연료 -{director.ContactFuelLost:0}", small);
                GUI.color = Color.white;
            }

            DrawPopups(s);
            DrawDangerVignette(s, fuel01);

            if (director.State == GameState.Field)
            {
                float rw = 150f * s;
                if (Btn(new Rect(Screen.width * 0.5f - rw * 0.5f, Screen.height - 42f * s, rw, 28f * s), "지금 종료", s, true, Danger))
                    director.ReturnNow();
            }
        }

        /// <summary>
        /// 🔴 위협을 화면으로 알린다. 맞으면 번쩍이고, 연료가 바닥나면 계속 맥동한다.
        ///    숫자만 줄어드는 위협은 위협으로 안 느껴진다.
        /// </summary>
        void DrawDangerVignette(float s, float fuel01)
        {
            float hit = Juice.Instance != null ? Juice.Instance.hitFlash : 0f;
            float low = fuel01 < 0.3f ? (0.3f - fuel01) / 0.3f : 0f;
            if (low > 0f) low *= 0.35f + 0.35f * Mathf.PingPong(Time.unscaledTime * 2.2f, 1f);

            float a = Mathf.Max(hit * 0.5f, low * 0.75f);

            float th = Mathf.Lerp(20f, 90f, a) * s;
            if (a > 0.01f)
            {
                var c = new Color(1f, 0.2f, 0.18f, a * 0.55f);
                Box(0, 0, Screen.width, th, c);
                Box(0, Screen.height - th, Screen.width, th, c);
                Box(0, 0, th, Screen.height, c);
                Box(Screen.width - th, 0, th, Screen.height, c);
            }

            // 🔴 **기지 피격은 다른 색이다.** 내 피격(붉은색)과 섞이면 어느 쪽이 위험한지 모른다.
            //    입금 보상을 키운 만큼 이 압력이 같이 세져야 "한 번만 더 주울까"가 고민이 된다 —
            //    한쪽만 넣으면 정답이 "항상 꽉 채우기" 하나로 굳는다.
            float bf = Juice.Instance != null ? Juice.Instance.baseFlash : 0f;
            if (bf <= 0.01f) return;

            // rev.8: 기지에 체력이 없으므로 긴급도는 쓰지 않는다. 맞았다는 사실만 알린다
            float ba = bf * 0.5f;

            float bth = Mathf.Lerp(24f, 100f, ba) * s;
            var bc = new Color(1f, 0.55f, 0.1f, ba * 0.5f);   // 주황 = 기지
            Box(0, 0, Screen.width, bth, bc);
            Box(0, Screen.height - bth, Screen.width, bth, bc);
            Box(0, 0, bth, Screen.height, bc);
            Box(Screen.width - bth, 0, bth, Screen.height, bc);
        }

        void DrawPopups(float s)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            for (int i = 0; i < director.Popups.Count; i++)
            {
                var p = director.Popups[i];
                Vector3 sp = cam.WorldToScreenPoint(p.worldPos);
                if (sp.z < 0f) continue;
                float a = Mathf.Clamp01(p.life / 0.9f);
                GUI.color = new Color(p.color.r, p.color.g, p.color.b, a);
                GUI.Label(new Rect(sp.x - 70f * s, Screen.height - sp.y - 20f * s, 140f * s, 26f * s), p.text, popup);
                GUI.color = Color.white;
            }
        }

        // ---------------------------------------------------------------- 카드

        /// <summary>
        /// 🔴 **보상 몇 장 중 몇 장째인가** (rev.10).
        ///    rev.9 이후 입금 한 번에 3~5레벨이 한꺼번에 오르는 게 정상이 됐다.
        ///    남은 장수를 안 보여주면 그건 **보상이 아니라 반복 절차**로 느껴진다 —
        ///    몇 장 남았는지 알아야 기다림이 **기대**가 된다.
        ///
        ///    그리고 뒤로 갈수록 **밝아진다.** 마지막 장이 가장 화려해야
        ///    "쌓였다가 터진다"는 리듬이 완성된다.
        /// </summary>
        void DrawDraftProgress(float s)
        {
            int total = director.DraftTotal;
            if (total <= 1) return;

            int idx = Mathf.Clamp(director.DraftIndex, 0, total - 1);
            float heat = total <= 1 ? 1f : idx / (float)(total - 1);

            float w = Mathf.Min(340f * s, Screen.width * 0.5f);
            float y = Screen.height * 0.20f - 34f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, y, w, 24f * s);

            var c = Color.Lerp(new Color(0.5f, 0.85f, 1f), new Color(1f, 0.8f, 0.35f), heat);

            // 남은 장수를 칸으로 — 숫자보다 칸이 빨리 읽힌다
            float pad = 3f * s;
            float cellW = (r.width - pad * (total - 1)) / total;
            for (int i = 0; i < total; i++)
            {
                var cell = new Rect(r.x + i * (cellW + pad), r.y + 14f * s, cellW, 6f * s);
                Box(cell, i <= idx ? c : new Color(c.r, c.g, c.b, 0.18f));
            }

            GUI.color = c;
            GUI.Label(new Rect(r.x, r.y - 4f * s, r.width, 20f * s),
                $"보상 {idx + 1} / {total}", center);
            GUI.color = Color.white;
        }

        void DrawDraft(float s)
        {
            Box(0, 0, Screen.width, Screen.height, new Color(0.02f, 0.03f, 0.05f, 0.82f));
            bool picking = director.PickingSecondWeapon;
            GUI.Label(new Rect(0, Screen.height * 0.20f, Screen.width, 40f * s),
                picking ? "두 번째 무기를 고른다" : $"Lv.{director.Level} — 강화 선택", big);

            GUI.color = picking ? new Color(1f, 0.85f, 0.45f) : new Color(0.55f, 0.7f, 0.85f);
            GUI.Label(new Rect(0, Screen.height * 0.20f + 40f * s, Screen.width, 22f * s),
                picking ? "이 판에서 무기는 둘뿐이다. 여기서 빌드가 정해진다." : "— 정지 —", small);
            GUI.color = Color.white;

            DrawDraftProgress(s);

            int n = director.Offers.Count;
            if (n == 0) return;

            // 🔴 카드 화면은 이 게임에서 **가장 중요한 순간**이다. 크게 준다
            float cw = Mathf.Min(310f * s, (Screen.width - 40f * s) / n - 12f * s);
            float ch = 240f * s;
            float total = n * cw + (n - 1) * 16f * s;
            float x0 = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height * 0.34f;

            for (int i = 0; i < n; i++)
            {
                var c = director.Offers[i];
                float x = x0 + i * (cw + 16f * s);

                // 🔴 등급을 **색으로** 보여준다. 흰 → 파랑 → 보라 → 주황.
                //    "+25%"와 "+45%"가 같은 색이면 매번 글을 읽어야 한다 (2026-08-22 피드백)
                var rc = c.RarityColor;

                Box(new Rect(x, y, cw, ch), Panel);
                Frame(new Rect(x, y, cw, ch), new Color(rc.r, rc.g, rc.b, 0.9f),
                      c.rarity >= CardRarity.Epic ? 3f * s : 2f * s);
                Box(x, y, cw, 4f * s, rc);   // 위쪽 등급 띠

                // 🔴 **어느 쪽을 키우는 카드인가** (rev.11 — 이 게임의 전략 축).
                //    기지(주황) / 우주선(청록). 자원은 하나인데 쓸 곳이 둘이므로,
                //    **매번 어느 쪽인지 한눈에 보여야** 저울질이 성립한다.
                var side = Cards.SideColor(c.effect);
                var sideRect = new Rect(x + cw - 62f * s, y + 8f * s, 54f * s, 18f * s);
                Box(sideRect, new Color(side.r * 0.25f, side.g * 0.25f, side.b * 0.25f, 0.95f));
                Frame(sideRect, new Color(side.r, side.g, side.b, 0.85f), 1.2f * s);
                GUI.color = side;
                GUI.Label(sideRect, Cards.SideName(c.effect), centerSmall);
                GUI.color = Color.white;

                // 등급 이름 — 일반은 굳이 안 쓴다
                if (c.rarity != CardRarity.Common)
                {
                    GUI.color = rc;
                    GUI.Label(new Rect(x + 10f * s, y + 6f * s, cw - 20f * s, 18f * s),
                        Cards.NameOf(c.rarity), centerSmall);
                    GUI.color = Color.white;
                }
                GUI.color = rc;
                GUI.Label(new Rect(x + 12f * s, y + 24f * s, cw - 24f * s, 28f * s), c.title, label);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 12f * s, y + 54f * s, cw - 24f * s, 110f * s), c.description, small);

                if (c.effect == CardEffect.Weapon)
                {
                    int lv = director.Stats.weaponLevel[c.param];
                    GUI.color = lv <= 0 ? new Color(1f, 0.9f, 0.4f) : new Color(0.7f, 0.9f, 1f);
                    GUI.Label(new Rect(x + 12f * s, y + ch - 68f * s, cw - 24f * s, 20f * s),
                        lv <= 0 ? "NEW — 두 번째 무기" : $"보유 Lv.{lv} → Lv.{lv + 1}", small);
                    GUI.color = Color.white;
                }

                if (Btn(new Rect(x + 12f * s, y + ch - 44f * s, cw - 24f * s, 32f * s), $"선택  [{i + 1}]", s, true, c.color))
                    director.ChooseCard(i);
            }

            int key = SalvageRun.Core.InputReader.NumberPressed();
            if (key >= 1 && key <= n) director.ChooseCard(key - 1);
        }

        // ---------------------------------------------------------------- 결과

        /// <summary>
        /// 🔴 결과 화면. **글이 넘치지 않게 상자 안에 접어 넣는다.**
        ///    한 줄짜리 라벨에 카드 13장을 이어 붙였더니 화면 밖으로 흘러나가
        ///    글자가 잘리고 겹쳤다 (2026-08-22 피드백: *"텍스트 잘리는 부분이 좀 많음"*).
        /// </summary>
        void DrawResult(float s)
        {
            Box(0, 0, Screen.width, Screen.height, BgDeep);
            DrawTitleStars(s);

            float cx = Screen.width * 0.5f;
            float y = Screen.height * 0.16f;

            // ---- 결과 한 줄 ----
            var head = director.Cleared ? Warm : Danger;
            GUI.color = head;
            GUI.Label(new Rect(0, y, Screen.width, 44f * s),
                director.Cleared ? "기지 탈출 성공" : "기지 상실", big);
            GUI.color = Color.white;
            y += 48f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(0, y, Screen.width, 24f * s), director.LastMessage, center);
            GUI.color = Color.white;

            // 🔴 **얼마나 왔는지 보여준다.** 졌을 때 "3/4까지 갔다"를 알면
            //    다음 판을 하고, 아무것도 안 보이면 거기서 끝난다.
            var field = director.field;
            if (field != null && field.AnchorsTotal > 0)
            {
                int broke = field.AnchorsTotal - field.AnchorsAlive;
                y += 26f * s;

                GUI.color = director.Cleared ? new Color(0.6f, 1f, 0.85f) : new Color(1f, 0.6f, 0.55f);
                GUI.Label(new Rect(0, y, Screen.width, 24f * s),
                          $"계류 장치 {broke} / {field.AnchorsTotal} 파괴", center);
                GUI.color = Color.white;
                y -= 26f * s;
            }
            y += 34f * s;

            // ---- 성적표 ----
            float pw = Mathf.Min(560f * s, Screen.width * 0.86f);
            var stat = new Rect(cx - pw * 0.5f, y, pw, 92f * s);
            Box(stat, Panel);
            Frame(stat, new Color(Edge.r, Edge.g, Edge.b, 0.7f), 1.5f * s);

            float col = pw / 4f;
            StatCell(stat.x,           stat.y, col, s, "생존",   $"{director.RunTime:0}초");
            StatCell(stat.x + col,     stat.y, col, s, "레벨",   $"{director.Level}");
            StatCell(stat.x + col * 2, stat.y, col, s, "파편",   $"{director.RunCollected}");
            StatCell(stat.x + col * 3, stat.y, col, s, "크레딧", $"{director.RunValue}");

            // 재화 — 이번 런에 주운 것
            var f = director.field;
            if (f != null)
            {
                float my = stat.y + 52f * s;
                float mw2 = pw / 3f;
                for (int i = 0; i < f.MatsThisRun.Length && i < 3; i++)
                {
                    GUI.color = Mats.ColorOf((MatKind)i);
                    GUI.Label(new Rect(stat.x + mw2 * i, my, mw2, 22f * s),
                        $"{Mats.Name((MatKind)i)} +{f.MatsThisRun[i]}", centerSmall);
                    GUI.color = Color.white;
                }
            }
            y += 102f * s;

            // ---- 고른 강화 — 계산서처럼 ----
            y = DrawReceipt(cx, y, pw, s);

            // ---- 버튼 ----
            float bw = 260f * s, bh = 42f * s;
            if (Btn(new Rect(cx - bw * 0.5f, y, bw, bh), "맵 선택으로", s)) director.BackToReady();
        }

        /// <summary>
        /// 🔴 이번 런에 고른 강화를 **계산서처럼** 항목별로 나열한다.
        ///    한 줄에 이어 붙였더니 화면 밖으로 흘러나가 잘렸고, 무엇을 골랐는지도 안 읽혔다.
        ///    (2026-08-22 요청: *"네모난 창 하나 띄워서 주르르륵 나오면 좋겠다, 계산서 마냥"*)
        ///
        ///    같은 강화를 여러 번 골랐으면 **×N으로 묶는다** — 13줄이 5줄이 되고,
        ///    "무엇에 몰빵했는가"가 한눈에 보인다.
        /// </summary>
        float DrawReceipt(float cx, float y, float pw, float s)
        {
            var taken = director.Taken;
            if (taken == null || taken.Count == 0) return y;

            // 같은 제목끼리 묶는다. 순서는 처음 고른 순서를 유지한다
            var names = new List<string>();
            var counts = new List<int>();
            var colors = new List<Color>();

            for (int i = 0; i < taken.Count; i++)
            {
                // 무기 카드는 "회전 절단날  Lv.3"처럼 레벨이 붙어 제목이 매번 다르다.
                // 레벨 부분을 떼고 묶어야 "절단날 ×5"로 읽힌다
                string n = taken[i].title;
                int cut = n.IndexOf("  Lv.");
                if (cut > 0) n = n.Substring(0, cut);

                int at = names.IndexOf(n);
                if (at >= 0) { counts[at]++; continue; }

                names.Add(n);
                counts.Add(1);
                colors.Add(taken[i].RarityColor);
            }

            float lineH = 20f * s;
            int maxLines = Mathf.Max(3, Mathf.FloorToInt((Screen.height * 0.30f) / lineH));
            int shown = Mathf.Min(names.Count, maxLines);

            float h = (shown + 2) * lineH + 26f * s;
            var box = new Rect(cx - pw * 0.5f, y, pw, h);

            Box(box, Panel);
            Frame(box, new Color(Edge.r, Edge.g, Edge.b, 0.85f), 1.5f * s);

            float ty = box.y + 8f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(box.x, ty, box.width, lineH), "정 비 내 역", center);
            GUI.color = Color.white;
            ty += lineH + 2f * s;

            Divider(box, ty, s);
            ty += 6f * s;

            float padX = 18f * s;
            for (int i = 0; i < shown; i++)
            {
                GUI.color = colors[i];
                GUI.Label(new Rect(box.x + padX, ty, box.width - padX * 2f, lineH), names[i], small);

                // 개수는 오른쪽 끝에 — 계산서처럼 줄이 맞아야 읽힌다
                GUI.color = counts[i] > 1 ? Warm : TextDim;
                GUI.Label(new Rect(box.x + padX, ty, box.width - padX * 2f, lineH),
                    counts[i] > 1 ? $"×{counts[i]}" : "·", rightSmall);
                GUI.color = Color.white;

                ty += lineH;
            }

            if (names.Count > shown)
            {
                GUI.color = TextDim;
                GUI.Label(new Rect(box.x, ty, box.width, lineH), $"외 {names.Count - shown}종", center);
                GUI.color = Color.white;
                ty += lineH;
            }

            Divider(box, ty + 2f * s, s);
            ty += 8f * s;

            GUI.color = TextDim;
            GUI.Label(new Rect(box.x + padX, ty, box.width - padX * 2f, lineH), "합계", small);
            GUI.color = Warm;
            GUI.Label(new Rect(box.x + padX, ty, box.width - padX * 2f, lineH), $"{taken.Count}개", rightSmall);
            GUI.color = Color.white;

            return box.yMax + 12f * s;
        }

        void Divider(Rect box, float y, float s)
        {
            Box(box.x + 14f * s, y, box.width - 28f * s, 1f, new Color(Edge.r, Edge.g, Edge.b, 0.5f));
        }

        void StatCell(float x, float y, float w, float s, string label, string value)
        {
            GUI.color = TextDim;
            GUI.Label(new Rect(x, y + 8f * s, w, 18f * s), label, centerSmall);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 24f * s, w, 26f * s), value, center);
        }

        /// <summary>
        /// 🔴 **기지 연료 — 이 게임의 패배 조건이다** (rev.10).
        ///    화면에서 가장 눈에 띄어야 한다. 연료가 0이면 끝이다.
        ///
        ///    "몇 초 남았는가"를 같이 보여준다 — 비율만 보면 얼마나 급한지 모른다.
        ///    남은 시간은 **지금 감소율 기준**이라 맵마다 다르게 나온다.
        /// </summary>
        void DrawBaseHp(float s)
        {
            if (director.State != GameState.Field || director.homeBase == null) return;

            var hb = director.homeBase;
            float w = Mathf.Min(Screen.width * 0.42f, 460f * s), h = 20f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 12f * s, w, h);

            float t = hb.FuelRatio;
            var c = t > 0.5f ? new Color(0.45f, 0.9f, 0.8f)
                  : t > 0.25f ? Warm
                  : new Color(1f, 0.4f, 0.35f);

            Box(r, new Color(0f, 0f, 0f, 0.6f));
            Box(r.x, r.y, r.width * t, r.height, c);
            Frame(r, new Color(c.r, c.g, c.b, 0.9f), 1.5f * s);

            // 🔴 **남은 여정**을 같이 보여준다 — 이 게임의 목표가 좌표 도달이므로
            //    *"얼마나 남았나"*가 항상 보여야 지금의 선택에 뜻이 생긴다
            int total = content.StageCount;
            int done = Mathf.Clamp(director.MapIndex, 0, total - 1);

            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y, r.width, h),
                $"기지 연료  {hb.Fuel:0} / {hb.FuelMax:0}      구간 {done + 1} / {total}", center);

            if (t < 0.25f)
            {
                GUI.color = new Color(1f, 0.4f, 0.35f, 0.6f + 0.4f * Mathf.Sin(Time.time * 9f));
                GUI.Label(new Rect(r.x, r.yMax + 3f * s, r.width, 20f * s),
                    "기지 연료 고갈 임박 — 쓰레기를 가져와라", center);
                GUI.color = Color.white;
            }
        }

        /// <summary>🔴 격침 중. **게임이 안 끝났다는 걸** 분명히 알려야 한다.</summary>
        void DrawRespawn(float s)
        {
            if (director.RespawnLeft <= 0f) return;

            Box(0, Screen.height * 0.38f, Screen.width, 84f * s, new Color(0.12f, 0.03f, 0.03f, 0.8f));

            GUI.color = new Color(1f, 0.5f, 0.42f);
            GUI.Label(new Rect(0, Screen.height * 0.38f + 8f * s, Screen.width, 32f * s), "격침", big);
            GUI.color = new Color(0.9f, 0.92f, 1f);
            GUI.Label(new Rect(0, Screen.height * 0.38f + 44f * s, Screen.width, 22f * s),
                $"재출항까지 {director.RespawnLeft:0.0}초   ·   그동안 기지는 무방비다", center);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 화물 상태. **저울질이 보여야 결정이 성립한다.**
        ///    얼마나 실었는지 · 얼마나 느려졌는지 · 입금하면 몇 레벨 오르는지를
        ///    한자리에 붙여 놓는다. 이 셋이 안 보이면 "더 모을까"를 판단할 수 없다.
        /// </summary>
        void DrawCargo(float s)
        {
            if (director.Travelling) return;      // 항행 중엔 화물·도킹 표시가 무의미하다
            if (director.State != GameState.Field) return;

            float w = 300f * s, h = 54f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - h - 24f * s, w, h);

            float fill = director.CargoRatio;
            bool full = fill >= 0.999f;

            Box(r, new Color(0.06f, 0.08f, 0.12f, 0.85f));

            // 🔴 입금 중에는 막대가 **줄어드는 것 자체가 연출**이다.
            //    카운터가 가속하며 떨어지는 걸 보고 있는 그 3초가 보상의 몸통이다.
            if (director.Depositing)
            {
                float done = director.DepositTotal <= 0 ? 1f
                           : 1f - director.CargoCount / (float)director.DepositTotal;

                Box(r.x + 2f * s, r.y + 2f * s, (r.width - 4f * s) * done, 12f * s,
                    new Color(0.5f, 1f, 0.8f, 0.35f + 0.35f * done));
            }

            // 막대 — 가득 차면 붉게 (돌아가라는 신호)
            var barC = full ? Danger
                     : fill > 0.7f ? Warm
                     : new Color(0.45f, 0.9f, 0.75f);
            Box(r.x + 2f * s, r.y + 2f * s, (r.width - 4f * s) * fill, 12f * s, barC);
            Frame(r, new Color(barC.r, barC.g, barC.b, 0.8f), 1.5f * s);

            GUI.color = new Color(0.85f, 0.9f, 0.97f);
            GUI.Label(new Rect(r.x, r.y + 15f * s, r.width, 20f * s),
                $"화물 {director.CargoCount} / {director.CargoMax}", center);

            // 속도 저하 — 무게의 대가를 숫자로
            float slow = (1f - director.CargoWeightMul) * 100f;
            GUI.color = slow > 30f ? Danger : TextDim;
            GUI.Label(new Rect(r.x, r.y + 33f * s, r.width * 0.5f, 18f * s),
                slow > 1f ? $"속도 -{slow:0}%" : "가볍다", centerSmall);

            // 입금하면 오를 레벨
            int levels = PendingLevels();
            GUI.color = levels > 0 ? new Color(0.5f, 1f, 0.8f) : TextDim;
            GUI.Label(new Rect(r.x + r.width * 0.5f, r.y + 33f * s, r.width * 0.5f, 18f * s),
                levels > 0 ? $"입금 시 +{levels}레벨" : "입금 대기", centerSmall);
            GUI.color = Color.white;

            if (full && !director.Depositing)
            {
                GUI.color = new Color(1f, 0.45f, 0.4f, 0.6f + 0.4f * Mathf.Sin(Time.time * 8f));
                GUI.Label(new Rect(r.x, r.y - 22f * s, r.width, 20f * s), "화물칸 가득 — 모선으로", center);
                GUI.color = Color.white;
            }

            DrawStreak(s, r);
            DrawTravel(s, r);
        }

        /// <summary>
        /// 🔴 **다음 지역으로 떠나기** — 이 게임에서 이기는 유일한 길이다 (rev.10).
        ///    기지에 있을 때만, 그리고 여비를 낼 수 있을 때만 뜬다.
        ///
        ///    자동으로 출발시키지 않은 이유: 그러면 결정이 사라진다.
        ///    여비가 있으므로 **"지금 갈까, 더 캐고 갈까"**가 매 지역마다 돌아온다.
        /// </summary>
        void DrawTravel(float s, Rect cargo)
        {
            if (!director.AtBase || director.State != GameState.Field) return;

            float w = 340f * s, h = 34f * s;
            var r = new Rect(cargo.x + cargo.width * 0.5f - w * 0.5f, cargo.y - h - 34f * s, w, h);

            bool can = director.CanTravel;
            bool last = director.AtLastRegion;

            if (last)
            {
                GUI.color = new Color(1f, 0.85f, 0.45f);
                GUI.Label(r, "최종 지역 — 여기서 버텨라", center);
                GUI.color = Color.white;
                return;
            }

            // 🔴 **왜 못 떠나는지** 말해 준다. 버튼이 그냥 안 되면 그건 버그로 읽힌다
            if (director.AnchorsBlocking)
            {
                var fld = director.field;
                GUI.color = new Color(1f, 0.45f, 0.5f, 0.75f + 0.25f * Mathf.Sin(Time.time * 5f));
                GUI.Label(r, $"계류 장치에 붙잡혀 있다 — {fld.AnchorsAlive}개 남음", center);
                GUI.color = TextDim;
                GUI.Label(new Rect(r.x, r.yMax - 4f * s, r.width, 18f * s),
                          "화살표를 따라가 전부 끊어라", centerSmall);
                GUI.color = Color.white;
                return;
            }

            if (!can)
            {
                GUI.color = TextDim;
                GUI.Label(r, $"다음 지역 — 연료 {director.TravelCost:0} 필요", centerSmall);
                GUI.color = Color.white;
                return;
            }

            // 🔴 **다음이 최종 지역이면 미리 알린다.**
            //    모르고 들어가서 갑자기 연료가 2.6배로 닳으면 그건 난이도가 아니라 함정이다.
            //    준비할 기회를 준 뒤에 어려운 건 괜찮다 — **모르고 당하는 것**이 나쁘다.
            bool nextIsFinal = director.MapIndex + 1 >= content.StageCount - 1;

            if (nextIsFinal)
            {
                GUI.color = new Color(1f, 0.5f, 0.5f, 0.75f + 0.25f * Mathf.Sin(Time.time * 4f));
                GUI.Label(new Rect(r.x, r.y - 40f * s, r.width, 20f * s),
                          "다음은 최종 지역 — 돌아올 수 없다", center);

                GUI.color = TextDim;
                GUI.Label(new Rect(r.x, r.y - 22f * s, r.width, 20f * s),
                          "기지 연료를 최대한 채우고 갈 것", centerSmall);
                GUI.color = Color.white;
            }

            string label = nextIsFinal
                ? $"최종 지역으로 출발  [E]   (연료 -{director.TravelCost:0})"
                : $"다음 지역으로 출발  [E]   (연료 -{director.TravelCost:0})";

            if (Btn(r, label, s, true, nextIsFinal ? Danger : Warm))
                director.TravelToNext();

            if (Core.InputReader.TravelPressed) director.TravelToNext();
        }

        /// <summary>
        /// 🔴 **연쇄 입금.** 배수 자체보다 *"지금 죽으면 끊긴다"*를 보이게 하는 게 목적이다.
        ///    그래서 숫자를 작게 쓰지 않고 화물 막대 바로 위에 크게 붙인다 —
        ///    돌아갈지 더 주울지 고민하는 그 순간에 눈에 들어와야 한다.
        /// </summary>
        void DrawStreak(float s, Rect cargo)
        {
            int st = director.DepositStreak;
            if (st <= 0) return;

            float w = 150f * s, h = 24f * s;
            var r = new Rect(cargo.x + cargo.width * 0.5f - w * 0.5f, cargo.y - h - 6f * s, w, h);

            // 연쇄가 길수록 뜨거워진다
            float heat = Mathf.Clamp01(st / 4f);
            var c = Color.Lerp(new Color(0.5f, 1f, 0.8f), new Color(1f, 0.75f, 0.25f), heat);

            Box(r, new Color(0.06f, 0.08f, 0.12f, 0.8f));
            Frame(r, new Color(c.r, c.g, c.b, 0.75f), 1.5f * s);

            GUI.color = c;
            GUI.Label(r, $"연쇄 {st}  ×{director.StreakMul:0.00}", center);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 **도킹 정산** (2026-08-22 요청: *"멈추면서 재화 얼마나 모았고
        ///    레벨업이 파파파파파박 되는 느낌"*).
        ///
        ///    빨려 들어가 **멈춘 그 자리**에 숫자를 띄운다.
        ///    움직이면서 받는 보상은 배경음이고, **멈춰서 받는 보상은 사건**이다.
        ///
        ///    입금 중에는 남은 화물이 줄어드는 걸 보여주고,
        ///    끝나면 **얼마 벌었는지 · 몇 레벨 오르는지**를 크게 띄운다.
        /// </summary>
        void DrawDockTally(float s)
        {
            if (director.Travelling) return;      // 항행 중엔 화물·도킹 표시가 무의미하다
            if (director.State != GameState.Field && director.State != GameState.Drafting) return;

            bool live = director.Depositing;
            float fade = live ? 1f : Mathf.Clamp01(director.dockFlash / 2.2f);
            if (!live && fade <= 0.01f) return;

            float w = 320f * s, h = 96f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.30f, w, h);

            var accent = new Color(0.55f, 1f, 0.85f);

            Box(r, new Color(0.03f, 0.09f, 0.08f, 0.80f * fade));
            Frame(r, new Color(accent.r, accent.g, accent.b, 0.85f * fade), 2f * s);

            GUI.color = new Color(accent.r, accent.g, accent.b, fade);
            GUI.Label(new Rect(r.x, r.y + 6f * s, r.width, 24f * s),
                      live ? "입 고 중" : "입 고 완 료", center);

            if (live)
            {
                // 남은 화물이 줄어드는 걸 보여준다 — 줄어드는 것 자체가 연출이다
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(r.x, r.y + 32f * s, r.width, 34f * s),
                          $"{director.CargoCount}", big);

                float done = director.DepositTotal <= 0 ? 1f
                           : 1f - director.CargoCount / (float)director.DepositTotal;
                Box(r.x + 12f * s, r.yMax - 16f * s, (r.width - 24f * s) * done, 6f * s,
                    new Color(accent.r, accent.g, accent.b, fade));
            }
            else
            {
                GUI.color = new Color(1f, 0.95f, 0.65f, fade);
                GUI.Label(new Rect(r.x, r.y + 30f * s, r.width, 34f * s),
                          $"+{director.DockedValue:N0}", big);

                GUI.color = new Color(0.8f, 0.9f, 1f, fade * 0.9f);
                GUI.Label(new Rect(r.x, r.yMax - 26f * s, r.width, 20f * s),
                          director.DepositBonus > 1.05f ? $"보너스 ×{director.DepositBonus:0.00}" : "", center);
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 만재 입금은 **다른 사건**이어야 한다.
        ///    보너스(×1.6)는 원래도 주고 있었는데 화면상 100%나 60%나 똑같이 생겨서
        ///    "만재로 넣었다"가 아무 감각도 아니었다. 보상은 이미 주고 있으니 연출만 붙인다.
        /// </summary>
        void DrawFullLoadBanner(float s)
        {
            float f = director.fullLoadFlash;
            if (f <= 0.01f) return;

            float a = Mathf.Clamp01(f / 1.6f);
            float pop = 1f + (1f - a) * 0.35f;      // 뜰 때 커졌다가 가라앉는다

            // 화면 전체가 한 번 물든다
            Box(0, 0, Screen.width, Screen.height, new Color(1f, 0.85f, 0.4f, a * 0.10f));

            float w = 420f * s * pop, h = 62f * s * pop;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.34f - h * 0.5f, w, h);

            Box(r, new Color(0.08f, 0.07f, 0.04f, a * 0.8f));
            Frame(r, new Color(1f, 0.8f, 0.35f, a), 2f * s);

            GUI.color = new Color(1f, 0.88f, 0.5f, a);
            GUI.Label(new Rect(r.x, r.y + 6f * s, r.width, 34f * s), "만 재 입 금", big);
            GUI.color = new Color(1f, 0.75f, 0.4f, a * 0.9f);
            GUI.Label(new Rect(r.x, r.y + 38f * s, r.width, 20f * s),
                      $"×{director.DepositBonus:0.00}", center);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 입금하면 몇 레벨 오르는지. 저울질의 핵심 정보다.
        ///
        /// 🔴 계산은 `RunDirector`가 한다. 여기서 따로 계산하던 것을 지웠다 —
        ///    HUD 쪽은 만재 보너스 0.6을 손으로 박아 두고 있어서 **연쇄 배수가 빠졌고**,
        ///    그래서 화면 숫자가 실제 입금액보다 작았다. 계산이 두 군데면 반드시 갈라진다.
        /// </summary>
        int PendingLevels() => director.CargoXp <= 0f ? 0 : director.PendingLevels;

        /// <summary>🔴 모선이 화면 밖이면 방향을 알려준다. 어디로 돌아갈지 모르면 루프가 안 돈다.</summary>
        /// <summary>
        /// 🔴 **지역이 썩고 있다는 걸 보여준다** (rev.11).
        ///
        ///    정박이 길어질수록 로봇이 는다. 그런데 **말해 주지 않으면**
        ///    플레이어는 "왜 갑자기 어려워지지"만 느끼고 원인을 모른다 —
        ///    그러면 그건 압박이 아니라 **부당함**이다.
        ///
        ///    출발 압박이 목적이므로 **"뜰 때가 됐다"가 읽혀야** 한다.
        /// </summary>
        void DrawRot(float s)
        {
            if (director.State != GameState.Field || director.Travelling) return;

            var field = director.field;
            if (field == null) return;

            float rot = field.RotRatio;
            if (rot <= 1.05f) return;               // 초반엔 굳이 안 띄운다

            float t = Mathf.InverseLerp(1f, 3f, rot);
            var c = Color.Lerp(new Color(0.8f, 0.85f, 0.5f), new Color(1f, 0.45f, 0.35f), t);

            float w = Mathf.Min(320f * s, Screen.width * 0.4f);
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 60f * s, w, 20f * s);

            GUI.color = t > 0.6f
                ? new Color(c.r, c.g, c.b, 0.7f + 0.3f * Mathf.Sin(Time.time * 5f))
                : c;
            GUI.Label(r, $"이 지역이 시끄러워지고 있다  ×{rot:0.0}", center);

            if (t > 0.7f)
            {
                GUI.color = new Color(1f, 0.6f, 0.45f, 0.85f);
                GUI.Label(new Rect(r.x, r.yMax, r.width, 18f * s), "떠날 때가 됐다", centerSmall);
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 **도입 연출의 자막.** 어레이가 뜯겨 나가는 동안 관제가 한마디 던진다.
        ///    설명문 다섯 줄보다 **관제 한마디**가 훨씬 빨리 박힌다.
        /// </summary>
        void DrawIntro(float s)
        {
            if (!director.InIntro) return;

            float t = director.IntroProgress;

            // 마지막 어레이가 터질 때 화면이 붉어진다
            if (t > 0.72f && t < 0.84f)
            {
                float f = 1f - Mathf.InverseLerp(0.72f, 0.84f, t);
                Box(0, 0, Screen.width, Screen.height, new Color(1f, 0.25f, 0.15f, f * 0.35f));
            }

            float w = Mathf.Min(560f * s, Screen.width * 0.85f);
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.68f, w, 96f * s);

            string line =
                t < 0.28f ? "항행 중 — 잔해 밀집 구역 진입" :
                t < 0.50f ? "경고 · 태양광 어레이 1번 손실" :
                t < 0.72f ? "경고 · 어레이 2번 손실. 회피 불가" :
                            "— 지구 관제 —";

            Box(r, new Color(0.03f, 0.04f, 0.07f, 0.78f));

            GUI.color = t < 0.72f ? new Color(1f, 0.55f, 0.4f) : Accent;
            GUI.Label(new Rect(r.x, r.y + 8f * s, r.width, 26f * s), line, center);
            GUI.color = Color.white;

            if (t >= 0.72f)
            {
                GUI.color = new Color(1f, 0.95f, 0.9f);
                GUI.Label(new Rect(r.x, r.y + 36f * s, r.width, 24f * s),
                          "연료 수단 전멸 확인. 잔해 회수로 자체 생산할 것.", center);
                GUI.color = TextDim;
                GUI.Label(new Rect(r.x, r.y + 62f * s, r.width, 22f * s),
                          "귀환 계획 없음.", center);
                GUI.color = Color.white;
            }

            // 스킵 — 두 번째부터는 매번 보면 고문이다
            GUI.color = new Color(0.6f, 0.65f, 0.75f, 0.7f);
            GUI.Label(new Rect(0, Screen.height - 34f * s, Screen.width, 20f * s),
                      "아무 키나 눌러 건너뛰기", center);
            GUI.color = Color.white;

            if (Event.current.type == EventType.KeyDown ||
                Event.current.type == EventType.MouseDown) director.SkipIntro();
        }

        // ================================================================ 항행 (rev.11)

        /// <summary>
        /// 🔴 **항행 화면.** 남은 거리 · 조작 안내 · 시작 브리핑.
        ///
        ///    조작이 *몰기 → 조준*으로 바뀌는 국면이라, **바뀌었다는 걸 말해 주지 않으면**
        ///    플레이어는 배가 고장 난 줄 안다.
        /// </summary>
        void DrawVoyage(float s)
        {
            if (director.State != GameState.Field || !director.Travelling) return;

            // ---- 남은 거리 ----
            float w = Mathf.Min(Screen.width * 0.5f, 520f * s), h = 22f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 60f * s, w, h);

            float t = director.LegProgress;

            Box(r, new Color(0f, 0f, 0f, 0.55f));
            Box(r.x, r.y, r.width * t, r.height, new Color(0.55f, 0.85f, 1f));
            Frame(r, new Color(0.6f, 0.9f, 1f, 0.9f), 1.5f * s);

            float left = Mathf.Max(0f, (1f - t) * director.LegSeconds);
            GUI.color = Color.white;
            GUI.Label(r, $"항행 중 — 다음 지역까지 {left:0}초", center);

            // ---- 조작 안내 (계속 띄운다. 이 국면은 조작이 다르다) ----
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.75f);
            GUI.Label(new Rect(r.x, r.yMax + 3f * s, r.width, 20f * s),
                      "커서로 조준 — 기지 무기가 그쪽을 먼저 쏜다", centerSmall);
            GUI.color = Color.white;

            DrawVoyageIntro(s);
        }

        /// <summary>
        /// 🔴 항행 시작 브리핑. **한 줄 목표 + 한 줄 조작**이면 충분하다.
        /// </summary>
        void DrawVoyageIntro(float s)
        {
            float f = director.legIntro;
            if (f <= 0.01f) return;

            float a = Mathf.Min(1f, Mathf.Clamp01(f / 3.5f) * 2.2f);

            Box(0, 0, Screen.width, Screen.height, new Color(0.5f, 0.35f, 0.05f, a * 0.14f));

            float w = Mathf.Min(520f * s, Screen.width * 0.84f), h = 104f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.30f, w, h);

            Box(r, new Color(0.09f, 0.06f, 0.02f, a * 0.88f));
            Frame(r, new Color(1f, 0.8f, 0.45f, a), 2f * s);

            GUI.color = new Color(1f, 0.85f, 0.5f, a);
            GUI.Label(new Rect(r.x, r.y + 10f * s, r.width, 34f * s), "항 행", big);

            GUI.color = new Color(1f, 0.95f, 0.9f, a);
            GUI.Label(new Rect(r.x, r.y + 48f * s, r.width, 24f * s),
                      "우주선은 격납됐다. 기지 무기로 잔해를 막아라", center);

            GUI.color = new Color(1f, 0.7f, 0.55f, a);
            GUI.Label(new Rect(r.x, r.y + 74f * s, r.width, 22f * s),
                      "기지에 부딪히면 연료를 잃는다 = 거리를 잃는다", center);
            GUI.color = Color.white;
        }

        // ================================================================ 계류 장치 (최종 지역)

        /// <summary>
        /// 🔴 **닻이 어디 있는지 못 찾으면 게임이 안 끝난다.**
        ///    맵이 넓으므로 화면 밖에 있으면 가장자리에 화살표로 가리킨다.
        ///    기지 화살표(청록)와 **색을 다르게**(붉은) 해서 무엇을 가리키는지 구분되게 한다.
        /// </summary>
        void DrawAnchorArrows(float s)
        {
            if (director.State != GameState.Field || cam == null) return;

            var field = director.field;
            if (field == null || field.AnchorsTotal <= 0) return;

            float margin = 64f * s;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            for (int i = 0; i < field.Anchors.Count; i++)
            {
                var a = field.Anchors[i];
                if (a == null || !a.Alive) continue;

                Vector3 sp = cam.WorldToScreenPoint(a.transform.position);
                sp.y = Screen.height - sp.y;

                bool onScreen = sp.z > 0f && sp.x > margin && sp.x < Screen.width - margin
                                          && sp.y > margin && sp.y < Screen.height - margin;
                if (onScreen) continue;

                Vector2 dir = new Vector2(sp.x, sp.y) - center;
                if (sp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
                dir.Normalize();

                float rx = Screen.width * 0.5f - margin;
                float ry = Screen.height * 0.5f - margin;
                Vector2 at = center + dir * Mathf.Min(rx / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                                                      ry / Mathf.Max(0.001f, Mathf.Abs(dir.y)));

                var c = new Color(1f, 0.45f, 0.5f, 0.55f + 0.25f * Mathf.Sin(Time.time * 4f + i));
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                ArrowBar(at, ang + 150f, 24f * s, 5f * s, c);

                // 🔴 몇 번 닻인지 같이 쓴다 — 번호가 곧 난이도 순서다
                GUI.color = c;
                GUI.Label(new Rect(at.x - 24f * s, at.y + 12f * s, 48f * s, 18f * s),
                          $"{i + 1}번", centerSmall);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// 🔴 **왜 갑자기 연료가 빨리 닳는지** 모르면 억울하다.
        ///    기지 연료 바 바로 아래에 **몇 개 남았고 몇 배로 닳는지**를 붙인다.
        ///    닻을 부수면 배수가 내려가는 게 눈에 보인다 — 그게 진행감이다.
        /// </summary>
        void DrawAnchorStatus(float s)
        {
            if (director.State != GameState.Field) return;

            var field = director.field;
            var hb = director.homeBase;
            if (field == null || hb == null || field.AnchorsTotal <= 0) return;

            float w = Mathf.Min(Screen.width * 0.42f, 460f * s);
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 34f * s, w, 22f * s);

            int alive = field.AnchorsAlive;
            int broke = field.AnchorsTotal - alive;

            var c = alive > 0 ? new Color(1f, 0.45f, 0.5f) : new Color(0.6f, 1f, 0.85f);

            GUI.color = c;
            GUI.Label(r, alive > 0
                ? $"계류 장치 {broke} / {field.AnchorsTotal} 파괴   ·   기지 부담 ×{hb.DrainMul:0.0}"
                : "계류 해제 완료", center);
            GUI.color = Color.white;

            // 부순 직후 크게 알린다
            float f = director.anchorFlash;
            if (f > 0.01f)
            {
                float a = Mathf.Clamp01(f / 2.6f);
                GUI.color = new Color(0.6f, 1f, 0.85f, a);
                GUI.Label(new Rect(0, Screen.height * 0.24f, Screen.width, 34f * s),
                          alive > 0 ? $"계류 장치 파괴 — 남은 {alive}개" : "계류 해제!", big);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// 🔴 **뭘 해야 하는지 모르면 아무것도 안 한다.**
        ///    최종 지역에 도착하면 한 번, 크게, 짧게 말해 준다.
        ///    긴 설명은 안 읽는다 — **한 줄 목표 + 한 줄 이유**면 충분하다.
        /// </summary>
        void DrawFinalIntro(float s)
        {
            float f = director.finalIntro;
            if (f <= 0.01f) return;

            float a = Mathf.Clamp01(f / 6f);
            a = Mathf.Min(1f, a * 2.2f);        // 뜰 때 빠르게, 사라질 때 천천히

            Box(0, 0, Screen.width, Screen.height, new Color(0.4f, 0.05f, 0.1f, a * 0.20f));

            float w = Mathf.Min(560f * s, Screen.width * 0.86f), h = 132f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.30f, w, h);

            Box(r, new Color(0.10f, 0.03f, 0.05f, a * 0.88f));
            Frame(r, new Color(1f, 0.45f, 0.5f, a), 2f * s);

            GUI.color = new Color(1f, 0.55f, 0.55f, a);
            GUI.Label(new Rect(r.x, r.y + 10f * s, r.width, 34f * s), "계 류 됨", big);

            GUI.color = new Color(1f, 0.92f, 0.9f, a);
            GUI.Label(new Rect(r.x, r.y + 50f * s, r.width, 24f * s),
                      "거대 잔해가 기지를 붙잡았다 — 계류 장치 4개", center);

            GUI.color = new Color(1f, 0.75f, 0.55f, a);
            GUI.Label(new Rect(r.x, r.y + 76f * s, r.width, 24f * s),
                      "전부 끊어내기 전에는 떠날 수 없다", center);

            GUI.color = new Color(0.75f, 0.85f, 1f, a * 0.9f);
            GUI.Label(new Rect(r.x, r.y + 102f * s, r.width, 22f * s),
                      "화살표를 따라가라 — 하나씩 끊을 때마다 부담도 준다", center);
            GUI.color = Color.white;
        }

        void DrawBaseArrow(float s)
        {
            if (director.Travelling) return;      // 항행 중엔 화물·도킹 표시가 무의미하다
            if (director.State != GameState.Field || cam == null) return;
            if (director.CargoCount <= 0) return;      // 실은 게 없으면 굳이 안 가도 된다

            Vector3 sp = cam.WorldToScreenPoint(Vector3.zero);
            sp.y = Screen.height - sp.y;

            float margin = 64f * s;
            if (sp.z > 0f && sp.x > margin && sp.x < Screen.width - margin
                          && sp.y > margin && sp.y < Screen.height - margin) return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(sp.x, sp.y) - center;
            if (sp.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();

            float rx = Screen.width * 0.5f - margin;
            float ry = Screen.height * 0.5f - margin;
            Vector2 at = center + dir * Mathf.Min(rx / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                                                  ry / Mathf.Max(0.001f, Mathf.Abs(dir.y)));

            // 가득 찼으면 더 강하게 재촉한다
            var c = director.CargoRatio > 0.85f
                ? new Color(1f, 0.55f, 0.4f, 0.55f + 0.45f * Mathf.Sin(Time.time * 7f))
                : new Color(0.45f, 0.95f, 0.8f, 0.6f);

            float len = 22f * s, thick = 5f * s;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            ArrowBar(at, ang + 150f, len, thick, c);
            ArrowBar(at, ang - 150f, len, thick, c);

            GUI.color = c;
            GUI.Label(new Rect(at.x - 50f * s, at.y + 14f * s, 100f * s, 20f * s), "모선", centerSmall);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 화면 밖 보스를 가리키는 화살표.
        ///
        ///    보스를 맵 한가운데에 고정했으므로, 플레이어가 가장자리에 있으면 안 보인다.
        ///    보스는 **찾아가는 것**이라 어디 있는지는 알려줘야 한다 —
        ///    안 알려주면 "보스가 안 나왔다"가 된다 (2026-08-22 피드백).
        ///
        ///    화면 안에 있으면 화살표를 안 그린다. 이미 보이는 걸 가리키면 지저분하다.
        /// </summary>
        void DrawBossArrow(float s)
        {
            if (director.Phase == FloorPhase.Collecting) return;
            if (cam == null || director.field == null) return;

            Vector3 world = director.field.BossCenter;
            Vector3 sp = cam.WorldToScreenPoint(world);
            sp.y = Screen.height - sp.y;      // GUI는 y가 뒤집혀 있다

            float margin = 64f * s;
            bool onScreen = sp.z > 0f
                         && sp.x > margin && sp.x < Screen.width - margin
                         && sp.y > margin && sp.y < Screen.height - margin;

            float dist = Vector2.Distance(ship.transform.position, world);

            if (onScreen)
            {
                // 화면 안이면 화살표 대신 **표식**만 — 어느 게 보스인지 알려준다
                if (director.Phase == FloorPhase.BossActive)
                {
                    GUI.color = new Color(1f, 0.55f, 0.4f, 0.75f);
                    GUI.Label(new Rect(sp.x - 60f * s, sp.y - 54f * s, 120f * s, 20f * s), "▼ 보스", centerSmall);
                    GUI.color = Color.white;
                }
                return;
            }

            // 화면 가장자리로 끌어와 그린다
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(sp.x, sp.y) - center;
            if (sp.z < 0f) dir = -dir;                     // 카메라 뒤쪽이면 반대로
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();

            float rx = Screen.width * 0.5f - margin;
            float ry = Screen.height * 0.5f - margin;
            float scale = Mathf.Min(rx / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                                    ry / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
            Vector2 at = center + dir * scale;

            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
            var c = new Color(1f, 0.5f, 0.35f, pulse);

            // 삼각형 대신 회전한 막대 두 개로 화살촉을 만든다 (OnGUI엔 도형이 없다)
            float len = 26f * s, thick = 6f * s;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            ArrowBar(at, ang + 150f, len, thick, c);
            ArrowBar(at, ang - 150f, len, thick, c);
            ArrowBar(at - dir * len * 0.7f, ang, len * 1.3f, thick * 0.6f, c);

            // 거리 — 얼마나 가야 하는지
            GUI.color = c;
            GUI.Label(new Rect(at.x - 60f * s, at.y + 16f * s, 120f * s, 20f * s),
                $"보스 {dist:0}m", centerSmall);
            GUI.color = Color.white;
        }

        void ArrowBar(Vector2 at, float angleDeg, float len, float thick, Color c)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angleDeg, at);
            Box(at.x, at.y - thick * 0.5f, len, thick, c);
            GUI.matrix = m;
        }

        /// <summary>
        /// 🔴 단발성 버프가 켜져 있는 동안 **남은 시간을 크게** 띄운다.
        ///    몇 초짜리 카드를 골랐는데 언제 끝나는지 모르면 쓸 수가 없다 —
        ///    "지금 몰아쳐야 한다"가 보여야 그 카드를 고른 값어치가 생긴다.
        /// </summary>
        void DrawBurstTimer(float s)
        {
            if (director.State != GameState.Field || director.Stats == null) return;

            float left = director.Stats.BurstLeft;
            if (left <= 0f) return;

            string name = director.Stats.BurstName;
            if (string.IsNullOrEmpty(name)) return;

            // 끝나기 직전엔 빠르게 깜빡인다
            float blink = left < 3f ? 0.55f + 0.45f * Mathf.Sin(Time.time * 14f) : 1f;
            var c = new Color(1f, 0.62f, 0.22f, blink);

            float w = 340f * s, h = 40f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.14f, w, h);

            Box(r, new Color(0.18f, 0.10f, 0.03f, 0.85f));
            Frame(r, c, 2f * s);

            // 남은 시간 막대
            Box(r.x, r.yMax - 4f * s, r.width * Mathf.Clamp01(left / 12f), 4f * s, c);

            GUI.color = c;
            GUI.Label(new Rect(r.x, r.y + 4f * s, r.width, 20f * s), name, center);
            GUI.color = new Color(1f, 0.9f, 0.7f, blink);
            GUI.Label(new Rect(r.x, r.y + 20f * s, r.width, 18f * s), $"{left:0.0}초", center);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 보스 등장. **"보스인 줄도 몰랐다"**는 피드백을 받고 만들었다 (2026-08-22).
        ///    웨이브가 끝나자마자 조용히 덩어리 4개가 생기니 알 방법이 없었다.
        /// </summary>
        void DrawBossIntro(float s)
        {
            if (director.Phase != FloorPhase.BossIncoming) return;

            float t = Mathf.Clamp01(director.BossIntroLeft / 3f);
            float a = Mathf.Clamp01(t * 2.2f);

            // 위아래 검은 띠 — 연출 구간이라는 걸 알린다
            float bar = 56f * s * (1f - t * 0.25f);
            Box(0, 0, Screen.width, bar, new Color(0f, 0f, 0f, 0.75f * a));
            Box(0, Screen.height - bar, Screen.width, bar, new Color(0f, 0f, 0f, 0.75f * a));

            var warn = new Color(1f, 0.42f, 0.32f, a);

            GUI.color = warn;
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 26f * s), "경   보", center);
            GUI.Label(new Rect(0, Screen.height * 0.40f, Screen.width, 40f * s),
                director.Stage != null ? director.Stage.boss.displayName : "대형 반응", big);
            GUI.color = new Color(0.9f, 0.93f, 1f, a);
            GUI.Label(new Rect(0, Screen.height * 0.47f, Screen.width, 22f * s),
                $"구역 중앙에 출현 — {director.BossIntroLeft:0.0}", center);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 조합이 열리는 순간을 화면 한가운데에 크게 띄운다.
        ///    조합은 이 게임이 뱀서와 갈리는 지점인데, 팝업 한 줄로 지나가면
        ///    플레이어는 **무엇이 달라졌는지 모른 채** 계속하게 된다.
        /// </summary>
        void DrawComboFlash(float s)
        {
            if (director.comboFlashLeft <= 0f || director.ActiveCombo == null) return;

            var combo = director.ActiveCombo;
            float a = Mathf.Clamp01(director.comboFlashLeft / 0.6f);

            float h = 92f * s;
            float y = Screen.height * 0.30f;

            Box(0, y, Screen.width, h, new Color(combo.color.r * 0.16f, combo.color.g * 0.16f, combo.color.b * 0.16f, 0.88f * a));
            Box(0, y, Screen.width, 2f * s, new Color(combo.color.r, combo.color.g, combo.color.b, a));
            Box(0, y + h - 2f * s, Screen.width, 2f * s, new Color(combo.color.r, combo.color.g, combo.color.b, a));

            GUI.color = new Color(combo.color.r, combo.color.g, combo.color.b, a);
            GUI.Label(new Rect(0, y + 10f * s, Screen.width, 22f * s), "★  계 열  조 합  발 동", center);
            GUI.Label(new Rect(0, y + 30f * s, Screen.width, 34f * s), combo.title, big);
            GUI.color = new Color(0.9f, 0.94f, 1f, a);
            GUI.Label(new Rect(0, y + 66f * s, Screen.width, 22f * s), combo.description, center);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 🔴 봇이 조종 중이면 크게 알린다. 입력이 안 먹는 걸로 오해하면 안 되기 때문이다.
        /// </summary>
        void DrawBotBanner(float s)
        {
            if (!AutoPilot.Engaged) return;

            float h = 28f * s;
            Box(0, 0, Screen.width, h, new Color(0.10f, 0.35f, 0.25f, 0.85f));
            GUI.color = new Color(0.6f, 1f, 0.8f);
            GUI.Label(new Rect(0, 0, Screen.width, h),
                "봇이 조종 중 — B로 되돌린다   (밸런스 시뮬과 같은 코드다)", center);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- 진단 (임시)

        void DrawDiagnostics(float s)
        {
            if (ship == null) return;

            Vector2 ms = SalvageRun.Core.InputReader.MouseScreen;
            float h = 20f * s;
            Box(0, Screen.height - h, Screen.width, h, new Color(0f, 0f, 0f, 0.6f));
            GUI.color = new Color(0.6f, 1f, 0.8f);
            GUI.Label(new Rect(10f * s, Screen.height - h, Screen.width, h),
                $"[진단] 입력 {SalvageRun.Core.InputReader.LastPath} · 커서 {ms.x:0},{ms.y:0} · " +
                $"목표 {ship.AimPoint.x:0.0},{ship.AimPoint.y:0.0} · 함선 {ship.transform.position.x:0.0},{ship.transform.position.y:0.0} · " +
                $"출력 {(ship.ThrottleNow * 100f):0}% · 상태 {director.State} · " +
                $"흔들림 {(Juice.ShakeScale > 0f ? "켜짐" : "꺼짐")}(K) · " +
                $"봇 {(AutoPilot.Engaged ? "조종 중" : "꺼짐")}(B)", small);
            GUI.color = Color.white;
        }
    }
}
