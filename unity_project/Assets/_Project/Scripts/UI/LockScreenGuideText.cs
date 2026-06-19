using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 입력칸 위 안내 텍스트(*View: LockScreen). 상태별 문구를 인스펙터에 직렬화하고
    /// <see cref="SetState"/>로 교체한다(ADR-012: 문구 = 데이터). label 미바인딩 시 no-op(안전).
    /// </summary>
    public class LockScreenGuideText : MonoBehaviour
    {
        public enum LockGuideState { Setup, SetupComplete, Normal, Lost }

        [SerializeField] TMP_Text label;
        [TextArea][SerializeField] string setupText = "앞으로 사용할 비밀번호를 입력해주세요.\n최대 7자까지 입력 가능합니다.";
        [SerializeField] string setupCompleteText = "비밀번호 설정 완료!";
        [SerializeField] string normalText = "비밀번호를 입력해주세요.";
        [TextArea][SerializeField] string lostText = "비밀번호를 잊으셨다면 우측 하단 열쇠 모양 버튼을 눌러주세요.";

        public TMP_Text Label { get => label; set => label = value; }
        public string SetupText { get => setupText; set => setupText = value; }
        public string SetupCompleteText { get => setupCompleteText; set => setupCompleteText = value; }
        public string NormalText { get => normalText; set => normalText = value; }
        public string LostText { get => lostText; set => lostText = value; }

        public void SetState(LockGuideState state)
        {
            if (label == null) return;
            label.text = state switch
            {
                LockGuideState.Setup => setupText,
                LockGuideState.SetupComplete => setupCompleteText,
                LockGuideState.Normal => normalText,
                LockGuideState.Lost => lostText,
                _ => label.text
            };
        }
    }
}
