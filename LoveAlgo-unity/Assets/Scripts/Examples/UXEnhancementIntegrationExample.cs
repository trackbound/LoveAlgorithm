using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using LoveAlgo.UI;
using LoveAlgo.Story;

namespace LoveAlgo.Examples
{
    /// <summary>
    /// UX 개선 시스템 통합 예제
    /// 실제 프로젝트에 통합하는 방법을 보여주는 예제 코드
    /// </summary>
    public class UXEnhancementIntegrationExample : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] DialogueUI dialogueUI;
        [SerializeField] EnhancedChoiceUI enhancedChoiceUI;
        [SerializeField] CharacterLayer characterLayer;

        [Header("Components (Auto-found)")]
        DialogueTransitionController transitionController;
        AdvancedTypingSoundController typingSoundController;
        InteractionFeedbackManager feedbackManager;

        string lastSpeaker;

        void Awake()
        {
            // 컴포넌트 자동 검색
            if (dialogueUI != null)
            {
                transitionController = dialogueUI.GetComponent<DialogueTransitionController>();
                typingSoundController = dialogueUI.GetComponent<AdvancedTypingSoundController>();
            }

            feedbackManager = InteractionFeedbackManager.Instance;
        }

        /// <summary>
        /// 예제 1: 대화 표시 시 UX 개선 통합
        /// </summary>
        public async UniTask ShowEnhancedDialogue(string speaker, string text, CancellationToken ct = default)
        {
            // 1. 화자 변경 시 전환 효과
            if (transitionController != null && speaker != lastSpeaker)
            {
                var speakerSlot = DetermineSpeakerSlot(speaker);
                await transitionController.OnSpeakerChanged(speaker, speakerSlot, ct);
                lastSpeaker = speaker;
            }

            // 2. 캐릭터 ID 가져오기 (타이핑 사운드용)
            string characterId = GetCharacterIdFromSpeaker(speaker);

            // 3. 타이핑 사운드 캐릭터 설정
            if (typingSoundController != null && !string.IsNullOrEmpty(characterId))
            {
                typingSoundController.SetCurrentCharacter(characterId);
            }

            // 4. 캐릭터 강조 (말하는 중)
            var characterSlot = GetCharacterSlotBySpeaker(speaker);
            var reactionSystem = characterSlot?.GetComponent<CharacterReactionSystem>();
            reactionSystem?.StartSpeaking();

            // 5. 대화 표시 (기존 DialogueUI 사용)
            if (dialogueUI != null)
            {
                await dialogueUI.ShowTextAsync(speaker, text, ct);
            }

            // 6. 대화 종료 후 캐릭터 강조 해제
            reactionSystem?.StopSpeaking();
        }

        /// <summary>
        /// 예제 2: 선택지 표시 시 UX 개선 통합
        /// </summary>
        public async UniTask<ChoiceResult> ShowEnhancedChoice(System.Collections.Generic.List<OptionData> options, CancellationToken ct = default)
        {
            // EnhancedChoiceUI 사용 (자동으로 인터랙션 피드백 포함)
            if (enhancedChoiceUI != null)
            {
                return await enhancedChoiceUI.ShowAndWaitAsync(options, ct);
            }

            // 폴백: 기존 ChoiceUI
            return null;
        }

        /// <summary>
        /// 예제 3: 중요한 선택 시 추가 피드백
        /// </summary>
        public async UniTask ShowImportantChoice(System.Collections.Generic.List<OptionData> options, Color flashColor, CancellationToken ct = default)
        {
            // 1. 화면 플래시로 중요성 강조
            if (feedbackManager != null)
            {
                await feedbackManager.PlayScreenFlash(flashColor, intensity: 0.3f, ct);
            }

            // 2. 선택지 표시
            var result = await ShowEnhancedChoice(options, ct);

            // 3. 선택 후 피드백
            if (feedbackManager != null && result != null)
            {
                feedbackManager.PlayImportantDialogueFeedback(Color.green).Forget();
            }
        }

        /// <summary>
        /// 예제 4: 캐릭터 감정 반응 재생
        /// </summary>
        public void PlayCharacterReaction(string speaker, ReactionType reactionType)
        {
            var characterSlot = GetCharacterSlotBySpeaker(speaker);
            var reactionSystem = characterSlot?.GetComponent<CharacterReactionSystem>();

            if (reactionSystem != null)
            {
                reactionSystem.PlayReaction(reactionType).Forget();
            }
        }

        /// <summary>
        /// 예제 5: 대화 시작 전 인터랙션 피드백
        /// </summary>
        public void OnDialogueClick(Vector2 screenPosition)
        {
            if (feedbackManager != null)
            {
                // 리플 효과만 (파티클은 선택지에만)
                feedbackManager.PlayRipple(screenPosition, default).Forget();
            }
        }

        #region Helper Methods

        /// <summary>
        /// 화자로부터 SlotPosition 결정
        /// </summary>
        SlotPosition? DetermineSpeakerSlot(string speaker)
        {
            if (characterLayer == null) return null;

            // 각 슬롯 확인
            var slotL = characterLayer.GetSlot(SlotPosition.L);
            var slotC = characterLayer.GetSlot(SlotPosition.C);
            var slotR = characterLayer.GetSlot(SlotPosition.R);

            string characterId = GetCharacterIdFromSpeaker(speaker);
            if (string.IsNullOrEmpty(characterId)) return null;

            if (slotL != null && !slotL.IsEmpty && slotL.CurrentCharacter == characterId)
                return SlotPosition.L;
            if (slotC != null && !slotC.IsEmpty && slotC.CurrentCharacter == characterId)
                return SlotPosition.C;
            if (slotR != null && !slotR.IsEmpty && slotR.CurrentCharacter == characterId)
                return SlotPosition.R;

            return null;
        }

        /// <summary>
        /// 화자 이름 → 캐릭터 ID 변환
        /// </summary>
        string GetCharacterIdFromSpeaker(string speaker)
        {
            // CharacterDatabase 사용 또는 간단한 매핑
            // 예: "다은" → "Daeun"
            // 실제로는 CharacterDatabase.SpeakerToCharacterId() 사용
            return speaker;
        }

        /// <summary>
        /// 화자로부터 CharacterSlot 가져오기
        /// </summary>
        GameObject GetCharacterSlotBySpeaker(string speaker)
        {
            var slotPos = DetermineSpeakerSlot(speaker);
            if (!slotPos.HasValue || characterLayer == null) return null;

            var slot = characterLayer.GetSlot(slotPos.Value);
            return slot?.gameObject;
        }

        #endregion

        #region Example Usage in ScriptRunner

        /*
        // ScriptRunner.ExecuteTextAsync()에서 통합 예제:

        async UniTask ExecuteTextAsync(ScriptLine line, CancellationToken ct)
        {
            // UX 개선 통합
            var uxExample = FindObjectOfType<UXEnhancementIntegrationExample>();
            if (uxExample != null)
            {
                await uxExample.ShowEnhancedDialogue(line.Speaker, line.Value, ct);
            }
            else
            {
                // 기존 방식
                var dialogueUI = UIManager.Instance?.DialogueUI;
                if (dialogueUI != null)
                {
                    await dialogueUI.ShowTextAsync(line.Speaker, line.Value, ct);
                }
            }
        }

        // ScriptRunner.ExecuteChoiceAsync()에서 통합 예제:

        async UniTask ExecuteChoiceAsync(ScriptLine line, CancellationToken ct)
        {
            var options = CollectOptions();

            // UX 개선 통합
            var uxExample = FindObjectOfType<UXEnhancementIntegrationExample>();
            ChoiceResult result = null;

            if (uxExample != null)
            {
                // 중요한 선택인지 판별 (예: 호감도 ±10 이상)
                bool isImportant = IsImportantChoice(options);
                if (isImportant)
                {
                    result = await uxExample.ShowImportantChoice(options, Color.yellow, ct);
                }
                else
                {
                    result = await uxExample.ShowEnhancedChoice(options, ct);
                }
            }
            else
            {
                // 기존 방식
                var choiceUI = UIManager.Instance?.ChoiceUI;
                if (choiceUI != null)
                {
                    result = await choiceUI.ShowAndWaitAsync(options, ct);
                }
            }

            // 결과 처리
            if (result != null && !string.IsNullOrEmpty(result.JumpTarget))
            {
                // Jump 처리...
            }
        }
        */

        #endregion
    }
}
