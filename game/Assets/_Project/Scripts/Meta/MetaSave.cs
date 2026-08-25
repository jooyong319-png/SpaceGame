using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SalvageRun.Data;

namespace SalvageRun.Meta
{
    /// <summary>
    /// 영구 층. 크레딧과 해금한 테크 노드가 전부다.
    ///
    /// ⚠️ 2026-08-19 설계 변경: 자동 귀환은 시간만 손해이므로 **런에서 잃는 것이 없다.**
    ///    그래서 게임의 진행은 전적으로 테크트리(플레이타임)에 달려 있다.
    ///    자세한 근거는 docs/content-design.md.
    ///
    /// PlayerPrefs를 쓰지 않는 이유: 구조체 저장이 안 돼서 항목이 늘면 갈아엎어야 한다.
    /// </summary>
    [Serializable]
    public class NodeRank
    {
        public string id;
        public int rank;
    }

    [Serializable]
    public class MetaData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;   // ⚠️ 나중에 넣으면 마이그레이션 경로가 없다
        public int credits;
        public int runsCompleted;
        public int bestRunValue;
        public int totalCollected;
        /// <summary>최고 도달 층(1부터).</summary>
        public int bestDepth;

        /// <summary>🔴 해금된 맵 수. 1이면 첫 맵만 열려 있다.</summary>
        public int unlockedMaps = 1;

        /// <summary>구 버전(v2)의 해금 목록. v3에서 <see cref="nodes"/>로 옮겨간다.</summary>
        public List<string> unlockedNodes = new List<string>();

        // ---- 우주선 ----
        /// <summary>해금한 우주선 id. 첫 배는 항상 열려 있으므로 여기 없어도 된다.</summary>
        public List<string> unlockedShips = new List<string>();

        /// <summary>지금 고른 배. 비어 있으면 첫 배.</summary>
        public string selectedShip = "";

        /// <summary>
        /// 🔴 지금 들고 시작할 무기 (`WeaponKind`의 정수값). -1이면 아직 안 골랐다.
        ///    2026-08-23부터 무기는 **테크트리 노드**로 열고 고른다.
        ///    ⚠️ 이름이 아니라 정수로 저장하므로, `WeaponKind`의 순서를 바꾸면
        ///       저장된 값이 다른 무기를 가리킨다. 순서는 함부로 안 바꾼다.
        /// </summary>
        public int selectedWeapon = -1;

        // ---- 영구 재화 ----
        //
        // 🔴 **배열이 아니라 개별 필드로 둔다** (2026-08-26에 3종 → 6종으로 늘리며 확인).
        //    배열로 바꾸면 이미 저장된 파일을 옮겨 심어야 하는데, 그 마이그레이션이
        //    조용히 틀리면 **사장님 재화가 사라진다.** 필드를 늘리는 쪽은
        //    JsonUtility가 없는 필드를 0으로 두므로 옛 저장이 그대로 열린다.
        public int scrap;
        public int circuit;
        public int core;
        public int alloy;
        public int crystal;
        public int isotope;

        /// <summary>
        /// 🔴 테크 노드는 **여러 번 찍을 수 있으므로** 목록이 아니라 랭크를 저장한다.
        ///    JsonUtility가 Dictionary를 못 다뤄서 리스트로 둔다.
        /// </summary>
        public List<NodeRank> nodes = new List<NodeRank>();

