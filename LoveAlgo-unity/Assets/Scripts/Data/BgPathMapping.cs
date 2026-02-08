// ═══════════════════════════════════════════════════════════════════
// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.
// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate Background Mapping)
// 생성 시각: 2026-01-26 09:59:06
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace LoveAlgo.Data
{
    /// <summary>
    /// 배경 이름 → Resources 경로 매핑 (자동 생성)
    /// </summary>
    public static class BgPathMapping
    {
        public static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
        {
            // ClubRoom
            { "BG_ClubRoom_Corridor_Day", "Backgrounds/ClubRoom/BG_ClubRoom_Corridor_Day" },
            { "BG_ClubRoom_Exterior_Day_Trees", "Backgrounds/ClubRoom/BG_ClubRoom_Exterior_Day_Trees" },

            // Common
            { "BG_Black", "Backgrounds/Common/BG_Black" },
            { "BG_RoaTheme", "Backgrounds/Common/BG_RoaTheme" },
            { "BG_ScriptSelect", "Backgrounds/Common/BG_ScriptSelect" },

            // Engineering
            { "BG_Engineering_Classroom_Day", "Backgrounds/Engineering/BG_Engineering_Classroom_Day" },
            { "BG_Engineering_Front_Day", "Backgrounds/Engineering/BG_Engineering_Front_Day" },

            // MyRoom
            { "BG_MyRoom_Bed_Day", "Backgrounds/MyRoom/BG_MyRoom_Bed_Day" },
            { "BG_MyRoom_Bed_Night", "Backgrounds/MyRoom/BG_MyRoom_Bed_Night" },
            { "BG_MyRoom_Desk_Day", "Backgrounds/MyRoom/BG_MyRoom_Desk_Day" },
            { "BG_MyRoom_Interior_Day", "Backgrounds/MyRoom/BG_MyRoom_Interior_Day" },
            { "BG_MyRoom_Interior_Night", "Backgrounds/MyRoom/BG_MyRoom_Interior_Night" },

            // StudentCenter
            { "BG_StudentCenter_Cafe_Day", "Backgrounds/StudentCenter/BG_StudentCenter_Cafe_Day" },
            { "BG_StudentCenter_Front_Day", "Backgrounds/StudentCenter/BG_StudentCenter_Front_Day" },
            { "BG_StudentCenter_Front_Night", "Backgrounds/StudentCenter/BG_StudentCenter_Front_Night" },

        };

        /// <summary>
        /// 레거시 이름 → 신규 이름 매핑 (하위 호환, 수동 관리)
        /// </summary>
        public static readonly Dictionary<string, string> LegacyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Black", "BG_Black" },
            { "RoaTheme", "BG_RoaTheme" },
            { "script_select", "BG_ScriptSelect" },
            { "MyRoom", "BG_MyRoom_Interior_Day" },
            { "MyRoom_Night", "BG_MyRoom_Interior_Night" },
            { "Bed", "BG_MyRoom_Bed_Day" },
            { "Bed_Night", "BG_MyRoom_Bed_Night" },
            { "Desk", "BG_MyRoom_Desk_Day" },
            { "StudentCenter", "BG_StudentCenter_Front_Day" },
            { "StudentCenter_Night", "BG_StudentCenter_Front_Night" },
            { "Cafe", "BG_StudentCenter_Cafe_Day" },
            { "ClubRoom", "BG_ClubRoom_Corridor_Day" },
            { "Engineering", "BG_Engineering_Front_Day" },
            { "MajorClass", "BG_Engineering_Classroom_Day" },
        };

        /// <summary>
        /// 배경 이름으로 Resources 경로 조회
        /// </summary>
        public static string GetPath(string bgName)
        {
            // 레거시 이름 변환
            string actualName = bgName;
            if (LegacyNames.TryGetValue(bgName, out string newName))
            {
                actualName = newName;
            }

            // 경로 매핑에서 찾기
            if (Paths.TryGetValue(actualName, out string path))
            {
                return path;
            }

            // 폴백: Backgrounds/이름
            return $"Backgrounds/{actualName}";
        }
    }
}
