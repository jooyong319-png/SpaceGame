#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SalvageRun.Orbit;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.EditorTools
{
    /// <summary>
    /// 🔍 글자 잘림 검사 한 바퀴 (09-28 리팩토링) — 한국어 · 영어로 모든 화면을 돌며 SweepHud.Probe 가 찾은
    /// 「칸보다 넓거나 높아 잘리는 글자」를 Builds/clip_probe.txt 에 남긴다. 저장(sweep.*)과 언어 설정은 돌려놓는다.
    /// 로비 · 설정 · 조종실 · 트리 칸 설명 전부 · 가게 카드 · 증권 · 격납고 배 전부 · 영구 강화 · 복권 · 대출 · 신문 · 1면 · 대사 전 장면 · 엔딩.
    /// </summary>
    public static class ClipProbeTour
    {
        static int f, oldLang, step, lastFrame; static List<System.Action<SweepGame>> acts; static bool built;
        public static string OutPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Application.dataPath)), "Builds", "clip_probe.txt");
        public static bool Running { get; private set; }

        [MenuItem("SalvageRun/글자 잘림 검사 (한 · 영 전 화면)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || Running) { Debug.Log("[잘림 검사] 플레이 중이라 건너뜀"); return; }
            PlayerPrefs.SetString("sweep.backup.state", PlayerPrefs.GetString("sweep.state", ""));
            PlayerPrefs.SetString("sweep.backup.meta", PlayerPrefs.GetString("sweep.meta", ""));
            oldLang = PlayerPrefs.GetInt(LocBoot.Key, -1);
            PlayerPrefs.SetInt(LocBoot.Key, 0); PlayerPrefs.Save();
            f = 0; step = -1; built = false; acts = null; Running = true;
            if (System.IO.File.Exists(OutPath)) System.IO.File.Delete(OutPath);
            EditorApplication.update -= Step; EditorApplication.update += Step;
            EditorApplication.EnterPlaymode();
        }

        static void Close(SweepGame g)
        {
            var h = g.hud; h.settingsOpen = false; h.newsOpen = false; h.loanOpen = false; h.TestOpen("front", false); h.TestBuyAsk(-1);
            h.TestTip = -1; h.testShopTip = -1; g.sim.M.careerOpen = false; h.lobby = false;
        }

        static void Build()
        {
            acts = new List<System.Action<SweepGame>>();
            string[] scenes = { "pro", "pro2", "re1", "re2", "stock", "shop", "news", "fuel", "ev0", "ev1", "ev2", "ev3", "ev4", "gate", "chain", "b1", "b2", "b3", "b4", "b5", "b6", "b7", "b8", "b9", "b10", "b11", "b12" };
            for (int lang = 0; lang < 2; lang++)
            {
                int L = lang;
                acts.Add(g => { LocBoot.Apply(L); Close(g); g.hud.lobby = true; });
                acts.Add(g => { g.hud.lobby = true; g.hud.TestSettings(); });
                acts.Add(g =>
                {
                    Close(g); g.hud.flow = 2; g.sim.S.overdue = false;
                    foreach (var id in new[] { "e_shop", "a_open" }) { int i = System.Array.FindIndex(SweepSim.Nodes, x => x.id == id); g.sim.S.lv[i] = 1; }
                    g.sim.RollShop();
                });
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.TestSettings(); });
                acts.Add(g => { Close(g); g.hud.flow = 3; });
                for (int k = 0; k < 400; k++) { int kk = k; acts.Add(g => { g.hud.flow = 3; g.hud.TestTip = kk < g.hud.TileCount ? kk : -1; }); }
                acts.Add(g => { Close(g); g.hud.flow = 5; });
                for (int k = 0; k < 6; k++) { int kk = k; acts.Add(g => { g.hud.flow = 5; g.hud.testShopTip = kk; }); }
                acts.Add(g => { g.hud.testShopTip = -1; g.hud.TestBuyAsk(0); });
                acts.Add(g => { Close(g); g.hud.flow = 4; });
                for (int s = 0; s < SweepSim.Ships.Length; s++) { int ss = s; acts.Add(g => { Close(g); g.sim.M.careerOpen = true; g.hud.TestHangar(0, ss); }); }
                acts.Add(g => { g.sim.M.careerOpen = true; g.hud.TestHangar(1, 0); });
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.TestLotto(0); });
                acts.Add(g => g.hud.TestLotto(1));
                acts.Add(g => g.hud.TestLotto(2));
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.loanOpen = true; });
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.newsOpen = true; });
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.TestOpen("front", true); });
                foreach (var sc in scenes)
                {
                    string id = sc; acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.TestDlg(id); });
                    for (int i = 0; i < 12; i++) { int ii = i; acts.Add(g => g.hud.TestDlgLine(ii)); }
                }
                for (int e = 0; e < 4; e++) { int ee = e; acts.Add(g => { Close(g); g.hud.TestEnd(ee); }); }
                acts.Add(g => g.hud.TestEnd(-1));
                // 📻 무전 — 모든 줄을 다 찍힌 채로
                for (int q = 0; q < 16; q++) { int qq = q; acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.TestRadio(qq); }); }
                // 🚀 출동 — 위 줄 · 계기 · 행성 이벤트 안내 전부 · 그다음 결산
                acts.Add(g => { Close(g); g.hud.flow = 2; g.sim.S.overdue = false; g.hud.Go(); });
                for (int q = 0; q < 30; q++) acts.Add(g => { if (!g.sim.R.over && g.sim.R.fuel < 8) g.sim.R.fuel = 20; });
                for (int e = 0; e < SweepSim.EventNames.Length; e++) { int ee = e; acts.Add(g => { if (!g.sim.R.over) { g.sim.R.fuel = 20; g.hud.Banner("⚠ " + SweepSim.EventNames[ee] + " — " + SweepSim.EventHint[ee], ee % 2, 2.2f); } }); }
                // 🛰 10-10 관문 이름표 — 관문 11개 × 방어막 · 열림 (사장님이 직접 찾은 잘림을 검사가 못 잡았다 → 상자 밖 검사 ProbeIn)
                for (int z = 0; z < SweepSim.GateNames.Length; z++)
                    for (int rdy = 0; rdy < 2; rdy++)
                    {
                        int zz = z, rr = rdy;
                        acts.Add(g => { if (g.sim.R.over) return; g.sim.R.fuel = 20; int pl = 0; for (int i = 1; i <= zz; i++) pl |= 1 << SweepSim.OrbitOrder[i]; g.sim.S.planets = pl; g.sim.M.cleanReady = rr == 1; });
                        acts.Add(g => { });
                    }
                acts.Add(g => { g.sim.S.planets = 0; g.sim.M.cleanReady = false; });
                // 📍 사건 예고 띠 · 발생 표적 이름표 — 띠가 다 펼쳐질 때까지 기다린다
                for (int e = 0; e < SweepSim.EventNames.Length; e++)
                {
                    int ee = e;
                    acts.Add(g => { if (g.sim.R.over) return; g.sim.R.fuel = 20; g.hud.EventWarn(ee, 480, 200, 60, SweepSim.EventNames[ee] + "\n" + SweepSim.EventHint[ee]); });
                    for (int q = 0; q < 10; q++) acts.Add(g => { if (!g.sim.R.over) g.sim.R.fuel = 20; });
                    acts.Add(g => { if (!g.sim.R.over) g.hud.EventGo(ee, 480, 200, 60, SweepSim.EventNames[ee]); });
                    for (int q = 0; q < 4; q++) acts.Add(g => { if (!g.sim.R.over) g.sim.R.fuel = 20; });
                }
                acts.Add(g => { g.sim.R.fuel = 0; });
                for (int q = 0; q < 40; q++) acts.Add(g => { });            // 결산 화면이 뜨는 동안
                // 🔢 10-08 극단 상태 — 큰 돈 · 큰 빚 · 납부일 · 마지막 청구서 · 모든 종목 보유 (숫자가 길어질 때 잘림)
                acts.Add(g =>
                {
                    Close(g); var S = g.sim.S;
                    S.cash = 987654321012; S.debt = 123456789012; S.overdue = true; S.bill = SweepSim.Bills.Length - 1; S.hired = true;
                    if (g.sim.Mk != null) foreach (var st in g.sim.Mk.M.st) { st.shares = 123456; st.cost = 987654321; }
                    g.hud.flow = 2;
                });
                foreach (int fl in new[] { 2, 4, 3, 5 }) { int ff = fl; acts.Add(g => { Close(g); g.hud.flow = ff; }); acts.Add(g => { }); }
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.loanOpen = true; });
                acts.Add(g => { Close(g); g.hud.flow = 2; g.hud.TestLotto(0); });
                // 🧾 10-09 많이 부순 판의 결산 — 종류별 아이콘 줄 · 피해 목록 13종 · 빚 상환 줄 (사장님 「자원을 많이 먹으면 글이 넘친다」)
                acts.Add(g =>
                {
                    Close(g); var R = g.sim.R;
                    R.cChip = 123456; R.cSat = 23456; R.cFuel = 3456; R.cVault = 456; R.cTank = 5678; R.cBig = 789;
                    R.broke = R.cChip + R.cSat + R.cFuel + R.cVault + R.cTank + R.cBig; R.chainBest = 12345;
                    R.earnClaw = 9.87e11; R.earnDrone = 1.2e11; R.earnBlast = 3.4e11; R.bonus = 5e10; R.interest = 2e9; R.cut = 7.6e10;
                    for (int i = 0; i < R.dmgBy.Length; i++) R.dmgBy[i] = 1e12 / (i + 1);
                    R.gateStock = 0; R.gateAmt = 9.9e11; R.gateShares = 123456789;   // 🛰 10-10 관문 보상 띠
                    g.sim.S.overdue = false; g.hud.OnRunEnd();
                });
                for (int q = 0; q < 40; q++) acts.Add(g => { g.hud.flow = 1; });
                acts.Add(g => g.hud.TestCert(1, 4)); for (int q = 0; q < 6; q++) acts.Add(g => { });   // 🛰 10-10 공로주 증서
                acts.Add(g => g.hud.SendMessage("CertClose", SendMessageOptions.DontRequireReceiver));
                acts.Add(g => { Close(g); g.sim.S.overdue = false; g.hud.flow = 2; });
            }
        }

        static void Step()
        {
            if (!EditorApplication.isPlaying) { if (f > 99000) Done(); return; }
            f++;
            var g = Object.FindFirstObjectByType<SweepGame>();
            if (g == null || g.sim == null || f < 80) return;
            if (!built) { Build(); built = true; SweepHud.ProbeHits.Clear(); SweepHud.Probe = true; step = 0; lastFrame = Time.frameCount; acts[0](g); return; }
            if (Time.frameCount - lastFrame < 3) return;              // 그릴 틈을 준다
            step++; lastFrame = Time.frameCount;
            if (step < acts.Count) { try { acts[step](g); } catch (System.Exception ex) { SweepHud.ProbeHits.Add("!! " + step + " " + ex.Message); } return; }
            var lines = new List<string> { "# 글자 잘림 검사 " + System.DateTime.Now.ToString("MM-dd HH:mm") + " · " + SweepHud.ProbeHits.Count + "곳" };
            lines.AddRange(SweepHud.ProbeHits);
            System.IO.File.WriteAllLines(OutPath, lines);
            SweepHud.Probe = false;
            f = 99001; EditorApplication.ExitPlaymode();
        }

        static void Done()
        {
            EditorApplication.update -= Step;
            PlayerPrefs.SetString("sweep.state", PlayerPrefs.GetString("sweep.backup.state", ""));
            PlayerPrefs.SetString("sweep.meta", PlayerPrefs.GetString("sweep.backup.meta", ""));
            if (oldLang < 0) PlayerPrefs.DeleteKey(LocBoot.Key); else PlayerPrefs.SetInt(LocBoot.Key, oldLang);
            PlayerPrefs.Save();
            Running = false;
            Debug.Log("[잘림 검사] 끝 — " + SweepHud.ProbeHits.Count + "곳 · " + OutPath);
        }
    }
}
#endif
