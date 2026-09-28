using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🕹 조종실 — 선체 그림 · 명판 · 조종대 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 조종실 — 판과 판 사이의 집. 출동은 여기서만
        // 시안(cockpit.html 1600×1000)을 960×600 으로 — 사다리꼴 창 · 양옆 선체 판 · 아래 조종대
        static readonly Vector2[] WinPoly = { new Vector2(198, 42), new Vector2(762, 42), new Vector2(834, 336), new Vector2(126, 336) };
        static Texture2D hullTex;
        static bool InQuad(Vector2[] q, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = q.Length - 1; i < q.Length; j = i++)
                if ((q[i].y > y) != (q[j].y > y) && x < (q[j].x - q[i].x) * (y - q[i].y) / (q[j].y - q[i].y) + q[i].x) inside = !inside;
            return inside;
        }
        static Vector2[] Grow(Vector2[] q, float d) => new[] { q[0] + new Vector2(-d * 1.1f, -d), q[1] + new Vector2(d * 1.1f, -d), q[2] + new Vector2(d * 1.1f, d), q[3] + new Vector2(-d * 1.1f, d) };
        // 🪢 밧줄 — 비스듬한 꼬임이 번갈아 밝고 어둡다
        static Color32 Rope(float fx, float fy, Color32 rim)
        {
            int k = ((int)(fx * 0.7f + fy) / 5) % 3;
            return k == 0 ? new Color32(214, 176, 112, 255) : k == 1 ? new Color32(168, 128, 74, 255) : rim;
        }
        // 🎖 군용 — 남색 틀 가운데 하늘색 선 한 줄 · 일정한 간격의 볼트
        static Color32 Mil(float fx, float fy, Color32 rim, Vector2[] outer)
        {
            float d = Mathf.Min(Mathf.Abs(fy - (outer[0].y + 6)), Mathf.Abs(fy - (outer[2].y - 6)));
            if (d < 1.2f) return new Color32(110, 210, 255, 255);
            return ((int)fx % 40 < 3 && ((int)fy % 40 < 3)) ? new Color32(80, 110, 160, 255) : rim;
        }
        // 🔺 붉은 화살 — 회색 틀에 일정한 간격으로 붉은 꺾쇠
        static Color32 Chevron(float fx, float fy, Color32 rim)
        {
            int m = ((int)(fx + Mathf.Abs(fy % 24 - 12)) % 28);
            return m < 5 ? new Color32(210, 60, 52, 255) : rim;
        }
        // ❄ 성에 — 하늘빛 틀에 흰 서리 알갱이가 드문드문 (냉동선)
        static Color32 Frost(float fx, float fy, Color32 rim)
        {
            int h = ((int)fx * 73856093) ^ ((int)fy * 19349663);
            int m = (h >> 3) & 63;
            return m < 3 ? new Color32(235, 248, 255, 255) : m < 9 ? new Color32(150, 200, 230, 255) : rim;
        }
        // 🎆 주황 띠 — 올리브 틀에 굵은 주황 사선 띠가 드문드문 (분열탄선)
        static Color32 Stripe(float fx, float fy, Color32 rim)
        {
            int m = ((int)(fx - fy)) % 44; if (m < 0) m += 44;
            return m < 6 ? new Color32(240, 120, 40, 255) : rim;
        }
        static void BuildHull(ShipTheme t)
        {
            const int W = 960, H = 600;
            hullTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Color32 bg = t.bg, plate = t.plate, rim = t.rim, edge = t.edge;   // 🎨 배 테마 (09-26)
            Vector2[] outer = Grow(WinPoly, t.rimW), inner = Grow(WinPoly, 1.4f);
            var leftPlate = new[] { new Vector2(0, 0), new Vector2(198, 42), new Vector2(126, 336), new Vector2(0, 384) };
            var rightPlate = new[] { new Vector2(W, 0), new Vector2(762, 42), new Vector2(834, 336), new Vector2(W, 384) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    Color32 c;
                    if (InQuad(WinPoly, fx, fy)) c = new Color32(0, 0, 0, 0);
                    else if (InQuad(inner, fx, fy)) c = edge;
                    else if (InQuad(outer, fx, fy)) c = t.deco == 1 ? (((int)(fx + fy) / 8) % 2 == 0 ? new Color32(255, 204, 31, 255) : rim) : t.deco == 2 ? Rope(fx, fy, rim) : t.deco == 3 ? Mil(fx, fy, rim, outer) : t.deco == 4 ? Chevron(fx, fy, rim) : t.deco == 5 ? Frost(fx, fy, rim) : t.deco == 6 ? Stripe(fx, fy, rim) : rim;   // 창틀 무늬 — 산탄선 경고 줄무늬 · 작살선 밧줄
                    else if (fy > 336 && (fy > 384 || InQuad(new[] { new Vector2(0, 384), new Vector2(126, 336), new Vector2(834, 336), new Vector2(W, 384) }, fx, fy) || fy >= 384))
                    {
                        float k = Mathf.InverseLerp(336, H, fy);   // 조종대 — 위가 밝고 아래로 어두워진다
                        c = Color32.Lerp(t.deskTop, t.deskBot, k);
                    }
                    else if (InQuad(leftPlate, fx, fy) || InQuad(rightPlate, fx, fy)) c = plate;
                    else c = bg;
                    px[(H - 1 - y) * W + x] = c;
                }
            hullTex.SetPixels32(px); hullTex.Apply();
        }

        // 계기판 — 테두리(베젤) · 제목줄 · 안쪽 어두운 화면
        Color PlateCol => Th.plateCol; Color Bezel => Th.bezel; Color ScreenCol => Th.screen;   // 🎨 배 테마
        bool Plate(Rect r, string cap, string right, Color hot, bool clickable)
        {
            bool hover = clickable && r.Contains(Event.current.mousePosition);
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = PlateCol; GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, hover ? hot : Bezel, hover ? 2.5f : 2f);
            Lbl(new Rect(r.x + 10, r.y + 6 - 3, r.width - 20, 22), "<size=11><color=#8a9bb3>" + cap + "</color></size>", label);
            if (!string.IsNullOrEmpty(right)) Lbl(new Rect(r.x + 10, r.y + 6 - 3, r.width - 20, 22), "<size=11>" + right + "</size>", cost);
            return clickable && Bt(r, GUIContent.none, GUIStyle.none);
        }
        Rect Scr(Rect r, float top, float h) { var sr = new Rect(r.x + 8, r.y + top, r.width - 16, h); GUI.color = ScreenCol; GUI.DrawTexture(sr, white); GUI.color = Color.white; return sr; }
        string Led(Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">●</color> ";

        void Cockpit()
        {
            var S = sim.S; var M = sim.M;
            if (hullTex == null || hullShip != sim.Ship) { hullShip = sim.Ship; BuildHull(Th); }
            // 선체 — 가운데 960 밖(넓은 화면)은 바탕색
            GUI.color = (Color)Th.bg; GUI.DrawTexture(new Rect(0, 0, ox + 1, RefH), white); GUI.DrawTexture(new Rect(ox + 959, 0, vw - ox - 959, RefH), white); GUI.color = Color.white;
            GUI.DrawTexture(new Rect(ox, 0, 960, 600), hullTex);
            GUI.color = Th.rivet;                                             // 리벳
            for (int i = 0; i <= 12; i++) GUI.DrawTexture(new Rect(ox + 198 + 564 * i / 12f - 2, 30, 4, 4), texDisc);
            for (int i = 0; i <= 18; i++) { float x = 36 + i * 49; GUI.DrawTexture(new Rect(ox + x - 2, 365 + (x < 126 || x > 834 ? 12 : 0), 4, 4), texDisc); }
            GUI.color = Color.white;
            CockDeco();                                                       // 🎨 배 테마는 창으로만 — 창틀 무늬(BuildHull) · 창 위 장식 (09-26)

            // 위 — 돈 (창 위 가운데)
            big.fontSize = 22;   // 도트 글꼴 11의 배수 (정돈 5)
            Lbl(new Rect(ox + 330, 2, 300, 28), Loc.T("<size=13><color=#8a9bb3>돈</color></size>  ") + KNum.Fmt(shown), new GUIStyle(big) { alignment = TextAnchor.MiddleCenter });
            big.fontSize = 20;
            CreditScreen = new Vector2((ox + 480) * scale, 16 * scale);
            // 창 안 — 납부 완료 알림
            if (paidT > 0 && paidBill > 0)
            {
                var pb = SweepSim.Bills[paidBill - 1];
                title.fontSize = 24; Lbl(new Rect(ox + 200, 60, 560, 32), Loc.T("<color=#ffdf95>납부 완료</color> · ") + pb.t, title);
                title.fontSize = 16; Lbl(new Rect(ox + 200, 92, 560, 24), pb.perk, title);
            }

            bool due = S.overdue && !M.cleanReady;
            // ① 청구서 — 조종대 위 홀로그램 (09-24 C안)
            HoloBill(due);

            // ② 출동 보고 — 초록 브라운관 · ③ 궤도일보 — LED 전광판 · ④ 회사 명판 — 황동판 (09-24 사장님 BBA)
            CrtReport(new Rect(ox + 12, 34, 158, 120));
            LedNews(new Rect(ox + 769, 36, 176, 150));
            BrassPlate(new Rect(ox + 769, 192, 176, 52));
            MyStockBoard();                                             // 📈 내 주식 시세판
            FrontPick();                                                // ★ 1면 조작

            // ⑤ ‹ 정비고로 · 증권 하러 가기 › — 화면 양옆 탭 (누르면 옆 방으로 슥)
            int canN = 0; for (int b = 0; b < 4; b++) canN += CanCount(b);
            if (sim.ShopOpen) { if (NavTab(false, Loc.T("부품 가게"), canN > 0 ? Loc.T("<color=#f2c14e>정비 ") + canN + Loc.T("칸</color>") : "", SweepGame.Amber)) GoFlow(5); }   // 한 칸씩 — 정비고 ‹ 가게 ‹ 조종실 › 증권 (09-25 사장님: 곧장 정비고로 가니 이상하다)
            else if (NavTab(false, Loc.T("정비고로"), canN > 0 ? Loc.T("<color=#f2c14e>살 칸 ") + canN + "</color>" : "", SweepGame.Amber)) GoFlow(3);
            if (NavTab(true, Loc.T("증권 하러 가기"), sim.StockOpen ? "" : Loc.T("<color=#5f6878>잠김</color>"), new Color(0.62f, 0.94f, 0.75f))) GoFlow(4);

            // ⑥ 항로 — 조종대 오른쪽 홀로그램 (행성 다섯 · 의뢰)
            RouteHolo();

            // ⑦ 출동 버튼 (가운데 아래) — 받침 위에 둥근 누름 버튼, 윗면에 「출동」
            {
                float cxm = ox + 480, fy0 = 458, fw = 150, fh = 62, side = 16;
                var hit = new Rect(cxm - fw / 2, fy0, fw, fh + side);
                bool hover = !due && hit.Contains(Event.current.mousePosition);
                bool press = hover && Mouse.current != null && Mouse.current.leftButton.isPressed;
                float dip = press ? 10 : 0;                                   // 누르면 몸통이 받침 속으로
                float glow = due ? 0 : 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
                Color face = due ? new Color(0.32f, 0.3f, 0.28f) : hover ? Th.btnHover : Th.btnFace;
                Color wall = due ? new Color(0.18f, 0.17f, 0.16f) : Th.btnWall;
                // 빛 번짐
                GUI.color = new Color(Th.btnGlow.r, Th.btnGlow.g, Th.btnGlow.b, due ? 0 : 0.10f + 0.10f * glow + (hover ? 0.08f : 0)); GUI.DrawTexture(new Rect(cxm - 120, fy0 - 26, 240, 150), texDisc);
                // 받침 (어두운 테 + 그림자)
                GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(cxm - 92, fy0 + 24, 184, 76), texDisc);
                GUI.color = new Color(0.1f, 0.13f, 0.18f); GUI.DrawTexture(new Rect(cxm - 88, fy0 + 16, 176, 76), texDisc);
                GUI.color = new Color(0.17f, 0.22f, 0.3f); GUI.DrawTexture(new Rect(cxm - 84, fy0 + 14, 168, 72), texDisc);
                GUI.color = new Color(0.05f, 0.07f, 0.1f); GUI.DrawTexture(new Rect(cxm - fw / 2 - 4, fy0 + side + 2, fw + 8, fh + 4), texDisc);
                // 몸통 옆면
                float fy = fy0 + dip, sh = side - dip * 0.8f;
                GUI.color = wall; GUI.DrawTexture(new Rect(cxm - fw / 2, fy + sh, fw, fh), texDisc);
                GUI.DrawTexture(new Rect(cxm - fw / 2, fy + fh / 2, fw, sh), white);
                // 윗면 + 반사
                GUI.color = face; GUI.DrawTexture(new Rect(cxm - fw / 2, fy, fw, fh), texDisc);
                GUI.color = new Color(1, 1, 1, due ? 0.05f : 0.22f); GUI.DrawTexture(new Rect(cxm - fw * 0.32f, fy + 6, fw * 0.5f, fh * 0.32f), texDisc);
                GUI.color = Color.white;
                // 윗면 글자
                string word = due ? Loc.T("납부일") : M.cleanReady ? Loc.T("청산") : Loc.T("출동");
                Lbl(new Rect(cxm - fw / 2, fy + 2, fw, fh - 4), "<size=" + (due ? 20 : 28) + "><b><color=" + (due ? "#6a655e" : Th.btnInk) + ">" + word + "</color></b></size>", center);
                if (Bt(hit, GUIContent.none, GUIStyle.none) && paidT < 2.4f) Go();
            }

            // 🧯 파산 단추 — 출동 단추 왼쪽 위, 유리 덮개 속 (09-24 사장님)
            BankruptGlass(new Rect(ox + 480 - 75 - 96, 404, 70, 78));
            // 🎟 복권 — 분홍 홀로그램 (출동 버튼 오른쪽)
            LottoHolo();
            // 아래 한 줄
            string tip = due ? "<color=" + (dueNag > 0 ? "#ff9b8f" : "#b8a89a") + Loc.T(">납부일 — 왼쪽 홀로그램 청구서: 납부 · 대출 · 또는 파산</color>") : Loc.T("Space = 출동 · 창밖 = 지금 내 궤도 · 궤도 넓히기 ") + Mathf.RoundToInt((float)(sim.Widen - 1) * 100) + Loc.T("% · 한 판 ") + Mathf.RoundToInt((float)sim.FuelMax) + Loc.T("초");
            if (due || sim.S.runs < 3 && sim.M.company <= 1) Lbl(new Rect(ox + 200, 570 - 3, 560, 26), "<size=12>" + tip + "</size>", center);   // 안내 줄은 처음 세 판 · 납부일만 (정돈 13)
        }

    }
}
