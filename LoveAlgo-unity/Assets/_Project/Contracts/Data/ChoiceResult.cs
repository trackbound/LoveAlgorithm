namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 선택 결과.
    /// C4-Phase B-7b 에서 LoveAlgo.Story → LoveAlgo.Contracts 로 이동 (IChoicePopup 반환).
    /// </summary>
    public class ChoiceResult
    {
        public int SelectedIndex;
        public string JumpTarget;
    }
}
