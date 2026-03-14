// ═══════════════════════════════════════════════════════════════════
// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.
// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate Background Mapping)
// 생성 시각: 2026-02-25 12:00:00
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
            // (root)
            { "BG_BlackCut", "Backgrounds/BG_BlackCut" },

            // CampusStreet
            { "BG_Campus_Street1_Day", "Backgrounds/CampusStreet/BG_Campus_Street1_Day" },
            { "BG_Campus_Street2_Day", "Backgrounds/CampusStreet/BG_Campus_Street2_Day" },

            // ClubRoom
            { "BG_ClubRoom_Interior_Day", "Backgrounds/ClubRoom/BG_ClubRoom_Interior_Day" },
            { "BG_ClubRoom_Interior_Day_Cherry", "Backgrounds/ClubRoom/BG_ClubRoom_Interior_Day_Cherry" },

            // ConvenienceStore
            { "BG_ConvenienceStore_Inside", "Backgrounds/ConvenienceStore/BG_ConvenienceStore_Inside" },
            { "BG_ConvenienceStore_Night", "Backgrounds/ConvenienceStore/BG_ConvenienceStore_Night" },

            // Engineering
            { "BG_Engineering_Classroom", "Backgrounds/Engineering/BG_Engineering_Classroom" },
            { "BG_Engineering_Classroom_Cherry", "Backgrounds/Engineering/BG_Engineering_Classroom_Cherry" },
            { "BG_Engineering_Corridor", "Backgrounds/Engineering/BG_Engineering_Corridor" },
            { "BG_Engineering_Front_Day", "Backgrounds/Engineering/BG_Engineering_Front_Day" },
            { "BG_Engineering_Front_Night", "Backgrounds/Engineering/BG_Engineering_Front_Night" },
            { "BG_Engineering_StudentLounge", "Backgrounds/Engineering/BG_Engineering_StudentLounge" },

            // MyRoom
            { "BG_MyRoom_Bed_Day", "Backgrounds/MyRoom/BG_MyRoom_Bed_Day" },
            { "BG_MyRoom_Bed_Night", "Backgrounds/MyRoom/BG_MyRoom_Bed_Night" },
            { "BG_MyRoom_Desk", "Backgrounds/MyRoom/BG_MyRoom_Desk" },
            { "BG_MyRoom_Interior_Day", "Backgrounds/MyRoom/BG_MyRoom_Interior_Day" },
            { "BG_MyRoom_Interior_Night", "Backgrounds/MyRoom/BG_MyRoom_Interior_Night" },
            { "BG_MyRoom_Interior_Night_LightOn", "Backgrounds/MyRoom/BG_MyRoom_Interior_Night_LightOn" },

            // StudentCenter
            { "BG_StudentCenter_Board", "Backgrounds/StudentCenter/BG_StudentCenter_Board" },
            { "BG_StudentCenter_Front_Day", "Backgrounds/StudentCenter/BG_StudentCenter_Front_Day" },
            { "BG_StudentCenter_Front_Night", "Backgrounds/StudentCenter/BG_StudentCenter_Front_Night" },
            { "BG_StudentCenter_Hallway", "Backgrounds/StudentCenter/BG_StudentCenter_Hallway" },
            { "BG_StudentCenter_Office", "Backgrounds/StudentCenter/BG_StudentCenter_Office" },
        };

        /// <summary>
        /// 레거시/별칭 이름 → 실제 이름 매핑 (영어 + 한글)
        /// </summary>
        public static readonly Dictionary<string, string> LegacyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // ── 영어 별칭 (하위 호환) ──
            { "Black", "BG_BlackCut" },
            { "BG_Black", "BG_BlackCut" },
            { "MyRoom", "BG_MyRoom_Interior_Day" },
            { "MyRoom_Night", "BG_MyRoom_Interior_Night" },
            { "Bed", "BG_MyRoom_Bed_Day" },
            { "Bed_Night", "BG_MyRoom_Bed_Night" },
            { "Desk", "BG_MyRoom_Desk" },
            { "StudentCenter", "BG_StudentCenter_Front_Day" },
            { "StudentCenter_Night", "BG_StudentCenter_Front_Night" },
            { "ClubRoom", "BG_ClubRoom_Interior_Day" },
            { "Engineering", "BG_Engineering_Front_Day" },
            { "MajorClass", "BG_Engineering_Classroom" },

            // ── _Day 접미사 하위 호환 (파일명에서 _Day 제거된 항목) ──
            { "BG_MyRoom_Desk_Day", "BG_MyRoom_Desk" },
            { "BG_Engineering_Classroom_Day", "BG_Engineering_Classroom" },
            { "BG_Engineering_Classroom_Day_Cherry", "BG_Engineering_Classroom_Cherry" },
            { "BG_Engineering_Corridor_Day", "BG_Engineering_Corridor" },
            { "BG_Engineering_Stall_Day", "BG_Engineering_StudentLounge" },
            { "BG_StudentCenter_Board_Day", "BG_StudentCenter_Board" },
            { "BG_StudentCenter_Office_Day", "BG_StudentCenter_Office" },

            // ── 짧은 이름 별칭 (시나리오 CSV용) ──
            { "BG_MyRoom_Day", "BG_MyRoom_Interior_Day" },
            { "BG_MyRoom_Night_LightOn", "BG_MyRoom_Interior_Night_LightOn" },

            // ── 한글 별칭 (시나리오 작가용) ──
            { "강의실_낮", "BG_Engineering_Classroom" },
            { "강의실_낮_벚꽃", "BG_Engineering_Classroom_Cherry" },
            { "게시판", "BG_StudentCenter_Board" },
            { "공대_강의실복도", "BG_Engineering_Corridor" },
            { "공대_앞_낮", "BG_Engineering_Front_Day" },
            { "공대_앞_밤", "BG_Engineering_Front_Night" },
            { "공대_학생라운지", "BG_Engineering_StudentLounge" },
            { "공대_학생복지실", "BG_Engineering_StudentLounge" },
            { "동아리방_낮_나무", "BG_ClubRoom_Interior_Day" },
            { "동아리방_낮_벚꽃", "BG_ClubRoom_Interior_Day_Cherry" },
            { "자취방_책상", "BG_MyRoom_Desk" },
            { "자취방_전경_낮", "BG_MyRoom_Interior_Day" },
            { "자취방_전경_밤", "BG_MyRoom_Interior_Night" },
            { "자취방_전경_밤_불켜기", "BG_MyRoom_Interior_Night_LightOn" },
            { "자취방_침대위_아침", "BG_MyRoom_Bed_Day" },
            { "자취방_침대위_밤", "BG_MyRoom_Bed_Night" },
            { "캠퍼스거리_1_맑음", "BG_Campus_Street1_Day" },
            { "캠퍼스거리_2_맑음", "BG_Campus_Street2_Day" },
            { "학생회관_앞_낮", "BG_StudentCenter_Front_Day" },
            { "학생회관_앞_밤", "BG_StudentCenter_Front_Night" },
            { "학생회관_복도", "BG_StudentCenter_Hallway" },
            { "학생회관_행정실", "BG_StudentCenter_Office" },
        };

        /// <summary>
        /// 배경 이름으로 Resources 경로 조회
        /// </summary>
        public static string GetPath(string bgName)
        {
            // 레거시/별칭 이름 변환
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
