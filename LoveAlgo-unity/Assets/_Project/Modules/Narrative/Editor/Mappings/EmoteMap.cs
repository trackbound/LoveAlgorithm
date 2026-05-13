#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor.Mappings
{
    /// <summary>
    /// 감정명(한글) ↔ 감정 ID(_NN) 매핑.
    /// 캐릭터 무관 공통. (캐릭터별 리소스명은 변환 시 `c01` + `_11` 합성)
    /// Character_Emotion_List.xlsx의 "감정"/"감정 ID" 컬럼에서 import.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/Emote Map", fileName = "EmoteMap")]
    public class EmoteMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string ko;   // 눈웃음, 활짝, 찌릿 ...
            public string id;   // _11, _13, _21 ...
        }

        public List<Entry> entries = new();

        public bool TryResolve(string ko, out string id)
        {
            foreach (var e in entries)
            {
                if (e.ko == ko) { id = e.id; return true; }
            }
            id = null;
            return false;
        }
    }
}
#endif
