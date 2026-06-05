using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 위치 배너(Place) 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). 등장/유지/퇴장 지속(초).
    /// 엔진(NarrativeController)이 참조해 ShowPlaceCommand에 실어 발행한다. 런타임 읽기 전용.
    /// 값 출처 = 구 PlaceNotification + docs/REWRITE_TUNING_VALUES.csv(등장 0.45·유지 2.0·퇴장 0.35s).
    /// </summary>
    [CreateAssetMenu(fileName = "PlaceTuning", menuName = "LoveAlgo/Place Tuning")]
    public class PlaceTuningSO : ScriptableObject
    {
        [Tooltip("배너 등장(페이드 인) 지속. 동결값 0.45s.")]
        [SerializeField] float enterDuration = 0.45f;
        [Tooltip("배너 기본 유지 지속. 동결값 2.0s.")]
        [SerializeField] float holdDuration = 2.0f;
        [Tooltip("배너 퇴장(페이드 아웃) 지속. 동결값 0.35s.")]
        [SerializeField] float exitDuration = 0.35f;

        public float EnterDuration => enterDuration;
        public float HoldDuration => holdDuration;
        public float ExitDuration => exitDuration;
    }
}
