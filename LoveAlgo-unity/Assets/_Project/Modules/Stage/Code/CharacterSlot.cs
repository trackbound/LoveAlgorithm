using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.Core;
using LoveAlgo.Modules.Audio;
using LoveAlgo.Stage;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    // C4-Phase B-8c-2: SlotPosition 은 LoveAlgo.Contracts 로 이동 (ICharacterLayer.GetSlot 매개변수).

    /// <summary>
    /// 개별 캐릭터 슬롯 (이미지 2개로 크로스페이드 지원)
    /// </summary>
    public class CharacterSlot : MonoBehaviour, ICharacterSlot
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
        [SerializeField] float fadeDuration = 0.5f;
        [SerializeField] float exitDuration = 0.4f;
        [SerializeField] float emoteFadeDuration = 0.25f;
        [SerializeField] float enterOffset = 40f;        // 등장 시 슬라이드 거리
        [SerializeField] float exitSlideDistance = 40f;  // 퇴장 시 슬라이드 거리
        [Tooltip("등장 시 미세 스케일 펀치 시작값 (1.0 = 비활성)")]
        [SerializeField] float enterScalePunch = 0.97f;

        [Header("Glitch FX")]
        [Tooltip("LoveAlgo/UI/Glitch 셰이더 사용 머티리얼. 비워두면 Shader.Find로 동적 생성")]
        [SerializeField] Material glitchMaterial;

        Material runtimeGlitchMat;       // 인스턴스 (셰이더 동적 생성 시)
        Material savedFrontMaterial;     // GlitchAsync 시작 시 백업



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

            // 원래 위치에서 페이드인 + 미세 스케일 펀치 (생기 부여)
            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition;
            SetSlotAlpha(0f);

            RectTransform punchTarget = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            Vector3 savedScale = punchTarget != null ? punchTarget.localScale : Vector3.one;
            if (punchTarget != null && enterScalePunch < 1f)
                punchTarget.localScale = savedScale * enterScalePunch;

            var seq = DOTween.Sequence();
            if (slotCanvasGroup != null)
                _ = seq.Join(slotCanvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuart));
            else
                SetSlotAlpha(1f);

            if (punchTarget != null && enterScalePunch < 1f)
                _ = seq.Join(punchTarget.DOScale(savedScale, fadeDuration).SetEase(Ease.OutCubic));

            await seq.ToUniTask(cancellationToken: ct);

            EventBus.Publish(new CharacterEnteredEvent(characterName));
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
                _ = sequence.Join(rectTransform.DOAnchorPos(originalPosition, fadeDuration).SetEase(Ease.OutQuart));
            if (slotCanvasGroup != null)
                _ = sequence.Join(slotCanvasGroup.DOFade(1f, fadeDuration * 0.75f).SetEase(Ease.OutCubic));

            await sequence.ToUniTask(cancellationToken: ct);

            EventBus.Publish(new CharacterEnteredEvent(characterName));
        }

        /// <summary>
        /// 등장 공통 준비 (스프라이트 로드 + 이미지 세팅 + 트랜스폼 적용)
        /// </summary>
        bool PrepareEnter(string characterName, string resolvedEmote)
        {
            var sprite = LoadSprite(characterName, resolvedEmote);
            if (sprite == null)
            {
                var c = StoryMappings.SpeakerToCharacterId(characterName) ?? characterName;
                var e = StoryMappings.ResolveEmote(resolvedEmote);
                Debug.LogWarning($"[CharacterSlot] 스프라이트 없음: {characterName}/{resolvedEmote} → Resources/Characters/{c}_{e}");
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
                var c = StoryMappings.SpeakerToCharacterId(currentCharacter) ?? currentCharacter;
                var e = StoryMappings.ResolveEmote(currentEmote);
                Debug.LogWarning($"[CharacterSlot] 스프라이트 없음: {currentCharacter}/{currentEmote} → Resources/Characters/{c}_{e}");
                return;
            }

            // Back 이미지에 새 표정 설정 (뒤에서 준비)
            imageBack.sprite = sprite;
            imageBack.enabled = true;
            backCanvasGroup.alpha = 0f;
            
            // 크로스페이드: Front 위에 Back을 페이드인 (Front는 유지)
            // Front를 페이드아웃하면 Back이 아직 불투명하지 않을 때 빈 곳이 보여 깜빡임 발생
            // → Back만 올려서 자연스럽게 덮기
            // InOutCubic — Sine보다 곡률이 풍부해 표정 전환이 더 자연스러움
            await backCanvasGroup.DOFade(1f, emoteFadeDuration)
                .SetEase(Ease.InOutCubic)
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
                await slotCanvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InQuart).ToUniTask(cancellationToken: ct);
            else
                SetSlotAlpha(0f);

            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition;

            EnableImages(false);
            ResetCharacterTransform();
            currentCharacter = null;
            currentEmote = null;

            EventBus.Publish(new CharacterExitedEvent(exitingCharacter));
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
                _ = sequence.Join(slotCanvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InCubic));

            await sequence.ToUniTask(cancellationToken: ct);

            if (rectTransform != null)
                rectTransform.anchoredPosition = originalPosition;

            EnableImages(false);
            ResetCharacterTransform();
            currentCharacter = null;
            currentEmote = null;

            EventBus.Publish(new CharacterExitedEvent(exitingCharacter));
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
        /// 스프라이트 로드 (캐시 지원, _00 폴백)
        /// 경로: Characters/{characterId}_{emoteId}  (예: c01_00)
        /// </summary>
        Sprite LoadSprite(string character, string emote)
        {
            // displayName/alias(예: 로아, Roa) → characterId(c01)
            var resolvedChar = StoryMappings.SpeakerToCharacterId(character) ?? character;

            // 한글 표정명 → 영문/ID 변환 (예: 기본 → _00, Default → _00)
            var resolvedEmote = StoryMappings.ResolveEmote(emote);

            // emote ID는 "_00" 처럼 이미 underscore prefix를 포함 → 구분자 없이 합성 (예: c01_00)
            string path = $"Characters/{resolvedChar}{resolvedEmote}";

            if (spriteCache.TryGetValue(path, out var cached))
            {
                TouchCache(path);
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);

            // _00(기본)으로 폴백
            if (sprite == null && resolvedEmote != "_00")
            {
                string fallback = $"Characters/{resolvedChar}_00";
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

            // Stage 시각 표현 DB — DB는 characterId(c01~c05) 기준이라 alias/displayName 정규화 필수
            var stageDb = StageModule.Instance?.CharacterStage;
            if (stageDb != null)
            {
                var id = StoryMappings.SpeakerToCharacterId(characterName) ?? characterName;
                var entry = stageDb.GetById(id);
                if (entry != null)
                {
                    entry.GetTransform(out scale, out offsetX, out offsetY, out pivotY);
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
        public async UniTask ShakeAsync(float strength = -1f, float duration = -1f, CancellationToken ct = default)
        {
            RectTransform target = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            if (target == null) return;

            // SO 기본값 사용 (음수 시) — CharLayer와 통일
            var cfg = FXDefaultsConfig.Instance;
            if (strength < 0f) strength = cfg != null ? cfg.charShakeStrength : 18f;
            if (duration < 0f) duration = cfg != null ? cfg.charShakeDuration : 0.3f;

            var saved = target.anchoredPosition;
            DOTween.Kill(target);
            // vibrato 살짝 낮춤 + randomness 상향 + fadeOut=true → 끝에서 자연 감쇠
            try
            {
                await target.DOShakeAnchorPos(duration, strength, vibrato: 11, randomness: 85f, fadeOut: true)
                    .SetEase(Ease.OutCubic)
                    .ToUniTask(cancellationToken: ct);
            }
            finally
            {
                if (target != null) target.anchoredPosition = saved;
            }
        }

        /// <summary>
        /// 캐릭터 점프 효과 (FX,,CharJump:슬롯:높이:시간)
        /// </summary>
        public async UniTask JumpAsync(float height = -1f, float duration = -1f, CancellationToken ct = default)
        {
            RectTransform target = imageContainer != null ? imageContainer : imageFront?.rectTransform;
            if (target == null) return;

            var cfg = FXDefaultsConfig.Instance;
            if (height < 0f)   height   = cfg != null ? cfg.charJumpHeight   : 35f;
            if (duration < 0f) duration = cfg != null ? cfg.charJumpDuration : 0.3f;

            var saved = target.anchoredPosition;
            DOTween.Kill(target);

            // 상승: OutCubic — 빠르게 솟구쳤다 정점에서 부드럽게 멈춤
            // 하강: OutBounce — 착지 시 자연스러운 반동
            var seq = DOTween.Sequence();
            _ = seq.Append(target.DOAnchorPosY(saved.y + height, duration * 0.4f).SetEase(Ease.OutCubic));
            _ = seq.Append(target.DOAnchorPosY(saved.y, duration * 0.6f).SetEase(Ease.OutBounce));
            try
            {
                await seq.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                if (target != null) target.anchoredPosition = saved;
            }
        }

        /// <summary>
        /// 캐릭터 글리치 효과 (FX,,CharGlitch:슬롯:강도:시간).
        /// 강도 0 → peak → 0 트윈으로 한 사이클 재생.
        /// </summary>
        public async UniTask GlitchAsync(float peakStrength = -1f, float duration = -1f, CancellationToken ct = default)
        {
            if (imageFront == null) return;

            var cfg = FXDefaultsConfig.Instance;
            if (peakStrength < 0f) peakStrength = cfg != null ? cfg.charGlitchStrength : 1.0f;
            if (duration < 0f)     duration     = cfg != null ? cfg.charGlitchDuration : 0.6f;

            var mat = EnsureGlitchMaterial();
            if (mat == null)
            {
                Debug.LogWarning("[CharacterSlot] LoveAlgo/UI/Glitch 셰이더를 찾을 수 없습니다. 글리치 스킵.");
                return;
            }

            savedFrontMaterial = imageFront.material;
            imageFront.material = mat;
            mat.SetFloat("_Strength", 0f);

            try
            {
                // 비대칭 분배: 짧고 폭발적인 상승 + 길고 자연스러운 감쇠
                float riseDur = duration * 0.35f;
                float fallDur = duration * 0.65f;

                // 0 → peak (날카로운 충격감)
                await DOTween.To(() => mat.GetFloat("_Strength"),
                                 v => mat.SetFloat("_Strength", v),
                                 peakStrength, riseDur)
                    .SetEase(Ease.OutExpo)
                    .ToUniTask(cancellationToken: ct);

                // peak → 0 (자연 감쇠)
                await DOTween.To(() => mat.GetFloat("_Strength"),
                                 v => mat.SetFloat("_Strength", v),
                                 0f, fallDur)
                    .SetEase(Ease.InOutQuart)
                    .ToUniTask(cancellationToken: ct);
            }
            finally
            {
                mat.SetFloat("_Strength", 0f);
                if (imageFront != null) imageFront.material = savedFrontMaterial;
            }
        }

        Material EnsureGlitchMaterial()
        {
            if (glitchMaterial != null) return glitchMaterial;
            if (runtimeGlitchMat != null) return runtimeGlitchMat;
            var shader = Shader.Find("LoveAlgo/UI/Glitch");
            if (shader == null) return null;
            runtimeGlitchMat = new Material(shader) { name = "UIGlitch (runtime)" };
            return runtimeGlitchMat;
        }

        /// <summary>
        /// 캐릭터 어둡게 (FX,,CharDim:슬롯:알파:시간)
        /// </summary>
        public async UniTask DimAsync(float targetAlpha = -1f, float duration = -1f, CancellationToken ct = default)
        {
            if (slotCanvasGroup == null) return;

            var cfg = FXDefaultsConfig.Instance;
            if (targetAlpha < 0f) targetAlpha = cfg != null ? cfg.charDimAlpha    : 0.4f;
            if (duration < 0f)    duration    = cfg != null ? cfg.charDimDuration : 0.3f;

            DOTween.Kill(slotCanvasGroup);
            await slotCanvasGroup.DOFade(targetAlpha, duration)
                .SetEase(Ease.InOutCubic)
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
            if (runtimeGlitchMat != null) Destroy(runtimeGlitchMat);
        }

        #endregion
    }
}
