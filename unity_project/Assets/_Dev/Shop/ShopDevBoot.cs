using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // MoneyChangedEvent
using LoveAlgo.Game;   // GameBoot
using UnityEngine;

namespace LoveAlgo.Dev
{
    /// <summary>
    /// 상점 dev 씬 부팅(감독 Play 확인 전용 — 프로덕션 씬 미사용, ShopDev.unity).
    /// <see cref="GameBoot.NewGame"/>으로 시뮬 상태(1일차·행동 풀충전)를 세우고 시드 소지금을 얹는다 —
    /// GameStateSO 런타임은 NonSerialized라 부팅 없는 씬에선 소지금 0이라 구매 동선을 확인할 수 없다.
    /// </summary>
    public class ShopDevBoot : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO(GameState_Main) — 씬의 ShopController/ScheduleController와 같은 에셋.")]
        [SerializeField] GameStateSO state;
        [Tooltip("호감도 공식 정의표. 비우면 검증된 폴백 정의표 사용.")]
        [SerializeField] GameBalanceSO balance;
        [Tooltip("부팅 후 얹을 시드 소지금(구매 동선 확인용 — 자유 조정).")]
        [SerializeField] long seedMoney = 500_000;

        void Start()
        {
            if (state == null)
            {
                Debug.LogError("[ShopDevBoot] state(GameStateSO) 미바인딩 — 시드 불가.");
                return;
            }
            GameBoot.NewGame(state, balance);
            state.Money = seedMoney;
            EventBus.Publish(new MoneyChangedEvent(state.Money)); // 잔액 표시 구독자 갱신
        }
    }
}
