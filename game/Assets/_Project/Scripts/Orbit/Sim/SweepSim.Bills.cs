using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🧾 청구서 · 파산 · 경력 · 항로 · 의뢰 · 궤도일보 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ───────────────────────── 청구서 · 파산 · 경력
        public bool PayBill()
        {
            if (!R.over || S.bill >= Bills.Length || S.cash < BillAmount) return false;
            S.cash -= BillAmount;
            FinishBill();
            return true;
        }

        void FinishBill()
        {
            var b = Bills[S.bill];
            S.creditPending += b.credit;
            S.bill++; S.keys++;                                              // 🔑 청구서마다 열쇠 1 (파산 없이도 조금씩 열린다)
            S.overdue = false; S.overRuns = 0; S.billAmount = -1;
            S.billDue = S.bill < Bills.Length ? Bills[S.bill].due + Lv("e_talk") : 0;
            string nid = "bill" + S.bill;
            AddNews(nid);
            Emit(SwEv.BillPaid, 0, 0, S.bill, 0, b.t + Loc.T(" 납부 완료 — ") + b.perk + Loc.T(" · 열쇠 +1"));
            CheckClean();
        }

        public bool Bankrupt()
        {
            if (!R.over || !CanBankrupt) return false;
            M.history.Add(new PastCompany { company = M.company, bill = S.bill, runs = S.runs, minutes = (M.playSeconds - S.startedAt) / 60 });
            M.credit += S.creditPending * CreditK;
            M.bankrupt++;
            AddNews(M.bankrupt == 1 ? "bankrupt1" : M.bankrupt == 2 ? "bankrupt2" : null, Loc.T("궤도 청소부 (") + M.company + Loc.T("대), 출동 ") + S.runs + Loc.T("번 만에 파산"), Loc.T("청구서 ") + S.bill + Loc.T("장을 갚고 문을 닫았다. 빚은 날아갔고, 조종사의 경력은 남았다."));
            M.company++;
            double carry = Lv("x_bh_eco") > 0 ? Math.Floor(S.cash * 0.1) : 0; int keepPart = -1;
            if (Lv("x_bh_eco") > 0 && S.parts != null) foreach (var pid in S.parts) if (pid >= 0 && (keepPart < 0 || Parts.Defs[pid].rar > Parts.Defs[keepPart].rar)) keepPart = pid;
            S = new SweepState { startedAt = M.playSeconds, layout = 2 };   // layout 2 — 빠뜨리면 다음에 켤 때 밀린 옛 저장으로 알고 칸을 옮겼다 (09-25 테스트 짜다 발견)
            if (carry > 0) S.cash += carry;
            S.keys += Up(11); S.cash += 60 * Up(3); S.billDue += Up(6); ApplyPerm();   // 🛠 영구 강화 — 예비 열쇠 · 시작 돈 · 첫 기한 (열쇠 이월 · 파산 열쇠는 09-26 §7 로 뺐다)
            if (keepPart >= 0) S.parts[Parts.Defs[keepPart].slot] = keepPart;   // ◆ 파산 보험 — 돈 10% · 제일 좋은 부품 하나
            MakeMarket();
            M.careerOpen = true;
            Preview();
            if (M.company == 2) AddNews("company2");
            Emit(SwEv.Bankrupt, 0, 0, M.company, 0, Loc.T("주식회사 궤도 청소부 (") + (M.company - 1) + Loc.T("대) — 파산 · 신용 ") + M.credit);
            return true;
        }

        public bool BuyCareer(int i)
        {
            int c = CareerCost(i);
            if (c < 0 || M.credit < c) return false;
            M.credit -= c; M.career[i]++;
            return true;
        }

        public void CloseCareer() { M.careerOpen = false; }

        public void SetOrbit(int i) { if (R.over && i >= 0 && i < Orbits.Length && Open(i) && i != S.orbit) { S.orbit = i; RollContract(); Preview(); } }

        public void RollContract()
        {
            if (!ContractsOn) { S.contract = -1; return; }
            var pool = new List<int>();
            for (int i = 0; i < Contracts.Length; i++)
                if (Contracts[i].orbit == S.orbit) pool.Add(i);                  // 압류 딱지 의뢰(orbit -1)는 추심선이 쉬어서 뺐다 — 딱지가 안 붙으니 못 이룬다
            int prev = S.contract;
            for (int t = 0; t < 6; t++) { S.contract = pool[rng.Next(pool.Count)]; if (S.contract != prev || pool.Count == 1) break; }
        }

        public bool Reroll() { if (!R.over || S.rerolled || !ContractsOn) return false; S.rerolled = true; RollContract(); return true; }

        public int ContractProgress(SweepRun r)
        {
            var c = CurContract; if (c == null) return 0;
            switch (c.Value.kind)
            {
                case 0: return r.cVault; case 1: return r.cFuel; case 2: return r.cChip; case 3: return r.cSat;
                case 4: return r.chainBest; case 5: return r.cTank; case 6: return r.packBest; case 7: return r.cBig; default: return r.cTag;
            }
        }

        // ───────────────────────── 뉴스 — 궤도일보 (§11)
        public struct Story { public string id, kind, head, body; }
        public static readonly Story[] Stories =
        {
            new Story { id = "first_run", kind = "us", head = Loc.T("폐업 청소업체, 새 주인 찾아"), body = Loc.T("궤도 청소부가 문을 닫은 지 석 달. 녹슨 청소선 한 척과 두툼한 할부 계약서가 새 주인에게 넘어갔다. 채권자 칸에는 낯익은 이름이 찍혀 있다 — 케슬러 금융. 새 사장은 「일단 연료비부터」라고만 말했다.") },
            new Story { id = "bill1", kind = "us", head = Loc.T("중고 드론 두 대, 청소선에 실려"), body = Loc.T("「줍는 건 드론, 돈 버는 건 사람」 — 드론을 판 중고상의 말이다. 드론은 한 방에 부서지는 작은 조각만 줍는다. 단단한 건 여전히 집게 몫이다.") },
            new Story { id = "run6", kind = "world", head = Loc.T("저궤도 쓰레기, 작년보다 40% 늘어"), body = Loc.T("우주청은 원인을 「불명」이라고 밝혔다. 한편 올해 발사 횟수 1위는 케슬러 발사로, 2위와의 차이는 세 배가 넘는다. 케슬러 발사 측은 「우연의 일치」라고 답했다.") },
            new Story { id = "bill2", kind = "us", head = Loc.T("중력 폭탄, 민간 판매 첫 허가"), body = Loc.T("잔해를 한데 빨아들였다 터뜨리는 중력 폭탄이 청소업체에 처음 팔렸다. 누르고 있으면 모이고, 놓으면 터진다. 제조사는 이미 폐업했고, 남은 재고는 케슬러 금융의 담보 창고에서 나왔다.") },
            new Story { id = "bill3", kind = "us", head = Loc.T("중궤도 청소 허가 — 폭발 탱크 주의보"), body = Loc.T("중궤도에는 옛 연료 탱크 수천 개가 그대로 떠 있다. 하나가 터지면 옆의 것도 터진다. 청소업계는 「조심하라」고 했지만, 몇몇 조종사는 「그게 좋다」고 했다.") },
            new Story { id = "overdue1", kind = "us", head = Loc.T("케슬러 금융, 연체 업체에 추심선 파견"), body = Loc.T("케슬러 금융은 기한을 넘긴 청소업체 궤도에 추심선을 보낸다고 밝혔다. 「압류 딱지가 붙은 잔해는 채권자 소유」라는 설명이다. 업계에서는 「딱지 붙은 걸 부수면 빚이 준다」는 말이 돈다.") },
            new Story { id = "bankrupt1", kind = "us", head = Loc.T("궤도 청소부 (1대) 파산… 청소선은 경매로"), body = Loc.T("경매에 나온 청소선의 낙찰자는 케슬러 금융. 같은 배는 다음 날 아침 다시 할부 상품으로 올라왔다. 이름도 그대로다.") },
            new Story { id = "company2", kind = "us", head = Loc.T("망한 이름 그대로, 다시 문 열어"), body = Loc.T("「이번엔 다르다.」 주식회사 궤도 청소부 (2대)가 같은 배, 같은 빚, 조금 더 능숙한 조종사로 다시 출범했다.") },
            new Story { id = "bill4", kind = "us", head = Loc.T("청소선 보험료 두 배로 — 보험사는 케슬러 보험"), body = Loc.T("큰 잔해를 건드리려면 보험이 필수다. 업계 유일의 청소선 보험사는 케슬러 보험. 보험료는 올해만 두 번 올랐다.") },
            new Story { id = "bill5", kind = "world", head = Loc.T("청소선 조종사, 인기 직업 7위"), body = Loc.T("「빚만 없으면 1위」라는 댓글이 가장 많은 추천을 받았다.") },
            new Story { id = "bill6", kind = "us", head = Loc.T("정지궤도 금고 위성, 주인은 누구?"), body = Loc.T("정지궤도를 가득 메운 금빛 위성들. 등록부에는 소유자 대신 담보 번호만 적혀 있다.") },
            new Story { id = "bankrupt2", kind = "us", head = Loc.T("궤도 청소부 (2대)도 파산 — 「이 회사는 망해도 온다」"), body = Loc.T("업계 반응은 엇갈린다. 「미련하다」와 「무섭다」. 케슬러 금융은 논평을 거부했다.") },
            new Story { id = "bill7", kind = "us", head = Loc.T("케슬러 금융, 할부 금리 인상 — 「한 업체 때문」"), body = Loc.T("케슬러 금융은 업체 이름을 밝히지 않았다. 다만 「망해도 다시 오는 회사가 있다」고 덧붙였다.") },
            new Story { id = "bill8", kind = "us", head = Loc.T("청소선 할부 완납 — 케슬러 금융 창사 이래 처음"), body = Loc.T("마지막 할부금이 들어왔다. 청소선은 이제 조종사의 것이다. 케슬러 금융은 「확인 중」이라고만 답했다.") },
            new Story { id = "clean", kind = "extra", head = Loc.T("궤도 청소율 100%"), body = Loc.T("지구 둘레에 쓰레기가 하나도 없다. 케슬러 발사는 이번 분기 발사 계획이 없다고 밝혔고, 케슬러 금융은 청소선 할부 사업 철수를 검토한다.") },
            new Story { id = "s1", kind = "scoop", head = Loc.T("추심선, 궤도에 잔해 버리는 모습 포착"), body = Loc.T("한 청소선의 블랙박스에 찍힌 영상이다. 붉은 추심선이 중궤도를 지나며 폐연료 탱크 수십 개를 떨어뜨린다. 버린 자리는 다음 날 「청소 구역」으로 지정됐다.") },
            new Story { id = "s2", kind = "scoop", head = Loc.T("케슬러 발사 · 금융 · 보험, 등기 주소가 같다"), body = Loc.T("세 회사는 같은 건물 12층, 13층, 14층을 쓴다. 엘리베이터는 하나다.") },
            new Story { id = "s3", kind = "scoop", head = Loc.T("궤도 청소부 (0대) 사장 인터뷰"), body = Loc.T("「할부는 끝까지 갚아라. 그래야 배가 네 것이 된다. 나는 못 했다.」 — 첫 번째 궤도 청소부 사장이 처음으로 입을 열었다.") },
            new Story { id = "s4", kind = "scoop", head = Loc.T("금고 위성 속은 비어 있었다 — 담보 서류만"), body = Loc.T("금고 위성 수백 기가 대출 담보로 궤도에 「보관」 중이다. 부서진 금고 안에서 나온 건 금이 아니라 서류 뭉치였다.") },
            new Story { id = "s5", kind = "scoop", head = Loc.T("내부 문서: 「쓰레기 1% 늘 때 대출 3% 는다」"), body = Loc.T("케슬러 그룹 전략 회의록이 새어 나왔다. 궤도가 더러울수록 청소선이 팔리고, 청소선이 팔릴수록 대출이 나간다.") },
            new Story { id = "s6", kind = "scoop", head = Loc.T("추심선 선장 익명 인터뷰 — 「우린 치우러 가는 게 아니다」"), body = Loc.T("「딱지를 붙이고, 가끔은 떨어뜨리고 온다. 회사가 그러라고 한다.」 그는 이번 달을 끝으로 그만둔다고 했다.") },
        };
        public static readonly string[] World =
        {
            Loc.T("우주 정거장 화장실 고장 3주째"), Loc.T("달 기지 김치 반입 허가"), Loc.T("궤도 위 고양이 사진 위성, 알고 보니 쓰레기"), Loc.T("화성 이주 신청자, 올해도 0명"),
            Loc.T("인공위성 이름 짓기 대회 1등: 「위성이」"), Loc.T("청소선 뒤 따라다니는 드론 떼, 관광 상품으로"), Loc.T("우주청, 「쓰레기 줍기 캠페인」 포스터 공개 — 인쇄는 케슬러 인쇄"),
            Loc.T("위성 인터넷 끊김, 원인은 조각 하나"), Loc.T("지구 사진 대회 대상: 「쓰레기 없는 부분만 찍었다」"), Loc.T("우주 택배 파업 이틀째"),
        };

        public void AddNews(string id, string head = null, string body = null)
        {
            if (id != null)
            {
                if (M.flags.Contains("n:" + id)) return;
                M.flags.Add("n:" + id);
                foreach (var st in Stories)
                    if (st.id == id) { head = st.head; body = st.body; Push(id, st.kind, head, body); return; }
                return;
            }
            if (head != null) Push("rec", "us", head, body ?? "");
        }

        void Push(string id, string kind, string head, string body)
        {
            M.news.Add(new NewsItem { id = id, kind = kind, head = head, body = body, run = S != null ? S.runs : 0, company = M.company });
            Emit(SwEv.News, 0, 0, 0, kind == "scoop" ? 1 : 0, head);
        }

        void Record(string flag, string head, string body)
        {
            if (M.flags.Contains(flag)) return;
            M.flags.Add(flag);
            Push("rec", "us", head, body);
        }

    }
}
