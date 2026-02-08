using UnityEngine;
using LoveAlgo.Story;
using LoveAlgo.Schedule;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 매니저 - 메인 UI들의 Show/Hide 관리
    /// 팝업은 PopupManager에서 별도 관리
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("메인 UI (인스펙터 바인딩)")]
        [SerializeField] DialogueUI dialogueUI;
        [SerializeField] ChoiceUI choiceUI;
        [SerializeField] ScheduleUI scheduleUI;
        [SerializeField] TitleUI titleUI;
        [SerializeField] UsernameUI usernameUI;

        // 외부 접근용 프로퍼티
        public DialogueUI DialogueUI => dialogueUI;
        public ChoiceUI ChoiceUI => choiceUI;
        public ScheduleUI ScheduleUI => scheduleUI;
        public TitleUI TitleUI => titleUI;
        public UsernameUI UsernameUI => usernameUI;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject);  // 데모: 단일 씬
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #region Dialogue UI

        public void ShowDialogue()
        {
            dialogueUI?.gameObject.SetActive(true);
        }

        public void HideDialogue()
        {
            dialogueUI?.gameObject.SetActive(false);
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
            }
        }

        #endregion
    }

    public enum MainUIType
    {
        Dialogue,
        Schedule,
        Title,
        Username
    }
}
