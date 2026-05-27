using LoveAlgo.Common;
using LoveAlgo.Contracts;
using UnityEngine;

namespace LoveAlgo.Save
{
    /// <summary>
    /// 마지막으로 재생된 BGM 이름을 캐싱 (Phase C3-6).
    /// BGMChangedEvent를 subscribe해 갱신, SaveDataSerializer가 저장 시 읽음.
    ///
    /// 이전엔 SaveDataSerializer가 AudioManager.Instance.CurrentBGM 직접 조회 →
    /// Save 모듈이 Audio 구체 타입에 의존. EventBus 구독으로 풀어줌.
    ///
    /// 도메인 리로드 가드 + RuntimeInitializeOnLoadMethod로 안전 초기화.
    /// </summary>
    public static class LastBGMTracker
    {
        static string _currentBGM = "";
        static System.IDisposable _subscription;

        /// <summary>마지막 재생된 BGM 이름. 정지/없음 시 빈 문자열.</summary>
        public static string CurrentBGM => _currentBGM ?? "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            // Reload Domain Off 환경에서 PlayMode 진입마다 정리/재구독
            _currentBGM = "";
            _subscription?.Dispose();
            _subscription = EventBus.Subscribe<BGMChangedEvent>(OnBGMChanged);
        }

        static void OnBGMChanged(BGMChangedEvent evt)
        {
            _currentBGM = evt.Name ?? "";
        }

        /// <summary>EditMode 테스트 격리용.</summary>
        public static void ResetForTests() => _currentBGM = "";
    }
}
