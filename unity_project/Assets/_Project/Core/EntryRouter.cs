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

        [Tooltip("첫 게임 진입 시 LockScreen을 띄울지 여부")]
        [SerializeField] bool showLockScreenOnFirstStart = true;

        public bool ShowLockScreenOnFirstStart => showLockScreenOnFirstStart;

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

            // 최초 진입 여부 판별 (PlayerPrefs 플래그 기반)
            bool isFirstStart = forceFirstSetup || PlayerPrefs.GetInt(PrefsKeys.FirstStartDone, 0) == 0;

            if (isFirstStart && showLockScreenOnFirstStart)
            {
                if (lockScreen == null)
                {
                    Debug.LogWarning("[EntryRouter] ILockScreen 미등록 — 첫 시작이지만 타이틀로 폴백");
                    PlayerPrefs.SetInt(PrefsKeys.FirstStartDone, 1);
                    PlayerPrefs.Save();
                    ShowTitle(title);
                    return;
                }

                Debug.Log("[EntryRouter] 첫 시작 — LockScreenPanel.OpenFirstSetup");
                ShowLockScreenFirstSetup();
            }
            else
            {
                Debug.Log("[EntryRouter] 첫 시작이 아님 또는 LockScreen 미사용 — 타이틀로 진입");
                if (isFirstStart)
                {
                    PlayerPrefs.SetInt(PrefsKeys.FirstStartDone, 1);
                    PlayerPrefs.Save();
                }
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
            var panel = title.TitlePanel;
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
            System.Action onBlackout = null;
            System.Action onComplete = null;
            onBlackout = () =>
            {
                panel.OnBlackoutReached -= onBlackout;
                Debug.Log("[EntryRouter] LockScreen blackout — 타이틀 화면 진입");
                
                PlayerPrefs.SetInt(PrefsKeys.FirstStartDone, 1);
                PlayerPrefs.Save();

                ShowTitle(Services.TryGet<ITitle>());
            };
            
            bool titleTransitionTriggered = false;
            onComplete = () =>
            {
                panel.OnFlowComplete -= onComplete;
                panel.OnBlackoutReached -= onBlackout;
                if (!titleTransitionTriggered)
                {
                    Debug.LogWarning("[EntryRouter] OnBlackout 미수신 — OnFlowComplete에서 타이틀 화면 진입 (안전망)");
                    PlayerPrefs.SetInt(PrefsKeys.FirstStartDone, 1);
                    PlayerPrefs.Save();
                    ShowTitle(Services.TryGet<ITitle>());
                }
                else
                {
                    Debug.Log("[EntryRouter] 첫 시작 잠금 해제 완료 및 타이틀 진입 완료");
                }
            };
            // onBlackout 콜백 안에서 플래그 set
            var prevOnBlackout = onBlackout;
            onBlackout = () => { titleTransitionTriggered = true; prevOnBlackout(); };
            panel.OnBlackoutReached += onBlackout;
            panel.OnFlowComplete += onComplete;

            // 게임 첫 시작 BGM (PC 잠금화면 백색소음 — 기획서 §진입 정보)
            LoveAlgo.Modules.Audio.AudioManager.Instance?.PlayBGMAsync("white_noise").Forget();

            // 게임 첫 시작: 5초 페이드인 + fade-out reveal (이후 Prologue가 자연스럽게 등장)
            panel.SetFadeOutAfter(true);
            panel.OpenForGameStart();
        }
    }
}
