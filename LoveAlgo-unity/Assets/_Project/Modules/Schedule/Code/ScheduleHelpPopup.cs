using LoveAlgo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 도움말 팝업. PopupBase 통합 흐름 사용 (Layer=Top 권장).
    /// closeButtons 배열에 닫힘 버튼을 여러 개 등록 가능.
    /// </summary>
    public class ScheduleHelpPopup : PopupBase
    {
        [Header("닫힘 버튼 (여러 개 등록 가능)")]
        [SerializeField] Button[] closeButtons;

        protected override void Awake()
        {
            base.Awake();
            if (closeButtons != null)
            {
                foreach (var btn in closeButtons)
                    btn?.onClick.AddListener(Close);
            }
            gameObject.SetActive(false);
        }

        /// <summary>외부 호출 진입점 (기존 Open API 유지).</summary>
        public void Open() => Show();

        /// <summary>런타임에서 닫힘 버튼 추가</summary>
        public void AddCloseButton(Button btn)
        {
            if (btn == null) return;
            btn.onClick.AddListener(Close);
        }
    }
}
