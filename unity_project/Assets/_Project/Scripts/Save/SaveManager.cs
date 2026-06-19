using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // SaveRequestedEvent, SaveCompletedEvent
using UnityEngine;

namespace LoveAlgo.Save
{
    /// <summary>
    /// 세이브 오케스트레이션 매니저(승인된 4매니저 중 SaveManager). <see cref="SaveRequestedEvent"/>를 구독해
    /// 순수 <see cref="SaveService"/>로 직렬화하고 <see cref="SaveCompletedEvent"/>를 통지한다(ADR-007).
    /// Service Locator 없음 — ScheduleController/GameManager와 동일한 얇은 어댑터 패턴.
    ///
    /// 범위 밖(후속): 썸네일 캡처(M5 UI)·로드 트리거(타이틀/이어하기=M5)·슬롯 메타 라벨 확장.
    /// 씬 하이어라키: _Managers/SaveManager, 인스펙터에서 <see cref="state"/> 바인딩(부팅 와이어링은 후속).
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. 저장 대상.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<SaveRequestedEvent>(OnSaveRequested);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>
        /// 세이브 요청 처리: 순수 <see cref="SaveService.Save"/> 호출 후 결과를 통지.
        /// 1차 경로는 EventBus 구독이지만 직접 호출도 가능(라이프사이클 비의존 — 테스트/부팅 와이어링).
        /// </summary>
        public void OnSaveRequested(SaveRequestedEvent e)
        {
            if (state == null)
            {
                Debug.LogError("[SaveManager] state(GameStateSO) 미바인딩 — 저장 불가.");
                EventBus.Publish(new SaveCompletedEvent(e.Slot, false));
                return;
            }

            bool ok = SaveService.Save(e.Slot, state, BuildChapterLabel());
            // 썸네일 기록(인게임 컨트롤러 구독, 없으면 no-op). 수동 저장은 팝업이 예열한 캐시 재사용(무깜빡임),
            // 자동/스토리 저장은 현재 화면 라이브 캡처(팝업 없음).
            if (ok) EventBus.Publish(new CaptureThumbnailCommand(e.Slot, useCache: e.Reason == "manual"));
            EventBus.Publish(new SaveCompletedEvent(e.Slot, ok));
        }

        /// <summary>슬롯 표시용 라벨(현재: 일차). 풍부한 라벨은 페이즈/진행 정보 연결 시 확장.</summary>
        string BuildChapterLabel() => $"Day {state.Day}";
    }
}
