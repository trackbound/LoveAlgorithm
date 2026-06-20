using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // SaveLoadMode
using LoveAlgo.Events; // ShowSaveLoadCommand, OpenDialogueLogCommand, SetAutoModeCommand, ReturnToTitleCommand, ShowSettingsCommand, ShowModalCommand/ModalButton/ModalRequest/ModalButtonKind
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사창 버튼 바(*View): 스토리 중 타이틀/불러오기/설정/로그/오토/저장/숨기기 버튼 — 표시·발행만(ADR-007).
    /// Story 페이즈엔 빠른메뉴가 숨으므로(기획 분담: Schedule=빠른메뉴 / Story=대사창 바) 이 바가 스토리 쪽 진입점.
    /// 타이틀/불러오기/설정/저장은 QuickMenuView와 동일 명령을 발행(정본 단일 — ReturnToTitle/SaveLoad/Settings).
    /// DialogueView.Root(슬라이드 패널) 자식 버튼들을 묶어 배선 — 숨기기 슬라이드/CG/페이즈 토글에 자동 연동되므로
    /// (버튼이 Root와 함께 사라짐) 자체 숨김·게이트 로직 없음.
    ///
    /// 오토 상태는 <see cref="SetAutoModeCommand"/> 스트림이 정본 — 자기 발행 포함 구독으로만 아이콘을 미러해
    /// 다른 발행처(CG 진입 오토 정지 등)와 자동 동기. root 토글로 구독이 끊긴 동안 놓친 명령은 OnEnable에서
    /// 상시 구독자인 <see cref="DialogueView"/>(홀더 GO는 안 꺼짐)의 AutoMode를 읽어 재동기.
    ///
    /// 세이브는 현행 의미(감독 결정 2026-06-13): 스토리 위치(스크립트 진행·무대)는 저장되지 않음 — 위치 영속은 후속 🔴 슬라이스.
    /// </summary>
    public class DialogueButtonBarView : MonoBehaviour
    {
        [SerializeField] Button titleButton;
        [SerializeField] Button loadButton;
        [SerializeField] Button configButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button logButton;
        [SerializeField] Button autoButton;
        [SerializeField] Button hideButton;
        [Tooltip("오토 버튼 아이콘(상태 스프라이트 스왑 대상). 보통 autoButton의 Image.")]
        [SerializeField] Image autoIcon;
        [SerializeField] Sprite autoOnSprite;
        [SerializeField] Sprite autoOffSprite;
        [Tooltip("숨기기 대상 대사 뷰(같은 트리). 미바인딩 시 부모에서 자동 탐색.")]
        [SerializeField] DialogueView dialogueView;

        [Header("타이틀 복귀 확인 모달 (QuickMenu 종료 확인 미러)")]
        [SerializeField] string titleConfirmTitle = "타이틀로 돌아가기";
        [SerializeField] string titleConfirmMessage = "저장하지 않은 진행은 사라집니다.\n타이틀 화면으로 돌아가시겠습니까?";
        [SerializeField] string titleConfirmYes = "예";
        [SerializeField] string titleConfirmNo = "아니오";

        public Button TitleButton { get => titleButton; set => titleButton = value; }
        public Button LoadButton { get => loadButton; set => loadButton = value; }
        public Button ConfigButton { get => configButton; set => configButton = value; }
        public Button SaveButton { get => saveButton; set => saveButton = value; }
        public Button LogButton { get => logButton; set => logButton = value; }
        public Button AutoButton { get => autoButton; set => autoButton = value; }
        public Button HideButton { get => hideButton; set => hideButton = value; }
        public Image AutoIcon { get => autoIcon; set => autoIcon = value; }
        public Sprite AutoOnSprite { get => autoOnSprite; set => autoOnSprite = value; }
        public Sprite AutoOffSprite { get => autoOffSprite; set => autoOffSprite = value; }
        public DialogueView DialogueView { get => dialogueView; set => dialogueView = value; }

        IDisposable _autoSub;
        bool _auto;
        ButtonStateDriver _autoDriver; // 오토 버튼 토글 ON 비주얼(onState 자식 스왑) 구동.

        void Awake()
        {
            if (dialogueView == null) dialogueView = GetComponentInParent<DialogueView>(true);
            if (autoButton != null) _autoDriver = autoButton.GetComponent<ButtonStateDriver>();
            Bind(titleButton, OnTitle);                                                 // 스토리 중 오조작 방지: 예/아니오 확인 모달 경유
            Bind(loadButton, () => EventBus.Publish(new ShowSaveLoadCommand(SaveLoadMode.Load)));
            Bind(configButton, () => EventBus.Publish(new ShowSettingsCommand()));
            Bind(saveButton, () => EventBus.Publish(new ShowSaveLoadCommand(SaveLoadMode.Save)));
            Bind(logButton, () => EventBus.Publish(new OpenDialogueLogCommand()));
            Bind(autoButton, () => EventBus.Publish(new SetAutoModeCommand(!_auto)));
            Bind(hideButton, () => { if (dialogueView != null) dialogueView.HideByUser(); });
        }

        void OnEnable()
        {
            _autoSub = EventBus.Subscribe<SetAutoModeCommand>(e => { _auto = e.On; ApplyAutoIcon(); });
            if (dialogueView != null) _auto = dialogueView.AutoMode; // 비활성 동안 놓친 토글 재동기
            ApplyAutoIcon();
        }

        void OnDisable()
        {
            _autoSub?.Dispose();
            _autoSub = null;
        }

        void ApplyAutoIcon()
        {
            // 정본: ButtonStateDriver의 토글 ON 상태(onState 자식 스왑, 호버보다 우선) 구동.
            if (_autoDriver != null) _autoDriver.SetOn(_auto);
            // 폴백: onState 미배선 프리팹용 단일 아이콘 스프라이트 스왑(둘 다 미설정이면 무동작).
            if (autoIcon == null) return;
            var sprite = _auto ? autoOnSprite : autoOffSprite;
            if (sprite != null) autoIcon.sprite = sprite;
        }

        // 타이틀 복귀는 즉시가 아니라 확인 모달 — 아니오(좌)·예(우), "예"(index 1)일 때만 ReturnToTitleCommand 발행.
        void OnTitle() => EventBus.Publish(new ShowModalCommand(
            titleConfirmTitle, titleConfirmMessage,
            new[]
            {
                new ModalButton(titleConfirmNo, ModalButtonKind.No),
                new ModalButton(titleConfirmYes, ModalButtonKind.Yes),
            },
            new ModalRequest(i => { if (i == 1) EventBus.Publish(new ReturnToTitleCommand()); })));

        static void Bind(Button button, UnityEngine.Events.UnityAction onClick)
        {
            if (button != null) button.onClick.AddListener(onClick);
        }
    }
}
