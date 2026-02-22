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
            // gameObject.SetActive(false)은 PopupManager.InitPopups()에서 처리
            // 여기서 호출하면: 씬에서 비활성화 시작 → 첫 Show() → SetActive(true) → Awake 실행 → 다시 꺼짐 버그 발생

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

            // 먼저 활성화해야 Instantiate 시 레이아웃 계산이 정상 작동
            gameObject.SetActive(true);

            if (hasEntries)
                BuildIncrementalAsync(log).Forget();
            else
                ScrollToBottomAsync().Forget();
        }

        public void Close() => Hide();

        public void Hide() => gameObject.SetActive(false);

        /// <summary>증분 빌드 — 새 항목만 추가 (비동기: 프레임 분산)</summary>
        async UniTaskVoid BuildIncrementalAsync(IReadOnlyList<DialogueLogEntry> log)
        {
            // 로그가 줄었으면 전체 리빌드 (undo/reset 대응)
            if (log.Count < builtCount)
            {
                ClearEntries();
            }

            int startIdx = builtCount;
            int batchSize = 10;  // N개씩 생성 후 1프레임 양보
            int created = 0;

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
                    created++;
                }

                lastGroup.AddLine(entry.Text);

                // 프레임 분산: N개 생성마다 1프레임 양보
                if (created >= batchSize)
                {
                    created = 0;
                    await UniTask.Yield();
                    // 팝업이 닫혔으면 중단
                    if (!gameObject.activeSelf) return;
                }
            }

            builtCount = log.Count;

            // 스크롤 (레이아웃 계산 대기)
            await ScrollToBottomAsync2();
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
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        async UniTask ScrollToBottomAsync2()
        {
            if (scrollRect == null) return;
            // 2프레임 대기로 레이아웃 완전 반영 보장
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
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
