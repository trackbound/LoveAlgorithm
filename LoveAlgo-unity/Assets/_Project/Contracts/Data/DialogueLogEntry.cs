namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 대사 로그 항목.
    /// C4-Phase A에서 LoveAlgo.Story → LoveAlgo.Contracts 로 이동.
    /// </summary>
    public struct DialogueLogEntry
    {
        public string Speaker;       // 표시 이름 (한글)
        public string Text;
        public string CharacterId;   // 영문 ID (썸네일 로드용)
    }
}
