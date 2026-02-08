using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 팝업 내 개별 대사 항목 UI
    /// 캐릭터 대사: 썸네일 + 이름표 + 말풍선
    /// 주인공/나레이션: 이름표(색상 다름) + 말풍선(색상 다름)
    /// </summary>
    public class LogEntryUI : MonoBehaviour
    {
        [Header("공통")]
        [SerializeField] TMP_Text messageText;

        [Header("캐릭터 대사 (좌측 썸네일)")]
        [SerializeField] GameObject portraitGroup;       // 썸네일+이름 묶음
        [SerializeField] Image portraitImage;
        [SerializeField] TMP_Text portraitNameText;
        [SerializeField] Image portraitNameBG;           // bg_log_namebox_character

        [Header("주인공 대사 (썸네일 없음)")]
        [SerializeField] GameObject userNameGroup;       // 주인공 이름 묶음
        [SerializeField] TMP_Text userNameText;
        [SerializeField] Image userNameBG;               // bg_log_namebox_user

        [Header("말풍선 배경")]
        [SerializeField] Image textboxBG;                // 스프라이트 교체용

        // 에셋 (LogPopup에서 주입)
        Sprite characterTextboxSprite;
        Sprite userTextboxSprite;

        /// <summary>
        /// 에셋 세팅 (풀링/재사용 시 한 번만)
        /// </summary>
        public void SetAssets(Sprite charTextbox, Sprite userTextbox)
        {
            characterTextboxSprite = charTextbox;
            userTextboxSprite = userTextbox;
        }

        /// <summary>
        /// 캐릭터 대사 설정
        /// </summary>
        public void SetCharacterEntry(string displayName, string text, Sprite portrait)
        {
            // 캐릭터 모드: 썸네일 + 캐릭터 이름표 표시
            if (portraitGroup != null) portraitGroup.SetActive(true);
            if (userNameGroup != null) userNameGroup.SetActive(false);

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = portrait != null;
            }

            if (portraitNameText != null)
                portraitNameText.text = displayName;

            if (messageText != null)
                messageText.text = text;

            // 말풍선 배경: 캐릭터용
            if (textboxBG != null && characterTextboxSprite != null)
                textboxBG.sprite = characterTextboxSprite;
        }

        /// <summary>
        /// 주인공 대사 설정
        /// </summary>
        public void SetUserEntry(string displayName, string text)
        {
            // 주인공 모드: 썸네일 없음, 주인공 이름표
            if (portraitGroup != null) portraitGroup.SetActive(false);
            if (userNameGroup != null) userNameGroup.SetActive(true);

            if (userNameText != null)
                userNameText.text = displayName;

            if (messageText != null)
                messageText.text = text;

            // 말풍선 배경: 주인공용
            if (textboxBG != null && userTextboxSprite != null)
                textboxBG.sprite = userTextboxSprite;
        }

        /// <summary>
        /// 나레이션(독백) 설정 — 이름 없음
        /// </summary>
        public void SetNarrationEntry(string text)
        {
            // 나레이션: 썸네일/이름 없음
            if (portraitGroup != null) portraitGroup.SetActive(false);
            if (userNameGroup != null) userNameGroup.SetActive(true);

            // 이름 비움 (독백은 이름 없이 텍스트만)
            if (userNameText != null)
                userNameText.text = "";
            if (userNameBG != null)
                userNameBG.enabled = false;

            if (messageText != null)
                messageText.text = text;

            // 말풍선 배경: 주인공/나레이션용
            if (textboxBG != null && userTextboxSprite != null)
                textboxBG.sprite = userTextboxSprite;
        }
    }
}
