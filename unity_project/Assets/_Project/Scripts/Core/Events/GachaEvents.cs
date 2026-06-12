namespace LoveAlgo.Events
{
    /// <summary>
    /// 가챠 화면 열기 명령(EventBus). <see cref="FromPurchase"/> true = 가챠권 구매 직후
    /// (컨트롤러가 즉시 추첨·기록 후 결과 통지 — 뷰는 이미 확정된 결과의 오픈 연출만 재생,
    /// 연출 중 크래시에도 조각 손실 없음), false = 현황 보기(추첨 없음, 퍼즐판 정적 표시).
    /// 발행: 상점 가챠권 구매 흐름(후속 배선)·빠른 진입(dev). 기획서 p43.
    /// </summary>
    public readonly struct OpenGachaCommand
    {
        public readonly bool FromPurchase;

        public OpenGachaCommand(bool fromPurchase)
        {
            FromPurchase = fromPurchase;
        }
    }

    /// <summary>가챠 화면 닫기 명령(나가기 버튼 — 상점 복귀는 발행측 소관).</summary>
    public readonly struct CloseGachaCommand { }

    /// <summary>
    /// 추첨 결과 통지(컨트롤러→뷰). <see cref="PieceIndex"/> -1 = 완성 후 추가 구매(새 조각 없음,
    /// 연출만 + 업적 카운트). 뷰는 이 값으로 오픈 연출(흔들→뒤집힘→조각 안착/완성 컨페티)을 재생한다.
    /// </summary>
    public readonly struct GachaDrawResultEvent
    {
        public readonly int PieceIndex;
        public readonly bool IsBonus;
        public readonly int OwnedCount;
        public readonly bool IsComplete;

        public GachaDrawResultEvent(int pieceIndex, bool isBonus, int ownedCount, bool isComplete)
        {
            PieceIndex = pieceIndex;
            IsBonus = isBonus;
            OwnedCount = ownedCount;
            IsComplete = isComplete;
        }
    }
}
