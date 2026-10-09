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
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.92f * (1 - Mathf.SmoothStep(0f, 1f, (k - 0.42f) / 0.58f)) * a);   // 이름 · 단추 뒤는 진하게, 오른쪽으로 옅게
                GUI.DrawTexture(new Rect(i * vw * 0.025f, 0, vw * 0.025f + 1, RefH), white);
            }
            // 🚀 10-07 지금 타는 배가 지구 쪽으로 — 오른쪽 아래 빈 곳에 (격납고 도트 그대로 · 움직임 줄이기면 멈춤)
            var shipTx = HullTex(Mathf.Clamp(sim.Ship, 0, HullName.Length - 1));
            if (shipTx != null)
            {
                float bob = reduceMotion ? 0 : Mathf.Sin(lobbyT * 1.3f) * 4, sz = 96;
                var sc = new Vector2(vw * 0.70f, RefH * 0.70f + bob);
                var m0 = GUI.matrix;
                GUI.matrix = m0 * Matrix4x4.TRS(sc, Quaternion.Euler(0, 0, -38), Vector3.one) * Matrix4x4.TRS(-sc, Quaternion.identity, Vector3.one);
                float fl = reduceMotion ? 1 : 0.8f + 0.2f * Mathf.Sin(lobbyT * 23);
                for (int k = 0; k < 4; k++)   // 엔진 불꽃 — 꼬리 쪽으로 가늘어지는 점 넷
                {
                    float fw = (10 - k * 2) * fl, fy2 = sc.y + sz * 0.40f + k * 7 * fl;
                    GUI.color = k == 0 ? new Color(1f, 0.92f, 0.65f, 0.8f * a) : new Color(1f, 0.6f, 0.25f, (0.55f - k * 0.12f) * a);
                    GUI.DrawTexture(new Rect(sc.x - fw / 2, fy2, fw, fw * 1.2f), texDisc);
                }
                GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(sc.x - sz / 2, sc.y - sz / 2, sz, sz), shipTx, ScaleMode.ScaleToFit);
                GUI.matrix = m0;
            }
            GUI.color = new Color(1, 1, 1, a);
            float x = Mathf.Max(48, ox + 40);
            // 이름
            title.fontSize = 58; title.alignment = TextAnchor.UpperLeft;
            while (Loc.En && title.fontSize > 40 && title.CalcSize(new GUIContent(Loc.T("궤도 청소부"))).x > 320) title.fontSize -= 2;   // 10-09 다듬기: 영어 제목이 길어 지구 궤도 그림을 덮었다
            GUI.color = new Color(1f, 0.76f, 0.3f, 0.25f * a); Lbl(new Rect(x + 2, 96, 520, 80), Loc.T("궤도 청소부"), title);
            GUI.color = new Color(1, 1, 1, a); Lbl(new Rect(x, 94, 520, 80), Loc.T("<color=#ffdf95>궤도 청소부</color>"), title);
            title.alignment = TextAnchor.MiddleCenter; title.fontSize = 34;
            if (!Loc.En) Lbl(new Rect(x + 4, 162, 400, 20), "<size=12><color=#8a93a3>O R B I T   S W E E P E R</color></size>", label);   // 🌐 영어면 큰 제목과 같은 말이라 뺀다

            float y = 240, w = 320, h = 54;
            bool Btn(string main, string sub, bool primary)
            {
                float bh = sub == null ? 40 : h;   // 10-09 다듬기: 설명 줄 없는 단추는 낮게 — 아래 반이 비어 보였다
                var r = new Rect(x, y, w, bh); y += bh + 10;
                bool ov = r.Contains(Event.current.mousePosition);
                GUI.color = new Color(0.04f, 0.055f, 0.08f, 0.9f * a); GUI.DrawTexture(r, white);
                Frame(r, primary ? new Color(1f, 0.76f, 0.35f, (ov ? 1f : 0.7f) * a) : new Color(0.3f, 0.34f, 0.42f, (ov ? 1f : 0.7f) * a), ov ? 2 : 1);
                if (ov) { GUI.color = new Color(1f, 0.76f, 0.35f, 0.08f * a); GUI.DrawTexture(r, white); }
                GUI.color = new Color(1, 1, 1, a);
                Lbl(new Rect(r.x + 16, r.y + (sub == null ? 6 : 4), r.width - 32, 28), "<size=" + (primary ? 19 : 16) + "><b>" + (primary ? "<color=#ffdf95>" + main + "</color>" : main) + "</b></size>", label);
                if (sub != null) Lbl(new Rect(r.x + 16, r.y + 30, r.width - 24, 18), "<color=#8a93a3>" + Fit(sub, 11, r.width - 26, label) + "</color>", label);
                return Bt(r, GUIContent.none, GUIStyle.none);
            }
            if (HasProgress)
            {
                int min = Mathf.RoundToInt((float)sim.M.playSeconds / 60f);
                string info = Loc.T("궤도 청소부 ") + sim.M.company + Loc.T("대 · 청구서 ") + Mathf.Min(sim.S.bill + 1, SweepSim.Bills.Length) + "/" + SweepSim.Bills.Length + " · " + (min >= 60 ? min / 60 + Loc.T("시간 ") + min % 60 + Loc.T("분") : min + Loc.T("분"));
                if (Btn(Loc.T("이어하기"), info, true)) LobbyContinue();
                if (Btn(Loc.T("새로 시작"), Loc.T("처음부터 — 지금 회사와 기록 삭제"), false)) { wipeAsk = true; OrbitSfx.Play("tick", 0.7f); }
            }
            else if (Btn(Loc.T("시작하기"), Loc.T("빚내서 산 청소선 한 척으로 궤도 청소"), true)) LobbyContinue();
            if (Btn(Loc.T("설정"), null, false)) { settingsOpen = true; OrbitSfx.Play("tick", 0.6f); }
            if (Btn(Loc.T("끝내기"), null, false)) { game.Save(); Application.Quit(); }
            Lbl(new Rect(x, RefH - 34, 500, 18), "<size=11><color=#5f6878>v" + Application.version + Loc.T(" · 오늘도 궤도는 깨끗합니다 · Space = 이어하기</color></size>"), label);   // 🏷 버전은 빌드 설정(bundleVersion) 한 곳에서 (09-29)
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
            Lbl(new Rect(w.x, w.y + 18, w.width, 30), Loc.T("<size=20><b><color=#ffb3a8>정말 처음부터 할까요?</color></b></size>"), center);
            Lbl(new Rect(w.x + 30, w.y + 60, w.width - 60, 20), Loc.T("<size=13>아래 내용이 모두 삭제되며 되돌릴 수 없음</size>"), center);
            string lost = Loc.T("주식회사 궤도 청소부 (") + M.company + Loc.T("대) · 청구서 ") + Mathf.Min(sim.S.bill + 1, SweepSim.Bills.Length) + "/" + SweepSim.Bills.Length + Loc.T("\n모은 기사 ") + M.news.Count + Loc.T(" · 특종 ") + M.scoops + (M.legend > 0 ? " · ★ " + M.legend : "") + (M.bestDepth > 0 ? Loc.T(" · 무한 궤도 ") + M.bestDepth + Loc.T("층") : "") + Loc.T("\n최고 연쇄 ") + M.bestChain;
            Lbl(new Rect(w.x + 30, w.y + 88, w.width - 60, 70), "<size=13><color=#c8d0dc>" + lost + "</color></size>", center);
            var al = btn.alignment; btn.alignment = TextAnchor.MiddleCenter;
            if (Bt(new Rect(w.x + 30, w.yMax - 62, 190, 40), Loc.T("<size=14><color=#ffb3a8>지우고 새로 시작</color></size>"), btn)) { game.WipeAll(); wipeAsk = false; LobbyContinue(); }
            if (Bt(new Rect(w.xMax - 220, w.yMax - 62, 190, 40), Loc.T("<size=14>취소</size>"), btn)) { wipeAsk = false; OrbitSfx.Play("tick", 0.6f); }
            btn.alignment = al;
        }
    }
}
