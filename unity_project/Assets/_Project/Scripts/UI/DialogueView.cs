using System;
using System.Collections;
using System.Collections.Generic; // IReadOnlyList<InlinePause>
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand, CompletionHandle
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems; // IPointerClickHandler
using UnityEngine.InputSystem;  // Keyboard (스페이스 진행)

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 표시 뷰(*View). <see cref="ShowDialogueCommand"/>를 구독해 화자/본문을 타이핑하고, 클릭 진행을
    /// 받아 완료 핸들(<see cref="CompletionHandle"/>)을 완료한다(ADR-007: UI는 표시만, 상태 변경 없음).
    /// 엔진(NarrativeController)은 이 뷰를 직접 알지 못한다 — 명령 이벤트 + 완료 핸들로만 연결.
    ///
    /// 슬라이스1 범위: 화자+본문 타이핑 + 클릭(스킵/진행). 인라인 태그(&lt;emote&gt;/&lt;wait&gt;)·오토모드·
    /// 로그·페이드는 범위 밖(후속). 클릭 입력은 <see cref="IPointerClickHandler"/>(전체화면 Graphic raycast
    /// 필요) 또는 외부에서 <see cref="Advance"/> 호출. TMP_Text는 인스펙터 바인딩(미바인딩 시 조용히 무시).
    /// </summary>
    public class DialogueView : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("대사창 비주얼 루트(선택). 지정 시 표시/숨김을 토글.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text speakerText;
        [SerializeField] TMP_Text bodyText;
        [Tooltip("글자당 타이핑 간격(초). 0이면 즉시 표시.")]
        [SerializeField] float charInterval = 0.02f;
        [Tooltip("오토 모드에서 타이핑 완료 후 자동 진행까지 지연(초). 클릭하면 즉시 진행.")]
        [SerializeField] float autoAdvanceDelay = 1.2f;

        public GameObject Root { get => root; set => root = value; }
        public TMP_Text SpeakerText { get => speakerText; set => speakerText = value; }
        public TMP_Text BodyText { get => bodyText; set => bodyText = value; }
        public float CharInterval { get => charInterval; set => charInterval = value; }
        public float AutoAdvanceDelay { get => autoAdvanceDelay; set => autoAdvanceDelay = value; }
        public bool AutoMode { get => _auto; set => _auto = value; }

        IDisposable _sub, _autoSub, _cgSub, _visSub;
        Coroutine _typeRoutine;
        CompletionHandle _active;
        bool _typing;
        bool _skipTyping;
        bool _awaitingClick;
        bool _auto;
        bool _cgHidden;
        IReadOnlyList<InlinePause> _pauses;
        IReadOnlyList<InlineEmote> _emotes;
        string _speaker;
        string _speakerId; // 별칭 해석된 캐릭터 코드 ID(없으면 null → _speaker 폴백)

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowDialogueCommand>(OnShow);
            _autoSub = EventBus.Subscribe<SetAutoModeCommand>(e => _auto = e.On);
            _cgSub = EventBus.Subscribe<SetCgModeCommand>(OnCgMode);
            _visSub = EventBus.Subscribe<SetDialogueVisibleCommand>(e => { if (root != null) root.SetActive(e.Visible); });
        }

        void OnDisable()
        {
            _sub?.Dispose();
            _autoSub?.Dispose();
            _cgSub?.Dispose();
            _visSub?.Dispose();
            _sub = _autoSub = _cgSub = _visSub = null;
        }

        // CG 컷신 진입 시 대사창을 숨기고 종료 시 복원(ADR-007: CG 뷰가 직접 참조하지 않고 명령으로). 대칭 토글.
        void OnCgMode(SetCgModeCommand e)
        {
            if (root == null) return;
            if (e.Active)
            {
                if (root.activeSelf) { root.SetActive(false); _cgHidden = true; }
            }
            else if (_cgHidden)
            {
                root.SetActive(true);
                _cgHidden = false;
            }
        }

        void OnShow(ShowDialogueCommand e)
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _active = e.Handle;
            if (root != null) root.SetActive(true);
            _speaker = e.Speaker;
            _speakerId = e.SpeakerId;
            if (speakerText != null) speakerText.text = e.Speaker ?? "";
            _pauses = e.Pauses; // 인라인 <wait> 멈춤 지점(없으면 null).
            _emotes = e.Emotes; // 인라인 <emote> 표정 지점(없으면 null).
            _typeRoutine = StartCoroutine(TypeRoutine(e.Text ?? "", e.RequireClick));
        }

        IEnumerator TypeRoutine(string text, bool requireClick)
        {
            _typing = true;
            _skipTyping = false;
            _awaitingClick = false;

            // 타이핑/멈춤 인덱스는 문자열 길이 기준 — 파서(InlineTagParser)의 CharIndex와 정합하고 TMP 메시 의존 제거.
            int total = text.Length;
            if (bodyText != null)
            {
                bodyText.text = text;
                bodyText.maxVisibleCharacters = 0;
            }

            if (charInterval > 0f && total > 0)
            {
                for (int i = 0; i <= total; i++)
                {
                    if (_skipTyping) break;
                    if (bodyText != null) bodyText.maxVisibleCharacters = i;

                    // 인라인 <wait>: i번째 글자 표시 직후 멈춤(스킵 중이면 무시).
                    if (_pauses != null)
                    {
                        for (int p = 0; p < _pauses.Count && !_skipTyping; p++)
                            if (_pauses[p].CharIndex == i)
                                yield return new WaitForSeconds(_pauses[p].Seconds);
                    }

                    // 인라인 <emote>: i번째 글자 시점에 화자 표정 변경 명령 발행(StageView가 슬롯 해석·교체).
                    FireEmotesAt(i);

                    yield return new WaitForSeconds(charInterval);
                }
            }

            if (bodyText != null) bodyText.maxVisibleCharacters = int.MaxValue;
            // 즉시표시(루프 미실행)거나 스킵으로 끊긴 경우 — 못 발행한 표정을 최종 상태로 마저 발행.
            if (_emotes != null && (_skipTyping || charInterval <= 0f || total == 0))
                FireAllEmotes();
            _typing = false;

            if (requireClick)
            {
                _awaitingClick = true;
                if (_auto)
                {
                    // 오토: 지연만큼 대기하되 클릭(_awaitingClick=false)이 오면 즉시 진행.
                    float t = 0f;
                    while (_awaitingClick && t < autoAdvanceDelay)
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }
                    _awaitingClick = false;
                }
                else
                {
                    yield return new WaitUntil(() => !_awaitingClick);
                }
            }

            _typeRoutine = null;
            _active?.Complete();
            _active = null;
        }

        // 인라인 <emote> 발행: charIndex 시점의 표정 변경 명령(화자→슬롯 해석은 StageView 구독자 몫).
        void FireEmotesAt(int charIndex)
        {
            if (_emotes == null) return;
            for (int m = 0; m < _emotes.Count; m++)
                if (_emotes[m].CharIndex == charIndex)
                    EventBus.Publish(new ShowSpeakerEmoteCommand(EmoteSpeaker, _emotes[m].Emote));
        }

        void FireAllEmotes()
        {
            if (_emotes == null) return;
            for (int m = 0; m < _emotes.Count; m++)
                EventBus.Publish(new ShowSpeakerEmoteCommand(EmoteSpeaker, _emotes[m].Emote));
        }

        // 슬롯 매칭은 해석된 코드 ID 우선(StageView가 Char 명령의 코드 ID를 추적) — 미해석 시 원문 화자명 폴백.
        string EmoteSpeaker => string.IsNullOrEmpty(_speakerId) ? _speaker : _speakerId;

        // 스페이스로도 진행(좌클릭과 동일). 신 Input System 직접 읽기 — 뷰가 활성(내러티브 중)일 때만 Update 동작.
        // 단 입력 필드(LockScreen 비번 등) 타이핑 중엔 무시 — 스페이스가 텍스트 입력 대신 대사 진행을 가로채지 않도록.
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;
            if (IsTextInputFocused()) return;
            Advance("스페이스");
        }

        // EventSystem이 현재 포커스한 GO가 입력 중인 TMP_InputField인가(있으면 스페이스를 진행으로 가로채지 않는다).
        static bool IsTextInputFocused()
        {
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            var field = sel.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }

        /// <summary>클릭/진행 입력. 타이핑 중이면 즉시 전체 표시, 클릭 대기 중이면 완료 핸들을 풀어준다.
        /// <paramref name="source"/>는 디버그 로그용 입력 출처(좌클릭/스페이스 등, <see cref="DebugInput"/>).</summary>
        public void Advance(string source = null)
        {
            string s = source ?? "?";
            if (_typing) { _skipTyping = true; DebugInput.Log($"{s} → 대사 타이핑 스킵"); }
            else if (_awaitingClick) { _awaitingClick = false; DebugInput.Log($"{s} → 대사 진행(다음)"); }
            else DebugInput.Log($"{s} → 입력됐으나 진행할 대사 없음");
        }

        public void OnPointerClick(PointerEventData eventData) => Advance("좌클릭");
    }
}
