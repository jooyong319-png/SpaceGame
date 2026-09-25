using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🚪 로비 — 켜면 여기부터: 이어하기 · 새로 시작 · 설정 · 끝내기 (09-24 사장님 19번)
    // 시안 https://claude.ai/artifact/CthkM2c8KDnFcaxG8m5zJt — 왼쪽에 이름과 단추, 오른쪽은 창밖 궤도가 그대로 돈다
    public partial class SweepHud
    {
        public bool lobby = true;
        float lobbyT; public bool wipeAsk;                                        // 새로 시작 — 확인 창 (09-25 사장님 「동의를 물으면 되지」)

        bool HasProgress => sim.M.totalRuns > 0 || sim.S.runs > 0 || sim.M.company > 1;

        void LobbyContinue() { lobby = false; flow = 2; OrbitSfx.Play("launch", 0.6f); }

        void Lobby()
        {
            float dt = Time.unscaledDeltaTime; lobbyT += dt;
            bool en0 = GUI.enabled; GUI.enabled = en0 && !wipeAsk;
            float a = Mathf.Clamp01(lobbyT / 0.6f);
            // 왼쪽 어두운 판 — 오른쪽으로 갈수록 옅게
            for (int i = 0; i < 24; i++)
            {
                float k = i / 23f;
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.9f * (1 - k * k) * a);
                GUI.DrawTexture(new Rect(i * vw * 0.025f, 0, vw * 0.025f + 1, RefH), white);
            }
            GUI.color = new Color(1, 1, 1, a);
            float x = Mathf.Max(48, ox + 40);
            // 이름
            title.fontSize = 58; title.alignment = TextAnchor.UpperLeft;
            GUI.color = new Color(1f, 0.76f, 0.3f, 0.25f * a); GUI.Label(new Rect(x + 2, 96, 520, 80), "궤도 청소부", title);
            GUI.color = new Color(1, 1, 1, a); GUI.Label(new Rect(x, 94, 520, 80), "<color=#ffdf95>궤도 청소부</color>", title);
            title.alignment = TextAnchor.MiddleCenter; title.fontSize = 34;
            GUI.Label(new Rect(x + 4, 162, 400, 20), "<size=12><color=#8a93a3>O R B I T   S W E E P E R</color></size>", label);

            float y = 240, w = 320, h = 54;
            bool Btn(string main, string sub, bool primary)
            {
                var r = new Rect(x, y, w, h); y += h + 10;
                bool ov = r.Contains(Event.current.mousePosition);
                GUI.color = new Color(0.04f, 0.055f, 0.08f, 0.9f * a); GUI.DrawTexture(r, white);
                Frame(r, primary ? new Color(1f, 0.76f, 0.35f, (ov ? 1f : 0.7f) * a) : new Color(0.3f, 0.34f, 0.42f, (ov ? 1f : 0.7f) * a), ov ? 2 : 1);
                if (ov) { GUI.color = new Color(1f, 0.76f, 0.35f, 0.08f * a); GUI.DrawTexture(r, white); }
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(r.x + 16, r.y + (sub == null ? 13 : 6), r.width - 32, 24), "<size=" + (primary ? 19 : 16) + "><b>" + (primary ? "<color=#ffdf95>" + main + "</color>" : main) + "</b></size>", label);
                if (sub != null) GUI.Label(new Rect(r.x + 16, r.y + 30, r.width - 32, 18), "<size=11><color=#8a93a3>" + sub + "</color></size>", label);
                return GUI.Button(r, GUIContent.none, GUIStyle.none);
            }
            if (HasProgress)
            {
                int min = Mathf.RoundToInt((float)sim.M.playSeconds / 60f);
                string info = "(" + sim.M.company + "대) · 청구서 " + Mathf.Min(sim.S.bill + 1, SweepSim.Bills.Length) + "/" + SweepSim.Bills.Length + " · " + (min >= 60 ? min / 60 + "시간 " + min % 60 + "분" : min + "분");
                if (Btn("이어하기", info, true)) LobbyContinue();
                if (Btn("새로 시작", "처음부터 — 지금 회사와 기록이 지워진다", false)) { wipeAsk = true; OrbitSfx.Play("tick", 0.7f); }
            }
            else if (Btn("시작하기", "빚내서 산 청소선 한 척 — 궤도를 치운다", true)) LobbyContinue();
            if (Btn("설정", null, false)) { settingsOpen = true; OrbitSfx.Play("tick", 0.6f); }
            if (Btn("끝내기", null, false)) { game.Save(); Application.Quit(); }
            GUI.Label(new Rect(x, RefH - 34, 500, 18), "<size=11><color=#5f6878>v0.9 · 오늘도 궤도는 깨끗합니다 · Space = 이어하기</color></size>", label);
            GUI.enabled = en0;
            GUI.color = Color.white;
            if (wipeAsk) WipeConfirm();
        }

        // ⚠️ 새로 시작 확인 창 — 무엇이 지워지는지 보여 주고 고르게 한다
        void WipeConfirm()
        {
            var M = sim.M;
            GUI.color = new Color(0, 0, 0, 0.65f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            var w = new Rect(vw / 2 - 230, RefH / 2 - 130, 460, 250);
            GUI.color = new Color(0.06f, 0.05f, 0.05f, 0.98f); GUI.DrawTexture(w, white); Frame(w, new Color(1f, 0.42f, 0.35f), 2); GUI.color = Color.white;
            GUI.Label(new Rect(w.x, w.y + 18, w.width, 30), "<size=20><b><color=#ffb3a8>정말 처음부터 할까요?</color></b></size>", center);
            GUI.Label(new Rect(w.x + 30, w.y + 60, w.width - 60, 20), "<size=13>아래가 모두 지워지고 되돌릴 수 없다</size>", center);
            string lost = "주식회사 궤도 청소부 (" + M.company + "대) · 청구서 " + Mathf.Min(sim.S.bill + 1, SweepSim.Bills.Length) + "/" + SweepSim.Bills.Length + "\n모은 기사 " + M.news.Count + " · 특종 " + M.scoops + (M.legend > 0 ? " · ★ " + M.legend : "") + (M.bestDepth > 0 ? " · 무한 궤도 " + M.bestDepth + "층" : "") + "\n최고 연쇄 " + M.bestChain;
            GUI.Label(new Rect(w.x + 30, w.y + 88, w.width - 60, 70), "<size=13><color=#c8d0dc>" + lost + "</color></size>", center);
            if (GUI.Button(new Rect(w.x + 30, w.yMax - 62, 190, 40), "<size=14><color=#ffb3a8>지우고 새로 시작</color></size>", btn)) { game.WipeAll(); wipeAsk = false; LobbyContinue(); }
            if (GUI.Button(new Rect(w.xMax - 220, w.yMax - 62, 190, 40), "<size=14>취소</size>", btn)) { wipeAsk = false; OrbitSfx.Play("tick", 0.6f); }
        }
    }
}
