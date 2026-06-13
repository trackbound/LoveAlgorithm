using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // ScreenPhase
using LoveAlgo.Events;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 트리거(얇은 어댑터) — 스케줄 화면 진입(ScreenPhaseChangedEvent → Schedule)을 감지해
    /// 미완료(설치 단위 PlayerPrefs)일 때 1회 <see cref="StartTutorialCommand"/>를 발행한다(기획:
    /// "게임 속에서 스탯/자유행동으로 첫 번째로 진입했을 때만"). 재생·종료 기록은 TutorialView 몫.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [Tooltip("완료 판정 키의 출처(시퀀스 정의). View와 같은 에셋 바인딩.")]
        [SerializeField] TutorialSequenceSO sequence;

        public TutorialSequenceSO Sequence { get => sequence; set => sequence = value; }

        readonly List<IDisposable> _subs = new();
        bool _firedThisSession; // 같은 세션 재진입(스케줄↔스토리 왕복) 중복 발행 방지

        void OnEnable() => _subs.Add(EventBus.Subscribe<ScreenPhaseChangedEvent>(OnPhaseChanged));

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
        }

        void OnPhaseChanged(ScreenPhaseChangedEvent e)
        {
            if (e.To != ScreenPhase.Schedule || _firedThisSession) return;
            if (sequence == null) return;
            if (TutorialFlag.IsDone(sequence.prefsKey)) return;

            _firedThisSession = true;
            EventBus.Publish(new StartTutorialCommand());
        }
    }
}
