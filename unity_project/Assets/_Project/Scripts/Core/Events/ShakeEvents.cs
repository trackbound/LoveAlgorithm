namespace LoveAlgo.Events
{
    // ── 흔들기 FX 명령(M3 슬라이스2: StageShake/DialogueShake/CharShake) ──
    // 스크린 FX·스테이지와 같은 구조: 순수 ShakeParser가 FX Value를 인텐트로 분해 → 엔진(NarrativeController)이
    // 동결 수치(ShakeTuningSO)로 강도(px)·지속(초)을 해석 → 명령에 실어 발행 → 뷰(ShakeView)가 대상 RectTransform의
    // anchoredPosition/회전을 임팩트형 감쇠 진동(Hitlag freeze → exp 감쇠 sin)으로 흔들고 완료 핸들을 푼다(ADR-007).
    //
    // 대상이 셋(스테이지 루트·대사창·캐릭터 슬롯)이라 단일 오버레이인 스크린 FX와 달리 Target으로 라우팅한다.
    // 구 ScreenFX(싱글톤+DOTween, 카메라/캐릭터까지 한 클래스)의 구조는 가져오지 않고, 대상별 ShakeView가 자기
    // RectTransform만 흔든다. 캐릭터 슬롯은 <see cref="CharSlot"/>(StageEvents) 재사용.

    /// <summary>흔들기 대상. 명령을 어느 RectTransform으로 보낼지 라우팅한다.</summary>
    public enum ShakeTarget
    {
        Stage,    // _Stage 캔버스 콘텐츠 루트(배경+캐릭터 전체)
        Dialogue, // 대사창
        Char      // 특정 캐릭터 슬롯(L/C/R)
    }

    /// <summary>
    /// 임팩트 진동 프로파일(엔진이 ShakeTuningSO에서 해석해 명령에 실음). 뷰는 이 값으로 감쇠 진동을 그린다 —
    /// 뷰가 튜닝 SO를 직접 알지 않도록(ADR-007: UI는 표시만) 명령에 동봉한다.
    /// </summary>
    public readonly struct ShakeProfile
    {
        public readonly float XMultiplier;
        public readonly float YMultiplier;
        public readonly float RotationMultiplier;
        public readonly float FrequencyHz;
        public readonly float Damping;
        public readonly float HitlagSeconds;

        public ShakeProfile(float xMul, float yMul, float rotMul, float frequencyHz, float damping, float hitlagSeconds)
        {
            XMultiplier = xMul;
            YMultiplier = yMul;
            RotationMultiplier = rotMul;
            FrequencyHz = frequencyHz;
            Damping = damping;
            HitlagSeconds = hitlagSeconds;
        }
    }

    /// <summary>
    /// 흔들기 표시 명령. <see cref="StrengthPx"/>·<see cref="Duration"/>·<see cref="Profile"/>은 엔진이 동결 수치로
    /// 이미 해석한 최종값. <see cref="Slot"/>은 <see cref="Target"/>=<see cref="ShakeTarget.Char"/>일 때만 의미 있다.
    /// </summary>
    public readonly struct ShakeCommand
    {
        public readonly ShakeTarget Target;
        public readonly CharSlot Slot;
        public readonly float StrengthPx;
        public readonly float Duration;
        public readonly ShakeProfile Profile;
        public readonly CompletionHandle Handle;

        public ShakeCommand(ShakeTarget target, CharSlot slot, float strengthPx, float duration, ShakeProfile profile, CompletionHandle handle)
        {
            Target = target;
            Slot = slot;
            StrengthPx = strengthPx;
            Duration = duration;
            Profile = profile;
            Handle = handle;
        }
    }
}
