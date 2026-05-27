using System;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 시뮬레이션 sub-mode가 구현하는 인터페이스. ScheduleModule/ShopModule 등이 구현.
    /// </summary>
    public interface ISimulationSubMode
    {
        SimulationMode Mode { get; }

        /// <summary>이 sub-mode 진입 (자기 UI 표시).</summary>
        void Enter();

        /// <summary>이 sub-mode 종료 (자기 UI 숨김).</summary>
        void Exit();
    }

    /// <summary>
    /// 시뮬레이션 컨텍스트 모듈 외부 계약.
    /// 진입/종료, sub-mode 라우팅, QuickMenu 호스팅.
    /// 구현: <see cref="SimulationModule"/>.
    /// </summary>
    public interface ISimulation
    {
        SimulationMode CurrentMode { get; }
        bool IsActive { get; }

        /// <summary>시뮬레이션 컨텍스트 사이드바 (QuickMenu) 인스턴스 (lazy spawn).</summary>
        IQuickMenu QuickMenu { get; }

        /// <summary>Sub-mode가 Awake에서 자기 등록 (모듈 독립성 — SimulationModule이 sub-mode 구현 모름).</summary>
        void RegisterSubMode(ISimulationSubMode subMode);

        /// <summary>시뮬레이션 컨텍스트 진입. mainMode가 None이면 모듈 기본값 사용.</summary>
        void EnterSimulation(SimulationMode mainMode = SimulationMode.None);

        /// <summary>시뮬레이션 컨텍스트 완전 종료 (Story 복귀).</summary>
        void ExitSimulation();

        /// <summary>특정 sub-mode 진입 (예: Shop).</summary>
        void OpenSubMode(SimulationMode mode);

        /// <summary>현재 sub-mode 종료. 메인 모드면 ExitSimulation, 아니면 메인으로 복귀.</summary>
        void CloseSubMode();

        event Action OnEntered;
        event Action OnExited;
        event Action<SimulationMode> OnSubModeChanged;
    }
}
