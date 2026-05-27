using System.Collections.Generic;
using LoveAlgo.Contracts;
using UnityEngine;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 데이터 ScriptableObject
    /// 인스펙터에서 스케줄별 효과/카테고리 정보를 편집할 수 있다
    /// </summary>
    [CreateAssetMenu(fileName = "ScheduleData", menuName = "LoveAlgo/Schedule Data")]
    public class ScheduleDataSO : ScriptableObject
    {
        [System.Serializable]
        public class ScheduleEntry
        {
            public ScheduleType type;
            public ScheduleEffect effect;
        }

        [System.Serializable]
        public class CategoryInfo
        {
            public ScheduleCategory category;
            public string displayName;
            public string description;
        }

        [SerializeField] List<ScheduleEntry> schedules = new();
        [SerializeField] List<CategoryInfo> categories = new();

        public IReadOnlyList<ScheduleEntry> Schedules => schedules;
        public IReadOnlyList<CategoryInfo> Categories => categories;
    }
}
