using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 아이마스크 연출 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). 눈감기/뜨기·깜빡임 기본 지속(초).
    /// 엔진(NarrativeController)이 참조해 CSV 미지정 지속을 동결값으로 해석한다. 런타임 읽기 전용.
    /// 값 출처 = docs/REWRITE_TUNING_VALUES.csv(눈감기·뜨기 0.8s, 깜빡 감기 0.1·뜨기 0.15·유지 0.05).
    /// </summary>
    [CreateAssetMenu(fileName = "EyeMaskTuning", menuName = "LoveAlgo/Eye Mask Tuning")]
    public class EyeMaskTuningSO : ScriptableObject
    {
        [Tooltip("EyeClose 기본 지속. 동결값 0.8s.")]
        [SerializeField] float closeDefault = 0.8f;
        [Tooltip("EyeOpen 기본 지속. 동결값 0.8s.")]
        [SerializeField] float openDefault = 0.8f;
        [Tooltip("EyeBlink 감기 기본 지속. 동결값 0.1s.")]
        [SerializeField] float blinkCloseDefault = 0.1f;
        [Tooltip("EyeBlink 뜨기 기본 지속. 동결값 0.15s.")]
        [SerializeField] float blinkOpenDefault = 0.15f;
        [Tooltip("EyeBlink 감은 채 유지 기본 시간. 동결값 0.05s.")]
        [SerializeField] float blinkHoldDefault = 0.05f;

        public float CloseDefault => closeDefault;
        public float OpenDefault => openDefault;
        public float BlinkCloseDefault => blinkCloseDefault;
        public float BlinkOpenDefault => blinkOpenDefault;
        public float BlinkHoldDefault => blinkHoldDefault;
    }
}
