#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SalvageRun.Orbit;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.EditorTools
{
    /// <summary>
    /// 📐 화면 비율 검사 (09-29 사장님 「화면 비율에 따라 깨지는 거 있는지」) — 게임 창 크기를 비율별로 바꿔 가며
    /// 조종실 · 트리 · 가게 · 출동 · 결산을 찍어 Builds/aspect/ 에 남긴다. 저장(sweep.*)은 돌려놓는다.
    /// 게임 창 크기 바꾸기는 유니티 속 기능(GameViewSizes)을 꺼내 쓴다 — 유니티 판이 바뀌면 이름이 달라질 수 있다.
    /// </summary>
    public static class AspectTour
    {
        public static readonly (int w, int h, string name)[] Sizes =
        {
            (1920, 1080, "16x9"), (1920, 1200, "16x10"), (1600, 1200, "4x3"), (1280, 1024, "5x4"),
            (2560, 1080, "21x9"), (3840, 1080, "32x9"), (1366, 768, "1366x768"),
        };
        public static string OutDir => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Application.dataPath)), "Builds", "aspect");
        static int f, si, step, f0, oldIndex = -1; static bool running;

        [MenuItem("SalvageRun/화면 비율 검사 (7가지)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || running) return;
            System.IO.Directory.CreateDirectory(OutDir);
            PlayerPrefs.SetString("sweep.backup.state", PlayerPrefs.GetString("sweep.state", ""));
            PlayerPrefs.SetString("sweep.backup.meta", PlayerPrefs.GetString("sweep.meta", ""));
            PlayerPrefs.Save();
            oldIndex = SelectedIndex();
            f = 0; si = 0; step = 0; running = true;
            EditorApplication.update -= Step; EditorApplication.update += Step;
            EditorApplication.EnterPlaymode();
        }

        // ── 게임 창 크기 (유니티 속 기능)
        static Assembly Ed => typeof(Editor).Assembly;
        static object Group()
        {
            var sizes = Ed.GetType("UnityEditor.GameViewSizes");
            var inst = typeof(ScriptableSingleton<>).MakeGenericType(sizes).GetProperty("instance").GetValue(null);
            return sizes.GetMethod("GetGroup").Invoke(inst, new object[] { (int)GameViewSizeGroupType.Standalone });
        }
        static EditorWindow GameView() => EditorWindow.GetWindow(Ed.GetType("UnityEditor.GameView"));
        static int SelectedIndex() { var gv = GameView(); return (int)gv.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gv); }
        static void Select(int i) { var gv = GameView(); gv.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gv, i); gv.Repaint(); }
        static int FindOrAdd(int w, int h)
        {
            var g = Group(); var gt = g.GetType();
            int n = (int)gt.GetMethod("GetTotalCount").Invoke(g, null);
            for (int i = 0; i < n; i++)
            {
                var s = gt.GetMethod("GetGameViewSize").Invoke(g, new object[] { i }); var st = s.GetType();
                if ((int)st.GetProperty("width").GetValue(s) == w && (int)st.GetProperty("height").GetValue(s) == h && st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") return i;
            }
            var sizeT = Ed.GetType("UnityEditor.GameViewSize"); var typeT = Ed.GetType("UnityEditor.GameViewSizeType");
            var ns = Activator.CreateInstance(sizeT, Enum.Parse(typeT, "FixedResolution"), w, h, "검사 " + w + "x" + h);
            gt.GetMethod("AddCustomSize").Invoke(g, new[] { ns });
            return n;
        }

        static void Next() { step++; f0 = Time.frameCount; }
        static bool Wait(int n) => Time.frameCount - f0 >= n;
        static void Shot(string what) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, Sizes[si].name + "_" + what + ".png"), 1);

        static void Step()
        {
            if (!EditorApplication.isPlaying) { if (f > 99000) Done(); return; }
            f++;
            var g = UnityEngine.Object.FindFirstObjectByType<SweepGame>();
            if (g == null || g.sim == null || f < 60) return;
            try
            {
                switch (step)
                {
                    case 0: Select(FindOrAdd(Sizes[si].w, Sizes[si].h)); g.hud.lobby = true; Next(); break;
                    case 1: if (Wait(40)) { Shot("0lobby"); Next(); } break;
                    case 2: if (Wait(3)) { g.hud.lobby = false; g.sim.M.careerOpen = false; g.hud.flow = 2; g.sim.S.overdue = false;
                            foreach (var id in new[] { "e_shop", "a_open" }) { int i = Array.FindIndex(SweepSim.Nodes, x => x.id == id); g.sim.S.lv[i] = Math.Max(1, g.sim.S.lv[i]); }
                            foreach (var fl in new[] { "dlg:stock", "dlg:shop", "dlg:pro", "g:yoon0" }) if (!g.sim.M.flags.Contains(fl)) g.sim.M.flags.Add(fl);   // 첫 대화가 화면을 덮지 않게
                            g.sim.RollShop(); Next(); } break;
                    case 3: if (Wait(40)) { Shot("1cockpit"); Next(); } break;
                    case 4: if (Wait(3)) { g.hud.flow = 3; Next(); } break;
                    case 5: if (Wait(40)) { Shot("2tree"); Next(); } break;
                    case 6: if (Wait(3)) { g.hud.flow = 5; Next(); } break;
                    case 7: if (Wait(40)) { Shot("3shop"); Next(); } break;
                    case 8: if (Wait(3)) { g.hud.flow = 4; Next(); } break;
                    case 9: if (Wait(40)) { Shot("3stock"); Next(); } break;
                    case 10: if (Wait(3)) { g.hud.flow = 2; Next(); } break;
                    case 11: if (Wait(10)) { g.sim.S.overdue = false; g.hud.Go(); Next(); } break;
                    case 12: if (!g.sim.R.over && g.sim.R.fuel < 5) g.sim.R.fuel = 20; if (Wait(150)) { Shot("4run"); g.sim.R.fuel = 0; Next(); } break;
                    case 13: if (g.sim.R.over && Wait(60)) { Shot("5result"); Next(); } break;
                    case 14: if (Wait(3)) { g.hud.flow = 2; si++; if (si >= Sizes.Length) { f = 99001; EditorApplication.ExitPlaymode(); return; } step = 0; } break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[비율 검사] " + e.Message); f = 99001; EditorApplication.ExitPlaymode(); }
        }
        static void Done()
        {
            EditorApplication.update -= Step; running = false;
            PlayerPrefs.SetString("sweep.state", PlayerPrefs.GetString("sweep.backup.state", ""));
            PlayerPrefs.SetString("sweep.meta", PlayerPrefs.GetString("sweep.backup.meta", ""));
            PlayerPrefs.Save();
            try { if (oldIndex >= 0) Select(oldIndex); } catch { }
            Debug.Log("[비율 검사] 끝 — " + OutDir);
        }
    }
}
#endif
