using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스크린 페이드 연출 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). 페이드/플래시 기본 시간(초).
    /// 엔진(NarrativeController)이 참조해 CSV가 duration을 생략한 경우(인텐트 Duration&lt;0) 이 값으로 해석한다.
    /// 값 출처 = docs/REWRITE_TUNING_VALUES.csv(화면 페이드 0.9s, 플래시 0.14s). 런타임 읽기 전용.
    /// </summary>
    [CreateAssetMenu(fileName = "ScreenFadeTuning", menuName = "LoveAlgo/Screen Fade Tuning")]
    public class ScreenFadeTuningSO : ScriptableObject
    {
        [Tooltip("FadeOut/FadeIn 기본 시간. 동결값 0.9s.")]
        [SerializeField] float fadeDefault = 0.9f;
        [Tooltip("Flash 기본 시간(0→1→0 왕복 총합). 동결값 0.14s.")]
        [SerializeField] float flashDefault = 0.14f;

        public float FadeDefault => fadeDefault;
        public float FlashDefault => flashDefault;
    }
}
