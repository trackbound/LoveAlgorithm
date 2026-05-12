using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 폰(메신저) 모듈 진입점.
    /// PhonePanel/MessengerManager를 IPhone 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/PhoneModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PhoneModule : MonoBehaviour, IPhone
    {
        [SerializeField] PhonePanel phonePanel;

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
    }
}
