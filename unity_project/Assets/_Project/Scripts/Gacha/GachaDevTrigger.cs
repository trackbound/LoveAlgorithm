using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events;

namespace LoveAlgo.Gacha
{
    /// <summary>
    /// 가챠 데브 씬 전용 트리거(감독 Play 검수용 — MessengerDevTrigger 계보, 프로덕션 씬 배치 금지).
    /// 부팅 시 새 런타임 세팅, 버튼으로 가챠권 구매(추첨)/현황 보기/보유 초기화.
    /// 상점 가챠권 연결은 병렬 Shop 작업 합류 후 — 여기선 같은 명령(OpenGachaCommand)을 직접 발행.
    /// </summary>
    public class GachaDevTrigger : MonoBehaviour
    {
        [SerializeField] GameStateSO state;
        [SerializeField] Button purchaseButton; // 가챠권 구매(추첨+오픈 연출)
        [SerializeField] Button viewButton;     // 현황 보기(무추첨)
        [SerializeField] Button resetButton;    // 보유/업적 초기화

        public GameStateSO State { get => state; set => state = value; }
        public Button PurchaseButton { get => purchaseButton; set => purchaseButton = value; }
        public Button ViewButton { get => viewButton; set => viewButton = value; }
        public Button ResetButton { get => resetButton; set => resetButton = value; }

        void Awake()
        {
            Wire(purchaseButton, () => EventBus.Publish(new OpenGachaCommand(fromPurchase: true)));
            Wire(viewButton, () => EventBus.Publish(new OpenGachaCommand(fromPurchase: false)));
            Wire(resetButton, ResetGacha);
        }

        void Start()
        {
            if (state != null) state.ResetRuntime(); // 매 Play 깨끗한 상태
        }

        void ResetGacha()
        {
            if (state == null) return;
            state.Data.gachaOwnedPieces.Clear();
            state.Data.gachaBonusPurchases = 0;
            EventBus.Publish(new OpenGachaCommand(fromPurchase: false)); // 비워진 판 재표시
        }

        static void Wire(Button button, System.Action action)
        {
            if (button != null) button.onClick.AddListener(() => action());
        }
    }
}
