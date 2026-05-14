using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 감정명(한글) ↔ 감정 ID(_NN) 매핑.
    /// Single Source of Truth — Editor 변환기 + 런타임 양쪽이 동일하게 참조.
    /// xlsx Character_Emotion_List의 "감정"/"감정 ID" 컬럼에서 import.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Story/Emote Map", fileName = "EmoteMap")]
    public class EmoteMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string ko;   // 눈웃음, 활짝, 찌릿 ...
            public string id;   // _11, _13, _21 또는 EyeSmile, BrightSmile ...
        }

        public List<Entry> entries = new();

        static EmoteMap _instance;

        /// <summary>Resources/Data/EmoteMap 자동 로드 싱글톤 (런타임 사용).</summary>
        public static EmoteMap Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<EmoteMap>("Data/EmoteMap");
                    if (_instance == null)
                        Debug.LogError("[EmoteMap] Resources/Data/EmoteMap.asset 을 찾을 수 없습니다");
                }
                return _instance;
            }
        }

        Dictionary<string, string> _cache;

        public bool TryResolve(string ko, out string id)
        {
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(ko ?? "", out id);
        }

        /// <summary>한글 → ID. 매칭 없으면 원본 그대로 반환.</summary>
        public string Resolve(string ko)
        {
            if (string.IsNullOrEmpty(ko)) return ko;
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(ko, out var id) ? id : ko;
        }

        void RebuildCache()
        {
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.ko) && !string.IsNullOrEmpty(e.id))
                    _cache[e.ko] = e.id;
        }

        void OnValidate() => _cache = null;
    }
}
