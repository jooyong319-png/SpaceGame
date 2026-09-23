using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    /// <summary>
    /// 📈 궤도 증권 — 실시간으로 움직이는 종목 여덟 (09-23 사장님 「진짜 주식처럼 · 종목도 좀 있고 · 뉴스에 따라 움직이게」).
    /// 봉 하나 = 6초. 판 중이든 조종실이든 늘 돈다. 게임 규칙의 난수와 따로 굴러서 봇의 속도 계산을 흔들지 않는다.
    /// 🗞 증권 속보가 먼저 뜨고, 주가는 뒤따라 몇 봉에 걸쳐 움직인다 (소문은 가끔 틀린다). 내 게임의 일(행성 허가 · 파산 …)도 속보가 된다.
    /// 사고팔 땐 금액으로 — 주는 소수로 쌓인다.
    /// </summary>
    [Serializable]
    public class StockCandle { public float o, h, l, c; }

    [Serializable]
    public class StockState
    {
        public double price = 100, shares, cost;                          // cost = 들고 있는 주식에 들인 돈 (평균 매수가 = cost / shares)
        public float co, ch, cl;                                          // 서고 있는 봉
        public float push; public int pushLeft;                           // 뉴스 여파 — 봉마다 더해지는 기울기 · 남은 봉
        public List<StockCandle> hist = new List<StockCandle>();
    }

    [Serializable]
    public class MarketNews { public string head, body; public int dir; public bool rumor; public float t; }

    [Serializable]
    public class MarketState
    {
        public List<StockState> st = new List<StockState>();
        public List<MarketNews> news = new List<MarketNews>();            // 최근 속보 (창에 보인다)
        public float candleT, newsT = 20, clock;
        public int seed = 1, sel;
        public float takeProfit = 0.2f, stopLoss = 0.15f;                 // 자동 매도 기준 (칸을 사야 쓴다)
        public int nextNews = -1; public float nextNewsT;                 // 다음 속보 (내부자 정보가 미리 본다)
    }

    public struct StockDef { public string id, name, sector, desc; public double mu, vol, div; }
    public struct NewsDef { public string head, body; public string[] up, down; public float size; public bool rumor; }

    public class Market
    {
        public const float CandleSec = 6f;
        public const int Keep = 60;
        public static readonly StockDef[] Defs =
        {
            new StockDef { id = "kes",   name = "케슬러 금융",   sector = "금융", desc = "안정 · 배당",     mu = 0.0010, vol = 0.016, div = 0.0012 },
            new StockDef { id = "ins",   name = "궤도 보험",     sector = "금융", desc = "안정",            mu = 0.0010, vol = 0.022, div = 0.0006 },
            new StockDef { id = "fuel",  name = "지구 연료공사", sector = "연료", desc = "경기를 탄다",     mu = 0.0012, vol = 0.032, div = 0 },
            new StockDef { id = "ship",  name = "청소선 조선",   sector = "조선", desc = "성장주",          mu = 0.0020, vol = 0.042, div = 0 },
            new StockDef { id = "moon",  name = "달 기지 조합",  sector = "달",   desc = "무난",            mu = 0.0015, vol = 0.034, div = 0 },
            new StockDef { id = "mars",  name = "화성 제련소",   sector = "화성", desc = "요동친다",        mu = 0.0018, vol = 0.055, div = 0 },
            new StockDef { id = "jup",   name = "목성 선박",     sector = "목성", desc = "대박 아니면 쪽박", mu = 0.0008, vol = 0.085, div = 0 },
            new StockDef { id = "sat",   name = "토성 고리 관광", sector = "관광", desc = "테마주",          mu = 0.0012, vol = 0.07,  div = 0 },
        };
        // 🗞 증권 속보 — up / down 은 종목 id 또는 업종 이름. size = 몇 봉에 걸쳐 오를 · 내릴 총 크기
        public static readonly NewsDef[] NewsBook =
        {
            new NewsDef { head = "화성 모래 폭풍 장기화 — 제련 설비 멈춰", body = "화성 적도 일대의 모래 폭풍이 한 달째. 제련소 굴뚝이 멈췄다.", down = new[] { "mars" }, size = 0.22f },
            new NewsDef { head = "화성 제련소, 신형 용광로 가동", body = "고철을 두 배 빠르게 녹이는 용광로가 불을 붙였다.", up = new[] { "mars" }, size = 0.2f },
            new NewsDef { head = "달 기지 확장 승인", body = "달 뒷면 기지 확장 계획이 의회를 통과했다.", up = new[] { "moon", "ship" }, size = 0.16f },
            new NewsDef { head = "달 기지 누수 사고", body = "달 기지 3구역에서 공기가 샜다. 다친 사람은 없다.", down = new[] { "moon" }, size = 0.15f },
            new NewsDef { head = "목성 폭풍 속 선박 실종", body = "목성 대적점 근처에서 화물선 두 척과 연락이 끊겼다.", down = new[] { "jup", "보험" , "ins" }, size = 0.3f },
            new NewsDef { head = "목성 선박, 초대형 계약 따내", body = "목성 수소 운송 10년 계약. 역대 최대 규모다.", up = new[] { "jup" }, size = 0.35f },
            new NewsDef { head = "토성 고리 관광 예약 폭주", body = "고리 사이를 나는 관광선 표가 석 달 치 매진됐다.", up = new[] { "sat" }, size = 0.28f },
            new NewsDef { head = "토성 관광선 충돌 — 운항 중단", body = "고리 파편에 관광선이 긁혔다. 당분간 운항을 쉰다.", down = new[] { "sat", "ins" }, size = 0.3f },
            new NewsDef { head = "지구 연료값 급등", body = "궤도 연료 값이 한 주 만에 40% 뛰었다.", up = new[] { "fuel" }, down = new[] { "ship", "sat" }, size = 0.16f },
            new NewsDef { head = "연료 과잉 생산 — 값 폭락", body = "정유 궤도 공장이 너무 많이 만들었다.", down = new[] { "fuel" }, up = new[] { "ship" }, size = 0.14f },
            new NewsDef { head = "케슬러 금융, 금리 인상", body = "빌려준 돈의 이자를 올렸다. 청소 업계는 한숨.", up = new[] { "kes" }, down = new[] { "ship" }, size = 0.1f },
            new NewsDef { head = "케슬러 금융 회계 의혹", body = "장부에 이상한 구멍이 있다는 내부 제보가 나왔다.", down = new[] { "kes", "ins" }, size = 0.14f },
            new NewsDef { head = "청소선 조선, 수주 잔고 사상 최대", body = "주문이 밀려 3년 치 일감이 쌓였다.", up = new[] { "ship" }, size = 0.2f },
            new NewsDef { head = "궤도 파편 경보 — 보험 청구 급증", body = "충돌 사고가 잇따르며 보험금 청구가 쏟아진다.", down = new[] { "ins" }, up = new[] { "ship" }, size = 0.14f },
            new NewsDef { head = "[소문] 화성 금맥 발견?", body = "확인되지 않은 소문. 화성 제련소가 금맥을 찾았다는 이야기가 돈다.", up = new[] { "mars" }, size = 0.3f, rumor = true },
            new NewsDef { head = "[소문] 목성 선박 부도 임박?", body = "확인되지 않은 소문. 목성 선박이 돈이 말랐다는 말이 돈다.", down = new[] { "jup" }, size = 0.35f, rumor = true },
            new NewsDef { head = "[소문] 달 기지 새 광산?", body = "확인되지 않은 소문. 달 뒷면에서 새 광맥이 나왔다고 한다.", up = new[] { "moon" }, size = 0.25f, rumor = true },
            new NewsDef { head = "[소문] 토성 관광 정부 지원금?", body = "확인되지 않은 소문. 관광 업계에 지원금이 풀린다는 말.", up = new[] { "sat" }, size = 0.3f, rumor = true },
        };

        public MarketState M;
        readonly Random rng;

        public Market(MarketState m)
        {
            M = m ?? new MarketState();
            if (M.seed == 0) M.seed = 1;
            rng = new Random(M.seed * 7919 + (int)M.clock);
            if (M.st.Count != Defs.Length)
            {
                M.st.Clear();
                for (int i = 0; i < Defs.Length; i++)
                {
                    var s = new StockState { price = 40 + rng.NextDouble() * 160 };
                    // 지난 봉 40개를 미리 — 처음 열어도 차트가 있다
                    for (int k = 0; k < 40; k++) { StartCandle(s); for (int j = 0; j < 12; j++) Wiggle(i, s, CandleSec / 12); Close(s); }
                    M.st.Add(s);
                }
            }
            foreach (var s in M.st) if (s.co <= 0) StartCandle(s);
            if (M.nextNews < 0) RollNews();
        }

        double N() { double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble(); return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); }
        static void StartCandle(StockState s) { s.co = s.ch = s.cl = (float)s.price; }

        void Wiggle(int i, StockState s, double dt)
        {
            var d = Defs[i]; double k = dt / CandleSec;
            double drift = d.mu + (s.pushLeft > 0 ? s.push : 0);
            s.price *= Math.Exp(drift * k - 0.5 * d.vol * d.vol * k + d.vol * Math.Sqrt(k) * N());
            s.price = Math.Max(0.5, s.price);
            s.ch = Math.Max(s.ch, (float)s.price); s.cl = Math.Min(s.cl, (float)s.price);
        }

        static void Close(StockState s)
        {
            s.hist.Add(new StockCandle { o = s.co, h = s.ch, l = s.cl, c = (float)s.price });
            if (s.hist.Count > Keep) s.hist.RemoveAt(0);
            if (s.pushLeft > 0) s.pushLeft--;
            StartCandle(s);
        }

        void RollNews() { M.nextNews = rng.Next(NewsBook.Length); M.nextNewsT = 30 + (float)rng.NextDouble() * 30; M.newsT = 0; }

        bool Hits(int i, string[] keys) { if (keys == null) return false; foreach (var k in keys) if (k == Defs[i].id || k == Defs[i].sector) return true; return false; }

        /// <summary>속보를 낸다 — 주가는 뒤따라 3~5봉에 걸쳐 움직인다. 소문은 40% 로 틀린다 (반대로 움직인다)</summary>
        public void Publish(string head, string body, string[] up, string[] down, float size, bool rumor)
        {
            bool wrong = rumor && rng.NextDouble() < 0.4;
            int n = 3 + rng.Next(3);
            for (int i = 0; i < M.st.Count; i++)
            {
                int dir = Hits(i, up) ? 1 : Hits(i, down) ? -1 : 0;
                if (dir == 0) continue;
                if (wrong) dir = -dir;
                var s = M.st[i];
                s.push = dir * size / n * (0.7f + (float)rng.NextDouble() * 0.6f); s.pushLeft = n;
            }
            M.news.Add(new MarketNews { head = head, body = body, dir = up != null ? 1 : -1, rumor = rumor, t = M.clock });
            if (M.news.Count > 8) M.news.RemoveAt(0);
            OnNews?.Invoke(head, body);
        }
        public event Action<string, string> OnNews;

        /// <summary>내 게임에서 일어난 일 → 속보 (행성 허가 · 파산 · 큰 연쇄 …)</summary>
        public void GameEvent(string head, string body, string[] up, string[] down, float size) => Publish(head, body, up, down, size, false);

        /// <summary>시간이 흐른다. 돌려주는 값 = 배당 · 자동 매도로 들어온 돈</summary>
        public double Update(double dt, bool tp, bool sl, double fee, double divBonus)
        {
            double got = 0;
            M.clock += (float)dt; M.candleT += (float)dt; M.newsT += (float)dt;
            for (int i = 0; i < M.st.Count; i++) Wiggle(i, M.st[i], dt);
            if (M.newsT >= M.nextNewsT) { var nd = NewsBook[M.nextNews]; Publish(nd.head, nd.body, nd.up, nd.down, nd.size, nd.rumor); RollNews(); }
            if (M.candleT >= CandleSec)
            {
                M.candleT -= CandleSec;
                for (int i = 0; i < M.st.Count; i++)
                {
                    var s = M.st[i];
                    double div = Defs[i].div + (s.shares > 0 ? divBonus : 0);
                    if (s.shares > 0 && div > 0) got += s.shares * s.price * div;   // 배당 — 들고 있으면 봉마다
                    Close(s);
                }
            }
            for (int i = 0; i < M.st.Count; i++)
            {
                var s = M.st[i];
                if (s.shares <= 0) continue;
                double ch = s.price / (s.cost / s.shares) - 1;
                if ((tp && ch >= M.takeProfit) || (sl && ch <= -M.stopLoss)) got += Sell(i, 1, fee);
            }
            return got;
        }

        /// <summary>내부자 정보 — 다음 속보와 남은 시간 (단계가 낮으면 제목만 흐릿하게)</summary>
        public NewsDef NextNews => NewsBook[Math.Max(0, M.nextNews)];
        public float NextNewsIn => M.nextNewsT - M.newsT;

        public double Buy(int i, double money, double fee)
        {
            if (money <= 0) return 0;
            var s = M.st[i];
            s.shares += money * (1 - fee) / s.price; s.cost += money;
            return money;
        }
        public double Sell(int i, double frac, double fee)
        {
            var s = M.st[i];
            if (s.shares <= 0) return 0;
            frac = Math.Min(1, Math.Max(0, frac));
            double sh = s.shares * frac, got = sh * s.price * (1 - fee);
            s.cost *= 1 - frac; s.shares -= sh;
            if (s.shares < 1e-9) { s.shares = 0; s.cost = 0; }
            return got;
        }
        public double Value(int i) => M.st[i].shares * M.st[i].price;
        public double TotalValue() { double v = 0; for (int i = 0; i < M.st.Count; i++) v += Value(i); return v; }
        public double Change(int i, int candles)
        {
            var s = M.st[i]; if (s.hist.Count == 0) return 0;
            var b = s.hist[Math.Max(0, s.hist.Count - candles)].o;
            return s.price / b - 1;
        }
    }
}
