using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 좌상단 장소/이벤트 배너 UI (2줄 구성)
    /// 
    /// CSV 사용법:
    ///   ,Place,,이벤트명|장소명,await              → 2줄 배너, 기본 2초 (이벤트명은 자동으로 [ ] 감싸짐)
    ///   ,Place,,단체 이벤트: 축제|주점 부스,await    → [ 단체 이벤트: 축제 ] + 주점 부스
    ///   ,Place,,이벤트명|장소명:3,await              → 3초 표시
    ///   ,Place,,장소명,await                        → 장소만 1줄 표시
    ///   ,Place,,Hide,>                             → 즉시 숨김
    ///
    /// Value 포맷: "이벤트명|장소명[:초]" 또는 "장소명[:초]"
    /// 
    /// 애니메이션: 페이드인 → 유지 → 페이드아웃
    /// </summary>
    public class PlaceUI : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] RectTransform bannerRect;
        [SerializeField] TMP_Text eventText;      // 상단: 이벤트명 (예: [ 단체 이벤트: 축제 ])
        [SerializeField] TMP_Text placeText;       // 하단: 장소명 (예: 주점 부스)
        [SerializeField] GameObject eventLine;     // 이벤트 줄 오브젝트 (1줄 모드 시 비활성화)

        [Header("애니메이션 설정")]
        [SerializeField] float enterDuration = 0.45f;    // 등장 시간
        [SerializeField] float exitDuration = 0.35f;    // 페이드아웃 시간
        [SerializeField] float defaultHoldDuration = 2f; // 기본 유지 시간

        Sequence currentSequence;
        UniTaskCompletionSource<bool> awaitSource;

        /// <summary>
        /// 현재 배너가 표시 중인지
        /// </summary>
        public bool IsShowing { get; private set; }

        void Awake()
        {
            // 초기 상태: alpha만 0으로 설정
            // 주의: SetActive(false)를 여기서 호출하면 안 됨!
            // 씬에서 비활성 상태(m_IsActive=0)로 시작하면 첫 SetActive(true) 시
            // Awake가 호출되어 다시 꺼버리는 문제 발생
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Place 명령 실행
        /// Value 포맷: "이벤트명|장소명[:초]" 또는 "장소명[:초]" 또는 "Hide"
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(value)) return;

            // Hide 명령
            if (value.Equals("Hide", System.StringComparison.OrdinalIgnoreCase))
            {
                await HideAsync(ct);
                return;
            }

            // 1. 표시 시간 파싱 (마지막 :숫자)
            float holdDuration = defaultHoldDuration;
            string body = value;

            int lastColon = value.LastIndexOf(':');
            if (lastColon > 0 && lastColon < value.Length - 1)
            {
                string afterColon = value.Substring(lastColon + 1);
                if (float.TryParse(afterColon, out float parsed))
                {
                    holdDuration = parsed;
                    body = value.Substring(0, lastColon);
                }
            }

            // 2. 이벤트명|장소명 분리
            string eventName = null;
            string placeName = body;

            int pipeIndex = body.IndexOf('|');
            if (pipeIndex >= 0)
            {
                eventName = body.Substring(0, pipeIndex).Trim();
                placeName = body.Substring(pipeIndex + 1).Trim();
            }

            await ShowAsync(eventName, placeName, holdDuration, ct);
        }

        /// <summary>
        /// 배너 표시: 슬라이드인 → 유지 → 슬라이드아웃
        /// </summary>
        async UniTask ShowAsync(string eventName, string placeName, float holdDuration, CancellationToken ct)
        {
            // 기존 애니메이션 정리
            KillCurrentSequence();

            // 이벤트 줄 표시 여부
            bool hasEvent = !string.IsNullOrEmpty(eventName);
            if (eventLine != null) eventLine.SetActive(hasEvent);
            if (hasEvent && eventText != null)
                eventText.text = $"[ {eventName} ]";

            // 장소명 설정
            placeText.text = placeName;

            // alpha=0 먼저 세팅 → SetActive 시 1프레임 깜빡임 방지
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(true);
            IsShowing = true;

            // 시퀀스: 페이드인 → 홀드 → 페이드아웃
            awaitSource = new UniTaskCompletionSource<bool>();

            currentSequence = DOTween.Sequence();

            // 1. 등장 (페이드인 — InOutSine으로 부드럽게 등장)
            _ = currentSequence.Append(
                canvasGroup.DOFade(1f, enterDuration)
                    .From(0f)
                    .SetEase(Ease.InOutSine));

            // 2. 유지
            _ = currentSequence.AppendInterval(holdDuration);

            // 3. 퇴장 (페이드아웃)
            _ = currentSequence.Append(
                canvasGroup.DOFade(0f, exitDuration)
                    .SetEase(Ease.InCubic));

            _ = currentSequence.OnComplete(() =>
            {
                IsShowing = false;
                gameObject.SetActive(false);
                awaitSource?.TrySetResult(true);
            });
            _ = currentSequence.OnKill(() =>
            {
                awaitSource?.TrySetResult(false);
            });

            _ = currentSequence.SetLink(gameObject);

            // 완료 대기
            await awaitSource.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 즉시 숨김
        /// </summary>
        UniTask HideAsync(CancellationToken ct)
        {
            KillCurrentSequence();

            if (!gameObject.activeSelf) return UniTask.CompletedTask;

            canvasGroup.alpha = 0f;
            IsShowing = false;
            gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 즉시 숨김 (동기)
        /// </summary>
        public void HideImmediate()
        {
            KillCurrentSequence();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            IsShowing = false;
            gameObject.SetActive(false);
        }

        void KillCurrentSequence()
        {
            if (currentSequence != null && currentSequence.IsActive())
            {
                currentSequence.Kill();
                currentSequence = null;
            }
        }

        void OnDestroy()
        {
            KillCurrentSequence();
        }
    }
}
