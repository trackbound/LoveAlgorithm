using LoveAlgo.UI;

namespace LoveAlgo.Title
{
    /// <summary>
    /// 타이틀/이름입력/Extra 모듈 외부 계약.
    /// 구현: <see cref="TitleModule"/>.
    /// </summary>
    public interface ITitle
    {
        TitlePanel TitlePanel { get; }
        UsernameUI UsernameUI { get; }

        /// <summary>Extra 팝업 표시 (모달).</summary>
        void ShowExtraUI();
    }
}
