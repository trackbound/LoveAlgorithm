using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 카메라 FX 연출 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). 줌/팬/리셋 기본 시간(초).
    /// 엔진(NarrativeController)이 참조해 CSV가 duration을 생략한 경우(인텐트 Duration&lt;0) 이 값으로 해석한다.
    /// 값 출처 = docs/REWRITE_TUNING_VALUES.csv(줌 0.5s, 팬 0.5s, 리셋 0.4s). 런타임 읽기 전용.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraTuning", menuName = "LoveAlgo/Camera Tuning")]
    public class CameraTuningSO : ScriptableObject
    {
        [Tooltip("CamZoom 기본 시간. 동결값 0.5s.")]
        [SerializeField] float zoomDefault = 0.5f;
        [Tooltip("CamPan 기본 시간. 동결값 0.5s.")]
        [SerializeField] float panDefault = 0.5f;
        [Tooltip("CamReset 기본 시간. 동결값 0.4s.")]
        [SerializeField] float resetDefault = 0.4f;

        public float ZoomDefault => zoomDefault;
        public float PanDefault => panDefault;
        public float ResetDefault => resetDefault;
    }
}
