using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 캐릭터 레이어 외부 계약 (Phase B-8c-2).
    /// 구현: <see cref="LoveAlgo.Story.CharacterLayer"/>.
    ///
    /// 외부 호출자 그룹:
    ///   - SDLineExecutor: IsLayerHidden / SetVisibleAsync (SD 컷씬 진입 시 캐릭 숨김)
    ///   - SessionController / StageRestorer: SetVisibleImmediate (SD 숨김 상태 복원)
    ///   - SaveDataSerializer / StageRestorer: GetSlot(SlotPosition) (세이브 직렬화/복원)
    ///   - CGLineExecutor: ExitAllAsync (CG 진입 시 모든 캐릭 퇴장)
    ///   - SessionController / DayEndMacro / SceneEndMacro: ClearAll
    ///   - SetupMacroExecutor: IsCharacterOnStage / ExecuteAsync
    ///   - StageModule.CharacterEmote: ChangeEmote (래퍼 위임)
    ///   - ScreenFX: ExecuteCharFXAsync (CharShake/CharJump/CharDim — Stage 내부, IStage 통과)
    ///
    /// AutoEnterBySpeakerAsync 는 외부 호출 0 — 인터페이스 비노출 (CharacterLayer 내부 트리거).
    /// </summary>
    public interface ICharacterLayer
    {
        /// <summary>레이어 전체 숨김 상태 (SD 컷씬 진입 시 true).</summary>
        bool IsLayerHidden { get; }

        /// <summary>레이어 전체 페이드 표시/숨김.</summary>
        UniTask SetVisibleAsync(bool visible, CancellationToken ct = default);

        /// <summary>레이어 전체 즉시 표시/숨김 (로드 복원용).</summary>
        void SetVisibleImmediate(bool visible);

        /// <summary>CSV 인라인 명령 실행: "slot:action:character:emote" 등.</summary>
        UniTask ExecuteAsync(string value, CancellationToken ct = default);

        /// <summary>지정 위치 슬롯 얻기 (세이브 직렬화 + 로드 복원).</summary>
        ICharacterSlot GetSlot(SlotPosition position);

        /// <summary>모든 슬롯 페이드 퇴장 (CG 진입 시).</summary>
        UniTask ExitAllAsync(CancellationToken ct = default);

        /// <summary>모든 슬롯 즉시 클리어 (세션/장면 종료).</summary>
        void ClearAll();

        /// <summary>지정 캐릭터가 현재 무대에 있는지 (Setup 매크로 조건).</summary>
        bool IsCharacterOnStage(string characterId);

        /// <summary>지정 슬롯의 표정 즉시 변경 (ScriptRunner emote 콜백).</summary>
        void ChangeEmote(string slotStr, string emote);

        /// <summary>CharFX(Shake/Jump/Dim) 실행 — ScreenFX 라우팅.</summary>
        UniTask ExecuteCharFXAsync(string effect, string[] parts, CancellationToken ct = default);
    }
}
