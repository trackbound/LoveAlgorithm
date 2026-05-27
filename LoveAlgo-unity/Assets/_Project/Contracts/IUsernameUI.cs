using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 이름 입력 UI 외부 계약 (Phase B-2).
    /// 구현: <see cref="LoveAlgo.UI.UsernameUI"/>.
    ///
    /// 외부 표면(ISP)은 인라인 입력 진입점 하나. 인스펙터 바인딩/내부 흔들기/팝업 사운드 등은
    /// concrete UsernameUI 캡슐화 내부 유지.
    /// </summary>
    public interface IUsernameUI
    {
        /// <summary>
        /// 인라인 모드로 입력창을 열고 확정된 이름을 반환 (Flow,,Username 전용).
        /// GameFlowController의 phase 전환 없이 이름만 받아온다.
        /// </summary>
        UniTask<string> ShowInlineAsync(CancellationToken ct);
    }
}
