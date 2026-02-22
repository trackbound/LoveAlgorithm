// ═══════════════════════════════════════════════════════════════════
// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.
// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate Character Mapping)
// 생성 시각: 2026-02-11 01:30:00
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace LoveAlgo.Data
{
    /// <summary>
    /// 캐릭터별 표정 매핑 (자동 생성)
    /// </summary>
    public static class CharacterEmoteMapping
    {
        /// <summary>
        /// 캐릭터 이름 → (표정 이름 → Resources 경로)
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, string>> Characters =
            new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Bom", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Bom/01_Default" },
                    { "EyeSmile", "Characters/Bom/02_EyeSmile" },
                    { "BrightSmile", "Characters/Bom/03_BrightSmile" },
                    { "Happy", "Characters/Bom/04_Happy" },
                    { "Glare", "Characters/Bom/05_Glare" },
                    { "Surprise", "Characters/Bom/06_Surprise" },
                    { "Tearful", "Characters/Bom/07_Tearful" },
                }
            },
            {
                "Daeun", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Daeun/01_Default" },
                    { "EyeSmile", "Characters/Daeun/02_EyeSmile" },
                    { "BrightSmile", "Characters/Daeun/03_BrightSmile" },
                    { "Happy", "Characters/Daeun/04_Happy" },
                    { "Glare", "Characters/Daeun/05_Glare" },
                    { "Surprise", "Characters/Daeun/06_Surprise" },
                    { "Tearful", "Characters/Daeun/07_Tearful" },
                }
            },
            {
                "Heewon", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Heewon/01_Default" },
                    { "EyeSmile", "Characters/Heewon/02_EyeSmile" },
                    { "BrightSmile", "Characters/Heewon/03_BrightSmile" },
                    { "Happy", "Characters/Heewon/04_Happy" },
                    { "Glare", "Characters/Heewon/05_Glare" },
                    { "Surprise", "Characters/Heewon/06_Surprise" },
                    { "Tearful", "Characters/Heewon/07_Tearful" },
                }
            },
            {
                "Roa", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Roa/01_Default" },
                    { "EyeSmile", "Characters/Roa/02_EyeSmile" },
                    { "BrightSmile", "Characters/Roa/03_BrightSmile" },
                    { "Happy", "Characters/Roa/04_Happy" },
                    { "Glare", "Characters/Roa/05_Glare" },
                    { "Surprise", "Characters/Roa/06_Surprise" },
                    { "Tearful", "Characters/Roa/07_Tearful" },
                }
            },
            {
                "Yeun", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Yeun/01_Default" },
                    { "EyeSmile", "Characters/Yeun/02_EyeSmile" },
                    { "BrightSmile", "Characters/Yeun/03_BrightSmile" },
                    { "Happy", "Characters/Yeun/04_Happy" },
                    { "Glare", "Characters/Yeun/05_Glare" },
                    { "Surprise", "Characters/Yeun/06_Surprise" },
                    { "Tearful", "Characters/Yeun/07_Tearful" },
                }
            },
        };

        /// <summary>
        /// 한글 표정 별칭 → 코드 이름 (시나리오 작가용)
        /// </summary>
        public static readonly Dictionary<string, string> EmoteAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "기본", "Default" },
            { "눈웃음", "EyeSmile" },
            { "밝게웃음", "BrightSmile" },
            { "활짝", "Happy" },
            { "찌릿", "Glare" },
            { "깜짝", "Surprise" },
            { "울먹", "Tearful" },
        };

        /// <summary>
        /// 캐릭터의 표정 경로 조회 (한글 별칭 지원)
        /// </summary>
        public static string GetPath(string character, string emote)
        {
            // 한글 별칭 변환
            if (EmoteAliases.TryGetValue(emote, out string resolvedEmote))
                emote = resolvedEmote;

            if (Characters.TryGetValue(character, out var emotes))
            {
                if (emotes.TryGetValue(emote, out string path))
                {
                    return path;
                }
                // 표정이 없으면 Default 시도
                if (emotes.TryGetValue("Default", out string defaultPath))
                {
                    return defaultPath;
                }
            }

            // 폴백
            return $"Characters/{character}/{emote}";
        }

        /// <summary>
        /// 캐릭터가 특정 표정을 가지고 있는지 확인
        /// </summary>
        public static bool HasEmote(string character, string emote)
        {
            if (EmoteAliases.TryGetValue(emote, out string resolved))
                emote = resolved;

            return Characters.TryGetValue(character, out var emotes) &&
                   emotes.ContainsKey(emote);
        }

        /// <summary>
        /// 캐릭터의 모든 표정 목록 조회
        /// </summary>
        public static IEnumerable<string> GetEmotes(string character)
        {
            if (Characters.TryGetValue(character, out var emotes))
            {
                return emotes.Keys;
            }
            return Array.Empty<string>();
        }
    }
}
