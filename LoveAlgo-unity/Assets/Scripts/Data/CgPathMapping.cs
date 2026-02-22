// ═══════════════════════════════════════════════════════════════════
// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.
// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate CG Mapping)
// 생성 시각: 2026-02-11 01:30:00
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace LoveAlgo.Data
{
    /// <summary>
    /// CG 이름 → Resources 경로 매핑 (자동 생성)
    /// </summary>
    public static class CgPathMapping
    {
        public static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "CG_Yeun_01", "CG/CG_Yeun_01" },
        };

        /// <summary>
        /// CG 이름으로 Resources 경로 조회
        /// </summary>
        public static string GetPath(string cgName)
        {
            if (Paths.TryGetValue(cgName, out string path))
                return path;
            return $"CG/{cgName}";
        }
    }
}
