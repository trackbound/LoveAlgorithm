using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 팝업 — 채팅 스타일
    /// 캐릭터: 좌측 썸네일 + 이름표 + 말풍선
    /// 주인공/나레이션: 이름표(다른 색상) + 말풍선(다른 색상)
    /// </summary>
    public class LogPopup : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform contentRoot;   // ScrollRect.content
        [SerializeField] Button closeButton;

        [Header("프리팹")]
        [SerializeField] LogEntryUI entryPrefab;      // 로그 항목 프리팹

        [Header("스프라이트 (텍스트박스 배경)")]
        [SerializeField] Sprite characterTextboxSprite;   // bg_log_textbox_character
        [SerializeField] Sprite userTextboxSprite;        // bg_log_textbox_user

        [Header("캐릭터 초상화")]
        [SerializeField] List<PortraitEntry> portraits;   // Inspector에서 할당

        [Header("빈 메시지")]
        [SerializeField] GameObject emptyMessage;         // "(로그가 없습니다)" 오브젝트

        // 캐릭터 초상화 룩업 (characterId → Sprite)
        readonly Dictionary<string, Sprite> portraitLookup = new();

        // 생성된 항목 인스턴스
        readonly List<LogEntryUI> spawnedEntries = new();

        public bool IsVisible => gameObject.activeSelf;

        void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            gameObject.SetActive(false);

            // 초상화 룩업 빌드
            if (portraits != null)
            {
                foreach (var p in portraits)
                {
                    if (p != null && !string.IsNullOrEmpty(p.characterId) && p.sprite != null)
                        portraitLookup[p.characterId.ToLower()] = p.sprite;
                }
            }
        }

        /// <summary>
        /// 로그 표시
        /// </summary>
        public void Show(IReadOnlyList<DialogueLogEntry> log)
        {
            // 기존 항목 제거
            ClearEntries();

            bool hasEntries = log != null && log.Count > 0;

            if (emptyMessage != null)
                emptyMessage.SetActive(!hasEntries);

            if (hasEntries)
            {
                BuildEntries(log);
            }

            gameObject.SetActive(true);

            // 스크롤 맨 아래로
            ScrollToBottom();
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

        void BuildEntries(IReadOnlyList<DialogueLogEntry> log)
        {
            for (int i = 0; i < log.Count; i++)
            {
                var entry = log[i];
                var item = Instantiate(entryPrefab, contentRoot);
                item.SetAssets(characterTextboxSprite, userTextboxSprite);

                if (string.IsNullOrEmpty(entry.Speaker))
                {
                    // 나레이션
                    item.SetNarrationEntry(entry.Text);
                }
                else if (!string.IsNullOrEmpty(entry.CharacterId))
                {
                    // 캐릭터 대사
                    var portrait = GetPortrait(entry.CharacterId);
                    item.SetCharacterEntry(entry.Speaker, entry.Text, portrait);
                }
                else
                {
                    // 주인공 대사 (CharacterId 없음 = 데이터베이스에 없는 화자)
                    item.SetUserEntry(entry.Speaker, entry.Text);
                }

                spawnedEntries.Add(item);
            }
        }

        Sprite GetPortrait(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;

            portraitLookup.TryGetValue(characterId.ToLower(), out var sprite);
            return sprite;
        }

        void ClearEntries()
        {
            foreach (var entry in spawnedEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            spawnedEntries.Clear();
        }

        void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    /// <summary>
    /// Inspector에서 캐릭터ID → 초상화 스프라이트 매핑
    /// </summary>
    [System.Serializable]
    public class PortraitEntry
    {
        public string characterId;   // "bom", "daeun", "heewon", "roa", "yeun"
        public Sprite sprite;        // log_portrait_xxx
    }
}
