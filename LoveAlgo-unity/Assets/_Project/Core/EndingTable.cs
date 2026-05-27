using System;
using System.Collections.Generic;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 엔딩 슬롯 매니페스트 — Resources/Story/endings.csv를 로드해
    /// (HeroineId, IsHappy) → 진입 스크립트 이름 매핑을 제공.
    ///
    /// 사용 흐름(GameFlowController.EnterEnding):
    ///   1. DetermineEndingHeroine() → 엔딩 히로인 ID (또는 null)
    ///   2. IsHappyEnding(id) → 해피/새드 분기
    ///   3. EndingTable.ResolveScriptName(id, isHappy) → 매니페스트가 있으면 매핑된 이름,
    ///      없으면 null. 호출자가 null인 경우 프로시져 폴백(Ending_{heroine}_{Happy|Sad}).
    ///
    /// 새 엔딩 추가는 endings.csv 한 줄과 Resources/Story/{name}.csv 스크립트만으로 가능.
    /// </summary>
    public static class EndingTable
    {
        const string ManifestPath = "Story/endings";

        /// <summary>한 엔딩 슬롯의 정보. 매니페스트 행 하나 = EndingEntry 하나.</summary>
        public readonly struct EndingEntry
        {
            public readonly string HeroineId;    // 빈 문자열이면 "노 고백" 엔딩
            public readonly bool IsHappy;
            public readonly string ScriptName;
            public readonly string DisplayName;  // 갤러리/엑스트라 표시용

            public EndingEntry(string heroineId, bool isHappy, string scriptName, string displayName)
            {
                HeroineId = heroineId ?? "";
                IsHappy = isHappy;
                ScriptName = scriptName;
                DisplayName = displayName ?? "";
            }
        }

        static readonly List<EndingEntry> _entries = new();
        static bool _loaded;

        static EndingTable()
        {
            LoadFromCsv();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload()
        {
            _entries.Clear();
            _loaded = false;
            LoadFromCsv();
        }

        /// <summary>
        /// Resources/Story/endings.csv를 파싱해 _entries에 등록.
        /// 잘못된 행은 LogError 하고 그 행만 skip — 부분 로드 허용.
        /// 파일 없거나 비어 있으면 _entries만 비고 끝 — 호출자는 null 받음 → 프로시져 폴백.
        /// </summary>
        static void LoadFromCsv()
        {
            if (_loaded) return;
            _loaded = true;

            var asset = Resources.Load<TextAsset>(ManifestPath);
            if (asset == null)
            {
                Debug.Log("[EndingTable] endings.csv 없음 — 프로시져 폴백 사용");
                return;
            }

            var records = CsvUtility.SplitRecords(asset.text);
            int loaded = 0;
            foreach (var rec in records)
            {
                var raw = rec.Text.Trim();
                if (string.IsNullOrEmpty(raw)) continue;
                if (raw.StartsWith("#")) continue;
                if (raw.StartsWith("HeroineId,", StringComparison.OrdinalIgnoreCase)) continue;

                var cols = CsvUtility.SplitCsv(raw);
                if (cols.Length < 3)
                {
                    Debug.LogError($"[EndingTable] endings.csv L{rec.StartLine}: 컬럼 부족 ({cols.Length}/3 필수: HeroineId,IsHappy,ScriptName) — '{Truncate(raw)}'");
                    continue;
                }

                string heroineId   = cols[0].Trim();
                string isHappyStr  = cols[1].Trim();
                string scriptName  = cols[2].Trim();
                string displayName = cols.Length >= 4 ? cols[3].Trim() : "";

                if (string.IsNullOrEmpty(scriptName))
                {
                    Debug.LogError($"[EndingTable] endings.csv L{rec.StartLine}: ScriptName 비어 있음");
                    continue;
                }

                bool isHappy = ParseIsHappy(isHappyStr);
                _entries.Add(new EndingEntry(heroineId, isHappy, scriptName, displayName));
                loaded++;
            }

            Debug.Log($"[EndingTable] endings.csv에서 {loaded}개 엔딩 로드");
        }

        static bool ParseIsHappy(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                            || s.Equals("happy", StringComparison.OrdinalIgnoreCase);
        }

        static string Truncate(string s, int max = 80)
            => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max) + "…");

        /// <summary>
        /// (heroineId, isHappy) 조합에 해당하는 스크립트 이름 반환.
        /// 매핑이 없으면 null — 호출자가 프로시져 폴백 사용.
        /// heroineId가 null/빈 문자열이면 "노 고백" 슬롯을 찾음 (isHappy는 무시).
        /// </summary>
        public static string ResolveScriptName(string heroineId, bool isHappy)
        {
            heroineId = heroineId ?? "";
            bool isNormalSlot = string.IsNullOrEmpty(heroineId);

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!string.Equals(e.HeroineId, heroineId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!isNormalSlot && e.IsHappy != isHappy)
                    continue;
                return e.ScriptName;
            }
            return null;
        }

        /// <summary>매니페스트에 등록된 엔딩 슬롯 수. 0이면 폴백 모드.</summary>
        public static int Count => _entries.Count;

        /// <summary>매니페스트 전체 슬롯 (갤러리/엑스트라 UI 구성 용).</summary>
        public static IReadOnlyList<EndingEntry> GetAll() => _entries;

        /// <summary>EditMode 테스트 격리용 — 매니페스트를 다시 로드.</summary>
        public static void ReloadForTests()
        {
            _entries.Clear();
            _loaded = false;
            LoadFromCsv();
        }
    }
}
