using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Core
{
    /// <summary>
    /// Stage 루트 프리팹 바인딩 (Background/Character/ScreenFX)
    /// </summary>
    public class StageRig : MonoBehaviour
    {
        [Header("Stage 루트")]
        [SerializeField] Canvas stageCanvas;

        [Header("레이어 바인딩")]
        [SerializeField] BackgroundLayer backgroundLayer;
        [SerializeField] VirtualBGOverlay virtualBGOverlay;
        [SerializeField] CharacterLayer characterLayer;
        [SerializeField] CGLayer cgLayer;
        [SerializeField] ScreenFX screenFX;

        public Canvas StageCanvas => stageCanvas;
        public BackgroundLayer Background => backgroundLayer;
        public VirtualBGOverlay VirtualBG => virtualBGOverlay;
        public CharacterLayer Character => characterLayer;
        public CGLayer CG => cgLayer;
        public ScreenFX ScreenFX => screenFX;

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
            if (cgLayer == null)
            {
                cgLayer = GetComponentInChildren<CGLayer>(true);
            }
            if (screenFX == null)
            {
                screenFX = GetComponentInChildren<ScreenFX>(true);
            }
        }
    }
}
