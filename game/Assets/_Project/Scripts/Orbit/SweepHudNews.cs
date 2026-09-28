using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 📰 궤도일보 · 신문 · 크레딧 · 엔딩 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 궤도일보 (§11)

        void Ticker()
        {
            string line;
            bool breaking = breakingT > 0 && breakingHead != null;
            if (breaking) line = breakingHead;
            else
            {
                var heads = new List<string>();
                for (int i = sim.M.news.Count - 1; i >= 0 && heads.Count < 3; i--) heads.Add(Loc.T(sim.M.news[i].head));
                int k = tickI % (heads.Count + 2);
                line = k < heads.Count ? heads[k] : SweepSim.World[(tickI * 7) % SweepSim.World.Length];
            }
            float y = RefH - 22;
            float x = sim.R.over ? ox + 14 : 14;
            if (breaking) { GUI.DrawTexture(new Rect(x, y + 2, 34, 15), texRed); Lbl(new Rect(x, y, 34, 18), Loc.T("<color=#ffffff>속보</color>"), center); x += 40; }
            Lbl(new Rect(x, y, 700, 18), Loc.T("궤도일보 · ") + line, dim);
        }

        void News()
        {
            var M = sim.M;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var box = new Rect(ox + 40, 30, 880, 520);
            GUI.DrawTexture(box, texCard);
            // 목록
            var lr = new Rect(box.x + 10, box.y + 34, 300, box.height - 70);
            Lbl(new Rect(box.x + 12, box.y + 8, 300, 20), Loc.T("궤도일보 — 기사 목록"), label);
            int total = M.news.Count;
            newsScroll = GUI.BeginScrollView(lr, newsScroll, new Rect(0, 0, 280, total * 40));
            for (int j = 0; j < total; j++)
            {
                int i = total - 1 - j; var it = M.news[i];
                string kind = it.kind == "scoop" ? Loc.T("<color=#ff6b5a>특종</color>") : it.kind == "world" ? Loc.T("<color=#8a93a3>세상</color>") : it.kind == "extra" ? Loc.T("<color=#ffdf95>호외</color>") : Loc.T("<color=#f2c14e>우리</color>");
                if (Bt(new Rect(0, j * 40, 280, 36), (it.read ? "   " : "<color=#ff6b5a>●</color> ") + kind + "  <size=12>" + Clip(Loc.T(it.head), Loc.En ? 16 : 20) + "</size>", i == newsSel ? btn : btnOff)) { newsSel = i; it.read = true; }
            }
            GUI.EndScrollView();
            int scoops = 0; foreach (var it in M.news) if (it.kind == "scoop") scoops++;
            Lbl(new Rect(box.x + 12, box.yMax - 30, 300, 18), Loc.T("모은 기사 ") + total + Loc.T(" · 특종 ") + scoops + Loc.T(" / 6 · 파산해도 남는다"), small);
            // 전문
            if (newsSel < 0 && total > 0) { newsSel = total - 1; M.news[newsSel].read = true; }
            var pr = new Rect(box.x + 322, box.y + 10, box.width - 332, box.height - 20);
            GUI.DrawTexture(pr, texPaper);
            if (newsSel >= 0 && newsSel < total)
            {
                var it = M.news[newsSel];
                float x = pr.x + 22, w = pr.width - 44, y = pr.y + 16;
                paperHead.fontSize = 26; Lbl(new Rect(x, y - 3, 240, 36), Loc.T("궤도일보"), paperHead);
                paperSmall.alignment = TextAnchor.UpperRight; Lbl(new Rect(x, y + 8, w, 18), Loc.T("출동 ") + it.run + Loc.T("일째 · ") + it.company + Loc.T("대 시절"), paperSmall); paperSmall.alignment = TextAnchor.UpperLeft;
                y += 36; GUI.DrawTexture(new Rect(x, y, w, 3), texInk); y += 12;
                string kn = it.kind == "scoop" ? Loc.T("특종") : it.kind == "world" ? Loc.T("세상 소식") : it.kind == "extra" ? Loc.T("호외") : Loc.T("우리 소식");
                GUI.color = it.kind == "scoop" ? new Color(0.75f, 0.22f, 0.17f) : it.kind == "world" ? new Color(0.42f, 0.39f, 0.34f) : new Color(0.72f, 0.53f, 0.04f);
                GUI.DrawTexture(new Rect(x, y, 70, 18), white); GUI.color = Color.white;
                Lbl(new Rect(x, y, 70, 18), "<color=#ffffff>" + kn + "</color>", center); y += 26;
                paperHead.fontSize = 22; float hh = paperHead.CalcHeight(new GUIContent(Loc.T(it.head)), w);
                Lbl(new Rect(x, y, w, hh), Loc.T(it.head), paperHead); y += hh + 14;
                float bh = paperBody.CalcHeight(new GUIContent(Loc.T(it.body)), w);
                Lbl(new Rect(x, y, w, bh), Loc.T(it.body), paperBody); y += bh + 16;
                Lbl(new Rect(x, y, w, 16), Loc.T("— 궤도일보 궤도부"), paperSmall);
            }
            if (Bt(new Rect(box.xMax - 110, box.y + 6, 100, 24), Loc.T("닫기 (Esc)"), btn)) newsOpen = false;
        }

        // ───────────────────────────────── 엔딩 — 마지막 호외 → 결과판 (§9-5)

        // 🎬 엔딩 크레딧 — 지구 둘레 쓰레기 0, 천천히 올라가는 글 (09-24 사장님 20번 · 시안). 누르거나 Space = 빨리
        float creditT;
        public void TestEnd(int st) { endStage = st; creditT = st == 2 ? 3 : 0; }   // 에디터 시험용
        void Credits()
        {
            var M = sim.M;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool fast = (kb != null && kb.spaceKey.isPressed) || (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed);
            creditT += Time.unscaledDeltaTime * (fast ? 6 : 1);
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            double minutes = 0; foreach (var h in M.history) minutes += h.minutes;
            string[,] rows =
            {
                { "", Loc.T("<size=34><b><color=#ffdf95>궤도 청소부</color></b></size>") },
                { "", Loc.T("<color=#c8d0dc>오늘도 궤도는 깨끗합니다</color>") },
                { Loc.T("만든 사람"), Loc.T("사장님") },
                { Loc.T("함께 만든"), "Claude" },
                { Loc.T("도트"), "PixelLab" },
                { Loc.T("글꼴"), Loc.T("갈무리 (Galmuri)") },
                { Loc.T("엔진"), "Unity" },
                { Loc.T("먼저 해 본 친구들"), Loc.T("고마워") },
                { Loc.T("기록"), Mathf.RoundToInt((float)minutes) + Loc.T("분 · 회사 ") + M.company + Loc.T("대 · 최고 연쇄 ") + M.bestChain },
                { "", Loc.T("<color=#d8ccff>★ 전설 경력 ") + M.legend + "</color>" },
            };
            float y = RefH + 20 - creditT * 42;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                if (rows[i, 0].Length > 0) { Lbl(new Rect(0, y, vw, 18), "<size=11><color=#8a93a3>" + rows[i, 0] + "</color></size>", center); y += 20; }
                Lbl(new Rect(0, y, vw, 40), "<size=20><b>" + rows[i, 1] + "</b></size>", center); y += 70;
            }
            Lbl(new Rect(vw - 320, RefH - 30, 222, 20), Loc.T("<size=11><color=#5f6878>누르고 있으면 빨리</color></size>"), cost);
            if (Bt(new Rect(vw - 90, RefH - 34, 80, 24), Loc.T("<size=11>넘기기</size>"), btnOff) || y < -40) endStage = 1;
        }

        void Ending()
        {
            var M = sim.M;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            if (endStage == 0)
            {
                var pr = new Rect(ox + 130, 40, 700, 500);
                GUI.DrawTexture(pr, texPaper);
                float x = pr.x + 30, w = pr.width - 60, y = pr.y + 20;
                paperHead.fontSize = 40; Lbl(new Rect(x, y, w, 48), Loc.T("궤도일보 — 호외"), paperHead);
                y += 56; GUI.DrawTexture(new Rect(x, y, w, 4), texInk); y += 20;
                paperHead.fontSize = 46; Lbl(new Rect(x, y, w, 56), Loc.T("궤도 청소율 100%"), paperHead); y += 70;
                Lbl(new Rect(x, y, w, 90), Loc.T("지구 둘레에 쓰레기가 하나도 없다. 주식회사 궤도 청소부 (") + M.company + Loc.T("대)가 청소선 할부를 끝까지 갚고, 마지막 출동에서 궤도를 전부 치웠다. 케슬러 발사는 이번 분기 발사 계획이 없다고 밝혔다."), paperBody); y += 100;
                if (M.scoops >= 6) { paperHead.fontSize = 24; Lbl(new Rect(x, y, w, 60), Loc.T("케슬러 그룹, 궤도 사업 전면 철수"), paperHead); y += 50; }
                else Lbl(new Rect(x, y, w, 20), Loc.T("(특종 ") + M.scoops + Loc.T(" / 6 — 블랙박스를 더 모으면 한 줄이 더 붙는다)"), paperSmall);
                if (Bt(new Rect(pr.center.x - 100, pr.yMax - 70, 200, 44), Loc.T("다음"), bigBtn)) { endStage = 2; creditT = 0; }
                return;
            }
            if (endStage == 2) { Credits(); return; }
            float cx = ox + 180, cw = 600;
            title.fontSize = 44; Lbl(new Rect(cx, 40, cw, 60), Loc.T("빚 청산"), title);
            Lbl(new Rect(cx, 102, cw, 22), Loc.T("주식회사 궤도 청소부 (") + M.company + Loc.T("대) — 청소선은 이제 조종사의 것이다"), center);
            double minutes = 0; foreach (var h in M.history) minutes += h.minutes;
            string[] rows =
            {
                Loc.T("걸린 시간   ") + Mathf.RoundToInt((float)minutes) + Loc.T("분"),
                Loc.T("출동 · 파산   ") + M.totalRuns + " · " + M.bankrupt,
                Loc.T("부순 잔해   ") + KNum.Fmt(M.broken),
                Loc.T("최대 연쇄 · 최대 압축   ") + M.bestChain + " · " + M.bestPack,
                Loc.T("기사 · 특종   ") + M.news.Count + " · " + M.scoops + " / 6",
            };
            for (int i = 0; i < rows.Length; i++) Lbl(new Rect(cx, 140 + i * 24, cw, 22), rows[i], center);
            Lbl(new Rect(cx, 272, cw, 18), Loc.T("지난 회사들"), head);
            for (int i = 0; i < M.history.Count && i < 8; i++)
            {
                var h = M.history[i];
                Lbl(new Rect(cx, 292 + i * 20, cw, 20), "(" + h.company + Loc.T("대)  ") + (h.won ? Loc.T("<color=#ffdf95>빚 청산</color>") : SweepSim.Bills[Mathf.Min(h.bill, SweepSim.Bills.Length - 1)].t + Loc.T("에서 파산")) + Loc.T(" · 출동 ") + h.runs + " · " + Mathf.RoundToInt((float)h.minutes) + Loc.T("분"), label);
            }
            Lbl(new Rect(cx, 470, cw, 20), Loc.T("<color=#f2c14e>오늘도 궤도는 깨끗합니다.</color>"), center);
            // ∞ 무한 궤도 · ★ 새 회사 (09-24 사장님 12 · 26번)
            if (Bt(new Rect(cx - 10, 500, 220, 44), Loc.T("<color=#d8ccff>무한 궤도로 ▸</color>"), bigBtn)) { sim.EnterEndless(); showResult = false; flow = 2; OrbitSfx.Play("launch", 0.8f); }
            if (Bt(new Rect(cx + 220, 500, 200, 44), Loc.T("새 회사로 · ★") + M.legend, bigBtn)) { game.NewGame(true); showResult = false; }
            if (Bt(new Rect(cx + 430, 506, 180, 32), Loc.T("<size=12>기록까지 모두 지우기</size>"), btnC)) { game.WipeAll(); showResult = false; }
            Lbl(new Rect(cx, 552, cw, 18), Loc.T("<size=11><color=#8a93a3>★ 전설 경력 ") + M.legend + Loc.T(" — 다음 회사부터 모든 값 +") + (M.legend * 10) + Loc.T("% · 처음 열쇠 +") + M.legend + "</color></size>", center);
        }
    }
}