        public int RankOf(string id)
        {
            if (nodes == null) return 0;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == id) return nodes[i].rank;
            return 0;
        }

        public void SetRank(string id, int rank)
        {
            if (nodes == null) nodes = new List<NodeRank>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == id) { nodes[i].rank = rank; return; }
            nodes.Add(new NodeRank { id = id, rank = rank });
        }

        public bool HasNode(string id) => RankOf(id) > 0;

        public bool HasShip(string id)
            => !string.IsNullOrEmpty(id) && unlockedShips != null && unlockedShips.Contains(id);

        public int Mat(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return scrap;
                case MatKind.Circuit: return circuit;
                case MatKind.Core:    return core;
                case MatKind.Alloy:   return alloy;
                case MatKind.Crystal: return crystal;
                case MatKind.Isotope: return isotope;
            }
            return 0;
        }

        public void AddMat(MatKind m, int n)
        {
            switch (m)
            {
                case MatKind.Scrap:   scrap += n;   break;
                case MatKind.Circuit: circuit += n; break;
                case MatKind.Core:    core += n;    break;
                case MatKind.Alloy:   alloy += n;   break;
                case MatKind.Crystal: crystal += n; break;
                case MatKind.Isotope: isotope += n; break;
            }
        }
    }

    public static class MetaSave
    {
        /// <summary>
        /// 🔴 테스트/시뮬레이션이 플레이어의 진짜 meta.json을 덮어쓰지 못하게 막는 스위치.
        ///    켜면 메모리에서만 동작한다.
        /// </summary>
        public static bool DisableWrites;

        static MetaData cached;
        static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "meta.json");

        /// <summary>
        /// 🔴 **WebGL에서는 파일이 아니라 PlayerPrefs에 넣는다.**
        ///
        ///    WebGL의 `persistentDataPath`는 브라우저 IndexedDB로 흉내 낸 것이라,
        ///    `File.WriteAllText`는 성공한 것처럼 보이지만 **flush를 안 하면 탭을 닫을 때 날아간다.**
        ///    테크트리·우주선 해금이 전부 여기 있어서, 껐다 켜면 진행이 통째로 사라진다.
        ///
        ///    PlayerPrefs는 `Save()`가 그 flush를 대신해 준다.
        ///    "PlayerPrefs는 구조체를 못 담는다"가 예전에 이걸 안 쓴 이유였는데,
        ///    어차피 JSON 문자열로 만들어 두므로 담을 것이 문자열 하나뿐이라 문제가 안 된다.
        /// </summary>
        const string PrefsKey = "salvagerun.meta";

#if UNITY_WEBGL && !UNITY_EDITOR
        const bool UsePrefs = true;
#else
        const bool UsePrefs = false;
