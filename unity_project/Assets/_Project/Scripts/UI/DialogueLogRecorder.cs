using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // PlayScriptCommand, ShowDialogueCommand
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그 수집 어댑터(MonoBehaviour, _Bootstrap — ThumbnailCaptureController 형제).
    /// 엔진을 건드리지 않고 EventBus만 구독해 <see cref="DialogueLogStore"/>에 적재한다(ADR-007):
    /// <see cref="PlayScriptCommand"/> = 현재 스크립트 id 추적(그룹핑 경계),
    /// <see cref="ShowDialogueCommand"/> = 한 줄 적재(Speaker는 치환 후 표시명, SpeakerId로 주인공/캐릭터 판별).
    /// Awake에서 Store 리셋 — 씬 부팅마다 새 세션(세이브 비영속 승인안과 정합).
    /// </summary>
    public class DialogueLogRecorder : MonoBehaviour
    {
        readonly List<IDisposable> _subs = new();
        string _scriptId = "";

        void Awake() => DialogueLogStore.Reset();

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<PlayScriptCommand>(cmd => _scriptId = cmd.Name ?? ""));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(
                e => DialogueLogStore.Append(_scriptId, e.Speaker, e.SpeakerId, e.Text)));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
        }
    }
}
