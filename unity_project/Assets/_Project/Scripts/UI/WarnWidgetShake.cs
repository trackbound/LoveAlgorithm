using System.Collections;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// warn 위젯의 경고 진동(*첫실행 연출). 핸드폰 진동처럼 짧은 고주파 버스트("지잉")로 흔들리고 잠깐 정지,
    /// 다시 버스트…를 반복한다(연속 루프 아님). 버스트는 감쇠 엔벨로프로 자연스럽게 잦아들어 기준 위치로 정착한다.
    /// 코루틴(ScreenFade/MessageStack과 동일 관례, DOTween 미사용). OnDisable 시 기준 위치로 복원. 수치는 인스펙터 노출(ADR-012).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WarnWidgetShake : MonoBehaviour
    {
        [Tooltip("흔들 대상. 비우면 자신의 RectTransform.")]
        [SerializeField] RectTransform target;
        [Tooltip("최대 오프셋(px). 작을수록 '살짝 진동'.")]
        [SerializeField] float amplitude = 2.5f;
        [Tooltip("X축 흔들림 주파수(rad/s). 클수록 빠른 진동.")]
        [SerializeField] float frequencyX = 40f;
        [Tooltip("Y축 흔들림 주파수(X와 달리해 리사주).")]
        [SerializeField] float frequencyY = 46f;
        [Tooltip("한 번 '지잉' 진동하는 버스트 길이(초).")]
        [SerializeField] float buzzDuration = 0.3f;
        [Tooltip("버스트 사이 정지 시간(초).")]
        [SerializeField] float restDuration = 0.45f;

        Vector2 _base;
        Coroutine _co;

        void Reset() => target = (RectTransform)transform;

        void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            _base = target.anchoredPosition;
        }

        void OnEnable() => _co = StartCoroutine(Wobble());

        void OnDisable()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (target != null) target.anchoredPosition = _base;
        }

        IEnumerator Wobble()
        {
            while (true)
            {
                // 지잉 — 짧은 고주파 버스트(감쇠 엔벨로프로 끝에서 자연스럽게 잦아듦)
                float t = 0f;
                while (t < buzzDuration)
                {
                    t += Time.deltaTime;
                    // 양끝이 0인 사인 윈도우 → 버스트 시작/끝 모두 기준 위치에서 부드럽게(팝 없음)
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / buzzDuration));
                    // X=Sin, Y=Cos(90° 위상차)로 두 축을 탈동조 → 한쪽 대각선 직선이 아닌 양방향 타원 진동
                    float ox = Mathf.Sin(t * frequencyX) * amplitude * env;
                    float oy = Mathf.Cos(t * frequencyY) * amplitude * env;
                    target.anchoredPosition = _base + new Vector2(ox, oy);
                    yield return null;
                }

                // 정지 — 기준 위치에서 잠깐 멈춤
                target.anchoredPosition = _base;
                float r = 0f;
                while (r < restDuration)
                {
                    r += Time.deltaTime;
                    yield return null;
                }
            }
        }
    }
}
