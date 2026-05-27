using LoveAlgo.Core;
using LoveAlgo.Stage;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 스테이지 모듈 외부 계약.
    /// 배경·캐릭터·CG·FX 레이어와 시각 트랜스폼 DB를 노출.
    /// 구현: <see cref="StageModule"/>.
    /// </summary>
    public interface IStage
    {
        Canvas StageCanvas { get; }
        BackgroundLayer Background { get; }
        VirtualBGOverlay VirtualBG { get; }
        CharacterLayer Character { get; }
        IMonologueDim MonologueDim { get; }
        SDCutsceneLayer SDCutscene { get; }
        CGLayer CG { get; }
        IEyeMask EyeMask { get; }
        CharacterStageDatabase CharacterStage { get; }

        /// <summary>캐릭터 슬롯에 이모트 적용.</summary>
        void CharacterEmote(string slot, string emote);

        /// <summary>모든 캐릭터 슬롯 클리어.</summary>
        void ClearAllCharacters();
    }
}
