using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 시퀀스 카탈로그(정의 SO) — 시퀀스 id → {방, CSV 경로, 자동 도착일}.
    /// <c>EventScriptCatalogSO</c>와 같은 패턴: 정적 정의라 런타임 상태/세이브와 무관(인스펙터 편집),
    /// CSV는 <c>StreamingAssets/Messenger</c> 아래라 빌드에서 작가 편집 가능.
    /// 기획 전체 시퀀스(어느 방에 어느 날 무엇이 오는가)를 이 에셋 한 곳에서 조망한다.
    /// </summary>
    [CreateAssetMenu(fileName = "MessengerScriptCatalog", menuName = "LoveAlgo/Messenger Script Catalog")]
    public class MessengerScriptCatalogSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("시퀀스 고유 id (스토리 CSV Flow Messenger:{id}와 세이브 기록이 참조). 대소문자 구분.")]
            public string sequenceId;
            [Tooltip("도착할 방(상대) id — 예: roa, c01. FriendCatalog의 친구 id와 일치.")]
            public string roomId;
            [Tooltip("시퀀스 CSV 상대 경로(StreamingAssets/Messenger 기준, 예: DateInvite_Roa.csv).")]
            public string csvPath;
            [Tooltip("자동 도착일(그 날 아침 도착). 0 = 자동 도착 없음(스토리 Flow 트리거 전용).")]
            public int deliverDay;
        }

        [SerializeField] List<Entry> entries = new();

        /// <summary>매핑 엔트리(읽기 — 인스펙터/툴).</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>엔트리 일괄 설정(테스트 주입용 — 인스펙터 편집과 동치).</summary>
        public void SetEntries(List<Entry> value) => entries = value ?? new List<Entry>();

        /// <summary>시퀀스 id로 엔트리 조회. 미등록/빈 id면 null(대소문자 구분).</summary>
        public Entry Resolve(string sequenceId) => Resolve(entries, sequenceId);

        /// <summary>해당 일자에 자동 도착할 엔트리들.</summary>
        public List<Entry> ForDay(int day) => ForDay(entries, day);

        /// <summary>순수 룩업 — SO 인스턴스 없이 검증 가능.</summary>
        public static Entry Resolve(IReadOnlyList<Entry> entries, string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.sequenceId == sequenceId) return e;
            }
            return null;
        }

        /// <summary>순수 일자 필터 — day ≥ 1만 유효(0은 "자동 도착 없음" 예약값).</summary>
        public static List<Entry> ForDay(IReadOnlyList<Entry> entries, int day)
        {
            var result = new List<Entry>();
            if (entries == null || day < 1) return result;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.deliverDay == day) result.Add(e);
            }
            return result;
        }
    }
}
