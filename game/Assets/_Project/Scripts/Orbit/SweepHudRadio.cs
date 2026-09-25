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
            "케슬러 금융 윤 대리야. 청소선 할부 시작이다. 첫 청구서는 연료비.",
            "어, 갚았네? …다음 것도 부탁해.",
            "할부 1회 확인. 생각보다 성실하네.",
            "궤도 사용료 들어왔고. 이름 외웠어, 궤도 청소부.",
            "정비비까지. 솔직히 여기까지 올 줄은 몰랐어.",
            "보험 처리했어요. …아, 존댓말이 나오네.",
            "할부 2회 확인했습니다. 청소선 절반은 이제 대표님 거예요.",
            "법인세 납부 확인. 어엿한 회사시네요.",
            "할부 3회 확인입니다. 위에서 대표님 얘기가 자주 나와요.",
            "외행성 면허세 확인했습니다. 목성 너머라니… 조심하세요.",
            "할부 4회 확인. 네 조각 중 셋이 대표님 겁니다.",
            "심우주 보험까지 처리했습니다. 마지막 한 장 남았어요.",
            "…완납 확인했습니다. 그동안 고생하셨습니다, 대표님.",
        };
        static Texture2D radioTex;
        string radioLine; float radioT; bool radioPending;

        public void Radio(string line) { radioLine = line; radioPending = true; }
        public void RadioPaid(int paid) => Radio(RadioLines[Mathf.Clamp(paid, 0, RadioLines.Length - 1)]);
        public void RadioOverdue(int bill) => Radio(bill < 5 ? "기한 지났다? 납부든 대출이든 오늘 정해." : "기한이 지났습니다. 창구는 열려 있어요, 대표님.");
        public void RadioBankrupt() => Radio("…파산 접수했어. 다음 회사도 우리가 빌려줄게. 그게 우리 일이니까.");

        void RadioBox()
        {
            if (!sim.M.flags.Contains("g:yoon0") && sim.S.bill == 0 && flow == 2 && sim.R.over && !lobby) { sim.M.flags.Add("g:yoon0"); RadioPaid(0); }   // 첫 인사
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
            GUI.Label(new Rect(r.x + 90, r.y + 6, 360, 18), "<size=11><color=#ffdf95><b>케슬러 금융 · 윤 대리</b></color>  <color=#5fa37d>● 무전</color></size>", label);
            int n = Mathf.Clamp(Mathf.FloorToInt(age * 28), 0, radioLine.Length);           // 타자 치듯
            GUI.Label(new Rect(r.x + 90, r.y + 26, 360, 54), "<size=13><color=#e8edf3>" + radioLine.Substring(0, n) + "</color></size>", small);
            GUI.color = Color.white;
        }
    }
}
