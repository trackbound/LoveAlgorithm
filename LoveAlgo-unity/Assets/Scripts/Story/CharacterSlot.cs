using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 슬롯 위치
    /// </summary>
    public enum SlotPosition
    {
        L,  // 왼쪽
        C,  // 중앙
        R   // 오른쪽
    }

    /// <summary>
    /// 개별 캐릭터 슬롯 (이미지 2개로 크로스페이드 지원)
    /// </summary>
    public class CharacterSlot : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] CanvasGroup slotCanvasGroup;   // 슬롯 전체 (등장/퇴장용)
        [SerializeField] Image imageFront;              // 현재 표시 이미지
        [SerializeField] CanvasGroup frontCanvasGroup;
        [SerializeField] Image imageBack;               // 크로스페이드용 이미지
        [SerializeField] CanvasGroup backCanvasGroup;
        [SerializeField] RectTransform imageContainer;  // 이미지들의 부모 (스케일/오프셋 적용용)

        [Header("설정")]
        [SerializeField] float fadeDuration = 0.3f;
        [SerializeField] float emoteFadeDuration = 0.2f;
        [SerializeField] float enterOffset = 100f;      // 등장 시 슬라이드 거리

        [Header("캐릭터 DB")]
        [SerializeField] CharacterDatabase characterDatabase;

        string currentCharacter;
        string currentEmote;
        RectTransform rectTransform;
        Vector2 originalPosition;

        public string CurrentCharacter => currentCharacter;
        public string CurrentEmote => currentEmote;
        public bool IsEmpty => string.IsNullOrEmpty(currentCharacter);

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalPosition = rectTransform.anchoredPosition;
            }

            // 초기 상태: 숨김
            SetSlotAlpha(0f);
            SetImageAlpha(frontCanvasGroup, 0f);
            SetImageAlpha(backCanvasGroup, 0f);
            EnableImages(false);
        }

        /// <summary>
        /// 캐릭터 등장 (이미 같은 캐릭터가 있으면 스킵)
        /// </summary>
        public async UniTask EnterAsync(string characterName, string emote = "Default", CancellationToken ct = default)
        {
            // 이미 같은 캐릭터가 표시 중이면 스킵
            if (currentCharacter == characterName && !IsEmpty)
            {
                return;
            }

            currentCharacter = characterName;
            currentEmote = string.IsNullOrEmpty(emote) ? "Default" : emote;

            // 스프라이트 로드
            var sprite = LoadSprite(currentCharacter, currentEmote);
            if (sprite == null)
            {
                Debug.LogWarning($"[CharacterSlot] 스프라이트 없음: {currentCharacter}/{currentEmote}");
                return;
            }

            // Front 이미지 설정
            imageFront.sprite = sprite;
            imageFront.enabled = true;
            SetImageAlpha(frontCanvasGroup, 1f);
            SetImageAlpha(backCanvasGroup, 0f);
            imageBack.enabled = false;

            // 캐릭터별 트랜스폼 적용 (스케일 + 오프셋을 imageContainer에 적용)
            ApplyCharacterTransform(characterName, currentEmote, applyOffset: true);

            // 등장 애니메이션: 슬라이드 + 페이드 (슬롯 위치는 고정, imageContainer가 오프셋 담당)
            if (rectTransform != null)
            {
                // 슬롯은 항상 originalPosition에서 시작해서 originalPosition으로 이동
                rectTransform.anchoredPosition = originalPosition + new Vector2(0, -enterOffset);
            }
            SetSlotAlpha(0f);

            var sequence = DOTween.Sequence();
            if (rectTransform != null)
            {
                _ = sequence.Join(rectTransform.DOAnchorPos(originalPosition, fadeDuration).SetEase(Ease.OutCubic));
            }
            if (slotCanvasGroup != null)
            {
                _ = sequence.Join(slotCanvasGroup.DOFade(1f, fadeDuration));
            }

            await sequence.ToUniTask(cancellationToken: ct);
            
            // AudioManager에 캐릭터 등장 알림 (BGM 자동 전환)
            AudioManager.Instance?.OnCharacterEnter(characterName);
        }

        /// <summary>
        /// 표정 변경 (크로스페이드)
        /// </summary>
        public async UniTask EmoteAsync(string emote, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(currentCharacter))
            {
                Debug.LogWarning("[CharacterSlot] 캐릭터가 없는데 표정 변경 시도");
                return;
            }

            // 같은 표정이면 스킵
            if (currentEmote == emote) return;

            currentEmote = emote;

            var sprite = LoadSprite(currentCharacter, currentEmote);
            if (sprite == null)
            {
                Debug.LogWarning($"[CharacterSlot] 스프라이트 없음: {currentCharacter}/{currentEmote}");
                return;
            }

            // Back 이미지에 새 표정 설정 (뒤에서 페이드인)
            imageBack.sprite = sprite;
            imageBack.enabled = true;
            backCanvasGroup.alpha = 0f;
            
            // Back 이미지 트랜스폼 적용
            ApplyCharacterTransform(currentCharacter, currentEmote, applyOffset: false);
            
            // 크로스페이드: Back 페이드인하면서 Front 페이드아웃
            await DOTween.Sequence()
                .Join(backCanvasGroup.DOFade(1f, emoteFadeDuration).SetEase(Ease.Linear))
                .Join(frontCanvasGroup.DOFade(0f, emoteFadeDuration).SetEase(Ease.Linear))
                .ToUniTask(cancellationToken: ct);

            // 스왑: Back의 내용을 Front로 복사
            // 중요: Back이 완전히 보이는 상태(alpha=1)에서 Front를 업데이트
            imageFront.sprite = imageBack.sprite;
            imageFront.enabled = true;
            
            // Front를 다시 보이게 하고 Back은 숨김
            // 이 순간 둘 다 같은 이미지이므로 깜빡임 없음
            frontCanvasGroup.alpha = 1f;
            backCanvasGroup.alpha = 0f;
            imageBack.enabled = false;
        }

        /// <summary>
        /// 캐릭터 퇴장
        /// </summary>
        public async UniTask ExitAsync(CancellationToken ct = default)
        {
            if (IsEmpty) return;

            string exitingCharacter = currentCharacter;

            // 퇴장 애니메이션: 슬롯 전체 페이드 아웃
            if (slotCanvasGroup != null)
            {
                await slotCanvasGroup.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
            }

            EnableImages(false);
            currentCharacter = null;
            currentEmote = null;
            
            // AudioManager에 캐릭터 퇴장 알림 (BGM 자동 전환)
            AudioManager.Instance?.OnCharacterExit(exitingCharacter);
        }

        /// <summary>
        /// 즉시 숨김 (애니메이션 없이)
        /// </summary>
        public void Clear()
        {
            SetSlotAlpha(0f);
            SetImageAlpha(frontCanvasGroup, 0f);
            SetImageAlpha(backCanvasGroup, 0f);
            EnableImages(false);
            ResetCharacterTransform();
            currentCharacter = null;
            currentEmote = null;
        }

        /// <summary>
        /// 캐릭터 트랜스폼 초기화
        /// </summary>
        void ResetCharacterTransform()
        {
            // 이미지 컨테이너에 적용 (없으면 이미지에 직접)
            RectTransform target = imageContainer != null ? imageContainer : imageFront.rectTransform;
            
            // 기본값으로 초기화
            target.localScale = Vector3.one;
            target.anchoredPosition = Vector2.zero;
            
            // 이미지들의 피벗 초기화
            if (imageFront != null)
            {
                imageFront.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                imageFront.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (imageBack != null)
            {
                imageBack.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                imageBack.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
        Sprite LoadSprite(string character, string emote)
        {
            string path = $"Characters/{character}/{emote}";
            var sprite = Resources.Load<Sprite>(path);

            // Default로 폴백
            if (sprite == null && emote != "Default")
            {
                path = $"Characters/{character}/Default";
                sprite = Resources.Load<Sprite>(path);
            }

            return sprite;
        }

        void SetSlotAlpha(float alpha)
        {
            if (slotCanvasGroup != null)
            {
                slotCanvasGroup.alpha = alpha;
            }
        }

        void SetImageAlpha(CanvasGroup cg, float alpha)
        {
            if (cg != null)
            {
                cg.alpha = alpha;
            }
        }

        void EnableImages(bool enable)
        {
            if (imageFront != null) imageFront.enabled = enable;
            if (imageBack != null) imageBack.enabled = enable;
        }

        /// <summary>
        /// 캐릭터별 스케일/오프셋 적용
        /// Container의 pivot을 조정하여 스케일 시 발 위치 고정
        /// </summary>
        void ApplyCharacterTransform(string characterName, string emoteName = null, bool applyOffset = true)
        {
            // 기본값
            float scale = 1f;
            float offsetX = 0f;
            float offsetY = 0f;
            float pivotY = 0f;

            // CharacterDatabase에서 데이터 가져오기
            if (characterDatabase != null)
            {
                var charData = characterDatabase.GetCharacterById(characterName);
                if (charData != null)
                {
                    charData.GetTransform(out scale, out offsetX, out offsetY, out pivotY);
                }
            }

            // 이미지 컨테이너에 적용 (없으면 이미지에 직접)
            RectTransform target = imageContainer != null ? imageContainer : imageFront.rectTransform;

            // Container의 피벗 설정 (스케일 시 기준점)
            // pivotY = 0: 하단 기준 (발 고정), pivotY = 0.5: 중앙 기준
            target.pivot = new Vector2(0.5f, pivotY);

            // 이미지들은 부모(Container)에 Stretch로 붙어있으므로 피벗 변경 불필요
            // anchoredPosition만 초기화
            if (imageFront != null)
            {
                imageFront.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (imageBack != null)
            {
                imageBack.rectTransform.anchoredPosition = Vector2.zero;
            }

            // 스케일 적용
            target.localScale = new Vector3(scale, scale, 1f);
            
            // 오프셋 적용 (선택적)
            if (applyOffset)
            {
                target.anchoredPosition = new Vector2(offsetX, offsetY);
            }
        }
    }
}
