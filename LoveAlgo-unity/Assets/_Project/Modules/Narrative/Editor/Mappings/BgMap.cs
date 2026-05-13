#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor.Mappings
{
    /// <summary>
    /// 배경: 작가표기(자취방 책상) ↔ 시멘틱 ID(bg_my_room_desk) ↔ 레거시 코드(bg_10_06).
    /// BG_List.xlsx의 시트별 장소 분류를 import.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/BG Map", fileName = "BgMap")]
    public class BgMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string category;     // 자취방, 공대, 캠퍼스 ...
            public string ko;            // 작가 표기 — 설명 텍스트 (자취방 책상, 공대 강의실 낮)
            public string semanticId;    // 시멘틱 (bg_my_room_desk) — 우선 사용
            public string legacyCode;    // 레거시 (bg_10_06) — semanticId 미입력 시 fallback
            public string filename;      // bg_10_06.png — 리소스 매칭용
        }

        public List<Entry> entries = new();

        public bool TryResolve(string ko, out string engineId)
        {
            foreach (var e in entries)
            {
                if (e.ko == ko)
                {
                    engineId = !string.IsNullOrEmpty(e.semanticId) ? e.semanticId : e.legacyCode;
                    return !string.IsNullOrEmpty(engineId);
                }
            }
            engineId = null;
            return false;
        }
    }
}
#endif
