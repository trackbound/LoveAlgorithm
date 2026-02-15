using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 연출 레이어 매니저 - 배경, 캐릭터, 화면 효과 관리
    /// ScriptRunner 등에서 StageManager.Instance.Background 등으로 접근
    /// </summary>
    public class StageManager : SingletonMonoBehaviour<StageManager>
    {

        [Header("Stage Rig (레이어 바인딩은 StageRig에서)")]
        [SerializeField] StageRig stageRig;

        // 외부 접근용 프로퍼티 - StageRig를 통해 접근
        public BackgroundLayer Background => stageRig?.Background;
        public VirtualBGOverlay VirtualBG => stageRig?.VirtualBG;
        public CharacterLayer Character => stageRig?.Character;
        public MonologueDim MonologueDim => stageRig?.MonologueDim;
        public SDCutsceneLayer SDCutscene => stageRig?.SDCutscene;
        public CGLayer CG => stageRig?.CG;

        protected override void OnSingletonAwake()
        {
            FindStageRig();
        }

        void OnValidate()
        {
            FindStageRig();
        }

        void FindStageRig()
        {
            if (stageRig == null)
            {
                stageRig = GetComponentInChildren<StageRig>(true);
            }
        }

        /// <summary>
        /// 캐릭터 표정 변경 (편의 메서드)
        /// </summary>
        public void CharacterEmote(string slot, string emote)
        {
            Character?.ChangeEmote(slot, emote);
        }

        /// <summary>
        /// 모든 캐릭터 퇴장
        /// </summary>
        public void ClearAllCharacters()
        {
            Character?.ClearAll();
        }
    }
}
