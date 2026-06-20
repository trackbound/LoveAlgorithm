using System.Collections;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 위젯 오류 피드백 흔들림(공용). host가 코루틴을 구동하고, rt를 수평 1버스트 감쇠 진동 후 기준 위치로 복원한다.
    /// 중복 호출 시 호출 측이 <paramref name="handle"/>로 이전 흔들림을 멈춰 기준 위치 드리프트를 막는다.
    /// </summary>
    public static class UiNudge
    {
        public static void Shake(MonoBehaviour host, RectTransform rt, ref Coroutine handle,
            float amplitude = 12f, float frequency = 60f, float duration = 0.25f)
        {
            if (host == null || rt == null || !host.isActiveAndEnabled) return;
            if (handle != null) host.StopCoroutine(handle);
            handle = host.StartCoroutine(ShakeRoutine(rt, amplitude, frequency, duration));
        }

        static IEnumerator ShakeRoutine(RectTransform rt, float amplitude, float frequency, float duration)
        {
            Vector2 basePos = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float env = Mathf.Clamp01(1f - t / duration);
                float ox = Mathf.Sin(t * frequency) * amplitude * env;
                rt.anchoredPosition = basePos + new Vector2(ox, 0f);
                yield return null;
            }
            rt.anchoredPosition = basePos;
        }
    }
}
