using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShakeCommand, ShakeTarget, ShakeProfile, CharSlot, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 흔들기 FX 뷰(*View, M3 슬라이스2: StageShake/DialogueShake/CharShake). <see cref="ShakeCommand"/>를 구독해
    /// 자신의 <see cref="target"/>(과 Char면 <see cref="slot"/>)이 일치할 때만 대상 RectTransform의 anchoredPosition/
    /// 회전을 임팩트형 감쇠 진동(Hitlag freeze → exp 감쇠 sin)으로 흔들고, 완료 시 핸들을 푼다(ADR-007: UI는 표시만).
    /// 슬라이스1처럼 DOTween 미사용(코루틴 lerp). 엔진(NarrativeController)은 이 뷰를 직접 알지 못한다 — 명령+핸들로만.
    ///
    /// 대상별 1개를 배치한다: Stage=_Stage 콘텐츠 루트 / Dialogue=대사창 / Char=슬롯 L·C·R 참조 보유.
    /// 진동 프로파일·강도·지속은 명령에 실려 오므로(엔진이 SO로 해석) 이 뷰는 튜닝 SO를 알지 않는다.
    /// </summary>
    public class ShakeView : MonoBehaviour
    {
        [Tooltip("이 뷰가 담당하는 흔들기 대상. 명령 라우팅 기준.")]
        [SerializeField] ShakeTarget target = ShakeTarget.Stage;

        [Tooltip("Stage/Dialogue: 흔들 RectTransform(미지정 시 자기 자신).")]
        [SerializeField] RectTransform body;

        [Header("Char 전용 슬롯 참조 (target=Char일 때)")]
        [SerializeField] RectTransform slotL;
        [SerializeField] RectTransform slotC;
        [SerializeField] RectTransform slotR;

        public ShakeTarget Target { get => target; set => target = value; }
        public RectTransform Body { get => body; set => body = value; }
        public RectTransform SlotL { get => slotL; set => slotL = value; }
        public RectTransform SlotC { get => slotC; set => slotC = value; }
        public RectTransform SlotR { get => slotR; set => slotR = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;
        RectTransform _shaking;        // 현재 흔드는 대상(리셋용)
        Vector2 _restPos;
        Quaternion _restRot;

        void Awake()
        {
            if (body == null) body = transform as RectTransform;
        }

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShakeCommand>(OnShake);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetShake());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetShake()); // 도구 화면 정리
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
            ResetShake();
        }

        void OnShake(ShakeCommand e)
        {
            if (e.Target != target) return; // 내 대상 아님.

            var rt = ResolveTarget(e);
            if (rt == null)
            {
                e.Handle?.Complete(); // 대상 없으면 엔진을 막지 않도록 즉시 완료.
                return;
            }

            // 이전 흔들기가 진행 중이면 정지·원위치·핸들 해제(엔진 블록 방지).
            StopCurrent();

            _pending = e.Handle;
            _routine = StartCoroutine(Run(rt, e.StrengthPx, e.Duration, e.Profile));
        }

        RectTransform ResolveTarget(ShakeCommand e)
        {
            if (target != ShakeTarget.Char) return body;
            switch (e.Slot)
            {
                case CharSlot.L: return slotL;
                case CharSlot.R: return slotR;
                default:         return slotC;
            }
        }

        IEnumerator Run(RectTransform rt, float strength, float duration, ShakeProfile p)
        {
            _shaking = rt;
            _restPos = rt.anchoredPosition;
            _restRot = rt.localRotation;

            float safeDuration = Mathf.Max(0.05f, duration);
            float omega = 2f * Mathf.PI * Mathf.Max(1f, p.FrequencyHz);
            float safeDamping = Mathf.Max(0.1f, p.Damping);

            // 완전 랜덤 방향(좌우뿐 아니라 2D 전 방향) — 초기 임팩트 킥용.
            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float dirX = Mathf.Cos(angle);
            float dirY = Mathf.Sin(angle);

            // 채널별 독립 노이즈 시드 — X·Y·회전이 같은 파형을 공유하지 않게(단일축 사인 진자 → 다방향 난류 = 지진 체감).
            float seedX = UnityEngine.Random.value * 1000f;
            float seedY = UnityEngine.Random.value * 1000f;
            float seedR = UnityEngine.Random.value * 1000f;

            // Hitlag: 충격 직후 변위 고정(프리즈 프레임). 지속의 10% 상한.
            float hitlag = Mathf.Min(p.HitlagSeconds, safeDuration * 0.10f);

            float elapsed = 0f;
            while (elapsed < safeDuration)
            {
                float x, y, zRot;
                if (elapsed < hitlag)
                {
                    // Phase 1 — Hitlag: 충격 방향으로 변위 고정.
                    x = dirX * strength * p.XMultiplier;
                    y = dirY * strength * p.YMultiplier;
                    zRot = dirX * strength * p.RotationMultiplier * 0.4f;
                }
                else
                {
                    // Phase 2 — 감쇠 난류(2-옥타브 Perlin). 매끈한 단일 사인이 아니라 불규칙·다방향 고주파 떨림 = 지진 체감.
                    // 연속 노이즈라 프레임당 점프 없이 깔끔하고, exp 감쇠로 빠르게 잦아든다.
                    float t2 = elapsed - hitlag;
                    float decay = Mathf.Exp(-safeDamping * t2);
                    float nt = t2 * omega / (2f * Mathf.PI); // 노이즈 시간(초당 ~FrequencyHz 특징 변화).
                    x = FractalNoise(seedX, nt) * strength * p.XMultiplier * decay;
                    y = FractalNoise(seedY, nt) * strength * p.YMultiplier * decay;
                    zRot = FractalNoise(seedR, nt) * strength * p.RotationMultiplier * decay;
                }

                rt.anchoredPosition = _restPos + new Vector2(x, y);
                rt.localRotation = Quaternion.Euler(0f, 0f, zRot) * _restRot;

                elapsed += Time.deltaTime;
                yield return null;
            }

            rt.anchoredPosition = _restPos;
            rt.localRotation = _restRot;
            Finish();
        }

        /// <summary>
        /// 2-옥타브 Perlin 난류, 대략 [-1,1]. 단일 사인보다 날카롭고 불규칙한 다방향 떨림(지진형)을 주되,
        /// 연속 노이즈라 프레임 점프 없이 깔끔하다. 게인 1.4로 체감 진폭을 사인 수준으로 맞춤(드물게 살짝 초과 = 더 큰 충격).
        /// </summary>
        static float FractalNoise(float seed, float x)
        {
            float n  = Mathf.PerlinNoise(seed, x) * 2f - 1f;              // 1옥타브(기저)
            n += (Mathf.PerlinNoise(seed + 37.2f, x * 2.3f) * 2f - 1f) * 0.5f; // 2옥타브(고주파 디테일)
            return (n / 1.5f) * 1.4f;
        }

        void StopCurrent()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                if (_shaking != null) { _shaking.anchoredPosition = _restPos; _shaking.localRotation = _restRot; }
                _pending?.Complete();
                _pending = null;
                _shaking = null;
            }
        }

        void Finish()
        {
            var h = _pending;
            _pending = null;
            _routine = null;
            _shaking = null;
            h?.Complete();
        }

        /// <summary>내러티브 종료/비활성 시 진행 중 흔들기를 즉시 원위치로 복귀.</summary>
        void ResetShake() => StopCurrent();
    }
}
