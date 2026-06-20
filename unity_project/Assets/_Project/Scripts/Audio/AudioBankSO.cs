using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Audio
{
    /// <summary>
    /// 오디오 클립 직접 바인딩 뱅크(키 → AudioClip). <see cref="AudioManager"/>가 BGM/SFX 재생 시
    /// <c>Resources.Load("Audio/{category}/{name}")</c> 문자열 경로 대신 이 SO에서 먼저 클립을 찾는다.
    /// 직접 오브젝트 참조(GUID)라 파일을 옮기거나 리소스명을 한글로 바꿔도 매핑이 깨지지 않는다
    /// (경로 오타·파일 이동 시 런타임 무음 대신 에디터에서 바인딩 누락이 바로 보인다).
    ///
    /// <para>키 규약: <see cref="AudioManager"/>가 받는 이름과 동일한 값으로 등록한다 — 즉
    /// 작가용 한글명(CSV)은 <see cref="LoveAlgo.Story.ResourceAliasCatalogSO"/>가 코드 id(roa, daily1 …)로
    /// 먼저 해석하므로, 여기 키는 그 **코드 id**다. 미등록 키는 null 반환 → AudioManager가 Resources 폴백.</para>
    ///
    /// 위치: <c>Resources/Data/AudioBank.asset</c>(AudioManager가 인스펙터 미바인딩 시 자동 로드).
    /// </summary>
    [CreateAssetMenu(fileName = "AudioBank", menuName = "LoveAlgo/Audio Bank")]
    public class AudioBankSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("재생 키 — AudioManager가 받는 코드 id(예: roa, daily1, night, 060_message).")]
            public string key;
            [Tooltip("실제 오디오 클립(직접 참조).")]
            public AudioClip clip;
        }

        [Header("BGM (키 → 클립)")]
        [SerializeField] List<Entry> bgm = new();
        [Header("SFX (키 → 클립)")]
        [SerializeField] List<Entry> sfx = new();
        [Header("Voice (키 → 클립)")]
        [SerializeField] List<Entry> voice = new();

        public IReadOnlyList<Entry> Bgm => bgm;
        public IReadOnlyList<Entry> Sfx => sfx;
        public IReadOnlyList<Entry> Voice => voice;

        /// <summary>카테고리("BGM"/"SFX"/"Voice", 대소문자 무시) + 키로 클립 조회. 미등록/공백 → null.</summary>
        public AudioClip Resolve(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(key)) return null;
            var list = Pick(category);
            if (list == null) return null;

            string k = key.Trim();
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                if (string.Equals(e.key, k, StringComparison.OrdinalIgnoreCase)) return e.clip;
            }
            return null;
        }

        List<Entry> Pick(string category)
        {
            switch (category.Trim().ToLowerInvariant())
            {
                case "bgm": return bgm;
                case "sfx": return sfx;
                case "voice": return voice;
                default: return null;
            }
        }
    }
}
