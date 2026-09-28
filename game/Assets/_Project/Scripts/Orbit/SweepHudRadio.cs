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
            if (paid > 0 && Scenes.ContainsKey("b" + paid)) { dlgPend = "b" + paid; dlgPendT = 1.2f; return; }
            Radio(RadioLines[Mathf.Clamp(paid, 0, RadioLines.Length - 1)]);
        }
        public void RadioOverdue(int bill) => Radio(bill < 5 ? Loc.T("기한 지났다? 납부든 대출이든 오늘 정해.") : Loc.T("기한이 지났습니다. 창구는 열려 있어요, 대표님."));
        public void RadioBankrupt() => Radio(Loc.T("…파산 접수했어. 다음 회사도 우리가 빌려줄게. 그게 우리 일이니까."));

        // 📻 첫 판 무전 — 쭉 누르기 → 연타 → 끝. 해 봐야 넘어간다 (09-27 사장님 「처음 하는 사람들이 연타 · 쭉 누르기를 모른다」 · 시안 TxxPY6mLoewcLnW1vmNVBG)
        int coachStep; float coachHold, coachEnd, coachAge; double coachLastPress = -9; readonly System.Collections.Generic.List<float> coachTaps = new System.Collections.Generic.List<float>();
        static readonly string[] CoachLines =
        {
            "",
            Loc.T("왼쪽 단추를 <color=#ffdf95>쭉 누르고</color> 있어 봐. 0.5초마다 한 발씩 나가."),
            Loc.T("좋아. 급할 땐 <color=#ffdf95>연타</color>! 누를 때마다 바로 한 발이야."),
            Loc.T("그거야. 연타는 빠른 대신 <color=#ffdf95>연료도 빨리 닳아</color>. 알아서 섞어 써."),
        };
        bool CoachBox()
        {
            var R = sim.R;
            if (R == null || R.over || R.clean || sim.M.flags.Contains("coach") || sim.M.company > 1 || sim.S.runs >= 3 || !manualFire) { coachStep = 0; return false; }
            if (coachStep == 0) { coachStep = 1; coachHold = 0; coachAge = 0; coachTaps.Clear(); coachLastPress = R.lastPress; }
            if (Event.current.type == EventType.Repaint)
            {   // OnGUI 는 한 프레임에 여러 번 — 세기는 그릴 때 한 번만
                float dt = Time.deltaTime; coachAge += dt;
                if (R.lastPress != coachLastPress) { coachLastPress = R.lastPress; coachTaps.Add(Time.time); }
                coachTaps.RemoveAll(x => Time.time - x > 2f);
                if (coachStep == 1) { coachHold = sim.FireHeld ? coachHold + dt : 0; if (coachHold >= 2f) { coachStep = 2; coachAge = 0; coachTaps.Clear(); OrbitSfx.Play("tick", 0.6f); } }
                else if (coachStep == 2) { if (coachTaps.Count >= 5) { coachStep = 3; coachAge = 0; coachEnd = Time.time + 4f; OrbitSfx.Play("buy", 0.5f); } }
                else if (Time.time > coachEnd) { sim.M.flags.Add("coach"); coachStep = 0; return false; }
            }
            if (radioTex == null) radioTex = Resources.Load<Texture2D>("news/yoon");
            float a = Mathf.Clamp01(coachAge / 0.25f) * (coachStep == 3 ? Mathf.Clamp01((coachEnd - Time.time) / 0.5f) : 1);
            var r = new Rect(vw / 2 - 240, RefH - 176, 480, 70);
            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.94f * a); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.95f, 0.76f, 0.31f, 0.7f * a), 1);
            var face = new Rect(r.x + 4, r.y + 4, 62, 62);
            GUI.color = new Color(0.1f, 0.14f, 0.2f, a); GUI.DrawTexture(face, white);
            if (radioTex != null) { GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(face.x + 2, face.y + 2, 58, 58), radioTex); }
            GUI.color = new Color(1, 1, 1, a);
            Lbl(new Rect(r.x + 76, r.y + 5, 300, 18), Loc.T("<size=11><color=#ffdf95><b>케슬러 금융 · 윤 대리</b></color>  <color=#5fa37d>● 무전</color></size>"), label);
            string meter = coachStep == 1 ? coachHold.ToString("0.0") + Loc.T(" / 2초") : coachStep == 2 ? coachTaps.Count + Loc.T(" / 5번") : "";
            if (meter != "") Lbl(new Rect(r.x + 76, r.y + 5, r.width - 86, 18), "<size=11><color=#8a93a3>" + meter + "</color></size>", cost);
            string line = CoachLines[coachStep]; int n = Mathf.Clamp(Mathf.FloorToInt(coachAge * 40), 0, line.Length);
            string shown = line.Substring(0, n);
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
