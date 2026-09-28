using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 💬 대화창 — 누르면 다음 대사 (09-26 사장님 「일반 게임 대화처럼 · 스토리 풀자 · 증권 · 가게도 알려주고」)
    // 시안 https://claude.ai/artifact/2x4Megbd3ccyXoJjXrgzZ8 — 초상화 + 이름표 + 한 글자씩. 출동 중엔 안 뜬다 (출동 중 말은 무전 그대로)
    // 이야기: 케슬러 그룹이 싸구려 위성을 쏘아 쓰레기를 만들고, 치우는 청소선에 돈도 빌려준다 → 완납 때 내부 문서 → 엔딩 호외
    public partial class SweepHud
    {
        class DlgWho { public string name, sub, hex; public string[] path; public Texture2D[] tex; }
        static readonly Dictionary<string, DlgWho> Who = new Dictionary<string, DlgWho>
        {
            { "yoon", new DlgWho { name = Loc.T("윤 대리"), sub = Loc.T("케슬러 금융"), hex = "#ffdf95", path = new[] { "news/yoon" } } },
            { "go",   new DlgWho { name = Loc.T("고 영감"), sub = Loc.T("고물상 · 부품 가게"), hex = "#ffb36b", path = new[] { "news/go" } } },
            { "han",  new DlgWho { name = Loc.T("한 주임"), sub = Loc.T("궤도 증권"), hex = "#9ff0bf", path = new[] { "news/han" } } },
            { "anc",  new DlgWho { name = Loc.T("서 기자"), sub = Loc.T("궤도일보"), hex = "#ff8a7a", path = new[] { "news/anchor_b0", "news/anchor_b1" } } },
        };

        // 「누구|대사」 — *별표 사이*는 말하는 사람 색으로
        static Dictionary<string, string[]> scenes; static bool scenesEn;   // 🌐 만든 때의 언어 — 바뀌면 다시 만든다 (09-28 사장님 「대사 안 바뀌는데?」)
        static Dictionary<string, string[]> Scenes
        {
            get
            {
                if (scenes != null && scenesEn == Loc.En) return scenes;
                scenesEn = Loc.En;
                string Y(int k) => "yoon|" + RadioLines[k];
                scenes = new Dictionary<string, string[]>
                {
                    { "pro", new[] {
                        Loc.T("yoon|케슬러 금융 윤 대리야. 이 청소선, 할부로 산 거 알지?"),
                        Loc.T("yoon|지구 궤도가 쓰레기로 꽉 찼어. 위성 하나가 부서지면 그 파편이 또 다른 걸 부수고… 업계에선 그걸 *케슬러 현상*이라고 불러."),
                        Loc.T("yoon|…이름이 우리 회사랑 같은 건 우연이야. 아마도."),
                        Loc.T("yoon|청구서는 *열두 장*. 다 갚으면 이 배는 네 거다."),
                        Loc.T("yoon|가운데 노란 단추가 *출동*. 쏘는 법은 나가면 무전으로 알려 줄게. 잔해를 부수면 돈이 되고."),
                        Loc.T("yoon|첫 청구서는 연료비. 다섯 판 안에. …기대는 안 할게.") } },
                    { "stock", new[] {
                        Loc.T("han|궤도 증권 한 주임입니다! 계좌 개설 축하드려요, 대표님."),
                        Loc.T("han|종목은 여덟 개. 전부 궤도 일이랑 엮여 있어요. 목성을 많이 치우면 *목성 선박*이 오르고, 운석이 떨어지면 *지구 연료공사*가 휘청해요."),
                        Loc.T("han|중요한 거 하나! 시세는 *대표님이 출동해 있을 때만* 움직여요. 조종실에 계실 땐 장이 멈춰 있어요. 위에 「시세 멈춤」 보이시죠?"),
                        Loc.T("han|사고파는 건 여기, *조종실에서만*. 출동 중엔 S 키로 시세만 볼 수 있어요."),
                        Loc.T("han|왼쪽 아래 속보요. ▲면 오르고 ▼면 내려요. 속보보다 먼저 사 두면… 그게 투자죠."),
                        Loc.T("han|아, 너무 자주 사고팔면 수수료가 다 저희 몫이에요. 헤헤.") } },
                    { "shop", new[] {
                        Loc.T("go|어서 와. 고물상 고 영감이다. 궤도에서 주운 것 중에 쓸 만한 건 다 여기 있지."),
                        Loc.T("go|네 배엔 칸이 다섯이야. *사출기 · 레이더 · 부적 · 선체 · 엔진*. 칸마다 하나씩만 끼운다."),
                        Loc.T("go|카드에 손 올려 봐. 어디에 끼우는지 왼쪽 배 그림에서 깜빡일 거다."),
                        Loc.T("go|진열은 *출동 한 번 다녀오면* 바뀐다. 빨간 띠 붙은 건 오늘만 반값이고."),
                        Loc.T("go|「다음 출동 한 번용」은 칸에 안 끼워. 사 두면 *다음 출동 때 저절로* 쓰이고 끝이야. 복권 묶음, 연료 캔 같은 것."),
                        Loc.T("go|…근데 요즘 주워 오는 것마다 *케슬러 발사* 딱지가 붙어 있어. 새것 같은데 벌써 고물이야. 이상하지?") } },
                    { "news", new[] {
                        Loc.T("anc|궤도일보 서 기자입니다. 아, 대표님이시죠? 저희 신문 늘 봐 주셔서 감사해요."),
                        Loc.T("anc|대표님이 한 일은 전부 기사가 돼요. 조종실 오른쪽 위 모니터를 누르면 지난 기사를 볼 수 있어요."),
                        Loc.T("anc|「*내일 1면 고르기*」를 누르면 다음 출동 때 어느 기사가 1면에 실릴지 정하실 수 있어요. 1면 소식은 시세를 더 크게 흔들어요."),
                        Loc.T("anc|…사실 제보 하나를 받았는데요. 케슬러 발사 위성이 *수명보다 훨씬 일찍* 부서진대요. 확인되면 꼭 1면에 싣고 싶어요.") } },
                    { "fuel", new[] {
                        Loc.T("yoon|아, 하나 알려 줄게. 이제 청소선은 *쏠 때마다 연료*를 먹어."),
                        Loc.T("yoon|손을 떼면 안 쏘고 연료도 덜 닳아. 가만히 있어도 조금씩은 닳지만."),
                        Loc.T("yoon|그러니까 *뭉친 데*를 골라서 쏴. 같은 연료로 더 많이 부수는 게 실력이야."),
                        Loc.T("yoon|왼쪽 단추를 *누르고 있는 동안* 쏴. 전탄 발사는 한 번에 연료를 크게 먹고.") } },
                    { "ev0", new[] { Loc.T("anc|방금 왼쪽에서 흘러온 건 *연료 보급선*이에요."), Loc.T("anc|연료통을 부수면 연료가 차요. 판이 그만큼 길어지죠.") } },
                    { "ev1", new[] { Loc.T("anc|오른쪽 위 *충돌 사고* 보셨죠? 위성끼리 부딪쳐 파편 서른 개가 한꺼번에 쏟아져요."), Loc.T("anc|흩어지기 전에 뭉친 데를 치면 한 방에 여럿 — *연쇄*가 붙어요.") } },
                    { "ev2", new[] { Loc.T("anc|*파편 폭풍*은 왼쪽에서 고철이 줄줄이 쏟아지는 거예요."), Loc.T("anc|크기가 큰 무기로 길목을 막고 서 있으면 저절로 쓸려 들어와요.") } },
                    { "ev3", new[] { Loc.T("anc|*금고 호송대*가 지나갔어요. 금고 위성 여러 대가 줄지어 돌아요."), Loc.T("anc|놓치기 전에 부수면 큰돈이에요. 금고는 값이 여러 배거든요.") } },
                    { "ev4", new[] { Loc.T("anc|방금 건 *대충돌*이에요! 파편 예순 개에 폭탄 달린 위성까지."), Loc.T("anc|폭탄 위성이 터지면 주변이 한꺼번에 날아가요. 연쇄 대박 기회예요.") } },
                    { "gate", new[] {
                        Loc.T("yoon|궤도에 떠 있는 커다란 거 봤어? 저게 *관문*이야. 저게 버티고 있는 동안은 다음 행성 항로가 막혀 있어."),
                        Loc.T("yoon|*직접 겨눠서* 쳐야 깎여. 연쇄나 폭발, 드론으로는 조금밖에 안 들어가."),
                        Loc.T("yoon|대신 *한 판 안에* 부숴야 해. 못 부수면 다음 판엔 체력이 다시 가득 차. 치다 보면 비싼 파편도 떨어지고."),
                        Loc.T("yoon|부수면 다음 행성 항로가 열리고, 열쇠 하나랑 두둑한 보너스. 정비고도 *한 줄에 한 칸씩 더* 찍을 수 있게 돼.") } },
                    { "chain", new[] {
                        Loc.T("yoon|연쇄 봤어? 부서진 조각이 옆 잔해를 또 부수는 거야."),
                        Loc.T("yoon|연쇄가 길수록 값에 *배수*가 붙어. 30 · 80 · 200을 넘을 때 화면이 번쩍이는 게 그거야."),
                        Loc.T("yoon|뭉친 데를 노려. 한 방이 수십 방 값을 한다.") } },
                    { "b1", new[] { Y(1),
                        Loc.T("yoon|첫 청구서를 제때 갚는 사람, 생각보다 적어."),
                        Loc.T("yoon|다음은 *할부 1회*. 이제부터가 진짜 빚이야.") } },
                    { "b2", new[] { Y(2),
                        Loc.T("yoon|…요즘 청소선이 부쩍 늘었어. 궤도가 그만큼 더러워졌다는 거지.") } },
                    { "b3", new[] { Y(3),
                        Loc.T("go|어이, 청소부. 오늘 주운 고물 좀 봐라."),
                        Loc.T("go|전부 *케슬러 발사* 딱지야. 만든 지 1년도 안 된 위성이 벌써 고철이라니."),
                        Loc.T("go|…튼튼하게 만들 생각이 없는 거지. 부서져야 또 쏘니까.") } },
                    { "b4", new[] { Y(4),
                        Loc.T("anc|궤도일보 서 기자예요. 대표님, 잠깐 통화 되세요?"),
                        Loc.T("anc|케슬러 발사 위성이 *설계 수명의 절반*도 못 버틴다는 제보가 또 들어왔어요."),
                        Loc.T("anc|증거가 없어서 아직 기사로는 못 써요. 궤도에서 이상한 걸 보시면 꼭 알려 주세요.") } },
                    { "b5", new[] { Y(5),
                        Loc.T("yoon|다섯 장째 갚는 분한테 반말하기가 좀 그러네요."),
                        Loc.T("yoon|앞으로는 *대표님*이라고 부를게요.") } },
                    { "b6", new[] { Y(6),
                        Loc.T("yoon|그리고 대표님. 이건 제 혼잣말인데요."),
                        Loc.T("yoon|이번 분기에 케슬러 발사가 위성을 *두 배로* 쏜대요. 싸구려 부품으로."),
                        Loc.T("yoon|쓰레기가 늘면 청소선이 더 필요하고, 청소선이 늘면 대출도 늘죠. …저희 그룹은 *양쪽에서* 다 벌어요."),
                        Loc.T("yoon|못 들은 걸로 해 주세요. 저는 창구 직원일 뿐이니까.") } },
                    { "b7", new[] { Y(7),
                        Loc.T("han|대표님, 한 주임이에요! 케슬러 금융 주식 보셨어요?"),
                        Loc.T("han|청소선 대출이 늘 때마다 오른대요. *쓰레기가 많을수록 돈 버는 회사*라니, 웃기죠?"),
                        Loc.T("han|…웃으면 안 되나? 헤헤.") } },
                    { "b8", new[] { Y(8),
                        Loc.T("anc|대표님, 서 기자예요. 케슬러 쪽에서 취재를 막고 있어요."),
                        Loc.T("anc|*발사 기록*만 있으면 되는데… 궤도에서 블랙박스 같은 걸 주우시면 꼭 연락 주세요.") } },
                    { "b9", new[] { Y(9),
                        Loc.T("yoon|그리고… 위에서 대표님을 *주시하고* 있어요. 조용히 갚기만 하시면 아무 일 없을 거예요."),
                        Loc.T("yoon|…라고 전하라더군요. 전 전했습니다.") } },
                    { "b10", new[] { Y(10),
                        Loc.T("go|청소부! 이거 봐라. 심우주에서 건진 *발사 기록 칩*이다."),
                        Loc.T("go|케슬러 발사가 일부러 약하게 만든 설계도가 통째로 들어 있어."),
                        Loc.T("go|이걸 누구한테 줄지는 네가 정해라. 난 고물상이지 기자가 아니거든.") } },
                    { "b11", new[] { Y(11),
                        Loc.T("yoon|대표님이 칩을 가지고 계신 거, 알고 있어요."),
                        Loc.T("yoon|…걱정 마세요. 저도 이제 *어느 편인지* 정했으니까.") } },
                    { "b12", new[] { Y(12),
                        Loc.T("yoon|이건 창구 직원 말고, 제 이름으로 드리는 거예요. *케슬러 발사 내부 문서*."),
                        Loc.T("yoon|고 영감님 칩이랑 같이 보면… 1면에 실을지는 대표님이 정하세요."),
                        Loc.T("anc|궤도일보 호외입니다! *케슬러 그룹, 궤도 사업 전면 철수!*"),
                        Loc.T("yoon|…저도 오늘부로 그만둬요. 다음엔 창구 말고, 궤도에서 봬요.") } },
                };
                return scenes;
            }
        }

        readonly List<string> dlgQueue = new List<string>();                   // 조종실로 돌아오면 차례로
        public void SawEvent(int k) { string id = "ev" + k; if (!sim.M.flags.Contains("dlg:" + id) && !dlgQueue.Contains(id)) dlgQueue.Add(id); }
        public void SawChain() { if (!sim.M.flags.Contains("dlg:chain") && !dlgQueue.Contains("chain")) dlgQueue.Add("chain"); }
        string dlgId, dlgPend; int dlgI; float dlgShown, dlgHold, dlgRep, dlgAge, dlgPendT; bool dlgLog;
        Rect dlgSkipR, dlgLogR; GUIStyle dlgText;
        public bool DlgOn => dlgId != null;
        public void TestDlg(string id) => DlgStart(id, true);          // 에디터 시험용

        void DlgStart(string id, bool force = false)
        {
            if (!Scenes.ContainsKey(id)) return;
            string f = "dlg:" + id;
            if (!force && sim.M.flags.Contains(f)) return;
            if (!sim.M.flags.Contains(f)) sim.M.flags.Add(f);
            dlgId = id; dlgI = 0; dlgShown = 0; dlgAge = 0; dlgLog = false; dlgHold = 0;
            OrbitSfx.Play("tick", 0.6f, 0.02f, 0f);
        }
        void DlgEnd() { dlgId = null; dlgLog = false; game.Save(); }

        static string DlgWhoOf(string ln) { int p = ln.IndexOf('|'); return p < 0 ? "yoon" : ln.Substring(0, p); }
        static string DlgTextOf(string ln) { int p = ln.IndexOf('|'); return p < 0 ? ln : ln.Substring(p + 1); }
        static int DlgLen(string t) { int n = 0; foreach (char ch in t) if (ch != '*') n++; return n; }
        static string DlgReveal(string t, string hex, int n)
        {   // 한 글자씩 — *강조*는 색 태그로 바꾸고, 열린 채로 끊기면 닫아 준다
            var sb = new System.Text.StringBuilder(); bool open = false; int k = 0;
            foreach (char ch in t)
            {
                if (ch == '*') { sb.Append(open ? "</color>" : "<color=" + hex + ">"); open = !open; continue; }
                if (k >= n) break;
                sb.Append(ch); k++;
            }
            if (open) sb.Append("</color>");
            return sb.ToString();
        }

        // 언제 뜨나 — 방에 처음 들어갈 때 한 번 · 청구서를 갚고 조종실로 돌아왔을 때 (출동 중 · 로비 · 결산 화면엔 안 뜬다)
        void DlgTriggers()
        {
            if (dlgId != null || lobby || !sim.R.over || sim.M.careerOpen) return;
            if (dlgPend != null)
            {
                dlgPendT -= Time.unscaledDeltaTime;
                if (dlgPendT <= 0 && (sim.M.won || flow == 2) && paidT <= 0) { var p = dlgPend; dlgPend = null; DlgStart(p, true); }
                return;
            }
            if (sim.M.won) return;
            if (flow == 2 && dlgQueue.Count > 0) { var q = dlgQueue[0]; dlgQueue.RemoveAt(0); DlgStart(q); return; }
            if (flow == 2 && sim.S.runs > 0) DlgStart("fuel");
            if (dlgId == null && flow == 2 && sim.S.runs > 0 && sim.GateReady) DlgStart("gate");
            if (dlgId != null) return;
            if (flow == 3 && sim.StockOpen && !sim.M.flags.Contains("dlg:stock")) { GoFlow(4); return; }   // 📈 09-26 사장님 「증권을 열면 아예 증권 페이지로 가면서 알려 주게」
            if (flow == 4 && sim.StockOpen) DlgStart("stock");
            else if (flow == 5 && sim.ShopOpen) DlgStart("shop");
            else if (newsOpen) DlgStart("news");
        }

        bool DlgUpdate(Keyboard kb)
        {
            if (dlgId == null) return false;
            float dt = Time.unscaledDeltaTime; dlgAge += dt;
            var lines = Scenes[dlgId];
            string t = DlgTextOf(lines[dlgI]);
            if (dlgShown < DlgLen(t)) dlgShown += dt * 32;
            if (dlgAge < 0.25f) return true;                                   // 방에 들어온 그 클릭이 첫 줄을 넘기지 않게
            bool adv = false;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame && !kb.altKey.isPressed) adv = true;
                if (kb.escapeKey.wasPressedThisFrame && dlgLog) dlgLog = false;
            }
            var ms = Mouse.current;
            if (ms != null)
            {
                var mp = ms.position.ReadValue(); var gp = new Vector2(mp.x / scale, (Screen.height - mp.y) / scale);
                if (ms.leftButton.wasPressedThisFrame)
                {
                    dlgHold = 0; dlgRep = 0;
                    if (dlgSkipR.Contains(gp)) { OrbitSfx.Play("tick", 0.5f); DlgEnd(); return true; }
                    if (dlgLogR.Contains(gp)) { dlgLog = !dlgLog; OrbitSfx.Play("tick", 0.4f); return true; }
                    adv = true;
                }
                else if (ms.leftButton.isPressed && !dlgLog)
                {   // 누르고 있으면 빨리
                    dlgHold += dt;
                    if (dlgHold > 0.45f) { dlgRep -= dt; if (dlgRep <= 0) { dlgRep = 0.14f; adv = true; } }
                }
            }
            if (adv) DlgAdvance();
            return true;
        }

        void DlgAdvance()
        {
            if (dlgId == null) return;
            if (dlgLog) { dlgLog = false; return; }
            var lines = Scenes[dlgId];
            int total = DlgLen(DlgTextOf(lines[dlgI]));
            if (dlgShown < total) { dlgShown = total; return; }
            dlgI++; dlgShown = 0;
            if (dlgI >= lines.Length) DlgEnd();
            else OrbitSfx.Play("tick", 0.35f, 0.02f, 0f);
        }

        static Texture2D DlgFace(DlgWho w, int i)
        {
            if (w.tex == null) { w.tex = new Texture2D[w.path.Length]; for (int k = 0; k < w.path.Length; k++) w.tex[k] = Resources.Load<Texture2D>(w.path[k]); }
            return w.tex[Mathf.Clamp(i, 0, w.tex.Length - 1)] ?? w.tex[0];
        }

        void DlgDraw()
        {
            if (dlgId == null) return;
            var lines = Scenes[dlgId];
            string ln = lines[dlgI], t = DlgTextOf(ln); var w = Who[DlgWhoOf(ln)];
            ColorUtility.TryParseHtmlString(w.hex, out var c);
            int total = DlgLen(t); bool talking = dlgShown < total;
            if (dlgText == null) dlgText = new GUIStyle(label) { fontSize = 18, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            dlgText.normal.textColor = new Color(0.91f, 0.93f, 0.95f);

            // 뒤를 조금 어둡게 — 아래로 갈수록 진하게
            for (int i = 0; i < 12; i++) { GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.22f + 0.05f * i); GUI.DrawTexture(new Rect(0, RefH * i / 12f, vw, RefH / 12f + 1), white); }
            float bw = Mathf.Min(900, vw - 40), bh = 132, bx = vw / 2 - bw / 2, by = RefH - bh - 16;
            var box = new Rect(bx, by, bw, bh);

            // 초상화 — 상자 위 왼쪽에 걸쳐 선다 · 말하는 동안 살짝 들썩 (서 기자는 입도 움직인다)
            var face = DlgFace(w, talking ? Mathf.FloorToInt(Time.unscaledTime * 7) % 2 : 0);
            float bob = talking && !reduceMotion ? (Mathf.FloorToInt(Time.unscaledTime * 6) % 2) * 2 : 0;
            if (face != null) { GUI.color = Color.white; GUI.DrawTexture(new Rect(bx + 16, by - 124 - bob, 128, 128), face); }

            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.96f); GUI.DrawTexture(box, white); Frame(box, c, 2); GUI.color = Color.white;

            // 이름표
            string nm = "<size=15><b><color=#11151d>" + w.name + "</color></b></size>  <size=11><color=#2a2f3a>" + w.sub + "</color></size>";
            float nw = label.CalcSize(new GUIContent(nm)).x + 26;
            var nr = new Rect(bx + 164, by - 26, nw, 26);
            GUI.color = c; GUI.DrawTexture(nr, white); GUI.color = Color.white;
            GUI.Label(new Rect(nr.x + 13, nr.y + 3, nw, 22), nm, label);

            // 대사
            GUI.Label(new Rect(bx + 24, by + 18, bw - 64, bh - 40), DlgReveal(t, w.hex, Mathf.FloorToInt(dlgShown)), dlgText);
            GUI.Label(new Rect(bx + 24, by + bh - 24, 120, 18), "<size=11><color=#5f6878>" + (dlgI + 1) + " / " + lines.Length + "</color></size>", label);
            if (!talking && (reduceMotion || Mathf.FloorToInt(Time.unscaledTime * 2.2f) % 2 == 0))
                GUI.Label(new Rect(box.xMax - 40, by + bh - 30, 24, 24), "<size=16><color=" + w.hex + ">▼</color></size>", center);

            // 기록 · 건너뛰기
            dlgLogR = new Rect(box.xMax - 214, by - 30, 86, 24); dlgSkipR = new Rect(box.xMax - 120, by - 30, 120, 24);
            foreach (var (r, s) in new[] { (dlgLogR, dlgLog ? Loc.T("기록 닫기") : Loc.T("기록")), (dlgSkipR, Loc.T("건너뛰기 ▸▸")) })
            {
                GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.9f); GUI.DrawTexture(r, white); Frame(r, new Color(0.3f, 0.34f, 0.42f), 1); GUI.color = Color.white;
                GUI.Label(r, "<size=12><color=#aab3c2>" + s + "</color></size>", center);
            }
            if (dlgLog)
            {
                var lr = new Rect(vw / 2 - 360, 60, 720, by - 110);
                GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.97f); GUI.DrawTexture(lr, white); Frame(lr, new Color(0.3f, 0.34f, 0.42f), 1); GUI.color = Color.white;
                GUI.Label(new Rect(lr.x + 18, lr.y + 10, 400, 20), Loc.T("<size=12><color=#8a93a3>기록 — 이 장면에서 지나간 대사 · 누르면 닫힘</color></size>"), label);
                var sb = new System.Text.StringBuilder();
                for (int i = Mathf.Max(0, dlgI - 7); i <= dlgI; i++) { var ww = Who[DlgWhoOf(lines[i])]; sb.Append("<color=" + ww.hex + ">" + ww.name + "</color>  " + DlgTextOf(lines[i]).Replace("*", "") + "\n\n"); }
                var ls = new GUIStyle(dlgText) { fontSize = 14 };
                GUI.Label(new Rect(lr.x + 18, lr.y + 36, lr.width - 36, lr.height - 46), sb.ToString(), ls);
            }
        }
    }
}
