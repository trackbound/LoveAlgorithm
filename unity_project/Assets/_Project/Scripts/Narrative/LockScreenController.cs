using System;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // ShowLockScreenCommand, SubmitPasswordCommand, LockMode, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// 잠금화면(LockScreen) 상태 어댑터 — ADR-007 완료-핸들 패턴, FlowCommandController 미러.
    /// NarrativeController가 대기형 Flow로 발행한 <see cref="ShowLockScreenCommand"/>로 활성 모드·완료 핸들을
    /// 보관하고, LockScreenView(입력 UI)가 발행한 <see cref="SubmitPasswordCommand"/>를 받아 FirstSetup이면
    /// 비번을 <see cref="GameStateSO.Password"/>에 저장한 뒤 핸들을 푼다(→ 엔진이 다음 라인으로 진행 = hang 해소).
    /// 이번 슬라이스는 FirstSetup(저장)만. Normal 검증/Reset/Auto/GameStart는 후속.
    /// 안전: 내러티브 종료/도구 화면정리 시 미완료 핸들을 정리해 데드락을 막는다.
    /// </summary>
    public class LockScreenController : MonoBehaviour
    {
        [Tooltip("비밀번호 저장 대상 런타임 상태 SO. 인스펙터/부팅 주입.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        IDisposable _showSub, _submitSub, _finishSub, _resetSub;
        CompletionHandle _pending;
        LockMode _mode;

        void OnEnable()
        {
            _showSub   = EventBus.Subscribe<ShowLockScreenCommand>(OnShow);
            _submitSub = EventBus.Subscribe<SubmitPasswordCommand>(OnSubmit);
            // 잠금화면 도중 내러티브가 끊기거나(재시작) 도구 화면정리 시 미완료 핸들이 엔진을 막지 않도록 정리.
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ReleasePending());
            _resetSub  = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ReleasePending());
        }

        void OnDisable()
        {
            _showSub?.Dispose(); _submitSub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _showSub = _submitSub = _finishSub = _resetSub = null;
        }

        /// <summary>잠금화면 표시 명령 수신 — 활성 핸들·모드 보관. 직접 호출도 가능(테스트/부팅).</summary>
        public void OnShow(ShowLockScreenCommand e)
        {
            _pending?.Complete(); // 이전 미완료 핸들(비정상)이 엔진을 막지 않도록 먼저 정리.
            _pending = e.Handle;
            _mode = e.Mode;
        }

        /// <summary>비번 확정 수신 — 모드별 처리 후 핸들 완료. 직접 호출도 가능(테스트/부팅).</summary>
        public void OnSubmit(SubmitPasswordCommand e)
        {
            if (_pending == null) return; // 활성 잠금화면 없음 — 무시.

            if (_mode == LockMode.FirstSetup)
            {
                if (state == null)
                    Debug.LogError("[LockScreenController] state(GameStateSO) 미바인딩 — 비번 저장 불가.");
                else
                {
                    state.Password = e.Password; // 평문 저장(이번 슬라이스). 해싱은 후속.
                    Log.Info($"[LockScreenController] FirstSetup 비번 설정 완료(len={e.Password?.Length ?? 0}).");
                }
            }
            // Normal(검증)/Reset 등은 이번 슬라이스 미구현 — 핸들만 풀어 진행.

            ReleasePending();
        }

        void ReleasePending()
        {
            var h = _pending; _pending = null; h?.Complete();
        }
    }
}
