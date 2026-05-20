using LoveAlgo.Common;
using LoveAlgo.LockScreen;
using LoveAlgo.Title;
using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 게임 진입 라우터.
    /// 기획서(PC잠금 연출) §진입 정보:
    ///   - 게임 첫 시작(비번 미설정) → 검은 → 5초 페이드 → LockScreenPanel.OpenFirstSetup
    ///   - 이후(비번 설정 후) → 메인 타이틀 (TitlePanel)
    ///
    /// 씬 하이어라키 권장: _Bootstrap/EntryRouter (DefaultExecutionOrder -900 — 모듈 등록 후, 첫 화면 띄우기 전)
    ///
    /// 모듈 직접 참조 금지 원칙(CLAUDE.md §2)을 지키기 위해
    /// Core 인프라 위치에서 ILockScreen / ITitle 두 IService만 사용.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class EntryRouter : MonoBehaviour
    {
        [Header("Behavior")]
        [Tooltip("Awake에서 자동 라우팅. false면 외부에서 RouteEntry() 호출 필요.")]
        [SerializeField] bool routeOnAwake = true;

        [Tooltip("디버그용 — 항상 첫 시작 흐름으로 진입 (비번 무시). 빌드 전 false 확인.")]
        [SerializeField] bool forceFirstSetup = false;

        [Tooltip("디버그용 — 항상 타이틀로 진입 (잠금 우회). 빌드 전 false 확인.")]
        [SerializeField] bool skipLockScreen = false;

        void Start()
        {
            // Start로 미룸 — Awake 단계에서는 다른 모듈 IService 등록이 끝나지 않을 수 있음
            if (routeOnAwake) RouteEntry();
        }

        public void RouteEntry()
        {
            var lockScreen = Services.TryGet<ILockScreen>();
            var title      = Services.TryGet<ITitle>();

            if (skipLockScreen)
            {
                Debug.Log("[EntryRouter] skipLockScreen=true — 타이틀로 직진");
                ShowTitle(title);
                return;
            }

            if (lockScreen == null)
            {
                Debug.LogWarning("[EntryRouter] ILockScreen 미등록 — 타이틀로 폴백");
                ShowTitle(title);
                return;
            }

            bool firstStart = forceFirstSetup || !lockScreen.IsPasswordSet;

            if (firstStart)
            {
                Debug.Log("[EntryRouter] 첫 시작 — LockScreenPanel.OpenFirstSetup (5초 페이드)");
                ShowLockScreenFirstSetup();
            }
            else
            {
                Debug.Log("[EntryRouter] 비번 설정됨 — TitlePanel 표출");
                ShowTitle(title);
            }
        }

        void ShowTitle(ITitle title)
        {
            if (title == null)
            {
                Debug.LogError("[EntryRouter] ITitle 미등록 — 첫 화면 표출 실패");
                return;
            }
            var panel = title.TitlePanel;
            if (panel == null) return;
            panel.gameObject.SetActive(true);
        }

        void ShowLockScreenFirstSetup()
        {
            // LockScreenModule 직접 참조는 LoveAlgo.LockScreen 네임스페이스 — Core이므로 허용
            var module = Object.FindObjectOfType<LockScreenModule>();
            if (module == null)
            {
                Debug.LogError("[EntryRouter] LockScreenModule GameObject 미발견 — 폴백: 타이틀");
                ShowTitle(Services.TryGet<ITitle>());
                return;
            }
            var panel = module.Panel;
            if (panel == null)
            {
                Debug.LogError("[EntryRouter] LockScreenPanel 미할당 — 폴백: 타이틀");
                ShowTitle(Services.TryGet<ITitle>());
                return;
            }

            // Blackout(Outro Phase 1 끝) 시점에 타이틀 셋업 → fade-out으로 자연스러운 reveal
            // OnFlowComplete는 추가 정리만.
            System.Action onBlackout = null;
            System.Action onComplete = null;
            onBlackout = () =>
            {
                panel.OnBlackoutReached -= onBlackout;
                Debug.Log("[EntryRouter] LockScreen blackout — 타이틀 셋업 (fade-out에서 reveal)");
                ShowTitle(Services.TryGet<ITitle>());
            };
            onComplete = () =>
            {
                panel.OnFlowComplete -= onComplete;
                Debug.Log("[EntryRouter] 첫 시작 잠금 해제 완료");
            };
            panel.OnBlackoutReached += onBlackout;
            panel.OnFlowComplete += onComplete;

            // 게임 첫 시작: 5초 페이드인 + fade-out reveal (타이틀이 자연스럽게 등장)
            panel.SetFadeOutAfter(true);
            panel.OpenForGameStart();
        }
    }
}
