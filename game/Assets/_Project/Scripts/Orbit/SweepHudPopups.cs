using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // ✨ 구입 효과 · 못 사는 이유 · 무기 효과판 · 내일 1면 고르기 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── ✨ 칸을 샀을 때 — 퍼지는 고리 · 불꽃 · 해금 칸이면 「해금!」 (09-24)
        struct BuyBurst { public Vector2 p; public Color c; public float t0; public bool big; public string txt; }
        readonly List<BuyBurst> bursts = new List<BuyBurst>();
        void BuyFx(Vector2 p, Color c, bool big, string txt = null) { bursts.Add(new BuyBurst { p = p, c = c, t0 = Time.unscaledTime, big = big, txt = txt ?? Loc.T("해금!") }); if (big) OrbitSfx.Play("launch", 0.35f); }
        // 🚫 못 사는 칸을 누르면 — 그 자리에 이유 한 줄
        Vector2 denyP; float denyT0 = -9; string denyMsg;
        public void Deny(Vector2 p, string msg) { denyP = p; denyT0 = Time.unscaledTime; denyMsg = msg; }
        public string WhyNot(int i)
        {
            var n = SweepSim.Nodes[i]; var st = sim.State(i); int z = SweepSim.Zone[i];
            if (st == NodeSt.Locked && n.id.StartsWith("p_")) return sim.HasGate ? (sim.GateReady ? "🛰 " + sim.GateName + Loc.T(" 관문을 부수면") : Loc.T("🛰 청구서 ") + sim.GateBill + Loc.T("장 뒤 관문")) : Loc.T("항로 먼저");
            if (st == NodeSt.Locked && sim.CapLocked(i)) return Loc.T("행성 한도 — ") + sim.GateName + Loc.T(" 부수면 한 칸 더");
            if (st == NodeSt.Locked && SweepSim.Ring4(n.id) && sim.Lv("p_jup") <= 0) return Loc.T("목성 항로 먼저");
            if (st == NodeSt.Locked && z > sim.ZoneOpen) return SweepSim.ZoneName[z] + Loc.T(" 항로 먼저");
            if (st == NodeSt.Hidden) return Loc.T("앞 칸 먼저");
            if (SweepSim.KeyNodes.Contains(n.id) && sim.S.keys < 1) return Loc.T("열쇠가 없다");
            return Loc.T("돈이 모자라다 · ") + KNum.Fmt(sim.TileCost(i) - sim.S.cash);
        }
        void BuyFxDraw()
        {
            float now = Time.unscaledTime;
            float dt = now - denyT0;
            if (dt < 1.2f && denyMsg != null)
            {
                float a = Mathf.Clamp01((1.2f - dt) / 0.4f), sx = dt < 0.25f ? Mathf.Sin(dt * 60) * 4 * (1 - dt / 0.25f) : 0;
                var dr = new Rect(denyP.x - 110 + sx, denyP.y - 52 - 10 * dt, 220, 24);
                GUI.color = new Color(0.08f, 0.03f, 0.03f, 0.85f * a); GUI.DrawTexture(new Rect(dr.center.x - center.CalcSize(new GUIContent(denyMsg)).x / 2 - 10, dr.y, center.CalcSize(new GUIContent(denyMsg)).x + 20, dr.height), white);
                GUI.color = new Color(1, 1, 1, a); GUI.Label(dr, "<size=13><b><color=#ff9b8f>" + denyMsg + "</color></b></size>", center); GUI.color = Color.white;
            }
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i]; float t = now - b.t0, L = b.big ? 1.1f : 0.6f;
                if (t > L) { bursts.RemoveAt(i); continue; }
                float k = t / L, a = 1 - k, R = (b.big ? 90 : 46) * Mathf.Sqrt(k);
                GUI.color = new Color(b.c.r, b.c.g, b.c.b, a * 0.8f);
                int seg = 28;
                for (int s = 0; s < seg; s++) { float an = s * Mathf.PI * 2 / seg; GUI.DrawTexture(new Rect(b.p.x + Mathf.Cos(an) * R - 2, b.p.y + Mathf.Sin(an) * R - 2, 4, 4), white); }
                for (int s = 0; s < (b.big ? 14 : 8); s++) { float an = s * 2.39996f, d = R * (0.6f + 0.5f * ((s * 37) % 10) / 10f); GUI.color = new Color(1f, 0.93f, 0.7f, a); GUI.DrawTexture(new Rect(b.p.x + Mathf.Cos(an) * d - 1.5f, b.p.y + Mathf.Sin(an) * d - 1.5f, 3, 3), white); }
                if (b.big) { GUI.color = new Color(1, 1, 1, Mathf.Clamp01(a * 1.5f)); GUI.Label(new Rect(b.p.x - 80, b.p.y - 46 - 30 * k, 160, 30), "<size=18><b><color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(b.c, Color.white, 0.4f)) + ">" + b.txt + "</color></b></size>", center); }
            }
            GUI.color = Color.white;
        }

        // ⚔ 무기 단추 — 정비고 왼쪽 위. 산 무기만 (무기고를 사야 보인다)
        int weaponDrop = -1;                                                  // 펼친 목록 — 0 주 무기 · 1 보조 무기
        /// <summary>⚔ 무기 고르기 — 평소엔 「무기 ▾」 한 칸, 누르면 산 무기 목록이 펼쳐진다 (아홉 개가 트리를 덮지 않게)</summary>
        // 🔫 무기 효과판 — 산 무기와 발동 확률 (09-24: 무기를 골라 끼우던 목록을 없앴다)
        void WeaponBar(Rect r)
        {
            weaponDrop = -1;
            if (sim.Lv("w_hub") <= 0) return;
            var list = new System.Collections.Generic.List<int>();
            for (int w = 1; w < SweepSim.ProcBase.Length; w++) if (sim.ProcChance(w) > 0) list.Add(w);
            var b = new Rect(r.x, r.y, 190, 26 + 18 * System.Math.Max(1, list.Count));
            GUI.color = new Color(0.07f, 0.08f, 0.11f, 0.94f); GUI.DrawTexture(b, white); Frame(b, new Color(0.25f, 0.28f, 0.34f), 1); GUI.color = Color.white;
            GUI.Label(new Rect(b.x + 8, b.y + 3, 180, 20), Loc.T("<size=12><color=#8a9bb3>공격 때 함께 터진다</color></size>"), label);
            if (list.Count == 0) GUI.Label(new Rect(b.x + 8, b.y + 22, 180, 18), Loc.T("<size=12><color=#5f6878>산 무기가 없다</color></size>"), label);
            for (int i = 0; i < list.Count; i++)
            {
                int w = list[i]; var c = SweepGame.WeaponCol(w);
                GUI.Label(new Rect(b.x + 8, b.y + 22 + i * 18, 180, 18), "<size=12><color=#" + ColorUtility.ToHtmlStringRGB(c) + ">● " + SweepSim.WeaponName[w] + "</color>  <b>" + Mathf.RoundToInt((float)sim.ProcChance(w) * 100) + "%</b></size>", label);
            }
        }
        static int NodeIndex(string id) { for (int i = 0; i < SweepSim.Nodes.Length; i++) if (SweepSim.Nodes[i].id == id) return i; return 0; }

        // ★ 1면 조작 — 조종실 창 위에 신문 두 장. 고른 기사가 증권 속보로 나간다
        bool frontOpen;
        public void TestOpen(string w, bool v) { if (w == "front") frontOpen = v; else if (w == "bank") bankruptArmed = v; else if (w == "glass") glassOpen = v; }   // 에디터 시험용 — 비공개 창 열기
        void FrontPick()
        {
            var S = sim.S;
            if (S.frontPick >= 0 && S.frontPick < Market.NewsBook.Length && S.front1 < 0)
            {   // 📰 내일 1면 확정 — 조종실 창 위 띠 (다음 출동과 함께 발행)
                var fr = new Rect(ox + 250, 50, 460, 24);
                GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.92f); GUI.DrawTexture(fr, white); Frame(fr, new Color(1f, 0.36f, 0.81f, 0.7f), 1); GUI.color = Color.white;
                GUI.Label(fr, Loc.T("<size=12><color=#ffb3ea>★ 내일 1면</color>  ") + Clip(Market.NewsBook[S.frontPick].head.Replace(Loc.T("[소문] "), ""), 24) + Loc.T("  <color=#8a93a3>· 출동하면 발행</color></size>"), center);
            }
            if (S.front1 < 0 || S.front2 < 0 || sim.Mk == null) { frontOpen = false; return; }
            if (!frontOpen) return;                                             // 궤도일보 모니터의 「★ 내일 1면 고르기」를 눌러야 뜬다
            var w = new Rect(ox + 250, 52, 460, 170);
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(w.x + 4, w.y + 6, w.width, w.height), white);
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.96f); GUI.DrawTexture(w, white); Frame(w, new Color(1f, 0.36f, 0.81f), 2);
            GUI.color = Color.white;
            GUI.Label(new Rect(w.x, w.y + 6, w.width, 22), Loc.T("<size=14><b><color=#ffb3ea>★ 내일 궤도일보 1면을 고른다</color></b></size>"), center);
            for (int k = 0; k < 2; k++)
            {
                var nd = Market.NewsBook[k == 0 ? S.front1 : S.front2];
                var r = new Rect(w.x + 14 + k * 222, w.y + 34, 210, 122);
                bool ov = r.Contains(Event.current.mousePosition);
                GUI.color = ov ? new Color(0.98f, 0.95f, 0.88f) : new Color(0.93f, 0.9f, 0.83f); GUI.DrawTexture(r, white);
                GUI.color = new Color(0.1f, 0.1f, 0.1f); GUI.DrawTexture(new Rect(r.x + 8, r.y + 24, r.width - 16, 2), white); GUI.color = Color.white;
                paperSmall.fontSize = 10; GUI.Label(new Rect(r.x + 8, r.y + 3, r.width - 16, 20), Loc.T("궤도일보 1면"), paperSmall);
                paperBody.fontSize = 13; GUI.Label(new Rect(r.x + 8, r.y + 30, r.width - 16, 40), "<b>" + nd.head.Replace(Loc.T("[소문] "), "") + "</b>", paperBody);
                string eff = (nd.up != null ? "<color=#b3261e>▲ " + SecName(new NewsDef { up = nd.up }) + "</color>  " : "") + (nd.down != null ? "<color=#1f4fb3>▼ " + SecName(new NewsDef { down = nd.down }) + "</color>" : "");
                paperBody.fontSize = 11; GUI.Label(new Rect(r.x + 8, r.y + 78, r.width - 16, 40), eff, paperBody);
                paperBody.fontSize = 14;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { sim.PickFront(k); frontOpen = false; OrbitSfx.Play("buy", 0.8f); BuyFx(r.center, new Color(1f, 0.36f, 0.81f), true, Loc.T("1면 확정!")); }
            }
            if (GUI.Button(new Rect(w.xMax - 30, w.y + 4, 26, 22), "<size=13>✕</size>", btnOff)) frontOpen = false;   // 나중에
        }
    }
}
