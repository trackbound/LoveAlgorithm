using LoveAlgo.Core; // GameStateSO
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 부팅 컴포지션 루트(MonoBehaviour). 씬 시작 시 <see cref="GameBoot.NewGame"/>로 런타임 초기화를 수행한다
    /// (상태 리셋 + 공식 정의표 주입 + 데이루프 시작). 매니저/컨트롤러의 State 바인딩은 인스펙터에서 같은
    /// GameStateSO를 가리키게 두고, 이 컴포넌트는 인스펙터로 못 하는 런타임 단계만 담당한다.
    ///
    /// 씬 하이어라키: _Boot/GameBootstrap. <see cref="state"/>=공유 GameStateSO, <see cref="balance"/>=GameBalanceSO(null이면 폴백).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("매니저들과 공유하는 단일 GameStateSO.")]
        [SerializeField] GameStateSO state;
        [Tooltip("호감도 정의표 소스. 비우면 검증된 폴백 사용.")]
        [SerializeField] GameBalanceSO balance;
        [Tooltip("Start에서 자동으로 새 게임 부팅.")]
        [SerializeField] bool newGameOnStart = true;

        public GameStateSO State { get => state; set => state = value; }
        public GameBalanceSO Balance { get => balance; set => balance = value; }

        void Start()
        {
            if (newGameOnStart) Boot();
        }

        /// <summary>새 게임 부팅. 직접 호출 가능(테스트/명시적 진입).</summary>
        public void Boot()
        {
            if (state == null)
            {
                Debug.LogError("[GameBootstrap] state(GameStateSO) 미바인딩 — 부팅 불가.");
                return;
            }
            GameBoot.NewGame(state, balance);
        }
    }
}
