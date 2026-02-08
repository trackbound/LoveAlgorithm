using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Core;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 이름 입력 UI
    /// </summary>
    public class UsernameUI : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] TMP_InputField inputField;
        [SerializeField] RectTransform inputBox;  // 흔들기 대상

        [Header("버튼")]
        [SerializeField] Button confirmButton;
        [SerializeField] Button backButton;

        [Header("텍스트 (선택)")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text hintText;

        [Header("설정")]
        [SerializeField] float shakeDuration = 0.3f;
        [SerializeField] float shakeStrength = 15f;

        void Start()
        {
            SetupInputField();
            SetupButtons();
        }

        void OnEnable()
        {
            ResetInput();
            FocusInput();
        }

        #region Setup

        void SetupInputField()
        {
            if (inputField == null) return;

            inputField.characterLimit = NameValidator.MaxLengthEnglish;
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.onSubmit.AddListener(OnInputSubmit);
        }

        void SetupButtons()
        {
            confirmButton?.onClick.AddListener(OnConfirmClick);
            backButton?.onClick.AddListener(OnBackClick);
        }

        #endregion

        #region Input

        void OnInputChanged(string value)
        {
            UpdateConfirmButton();
        }

        void OnInputSubmit(string value)
        {
            // Enter 키로 제출
            if (!string.IsNullOrWhiteSpace(value))
            {
                OnConfirmClick();
            }
        }

        void UpdateConfirmButton()
        {
            if (confirmButton == null) return;
            confirmButton.interactable = !string.IsNullOrWhiteSpace(inputField?.text);
        }

        void ResetInput()
        {
            if (inputField != null)
                inputField.text = "";
            UpdateConfirmButton();
        }

        void FocusInput()
        {
            if (inputField != null)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        #endregion

        #region Buttons

        void OnConfirmClick()
        {
            string name = inputField?.text?.Trim() ?? "";

            var result = NameValidator.Validate(name);

            if (result != NameValidator.Result.Valid)
            {
                ShowError(result).Forget();
                return;
            }

            // 확인 팝업
            ConfirmName(name).Forget();
        }

        async UniTaskVoid ConfirmName(string name)
        {
            bool confirmed = await PopupManager.Instance.ConfirmAsync(
                $"'{name}'(으)로 시작할까요?"
            );

            if (confirmed)
            {
                // GameManager에 이름 전달
                GameManager.Instance?.OnNameConfirmed(name);
            }
            else
            {
                // 팝업 닫히면 다시 입력 포커스
                FocusInput();
            }
        }

        void OnBackClick()
        {
            // Title로 복귀
            GameManager.Instance?.ChangePhase(GamePhase.Title);
        }

        #endregion

        #region Error

        async UniTaskVoid ShowError(NameValidator.Result result)
        {
            // 1. 에러 사운드
            Story.AudioManager.Instance?.PlaySFX("Error");

            // 2. InputBox 흔들기
            if (inputBox != null)
            {
                await inputBox.DOShakeAnchorPos(shakeDuration, new Vector2(shakeStrength, 0), 12, 0)
                    .AsyncWaitForCompletion();
            }

            // 3. 입력 초기화
            ResetInput();

            // 4. 에러 알림
            string message = NameValidator.GetErrorMessage(result);
            await PopupManager.Instance.AlertAsync(message);

            // 5. 다시 포커스
            FocusInput();
        }

        #endregion
    }
}
