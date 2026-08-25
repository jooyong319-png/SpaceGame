using UnityEngine;
using UnityEngine.Rendering.Universal;
using SalvageRun.Data;
using SalvageRun.UI;

namespace SalvageRun.Run
{
    /// <summary>
    /// 그레이박스 씬을 코드로 조립한다. 흰 네모 단계에서 오브젝트 배치·컴포넌트 드래그에
    /// 시간을 쓰지 않으려는 것. (wiki/SCHEMA.md 제1원칙 — 재미 검증이 먼저다)
    ///
    /// 사용법: 빈 씬에 빈 GameObject 하나 만들고 이 컴포넌트만 붙인 뒤 Play.
    /// 에셋 칸을 비워두면 ContentDefaults의 값으로 런타임 생성한다.
    /// ⚠️ 아트 단계에서 통째로 버린다.
    /// </summary>
    public class GreyboxBootstrap : MonoBehaviour
    {
        [Header("데이터 에셋 (비우면 기본값으로 생성)")]
        public RunConfig configAsset;
        public GameContent contentAsset;

        RunConfig config;
        GameContent content;
        Sprite square;      // 이펙트 · 선 — 모양이 필요 없는 것
        Sprite shipArt;
        Sprite bladeArt;
        Sprite glowArt;

        /// <summary>
        /// 🔴 **씬에 아무것도 없어도 게임이 뜬다.**
        ///
        ///    지금까지는 에디터에서 메뉴로 부트스트랩을 씬에 넣어야만 돌아갔다.
        ///    그런데 그건 씬에 저장되지 않아서, **빌드하면 빈 화면이 나온다** —
        ///    2026-08-22에 첫 WebGL 빌드를 준비하다 발견했다.
        ///    (빌드에 들어 있던 씬은 7월에 만든 유니티 기본 템플릿 그대로였다)
        ///
        ///    씬 구성을 사람 손에 맡기면 반드시 이런 게 생긴다.
        ///    코드가 스스로 조립하게 두면 어떤 씬에서 실행하든 게임이 된다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<GreyboxBootstrap>() != null) return;

            var go = new GameObject("== GREYBOX BOOTSTRAP (auto) ==");
            go.AddComponent<GreyboxBootstrap>();
        }

