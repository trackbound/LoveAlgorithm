using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 선택지 팝업 UI 외부 계약 (Phase B-7b).
    /// 구현: <see cref="LoveAlgo.Story.ChoicePopup"/>.
    ///
    /// 외부 표면(ISP): 선택지 표시 + 결과 대기, 그리고 로드/씬 전환 시 즉시 초기화.
    /// 호버/펄스/효과 적용/버튼 spawn 같은 내부 동작은 concrete 캡슐화.
    /// </summary>
    public interface IChoicePopup
    {
        /// <summary>선택지 표시 및 선택 대기. 결과(ChoiceResult)는 클릭 시 반환.</summary>
        UniTask<ChoiceResult> ShowAndWaitAsync(List<OptionData> options, CancellationToken ct);

        /// <summary>
        /// 선택지 상태를 즉시 초기화 (로드/씬 전환 시 호출).
        /// 진행 중이던 ShowAndWaitAsync가 취소되어도 버튼/CanvasGroup이 화면에 남는 잔상 방지.
        /// </summary>
        void ResetImmediate();
    }
}
