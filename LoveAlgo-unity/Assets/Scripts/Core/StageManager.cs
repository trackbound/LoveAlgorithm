using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 연출 레이어 매니저 - 배경, 캐릭터, 화면 효과 관리
    /// 인스펙터에 StageRig 프리팹을 바인딩하면 첫 접근 시 자동 인스턴스화.
    /// ScriptRunner 등에서 StageManager.Instance.Background 등으로 접근.
    /// ScreenFX는 전역 오버레이라 씬에 미리 배치 (Bootstrap에서 활성화).
    /// </summary>
    public class StageManager : SingletonMonoBehaviour<StageManager>
    {
        [Header("Stage Rig 프리팩")]
        [Tooltip("씬에 이미 인스턴스가 있으면 그걸 우선 사용. 없으면 이 프리팩을 lazy-instantiate.")]
        [SerializeField] StageRig stageRigPrefab;

        [Header("Stage Root (인스턴스 부모, optional)")]
        [Tooltip("비워두면 이 GameObject 하위에 생성")]
        [SerializeField] Transform stageRoot;

        StageRig _stageRig;

        StageRig Rig
        {
            get
            {
                if (_stageRig == null)
                    _stageRig = SpawnOrFind();
                return _stageRig;
            }
        }

        // 외부 접근용 프로퍼티 - StageRig를 통해 접근
        public Canvas StageCanvas => Rig?.StageCanvas;
        public BackgroundLayer Background => Rig?.Background;
        public VirtualBGOverlay VirtualBG => Rig?.VirtualBG;
        public CharacterLayer Character => Rig?.Character;
        public MonologueDim MonologueDim => Rig?.MonologueDim;
        public SDCutsceneLayer SDCutscene => Rig?.SDCutscene;
        public CGLayer CG => Rig?.CG;
        public EyeMask EyeMask => Rig?.EyeMask;

        StageRig SpawnOrFind()
        {
            // 이미 씬에 있는 인스턴스 우선
            var existing = FindAnyObjectByType<StageRig>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            if (stageRigPrefab == null)
            {
                Debug.LogError("[StageManager] StageRig 프리팹이 인스펙터에 바인딩되지 않았고 씬에도 없습니다.");
                return null;
            }

            var parent = stageRoot != null ? stageRoot : transform;
            var inst = Instantiate(stageRigPrefab, parent);
            inst.name = stageRigPrefab.name; // (Clone) 제거
            return inst;
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
