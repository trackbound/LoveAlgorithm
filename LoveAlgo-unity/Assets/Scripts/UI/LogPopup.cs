using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 팝업 — 채팅 스타일
    /// 연속 같은 화자의 대사를 하나의 그룹으로 묶어 표시
    ///
    /// 최적화: 증분 빌드 — 이전 Show 이후 추가된 로그만 생성
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
        [SerializeField] Sprite characterTextboxSprite;
        [SerializeField] Sprite userTextboxSprite;

        [Header("독백 스타일")]
        [SerializeField] TMP_FontAsset narrationFont;

        [Header("캐릭터 초상화")]
        [SerializeField] List<PortraitEntry> portraits;

        [Header("빈 메시지")]
        [SerializeField] GameObject emptyMessage;

        // 초상화 룩업
        readonly Dictionary<string, Sprite> portraitLookup = new();

        // 생성된 항목
        readonly List<LogEntryUI> spawnedEntries = new();

        // 증분 빌드 상태
        int builtCount;              // 이미 빌드한 로그 수
        string lastSpeaker;          // 마지막 그룹의 화자
        string lastCharId;           // 마지막 그룹의 캐릭터ID
        LogEntryUI lastGroup;        // 마지막 그룹 (연속 대사 추가용)

        public bool IsVisible => gameObject.activeSelf;

        void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            gameObject.SetActive(false);

            if (portraits != null)
            {
                foreach (var p in portraits)
                {
                    if (p != null && !string.IsNullOrEmpty(p.characterId) && p.sprite != null)
                        portraitLookup[p.characterId.ToLower()] = p.sprite;
                }
            }
        }

        /// <summary>로그 표시 (증분)</summary>
        public void Show(IReadOnlyList<DialogueLogEntry> log)
        {
            bool hasEntries = log != null && log.Count > 0;

            if (emptyMessage != null)
                emptyMessage.SetActive(!hasEntries);

            if (hasEntries)
                BuildIncremental(log);

            gameObject.SetActive(true);

            // 다음 프레임에 스크롤 (레이아웃 계산 대기)
            ScrollToBottomAsync().Forget();
        }

        public void Close() => Hide();

        public void Hide() => gameObject.SetActive(false);

        /// <summary>증분 빌드 — 새 항목만 추가</summary>
        void BuildIncremental(IReadOnlyList<DialogueLogEntry> log)
        {
            // 로그가 줄었으면 전체 리빌드 (undo/reset 대응)
            if (log.Count < builtCount)
            {
                ClearEntries();
            }

            int startIdx = builtCount;

            for (int i = startIdx; i < log.Count; i++)
            {
                var entry = log[i];
                bool isNarration = string.IsNullOrEmpty(entry.Speaker);

                bool sameGroup = lastGroup != null
                    && entry.Speaker == lastSpeaker
                    && entry.CharacterId == lastCharId;

                if (!sameGroup)
                {
                    lastGroup = Instantiate(entryPrefab, contentRoot);
                    lastGroup.SetAssets(
                        characterTextboxSprite, userTextboxSprite,
                        narrationFont);

                    if (isNarration)
                    {
                        lastGroup.SetNarrationMode();
                    }
                    else if (!string.IsNullOrEmpty(entry.CharacterId))
                    {
                        var portrait = GetPortrait(entry.CharacterId);
                        if (portrait != null)
                            lastGroup.SetCharacterWithPortrait(entry.Speaker, portrait);
                        else
                            lastGroup.SetExtraEntry(entry.Speaker);
                    }
                    else
                    {
                        lastGroup.SetUserEntry(entry.Speaker);
                    }

                    lastSpeaker = entry.Speaker;
                    lastCharId = entry.CharacterId;
                    spawnedEntries.Add(lastGroup);
                }

                lastGroup.AddLine(entry.Text);
            }

            builtCount = log.Count;
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
            builtCount = 0;
            lastGroup = null;
            lastSpeaker = null;
            lastCharId = null;
        }

        async UniTaskVoid ScrollToBottomAsync()
        {
            if (scrollRect == null) return;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    [System.Serializable]
    public class PortraitEntry
    {
        public string characterId;
        public Sprite sprite;
    }
}
