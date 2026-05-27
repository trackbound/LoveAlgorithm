using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 튜토리얼 오버레이 UI 외부 계약 (Phase B-1).
    /// 구현: <see cref="LoveAlgo.UI.TutorialOverlay"/>.
    ///
    /// 핵심 표면만 노출 (ISP): 외부 호출자(ScheduleUI 등)는 RunAsync + HasSeen 만 사용.
    /// 나머지 인스펙터 필드/CSV 파싱/dim 처리 등은 캡슐화 내부 유지.
    /// </summary>
    public interface ITutorialOverlay
    {
        /// <summary>튜토리얼 실행 — CSV 파싱 → 스텝별 표시 → 클릭 진행 → 완료 시 플래그 설정.</summary>
        UniTask RunAsync(string csvResourcePath, string seenFlagKey, CancellationToken ct);

        /// <summary>이 튜토리얼을 이전에 본 적이 있는지 (PlayerPrefs 영구 확인).</summary>
        bool HasSeen(string seenFlagKey);
    }
}
