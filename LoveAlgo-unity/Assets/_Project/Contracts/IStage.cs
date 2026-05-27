using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 스테이지 모듈 외부 계약.
    /// 배경·캐릭터·CG·FX 레이어를 노출.
    /// 구현: <see cref="LoveAlgo.Stage.StageModule"/>.
    /// </summary>
    public interface IStage
    {
        Canvas StageCanvas { get; }
        BackgroundLayer Background { get; }
        IVirtualBGOverlay VirtualBG { get; }
        CharacterLayer Character { get; }
        IMonologueDim MonologueDim { get; }
        ISDCutsceneLayer SDCutscene { get; }
        ICGLayer CG { get; }
        IEyeMask EyeMask { get; }

        // Phase B-8 cleanup: CharacterStage / ClearAllCharacters 외부 호출자 0 — 제거.
        //   - CharacterStageDatabase 접근은 Stage 모듈 내부 (CharacterSlot) 만이라 StageModule
        //     concrete property 로만 유지 (인터페이스 비노출).
        //   - ClearAllCharacters 는 ScriptRunner 등 외부 호출 0 — Character.ClearAll 직접 호출
        //     으로 충분.

        /// <summary>캐릭터 슬롯에 이모트 적용 (ScriptRunner emote 콜백 라우팅).</summary>
        void CharacterEmote(string slot, string emote);
    }
}