        void Awake()
        {
            BuildData();

            // 🔴 코드로 찍은 도트. 흰 사각형만으론 "쓰레기가 22종"이 화면에서 안 읽힌다.
            //    진짜 아트가 들어오면 PixelArt.cs만 지우면 된다 — 밖으로 안 새게 짰다.
            square = PixelArt.Square();
            // 🔴 청소선 — 앞이 벌어진 흡입구. 형태가 곧 설명이다 (2026-08-22)
            shipArt = PixelArt.Cleaner(24);
            bladeArt = PixelArt.Blade(20, 7);
            glowArt = PixelArt.Glow(32);

            var cam = SetupCamera();
            EnsureGlobalLight();

            // 넓어진 만큼 개수도 늘린다 — 안 그러면 밀도가 떨어져 더 휑해 보인다
            BuildStars(900, 0.10f, 0.45f);
            BuildStars(420, 0.17f, 0.8f);

            var bounds = BuildArenaFrame();

            // ⬜ 2026-08-23: **모선을 없앴다** (사장님 지시).
            //    회복 지점이 사라지면서 연료가 순수한 타이머가 됐다.
            //    `BuildMothership()`은 남겨 뒀다 — 되살릴 때 그대로 쓴다.
            var ship = BuildShip(cam);

            var fieldGo = new GameObject("Field");
            var field = fieldGo.AddComponent<StageField>();
            field.content = content;
            field.sprite = square;
            field.debrisSprites = MakeDebrisSet();
            field.shardSprite = PixelArt.Shard(10);
            field.crystalSprite = PixelArt.Crystal(16);
            field.ringSprite = PixelArt.Ring(48, 0.16f);

            var runGo = new GameObject("Run");
            var director = runGo.AddComponent<RunDirector>();
            var rig = runGo.AddComponent<WeaponRig>();

            director.content = content;
            director.config = config;
            director.ship = ship;
            director.field = field;
            director.arms = rig;
            director.stageBounds = bounds;
            director.cam = cam;

            field.director = director;

            rig.config = config;
            rig.ship = ship;
            rig.field = field;
            rig.director = director;
            rig.content = content;
            rig.sprite = square;
            rig.bladeSprite = bladeArt;
            rig.glowSprite = glowArt;
            rig.ringSprite = PixelArt.Ring(48, 0.14f);

            BuildAimCursor(ship, director);

            var follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = ship.transform;

            // 타격감 — 화면 흔들림 + 코드로 만든 소리
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            var juice = new GameObject("Juice").AddComponent<Juice>();
            juice.cam = cam;

            var hud = new GameObject("HUD").AddComponent<GameHud>();
            hud.content = content;
            hud.config = config;
            hud.director = director;
            hud.ship = ship;
            hud.arms = rig;
            hud.cam = cam;

            var fxGo = new GameObject("Fx").AddComponent<Fx>();
            fxGo.square = square;
            fxGo.ring = PixelArt.Ring(48, 0.14f);
            fxGo.glow = glowArt;
            fxGo.shard = PixelArt.Shard(10);

            var bossAI = runGo.AddComponent<BossBehaviour>();
            bossAI.director = director;
            bossAI.field = field;
            bossAI.ship = ship;
            director.boss = bossAI;

            var pilot = runGo.AddComponent<AutoPilot>();
            pilot.director = director;
            pilot.ship = ship;

            var tech = new GameObject("TechTree").AddComponent<SalvageRun.UI.TechTreeScreen>();
            tech.content = content;
            hud.tech = tech;

        }

        void BuildData()
        {
            // 런타임에 ScriptableObject 에셋을 고치면 에디터에서는 그 변경이 파일에 저장된다.
            // 그래서 항상 복사본을 만들어 쓴다.
            config = configAsset != null
                ? Instantiate(configAsset)
                : ScriptableObject.CreateInstance<RunConfig>();

            content = contentAsset != null
                ? Instantiate(contentAsset)
                : ScriptableObject.CreateInstance<GameContent>();

            if (content.IsEmpty) ContentDefaults.Fill(content);
        }

