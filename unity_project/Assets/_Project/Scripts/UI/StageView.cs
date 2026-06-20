using System;
using System.Collections;
using System.Collections.Generic; // IReadOnlyList<string>
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // ShowBackgroundCommand, ShowCharacterCommand, ShowSpeakerEmoteCommand, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 스테이지 표시 뷰(*View, M3 슬라이스2: BG + Char). <see cref="ShowBackgroundCommand"/>·
    /// <see cref="ShowCharacterCommand"/>를 구독해 배경 전환과 캐릭터 슬롯(L/C/R) 페이드를 코루틴 lerp로 수행하고,
    /// 완료 시 핸들(<see cref="CompletionHandle"/>)을 풀어준다(ADR-007: UI는 표시만, 상태 변경 없음). 엔진
    /// (NarrativeController)은 이 뷰를 직접 알지 못한다 — 명령 이벤트 + 완료 핸들로만 연결(DialogueView와 동형).
    /// 슬라이스1처럼 DOTween/UniTask 미사용. 별도 _Stage 캔버스(대사 UI보다 낮은 sortingOrder)에 부착.
    ///
    /// 리소스 로딩 = 컨벤션(별칭/카탈로그는 후속 슬라이스): BG→<c>Resources.Load("BG/{name}")</c>,
    /// Char→<c>Resources.Load("Characters/{char}_{emote}")</c>(표정 생략 시 <c>Characters/{char}</c>).
    /// 슬라이스 밖: Overlay/CG/SD/CharFX/슬라이드/등장SFX(스킵).
    /// </summary>
    public class StageView : MonoBehaviour
    {
        [Serializable]
        public class SlotBinding
        {
            public Image image;
            public CanvasGroup group;
        }

        [Header("배경 (front/back 크로스페이드)")]
        [Tooltip("BG 표시 중 뒤에 깔리는 검은 backdrop(Background 컨테이너 Image). Fade가 검정을 경유하도록. BG 없을 땐 꺼서 시뮬 화면이 까매지지 않게 함.")]
        [SerializeField] Image backdrop;
        [SerializeField] Image bgFront;
        [SerializeField] CanvasGroup bgFrontGroup;
        [SerializeField] Image bgBack;
        [SerializeField] CanvasGroup bgBackGroup;

        [Header("캐릭터 슬롯")]
        [SerializeField] SlotBinding slotL = new();
        [SerializeField] SlotBinding slotC = new();
        [SerializeField] SlotBinding slotR = new();
        [Tooltip("캐릭터 슬롯 컨테이너(선택). CG 컷신 진입 시 일괄 숨기고 종료 시 복원 — 슬롯별 알파는 보존.")]
        [SerializeField] GameObject charContainer;

        [Tooltip("스테이지 리소스 루트(Resources 하위). BG는 {bgRoot}/{name}, 캐릭터는 {charRoot}/{char}_{emote}로 로드.")]
        [SerializeField] string bgRoot = "BG";
        [SerializeField] string charRoot = "Characters";

        [Header("히로인별 스테이지 배치(크기·오프셋)")]
        [Tooltip("캐릭터 ID별 스케일 배율 + x/y 오프셋 카탈로그. 미바인딩 시 stageCatalogPath의 Resources에서 로드. 미발견·미등록은 슬롯 기본 그대로(항등).")]
        [SerializeField] CharacterStageCatalogSO stageCatalog;
        [Tooltip("stageCatalog 미바인딩 시 Resources에서 찾을 경로(Resources 하위). 비우면 자동 로드 생략.")]
        [SerializeField] string stageCatalogPath = "Data/CharacterStageCatalog";

        [Header("캐릭터 등장 글리치(CharFX) — 가상공간 순간이동 등장")]
        [Tooltip("캐릭터 외곽 글리치 머티리얼(LoveAlgo/UICharacterGlitch). 미바인딩 시 일반 페이드 등장.")]
        [SerializeField] Material characterGlitchMaterial;
        [Tooltip("이 캐릭터 ID로 Enter할 때만 외곽 글리치로 등장(그 외는 일반 페이드). 로아 전용 시그니처.")]
        [SerializeField] string glitchEnterCharId = "roa";
        [Tooltip("외곽 글리치 등장 연출 시간(초). Enter duration과 무관하게 이 값으로 구동.")]
        [SerializeField] float characterGlitchSeconds = 0.6f;

        [Header("표정 전환 크로스페이드(깜빡임 제거)")]
        [Tooltip("Emote/<emote> 표정 교체 시 이전 표정을 위에 겹쳐 페이드아웃하는 디졸브 시간(초). 0이면 즉시 하드 스왑(구 동작).")]
        [SerializeField] float emoteCrossfadeSeconds = 0.12f;

        [Tooltip("요청한 표정 스프라이트가 없을 때 대신 로드할 기본 표정 코드. 비우면 폴백 없음(구 동작). 폴백 적용 시 개발 토스트로 알림.")]
        [SerializeField] string fallbackEmote = "기본";

        public Image Backdrop { get => backdrop; set => backdrop = value; }
        public Image BgFront { get => bgFront; set => bgFront = value; }
        public CanvasGroup BgFrontGroup { get => bgFrontGroup; set => bgFrontGroup = value; }
        public Image BgBack { get => bgBack; set => bgBack = value; }
        public CanvasGroup BgBackGroup { get => bgBackGroup; set => bgBackGroup = value; }
        public SlotBinding SlotL { get => slotL; set => slotL = value; }
        public SlotBinding SlotC { get => slotC; set => slotC = value; }
        public SlotBinding SlotR { get => slotR; set => slotR = value; }
        public GameObject CharContainer { get => charContainer; set => charContainer = value; }
        public CharacterStageCatalogSO StageCatalog { get => stageCatalog; set => stageCatalog = value; }
        public Material CharacterGlitchMaterial { get => characterGlitchMaterial; set => characterGlitchMaterial = value; }

        static readonly int CharGlitchAmountId = Shader.PropertyToID("_GlitchAmount");

        IDisposable _bgSub, _charSub, _finishSub, _cgSub, _emoteSub, _resetSub;
        bool _cgHidden;

        // 슬롯(L/C/R) 인덱스 → 현재 올라간 캐릭터 식별자(Char Enter 시 기록). 인라인 <emote> 화자→슬롯 해석에 사용.
        readonly string[] _slotChar = new string[3];

        Coroutine _bgRoutine;
        CompletionHandle _bgPending;

        readonly Coroutine[] _slotRoutines = new Coroutine[3];
        readonly CompletionHandle[] _slotPending = new CompletionHandle[3];

        // 슬롯의 authored 기본 스케일/위치(배치 합성의 baseline). 슬롯별 첫 배치 직전 1회 캡처.
        readonly Vector3[] _baseScale = new Vector3[3];
        readonly Vector2[] _basePos = new Vector2[3];
        readonly bool[] _baseCaptured = new bool[3];

        // CharFX 글리치: 슬롯별 글리치 머티리얼 런타임 인스턴스(공유 에셋 비변형) + 글리치 전 원본 머티리얼(복원용).
        readonly Material[] _charGlitchInst = new Material[3];
        readonly Material[] _charBaseMat = new Material[3];

        // 표정 크로스페이드: 슬롯별 이전 표정 오버레이 이미지(슬롯 이미지 자식, 지연 생성·재사용) + 진행 중 페이드 코루틴.
        readonly Image[] _emoteFadeImg = new Image[3];
        readonly Coroutine[] _emoteFadeRoutine = new Coroutine[3];

        void OnEnable()
        {
            _bgSub = EventBus.Subscribe<ShowBackgroundCommand>(OnShowBackground);
            _charSub = EventBus.Subscribe<ShowCharacterCommand>(OnShowCharacter);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ClearAll());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ClearAll()); // 도구 화면 정리
            _cgSub = EventBus.Subscribe<SetCgModeCommand>(OnCgMode);
            _emoteSub = EventBus.Subscribe<ShowSpeakerEmoteCommand>(OnSpeakerEmote);

            // 초기 상태: front 보임(빈 스프라이트)·back 숨김·backdrop 꺼짐(BG 없을 땐 시뮬 화면이 까매지지 않게).
            SetAlpha(bgFrontGroup, 1f);
            SetAlpha(bgBackGroup, 0f);
            if (bgBack != null) bgBack.enabled = false;
            if (backdrop != null) backdrop.enabled = false;

            // 히로인 배치 카탈로그: 미바인딩 시 Resources에서 로드(미발견 시 null → 항등 적용). 슬롯 기본은 슬롯별 첫 적용 직전 캡처.
            if (stageCatalog == null && !string.IsNullOrEmpty(stageCatalogPath))
                stageCatalog = Resources.Load<CharacterStageCatalogSO>(stageCatalogPath);
        }

        void OnDisable()
        {
            _bgSub?.Dispose(); _charSub?.Dispose(); _finishSub?.Dispose(); _cgSub?.Dispose(); _emoteSub?.Dispose(); _resetSub?.Dispose();
            _bgSub = _charSub = _finishSub = _cgSub = _emoteSub = _resetSub = null;
            for (int i = 0; i < _charGlitchInst.Length; i++)
            {
                if (_charGlitchInst[i] != null) { Destroy(_charGlitchInst[i]); _charGlitchInst[i] = null; }
            }
            for (int i = 0; i < _emoteFadeImg.Length; i++)
            {
                if (_emoteFadeImg[i] != null) { Destroy(_emoteFadeImg[i].gameObject); _emoteFadeImg[i] = null; }
                _emoteFadeRoutine[i] = null;
            }
        }

        // CG 컷신 진입 시 캐릭터를 일괄 숨기고 종료 시 복원(슬롯별 알파 보존 위해 컨테이너 토글). 대칭.
        void OnCgMode(SetCgModeCommand e)
        {
            if (charContainer == null) return;
            if (e.Active)
            {
                if (charContainer.activeSelf) { charContainer.SetActive(false); _cgHidden = true; }
            }
            else if (_cgHidden)
            {
                charContainer.SetActive(true);
                _cgHidden = false;
            }
        }

        // ── 배경 ──

        void OnShowBackground(ShowBackgroundCommand e)
        {
            if (_bgRoutine != null)
            {
                StopCoroutine(_bgRoutine);
                _bgPending?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록 마무리.
            }
            _bgPending = e.Handle;
            _bgRoutine = StartCoroutine(BgRoutine(e));
        }

        IEnumerator BgRoutine(ShowBackgroundCommand e)
        {
            var sprite = LoadSprite($"{bgRoot}/{e.Name}");
            if (sprite == null)
            {
                Log.Warn($"[StageView] BG 스프라이트 없음: {e.Name}");
                FinishBg();
                yield break;
            }

            if (bgFront == null)
            {
                FinishBg();
                yield break;
            }

            // BG가 보이는 동안에만 검은 backdrop 노출(Fade 검정 경유용). 시뮬 복귀 시 ClearAll이 끈다.
            if (backdrop != null) backdrop.enabled = true;

            // 즉시 교체: Cut · 시간 0 · 페이드용 CanvasGroup 미바인딩.
            if (e.Transition == BgTransition.Cut || e.Duration <= 0f || bgFrontGroup == null)
            {
                bgFront.sprite = sprite;
                bgFront.enabled = true;
                SetAlpha(bgFrontGroup, 1f);
                FinishBg();
                yield break;
            }

            if (e.Transition == BgTransition.Cross && bgBack != null && bgBackGroup != null)
            {
                // 크로스페이드: back에 새 BG → back 페이드인 + front 페이드아웃 → 스왑.
                bgBack.sprite = sprite;
                bgBack.enabled = true;
                SetAlpha(bgBackGroup, 0f);
                yield return Fade2(bgBackGroup, 0f, 1f, bgFrontGroup, 1f, 0f, e.Duration);
                bgFront.sprite = sprite;
                bgFront.enabled = true;
                SetAlpha(bgFrontGroup, 1f);
                SetAlpha(bgBackGroup, 0f);
                bgBack.enabled = false;
            }
            else
            {
                // Fade(검정 경유): _Stage 맨 뒤의 검은 backdrop이 상시 깔려 있어, front를 0으로 내리면
                // 검정이 드러난다 → 교체 → 페이드인. 구의 전역 ScreenFX 의존을 스테이지 자체 backdrop으로 대체.
                float half = e.Duration * 0.5f;
                if (bgFront.enabled)
                    yield return FadeGroup(bgFrontGroup, bgFrontGroup.alpha, 0f, half);
                bgFront.sprite = sprite;
                bgFront.enabled = true;
                yield return FadeGroup(bgFrontGroup, 0f, 1f, half);
            }

            FinishBg();
        }

        void FinishBg()
        {
            var h = _bgPending;
            _bgPending = null;
            _bgRoutine = null;
            h?.Complete();
        }

        // ── 캐릭터 ──

        void OnShowCharacter(ShowCharacterCommand e)
        {
            int idx = (int)e.Slot;
            var slot = GetSlot(e.Slot);
            if (slot == null || slot.image == null)
            {
                Log.Warn($"[StageView] 슬롯 미바인딩: {e.Slot}");
                e.Handle?.Complete();
                return;
            }

            // 슬롯→캐릭터 추적(인라인 <emote> 화자 해석용): Enter=기록, Exit/Clear=해제. Emote=식별 불변.
            if (e.Action == CharAction.Enter) _slotChar[idx] = e.Character;
            else if (e.Action == CharAction.Exit || e.Action == CharAction.Clear) _slotChar[idx] = null;

            if (_slotRoutines[idx] != null)
            {
                StopCoroutine(_slotRoutines[idx]);
                _slotPending[idx]?.Complete();
            }
            _slotPending[idx] = e.Handle;
            _slotRoutines[idx] = StartCoroutine(CharRoutine(e, idx, slot));
        }

        IEnumerator CharRoutine(ShowCharacterCommand e, int idx, SlotBinding slot)
        {
            // 직전 글리치가 중단된 채 다른 액션이 오면 슬롯 머티리얼을 원본으로 복원(글리치 눌어붙음 방지).
            if (_charGlitchInst[idx] != null && slot.image != null && slot.image.material == _charGlitchInst[idx]
                && !(e.Action == CharAction.Enter && UseGlitchEnter(e.Character)))
            {
                slot.image.material = _charBaseMat[idx];
            }

            switch (e.Action)
            {
                case CharAction.Enter:
                {
                    var sprite = LoadCharSpriteOrFallback(e.Character, e.Emote);
                    if (sprite == null)
                    {
                        Log.Warn($"[StageView] 캐릭터 스프라이트 없음: {e.Character}/{e.Emote}");
                        DevToast.Error($"캐릭터 스프라이트 없음: {e.Character}/{e.Emote}");
                        break;
                    }
                    CancelEmoteFade(idx); // 새 등장 위에 이전 표정 오버레이가 남지 않도록.
                    slot.image.sprite = sprite;
                    slot.image.enabled = true;
                    ApplyPlacement(idx, slot, e.Character);
                    if (UseGlitchEnter(e.Character))
                        yield return GlitchEnter(idx, slot);   // 가상공간 순간이동 등장(외곽 글리치 broken→clean)
                    else
                        yield return FadeGroup(slot.group, 0f, 1f, e.Duration);
                    break;
                }
                case CharAction.Emote:
                {
                    if (!slot.image.enabled)
                    {
                        Log.Warn($"[StageView] Emote 대상 슬롯이 비어있음: {e.Slot}");
                        break;
                    }
                    var sprite = LoadCharSpriteOrFallback(e.Character, e.Emote);
                    if (sprite != null) CrossfadeEmote(idx, slot, sprite); // 깜빡임 없이 디졸브 교체.
                    else DevToast.Error($"표정 스프라이트 없음: {e.Character}/{e.Emote}");
                    // duration은 미사용(크로스페이드 시간은 emoteCrossfadeSeconds).
                    break;
                }
                case CharAction.Exit:
                {
                    CancelEmoteFade(idx);
                    yield return FadeGroup(slot.group, slot.group != null ? slot.group.alpha : 1f, 0f, e.Duration);
                    ClearSlotVisual(slot);
                    break;
                }
                case CharAction.Clear:
                {
                    CancelEmoteFade(idx);
                    ClearSlotVisual(slot);
                    SetAlpha(slot.group, 0f);
                    break;
                }
            }

            var h = _slotPending[idx];
            _slotPending[idx] = null;
            _slotRoutines[idx] = null;
            h?.Complete();
        }

        // ── 인라인 표정(<emote=표정/>) ──
        // DialogueView가 타이핑 중 발행한 ShowSpeakerEmoteCommand를 받아, 화자가 올라간 슬롯의 스프라이트를
        // 표정 버전으로 즉시 교체(완료 핸들 없음 — 타이핑과 병행되는 fire-and-forget). 화자→슬롯은 _slotChar 직접 매칭.

        void OnSpeakerEmote(ShowSpeakerEmoteCommand e)
        {
            if (string.IsNullOrEmpty(e.Emote)) return;
            int idx = ResolveSlotForSpeaker(_slotChar, e.Speaker);
            if (idx < 0)
            {
                Log.Info($"[StageView] <emote> 대상 캐릭터가 무대에 없음: 화자='{e.Speaker}'");
                return;
            }
            var slot = GetSlot((CharSlot)idx);
            if (slot?.image == null || !slot.image.enabled) return;
            var sprite = LoadCharSpriteOrFallback(_slotChar[idx], e.Emote);
            if (sprite != null) CrossfadeEmote(idx, slot, sprite); // 타이핑 중 인라인 표정도 디졸브.
            else
            {
                Log.Warn($"[StageView] <emote> 스프라이트 없음: {_slotChar[idx]}/{e.Emote}");
                DevToast.Error($"인라인 표정 없음: {_slotChar[idx]}/{e.Emote}");
            }
        }

        /// <summary>화자 문자열과 일치하는(직접·대소문자 무시) 캐릭터가 올라간 슬롯 인덱스. 없으면 -1(순수).</summary>
        public static int ResolveSlotForSpeaker(IReadOnlyList<string> slotChars, string speaker)
        {
            if (slotChars == null || string.IsNullOrEmpty(speaker)) return -1;
            string s = speaker.Trim();
            for (int i = 0; i < slotChars.Count; i++)
            {
                string c = slotChars[i];
                if (!string.IsNullOrEmpty(c) && string.Equals(c.Trim(), s, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        /// <summary>화자가 현재 올라간 슬롯 인덱스(추적 상태 기준, 테스트/조회용). 없으면 -1.</summary>
        public int SlotIndexForSpeaker(string speaker) => ResolveSlotForSpeaker(_slotChar, speaker);

        SlotBinding GetSlot(CharSlot slot)
        {
            switch (slot)
            {
                case CharSlot.L: return slotL;
                case CharSlot.R: return slotR;
                default: return slotC;
            }
        }

        void ClearSlotVisual(SlotBinding slot)
        {
            if (slot?.image == null) return;
            // 글리치 머티리얼이 걸린 채 비워지지 않도록 원본 머티리얼로 복원.
            int idx = slot == slotL ? 0 : slot == slotR ? 2 : slot == slotC ? 1 : -1;
            if (idx >= 0) CancelEmoteFade(idx); // 비워지는 슬롯에 표정 오버레이가 남지 않도록.
            if (idx >= 0 && _charGlitchInst[idx] != null && slot.image.material == _charGlitchInst[idx])
                slot.image.material = _charBaseMat[idx];
            slot.image.sprite = null;
            slot.image.enabled = false;
        }

        // ── 표정 크로스페이드(깜빡임 제거) ──
        // 표정 교체는 본래 슬롯 이미지 스프라이트를 즉시 바꿔 한 프레임 하드 컷(깜빡임)이 보였다. 대신 슬롯 이미지에
        // 새 표정을 즉시 깔고, 이전 표정을 똑같은 위치/스케일의 오버레이(슬롯 이미지 자식)로 위에 얹어 페이드아웃 →
        // 부드러운 디졸브. 오버레이는 슬롯별 1개를 지연 생성·재사용. emoteCrossfadeSeconds<=0이면 구 하드 스왑.

        void CrossfadeEmote(int idx, SlotBinding slot, Sprite newSprite)
        {
            var img = slot?.image;
            if (img == null || newSprite == null) return;
            var oldSprite = img.sprite;

            // 즉시 모드·변화 없음·이전 표정/슬롯 없음 → 하드 스왑(구 동작 보존).
            if (emoteCrossfadeSeconds <= 0f || !img.enabled || oldSprite == null || oldSprite == newSprite)
            {
                img.sprite = newSprite;
                return;
            }

            // 진행 중인 이전 디졸브는 즉시 마무리(겹침 방지).
            if (_emoteFadeRoutine[idx] != null) { StopCoroutine(_emoteFadeRoutine[idx]); _emoteFadeRoutine[idx] = null; }

            var ov = GetEmoteFadeOverlay(idx, img);
            var brt = img.rectTransform;
            var ort = ov.rectTransform;
            // 슬롯 이미지와 동일한 표시 영역(자식이므로 위치는 0, 스케일은 부모 상속=1).
            ort.anchorMin = brt.anchorMin; ort.anchorMax = brt.anchorMax; ort.pivot = brt.pivot;
            ort.sizeDelta = brt.sizeDelta; ort.anchoredPosition = Vector2.zero; ort.localScale = Vector3.one;
            ov.sprite = oldSprite;
            ov.type = img.type;
            ov.preserveAspect = img.preserveAspect;
            ov.color = Color.white; // 알파 1 → 이전 표정이 위를 덮은 상태에서 시작.
            ov.enabled = true;
            ov.transform.SetAsLastSibling(); // 슬롯 이미지(부모) 위에 렌더.

            img.sprite = newSprite; // 새 표정은 아래에 즉시 노출.
            _emoteFadeRoutine[idx] = StartCoroutine(EmoteFadeOut(idx, ov));
        }

        IEnumerator EmoteFadeOut(int idx, Image ov)
        {
            float dur = emoteCrossfadeSeconds;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                var c = ov.color; c.a = 1f - Mathf.Clamp01(t / dur); ov.color = c;
                yield return null;
            }
            ov.enabled = false;
            ov.sprite = null;
            _emoteFadeRoutine[idx] = null;
        }

        // 슬롯 이미지의 자식으로 오버레이 Image를 지연 생성·재사용(클릭 비차단).
        Image GetEmoteFadeOverlay(int idx, Image baseImg)
        {
            var ov = _emoteFadeImg[idx];
            if (ov == null)
            {
                var go = new GameObject("EmoteFade", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(baseImg.rectTransform, false);
                ov = go.GetComponent<Image>();
                ov.raycastTarget = false;
                ov.enabled = false;
                _emoteFadeImg[idx] = ov;
            }
            return ov;
        }

        // 진행 중 디졸브 중단 + 오버레이 숨김(슬롯이 비거나 새로 등장할 때).
        void CancelEmoteFade(int idx)
        {
            if (idx < 0 || idx >= _emoteFadeRoutine.Length) return;
            if (_emoteFadeRoutine[idx] != null) { StopCoroutine(_emoteFadeRoutine[idx]); _emoteFadeRoutine[idx] = null; }
            var ov = _emoteFadeImg[idx];
            if (ov != null) { ov.enabled = false; ov.sprite = null; }
        }

        // ── 히로인별 배치(크기·오프셋) ──

        // 슬롯의 authored 기본 스케일/위치를 슬롯별 첫 배치 직전 1회 캡처. 바인딩/활성 타이밍과 무관하게 변형 전 값을 baseline으로.
        void EnsureBaseCaptured(int idx, SlotBinding slot)
        {
            if (_baseCaptured[idx]) return;
            var rt = slot.image.rectTransform;
            _baseScale[idx] = rt.localScale;
            _basePos[idx] = rt.anchoredPosition;
            _charBaseMat[idx] = slot.image.material; // 글리치 전 원본 머티리얼(보통 기본 UI) — 글리치 종료 시 복원.
            _baseCaptured[idx] = true;
        }

        // ── 캐릭터 등장 글리치(CharFX) ──

        bool UseGlitchEnter(string character) =>
            characterGlitchMaterial != null && !string.IsNullOrEmpty(glitchEnterCharId)
            && !string.IsNullOrEmpty(character)
            && string.Equals(character.Trim(), glitchEnterCharId.Trim(), StringComparison.OrdinalIgnoreCase);

        // 슬롯 이미지에 외곽 글리치 머티리얼을 입히고 _GlitchAmount 1→0(깨짐→정착) 구동.
        // 알파(CanvasGroup)는 빠르게(60%) 올려 등장은 즉시 보이고 외곽 글리치만 마저 풀린다. 종료 시 원본 머티리얼 복원.
        IEnumerator GlitchEnter(int idx, SlotBinding slot)
        {
            var img = slot.image;
            float dur = characterGlitchSeconds;
            if (img == null || dur <= 0f) { yield return FadeGroup(slot.group, 0f, 1f, dur); yield break; }

            var inst = _charGlitchInst[idx];
            if (inst == null) { inst = new Material(characterGlitchMaterial); _charGlitchInst[idx] = inst; }
            img.material = inst;

            SetAlpha(slot.group, 0f);
            inst.SetFloat(CharGlitchAmountId, 1f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                SetAlpha(slot.group, Mathf.Clamp01(k / 0.6f)); // 등장 알파는 빠르게 도달
                inst.SetFloat(CharGlitchAmountId, 1f - k);     // 글리치는 전체 구간에 걸쳐 정착
                yield return null;
            }
            SetAlpha(slot.group, 1f);
            inst.SetFloat(CharGlitchAmountId, 0f);
            img.material = _charBaseMat[idx]; // 정상 상태는 글리치 없음 — 원본 머티리얼 복귀.
        }

        // 캐릭터 Enter 시 히로인별 스케일·오프셋을 슬롯 기본 위에 적용(미등록·카탈로그 없음 = 항등 = 슬롯 기본 그대로).
        void ApplyPlacement(int idx, SlotBinding slot, string character)
        {
            if (slot?.image == null) return;
            EnsureBaseCaptured(idx, slot);
            var p = stageCatalog != null ? stageCatalog.Resolve(character) : StagePlacement.Identity;
            var rt = slot.image.rectTransform;
            rt.localScale = p.ScaleFrom(_baseScale[idx]);
            rt.anchoredPosition = p.PositionFrom(_basePos[idx]);
        }

        /// <summary>BG·모든 슬롯 즉시 클리어(내러티브 종료 시 시뮬레이션으로 새지 않도록).</summary>
        public void ClearAll()
        {
            if (_bgRoutine != null) { StopCoroutine(_bgRoutine); _bgPending?.Complete(); _bgPending = null; _bgRoutine = null; }
            if (bgFront != null) { bgFront.sprite = null; bgFront.enabled = false; }
            if (bgBack != null) { bgBack.sprite = null; bgBack.enabled = false; }
            if (backdrop != null) backdrop.enabled = false;
            SetAlpha(bgBackGroup, 0f);

            for (int i = 0; i < _slotRoutines.Length; i++)
            {
                if (_slotRoutines[i] != null) { StopCoroutine(_slotRoutines[i]); _slotPending[i]?.Complete(); _slotPending[i] = null; _slotRoutines[i] = null; }
            }
            ClearSlotVisual(slotL); ClearSlotVisual(slotC); ClearSlotVisual(slotR);
            SetAlpha(slotL?.group, 0f); SetAlpha(slotC?.group, 0f); SetAlpha(slotR?.group, 0f);
            for (int i = 0; i < _slotChar.Length; i++) _slotChar[i] = null;
        }

        // ── 코루틴 lerp ──

        IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            if (duration <= 0f) { cg.alpha = to; yield break; }

            cg.alpha = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        /// <summary>두 CanvasGroup을 동시에 lerp(크로스페이드).</summary>
        IEnumerator Fade2(CanvasGroup a, float aFrom, float aTo, CanvasGroup b, float bFrom, float bTo, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(a, aTo); SetAlpha(b, bTo);
                yield break;
            }
            SetAlpha(a, aFrom); SetAlpha(b, bFrom);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                SetAlpha(a, Mathf.Lerp(aFrom, aTo, k));
                SetAlpha(b, Mathf.Lerp(bFrom, bTo, k));
                yield return null;
            }
            SetAlpha(a, aTo); SetAlpha(b, bTo);
        }

        static void SetAlpha(CanvasGroup cg, float a) { if (cg != null) cg.alpha = a; }

        /// <summary>캐릭터/표정 → Resources 하위 경로 키(순수). 폴더 구조 Characters/{char}/{emote}.</summary>
        public static string CharSpriteKey(string character, string emote)
        {
            if (string.IsNullOrEmpty(character)) return null;
            return string.IsNullOrEmpty(emote) ? character : $"{character}/{emote}";
        }

        Sprite LoadCharSprite(string character, string emote)
        {
            string key = CharSpriteKey(character, emote);
            return key == null ? null : LoadSprite($"{charRoot}/{key}");
        }

        // 표정 스프라이트 로드 + 누락 시 기본 표정(fallbackEmote) 폴백. 작가 스크립트가 정본이라 연출을 멈추지 않고
        // 기본 표정으로 대체하되, 누락 사실을 개발 토스트(우측 상단)로 남겨 에셋 추가를 유도한다(릴리즈 빌드선 호출 제거).
        Sprite LoadCharSpriteOrFallback(string character, string emote)
        {
            var sprite = LoadCharSprite(character, emote);
            if (sprite != null) return sprite;
            if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(fallbackEmote)) return null;
            if (string.Equals(emote, fallbackEmote, StringComparison.OrdinalIgnoreCase)) return null; // 기본조차 없으면 폴백 무의미

            var fb = LoadCharSprite(character, fallbackEmote);
            if (fb != null) DevToast.Warn($"표정 없음: {character}/{emote} → {fallbackEmote}");
            return fb;
        }

        Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);
    }
}
