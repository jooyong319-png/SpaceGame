using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🌐 켤 때 언어를 정한다 — 장면을 불러오기 전에.
    /// 저장된 설정(orbit.lang: 0 한국어 · 1 English)이 없으면 윈도우 언어를 따른다 (한국어가 아니면 영어).
    /// 🔁 09-28: 정적 표(트리 칸 이름 …)를 먼저 한국어로 만들고 글자 자리를 기억한 뒤(Loc.Track) 언어를 입힌다 —
    ///    그래서 설정에서 바꾸면 다시 켜지 않아도 바로 바뀐다(Apply).
    /// </summary>
    public static class LocBoot
    {
        public const string Key = "orbit.lang";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            int lang = PlayerPrefs.GetInt(Key, Application.systemLanguage == SystemLanguage.Korean ? 0 : 1);
            Loc.En = false;                                                  // 표는 한국어로 먼저
            Loc.Source = () => { var t = Resources.Load<TextAsset>("loc_en"); return t != null ? t.text : null; };
            Loc.Reset();
            var types = new[] { typeof(SweepSim), typeof(Parts), typeof(Market), typeof(SweepHud) };
            foreach (var t in types) System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            Loc.Track(types);
            Loc.SetLang(lang == 1);
        }
        public static int Saved => PlayerPrefs.GetInt(Key, Loc.En ? 1 : 0);
        public static void Save(int lang) { PlayerPrefs.SetInt(Key, lang); PlayerPrefs.Save(); }
        /// <summary>설정에서 고르면 — 저장하고 바로 바꾼다</summary>
        public static void Apply(int lang) { Save(lang); Loc.SetLang(lang == 1); }
    }
}
