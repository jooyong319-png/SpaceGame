using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SalvageRun.Orbit.Sim
{
    /// <summary>
    /// 🌐 언어 (09-27 사장님 「언어 영어 해놔봐」 — 스팀 출시 준비).
    /// 코드 안 한국어 글은 그대로 두고 Loc.T("한국어") 로 감싼다 — 영어면 번역표(Resources/loc_en.txt)에서 찾아 바꾸고, 없으면 한국어 그대로.
    /// 🔁 09-28 사장님 「왜 껐다 켜야 함?」 — 이제 게임 도중에 바꿔도 바로 바뀐다:
    ///    켤 때 정적 표(트리 칸 · 부품 · 행성 …)를 늘 한국어로 먼저 만들고, 그 안의 글자 자리를 기억해 둔다(Track).
    ///    언어를 바꾸면(SetLang) 기억해 둔 자리의 글자만 영어 ↔ 한국어로 갈아 끼운다. 봇은 늘 한국어.
    /// </summary>
    public static class Loc
    {
        public static bool En;                                              // 지금 언어 (LocBoot 이 켤 때 정한다)
        public static Func<string> Source;                                  // 번역표 읽기 (유니티가 넣는다 — 봇에선 없음)
        static Dictionary<string, string> map, back;                        // 한 → 영 · 영 → 한 (저장된 영어 기사를 한국어로 돌려 보일 때)

        public static string T(string ko)
        {
            if (string.IsNullOrEmpty(ko)) return ko;
            if (map == null) Load();
            if (En) return map.TryGetValue(ko, out var e) ? e : ko;
            return back.Count > 0 && back.TryGetValue(ko, out var k) ? k : ko;   // 영어일 때 저장된 글(기사 제목 …)도 한국어로
        }

        static void Load()
        {
            map = new Dictionary<string, string>(); back = new Dictionary<string, string>();
            var src = Source != null ? Source() : null;
            if (string.IsNullOrEmpty(src)) return;
            foreach (var raw in src.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t'); if (tab <= 0) continue;
                string ko = Unesc(line.Substring(0, tab)), en = Unesc(line.Substring(tab + 1));
                if (en.Length == 0) continue;
                map[ko] = en;
                if (en.Trim().Length > 1 && !back.ContainsKey(en)) back[en] = ko;   // 같은 영어가 여럿이면 먼저 나온 한국어 · 공백만인 번역은 거꾸로 안 찾는다
            }
        }
        static string Unesc(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");
        public static void Reset() { map = null; back = null; slots = null; }

        // ───────────────────────── 🔁 도중에 언어 바꾸기
        // 자리 하나 = 「어느 그릇의 어디에 있는 한국어 글」. 그릇은 정적 필드 · 배열 칸 · 객체 필드 · 사전 값.
        class Slot { public Action<object> set; public string ko; }
        static List<Slot> slots;
        static Type[] tracked = new Type[0];

        /// <summary>켤 때 한 번 — 표들이 한국어로 만들어진 뒤에 부른다. 번역표에 있는 한국어 글 자리만 기억한다.</summary>
        public static void Track(params Type[] types)
        {
            if (map == null) Load();
            tracked = types; slots = new List<Slot>();
            var seen = new HashSet<object>(new RefEq());
            foreach (var t in types)
                foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.IsLiteral || !f.IsInitOnly) continue;             // const · 바뀌는 상태는 빼고 — readonly 표만
                    var fi = f;
                    Walk(f.FieldType, f.GetValue(null), v => fi.SetValue(null, v), seen, 0);
                }
        }

        /// <summary>언어를 바꾼다 — 기억해 둔 자리를 모두 갈아 끼운다. 그 뒤에 새로 만드는 글은 Loc.T 가 알아서.</summary>
        public static void SetLang(bool en)
        {
            En = en;
            if (map == null) Load();
            if (slots == null) { Changed?.Invoke(); return; }
            foreach (var s in slots) s.set(en && map.TryGetValue(s.ko, out var e) ? e : s.ko);
            Changed?.Invoke();
        }
        /// <summary>언어가 바뀐 뒤 — 표에서 계산해 지은 글(부품 설명 …)을 다시 짓는 곳이 붙는다</summary>
        public static event Action Changed;

        /// <summary>시험용 — 기억한 자리 수 · 번역 안 되는 한국어가 남은 곳 (영어일 때 표 안에 한글이 보이면 여기 나온다)</summary>
        public static int TrackedCount => slots != null ? slots.Count : 0;
        public static bool Tracked => slots != null;
        public static List<string> LeftKorean()
        {
            var left = new List<string>(); var seen = new HashSet<object>(new RefEq());
            foreach (var t in tracked)
                foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    if (!f.IsLiteral && f.IsInitOnly) Scan(t.Name + "." + f.Name, f.GetValue(null), left, seen, 0);
            return left;
        }

        static bool Hangul(string s) { foreach (char c in s) if (c >= '가' && c <= '힣') return true; return false; }

        static void Walk(Type ft, object v, Action<object> set, HashSet<object> seen, int depth)
        {
            if (v == null || depth > 4) return;
            if (v is string s)
            {
                if (map.ContainsKey(s)) slots.Add(new Slot { set = set, ko = s });
                return;
            }
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum || v is Delegate) return;
            if (t.Namespace != null && t.Namespace.StartsWith("UnityEngine")) return;   // 그림 · 색 · 좌표
            if (!t.IsValueType && !seen.Add(v)) return;
            if (v is string[] sa)
            {
                for (int i = 0; i < sa.Length; i++) { int ii = i; var arr = sa; if (sa[i] != null && map.ContainsKey(sa[i])) slots.Add(new Slot { set = x => arr[ii] = (string)x, ko = sa[i] }); }
                return;
            }
            if (v is Array a)
            {
                var et = t.GetElementType();
                if (et.IsPrimitive || et.IsEnum) return;
                for (int i = 0; i < a.Length; i++)
                {
                    int ii = i; var arr = a;
                    if (et.IsValueType) WalkStruct(a.GetValue(i), boxed => arr.SetValue(boxed, ii), seen, depth + 1);
                    else Walk(et, a.GetValue(i), x => arr.SetValue(x, ii), seen, depth + 1);
                }
                return;
            }
            if (v is IDictionary d)
            {
                var keys = new List<object>(); foreach (var k in d.Keys) keys.Add(k);
                foreach (var k in keys) { var kk = k; var dd = d; Walk(null, d[k], x => dd[kk] = x, seen, depth + 1); }
                return;
            }
            if (v is IList l)
            {
                for (int i = 0; i < l.Count; i++) { int ii = i; var ll = l; Walk(null, l[i], x => ll[ii] = x, seen, depth + 1); }
                return;
            }
            if (t.IsValueType) { WalkStruct(v, set, seen, depth); return; }
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var fi = f; var obj = v;
                Walk(f.FieldType, f.GetValue(v), x => fi.SetValue(obj, x), seen, depth + 1);
            }
        }
        static void WalkStruct(object boxed, Action<object> putBack, HashSet<object> seen, int depth)
        {   // 구조체는 복사본이라 — 고친 뒤 제자리에 다시 넣는다
            if (boxed == null || putBack == null) return;
            var t = boxed.GetType(); if (t.IsPrimitive || t.IsEnum || t.Namespace != null && t.Namespace.StartsWith("UnityEngine")) return;
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var fi = f;
                if (f.FieldType == typeof(string))
                {
                    var s = (string)f.GetValue(boxed);
                    if (s != null && map.ContainsKey(s)) slots.Add(new Slot { set = x => { fi.SetValue(boxed, x); putBack(boxed); }, ko = s });
                }
                else if (!f.FieldType.IsValueType) Walk(f.FieldType, f.GetValue(boxed), x => { fi.SetValue(boxed, x); putBack(boxed); }, seen, depth + 1);
            }
        }
        static void Scan(string path, object v, List<string> left, HashSet<object> seen, int depth)
        {
            if (v == null || depth > 4) return;
            if (v is string s) { if (Hangul(s)) left.Add(path + " = " + (s.Length > 40 ? s.Substring(0, 40) : s)); return; }
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum || v is Delegate || t.Namespace != null && t.Namespace.StartsWith("UnityEngine")) return;
            if (!t.IsValueType && !seen.Add(v)) return;
            if (v is IDictionary d) { foreach (DictionaryEntry e in d) Scan(path + "[" + e.Key + "]", e.Value, left, seen, depth + 1); return; }
            if (v is IEnumerable en) { int i = 0; foreach (var x in en) Scan(path + "[" + i++ + "]", x, left, seen, depth + 1); return; }
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) Scan(path + "." + f.Name, f.GetValue(v), left, seen, depth + 1);
        }
        class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        static readonly string[] EnUnits = { "", "K", "M", "B", "T", "Qa", "Qi" };
        static readonly System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;
        /// <summary>영어 숫자 — 1000 아래는 그대로 · 위는 유효 숫자 셋 (12.4K · 380M · 1.2T)</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + Num(-v);
            if (v < 1000) return ((long)Math.Floor(v)).ToString(IC);
            int u = 0; while (v >= 1000 && u < EnUnits.Length - 1) { v /= 1000; u++; }
            string n = v >= 100 ? Math.Floor(v).ToString("0", IC) : v >= 10 ? (Math.Floor(v * 10) / 10).ToString("0.#", IC) : (Math.Floor(v * 100) / 100).ToString("0.##", IC);
            return n + EnUnits[u];
        }
    }
}
