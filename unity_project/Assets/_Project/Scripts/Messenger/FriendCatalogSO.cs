using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 친구 카탈로그(정의 SO) — 친구 id → 표시명·프로필·기본 상태메시지. 리스트 순서가
    /// 친구 목록 표시 순서다. 구 "5히로인 하드코딩"을 데이터로 대체 — NPC 친구 추가는 에셋 편집만.
    /// 프로필 이미지의 "진행도에 따라 변경"(기획서)은 콘텐츠 확정 후 조건 규칙으로 후속(과설계 게이트).
    /// </summary>
    [CreateAssetMenu(fileName = "FriendCatalog", menuName = "LoveAlgo/Messenger Friend Catalog")]
    public class FriendCatalogSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("친구 id — 시퀀스 CSV의 발신자/카탈로그 roomId와 일치(예: roa, c01).")]
            public string id;
            [Tooltip("친구 목록/말풍선에 표시할 이름.")]
            public string displayName;
            [Tooltip("프로필 사진(작은 원형). 비우면 기본 실루엣.")]
            public Sprite portrait;
            [Tooltip("프로필 화면 배경(친구 클릭 시 우측 영역). 비우면 기본 배경. 진행도별 변경은 후속.")]
            public Sprite profileBg;
            [Tooltip("기본 상태메시지(기획서: '상태 메세지입니다.' 류 기본 문구).")]
            public string defaultStatus;
        }

        [SerializeField] List<Entry> entries = new();

        /// <summary>친구 목록(표시 순서 = 리스트 순서).</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>엔트리 일괄 설정(테스트 주입용 — 인스펙터 편집과 동치).</summary>
        public void SetEntries(List<Entry> value) => entries = value ?? new List<Entry>();

        /// <summary>id로 친구 조회. 미등록이면 null.</summary>
        public Entry Resolve(string id) => Resolve(entries, id);

        /// <summary>표시명 해석 — 미등록 id는 원문 그대로(별칭 카탈로그 passthrough 선례).</summary>
        public string DisplayName(string id)
        {
            var entry = Resolve(id);
            return entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : id;
        }

        /// <summary>순수 룩업 — SO 인스턴스 없이 검증 가능.</summary>
        public static Entry Resolve(IReadOnlyList<Entry> entries, string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.id == id) return e;
            }
            return null;
        }
    }
}
