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

        public Image Backdrop { get => backdrop; set => backdrop = value; }
        public Image BgFront { get => bgFront; set => bgFront = value; }
        public CanvasGroup BgFrontGroup { get => bgFrontGroup; set => bgFrontGroup = value; }
        public Image BgBack { get => bgBack; set => bgBack = value; }
        public CanvasGroup BgBackGroup { get => bgBackGroup; set => bgBackGroup = value; }
        public SlotBinding SlotL { get => slotL; set => slotL = value; }
        public SlotBinding SlotC { get => slotC; set => slotC = value; }
        public SlotBinding SlotR { get => slotR; set => slotR = value; }
        public GameObject CharContainer { get => charContainer; set => charContainer = value; }

        IDisposable _bgSub, _charSub, _finishSub, _cgSub, _emoteSub, _resetSub;
        bool _cgHidden;

        // 슬롯(L/C/R) 인덱스 → 현재 올라간 캐릭터 식별자(Char Enter 시 기록). 인라인 <emote> 화자→슬롯 해석에 사용.
        readonly string[] _slotChar = new string[3];

        Coroutine _bgRoutine;
        CompletionHandle _bgPending;

        readonly Coroutine[] _slotRoutines = new Coroutine[3];
        readonly CompletionHandle[] _slotPending = new CompletionHandle[3];

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
        }

        void OnDisable()
        {
            _bgSub?.Dispose(); _charSub?.Dispose(); _finishSub?.Dispose(); _cgSub?.Dispose(); _emoteSub?.Dispose(); _resetSub?.Dispose();
            _bgSub = _charSub = _finishSub = _cgSub = _emoteSub = _resetSub = null;
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
            switch (e.Action)
            {
                case CharAction.Enter:
                {
                    var sprite = LoadCharSprite(e.Character, e.Emote);
                    if (sprite == null)
                    {
                        Log.Warn($"[StageView] 캐릭터 스프라이트 없음: {e.Character}/{e.Emote}");
                        break;
                    }
                    slot.image.sprite = sprite;
                    slot.image.enabled = true;
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
                    var sprite = LoadCharSprite(e.Character, e.Emote);
                    if (sprite != null) slot.image.sprite = sprite;
                    // 슬라이스2: 표정은 즉시 교체(슬롯 내 크로스페이드는 후속). duration은 미사용.
                    break;
                }
                case CharAction.Exit:
                {
                    yield return FadeGroup(slot.group, slot.group != null ? slot.group.alpha : 1f, 0f, e.Duration);
                    ClearSlotVisual(slot);
                    break;
                }
                case CharAction.Clear:
                {
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
            var sprite = LoadCharSprite(_slotChar[idx], e.Emote);
            if (sprite != null) slot.image.sprite = sprite;
            else Log.Warn($"[StageView] <emote> 스프라이트 없음: {_slotChar[idx]}/{e.Emote}");
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
            slot.image.sprite = null;
            slot.image.enabled = false;
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

        Sprite LoadCharSprite(string character, string emote)
        {
            if (string.IsNullOrEmpty(character)) return null;
            string key = string.IsNullOrEmpty(emote) ? character : $"{character}_{emote}";
            return LoadSprite($"{charRoot}/{key}");
        }

        Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);
    }
}
