using LoveAlgo.Contracts;
using Cysharp.Threading.Tasks;
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
            // GameManager가 phase 보류 중일 수 있으므로 phase 전환으로 통일 (BGM/UI 정상화)
            var gm = GameManager.Instance;
            if (gm != null && gm.Flow != null)
            {
                gm.Flow.ChangePhase(GamePhase.Title);
                return;
            }

            // 폴백: GameManager 미초기화 — panel 직접 활성화
            if (title == null)
            {
                Debug.LogError("[EntryRouter] ITitle 미등록 — 첫 화면 표출 실패");
                return;
            }
            // Phase B-4: ITitle.TitlePanel 은 ITitlePanel 반환 — GameObject 활성화는 구체 cast.
            var panel = title.TitlePanel as MonoBehaviour;
            if (panel == null) return;
            panel.gameObject.SetActive(true);
        }

        void ShowLockScreenFirstSetup()
        {
            // LockScreenModule 직접 참조는 LoveAlgo.LockScreen 네임스페이스 — Core이므로 허용
            var module = Object.FindAnyObjectByType<LockScreenModule>();
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
                Debug.Log("[EntryRouter] LockScreen blackout — 새 게임 즉시 시작 (Prologue 직진)");
                // 첫 진입은 타이틀 거치지 않고 곧장 프롤로그 시작.
                // StartNewGame → Flow.StartPrologueFromNewGame → TransitionToPrologueAsync (로딩+페이드)
                GameManager.Instance?.StartNewGame();
            };
            // 안전망: OnBlackout 못 받고 OnFlowComplete 먼저 오면 거기서도 새 게임 시작
            //         (Outro 페이드 옵션/순서 변경에 대한 미래 대비)
            bool newGameTriggered = false;
            onComplete = () =>
            {
                panel.OnFlowComplete -= onComplete;
                panel.OnBlackoutReached -= onBlackout;
                if (!newGameTriggered)
                {
                    Debug.LogWarning("[EntryRouter] OnBlackout 미수신 — OnFlowComplete에서 새 게임 시작 (안전망)");
                    GameManager.Instance?.StartNewGame();
                }
                else
                {
                    Debug.Log("[EntryRouter] 첫 시작 잠금 해제 완료");
                }
            };
            // onBlackout 콜백 안에서 플래그 set
            var prevOnBlackout = onBlackout;
            onBlackout = () => { newGameTriggered = true; prevOnBlackout(); };
            panel.OnBlackoutReached += onBlackout;
            panel.OnFlowComplete += onComplete;

            // 게임 첫 시작 BGM (PC 잠금화면 백색소음 — 기획서 §진입 정보)
            Services.TryGet<IAudio>()?.PlayBGM("white_noise");

            // 게임 첫 시작: 5초 페이드인 + fade-out reveal (이후 Prologue가 자연스럽게 등장)
            panel.SetFadeOutAfter(true);
            panel.OpenForGameStart();
        }
    }
}
