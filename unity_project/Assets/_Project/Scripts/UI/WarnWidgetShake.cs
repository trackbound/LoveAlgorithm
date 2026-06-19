using System.Collections;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// warn 위젯의 상시 idle 흔들림(*첫실행 연출). 기준 anchoredPosition을 중심으로 X/Y 서로 다른 주파수의
    /// 사인 오프셋을 더해 떨리게 한다(리사주). 기본값은 경고 알림처럼 **작은 진폭 + 높은 주파수**(짧고 빠른 진동).
    /// 코루틴 lerp(ScreenFade/MessageStack과 동일 관례, DOTween 미사용). OnDisable 시 기준 위치로 복원. 수치는 인스펙터 노출(ADR-012).
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
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float ox = Mathf.Sin(t * frequencyX) * amplitude;
                float oy = Mathf.Sin(t * frequencyY) * amplitude;
                target.anchoredPosition = _base + new Vector2(ox, oy);
                yield return null;
            }
        }
    }
}
