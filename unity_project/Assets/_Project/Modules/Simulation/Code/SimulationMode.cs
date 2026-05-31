namespace LoveAlgo.Simulation
{
    /// <summary>
    /// 시뮬레이션 컨텍스트의 sub-mode.
    /// 새 sub-mode 추가 시 여기에 값만 추가하면 됨 (SimulationModule 무수정).
    /// </summary>
    public enum SimulationMode
    {
        None,       // 시뮬레이션 비활성 (Story 모드)
        Schedule,   // 자유행동/스케줄 선택 (메인)
        Shop        // 상점 (Schedule 위 sub-mode)
        // 미래: MiniGame, Rest, ...
    }
}
