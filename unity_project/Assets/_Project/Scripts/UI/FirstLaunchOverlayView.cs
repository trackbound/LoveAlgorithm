using LoveAlgo.Common; // EventBus, DebugInput
using LoveAlgo.Events; // StartNewGameCommand
using UnityEngine;
using UnityEngine.EventSystems; // IPointerClickHandler

namespace LoveAlgo.UI
{
    /// <summary>
    /// 최초 실행 1회 인트로 오버레이(*View). 화면 어디를 클릭/탭하든 새 게임을 시작한다 —
    /// <see cref="StartNewGameCommand"/> 발행 → 기존 <c>SceneFlowController</c>가 게임 씬 로드 →
    /// <c>GameBootstrap</c>이 프롤로그 자동 재생(경로 100% 재사용). 표시·아트는 프리팹, 의도만 발행(ADR-007).
    /// 1회 소비 가드 — 씬 전환 직전 중복 탭 무시. 첫실행 판정·스폰은 <see cref="FirstLaunchBootstrap"/> 책임,
    /// 여기선 "표시 후 탭→넘김"만. 풀스크린 레이캐스트 타깃(Image) 위에 두면 화면 전체 탭을 받는다.
    /// </summary>
    public class FirstLaunchOverlayView : MonoBehaviour, IPointerClickHandler
    {
        bool _consumed;

        public void OnPointerClick(PointerEventData eventData) => Advance();

        /// <summary>오버레이를 닫고(씬 전환) 새 게임으로 진입(프롤로그). 1회만 동작.</summary>
        public void Advance()
        {
            if (_consumed) return;
            _consumed = true;
            DebugInput.Log("좌클릭 → 첫실행 오버레이 닫고 새 게임 시작(프롤로그)");
            EventBus.Publish(new StartNewGameCommand());
        }
    }
}
