namespace LoveAlgo.Contracts
{
    /// <summary>
    /// Stage가 모든 캐릭터를 슬롯에서 제거했을 때 발행 (Phase C3-5).
    /// Audio 모듈이 구독해 voice 라우팅 상태를 일괄 리셋.
    /// 씬 전환·세션 종료 등에서 트리거.
    /// </summary>
    public readonly struct AllCharactersExitedEvent { }
}
