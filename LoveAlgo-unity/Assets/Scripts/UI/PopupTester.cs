using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 팝업 시스템 테스트용
    /// </summary>
    public class PopupTester : MonoBehaviour
    {
        [Header("테스트 키 바인딩")]
        [SerializeField] Key confirmKey = Key.Digit1;
        [SerializeField] Key alertKey = Key.Digit2;
        [SerializeField] Key toastKey = Key.Digit3;
        [SerializeField] Key scheduleConfirmKey = Key.Digit4;
        [SerializeField] Key saveKey = Key.Digit5;
        [SerializeField] Key loadKey = Key.Digit6;
        [SerializeField] Key settingsKey = Key.Digit7;

        void Start()
        {
            Debug.Log("=== PopupTester 시작 ===");
            Debug.Log("1: Confirm 팝업");
            Debug.Log("2: Alert 팝업");
            Debug.Log("3: Toast 메시지");
            Debug.Log("4: Schedule Confirm 팝업");
            Debug.Log("5: Save 팝업");
            Debug.Log("6: Load 팝업");
            Debug.Log("7: Settings 팝업");
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[confirmKey].wasPressedThisFrame)
            {
                TestConfirm().Forget();
            }
            else if (keyboard[alertKey].wasPressedThisFrame)
            {
                TestAlert().Forget();
            }
            else if (keyboard[toastKey].wasPressedThisFrame)
            {
                TestToast();
            }
            else if (keyboard[scheduleConfirmKey].wasPressedThisFrame)
            {
                TestScheduleConfirm().Forget();
            }
            else if (keyboard[saveKey].wasPressedThisFrame)
            {
                TestSave();
            }
            else if (keyboard[loadKey].wasPressedThisFrame)
            {
                TestLoad();
            }
            else if (keyboard[settingsKey].wasPressedThisFrame)
            {
                TestSettings();
            }
        }

        async UniTaskVoid TestConfirm()
        {
            Debug.Log("[Test] Confirm 팝업 표시");
            bool result = await PopupManager.Instance.ConfirmAsync("저장하시겠습니까?");
            Debug.Log($"[Test] Confirm 결과: {result}");
        }

        async UniTaskVoid TestAlert()
        {
            Debug.Log("[Test] Alert 팝업 표시");
            await PopupManager.Instance.AlertAsync("알림 테스트입니다.");
            Debug.Log("[Test] Alert 완료");
        }

        void TestToast()
        {
            Debug.Log("[Test] Toast 표시");
            PopupManager.Instance.Toast("호감도 상승", "<color=#FF6B6B>로아 ♥ +5</color>");
        }

        async UniTaskVoid TestScheduleConfirm()
        {
            Debug.Log("[Test] Schedule Confirm 팝업 표시");
            bool result = await PopupManager.Instance.ScheduleConfirmAsync(
                "운동을 진행할까요?",
                "<color=#4CAF50>체력 +3</color>"
            );
            Debug.Log($"[Test] Schedule Confirm 결과: {result}");
        }

        void TestSave()
        {
            Debug.Log("[Test] Save 팝업 표시");
            PopupManager.Instance.ShowSavePopup(slot =>
            {
                Debug.Log($"[Test] 슬롯 {slot} 선택됨 - 저장 처리");
                
                // GameManager로 저장
                LoveAlgo.Core.GameManager.Instance?.Save(slot);
                PopupManager.Instance.Toast("저장 완료", $"슬롯 {slot}에 저장되었습니다.");
            });
        }

        void TestLoad()
        {
            Debug.Log("[Test] Load 팝업 표시");
            PopupManager.Instance.ShowLoadPopup(slot =>
            {
                Debug.Log($"[Test] 슬롯 {slot} 선택됨 - 로드 처리");
                
                var data = SaveManager.Load(slot);
                if (data != null)
                {
                    SaveManager.ApplyToGameState(data);
                    PopupManager.Instance.Toast("로드 완료", $"슬롯 {slot + 1}에서 로드되었습니다.");
                }
            });
        }

        void TestSettings()
        {
            Debug.Log("[Test] Settings 팝업 표시");
            PopupManager.Instance.ShowSettings();
        }
    }
}
