using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Modal 팝업 기본 클래스
    /// </summary>
    public abstract class ModalPopupBase : MonoBehaviour
    {
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 닫기 시도 (변경사항 확인 등)
        /// 닫아도 되면 true 반환, 취소하면 false 반환
        /// </summary>
        public virtual UniTask<bool> TryCloseAsync()
        {
            // 기본: 바로 닫기 허용
            return UniTask.FromResult(true);
        }

        /// <summary>
        /// 닫기 버튼에서 호출
        /// </summary>
        public void Close()
        {
            TryCloseAndDismiss().Forget();
        }

        /// <summary>
        /// 닫기 시도 후 실제 닫기
        /// </summary>
        async UniTaskVoid TryCloseAndDismiss()
        {
            bool canClose = await TryCloseAsync();
            if (canClose)
            {
                PopupManager.Instance?.CloseModal();
            }
        }
    }
}
