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

            float s = Mathf.Clamp(Screen.height / 720f, 0.75f, 2.2f);
            Styles(s);

            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.025f, 0.05f, 0.97f));

            DrawHeader(s);
            HandlePan();

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

            float x = Screen.width - 470f * s;
            DrawMat(ref x, s, MatKind.Scrap, d.scrap);
            DrawMat(ref x, s, MatKind.Circuit, d.circuit);
            DrawMat(ref x, s, MatKind.Core, d.core);

            GUI.Label(new Rect(20f * s, 34f * s, 700f * s, 20f * s),
                "드래그 = 이동 · 클릭 = 강화 · T 또는 Esc = 닫기", small);
        }

        void DrawMat(ref float x, float s, MatKind m, int amount)
        {
            GUI.color = Mats.ColorOf(m);
            GUI.Label(new Rect(x, 14f * s, 150f * s, 26f * s), $"{Mats.Name(m)}  {amount}", label);
            GUI.color = Color.white;
            x += 150f * s;
        }

        // ---------------------------------------------------------------- 이동

        void HandlePan()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
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

                    Vector2 from = PosOf(req, origin, cell);

                    bool reqDone = meta.RankOf(req.id) > 0;
                    bool bothDone = reqDone && meta.RankOf(n.id) > 0;

                    Color c = bothDone ? new Color(0.55f, 0.85f, 1f, 0.75f)
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

                int rank = meta.RankOf(n.id);
                bool maxed = rank >= n.maxRank;
                bool can = MetaSave.CanBuy(n, content, out string why);
                bool locked = !can && !maxed && why == "선행 필요";

                Color bc = BranchColor(n.branch);

                // 배경 — 상태가 한눈에 갈려야 한다
                Color fill = maxed ? new Color(bc.r * 0.42f, bc.g * 0.42f, bc.b * 0.42f, 0.95f)
                           : rank > 0 ? new Color(bc.r * 0.30f, bc.g * 0.30f, bc.b * 0.30f, 0.92f)
                           : locked ? new Color(0.09f, 0.10f, 0.13f, 0.92f)
                           : new Color(0.14f, 0.16f, 0.21f, 0.94f);

                Box(r, fill);

                // 테두리 — 찍은 것은 밝게, 지금 살 수 있는 것은 흰 테두리
                Color edge = maxed ? bc
                           : can ? Color.white
                           : rank > 0 ? new Color(bc.r, bc.g, bc.b, 0.7f)
                           : new Color(0.3f, 0.33f, 0.4f, 0.8f);
                Frame(r, edge, (can || maxed) ? 2.5f * s : 1.5f * s);

                // 이름
                GUI.color = locked ? new Color(0.45f, 0.47f, 0.55f) : Color.white;
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

            CostLine(r, ref ty, s, MatKind.Scrap, n.CostAt(MatKind.Scrap, next), meta.scrap);
            CostLine(r, ref ty, s, MatKind.Circuit, n.CostAt(MatKind.Circuit, next), meta.circuit);
            CostLine(r, ref ty, s, MatKind.Core, n.CostAt(MatKind.Core, next), meta.core);

            if (!MetaSave.CanBuy(n, content, out string why) && why == "선행 필요")
            {
                GUI.color = new Color(1f, 0.6f, 0.5f);
                GUI.Label(new Rect(r.x + 10f * s, ty, w - 20f * s, 20f * s),
                    "선행 노드를 먼저 찍어야 한다", small);
                GUI.color = Color.white;
            }
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
