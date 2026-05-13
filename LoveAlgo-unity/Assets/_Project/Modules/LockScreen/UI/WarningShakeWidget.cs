using DG.Tweening;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// 잠금화면의 WARNING(PATIENCE LIMIT REACHED) 위젯 흔들기 애니메이션.
    /// 기획서 §구성: "흔들흔들 애니메이션 필요"
    ///
    /// 동작: 활성화되면 일정 간격으로 좌우/회전 흔들기 반복.
    /// </summary>
    public class WarningShakeWidget : MonoBehaviour
    {
        [Header("타겟 (비워두면 자기 RectTransform)")]
        [SerializeField] RectTransform target;

        [Header("흔들기 패턴")]
        [Tooltip("한 사이클 흔들기 길이 (초)")]
        [SerializeField] float shakeDuration = 0.6f;
        [Tooltip("사이클 사이 휴식 시간 (초)")]
        [SerializeField] float restDuration = 1.5f;
        [Tooltip("회전 흔들기 강도 (도)")]
        [SerializeField] float rotationStrength = 8f;
        [Tooltip("좌우 위치 흔들기 강도 (px)")]
        [SerializeField] float positionStrength = 4f;
        [Tooltip("진동수 (높을수록 더 빠르게)")]
        [SerializeField] int vibrato = 10;
        [Tooltip("랜덤성 (0 = 직선, 90 = 완전 랜덤)")]
        [SerializeField] float randomness = 70f;

        Sequence shakeSeq;
        Quaternion originalRotation;
        Vector2 originalAnchoredPos;

        void Awake()
        {
            if (target == null) target = GetComponent<RectTransform>();
            if (target != null)
            {
                originalRotation = target.localRotation;
                originalAnchoredPos = target.anchoredPosition;
            }
        }

        void OnEnable()
        {
            StartShakeLoop();
        }

        void OnDisable()
        {
            StopShakeLoop();
        }

        void StartShakeLoop()
        {
            if (target == null) return;
            shakeSeq?.Kill();

            shakeSeq = DOTween.Sequence().SetLoops(-1).SetUpdate(true);

            // 회전 + 위치 동시 흔들기
            shakeSeq.AppendCallback(() =>
            {
                target.DOShakeRotation(shakeDuration, new Vector3(0, 0, rotationStrength), vibrato, randomness, true)
                    .SetUpdate(true);
                target.DOShakeAnchorPos(shakeDuration, new Vector2(positionStrength, positionStrength * 0.5f), vibrato, randomness, false, true)
                    .SetUpdate(true);
            });

            shakeSeq.AppendInterval(shakeDuration);
            shakeSeq.AppendInterval(restDuration);
        }

        void StopShakeLoop()
        {
            shakeSeq?.Kill();
            shakeSeq = null;
            if (target != null)
            {
                target.localRotation = originalRotation;
                target.anchoredPosition = originalAnchoredPos;
            }
        }

        void OnDestroy()
        {
            shakeSeq?.Kill();
        }
    }
}
