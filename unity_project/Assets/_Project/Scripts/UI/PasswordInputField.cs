using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 비밀번호 입력칸(*View: LockScreen). TMP_InputField를 래핑해 7자 제한,
    /// 모드별 마스킹 기본값(★/평문)과 눈 토글, 오류 시 빠른 진동을 담당한다(스펙 §3).
    /// 마스킹은 <see cref="TMP_InputField.contentType"/> Standard↔Password 전환 + 라벨 강제 갱신.
    /// 진동은 코루틴 1버스트(WarnWidgetShake 관례), 종료 시 기준 위치 복원. 수치는 인스펙터 노출.
    /// </summary>
    public class PasswordInputField : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;
        [Tooltip("눈 아이콘 Image(감은눈/뜬눈 스프라이트 교체).")]
        [SerializeField] Image eyeIcon;
        [Tooltip("눈 토글 버튼. 클릭 시 마스킹 반전. 미바인딩 시 토글 직접 호출만 가능.")]
        [SerializeField] Button eyeButton;
        [SerializeField] Sprite eyeClosedSprite; // 감은눈 = 마스킹(★)
        [SerializeField] Sprite eyeOpenSprite;   // 뜬눈 = 평문 노출
        [SerializeField] int maxLength = 7;

        [Header("Shake")]
        [SerializeField] float shakeAmplitude = 12f;
        [SerializeField] float shakeFrequency = 60f;
        [SerializeField] float shakeDuration = 0.25f;

        public TMP_InputField Input
        {
            get => input;
            set { input = value; ApplyCharacterLimit(); }
        }
        public Image EyeIcon { get => eyeIcon; set => eyeIcon = value; }
        public Button EyeButton { get => eyeButton; set => eyeButton = value; }
        public Sprite EyeClosedSprite { get => eyeClosedSprite; set => eyeClosedSprite = value; }
        public Sprite EyeOpenSprite { get => eyeOpenSprite; set => eyeOpenSprite = value; }
        public int MaxLength
        {
            get => maxLength;
            set { maxLength = value; ApplyCharacterLimit(); }
        }
        public float ShakeDuration { get => shakeDuration; set => shakeDuration = value; }
        public bool Masked { get; private set; }

        Coroutine _shakeCo;

        void OnEnable()
        {
            ApplyCharacterLimit();
            if (eyeButton != null) eyeButton.onClick.AddListener(ToggleEye);
        }

        /// <summary>7자 제한 적용. 인스펙터 배선·런타임 셋업 어느 경로에서도 즉시 반영(OnEnable 타이밍 비의존).</summary>
        void ApplyCharacterLimit()
        {
            if (input != null) input.characterLimit = maxLength;
        }

        void OnDisable()
        {
            if (eyeButton != null) eyeButton.onClick.RemoveListener(ToggleEye);
        }

        /// <summary>마스킹 on/off — contentType 전환 + 라벨 갱신 + 눈 스프라이트 동기.</summary>
        public void SetMasked(bool masked)
        {
            Masked = masked;
            if (input != null)
            {
                input.contentType = masked ? TMP_InputField.ContentType.Password
                                           : TMP_InputField.ContentType.Standard;
                input.ForceLabelUpdate();
            }
            if (eyeIcon != null)
            {
                var s = masked ? eyeClosedSprite : eyeOpenSprite;
                if (s != null) eyeIcon.sprite = s;
            }
        }

        public void ToggleEye() => SetMasked(!Masked);

        public void ResetField()
        {
            if (input != null) input.text = "";
        }

        /// <summary>오류 시 빠른 진동 1버스트(감쇠). 종료 시 기준 위치 복원. 공용 <see cref="UiNudge"/> 위임.</summary>
        public void Shake()
        {
            var rt = input != null ? input.transform as RectTransform : null;
            if (rt == null) return;
            UiNudge.Shake(this, rt, ref _shakeCo, shakeAmplitude, shakeFrequency, shakeDuration);
        }
    }
}
