// ═══════════════════════════════════════════════════════════════════
// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.
// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate Character Mapping)
// 생성 시각: 2026-01-26 09:59:06
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
                    { "Default", "Characters/Bom/Default" },
                    { "Bright", "Characters/Bom/Bright" },
                    { "EyeSmile", "Characters/Bom/EyeSmile" },
                    { "Glare", "Characters/Bom/Glare" },
                    { "Happy", "Characters/Bom/Happy" },
                    { "Surprise", "Characters/Bom/Surprise" },
                    { "Tearful", "Characters/Bom/Tearful" },
                }
            },
            {
                "Daeun", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Daeun/Default" },
                    { "Bright", "Characters/Daeun/Bright" },
                    { "EyeSmile", "Characters/Daeun/EyeSmile" },
                    { "Glare", "Characters/Daeun/Glare" },
                    { "Happy", "Characters/Daeun/Happy" },
                    { "Surprise", "Characters/Daeun/Surprise" },
                    { "Tearful", "Characters/Daeun/Tearful" },
                }
            },
            {
                "Heewon", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Heewon/Default" },
                    { "Bright", "Characters/Heewon/Bright" },
                    { "EyeSmile", "Characters/Heewon/EyeSmile" },
                    { "Glare", "Characters/Heewon/Glare" },
                    { "Happy", "Characters/Heewon/Happy" },
                    { "Surprise", "Characters/Heewon/Surprise" },
                    { "Tearful", "Characters/Heewon/Tearful" },
                }
            },
            {
                "Roa", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Roa/Default" },
                    { "Bright", "Characters/Roa/Bright" },
                    { "Glare", "Characters/Roa/Glare" },
                    { "Happy", "Characters/Roa/Happy" },
                    { "Surprise", "Characters/Roa/Surprise" },
                    { "Tearful", "Characters/Roa/Tearful" },
                }
            },
            {
                "Yeun", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default", "Characters/Yeun/Default" },
                    { "Bright", "Characters/Yeun/Bright" },
                    { "EyeSmile", "Characters/Yeun/EyeSmile" },
                    { "Glare", "Characters/Yeun/Glare" },
                    { "Happy", "Characters/Yeun/Happy" },
                    { "Surprise", "Characters/Yeun/Surprise" },
                    { "Tearful", "Characters/Yeun/Tearful" },
                }
            },
        };

        /// <summary>
        /// 캐릭터의 표정 경로 조회
        /// </summary>
        public static string GetPath(string character, string emote)
        {
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
