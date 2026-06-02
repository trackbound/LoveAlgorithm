using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스테이지 연출 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). BG/Char 페이드 기본 시간(초)을 담는다.
    /// 엔진(NarrativeController)이 참조해 CSV가 duration을 생략한 경우(인텐트 Duration&lt;0) 이 값으로 해석한 뒤
    /// 명령에 실어 발행한다. 값 출처 = docs/REWRITE_TUNING_VALUES.csv(Stage 영역). 런타임 읽기 전용(불변).
    /// </summary>
    [CreateAssetMenu(fileName = "StageTuning", menuName = "LoveAlgo/Stage Tuning")]
    public class StageTuningSO : ScriptableObject
    {
        [Header("배경 (s)")]
        [Tooltip("BG 전환 기본 시간(Fade/Cross). 동결값 0.5s.")]
        [SerializeField] float bgTransitionDefault = 0.5f;

        [Header("캐릭터 (s)")]
        [Tooltip("캐릭터 등장 페이드 기본. 동결값 0.5s.")]
        [SerializeField] float charEnterDefault = 0.5f;
        [Tooltip("캐릭터 퇴장 페이드 기본. 동결값 0.4s.")]
        [SerializeField] float charExitDefault = 0.4f;
        [Tooltip("표정 변경 페이드 기본. 동결값 0.25s.")]
        [SerializeField] float charEmoteDefault = 0.25f;

        public float BgTransitionDefault => bgTransitionDefault;
        public float CharEnterDefault => charEnterDefault;
        public float CharExitDefault => charExitDefault;
        public float CharEmoteDefault => charEmoteDefault;
    }
}
