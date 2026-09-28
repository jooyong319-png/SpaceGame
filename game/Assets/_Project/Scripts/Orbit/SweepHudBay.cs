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
        /// <summary>산 칸이 튕긴다 — 쑥 커졌다 살짝 줄고 제자리 (0.5초)</summary>
        float BuyScale(int k) { if (!tileBuyT.TryGetValue(k, out var t0)) return 1; float e = Time.unscaledTime - t0; if (e > 0.6f) { tileBuyT.Remove(k); return 1; } return reduceMotion ? 1 : 1 + 0.34f * Mathf.Exp(-7 * e) * Mathf.Sin(e * 24 + 0.35f); }
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

        static readonly string[] PlanetHint = { "", Loc.T("달 — 궤도가 느리다 · 금고 위성이 많으니 노려 보자"), Loc.T("화성 — 판 중간에 모래 폭풍이 온다 · 얼음 껍질은 먼저 깨 두자"), Loc.T("목성 — 중력이 잔해를 안쪽으로 모은다 · 안쪽 가장자리에 블랙홀을"), Loc.T("토성 — 고리가 두 겹 · 가운데 틈은 비어 있다") , Loc.T("소행성대 — 단단한 암석과 광석이 많다"), Loc.T("천왕성 — 옆으로 누운 궤도 · 얼음 결정"), Loc.T("해왕성 — 초속 폭풍이 잔해를 흩는다"), Loc.T("카이퍼 벨트 — 태양계 끝 · 고대 탐사선과 혜성"), Loc.T("오르트 구름 — 얼음 혜성 떼가 단단하다 · 혜성 머리를 맞히면 열쇠"), Loc.T("태양권 계면 — 태양풍이 불면 잔해가 한쪽으로 쏠린다 · 폭풍 땐 값 ×1.5"), Loc.T("성간 공간 — 드문드문하지만 하나하나가 비싸다 · 결정 하나를 깨면 같은 결정이 모두") };
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

        void Bay()
        {
            if (gtiles == null) BuildGraph();
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            void BayHead()
            {
            // 머리 — 돈 · 청구서 · 궤도일보
            big.fontSize = 22;   // 도트 글꼴 11의 배수 (정돈 5)
            float cashLw = dim.CalcSize(new GUIContent(Loc.T("돈"))).x;   // 🌐 이름 폭만큼 띄운다 (영어 Cash 는 더 길다)
            Lbl(new Rect(ox + 16, 12, cashLw + 4, 20), Loc.T("돈"), dim);
            Lbl(new Rect(ox + Mathf.Max(38, 22 + cashLw), 6, 240, 32), "<color=#ffdf95>" + KNum.Fmt(shown) + "</color>", big);
            big.fontSize = 20;
            CreditScreen = new Vector2((ox + 60) * scale, Screen.height - 22 * scale);
            if (S.bill < SweepSim.Bills.Length && !sim.M.cleanReady)
            {
                string bl = Loc.T("청구서 · ") + SweepSim.Bills[S.bill].t + " <color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color>" + (S.overdue ? Loc.T("  <color=#ff8a7a>오늘 납부일</color>") : " · " + S.billDue + Loc.T("판 남음")) + (S.debt > 0 ? Loc.T("  <color=#ffb3a8>빚 ") + KNum.Fmt(S.debt) + "</color>" : "") + (S.cash >= sim.BillAmount ? Loc.T("  <color=#6fcf97>▶ 눌러서 갚기</color>") : "");
                bool loanPay = S.overdue && S.cash < sim.BillAmount && sim.BillAmount - S.cash <= sim.LoanCap;
                if (loanPay) bl += Loc.T("  <color=#6fcf97>▶ 대출 ") + KNum.Fmt(sim.BillAmount - S.cash) + Loc.T(" 받아 갚기</color>");
                if (Bt(new Rect(ox + 240, 8, 500, 30), bl, S.cash >= sim.BillAmount || loanPay ? btn : btnOff)) { if (S.cash >= sim.BillAmount) sim.PayBill(); else if (loanPay) RequestLoan(sim.BillAmount - S.cash, true); }
            }
            int unreadN = sim.Unread;
            // 오른쪽 끝은 ⚙ 설정 단추(vw-58) 앞에서 멈춘다 — 겹쳐서 설정을 누르면 신문이 열렸다 (09-28)
            if (Bt(new Rect(ox + 750, 8, Mathf.Min(166, vw - 66 - (ox + 750)), 30), Loc.T("궤도일보") + (unreadN > 0 ? "  <color=#ff8a7a>● " + unreadN + "</color>" : ""), btn)) { newsOpen = true; newsSel = -1; }
            }

            var area = new Rect(0, 46, vw, 500);
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
            var zb = new Rect(ox + 16, area.y + 8, 118, 30);
            void ZoomAt(Vector2 at, float nz)
            {
                nz = Mathf.Clamp(nz, 0.6f, 3f);
                float z0 = camZ * userZ, z1 = camZ * nz;
                var wc = (at - area.center - pan) / (cellPx * z0);      // 마우스 아래 칸 좌표 (camC 기준)
                pan = at - area.center - wc * cellPx * z1; userZ = nz;
            }
            if (ev.type == EventType.ScrollWheel && area.Contains(ev.mousePosition)) { ZoomAt(ev.mousePosition, userZ * (ev.delta.y > 0 ? 1 / 1.15f : 1.15f)); ev.Use(); }
            if (ev.type == EventType.MouseDown && (ev.button == 1 || ev.button == 0) && area.Contains(ev.mousePosition) && !zb.Contains(ev.mousePosition)) { dragging = true; dragMoved = false; dragBtn = ev.button; dragFrom = dragStart = ev.mousePosition; }
            if (ev.type == EventType.MouseDrag && dragging)
            {
                if (!dragMoved && (ev.mousePosition - dragStart).sqrMagnitude > 36) dragMoved = true;   // 6px 넘게 움직여야 끌기 — 칸 누르기와 안 헷갈리게
                if (dragMoved) { pan += ev.mousePosition - dragFrom; dragFrom = ev.mousePosition; ev.Use(); }
            }
            if (ev.type == EventType.MouseUp && dragging && ev.button == dragBtn) { dragging = false; if (dragMoved) ev.Use(); }
            float zz = camZ * userZ;
            Vector2 ToScr(Vector2Int c) => area.center + pan + ((Vector2)c - camC) * cellPx * zz;
            float tile = 44f * zz;

            // 선 — 둘 다 산 것이면 금색
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
                    {   // ✕ 연결 칸의 둘째 선 — 안 샀으면 흐린 점선, 마우스를 올린 칸 것만 밝게 (09-26 정돈 15)
                        bool hl = treeHover == k || treeHover == pk;
                        DotLine(a, b, new Color(0.5f, 0.47f, 0.42f, hl ? 0.9f : 0.28f), (hl ? 2f : 1.2f) * zz);
                        continue;
                    }
                    if (gold) Line(a, b, new Color(1f, 0.72f, 0.2f, 0.3f), 8 * zz);
                    Line(a, b, gold ? new Color(1f, 0.74f, 0.2f) : new Color(0.32f, 0.3f, 0.28f, st[k] == 1 ? 0.5f : 0.9f), (gold ? 3.2f : 2f) * zz);
                }
            }
            // ✨ 산 칸 → 다음 칸으로 선을 따라 흐르는 빛 (꼬리 넷)
            {
                float now = Time.unscaledTime;
                for (int i = buyFlows.Count - 1; i >= 0; i--)
                {
                    var f = buyFlows[i]; float e = now - f.t0;
                    if (e > FlowLen + PingLen) { buyFlows.RemoveAt(i); continue; }
                    if (e < 0 || e > FlowLen || f.from >= nT || f.to >= nT || st[f.to] == 0) continue;
                    var a = ToScr(gtiles[f.from].cell); var b = ToScr(gtiles[f.to].cell);
                    for (int q = 0; q < 5; q++)
                    {
                        float u = Mathf.Clamp01((e - q * 0.035f) / FlowLen); if (u <= 0) continue;
                        u = 1 - (1 - u) * (1 - u);                                   // 빨리 나가 부드럽게 닿는다
                        var p = Vector2.Lerp(a, b, u); float s = (q == 0 ? 9 : 7 - q) * zz, al = q == 0 ? 1 : 0.55f - 0.1f * q;
                        GUI.color = new Color(1f, 0.86f, 0.45f, al * 0.45f); GUI.DrawTexture(new Rect(p.x - s * 1.6f, p.y - s * 1.6f, s * 3.2f, s * 3.2f), texDisc);
                        GUI.color = new Color(1f, 0.97f, 0.85f, al); GUI.DrawTexture(new Rect(p.x - s / 2, p.y - s / 2, s, s), texDisc);
                    }
                }
                GUI.color = Color.white;
            }
            // ⭐ 추천 한 칸 — 모르면 이것만 사도 된다 (09-24 설계서 「무거워도 자연스럽게」)
            int recK = -1;                                                   // 추천 표시는 뺐다 (09-26 사장님 「추천은 하지 말자」)
            // 칸
            int hover = -1;
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            var m0 = GUI.matrix;
            for (int k = 0; k < nT; k++)
            {
                if (st[k] == 0) continue;
                var t = gtiles[k];
                bool isRoot = t.stat < 0;
                var n = isRoot ? SweepSim.Nodes[0] : SweepSim.Nodes[t.stat];
                Color bcol = isRoot ? SweepGame.Amber : VisCol(n.id);
                bool next = st[k] == 2, owned = st[k] == 3;
                var ns = isRoot ? NodeSt.Max : sim.State(t.stat);
                bool inf = !isRoot && SweepSim.Infinite(t.stat);
                bool can = (next || inf && owned) && ns == NodeSt.Can;
                bool circle = !isRoot && n.id.StartsWith("q_");
                bool diamond = !isRoot && !circle && n.max == 1;
                var pc = ToScr(t.cell);
                float grow = isRoot ? 0 : nodePulse[t.stat] * 8 * zz;
                float sz = ((isRoot ? tile * 1.25f : diamond ? tile * 0.92f : tile) + grow) * BuyScale(k);
                var r = new Rect(pc.x - sz / 2, pc.y - sz / 2, sz, sz);
                if (can) { GUI.color = new Color(1f, 0.78f, 0.3f, 0.28f + 0.18f * Mathf.Sin(Time.time * 5 + k)); GUI.DrawTexture(new Rect(r.x - 9 * zz, r.y - 9 * zz, r.width + 18 * zz, r.height + 18 * zz), texDisc); }
                if (diamond) GUI.matrix = m0 * Matrix4x4.TRS(new Vector3(pc.x, pc.y, 0), Quaternion.Euler(0, 0, 45), Vector3.one) * Matrix4x4.TRS(new Vector3(-pc.x, -pc.y, 0), Quaternion.identity, Vector3.one);
                Color bg = owned ? Color.Lerp(bcol, new Color(0.1f, 0.08f, 0.06f), 0.62f) : next ? new Color(0.09f, 0.09f, 0.1f) : new Color(0.06f, 0.06f, 0.07f);
                GUI.color = bg; GUI.DrawTexture(r, circle ? texDisc : white);
                float bfl = BuyFlash(k); if (bfl > 0) { GUI.color = new Color(1f, 0.97f, 0.88f, 0.75f * bfl); GUI.DrawTexture(r, circle ? texDisc : white); }   // ✨ 산 순간 흰빛
                Color edge = can ? new Color(1f, 0.8f, 0.35f) : owned ? Color.Lerp(bcol, Color.black, 0.25f) : new Color(0.22f, 0.21f, 0.2f);
                if (circle) { GUI.color = edge; GUI.DrawTexture(r, texRing); GUI.color = Color.white; } else Frame(r, edge, can ? 2.5f : 1.5f);
                GUI.matrix = m0;
                // 아이콘 — 칸 안에 글자 없이 (시안에서 구운 tree_icons.png)
                bool lockedTile = !owned && ns == NodeSt.Locked;
                GUI.color = owned ? new Color(1f, 0.96f, 0.86f) : next ? (lockedTile ? new Color(0.3f, 0.31f, 0.34f) : new Color(0.72f, 0.72f, 0.74f)) : new Color(0.17f, 0.17f, 0.19f);
                float isz = sz * 0.62f;
                int planetI = isRoot ? -1 : System.Array.IndexOf(SweepSim.PlanetNode, n.id);
                if (planetI > 0) { var pr = new Rect(pc.x - isz * 0.62f, pc.y - isz * 0.62f, isz * 1.24f, isz * 1.24f); GUI.color = owned ? Color.white : next ? new Color(0.6f, 0.62f, 0.66f) : new Color(0.2f, 0.2f, 0.22f); var ps = PlanetArt.Icon(planetI); GUI.DrawTextureWithTexCoords(pr, ps.texture, SprUV(ps)); }
                else DrawIcon(new Rect(pc.x - isz / 2, pc.y - isz / 2, isz, isz), isRoot ? "R" : n.id);
                if (!isRoot && SweepSim.KeyNodes.Contains(n.id)) { float kp = sim.S.keys > 0 && next ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3 + k) : 0.3f; GUI.color = new Color(0.71f, 0.61f, 1f, owned ? 0.9f : 0.35f + 0.3f * kp); Frame(new Rect(r.x - 3 * zz, r.y - 3 * zz, r.width + 6 * zz, r.height + 6 * zz), GUI.color, 2f); GUI.color = Color.white; }   // 「열쇠」 글자는 툴팁에서 · 열쇠가 있을 때만 깜빡 (09-26 정돈 14)
                if (!isRoot && n.max == 1 && System.Array.IndexOf(SweepSim.WeaponNode, n.id) == sim.Weapon && owned) { GUI.color = new Color(1f, 0.55f, 0.5f); Lbl(new Rect(pc.x - 40, r.yMax + 2 * zz, 80, 16), Loc.T("<size=10><b>장착 중</b></size>"), center); GUI.color = Color.white; }
                if (k == recK) { float rp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4); GUI.color = new Color(1f, 0.87f, 0.4f, 0.55f + 0.45f * rp); Frame(new Rect(r.x - 5 * zz, r.y - 5 * zz, r.width + 10 * zz, r.height + 10 * zz), GUI.color, 2.5f); GUI.color = Color.white; recLbl = new Rect(pc.x - 17, r.yMax + 1 * zz, 34, 15); }
                if (lockedTile && next) { var lcol = gtiles[k].stat >= 0 && sim.CapLocked(gtiles[k].stat) ? new Color(1f, 0.6f, 0.4f) : new Color(1f, 0.42f, 0.38f); DashFrame(new Rect(r.x - 3 * zz, r.y - 3 * zz, r.width + 6 * zz, r.height + 6 * zz), lcol); lockLbls.Add((new Rect(pc.x - 17, r.yMax - 8, 34, 14), lcol)); }   // 잠김 = 점선 테두리 + 아래 가장자리 「잠김」 딱지 (주황 = 행성 한도)   // 잠김 = 점선 테두리 (주황 = 행성 한도) — 「잠」 글자가 칸 위에 겹쳐 두 개처럼 보였다 (09-26)
                GUI.color = Color.white;
                if (inArea && r.Contains(ev.mousePosition)) hover = k;
                if (inf && owned) Lbl(new Rect(r.x - 10, r.yMax, r.width + 20, 16), "<size=10><color=#ffdf95>∞ " + sim.S.lv[t.stat] + "</color></size>", center);
                bool clicked = !isRoot && (next || inf && owned) && inArea && Bt(r, GUIContent.none, GUIStyle.none);
                if (clicked && ns == NodeSt.Can)
                {
                    int times = shift ? 5 : 1; double cash0 = sim.S.cash;
                    while (times-- > 0 && sim.State(t.stat) == NodeSt.Can) sim.BuyTile(t.stat);
                    BuyAnim(k, cash0 - sim.S.cash >= 1 ? "-" + KNum.Short(cash0 - sim.S.cash) : null);
                    if (n.id == "e_shop") { roomGo = 5; roomGoAt = Time.unscaledTime + 0.5f; }                // 🧭 09-27 사장님 「증권 · 가게 스킬 열리면 바로 거기 가게」 — 사는 효과를 보고 그 방으로
                    else if (n.id == "v_volley" && sim.S.lv[t.stat] == 1) Guide(Loc.T("전탄 발사가 생겼다 — 출동 중 게이지가 차면 Space · 계기판 가운데 단추 (무기 둘부터)"));
                    else if (n.id == "a_open") { roomGo = 4; roomGoAt = Time.unscaledTime + 0.5f; }
                    nodePulse[t.stat] = 1; OrbitSfx.Play("buy", 0.7f, 0.01f, 0.15f); lastBuyBranch = n.branch; BuyFx(pc, SweepSim.KeyNodes.Contains(n.id) ? new Color(0.71f, 0.61f, 1f) : bcol, n.max == 1, SweepSim.KeyNodes.Contains(n.id) ? Loc.T("핵심 해금!") : Loc.T("해금!"));
                }
                else if (clicked && ns != NodeSt.Max)
                { OrbitSfx.Play("clank", 0.35f, 0.05f, 0f); int bk = ns == NodeSt.Hidden ? BlockTile(k) : -1; Deny(pc, bk >= 0 ? TileName(bk) + Loc.T(" 먼저") : WhyNot(t.stat)); }   // 🚫 안 눌리는 칸 — 왜 안 되는지 그 자리에 (09-25 사장님 「안 눌리는 게 있던데」)
            }
            // ✨ 빛이 닿은 다음 칸 — 작은 고리가 번진다 · 쓴 돈이 금색으로 떠오른다
            {
                float now = Time.unscaledTime;
                foreach (var f in buyFlows)
                {
                    float e = now - f.t0 - FlowLen; if (e < 0 || e > PingLen || f.to >= nT || st[f.to] == 0) continue;
                    var p = ToScr(gtiles[f.to].cell); float k2 = e / PingLen, R = tile * (0.55f + 0.6f * k2);
                    GUI.color = new Color(1f, 0.86f, 0.45f, 0.8f * (1 - k2)); GUI.DrawTexture(new Rect(p.x - R, p.y - R, R * 2, R * 2), texRing);
                }
                for (int i = buyCoins.Count - 1; i >= 0; i--)
                {
                    var c = buyCoins[i]; float e = now - c.t0; if (e > 0.9f || c.k >= nT) { buyCoins.RemoveAt(i); continue; }
                    var p = ToScr(gtiles[c.k].cell); float a = Mathf.Clamp01((0.9f - e) / 0.3f);
                    GUI.color = new Color(1, 1, 1, a); Lbl(new Rect(p.x - 60, p.y - tile * 0.9f - 26 * e, 120, 20), "<size=13><b><color=#ffdf95>" + c.txt + "</color></b></size>", center);
                }
                GUI.color = Color.white;
            }
            // 영역 밖 띠 — 넘어간 칸을 덮고 머리 · 안내를 다시 그린다
            GUI.color = new Color(0.02f, 0.027f, 0.04f); GUI.DrawTexture(new Rect(0, 0, vw, area.y), white); GUI.DrawTexture(new Rect(0, area.yMax, vw, RefH - area.yMax), white);
            GUI.color = new Color(1f, 0.78f, 0.3f, 0.18f); GUI.DrawTexture(new Rect(0, area.y - 1, vw, 1), white); GUI.DrawTexture(new Rect(0, area.yMax, vw, 1), white);
            GUI.color = Color.white;
            BayHead();
            {
                float bw3 = 36;
                if (Bt(new Rect(zb.x, zb.y, bw3, zb.height), "<size=16>−</size>", btnOff)) ZoomAt(area.center, userZ / 1.25f);
                if (Bt(new Rect(zb.x + bw3 + 3, zb.y, bw3, zb.height), "<size=16>+</size>", btnOff)) ZoomAt(area.center, userZ * 1.25f);
                if (Bt(new Rect(zb.x + (bw3 + 3) * 2, zb.y, 40, zb.height), Loc.T("<size=11>맞춤</size>"), btnOff)) { userZ = 1; pan = Vector2.zero; }
                var zr = new Rect(zb.xMax + 4, zb.y + 5, 44, 20);
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.9f); GUI.DrawTexture(zr, white); GUI.color = Color.white;
                Lbl(zr, "<size=11><color=#8a93a3>" + Mathf.RoundToInt(userZ * 100) + "%</color></size>", center);
            }
            foreach (var (lr, lc) in lockLbls)
            {   // 🔒 「잠김」 — 칸을 다 그린 뒤 맨 위에 (09-26 사장님 「잠? 이해하기 힘듦 — 잠김으로」)
                GUI.color = new Color(0.08f, 0.04f, 0.04f, 0.92f); GUI.DrawTexture(lr, white); GUI.color = Color.white;
                string lt = "<size=9><b><color=#" + ColorUtility.ToHtmlStringRGB(lc) + Loc.T(">잠김</color></b></size>");
                float lw = center.CalcSize(new GUIContent(lt)).x + 4;              // 🌐 영어 Locked 는 딱지보다 넓다 — 가운데를 두고 넓힌다
                if (lw > lr.width) { var lr2 = new Rect(lr.center.x - lw / 2, lr.y, lw, lr.height); GUI.color = new Color(0.08f, 0.04f, 0.04f, 0.92f); GUI.DrawTexture(lr2, white); GUI.color = Color.white; Lbl(lr2, lt, center); }
                else Lbl(lr, lt, center);
            }
            lockLbls.Clear();
            if (recLbl.width > 0)
            {   // ⭐ 추천 글자 — 칸을 다 그린 뒤 맨 위에, 어두운 바탕으로 (아래 칸에 깔려 안 읽혔다)
                GUI.color = new Color(0.1f, 0.08f, 0.03f, 0.95f); GUI.DrawTexture(recLbl, white); Frame(recLbl, new Color(1f, 0.8f, 0.4f, 0.8f), 1); GUI.color = Color.white;
                Lbl(recLbl, Loc.T("<size=10><b><color=#ffdf95>추천</color></b></size>"), center);
                recLbl = default;
            }
            PartsButton(new Rect(zb.x, zb.yMax + 8, 230, 30));             // 무기 효과판 뺌 (09-24 23번)
            {   // 🪐 구역 진행 — 지금 구역 칸을 다 찍으면 다음 항로 (09-24 6·21번)
                int zo = sim.ZoneOpen, zl = sim.ZoneLeft(zo), zt = 0; for (int i = 0; i < SweepSim.Nodes.Length; i++) if (SweepSim.Zone[i] == zo && SweepSim.ZoneNeed(i)) zt++;
                bool last = zo + 1 >= SweepSim.OrbitOrder.Length;
                string zs = sim.HasGate && !sim.GateReady
                    ? "<size=12><color=#ffdf95>" + SweepSim.ZoneName[zo] + Loc.T("</color> 한 줄 ") + sim.TileCap + Loc.T("칸까지 · <color=#ffb36b>🛰 청구서 ") + sim.GateBill + Loc.T("장을 갚으면 ") + sim.GateName + Loc.T(" 방어막이 풀린다</color></size>")
                    : sim.HasGate
                    ? "<size=12><color=#ffdf95>" + SweepSim.ZoneName[zo] + Loc.T("</color> 한 줄 ") + sim.TileCap + Loc.T("칸까지 · <color=#ffb36b>🛰 ") + sim.GateName + " " + Mathf.CeilToInt((float)sim.GateLeft * 100) + Loc.T("%</color> <color=#8a93a3>부수면 ") + sim.NextName + "</color></size>"
                    : "<size=12><color=#ffdf95>" + SweepSim.ZoneName[zo] + Loc.T("</color> <color=#8a93a3>— 마지막 항로 · 한도 없음</color></size>");
                var zr = new Rect(zb.xMax + 58, zb.y + 3, label.CalcSize(new GUIContent(zs)).x + 20, 24);   // 글자 폭만큼만 — 넓으면 트리 윗줄 칸을 덮었다
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.9f); GUI.DrawTexture(zr, white); GUI.color = Color.white;
                Lbl(new Rect(zr.x + 8, zr.y + 3, zr.width - 8, 18), zs, label);   // 🛰 09-26 구역 칸 개수 → 관문
            }
            if (testTip >= 0) { for (int k = 0; k < nT; k++) if (gtiles[k].stat >= 0 && SweepSim.Nodes[gtiles[k].stat].id == testTipId) hover = k; }   // 에디터 시험용
            treeHover = hover;
            if (hover >= 0 && gtiles[hover].stat >= 0 && st[hover] != 3 && (st[hover] == 1 || sim.State(gtiles[hover].stat) == NodeSt.Hidden))
            {   // 🔗 막고 있는 칸을 깜빡 — 「먼저 이것」
                int bk = BlockTile(hover);
                if (bk >= 0 && st[bk] != 0 && area.Contains(ToScr(gtiles[bk].cell)))
                {
                    var bp = ToScr(gtiles[bk].cell); float bp2 = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8), bs = tile * (1.5f + 0.25f * bp2);
                    GUI.color = new Color(1f, 0.6f, 0.55f, 0.35f + 0.3f * bp2); GUI.DrawTexture(new Rect(bp.x - bs / 2, bp.y - bs / 2, bs, bs), texRing);
                    GUI.color = Color.white; Lbl(new Rect(bp.x - 40, bp.y - tile * 0.5f - 20, 80, 18), Loc.T("<size=11><b><color=#ff9b8f>먼저 이 칸</color></b></size>"), center);
                }
            }
            if (hover < 0 && TestTip >= 0 && TestTip < nT && st[TestTip] != 0) hover = TestTip;   // 에디터 시험용 — 이 칸 설명을 띄운다
            if (hover >= 0) Tip(hover, ToScr(gtiles[hover].cell), st[hover], tile);
            else Lbl(new Rect(ox, area.yMax + 2, 750, 16), Loc.T("<size=11>칸에 마우스를 올리면 무엇인지 보인다 · 빛나는 칸을 누르면 산다 · 휠 = 확대 · 끌기 = 이동</size>"), center);
        }

        GUIStyle tipWrap; Rect recLbl; readonly List<(Rect, Color)> lockLbls = new List<(Rect, Color)>();
        public int testTip = -1; public string testTipId;                  // 에디터 시험용 — 툴팁 강제로 띄우기
        void Tip(int k, Vector2 at, int vis, float tile)
        {
            var t = gtiles[k];
            if (t.stat < 0)
            {
                var r0 = new Rect(at.x + tile / 2 + 14, at.y - 40, 260, 76);
                GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.97f); GUI.DrawTexture(r0, white); GUI.color = Color.white; Frame(r0, new Color(0.6f, 0.5f, 0.35f), 2);
                title.fontSize = 18; Lbl(new Rect(r0.x, r0.y + 6, r0.width, 26), Loc.T("<color=#d9b98a>청소선</color>"), title);
                Lbl(new Rect(r0.x + 10, r0.y + 38, r0.width - 20, 22), Loc.T("여기서 다섯 갈래로 뻗는다"), center);
                return;
            }
            var n = SweepSim.Nodes[t.stat]; var ns = sim.State(t.stat); int lv = sim.S.lv[t.stat];
            int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
            // 설명 길이에 맞춰 키가 자란다 (09-24 글자 잘림 점검 — 긴 설명이 한 줄 칸에서 잘렸다)
            if (tipWrap == null) tipWrap = new GUIStyle(center) { wordWrap = true, fontSize = 13 };
            const float TipW = 340;
            string tipDesc = KWrap(sim.NodeDesc(t.stat), 13, TipW - 30);
            float dh = vis == 1 ? 22 : Mathf.Max(22, tipWrap.CalcHeight(new GUIContent(tipDesc), TipW - 24));
            bool keyNote = vis != 3 && vis != 1 && SweepSim.KeyNodes.Contains(n.id) && sim.State(t.stat) != NodeSt.Locked && sim.State(t.stat) != NodeSt.Hidden;
            var r = new Rect(at.x + tile / 2 + 14, at.y - 70, TipW, vis == 1 ? 96 : 138 + dh + (keyNote ? 18 : 0));
            if (r.xMax > vw - 8) r.x = at.x - tile / 2 - 14 - r.width;
            r.x = Mathf.Max(8, r.x);
            r.y = Mathf.Clamp(r.y, 50, 500 - r.height);
            GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.97f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.6f, 0.5f, 0.35f), 2);
            GUI.color = new Color(0.12f, 0.12f, 0.14f); GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, 34), white); GUI.color = Color.white;
            title.fontSize = 18;
            if (vis == 1)
            {
                Lbl(new Rect(r.x, r.y + 4, r.width, 28), "<color=#b89a6a>?</color>", title);
                { int bk = BlockTile(k); Lbl(new Rect(r.x, r.y + 52, r.width, 20), bk >= 0 ? "「" + TileName(bk) + Loc.T("」 사면 무엇인지 보인다") : Loc.T("앞 칸을 사면 무엇인지 보인다"), center); }
                return;
            }
            string nm = sim.NodeName(t.stat) + (SweepSim.Tiles(t.stat) > 1 ? " " + Roman[t.j] : "");
            Lbl(new Rect(r.x, r.y + 4, r.width, 28), "<color=#d9b98a>" + nm + "</color>", title);
            Lbl(new Rect(r.x + 12, r.y + 42, r.width - 24, dh), tipDesc, tipWrap);
            float oy = dh - 22;                                          // 설명이 길어진 만큼 아래 줄을 내린다
            GUI.color = new Color(0.3f, 0.28f, 0.24f); GUI.DrawTexture(new Rect(r.x + 24, r.y + 70 + oy, r.width - 48, 1), white); GUI.DrawTexture(new Rect(r.x + 24, r.y + 98 + oy, r.width - 48, 1), white); GUI.color = Color.white;
            int from = t.j == 1 ? 0 : SweepSim.TileLv(t.stat, t.j - 1), to = SweepSim.TileLv(t.stat, t.j);
            if (vis == 3 && !SweepSim.Infinite(t.stat)) Lbl(new Rect(r.x + 10, r.y + 74 + oy, r.width - 20, 20), Loc.T("<color=#8a93a3>지금</color>  <color=#ffdf95>") + Val(n.id, to) + "</color>", center);   // 산 칸은 「0 ▸ 1」 대신 지금 값만
            else Lbl(new Rect(r.x + 10, r.y + 74 + oy, r.width - 20, 20), Val(n.id, from) + "  <color=#d9b98a>▸</color>  <color=#ffdf95>" + Val(n.id, to) + "</color>", center);
            string foot;
            if (vis == 3 && SweepSim.Infinite(t.stat)) foot = (ns == NodeSt.Can ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + "</color>  <color=#ffdf95>∞ " + sim.S.lv[t.stat] + Loc.T("번 삼 · 계속 살 수 있다</color>");   // 누적 칸 — 다음 가격 (09-24 친구들 「가격이 안 보인다」)
            else if (vis == 3) foot = Loc.T("<color=#6fcf97>샀다</color>");
            else if (ns == NodeSt.Locked && n.id.StartsWith("p_")) foot = "<color=#ffb36b>🛰 " + (sim.HasGate ? (sim.GateReady ? "" : Loc.T("청구서 ") + sim.GateBill + Loc.T("장을 갚으면 나타나는 ")) + sim.GateName + Loc.T(" 관문을 부수면 열린다") : Loc.T("앞 항로부터")) + "</color>";   // 🛰 항로 = 관문
            else if (ns == NodeSt.Locked && sim.CapLocked(t.stat)) foot = Loc.T("<color=#ffb36b>행성 한도 — ") + sim.GateName + Eul(sim.GateName) + Loc.T(" 부수면 한 칸 더</color>");
            else if (ns == NodeSt.Locked && SweepSim.Zone[t.stat] > sim.ZoneOpen) foot = "<color=#ff9b8f>" + SweepSim.ZoneName[SweepSim.Zone[t.stat]] + Loc.T(" 항로를 열면 열린다</color>");
            else if (ns == NodeSt.Locked) foot = SweepSim.Ring4(n.id) ? Loc.T("<color=#ff9b8f>목성 항로를 열면 — 외행성 면허</color>") : Loc.T("<color=#ff9b8f>청구서 ") + SweepSim.BranchNeed[b] + Loc.T("을 갚으면 열린다</color>");
            else if (ns == NodeSt.Hidden) { int bk = BlockTile(k); foot = "<color=#ff9b8f>" + (bk >= 0 ? "「" + TileName(bk) + Loc.T("」 먼저 사야 열린다") : Loc.T("앞 칸을 먼저 사야 한다")) + "</color>"; }   // 어느 칸인지 콕 집어 — 「앞 칸」만으론 이미 산 칸을 또 눌러야 하나 헷갈렸다 (09-25 사장님)
            else if (SweepSim.KeyNodes.Contains(n.id) && sim.S.keys < 1) foot = (sim.S.cash >= sim.TileCost(t.stat) ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + Loc.T("</color>  <color=#ff9b8f>+ 열쇠 1 (없음)</color>");   // 돈은 되는데 열쇠가 없다 — 값만 빨개서 이유를 몰랐다
            else foot = (ns == NodeSt.Can ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + "</color>" + (SweepSim.KeyNodes.Contains(n.id) ? Loc.T("  <color=#d8ccff>+ 열쇠 1</color>") : "");
            if (keyNote) Lbl(new Rect(r.x, r.y + 136 + oy, r.width, 16), "<size=11><color=#b9a9ee>" + (sim.S.keys < 1 ? Loc.T("열쇠 0 — 청구서를 갚거나 파산하면 +1") : Loc.T("가진 열쇠 ") + sim.S.keys + Loc.T(" · ◆ 핵심 칸은 파산해도 남는다")) + "</color></size>", center);
            center.fontSize = 20; while (center.fontSize > 12 && center.CalcSize(new GUIContent(foot)).x > r.width - 12) center.fontSize--;   // 핵심 칸 · 누적 칸 · 영어는 줄이 길다 — 들어갈 때까지 줄인다
            bool footWrap = center.CalcSize(new GUIContent(foot)).x > r.width - 12;   // 12로도 안 들어가면 두 줄로 접는다
            if (footWrap) { center.fontSize = 11; center.wordWrap = true; }
            Lbl(new Rect(r.x + 6, r.y + 104 + oy, r.width - 12, 34), foot, center); center.fontSize = 13; center.wordWrap = false;
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
                case "c_pow": return Loc.T("한 방 ") + (1 + l);
                case "c_rad": return l > 0 ? Loc.T("반지름 ") + (20 + 5 * l) : Loc.T("한 점");
                case "c_spd": return Loc.T("화력 +") + 8 * l + "%";
                case "a_open": return l > 0 ? Loc.T("열림") : Loc.T("잠김");
                case "w_hub": return l > 0 ? Loc.T("무기 효과 열림") : Loc.T("잠김");
                case "w_laser": case "w_chain": return l > 0 ? Loc.T("발동 ") + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : Loc.T("잠김");
                case "w_laser_e": case "w_chain_e": case "w_vac_e": case "w_mine_e": case "w_frz_e": case "w_clus_e": case "w_mag_e": case "w_rail_e": return l > 0 ? Loc.T("특화 ") + l + Loc.T("단계") : Loc.T("없음");   // ◇ 무기 특화
                case "w_laser_u": return new[] { Loc.T("없음"), Loc.T("굵기 +50%"), Loc.T("굵기 +50% · 열 축적") }[Mathf.Min(2, l)];
                case "w_chain_u": return new[] { Loc.T("없음"), Loc.T("7번 튄다"), Loc.T("7번 · 튈수록 ×1.2") }[Mathf.Min(2, l)];
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
                case "p_moon": case "p_mars": case "p_jup": case "p_sat": return l > 0 ? Loc.T("열림 — 항로 다이얼에서 고른다") : Loc.T("잠김");
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
                case "d_grade": return Loc.T("한 방 ") + (1 + l);
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
            }
            return l.ToString();
        }

        // 파산 뒤 화면은 격납고 (SweepHudHangar.cs · 09-26) — 경력 6칸은 영구 강화로 바꿨다

    }
}
