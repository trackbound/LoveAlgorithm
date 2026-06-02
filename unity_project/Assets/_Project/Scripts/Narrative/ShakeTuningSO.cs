using System;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 흔들기 연출 동결 수치 Definition SO(ADR-012: 코드 매직넘버 금지). 강도 프리셋(px)·기본 지속과
    /// 대상별 임팩트 진동 프로파일(X/Y/회전 배수·진동수·감쇠)을 담는다. 엔진(NarrativeController)이 참조해
    /// CSV 미지정값을 해석하고, 뷰(ShakeView)가 프로파일로 감쇠 진동을 그린다. 런타임 읽기 전용.
    ///
    /// 값 출처 = 실제 런타임 임팩트 모델(구 ScreenFX SerializeField 동결값, 감독 결정 2026-06-02). 프리셋
    /// 10/25/50px·지속 0.3s·Stage(X1.0/Y0.35/Rot0.06, 5Hz/감쇠5.2)·Dialogue(Y0.12/Rot0.02, 6Hz/감쇠6.5).
    /// Char 프로파일은 구 DOTween DOShakeAnchorPos를 임팩트 모델로 통일하며 신설 — 감독 튜닝 영역.
    /// </summary>
    [CreateAssetMenu(fileName = "ShakeTuning", menuName = "LoveAlgo/Shake Tuning")]
    public class ShakeTuningSO : ScriptableObject
    {
        /// <summary>대상별 임팩트 진동 프로파일(직렬화). 0이면 해당 축 정지.</summary>
        [Serializable]
        public struct Profile
        {
            [Tooltip("X축 변위 배수")] public float xMultiplier;
            [Tooltip("Y축 변위 배수")] public float yMultiplier;
            [Tooltip("Z 회전 배수(deg/px)")] public float rotationMultiplier;
            [Tooltip("진동수(Hz)")] public float frequencyHz;
            [Tooltip("감쇠 계수(클수록 빨리 잦아듦)")] public float damping;
        }

        [Header("강도 프리셋 (px)")]
        [SerializeField] float weak = 10f;
        [SerializeField] float medium = 25f;
        [SerializeField] float strong = 50f;

        [Header("기본 지속 (s)")]
        [SerializeField] float shakeDuration = 0.3f;
        [Tooltip("충격 직후 변위를 유지하는 시간(Hitlag/프리즈 프레임). 지속의 10% 상한.")]
        [SerializeField] float hitlagSeconds = 0.025f;

        [Header("캐릭터 기본 강도 (px) — 프리셋 미사용")]
        [SerializeField] float charStrength = 18f;

        [Header("대상별 임팩트 프로파일")]
        [SerializeField] Profile stage = new Profile { xMultiplier = 1.0f, yMultiplier = 0.35f, rotationMultiplier = 0.06f, frequencyHz = 5.0f, damping = 5.2f };
        [SerializeField] Profile dialogue = new Profile { xMultiplier = 1.0f, yMultiplier = 0.12f, rotationMultiplier = 0.02f, frequencyHz = 6.0f, damping = 6.5f };
        [SerializeField] Profile character = new Profile { xMultiplier = 1.0f, yMultiplier = 1.0f, rotationMultiplier = 0.0f, frequencyHz = 12.0f, damping = 6.5f };

        public float ShakeDuration => shakeDuration;
        public float HitlagSeconds => hitlagSeconds;
        public float CharStrength => charStrength;

        /// <summary>프리셋(약/중/강) → px.</summary>
        public float PresetPx(ShakeStrength preset)
        {
            switch (preset)
            {
                case ShakeStrength.Weak:   return weak;
                case ShakeStrength.Strong: return strong;
                default:                   return medium;
            }
        }

        public Profile ProfileFor(LoveAlgo.Events.ShakeTarget target)
        {
            switch (target)
            {
                case LoveAlgo.Events.ShakeTarget.Dialogue: return dialogue;
                case LoveAlgo.Events.ShakeTarget.Char:     return character;
                default:                                   return stage;
            }
        }
    }
}
