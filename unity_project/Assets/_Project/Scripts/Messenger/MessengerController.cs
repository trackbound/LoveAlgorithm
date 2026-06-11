using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // DeliverMessengerSequenceCommand, DayChangedEvent, ...

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 얇은 어댑터(*Controller) — 구독→순수(MessengerService) 호출→통지 발행만(ADR-007).
    /// 도착 경로 2개: ① 스토리/디버그의 <see cref="DeliverMessengerSequenceCommand"/>
    /// ② 하루 전환(<see cref="DayChangedEvent"/>) 시 카탈로그의 자동 도착일 매칭(저녁 이벤트 씨임 선례 —
    /// GameManager 무수정, 피처가 스스로 구독해 자율 작동).
    /// "확인 필수 메시지" 대기(OnRead 핸들)는 뷰의 <see cref="MessengerSequenceReadEvent"/>로 완료한다.
    /// </summary>
    public class MessengerController : MonoBehaviour
    {
        [SerializeField] GameStateSO state;
        [SerializeField] MessengerScriptCatalogSO catalog;

        public GameStateSO State { get => state; set => state = value; }
        public MessengerScriptCatalogSO Catalog { get => catalog; set => catalog = value; }

        // 시퀀스 id → 읽힘 대기 핸들("확인 필수 메시지"). 씬 수명 — 세이브 무관(런타임 대기일 뿐).
        readonly Dictionary<string, CompletionHandle> _pendingRead = new();
        readonly List<IDisposable> _subs = new();

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<DeliverMessengerSequenceCommand>(OnDeliver));
            _subs.Add(EventBus.Subscribe<DayChangedEvent>(OnDayChanged));
            _subs.Add(EventBus.Subscribe<MessengerSequenceReadEvent>(OnSequenceRead));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
            _pendingRead.Clear();
        }

        void OnDeliver(DeliverMessengerSequenceCommand cmd)
        {
            var entry = catalog != null ? catalog.Resolve(cmd.SequenceId) : null;
            if (entry == null)
            {
                Log.Warn($"[MessengerController] 미등록 시퀀스 '{cmd.SequenceId}' — 무시(카탈로그 확인). 대기 핸들은 즉시 완료(fail-open).");
                cmd.OnRead?.Complete();
                return;
            }

            DeliverEntry(entry, cmd.OnRead);
        }

        void OnDayChanged(DayChangedEvent e)
        {
            if (catalog == null || state == null) return;
            var due = catalog.ForDay(e.NewDay);
            for (int i = 0; i < due.Count; i++)
                DeliverEntry(due[i], onRead: null);
        }

        void OnSequenceRead(MessengerSequenceReadEvent e)
        {
            if (string.IsNullOrEmpty(e.SequenceId)) return;
            if (_pendingRead.TryGetValue(e.SequenceId, out var handle))
            {
                _pendingRead.Remove(e.SequenceId);
                handle?.Complete();
            }
        }

        void DeliverEntry(MessengerScriptCatalogSO.Entry entry, CompletionHandle onRead)
        {
            if (state == null)
            {
                Log.Warn("[MessengerController] State 미바인딩 — 도착 무시.");
                onRead?.Complete();
                return;
            }

            bool delivered = MessengerService.Deliver(state, entry.sequenceId, entry.roomId, state.Day);
            if (delivered)
                EventBus.Publish(new MessengerMessageArrivedEvent(entry.roomId, entry.sequenceId));

            if (onRead == null) return;

            // 이미 읽은 시퀀스에 다시 Wait이 걸리면 즉시 통과, 아니면 읽힘까지 대기 등록.
            var record = MessengerService.FindRecord(state, entry.sequenceId);
            if (record != null && record.read) onRead.Complete();
            else _pendingRead[entry.sequenceId] = onRead;
        }
    }
}
