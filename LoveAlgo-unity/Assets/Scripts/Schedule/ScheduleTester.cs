using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 UI 테스트용
    /// </summary>
    public class ScheduleTester : MonoBehaviour
    {
        [Header("테스트 키")]
        [SerializeField] Key openKey = Key.S;

        [Header("바인딩")]
        [SerializeField] ScheduleUI scheduleUI;

        void Start()
        {
            Debug.Log("=== ScheduleTester 시작 ===");
            Debug.Log("S: Schedule UI 열기");
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[openKey].wasPressedThisFrame)
            {
                OpenSchedule().Forget();
            }
        }

        async UniTaskVoid OpenSchedule()
        {
            if (scheduleUI == null)
            {
                Debug.LogWarning("[ScheduleTester] scheduleUI가 바인딩되지 않음");
                return;
            }

            Debug.Log("[ScheduleTester] Schedule UI 표시");

            await scheduleUI.ShowAsync(type =>
            {
                var effect = ScheduleTable.Get(type);
                Debug.Log($"[ScheduleTester] 스케줄 선택: {effect.displayName}");

                // TODO: GameState에 효과 적용
                // GameState.Instance.AddMoney(effect.moneyChange);
                // GameState.Instance.AddStat("Str", effect.strengthChange);
                // ...
            });
        }
    }
}
