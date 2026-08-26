using UnityEngine;
using SalvageRun.Data;
using SalvageRun.Meta;

namespace SalvageRun.UI
{
    /// <summary>
    /// 영구 강화 화면. 격자에 노드를 놓고 **선행 관계에서 선을 자동으로 그린다.**
    ///
    /// 🔴 선을 데이터로 두지 않는 이유: 그림과 규칙이 어긋날 수 있기 때문이다.
    ///    `requires`가 곧 선이므로 "화면엔 이어져 있는데 안 열린다"가 원천적으로 없다.
    ///
    /// ⚠️ OnGUI다. HUD와 같은 이유로 임시이며 UGUI로 교체 대상이다.
    /// </summary>
    public class TechTreeScreen : MonoBehaviour
    {
        public GameContent content;

        public bool Open { get; private set; }

        Texture2D px;
        GUIStyle label, small, title, center;

        Vector2 pan;
        bool dragging;
        Vector2 dragFrom, panFrom;

        string flash;
        float flashLeft;
        Color flashColor = Color.white;

        int hovered = -1;

        // 격자 한 칸의 픽셀 크기 — 화면 배율에 맞춰 커진다
        const float CellBase = 96f;
        const float NodeBase = 62f;

        public void Toggle() => Open = !Open;
        public void Close() => Open = false;

        void Awake()
        {
            px = new Texture2D(1, 1);
            px.SetPixel(0, 0, Color.white);
            px.Apply();
        }

