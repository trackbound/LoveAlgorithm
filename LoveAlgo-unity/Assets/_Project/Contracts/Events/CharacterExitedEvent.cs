namespace LoveAlgo.Contracts
{
    /// <summary>
    /// Stage가 캐릭터를 슬롯에서 제거했을 때 발행 (Phase C3-5).
    /// Audio 모듈이 구독해 voice 라우팅 상태 정리.
    /// </summary>
    public readonly struct CharacterExitedEvent
    {
        public readonly string Character;

        public CharacterExitedEvent(string character)
        {
            Character = character ?? "";
        }
    }
}
