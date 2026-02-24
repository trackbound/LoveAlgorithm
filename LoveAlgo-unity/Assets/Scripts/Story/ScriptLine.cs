using System;

namespace LoveAlgo.Story
{
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
        public string LineID;       // 점프 대상 앵커 (없으면 빈 문자열)
        public LineType Type;       // 라인 종류
        public string Speaker;      // Text 타입의 화자 (없으면 빈 문자열)
        public string Value;        // Type별 데이터
        public NextType NextType;   // 진행 방식
        public float DelaySeconds;  // NextType.Delay일 때 대기 시간

        public ScriptLine() { }

        public ScriptLine(string lineId, LineType type, string speaker, string value, NextType nextType, float delay = 0f)
        {
            LineID = lineId ?? "";
            Type = type;
            Speaker = speaker ?? "";
            Value = value ?? "";
            NextType = nextType;
            DelaySeconds = delay;
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