        // ---------- 조립 ----------

        Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 11f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.047f, 0.07f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            return cam;
        }

        /// <summary>
        /// URP 2D에서 SpriteRenderer 기본 머티리얼은 Sprite-Lit-Default라,
        /// 씬에 Light2D가 하나도 없으면 스프라이트가 전부 새까맣게 나온다.
        /// </summary>
        void EnsureGlobalLight()
        {
            if (Object.FindFirstObjectByType<Light2D>() != null) return;
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        /// <summary>
        /// 아레나 경계. 화면 하나가 곧 맵이므로 가장자리가 어디인지 읽혀야 한다.
        /// 스케일이 (폭, 높이)로 들어오면 테두리 네 줄이 그에 맞게 늘어난다.
        /// </summary>
        Transform BuildArenaFrame()
        {
            var root = new GameObject("ArenaFrame").transform;

            var fill = NewSprite("Fill", Vector3.zero, Vector3.one, new Color(1f, 1f, 1f, 0.022f), -8);
            fill.transform.SetParent(root, false);

            // 🔴 경계선을 **또렷하게.** 0.16이라 사실상 안 보였다 —
            //    "맵 끝에 선 하나를 만들어주는 게 좋을듯" (2026-08-22 피드백).
            //    어디까지가 맵인지 모르면 도망칠 방향을 정할 수 없다.
            var edge = new Color(0.55f, 0.85f, 1f, 0.55f);
            // 부모 스케일이 (폭,높이)이므로 자식은 정규화 좌표(±0.5)로 둔다
            var top = NewSprite("Top", Vector3.zero, Vector3.one, edge, -7);
            top.transform.SetParent(root, false);
            top.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            top.transform.localScale = new Vector3(1f, 0.010f, 1f);

            var bottom = NewSprite("Bottom", Vector3.zero, Vector3.one, edge, -7);
            bottom.transform.SetParent(root, false);
            bottom.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            bottom.transform.localScale = new Vector3(1f, 0.010f, 1f);

            var left = NewSprite("Left", Vector3.zero, Vector3.one, edge, -7);
            left.transform.SetParent(root, false);
            left.transform.localPosition = new Vector3(-0.5f, 0f, 0f);
            left.transform.localScale = new Vector3(0.003f, 1f, 1f);

            var right = NewSprite("Right", Vector3.zero, Vector3.one, edge, -7);
            right.transform.SetParent(root, false);
            right.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            right.transform.localScale = new Vector3(0.003f, 1f, 1f);

            return root;
        }

        Transform BuildMothership()
        {
            // 🔴 **셋의 실루엣을 다르게 한다** (2026-08-21 요청: *"구분이 될 정도면 됨"*).
            //    색이 아니라 **형태**로 갈라야 화면이 복잡해져도 안 헷갈린다:
            //      기지 = 크고 각진 **육각형** + 창문 불빛 (움직이지 않는 인공물)
            //      쓰레기 = 울퉁불퉁한 **파편** (규칙 없음)
            //      우주선 = 앞이 벌어진 **흡입구** (방향이 있다)
            var root = new GameObject("Mothership").transform;

            var hull = NewSprite("Hull", Vector3.zero, new Vector3(6.2f, 6.2f, 1f), Color.white, 1);
            hull.GetComponent<SpriteRenderer>().sprite = PixelArt.Station(48);
            hull.transform.SetParent(root, false);

            // 도킹 반경을 바닥에 그려 둔다 — 어디까지 들어가야 입금되는지 보여야 한다
            var pad = NewSprite("DockPad", Vector3.zero, new Vector3(6.4f, 6.4f, 1f),
                new Color(0.45f, 0.9f, 0.8f, 0.10f), 0);
            pad.GetComponent<SpriteRenderer>().sprite = PixelArt.Ring(64, 0.06f);
            pad.transform.SetParent(root, false);

            // 🔴 **태양광 어레이 3개** — 도입 연출에서 하나씩 뜯겨 나간다 (rev.12).
            //    양옆으로 펼쳐 놓아야 "떨어져 나간다"가 실루엣으로 읽힌다.
            var wings = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                float side = (i % 2 == 0) ? 1f : -1f;
                float up = 0.9f + (i / 2) * 1.9f;

                var w = NewSprite("Array" + i,
                    new Vector3(side * (4.6f + (i / 2) * 0.7f), up - 1.4f, 0f),
                    new Vector3(3.4f, 1.05f, 1f),
                    new Color(0.45f, 0.62f, 0.95f, 0.95f), 2);
                w.GetComponent<SpriteRenderer>().sprite = square;
                w.transform.SetParent(root, false);
                wings.Add(w.transform);
            }

            // 회전하는 표지등 — 멀리서도 "저기가 기지다"가 읽힌다
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var l = NewSprite("Light", new Vector3(Mathf.Cos(a) * 3.4f, Mathf.Sin(a) * 3.4f, 0f),
                    new Vector3(0.42f, 0.42f, 1f), new Color(1f, 0.9f, 0.5f, 0.95f), 3);
                l.GetComponent<SpriteRenderer>().sprite = PixelArt.Glow(24);
                l.transform.SetParent(root, true);
            }
            return root;
        }

        ShipController BuildShip(Camera cam)
        {
            var go = new GameObject("Ship");

            var bodyRoot = new GameObject("BodyRoot").transform;
            bodyRoot.SetParent(go.transform, false);

            var bodyGo = NewSprite("Body", Vector3.zero, new Vector3(1.05f, 1.05f, 1f), Color.white, 12);
            bodyGo.transform.SetParent(bodyRoot, false);
            // 🔴 배 스프라이트는 위를 향해 그려져 있다. 코드가 오른쪽을 앞으로 쓰므로 -90도 돌린다
            bodyGo.GetComponent<SpriteRenderer>().sprite = shipArt;
            bodyGo.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            var flameGo = NewSprite("Flame", new Vector3(-0.9f, 0f, 0f), new Vector3(0.5f, 0.34f, 1f),
                new Color(1f, 0.65f, 0.25f), 11);
            flameGo.transform.SetParent(bodyRoot, false);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var ship = go.AddComponent<ShipController>();
            ship.config = config;
            ship.cam = cam;

            var vis = go.AddComponent<ShipVisual>();
            vis.ship = ship;
            vis.bodyRoot = bodyRoot;
            vis.body = bodyGo.GetComponent<SpriteRenderer>();
            vis.flame = flameGo.GetComponent<SpriteRenderer>();

            return ship;
        }

        void BuildAimCursor(ShipController ship, RunDirector director)
        {
            var root = new GameObject("AimCursor");
            var cursor = root.AddComponent<AimCursor>();
            cursor.ship = ship;
            cursor.director = director;

            var c = new Color(1f, 1f, 1f, 0.5f);
            var h = NewSprite("H", Vector3.zero, new Vector3(0.9f, 0.06f, 1f), c, 20);
            var v = NewSprite("V", Vector3.zero, new Vector3(0.06f, 0.9f, 1f), c, 20);
            h.transform.SetParent(root.transform, false);
            v.transform.SetParent(root.transform, false);

            cursor.parts = new[] { h.GetComponent<SpriteRenderer>(), v.GetComponent<SpriteRenderer>() };
        }

        // 카메라가 고정이라 시차(Parallax)는 의미가 없다 — 정적 별 배경으로 대체했다.
        Transform BuildStars(int count, float size, float bright)
        {
            var parent = new GameObject("StarLayer");
            var rng = new System.Random(count * 7919);

            // 🔴 별을 **가장 큰 맵**까지 덮는다.
            //    ±32×19만 뿌리고 있었는데 맵은 최대 ±84×56이라
            //    가장자리가 텅 비어 "맵 끝에 별이 없다"는 피드백이 나왔다 (2026-08-22).
            //    여백을 조금 더 줘서 경계선 바깥도 우주로 보이게 한다.
            const float SpanX = 95f, SpanY = 66f;

            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-SpanX, SpanX, (float)rng.NextDouble());
                float y = Mathf.Lerp(-SpanY, SpanY, (float)rng.NextDouble());
                float a = bright * (0.4f + 0.6f * (float)rng.NextDouble());
                var s = NewSprite("star", new Vector3(x, y, 0f), Vector3.one * size,
                    new Color(0.8f, 0.85f, 1f, a), -10);
                s.transform.SetParent(parent.transform, true);
            }
            return parent.transform;
        }

        // ---------- 유틸 ----------

        GameObject NewSprite(string name, Vector3 pos, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        /// <summary>
        /// 잔해 실루엣 여러 벌. 🔴 쓰레기마다 새로 찍으면 22종 × 인스턴스가 전부 메모리를 먹으므로,
        /// 몇 벌만 만들어 돌려 쓰고 색으로 구분한다.
        /// </summary>
        static Sprite[] MakeDebrisSet()
        {
            var set = new Sprite[8];
            for (int i = 0; i < set.Length; i++)
                set[i] = PixelArt.Debris(16, 1000 + i * 37, 0.24f + (i % 4) * 0.07f);
            return set;
        }

        static Sprite MakeSquareSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
