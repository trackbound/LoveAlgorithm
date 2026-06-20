using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // ShowUsernameCommand, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 플레이어 이름 입력 화면 뷰(*View, _Screen 축 — LockScreenView 미러). 스토리 Flow <c>Username</c>이
    /// <see cref="ShowUsernameCommand"/>(완료 핸들)로 띄우고, 확인 시 이름을 GameStateSO(<c>Data.playerName</c>,
    /// 세이브 직렬화)에 저장 후 핸들을 풀어 스토리가 이어진다. 빈 입력은 차단하지 않고 기본 이름
    /// (<see cref="defaultName"/> — "성민")으로 진행(엔터/확인 동일). 확정(엔터/확인) 시 곧장 저장하지 않고 yes/no
    /// 최종 확인 모달(<see cref="ShowModalCommand"/>)을 띄워 Yes에서만 저장한다(No=재입력). 비주얼 = 자식 <see cref="overlay"/> 토글(홀더 GO 불변 —
    /// 구독 유지 불변식), 안전망 = NarrativeFinished/Reset 시 즉시 숨김+핸들 해제(LockScreenView 미러).
    /// </summary>
    public class UsernameScreenView : MonoBehaviour
    {
        [Tooltip("이름 저장 대상 상태 SO(GameState_Main).")]
        [SerializeField] GameStateSO state;
        [Tooltip("비주얼 루트(자식 Box — 홀더 GO 자신 금지).")]
        [SerializeField] GameObject overlay;
        [SerializeField] TMP_InputField input;
        [SerializeField] Button confirmButton;
        [Tooltip("무효 입력 시 사유 표시(TMP). 미바인딩 시 메시지 생략.")]
        [SerializeField] TMP_Text errorLabel;

        [Tooltip("입력 없이 확정(엔터/확인) 시 사용할 기본 이름. 빈 입력은 막지 않고 이 이름으로 저장.")]
        [SerializeField] string defaultName = "성민";

        [Header("최종 확인 모달")]
        [Tooltip("확정 시 띄우는 yes/no 모달 제목.")]
        [SerializeField] string confirmTitle = "이름 확인";
        [Tooltip("확인 모달 본문. '{name}' 토큰이 입력/기본 이름으로 치환된다.")]
        [SerializeField] string confirmMessage = "'{name}'(으)로 시작할까요?";

        [Tooltip("시작 크로스페이드(페이드인) 지속(초). 0이면 즉시 표시. overlay의 CanvasGroup 알파 0→1로 부드럽게 진입(없으면 런타임 추가).")]
        [SerializeField] float fadeInDuration = 0.3f;

        public GameStateSO State { get => state; set => state = value; }
        public GameObject Overlay { get => overlay; set => overlay = value; }
        public TMP_InputField Input { get => input; set => input = value; }
        public Button ConfirmButton { get => confirmButton; set => confirmButton = value; }
        public TMP_Text ErrorLabel { get => errorLabel; set => errorLabel = value; }
        public string DefaultName { get => defaultName; set => defaultName = value; }
        public string ConfirmTitle { get => confirmTitle; set => confirmTitle = value; }
        public string ConfirmMessage { get => confirmMessage; set => confirmMessage = value; }
        public bool IsShown => overlay != null && overlay.activeSelf;

        readonly List<IDisposable> _subs = new();
        CompletionHandle _pending;
        CanvasGroup _cg;          // 페이드 대상(overlay의 CanvasGroup — get-or-add)
        Coroutine _fadeRoutine;
        Coroutine _shakeCo;       // 오류 흔들림(UiNudge)

        void Awake()
        {
            if (overlay == gameObject)
            {
                Debug.LogError("[UsernameScreenView] overlay가 뷰 GO 자신으로 바인딩 — 비주얼 자식(Box)을 바인딩해야 한다. 토글 생략.");
                overlay = null;
            }
            if (confirmButton != null) confirmButton.onClick.AddListener(Submit);
            if (input != null) input.onSubmit.AddListener(OnInputSubmit); // 엔터 확정(확정 버튼과 동일 — PasswordInputField 미러)
            if (overlay != null) overlay.SetActive(false); // 부팅 숨김(authored-active 방어)
        }

        void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Submit);
            if (input != null) input.onSubmit.RemoveListener(OnInputSubmit);
        }

        // 엔터(onSubmit) → 확정. 확정 버튼과 동일 경로(빈/무효 입력은 Submit이 무시).
        void OnInputSubmit(string _) => Submit();

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<ShowUsernameCommand>(OnShow));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => HideImmediate()));
            _subs.Add(EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => HideImmediate()));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
            HideImmediate();
        }

        void OnShow(ShowUsernameCommand e)
        {
            _pending?.Complete(); // 중복 표시 안전망 — 앞선 핸들 hang 방지(fail-open)
            _pending = e.Handle;
            if (overlay != null)
            {
                overlay.SetActive(true);
                StartFadeIn(); // 시작 크로스페이드(페이드인)
            }
            if (input != null)
            {
                input.text = "";
                input.ActivateInputField();
            }
        }

        // overlay의 CanvasGroup 알파 0→1로 짧게 페이드인(없으면 런타임 추가). 0초/비활성이면 즉시 표시(폴백).
        void StartFadeIn()
        {
            if (overlay == null) return;
            if (_cg == null)
            {
                var cg = overlay.GetComponent<CanvasGroup>();
                _cg = cg != null ? cg : overlay.AddComponent<CanvasGroup>();
            }
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            if (!isActiveAndEnabled || fadeInDuration <= 0f) { _cg.alpha = 1f; return; }
            _fadeRoutine = StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            float t = 0f;
            _cg.alpha = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                _cg.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            _cg.alpha = 1f;
            _fadeRoutine = null;
        }

        /// <summary>확인 — 빈 입력(공백뿐)은 기본 이름, 아니면 유효성 검사. 확정 후보가 정해지면 yes/no 모달을 띄우고
        /// (무효 입력은 모달 없이 재입력 유도) Yes에서만 저장+핸들 완료+숨김, No면 화면 유지·재입력.</summary>
        public void Submit()
        {
            if (_pending == null && !IsShown) return;
            string raw = input != null ? input.text.Trim() : "";
            string name;
            if (string.IsNullOrEmpty(raw)) // 빈 입력 → 기본 이름으로 진행(차단 안 함)
            {
                name = defaultName;
            }
            else
            {
                var result = NameValidator.Validate(raw);
                if (result != NameValidator.Result.Valid)
                {
                    if (errorLabel != null) errorLabel.text = NameValidator.GetErrorMessage(result);
                    var rt = input != null ? input.transform as RectTransform : null;
                    if (rt != null) UiNudge.Shake(this, rt, ref _shakeCo);
                    return; // 무효 → 모달 없이 재입력
                }
                name = raw;
            }

            if (errorLabel != null) errorLabel.text = "";
            ShowConfirm(name); // 최종 확인 모달 → Yes에서만 저장
        }

        // 확정 후보 이름으로 yes/no 모달 발행. Yes(index 1)면 저장+숨김, No면 입력칸 재활성(화면 유지).
        void ShowConfirm(string name)
        {
            string msg = (confirmMessage ?? "").Replace("{name}", name);
            EventBus.Publish(new ShowModalCommand(
                confirmTitle, msg,
                new[] { new ModalButton(null, ModalButtonKind.No), new ModalButton(null, ModalButtonKind.Yes) },
                new ModalRequest(i =>
                {
                    if (i == 1) SaveAndHide(name);
                    else if (input != null) input.ActivateInputField(); // No → 재입력
                })));
        }

        void SaveAndHide(string name)
        {
            if (state != null) state.Data.playerName = name;
            else Debug.LogError("[UsernameScreenView] state(GameStateSO) 미바인딩 — 이름 저장 불가.");
            HideImmediate();
        }

        void HideImmediate()
        {
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            if (_cg != null) _cg.alpha = 1f; // 다음 표시가 0에서 다시 페이드인하도록 리셋(끊긴 페이드 잔여 알파 방지)
            if (overlay != null) overlay.SetActive(false);
            _pending?.Complete();
            _pending = null;
        }
    }
}
