using System;
using System.Collections.Generic;
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
        /// <summary>
        /// 스프라이트 캐시 (Resources.Load 중복 호출 방지)
        /// LRU 방식: MaxCacheSize 초과 시 가장 오래된 항목 제거
        /// </summary>
        static readonly Dictionary<string, Sprite> spriteCache = new();
        static readonly LinkedList<string> cacheOrder = new();
        const int MaxCacheSize = 40;

        /// <summary>
        /// 스프라이트 캐시 전체 클리어 (장면 전환 시 호출하여 메모리 해제)
        /// </summary>
        public static void ClearSpriteCache()
        {
            spriteCache.Clear();
            cacheOrder.Clear();
            Debug.Log($"[CharacterSlot] 스프라이트 캐시 클리어");
        }

        [Header("바인딩")]
        [SerializeField] CanvasGroup slotCanvasGroup;   // 슬롯 전체 (등장/퇴장용)
        [SerializeField] Image imageFront;              // 현재 표시 이미지
        [SerializeField] CanvasGroup frontCanvasGroup;
        [SerializeField] Image imageBack;               // 크로스페이드용 이미지
        [SerializeField] CanvasGroup backCanvasGroup;
        [SerializeField] RectTransform imageContainer;  // 이미지들의 부모 (스케일/오프셋 적용용)

        [Header("설정")]
        [SerializeField] float fadeDuration = 0.4f;
        [SerializeField] float exitDuration = 0.3f;
        [SerializeField] float emoteFadeDuration = 0.2f;
        [SerializeField] float enterOffset = 40f;        // 등장 시 슬라이드 거리
        [SerializeField] float exitSlideDistance = 40f;  // 퇴장 시 슬라이드 거리

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
        /// 캐릭터 등장 — 순수 페이드 (기본)
        /// 같은 캐릭터+같은 표정이면 스킵, 표정만 다르면 EmoteAsync
        /// </summary>
        public async UniTask EnterAsync(string characterName, string emote = "Default", CancellationToken ct = default)
        {
            string resolvedEmote = string.IsNullOrEmpty(emote) ? "Default" : emote;

            // 이미 같은 캐릭터가 표시 중인 경우
            if (currentCharacter == characterName && !IsEmpty)
            {
                if (currentEmote != resolvedEmote)
                    await EmoteAsync(resolvedEmote, ct);
                return;
            }

            if (!PrepareEnter(characterName, resolvedEmote)) return;

            // 원래 위치에서 페이드인만
            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition;
            SetSlotAlpha(0f);

            if (slotCanvasGroup != null)
                await slotCanvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
            else
                SetSlotAlpha(1f);

            AudioManager.Instance?.OnCharacterEnter(characterName);
        }

        /// <summary>
        /// 캐릭터 등장 — 아래에서 위로 슬라이드 + 페이드
        /// CSV: ,Char,,슬롯:EnterUp:캐릭터[:표정],await
        /// </summary>
        public async UniTask EnterSlideUpAsync(string characterName, string emote = "Default", CancellationToken ct = default)
        {
            string resolvedEmote = string.IsNullOrEmpty(emote) ? "Default" : emote;

            if (currentCharacter == characterName && !IsEmpty)
            {
                if (currentEmote != resolvedEmote)
                    await EmoteAsync(resolvedEmote, ct);
                return;
            }

            if (!PrepareEnter(characterName, resolvedEmote)) return;

            // 아래에서 위로 슬라이드
            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition + new Vector2(0, -enterOffset);
            SetSlotAlpha(0f);

            var sequence = DOTween.Sequence();
            if (rectTransform != null)
                _ = sequence.Join(rectTransform.DOAnchorPos(originalPosition, fadeDuration).SetEase(Ease.OutBack, 1.05f));
            if (slotCanvasGroup != null)
                _ = sequence.Join(slotCanvasGroup.DOFade(1f, fadeDuration * 0.7f).SetEase(Ease.OutCubic));

            await sequence.ToUniTask(cancellationToken: ct);

            AudioManager.Instance?.OnCharacterEnter(characterName);
        }

        /// <summary>
        /// 등장 공통 준비 (스프라이트 로드 + 이미지 세팅 + 트랜스폼 적용)
        /// </summary>
        bool PrepareEnter(string characterName, string resolvedEmote)
        {
            var sprite = LoadSprite(characterName, resolvedEmote);
            if (sprite == null)
            {
                Debug.LogWarning($"[CharacterSlot] 스프라이트 없음: {characterName}/{resolvedEmote}");
                return false;
            }

            currentCharacter = characterName;
            currentEmote = resolvedEmote;

            imageFront.sprite = sprite;
            imageFront.enabled = true;
            SetImageAlpha(frontCanvasGroup, 1f);
            SetImageAlpha(backCanvasGroup, 0f);
            imageBack.enabled = false;

            ApplyCharacterTransform(characterName, currentEmote, applyOffset: true);
            return true;
        }

        /// <summary>
        /// 표정 변경 (크로스페이드)
        /// Back에 새 표정을 준비하고 동시에 페이드 → 완료 후 Front로 승격
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

            // Back 이미지에 새 표정 설정 (뒤에서 준비)
            imageBack.sprite = sprite;
            imageBack.enabled = true;
            backCanvasGroup.alpha = 0f;
            
            // 크로스페이드: Front 위에 Back을 페이드인 (Front는 유지)
            // Front를 페이드아웃하면 Back이 아직 불투명하지 않을 때 빈 곳이 보여 깜빡임 발생
            // → Back만 올려서 자연스럽게 덮기
            await backCanvasGroup.DOFade(1f, emoteFadeDuration)
                .SetEase(Ease.InOutSine)
                .ToUniTask(cancellationToken: ct);

            // 스왑: Back이 완전히 덮은 상태에서 Front를 교체
            // Back alpha=1 유지 → Front sprite 교체 → Front alpha=1 → Back 숨김
            // 이 순서라면 화면에는 항상 무언가 보이므로 깜빡임 없음
            imageFront.sprite = imageBack.sprite;
            frontCanvasGroup.alpha = 1f;
            
            // Back 정리 (Front가 이미 같은 이미지로 덮고 있으므로 안전)
            backCanvasGroup.alpha = 0f;
            imageBack.enabled = false;
        }

        /// <summary>
        /// 캐릭터 퇴장 — 순수 페이드 (기본)
        /// </summary>
        public async UniTask ExitAsync(CancellationToken ct = default)
        {
            if (IsEmpty) return;

            string exitingCharacter = currentCharacter;

            if (slotCanvasGroup != null)
                await slotCanvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InCubic).ToUniTask(cancellationToken: ct);
            else
                SetSlotAlpha(0f);

            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition;

            EnableImages(false);
            ResetCharacterTransform();
            currentCharacter = null;
            currentEmote = null;

            AudioManager.Instance?.OnCharacterExit(exitingCharacter);
        }

        /// <summary>
        /// 캐릭터 퇴장 — 아래로 슬라이드 + 페이드
        /// CSV: ,Char,,슬롯:ExitDown,await
        /// </summary>
        public async UniTask ExitSlideDownAsync(CancellationToken ct = default)
        {
            if (IsEmpty) return;

            string exitingCharacter = currentCharacter;

            var sequence = DOTween.Sequence();
            if (rectTransform != null)
                _ = sequence.Join(rectTransform.DOAnchorPos(
                    originalPosition + new Vector2(0, -exitSlideDistance), exitDuration)
                    .SetEase(Ease.InCubic));
            if (slotCanvasGroup != null)
                _ = sequence.Join(slotCanvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InQuad));

            await sequence.ToUniTask(cancellationToken: ct);

            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition;

            EnableImages(false);
            ResetCharacterTransform();
            currentCharacter = null;
            currentEmote = null;

            AudioManager.Instance?.OnCharacterExit(exitingCharacter);
        }

        /// <summary>
        /// 즉시 숨김 (애니메이션 없이)
        /// </summary>
        public void Clear()
        {
            DOTween.Kill(slotCanvasGroup);
            DOTween.Kill(frontCanvasGroup);
            DOTween.Kill(backCanvasGroup);
            DOTween.Kill(rectTransform);
            RectTransform target = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            DOTween.Kill(target);

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

        /// <summary>
        /// 스프라이트 로드 (캐시 지원, Default 폴백)
        /// 경로: Characters/{character}/{emote}
        /// </summary>
        Sprite LoadSprite(string character, string emote)
        {
            string path = $"Characters/{character}/{emote}";

            if (spriteCache.TryGetValue(path, out var cached))
            {
                TouchCache(path);
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);

            // Default로 폴백
            if (sprite == null && emote != "Default")
            {
                string fallback = $"Characters/{character}/Default";
                if (spriteCache.TryGetValue(fallback, out cached))
                {
                    TouchCache(fallback);
                    return cached;
                }

                sprite = Resources.Load<Sprite>(fallback);
                if (sprite != null)
                    AddToCache(fallback, sprite);

                return sprite;
            }

            if (sprite != null)
                AddToCache(path, sprite);

            return sprite;
        }

        /// <summary>
        /// 캐시에 항목 추가 (MaxCacheSize 초과 시 LRU 제거)
        /// </summary>
        static void AddToCache(string key, Sprite sprite)
        {
            if (spriteCache.ContainsKey(key))
            {
                TouchCache(key);
                return;
            }

            while (spriteCache.Count >= MaxCacheSize && cacheOrder.Count > 0)
            {
                string oldest = cacheOrder.First.Value;
                cacheOrder.RemoveFirst();
                spriteCache.Remove(oldest);
            }

            spriteCache[key] = sprite;
            cacheOrder.AddLast(key);
        }

        /// <summary>
        /// LRU 순서 갱신 (최근 사용으로 이동)
        /// </summary>
        static void TouchCache(string key)
        {
            cacheOrder.Remove(key);
            cacheOrder.AddLast(key);
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

        #region 캐릭터 FX

        /// <summary>
        /// 캐릭터 흔들기 효과 (FX,,CharShake:슬롯:강도:시간)
        /// </summary>
        public async UniTask ShakeAsync(float strength = 20f, float duration = 0.4f, CancellationToken ct = default)
        {
            RectTransform target = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            if (target == null) return;

            var saved = target.anchoredPosition;
            await target.DOShakeAnchorPos(duration, strength, vibrato: 14, randomness: 60f)
                .SetEase(Ease.OutCubic)
                .ToUniTask(cancellationToken: ct);
            target.anchoredPosition = saved;
        }

        /// <summary>
        /// 캐릭터 점프 효과 (FX,,CharJump:슬롯:높이:시간)
        /// </summary>
        public async UniTask JumpAsync(float height = 40f, float duration = 0.3f, CancellationToken ct = default)
        {
            RectTransform target = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            if (target == null) return;

            var saved = target.anchoredPosition;
            var seq = DOTween.Sequence();
            _ = seq.Append(target.DOAnchorPosY(saved.y + height, duration * 0.4f).SetEase(Ease.OutQuad));
            _ = seq.Append(target.DOAnchorPosY(saved.y, duration * 0.6f).SetEase(Ease.OutBounce));
            await seq.ToUniTask(cancellationToken: ct);
            target.anchoredPosition = saved;
        }

        /// <summary>
        /// 캐릭터 어둡게 (FX,,CharDim:슬롯:알파:시간)
        /// </summary>
        public async UniTask DimAsync(float targetAlpha = 0.4f, float duration = 0.3f, CancellationToken ct = default)
        {
            if (slotCanvasGroup == null) return;
            await slotCanvasGroup.DOFade(targetAlpha, duration)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: ct);
        }

        void OnDestroy()
        {
            DOTween.Kill(slotCanvasGroup);
            DOTween.Kill(frontCanvasGroup);
            DOTween.Kill(backCanvasGroup);
            DOTween.Kill(rectTransform);
            RectTransform target = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            DOTween.Kill(target);
        }

        #endregion
    }
}
