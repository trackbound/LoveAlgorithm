namespace LoveAlgo.Contracts
{
    /// <summary>
    /// Stage가 캐릭터를 슬롯에 등장시켰을 때 발행 (Phase C3-5).
    /// Audio 모듈이 구독해 voice 라우팅 상태를 갱신.
    /// Entry SFX는 별도의 CharacterEntrySFXRequestedEvent로 트리거 (호출처마다 SFX 의도가 다름).
    /// </summary>
    public readonly struct CharacterEnteredEvent
    {
        public readonly string Character;

        public CharacterEnteredEvent(string character)
        {
            Character = character ?? "";
        }
    }
}
