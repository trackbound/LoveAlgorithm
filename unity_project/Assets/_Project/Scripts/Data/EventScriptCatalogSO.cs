using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo
{
    /// <summary>
    /// 이벤트 태그 → 스토리 스크립트(TextAsset) 매핑 카탈로그(정의 SO). 타임라인의 <c>DayInfo.EventTag</c>
    /// (예: "Event1")를 엔진 포맷 CSV(Resources/Story)로 잇는다 — <c>GameManager</c>의 저녁 이벤트 씨임이
    /// 조회해 그 날 종료 시 재생한다. 정적 정의라 런타임 상태/세이브와 무관(인스펙터 편집). 매핑이 없으면
    /// 그 이벤트는 재생되지 않는다(조용히 건너뜀 → 시뮬만 진행).
    /// </summary>
    [CreateAssetMenu(fileName = "EventScriptCatalog", menuName = "LoveAlgo/Event Script Catalog")]
    public class EventScriptCatalogSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("GameTimeline DayInfo.EventTag와 정확히 일치(예: Event1). 대소문자 구분.")]
            public string eventTag;
            [Tooltip("재생할 엔진 포맷 스토리 CSV(Resources/Story의 TextAsset).")]
            public TextAsset script;
        }

        [SerializeField] List<Entry> entries = new();

        /// <summary>매핑 엔트리(읽기 — 인스펙터/툴).</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>엔트리 일괄 설정(런타임 카탈로그 구성/테스트 주입용 — 인스펙터 편집과 동치).</summary>
        public void SetEntries(List<Entry> value) => entries = value ?? new List<Entry>();

        /// <summary>eventTag에 매핑된 스크립트를 반환. 미매핑/빈 태그/null이면 null(대소문자 구분).</summary>
        public TextAsset Resolve(string eventTag) => Resolve(entries, eventTag);

        /// <summary>순수 룩업(엔트리 목록 + 태그 → 스크립트). SO 인스턴스 없이 검증 가능.</summary>
        public static TextAsset Resolve(IReadOnlyList<Entry> entries, string eventTag)
        {
            if (string.IsNullOrEmpty(eventTag) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.eventTag == eventTag) return e.script;
            }
            return null;
        }
    }
}
