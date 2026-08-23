using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 타격감 담당. 화면 흔들림 + **코드로 만든 소리**.
    ///
    /// 🔴 아트도 사운드 에셋도 없는 그레이박스에서 "손맛이 있나"를 판정하려면
    ///    최소한의 피드백은 있어야 한다. 소리가 손맛의 절반인데 그게 0이면
    ///    구조가 좋아도 밋밋하게 느껴진다 — 2026-08-20 첫 플레이가 정확히 그랬다.
    ///
    /// 파형을 런타임에 생성하므로 오디오 파일이 필요 없다. 아트 단계에서 진짜 소리로 교체한다.
    /// </summary>
    public class Juice : MonoBehaviour
    {
        public static Juice Instance { get; private set; }

        public Camera cam;

        AudioSource[] sources;
        int nextSource;

        AudioClip clipChip;
        AudioClip clipBreak;
        AudioClip clipPickup;
        AudioClip clipLevel;
        AudioClip clipTick;
        AudioClip clipFanfare;
        AudioClip clipAlarm;

        /// <summary>
        /// 🔴 흔들림 오프셋. **카메라를 직접 움직이지 않는다** —
        ///    CameraFollow가 추적 위치를 정한 뒤 마지막에 이걸 더한다.
        ///    (직접 움직이면 추적과 싸우고, 조준까지 흔들려 배가 튄다)
        /// </summary>
        public Vector3 ShakeOffset { get; private set; }

        float shake;
        float shakeDecay = 14f;

        /// <summary>
        /// 🔴 흔들림 총량 배율. 0이면 완전히 끈다 (F3).
        ///    이 장르는 초당 수십 개가 터진다 — 한 번의 흔들림이 작아도 **겹쳐서 멀미가 난다.**
        ///    2026-08-21 실제 피드백: "화면이 너무 흔들리고 어지러움".
        /// </summary>
        public static float ShakeScale = 1f;

        /// <summary>겹쳐도 이 이상은 안 흔들린다. 상한이 곧 멀미 한계선이다.</summary>
        const float ShakeCap = 0.22f;

        const int SampleRate = 44100;

        void Awake()
        {
            Instance = this;

            sources = new AudioSource[6];
            for (int i = 0; i < sources.Length; i++)
            {
                var go = new GameObject("SFX" + i);
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                sources[i] = a;
            }

            clipChip = MakeNoise("chip", 0.045f, 900f, 0.18f);
            clipBreak = MakeNoise("break", 0.30f, 180f, 0.55f);
            clipPickup = MakeBlip("pickup", 0.09f, 620f, 1180f, 0.22f);
            clipLevel = MakeBlip("level", 0.42f, 440f, 1320f, 0.35f);

            // 🔴 입금 연출용. 짧고 건조해야 수십 번 겹쳐도 안 지저분하다
            clipTick = MakeBlip("tick", 0.055f, 700f, 900f, 0.16f);
            clipFanfare = MakeBlip("fanfare", 0.75f, 330f, 1760f, 0.42f);
            clipAlarm = MakeBlip("alarm", 0.30f, 300f, 190f, 0.30f);   // 내려가는 음 = 나쁜 일
        }

        void LateUpdate()
        {
            if (hitFlash > 0f) hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 2.6f);

            // 🔴 기지 섬광은 **천천히** 꺼진다. 내 피격보다 오래 남아야
            //    "기지가 맞고 있다"가 배경 불안으로 깔린다
            if (baseFlash > 0f) baseFlash = Mathf.Max(0f, baseFlash - Time.deltaTime * 1.1f);

            if (shake <= 0.001f || ShakeScale <= 0f) { ShakeOffset = Vector3.zero; return; }

            shake = Mathf.Max(0f, shake - shakeDecay * shake * Time.deltaTime - 0.05f * Time.deltaTime);

            // 🔴 절대 시각(Time.time)을 쓰지 않는다 — 시뮬 결정론이 조용히 깨진다
            shakeClock += Time.deltaTime * 47f;
            float t = shakeClock;
            ShakeOffset = new Vector3(Mathf.Sin(t * 1.7f), Mathf.Cos(t * 2.3f), 0f) * (shake * ShakeScale);
        }

        // ---------------------------------------------------------------- 외부에서 부르는 것

        public static void Chip(float strength)
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipChip, 0.25f + strength * 0.2f, 0.9f + strength * 0.35f);
        }

        public static void Break()
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipBreak, 0.75f, Random01(0.9f, 1.15f));
            Instance.AddShake(0.030f);   // 🔴 초당 수십 번 불린다. 한 번은 거의 안 보여야 한다
        }

        public static void Pickup()
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipPickup, 0.35f, Random01(0.95f, 1.25f));
        }

        public static void LevelUp()
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipLevel, 0.7f, 1f);
            Instance.AddShake(0.10f);
        }

        /// <summary>
        /// 🔴 입금 카운터가 한 칸 떨어질 때마다. **음이 올라간다.**
        ///    이게 입금 연출의 심장이다 — 값이 아니라 *상승*이 도파민을 만든다.
        ///    슬롯머신이 결과를 바로 안 보여주는 것과 같은 이유다.
        /// </summary>
        public static void DepositTick(float t01)
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipTick, 0.30f, 0.85f + Mathf.Clamp01(t01) * 1.15f);
        }

        /// <summary>입금이 끝났다. 만재였으면 더 크게 터진다.</summary>
        public static void DepositDone(bool full)
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipFanfare, full ? 0.85f : 0.5f, full ? 1f : 1.25f);
            Instance.AddShake(full ? 0.14f : 0.06f);
        }

        /// <summary>
        /// 🔴 기지가 맞았다. **입금 도파민의 반대편 추다.**
        ///    보상만 키우면 "꽉 채울 때까지 안 돌아간다"가 정답이 되어
        ///    이 게임의 세 번째 결정이 죽는다. 채우는 동안 기지가 맞고 있다는 게
        ///    귀로 들려야 "한 번만 더 주울까"가 진짜 고민이 된다.
        /// </summary>
        public static void BaseAlarm()
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipAlarm, 0.55f, Random01(0.95f, 1.05f));
            Instance.baseFlash = 1f;
        }

        /// <summary>기지 피격 섬광 0~1. HUD가 읽어 화면 가장자리를 붉게 물들인다.</summary>
        public float baseFlash;

        public static void Fanfare(float volume, float pitch)
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipFanfare, volume, pitch);
        }

        public static void Contact()
        {
            if (Instance == null) return;
            Instance.Play(Instance.clipBreak, 0.5f, 0.6f);
            Instance.AddShake(0.14f);   // 맞은 건 알려야 하니 파괴보다 크게
            Instance.hitFlash = 1f;     // 🔴 맞은 걸 화면으로 알려준다
        }

        /// <summary>피격 섬광 0~1. HUD가 읽어 붉은 비네트를 그린다.</summary>
        public float hitFlash;

        public void AddShake(float amount) => shake = Mathf.Min(ShakeCap, shake + amount);

        float shakeClock;

        // ---------------------------------------------------------------- 내부

        void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || sources == null) return;
            var a = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;

            a.pitch = pitch;
            a.volume = volume;
            a.clip = clip;
            a.Play();
        }

        static float seed = 1f;
        static float Random01(float min, float max)
        {
            seed += 0.618f;
            float v = seed * 12.9898f;
            v = v - Mathf.Floor(v);
            return Mathf.Lerp(min, max, v);
        }

        /// <summary>짧은 노이즈 버스트 — 깎는 소리, 부서지는 소리.</summary>
        static AudioClip MakeNoise(string name, float seconds, float lowpassHz, float peak)
        {
            int n = Mathf.Max(16, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[n];

            float rnd = 0.37f;
            float last = 0f;
            float k = Mathf.Clamp01(lowpassHz / (SampleRate * 0.5f));

            for (int i = 0; i < n; i++)
            {
                rnd = rnd * 16807f % 1f;
                float white = rnd * 2f - 1f;
                last += (white - last) * k;                 // 간단한 로우패스
                float env = Mathf.Pow(1f - i / (float)n, 2.5f);  // 감쇠
                data[i] = last * env * peak;
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>피치가 올라가는 짧은 사인 — 파편 흡수, 레벨업.</summary>
        static AudioClip MakeBlip(string name, float seconds, float fromHz, float toHz, float peak)
        {
            int n = Mathf.Max(16, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[n];
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float hz = Mathf.Lerp(fromHz, toHz, t * t);
                phase += hz / SampleRate * Mathf.PI * 2f;

                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * Mathf.Pow(1f - t, 0.6f);
                data[i] = Mathf.Sin(phase) * env * peak;
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
