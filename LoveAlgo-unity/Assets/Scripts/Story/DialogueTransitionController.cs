using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 대화 전환 제어 시스템
    /// - 화자 변경 시 부드러운 전환
    /// - 대화창 위치 조정 (왼쪽 캐릭터 말할 때 오른쪽으로 이동 등)
    /// - 이름박스 강조 효과
    /// - 배경 흐림 효과 (선택적)
    /// </summary>
    public class DialogueTransitionController : MonoBehaviour
    {
        [Header("Name Box Animation")]
        [SerializeField] RectTransform nameBoxTransform;
        [SerializeField] bool enableNameBoxPulse = true;
        [SerializeField] float nameBoxPulseScale = 1.08f;
        [SerializeField] float nameBoxPulseDuration = 0.2f;

        [Header("Dialogue Box Position")]
        [SerializeField] RectTransform dialogueBoxTransform;
        [SerializeField] bool enableDialogueBoxShift = false;
        [SerializeField] float shiftAmount = 50f;  // 좌/우 이동 거리
        [SerializeField] float shiftDuration = 0.3f;

        [Header("Background Dim")]
        [SerializeField] CanvasGroup backgroundDimGroup;
        [SerializeField] bool enableBackgroundDim = false;
        [SerializeField] float dimAlpha = 0.3f;
        [SerializeField] float dimDuration = 0.3f;

        [Header("Speaker Indicator")]
        [SerializeField] GameObject leftSpeakerIndicator;   // 왼쪽 캐릭터 화살표 등
        [SerializeField] GameObject rightSpeakerIndicator;  // 오른쪽 캐릭터 화살표 등
        [SerializeField] GameObject centerSpeakerIndicator; // 중앙 캐릭터 화살표 등

        Vector3 originalNameBoxScale;
        Vector2 originalDialogueBoxPosition;
        Vector2 leftShiftPosition;
        Vector2 rightShiftPosition;

        string currentSpeaker;
        Sequence nameBoxAnimation;
        Sequence dialogueBoxAnimation;

        void Awake()
        {
            if (nameBoxTransform != null)
                originalNameBoxScale = nameBoxTransform.localScale;

            if (dialogueBoxTransform != null)
            {
                originalDialogueBoxPosition = dialogueBoxTransform.anchoredPosition;
                leftShiftPosition = originalDialogueBoxPosition + new Vector2(shiftAmount, 0);
                rightShiftPosition = originalDialogueBoxPosition - new Vector2(shiftAmount, 0);
            }

            // 인디케이터 초기 상태
            HideAllSpeakerIndicators();
        }

        /// <summary>
        /// 화자 변경 시 호출
        /// </summary>
        public async UniTask OnSpeakerChanged(string newSpeaker, SlotPosition? speakerSlot = null, CancellationToken ct = default)
        {
            if (currentSpeaker == newSpeaker) return;
            currentSpeaker = newSpeaker;

            // 여러 효과를 동시에 실행
            var tasks = new System.Collections.Generic.List<UniTask>();

            // 1. 이름박스 펄스
            if (enableNameBoxPulse && nameBoxTransform != null)
            {
                tasks.Add(PlayNameBoxPulse(ct));
            }

            // 2. 대화창 위치 조정
            if (enableDialogueBoxShift && speakerSlot.HasValue)
            {
                tasks.Add(ShiftDialogueBox(speakerSlot.Value, ct));
            }

            // 3. 화자 인디케이터 표시
            if (speakerSlot.HasValue)
            {
                ShowSpeakerIndicator(speakerSlot.Value);
            }

            // 4. 배경 Dim 조정
            if (enableBackgroundDim)
            {
                tasks.Add(UpdateBackgroundDim(ct));
            }

            // 모든 효과 대기
            if (tasks.Count > 0)
            {
                try
                {
                    await UniTask.WhenAll(tasks);
                }
                catch (System.OperationCanceledException)
                {
                    // 취소됨
                }
            }
        }

        /// <summary>
        /// 이름박스 펄스 애니메이션
        /// </summary>
        async UniTask PlayNameBoxPulse(CancellationToken ct)
        {
            nameBoxAnimation?.Kill();

            Vector3 pulseScale = originalNameBoxScale * nameBoxPulseScale;

            try
            {
                await nameBoxTransform.DOScale(pulseScale, nameBoxPulseDuration * 0.5f)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);

                await nameBoxTransform.DOScale(originalNameBoxScale, nameBoxPulseDuration * 0.5f)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨
            }
        }

        /// <summary>
        /// 대화창 위치 조정 (화자 위치에 따라)
        /// </summary>
        async UniTask ShiftDialogueBox(SlotPosition speakerSlot, CancellationToken ct)
        {
            if (dialogueBoxTransform == null) return;

            dialogueBoxAnimation?.Kill();

            Vector2 targetPos = originalDialogueBoxPosition;

            switch (speakerSlot)
            {
                case SlotPosition.L:
                    targetPos = rightShiftPosition;  // 왼쪽 캐릭터 → 대화창 오른쪽으로
                    break;
                case SlotPosition.R:
                    targetPos = leftShiftPosition;   // 오른쪽 캐릭터 → 대화창 왼쪽으로
                    break;
                case SlotPosition.C:
                    targetPos = originalDialogueBoxPosition;
                    break;
            }

            try
            {
                await dialogueBoxTransform.DOAnchorPos(targetPos, shiftDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨
            }
        }

        /// <summary>
        /// 화자 인디케이터 표시
        /// </summary>
        void ShowSpeakerIndicator(SlotPosition speakerSlot)
        {
            HideAllSpeakerIndicators();

            switch (speakerSlot)
            {
                case SlotPosition.L:
                    if (leftSpeakerIndicator != null)
                        leftSpeakerIndicator.SetActive(true);
                    break;
                case SlotPosition.R:
                    if (rightSpeakerIndicator != null)
                        rightSpeakerIndicator.SetActive(true);
                    break;
                case SlotPosition.C:
                    if (centerSpeakerIndicator != null)
                        centerSpeakerIndicator.SetActive(true);
                    break;
            }
        }

        void HideAllSpeakerIndicators()
        {
            if (leftSpeakerIndicator != null)
                leftSpeakerIndicator.SetActive(false);
            if (rightSpeakerIndicator != null)
                rightSpeakerIndicator.SetActive(false);
            if (centerSpeakerIndicator != null)
                centerSpeakerIndicator.SetActive(false);
        }

        /// <summary>
        /// 배경 Dim 업데이트 (대화 중 배경을 약간 어둡게)
        /// </summary>
        async UniTask UpdateBackgroundDim(CancellationToken ct)
        {
            if (backgroundDimGroup == null) return;

            try
            {
                await backgroundDimGroup.DOFade(dimAlpha, dimDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨
            }
        }

        /// <summary>
        /// 배경 Dim 해제 (대화 종료 시)
        /// </summary>
        public async UniTask ClearBackgroundDim(CancellationToken ct = default)
        {
            if (backgroundDimGroup == null) return;

            try
            {
                await backgroundDimGroup.DOFade(0f, dimDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨
            }
        }

        /// <summary>
        /// 리셋 (장면 전환 시)
        /// </summary>
        public void Reset()
        {
            nameBoxAnimation?.Kill();
            dialogueBoxAnimation?.Kill();

            currentSpeaker = null;

            if (nameBoxTransform != null)
                nameBoxTransform.localScale = originalNameBoxScale;

            if (dialogueBoxTransform != null)
                dialogueBoxTransform.anchoredPosition = originalDialogueBoxPosition;

            HideAllSpeakerIndicators();

            if (backgroundDimGroup != null)
                backgroundDimGroup.alpha = 0f;
        }
    }
}
