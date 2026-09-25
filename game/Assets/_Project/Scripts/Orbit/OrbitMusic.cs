using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🎵 배경음 — 효과음처럼 음원 파일 없이 코드로 합성한다 (09-25 사장님 「다 괜찮은데」 · 시안 https://claude.ai/artifact/X6wDBDgffM3ywsfvojuYez).
    /// 여섯 곡을 켤 때 뒤 스레드에서 한 번 만들어 두고, 곡이 바뀌면 1.2초 겹쳐 넘긴다.
    /// ⚠️ 만든 쪽(Claude)은 소리를 못 듣는다 — 음색 · 크기는 사장님이 들어 보고 정하신다. 곡 데이터는 아래 Tracks.
    /// </summary>
    public class OrbitMusic : MonoBehaviour
    {
        public static OrbitMusic I;
        public static float Vol = 0.6f;                                  // ⚙ 설정 — 배경음
        const int Rate = 32000;

        class Part { public string i; public Func<int, System.Random, bool> at; public bool chord, root, arp, pent, octJump, tri; public int[] mel, pat; public int oct, len = 1; public float vol; }
        class Track { public string id; public int bpm, bars; public (int root, int[] kind)[] prog; public Part[] parts; }

        static readonly int[] Maj = { 0, 4, 7 }, Min = { 0, 3, 7 }, Maj7 = { 0, 4, 7, 11 }, Min7 = { 0, 3, 7, 10 }, Sus2 = { 0, 2, 7 };
        static Part P(string i, Func<int, System.Random, bool> at, float vol, int oct = 0, int len = 1) => new Part { i = i, at = at, vol = vol, oct = oct, len = len };

        static readonly Track[] Tracks =
        {
            new Track { id = "cockpit", bpm = 76, bars = 8, prog = new[] { (57, Min7), (53, Maj7), (60, Maj7), (55, Sus2) }, parts = new[] {
                With(P("pad", (s, r) => s == 0, .10f, 0, 16), p => p.chord = true),
                With(P("bell", (s, r) => s % 4 == 2 && r.NextDouble() < .55, .05f, 12, 2), p => p.arp = true),
                With(P("sub", (s, r) => s == 0, .10f, -12, 16), p => p.root = true) } },
            new Track { id = "runA", bpm = 132, bars = 8, prog = new[] { (57, Min), (53, Maj), (60, Maj), (55, Maj) }, parts = new[] {
                With(P("square", (s, r) => true, .045f, 12, 1), p => p.arp = true),
                With(P("tri", (s, r) => s % 2 == 0, .16f, -12, 2), p => p.root = true),
                P("kick", (s, r) => s % 8 == 0, .5f), P("hat", (s, r) => s % 2 == 1, .05f), P("snare", (s, r) => s % 8 == 4, .12f) } },
            new Track { id = "runB", bpm = 108, bars = 8, prog = new[] { (50, Min), (46, Maj), (53, Maj), (48, Maj) }, parts = new[] {
                With(P("saw", (s, r) => true, .07f, -12, 1), p => { p.root = true; p.octJump = true; }),
                With(P("pad", (s, r) => s == 0, .06f, 12, 16), p => p.chord = true),
                With(P("lead", (s, r) => s == 0 || s == 3 || s == 6 || s == 10 || s == 12, .05f, 24, 2), p => p.mel = new[] { 0, 3, 7, 5, 3 }),
                P("kick", (s, r) => s % 4 == 0, .45f), P("snare", (s, r) => s % 8 == 4, .14f), P("hat", (s, r) => s % 2 == 1, .04f) } },
            new Track { id = "runC", bpm = 90, bars = 8, prog = new[] { (52, Sus2), (50, Sus2), (55, Sus2), (57, Min) }, parts = new[] {
                With(P("bell", (s, r) => r.NextDouble() < .28, .05f, 12, 3), p => p.pent = true),
                With(P("drone", (s, r) => s == 0, .12f, -12, 16), p => p.root = true),
                With(P("pad", (s, r) => s == 0, .05f, 0, 16), p => p.chord = true) } },
            new Track { id = "due", bpm = 100, bars = 4, prog = new[] { (50, Min), (49, Maj) }, parts = new[] {
                With(P("pulse", (s, r) => s % 2 == 0, .12f, -12, 1), p => { p.root = true; p.pat = new[] { 0, 0, 12, 0, 3, 0, 12, 5 }; }),
                P("tick", (s, r) => s % 4 == 0, .06f),
                With(P("pad", (s, r) => s == 0, .05f, 0, 16), p => p.chord = true) } },
            new Track { id = "end", bpm = 84, bars = 8, prog = new[] { (60, Maj), (55, Maj), (57, Min), (53, Maj) }, parts = new[] {
                With(P("pad", (s, r) => s == 0, .09f, 0, 16), p => p.chord = true),
                With(P("lead", (s, r) => s % 4 == 0, .06f, 12, 4), p => { p.mel = new[] { 7, 4, 2, 0, 4, 7, 9, 7 }; p.tri = true; }),
                With(P("tri", (s, r) => s % 4 == 0, .14f, -12, 4), p => p.root = true),
                P("kick", (s, r) => s % 8 == 0, .2f) } },
        };
        static Part With(Part p, Action<Part> f) { f(p); return p; }

        // ───────────────────────── 합성 (시안 페이지 WebAudio 와 같은 규칙 · 끝은 앞으로 감아 이음새 없이)
        static float Note(int n) => 440f * Mathf.Pow(2f, (n - 69) / 12f);
        static float[] Render(Track tr)
        {
            double sp = 60.0 / tr.bpm / 4;
            int steps = tr.bars * 16, len = (int)(steps * sp * Rate);
            var buf = new float[len];
            var rng = new System.Random(tr.id.GetHashCode() & 0xffff);
            int melI = 0;
            for (int st = 0; st < steps; st++)
            {
                int s = st % 16, bar = st / 16;
                var (root, kind) = tr.prog[bar % tr.prog.Length];
                double t = st * sp;
                foreach (var p in tr.parts)
                {
                    if (!p.at(s, rng)) continue;
                    double dur = p.len * sp;
                    switch (p.i)
                    {
                        case "kick": Kick(buf, t, p.vol); continue;
                        case "hat": Noise(buf, t, .03, p.vol, 7000, rng); continue;
                        case "snare": Noise(buf, t, .12, p.vol, 1500, rng); continue;
                        case "tick": Noise(buf, t, .015, p.vol, 9000, rng); continue;
                    }
                    var notes = new List<int>();
                    if (p.chord) foreach (var k in kind) notes.Add(root + k);
                    else if (p.root) { int n = root; if (p.pat != null) n += p.pat[s % p.pat.Length]; if (p.octJump && s % 2 == 1) n += 12; notes.Add(n); }
                    else if (p.arp) notes.Add(root + kind[s % kind.Length] + (s % 8 >= 4 ? 12 : 0));
                    else if (p.pent) { int[] pen = { 0, 2, 4, 7, 9 }; notes.Add(root + pen[rng.Next(5)] + (rng.NextDouble() < .4 ? 12 : 0)); }
                    else if (p.mel != null) notes.Add(root + p.mel[melI++ % p.mel.Length]);
                    foreach (int n0 in notes)
                    {
                        float f = Note(n0 + p.oct), per = notes.Count > 0 ? 1f / notes.Count : 1f;
                        switch (p.i)
                        {
                            case "pad": Osc(buf, 3, f, t, .35, dur, p.vol * per * 2, 900); Osc(buf, 1, f * 1.005f, t, .4, dur, p.vol * per, 0); break;
                            case "sub": case "drone": Osc(buf, 0, f, t, .5, dur, p.vol, 0); break;
                            case "bell": Osc(buf, 0, f, t, .005, dur + .6, p.vol, 0); Osc(buf, 0, f * 2.01f, t, .005, dur * .5, p.vol * .3f, 0); break;
                            case "square": Osc(buf, 2, f, t, .003, dur * .9, p.vol, 3200); break;
                            case "tri": Osc(buf, 1, f, t, .005, dur * .9, p.vol, 0); break;
                            case "saw": Osc(buf, 3, f, t, .004, dur * .8, p.vol, 700); break;
                            case "lead": if (p.tri) Osc(buf, 1, f, t, .01, dur, p.vol, 0); else Osc(buf, 2, f, t, .01, dur, p.vol, 2400); break;
                            case "pulse": Osc(buf, 2, f, t, .003, dur * .7, p.vol, 900); break;
                        }
                    }
                }
            }
            // 메아리 (0.33초 · 되먹임 0.28 · 0.22) — 두 바퀴 돌려 끝 → 앞 이음새도 채운다
            int dl = (int)(.33 * Rate); var line = new float[dl]; int w = 0; var wet = new float[len];
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < len; i++) { float d = line[w]; line[w] = buf[i] + d * .28f; w = (w + 1) % dl; if (pass == 1) wet[i] = d * .22f; }
            for (int i = 0; i < len; i++) buf[i] = Mathf.Clamp((buf[i] + wet[i]) * .8f, -1f, 1f);
            return buf;
        }
        // 파형 0 사인 · 1 삼각 · 2 사각 · 3 톱니, cut > 0 이면 한 겹 저역 통과
        static void Osc(float[] buf, int wave, float f, double t0, double a, double d, float peak, float cut)
        {
            int len = buf.Length, i0 = (int)(t0 * Rate), n = (int)((a + d + .05) * Rate);
            double ph = 0, inc = f / (double)Rate; float lp = 0, k = cut > 0 ? 1f - Mathf.Exp(-2f * Mathf.PI * cut / Rate) : 1f;
            double lnk = Math.Log(.0001);
            for (int j = 0; j < n; j++)
            {
                double tt = j / (double)Rate;
                float g = tt < a ? (float)(peak * tt / a) : (float)(peak * Math.Exp(lnk * Math.Min(1, (tt - a) / d)));
                float x; double fr = ph - Math.Floor(ph);
                switch (wave)
                {
                    case 0: x = (float)Math.Sin(ph * 2 * Math.PI); break;
                    case 1: x = (float)(fr < .5 ? fr * 4 - 1 : 3 - fr * 4); break;
                    case 2: x = fr < .5 ? 1 : -1; break;
                    default: x = (float)(fr * 2 - 1); break;
                }
                lp += (x - lp) * k;
                buf[(i0 + j) % len] += lp * g;
                ph += inc;
            }
        }
        static void Noise(float[] buf, double t0, double d, float vol, float hp, System.Random rng)
        {
            int len = buf.Length, i0 = (int)(t0 * Rate), n = (int)((d + .05) * Rate); float prev = 0, y = 0, k = Mathf.Exp(-2f * Mathf.PI * hp / Rate);
            double lnk = Math.Log(.0001);
            for (int j = 0; j < n; j++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1); y = k * (y + x - prev); prev = x;
                double tt = j / (double)Rate; float g = (float)(vol * Math.Exp(lnk * Math.Min(1, tt / d)));
                buf[(i0 + j) % len] += y * g;
            }
        }
        static void Kick(float[] buf, double t0, float vol)
        {
            int len = buf.Length, i0 = (int)(t0 * Rate), n = (int)(.25 * Rate); double ph = 0, lnk = Math.Log(.0001);
            for (int j = 0; j < n; j++)
            {
                double tt = j / (double)Rate, f = 140 * Math.Pow(40.0 / 140, Math.Min(1, tt / .12));
                float g = (float)(vol * Math.Exp(lnk * Math.Min(1, tt / .18)));
                buf[(i0 + j) % len] += (float)Math.Sin(ph * 2 * Math.PI) * g; ph += f / Rate;
            }
        }

        // ───────────────────────── 재생
        readonly Dictionary<string, float[]> ready = new Dictionary<string, float[]>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource a, b; string playing, want; float fade = 1;

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("OrbitMusic"); I = go.AddComponent<OrbitMusic>();
        }
        void Awake()
        {
            I = this;
            Vol = PlayerPrefs.GetFloat("orbit.bgm", 0.6f);
            a = gameObject.AddComponent<AudioSource>(); b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b }) { s.loop = true; s.playOnAwake = false; s.volume = 0; }
            var th = new Thread(() =>
            {
                foreach (var tr in Tracks)
                {
                    var data = Render(tr);
                    lock (ready) ready[tr.id] = data;
                }
            }) { IsBackground = true };
            th.Start();
        }
        public static void Want(string id) { if (I != null) I.want = id; }
        AudioClip Clip(string id)
        {
            if (clips.TryGetValue(id, out var c)) return c;
            float[] data; lock (ready) if (!ready.TryGetValue(id, out data)) return null;
            c = AudioClip.Create("bgm_" + id, data.Length, 1, Rate, false); c.SetData(data, 0); clips[id] = c; return c;
        }
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (want != null && want != playing)
            {
                var c = Clip(want);
                if (c != null) { var t = a; a = b; b = t; a.clip = c; a.time = 0; a.Play(); playing = want; fade = 0; }
            }
            fade = Mathf.Min(1, fade + dt / 1.2f);
            bool mute = OrbitSfx.I != null && OrbitSfx.I.Muted;
            float v = mute ? 0 : Vol * 0.55f;
            a.volume = v * fade; b.volume = v * (1 - fade);
            if (fade >= 1 && b.isPlaying) b.Stop();
        }
    }
}
