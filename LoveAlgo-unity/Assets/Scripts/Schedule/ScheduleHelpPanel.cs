using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 도움말 패널
    /// Open() / Close() 로 표시/숨김.
    /// closeButtons 배열에 닫힘 버튼을 여러 개 등록 가능.
    /// </summary>
    public class ScheduleHelpPanel : MonoBehaviour
    {
        [Header("닫힘 버튼 (여러 개 등록 가능)")]
        [SerializeField] Button[] closeButtons;

        void Awake()
        {
            foreach (var btn in closeButtons)
                btn?.onClick.AddListener(Close);

            gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>런타임에서 닫힘 버튼 추가</summary>
        public void AddCloseButton(Button btn)
        {
            if (btn == null) return;
            btn.onClick.AddListener(Close);
        }
    }
}
