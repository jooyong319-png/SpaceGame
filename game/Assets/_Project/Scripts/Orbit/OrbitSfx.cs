using System.Collections.Generic;
using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 소리 — 음원 파일 없이 코드로 합성한다 (완성도 루프 3, 2026-09-22).
    /// 도파민에 제일 크게 보탤 곳인데 그전까지 소리가 0 이었다.
    ///
    /// ⚠️ 만든 쪽(Claude)은 소리를 못 듣는다. 크기 · 음색은 사장님이 들어보고 판정하신다.
    ///    값은 전부 이 파일 위쪽 Make(...) 줄에 있다. M 키 / Esc 메뉴로 끈다.
    /// ⚠️ 씬에 AudioListener 가 없으면 소리가 안 난다 — 없으면 카메라에 붙인다 (Wiki_gm verification ⑧).
    /// </summary>
    public class OrbitSfx : MonoBehaviour
    {
        public static OrbitSfx I;
        const int Rate = 44100;
        const string MuteKey = "orbit.mute";

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        int next;
        public bool Muted { get; private set; }

        void Awake()
        {
            I = this;
            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            var cam = Camera.main;
            if (FindFirstObjectByType<AudioListener>() == null && cam != null) cam.gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < 12; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                voices.Add(src);
            }
            var rng = new System.Random(3);
            float N() => (float)(rng.NextDouble() * 2 - 1);

            // 이름 · 길이(초) · 파형
            Make("pick", 0.07f, t => Sweep(t, 900, 1500, 0.07f) * Env(t, 0.004f, 0.03f) * 0.5f);
            Make("tick", 0.03f, t => N() * Env(t, 0.001f, 0.008f) * 0.35f);
            Make("coin", 0.16f, t => (Mathf.Sin(6.2832f * 1568 * t) + 0.6f * Mathf.Sin(6.2832f * 2093 * t)) * Env(t, 0.002f, 0.05f) * 0.35f);
            Make("blast", 0.32f, t => (Sweep(t, 140, 45, 0.32f) * 0.8f + N() * 0.5f * Env(t, 0.001f, 0.05f)) * Env(t, 0.003f, 0.09f));
            Make("clank", 0.14f, t => (Square(t, 230) * 0.4f + Mathf.Sin(6.2832f * 1310 * t) * 0.3f + N() * 0.3f) * Env(t, 0.001f, 0.04f) * 0.6f);
            Make("break", 0.6f, t => (N() * 0.7f + Sweep(t, 110, 35, 0.6f) * 0.6f) * Env(t, 0.004f, 0.18f));
            Make("collide", 0.35f, t => (N() * 0.6f + Sweep(t, 90, 40, 0.35f) * 0.4f) * Env(t, 0.002f, 0.1f) * 0.8f);
            Make("buy", 0.2f, t => (t < 0.08f ? Mathf.Sin(6.2832f * 660 * t) : Mathf.Sin(6.2832f * 990 * t)) * Env(t % 0.08f, 0.003f, 0.05f) * 0.35f);
            Make("unit", 0.9f, t => Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.09f) * Env(t, 0.005f, 0.45f) * 0.4f);
            Make("warn", 0.8f, t => Square(t, t % 0.4f < 0.2f ? 740 : 560) * 0.25f * Env(t % 0.4f, 0.005f, 0.15f));
            Make("lock", 1.4f, t => (Sweep(t, 70, 28, 1.4f) * 0.9f + N() * 0.5f * Env(t, 0.002f, 0.25f)) * Env(t, 0.005f, 0.5f));
            Make("cine", 2.4f, t => (Sweep(t, 55, 30, 2.4f) * 0.8f + N() * 0.35f * Env(t, 0.01f, 0.6f) + Mathf.Sin(6.2832f * 110 * t) * 0.2f * Mathf.Clamp01(t)) * Env(t, 0.01f, 1.1f));
            // rev17 — 지구 보급 · 붕괴 경고 (2026-09-23)
            Make("launch", 1.1f, t => (Sweep(t, 55, 480, 1.1f) * 0.45f + N() * 0.4f * Env(t, 0.04f, 0.45f)) * Env(t, 0.06f, 0.55f) * 0.6f);   // 출발
            Make("heart", 0.32f, t => (Mathf.Sin(6.2832f * 55 * t) * Env(t, 0.005f, 0.06f) + Mathf.Sin(6.2832f * 48 * t) * Env(Mathf.Max(0, t - 0.14f), 0.005f, 0.07f) * (t > 0.14f ? 1 : 0)) * 0.9f);   // 경매 — 두근
            Make("crash", 0.9f, t => (N() * 0.6f * Env(t, 0.002f, 0.25f) + Sweep(t, 900, 60, 0.9f) * 0.4f) * Env(t, 0.003f, 0.5f) * 0.7f);   // 경매 — 폭락
            Make("supply", 0.5f, t => (Sweep(t, 300, 1100, 0.5f) * 0.5f + N() * 0.25f * Env(t, 0.01f, 0.12f)) * Env(t, 0.02f, 0.22f) * 0.5f);
            Make("grab", 0.22f, t => (Mathf.Sin(6.2832f * 880 * t) * 0.5f + Mathf.Sin(6.2832f * 1320 * t) * 0.4f) * Env(t, 0.003f, 0.08f) * 0.45f);
            Make("danger", 0.5f, t => (Square(t, t % 0.25f < 0.125f ? 320 : 250) * 0.3f + Sweep(t, 180, 120, 0.5f) * 0.3f) * Env(t % 0.25f, 0.004f, 0.1f) * 0.7f);
            Make("ending", 2.2f, t => (Mathf.Sin(6.2832f * 262 * t) + Mathf.Sin(6.2832f * 330 * t) + Mathf.Sin(6.2832f * 392 * t) + 0.5f * Mathf.Sin(6.2832f * 523 * t)) * 0.18f * Env(t, 0.08f, 1.0f));
        }

        // ───────────────────────────────── 재생

        /// <summary>
        /// minGap = 같은 소리를 이 초 안에 다시 안 낸다 (초당 수십 번 터지는 장르라 안 막으면 소음이다 —
        /// Wiki_gm: 이 장르는 이벤트 빈도가 다른 장르의 10배).
        /// </summary>
        public static void Play(string name, float volume = 1f, float minGap = 0.03f, float pitchJitter = 0.06f)
        {
            if (I == null || I.Muted || !I.clips.TryGetValue(name, out var clip)) return;
            float now = Time.unscaledTime;
            if (I.lastAt.TryGetValue(name, out var last) && now - last < minGap) return;
            I.lastAt[name] = now;
            var v = I.voices[I.next];
            I.next = (I.next + 1) % I.voices.Count;
            v.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            v.PlayOneShot(clip, volume * 0.8f);
        }

        /// <summary>음높이를 정해서 (경매 띡띡 — 오를수록 높게)</summary>
        public static void PlayPitch(string name, float volume, float pitch)
        {
            if (I == null || I.Muted || !I.clips.TryGetValue(name, out var clip)) return;
            var v = I.voices[I.next];
            I.next = (I.next + 1) % I.voices.Count;
            v.pitch = pitch;
            v.PlayOneShot(clip, volume * 0.8f);
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
            PlayerPrefs.Save();
            if (Muted) foreach (var v in voices) v.Stop();
        }

        // ───────────────────────────────── 합성

        void Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            // 끝 5ms 는 0 으로 — 뚝 끊기는 딸깍 소리를 없앤다
            int tail = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < tail; i++) data[n - 1 - i] *= i / (float)tail;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        static float Sweep(float t, float f0, float f1, float len)
        {
            float k = (f1 - f0) / len;
            return Mathf.Sin(6.2832f * (f0 * t + 0.5f * k * t * t));
        }
        static float Square(float t, float f) => Mathf.Sin(6.2832f * f * t) >= 0 ? 1f : -1f;
        static float Arp(float t, float[] notes, float step)
        {
            float s = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float tt = t - i * step;
                if (tt < 0) break;
                s += Mathf.Sin(6.2832f * notes[i] * tt) * Mathf.Exp(-tt / 0.35f);
            }
            return s * 0.5f;
        }
    }
}
