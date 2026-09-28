using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🧰 글자 · 틀 도우미 — 조사 · 줄바꿈 · 자르기 · 테두리 · 판 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 조종실 — 첫 화면 (사장님 09-23: "첫 화면 자체를 우주선 화면 컨셉으로 · 유저 친화적으로")
        // 가운데 창 = 지금 내 궤도 (사면 바로 창밖에 보인다) · 계기판마다 할 일 하나 · 강화는 정비고(네 칸 · 36칸)

        public bool bayOpen; int bayTab;
        // 한글은 유니티가 글자 가운데서 줄을 끊는다 (09-27 「머물 / 다」) — 띄어쓰기 자리에서만 끊도록 줄바꿈을 미리 넣는다. 꾸밈 태그 없는 글만
        static string Ro(string w)   // 받침이 있으면 「으로」 (ㄹ 받침은 「로」)
        {
            if (string.IsNullOrEmpty(w)) return Loc.T("로");
            char c = w[w.Length - 1]; if (c < 0xAC00 || c > 0xD7A3) return Loc.T("로");
            int jong = (c - 0xAC00) % 28; return jong == 0 || jong == 8 ? Loc.T("로") : Loc.T("으로");
        }
        static string Eul(string w)  // 받침이 있으면 「을」
        {
            if (string.IsNullOrEmpty(w)) return Loc.T("를");
            char c = w[w.Length - 1]; if (c < 0xAC00 || c > 0xD7A3) return Loc.T("를");
            return (c - 0xAC00) % 28 == 0 ? Loc.T("를") : Loc.T("을");
        }
        GUIStyle kwrapSt;
        string KWrap(string s, int size, float w)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') >= 0 || w < 20) return s;
            if (kwrapSt == null) kwrapSt = new GUIStyle(label) { wordWrap = false, richText = false };
            kwrapSt.fontSize = size;
            var sb = new System.Text.StringBuilder();
            foreach (var para in s.Split('\n'))
            {
                if (sb.Length > 0) sb.Append('\n');
                string line = "";
                foreach (var word in para.Split(' '))
                {
                    string t = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && kwrapSt.CalcSize(new GUIContent(t)).x > w) { sb.Append(line).Append('\n'); line = word; }
                    else line = t;
                }
                sb.Append(line);
            }
            return sb.ToString();
        }

        public bool CockpitView => (flow >= 2 && flow <= 5) && sim != null && sim.R.over && !sim.M.careerOpen && !sim.M.won;   // 조종실 — 카메라가 물러나 지구가 창 가운데 (09-23 부활)
        public int flow;                         // 0 출동 중 · 1 결산 · 2 조종실 · 3 정비고(왼쪽 끝) · 5 부품 가게(정비고 오른쪽) · 4 증권(오른쪽 방)
        static readonly Color[] BranchCol = { SweepGame.Amber, SweepGame.Cyan, SweepGame.Violet, SweepGame.Green };
        static readonly string[] BayDesc = { Loc.T("조준점 하나 → 넓은 착탄 → 한 번에 여럿"), Loc.T("알아서 줍는다 — 한 방에 부서지는 것만"), Loc.T("블랙홀 스킬 · 지구에서 연료 보급"), Loc.T("돈 · 청구서 · 대출 · 기사") };
        public static readonly Rect Win = new Rect(200, 44, 560, 344);

        static string Clip(string s, int n) { if (Loc.En) n = n * 17 / 10; return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }   // 🌐 영어 글자는 한글보다 좁다

        void Frame(Rect r, Color c, float w)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), white); GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), white);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), white); GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), white);
            GUI.color = Color.white;
        }

        bool Panel(Rect r, string label, string right, Color edge, bool clickable = true)   // 안에 단추가 있는 칸은 clickable = false (칸 전체 단추가 안쪽 클릭을 가로챈다)
        {
            bool hover = clickable && r.Contains(Event.current.mousePosition);
            GUI.DrawTexture(r, texCard2);
            Frame(r, hover ? edge : new Color(0.14f, 0.2f, 0.28f), hover ? 2 : 1.5f);
            GUI.Label(new Rect(r.x + 9, r.y + 6, r.width - 18, 16), label, head);
            if (right != null) GUI.Label(new Rect(r.x + 9, r.y + 5, r.width - 18, 16), right, cost);
            return clickable && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }



    }
}
