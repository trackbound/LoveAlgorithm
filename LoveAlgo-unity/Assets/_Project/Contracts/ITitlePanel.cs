namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 타이틀 패널 UI 외부 계약 (Phase B-4).
    /// 구현: <see cref="LoveAlgo.UI.TitlePanel"/>.
    ///
    /// 외부 표면(ISP)은 타이틀 BGM 재생 진입점 1개뿐. 메뉴 버튼/데코 크로스페이드/호버 등은
    /// concrete 캡슐화 내부. 활성/비활성은 UIManager.SetActiveIfExists가 `as MonoBehaviour`로 처리.
    /// </summary>
    public interface ITitlePanel
    {
        /// <summary>타이틀 BGM 재생 (LockScreen 첫 진입 흐름에서는 자동 보류).</summary>
        void PlayTitleBGM();
    }
}
