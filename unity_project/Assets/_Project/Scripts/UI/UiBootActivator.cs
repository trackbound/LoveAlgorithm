using LoveAlgo.Common; // Log
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 부팅 일괄 활성화(얇은 부트 유틸, ADR-007). 씬에 **inactive로 저장**된 Overlay 축 UI
    /// (팝업/모달/빠른메뉴/로딩)를 Awake에서 켠다 — 에디터는 깔끔하게(겹침 없는 inactive 저장),
    /// 런타임은 OnEnable 구독이 살아나게. 이 프로젝트의 전 뷰가 OnEnable에서 EventBus를 구독하므로
    /// inactive 저장 = 열기 명령을 영영 못 받는 죽은 UI가 된다(2026-06-12 본편 6종 실증 버그).
    /// 각 뷰는 활성화 직후 자체 숨김(CanvasGroup alpha0 / 자식 root inactive)이라 화면엔 나타나지 않는다.
    ///
    /// 대상 아님: 페이즈 그룹(Story/Simulation/Ending — UIManager가 ScreenPhaseChangedEvent로 토글)과
    /// LockOverlay(구독자는 별도 홀더 LockScreen, 비주얼은 LockScreenView.OnShow가 켠다).
    ///
    /// 전제(dev_guide §3-5): 어떤 컴포넌트도 Awake/OnEnable에서 EventBus를 발행하지 않는다
    /// (최초 발행 = GameBootstrap.Start). SetActive(true)가 대상 Awake/OnEnable을 동기 실행하므로
    /// 실행 순서 지정 없이도 모든 Start 전에 구독이 끝난다.
    /// 이미 active인 대상은 무해(no-op) — 감독이 에디터에서 켜둔 채 저장해도 안전(양방향 드리프트 내성).
    /// </summary>
    public class UiBootActivator : MonoBehaviour
    {
        [Tooltip("부팅 시 켤 대상(씬에 inactive로 저장된 Overlay 축 UI 루트들).")]
        [SerializeField] GameObject[] targets;

        /// <summary>테스트/배선 주입용.</summary>
        public GameObject[] Targets { get => targets; set => targets = value; }

        void Awake() => ActivateAll();

        /// <summary>대상 일괄 활성화. 직접 호출 가능(라이프사이클 비의존 — UIManager.ShowGroup 관례).</summary>
        public void ActivateAll()
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                var go = targets[i];
                if (go == null)
                {
                    Log.Warn($"[UiBootActivator] targets[{i}] 미배선 — 등록 누락 확인 필요.");
                    continue;
                }
                if (!go.activeSelf) go.SetActive(true);
                // 부모 컨테이너가 inactive로 저장된 사고 감지 — 자식만 켜져선 OnEnable이 안 돌아 구독 불가.
                if (!go.activeInHierarchy)
                    Log.Warn($"[UiBootActivator] '{go.name}' 활성화 후에도 비활성 계층 — 부모 컨테이너 inactive 저장 사고(구독 불가).");
            }
        }
    }
}
