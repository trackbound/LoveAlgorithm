using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// 비밀번호 입력 위젯 (기획서 §비밀번호 입력 커스텀 시스템).
    /// - TMP_InputField (자유 문자 1~7자, 한글 IME 지원)
    /// - 눈 토글: 감은눈 기본(마스킹) / 뜬눈(평문). 첫 설정 모드에서는 SetMaskMode(false).
    /// - 오류 시 빠른 진동 (코루틴, DOTween 비의존)
    /// - 3회 이상 오류 시 우측 하단 열쇠 아이콘
    /// </summary>
    public class PasswordInputWidget : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] TMP_InputField inputField;
        [Tooltip("LOGIN 또는 입력 완료 버튼")]
        [SerializeField] Button confirmButton;

        [Header("Reveal Toggle")]
        [Tooltip("isOn=true → 평문 노출(뜬눈), false → 마스킹(감은눈, 기본)")]
        [SerializeField] Toggle revealToggle;

        [Header("Key Icon")]
        [SerializeField] GameObject keyIcon;
        [Tooltip("열쇠 클릭 — LockScreenPanel가 ConfirmPopup 표시")]
        [SerializeField] Button keyButton;

        [Header("Settings")]
        [Tooltip("최대 입력 글자수 (기획서: 7자)")]
        [SerializeField] int maxLength = 7;

        [Header("Shake Animation")]
        [SerializeField] RectTransform shakeTarget;
        [SerializeField] float shakeStrength = 10f;
        [SerializeField] float shakeDuration = 0.35f;

        Vector2 shakeOriginalPos;
        Coroutine shakeCo;
        bool _loginOnly;

        public event Action<string> OnPasswordEntered;
        public event Action OnKeyClicked;

        void Awake()
        {
            if (inputField != null)
            {
                inputField.characterLimit = maxLength;
                inputField.onSubmit.AddListener(_ => Confirm());
            }
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (revealToggle != null) revealToggle.onValueChanged.AddListener(OnRevealChanged);
            if (keyButton != null) keyButton.onClick.AddListener(() => OnKeyClicked?.Invoke());
            if (shakeTarget != null) shakeOriginalPos = shakeTarget.anchoredPosition;
        }

        void OnEnable()
        {
            Clear();
            SetKeyIcon(false);
            SetMaskMode(true); // 기본: 감은눈
        }

        public void Clear()
        {
            if (inputField != null)
            {
                inputField.SetTextWithoutNotify("");
                inputField.ActivateInputField();
            }
        }

        public void SetKeyIcon(bool show)
        {
            if (keyIcon != null) keyIcon.SetActive(show);
        }

        /// <summary>
        /// LOGIN 버튼만 노출 (게임 첫 진입 GameStart 모드).
        /// InputField + 눈 토글 + 열쇠 아이콘 모두 숨김. Confirm() 호출 시 빈 비번으로 OnPasswordEntered 발행.
        /// </summary>
        public void SetLoginOnly(bool loginOnly)
        {
            _loginOnly = loginOnly;
            if (inputField != null) inputField.gameObject.SetActive(!loginOnly);
            if (revealToggle != null) revealToggle.gameObject.SetActive(!loginOnly);
            if (keyIcon != null && loginOnly) keyIcon.SetActive(false);
        }

        /// <summary>
        /// 마스킹 모드. 기획서 §개발: 첫 설정 시 *로 표기되지 않음.
        /// FirstSetup/Reset → SetMaskMode(false). Normal → SetMaskMode(true).
        /// </summary>
        public void SetMaskMode(bool mask)
        {
            if (inputField != null)
            {
                inputField.contentType = mask ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
                inputField.ForceLabelUpdate();
            }
            if (revealToggle != null) revealToggle.SetIsOnWithoutNotify(!mask);
        }

        public void PlayShake()
        {
            if (shakeTarget == null) return;
            if (shakeCo != null) StopCoroutine(shakeCo);
            shakeCo = StartCoroutine(ShakeRoutine());
        }

        public void Confirm()
        {
            // LoginOnly(게임 첫 진입 GameStart 모드) — 비번 검증 우회, 빈 문자열로 발행
            if (_loginOnly)
            {
                OnPasswordEntered?.Invoke("");
                return;
            }

            if (inputField == null) return;
            string pwd = inputField.text ?? "";
            if (!PasswordHasher.IsValidPassword(pwd))
            {
                Debug.LogWarning($"[LockScreen] Confirm rejected — invalid length ({pwd.Length})");
                PlayShake();
                return;
            }
            OnPasswordEntered?.Invoke(pwd);
        }

        void OnRevealChanged(bool reveal)
        {
            if (inputField == null) return;
            inputField.contentType = reveal ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            inputField.ForceLabelUpdate();
        }

        IEnumerator ShakeRoutine()
        {
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.unscaledDeltaTime;
                float damper = 1f - Mathf.Clamp01(t / shakeDuration);
                float x = (UnityEngine.Random.value * 2f - 1f) * shakeStrength * damper;
                float y = (UnityEngine.Random.value * 2f - 1f) * shakeStrength * damper * 0.3f;
                shakeTarget.anchoredPosition = shakeOriginalPos + new Vector2(x, y);
                yield return null;
            }
            shakeTarget.anchoredPosition = shakeOriginalPos;
            shakeCo = null;

            // 오류 후 자동 클리어
            if (inputField != null)
            {
                inputField.SetTextWithoutNotify("");
                inputField.ActivateInputField();
            }
        }
    }
}
