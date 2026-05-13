#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor.Mappings
{
    /// <summary>
    /// 사운드: BGM/SFX 작가표기 ↔ 엔진 ID. (xlsx 없음 — 수동 또는 사용 시 누락 리포트 기반 채움)
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/Sound Map", fileName = "SoundMap")]
    public class SoundMap : ScriptableObject
    {
        public enum Kind { BGM, SFX }

        [Serializable]
        public struct Entry
        {
            public Kind kind;
            public string ko;        // "백색소음1", "로아 BGM"
            public string engineId;   // "WhiteNoise1", "Roa"
        }

        public List<Entry> entries = new();

        public bool TryResolve(Kind kind, string ko, out string engineId)
        {
            foreach (var e in entries)
            {
                if (e.kind == kind && e.ko == ko) { engineId = e.engineId; return true; }
            }
            engineId = null;
            return false;
        }
    }
}
#endif
