using System.Collections.Generic;
using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 이펙트 풀.
    ///
    /// 🔴 **왜 만드는가**: 무기 12종이 전부 "흰 사각형을 키우고 색 입히기"였다.
    ///    2026-08-22 플레이 피드백 — *"번개 같은 무기는 마음에 들었는데 나머지는 특징이 없다"*.
    ///
    ///    번개만 좋았던 이유는 분명하다. **대상과 대상 사이에 선을 그리기 때문**이다.
    ///    무엇을 때리고 있는지가 화면에 남는다.
    ///
    /// 🔴 기준: **무기는 "내가 무엇을 했는가"를 화면에 남겨야 한다.**
    ///    숫자가 아니라 자국이 남아야 한다 — 선, 고리, 잔상, 빨려드는 입자.
    ///
    /// 종류마다 사라지는 방식이 다르다(커지며 옅어지는가 / 줄며 사라지는가)는 것이
    /// 이 클래스가 하는 일의 전부다.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }

        public Sprite square;     // 선 · 막대
        public Sprite ring;       // 충격파 고리
        public Sprite glow;       // 번짐 · 스파크
        public Sprite shard;      // 빨려드는 입자

        enum Kind { Fade, Ring, Spark, Streak, Mote }

        class Item
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Kind kind;
            public float life, maxLife;
            public Vector2 vel;
            public Vector3 startScale, endScale;
            public Color color;
            public Transform follow;      // Mote가 쫓아가는 대상
        }

        readonly List<Item> pool = new List<Item>();

        void Awake() => Instance = this;

        // ==============================================================================
        //  바깥에서 부르는 것
        // ==============================================================================

        /// <summary>대상 사이를 잇는 선. 🔴 방전이 좋았던 이유 — 이걸 다른 무기에도 쓴다.</summary>
        public static void Line(Vector2 a, Vector2 b, Color c, float thick = 0.14f, float life = 0.16f)
        {
            if (!Sane(a) || !Sane(b) || !Sane(thick, life)) { Complain("Line", $"a={a} b={b} thick={thick}"); return; }
            var it = Instance ? Instance.Take(Kind.Fade, Instance.square, c, life) : null;
            if (it == null) return;

            float len = Vector2.Distance(a, b);
            it.tr.position = (a + b) * 0.5f;
            it.tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            it.startScale = new Vector3(len, thick, 1f);
            it.endScale = new Vector3(len, thick * 0.2f, 1f);   // 가늘어지며 사라진다
        }

        /// <summary>퍼져나가는 충격파 고리. 폭발·파동에 쓴다.</summary>
        public static void Shockwave(Vector2 at, float radius, Color c, float life = 0.32f)
        {
            if (!Sane(at) || !Sane(radius, life)) { Complain("Shockwave", $"at={at} r={radius}"); return; }
            var it = Instance ? Instance.Take(Kind.Ring, Instance.ring, c, life) : null;
            if (it == null) return;

            it.tr.position = at;
            it.tr.rotation = Quaternion.identity;
            it.startScale = Vector3.one * (radius * 0.35f);
            it.endScale = Vector3.one * (radius * 2.3f);        // 커지며 옅어진다
        }

        /// <summary>터지는 빛. 명중 지점에 쓴다.</summary>
        public static void Spark(Vector2 at, float size, Color c, float life = 0.18f)
        {
            if (!Sane(at) || !Sane(size, life)) { Complain("Spark", $"at={at} size={size}"); return; }
            var it = Instance ? Instance.Take(Kind.Spark, Instance.glow, c, life) : null;
            if (it == null) return;

            it.tr.position = at;
            it.tr.rotation = Quaternion.identity;
            it.startScale = Vector3.one * (size * 1.5f);
            it.endScale = Vector3.one * (size * 0.3f);          // 줄며 사라진다
        }

        /// <summary>움직인 자리에 남는 잔상. 궤도체·부메랑에 쓴다.</summary>
        public static void Streak(Vector2 at, float angleDeg, float len, Color c, float life = 0.22f)
        {
            if (!Sane(at) || !Sane(angleDeg, len, life)) { Complain("Streak", $"at={at} len={len}"); return; }
            var it = Instance ? Instance.Take(Kind.Streak, Instance.square, c, life) : null;
            if (it == null) return;

            it.tr.position = at;
            it.tr.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            it.startScale = new Vector3(len, 0.16f, 1f);
            it.endScale = new Vector3(len * 0.7f, 0.03f, 1f);
        }

        /// <summary>
        /// 🔴 빨려드는 입자. **소용돌이와 중력 우물이 지금 아무것도 안 움직인다** —
        ///    고리만 있고 흡입이 화면에 안 보인다. 이게 그 그림을 만든다.
        /// </summary>
        public static void Mote(Vector2 from, Transform toward, Color c, float life = 0.5f)
        {
            if (!Sane(from) || !Sane(life)) { Complain("Mote", $"from={from}"); return; }
            var it = Instance ? Instance.Take(Kind.Mote, Instance.shard, c, life) : null;
            if (it == null) return;

            it.tr.position = from;
            it.tr.rotation = Quaternion.identity;
            it.startScale = Vector3.one * 0.22f;
            it.endScale = Vector3.one * 0.04f;
            it.follow = toward;
        }

        /// <summary>날아가는 것의 꼬리.</summary>
        public static void Trail(Vector2 at, Vector2 vel, Color c, float life = 0.14f)
        {
            if (!Sane(at) || !Sane(vel) || !Sane(life)) { Complain("Trail", $"at={at} vel={vel}"); return; }
            var it = Instance ? Instance.Take(Kind.Streak, Instance.square, c, life) : null;
            if (it == null) return;

            float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            it.tr.position = at;
            it.tr.rotation = Quaternion.Euler(0f, 0f, ang);
            it.startScale = new Vector3(0.8f, 0.12f, 1f);
            it.endScale = new Vector3(0.2f, 0.02f, 1f);
        }

        public static void ClearAll()
        {
            if (Instance == null) return;
            for (int i = 0; i < Instance.pool.Count; i++)
            {
                Instance.pool[i].life = 0f;
                Instance.pool[i].follow = null;
                if (Instance.pool[i].tr.gameObject.activeSelf)
                    Instance.pool[i].tr.gameObject.SetActive(false);
            }
        }

        // ==============================================================================
        //  내부
        // ==============================================================================

        /// <summary>
        /// 🔴 값이 성한지 본다.
        ///
        ///    2026-08-22 시뮬에서 `localScale`에 Infinity가 들어가 유니티가 에러를 뱉었다.
        ///    이펙트는 **게임을 망가뜨리면 안 되는 부품**이라 여기서 막는다.
        ///    다만 조용히 막으면 원인을 영영 못 찾으므로, **한 번은 크게 알린다.**
        /// </summary>
        static bool Sane(params float[] vs)
        {
            for (int i = 0; i < vs.Length; i++)
                if (float.IsNaN(vs[i]) || float.IsInfinity(vs[i])) return false;
            return true;
        }

        static bool Sane(Vector2 v) => Sane(v.x, v.y);

        static bool warned;

        static void Complain(string where, string detail)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning($"[Fx] 성하지 않은 값이 들어왔다 — {where}: {detail}" +
                             "\n     이펙트는 막았지만 **원인은 다른 곳에 있다.** 이 줄을 단서로 찾을 것.");
        }

        Item Take(Kind kind, Sprite sprite, Color c, float life)
        {
            Item it = null;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].life <= 0f) { it = pool[i]; break; }

            if (it == null)
            {
                // 🔴 상한을 둔다. 화면에 200개가 뜨는 게임이라 이펙트가 무제한이면
                //    프레임이 먼저 죽는다 — WebGL에서 특히
                if (pool.Count >= 220) return null;

                var go = new GameObject("Fx");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 13;

                it = new Item { tr = go.transform, sr = sr };
                pool.Add(it);
            }

            it.kind = kind;
            it.sr.sprite = sprite != null ? sprite : square;
            it.color = c;
            it.sr.color = c;
            it.life = it.maxLife = Mathf.Max(0.02f, life);
            it.follow = null;
            it.vel = Vector2.zero;
            it.tr.gameObject.SetActive(true);
            return it;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < pool.Count; i++)
            {
                var it = pool[i];
                if (it.life <= 0f) continue;

                it.life -= dt;
                if (it.life <= 0f)
                {
                    it.follow = null;
                    it.tr.gameObject.SetActive(false);
                    continue;
                }

                float t = 1f - it.life / it.maxLife;          // 0 → 1

                // 빨려드는 입자는 대상을 향해 가속한다
                if (it.kind == Kind.Mote && it.follow != null)
                {
                    Vector2 d = (Vector2)it.follow.position - (Vector2)it.tr.position;
                    it.tr.position += (Vector3)(d.normalized * (6f + 22f * t) * dt);
                }

                var scale = Vector3.Lerp(it.startScale, it.endScale, t);
                if (!Sane(scale.x, scale.y, scale.z)) { it.life = 0f; it.tr.gameObject.SetActive(false); continue; }
                it.tr.localScale = scale;

                // 고리는 늦게까지 진하다가 끝에서 빠르게 사라진다 — 퍼지는 게 보여야 한다
                float a = it.kind == Kind.Ring ? Mathf.Clamp01(1f - t * t) : 1f - t;
                it.sr.color = new Color(it.color.r, it.color.g, it.color.b, it.color.a * a);
            }
        }
    }
}
