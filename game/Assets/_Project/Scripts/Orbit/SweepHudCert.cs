using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🛰 10-10 공로주 증서 — 관문을 깬 판이 끝나면 결산 전에 (사장님 「받은 줄도 모르겠다 · 모달로 도장 찍게」 · 시안 X5LEtdiKcREtZuhphDt6KT)
    /// 증서(관문 그림 · 항로 · 종목 · 주식 수 · 평가 · 열쇠 +1) → [도장 쾅!] 클릭 · Space → 「지급」 도장 · 잉크 · 소리 · 흔들림 → 주식 종이가 날아감 → [받기 ▸ 결산으로]
    /// 보상은 관문을 깰 때 이미 들어가 있다 — 여기는 받는 순간을 보여 줄 뿐.
    /// </summary>
    public partial class SweepHud
    {
        bool certOpen; float certStampT = -1;
        static Texture2D[] certGate;
        public void TestCert(int stock, int gate) { last.gateStock = stock; last.gateIdx = gate; last.gateAmt = 18400; last.gateShares = 99; last.gateName = SweepSim.GateNames[Mathf.Clamp(gate, 0, SweepSim.GateNames.Length - 1)]; last.gateRoute = SweepSim.Orbits[1].name; certOpen = true; certStampT = -1; flow = 1; }   // 에디터 시험용

        void CertKey()
        {
            if (certStampT < 0) CertStamp();
            else if (Time.unscaledTime - certStampT > 0.5f) CertClose();
        }
        void CertStamp() { certStampT = Time.unscaledTime; OrbitSfx.Play("clank", 1f); OrbitSfx.Play("coin", 0.7f); game.shake = Mathf.Max(game.shake, 0.18f); }
        void CertClose() { certOpen = false; resultAt = Time.time; flyers.Clear(); OrbitSfx.Play("buy", 0.8f); }   // 결산 숫자는 지금부터 올라간다

        void CertWin()
        {
            var R = last; if (R == null || R.gateStock < 0 || R.gateStock >= Market.Defs.Length) { certOpen = false; return; }
            GUI.color = new Color(0, 0, 0, 0.72f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            var r = new Rect(vw / 2 - 210, 22, 420, 460);
            Color gold = new Color(0.77f, 0.59f, 0.16f), ink = new Color(0.2f, 0.16f, 0.12f), red = new Color(0.75f, 0.18f, 0.14f);
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(r.x + 7, r.y + 9, r.width, r.height), white); GUI.color = Color.white;
            GUI.DrawTexture(r, texPaper);
            Frame(new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20), gold, 3);
            Frame(new Rect(r.x + 17, r.y + 17, r.width - 34, r.height - 34), gold, 1);
            GUI.color = gold;
            foreach (var p in new[] { new Vector2(r.x + 17, r.y + 17), new Vector2(r.xMax - 17, r.y + 17), new Vector2(r.x + 17, r.yMax - 17), new Vector2(r.xMax - 17, r.yMax - 17) })
                GUI.DrawTexture(new Rect(p.x - 7, p.y - 7, 14, 14), texDisc);
            GUI.color = Color.white;
            var hc = new GUIStyle(paperHead) { alignment = TextAnchor.UpperCenter };
            paperHead.fontSize = 22; hc.fontSize = 22;
            Lbl(new Rect(r.x, r.y + 30, r.width, 32), Fit(Loc.T("궤도 항로 개통 공로주"), 22, r.width - 60, hc, 14), hc);
            var sc = new GUIStyle(paperSmall) { alignment = TextAnchor.UpperCenter }; sc.fontSize = 11;
            Lbl(new Rect(r.x, r.y + 62, r.width, 18), Loc.T("주권 · 무기명 · 양도 가능"), sc);
            GUI.DrawTexture(new Rect(r.x + 36, r.y + 84, r.width - 72, 2), texInk);
            // 관문 그림 + 사연
            if (certGate == null) { certGate = new Texture2D[11]; for (int i = 0; i < 11; i++) certGate[i] = Resources.Load<Texture2D>("gate/gate_" + i); }
            var gt = certGate[Mathf.Clamp(R.gateIdx, 0, 10)];
            if (gt != null) GUI.DrawTexture(new Rect(r.x + 36, r.y + 98, 92, 92), gt, ScaleMode.ScaleToFit);
            paperBody.fontSize = 16; Lbl(new Rect(r.x + 140, r.y + 104, r.width - 170, 24), Fit((R.gateName ?? "") + Loc.T(" 붕괴"), 16, r.width - 170, paperBody, 11), paperBody);
            paperSmall.fontSize = 11;
            var ws = new GUIStyle(paperSmall) { wordWrap = true };
            Lbl(new Rect(r.x + 140, r.y + 132, r.width - 170, 50), (R.gateRoute ?? "") + Loc.T(" 항로를 민간 청소선이 열었다. 이 공로로 아래 주식을 지급함."), ws);
            // 주식 칸
            var bx = new Rect(r.x + 36, r.y + 204, r.width - 72, 92);
            Frame(bx, ink, 2);
            string[] k = { Loc.T("종목"), Loc.T("수량"), Loc.T("평가") };
            string[] v = { Market.Defs[R.gateStock].name, R.gateShares.ToString(R.gateShares < 10 ? "0.#" : "#,0") + Loc.T("주"), "<color=#2f7a46>" + KNum.Fmt(R.gateAmt) + "</color>" };
            for (int i = 0; i < 3; i++)
            {
                paperSmall.fontSize = 12; Lbl(new Rect(bx.x + 12, bx.y + 8 + i * 26, 60, 20), k[i], paperSmall);
                paperBody.fontSize = 16; Lbl(new Rect(bx.x + 76, bx.y + 5 + i * 26, bx.width - 180, 24), Fit(v[i], 16, bx.width - 180, paperBody, 10), paperBody);
            }
            Lbl(new Rect(bx.xMax - 104, bx.y + 31, 96, 24), Loc.T("<color=#6e46aa>+ 열쇠 1개</color>"), paperBody);
            paperSmall.fontSize = 10;
            Lbl(new Rect(r.x + 36, r.y + 304, r.width - 72, 16), Loc.T("발행 · 궤도청 · 케슬러 증권 대행"), paperSmall);
            GUI.DrawTexture(new Rect(r.x + 190, r.y + 410, r.width - 226, 1), texInk);
            Lbl(new Rect(r.x + 190, r.y + 414, r.width - 226, 16), Loc.T("수령인 · 주식회사 궤도 청소부 (") + sim.M.company + Loc.T("대)"), paperSmall);
            // 도장
            var c = new Vector2(r.xMax - 100, r.y + 362);
            if (certStampT < 0)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5);
                GUI.color = new Color(red.r, red.g, red.b, 0.35f + 0.35f * pulse); GUI.DrawTexture(new Rect(c.x - 40, c.y - 40, 80, 80), texRing); GUI.color = Color.white;
                var sc2 = new GUIStyle(paperBody) { alignment = TextAnchor.MiddleCenter }; sc2.fontSize = 13;
                Lbl(new Rect(c.x - 40, c.y - 12, 80, 24), Loc.T("<color=#b8432f>도장</color>"), sc2);
                var b = new Rect(vw / 2 - 120, r.yMax + 14, 240, 46);
                GUI.color = new Color(0.47f, 0.12f, 0.1f); GUI.DrawTexture(b, white); GUI.color = Color.white; Frame(b, new Color(1f, 0.82f, 0.47f, 0.6f + 0.4f * pulse), 2);
                if (Bt(b, Fit(Loc.T("<b>도장 쾅!</b>  <color=#e8c9a0>클릭 · Space</color>"), 17, b.width - 16, center, 11), center) || (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))) CertStamp();
                return;
            }
            float t = Time.unscaledTime - certStampT, k2 = Mathf.Clamp01(t / 0.16f), s = Mathf.Lerp(2.4f, 1f, k2);
            {   // 「지급」 도장 — 크게 떨어져 기울어진 채 찍힌다 (계약서 도장과 같은 방식)
                var m = GUI.matrix;
                GUI.matrix = m * Matrix4x4.TRS(c, Quaternion.Euler(0, 0, -14), new Vector3(s, s, 1)) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);
                GUI.color = new Color(red.r, red.g, red.b, 0.9f * k2);
                GUI.DrawTexture(new Rect(c.x - 50, c.y - 50, 100, 100), texRing);
                GUI.DrawTexture(new Rect(c.x - 40, c.y - 40, 80, 80), texRing);
                var st = new GUIStyle(paperHead) { alignment = TextAnchor.MiddleCenter }; st.fontSize = 26;
                st.normal.textColor = new Color(red.r, red.g, red.b, 0.95f * k2);
                GUI.color = Color.white;
                Lbl(new Rect(c.x - 50, c.y - 26, 100, 34), Loc.T("지급"), st);
                st.fontSize = 11; Lbl(new Rect(c.x - 50, c.y + 8, 100, 18), Loc.T("궤도청"), st);
                GUI.matrix = m;
            }
            if (t > 0.16f && t < 0.6f)
            {   // 잉크 튐
                float a = 1 - (t - 0.16f) / 0.44f;
                GUI.color = new Color(red.r, red.g, red.b, a);
                for (int i = 0; i < 12; i++)
                {
                    float an = i / 12f * Mathf.PI * 2, r0 = 58 + (t - 0.16f) * 60, len = 10 + (i % 3) * 6;
                    var p = c + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * r0;
                    GUI.DrawTexture(new Rect(p.x - 2, p.y - 2, 4 + Mathf.Abs(Mathf.Cos(an)) * len, 4 + Mathf.Abs(Mathf.Sin(an)) * len), white);
                }
                GUI.color = Color.white;
            }
            // 주식 종이가 오른쪽 위로 날아간다
            for (int i = 0; i < 5; i++)
            {
                float u = Mathf.Clamp01((t - 0.35f - i * 0.08f) / 0.7f); if (u <= 0 || u >= 1) continue;
                var from = new Vector2(bx.center.x, bx.center.y); var to = new Vector2(vw - 60, 40);
                var p = Vector2.Lerp(from, to, u * u) - new Vector2(0, Mathf.Sin(u * Mathf.PI) * 60);
                var pr = new Rect(p.x - 18, p.y - 11, 36, 22);
                GUI.color = new Color(1, 1, 1, 1 - u * 0.6f); GUI.DrawTexture(pr, texPaper); Frame(pr, gold, 2);
                var ps = new GUIStyle(paperBody) { alignment = TextAnchor.MiddleCenter }; ps.fontSize = 11; Lbl(pr, Loc.T("주"), ps);
                GUI.color = Color.white;
            }
            if (t > 0.5f)
            {
                var b = new Rect(vw / 2 - 120, r.yMax + 14, 240, 46);
                GUI.color = new Color(0.12f, 0.28f, 0.18f); GUI.DrawTexture(b, white); GUI.color = Color.white; Frame(b, new Color(0.63f, 0.94f, 0.75f), 2);
                if (Bt(b, Fit(Loc.T("<b>받기 ▸ 결산으로</b>"), 17, b.width - 16, center, 11), center)) CertClose();
            }
        }
    }
}
