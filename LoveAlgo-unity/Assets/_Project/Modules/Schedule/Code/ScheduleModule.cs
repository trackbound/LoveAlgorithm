using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 모듈 진입점.
    /// ScheduleTable 정적 클래스를 ISchedule 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/ScheduleModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ScheduleModule : MonoBehaviour, ISchedule
    {
        void Awake()
        {
            Services.Register<ISchedule>(this);
        }

        void OnDestroy()
        {
            if (Services.TryGet<ISchedule>() == (ISchedule)this)
                Services.Unregister<ISchedule>();
        }

        public ScheduleEffect GetEffect(ScheduleType type) => ScheduleTable.Get(type);
        public ScheduleType[] GetTypes(ScheduleCategory category) => ScheduleTable.GetTypes(category);
        public string GetCategoryName(ScheduleCategory category) => ScheduleTable.GetCategoryName(category);
        public ScheduleCategory GetCategory(ScheduleType type) => ScheduleTable.GetCategory(type);
    }
}
