using LoveAlgo.Common;
using LoveAlgo.Simulation;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 모듈 진입점.
    /// ScheduleTable 정적 클래스를 ISchedule 인터페이스로 노출 + ScheduleUI lazy spawn.
    /// 시뮬레이션 sub-mode(Schedule)로서 SimulationModule에 자기 등록.
    /// 씬 하이어라키: _Modules/ScheduleModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ScheduleModule : MonoBehaviour, ISchedule, ISimulationSubMode
    {
        [Header("UI Prefab (모듈 응집)")]
        [SerializeField] ScheduleUI scheduleUIPrefab;

        ScheduleUI _scheduleUI;

        public ScheduleUI ScheduleUI
        {
            get
            {
                if (_scheduleUI == null && scheduleUIPrefab != null)
                {
                    var parent = UIManager.Instance?.GetGroupRoot(UIGroup.Simulate);
                    _scheduleUI = parent != null ? Instantiate(scheduleUIPrefab, parent) : Instantiate(scheduleUIPrefab);
                    _scheduleUI.name = scheduleUIPrefab.name;
                    _scheduleUI.gameObject.SetActive(false);
                    UISoundManager.Instance?.BindButtonsInTransform(_scheduleUI.transform);
                }
                return _scheduleUI;
            }
        }

        public LoveAlgo.Simulation.SimulationMode Mode => LoveAlgo.Simulation.SimulationMode.Schedule;

        void Awake()
        {
            Services.Register<ISchedule>(this);
            Services.Get<ISimulation>()?.RegisterSubMode(this);
        }

        void OnDestroy()
        {
            if (Services.TryGet<ISchedule>() == (ISchedule)this)
                Services.Unregister<ISchedule>();
        }

        // ── ISimulationSubMode ───────────────────────
        public void Enter()
        {
            var ui = ScheduleUI;
            if (ui != null) ui.gameObject.SetActive(true);
        }

        public void Exit()
        {
            if (_scheduleUI != null) _scheduleUI.gameObject.SetActive(false);
        }

        // ── ISchedule (도메인 데이터) ────────────────
        public ScheduleEffect GetEffect(ScheduleType type) => ScheduleTable.Get(type);
        public ScheduleType[] GetTypes(ScheduleCategory category) => ScheduleTable.GetTypes(category);
        public string GetCategoryName(ScheduleCategory category) => ScheduleTable.GetCategoryName(category);
        public ScheduleCategory GetCategory(ScheduleType type) => ScheduleTable.GetCategory(type);
    }
}
