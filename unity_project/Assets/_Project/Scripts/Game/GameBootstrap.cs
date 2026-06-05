using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // PlayScriptCommand
using LoveAlgo.Story;  // StoryAssetLoader
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 부팅 컴포지션 루트(MonoBehaviour). 씬 시작 시 부팅 모드(<see cref="GameEntry"/>)에 따라 런타임을
    /// 초기화한다 — 새 게임(<see cref="GameBoot.NewGame"/>: 리셋+공식+1일차) 또는 이어하기
    /// (<see cref="GameBoot.ContinueGame"/>: 오토세이브 복원+공식). 매니저/컨트롤러의 State 바인딩은
    /// 인스펙터에서 같은 GameStateSO를 가리키게 두고, 이 컴포넌트는 인스펙터로 못 하는 런타임 단계만 담당한다.
    ///
    /// 새 게임이면 부팅 직후 프롤로그를 1회 자동 재생한다(<see cref="PlayPrologue"/>). 이어하기는 스킵.
    ///
    /// 씬 하이어라키: _Boot/GameBootstrap. <see cref="state"/>=공유 GameStateSO, <see cref="balance"/>=GameBalanceSO(null이면 폴백).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("매니저들과 공유하는 단일 GameStateSO.")]
        [SerializeField] GameStateSO state;
        [Tooltip("호감도 정의표 소스. 비우면 검증된 폴백 사용.")]
        [SerializeField] GameBalanceSO balance;
        [Tooltip("Start에서 자동 부팅(모드는 GameEntry.PendingMode — 기본 새 게임).")]
        [SerializeField] bool newGameOnStart = true;
        [Tooltip("새 게임 첫 진입 시 1회 재생할 프롤로그 CSV(StreamingAssets/Story/ 기준 파일명). 비우면 스킵.")]
        [SerializeField] string prologueCsv = "Prologue.csv";

        public GameStateSO State { get => state; set => state = value; }
        public GameBalanceSO Balance { get => balance; set => balance = value; }
        public string PrologueCsv { get => prologueCsv; set => prologueCsv = value; }

        void Start()
        {
            if (newGameOnStart) Boot();
        }

        /// <summary>부팅 모드(GameEntry)를 소비해 새 게임/이어하기를 수행. 직접 호출 가능(테스트/명시적 진입).</summary>
        public void Boot()
        {
            if (state == null)
            {
                Debug.LogError("[GameBootstrap] state(GameStateSO) 미바인딩 — 부팅 불가.");
                return;
            }
            var mode = GameEntry.Consume();
            if (mode == BootMode.Continue && GameBoot.ContinueGame(state, balance)) return;
            GameBoot.NewGame(state, balance); // NewGame이거나 Continue 폴백(세이브 없음/손상)
            PlayPrologue();                   // 새 게임 1회: 프롤로그 자동 재생
        }

        /// <summary>
        /// 새 게임 첫 진입 프롤로그를 발행한다(저녁이벤트와 같은 StoryAssetLoader→PlayScriptCommand 패턴).
        /// 부팅 시 state.Phase=Schedule이지만 NarrativeController.Run이 같은 프레임에 Story로 전환하므로
        /// 스케줄 UI는 깜빡이지 않는다. 종료 시 Run이 Schedule 페이즈로 복귀시킨다. CSV 없으면 fail-open
        /// (로그 후 스킵 — 시뮬 루프는 정상 진행). 이어하기엔 호출되지 않는다(Boot의 early return).
        /// </summary>
        void PlayPrologue()
        {
            if (string.IsNullOrEmpty(prologueCsv)) return;
            string csv = StoryAssetLoader.Read(prologueCsv);
            if (string.IsNullOrEmpty(csv))
            {
                Log.Warn($"[GameBootstrap] 프롤로그 CSV 로드 실패 — 스킵: {prologueCsv}");
                return;
            }
            EventBus.Publish(new PlayScriptCommand(csv, "prologue"));
        }
    }
}