#endif

        public static MetaData Data
        {
            get
            {
                if (cached == null) Load();
                return cached;
            }
        }

        public static void Load()
        {
            try
            {
                string json = ReadRaw();
                if (!string.IsNullOrEmpty(json))
                {
                    cached = JsonUtility.FromJson<MetaData>(json) ?? new MetaData();
                    if (cached.unlockedNodes == null) cached.unlockedNodes = new List<string>();
                    if (cached.nodes == null) cached.nodes = new List<NodeRank>();
                    if (cached.unlockedShips == null) cached.unlockedShips = new List<string>();
                    Migrate(cached);
                    return;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[MetaSave] 로드 실패, 새로 시작: {e.Message}"); }
            cached = new MetaData();
        }

        static string ReadRaw()
        {
            if (UsePrefs) return PlayerPrefs.GetString(PrefsKey, null);
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }

        /// <summary>
        /// 🔴 세이브 이주. v2의 테크 노드는 **다른 게임의 것**이라 옮기지 않고 버린다.
        ///    대신 그때까지 쓴 크레딧을 고철로 환산해 돌려준다 —
        ///    남의 진행을 말없이 지우면 그게 제일 나쁜 버그다.
        /// </summary>
        static void Migrate(MetaData d)
        {
            if (d.version >= MetaData.CurrentVersion) return;

            if (d.version <= 2)
            {
                if (d.unlockedNodes != null && d.unlockedNodes.Count > 0)
                {
                    d.scrap += d.unlockedNodes.Count * 120;   // 옛 노드값을 고철로 환산
                    d.unlockedNodes.Clear();
                }
                d.scrap += Mathf.RoundToInt(d.credits * 0.25f);
            }

            d.version = MetaData.CurrentVersion;
            Debug.Log($"[MetaSave] 세이브를 v{MetaData.CurrentVersion}로 이주했다. 고철 {d.scrap}");
        }

        // ---------------------------------------------------------------- 재화 · 노드

        // ---------------------------------------------------------------- 우주선

        /// <summary>이 배를 쓸 수 있는가. 무료 배는 해금 목록에 없어도 열려 있다.</summary>
        public static bool ShipUnlocked(ShipDef s)
            => s != null && (s.FreeFromStart || Data.HasShip(s.id));

        public static bool CanBuyShip(ShipDef s, out string why)
        {
            why = null;
            if (s == null) { why = "없는 배"; return false; }
            if (ShipUnlocked(s)) { why = "이미 보유"; return false; }

            if (Data.scrap   < s.costScrap)   { why = "고철 부족"; return false; }
            if (Data.circuit < s.costCircuit) { why = "회로 부족"; return false; }
            if (Data.core    < s.costCore)    { why = "코어 부족"; return false; }
            return true;
        }

        public static bool BuyShip(ShipDef s)
        {
            if (!CanBuyShip(s, out _)) return false;

            Data.scrap   -= s.costScrap;
            Data.circuit -= s.costCircuit;
            Data.core    -= s.costCore;

            if (Data.unlockedShips == null) Data.unlockedShips = new List<string>();
            Data.unlockedShips.Add(s.id);
            Data.selectedShip = s.id;
            Save();
            return true;
        }

        /// <summary>
        /// 🔴 **공짜이고 선행도 없는 노드는 저절로 찍힌다.**
        ///
        ///    그런 노드는 "살까 말까"가 아니다 — 누구나 즉시 누를 수 있으므로
        ///    안 누를 이유가 없고, 안 누르면 **그 아래가 통째로 잠겨 보인다.**
        ///    (뿌리와 첫 무기가 그렇다. 첫 무기를 안 찍으면 무기 가지가 다 안 보인다)
        ///
        ///    랭크를 실제로 채워 두면 `RankOf`를 보는 모든 곳이 한 가지로 답한다 —
        ///    "여긴 공짜니까 사실 열린 거야" 같은 예외를 화면마다 따로 둘 필요가 없다.
        /// </summary>
        public static void EnsureFreeNodes(GameContent content)
        {
            if (content == null || content.techTree == null) return;

            bool changed = false;
            for (int i = 0; i < content.techTree.Length; i++)
            {
                var n = content.techTree[i];
                if (n == null || !n.IsFree) continue;
                if (n.requires != null && n.requires.Length > 0) continue;
                if (Data.RankOf(n.id) > 0) continue;

                Data.SetRank(n.id, 1);
                changed = true;
            }
            if (changed) Save();
        }

        // ---------------------------------------------------------------- 무기

        /// <summary>
        /// 🔴 **무기를 골라 든다.** 이미 연 무기 노드를 다시 누르면 여기로 온다.
        ///    연 적 없는 무기는 못 고른다 — 노드를 안 찍고 저장을 손대도 안 넘어간다.
        /// </summary>
        public static void SelectWeapon(GameContent content, WeaponKind k)
        {
            if (!WeaponUnlocked(content, k)) return;
            Data.selectedWeapon = (int)k;
            Save();
        }

        /// <summary>이 무기를 여는 노드를 찍었는가.</summary>
        public static bool WeaponUnlocked(GameContent content, WeaponKind k)
        {
            if (content == null || content.techTree == null) return false;

            for (int i = 0; i < content.techTree.Length; i++)
            {
                var n = content.techTree[i];
                if (n == null || n.effect != TechEffect.UnlockWeapon) continue;
                if (n.weapon != k) continue;

                // `EnsureFreeNodes`가 공짜 노드를 미리 찍어 두므로 랭크만 보면 된다
                return Data.RankOf(n.id) > 0;
            }
            return false;
        }

        /// <summary>
        /// 🔴 지금 들고 시작할 무기.
        ///
        ///    고른 것이 없거나 **잠긴 것을 가리키고 있으면**(데이터가 바뀌었을 수 있다)
        ///    열려 있는 것 중 첫 번째로 되돌린다. 그것도 없으면 `fallback`.
        ///    ⚠️ 여기서 안 막으면 **무기 없이 판이 시작된다** — 아무것도 못 하고 3분을 본다.
        /// </summary>
        public static WeaponKind CurrentWeapon(GameContent content, WeaponKind fallback)
        {
            if (Data.selectedWeapon >= 0 && Data.selectedWeapon < Weapons.Count)
            {
                var picked = (WeaponKind)Data.selectedWeapon;
                if (WeaponUnlocked(content, picked)) return picked;
            }

            for (int i = 0; i < Weapons.Count; i++)
                if (WeaponUnlocked(content, (WeaponKind)i)) return (WeaponKind)i;

            return fallback;
        }

        public static void SelectShip(ShipDef s)
        {
            if (!ShipUnlocked(s)) return;
            Data.selectedShip = s.id;
            Save();
        }

        public static ShipDef CurrentShip(GameContent content)
        {
            if (content == null) return null;

            var s = content.ShipOrDefault(Data.selectedShip);
            // 🔴 세이브에 적힌 배가 잠겨 있으면(데이터가 바뀌었을 수 있다) 첫 배로 되돌린다
            if (s != null && !ShipUnlocked(s))
                s = content.ships != null && content.ships.Length > 0 ? content.ships[0] : null;
            return s;
        }

        public static void AddMaterial(MatKind m, int amount)
        {
            if (amount <= 0) return;
            Data.AddMat(m, amount);
        }

        /// <summary>이 노드를 지금 찍을 수 있는가. 못 찍으면 이유를 돌려준다.</summary>
        public static bool CanBuy(TechNodeDef n, GameContent content, out string why)
        {
            why = null;
            if (n == null) { why = "없는 노드"; return false; }

            int rank = Data.RankOf(n.id);
            if (rank >= n.maxRank) { why = "최대"; return false; }

            if (n.requires != null)
            {
                for (int i = 0; i < n.requires.Length; i++)
                {
                    if (string.IsNullOrEmpty(n.requires[i])) continue;
                    if (Data.RankOf(n.requires[i]) > 0) continue;

                    why = "선행 필요";
                    return false;
                }
            }

            int next = rank + 1;
            if (Data.scrap   < n.CostAt(MatKind.Scrap, next))   { why = "고철 부족"; return false; }
            if (Data.circuit < n.CostAt(MatKind.Circuit, next)) { why = "회로 부족"; return false; }
            if (Data.core    < n.CostAt(MatKind.Core, next))    { why = "코어 부족"; return false; }
            return true;
        }

        public static bool Buy(TechNodeDef n, GameContent content)
        {
            if (!CanBuy(n, content, out _)) return false;

            int next = Data.RankOf(n.id) + 1;
            Data.scrap   -= n.CostAt(MatKind.Scrap, next);
            Data.circuit -= n.CostAt(MatKind.Circuit, next);
            Data.core    -= n.CostAt(MatKind.Core, next);
            Data.SetRank(n.id, next);
            Save();
            return true;
        }

        public static void Save()
        {
            if (DisableWrites) return;
            try
            {
                string json = JsonUtility.ToJson(Data, true);

                if (UsePrefs)
                {
                    PlayerPrefs.SetString(PrefsKey, json);
                    PlayerPrefs.Save();   // 🔴 이게 IndexedDB flush다. 빼면 탭 닫을 때 날아간다
                }
                else
                {
                    File.WriteAllText(FilePath, json);
                }
            }
            catch (Exception e) { Debug.LogWarning($"[MetaSave] 저장 실패: {e.Message}"); }
        }

        // ---------------------------------------------------------------- 구역 해금

        /// <summary>
        /// 🔴 **구역은 재화로 산다** (2026-08-26 · Space Rock Breaker 방향).
        ///    보스를 잡아서 여는 방식을 버렸다 — 자세한 이유는 `StageDef`의 주석에.
        /// </summary>
        public static bool StageUnlocked(GameContent content, int index)
        {
            if (index <= 0) return true;                    // 첫 구역은 항상 열려 있다

            var st = content != null ? content.Stage(index) : null;
            if (st == null) return false;
            if (st.FreeFromStart) return true;

            return Data.unlockedMaps > index;
        }

        public static bool CanUnlockStage(GameContent content, int index, out string why)
        {
            why = null;
            if (StageUnlocked(content, index)) { why = "이미 열림"; return false; }

            var st = content != null ? content.Stage(index) : null;
            if (st == null) { why = "없는 구역"; return false; }

            // 🔴 **앞 구역부터 차례로** 연다. 건너뛰게 두면 재화를 모아 마지막 구역으로
            //    바로 가고, 그러면 중간 구역이 통째로 안 쓰인다.
            if (!StageUnlocked(content, index - 1)) { why = "앞 구역 먼저"; return false; }

            if (Data.scrap   < st.unlockScrap)   { why = "고철 부족"; return false; }
            if (Data.circuit < st.unlockCircuit) { why = "회로 부족"; return false; }
            if (Data.core    < st.unlockCore)    { why = "코어 부족"; return false; }
            return true;
        }

        public static bool UnlockStage(GameContent content, int index)
        {
            if (!CanUnlockStage(content, index, out _)) return false;

            var st = content.Stage(index);
            Data.scrap   -= st.unlockScrap;
            Data.circuit -= st.unlockCircuit;
            Data.core    -= st.unlockCore;

            if (Data.unlockedMaps < index + 1) Data.unlockedMaps = index + 1;
            Save();
            return true;
        }

        public static void RecordRun(int value, int collected, int depthReached)
        {
            Data.credits += value;
            Data.runsCompleted++;
            Data.totalCollected += collected;
            if (value > Data.bestRunValue) Data.bestRunValue = value;
            if (depthReached > Data.bestDepth) Data.bestDepth = depthReached;
            Save();
        }

        public static void WipeForTesting()
        {
            cached = new MetaData();
            if (UsePrefs) PlayerPrefs.DeleteKey(PrefsKey);
            Save();
        }

        /// <summary>테스트 전용: 디스크를 건드리지 않고 메모리 상태만 갈아끼운다.</summary>
        public static void ReplaceInMemory(MetaData data)
        {
            cached = data ?? new MetaData();
        }
    }
}
