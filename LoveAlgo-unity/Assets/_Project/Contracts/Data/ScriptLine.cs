using System;

namespace LoveAlgo.Contracts
{
    // C4-Phase A: LoveAlgo.Story → LoveAlgo.Contracts 로 이동.
    // ScriptLine + LineType + NextType — 데이터/enum이라 Contracts back-ref 안 만듦.

    /// <summary>
    /// CSV 라인 타입
    /// </summary>
    public enum LineType
    {
        Text,       // 대사/나레이션
        Char,       // 캐릭터 제어
        BG,         // 배경 전환
        CG,         // CG 이미지 (배경 위 오버레이, 대사창 자동 숨김)
        SD,         // SD 컷씬 (부분 표시, 캐릭터/대사창 유지)
        Overlay,    // 보조 배경 (캐릭터별 테마 등)
        Sound,      // 오디오
        FX,         // 시각 효과
        Flow,       // 흐름 제어
        Choice,     // 선택지 시작
        Option,     // 선택지 항목
        Place       // 장소/이벤트 표시 (좌상단 배너)
    }

    /// <summary>
    /// Next 컬럼의 진행 방식
    /// </summary>
    public enum NextType
    {
        Immediate,  // > : 즉시 다음 라인
        Click,      // click : 플레이어 입력 대기
        Await,      // await : 현재 액션 완료 대기
        Delay       // 숫자 : N초 후 자동 진행
    }

    /// <summary>
    /// CSV 한 줄을 표현하는 데이터 구조
    /// </summary>
    [Serializable]
    public class ScriptLine
    {
        public string LineID { get; set; }       // 점프 대상 앵커
        public LineType Type { get; set; }       // 라인 종류
        public string Speaker { get; set; }      // 화자
        public string Value { get; set; }        // 데이터
        public NextType NextType { get; set; }   // 진행 방식
        public float DelaySeconds { get; set; }  // Delay 시 대기 시간
        public int SourceLine { get; set; }      // 원본 CSV 라인 번호 (디버그용)

        public ScriptLine() { }

        public ScriptLine(string lineId, LineType type, string speaker, string value, NextType nextType, float delay = 0f, int sourceLine = 0)
        {
            LineID = lineId ?? "";
            Type = type;
            Speaker = speaker ?? "";
            Value = value ?? "";
            NextType = nextType;
            DelaySeconds = delay;
            SourceLine = sourceLine;
        }

        /// <summary>
        /// 나레이션인지 확인 (Text 타입이면서 Speaker가 없는 경우)
        /// </summary>
        public bool IsNarration => Type == LineType.Text && string.IsNullOrEmpty(Speaker);

        public override string ToString()
        {
            return $"[{Type}] {(string.IsNullOrEmpty(LineID) ? "" : $"#{LineID} ")}{Speaker}: {Value} -> {NextType}";
        }
    }
}
