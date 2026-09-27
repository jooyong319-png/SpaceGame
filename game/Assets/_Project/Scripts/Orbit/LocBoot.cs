using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🌐 켤 때 언어를 정한다 — 장면을 불러오기 전에 (트리 칸 이름 같은 정적 표가 만들어지기 전에).
    /// 저장된 설정(orbit.lang: 0 한국어 · 1 English)이 없으면 윈도우 언어를 따른다 (한국어가 아니면 영어).
    /// </summary>
    public static class LocBoot
    {
        public const string Key = "orbit.lang";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            int lang = PlayerPrefs.GetInt(Key, Application.systemLanguage == SystemLanguage.Korean ? 0 : 1);
            Loc.En = lang == 1;
            Loc.Source = () => { var t = Resources.Load<TextAsset>("loc_en"); return t != null ? t.text : null; };
            Loc.Reset();
        }
        public static int Saved => PlayerPrefs.GetInt(Key, Loc.En ? 1 : 0);
        public static void Save(int lang) { PlayerPrefs.SetInt(Key, lang); PlayerPrefs.Save(); }
    }
}
