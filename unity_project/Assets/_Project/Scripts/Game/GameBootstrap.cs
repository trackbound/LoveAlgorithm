using LoveAlgo.Core; // GameStateSO
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 부팅 컴포지션 루트(MonoBehaviour). 씬 시작 시 부팅 모드(<see cref="GameEntry"/>)에 따라 런타임을
    /// 초기화한다 — 새 게임(<see cref="GameBoot.NewGame"/>: 리셋+공식+1일차) 또는 이어하기
    /// (<see cref="GameBoot.ContinueGame"/>: 오토세이브 복원+공식). 매니저/컨트롤러의 State 바인딩은
    /// 인스펙터에서 같은 GameStateSO를 가리키게 두고, 이 컴포넌트는 인스펙터로 못 하는 런타임 단계만 담당한다.
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

        public GameStateSO State { get => state; set => state = value; }
        public GameBalanceSO Balance { get => balance; set => balance = value; }

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
        }
    }
}
