using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🚪 로비 — 켜면 여기부터: 이어하기 · 새로 시작 · 설정 · 끝내기 (09-24 사장님 19번)
    // 시안 https://claude.ai/artifact/CthkM2c8KDnFcaxG8m5zJt — 왼쪽에 이름과 단추, 오른쪽은 창밖 궤도가 그대로 돈다
    public partial class SweepHud
    {
        public bool lobby = true;
        float lobbyT, wipeArmT;

        bool HasProgress => sim.M.totalRuns > 0 || sim.S.runs > 0 || sim.M.company > 1;

        void LobbyContinue() { lobby = false; flow = 2; OrbitSfx.Play("launch", 0.6f); }

        void Lobby()
        {
            float dt = Time.unscaledDeltaTime; lobbyT += dt; wipeArmT -= dt;
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
                if (Btn("새로 시작", wipeArmT > 0 ? "<color=#ff9b8f>한 번 더 누르면 지금 회사가 지워진다</color>" : "처음부터 — 한 번 더 묻는다", false))
                {
                    if (wipeArmT > 0) { game.WipeAll(); wipeArmT = 0; LobbyContinue(); }
                    else { wipeArmT = 3f; OrbitSfx.Play("tick", 0.7f); }
                }
            }
            else if (Btn("시작하기", "빚내서 산 청소선 한 척 — 궤도를 치운다", true)) LobbyContinue();
            if (Btn("설정", null, false)) { settingsOpen = true; OrbitSfx.Play("tick", 0.6f); }
            if (Btn("끝내기", null, false)) { game.Save(); Application.Quit(); }
            GUI.Label(new Rect(x, RefH - 34, 500, 18), "<size=11><color=#5f6878>v0.9 · 오늘도 궤도는 깨끗합니다 · Space = 이어하기</color></size>", label);
            GUI.color = Color.white;
        }
    }
}
