using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 수집 어댑터(MonoBehaviour, _Bootstrap — ThumbnailCaptureController 형제).
    /// 엔진을 건드리지 않고 EventBus만 구독해 <see cref="DialogueLogStore"/>에 적재한다(ADR-007):
    /// <see cref="ShowDialogueCommand"/> = 한 줄(한 진행) 적재 → 진행 단위로 박스가 나뉜다(목업 동결 규칙).
    /// Speaker는 치환 후 표시명, SpeakerId로 주인공/캐릭터 판별. Awake에서 Store 리셋 — 씬 부팅마다 새 세션
    /// (세이브 비영속 승인안과 정합).
    /// </summary>
    public class DialogueLogRecorder : MonoBehaviour
    {
        IDisposable _sub;

        void Awake() => DialogueLogStore.Reset();

        void OnEnable()
            => _sub = EventBus.Subscribe<ShowDialogueCommand>(
                e => DialogueLogStore.Append(e.Speaker, e.SpeakerId, e.Text));

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }
    }
}
