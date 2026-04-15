using System.Collections.Generic;
using LoveAlgo.Core;
using UnityEngine;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 카테고리 (탭 3개)
    /// </summary>
    public enum ScheduleCategory
    {
        PartTime,   // 알바 (편의점, 상하차, 투자)
        Exercise,   // 운동 (임시 A, B, C)
        Study       // 공부 (임시 D, E, F)
    }

    /// <summary>
    /// 스케줄 종류 (탭 3개 × 3개 = 9개)
    /// 자유행동: 하루 2회 (낮/밤)
    /// </summary>
    public enum ScheduleType
    {
        // 알바
        PartTime_Store,     // 편의점: 끈기+1, 돈+20000, 피로+5
        PartTime_Loading,   // 상하차: 끈기+2, 돈+50000, 피로+15 (하루 1회 제한)
        Invest,             // 코인 투자: 돈 ±50~100%, 피로+0 (자산≥30000 필요)

        // 운동 (기획 추가 예정 — 임시)
        Exercise_A,
        Exercise_B,
        Exercise_C,

        // 공부 (기획 추가 예정 — 임시)
        Study_D,
        Study_E,
        Study_F
    }

    /// <summary>
    /// 스케줄 효과 데이터
    /// </summary>
    [System.Serializable]
    public struct ScheduleEffect
    {
        public string displayName;
        public string description;     // 3줄 설명
        public int moneyChange;        // + 수입, - 지출
        public int strengthChange;     // 체력
        public int intelligenceChange; // 지성
        public int socialChange;       // 사교성
        public int perseveranceChange; // 끈기
        public int fatigueChange;      // 피로
        public bool isLimited;         // 하루 1회 제한 여부

        public ScheduleEffect(string name, string desc, int money, int str, int intel, int soc, int per, int fatigue, bool limited = false)
        {
            displayName = name;
            description = desc;
            moneyChange = money;
            strengthChange = str;
            intelligenceChange = intel;
            socialChange = soc;
            perseveranceChange = per;
            fatigueChange = fatigue;
            isLimited = limited;
        }
    }

    /// <summary>
    /// 스케줄 데이터 정적 테이블
    /// SO(ScheduleDataSO)에서 데이터를 로드하며, SO가 없으면 하드코딩 폴백 사용
    /// </summary>
    public static class ScheduleTable
    {
        static Dictionary<ScheduleType, ScheduleEffect> effectMap;
        static Dictionary<ScheduleCategory, (string name, string desc)> categoryMap;
        static bool initialized;

        static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            effectMap = new Dictionary<ScheduleType, ScheduleEffect>();
            categoryMap = new Dictionary<ScheduleCategory, (string, string)>();

            var so = Resources.Load<ScheduleDataSO>("Data/ScheduleData");
            if (so != null && so.Schedules.Count > 0)
            {
                foreach (var e in so.Schedules)
                    effectMap[e.type] = e.effect;
                foreach (var c in so.Categories)
                    categoryMap[c.category] = (c.displayName, c.description);
            }
            else
            {
                RegisterDefaults();
            }
        }

        public static void Reload()
        {
            effectMap = null;
            categoryMap = null;
            initialized = false;
            EnsureInit();
        }

        public static ScheduleEffect Get(ScheduleType type)
        {
            EnsureInit();
            return effectMap.TryGetValue(type, out var e) ? e : new("알 수 없음", "", 0, 0, 0, 0, 0, 0);
        }

        public static ScheduleCategory GetCategory(ScheduleType type) => type switch
        {
            ScheduleType.PartTime_Store or
            ScheduleType.PartTime_Loading or
            ScheduleType.Invest => ScheduleCategory.PartTime,

            ScheduleType.Exercise_A or
            ScheduleType.Exercise_B or
            ScheduleType.Exercise_C => ScheduleCategory.Exercise,

            ScheduleType.Study_D or
            ScheduleType.Study_E or
            ScheduleType.Study_F => ScheduleCategory.Study,

            _ => ScheduleCategory.PartTime
        };

        /// <summary>해당 카테고리에 속하는 모든 ScheduleType 반환</summary>
        public static ScheduleType[] GetTypes(ScheduleCategory category) => category switch
        {
            ScheduleCategory.PartTime => new[] { ScheduleType.PartTime_Store, ScheduleType.PartTime_Loading, ScheduleType.Invest },
            ScheduleCategory.Exercise => new[] { ScheduleType.Exercise_A, ScheduleType.Exercise_B, ScheduleType.Exercise_C },
            ScheduleCategory.Study    => new[] { ScheduleType.Study_D, ScheduleType.Study_E, ScheduleType.Study_F },
            _ => System.Array.Empty<ScheduleType>()
        };

        /// <summary>카테고리 한글 이름</summary>
        public static string GetCategoryName(ScheduleCategory cat)
        {
            EnsureInit();
            return categoryMap.TryGetValue(cat, out var info) ? info.name : cat switch
            {
                ScheduleCategory.PartTime => "알바",
                ScheduleCategory.Exercise => "운동",
                ScheduleCategory.Study    => "공부",
                _ => ""
            };
        }

        /// <summary>카테고리 설명</summary>
        public static string GetCategoryDescription(ScheduleCategory cat)
        {
            EnsureInit();
            return categoryMap.TryGetValue(cat, out var info) ? info.desc : cat switch
            {
                ScheduleCategory.PartTime => "각종 아르바이트와 투자 활동을 통해 재화를 벌 수 있습니다.\n어떤 일을 하는지에 따라 수입과 컨디션이 달라질 수 있습니다.",
                ScheduleCategory.Exercise => "체력을 올릴 수 있어요.",
                ScheduleCategory.Study    => "지성을 올릴 수 있어요. 피로도가 오릅니다.",
                _ => ""
            };
        }

        #region Hardcoded Fallback
        static void RegisterDefaults()
        {
            // 카테고리 정보
            categoryMap[ScheduleCategory.PartTime] = ("알바", "돈을 벌 수 있어요. 피로도가 오릅니다.");
            categoryMap[ScheduleCategory.Exercise] = ("운동", "체력을 올릴 수 있어요.");
            categoryMap[ScheduleCategory.Study]    = ("공부", "지성을 올릴 수 있어요. 피로도가 오릅니다.");

            // 스케줄 효과
            void Add(ScheduleType t, string name, string desc, int money, int str, int intel, int soc, int per, int fatigue, bool limited = false)
                => effectMap[t] = new ScheduleEffect(name, desc, money, str, intel, soc, per, fatigue, limited);

            Add(ScheduleType.PartTime_Store, "편의점", $"어쩌구 편의점에서 아르바이트를 합니다.\n{MoneyFormat.Currency(10000)}의 수익을 획득합니다.", 20000, 0, 0, 0, 1, 5);
            Add(ScheduleType.PartTime_Loading, "상하차 알바", $"상하차를 하면 돈도 벌고 힘도 세지고\n{MoneyFormat.Currency(50000)}을 버는데\n밤은 새야할지도 어쩌구 저쩌구 3줄까지 입니다.", 50000, 0, 0, 0, 2, 15, true);
            Add(ScheduleType.Invest, "코인투자", "영차영차\n다같이 외쳐 영차영차", 0, 0, 0, 0, 0, 0);
            Add(ScheduleType.Exercise_A, "헬스장", "학교 근처 헬스장에서 운동합니다.\n체력이 크게 오릅니다.", 0, 3, 0, 0, 0, 0);
            Add(ScheduleType.Exercise_B, "러닝", "동네 공원에서 달리기를 합니다.\n체력과 끈기가 오르지만 피로도 쌓입니다.", 0, 2, 0, 0, 0, 5);
            Add(ScheduleType.Exercise_C, "스트레칭", "가볍게 스트레칭을 합니다.\n체력과 끈기가 조금 오릅니다.", 0, 1, 0, 0, 1, 3);
            Add(ScheduleType.Study_D, "독서실", "독서실에서 집중해서 공부합니다.\n지성이 크게 오르지만 피로도 쌓입니다.", 0, 0, 3, 0, 0, 5);
            Add(ScheduleType.Study_E, "인강", "집에서 인터넷 강의를 듣습니다.\n지성이 적당히 오릅니다.", 0, 0, 2, 0, 0, 3);
            Add(ScheduleType.Study_F, "스터디 카페", "친구들과 카페에서 공부합니다.\n지성과 사교성이 조금 오릅니다.", 0, 0, 1, 1, 0, 2);
        }
        #endregion
    }
}
