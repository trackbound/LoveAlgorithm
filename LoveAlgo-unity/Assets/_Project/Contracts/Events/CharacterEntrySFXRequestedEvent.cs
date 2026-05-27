namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 캐릭터 entry SFX를 재생하고 싶을 때 발행 (Phase C3-5).
    /// Audio 모듈이 구독해 AudioManager.PlayCharacterEntrySFX 호출.
    ///
    /// CharacterEnteredEvent와 별도 — 호출처마다 SFX 의도가 달라서 분리.
    /// 일부 Stage 코드는 exit 시점에도 character entry SFX를 의도적으로 재생 (presence 사운드 공유).
    /// </summary>
    public readonly struct CharacterEntrySFXRequestedEvent
    {
        public readonly string Character;

        public CharacterEntrySFXRequestedEvent(string character)
        {
            Character = character ?? "";
        }
    }
}
