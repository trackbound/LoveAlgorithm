#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor.Mappings
{
    /// <summary>
    /// 화자명(한글) ↔ 캐릭터 ID(c01) ↔ 명령어용 영문 alias(Roa) 매핑.
    /// Character_Emotion_List.xlsx의 각 시트(=캐릭터)의 "캐릭터 ID"/"캐릭터 이름"에서 import.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/Character Map", fileName = "CharacterMap")]
    public class CharacterMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string ko;        // 로아, 서다은, 하예은 ...
            public string code;       // c01, c02, c03 ...
            public string engineId;   // Roa, Daeun, Yeeun (Char 명령어용)
        }

        public List<Entry> entries = new();

        public bool TryResolve(string ko, out Entry entry)
        {
            foreach (var e in entries)
            {
                if (e.ko == ko) { entry = e; return true; }
            }
            entry = default;
            return false;
        }

        public string ResolveCode(string ko)  => TryResolve(ko, out var e) ? e.code : null;
        public string ResolveEngine(string ko) => TryResolve(ko, out var e) ? e.engineId : null;
    }
}
#endif
