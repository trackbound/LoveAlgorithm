#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor.Mappings
{
    /// <summary>
    /// CG: 작가표기(로아 첫만남) ↔ 엔진 ID(cg_01_char_roa_01).
    /// CG_List.xlsx의 "리소스명"/"설명" 컬럼에서 import.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/CG Map", fileName = "CgMap")]
    public class CgMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string ko;        // 설명: "로아 첫만남"
            public string engineId;   // 리소스명: "cg_01_char_roa_01"
            public string filename;   // "cg_01_char_roa_01.png"
        }

        public List<Entry> entries = new();

        public bool TryResolve(string ko, out string engineId)
        {
            foreach (var e in entries)
            {
                if (e.ko == ko) { engineId = e.engineId; return true; }
            }
            engineId = null;
            return false;
        }
    }
}
#endif
