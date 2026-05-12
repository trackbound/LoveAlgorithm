using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Core
{
    /// <summary>
    /// Stage 루트 프리팹 바인딩 (Background/Character 등 stage 레이어)
    /// ScreenFX는 전역 오버레이라 Stage 외부 — StageManager에서 별도 관리
    /// </summary>
    public class StageRig : MonoBehaviour
    {
        [Header("Stage 루트")]
        [SerializeField] Canvas stageCanvas;

        [Header("레이어 바인딩")]
        [SerializeField] BackgroundLayer backgroundLayer;
        [SerializeField] VirtualBGOverlay virtualBGOverlay;
        [SerializeField] CharacterLayer characterLayer;
        [SerializeField] MonologueDim monologueDim;
        [SerializeField] SDCutsceneLayer sdCutsceneLayer;
        [SerializeField] CGLayer cgLayer;
        [Tooltip("눈 감기/뜨기 연출 마스크. Stage 하위에 두어 BG/캐릭터는 가리되 대화창은 그대로 표시.")]
        [SerializeField] EyeMask eyeMask;

        public Canvas StageCanvas => stageCanvas;
        public BackgroundLayer Background => backgroundLayer;
        public VirtualBGOverlay VirtualBG => virtualBGOverlay;
        public CharacterLayer Character => characterLayer;
        public MonologueDim MonologueDim => monologueDim;
        public SDCutsceneLayer SDCutscene => sdCutsceneLayer;
        public CGLayer CG => cgLayer;
        public EyeMask EyeMask => eyeMask;

        void Awake()
        {
            // 런타임 안전망: 프리팹이 lazy-instantiate된 경우 OnValidate가 실행되지 않아
            // serialized 필드가 비어있을 수 있음 → 자식에서 한 번 더 자동 바인딩
            AutoBind();
        }

        void OnValidate()
        {
            AutoBind();
        }

        /// <summary>
        /// 자식에서 자동 바인딩
        /// </summary>
        public void AutoBind()
        {
            if (stageCanvas == null)
            {
                stageCanvas = GetComponentInChildren<Canvas>(true);
            }
            if (backgroundLayer == null)
            {
                backgroundLayer = GetComponentInChildren<BackgroundLayer>(true);
            }
            if (virtualBGOverlay == null)
            {
                virtualBGOverlay = GetComponentInChildren<VirtualBGOverlay>(true);
            }
            if (characterLayer == null)
            {
                characterLayer = GetComponentInChildren<CharacterLayer>(true);
            }
            if (monologueDim == null)
            {
                monologueDim = GetComponentInChildren<MonologueDim>(true);
            }
            if (sdCutsceneLayer == null)
            {
                sdCutsceneLayer = GetComponentInChildren<SDCutsceneLayer>(true);
            }
            if (cgLayer == null)
            {
                cgLayer = GetComponentInChildren<CGLayer>(true);
            }
            if (eyeMask == null)
            {
                eyeMask = GetComponentInChildren<EyeMask>(true);
            }
        }
    }
}
