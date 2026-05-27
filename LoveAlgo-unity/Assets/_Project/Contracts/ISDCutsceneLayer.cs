using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// SD(슈퍼디포름) 컷씬 레이어 외부 계약 (Phase B-8b).
    /// 구현: <see cref="LoveAlgo.Stage.SDCutsceneLayer"/>.
    /// 호출자: SDLineExecutor (Execute/IsShowing 체크), PhoneNotificationButton (IsShowing),
    /// SaveDataSerializer/StageRestorer (세이브/복원), GameFlowJumper/SessionController (Clear).
    /// </summary>
    public interface ISDCutsceneLayer
    {
        bool IsShowing { get; }
        string CurrentSD { get; }

        /// <summary>CSV 인라인 명령 실행: "name:duration" 등.</summary>
        UniTask ExecuteAsync(string value, CancellationToken ct = default);

        UniTask ShowAsync(string sdName, float duration = 0.5f, CancellationToken ct = default);

        /// <summary>즉시 비활성 + 상태 클리어.</summary>
        void Clear();
    }
}
