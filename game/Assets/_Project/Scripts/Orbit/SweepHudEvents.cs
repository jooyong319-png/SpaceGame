using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 📍 10-10 출동 중 사건 — 「무슨 일이 일어난 줄도 모르겠다」(사장님). 시안 https://claude.ai/artifact/GD3vDhbo6FBPdUbV3kdkCj
    /// ① 2초 전: 화면 위 사선 띠 + 큰 이름 + 한 줄 설명 + 카운트다운, 나올 자리에 점선 표적
    /// ② 발생: 그 자리에 실선 표적 · 꺾쇠 · 이름표 + 그쪽 가장자리 빛
    /// ③ 빨강 = 위험 · 기회(충돌 · 폭풍 · 대충돌), 노랑 = 놓치면 손해(보급선 · 금고 호송대 · 떠돌이 소행성)
    /// 그리는 것만 — 판정 · 숫자는 그대로.
    /// </summary>
    public partial class SweepHud
    {
        static readonly Color EvRed = new Color(1f, 0.36f, 0.31f), EvGold = new Color(1f, 0.81f, 0.43f);
        static Color EvCol(int ev) => ev == 0 || ev == 3 ? EvGold : EvRed;

        float evWarnT, evGoT; int evWarnEv = -1, evGoEv = -1; string evWarnName, evWarnHint, evGoName, evGoArrow;
        Vector2 evWarnPos, evGoPos; float evWarnR, evGoR;
        class EvMark { public Vector2 p; public float r, t; public Color c; public string label; }
        readonly System.Collections.Generic.List<EvMark> evMarks = new System.Collections.Generic.List<EvMark>();
        static Texture2D texStripe, texDash;

        public void EventWarn(int ev, double x, double y, double rad, string text)
        {
            evWarnEv = ev; evWarnT = 2.2f;
            var parts = (text ?? "").Split('\n'); evWarnName = parts[0]; evWarnHint = parts.Length > 1 ? parts[1] : "";
            evWarnPos = new Vector2((float)x, (float)y); evWarnR = (float)rad;
            actEv = ev; actWarnAt = Time.unscaledTime; actPos = evWarnPos; actR = evWarnR;   // 🎬 등장 시작
            OrbitSfx.Play(ev == 0 || ev == 3 ? "grab" : "danger", 0.9f);
        }
        public void EventGo(int ev, double x, double y, double rad, string name)
        {
            evGoEv = ev; evGoT = 1.8f; evGoName = name; evGoArrow = null; evGoPos = new Vector2((float)x, (float)y); evGoR = (float)rad;
            if (actEv != ev) { actEv = ev; actWarnAt = Time.unscaledTime - 2f; actPos = evGoPos; actR = evGoR; }
            actGoAt = Time.unscaledTime;
            if (evWarnEv == ev) evWarnT = Mathf.Min(evWarnT, 0.25f);                 // 띠는 접혀 들어간다
            OrbitSfx.Play(ev == 0 || ev == 3 ? "supply" : "blast", 1f);
            if (ev != 0 && ev != 3) OrbitSfx.Play("collide", 0.8f);
        }
        /// <summary>호송대처럼 발생 순간에야 자리가 정해지는 것</summary>
        public void EventAt(double x, double y, double rad, string arrow) { tugAt = Time.unscaledTime; tugFuel = evGoEv == 0; tugDir = arrow == "◀" ? -1 : 1; tugStart = new Vector2(tugDir > 0 ? -30 : 990, (float)y); evGoPos = new Vector2((float)x, (float)y); evGoR = (float)rad; evGoT = Mathf.Max(evGoT, 1.8f); evGoArrow = arrow; }
        /// <summary>떠돌이 소행성 · 운석 — 작은 표적 하나</summary>
        public void MarkAt(double x, double y, double rad, Color c, string label)
        {
            evMarks.Add(new EvMark { p = new Vector2((float)x, (float)y), r = (float)rad, t = 1.6f, c = c, label = label });
            if (evMarks.Count > 6) evMarks.RemoveAt(0);
        }
        public void ClearEventFx() { evWarnT = evGoT = 0; evMarks.Clear(); }

        Vector2 SimGui(Vector2 p) { var sp = game.WorldToScreen(game.PxToWorld(p.x, p.y)); return new Vector2(sp.x / scale, (Screen.height - sp.y) / scale - oy); }

        static void EnsureEvTex()
        {
            if (texStripe == null)
            {   // 사선 띠 — 32×32 한 칸, 가로로 반복
                texStripe = new Texture2D(32, 32) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) px[y * 32 + x] = ((x + y) % 32) < 12 ? Color.white : new Color(1, 1, 1, 0);
                texStripe.SetPixels(px); texStripe.Apply();
            }
            if (texDash == null)
            {   // 점선 고리 — 128칸, 조각 28개 중 절반
                const int N = 128; texDash = new Texture2D(N, N) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color[N * N]; float r1 = N / 2f - 1, r0 = r1 - 5;
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - N / 2f, dy = y + 0.5f - N / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2) + 0.5f;
                    bool on = d <= r1 && d >= r0 && ((int)(a * 28)) % 2 == 0;
                    px[y * N + x] = on ? Color.white : new Color(1, 1, 1, 0);
                }
                texDash.SetPixels(px); texDash.Apply();
            }
        }

        /// <summary>표적 — 점선(예고) 또는 실선(발생) 고리 + 꺾쇠 넷 + 바깥 빛</summary>
        void DrawTarget(Vector2 g, float rGui, Color c, bool dashed, float a, string label)
        {
            float pulse = 1 + 0.06f * Mathf.Sin(Time.unscaledTime * 9);
            float r = rGui * pulse;
            var rr = new Rect(g.x - r, g.y - r, r * 2, r * 2);
            GUI.color = new Color(c.r, c.g, c.b, (dashed ? 0.1f : 0.2f) * a); GUI.DrawTexture(new Rect(g.x - r - 14, g.y - r - 14, r * 2 + 28, r * 2 + 28), texDisc);   // 은은한 바탕 빛
            GUI.color = new Color(c.r, c.g, c.b, a);
            if (dashed) GUI.DrawTexture(rr, texDash);   // 회전은 화면 배율과 겹쳐 자리가 어긋났다 — 맥박만
            else GUI.DrawTexture(rr, texRing);
            float k = r * 0.78f, L = Mathf.Max(8, r * 0.22f), th = dashed ? 2 : 3;
            for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2)
            {
                float cx = g.x + sx * k, cy = g.y + sy * k;
                GUI.DrawTexture(new Rect(sx > 0 ? cx - L : cx, cy - th / 2, L, th), white);
                GUI.DrawTexture(new Rect(cx - th / 2, sy > 0 ? cy - L : cy, th, L), white);
            }
            if (!string.IsNullOrEmpty(label))
            {
                string t = "<size=22><b>" + label + "</b></size>"; float w = pop.CalcSize(new GUIContent(t)).x + 20;
                var lb = new Rect(Mathf.Clamp(g.x - w / 2, 4, vw - w - 4), Mathf.Max(34, g.y - r - 40), w, 32);
                GUI.color = new Color(0.04f, 0.02f, 0.03f, 0.85f * a); GUI.DrawTexture(lb, white);
                GUI.color = Color.white; Frame(lb, new Color(c.r, c.g, c.b, a), 2);
                pop.fontSize = 22; pop.normal.textColor = new Color(c.r, c.g, c.b, a); Lbl(lb, t, pop);
            }
            GUI.color = Color.white;
        }

        // ───────────────────────────────── 🎬 10-10 「뭐가 나온다 하면 진짜 뭐가 나와야」(사장님) — 사건마다 등장하는 것
        // 충돌 · 대충돌 = 있는 잔해 그림 둘이 다가와 부딪쳐 폭발 · 보급선 = 셔틀이 연료통을 놓고 감 · 폭풍 = 부서진 화물선이 고철을 쏟음 · 호송대 = 예인선이 앞장
        static Texture2D[] exTex; static Texture2D tShuttle, tWreck, tTug, tSatA, tSatB, tStation, tBig;
        float actWarnAt = -99, actGoAt = -99; int actEv = -1; Vector2 actPos; float actR; float tugAt = -99; Vector2 tugStart; int tugDir = 1; bool tugFuel;
        static Texture2D LoadTex(string p) => Resources.Load<Texture2D>(p);
        void EnsureActTex()
        {
            if (exTex != null) return;
            exTex = new Texture2D[9]; for (int i = 0; i < 9; i++) exTex[i] = LoadTex("anim/explode_" + i);
            tShuttle = LoadTex("event/ev_shuttle"); tWreck = LoadTex("event/ev_wreck"); tTug = LoadTex("event/ev_tug");
            tSatA = LoadTex("junk/junk_sat"); tSatB = LoadTex("junk/junk_sat_comm"); tStation = LoadTex("junk/junk_big_station"); tBig = LoadTex("junk/junk_big");
        }
        /// <summary>시뮬 자리 p 에 크기 size(시뮬 px) 로 — flip 이면 좌우 뒤집기</summary>
        void ActSprite(Texture2D t, Vector2 p, float size, bool flip, float a)
        {
            if (t == null || a <= 0.01f) return;
            var g = SimGui(p); float s = Mathf.Abs(SimGui(p + new Vector2(size, 0)).x - g.x);
            GUI.color = new Color(1, 1, 1, a);
            GUI.DrawTextureWithTexCoords(new Rect(g.x - s / 2, g.y - s / 2, s, s), t, flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
            GUI.color = Color.white;
        }
        static float EaseIn(float k) { k = Mathf.Clamp01(k); return k * k; }
        void EventActors()
        {
            EnsureActTex();
            float now = Time.unscaledTime, tw = now - actWarnAt, tg = now - actGoAt;
            bool going = actGoAt > actWarnAt;
            switch (actEv)
            {
                case 1: case 4:
                {   // 💥 둘이 양쪽에서 다가와 부딪친다 → 폭발
                    bool big = actEv == 4; float sz = big ? 120 : 58, far = big ? 330 : 240;
                    if (!going && tw < 2.4f)
                    {
                        float k = EaseIn(tw / 2f);
                        ActSprite(big ? tStation : tSatA, actPos + new Vector2(-far, -far * 0.22f) * (1 - k), sz, false, Mathf.Clamp01(tw / 0.3f));
                        ActSprite(big ? tBig : tSatB, actPos + new Vector2(far, far * 0.18f) * (1 - k), sz, true, Mathf.Clamp01(tw / 0.3f));
                    }
                    if (going && tg < 0.75f)
                    {
                        int f = Mathf.Clamp((int)(tg / 0.75f * 9), 0, 8);
                        ActSprite(exTex[f], actPos, Mathf.Min(actR * 2.2f, 190), false, 1);
                    }
                    break;
                }
                case 2:
                {   // 🌪 부서진 화물선이 왼쪽 끝에서 흔들리며 고철을 쏟는다 (폭풍 40개 · 약 5초)
                    float life = going ? 5.5f - tg : 99, a = Mathf.Clamp01(tw / 0.4f) * Mathf.Clamp01(life / 0.6f);
                    if (a > 0 && (!going || tg < 5.5f))
                    {
                        float shake = going ? 3f : 1.2f;
                        var p = new Vector2(36 + Mathf.Sin(now * 31) * shake, actPos.y + Mathf.Sin(now * 23) * shake);
                        ActSprite(tWreck, p, 118, false, a);
                    }
                    break;
                }
            }
            // 🚚 호송대 앞 예인선 — 행렬과 같은 속도 190px/초
            if (now - tugAt < 7f)
            {
                float t = now - tugAt;
                if (tugFuel) { var p = tugStart + new Vector2(tugDir * 170 * t, -28); ActSprite(tShuttle, p, 78, tugDir < 0, Mathf.Clamp01(t / 0.3f)); }   // ⛽ 연료통을 싣고 가는 셔틀
                else { var p = tugStart + new Vector2(tugDir * (90 + 190 * t), 0); ActSprite(tTug, p, 84, tugDir < 0, Mathf.Clamp01(t / 0.3f)); }
            }
        }

        void EventFx()
        {
            if (sim.R.over) { evWarnT = evGoT = 0; evMarks.Clear(); actEv = -1; tugAt = -99; return; }
            EnsureEvTex();
            EventActors();
            float dt = Time.unscaledDeltaTime;
            // ② 발생 — 표적 · 이름표 · 가장자리 빛
            if (evGoT > 0)
            {
                evGoT -= dt; var c = EvCol(evGoEv); float a = Mathf.Clamp01(evGoT / 0.4f);
                if (evGoR > 0)
                {
                    var g = SimGui(evGoPos); float rg = Mathf.Abs(SimGui(evGoPos + new Vector2(evGoR, 0)).x - g.x);
                    if (!reduceMotion) { GUI.color = new Color(c.r, c.g, c.b, 0.35f * Mathf.Clamp01(evGoT - 0.6f)); GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette); }
                    DrawTarget(g, rg, c, false, a, evGoName + "!" + (evGoArrow != null ? " " + evGoArrow : ""));
                }
            }
            // 작은 표적 (소행성 · 운석)
            for (int i = evMarks.Count - 1; i >= 0; i--)
            {
                var m = evMarks[i]; m.t -= dt; if (m.t <= 0) { evMarks.RemoveAt(i); continue; }
                var g = SimGui(m.p); float rg = Mathf.Abs(SimGui(m.p + new Vector2(m.r, 0)).x - g.x);
                DrawTarget(g, rg, m.c, false, Mathf.Clamp01(m.t / 0.4f), m.label);
            }
            // ① 예고 띠 — 위 계기 줄 아래. 처음 0.2초 펼쳐지고, 끝 0.3초 위로 접힌다
            if (evWarnT > 0)
            {
                evWarnT -= dt; var c = EvCol(evWarnEv);
                float age = 2.2f - evWarnT, open = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01(evWarnT / 0.3f);
                if (evWarnR > 0)
                {
                    var g = SimGui(evWarnPos); float rg = Mathf.Abs(SimGui(evWarnPos + new Vector2(evWarnR, 0)).x - g.x);
                    DrawTarget(g, rg, c, true, Mathf.Clamp01(age / 0.3f), null);
                }
                float h = 64 * open, y0 = 46;
                if (h > 1)
                {
                    var band = new Rect(0, y0 + (64 - h) / 2, vw, h);
                    GUI.color = new Color(0.04f, 0.02f, 0.03f, 0.8f); GUI.DrawTexture(band, white);
                    GUI.color = new Color(c.r, c.g, c.b, 0.28f);
                    float sw = 210, sc = Time.unscaledTime * 1.5f;
                    GUI.DrawTextureWithTexCoords(new Rect(0, band.y, sw, h), texStripe, new Rect(sc, 0, sw / 32f, h / 32f));
                    GUI.DrawTextureWithTexCoords(new Rect(vw - sw, band.y, sw, h), texStripe, new Rect(-sc, 0, sw / 32f, h / 32f));
                    GUI.color = c; GUI.DrawTexture(new Rect(0, band.y, vw, 3), white); GUI.DrawTexture(new Rect(0, band.yMax - 3, vw, 3), white);
                    GUI.color = new Color(1, 1, 1, open);
                    if (open > 0.6f)
                    {
                        bool blink = Mathf.Sin(Time.unscaledTime * 14) > -0.4f;
                        string nm = "<size=30><b><color=#" + ColorUtility.ToHtmlStringRGB(blink ? c : Color.Lerp(c, Color.white, 0.4f)) + ">!! " + evWarnName + "</color></b></size>";
                        center.fontSize = 13; float nw = center.CalcSize(new GUIContent(nm)).x;
                        Lbl(new Rect(vw / 2 - 360, band.y, 720, 40), nm, center);
                        string hn = Fit("<color=#f5efe5>" + evWarnHint + "</color>", 14, 700, center, 10);
                        Lbl(new Rect(vw / 2 - 360, band.y + 38, 720, 22), hn, center);
                        if (open >= 1) { var bx = new Rect(vw / 2 - 340, band.y, 680, band.height - 3); ProbeIn(bx, new Rect(vw / 2 - 360, band.y, 720, 40), nm, center); ProbeIn(bx, new Rect(vw / 2 - 360, band.y + 38, 720, 22), hn, center); }   // 🔍 띠 가운데 680 (양옆 줄무늬 · 카운트다운 자리)
                        // 카운트다운 2 → 1
                        int cd = Mathf.Max(1, Mathf.CeilToInt(evWarnT - 0.2f));
                        var cr = new Rect(vw / 2 + Mathf.Min(340, nw / 2 + 18), band.y + 6, 34, 34);
                        GUI.color = c; GUI.DrawTexture(cr, texRing); GUI.color = Color.white;
                        Lbl(cr, "<size=20><b><color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + cd + "</color></b></size>", center);
                    }
                    GUI.color = Color.white;
                }
            }
        }
    }
}
