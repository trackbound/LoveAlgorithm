using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 팝업
    /// </summary>
    public class LogPopup : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] TMP_Text logText;
        [SerializeField] Button closeButton;

        [Header("설정")]
        [SerializeField] string speakerFormat = "<b>{0}</b>\n";  // 화자 포맷
        [SerializeField] string narrationPrefix = "";            // 나레이션 접두사
        [SerializeField] string entrySeparator = "\n\n";         // 항목 구분자

        public bool IsVisible => gameObject.activeSelf;

        void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 로그 표시
        /// </summary>
        public void Show(IReadOnlyList<DialogueLogEntry> log)
        {
            if (log == null || log.Count == 0)
            {
                logText.text = "(로그가 없습니다)";
            }
            else
            {
                logText.text = BuildLogText(log);
            }

            gameObject.SetActive(true);

            // 스크롤 맨 아래로
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 로그 닫기
        /// </summary>
        public void Close()
        {
            Hide();
        }

        /// <summary>
        /// 로그 숨기기
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        string BuildLogText(IReadOnlyList<DialogueLogEntry> log)
        {
            var sb = new System.Text.StringBuilder();

            foreach (var entry in log)
            {
                if (string.IsNullOrEmpty(entry.Speaker))
                {
                    // 나레이션
                    sb.Append(narrationPrefix);
                    sb.Append(entry.Text);
                }
                else
                {
                    // 캐릭터 대사
                    sb.AppendFormat(speakerFormat, entry.Speaker);
                    sb.Append(entry.Text);
                }
                sb.Append(entrySeparator);
            }

            return sb.ToString();
        }
    }
}
