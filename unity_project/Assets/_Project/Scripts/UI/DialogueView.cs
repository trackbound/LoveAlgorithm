using System;
using System.Collections;
using System.Collections.Generic; // IReadOnlyList<InlinePause>
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // SettingsSO (속도 초기값)
using LoveAlgo.Events; // ShowDialogueCommand, CompletionHandle
using TMPro;
using UnityEngine;
using UnityEngine.UI;           // Image (독백 점 애니메이션)
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

        [Header("설정 속도 매핑 (정규화 0=느림~1=빠름 → 초)")]
        [SerializeField] float slowCharInterval = 0.08f;
        [SerializeField] float fastCharInterval = 0.004f;
        [SerializeField] float slowAutoDelay = 2.5f;
        [SerializeField] float fastAutoDelay = 0.3f;
        [Tooltip("속도 초기값 소스(미바인딩 시 SettingsSO.Shared).")]
        [SerializeField] SettingsSO settings;

        [Header("숨기기 슬라이드 + 보이기 버튼 (인포바 Hide)")]
        [Tooltip("숨기기 시 아래로 슬라이드할 패널. 미지정 시 root의 RectTransform.")]
        [SerializeField] RectTransform slidePanel;
        [Tooltip("숨김 동안 기존 대사창 위치에 뜨는 '보이기' 버튼 루트(GameObject). 부팅 inactive.")]
        [SerializeField] GameObject showButton;
        [Tooltip("슬라이드 다운/업 시간(초). 0이면 즉시.")]
        [SerializeField] float slideDuration = 0.25f;
        [Tooltip("하강 거리(px). 0이면 패널 높이+여백 자동(화면 아래로 완전히 사라짐).")]
        [SerializeField] float slideDistance = 0f;

        [Header("진행 표시(End Mark)")]
        [Tooltip("대사 진행 아이콘(NextIndicator RectTransform). 타이핑 완료 후 본문 마지막 글자 뒤에 표시하고 진행 시 숨김. 미지정 시 무시.")]
        [SerializeField] RectTransform endMark;
        [Tooltip("마지막 글자 오른쪽 끝 기준 아이콘 위치 보정(px). 아이콘 중심이 글자 끝에 오므로 x는 보통 +(절반폭+여백). y=세로 미세조정.")]
        [SerializeField] Vector2 endMarkOffset = new Vector2(20f, 0f);
        [Tooltip("진행 아이콘 위아래 바운스 진폭(px). 0이면 정지.")]
        [SerializeField] float endMarkBobAmplitude = 4f;
        [Tooltip("진행 아이콘 바운스 속도(라디안/초).")]
        [SerializeField] float endMarkBobSpeed = 6f;

        [Header("독백 점 애니메이션 (화자 빈 칸 = 주인공 독백)")]
        [Tooltip("이름 위치에 뜨는 점 Image(MonoDot). 독백 라인에서 화자명 대신 표시·애니메이션. 미바인딩 시 무동작.")]
        [SerializeField] Image monoDot;
        [Tooltip("루프 재생할 점 프레임(mono_dot_0~4 순서). 비어 있으면 점 애니메이션 비활성.")]
        [SerializeField] Sprite[] monoDotFrames;
        [Tooltip("점 프레임 교체 간격(초).")]
        [SerializeField] float monoDotInterval = 0.15f;

        [Header("눈감김(아이마스크) 동안 정렬 상승")]
        [Tooltip("눈꺼풀 차폐 중에만 대사창 Canvas를 이 정렬값으로 올려 암전 위에 대사가 보이게 한다. " +
                 "눈꺼풀 바(95)보다 크고 ScreenFade(100)보다 작아야 함. 차폐 해제 시 overrideSorting=off로 복원(평상시 모달/팝업 아래).")]
        [SerializeField] int eyeMaskShroudSortingOrder = 96;

        public GameObject Root { get => root; set => root = value; }
        public RectTransform SlidePanel { get => slidePanel; set => slidePanel = value; }
        public GameObject ShowButton { get => showButton; set => showButton = value; }
        public float SlideDuration { get => slideDuration; set => slideDuration = value; }
        public float SlideDistance { get => slideDistance; set => slideDistance = value; }
        public TMP_Text SpeakerText { get => speakerText; set => speakerText = value; }
        public TMP_Text BodyText { get => bodyText; set => bodyText = value; }
        public float CharInterval { get => charInterval; set => charInterval = value; }
        public float AutoAdvanceDelay { get => autoAdvanceDelay; set => autoAdvanceDelay = value; }
        public bool AutoMode { get => _auto; set => _auto = value; }
        public RectTransform EndMark { get => endMark; set => endMark = value; }
        public Vector2 EndMarkOffset { get => endMarkOffset; set => endMarkOffset = value; }
        public float EndMarkBobAmplitude { get => endMarkBobAmplitude; set => endMarkBobAmplitude = value; }
        public float EndMarkBobSpeed { get => endMarkBobSpeed; set => endMarkBobSpeed = value; }
        public Image MonoDot { get => monoDot; set => monoDot = value; }
        public Sprite[] MonoDotFrames { get => monoDotFrames; set => monoDotFrames = value; }
        public float MonoDotInterval { get => monoDotInterval; set => monoDotInterval = value; }

        IDisposable _sub, _autoSub, _cgSub, _visSub, _textSpeedSub, _autoSpeedSub, _finishSub, _resetSub, _eyeShroudSub;
        Canvas _canvas; // 대사창 루트 Canvas(눈감김 차폐 동안만 overrideSorting으로 정렬 상승).
        Coroutine _typeRoutine;
        CompletionHandle _active;
        bool _typing;
        bool _skipTyping;
        bool _awaitingClick;
        bool _auto;
        bool _cgHidden;
        bool _hiddenByUser; // 인포 바 "숨기기" — CSV SetDialogueVisibleCommand(연출 채널)와 분리된 로컬 상태.
        bool _endMarkShown;
        bool _fastForward; // 시프트 홀드 빠른 진행 중 — 진행음/디버그 로그 억제용.
        float _endMarkBaseY;
        RectTransform _slideRt;     // 슬라이드 대상(slidePanel 또는 root)
        float _panelHomeY;          // 슬라이드 홈 y(첫 숨김 전 캡처)
        bool _slideHomeCaptured;
        Coroutine _slideRoutine;
        IReadOnlyList<InlinePause> _pauses;
        IReadOnlyList<InlineEmote> _emotes;
        IReadOnlyList<InlineSfx> _sfx;
        Coroutine _monoRoutine; // 독백 점 루프(타이핑 동안만).
        bool _isMono;           // 현재 라인이 독백(화자 빈 칸)인가.
        string _speaker;
        string _speakerId; // 별칭 해석된 캐릭터 코드 ID(없으면 null → _speaker 폴백)

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowDialogueCommand>(OnShow);
            _autoSub = EventBus.Subscribe<SetAutoModeCommand>(e => _auto = e.On);
            _cgSub = EventBus.Subscribe<SetCgModeCommand>(OnCgMode);
            _visSub = EventBus.Subscribe<SetDialogueVisibleCommand>(e =>
            {
                // 스크립트(연출)가 명시 표시하면 사용자 숨김 상태·슬라이드·보이기 버튼을 모두 원복(이중 상태 잔존 방지).
                if (e.Visible && _hiddenByUser)
                {
                    _hiddenByUser = false;
                    if (showButton != null) showButton.SetActive(false);
                    StartSlide(toHome: true);
                }
                if (root != null) root.SetActive(e.Visible);
                if (!e.Visible) ClearContent(); // 연출 숨김 → 비워서 다시 떴을 때 잔상 없게.
            });
            _textSpeedSub = EventBus.Subscribe<SetTextSpeedCommand>(e => ApplyTextSpeed(e.Value01));
            _autoSpeedSub = EventBus.Subscribe<SetAutoSpeedCommand>(e => ApplyAutoSpeed(e.Value01));
            // 내러티브 종료/도구 화면정리 → 대사창 비우고 숨김(VideoView 등 다른 뷰와 동일 규약). 다음 스크립트가
            // 깨끗한 상태에서 시작하도록 — 새 게임/재진입 시 직전 대사·에디터 플레이스홀더 잔상 방지.
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetView());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetView());
            // 눈감김(아이마스크) 차폐 동안에만 대사창을 눈꺼풀 위로 올린다(평상시엔 overrideSorting=off → 모달/팝업 아래 유지).
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            _eyeShroudSub = EventBus.Subscribe<EyeMaskShroudChanged>(OnEyeMaskShroud);
            ApplyFromSettings(); // 영속 속도 채택(SettingsController 부팅 재발행 전이라도 직접 반영)
            HideEndMark(); // 씬에 authored-active로 둔 아이콘을 플레이 시작 시 숨김(첫 대사 완료 전까지 비표시).
            StopMonoDots(hide: true); // 점 애니메이션도 부팅 시 숨김(독백 라인 진입 전까지 비표시).
            EnsureSlideRef(); // 슬라이드 홈 y 캡처(패널이 홈 위치일 때 = 부팅 직후).
            if (showButton != null) showButton.SetActive(false); // 보이기 버튼은 숨김 상태에서만 노출.
            // 첫 ShowDialogueCommand 전까지 대사창 박스를 비우고 숨김 — 씬에 authored-active로 둔 박스가
            // (에디터 플레이스홀더 텍스트와 함께) 비디오 재생 전 노출되는 문제 차단. OnShow가 다시 켠다.
            ClearContent();
            HideBox();
        }

        // 대사창 박스(root)를 숨김. root가 이 컴포넌트 자신의 GO면(자기참조 배선) 끄지 않는다 —
        // 자신을 비활성화하면 구독이 끊겨 다음 ShowDialogueCommand를 못 받기 때문. 프로덕션 프리팹의 root는
        // 자식 박스라 정상적으로 숨겨진다. 사용자 숨김(슬라이드) 중에는 그 상태를 존중해 건드리지 않는다.
        void HideBox()
        {
            if (root != null && root != gameObject && !_hiddenByUser) root.SetActive(false);
        }

        /// <summary>대사창 내용 비우기 — 화자/본문 텍스트·진행 아이콘·독백 점을 초기 상태로. 박스 active는 건드리지 않음.
        /// 타이핑 코루틴을 끊는 경우 보류 완료 핸들을 풀어준다(엔진 hang 방지, Complete는 멱등).</summary>
        void ClearContent()
        {
            if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
            _typing = false;
            _awaitingClick = false;
            _skipTyping = false;
            _active?.Complete();
            _active = null;
            if (speakerText != null) speakerText.text = "";
            if (bodyText != null) { bodyText.text = ""; bodyText.maxVisibleCharacters = 0; }
            HideEndMark();
            StopMonoDots(hide: true);
        }

        /// <summary>내러티브 종료/화면정리 — 내용을 비우고(보류 핸들 해제 포함) 박스를 숨긴다.</summary>
        void ResetView()
        {
            ClearContent();
            HideBox();
        }

        // 슬라이드 대상/홈 위치 확보(지연 캡처 — 패널이 홈에 있을 때 1회).
        void EnsureSlideRef()
        {
            if (_slideRt == null)
                _slideRt = slidePanel != null ? slidePanel : (root != null ? root.transform as RectTransform : null);
            if (_slideRt != null && !_slideHomeCaptured)
            {
                _panelHomeY = _slideRt.anchoredPosition.y;
                _slideHomeCaptured = true;
            }
        }

        void OnDisable()
        {
            _sub?.Dispose();
            _autoSub?.Dispose();
            _cgSub?.Dispose();
            _visSub?.Dispose();
            _textSpeedSub?.Dispose();
            _autoSpeedSub?.Dispose();
            _finishSub?.Dispose();
            _resetSub?.Dispose();
            _eyeShroudSub?.Dispose();
            _sub = _autoSub = _cgSub = _visSub = _textSpeedSub = _autoSpeedSub = _finishSub = _resetSub = _eyeShroudSub = null;
            // 차폐 정렬 상승이 남지 않도록 원복(다음 진입/도구화면에서 기본 정렬로 시작).
            if (_canvas != null) _canvas.overrideSorting = false;
        }

        // ── 설정 속도(정규화 0=느림~1=빠름 → 초) ──
        void ApplyFromSettings()
        {
            var s = settings != null ? settings : SettingsSO.Shared;
            if (s == null) return;
            ApplyTextSpeed(s.TextSpeed);
            ApplyAutoSpeed(s.AutoSpeed);
        }

        void ApplyTextSpeed(float t01) => charInterval = MapSpeed(t01, slowCharInterval, fastCharInterval);
        void ApplyAutoSpeed(float t01) => autoAdvanceDelay = MapSpeed(t01, slowAutoDelay, fastAutoDelay);

        /// <summary>정규화 0(느림)~1(빠름) → 초(느림 경계~빠름 경계). 빠를수록 작은 초. EditMode 테스트 대상.</summary>
        public static float MapSpeed(float t01, float slowSeconds, float fastSeconds) => Mathf.Lerp(slowSeconds, fastSeconds, Mathf.Clamp01(t01));

        // CG 컷신 진입 시 대사창을 숨기고 종료 시 복원(ADR-007: CG 뷰가 직접 참조하지 않고 명령으로). 대칭 토글.
        void OnCgMode(SetCgModeCommand e)
        {
            if (root == null) return;
            if (e.Active)
            {
                if (root.activeSelf) { root.SetActive(false); _cgHidden = true; }
                ClearContent(); // 다시 떴을 때(다음 대사 전) 직전 대사 잔상이 비치지 않도록 비움.
            }
            else if (_cgHidden)
            {
                root.SetActive(true);
                _cgHidden = false;
            }
        }

        // 눈감김(아이마스크) 차폐 동안만 대사창 Canvas를 눈꺼풀 위로 올린다. 차폐 해제 시 overrideSorting을 꺼서
        // 평상시 정렬(모달/팝업 아래)로 복원 — 정적 상승이 아니라 차폐 구간에만 한정(차폐 중엔 팝업이 안 뜨므로 충돌 없음).
        void OnEyeMaskShroud(EyeMaskShroudChanged e)
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_canvas == null) return;
            if (e.Active) _canvas.sortingOrder = eyeMaskShroudSortingOrder;
            _canvas.overrideSorting = e.Active;
        }

        /// <summary>사용자 숨김 상태(인포 바 "숨기기"). CSV 연출 숨김(<see cref="SetDialogueVisibleCommand"/>)과 별개.</summary>
        public bool IsHiddenByUser => _hiddenByUser;

        /// <summary>인포 바 "숨기기": 대사창을 아래로 슬라이드해 사라지게 하고 기존 위치에 '보이기' 버튼을 띄운다.
        /// 보이기 버튼 클릭(또는 좌클릭/스페이스)에서 복원하며 그 복원 입력은 진행으로 소비하지 않는다.
        /// 오토 진행도 복원까지 일시정지(숨긴 채 스토리가 흘러가지 않게).</summary>
        public void HideByUser()
        {
            if (_hiddenByUser || root == null || !root.activeSelf) return; // CSV/CG로 이미 꺼진 상태에선 무동작
            EnsureSlideRef();
            _hiddenByUser = true;
            HideEndMark();
            if (showButton != null) showButton.SetActive(true); // 대사창 위치에 보이기 버튼 노출
            StartSlide(toHome: false); // 아래로 슬라이드(사라짐) — root는 active 유지(복원 시 다시 올림)
            DebugInput.Log("숨기기 → 대사창 슬라이드 다운(보이기 버튼/클릭/스페이스로 복원)");
        }

        /// <summary>'보이기' 버튼/좌클릭/스페이스 복원 진입점(인스펙터 onClick 배선 가능 — public).</summary>
        public void RestoreByUser()
        {
            if (!_hiddenByUser) return;
            _hiddenByUser = false;
            if (showButton != null) showButton.SetActive(false);
            if (root != null && !_cgHidden && !root.activeSelf) root.SetActive(true);
            StartSlide(toHome: true); // 위로 슬라이드(원위치 복귀)
        }

        // 슬라이드 시작 — toHome=false면 패널 높이만큼 아래로, true면 홈 y 복귀. duration 0/비활성 시 즉시 스냅.
        void StartSlide(bool toHome)
        {
            EnsureSlideRef();
            if (_slideRt == null) return;
            float dist = slideDistance > 0f ? slideDistance : _slideRt.rect.height + 60f;
            float targetY = toHome ? _panelHomeY : _panelHomeY - dist;

            if (_slideRoutine != null) { StopCoroutine(_slideRoutine); _slideRoutine = null; }
            if (slideDuration <= 0f || !isActiveAndEnabled)
            {
                var p = _slideRt.anchoredPosition; p.y = targetY; _slideRt.anchoredPosition = p;
                return;
            }
            _slideRoutine = StartCoroutine(SlideTo(targetY));
        }

        IEnumerator SlideTo(float targetY)
        {
            float fromY = _slideRt.anchoredPosition.y;
            float dur = Mathf.Max(0.0001f, slideDuration);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime) // 메뉴 timeScale=0에도 동작
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                var p = _slideRt.anchoredPosition; p.y = Mathf.Lerp(fromY, targetY, k); _slideRt.anchoredPosition = p;
                yield return null;
            }
            var fp = _slideRt.anchoredPosition; fp.y = targetY; _slideRt.anchoredPosition = fp;
            _slideRoutine = null;
        }

        void OnShow(ShowDialogueCommand e)
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            HideEndMark(); // 새 줄 타이핑 시작 — 직전 진행 아이콘 숨김.
            _active = e.Handle;
            if (root != null && !_hiddenByUser) root.SetActive(true); // 사용자 숨김 중엔 강제 표시 금지(복원 입력까지 유지)
            _speaker = e.Speaker;
            _speakerId = e.SpeakerId;
            // 독백(화자 빈 칸) = NarrativeController가 IsNarration으로 보낸 빈 Speaker. 화자명을 가리고 점 애니메이션으로 대체.
            _isMono = string.IsNullOrEmpty(e.Speaker);
            if (speakerText != null) speakerText.text = _isMono ? "" : e.Speaker;
            if (!_isMono) StopMonoDots(hide: true); // 일반 대사: 점 숨김(독백 시작은 타이핑 시점에).
            _pauses = e.Pauses; // 인라인 <wait> 멈춤 지점(없으면 null).
            _emotes = e.Emotes; // 인라인 <emote> 표정 지점(없으면 null).
            _sfx = e.Sfx;       // 인라인 <sfx> 효과음 지점(없으면 null).
            _typeRoutine = StartCoroutine(TypeRoutine(e.Text ?? "", e.RequireClick));
        }

        IEnumerator TypeRoutine(string text, bool requireClick)
        {
            _typing = true;
            _skipTyping = false;
            _awaitingClick = false;
            if (_isMono) StartMonoDots(); // 독백: 이름 위치 점 루프 시작(타이핑 동안만).

            // 타이핑/멈춤 인덱스는 문자열 길이 기준 — 파서(InlineTagParser)의 CharIndex와 정합하고 TMP 메시 의존 제거.
            int total = text.Length;
            if (bodyText != null)
            {
                bodyText.text = text;
                bodyText.maxVisibleCharacters = 0;
            }

            // 타이핑 사운드 1회 캡처(per-char 프로퍼티 접근 방지). snd 부재/빈값이면 typeSfx=null → 무음.
            var snd = UiSoundSO.Shared;
            string typeSfx = snd != null ? snd.DialogueType : null;
            int typeStride = snd != null ? snd.TypeStride : 1;

            if (charInterval > 0f && total > 0)
            {
                for (int i = 0; i <= total; i++)
                {
                    if (_skipTyping) break;
                    if (bodyText != null) bodyText.maxVisibleCharacters = i;

                    // 타이핑 블립(스로틀: stride 글자마다 1회 + 공백 제외). per-char 기관총/공백음 방지.
                    if (!string.IsNullOrEmpty(typeSfx) && ShouldTypeBlip(text, i, typeStride))
                        EventBus.Publish(new PlaySfxCommand(typeSfx));

                    // 인라인 <wait>: i번째 글자 표시 직후 멈춤. 클릭(_skipTyping) 시 즉시 중단되도록 직접 타이머로
                    // 센다 — WaitForSeconds는 진행 중 끊기지 않아 클릭해도 wait 끝까지 멈춰 "화면이 멎은" 인상을 줬다.
                    if (_pauses != null)
                    {
                        for (int p = 0; p < _pauses.Count && !_skipTyping; p++)
                            if (_pauses[p].CharIndex == i)
                            {
                                float w = 0f;
                                while (w < _pauses[p].Seconds && !_skipTyping) { w += Time.deltaTime; yield return null; }
                            }
                    }

                    // 인라인 <emote>: i번째 글자 시점에 화자 표정 변경 명령 발행(StageView가 슬롯 해석·교체).
                    FireEmotesAt(i);
                    // 인라인 <sfx>: i번째 글자 시점에 효과음 1회 발행.
                    FireSfxAt(i);

                    yield return new WaitForSeconds(charInterval);
                }
            }

            if (bodyText != null) bodyText.maxVisibleCharacters = int.MaxValue;
            // 즉시표시(루프 미실행)거나 스킵으로 끊긴 경우 — 못 발행한 표정을 최종 상태로 마저 발행.
            if (_emotes != null && (_skipTyping || charInterval <= 0f || total == 0))
                FireAllEmotes();
            // 효과음: 즉시표시(루프 미실행)일 때만 마저 발행. 스킵은 중간 효과음 생략(연타 방지).
            if (_sfx != null && (charInterval <= 0f || total == 0))
                FireAllSfx();
            _typing = false;
            StopMonoDots(hide: false); // 타이핑 완료 → 점 루프 정지(이름 위치엔 마지막 프레임으로 멈춰 유지).

            if (requireClick)
            {
                _awaitingClick = true;
                ShowEndMarkAtTextEnd(); // 타이핑 완료 + 클릭 대기 → 본문 끝에 진행 아이콘 표시.
                if (_auto)
                {
                    // 오토: 지연만큼 대기하되 클릭(_awaitingClick=false)이 오면 즉시 진행.
                    float t = 0f;
                    while (_awaitingClick && t < autoAdvanceDelay)
                    {
                        // 오버레이 열림/사용자 숨김 중 오토 일시정지(숨긴 채 스토리가 흘러가지 않게)
                        if (!OverlayGate.IsBlocked && !_hiddenByUser) t += Time.deltaTime;
                        yield return null;
                    }
                    _awaitingClick = false;
                }
                else
                {
                    yield return new WaitUntil(() => !_awaitingClick);
                }
            }

            HideEndMark(); // 진행/오토 완료 — 아이콘 숨김.
            _typeRoutine = null;
            _active?.Complete();
            _active = null;
        }

        // 인라인 <emote> 발행: charIndex 시점의 표정 변경 명령(화자→슬롯 해석은 StageView 구독자 몫).
        // 지정형 <emote=대상:표정>이면 그 줄 화자가 아닌 대상(Target)에게 적용 — 내레이션 줄에서도 동작.
        void FireEmotesAt(int charIndex)
        {
            if (_emotes == null) return;
            for (int m = 0; m < _emotes.Count; m++)
                if (_emotes[m].CharIndex == charIndex)
                    EventBus.Publish(new ShowSpeakerEmoteCommand(EmoteTargetOf(_emotes[m]), _emotes[m].Emote));
        }

        void FireAllEmotes()
        {
            if (_emotes == null) return;
            for (int m = 0; m < _emotes.Count; m++)
                EventBus.Publish(new ShowSpeakerEmoteCommand(EmoteTargetOf(_emotes[m]), _emotes[m].Emote));
        }

        // 인라인 <sfx> 발행: charIndex 시점의 효과음 1회(이름은 엔진이 별칭 해석해 채움).
        void FireSfxAt(int charIndex)
        {
            if (_sfx == null) return;
            for (int m = 0; m < _sfx.Count; m++)
                if (_sfx[m].CharIndex == charIndex)
                    EventBus.Publish(new PlaySfxCommand(_sfx[m].Name));
        }

        void FireAllSfx()
        {
            if (_sfx == null) return;
            for (int m = 0; m < _sfx.Count; m++)
                EventBus.Publish(new PlaySfxCommand(_sfx[m].Name));
        }

        // 표정 적용 대상: 지정형(Target)이 있으면 그 대상, 없으면 그 줄 화자(EmoteSpeaker).
        string EmoteTargetOf(InlineEmote e) => string.IsNullOrEmpty(e.Target) ? EmoteSpeaker : e.Target;

        // 슬롯 매칭은 해석된 코드 ID 우선(StageView가 Char 명령의 코드 ID를 추적) — 미해석 시 원문 화자명 폴백.
        string EmoteSpeaker => string.IsNullOrEmpty(_speakerId) ? _speaker : _speakerId;

        // 독백 점 루프 시작 — MonoDot을 켜고 프레임(mono_dot_0~4)을 간격마다 순환. 미바인딩/프레임 없으면 무동작.
        void StartMonoDots()
        {
            if (monoDot == null || monoDotFrames == null || monoDotFrames.Length == 0) return;
            if (!monoDot.gameObject.activeSelf) monoDot.gameObject.SetActive(true);
            if (_monoRoutine != null) StopCoroutine(_monoRoutine);
            _monoRoutine = StartCoroutine(MonoDotLoop());
        }

        IEnumerator MonoDotLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.01f, monoDotInterval));
            int i = 0;
            while (true)
            {
                monoDot.sprite = monoDotFrames[i % monoDotFrames.Length];
                i++;
                yield return wait;
            }
        }

        // 점 루프 정지. hide=true면 MonoDot 자체를 숨김(일반 대사·부팅), false면 마지막 프레임으로 멈춰 유지(타이핑 완료).
        void StopMonoDots(bool hide)
        {
            if (_monoRoutine != null) { StopCoroutine(_monoRoutine); _monoRoutine = null; }
            if (hide && monoDot != null && monoDot.gameObject.activeSelf) monoDot.gameObject.SetActive(false);
        }

        // 스페이스로도 진행(좌클릭과 동일). 신 Input System 직접 읽기 — 뷰가 활성(내러티브 중)일 때만 Update 동작.
        // 단 입력 필드(LockScreen 비번 등) 타이핑 중엔 무시 — 스페이스가 텍스트 입력 대신 대사 진행을 가로채지 않도록.
        void Update()
        {
            UpdateEndMarkBob();

            var mouse = Mouse.current;

            // 사용자 숨김 중: 좌클릭/스페이스 = 복원만(Advance가 진행 대신 복원으로 소비). 좌클릭은 뷰 GO의
            // 전체화면 투명 클릭캐처(OnPointerClick)가 root 비활성과 무관하게 받으므로 여기서 폴링하지 않는다
            // — press 폴링+release 클릭의 이중 Advance(복원 클릭이 진행까지 소비) 방지. 휠 로그 등 다른 입력은 숨김 동안 무시.
            if (_hiddenByUser)
            {
                var k = Keyboard.current;
                if (k != null && k.spaceKey.wasPressedThisFrame && !IsTextInputFocused()) Advance("스페이스");
                return;
            }

            // 휠 업 = 대사 로그 열기(VN 관례, 감독 승인 진입점). 오버레이 열림 중엔 무시(게이트 — 로그 자신 포함).
            if (mouse != null && mouse.scroll.ReadValue().y > 0.01f && !OverlayGate.IsBlocked)
            {
                DebugInput.Log("휠 업 → 대사 로그 열기");
                EventBus.Publish(new OpenDialogueLogCommand());
            }

            var kb = Keyboard.current;

            // 시프트 홀드 = 빠른 진행(테스트 편의). 누르고 있는 동안 매 프레임 Advance를 호출해
            // 타이핑은 즉시 완성·완료 줄은 자동으로 다음으로 넘긴다(오토 대기 포함). 진행음/디버그 로그는 억제.
            bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (shiftHeld && !IsTextInputFocused() && !OverlayGate.IsBlocked)
            {
                _fastForward = true;
                Advance("시프트");
                return;
            }
            _fastForward = false;

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
            if (OverlayGate.IsBlocked) return; // 오버레이(설정/세이브로드) 열림 중 진행/스킵 차단 — 키보드 직접 읽기 보호
            string s = source ?? "?";
            if (_hiddenByUser) { RestoreByUser(); DebugInput.Log($"{s} → 대사창 복원(숨김 해제)"); return; } // 복원 입력은 진행 미소비
            if (_typing) { _skipTyping = true; if (!_fastForward) DebugInput.Log($"{s} → 대사 타이핑 스킵"); } // 스킵(완성 가속)은 무음
            else if (_awaitingClick)
            {
                _awaitingClick = false;
                // 다음 줄로 넘어갈 때만 진행음(요구사항: 타이핑 완성/스킵 시에는 재생 안 함). 빠른 진행 중엔 진행음 스팸 방지.
                if (!_fastForward)
                {
                    var snd = UiSoundSO.Shared;
                    if (snd != null && !string.IsNullOrEmpty(snd.DialogueAdvance))
                        EventBus.Publish(new PlaySfxCommand(snd.DialogueAdvance));
                    DebugInput.Log($"{s} → 대사 진행(다음)");
                }
            }
            else if (!_fastForward) DebugInput.Log($"{s} → 입력됐으나 진행할 대사 없음");
        }

        public void OnPointerClick(PointerEventData eventData) => Advance("좌클릭");

        /// <summary>i번째 글자 표시 시점에 타이핑 블립을 낼지(순수): stride 글자마다 1회 + 공백 제외 + 경계 가드.
        /// (호출 측이 typeSfx 비어있는지는 먼저 가드한다.) EditMode 테스트 대상.</summary>
        public static bool ShouldTypeBlip(string text, int i, int stride)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (i <= 0 || i > text.Length) return false;
            if (stride < 1) stride = 1;
            if (i % stride != 0) return false;
            return !char.IsWhiteSpace(text[i - 1]);
        }

        // 본문 마지막 가시 글자 뒤(그 줄의 세로 중앙)에 진행 아이콘을 배치·활성화한다. bodyText와 endMark는 같은 부모
        // (대사창 Box)의 형제지만, 글자 좌표는 TMP 로컬이라 월드 경유로 변환해 앵커/피벗에 무관하게 정확히 배치한다
        // (아이콘 피벗=중앙 가정, endMarkOffset으로 간격/세로 보정).
        void ShowEndMarkAtTextEnd()
        {
            if (endMark == null || bodyText == null) return;
            bodyText.ForceMeshUpdate();
            TMP_TextInfo ti = bodyText.textInfo;
            int last = -1;
            for (int i = ti.characterCount - 1; i >= 0; i--)
                if (ti.characterInfo[i].isVisible) { last = i; break; }
            if (last < 0) { HideEndMark(); return; } // 가시 글자 없음(빈 내레이션 등) → 표시 안 함.

            TMP_CharacterInfo ci = ti.characterInfo[last];
            TMP_LineInfo line = ti.lineInfo[ci.lineNumber];
            Vector3 local = new Vector3(ci.topRight.x, (line.ascender + line.descender) * 0.5f, 0f);

            if (!endMark.gameObject.activeSelf) endMark.gameObject.SetActive(true);
            endMark.position = bodyText.rectTransform.TransformPoint(local); // 아이콘 중심을 글자 끝·줄 중앙으로.
            endMark.localPosition += (Vector3)endMarkOffset;                  // 간격/세로 보정(부모 스케일 1·무회전 가정).
            _endMarkBaseY = endMark.localPosition.y;
            _endMarkShown = true;
        }

        void HideEndMark()
        {
            _endMarkShown = false;
            if (endMark != null && endMark.gameObject.activeSelf) endMark.gameObject.SetActive(false);
        }

        // 표시 중일 때만 위아래 사인 바운스(정지 스프라이트라 트랜스폼으로 생동감). 메뉴 등 timeScale=0에도 돌도록 unscaled.
        void UpdateEndMarkBob()
        {
            if (!_endMarkShown || endMark == null || endMarkBobAmplitude == 0f) return;
            Vector3 lp = endMark.localPosition;
            lp.y = _endMarkBaseY + Mathf.Sin(Time.unscaledTime * endMarkBobSpeed) * endMarkBobAmplitude;
            endMark.localPosition = lp;
        }
    }
}
