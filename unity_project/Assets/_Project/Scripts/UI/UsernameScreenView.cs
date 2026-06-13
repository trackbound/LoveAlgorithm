using System;
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
    /// 세이브 직렬화)에 저장 후 핸들을 풀어 스토리가 이어진다. 빈 입력은 확인 무시(이름은 전 스토리
    /// <c>{{Player}}</c> 치환에 쓰이므로 입력 강제). 비주얼 = 자식 <see cref="overlay"/> 토글(홀더 GO 불변 —
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

        public GameStateSO State { get => state; set => state = value; }
        public GameObject Overlay { get => overlay; set => overlay = value; }
        public TMP_InputField Input { get => input; set => input = value; }
        public Button ConfirmButton { get => confirmButton; set => confirmButton = value; }
        public bool IsShown => overlay != null && overlay.activeSelf;

        readonly List<IDisposable> _subs = new();
        CompletionHandle _pending;

        void Awake()
        {
            if (overlay == gameObject)
            {
                Debug.LogError("[UsernameScreenView] overlay가 뷰 GO 자신으로 바인딩 — 비주얼 자식(Box)을 바인딩해야 한다. 토글 생략.");
                overlay = null;
            }
            if (confirmButton != null) confirmButton.onClick.AddListener(Submit);
            if (overlay != null) overlay.SetActive(false); // 부팅 숨김(authored-active 방어)
        }

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
            if (overlay != null) overlay.SetActive(true);
            if (input != null)
            {
                input.text = "";
                input.ActivateInputField();
            }
        }

        /// <summary>확인 — 빈 입력(공백뿐)은 무시(입력 강제), 유효하면 저장+핸들 완료+숨김.</summary>
        public void Submit()
        {
            if (_pending == null && !IsShown) return;
            string name = input != null ? input.text.Trim() : "";
            if (string.IsNullOrEmpty(name)) return; // 입력 강제 — {{Player}} 치환 전제

            if (state != null) state.Data.playerName = name;
            else Debug.LogError("[UsernameScreenView] state(GameStateSO) 미바인딩 — 이름 저장 불가.");

            HideImmediate();
        }

        void HideImmediate()
        {
            if (overlay != null) overlay.SetActive(false);
            _pending?.Complete();
            _pending = null;
        }
    }
}
