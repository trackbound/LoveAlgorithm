using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 팝업 — 채팅 스타일
    /// 연속 같은 화자의 대사를 하나의 그룹으로 묶어 표시
    /// </summary>
    public class LogPopup : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] Button closeButton;

        [Header("프리팹")]
        [SerializeField] LogEntryUI entryPrefab;

        [Header("스프라이트")]
        [SerializeField] Sprite characterTextboxSprite;    // 캐릭터 말풍선 배경
        [SerializeField] Sprite userTextboxSprite;         // 주인공 말풍선 배경

        [Header("독백 스타일")]
        [SerializeField] TMP_FontAsset narrationFont;      // Aggro Light

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
            LogEntryUI currentGroup = null;
            string prevSpeaker = null;
            string prevCharId = null;

            for (int i = 0; i < log.Count; i++)
            {
                var entry = log[i];
                bool isNarration = string.IsNullOrEmpty(entry.Speaker);

                // 같은 화자 연속 → 기존 그룹에 대사만 추가
                bool sameGroup = currentGroup != null
                    && entry.Speaker == prevSpeaker
                    && entry.CharacterId == prevCharId;

                if (!sameGroup)
                {
                    // 새 그룹 생성
                    currentGroup = Instantiate(entryPrefab, contentRoot);
                    currentGroup.SetAssets(
                        characterTextboxSprite, userTextboxSprite,
                        narrationFont);

                    if (isNarration)
                    {
                        // 케이스 4: 독백
                        currentGroup.SetNarrationMode();
                    }
                    else if (!string.IsNullOrEmpty(entry.CharacterId))
                    {
                        // 케이스 1 or 2: 캐릭터 (초상화 유무)
                        var portrait = GetPortrait(entry.CharacterId);
                        if (portrait != null)
                            currentGroup.SetCharacterWithPortrait(entry.Speaker, portrait);
                        else
                            currentGroup.SetExtraEntry(entry.Speaker);
                    }
                    else
                    {
                        // 케이스 3: 주인공
                        currentGroup.SetUserEntry(entry.Speaker);
                    }

                    prevSpeaker = entry.Speaker;
                    prevCharId = entry.CharacterId;
                    spawnedEntries.Add(currentGroup);
                }

                // 대사 버블 추가
                currentGroup.AddLine(entry.Text);
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
