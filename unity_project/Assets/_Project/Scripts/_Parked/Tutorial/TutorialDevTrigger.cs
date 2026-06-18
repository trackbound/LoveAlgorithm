using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events;

namespace LoveAlgo.Tutorial
{
    /// <summary>튜토리얼 데브 씬 전용 트리거(감독 검수용 — 프로덕션 배치 금지). 재생/완료 기록 리셋.</summary>
    public class TutorialDevTrigger : MonoBehaviour
    {
        [SerializeField] TutorialSequenceSO sequence;
        [SerializeField] Button playButton;
        [SerializeField] Button resetFlagButton;

        public TutorialSequenceSO Sequence { get => sequence; set => sequence = value; }
        public Button PlayButton { get => playButton; set => playButton = value; }
        public Button ResetFlagButton { get => resetFlagButton; set => resetFlagButton = value; }

        void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(() => EventBus.Publish(new StartTutorialCommand()));
            if (resetFlagButton != null)
                resetFlagButton.onClick.AddListener(() =>
                {
                    if (sequence != null) TutorialFlag.Reset(sequence.prefsKey);
                    Debug.Log("[TutorialDevTrigger] 완료 기록 리셋");
                });
        }
    }
}
