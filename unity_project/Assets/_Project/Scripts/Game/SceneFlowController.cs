using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events;  // StartNewGameCommand, ContinueGameCommand, QuitGameCommand
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 씬 전환 어댑터(<c>PhaseController</c> 얇은 어댑터 패턴 미러). 씬별 자족 — 타이틀 씬에 두고
    /// 타이틀 메뉴 의도(새 게임/이어하기/종료)를 구독한다. 새 게임/이어하기는 부팅 모드(<see cref="GameEntry"/>)를
    /// 설정하고 게임 씬을 로드하며, 종료는 앱을 닫는다(ADR-013 씬축, persistent 매니저 없이). <c>GameStateSO</c>는
    /// .asset이라 씬 간 공유되고, 런타임 초기화/복원은 게임 씬의 <c>GameBootstrap</c>이 수행하므로 여기선
    /// 모드 설정 + 씬 로드(+종료)만 담당한다.
    /// </summary>
    public class SceneFlowController : MonoBehaviour
    {
        [Tooltip("로드할 게임 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        [SerializeField] string gameSceneName = "Game";

        readonly List<IDisposable> _subs = new();

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<StartNewGameCommand>(OnStartNewGame));
            _subs.Add(EventBus.Subscribe<ContinueGameCommand>(OnContinueGame));
            _subs.Add(EventBus.Subscribe<QuitGameCommand>(OnQuit));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
        }

        /// <summary>새 게임 요청: 부팅 모드=NewGame 후 게임 씬 로드. 직접 호출도 가능(라이프사이클 비의존).</summary>
        public void OnStartNewGame(StartNewGameCommand _)
        {
            GameEntry.PendingMode = BootMode.NewGame;
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>이어하기 요청: 부팅 모드=Continue 후 게임 씬 로드(GameBootstrap이 오토세이브 복원).</summary>
        public void OnContinueGame(ContinueGameCommand _)
        {
            GameEntry.PendingMode = BootMode.Continue;
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>
        /// 종료 요청: 빌드에선 앱 종료, 에디터에선 PlayMode 정지. PlayMode 테스트는 이 핸들러를 직접
        /// 호출하지 않는다(에디터 정지가 테스트 런을 끊으므로) — TitleView의 발행만 검증한다.
        /// </summary>
        public void OnQuit(QuitGameCommand _)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
