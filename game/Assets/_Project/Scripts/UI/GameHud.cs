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

        /// <summary>지금 열려 있는 무기. 준비 화면이 목록으로 그린다.</summary>
        readonly List<WeaponKind> ownedWeapons = new List<WeaponKind>();

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
            if (director.State == GameState.Field)
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
                case GameState.Ready: DrawReady(s); break;
                case GameState.Field: DrawField(s); break;
                case GameState.Result: DrawResult(s); break;
            }
            DrawDiagnostics(s);
            DrawBotBanner(s);
            DrawComboFlash(s);
            DrawBossIntro(s);
            DrawBurstTimer(s);
            DrawBossArrow(s);
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

            Tuning.HunterRatio     = Row(s, r.x, ref y, w, "로봇 비율",  Tuning.HunterRatio,     0f,    0.6f);
            Tuning.ShipFuelMul     = Row(s, r.x, ref y, w, "배 연료",    Tuning.ShipFuelMul,     0.3f,  4f);
            Tuning.FuelDrainMul    = Row(s, r.x, ref y, w, "연료 감소",  Tuning.FuelDrainMul,    0f,    3f);
            Tuning.JunkSize        = Row(s, r.x, ref y, w, "쓰레기 크기", Tuning.JunkSize,        0.5f,  3f);
            Tuning.JunkDensity     = Row(s, r.x, ref y, w, "쓰레기 밀도", Tuning.JunkDensity,     0.3f,  3f);
            Tuning.JunkSpeedMul    = Row(s, r.x, ref y, w, "쓰레기 속도", Tuning.JunkSpeedMul,    0.2f,  3f);
            Tuning.TowWeightMul    = Row(s, r.x, ref y, w, "견인 무게",   Tuning.TowWeightMul,    0.2f,  4f);
            Tuning.IncomingCostMul = Row(s, r.x, ref y, w, "충돌 손실",   Tuning.IncomingCostMul, 0f,    3f);
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
        /// <summary>
        /// 🔴 **글이 상자를 넘으면 글자 크기를 줄여서 넣는다.**
        ///
        ///    (2026-08-22 피드백: *"텍스트 잘리는 부분이 좀 많음"*)
        ///
        ///    `GUI.Label`은 상자를 넘는 글을 **말없이 잘라 버린다.** 그래서 카드 제목이
        ///    길거나, 창이 좁거나, 한글이 라틴 문자보다 넓게 잡히면 뒷부분이 사라진다.
        ///    화면에서는 "글이 짧다"로 보여서 **버그로 안 보이는 게 더 나쁘다.**
        ///
        ///    고정 크기로 그리고 잘리는 대신, **들어갈 때까지 줄인다.**
        ///    줄여도 안 되면 그때는 잘리지만, 그건 상자 자체가 잘못 잡힌 것이다.
        ///
        ///    ⚠️ `CalcHeight`는 `wordWrap`이 켜져 있어야 뜻이 있다. 꺼져 있으면
        ///       한 줄 높이만 돌려주므로 **가로 넘침을 못 잡는다** — 그래서 여기서 켠다.
        /// </summary>
        void Fit(Rect r, string text, GUIStyle style, float minRatio = 0.62f)
        {
            if (string.IsNullOrEmpty(text)) return;

            int want = style.fontSize;
            int floor = Mathf.Max(8, Mathf.RoundToInt(want * minRatio));
            bool wrapWas = style.wordWrap;
            style.wordWrap = true;

            var gc = new GUIContent(text);
            while (style.fontSize > floor && style.CalcHeight(gc, r.width) > r.height)
                style.fontSize--;

            GUI.Label(r, gc, style);

            style.fontSize = want;
            style.wordWrap = wrapWas;
        }

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
            // 🔴 버튼 글씨는 **가로로** 넘친다. 좌우 여백을 빼고 재야 잘리는 걸 잡는다
            float pad = 8f * s;
            Fit(new Rect(r.x + pad, r.y + (r.height - 20f * s) * 0.5f, r.width - pad * 2f, 20f * s),
                text, center);
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
        ///    예전엔 준비 화면 하나에 임무 설명 5줄 · 재화 · 정비소 · 조작 · 우주선 고르기 ·
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
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 24f * s),
                "우주 쓰레기를 쓸어담으며 버틴다", center);
            GUI.color = Color.white;
            y += 50f * s;

            float bw = 280f * s, bh = 42f * s;
            bool hasSave = false;      // ⬜ 이어하기 저장은 아직 없다 — 붙일지는 출시 뒤에 판단

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

            // ⬜ **조작 방식 고르는 버튼을 뺐다** (2026-08-27 사장님: *"마우스는 없애고
            //    키보드로만"*). 고를 것이 없는데 버튼을 두면 **화면이 거짓말을 한다.**
            bool kb = true;

            GUI.color = TextDim;
            GUI.Label(new Rect(0, y, Screen.width, 20f * s),
                kb ? "WASD 이동 · Shift 대시 · Q 화물 버리기 · E 출발"
                   : "WASD 이동 · Shift 대시 · Q 화물 버리기 · E 출발", center);
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

            // 🔴 **첫 화면이 장르를 말한다** (rev.12).
            //
            //    rev.11에서는 임무 브리핑이었다 — 기지가 좌표까지 가는 이야기였고,
            //    규칙이 전부 거기서 나왔다. 그 구조를 걷어냈으니 문구도 같이 걷는다.
            //    지금 여기서 알려줘야 하는 건 딱 셋이다:
            //    **몰려온다 / 줍는다 / 닿으면 닳는다.**
            GUI.color = Warm;
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 22f * s),
                "연료가 다 닳기 전에 최대한 쓸어담는다", center);
            y += 26f * s;

            GUI.color = TextDim;
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 22f * s),
                "무기는 알아서 조준하고 쏜다 — 당신이 정하는 건 어디에 서 있을지다", center);
            y += 22f * s;
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 22f * s),
                "재화 위에서 Space — 한 번에 하나씩 배 뒤에 매달린다", center);
            y += 22f * s;
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 22f * s),
                "많이 달수록 느려진다 — 무엇을 싣고 갈지가 이 게임의 전부다", center);
            y += 22f * s;
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 22f * s),
                "WASD = 이동"
                + " · Shift = 대시 · Space = 줍기 · T = 정비소", center);
            GUI.color = Color.white;
            y += 34f * s;

            // ---- 재화 · 정비소 ----
            var md = MetaSave.Data;
            float bw = 340f * s;

            MetaSave.FillOwnedWeapons(content,
                ownedWeapons, config != null ? config.startingWeapon : WeaponKind.Harpoon);

            // 🔴 재화는 색을 각자 준다 — 어느 게 귀한 건지 색으로 배우게 한다.
            //    6종이 되면서(2026-08-26) 한 줄에 셋씩 **두 줄**로 깐다 —
            //    여섯을 한 줄에 밀어 넣으면 글씨가 줄어 이름이 안 읽힌다.
            float chipW = bw / 3f;
            for (int i = 0; i < Mats.Count; i++)
            {
                var m = (MatKind)i;
                float mx = cx - bw * 0.5f + chipW * (i % 3);
                float myy = y + (i / 3) * 22f * s;
                DrawMatChip(mx, myy, chipW, s, m, md.Mat(m));
            }
            y += 22f * s * Mathf.CeilToInt(Mats.Count / 3f) + 4f * s;

            if (Btn(new Rect(cx - bw * 0.5f, y, bw, 34f * s), "정비소 — 우주선 · 영구 강화  [T]", s, true, Warm))
                tech?.Toggle();
            y += 38f * s;

            // ⬜ 조작 방식 선택을 뺐다 (2026-08-27) — 키보드뿐이다
            bool kb = true;

            GUI.color = TextDim;
            GUI.Label(new Rect(cx - bw * 0.5f, y, bw, 18f * s),
                "WASD · 방향키로 이동 · 조준은 자동",
                centerSmall);
            GUI.color = Color.white;
            y += 26f * s;

            // ---- 지금 붙어 있는 무기 ----
            //
            // 🔴 **여기서 고르지 않는다.** 무기는 테크트리에서 열고,
            //    연 것은 **전부 배에 붙는다** (2026-08-26). 여기서는 목록만 보여준다.
            {
                float sw = Mathf.Min(340f * s, Screen.width * 0.86f);

                GUI.color = TextDim;
                Fit(new Rect(cx - sw * 0.5f, y, sw, 18f * s),
                    $"장착된 무기 {ownedWeapons.Count}개", centerSmall);
                GUI.color = Color.white;
                y += 20f * s;

                for (int i = 0; i < ownedWeapons.Count; i++)
                {
                    var wdef = content.Weapon(ownedWeapons[i]);
                    var wc = wdef != null ? wdef.color : Accent;
                    var wr = new Rect(cx - sw * 0.5f, y, sw, 24f * s);

                    Box(wr, new Color(wc.r * 0.20f, wc.g * 0.20f, wc.b * 0.20f, 0.92f));
                    Frame(wr, new Color(wc.r, wc.g, wc.b, 0.5f), 1.2f * s);

                    GUI.color = wc;
                    Fit(new Rect(wr.x + 8f * s, wr.y + 3f * s, wr.width - 16f * s, 18f * s),
                        Weapons.Name(ownedWeapons[i]), center);
                    GUI.color = Color.white;
                    y += 26f * s;
                }

                GUI.color = TextDim;
                Fit(new Rect(cx - sw * 0.5f, y, sw, 18f * s),
                    "무기는 정비소 테크트리에서 연다 — 열면 계속 붙는다  [T]", centerSmall);
                GUI.color = Color.white;
                y += 24f * s;
            }

            // ---- 구역 ----
            //
            // 🔴 **구역은 재화로 산다** (2026-08-26 · Space Rock Breaker 방향).
            //    잠긴 구역은 회색이 아니라 **값이 적힌 버튼**이다 — 눌러서 연다.
            //    "언제 열리지?"가 아니라 **"얼마 모으면 되지?"**가 되어야
            //    판을 한 번 더 도는 이유가 화면에 적혀 있게 된다.
            SectionLabel(ref y, s, "구역 — 재화로 연다");

            float mw = 340f * s, mh = 34f * s;

            for (int i = 0; i < content.StageCount; i++)
            {
                var st = content.Stage(i);
                var r = new Rect(cx - mw * 0.5f, y, mw, mh);

                if (MetaSave.StageUnlocked(content, i))
                {
                    if (Btn(r, $"{st.displayName}   ·   난이도 {st.rank}", s, true))
                        director.StartRun(i);
                }
                else
                {
                    bool can = MetaSave.CanUnlockStage(content, i, out string why);

                    // 앞 구역도 안 열렸으면 값도 안 보여준다 — 한 칸 앞만 보이게
                    string t = why == "앞 구역 먼저"
                        ? "???"
                        : $"{st.displayName} 열기   ·   {StageCostText(st)}";

                    if (Btn(r, t, s, can, can ? Warm : TextDim) && can)
                        MetaSave.UnlockStage(content, i);
                }
                y += mh + 5f * s;
            }
        }

        static string StageCostText(StageDef st)
        {
            string t = "";
            if (st.unlockScrap > 0) t += $"{Mats.Name(MatKind.Scrap)} {st.unlockScrap}";
            if (st.unlockCircuit > 0)
                t += (t.Length > 0 ? " · " : "") + $"{Mats.Name(MatKind.Circuit)} {st.unlockCircuit}";
            if (st.unlockCore > 0)
                t += (t.Length > 0 ? " · " : "") + $"{Mats.Name(MatKind.Core)} {st.unlockCore}";
            return t;
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
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 20f * s), text, center);
            GUI.color = Color.white;
            y += 22f * s;

            // 얇은 구분선 — 구역이 나뉘어 보인다
            Box(Screen.width * 0.30f, y - 6f * s, Screen.width * 0.40f, 1f, new Color(Edge.r, Edge.g, Edge.b, 0.35f));
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

            // 🔴 **남은 시간을 초로 같이 쓴다** (2026-08-23 — 연료가 타이머가 됐다).
            //    바는 "얼마나 남았나"를 어림으로 보여주지만 **얼마나 급한지**는 안 알려준다.
            //    40초 남은 것과 4분 남은 것은 완전히 다른 판단인데 바로는 구분이 안 된다.
            //    ⚠️ 감소율이 1이 아니게 되면서(2026-08-26: 2.5) **숫자 = 초가 아니다.**
            //       그래서 여기서 나눠서 초로 바꿔 쓴다 — 플레이어가 암산할 일이 아니다.
            float left = director.Config != null && director.Config.idleFuelPerSecond > 0.01f
                ? ship.Fuel / (director.Config.idleFuelPerSecond * Tuning.FuelDrainMul)
                : 0f;

            string clock = Tuning.FuelDrainMul < 0.01f
                ? "정지"
                : $"{Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";

            GUI.color = fuel01 > 0.15f ? Color.white
                      : new Color(1f, 0.5f, 0.45f, 0.65f + 0.35f * Mathf.Sin(Time.time * 8f));
            GUI.Label(new Rect(pad, pad + barH + 2f * s, barW * 2f, 22f * s),
                $"연료 {ship.Fuel:0} / {ship.FuelMax:0}   ·   남은 시간 {clock}", small);
            GUI.color = Color.white;

            // 🔴 **끌고 있는 짐** (2026-08-26 — 경험치 바가 있던 자리).
            //    레벨업이 없어졌으므로 여기 있어야 하는 건 **지금 얼마나 무거운가**다.
            //    꼬리를 보면 개수는 알지만 **얼마나 느려졌는지**는 숫자로 봐야 안다.
            float xy = pad + barH + 24f * s;
            float slow = (1f - director.TowWeightMul) * 100f;

            Box(pad, xy, barW, 10f * s, new Color(0f, 0f, 0f, 0.5f));
            // 🔴 **칸이 곧 한계다.** 몇 칸 중 몇 개인지가 보여야 "하나 더?"가 계산이 된다
            int cap = director.MaxTow;
            float fill = cap <= 0 ? 0f : director.TowedCount / (float)cap;
            bool heavy = director.TowedCount >= cap;

            Box(pad, xy, barW * Mathf.Clamp01(fill), 10f * s,
                heavy ? new Color(1f, 0.55f, 0.4f) : new Color(1f, 0.85f, 0.45f));

            GUI.color = heavy ? new Color(1f, 0.7f, 0.55f) : Color.white;
            GUI.Label(new Rect(pad, xy + 10f * s, barW * 2f, 22f * s),
                director.TowedCount > 0
                    ? $"짐 {director.TowedCount}/{cap}   ·   속도 -{slow:0}%"
                      + (heavy ? "   ·   꽉 참 — 주우면 앞엣것이 밀려난다" : "")
                    : $"짐 0/{cap}", small);
            GUI.color = Color.white;

            // 🔴 **지금 무엇을 주울 수 있는지 보여준다** (2026-08-26).
            //    "왜 안 주워지지?"는 이 게임에서 나올 수 있는 최악의 질문이다 —
            //    조작을 바꿨으면 그 조작이 화면에 상주해야 한다.
            float cy = xy + 30f * s;
            var pick = director.PickTarget;

            if (pick != null)
            {
                var pc = Mats.ColorOf(pick.mat);
                Box(pad, cy, 12f * s, 12f * s, pc);

                GUI.color = pc;
                GUI.Label(new Rect(pad + 18f * s, cy - 3f * s, barW * 2f, 20f * s),
                    $"Space = {Mats.Name(pick.mat)} 줍기", small);
            }
            else
            {
                Box(pad, cy, 12f * s, 12f * s, new Color(0.32f, 0.35f, 0.42f));

                GUI.color = TextDim;
                GUI.Label(new Rect(pad + 18f * s, cy - 3f * s, barW * 2f, 20f * s),
                    "재화 위로 가면 테두리가 뜬다 · Space = 줍기", small);
            }
            GUI.color = Color.white;

            // 보유 무기 목록 — 늘어나는 게 눈에 보여야 성장이 체감된다
            // 🔴 **이름을 손으로 적어 두고 있었다.** 목록이 `{"절단날","작살","소용돌이",...}`라
            //    실제 `WeaponKind` 순서(절단날·원반·작살·레이저·방전…)와 **어긋나 있었다** —
            //    즉 이 패널은 **틀린 이름을 보여주고 있었고**, 앞 5칸만 보여줬다.
            //    2026-08-23 무기를 5종으로 줄이다가 발견했다.
            //    이제 `Weapons.Name`에서 뽑는다. 무기를 늘리든 줄이든 다시는 안 어긋난다.
            float wy = xy + 30f * s;
            for (int i = 0; i < Weapons.Count && i < director.Stats.weaponLevel.Length; i++)
            {
                int lv = director.Stats.weaponLevel[i];
                if (lv <= 0) continue;
                GUI.color = new Color(0.7f, 0.95f, 1f);
                Fit(new Rect(pad, wy, 220f * s, 20f * s),
                    $"{Weapons.Name((WeaponKind)i)}  Lv.{lv}", small);
                GUI.color = Color.white;
                wy += 18f * s;
            }

            // ⬜ 조합은 2026-08-26에 껐다 (무기가 쌓이면 전제가 사라진다).
            //    되살릴 때를 위해 그리는 쪽은 남겨 뒀다 — 지금은 `ActiveCombo`가 항상 null이다.
            if (director.ActiveCombo != null)
            {
                GUI.color = director.ActiveCombo.color;
                GUI.Label(new Rect(pad, wy, 320f * s, 20f * s), $"★ {director.ActiveCombo.title}", small);
                GUI.color = new Color(0.75f, 0.75f, 0.8f);
                GUI.Label(new Rect(pad, wy + 17f * s, 340f * s, 20f * s), director.ActiveCombo.description, small);
                GUI.color = Color.white;
                wy += 38f * s;
            }
            // ⬜ "두 무기를 Lv.N까지 올리면 ???" 안내가 있었다.
            //    조합을 끄면서(2026-08-26) 영영 안 열리므로 뺐다 —
            //    안 열리는 조건을 계속 보여주는 건 거짓말이다.


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

            // 🔴 **주운 연료를 보여준다.** 모선도 파편 변환도 없어진 지금
            //    시계를 되감는 방법은 **연료 아이템 하나뿐**이라 눈에 띄어야 한다
            if (director.FuelRecovered > 0.5f)
            {
                GUI.color = new Color(0.55f, 1f, 0.8f);
                GUI.Label(new Rect(rx, pad + 80f * s, 260f * s, 20f * s),
                    $"주운 연료 +{director.FuelRecovered:0}", small);
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
            // 🔴 **정산 화면이지 패배 화면이 아니다** (2026-08-26).
            //    붉은색은 "뭘 잘못했다"로 읽힌다 — 한 바퀴를 무사히 마친 것이므로 따뜻한 색으로.
            var head = Warm;
            GUI.color = head;
            GUI.Label(new Rect(0, y, Screen.width, 44f * s),
                director.Cleared ? "구역 정리 완료" : "귀환 — 정산", big);
            GUI.color = Color.white;
            y += 48f * s;

            GUI.color = TextDim;
            Fit(new Rect(Screen.width * 0.06f, y, Screen.width * 0.88f, 24f * s), director.LastMessage, center);
            GUI.color = Color.white;

            y += 34f * s;

            // ---- 성적표 ----
            float pw = Mathf.Min(560f * s, Screen.width * 0.86f);
            var stat = new Rect(cx - pw * 0.5f, y, pw, 92f * s);
            Box(stat, Panel);
            Frame(stat, new Color(Edge.r, Edge.g, Edge.b, 0.7f), 1.5f * s);

            float col = pw / 4f;
            // ⬜ "생존"이 아니라 "조업 시간"이다 — 살아남은 게 아니라 일한 것이다 (2026-08-26)
            StatCell(stat.x,           stat.y, col, s, "조업",   $"{director.RunTime:0}초");
            StatCell(stat.x + col,     stat.y, col, s, "가져옴", $"{director.BankedCount}");
            StatCell(stat.x + col * 2, stat.y, col, s, "파편",   $"{director.RunCollected}");
            StatCell(stat.x + col * 3, stat.y, col, s, "크레딧", $"{director.RunValue}");

            // 재화 — 이번 런에 주운 것
            var f = director.field;
            if (f != null)
            {
                // 🔴 **가져온 것만 보여준다** (2026-08-26). 6종이 되면서 0을 다 깔면
                //    성적표가 0으로 도배된다 — 번 것이 안 읽힌다.
                float my = stat.y + 52f * s;
                float mw2 = pw / 3f;
                int shown = 0;

                for (int i = 0; i < f.MatsThisRun.Length; i++)
                {
                    if (f.MatsThisRun[i] <= 0) continue;

                    GUI.color = Mats.ColorOf((MatKind)i);
                    GUI.Label(new Rect(stat.x + mw2 * (shown % 3), my + (shown / 3) * 20f * s,
                                       mw2, 22f * s),
                        $"{Mats.Name((MatKind)i)} +{f.MatsThisRun[i]}", centerSmall);
                    GUI.color = Color.white;
                    shown++;
                }
            }
            y += 102f * s;

            // ⬜ 여기 "정비 내역"(고른 카드 목록)이 있었다. 카드 뽑기를 없애면서(2026-08-23)
            //    고른 것이 없으므로 표도 없앴다.

            // ---- 버튼 ----
            float bw = 260f * s, bh = 42f * s;
            if (Btn(new Rect(cx - bw * 0.5f, y, bw, bh), "맵 선택으로", s)) director.BackToReady();
        }

        void StatCell(float x, float y, float w, float s, string label, string value)
        {
            GUI.color = TextDim;
            GUI.Label(new Rect(x, y + 8f * s, w, 18f * s), label, centerSmall);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 24f * s, w, 26f * s), value, center);
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
