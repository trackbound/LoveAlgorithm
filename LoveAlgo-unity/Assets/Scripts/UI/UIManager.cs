using UnityEngine;
using LoveAlgo.Story;
using LoveAlgo.Schedule;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 매니저 - 메인 UI들의 Show/Hide 관리
    /// 팝업은 PopupManager에서 별도 관리
    /// </summary>
    public class UIManager : SingletonMonoBehaviour<UIManager>
    {
        [Header("메인 UI (인스펙터 바인딩)")]
        [SerializeField] DialogueUI dialogueUI;
        [SerializeField] ChoiceUI choiceUI;
        [SerializeField] ScheduleUI scheduleUI;
        [SerializeField] TitleUI titleUI;
        [SerializeField] UsernameUI usernameUI;
        [SerializeField] PlaceUI placeUI;

        public DialogueUI DialogueUI => dialogueUI;
        public ChoiceUI ChoiceUI => choiceUI;
        public ScheduleUI ScheduleUI => scheduleUI;
        public TitleUI TitleUI => titleUI;
        public UsernameUI UsernameUI => usernameUI;
        public PlaceUI PlaceUI => placeUI;

        void SetMainUIActive(MonoBehaviour ui, bool active)
        {
            if (ui != null)
                ui.gameObject.SetActive(active);
        }

        /// <summary>
        /// 모든 메인 UI 숨기기
        /// </summary>
        public void HideAll()
        {
            SetMainUIActive(dialogueUI, false);
            SetMainUIActive(scheduleUI, false);
            SetMainUIActive(titleUI, false);
            SetMainUIActive(usernameUI, false);
            placeUI?.HideImmediate();
        }

        /// <summary>
        /// 특정 UI만 표시 (나머지 숨김)
        /// </summary>
        public void ShowOnly(MainUIType type)
        {
            HideAll();

            switch (type)
            {
                case MainUIType.Dialogue:
                case MainUIType.Ending:
                    // 엔딩은 DialogueUI를 재사용한다.
                    SetMainUIActive(dialogueUI, true);
                    break;
                case MainUIType.Schedule:
                    SetMainUIActive(scheduleUI, true);
                    break;
                case MainUIType.Title:
                    SetMainUIActive(titleUI, true);
                    break;
                case MainUIType.Username:
                    SetMainUIActive(usernameUI, true);
                    break;
            }
        }
    }

    public enum MainUIType
    {
        Dialogue,
        Schedule,
        Title,
        Username,
        Ending
    }
}