        void Styles(float s)
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label);
                small = new GUIStyle(GUI.skin.label) { wordWrap = true };
                title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            }
            // 🔴 한글 폰트 — 없으면 WebGL에서 전부 빈칸이 된다
            var f = Resources.Load<Font>("Galmuri11");
            if (f != null) label.font = small.font = title.font = center.font = f;

            label.fontSize = Mathf.RoundToInt(14 * s);
            small.fontSize = Mathf.RoundToInt(12 * s);
            title.fontSize = Mathf.RoundToInt(20 * s);
            center.fontSize = Mathf.RoundToInt(11 * s);
        }

        void Box(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, px);
            GUI.color = Color.white;
        }

        /// <summary>두 점을 잇는 선. OnGUI에는 선이 없어서 회전한 사각형으로 그린다.</summary>
        void Line(Vector2 a, Vector2 b, Color c, float thick)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;

            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            Box(new Rect(a.x, a.y - thick * 0.5f, len, thick), c);
            GUI.matrix = m;
        }

        static Color BranchColor(TechBranch b)
        {
            switch (b)
            {
                case TechBranch.Hull:    return new Color(0.45f, 0.80f, 1.00f);
                case TechBranch.Drive:   return new Color(0.55f, 1.00f, 0.75f);
                case TechBranch.Power:   return new Color(1.00f, 0.60f, 0.45f);
                case TechBranch.Salvage: return new Color(1.00f, 0.85f, 0.40f);
                case TechBranch.Weapon:  return new Color(0.85f, 0.65f, 1.00f);
                case TechBranch.Special: return new Color(1.00f, 0.45f, 0.85f);
            }
            return new Color(0.85f, 0.88f, 0.95f);
        }

        void Update()
        {
            if (flashLeft > 0f) flashLeft -= Time.unscaledDeltaTime;
        }

        void OnGUI()
        {
            if (!Open || content == null || content.techTree == null) return;

            MetaSave.EnsureFreeNodes(content);

            float s = Mathf.Clamp(Screen.height / 720f, 0.75f, 2.2f);
            Styles(s);

            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.025f, 0.05f, 0.97f));

            DrawHeader(s);
            HandlePan(56f * s);      // 머리말 아래에서만 끌린다

            Vector2 origin = new Vector2(Screen.width * 0.5f, Screen.height * 0.52f) + pan;
            float cell = CellBase * s;
            float node = NodeBase * s;

            DrawLinks(origin, cell, node, s);
            DrawNodes(origin, cell, node, s);
            DrawTooltip(s);
            DrawFlash(s);
        }

        // ---------------------------------------------------------------- 머리말

        void DrawHeader(float s)
        {
            var d = MetaSave.Data;

            Box(new Rect(0, 0, Screen.width, 56f * s), new Color(0.05f, 0.07f, 0.11f, 0.95f));
            GUI.Label(new Rect(20f * s, 12f * s, 400f * s, 32f * s), "정비소 — 영구 강화", title);

            // 🔴 6종이 되면서(2026-08-26) **가진 것만** 보여준다.
            //    0을 다 깔면 머리말이 0으로 도배되고, 정작 가진 것이 안 읽힌다.
            //    (첫 셋은 0이어도 보여준다 — 있어야 할 자리가 비면 사라진 줄 안다)
            float x = Screen.width - 620f * s;
            for (int i = 0; i < Mats.Count; i++)
            {
                var m = (MatKind)i;
                if (i >= 3 && d.Mat(m) <= 0) continue;
                DrawMat(ref x, s, m, d.Mat(m));
            }

            GUI.Label(new Rect(20f * s, 34f * s, 700f * s, 20f * s),
                "드래그 = 이동 · 클릭 = 강화 · 연 무기는 전부 배에 붙는다 · T/Esc = 닫기", small);
        }

        void DrawMat(ref float x, float s, MatKind m, int amount)
        {
            GUI.color = Mats.ColorOf(m);
            GUI.Label(new Rect(x, 14f * s, 105f * s, 26f * s), $"{Mats.Name(m)} {amount}", label);
            GUI.color = Color.white;
            x += 105f * s;
        }

        // ---------------------------------------------------------------- 이동

        void HandlePan(float topGuard)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // 🔴 **머리말·우주선 줄에서는 이동이 안 걸린다.**
                //    안 막으면 배를 고르려고 누른 것이 트리를 끌어 버린다 —
                //    누르는 순간 화면이 통째로 미끄러지므로 "고장 났다"로 읽힌다.
                if (e.mousePosition.y < topGuard) return;

                dragging = true;
                dragFrom = e.mousePosition;
                panFrom = pan;
            }
            else if (e.type == EventType.MouseDrag && dragging)
            {
                // 🔴 조금 움직인 건 드래그가 아니라 클릭이다. 안 그러면 노드를 못 누른다
                if ((e.mousePosition - dragFrom).sqrMagnitude > 36f)
                    pan = panFrom + (e.mousePosition - dragFrom);
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                dragging = false;
            }
        }

        bool Dragged => dragging && (Event.current.mousePosition - dragFrom).sqrMagnitude > 36f;

        Vector2 PosOf(TechNodeDef n, Vector2 origin, float cell)
            => new Vector2(origin.x + n.cell.x * cell, origin.y - n.cell.y * cell);

        // ---------------------------------------------------------------- 선

        /// <summary>노드가 화면에 어떻게 나오는가.</summary>
        enum Vis
        {
            /// <summary>안 그린다. 여기 뭐가 있는지도 모른다</summary>
            Hidden = 0,
            /// <summary>이름 없이 흐린 칸만. **"이 앞에 뭔가 더 있다"**만 알려준다</summary>
            Ghost,
            /// <summary>제대로 보인다. 재화만 있으면 누를 수 있다</summary>
            Open,
        }

        /// <summary>
        /// 🔴 **안 열린 건 안 보인다 — 다만 한 칸 앞은 흐리게** (2026-08-26 사장님 지시).
        ///
        ///    · 찍었거나 · 선행을 전부 찍었으면 → `Open`
        ///    · 선행이 **전부 `Open`**이지만 아직 안 찍었으면 → `Ghost` (한 칸 앞)
        ///    · 그보다 멀면 → `Hidden`
        ///
        /// 🔴 잠긴 것을 전부 회색으로 펼쳐 보이던 것을 그만둔 이유:
        ///    52칸이 한꺼번에 깔리면 **어디부터 봐야 할지 모른다.**
        ///
        ///    그렇다고 통째로 숨기면 **앞에 뭐가 있는지 몰라 아껴 둘 이유가 없어진다** —
        ///    목표가 안 보이면 재화는 그냥 눈앞의 것에 다 쓰게 된다.
        ///    한 칸만 흐리게 두는 게 둘 사이의 답이다: **길은 보이되 지도는 안 준다.**
        /// </summary>
        Vis VisOf(TechNodeDef n)
        {
            if (n == null) return Vis.Hidden;
            if (MetaSave.Data.RankOf(n.id) > 0) return Vis.Open;
            if (n.requires == null || n.requires.Length == 0) return Vis.Open;

            bool allBought = true;
            for (int i = 0; i < n.requires.Length; i++)
            {
                if (string.IsNullOrEmpty(n.requires[i])) continue;
                if (MetaSave.Data.RankOf(n.requires[i]) <= 0) { allBought = false; break; }
            }
            if (allBought) return Vis.Open;

            // 선행이 **보이기는 하는가** — 보이면 그 바로 다음 칸이므로 흐리게 보여준다
            for (int i = 0; i < n.requires.Length; i++)
            {
                if (string.IsNullOrEmpty(n.requires[i])) continue;

                var req = Find(n.requires[i]);
                if (req == null) continue;
                if (VisOfShallow(req) != Vis.Open) return Vis.Hidden;
            }
            return Vis.Ghost;
        }

        /// <summary>
        /// `VisOf`가 자기를 다시 부르면 사슬이 길어질수록 비싸진다(그리고 순환하면 멈추지 않는다).
        /// 한 단계만 본다 — 유령 판정에는 그것으로 충분하다.
        /// </summary>
        Vis VisOfShallow(TechNodeDef n)
        {
            if (n == null) return Vis.Hidden;
            if (MetaSave.Data.RankOf(n.id) > 0) return Vis.Open;
            if (n.requires == null || n.requires.Length == 0) return Vis.Open;

            for (int i = 0; i < n.requires.Length; i++)
            {
                if (string.IsNullOrEmpty(n.requires[i])) continue;
                if (MetaSave.Data.RankOf(n.requires[i]) <= 0) return Vis.Hidden;
            }
            return Vis.Open;
        }

        void DrawLinks(Vector2 origin, float cell, float node, float s)
        {
            var tree = content.techTree;
            var meta = MetaSave.Data;

            for (int i = 0; i < tree.Length; i++)
            {
                var n = tree[i];
                if (n.requires == null) continue;

                Vector2 to = PosOf(n, origin, cell);

                for (int r = 0; r < n.requires.Length; r++)
                {
                    var req = Find(n.requires[r]);
                    if (req == null) continue;

                    // 숨긴 노드로 가는 선은 안 그린다 — 안 그러면 허공으로 선이 뻗는다
                    var vTo = VisOf(n);
                    if (vTo == Vis.Hidden || VisOf(req) == Vis.Hidden) continue;

                    Vector2 from = PosOf(req, origin, cell);

                    bool reqDone = meta.RankOf(req.id) > 0;
                    bool bothDone = reqDone && meta.RankOf(n.id) > 0;

                    Color c = vTo == Vis.Ghost ? new Color(0.30f, 0.33f, 0.40f, 0.30f)
                            : bothDone ? new Color(0.55f, 0.85f, 1f, 0.75f)
                            : reqDone  ? new Color(0.45f, 0.55f, 0.70f, 0.55f)
                                       : new Color(0.25f, 0.28f, 0.35f, 0.40f);

                    // 참고 그림처럼 직각으로 꺾어 잇는다 — 대각선보다 격자가 읽힌다
                    Vector2 mid = new Vector2(from.x, to.y);
                    Line(from, mid, c, bothDone ? 3f * s : 2f * s);
                    Line(mid, to, c, bothDone ? 3f * s : 2f * s);
                }
            }
        }

        TechNodeDef Find(string id)
        {
            if (string.IsNullOrEmpty(id) || content.techTree == null) return null;
            for (int i = 0; i < content.techTree.Length; i++)
                if (content.techTree[i].id == id) return content.techTree[i];
            return null;
        }

        // ---------------------------------------------------------------- 노드

        void DrawNodes(Vector2 origin, float cell, float node, float s)
        {
            var tree = content.techTree;
            var meta = MetaSave.Data;
            var mouse = Event.current.mousePosition;

            hovered = -1;

            for (int i = 0; i < tree.Length; i++)
            {
                var n = tree[i];
                Vector2 p = PosOf(n, origin, cell);
                var r = new Rect(p.x - node * 0.5f, p.y - node * 0.5f, node, node);

                if (r.yMax < 56f * s || r.yMin > Screen.height) continue;

                var vis = VisOf(n);
                if (vis == Vis.Hidden) continue;      // 🔴 멀리 있는 것은 **아예 안 보인다**

                // 🔴 **한 칸 앞은 흐린 칸만.** 이름도 값도 안 준다 —
                //    "이 앞에 뭔가 더 있다"만 알려주고 나머지는 찍어야 알 수 있다
                if (vis == Vis.Ghost)
                {
                    Box(r, new Color(0.10f, 0.11f, 0.14f, 0.55f));
                    Frame(r, new Color(0.28f, 0.31f, 0.38f, 0.35f), 1.2f * s);

                    GUI.color = new Color(0.40f, 0.43f, 0.52f, 0.75f);
                    GUI.Label(new Rect(r.x, r.y + r.height * 0.3f, r.width, 20f * s), "???", center);
                    GUI.color = Color.white;
                    continue;                          // 눌러도 안 되고 설명도 안 뜬다
                }

                int rank = meta.RankOf(n.id);
                bool maxed = rank >= n.maxRank;

                // 🔴 무기 노드는 상태가 셋이다 — 잠김 / 열림 / **장착 중**.
                //    지금 무엇을 들고 나가는지가 트리에서 바로 보여야 한다
                bool isWeapon = n.effect == TechEffect.UnlockWeapon;
                bool wOpen = isWeapon && MetaSave.WeaponUnlocked(content, n.weapon);
                if (wOpen) { rank = Mathf.Max(rank, 1); maxed = true; }
                bool can = MetaSave.CanBuy(n, content, out string why);
                // ⬜ "선행 필요"로 잠긴 상태는 이제 화면에 안 온다 — `VisOf`가 걸러낸다.
                //    남은 잠김은 **재화 부족**뿐이라 따로 어둡게 칠하지 않는다.

                Color bc = BranchColor(n.branch);

                // 배경 — 상태가 한눈에 갈려야 한다
                Color fill = maxed ? new Color(bc.r * 0.42f, bc.g * 0.42f, bc.b * 0.42f, 0.95f)
                           : rank > 0 ? new Color(bc.r * 0.30f, bc.g * 0.30f, bc.b * 0.30f, 0.92f)
                           : new Color(0.14f, 0.16f, 0.21f, 0.94f);

                Box(r, fill);

                // 테두리 — 찍은 것은 밝게, 지금 살 수 있는 것은 흰 테두리
                Color edge = maxed ? bc
                           : can ? Color.white
                           : rank > 0 ? new Color(bc.r, bc.g, bc.b, 0.7f)
                           : new Color(0.3f, 0.33f, 0.4f, 0.8f);
                Frame(r, edge, (can || maxed) ? 2.5f * s : 1.5f * s);

                // 이름
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 3f * s, r.y + 5f * s, r.width - 6f * s, r.height * 0.55f),
                    n.title, center);
                GUI.color = Color.white;

                // 랭크 — maxRank가 1이면 굳이 안 쓴다
                if (n.maxRank > 1)
                {
                    GUI.color = maxed ? bc : new Color(0.7f, 0.75f, 0.85f);
                    GUI.Label(new Rect(r.x, r.yMax - 18f * s, r.width, 16f * s),
                        $"{rank}/{n.maxRank}", center);
                    GUI.color = Color.white;
                }
                else if (isWeapon)
                {
                    // 🔴 **연 무기는 배에 그대로 붙어 있다** (2026-08-26). 고르는 게 아니다
                    GUI.color = wOpen ? new Color(0.7f, 1f, 0.85f) : new Color(0.62f, 0.55f, 0.45f);
                    GUI.Label(new Rect(r.x, r.yMax - 18f * s, r.width, 16f * s),
                              wOpen ? "장착됨" : "열기", center);
                    GUI.color = Color.white;
                }
                else if (rank > 0)
                {
                    GUI.color = bc;
                    GUI.Label(new Rect(r.x, r.yMax - 18f * s, r.width, 16f * s), "완료", center);
                    GUI.color = Color.white;
                }

                if (r.Contains(mouse)) hovered = i;

                if (Event.current.type == EventType.MouseUp && Event.current.button == 0
                    && r.Contains(mouse) && !Dragged)
                {
                    TryBuy(n, can, maxed, why);
                    Event.current.Use();
                }
            }
        }

        void Frame(Rect r, Color c, float t)
        {
            Box(new Rect(r.x, r.y, r.width, t), c);
            Box(new Rect(r.x, r.yMax - t, r.width, t), c);
            Box(new Rect(r.x, r.y, t, r.height), c);
            Box(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void TryBuy(TechNodeDef n, bool can, bool maxed, string why)
        {
            // ⬜ 예전에는 연 무기를 다시 누르면 **골라 드는** 분기가 여기 있었다.
            //    2026-08-26부터 연 무기는 전부 붙으므로 고를 일이 없다 — 분기를 뺐다.

            if (maxed) { Flash("이미 최대다", new Color(0.7f, 0.75f, 0.85f)); return; }
            if (!can) { Flash(why ?? "아직 안 된다", new Color(1f, 0.55f, 0.45f)); return; }

            if (MetaSave.Buy(n, content))
                Flash($"{n.title} 강화 — {n.description}", BranchColor(n.branch));
        }

        void Flash(string text, Color c)
        {
            flash = text;
            flashColor = c;
            flashLeft = 2.4f;
        }

        void DrawFlash(float s)
        {
            if (flashLeft <= 0f || string.IsNullOrEmpty(flash)) return;

            float a = Mathf.Clamp01(flashLeft / 0.7f);
            var r = new Rect(0, Screen.height - 62f * s, Screen.width, 30f * s);

            GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, a);
            GUI.Label(r, flash, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- 설명창

        void DrawTooltip(float s)
        {
            if (hovered < 0 || hovered >= content.techTree.Length) return;

            var n = content.techTree[hovered];
            var meta = MetaSave.Data;
            int rank = meta.RankOf(n.id);
            int next = rank + 1;
            bool maxed = rank >= n.maxRank;

            float w = 330f * s, h = 168f * s;
            var m = Event.current.mousePosition;
            float x = Mathf.Min(m.x + 18f * s, Screen.width - w - 8f * s);
            float y = Mathf.Min(m.y + 18f * s, Screen.height - h - 8f * s);
            var r = new Rect(x, y, w, h);

            Box(r, new Color(0.06f, 0.07f, 0.11f, 0.97f));
            Frame(r, BranchColor(n.branch), 2f * s);

            float ty = r.y + 8f * s;
            GUI.color = BranchColor(n.branch);
            GUI.Label(new Rect(r.x + 10f * s, ty, w - 20f * s, 24f * s), n.title, label);
            GUI.color = Color.white;
            ty += 24f * s;

            GUI.Label(new Rect(r.x + 10f * s, ty, w - 20f * s, 44f * s), n.description, small);
            ty += 46f * s;

            if (maxed)
            {
                GUI.color = new Color(0.6f, 0.9f, 1f);
                GUI.Label(new Rect(r.x + 10f * s, ty, w - 20f * s, 20f * s), "최대까지 강화했다", small);
                GUI.color = Color.white;
                return;
            }

            GUI.Label(new Rect(r.x + 10f * s, ty, w - 20f * s, 20f * s),
                $"다음 랭크 {next}/{n.maxRank}", small);
            ty += 20f * s;

            // 🔴 **여섯 종류를 다 훑는다** (2026-08-27). 셋만 그리면
            //    초합금 이상이 드는 노드가 **값이 없는 것처럼 보인다** —
            //    사고 나서야 "왜 초합금이 줄었지?"가 된다.
            //    (`CostLine`이 0은 알아서 건너뛰므로 빈 줄은 안 생긴다)
            for (int i = 0; i < Mats.Count; i++)
            {
                var mk = (MatKind)i;
                CostLine(r, ref ty, s, mk, n.CostAt(mk, next), meta.Mat(mk));
            }

            // ⬜ "선행 노드를 먼저 찍어야 한다" 줄이 있었다. 안 열린 노드는 이제
            //    화면에 안 나오므로(`VisOf`) 그 안내를 볼 일 자체가 없다.
        }

        void CostLine(Rect r, ref float ty, float s, MatKind m, int cost, int have)
        {
            if (cost <= 0) return;

            // 🔴 부족한 건 붉게. 숫자만 보여주면 왜 못 사는지 한참 찾게 된다
            GUI.color = have >= cost ? Mats.ColorOf(m) : new Color(1f, 0.45f, 0.40f);
            GUI.Label(new Rect(r.x + 10f * s, ty, r.width - 20f * s, 18f * s),
                $"{Mats.Name(m)}  {cost}   (보유 {have})", small);
            GUI.color = Color.white;
            ty += 18f * s;
        }
    }
}
