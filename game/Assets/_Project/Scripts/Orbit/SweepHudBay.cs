using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🌳 정비고 트리 — 칸 그래프 · 아이콘 · 설명 · 칸 수치 (09-28 나눔)
    public partial class SweepHud
    {
        int CanCount(int b) { int n = 0; for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Nodes[i].branch == SweepSim.BranchIds[b] && sim.State(i) == NodeSt.Can) n++; return n; }
        int Total(int b) { int n = 0; for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Nodes[i].branch == SweepSim.BranchIds[b]) n++; return n; }
        int Owned(int b) { int n = 0; for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Nodes[i].branch == SweepSim.BranchIds[b] && sim.S.lv[i] > 0) n++; return n; }

        // ───────────────────────────────── 정비고 — 네 칸 · 칸마다 카드 (설명 창 없이 카드에 다)

        // ───────────────────────────────── 정비소 — 가운데 한 칸에서 격자로 뻗는 트리 (사장님이 보여 주신 Bills Must Be Paid 트리)
        // 🔴 칸 = 한 번 사기. 레벨이 여럿인 능력은 칸 여러 개가 한 줄로 이어진다 (Ⅰ Ⅱ Ⅲ …)
        // 산 칸 = 밝게 · 다음 칸 = 보인다(살 수 있으면 빛난다) · 그 너머 = 어두운 실루엣 · 더 먼 곳 = 안 보인다
        // 화면은 보이는 칸에 맞춰 저절로 당겨지고 물러난다. 끌어서 옮길 수도 있다.

        class GTile { public int stat, j, lpar = -1; public List<int> xpar = new List<int>(); public Vector2Int cell; public Vector2Int inDir; }
        static List<GTile> gtiles;
        public int TestTip = -1;                                               // 에디터 시험용 — 마우스 없이 칸 설명 띄우기
        // ✨ 09-29 사장님 「스킬 찍을 때 이펙트 + 애니」 — 칸 튕김 · 흰빛 · 다음 칸으로 흐르는 빛 · 쓴 돈이 떠오른다 (화려하되 정돈)
        readonly Dictionary<int, float> tileBuyT = new Dictionary<int, float>();   // 칸(타일) 번호 → 산 시각
        readonly List<(int from, int to, float t0)> buyFlows = new List<(int, int, float)>();
        readonly List<(int k, string txt, float t0)> buyCoins = new List<(int, string, float)>();
        const float FlowLen = 0.45f, PingLen = 0.4f;
        public void TestBuyFx(int k) { if (gtiles != null && k >= 0 && k < gtiles.Count) BuyAnim(k, "-1.2K"); }   // 에디터 시험용
        void BuyAnim(int k, string spent)
        {
            float now = Time.unscaledTime; tileBuyT[k] = now;
            if (spent != null) buyCoins.Add((k, spent, now));
            if (reduceMotion) return;
            int j = 0;
            for (int c = 0; c < gtiles.Count; c++)
                if ((gtiles[c].lpar == k || gtiles[c].xpar.Contains(k)) && !(gtiles[c].stat >= 0 && sim.State(gtiles[c].stat) == NodeSt.Locked))
                    buyFlows.Add((k, c, now + 0.08f + 0.06f * j++));   // 새로 열린 다음 칸들로 — 조금씩 어긋나게 · 잠긴 칸은 빼서 「다음은 여기」만
        }
        /// <summary>산 칸 — 꾹 눌렸다 튀어 오른다 (0.42초)</summary>
        float BuyScale(int k) { if (!tileBuyT.TryGetValue(k, out var t0)) return 1; float e = Time.unscaledTime - t0; if (e > 0.75f) { tileBuyT.Remove(k); return 1; } if (reduceMotion || e > 0.42f) return 1; float u = e / 0.42f; return u < 0.3f ? Mathf.Lerp(1, 0.84f, u / 0.3f) : u < 0.65f ? Mathf.Lerp(0.84f, 1.12f, Mathf.SmoothStep(0, 1, (u - 0.3f) / 0.35f)) : Mathf.Lerp(1.12f, 1, (u - 0.65f) / 0.35f); }   // 📐 도장 — 꾹 눌렸다(0.84) 튀고(1.12) 제자리 · 도장 그림 때문에 0.75초 남긴다
        float BuyFlash(int k) => tileBuyT.TryGetValue(k, out var t0) ? Mathf.Clamp01(1 - (Time.unscaledTime - t0) / 0.25f) : 0;
        public int TileCount => gtiles != null ? gtiles.Count : 0;
        static readonly Vector2Int[] Dirs8 = { new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(-1, -1) };
        string lastBuyBranch = "";
        Vector2 camC; float camZ = 1f, userZ = 1f; Vector2 pan; bool dragging, dragMoved; int dragBtn; Vector2 dragFrom, dragStart;

        static Vector2Int Rot(Vector2Int d, int k) { int i = System.Array.IndexOf(Dirs8, d); return Dirs8[((i + k) % 8 + 8) % 8]; }

        static void BuildGraph()
        {
            gtiles = new List<GTile>();
            var N = SweepSim.Nodes;
            var first = new Dictionary<string, int>();
            gtiles.Add(new GTile { stat = -1, j = 1, cell = Vector2Int.zero });          // 가운데 — 청소선
            first["R"] = 0;
            for (int i = 0; i < N.Length; i++)
            {
                if (SweepSim.Retired(i)) continue;                             // 없앤 칸 (의뢰) — 트리에 안 그린다
                var pl = SweepSim.Layout[N[i].id];
                for (int j = 1; j <= SweepSim.Tiles(i); j++)
                {
                    if (j == 1) first[N[i].id] = gtiles.Count;
                    gtiles.Add(new GTile { stat = i, j = j, cell = new Vector2Int(pl.x + pl.dx * (j - 1), pl.y + pl.dy * (j - 1)) });
                }
            }
            for (int k = 1; k < gtiles.Count; k++)
            {
                var t = gtiles[k]; var n = N[t.stat];
                if (t.j > 1) { t.lpar = k - 1; continue; }
                var pl = SweepSim.Layout[n.id];
                t.lpar = pl.par == "R" ? 0 : first[pl.par] + pl.tile - 1;
                foreach (var p in n.par) if (p != pl.par) t.xpar.Add(first[p]);
            }
        }

        int[] gmemo; int treeHover = -1;
        /// <summary>이 칸을 막고 있는 앞 칸 (아직 안 산 부모 칸) — 없으면 -1</summary>
        int BlockTile(int k)
        {
            var t = gtiles[k];
            if (t.lpar >= 0 && GTileState(t.lpar) != 3) return t.lpar;
            foreach (var x in t.xpar) if (GTileState(x) != 3) return x;
            return -1;
        }
        string TileName(int k) { var t = gtiles[k]; if (t.stat < 0) return Loc.T("청소선"); return sim.NodeName(t.stat) + (SweepSim.Tiles(t.stat) > 1 ? " " + Roman[t.j] : ""); }
        int GTileState(int k)   // 0 안 보임 · 1 실루엣 · 2 다음 칸 · 3 산 것 (한 번 그릴 때 한 번만 계산 — 부모를 거슬러 가는 재귀가 겹치면 기하급수로 느려진다)
        {
            if (gmemo[k] >= 0) return gmemo[k];
            return gmemo[k] = GTileCalc(k);
        }
        int GTileCalc(int k)
        {
            var t = gtiles[k];
            if (t.stat < 0) return 3;                                   // 청소선 — 늘 있다
            if (sim.S.lv[t.stat] >= SweepSim.TileLv(t.stat, t.j)) return 3;
            bool parOwned = t.lpar < 0 || GTileState(t.lpar) == 3;
            foreach (var x in t.xpar) if (GTileState(x) != 3) parOwned = false;
            if (parOwned) return 2;
            if (t.lpar >= 0 && GTileState(t.lpar) == 2) return 1;
            return 0;
        }

        static readonly string[] PlanetHint = { "", Loc.T("달 — 느린 궤도 · 금고 위성이 많아 노려 볼 만함"), Loc.T("화성 — 판 중간에 모래 폭풍 · 얼음 껍질은 먼저 깨 둘 것"), Loc.T("목성 — 잔해를 안쪽으로 모으는 중력 · 블랙홀은 안쪽 가장자리에"), Loc.T("토성 — 고리가 두 겹 · 가운데 틈은 비어 있음") , Loc.T("소행성대 — 단단한 암석과 광석 많음"), Loc.T("천왕성 — 옆으로 누운 궤도 · 얼음 결정"), Loc.T("해왕성 — 초속 폭풍이 잔해를 흩뜨림"), Loc.T("카이퍼 벨트 — 태양계 끝 · 고대 탐사선과 혜성"), Loc.T("오르트 구름 — 단단한 얼음 혜성 떼 · 혜성 머리를 맞히면 열쇠"), Loc.T("태양권 계면 — 태양풍이 불면 잔해가 한쪽으로 쏠림 · 폭풍 땐 값 ×1.5"), Loc.T("성간 공간 — 드문드문하지만 하나하나가 비쌈 · 결정 하나를 깨면 같은 결정 전부") };
        static Texture2D iconTex;
        void DrawIcon(Rect r, string id)
        {
            if (iconTex == null) iconTex = Resources.Load<Texture2D>("tree_icons");
            int idx = System.Array.IndexOf(SweepSim.IconOrder, SweepSim.IconAlias.TryGetValue(id, out var al) ? al : id);   // ✦ 새 칸은 비슷한 아이콘을 빌린다
            if (iconTex == null || idx < 0) return;
            int cols = 8, rows = Mathf.Max(1, iconTex.height / 96);
            float cw = 1f / cols, ch = 1f / rows;
            GUI.DrawTextureWithTexCoords(r, iconTex, new Rect((idx % cols) * cw, 1f - (idx / cols + 1) * ch, cw, ch));
        }

        static Color VisCol(string id)
        {
            if (id.StartsWith("q_")) return new Color(1f, 0.36f, 0.81f);
            switch (SweepSim.VisBranch(id))
            {
                case "claw": return SweepGame.Amber;
                case "hull": return new Color(0.56f, 0.72f, 0.9f);
                case "route": return new Color(0.6f, 0.72f, 1f);
                case "cross": return new Color(1f, 0.87f, 0.58f);
                case "drone": return SweepGame.Cyan;
                case "bh": return SweepGame.Violet;
                default: return SweepGame.Green;
            }
        }

        static readonly string[] Roman = { "", "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ" };

        // ───────────────────────── 📐 정비 도면 (10-01 사장님 「싸 보인다 · 지저분」 → 시안 A 승인 · https://claude.ai/artifact/WV1DvQ4hB4ciFTTHXBCwTf)
        // 청사진 모눈 · 칸은 얇은 상자 + 살 수 있으면 숨 쉬는 꺾쇠 · 안 산 선 점선 / 산 선 금색 · 잠김은 도트 자물쇠 · 설명은 오른쪽 고정 판
        // 누르면 꾹 눌렸다 튀고 「승인」 도장 · 다음 칸으로 금색 선이 그려진다
        static readonly Color BpBg = new Color(0.039f, 0.086f, 0.149f), BpMinor = new Color(0.055f, 0.133f, 0.22f), BpMajor = new Color(0.07f, 0.188f, 0.322f),
            BpBox = new Color(0.043f, 0.102f, 0.173f), BpEdge = new Color(0.114f, 0.227f, 0.361f), BpTile = new Color(0.169f, 0.29f, 0.431f), BpWire = new Color(0.169f, 0.329f, 0.502f), BpStrip = new Color(0.031f, 0.071f, 0.122f);
        const float PanelW = 226;
        int panelK = -1, pendingBuy = -1;
        readonly Dictionary<int, float> tileDenyT = new Dictionary<int, float>();
        readonly List<(Vector2 p, string s, Color c)> costLbls = new List<(Vector2, string, Color)>();
        GUIStyle pTitle, pBody, pRight;

        bool BpBtn(Rect r, string s, bool hot = false)
        {
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = ov ? new Color(0.06f, 0.14f, 0.235f) : BpBox; GUI.DrawTexture(r, white);
            Frame(r, hot ? SweepGame.Amber : ov ? new Color(0.18f, 0.37f, 0.56f) : BpEdge, 1);
            GUI.color = Color.white; Lbl(r, s, center);
            return Bt(r, GUIContent.none, GUIStyle.none);
        }
        void DashLine(Vector2 a, Vector2 b, Color c, float w, float dash)
        {
            float L = Vector2.Distance(a, b); if (L < 1) return; var d = (b - a) / L; dash = Mathf.Max(2, dash);
            for (float s = 0; s < L; s += dash * 2) Line(a + d * s, a + d * Mathf.Min(L, s + dash), c, w);
        }
        void Brackets(Rect r, Color c, float len, float th)   // 모서리 꺾쇠 넷
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, len, th), white); GUI.DrawTexture(new Rect(r.x, r.y, th, len), white);
            GUI.DrawTexture(new Rect(r.xMax - len, r.y, len, th), white); GUI.DrawTexture(new Rect(r.xMax - th, r.y, th, len), white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - th, len, th), white); GUI.DrawTexture(new Rect(r.x, r.yMax - len, th, len), white);
            GUI.DrawTexture(new Rect(r.xMax - len, r.yMax - th, len, th), white); GUI.DrawTexture(new Rect(r.xMax - th, r.yMax - len, th, len), white);
            GUI.color = Color.white;
        }
        void Padlock(Vector2 p, float u, Color c)   // 도트 자물쇠 7×7 (p = 왼쪽 위 · u = 한 점 크기)
        {
            GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.92f); GUI.DrawTexture(new Rect(p.x - u, p.y - u, u * 9, u * 9), white);
            GUI.color = c;
            GUI.DrawTexture(new Rect(p.x + u, p.y + 3 * u, 5 * u, 4 * u), white);
            GUI.DrawTexture(new Rect(p.x + 2 * u, p.y, 3 * u, u), white);
            GUI.DrawTexture(new Rect(p.x + 2 * u, p.y, u, 3 * u), white); GUI.DrawTexture(new Rect(p.x + 4 * u, p.y, u, 3 * u), white);
            GUI.color = new Color(0.03f, 0.05f, 0.08f); GUI.DrawTexture(new Rect(p.x + 3 * u, p.y + 4 * u, u, 2 * u), white);
            GUI.color = Color.white;
        }
        /// <summary>꾸밈 태그가 있어도 띄어쓰기 자리에서만 줄을 끊는다 (KWrap 은 태그가 있으면 건너뛴다)</summary>
        string KWrapRich(string s, int size, float w)
        {
            if (string.IsNullOrEmpty(s) || w < 20) return s;
            if (kwrapSt == null) kwrapSt = new GUIStyle(label) { wordWrap = false, richText = false };
            kwrapSt.fontSize = size;
            var sb = new System.Text.StringBuilder(); string line = "", plain = "";
            foreach (var word in s.Split(' '))
            {
                string wp = System.Text.RegularExpressions.Regex.Replace(word, "<[^>]+>", "");
                string tp = plain.Length == 0 ? wp : plain + " " + wp;
                if (plain.Length > 0 && kwrapSt.CalcSize(new GUIContent(tp)).x > w) { sb.Append(line).Append('\n'); line = word; plain = wp; }
                else { line = line.Length == 0 ? word : line + " " + word; plain = tp; }
            }
            return sb.Append(line).ToString();
        }

        void Bay()
        {
            if (gtiles == null) BuildGraph();
            var S = sim.S;
            float right = ox + 948;                                          // 오른쪽 세로 탭(ox+964) · 모니터 테(8) 앞에서 멈춘다
            var pr = new Rect(right - PanelW, 84, PanelW, 460);
            var area = new Rect(0, 84, pr.x - 2, 460);
            GUI.color = BpBg; GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            void BayHead()
            {
            // 머리 — 돈 · 청구서 · 궤도일보
            GUI.color = BpStrip; GUI.DrawTexture(new Rect(0, 0, vw, 46), white); GUI.color = BpEdge; GUI.DrawTexture(new Rect(0, 46, vw, 1), white); GUI.color = Color.white;
            big.fontSize = 22;   // 도트 글꼴 11의 배수 (정돈 5)
            float cashLw = dim.CalcSize(new GUIContent(Loc.T("돈"))).x;   // 🌐 이름 폭만큼 띄운다 (영어 Cash 는 더 길다)
            Lbl(new Rect(ox + 16, 12, cashLw + 4, 20), Loc.T("돈"), dim);
            Lbl(new Rect(ox + Mathf.Max(38, 22 + cashLw), 6, 240, 32), "<color=#ffdf95>" + KNum.Fmt(shown) + "</color>", big);
            big.fontSize = 20;
            var cp = RoomPt(new Vector2(ox + 60, 22)); CreditScreen = new Vector2(cp.x * scale, Screen.height - (cp.y + oy) * scale);
            if (S.bill < SweepSim.Bills.Length && !sim.M.cleanReady)
            {
                string bl = Loc.T("청구서 · ") + SweepSim.Bills[S.bill].t + " <color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color>" + (S.overdue ? Loc.T("  <color=#ff8a7a>오늘 납부일</color>") : " · " + Loc.RunsLeft(S.billDue)) + (S.debt > 0 ? Loc.T("  <color=#ffb3a8>빚 ") + KNum.Fmt(S.debt) + "</color>" : "") + (S.cash >= sim.BillAmount ? Loc.T("  <color=#6fcf97>▶ 눌러서 갚기</color>") : "");
                bool loanPay = S.overdue && S.cash < sim.BillAmount && sim.BillAmount - S.cash <= sim.LoanCap;
                if (loanPay) bl += Loc.T("  <color=#6fcf97>▶ 대출 ") + KNum.Fmt(sim.BillAmount - S.cash) + Loc.T(" 받아 갚기</color>");
                if (BpBtn(new Rect(ox + 240, 8, 500, 30), Fit(bl, 13, 492, center, 9), S.cash >= sim.BillAmount || loanPay)) { if (S.cash >= sim.BillAmount) sim.PayBill(); else if (loanPay) RequestLoan(sim.BillAmount - S.cash, true); }   // 10-10 큰 돈 · 마지막 청구서 · 납부일 · 빚이 다 붙으면 칸을 넘었다
            }
            int unreadN = sim.Unread;
            // 오른쪽 끝은 ⚙ 설정 단추(vw-58) 앞에서 멈춘다 — 겹쳐서 설정을 누르면 신문이 열렸다 (09-28)
            if (BpBtn(new Rect(ox + 750, 8, Mathf.Min(166, vw - 66 - (ox + 750)), 30), Loc.T("궤도일보") + (unreadN > 0 ? "  <color=#ff8a7a>● " + unreadN + "</color>" : ""))) { newsOpen = true; newsSel = -1; }
            }

            bool inArea = area.Contains(Event.current.mousePosition);
            int nT = gtiles.Count;
            int[] st = new int[nT];
            Vector2 lo = new Vector2(1e9f, 1e9f), hi = new Vector2(-1e9f, -1e9f);
            for (int k = 0; k < nT; k++)
            {
                if (k == 0) { if (gmemo == null || gmemo.Length != nT) gmemo = new int[nT]; System.Array.Fill(gmemo, -1); }
                st[k] = GTileState(k);
                if (st[k] == 0) continue;
                lo = Vector2.Min(lo, gtiles[k].cell); hi = Vector2.Max(hi, gtiles[k].cell);
            }
            // 화면 맞추기 — 보이는 칸이 다 들어오게 (끌면 옮겨진다)
            float cellPx = 64f;
            var size = (hi - lo + Vector2.one * 2f) * cellPx;
            float wantZ = Mathf.Clamp(Mathf.Min(area.width / size.x, area.height / size.y), 0.45f, 1.15f);
            var wantC = (lo + hi) / 2f;
            camZ = Mathf.Lerp(camZ, wantZ, 1 - Mathf.Exp(-Time.deltaTime * 4));
            camC = Vector2.Lerp(camC, wantC, 1 - Mathf.Exp(-Time.deltaTime * 4));
            var ev = Event.current;
            // 🔍 확대 (09-24 사장님 「PC 게임이니 확대」) — 휠 = 마우스 자리를 중심으로 0.6~3배 · 끌기(왼쪽/오른쪽) = 이동 · [+][−][맞춤]
            void ZoomAt(Vector2 at, float nz)
            {
                nz = Mathf.Clamp(nz, 0.6f, 3f);
                float z0 = camZ * userZ, z1 = camZ * nz;
                var wc = (at - area.center - pan) / (cellPx * z0);      // 마우스 아래 칸 좌표 (camC 기준)
                pan = at - area.center - wc * cellPx * z1; userZ = nz;
            }
            if (ev.type == EventType.ScrollWheel && area.Contains(ev.mousePosition)) { ZoomAt(ev.mousePosition, userZ * (ev.delta.y > 0 ? 1 / 1.15f : 1.15f)); ev.Use(); }
            if (ev.type == EventType.MouseDown && (ev.button == 1 || ev.button == 0) && area.Contains(ev.mousePosition)) { dragging = true; dragMoved = false; dragBtn = ev.button; dragFrom = dragStart = ev.mousePosition; }
            if (ev.type == EventType.MouseDrag && dragging)
            {
                if (!dragMoved && (ev.mousePosition - dragStart).sqrMagnitude > 36) dragMoved = true;   // 6px 넘게 움직여야 끌기 — 칸 누르기와 안 헷갈리게
                if (dragMoved) { pan += ev.mousePosition - dragFrom; dragFrom = ev.mousePosition; ev.Use(); }
            }
            if (ev.type == EventType.MouseUp && dragging && ev.button == dragBtn) { dragging = false; if (dragMoved) ev.Use(); }
            float zz = camZ * userZ;
            Vector2 ToScr(Vector2Int c) => area.center + pan + ((Vector2)c - camC) * cellPx * zz;
            float tile = 44f * zz;
            // 📐 모눈 — 굵은 선이 칸 자리를 지난다 (트리와 같이 움직인다) · 가는 선은 다섯 칸
            {
                float maj = cellPx * zz, mn = maj / 5; var o = ToScr(Vector2Int.zero);
                for (int pass = mn >= 7 ? 0 : 1; pass < 2; pass++)
                {
                    float step = pass == 0 ? mn : maj; GUI.color = pass == 0 ? BpMinor : BpMajor;
                    for (float x = area.x + Mathf.Repeat(o.x - area.x, step); x < area.xMax; x += step) GUI.DrawTexture(new Rect(x, area.y, 1, area.height), white);
                    for (float y = area.y + Mathf.Repeat(o.y - area.y, step); y < area.yMax; y += step) GUI.DrawTexture(new Rect(area.x, y, area.width, 1), white);
                }
                GUI.color = new Color(0.01f, 0.03f, 0.06f, 0.85f); GUI.DrawTexture(area, texVignette); GUI.color = Color.white;
            }
            // 선 — 안 산 길은 점선 · 둘 다 산 것이면 금색
            for (int k = 0; k < nT; k++)
            {
                if (st[k] == 0) continue;
                var t = gtiles[k];
                var pars = new List<int>(t.xpar); if (t.lpar >= 0) pars.Add(t.lpar);
                foreach (var pk in pars)
                {
                    if (st[pk] == 0) continue;
                    bool gold = st[k] == 3 && st[pk] == 3;
                    var a = ToScr(gtiles[pk].cell); var b = ToScr(t.cell);
                    if (!gold && pk != t.lpar)
                    {   // ✕ 연결 칸의 둘째 선 — 안 샀으면 흐리게, 마우스를 올린 칸 것만 밝게 (09-26 정돈 15)
                        bool hl = treeHover == k || treeHover == pk;
                        DashLine(a, b, new Color(BpWire.r, BpWire.g, BpWire.b, hl ? 1f : 0.4f), (hl ? 2f : 1.2f) * zz, 3 * zz);
                        continue;
                    }
                    if (gold) Line(a, b, SweepGame.Amber, 2.5f * zz);
                    else DashLine(a, b, new Color(BpWire.r, BpWire.g, BpWire.b, st[k] == 1 ? 0.4f : 1f), 1.5f * zz, 4 * zz);
                }
            }
            // ✨ 산 칸 → 다음 칸으로 금색 선이 그려진다 (잉크) · 닿으면 잠깐 남았다 사라진다
            {
                float now = Time.unscaledTime;
                for (int i = buyFlows.Count - 1; i >= 0; i--)
                {
                    var f = buyFlows[i]; float e = now - f.t0;
                    if (e > FlowLen + PingLen) { buyFlows.RemoveAt(i); continue; }
                    if (e < 0 || f.from >= nT || f.to >= nT || st[f.to] == 0) continue;
                    var a = ToScr(gtiles[f.from].cell); var b = ToScr(gtiles[f.to].cell);
                    float u = Mathf.Clamp01(e / FlowLen); u = 1 - (1 - u) * (1 - u);
                    float al = e > FlowLen ? 1 - (e - FlowLen) / PingLen : 1;
                    Line(a, Vector2.Lerp(a, b, u), new Color(1f, 0.76f, 0.31f, al), 2.5f * zz);
                }
                GUI.color = Color.white;
            }
            // 칸
            int hover = -1;
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            bool showCost = zz >= 0.6f;
            var m0 = GUI.matrix;
            for (int k = 0; k < nT; k++)
            {
                if (st[k] == 0) continue;
                var t = gtiles[k];
                bool isRoot = t.stat < 0;
                var n = isRoot ? SweepSim.Nodes[0] : SweepSim.Nodes[t.stat];
                Color bcol = isRoot ? SweepGame.Amber : VisCol(n.id);
                bool next = st[k] == 2, owned = st[k] == 3, sil = st[k] == 1;
                var ns = isRoot ? NodeSt.Max : sim.State(t.stat);
                bool inf = !isRoot && SweepSim.Infinite(t.stat);
                bool can = (next || inf && owned) && ns == NodeSt.Can;
                bool lockedTile = !owned && ns == NodeSt.Locked;
                bool circle = !isRoot && n.id.StartsWith("q_");
                bool diamond = !isRoot && !circle && n.max == 1;
                var pc = ToScr(t.cell);
                if (tileDenyT.TryGetValue(k, out var dt0)) { float de = Time.unscaledTime - dt0; if (de > 0.32f) tileDenyT.Remove(k); else if (!reduceMotion) pc.x += Mathf.Sin(de * 60) * 4 * (1 - de / 0.32f); }   // 🚫 안 되는 칸 — 도리도리
                float grow = isRoot ? 0 : nodePulse[t.stat] * 6 * zz;
                float sz = ((isRoot ? tile * 1.25f : diamond ? tile * 0.92f : tile) + grow) * BuyScale(k);
                var r = new Rect(pc.x - sz / 2, pc.y - sz / 2, sz, sz);
                if (diamond) GUI.matrix = m0 * Matrix4x4.TRS(new Vector3(pc.x, pc.y, 0), Quaternion.Euler(0, 0, 45), Vector3.one) * Matrix4x4.TRS(new Vector3(-pc.x, -pc.y, 0), Quaternion.identity, Vector3.one);
                GUI.color = owned ? Color.Lerp(BpBox, bcol, 0.3f) : sil ? BpBg : BpBox; GUI.DrawTexture(r, circle ? texDisc : white);
                float bfl = BuyFlash(k); if (bfl > 0) { GUI.color = new Color(1f, 0.85f, 0.5f, 0.55f * bfl); GUI.DrawTexture(r, circle ? texDisc : white); }   // ✨ 산 순간 금빛
                if (circle) { GUI.color = owned ? bcol : sil ? BpEdge : BpTile; GUI.DrawTexture(r, texRing); GUI.color = Color.white; }
                else if (sil) DashFrame(r, new Color(0.14f, 0.28f, 0.44f));
                else if (lockedTile) DashFrame(r, new Color(0.3f, 0.42f, 0.58f));
                else Frame(r, owned ? bcol : BpTile, owned ? 1.5f : 1f);
                GUI.matrix = m0;
                if (can) { float br = (5 + 3 * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f + k * 0.3f))) * zz; Brackets(new Rect(r.x - br, r.y - br, r.width + br * 2, r.height + br * 2), SweepGame.Amber, 9 * zz, Mathf.Max(1.5f, 2 * zz)); }   // 살 수 있다 — 꺾쇠가 숨 쉰다
                else if (k == panelK && !isRoot && !sil) Brackets(new Rect(r.x - 5 * zz, r.y - 5 * zz, r.width + 10 * zz, r.height + 10 * zz), new Color(0.91f, 0.95f, 0.98f), 9 * zz, Mathf.Max(1.5f, 2 * zz));   // 판에 보이는 칸
                // 아이콘 — 칸 안에 글자 없이 (시안에서 구운 tree_icons.png)
                GUI.color = owned ? new Color(1f, 0.96f, 0.86f) : next ? (lockedTile ? new Color(0.3f, 0.36f, 0.44f) : new Color(0.74f, 0.8f, 0.88f)) : new Color(0.16f, 0.22f, 0.3f);
                float isz = sz * 0.62f;
                int planetI = isRoot ? -1 : System.Array.IndexOf(SweepSim.PlanetNode, n.id);
                if (planetI > 0) { var plr = new Rect(pc.x - isz * 0.62f, pc.y - isz * 0.62f, isz * 1.24f, isz * 1.24f); GUI.color = owned ? Color.white : next ? new Color(0.6f, 0.62f, 0.66f) : new Color(0.2f, 0.22f, 0.26f); var ps = PlanetArt.Icon(planetI); GUI.DrawTextureWithTexCoords(plr, ps.texture, SprUV(ps)); }
                else DrawIcon(new Rect(pc.x - isz / 2, pc.y - isz / 2, isz, isz), isRoot ? "R" : n.id);
                if (!isRoot && SweepSim.KeyNodes.Contains(n.id) && !sil) { float kp = sim.S.keys > 0 && next ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3 + k) : 0.3f; GUI.color = new Color(0.71f, 0.61f, 1f, owned ? 0.9f : 0.35f + 0.3f * kp); Frame(new Rect(r.x - 3 * zz, r.y - 3 * zz, r.width + 6 * zz, r.height + 6 * zz), GUI.color, 1.5f); GUI.color = Color.white; }   // 열쇠 칸 — 보라 테 · 열쇠가 있을 때만 깜빡
                if (!isRoot && n.max == 1 && System.Array.IndexOf(SweepSim.WeaponNode, n.id) == sim.Weapon && owned) { GUI.color = new Color(1f, 0.55f, 0.5f); Lbl(new Rect(pc.x - 40, r.yMax + 2 * zz, 80, 16), Loc.T("<size=10><b>장착 중</b></size>"), center); GUI.color = Color.white; }
                if (lockedTile && next) { float u = Mathf.Max(1f, Mathf.Round(1.6f * zz)); bool gate = gtiles[k].stat >= 0 && (sim.CapLocked(gtiles[k].stat) || n.id.StartsWith("p_")); Padlock(new Vector2(r.xMax - 4 * u, r.yMax - 4 * u), u, gate ? new Color(1f, 0.7f, 0.42f) : new Color(0.6f, 0.65f, 0.72f)); }   // 🔒 잠김 = 자물쇠 (주황 = 관문을 부수면 풀린다)
                if (showCost && (next && !lockedTile || inf && owned))
                    costLbls.Add((new Vector2(pc.x, r.yMax), (inf && owned ? "∞" + sim.S.lv[t.stat] + " · " : "") + KNum.Short(sim.TileCost(t.stat)), can ? new Color(1f, 0.87f, 0.58f) : new Color(0.5f, 0.56f, 0.64f)));   // 값은 칸 아래에 바로
                GUI.color = Color.white;
                if (inArea && r.Contains(ev.mousePosition)) hover = k;
                bool clicked = !isRoot && (next || inf && owned) && (pendingBuy == k || inArea && Bt(r, GUIContent.none, GUIStyle.none));
                if (clicked && ns == NodeSt.Can)
                {
                    int times = shift ? 5 : 1; double cash0 = sim.S.cash;
                    while (times-- > 0 && sim.State(t.stat) == NodeSt.Can) sim.BuyTile(t.stat);
                    BuyAnim(k, cash0 - sim.S.cash >= 1 ? "-" + KNum.Short(cash0 - sim.S.cash) : null);
                    if (n.id == "e_shop") { roomGo = 5; roomGoAt = Time.unscaledTime + 0.5f; }                // 🧭 09-27 사장님 「증권 · 가게 스킬 열리면 바로 거기 가게」 — 사는 효과를 보고 그 방으로
                    else if (n.id == "v_volley" && sim.S.lv[t.stat] == 1) Guide(Loc.T("전탄 발사 해금 — 출동 중 게이지가 차면 Space · 계기판 가운데 단추 (무기 2개부터)"));
                    else if (n.id == "a_open") { roomGo = 4; roomGoAt = Time.unscaledTime + 0.5f; Guide(Loc.T("직접 매수 해금 — 증권에서 원하는 종목 매수 가능")); }
                    nodePulse[t.stat] = 1; OrbitSfx.Play("buy", 0.7f, 0.01f, 0.15f); lastBuyBranch = n.branch; panelK = k;
                    if (SweepSim.KeyNodes.Contains(n.id)) BuyFx(pc, new Color(0.71f, 0.61f, 1f), true, Loc.T("핵심 해금!"));   // 핵심 칸만 큰 글자 — 나머지는 도장으로
                }
                else if (clicked && ns != NodeSt.Max)
                { OrbitSfx.Play("clank", 0.35f, 0.05f, 0f); tileDenyT[k] = Time.unscaledTime; int bk = ns == NodeSt.Hidden ? BlockTile(k) : -1; Deny(pc, bk >= 0 ? TileName(bk) + Loc.T(" 먼저") : WhyNot(t.stat)); }   // 🚫 안 눌리는 칸 — 왜 안 되는지 그 자리에 (09-25 사장님 「안 눌리는 게 있던데」)
            }
            pendingBuy = -1;
            // 💲 값 딱지 — 칸을 다 그린 뒤 맨 위에 (옆 칸에 깔리지 않게)
            foreach (var (cp, cs, cc) in costLbls)
            {
                string ct = "<size=10><color=#" + ColorUtility.ToHtmlStringRGB(cc) + ">" + cs + "</color></size>";
                float cw = center.CalcSize(new GUIContent(ct)).x + 8; var cr = new Rect(cp.x - cw / 2, cp.y - 7, cw, 16);
                GUI.color = new Color(BpStrip.r, BpStrip.g, BpStrip.b, 0.92f); GUI.DrawTexture(cr, white); GUI.color = Color.white; Lbl(cr, ct, center);
            }
            costLbls.Clear();
            // ✨ 「승인」 도장 · 잉크 번짐 · 다음 칸에 닿은 선 끝 상자 · 쓴 돈이 금색으로 떠오른다
            {
                float now = Time.unscaledTime;
                foreach (var kv in tileBuyT)
                {
                    if (kv.Key >= nT || st[kv.Key] == 0) continue;
                    float e = now - kv.Value; var p = ToScr(gtiles[kv.Key].cell);
                    if (e < 0.55f && !reduceMotion) { float s = tile * (1 + 1.4f * e / 0.55f) / 2; GUI.color = new Color(1f, 0.76f, 0.31f, 1 - e / 0.55f); Frame(new Rect(p.x - s, p.y - s, s * 2, s * 2), GUI.color, 1); }
                    if (e < 0.7f)
                    {
                        float k1 = Mathf.Clamp01(e / 0.154f), sc = reduceMotion ? 1 : Mathf.Lerp(1.7f, 1f, k1 * k1), al = e < 0.154f ? k1 : e > 0.49f ? 1 - (e - 0.49f) / 0.21f : 1;
                        float hs = 40 * zz * sc;
                        GUI.matrix = m0 * Matrix4x4.TRS(new Vector3(p.x, p.y, 0), Quaternion.Euler(0, 0, -9), Vector3.one) * Matrix4x4.TRS(new Vector3(-p.x, -p.y, 0), Quaternion.identity, Vector3.one);
                        var sr = new Rect(p.x - hs, p.y - hs, hs * 2, hs * 2);
                        GUI.color = new Color(0.04f, 0.09f, 0.15f, 0.55f * al); GUI.DrawTexture(sr, white);
                        Frame(sr, new Color(1f, 0.76f, 0.31f, al), Mathf.Max(1.5f, 2 * zz));
                        GUI.color = new Color(1, 1, 1, al); Lbl(sr, "<size=" + Mathf.Clamp(Mathf.RoundToInt(12 * zz * sc), 9, 26) + "><color=#f2c24f>" + Loc.T("승인") + "</color></size>", center);
                        GUI.matrix = m0; GUI.color = Color.white;
                    }
                }
                foreach (var f in buyFlows)
                {
                    float e = now - f.t0 - FlowLen; if (e < 0 || e > PingLen || f.to >= nT || st[f.to] == 0) continue;
                    var p = ToScr(gtiles[f.to].cell); float k2 = e / PingLen, R = tile * (0.55f + 0.5f * k2);
                    Frame(new Rect(p.x - R, p.y - R, R * 2, R * 2), new Color(1f, 0.86f, 0.45f, 0.8f * (1 - k2)), 1.5f);
                }
                for (int i = buyCoins.Count - 1; i >= 0; i--)
                {
                    var c = buyCoins[i]; float e = now - c.t0; if (e > 0.9f || c.k >= nT) { buyCoins.RemoveAt(i); continue; }
                    var p = ToScr(gtiles[c.k].cell); float a = Mathf.Clamp01((0.9f - e) / 0.3f);
                    GUI.color = new Color(1, 1, 1, a); Lbl(new Rect(p.x - 60, p.y - tile * 0.9f - 26 * e, 120, 20), "<size=13><b><color=#ffdf95>" + c.txt + "</color></b></size>", center);
                }
                GUI.color = Color.white;
            }
            if (testTip >= 0) { for (int k = 0; k < nT; k++) if (gtiles[k].stat >= 0 && SweepSim.Nodes[gtiles[k].stat].id == testTipId) hover = k; }   // 에디터 시험용
            treeHover = hover;
            if (hover >= 0 && gtiles[hover].stat >= 0 && st[hover] != 3 && (st[hover] == 1 || sim.State(gtiles[hover].stat) == NodeSt.Hidden))
            {   // 🔗 막고 있는 칸을 깜빡 — 「먼저 이것」
                int bk = BlockTile(hover);
                if (bk >= 0 && st[bk] != 0 && area.Contains(ToScr(gtiles[bk].cell)))
                {
                    var bp = ToScr(gtiles[bk].cell); float bp2 = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8), bs = tile * (1.3f + 0.2f * bp2);
                    Brackets(new Rect(bp.x - bs / 2, bp.y - bs / 2, bs, bs), new Color(1f, 0.6f, 0.55f, 0.5f + 0.4f * bp2), 9 * zz, 2);
                    Lbl(new Rect(bp.x - 60, bp.y - tile * 0.5f - 20, 120, 18), Loc.T("<size=11><b><color=#ff9b8f>먼저 이 칸</color></b></size>"), center);
                }
            }
            // 영역 밖 띠 — 넘어간 칸을 덮고 머리 · 줄 · 판 · 아래 안내를 다시 그린다
            GUI.color = BpStrip; GUI.DrawTexture(new Rect(0, 0, vw, area.y), white); GUI.DrawTexture(new Rect(0, area.yMax, vw, RefH - area.yMax), white);
            GUI.color = BpEdge; GUI.DrawTexture(new Rect(0, area.y - 1, vw, 1), white); GUI.DrawTexture(new Rect(0, area.yMax, vw, 1), white);
            GUI.color = Color.white;
            BayHead();
            {   // 둘째 줄 — 구역 진행 · 관문 · 열쇠 · 부품 가게 · 확대
                float y = 54, zx = right - 166;
                GUI.color = new Color(0.07f, 0.06f, 0.11f);
                float bw3 = 36;
                if (BpBtn(new Rect(zx, y - 1, bw3, 26), "<size=16>−</size>")) ZoomAt(area.center, userZ / 1.25f);
                if (BpBtn(new Rect(zx + bw3 + 3, y - 1, bw3, 26), "<size=16>+</size>")) ZoomAt(area.center, userZ * 1.25f);
                if (BpBtn(new Rect(zx + (bw3 + 3) * 2, y - 1, 40, 26), Loc.T("<size=11>맞춤</size>"))) { userZ = 1; pan = Vector2.zero; }
                Lbl(new Rect(zx + (bw3 + 3) * 2 + 44, y + 2, 44, 20), "<size=11><color=#8a93a3>" + Mathf.RoundToInt(userZ * 100) + "%</color></size>", center);
                float endX = zx - 10;
                if (sim.ShopOpen)
                {
                    int filled = 0; foreach (var pp in S.parts) if (pp >= 0) filled++;
                    var b = new Rect(endX - 150, y - 1, 150, 26); endX = b.x - 6;
                    if (BpBtn(b, Loc.T("<size=12><color=#ffdf95>부품 가게 ▸</color>  <color=#8a9bb3>") + filled + "/5</color></size>", true)) { GoFlow(5); OrbitSfx.Play("tick", 0.6f); }
                }
                if (S.keys > 0 || sim.ShopOpen)
                {
                    var kr = new Rect(endX - 70, y - 1, 70, 26); endX = kr.x - 6;
                    GUI.color = new Color(0.08f, 0.07f, 0.15f); GUI.DrawTexture(kr, white); Frame(kr, new Color(KeyCol.r, KeyCol.g, KeyCol.b, 0.7f), 1); GUI.color = Color.white;
                    Lbl(kr, Loc.T("<size=12><color=#d8ccff>열쇠 <b>") + S.keys + "</b></color></size>", center);
                }
                GUI.color = Color.white;
                int zo = sim.ZoneOpen, zl = sim.ZoneLeft(zo), zt = 0; for (int i = 0; i < SweepSim.Nodes.Length; i++) if (SweepSim.Zone[i] == zo && SweepSim.ZoneNeed(i)) zt++;
                float x = ox + 16;
                string zn = "<size=12><color=#ffdf95>" + SweepSim.ZoneName[zo] + "</color></size>";
                float zw = label.CalcSize(new GUIContent(zn)).x; Lbl(new Rect(x, y + 1, zw + 4, 22), zn, label); x += zw + 8;
                if (zt > 0)
                {
                    float sw = Mathf.Min(12, (110 - 3 * (zt - 1)) / (float)zt);
                    for (int i = 0; i < zt; i++) { GUI.color = i < zt - zl ? SweepGame.Amber : BpEdge; GUI.DrawTexture(new Rect(x + i * (sw + 3), y + 8, sw, 7), white); }
                    GUI.color = Color.white; x += zt * (sw + 3) + 4;
                    string zc = "<size=12><color=#bcd3ea>" + (zt - zl) + "/" + zt + "</color></size>"; float cw = label.CalcSize(new GUIContent(zc)).x;
                    Lbl(new Rect(x, y + 1, cw + 4, 22), zc, label); x += cw + 10;
                }
                string zs = sim.HasGate && !sim.GateReady
                    ? "<color=#8a93a3>" + Loc.T("</color> 한 줄 ") + sim.TileCap + Loc.T("칸까지 · <color=#ffb36b>🛰 청구서 ") + sim.GateBill + Loc.T("장 납부 시 ") + sim.GateName + Loc.T(" 방어막 해제</color></size>")
                    : sim.HasGate
                    ? "<color=#8a93a3>" + Loc.T("</color> 한 줄 ") + sim.TileCap + Loc.T("칸까지 · <color=#ffb36b>🛰 ") + sim.GateName + " " + Mathf.CeilToInt((float)sim.GateLeft * 100) + Loc.T("%</color> <color=#8a93a3>부수면 ") + sim.NextName + "</color>"
                    : "<color=#8a93a3>" + Loc.T("</color> <color=#8a93a3>— 마지막 항로 · 한도 없음</color></size>");
                if (endX - x > 30) Marquee(new Rect(x, y + 1, endX - x, 22), zs.Replace("</size>", ""), 12, label);   // 🛰 09-26 구역 칸 개수 → 관문 · 길면 흐른다
            }
            {   // 아래 줄 — 알림표 · 조작
                float y = area.yMax + 6, x = ox + 16;
                void LegendKey(Color c, bool hollow, string s)
                {
                    var kr = new Rect(x, y + 4, 10, 10);
                    if (hollow) Brackets(new Rect(kr.x - 1, kr.y - 1, 12, 12), c, 4, 1.5f); else { GUI.color = c; GUI.DrawTexture(kr, white); GUI.color = Color.white; }
                    string t = "<size=11><color=#8a93a3>" + s + "</color></size>"; float w = label.CalcSize(new GUIContent(t)).x;
                    Lbl(new Rect(x + 16, y, w + 4, 18), t, label); x += w + 34;
                }
                LegendKey(SweepGame.Amber, false, Loc.T("보유 칸")); LegendKey(SweepGame.Amber, true, Loc.T("구매 가능")); LegendKey(BpTile, false, Loc.T("아직 · 잠김"));
                Lbl(new Rect(x, y, right - x, 18), Loc.T("<size=11><color=#8a93a3>휠 = 확대 · 끌기 = 이동 · Shift+누르기 = 다섯 번</color></size>"), pRight ?? (pRight = new GUIStyle(label) { alignment = TextAnchor.UpperRight }));
            }
            if (hover < 0 && TestTip >= 0 && TestTip < nT && st[TestTip] != 0) hover = TestTip;   // 에디터 시험용 — 이 칸 설명을 띄운다
            if (hover >= 0) panelK = hover;
            BayPanel(pr, panelK, st);
        }

        GUIStyle tipWrap; Rect recLbl; readonly List<(Rect, Color)> lockLbls = new List<(Rect, Color)>();
        public int testTip = -1; public string testTipId;                  // 에디터 시험용 — 툴팁 강제로 띄우기
        /// <summary>오른쪽 고정 판 — 칸 이름 · 단계 · 설명 · 지금 ▸ 사면 · 값 · [사기] (마우스를 떼도 마지막 칸이 남는다)</summary>
        void BayPanel(Rect pr, int k, int[] st)
        {
            GUI.color = new Color(0.027f, 0.059f, 0.102f, 0.97f); GUI.DrawTexture(pr, white); GUI.color = BpEdge; GUI.DrawTexture(new Rect(pr.x, pr.y, 1, pr.height), white); GUI.color = Color.white;
            if (pTitle == null) { pTitle = new GUIStyle(label) { fontSize = 16, wordWrap = true }; pBody = new GUIStyle(label) { fontSize = 12, wordWrap = true }; }
            float x = pr.x + 14, w = pr.width - 28, y = pr.y + 12;
            void Text(string s, GUIStyle gs, float gap) { float h = gs.CalcHeight(new GUIContent(s), w); Lbl(new Rect(x, y, w, h), s, gs); y += h + gap; }
            void Rule() { GUI.color = BpEdge; GUI.DrawTexture(new Rect(x, y, w, 1), white); GUI.color = Color.white; y += 9; }
            if (k < 0 || k >= gtiles.Count || st[k] == 0 || gtiles[k].stat < 0)
            {
                Text(Loc.T("<color=#e8f1fb>청소선</color>"), pTitle, 8);
                Text("<color=#c9c3b6>" + KWrap(Loc.T("여기서 다섯 갈래로 뻗어 나감. 칸에 마우스를 올리면 여기에 설명 표시 — 꺾쇠가 움직이는 칸은 지금 구매 가능."), 12, w) + "</color>", pBody, 0);
                return;
            }
            var t = gtiles[k]; var n = SweepSim.Nodes[t.stat]; var ns = sim.State(t.stat); int vis = st[k];
            if (vis == 1)
            {
                Text("<color=#8aa0b8>?</color>", pTitle, 8);
                int bk = BlockTile(k);
                Text("<color=#c9c3b6>" + KWrapRich(bk >= 0 ? "「" + TileName(bk) + Loc.T("」 구매 시 공개") : Loc.T("앞 칸 구매 시 공개"), 12, w) + "</color>", pBody, 0);
                return;
            }
            Text("<color=#" + ColorUtility.ToHtmlStringRGB(VisCol(n.id)) + ">" + KWrap(TileName(k), 16, w) + "</color>", pTitle, 6);
            int tiles = SweepSim.Tiles(t.stat);
            if (tiles > 1 && tiles <= 9)
            {
                for (int j = 1; j <= tiles; j++) { GUI.color = sim.S.lv[t.stat] >= SweepSim.TileLv(t.stat, j) ? SweepGame.Amber : BpEdge; GUI.DrawTexture(new Rect(x + (j - 1) * 22, y, 18, 6), white); }
                GUI.color = Color.white; y += 14;
            }
            Text("<color=#c9c3b6>" + KWrap(sim.NodeDesc(t.stat), 12, w) + "</color>", pBody, 10);
            Rule();
            int from = t.j == 1 ? 0 : SweepSim.TileLv(t.stat, t.j - 1), to = SweepSim.TileLv(t.stat, t.j);
            bool inf = SweepSim.Infinite(t.stat);
            if (vis == 3 && !inf) Text(Loc.T("<color=#8a93a3>지금</color>  <color=#ffdf95>") + Val(n.id, to) + "</color>", pBody, 6);   // 산 칸은 지금 값만
            else Text("<color=#8a93a3>" + Loc.T("지금 ") + "</color>" + Val(n.id, from) + "\n<color=#8a93a3>" + Loc.T("구매 후 ") + "</color><color=#ffdf95>" + Val(n.id, to) + "</color>", pBody, 6);
            Rule();
            int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
            string foot;
            if (vis == 3 && inf) foot = "<color=#ffdf95>∞ " + sim.S.lv[t.stat] + Loc.T("번 구매 · 반복 구매 가능</color>");   // 누적 칸 — 다음 가격은 단추에
            else if (vis == 3) foot = Loc.T("<color=#6fcf97>보유 중</color>");
            else if (ns == NodeSt.Locked && n.id.StartsWith("p_")) foot = "<color=#ffb36b>🛰 " + (sim.HasGate ? (sim.GateReady ? "" : Loc.T("청구서 ") + sim.GateBill + Loc.T("장을 갚으면 나타나는 ")) + sim.GateName + Loc.T(" 관문을 부수면 해금") : Loc.T("앞 항로부터")) + "</color>";   // 🛰 항로 = 관문
            else if (ns == NodeSt.Locked && sim.CapLocked(t.stat)) foot = Loc.T("<color=#ffb36b>행성 한도 — ") + sim.GateName + Eul(sim.GateName) + Loc.T(" 부수면 한 칸 더</color>");
            else if (ns == NodeSt.Locked && SweepSim.Zone[t.stat] > sim.ZoneOpen) foot = "<color=#ff9b8f>" + SweepSim.ZoneName[SweepSim.Zone[t.stat]] + Loc.T(" 항로를 열면 해금</color>");
            else if (ns == NodeSt.Locked) foot = SweepSim.Ring4(n.id) ? Loc.T("<color=#ff9b8f>목성 항로를 열면 — 외행성 면허</color>") : Loc.T("<color=#ff9b8f>청구서 ") + SweepSim.BranchNeed[b] + Loc.T("을 갚으면 해금</color>");
            else if (ns == NodeSt.Hidden) { int bk = BlockTile(k); foot = "<color=#ff9b8f>" + (bk >= 0 ? "「" + TileName(bk) + Loc.T("」 먼저 구매 필요") : Loc.T("앞 칸 먼저 구매 필요")) + "</color>"; }   // 어느 칸인지 콕 집어 (09-25 사장님)
            else if (SweepSim.KeyNodes.Contains(n.id) && sim.S.keys < 1) foot = Loc.T("<color=#ff9b8f>+ 열쇠 1 (없음)</color>");   // 돈은 되는데 열쇠가 없다
            else if (ns == NodeSt.Poor) foot = "<color=#ff9b8f>" + Loc.T("돈 ") + KNum.Fmt(sim.TileCost(t.stat) - sim.S.cash) + Loc.T(" 부족") + "</color>";
            else foot = SweepSim.KeyNodes.Contains(n.id) ? Loc.T("  <color=#d8ccff>+ 열쇠 1</color>").Trim() : "";
            if (foot.Length > 0) Text(KWrapRich(foot, 12, w), pBody, 6);
            bool keyNote = vis != 3 && SweepSim.KeyNodes.Contains(n.id) && ns != NodeSt.Locked && ns != NodeSt.Hidden;
            if (keyNote) Text("<size=11><color=#b9a9ee>" + KWrap(sim.S.keys < 1 ? Loc.T("열쇠 0 — 청구서를 갚거나 파산하면 +1") : Loc.T("가진 열쇠 ") + sim.S.keys + Loc.T(" · ◆ 핵심 칸은 파산해도 유지"), 11, w) + "</color></size>", pBody, 6);
            // [사기] — 칸을 누르는 것과 같다
            var bb = new Rect(x, pr.yMax - 52, w, 38);
            bool canBuy = (vis == 2 || inf && vis == 3) && ns == NodeSt.Can;
            string bt = vis == 3 && !inf ? Loc.T("보유 중") : ns == NodeSt.Locked || ns == NodeSt.Hidden ? Loc.T("잠김") : Loc.T("구매 · ") + KNum.Fmt(sim.TileCost(t.stat));
            bool ov = canBuy && bb.Contains(Event.current.mousePosition);
            GUI.color = canBuy ? (ov ? new Color(1f, 0.82f, 0.4f) : SweepGame.Amber) : BpBox; GUI.DrawTexture(bb, white); if (!canBuy) Frame(bb, BpEdge, 1); GUI.color = Color.white;
            Lbl(bb, Fit("<color=#" + (canBuy ? "1c1405" : "56708f") + ">" + bt + "</color>", 14, bb.width - 10, center, 10), center);
            if (canBuy && Bt(bb, GUIContent.none, GUIStyle.none)) pendingBuy = k;
        }

        void Line(Vector2 a, Vector2 b, Color col, float w)
        {
            var m = GUI.matrix;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = m * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, ang), Vector3.one);
            GUI.color = col; GUI.DrawTexture(new Rect(0, -w / 2, (b - a).magnitude, w), white);
            GUI.matrix = m; GUI.color = Color.white;
        }

        string Val(string id, int l)
        {
            switch (id)
            {
                case "c_pow": return Loc.T("피해 ") + (1 + l);
                case "c_rad": return l > 0 ? Loc.T("반지름 ") + (20 + 5 * l) : Loc.T("한 점");
                case "c_spd": return Loc.T("화력 +") + 8 * l + "%";
                case "a_open": return l > 0 ? Loc.T("열림") : Loc.T("잠김");
                case "w_hub": return l > 0 ? Loc.T("무기 효과 열림") : Loc.T("잠김");
                case "w_laser": case "w_chain": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_laser_e": case "w_chain_e": case "w_vac_e": case "w_mine_e": case "w_frz_e": case "w_clus_e": case "w_mag_e": case "w_rail_e": return l > 0 ? Loc.T("특화 ") + l + Loc.T("단계") : Loc.T("없음");   // ◇ 무기 특화
                case "w_laser_u": return new[] { Loc.T("없음"), Loc.T("굵기 +50%"), Loc.T("굵기 +50% · 열 축적") }[Mathf.Min(2, l)];
                case "w_chain_u": return new[] { Loc.T("없음"), Loc.T("7번 튐"), Loc.T("7번 · 튈수록 ×1.2") }[Mathf.Min(2, l)];
                case "w_laser_a": case "w_chain_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "e_shop": return l > 0 ? Loc.T("부품 가게 열림") : Loc.T("잠김");
                case "x_claw_arm": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "x_arm_drone": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "x_drone_bh": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "x_bh_eco": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "x_eco_route": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "x_route_claw": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "i_claw": return Loc.T("화력 +") + 10 * l + "%";
                case "i_drone": return Loc.T("드론 +") + 10 * l + "%";
                case "i_bh": return Loc.T("블랙홀 확률 +") + (0.2 * l).ToString("0.#") + "%";
                case "i_eco": return Loc.T("값 +") + 8 * l + "%";
                case "i_route": return Loc.T("행성 값 +") + 5 * l + "%";
                case "w_vac": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_vac_u": return new[] { Loc.T("없음"), Loc.T("1단계"), Loc.T("2단계") }[Mathf.Min(2, l)];
                case "w_vac_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "w_mine": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_mine_u": return new[] { Loc.T("없음"), Loc.T("1단계"), Loc.T("2단계") }[Mathf.Min(2, l)];
                case "w_mine_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "w_frz": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_frz_u": return new[] { Loc.T("없음"), Loc.T("1단계"), Loc.T("2단계") }[Mathf.Min(2, l)];
                case "w_frz_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "w_clus": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_clus_u": return new[] { Loc.T("없음"), Loc.T("1단계"), Loc.T("2단계") }[Mathf.Min(2, l)];
                case "w_clus_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "w_mag": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_mag_u": return new[] { Loc.T("없음"), Loc.T("1단계"), Loc.T("2단계") }[Mathf.Min(2, l)];
                case "w_mag_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "w_rail": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_rail_u": return new[] { Loc.T("없음"), Loc.T("1단계"), Loc.T("2단계") }[Mathf.Min(2, l)];
                case "w_rail_a": return l > 0 ? Loc.T("각성!") : Loc.T("잠김");
                case "k_claw": case "k_drone": case "k_bh": case "k_eco": case "k_route": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "w_slot2": return l > 0 ? Loc.T("발동률 ×1.5") : Loc.T("없음");
                case "v_volley": { double[] du = { 0, 0.5, 0.8, 1.1 }, gp = { 0, 0.16, 0.12, 0.09 }, fl = { 0, 0.5, 0.75, 1 }; int q = Mathf.Clamp(l, 0, 3); return q == 0 ? Loc.T("없음") : du[q].ToString("0.0") + Loc.T("초 · 초당 ") + Mathf.RoundToInt((float)(1 / gp[q])) + Loc.T("발"); }   // 게이지 배율은 설명에 — 값 줄이 길어 잘렸다
                case "q_insider": case "q_rage": case "q_front": case "q_debt": case "q_meteor": case "q_sling": case "q_tour": case "q_rock": case "q_gold": case "q_lazy": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");
                case "p_moon": case "p_mars": case "p_jup": case "p_sat": return l > 0 ? Loc.T("열림 — 항로 다이얼에서 선택") : Loc.T("잠김");
                case "a_auto": return l > 0 ? Loc.T("내 종목 봉마다 +0.08% 쪽으로") : Loc.T("없음");
                case "a_read": return new[] { Loc.T("없음"), Loc.T("다음 속보까지 시간"), Loc.T("+ 업종"), Loc.T("+ 제목까지") }[Mathf.Min(3, l)];
                case "a_ins": return l == 0 ? Loc.T("없음") : Loc.T("나쁜 속보 피하기 ") + new[] { 0, 60, 70, 80 }[Mathf.Min(3, l)] + "%";
                case "a_big": return Loc.T("수수료 ") + new[] { "1", "0.6", "0.3", "0" }[Mathf.Min(3, l)] + Loc.T("% · 배당 +") + (0.03f * l).ToString("0.00") + "%";
                case "c_fuel": return "+" + Mathf.RoundToInt((float)(SweepSim.FuelTankPct * l * 100)) + "%";
                case "c_crit": return (5 * l) + "%";
                case "c_double": return (10 * l) + "%";
                case "c_magnet": return l > 0 ? Loc.T("반경 +") + (40 + 20 * l) : Loc.T("없음");
                case "c_over": return l > 0 ? Loc.T("마지막 5초 ×2") : Loc.T("없음");
                case "d_n": return (2 + l) + Loc.T("대");
                case "d_spd": return Mathf.Max(0.4f, 1 - 0.1f * l).ToString("0.0") + Loc.T("초마다");
                case "d_reach": return Loc.T("거리 ") + (80 + 15 * l);
                case "d_mag": return "+" + (25 * l) + "%";
                case "d_sig": return (3 + 2 * l) + Loc.T("초");
                case "d_grade": return Loc.T("피해 ") + (1 + l);
                case "d_fix": return "+" + l + Loc.T("초");
                case "d_pair": return l > 0 ? Loc.T("둘씩") : Loc.T("하나씩");
                case "d_fact": return "+" + l + Loc.T("대");
                case "b_n": return Loc.T("공격마다 +") + (0.2f * l).ToString("0.#") + "%";
                case "c_find": return Loc.T("판마다 ") + l + Loc.T("번");
                case "s_speed": return Loc.T("공격마다 ") + (1.2f + 0.25f * l).ToString("0.##") + "%";
                case "b_pr": return Loc.T("반경 ") + (260 + 30 * l);
                case "b_cap": return (40 + 15 * l) + Loc.T("개");
                case "b_pf": return "×" + (1 + 0.25f * l).ToString("0.00");
                case "b_br": return "+" + (15 * l) + "%";
                case "b_chain": return (25 + 7 * l) + "%";
                case "b_pack": return Loc.T("개당 +") + (2 + 1.2f * l).ToString("0.0") + "%";
                case "o_wide": return Loc.T("궤도 폭 +") + (10 * l) + "%";
                case "e_val": return "×" + Mathf.Pow(1.25f, l).ToString("0.00");
                case "e_vault": return "+" + (50 * l) + "%";
                case "e_att": return "+" + (40 * l) + "%";
                case "e_quest": return "+" + (25 + 10 * l) + "%";
                case "e_talk": return Loc.T("기한 +") + l + Loc.T("판");
                case "e_tip": return (25 + 10 * l) + "%";
                case "e_save": return Loc.T("이자 ") + (2 * l) + "%";
                case "e_guard": return Loc.T("상환 ") + (l > 0 ? 20 : 30) + "%";
                case "e_used": return "-" + (5 * l) + "%";
                case "l_more": case "l_free": case "l_jack": case "l_wild": case "l_again": return l > 0 ? Loc.T("켜짐") : Loc.T("꺼짐");   // 🎟 10-06 복권 칸
                case "l_luck": return Loc.T("당첨 +") + (30 * l) + "%";
            }
            return l.ToString();
        }

        // 파산 뒤 화면은 격납고 (SweepHudHangar.cs · 09-26) — 경력 6칸은 영구 강화로 바꿨다

    }
}
