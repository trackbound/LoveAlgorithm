using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO, ScreenPhase
using LoveAlgo.Events;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 데브 씬 전용 트리거(감독 Play 검수용 — 구 NarrativeDevTrigger 계보, 프로덕션 씬 배치 금지).
    /// 부팅 시 새 런타임 + Story 페이즈(폰 버튼 노출 조건) 세팅, 버튼으로 열기/데모 시퀀스 도착 발행.
    /// </summary>
    public class MessengerDevTrigger : MonoBehaviour
    {
        [SerializeField] GameStateSO state;
        [SerializeField] string playerName = "감독";
        [SerializeField] Button openButton;
        [SerializeField] Button deliverRoaButton;
        [SerializeField] Button deliverYeeunButton;
        [SerializeField] Button deliverHeewonButton;

        public GameStateSO State { get => state; set => state = value; }
        public Button OpenButton { get => openButton; set => openButton = value; }
        public Button DeliverRoaButton { get => deliverRoaButton; set => deliverRoaButton = value; }
        public Button DeliverYeeunButton { get => deliverYeeunButton; set => deliverYeeunButton = value; }
        public Button DeliverHeewonButton { get => deliverHeewonButton; set => deliverHeewonButton = value; }

        void Awake()
        {
            Wire(openButton, () => EventBus.Publish(new OpenMessengerCommand()));
            Wire(deliverRoaButton, () => Deliver("Demo_Roa"));
            Wire(deliverYeeunButton, () => Deliver("Demo_Yeeun"));
            Wire(deliverHeewonButton, () => Deliver("Demo_Heewon"));
        }

        void Start()
        {
            if (state == null) return;
            state.ResetRuntime(); // 매 Play 깨끗한 메신저 상태
            state.Data.playerName = playerName;
            state.Phase = ScreenPhase.Story; // 폰 버튼 노출 조건
            EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Schedule, ScreenPhase.Story));
        }

        static void Wire(Button button, System.Action action)
        {
            if (button != null) button.onClick.AddListener(() => action());
        }

        static void Deliver(string sequenceId)
            => EventBus.Publish(new DeliverMessengerSequenceCommand(sequenceId));
    }
}
