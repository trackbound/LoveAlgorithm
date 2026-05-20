using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 팝업 — 채팅 스타일
    /// 연속 같은 화자의 대사를 하나의 그룹으로 묶어 표시
    /// 타입별 프리팹: Character / Extra / User / Narration
    ///
    /// 최적화: 증분 빌드 — 이전 Show 이후 추가된 로그만 생성
    /// </summary>
    public class LogPopup : PopupBase
    {
        [Header("바인딩")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] Button closeButton;

        [Header("프리팹 — LogEntryBase 서브클래스")]
        [SerializeField] LogEntryBase dialogueEntryPrefab;   // 히로인/엑스트라/주인공 (LogDialogueEntry)
        [SerializeField] LogEntryBase narrationEntryPrefab;  // 독백 (LogNarrationEntry)

        [Header("캐릭터 초상화")]
        [SerializeField] List<PortraitEntry> portraits;

        [Header("빈 메시지")]
        [SerializeField] GameObject emptyMessage;

        // 초상화 룩업
        readonly Dictionary<string, Sprite> portraitLookup = new();

        // 생성된 항목
        readonly List<LogEntryBase> spawnedEntries = new();

        // 증분 빌드 상태
        int builtCount;              // 이미 빌드한 로그 수
        string lastSpeaker;          // 마지막 그룹의 화자
        string lastCharId;           // 마지막 그룹의 캐릭터ID
        LogEntryBase lastGroup;      // 마지막 그룹 (연속 대사 추가용)
        CancellationTokenSource buildCts;  // 중복 빌드 방지용
        readonly ListenerBag _listeners = new();

        protected override void Awake()
        {
            base.Awake();
            _listeners.Bind(closeButton, Close);

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
            Show(); // base — SetActive(true) + NotifyOpened

            if (hasEntries)
            {
                // 이전 빌드 취소
                buildCts?.Cancel();
                buildCts?.Dispose();
                buildCts = new CancellationTokenSource();
                BuildIncrementalAsync(log, buildCts.Token).Forget();
            }
            else
                ScrollToBottomAsync().Forget();
        }

        // Hide/Close는 PopupBase에서 제공 (base.Hide는 SetActive(false) + NotifyClosed)

        /// <summary>증분 빌드 — 새 항목만 추가 (비동기: 프레임 분산)</summary>
        async UniTaskVoid BuildIncrementalAsync(IReadOnlyList<DialogueLogEntry> log, CancellationToken ct)
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
                    LogEntryBase prefab;
                    Sprite portrait = null;
                    bool isUser = false;

                    if (isNarration)
                    {
                        prefab = narrationEntryPrefab;
                    }
                    else
                    {
                        prefab = dialogueEntryPrefab;
                        isUser = string.IsNullOrEmpty(entry.CharacterId);
                        if (!isUser) portrait = GetPortrait(entry.CharacterId);
                    }

                    lastGroup = Instantiate(prefab, contentRoot);
                    lastGroup.Init(entry.Speaker, portrait, isUser);

                    lastSpeaker = entry.Speaker;
                    lastCharId = entry.CharacterId;
                    spawnedEntries.Add(lastGroup);
                    created++;
                }

                lastGroup.AddLine(entry.Text);

                // 프레임 분산: N개 생성마다 1프레임 양보 + 중간 스크롤 보정
                if (created >= batchSize)
                {
                    created = 0;
                    // 배치 종료 시점에 즉시 레이아웃 갱신 후 바닥 고정
                    ForceLayoutAndScrollToBottom();
                    await UniTask.Yield(ct);
                    // 팝업이 닫혔으면 중단
                    if (!gameObject.activeSelf) return;
                }
            }

            builtCount = log.Count;

            // 최종 스크롤 (레이아웃 완전 반영 보장)
            await ScrollToBottomAsync();
        }

        Sprite GetPortrait(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;
            portraitLookup.TryGetValue(characterId.ToLower(), out var sprite);
            return sprite;
        }

        protected override void OnDestroy()
        {
            _listeners.Dispose();
            buildCts?.Cancel();
            buildCts?.Dispose();
            buildCts = null;
            ClearEntries();
            base.OnDestroy();
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

        /// <summary>contentRoot 레이아웃 즉시 재계산 후 스크롤 바닥 고정</summary>
        void ForceLayoutAndScrollToBottom()
        {
            if (scrollRect == null || contentRoot == null) return;

            // 자식(엔트리) 레이아웃부터 안쪽→바깥 순서로 강제 재계산
            // (VLG + ContentSizeFitter 중첩 시 한 번에 안 잡히는 경우 대응)
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                var child = contentRoot.GetChild(i) as RectTransform;
                if (child != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(child);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        async UniTask ScrollToBottomAsync()
        {
            if (scrollRect == null) return;

            // 3패스로 스크롤 보정 — VLG+CSF+TMP 비동기 메시 빌드 대응
            // 1) 즉시: 현재 프레임에서 강제 레이아웃 + 스크롤
            ForceLayoutAndScrollToBottom();

            // 2) 1프레임 후: TMP 메시 빌드 완료 대기
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (this == null || scrollRect == null || !gameObject.activeSelf) return;
            ForceLayoutAndScrollToBottom();

            // 3) 1프레임 더: 폰트/스프라이트 폴백 등 추가 변동 대응
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (this == null || scrollRect == null || !gameObject.activeSelf) return;
            ForceLayoutAndScrollToBottom();
        }
    }

    [System.Serializable]
    public class PortraitEntry
    {
        public string characterId;
        public Sprite sprite;
    }
}
