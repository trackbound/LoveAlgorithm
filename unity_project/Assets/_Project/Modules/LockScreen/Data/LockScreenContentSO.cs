using UnityEngine;

namespace LoveAlgo.LockScreen.Data
{
    /// <summary>
    /// PC잠금 화면 콘텐츠 — 로아 메시지 + 안내 문구 + 시계 + 사운드.
    /// 기획서 §구성 / §비밀번호 입력 커스텀 시스템 / §할 일 목록.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/LockScreen/Content", fileName = "LockScreenContent")]
    public class LockScreenContentSO : ScriptableObject
    {
        [Header("Roa Messages (4개 순차)")]
        [Tooltip("기획서 §구성 — 위로 올라오는 방식으로 4개 출력. 1개당 효과음.")]
        [TextArea(1, 3)]
        public string[] roaMessages = new string[4]
        {
            "어디야?",
            "왜 안 와…",
            "지금 들어와줘.",   // TODO: 정식 메시지 도착 시 교체
            "기다리고 있을게."  // TODO: 정식 메시지 도착 시 교체
        };

        [Header("Hint Texts (입력창 위 안내)")]
        [Tooltip("기획서 §비밀번호 입력 커스텀 시스템 — 첫 설정")]
        [TextArea(2, 3)]
        public string hintFirstSetup = "앞으로 사용할 비밀번호를 입력해주세요.\n최대 7자까지 입력 가능합니다.";

        [Tooltip("설정 완료 직후")]
        public string hintComplete = "비밀번호 설정 완료!";

        [Tooltip("평상시 입력")]
        public string hintNormal = "비밀번호를 입력해주세요.";

        [Tooltip("1회 오류 시")]
        public string hintWrongOnce = "비밀번호가 일치하지 않습니다.";

        [Tooltip("2회 오류 시")]
        public string hintWrongTwice = "비밀번호를 다시 한 번 확인해주세요.";

        [Tooltip("기획서 §오류/분실 — 3회 이상 오류 시")]
        public string hintForgot = "비밀번호를 잊으셨다면 우측 하단 열쇠 모양 버튼을 눌러주세요.";

        [Header("Reset Confirm Popup")]
        public string resetConfirmTitle = "새로운 비밀번호 설정을\n진행하시겠습니까?";
        public string resetConfirmYes = "예";
        public string resetConfirmNo = "아니오";

        [Header("Clock")]
        [Tooltip("고정 시각 (HH:mm). 빈 문자열이면 실시간 OS 시각. 기획서: 23:58.")]
        public string fixedClockTime = "23:58";

        [Header("Sound (임시 — 정식 SFX 도착 시 교체)")]
        [Tooltip("로아 메시지 1개 출력 시 효과음. 임시로 dialoguenext.mp3 등 재활용 권장.")]
        public AudioClip messageSfx;
    }
}
