using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Stage
{
    /// <summary>
    /// 스테이지 모듈 진입점.
    /// StageManager 싱글톤을 IStage 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/StageModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class StageModule : MonoBehaviour, IStage
    {
        void Awake() => Services.Register<IStage>(this);

        void OnDestroy()
        {
            if (Services.TryGet<IStage>() == (IStage)this)
                Services.Unregister<IStage>();
        }

        StageManager Stage => StageManager.Instance;

        public BackgroundLayer Background => Stage?.Background;
        public VirtualBGOverlay VirtualBG => Stage?.VirtualBG;
        public CharacterLayer Character => Stage?.Character;
        public MonologueDim MonologueDim => Stage?.MonologueDim;
        public SDCutsceneLayer SDCutscene => Stage?.SDCutscene;
        public CGLayer CG => Stage?.CG;
        public EyeMask EyeMask => Stage?.EyeMask;

        public void CharacterEmote(string slot, string emote)
            => Stage?.CharacterEmote(slot, emote);

        public void ClearAllCharacters()
            => Stage?.ClearAllCharacters();
    }
}
