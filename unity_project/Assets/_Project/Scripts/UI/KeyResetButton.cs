using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowModalCommand, ModalButton, ModalButtonKind, ModalRequest, RequestPasswordResetCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 비밀번호 분실 시 우하단 열쇠 버튼(*View: LockScreen). 3회+ 오류 시 View가 <see cref="SetVisible"/>(true).
    /// 클릭 시 재설정 확인 모달(예/아니오)을 기존 <see cref="ShowModalCommand"/>로 발행하고, 예 선택 콜백에서
    /// <see cref="RequestPasswordResetCommand"/>를 발행한다(→Controller/View Reset 재진입). ADR-007 표시+명령.
    /// 표시/숨김은 <see cref="root"/>(미바인딩 시 자신) 활성 토글. 문구/라벨은 인스펙터 직렬화.
    /// </summary>
    public class KeyResetButton : MonoBehaviour
    {
        [Tooltip("표시/숨김 대상(열쇠 비주얼 루트). 미바인딩 시 자기 GameObject.")]
        [SerializeField] GameObject root;
        [SerializeField] Button button;
        [SerializeField] string modalTitle = "";
        [TextArea][SerializeField] string modalMessage = "새로운 비밀번호 설정을 진행하시겠습니까?";
        [SerializeField] string yesLabel = "예";
        [SerializeField] string noLabel = "아니오";

        public GameObject Root { get => root; set => root = value; }
        public Button Button { get => button; set => button = value; }

        void OnEnable() { if (button != null) button.onClick.AddListener(RequestReset); }
        void OnDisable() { if (button != null) button.onClick.RemoveListener(RequestReset); }

        /// <summary>열쇠 표시/숨김(root 활성 토글).</summary>
        public void SetVisible(bool visible)
        {
            var target = root != null ? root : gameObject;
            target.SetActive(visible);
        }

        /// <summary>재설정 확인 모달 발행 — 예(0)=재설정 요청, 아니오(1)=닫기만.</summary>
        public void RequestReset()
        {
            var buttons = new List<ModalButton>
            {
                new ModalButton(yesLabel, ModalButtonKind.Yes),
                new ModalButton(noLabel, ModalButtonKind.No),
            };
            var req = new ModalRequest(idx => { if (idx == 0) EventBus.Publish(new RequestPasswordResetCommand()); });
            EventBus.Publish(new ShowModalCommand(modalTitle, modalMessage, buttons, req));
        }
    }
}
