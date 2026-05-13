using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 폰(메신저) 모듈 진입점.
    /// PhonePopup/MessengerManager를 IPhone 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/PhoneModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PhoneModule : MonoBehaviour, IPhone
    {
        [SerializeField] PhonePopup phonePanel;
        [Tooltip("스토리 진행 중 화면 우측 알림 버튼 (선택). 씬에 배치된 인스턴스를 드래그.")]
        [SerializeField] PhoneNotificationButton notificationButton;

        void Awake() => Services.Register<IPhone>(this);

        void OnDestroy()
        {
            if (Services.TryGet<IPhone>() == (IPhone)this)
                Services.Unregister<IPhone>();
        }

        public bool IsOpen => phonePanel != null && phonePanel.gameObject.activeSelf;

        public void OpenChat(string heroineId)
        {
            if (phonePanel == null) return;
            phonePanel.gameObject.SetActive(true);
            phonePanel.OpenChatRoom(heroineId);
        }

        public void Close()
        {
            if (phonePanel == null) return;
            phonePanel.gameObject.SetActive(false);
        }

        public void ShowPhoneUI()
        {
            if (phonePanel == null) return;
            phonePanel.Show();
            // 폰 열면 곧 읽음 처리되므로 알림 뱃지 갱신
            notificationButton?.UpdateBadge();
        }

        /// <summary>외부(메시지 수신 시 등)에서 알림 뱃지 강제 갱신.</summary>
        public void RefreshNotification() => notificationButton?.UpdateBadge();
    }
}
