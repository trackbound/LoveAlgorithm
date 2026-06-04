using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events;  // StartNewGameCommand
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 씬 전환 어댑터(<c>PhaseController</c> 얇은 어댑터 패턴 미러). 씬별 자족 — 타이틀 씬에 두고
    /// <see cref="StartNewGameCommand"/>를 구독해 게임 씬을 로드한다(ADR-013 씬축, persistent 매니저 없이).
    /// <c>GameStateSO</c>는 .asset이라 씬 간 공유되고, 새 게임 초기화는 게임 씬의 <c>GameBootstrap</c>이
    /// 수행하므로 여기선 씬 로드만 담당한다(LoadScene은 부수효과뿐 — 순수 Service 불요).
    /// </summary>
    public class SceneFlowController : MonoBehaviour
    {
        [Tooltip("New Game 시 로드할 게임 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        [SerializeField] string gameSceneName = "Game";

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<StartNewGameCommand>(OnStartNewGame);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>새 게임 요청 처리: 게임 씬 로드. 직접 호출도 가능(라이프사이클 비의존).</summary>
        public void OnStartNewGame(StartNewGameCommand _) => SceneManager.LoadScene(gameSceneName);
    }
}
