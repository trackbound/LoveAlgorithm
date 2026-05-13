#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor.Mappings
{
    /// <summary>SD: 작가표기 ↔ 엔진 ID. SD_List.xlsx에서 import.</summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/SD Map", fileName = "SdMap")]
    public class SdMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string ko;        // "다은 첫만남"
            public string engineId;   // "sd_01_char_daeun_01"
            public string filename;
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
