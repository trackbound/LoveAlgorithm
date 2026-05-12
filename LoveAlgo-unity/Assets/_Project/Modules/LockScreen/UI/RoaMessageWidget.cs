using TMPro;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// 잠금화면 로아 메시지 위젯. 인덱스로 4개 중 선택 표출.
    /// </summary>
    public class RoaMessageWidget : MonoBehaviour
    {
        [SerializeField] TMP_Text messageText;

        public void ShowIndex(ILockScreen lockScreen, int index)
        {
            if (lockScreen == null || messageText == null) return;
            messageText.text = lockScreen.GetRoaMessage(index);
        }

        public void ShowText(string text)
        {
            if (messageText != null) messageText.text = text ?? "";
        }
    }
}
