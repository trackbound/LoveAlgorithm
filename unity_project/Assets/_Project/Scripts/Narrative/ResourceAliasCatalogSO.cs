using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 에셋 별칭(작가용 한글명) → 코드 ID Definition SO. CSV는 한글 별칭("공대 강의실 낮", "로아", "기본")으로
    /// 쓰고, 에셋 파일명은 ASSET_NAMING 코드명(bg_20_05, c01, _00)을 유지한다 — 이 카탈로그가 둘을 잇는다.
    ///
    /// 해석 위치 = 엔진(NarrativeController)이 명령 발행 **전**(ColorTint 프리셋 선례: 컨트롤러가 SO로 해석해
    /// 발행, 뷰는 SO를 모른다). 뷰의 컨벤션 로딩(<c>Resources.Load("BG/{name}")</c> 등)은 무변경.
    ///
    /// 매칭 규칙(순수 <see cref="Resolve(IReadOnlyList{Entry}, string)"/>):
    ///   ① id 일치(대소문자 무시) → 정본 id 반환(예: "Daily1" → "daily1")
    ///   ② 별칭 일치(대소문자 무시) → id 반환(예: "로아" → "roa")
    ///   ③ 미등록 → 입력 그대로(passthrough — 코드명 직접 기입·신규 에셋이 카탈로그 없이도 동작).
    ///
    /// 표정(Emote) id는 밑줄 없는 코드("00", "41")로 등록한다 — StageView가 <c>{char}_{emote}</c>로 합성하므로.
    /// 데이터 출처 = 구 ResourceCatalog.asset(9152615^ 회수). 정적 정의라 런타임 상태/세이브 무관.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceAliasCatalog", menuName = "LoveAlgo/Resource Alias Catalog")]
    public class ResourceAliasCatalogSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("코드 ID — 에셋 파일명(확장자 제외). 예: bg_20_05, c01, roa. Emote는 밑줄 없이(00, 41).")]
            public string id;
            [Tooltip("작가용 별칭들(한글명 등). 대소문자 무시 매칭.")]
            public string[] aliases;
        }

        [Header("스테이지")]
        [SerializeField] List<Entry> bg = new();
        [SerializeField] List<Entry> cg = new();
        [SerializeField] List<Entry> sd = new();

        [Header("캐릭터")]
        [Tooltip("캐릭터 id(c01~c05) ← 한글 이름. 화자명→슬롯 해석에도 쓰인다.")]
        [SerializeField] List<Entry> characters = new();
        [Tooltip("표정 코드(밑줄 없이) ← 한글 표정명.")]
        [SerializeField] List<Entry> emotes = new();
        [Tooltip("Char Enter에 표정 생략 시 기본 표정 코드(파일 c01_00 등 — 캐릭터 단독 파일은 없다).")]
        [SerializeField] string defaultEmote = "00";

        [Header("오디오")]
        [SerializeField] List<Entry> bgm = new();
        [SerializeField] List<Entry> sfx = new();

        public IReadOnlyList<Entry> Bg => bg;
        public IReadOnlyList<Entry> Cg => cg;
        public IReadOnlyList<Entry> Sd => sd;
        public IReadOnlyList<Entry> Characters => characters;
        public IReadOnlyList<Entry> Emotes => emotes;
        public string DefaultEmote => defaultEmote;
        public IReadOnlyList<Entry> Bgm => bgm;
        public IReadOnlyList<Entry> Sfx => sfx;

        public string ResolveBg(string name) => Resolve(bg, name);
        public string ResolveCg(string name) => Resolve(cg, name);
        public string ResolveSd(string name) => Resolve(sd, name);
        public string ResolveCharacter(string name) => Resolve(characters, name);
        public string ResolveEmote(string name) => Resolve(emotes, name);
        public string ResolveBgm(string name) => Resolve(bgm, name);
        public string ResolveSfx(string name) => Resolve(sfx, name);

        /// <summary>화자명이 등록된 캐릭터인가(해석 성공 여부 — 인라인 emote 슬롯 매칭에 id를 실을지 결정).</summary>
        public bool TryResolveCharacter(string name, out string id)
        {
            if (IsRegistered(characters, name)) { id = Resolve(characters, name); return true; }
            id = null;
            return false;
        }

        /// <summary>순수 룩업. id/별칭 대소문자 무시 일치 → 정본 id, 미등록/공백 → 입력 그대로(trim).</summary>
        public static string Resolve(IReadOnlyList<Entry> entries, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            string key = name.Trim();
            if (entries == null) return key;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (string.Equals(e.id, key, StringComparison.OrdinalIgnoreCase)) return e.id;
                if (e.aliases == null) continue;
                for (int a = 0; a < e.aliases.Length; a++)
                    if (string.Equals(e.aliases[a], key, StringComparison.OrdinalIgnoreCase)) return e.id;
            }
            return key;
        }

        /// <summary>순수: 등록 여부(별칭/ID 일치 존재).</summary>
        public static bool IsRegistered(IReadOnlyList<Entry> entries, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || entries == null) return false;
            string key = name.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (string.Equals(e.id, key, StringComparison.OrdinalIgnoreCase)) return true;
                if (e.aliases == null) continue;
                for (int a = 0; a < e.aliases.Length; a++)
                    if (string.Equals(e.aliases[a], key, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
