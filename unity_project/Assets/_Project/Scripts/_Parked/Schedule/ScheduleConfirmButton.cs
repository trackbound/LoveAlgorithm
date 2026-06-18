using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowModalCommand, ModalButton, ModalButtonKind, ModalRequest

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 정적 스케줄 확인 버튼(얇은 UI 어댑터). 클릭 시 "○○을/를 진행할까요?"(메인) + 효과 요약(부가) 2줄짜리
    /// 확인 모달(<see cref="ShowModalCommand"/>, [아니오, 예])을 띄우고, "예"(index 1)일 때만
    /// <see cref="ScheduleSelectedCommand"/>를 발행한다(ScheduleController가 처리 — ADR-007). 운동/공부 탭처럼
    /// 즉시 실행이 아니라 확인을 거치는 액션용. 기존 ModalView 재사용(Title=메인 질문, Message=효과 요약).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ScheduleConfirmButton : MonoBehaviour
    {
        [Tooltip("확인 후 실행할 스케줄 타입.")]
        [SerializeField] ScheduleType type;
        [Tooltip("긍정/부정 버튼 라벨.")]
        [SerializeField] string yesLabel = "예";
        [SerializeField] string noLabel = "아니오";

        public ScheduleType Type { get => type; set => type = value; }

        Button _button;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.onClick.AddListener(Confirm);
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(Confirm);
        }

        void Confirm()
        {
            var e = ScheduleTable.Get(type);
            string main = $"{e.displayName}{Josa(e.displayName)} 진행할까요?";
            string detail = ScheduleTable.FormatEffect(type);
            var buttons = new List<ModalButton>
            {
                new ModalButton(noLabel, ModalButtonKind.No),    // index 0 (좌)
                new ModalButton(yesLabel, ModalButtonKind.Yes),  // index 1 (우) — 실행
            };
            EventBus.Publish(new ShowModalCommand(main, detail, buttons,
                new ModalRequest(i => { if (i == 1) EventBus.Publish(new ScheduleSelectedCommand(type)); })));
        }

        // "을/를" 조사 선택(받침 있으면 "을"). 한글 음절이 아니면 "를".
        static string Josa(string word)
        {
            if (string.IsNullOrEmpty(word)) return "를";
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return "를";
            return (last - 0xAC00) % 28 != 0 ? "을" : "를";
        }
    }
}
