using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using MoreMountains.Feedbacks;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 피드백 효과 관리자
    /// FEEL MMF_Player와 기존 UnityEvent 모두 지원
    /// </summary>
    public class FeedbackManager : MonoBehaviour
    {
        public static FeedbackManager Instance { get; private set; }

        [Header("피드백 매핑")]
        [SerializeField] FeedbackEntry[] entries;

        // 런타임 딕셔너리
        Dictionary<string, FeedbackEntry> feedbacks;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject);  // 데모: 단일 씬
                BuildDictionary();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void BuildDictionary()
        {
            feedbacks = new Dictionary<string, FeedbackEntry>(StringComparer.OrdinalIgnoreCase);
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.name)) continue;
                feedbacks[entry.name] = entry;
            }
        }

        /// <summary>
        /// 피드백이 등록되어 있는지 확인
        /// </summary>
        public bool HasFeedback(string name)
        {
            return feedbacks != null && feedbacks.ContainsKey(name);
        }

        /// <summary>
        /// 피드백 재생 시도. 등록된 피드백이 없으면 false 반환
        /// </summary>
        /// <param name="name">피드백 이름 (CSV의 FX 값)</param>
        /// <param name="args">추가 인자 (duration, strength 등)</param>
        /// <returns>피드백이 실행되었으면 true</returns>
        public bool TryPlay(string name, params string[] args)
        {
            if (feedbacks == null || !feedbacks.TryGetValue(name, out var entry))
                return false;

            // MMF_Player 우선, 없으면 UnityEvent 폴백
            if (entry.player != null)
            {
                entry.player.PlayFeedbacks();
            }
            else
            {
                entry.onPlay?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// 피드백 재생 + 완료 대기
        /// </summary>
        /// <param name="name">피드백 이름</param>
        /// <param name="ct">취소 토큰</param>
        public async UniTask PlayAsync(string name, CancellationToken ct = default)
        {
            if (feedbacks == null || !feedbacks.TryGetValue(name, out var entry))
                return;

            // MMF_Player 우선
            if (entry.player != null)
            {
                entry.player.PlayFeedbacks();
                
                // 피드백 완료 대기
                await UniTask.WaitUntil(
                    () => !entry.player.IsPlaying, 
                    cancellationToken: ct
                );
            }
            else
            {
                // UnityEvent 폴백 + duration 대기
                entry.onPlay?.Invoke();
                
                if (entry.duration > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(entry.duration), cancellationToken: ct);
                }
            }
        }

        /// <summary>
        /// 피드백 정지
        /// </summary>
        public void Stop(string name)
        {
            if (feedbacks == null || !feedbacks.TryGetValue(name, out var entry))
                return;

            if (entry.player != null)
            {
                entry.player.StopFeedbacks();
            }
            
            entry.onStop?.Invoke();
        }

        /// <summary>
        /// 모든 피드백 정지
        /// </summary>
        public void StopAll()
        {
            if (feedbacks == null) return;

            foreach (var entry in feedbacks.Values)
            {
                if (entry.player != null)
                {
                    entry.player.StopFeedbacks();
                }
                entry.onStop?.Invoke();
            }
        }

        /// <summary>
        /// 피드백의 MMF_Player 직접 접근 (고급 사용)
        /// </summary>
        public MMF_Player GetPlayer(string name)
        {
            if (feedbacks != null && feedbacks.TryGetValue(name, out var entry))
            {
                return entry.player;
            }
            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("피드백 목록 출력")]
        void LogFeedbacks()
        {
            if (entries == null || entries.Length == 0)
            {
                Debug.Log("[FeedbackManager] 등록된 피드백 없음");
                return;
            }

            Debug.Log($"[FeedbackManager] 등록된 피드백 {entries.Length}개:");
            foreach (var entry in entries)
            {
                string type = entry.player != null ? "MMF_Player" : "UnityEvent";
                Debug.Log($"  - {entry.name} ({type}, duration: {entry.duration}s)");
            }
        }

        [ContextMenu("모든 MMF_Player 초기화")]
        void InitializeAllPlayers()
        {
            if (entries == null) return;
            
            foreach (var entry in entries)
            {
                if (entry.player != null)
                {
                    entry.player.Initialization();
                }
            }
            Debug.Log("[FeedbackManager] 모든 MMF_Player 초기화 완료");
        }
#endif
    }

    /// <summary>
    /// 피드백 항목 (FEEL MMF_Player + UnityEvent 폴백)
    /// </summary>
    [Serializable]
    public class FeedbackEntry
    {
        [Tooltip("CSV에서 사용할 피드백 이름 (예: Shock, Heartbeat, Romantic)")]
        public string name;

        [Header("FEEL 피드백")]
        [Tooltip("MMF_Player 컴포넌트. 설정되면 이것을 우선 사용")]
        public MMF_Player player;

        [Header("폴백 (MMF_Player 없을 때)")]
        [Tooltip("피드백 재생 시간 (초). UnityEvent 사용 시 대기 시간")]
        public float duration = 0.5f;

        [Tooltip("피드백 재생 시 호출 (MMF_Player 없을 때)")]
        public UnityEvent onPlay;

        [Tooltip("피드백 정지 시 호출")]
        public UnityEvent onStop;
    }
}
