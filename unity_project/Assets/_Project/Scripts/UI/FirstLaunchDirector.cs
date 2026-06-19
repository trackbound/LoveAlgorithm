using System.Collections;
using LoveAlgo.Common;      // EventBus, Log
using LoveAlgo.Events;      // StartNewGameCommand
using LoveAlgo.MessageStack; // MessageStackController
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 첫실행 인트로 연출 오케스트레이터(*첫실행). Start에서 ① 콘텐츠 페이드인 ② warn 흔들림 시작
    /// ③ 메시지 스택 자동 재생 ④ 시퀀스 종료(Completed) 후 clickEnableDelay(기본 1초) 뒤 클릭 캐처 무장
    /// ⑤ 클릭/탭 시 TransitionBridge로 프롤로그 핸드오프. 메시지 재생까지는 무입력 자동, 마지막 진행만 클릭.
    /// 버블 도착마다 선택적 SFX(messageSfx, null=무음). 표시·발행만(ADR-007). 수치 인스펙터 노출.
    /// messages 미바인딩은 즉시 완료, clickCatcher 미바인딩은 대기 후 자동 진행, bridgePrefab 미바인딩은
    /// StartNewGameCommand 직접 발행(모두 fail-open — 소프트락 방지).
    /// </summary>
    public class FirstLaunchDirector : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("페이드인할 오버레이 콘텐츠 CanvasGroup.")]
        [SerializeField] CanvasGroup content;
        [Tooltip("재생할 메시지 스택. 비우면 즉시 핸드오프.")]
        [SerializeField] MessageStackController messages;
        [Tooltip("연출 동안 흔들 warn 위젯.")]
        [SerializeField] WarnWidgetShake warnShake;
        [Tooltip("핸드오프용 블랙 브리지 프리팹. 비우면 StartNewGameCommand 직접 발행(폴백).")]
        [SerializeField] FirstLaunchTransitionBridge bridgePrefab;
        [Tooltip("진행용 클릭 캐처(풀스크린). 비우면 대기 후 자동 진행(폴백).")]
        [SerializeField] ClickAdvanceCatcher clickCatcher;

        [Header("SFX (optional)")]
        [Tooltip("메시지 도착 효과음 재생 소스.")]
        [SerializeField] AudioSource sfxSource;
        [Tooltip("버블 등장 시 재생할 효과음. 비우면 무음.")]
        [SerializeField] AudioClip messageSfx;

        [Header("Timing")]
        [Tooltip("콘텐츠 페이드인 시간(초).")]
        [SerializeField] float fadeIn = 0.6f;
        [Tooltip("마지막 버블이 다 쌓인 뒤 클릭으로 진행 가능해지기까지 대기(초).")]
        [SerializeField] float clickEnableDelay = 1f;

        bool _completed;
        bool _handedOff;

        void OnEnable()
        {
            if (messages != null)
            {
                messages.MessageSpawned += OnMessageSpawned;
                messages.Completed += OnSequenceCompleted;
            }
            if (clickCatcher != null) clickCatcher.Clicked += OnAdvanceClicked;
        }

        void OnDisable()
        {
            if (messages != null)
            {
                messages.MessageSpawned -= OnMessageSpawned;
                messages.Completed -= OnSequenceCompleted;
            }
            if (clickCatcher != null) clickCatcher.Clicked -= OnAdvanceClicked;
        }

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (warnShake != null) warnShake.enabled = true;
            yield return FadeInContent();
            if (messages != null) messages.Play();
            else OnSequenceCompleted(); // 메시지 없음 → 바로 핸드오프
        }

        IEnumerator FadeInContent()
        {
            if (content == null || fadeIn <= 0f) { if (content != null) content.alpha = 1f; yield break; }
            float t = 0f;
            content.alpha = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                content.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            content.alpha = 1f;
        }

        void OnMessageSpawned()
        {
            if (messageSfx != null && sfxSource != null) sfxSource.PlayOneShot(messageSfx);
        }

        void OnSequenceCompleted()
        {
            if (_completed) return;
            _completed = true;
            StartCoroutine(EnableClickAfterDelay());
        }

        IEnumerator EnableClickAfterDelay()
        {
            if (clickEnableDelay > 0f) yield return new WaitForSeconds(clickEnableDelay);
            if (clickCatcher != null) clickCatcher.Arm(); // 이제부터 클릭하면 진행
            else Advance(); // 캐처 없음 → 소프트락 방지로 자동 진행
        }

        void OnAdvanceClicked() => Advance();

        /// <summary>프롤로그로 핸드오프(1회만). 클릭 또는 폴백 경로에서 호출.</summary>
        void Advance()
        {
            if (_handedOff) return;
            _handedOff = true;
            if (clickCatcher != null) clickCatcher.Disarm(); // 이후 클릭 무시
            if (bridgePrefab != null)
            {
                var bridge = Instantiate(bridgePrefab);
                bridge.Begin();
            }
            else
            {
                Log.Warn("[FirstLaunch] bridgePrefab 미바인딩 — StartNewGameCommand 직접 발행(폴백).");
                EventBus.Publish(new StartNewGameCommand());
            }
        }
    }
}
