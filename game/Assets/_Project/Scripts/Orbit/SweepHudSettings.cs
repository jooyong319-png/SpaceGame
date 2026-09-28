using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // ⚙ 설정 창 — 소리 · 언어 · 흔들림 · 화면 (09-28 SweepHud.cs 에서 나눔)
    public partial class SweepHud
    {
        // ⚙ 설정 — 늘 오른쪽 위 (09-24 사장님 18번 「소리 설정 버튼 · 항상 오른쪽 위」). 시안 https://claude.ai/artifact/CthkM2c8KDnFcaxG8m5zJt
        public bool settingsOpen;
        float vol = -1, sfxVol = 1; int shakeLv, flashLv;
        static readonly float[] ShakeLvMul = { 1f, 0.4f, 0f }, FlashLvMul = { 1f, 0.35f };
        void LoadSettings()
        {
            vol = PlayerPrefs.GetFloat("orbit.vol", 0.7f); sfxVol = PlayerPrefs.GetFloat("orbit.sfx", 1f);
            shakeLv = PlayerPrefs.GetInt("orbit.shake", 0); flashLv = PlayerPrefs.GetInt("orbit.flash", 0);
            manualFire = true;                                                 // 👆 수동 공격으로 확정 (09-26 사장님 「아싸리 수동으로 하자」)
            ApplySettings();
        }
        void ApplySettings()
        {
            AudioListener.volume = vol; OrbitSfx.SfxVol = sfxVol;
            SweepGame.ShakeMul = ShakeLvMul[Mathf.Clamp(shakeLv, 0, 2)]; SweepGame.FlashMul = FlashLvMul[Mathf.Clamp(flashLv, 0, 1)];
            reduceMotion = shakeLv == 2;
        }
        // 🖥 화면 — 저장한 대로 (0 전체 화면 · 1 창). 창 크기는 모니터보다 크면 들어가는 가장 큰 것으로. Alt+Enter 는 껐다 (09-27)
        static readonly int[] WinW = { 1280, 1600, 1920 }, WinH = { 720, 900, 1080 };
        public static void ApplyScreen()
        {
            if (Application.isEditor) return;
            int sw = Display.main.systemWidth, sh = Display.main.systemHeight;
            if (PlayerPrefs.GetInt("orbit.screen", 0) == 0) { Screen.SetResolution(sw, sh, FullScreenMode.FullScreenWindow); return; }
            int k = Mathf.Clamp(PlayerPrefs.GetInt("orbit.win", 1), 0, WinW.Length - 1);
            while (k > 0 && (WinW[k] > sw || WinH[k] > sh - 60)) k--;
            Screen.SetResolution(WinW[k], WinH[k], FullScreenMode.Windowed);
        }
        void SaveSettings()
        {
            PlayerPrefs.SetFloat("orbit.vol", vol); PlayerPrefs.SetFloat("orbit.sfx", sfxVol); PlayerPrefs.SetFloat("orbit.bgm", OrbitMusic.Vol);
            PlayerPrefs.SetInt("orbit.shake", shakeLv); PlayerPrefs.SetInt("orbit.flash", flashLv); PlayerPrefs.SetInt("orbit.manual", manualFire ? 1 : 0); PlayerPrefs.Save();
            ApplySettings();
        }
        void VolumeButton()                                                     // 이름은 그대로 — 이제 ⚙ 설정 단추
        {
            if (vol < 0) LoadSettings();
            if (lobby) { if (settingsOpen) SettingsWin(); return; }            // 로비엔 메뉴에 설정이 있다 — 구석 단추는 숨김
            var r = new Rect(vw - 58, 8, 50, 22);
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = settingsOpen || ov ? new Color(0.24f, 0.19f, 0.08f, 0.95f) : new Color(0.08f, 0.1f, 0.13f, 0.85f); GUI.DrawTexture(r, white);
            Frame(r, settingsOpen ? SweepGame.Amber : new Color(0.3f, 0.34f, 0.42f), 1); GUI.color = Color.white;
            GUI.Label(r, "<size=12><color=" + (settingsOpen ? "#ffdf95" : "#c8d0dc") + Loc.T(">설정</color></size>"), center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { settingsOpen = !settingsOpen; OrbitSfx.Play("tick", 0.8f); }
            if (settingsOpen) SettingsWin();
        }
        public void TestSettings() => settingsOpen = true;   // 에디터 캡처용
        int roomGo; float roomGoAt;                                         // 🧭 새 방을 사면 그 방으로 (0.5초 뒤)
        void RoomGoTick() { if (roomGoAt > 0 && Time.unscaledTime >= roomGoAt) { roomGoAt = 0; if (sim.R.over && !sim.M.careerOpen) GoFlow(roomGo); } }
        void SettingsWin()
        {
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            var w = new Rect(vw / 2 - 250, 44, 500, 510);
            GUI.color = new Color(0.043f, 0.063f, 0.09f, 0.98f); GUI.DrawTexture(w, white); Frame(w, SweepGame.Amber, 2); GUI.color = Color.white;
            GUI.Label(new Rect(w.x + 22, w.y + 14, 200, 30), Loc.T("<size=20><b><color=#ffdf95>설정</color></b></size>"), label);
            if (GUI.Button(new Rect(w.xMax - 104, w.y + 14, 88, 26), Loc.T("<size=12>닫기 Esc</size>"), btn)) settingsOpen = false;
            float y = w.y + 62;
            bool changed = false;
            void Slider(string name, ref float v)
            {
                GUI.Label(new Rect(w.x + 24, y, 140, 24), "<size=14>" + name + "</size>", label);
                var sr = new Rect(w.x + 170, y + 9, 230, 8);
                GUI.DrawTexture(sr, texBar); GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(sr.x, sr.y, sr.width * v, sr.height), white);
                GUI.color = new Color(1f, 0.87f, 0.58f); GUI.DrawTexture(new Rect(sr.x + sr.width * v - 6, sr.y - 4, 12, 16), white); GUI.color = Color.white;
                GUI.Label(new Rect(sr.xMax + 10, y, 60, 24), "<size=13>" + Mathf.RoundToInt(v * 100) + "%</size>", label);
                var hit = new Rect(sr.x - 8, y, sr.width + 16, 26); var e = Event.current;
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && hit.Contains(e.mousePosition)) { v = Mathf.Clamp01((e.mousePosition.x - sr.x) / sr.width); v = Mathf.Round(v * 20) / 20f; changed = true; e.Use(); }
                y += 40;
            }
            int Pick(string name, int cur, string[] opts)
            {
                GUI.Label(new Rect(w.x + 24, y, 140, 24), "<size=14>" + name + "</size>", label);
                for (int k = 0; k < opts.Length; k++)
                    if (GUI.Button(new Rect(w.x + 170 + k * 84, y, 80, 26), "<size=12>" + (k == cur ? "<color=#ffdf95>" + opts[k] + "</color>" : opts[k]) + "</size>", k == cur ? btn : btnOff)) { cur = k; changed = true; OrbitSfx.Play("tick", 0.6f); }
                y += 40; return cur;
            }
            Slider(Loc.T("전체 소리"), ref vol);
            Slider(Loc.T("효과음"), ref sfxVol);
            Slider(Loc.T("배경음"), ref OrbitMusic.Vol);
            {   // 🌐 언어 — 09-28부터 고르면 바로 바뀐다 (Loc.SetLang 이 표의 글자를 갈아 끼운다)
                int sl = Loc.En ? 1 : 0, nl = Pick("언어 · Language", sl, new[] { "한국어", "English" });
                if (nl != sl) { LocBoot.Apply(nl); OrbitSfx.Play("tick", 0.6f); }
            }
            shakeLv = Pick(Loc.T("화면 흔들림"), shakeLv, new[] { Loc.T("켬"), Loc.T("줄임"), Loc.T("끔") });
            { int sq = PlayerPrefs.GetInt("orbit.shopQuick", 0), nq = Pick(Loc.T("가게 구입"), sq, new[] { Loc.T("묻기"), Loc.T("바로") }); if (nq != sq) { PlayerPrefs.SetInt("orbit.shopQuick", nq); PlayerPrefs.Save(); } }   // 🛒 09-27 「다음부터 바로 구매」 되돌리기
            flashLv = Pick(Loc.T("번쩍임"), flashLv, new[] { Loc.T("켬"), Loc.T("줄임") });
            int fs = PlayerPrefs.GetInt("orbit.screen", 0);
            int nf = Pick(Loc.T("화면"), fs, new[] { Loc.T("전체 화면"), Loc.T("창") });
            if (nf != fs) { PlayerPrefs.SetInt("orbit.screen", nf); PlayerPrefs.Save(); ApplyScreen(); }
            if (nf == 1)
            {   // 🖥 창 크기 (09-27 사장님 「창 크기 조절 · 알트엔터 없애고 설정에서」)
                int ws = PlayerPrefs.GetInt("orbit.win", 1);
                int nw = Pick(Loc.T("창 크기 (가로)"), ws, new[] { "1280", "1600", "1920" });
                if (nw != ws) { PlayerPrefs.SetInt("orbit.win", nw); PlayerPrefs.Save(); ApplyScreen(); }
            }
            GUI.Label(new Rect(w.x + 24, w.yMax - 30, w.width - 48, 20), Loc.T("<size=11><color=#8a93a3>저장은 자동 · M = 소리 끄기 · Esc = 닫기</color></size>"), label);
            if (!lobby && sim.R.over && GUI.Button(new Rect(w.x + 24, w.yMax - 66, 130, 26), Loc.T("<size=12>설명 다시 보기</size>"), btnOffC)) { sim.M.flags.Remove("dlg:stock"); sim.M.flags.Remove("dlg:shop"); sim.M.flags.Remove("dlg:news"); settingsOpen = false; OrbitSfx.Play("tick", 0.6f); }   // 💬 증권 · 가게 · 신문 설명을 다시 — 그 방에 다시 들어가면 뜬다
            if (!lobby && sim.R.over && GUI.Button(new Rect(w.xMax - 134, w.yMax - 36, 118, 26), Loc.T("<size=12>로비로 나가기</size>"), btnOff)) { game.Save(); settingsOpen = false; lobby = true; lobbyT = 0; }
            if (changed) SaveSettings();
        }

    }
}
