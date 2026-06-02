using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand, DialogueRequest
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems; // IPointerClickHandler

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 표시 뷰(*View). <see cref="ShowDialogueCommand"/>를 구독해 화자/본문을 타이핑하고, 클릭 진행을
    /// 받아 완료 핸들(<see cref="DialogueRequest"/>)을 완료한다(ADR-007: UI는 표시만, 상태 변경 없음).
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

        public GameObject Root { get => root; set => root = value; }
        public TMP_Text SpeakerText { get => speakerText; set => speakerText = value; }
        public TMP_Text BodyText { get => bodyText; set => bodyText = value; }
        public float CharInterval { get => charInterval; set => charInterval = value; }

        IDisposable _sub;
        Coroutine _typeRoutine;
        DialogueRequest _active;
        bool _typing;
        bool _skipTyping;
        bool _awaitingClick;

        void OnEnable() => _sub = EventBus.Subscribe<ShowDialogueCommand>(OnShow);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        void OnShow(ShowDialogueCommand e)
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _active = e.Handle;
            if (root != null) root.SetActive(true);
            if (speakerText != null) speakerText.text = e.Speaker ?? "";
            _typeRoutine = StartCoroutine(TypeRoutine(e.Text ?? "", e.RequireClick));
        }

        IEnumerator TypeRoutine(string text, bool requireClick)
        {
            _typing = true;
            _skipTyping = false;
            _awaitingClick = false;

            int total = 0;
            if (bodyText != null)
            {
                bodyText.text = text;
                bodyText.ForceMeshUpdate();
                total = bodyText.textInfo.characterCount;
                bodyText.maxVisibleCharacters = 0;
            }

            if (charInterval > 0f && total > 0)
            {
                for (int i = 0; i <= total; i++)
                {
                    if (_skipTyping) break;
                    if (bodyText != null) bodyText.maxVisibleCharacters = i;
                    yield return new WaitForSeconds(charInterval);
                }
            }

            if (bodyText != null) bodyText.maxVisibleCharacters = int.MaxValue;
            _typing = false;

            if (requireClick)
            {
                _awaitingClick = true;
                yield return new WaitUntil(() => !_awaitingClick);
            }

            _typeRoutine = null;
            _active?.Complete();
            _active = null;
        }

        /// <summary>클릭/진행 입력. 타이핑 중이면 즉시 전체 표시, 클릭 대기 중이면 완료 핸들을 풀어준다.</summary>
        public void Advance()
        {
            if (_typing) _skipTyping = true;
            else if (_awaitingClick) _awaitingClick = false;
        }

        public void OnPointerClick(PointerEventData eventData) => Advance();
    }
}
