using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 캐릭터 슬롯 외부 계약 (Phase B-8c-2).
    /// 구현: <see cref="LoveAlgo.Story.CharacterSlot"/>.
    ///
    /// 외부 호출자(SaveDataSerializer/StageRestorer) 가 ICharacterLayer.GetSlot 반환받아
    /// 세이브 직렬화/복원에 사용. EmoteAsync/ExitAsync/Shake/Jump/Glitch/Dim 같은 슬롯
    /// 동작 표면은 CharacterLayer 내부에서만 사용 — 인터페이스 비노출.
    /// </summary>
    public interface ICharacterSlot
    {
        /// <summary>슬롯이 비어있는지 (캐릭터 없음).</summary>
        bool IsEmpty { get; }

        /// <summary>현재 슬롯 캐릭터 ID (세이브용).</summary>
        string CurrentCharacter { get; }

        /// <summary>현재 표정 (세이브용).</summary>
        string CurrentEmote { get; }

        /// <summary>로드 복원 시 캐릭터 + 표정 즉시 진입.</summary>
        UniTask EnterAsync(string characterName, string emote = "Default", CancellationToken ct = default);
    }
}
