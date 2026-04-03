using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Core;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 레이어 - 3개 슬롯(L/C/R) 관리
    /// SD 컷씬 표시 시 레이어 전체를 페이드아웃하고, SD 종료 시 복원
    /// </summary>
    public class CharacterLayer : MonoBehaviour
    {
        [Header("슬롯 바인딩")]
        [SerializeField] CharacterSlot slotL;
        [SerializeField] CharacterSlot slotC;
        [SerializeField] CharacterSlot slotR;



        [Header("레이어 가시성")]
        [SerializeField] CanvasGroup layerCanvasGroup;  // 레이어 전체 페이드용
        [SerializeField] float layerFadeDuration = 0.3f;

        Dictionary<SlotPosition, CharacterSlot> slots;
        bool isLayerHidden;  // SD 등에 의해 레이어가 숨겨진 상태

        /// <summary>레이어가 SD 등에 의해 숨겨진 상태인지</summary>
        public bool IsLayerHidden => isLayerHidden;

        void Awake()
        {
            slots = new Dictionary<SlotPosition, CharacterSlot>
            {
                { SlotPosition.L, slotL },
                { SlotPosition.C, slotC },
                { SlotPosition.R, slotR }
            };

            // CanvasGroup 자동 바인딩
            if (layerCanvasGroup == null)
                layerCanvasGroup = GetComponent<CanvasGroup>();
            if (layerCanvasGroup == null)
                layerCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// 레이어 전체 페이드 표시/숨김 (SD 컷씬 등에서 사용)
        /// </summary>
        public async UniTask SetVisibleAsync(bool visible, CancellationToken ct = default)
        {
            if (layerCanvasGroup == null) return;

            isLayerHidden = !visible;
            float target = visible ? 1f : 0f;

            if (layerFadeDuration > 0f)
            {
                await layerCanvasGroup.DOFade(target, layerFadeDuration)
                    .SetEase(visible ? Ease.OutCubic : Ease.InCubic)
                    .ToUniTask(cancellationToken: ct);
            }
            else
            {
                layerCanvasGroup.alpha = target;
            }
        }

        /// <summary>
        /// 레이어 가시성 즉시 설정 (로드/초기화 시)
        /// </summary>
        public void SetVisibleImmediate(bool visible)
        {
            isLayerHidden = !visible;
            if (layerCanvasGroup != null)
                layerCanvasGroup.alpha = visible ? 1f : 0f;
        }

        /// <summary>
        /// Char 명령 실행
        /// Value 형식: 슬롯:액션:대상[:옵션[:오버레이]]
        /// 오버레이는 로아 등장/표정 변경 시 5번째(Enter) 또는 4번째(Emote) 파라미터로 지정
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            if (parts.Length < 2)
            {
                Debug.LogWarning($"[CharacterLayer] 잘못된 형식: {value}");
                return;
            }

            // 슬롯 파싱
            if (!TryParseSlot(parts[0], out SlotPosition slotPos))
            {
                Debug.LogWarning($"[CharacterLayer] 알 수 없는 슬롯: {parts[0]}");
                return;
            }

            var slot = GetSlot(slotPos);
            if (slot == null)
            {
                Debug.LogWarning($"[CharacterLayer] 슬롯이 바인딩되지 않음: {slotPos}");
                return;
            }

            // 액션 파싱 (대소문자 무시)
            string action = parts[1];

            switch (action.ToLowerInvariant())
            {
                case "enter":
                    // 형식: 슬롯:Enter:캐릭터[:표정[:오버레이]]  — 페이드 (기본)
                    if (parts.Length >= 3)
                    {
                        string character = parts[2];
                        string emote = parts.Length >= 4 ? parts[3] : "Default";
                        string overlay = parts.Length >= 5 ? parts[4] : null;

                        // 로아: 글리치 SFX 즉시 + 캐릭터&오버레이 동시 페이드
                        if (IsOverlayCharacter(character))
                            PlayGlitchSFX();

                        if (!string.IsNullOrEmpty(overlay))
                        {
                            await UniTask.WhenAll(
                                slot.EnterAsync(character, emote, ct),
                                ShowOverlayAsync(overlay, ct)
                            );
                        }
                        else
                        {
                            await slot.EnterAsync(character, emote, ct);
                        }
                    }
                    break;

                case "enterup":
                    // 형식: 슬롯:EnterUp:캐릭터[:표정[:오버레이]]  — 아래에서 위로 슬라이드 + 페이드
                    if (parts.Length >= 3)
                    {
                        string character = parts[2];
                        string emote = parts.Length >= 4 ? parts[3] : "Default";
                        string overlay = parts.Length >= 5 ? parts[4] : null;

                        if (IsOverlayCharacter(character))
                            PlayGlitchSFX();

                        if (!string.IsNullOrEmpty(overlay))
                        {
                            await UniTask.WhenAll(
                                slot.EnterSlideUpAsync(character, emote, ct),
                                ShowOverlayAsync(overlay, ct)
                            );
                        }
                        else
                        {
                            await slot.EnterSlideUpAsync(character, emote, ct);
                        }
                    }
                    break;

                case "emote":
                    // 형식: 슬롯:Emote:표정[:오버레이]
                    if (parts.Length >= 3)
                    {
                        string emote = parts[2];
                        string overlay = parts.Length >= 4 ? parts[3] : null;

                        // 표정 + 오버레이 동시 전환
                        if (!string.IsNullOrEmpty(overlay))
                        {
                            await UniTask.WhenAll(
                                slot.EmoteAsync(emote, ct),
                                ShowOverlayAsync(overlay, ct)
                            );
                        }
                        else
                        {
                            await slot.EmoteAsync(emote, ct);
                        }
                    }
                    break;

                case "exit":
                    // 형식: 슬롯:Exit  — 페이드 (기본)
                    {
                        bool shouldHideOverlay = ShouldHideOverlayOnExit(slot);

                        // 로아: 글리치 SFX 즉시 + 캐릭터&오버레이 동시 페이드아웃
                        if (shouldHideOverlay)
                        {
                            PlayGlitchSFX();
                            await UniTask.WhenAll(
                                slot.ExitAsync(ct),
                                HideOverlayAsync(ct)
                            );
                        }
                        else
                        {
                            await slot.ExitAsync(ct);
                        }
                    }
                    break;

                case "exitdown":
                    // 형식: 슬롯:ExitDown  — 아래로 슬라이드 + 페이드
                    {
                        bool shouldHideOverlay = ShouldHideOverlayOnExit(slot);

                        if (shouldHideOverlay)
                        {
                            PlayGlitchSFX();
                            await UniTask.WhenAll(
                                slot.ExitSlideDownAsync(ct),
                                HideOverlayAsync(ct)
                            );
                        }
                        else
                        {
                            await slot.ExitSlideDownAsync(ct);
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"[CharacterLayer] 알 수 없는 액션: {action}");
                    break;
            }
        }

        /// <summary>
        /// 슬롯 가져오기
        /// </summary>
        public CharacterSlot GetSlot(SlotPosition position)
        {
            return slots.TryGetValue(position, out var slot) ? slot : null;
        }

        /// <summary>
        /// 모든 캐릭터 퇴장
        /// </summary>
        public async UniTask ExitAllAsync(CancellationToken ct = default)
        {
            var tasks = new List<UniTask>();
            bool shouldHideOverlay = false;

            foreach (var slot in slots.Values)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    shouldHideOverlay |= IsOverlayCharacter(slot.CurrentCharacter);
                    tasks.Add(slot.ExitAsync(ct));
                }
            }

            if (shouldHideOverlay)
            {
                PlayGlitchSFX();
                tasks.Add(HideOverlayAsync(ct));
            }

            await UniTask.WhenAll(tasks);
            
            // AudioManager에 모든 캐릭터 퇴장 알림
            AudioManager.Instance?.OnAllCharactersExit();
        }

        /// <summary>
        /// 모든 슬롯 즉시 클리어
        /// </summary>
        public void ClearAll()
        {
            bool shouldHideOverlay = false;

            foreach (var slot in slots.Values)
            {
                shouldHideOverlay |= slot != null && IsOverlayCharacter(slot.CurrentCharacter);
                slot?.Clear();
            }

            if (shouldHideOverlay)
            {
                var overlay = StageManager.Instance?.VirtualBG;
                overlay?.HideImmediate();
            }

            AudioManager.Instance?.OnAllCharactersExit();
        }

        /// <summary>
        /// Speaker 이름으로 캐릭터 자동 등장 (아직 없으면)
        /// </summary>
        public async UniTask AutoEnterBySpeakerAsync(string speaker, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(speaker)) return;
            
            // Speaker → CharacterID 매핑
            string characterId = SpeakerToCharacterId(speaker);
            if (string.IsNullOrEmpty(characterId)) return;
            
            // 이미 해당 캐릭터가 화면에 있는지 확인
            if (IsCharacterOnStage(characterId)) return;
            
            // 기본 슬롯(C)에 등장
            var slot = GetSlot(SlotPosition.C);
            if (slot != null)
            {
                await slot.EnterAsync(characterId, "Default", ct);
            }
        }
        
        /// <summary>
        /// 캐릭터가 화면에 있는지 확인
        /// </summary>
        public bool IsCharacterOnStage(string characterId)
        {
            foreach (var slot in slots.Values)
            {
                if (slot != null && !slot.IsEmpty && 
                    string.Equals(slot.CurrentCharacter, characterId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Speaker 이름 → CharacterID 변환
        /// </summary>
        string SpeakerToCharacterId(string speaker)
        {
            // CharacterDatabase에서 매핑 조회
            var charDb = CharacterDatabase.Instance;
            if (charDb != null)
            {
                string id = charDb.SpeakerToCharacterId(speaker);
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
            
            // CharacterDatabase에 매핑이 없으면 null 반환 (주인공, 나레이션, 엑스트라 등)
            return null;
        }

        /// <summary>
        /// 표정 변경 (인라인 태그용)
        /// </summary>
        public void ChangeEmote(string slotStr, string emote)
        {
            if (TryParseSlot(slotStr, out SlotPosition pos))
            {
                var slot = GetSlot(pos);
                if (slot != null && !slot.IsEmpty)
                {
                    slot.EmoteAsync(emote).Forget();
                }
            }
        }

        /// <summary>
        /// 슬롯 문자열 파싱
        /// </summary>
        bool TryParseSlot(string str, out SlotPosition position)
        {
            position = SlotPosition.C;

            switch (str.ToUpper())
            {
                case "L":
                    position = SlotPosition.L;
                    return true;
                case "C":
                    position = SlotPosition.C;
                    return true;
                case "R":
                    position = SlotPosition.R;
                    return true;
                default:
                    return false;
            }
        }

        #region 오버레이 (로아)

        /// <summary>
        /// 오버레이 연동이 필요한 캐릭터인지 확인 (CharacterDatabase에서 판별)
        /// </summary>
        bool IsOverlayCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return false;
            var characterDatabase = CharacterDatabase.Instance;
            if (characterDatabase != null)
            {
                var data = characterDatabase.GetCharacterById(characterName);
                return data != null && data.UseOverlay;
            }
            // DB 없으면 기존 하드코드 폴백
            return characterName.Equals("Roa", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 오버레이 표시 (VirtualBGOverlay 연동)
        /// </summary>
        async UniTask ShowOverlayAsync(string overlayName, CancellationToken ct)
        {
            var overlay = StageManager.Instance?.VirtualBG;
            if (overlay != null)
            {
                await overlay.ShowAsync(overlayName, ct: ct);
                Debug.Log($"[CharacterLayer] 오버레이 표시: {overlayName}");
            }
        }

        /// <summary>
        /// 오버레이 숨김 (VirtualBGOverlay 연동)
        /// </summary>
        async UniTask HideOverlayAsync(CancellationToken ct)
        {
            var overlay = StageManager.Instance?.VirtualBG;
            if (overlay != null && overlay.IsShowing)
            {
                await overlay.HideAsync(ct: ct);
                Debug.Log("[CharacterLayer] 오버레이 자동 숨김 (로아 퇴장)");
            }
        }

        bool ShouldHideOverlayOnExit(CharacterSlot slot)
        {
            var overlay = StageManager.Instance?.VirtualBG;
            if (overlay == null || !overlay.IsShowing)
                return false;

            if (slot == null)
                return true;

            if (IsOverlayCharacter(slot.CurrentCharacter))
                return true;

            // 슬롯의 캐릭터명이 비어 있어도 현재 오버레이가 남아 있으면 같이 정리한다.
            return string.IsNullOrEmpty(slot.CurrentCharacter);
        }

        /// <summary>
        /// 오버레이 효과음 재생 (오버레이 캐릭터 등장/퇴장 시 자동)
        /// </summary>
        static void PlayGlitchSFX()
        {
            AudioManager.Instance?.PlaySFX("Glitch");
        }

        #endregion

        void OnDestroy()
        {
            DOTween.Kill(layerCanvasGroup);
        }

        #region 캐릭터 FX

        /// <summary>
        /// 캐릭터 시각 효과 실행
        /// CSV: FX,,CharShake[:슬롯:강도:시간], FX,,CharJump[:슬롯:높이:시간], FX,,CharDim[:슬롯:알파:시간]
        /// 슬롯 생략 시 화면의 첫 번째 활성 캐릭터에 적용
        /// </summary>
        public async UniTask ExecuteCharFXAsync(string effect, string[] parts, CancellationToken ct = default)
        {
            // 슬롯 결정: parts[1]이 있으면 사용, 없으면 첫 활성 슬롯
            CharacterSlot slot = null;
            int paramOffset = 1; // 파라미터 시작 인덱스

            if (parts.Length > 1 && TryParseSlot(parts[1], out SlotPosition pos))
            {
                slot = GetSlot(pos);
                paramOffset = 2;
            }
            else
            {
                slot = FindFirstActiveSlot();
            }

            if (slot == null || slot.IsEmpty)
            {
                Debug.LogWarning($"[CharacterLayer] {effect}: 대상 캐릭터 없음");
                return;
            }

            switch (effect)
            {
                case "CharShake":
                {
                    float strength = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float s) ? s : 15f;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : 0.3f;
                    await slot.ShakeAsync(strength, duration, ct);
                    break;
                }
                case "CharJump":
                {
                    float height = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float h) ? h : 30f;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : 0.3f;
                    await slot.JumpAsync(height, duration, ct);
                    break;
                }
                case "CharDim":
                {
                    float alpha = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float a) ? a : 0.4f;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : 0.3f;
                    await slot.DimAsync(alpha, duration, ct);
                    break;
                }
            }
        }

        /// <summary>
        /// 첫 번째 활성 캐릭터 슬롯 찾기 (C → L → R 우선순위)
        /// </summary>
        CharacterSlot FindFirstActiveSlot()
        {
            if (slotC != null && !slotC.IsEmpty) return slotC;
            if (slotL != null && !slotL.IsEmpty) return slotL;
            if (slotR != null && !slotR.IsEmpty) return slotR;
            return null;
        }

        #endregion
    }
}
