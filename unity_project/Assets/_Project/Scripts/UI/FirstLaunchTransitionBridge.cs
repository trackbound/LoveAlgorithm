using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // StartNewGameCommand
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 첫실행 인트로 → 프롤로그 핸드오프 브리지(*첫실행 연출). <see cref="Begin"/> 시 부모에서 분리해
    /// DontDestroyOnLoad로 씬 로드를 가로질러 생존하며, 풀스크린 블랙을 페이드인 → <c>StartNewGameCommand</c>
    /// 발행(동기 씬 로드) → 새 씬 부팅 대기(postLoadHold) → 페이드아웃 → 자기 파괴. 프롤로그가 BG=블랙에서
    /// 시작하므로 컷/번쩍임 없이 이어진다. 코루틴 lerp(기존 관례). 수치 인스펙터 노출(ADR-012).
    /// </summary>
    public class FirstLaunchTransitionBridge : MonoBehaviour
    {
        [Tooltip("풀스크린 블랙을 담는 CanvasGroup(이 오브젝트의 Canvas 하위).")]
        [SerializeField] CanvasGroup group;
        [Tooltip("블랙 페이드인 시간(초).")]
        [SerializeField] float blackIn = 0.8f;
        [Tooltip("씬 로드 후 페이드아웃 전 대기(초) — 동기 로드 직후 1~2프레임 정착용.")]
        [SerializeField] float postLoadHold = 0.2f;
        [Tooltip("블랙 페이드아웃 시간(초).")]
        [SerializeField] float blackOut = 0.8f;
        [Tooltip("전환 시작 시 오버레이 BGM(백색소음)을 블랙 인과 같은 길이로 페이드아웃해 인게임으로 자연 전환.")]
        [SerializeField] bool stopBgmOnTransition = true;

        bool _begun;

        /// <summary>핸드오프 시작(1회만). 부모 분리 + DontDestroyOnLoad 후 시퀀스 진행.</summary>
        public void Begin()
        {
            if (_begun) return;
            _begun = true;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // 백색소음을 블랙 인과 같은 길이로 페이드아웃(씬 로드 전에 자연스럽게 끈다 — 하드컷 방지).
            if (stopBgmOnTransition) EventBus.Publish(new StopBgmCommand(blackIn));
            yield return Fade(0f, 1f, blackIn);
            EventBus.Publish(new StartNewGameCommand());
            yield return null; // 동기 LoadScene 교체 프레임 양보
            if (postLoadHold > 0f) yield return new WaitForSeconds(postLoadHold);
            yield return Fade(1f, 0f, blackOut);
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            if (group == null || dur <= 0f) { if (group != null) group.alpha = to; yield break; }
            float t = 0f;
            group.alpha = from;
            while (t < dur)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
