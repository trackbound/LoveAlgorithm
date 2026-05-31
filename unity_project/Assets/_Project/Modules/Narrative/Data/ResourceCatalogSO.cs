using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story.Data
{
    /// <summary>
    /// 스토리 리소스 통합 카탈로그.
    ///
    /// **schema v2: Id + Aliases[]**
    ///   - `Id` = 영구 안정 키 (영문, 파일명 기반) — 변경 비권장
    ///   - `Aliases[]` = 작가가 자유롭게 추가 (한글, 영문 약어 등) — 0개도 허용
    ///
    /// Lookup: Id → Aliases 순서로 매칭. CSV에서 둘 다 호출 가능.
    ///
    /// 위치: `Assets/Resources/ResourceCatalog.asset` (런타임 싱글톤 접근)
    ///
    /// 워크플로:
    ///   - 개발: CSV/Editor에서 한글 별칭 자유롭게 사용
    ///   - 출시: 별칭 → Id 치환 도구(추후) 또는 별칭만 유지하고 데이터 stable
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Resource Catalog", fileName = "ResourceCatalog")]
    public class ResourceCatalogSO : ScriptableObject
    {
        // ── Entry types ──
        [Serializable]
        public class SpriteEntry
        {
            public string Id;          // 영구 안정 키 (예: "bg_10_04")
            public string[] Aliases;   // 작가 별칭 (예: ["자취방 침대위 아침", "자취방_아침"])
            public Sprite Sprite;
            public string Note;        // 작가 메모

            /// <summary>표시용 첫 별칭 또는 Id.</summary>
            public string DisplayLabel => (Aliases != null && Aliases.Length > 0 && !string.IsNullOrEmpty(Aliases[0]))
                ? Aliases[0] : (Id ?? "");
        }

        [Serializable]
        public class AudioEntry
        {
            public string Id;          // 예: "white_noise"
            public string[] Aliases;   // 예: ["백색소음1", "노이즈"]
            public AudioClip Clip;
            public string Note;

            public string DisplayLabel => (Aliases != null && Aliases.Length > 0 && !string.IsNullOrEmpty(Aliases[0]))
                ? Aliases[0] : (Id ?? "");
        }

        [Serializable]
        public class CharacterEntry
        {
            public string Id;          // 엔진 ID (예: "c01")
            public string DisplayName; // 한글 이름 (예: "로아")
            public string[] Aliases;   // CSV 별칭 (예: ["Roa", "로아01"])
            public string Note;
        }

        [Serializable]
        public class EmoteEntry
        {
            public string Id;          // 파일 코드 (예: "_11")
            public string[] Aliases;   // 한글·영문 별칭 (예: ["눈웃음", "EyeSmile"])
            public string Note;

            public string DisplayLabel => (Aliases != null && Aliases.Length > 0 && !string.IsNullOrEmpty(Aliases[0]))
                ? Aliases[0] : (Id ?? "");
        }

        // ── Catalog data ──
        [Header("배경")] public List<SpriteEntry> BG = new();
        [Header("CG")] public List<SpriteEntry> CG = new();
        [Header("SD 컷씬")] public List<SpriteEntry> SD = new();
        [Header("VirtualBG Overlay")] public List<SpriteEntry> Overlays = new();
        [Header("BGM")] public List<AudioEntry> BGM = new();
        [Header("SFX")] public List<AudioEntry> SFX = new();
        [Header("캐릭터")] public List<CharacterEntry> Characters = new();
        [Header("표정 (코드 = Id)")] public List<EmoteEntry> Emotes = new();

        // ══════════════════════════════════════════════
        //  싱글톤
        // ══════════════════════════════════════════════
        const string ResourcePath = "ResourceCatalog";
        static ResourceCatalogSO _instance;

        public static ResourceCatalogSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ResourceCatalogSO>(ResourcePath);
                    if (_instance == null)
                        Debug.LogError($"[ResourceCatalog] Resources/{ResourcePath}.asset 없음 — Editor 메뉴로 생성하세요.");
                }
                return _instance;
            }
        }

        public static void ResetInstance() => _instance = null;

        // ══════════════════════════════════════════════
        //  Lookup API — Id 우선, Aliases 폴백 (case-insensitive)
        // ══════════════════════════════════════════════

        public bool TryGetBg(string key, out Sprite sprite)       => TryFindSprite(BG, key, out sprite);
        public bool TryGetCg(string key, out Sprite sprite)       => TryFindSprite(CG, key, out sprite);
        public bool TryGetSd(string key, out Sprite sprite)       => TryFindSprite(SD, key, out sprite);
        public bool TryGetOverlay(string key, out Sprite sprite)  => TryFindSprite(Overlays, key, out sprite);
        public bool TryGetBgm(string key, out AudioClip clip)     => TryFindAudio(BGM, key, out clip);
        public bool TryGetSfx(string key, out AudioClip clip)     => TryFindAudio(SFX, key, out clip);

        public bool TryGetEmoteCode(string key, out string code)
        {
            code = null;
            if (string.IsNullOrEmpty(key) || Emotes == null) return false;
            string k = Normalize(key);
            foreach (var e in Emotes)
            {
                if (e == null) continue;
                if (KeyMatch(e.Id, k)) { code = e.Id; return true; }
                if (e.Aliases != null)
                    foreach (var a in e.Aliases)
                        if (KeyMatch(a, k)) { code = e.Id; return true; }
            }
            return false;
        }

        public CharacterEntry GetCharacter(string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName) || Characters == null) return null;
            string k = Normalize(idOrName);
            foreach (var c in Characters)
            {
                if (c == null) continue;
                if (KeyMatch(c.Id, k)) return c;
                if (KeyMatch(c.DisplayName, k)) return c;
                if (c.Aliases != null)
                    foreach (var a in c.Aliases)
                        if (KeyMatch(a, k)) return c;
            }
            return null;
        }

        public string ResolveCharacterId(string speakerOrName) => GetCharacter(speakerOrName)?.Id;

        // ── 정규화·매칭 ──
        /// <summary>
        /// 키 정규화 — 우발적 공백 방어:
        ///   - 앞뒤 trim
        ///   - 중간 다중 공백 → 단일 공백
        /// "  자취방  침대위 아침  " → "자취방 침대위 아침" 으로 변환되어 lookup 성공.
        /// </summary>
        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            s = s.Trim();
            // 다중 공백 → 단일
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        static bool KeyMatch(string stored, string normalizedKey)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            return string.Equals(Normalize(stored), normalizedKey, StringComparison.OrdinalIgnoreCase);
        }

        // ── 헬퍼: Id → Aliases 순 매칭 (정규화 포함) ──
        static bool TryFindSprite(List<SpriteEntry> list, string key, out Sprite sprite)
        {
            sprite = null;
            if (list == null || string.IsNullOrEmpty(key)) return false;
            string k = Normalize(key);
            foreach (var e in list)
            {
                if (e == null) continue;
                if (KeyMatch(e.Id, k))
                {
                    sprite = e.Sprite;
                    return sprite != null;
                }
                if (e.Aliases != null)
                    foreach (var a in e.Aliases)
                        if (KeyMatch(a, k))
                        {
                            sprite = e.Sprite;
                            return sprite != null;
                        }
            }
            return false;
        }

        static bool TryFindAudio(List<AudioEntry> list, string key, out AudioClip clip)
        {
            clip = null;
            if (list == null || string.IsNullOrEmpty(key)) return false;
            string k = Normalize(key);
            foreach (var e in list)
            {
                if (e == null) continue;
                if (KeyMatch(e.Id, k))
                {
                    clip = e.Clip;
                    return clip != null;
                }
                if (e.Aliases != null)
                    foreach (var a in e.Aliases)
                        if (KeyMatch(a, k))
                        {
                            clip = e.Clip;
                            return clip != null;
                        }
            }
            return false;
        }

        // ══════════════════════════════════════════════
        //  Validate
        // ══════════════════════════════════════════════
        public List<string> Validate()
        {
            var issues = new List<string>();
            CheckSpriteIdsUnique(BG, "BG", issues);
            CheckSpriteIdsUnique(CG, "CG", issues);
            CheckSpriteIdsUnique(SD, "SD", issues);
            CheckSpriteIdsUnique(Overlays, "Overlay", issues);
            CheckAudioIdsUnique(BGM, "BGM", issues);
            CheckAudioIdsUnique(SFX, "SFX", issues);
            CheckSpriteNotNull(BG, "BG", issues);
            CheckSpriteNotNull(CG, "CG", issues);
            CheckSpriteNotNull(SD, "SD", issues);
            CheckSpriteNotNull(Overlays, "Overlay", issues);
            CheckAudioNotNull(BGM, "BGM", issues);
            CheckAudioNotNull(SFX, "SFX", issues);
            CheckCharacterIdsUnique(issues);
            CheckEmoteIdsUnique(issues);
            return issues;
        }

        static void CheckSpriteIdsUnique(List<SpriteEntry> list, string cat, List<string> issues)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in list)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Id)) { issues.Add($"[{cat}] 빈 Id"); continue; }
                if (!seen.Add(e.Id)) issues.Add($"[{cat}] 중복 Id: '{e.Id}'");
            }
        }
        static void CheckAudioIdsUnique(List<AudioEntry> list, string cat, List<string> issues)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in list)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Id)) { issues.Add($"[{cat}] 빈 Id"); continue; }
                if (!seen.Add(e.Id)) issues.Add($"[{cat}] 중복 Id: '{e.Id}'");
            }
        }
        static void CheckSpriteNotNull(List<SpriteEntry> list, string cat, List<string> issues)
        {
            foreach (var e in list)
                if (e != null && !string.IsNullOrEmpty(e.Id) && e.Sprite == null)
                    issues.Add($"[{cat}] '{e.Id}' Sprite 미할당");
        }
        static void CheckAudioNotNull(List<AudioEntry> list, string cat, List<string> issues)
        {
            foreach (var e in list)
                if (e != null && !string.IsNullOrEmpty(e.Id) && e.Clip == null)
                    issues.Add($"[{cat}] '{e.Id}' AudioClip 미할당");
        }
        void CheckCharacterIdsUnique(List<string> issues)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in Characters)
            {
                if (c == null) continue;
                if (string.IsNullOrEmpty(c.Id)) { issues.Add("[Character] 빈 Id"); continue; }
                if (!seen.Add(c.Id)) issues.Add($"[Character] 중복 Id: '{c.Id}'");
            }
        }
        void CheckEmoteIdsUnique(List<string> issues)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Emotes)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Id)) { issues.Add("[Emote] 빈 Id"); continue; }
                if (!seen.Add(e.Id)) issues.Add($"[Emote] 중복 Id: '{e.Id}'");
            }
        }
    }
}
