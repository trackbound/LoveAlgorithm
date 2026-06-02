using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스테이지 레이어(CG/SD/Overlay) 연출 동결 수치 Definition SO(ADR-012). 종류별 페이드 기본 시간(초).
    /// 엔진(NarrativeController)이 참조해 CSV 미지정 fade를 동결값으로 해석한다. 런타임 읽기 전용.
    /// 값 출처 = docs/REWRITE_TUNING_VALUES.csv(CG 페이드 0.5·SD 페이드 0.5·오버레이 페이드 0.5).
    /// </summary>
    [CreateAssetMenu(fileName = "StageLayerTuning", menuName = "LoveAlgo/Stage Layer Tuning")]
    public class StageLayerTuningSO : ScriptableObject
    {
        [Tooltip("CG 페이드 기본 시간. 동결값 0.5s.")]
        [SerializeField] float cgFadeDefault = 0.5f;
        [Tooltip("SD 페이드 기본 시간. 동결값 0.5s.")]
        [SerializeField] float sdFadeDefault = 0.5f;
        [Tooltip("Overlay 페이드 기본 시간. 동결값 0.5s.")]
        [SerializeField] float overlayFadeDefault = 0.5f;

        public float CgFadeDefault => cgFadeDefault;
        public float SdFadeDefault => sdFadeDefault;
        public float OverlayFadeDefault => overlayFadeDefault;
    }
}
