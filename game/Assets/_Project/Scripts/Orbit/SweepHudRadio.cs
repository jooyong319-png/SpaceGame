using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 📻 케슬러 금융 「윤 대리」 무전 — 청구서를 갚거나 · 밀리거나 · 파산하면 조종실에서 한 줄 (09-24 사장님 13번 「이야기」)
    // 말투가 이야기다: 처음엔 반말로 떠보다가, 갚을수록 존댓말 — 완납하면 「그동안 고생하셨습니다」
    public partial class SweepHud
    {
        static readonly string[] RadioLines =
        {
            Loc.T("케슬러 금융 윤 대리야. 청소선 할부 시작이다. 첫 청구서는 연료비."),
            Loc.T("어, 갚았네? …다음 것도 부탁해."),
            Loc.T("할부 1회 확인. 생각보다 성실하네."),
            Loc.T("궤도 사용료 들어왔고. 이름 외웠어, 궤도 청소부."),
            Loc.T("정비비까지. 솔직히 여기까지 올 줄은 몰랐어."),
            Loc.T("보험 처리했어요. …아, 존댓말이 나오네."),
            Loc.T("할부 2회 확인했습니다. 청소선 절반은 이제 대표님 거예요."),
            Loc.T("법인세 납부 확인. 어엿한 회사시네요."),
            Loc.T("할부 3회 확인입니다. 위에서 대표님 얘기가 자주 나와요."),
            Loc.T("외행성 면허세 확인했습니다. 목성 너머라니… 조심하세요."),
            Loc.T("할부 4회 확인. 네 조각 중 셋이 대표님 겁니다."),
            Loc.T("심우주 보험까지 처리했습니다. 마지막 한 장 남았어요."),
            Loc.T("…완납 확인했습니다. 그동안 고생하셨습니다, 대표님."),
        };
        static Texture2D radioTex;
        string radioLine; float radioT; bool radioPending;

        public void Radio(string line) { radioLine = line; radioPending = true; }
        public void RadioPaid(int paid)
        {   // 💬 청구서를 갚으면 한 줄 무전 대신 대화 장면 — 조종실로 돌아오면 뜬다 (SweepHudDialogue.cs)
            if (paid > 0 && Scenes.ContainsKey("b" + paid)) { dlgPend = "b" + paid; dlgPendT = 1.2f; if (flow == 2) dlgWindow = true; return; }   // 조종실에서 갚았으면 바로
            Radio(RadioLines[Mathf.Clamp(paid, 0, RadioLines.Length - 1)]);
        }
        public void RadioOverdue(int bill) => Radio(bill < 5 ? Loc.T("기한 지났다? 납부든 대출이든 오늘 정해.") : Loc.T("기한이 지났습니다. 창구는 열려 있어요, 대표님."));
        public void RadioBankrupt() => Radio(Loc.T("…파산 접수했어. 다음 회사도 우리가 빌려줄게. 그게 우리 일이니까."));
        /// <summary>에디터 시험용 — 무전 k 번째 줄을 다 찍힌 채로 (0~12 청구서 · 13 · 14 연체 · 15 파산)</summary>
        public void TestRadio(int k)
        {
            if (k < RadioLines.Length) Radio(RadioLines[k]); else if (k <= RadioLines.Length + 1) RadioOverdue(k == RadioLines.Length ? 1 : 9); else RadioBankrupt();
            radioPending = false; radioT = 0.6f;
        }
        public int TestRadioCount => RadioLines.Length + 3;

        // 🖱 누르기 연습 — 첫 출동 3초 뒤 시간이 멈추고 쭉 누르기(2초) → 연타(5번) → 3 · 2 · 1 재개 (10-06 · 시안 A4mdNF9eHBPTRXKCpUater)
        // 멈춘 동안은 시계 · 연료 · 잔해가 그대로 (sim.Practice) — 내 발사만 움직여서 누르는 법과 결과가 한눈에. 처음 회사 첫 판에 한 번. [건너뛰기]는 5초 뒤
        int coachStep; float coachHold, coachEnd, coachAge, coachT0; double coachLastPress = -9; int coachTapN;
        static readonly string[] CoachLines =
        {
            "",
            Loc.T("잠깐, 시간 멈춰 놨어. 왼쪽 단추를 <color=#ffdf95>쭉 누르고</color> 있어 봐."),
            Loc.T("좋아. 급할 땐 <color=#ffdf95>연타</color>! 누를 때마다 바로 한 발이야."),
            Loc.T("그거야. 연타는 빠른 대신 <color=#ffdf95>연료도 빨리 닳아</color>. 알아서 섞어 써."),
        };
        void CoachDone() { sim.Practice = false; sim.M.flags.Add("coach"); coachStep = 0; }
        bool CoachBox()
        {
            var R = sim.R;
            if (R == null || R.over || R.clean || sim.M.flags.Contains("coach") || sim.M.company > 1 || sim.S.runs > 1 || !manualFire) { if (coachStep != 0) sim.Practice = false; coachStep = 0; return false; }
            if (coachStep == 0) { if (R.t < 3) return false; coachStep = 1; coachHold = 0; coachAge = 0; coachTapN = 0; coachLastPress = R.lastPress; coachT0 = Time.unscaledTime; sim.Practice = true; OrbitSfx.Play("tick", 0.7f); }
            if (Event.current.type == EventType.Repaint)
            {   // OnGUI 는 한 프레임에 여러 번 — 세기는 그릴 때 한 번만
                float dt = Time.unscaledDeltaTime; coachAge += dt;
                bool pressed = R.lastPress != coachLastPress; coachLastPress = R.lastPress;
                if (coachStep == 1) { coachHold = sim.FireHeld ? coachHold + dt : 0; if (coachHold >= 2f) { coachStep = 2; coachAge = 0; coachTapN = 0; OrbitSfx.Play("tick", 0.6f); } }
                else if (coachStep == 2) { if (pressed) { coachTapN++; OrbitSfx.Play("tick", 0.35f, 0.05f, 0.2f); } if (coachTapN >= 5) { coachStep = 3; coachAge = 0; coachEnd = Time.unscaledTime + 2.4f; OrbitSfx.Play("buy", 0.5f); } }
                else if (Time.unscaledTime > coachEnd) { sim.Practice = false; if (Time.unscaledTime > coachEnd + 2.2f) { CoachDone(); return false; } }
            }
            bool frozen = sim.Practice;
            // ⏸ 멈춘 화면 — 살짝 푸르게 · 테두리
            if (frozen)
            {
                GUI.color = new Color(0.24f, 0.47f, 0.7f, 0.12f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
                Frame(new Rect(2, 2, vw - 4, RefH - 4), new Color(0.62f, 0.86f, 1f, 0.35f), 4);
                GUI.color = Color.white;
                Lbl(new Rect(vw / 2 - 160, 52, 320, 20), Loc.T("<size=13><color=#9fdcff>⏸ 시간 멈춤 · 연료 그대로</color></size>"), center);
            }
            float cx = vw / 2;
            if (coachStep == 1 || coachStep == 2)
            {
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f); GUI.DrawTexture(new Rect(cx - 230, 78, 460, 80), white); GUI.color = Color.white;   // 지구 위에서도 읽히게
                Lbl(new Rect(cx - 220, 82, 440, 16), "<size=11><color=#9fdcff>" + (coachStep == 1 ? Loc.T("연습 1 / 2") : Loc.T("연습 2 / 2")) + "</color></size>", center);
                Lbl(new Rect(cx - 220, 98, 440, 32), "<size=24><color=#ffdf95>" + (coachStep == 1 ? Loc.T("쭉 누르기") : Loc.T("연타")) + "</color></size>", center);
                Lbl(new Rect(cx - 220, 132, 440, 20), "<size=13><color=#c9d2de>" + (coachStep == 1 ? Loc.T("왼쪽 단추를 누르고 있기 — 0.5초마다 한 발") : Loc.T("누를 때마다 바로 한 발 · 다섯 번")) + "</color></size>", center);
            }
            if (coachStep == 1)
            {   // 🖱 마우스 그림 — 왼쪽 단추가 빛나고, 누르고 있는 동안 둘레 고리가 2초 동안 찬다
                var mb = new Rect(cx - 30, 200, 60, 90);
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f); GUI.DrawTexture(new Rect(cx - 92, mb.center.y - 92, 184, 184), texDisc);   // 고리 뒤 어두운 원
                GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.9f); GUI.DrawTexture(mb, white); Frame(mb, new Color(0.79f, 0.82f, 0.87f), 2.5f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6);
                GUI.color = sim.FireHeld ? SweepGame.Amber : new Color(0.95f, 0.76f, 0.31f, 0.35f + 0.35f * pulse); GUI.DrawTexture(new Rect(mb.x + 3, mb.y + 3, 26, 36), white);
                GUI.color = new Color(0.79f, 0.82f, 0.87f); GUI.DrawTexture(new Rect(mb.x + 29, mb.y + 3, 2.5f, 36), white); GUI.DrawTexture(new Rect(mb.x + 3, mb.y + 39, 54, 2.5f), white);
                float pr = Mathf.Clamp01(coachHold / 2f); const int Dots = 40;
                for (int i = 0; i < Dots; i++)
                {
                    float ang = -Mathf.PI / 2 + i / (float)Dots * Mathf.PI * 2; var dp = new Vector2(cx + Mathf.Cos(ang) * 72, mb.center.y + Mathf.Sin(ang) * 72);
                    GUI.color = i < pr * Dots ? SweepGame.Amber : new Color(1, 1, 1, 0.22f); GUI.DrawTexture(new Rect(dp.x - 2.5f, dp.y - 2.5f, 5, 5), white);
                }
                GUI.color = Color.white;
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f); GUI.DrawTexture(new Rect(cx - 40, mb.yMax + 34, 80, 18), white); GUI.color = Color.white;
                Lbl(new Rect(cx - 60, mb.yMax + 34, 120, 18), "<size=12><color=#8a93a3>" + coachHold.ToString("0.0") + Loc.T(" / 2초") + "</color></size>", center);
            }
            else if (coachStep == 2)
            {   // 연타 — 칸 다섯
                for (int i = 0; i < 5; i++)
                {
                    if (i == 0) { GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f); GUI.DrawTexture(new Rect(cx - 86, 220, 172, 40), white); GUI.color = Color.white; }
                    var bx = new Rect(cx - 72 + i * 30, 230, 20, 20);
                    if (i < coachTapN) { GUI.color = SweepGame.Amber; GUI.DrawTexture(bx, white); } else Frame(bx, new Color(0.3f, 0.34f, 0.4f), 2);
                }
                GUI.color = Color.white;
            }
            else if (coachStep == 3 && frozen)
            {   // 3 · 2 · 1
                int n = Mathf.Clamp(Mathf.CeilToInt((coachEnd - Time.unscaledTime) / 0.8f), 1, 3);
                float f = 1 - Mathf.Repeat((coachEnd - Time.unscaledTime) / 0.8f, 1);
                Lbl(new Rect(cx - 80, 170, 160, 100), "<size=" + Mathf.RoundToInt(Mathf.Lerp(96, 72, Mathf.Clamp01(f * 4))) + "><color=#ffdf95>" + n + "</color></size>", center);
            }
            if (frozen && coachStep < 3 && Time.unscaledTime - coachT0 > 5f && Bt(new Rect(vw - 120, RefH - 150, 100, 28), Loc.T("<size=12>건너뛰기</size>"), btnOff)) { coachStep = 3; coachAge = 0; coachEnd = Time.unscaledTime; }
            // 📻 무전 — 윤 대리
            if (radioTex == null) radioTex = Resources.Load<Texture2D>("news/yoon");
            float a = Mathf.Clamp01(coachAge / 0.25f) * (coachStep == 3 ? Mathf.Clamp01((coachEnd + 2.2f - Time.unscaledTime) / 0.5f) : 1);
            var r = new Rect(vw / 2 - 240, RefH - 176, 480, 70);
            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.94f * a); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.95f, 0.76f, 0.31f, 0.7f * a), 1);
            var face = new Rect(r.x + 4, r.y + 4, 62, 62);
            GUI.color = new Color(0.1f, 0.14f, 0.2f, a); GUI.DrawTexture(face, white);
            if (radioTex != null) { GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(face.x + 2, face.y + 2, 58, 58), radioTex); }
            GUI.color = new Color(1, 1, 1, a);
            Lbl(new Rect(r.x + 76, r.y + 5, 300, 18), Loc.T("<size=11><color=#ffdf95><b>케슬러 금융 · 윤 대리</b></color>  <color=#5fa37d>● 무전</color></size>"), label);
            string line = CoachLines[Mathf.Clamp(coachStep, 1, 3)]; int nch = Mathf.Clamp(Mathf.FloorToInt(coachAge * 40), 0, line.Length);
            string shown = line.Substring(0, nch);
            int lt = shown.LastIndexOf('<'), gtx = shown.LastIndexOf('>'); if (lt > gtx) shown = shown.Substring(0, lt);   // 찍다 만 태그 조각은 빼고
            if (shown.LastIndexOf("<color") > shown.LastIndexOf("</color>")) shown += "</color>";
            Lbl(new Rect(r.x + 76, r.y + 24, r.width - 86, 44), "<size=13><color=#e8edf3>" + shown + "</color></size>", small);
            GUI.color = Color.white;
            return true;
        }
        void RadioBox()
        {
            if (!sim.M.flags.Contains("g:yoon0") && sim.S.bill == 0 && flow == 2 && sim.R.over && !lobby) { sim.M.flags.Add("g:yoon0"); DlgStart("pro", true); }   // 첫 인사 — 대화 장면으로
            if (radioPending && flow == 2 && sim.R.over && !lobby && radioT <= 0) { radioPending = false; radioT = 7f; OrbitSfx.Play("tick", 0.6f, 0.02f, 0f); }
            if (radioT <= 0 || radioLine == null) return;
            if (flow != 2) { radioT = 0; return; }
            radioT -= Time.unscaledDeltaTime;
            float age = 7f - radioT, a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01(radioT / 0.5f);
            if (radioTex == null) radioTex = Resources.Load<Texture2D>("news/yoon");
            var r = new Rect(ox + 250, sim.S.front1 >= 0 && frontOpen ? 228 : 80, 460, 84);            // 1면 고르기 창이 떠 있으면 그 아래                                   // 창 위쪽 가운데 — 홀로그램 · 파산 단추를 안 가린다
            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.94f * a); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.95f, 0.76f, 0.31f, 0.7f * a), 1);
            var face = new Rect(r.x + 4, r.y + 4, 76, 76);
            GUI.color = new Color(0.1f, 0.14f, 0.2f, a); GUI.DrawTexture(face, white);
            if (radioTex != null) { GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(face.x + 2, face.y + 2, 72, 72), radioTex); }
            // 무전 줄무늬
            GUI.color = new Color(0.44f, 0.81f, 0.59f, 0.06f * a); for (float yy = face.y; yy < face.yMax; yy += 3) GUI.DrawTexture(new Rect(face.x, yy, face.width, 1), white);
            GUI.color = new Color(1, 1, 1, a);
            Lbl(new Rect(r.x + 90, r.y + 6, 360, 18), Loc.T("<size=11><color=#ffdf95><b>케슬러 금융 · 윤 대리</b></color>  <color=#5fa37d>● 무전</color></size>"), label);
            int n = Mathf.Clamp(Mathf.FloorToInt(age * 28), 0, radioLine.Length);           // 타자 치듯
            Lbl(new Rect(r.x + 90, r.y + 26, 360, 54), "<size=13><color=#e8edf3>" + radioLine.Substring(0, n) + "</color></size>", small);
            GUI.color = Color.white;
        }
    }
}
