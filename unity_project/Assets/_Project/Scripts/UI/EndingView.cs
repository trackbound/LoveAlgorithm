using LoveAlgo.Core; // GameStateSO
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 엔딩 화면 뷰(Ending 그룹). 화면 전환은 PhaseController→UIManager가 그룹 토글로 처리하므로(ADR-013) 이 뷰는
    /// 표시만 — Ending 그룹이 활성화되며 <see cref="OnEnable"/>이 불리면 현재 일차로 결과 텍스트를 구성한다
    /// (state 동기 읽기, ADR-007). 구 EnteredEndingEvent 구독·자체 루트 토글은 제거(그룹 시스템 일원화로 "두 화면
    /// 동시 active" 버그 해소). 30일 루프 종료점 — 엔딩 분기·화려한 연출은 범위 밖.
    /// </summary>
    public class EndingView : MonoBehaviour
    {
        [Tooltip("도달 일차 표시용 상태 SO. 인스펙터/부팅 주입.")]
        [SerializeField] GameStateSO state;
        [SerializeField] TMP_Text resultText;

        public GameStateSO State { get => state; set => state = value; }
        public TMP_Text ResultText { get => resultText; set => resultText = value; }

        /// <summary>엔딩 화면이 현재 표시 중인가(Ending 그룹 활성 = 이 뷰 활성).</summary>
        public bool IsShown => isActiveAndEnabled;

        void OnEnable()
        {
            // 엔딩 진입 시 state.Day = MaxDay+1(DayLoop.AdvanceDay가 경계에서도 day++) → 도달 일차는 그 직전.
            if (resultText != null && state != null)
                resultText.text = $"{state.Day - 1}일의 여정이 끝났습니다.";
        }
    }
}
