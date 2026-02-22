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

        // 외부 접근용 프로퍼티
        public DialogueUI DialogueUI => dialogueUI;
        public ChoiceUI ChoiceUI => choiceUI;
        public ScheduleUI ScheduleUI => scheduleUI;
        public TitleUI TitleUI => titleUI;
        public UsernameUI UsernameUI => usernameUI;
        public PlaceUI PlaceUI => placeUI;

        #region Dialogue UI

        public void ShowDialogue()
        {
            dialogueUI?.ShowImmediate();
        }

        public void HideDialogue()
        {
            dialogueUI?.HideImmediate();
        }

        public bool IsDialogueVisible => dialogueUI != null && dialogueUI.gameObject.activeSelf;

        #endregion

        #region Schedule UI

        public void ShowSchedule()
        {
            scheduleUI?.gameObject.SetActive(true);
        }

        public void HideSchedule()
        {
            scheduleUI?.gameObject.SetActive(false);
        }

        public bool IsScheduleVisible => scheduleUI != null && scheduleUI.gameObject.activeSelf;

        #endregion

        #region Title UI

        public void ShowTitle()
        {
            titleUI?.gameObject.SetActive(true);
        }

        public void HideTitle()
        {
            titleUI?.gameObject.SetActive(false);
        }

        public bool IsTitleVisible => titleUI != null && titleUI.gameObject.activeSelf;

        #endregion

        #region Username UI

        public void ShowUsername()
        {
            usernameUI?.gameObject.SetActive(true);
        }

        public void HideUsername()
        {
            usernameUI?.gameObject.SetActive(false);
        }

        public bool IsUsernameVisible => usernameUI != null && usernameUI.gameObject.activeSelf;

        #endregion

        #region 유틸

        /// <summary>
        /// 모든 메인 UI 숨기기
        /// </summary>
        public void HideAll()
        {
            HideDialogue();
            HideSchedule();
            HideTitle();
            HideUsername();
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
                    ShowDialogue();
                    break;
                case MainUIType.Schedule:
                    ShowSchedule();
                    break;
                case MainUIType.Title:
                    ShowTitle();
                    break;
                case MainUIType.Username:
                    ShowUsername();
                    break;
                case MainUIType.Ending:
                    // 엔딩은 DialogueUI를 재사용 (엔딩 스크립트 재생)
                    ShowDialogue();
                    break;
            }
        }

        #endregion
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
