using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading;
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
        [SerializeField] string defaultName = "성민";

        // 인라인 모드 (Flow,,Username): true일 때는 확인 후 OnNameConfirmed 대신 TCS에 이름을 설정
        bool _inlineMode;
        UniTaskCompletionSource<string> _inlineTcs;

        /// <summary>인라인 모드 여부 (Flow,,Username으로 열린 경우 true)</summary>
        public bool IsInline => _inlineMode;

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

            // 기본 이름 플레이스홀더
            if (inputField.placeholder is TMP_Text placeholder)
            {
                placeholder.text = $"{defaultName} (기본)";
            }
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
            // Enter 키로 제출 (빈 입력시 기본 이름 사용)
            OnConfirmClick();
        }

        void UpdateConfirmButton()
        {
            if (confirmButton == null) return;
            // 빈 입력이어도 기본 이름이 있으므로 항상 활성화
            confirmButton.interactable = true;
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
            if (string.IsNullOrWhiteSpace(name))
                name = defaultName;

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
                if (_inlineMode)
                {
                    // 인라인 모드: phase 전환 없이 TCS로 결과 전달 (대기 중인 UsernameFlowCommand가 이어받음)
                    var tcs = _inlineTcs;
                    _inlineTcs = null;
                    _inlineMode = false;
                    tcs?.TrySetResult(name);
                }
                else
                {
                    // 기본 흐름: GameManager로 이름 전달 (Username → Prologue 전환)
                    GameManager.Instance?.OnNameConfirmed(name);
                }
            }
            else
            {
                // 팝업 닫히면 다시 입력 포커스
                FocusInput();
            }
        }

        void OnBackClick()
        {
            if (_inlineMode)
            {
                // 인라인 모드에서는 뒤로가기 버튼 동작 없음 (기본 이름으로 결정되고 스토리 복귀되지 않도록 무시)
                return;
            }
            // Title로 복귀
            GameManager.Instance?.ChangePhase(GamePhase.Title);
        }

        /// <summary>
        /// 인라인 모드로 입력창을 열고 확정된 이름을 반환한다 (Flow,,Username 전용).
        /// GameFlowController의 phase 전환 없이 이름만 받아온다.
        /// </summary>
        public async UniTask<string> ShowInlineAsync(CancellationToken ct)
        {
            _inlineMode = true;
            _inlineTcs = new UniTaskCompletionSource<string>();

            using (ct.Register(() =>
            {
                _inlineTcs?.TrySetCanceled();
                _inlineTcs = null;
                _inlineMode = false;
            }))
            {
                gameObject.SetActive(true);
                ResetInput();
                FocusInput();
                return await _inlineTcs.Task;
            }
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

        void OnDestroy()
        {
            if (inputBox != null) DOTween.Kill(inputBox);
        }

        #endregion
    }
}
