using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Core;
using UnityEngine;
using LoveAlgo.Modules.Audio;

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

        // VirtualOverlay 모드 추적 (characterId → 현재 모드, 예: "Roa"→"Mob")
        readonly Dictionary<string, string> overlayModes = new(StringComparer.OrdinalIgnoreCase);

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
                    // 형식: 슬롯:Enter:캐릭터[:표정[:모드 또는 오버레이명]]
                    // - 5번째가 overlayModes에 등록된 모드(Mob/PC)면 모드로 사용
                    // - 그 외에는 명시적 오버레이 이름으로 사용 (구버전 호환)
                    if (parts.Length >= 3)
                    {
                        string character = parts[2];
                        string emote = parts.Length >= 4 ? parts[3] : "Default";
                        string fifth = parts.Length >= 5 ? parts[4] : null;
                        string overlay = ResolveEnterOverlay(character, emote, fifth);

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
                    // 형식: 슬롯:EnterUp:캐릭터[:표정[:모드 또는 오버레이명]]
                    if (parts.Length >= 3)
                    {
                        string character = parts[2];
                        string emote = parts.Length >= 4 ? parts[3] : "Default";
                        string fifth = parts.Length >= 5 ? parts[4] : null;
                        string overlay = ResolveEnterOverlay(character, emote, fifth);

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
                    // 형식: 슬롯:Emote:표정[:오버레이명]  — 모드는 현재 추적값 유지
                    if (parts.Length >= 3)
                    {
                        string emote = parts[2];
                        string overlay = parts.Length >= 4 ? parts[3] : null;

                        if (string.IsNullOrEmpty(overlay))
                            overlay = ResolveAutoOverlay(slot.CurrentCharacter, emote, GetCurrentMode(slot.CurrentCharacter));

                        // 표정 + 오버레이 동시 전환
                        if (!string.IsNullOrEmpty(overlay))
                        {
                            await UniTask.WhenAll(
                                slot.EmoteAsync(emote, ct),
                                SwitchOverlayAsync(overlay, ct)
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
                        string exitingChar = slot.CurrentCharacter;

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
                        if (!string.IsNullOrEmpty(exitingChar)) overlayModes.Remove(exitingChar);
                    }
                    break;

                case "mode":
                    // 형식: 슬롯:Mode:캐릭터:새모드  — VirtualOverlay 모드만 전환 (캐릭터 슬롯 유지)
                    if (parts.Length >= 4)
                    {
                        string character = parts[2];
                        string newMode = parts[3];
                        SetMode(character, newMode);
                        string overlay = ResolveAutoOverlay(slot.CurrentCharacter, slot.CurrentEmote, newMode);
                        if (!string.IsNullOrEmpty(overlay))
                            await SwitchOverlayAsync(overlay, ct);
                    }
                    break;

                case "exitdown":
                    // 형식: 슬롯:ExitDown  — 아래로 슬라이드 + 페이드
                    {
                        bool shouldHideOverlay = ShouldHideOverlayOnExit(slot);
                        string exitingChar = slot.CurrentCharacter;

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
                        if (!string.IsNullOrEmpty(exitingChar)) overlayModes.Remove(exitingChar);
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

            overlayModes.Clear();
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

            overlayModes.Clear();
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
            var meta = CharacterMetaDatabase.Instance;
            return meta?.SpeakerToCharacterId(speaker);
        }

        /// <summary>
        /// 표정 변경 (인라인 태그용) — 오버레이 캐릭터면 오버레이도 자동 전환
        /// </summary>
        public void ChangeEmote(string slotStr, string emote)
        {
            if (TryParseSlot(slotStr, out SlotPosition pos))
            {
                var slot = GetSlot(pos);
                if (slot != null && !slot.IsEmpty)
                {
                    ChangeEmoteWithOverlayAsync(slot, emote).Forget();
                }
            }
        }

        async UniTaskVoid ChangeEmoteWithOverlayAsync(CharacterSlot slot, string emote)
        {
            string overlay = ResolveAutoOverlay(slot.CurrentCharacter, emote, GetCurrentMode(slot.CurrentCharacter));

            if (!string.IsNullOrEmpty(overlay))
            {
                await UniTask.WhenAll(
                    slot.EmoteAsync(emote),
                    SwitchOverlayAsync(overlay, default)
                );
            }
            else
            {
                await slot.EmoteAsync(emote);
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

        /// <summary>Overlay 사용 캐릭터인지 (VirtualOverlayDatabase에 entry가 있으면 true)</summary>
        bool IsOverlayCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return false;
            var ovDb = VirtualOverlayDatabase.Instance;
            return ovDb?.GetById(characterName) != null;
        }

        /// <summary>
        /// 표정 + 모드 → overlay 이름 자동 결정. 오버레이 캐릭터가 아니면 null.
        /// </summary>
        string ResolveAutoOverlay(string characterId, string emote, string mode = null)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            var entry = VirtualOverlayDatabase.Instance?.GetById(characterId);
            return entry?.GetOverlayName(emote, mode);
        }

        /// <summary>Enter 5번째 segment 해석 — overlayModes 등록 모드면 모드, 그 외엔 명시적 overlay 이름.</summary>
        string ResolveEnterOverlay(string characterId, string emote, string fifthSegment)
        {
            if (!string.IsNullOrEmpty(fifthSegment))
            {
                var entry = VirtualOverlayDatabase.Instance?.GetById(characterId);
                if (entry != null && entry.IsValidMode(fifthSegment))
                {
                    SetMode(characterId, fifthSegment);
                    return entry.GetOverlayName(emote, fifthSegment);
                }
                // overlayModes에 없는 값이면 명시적 overlay 이름으로 사용 (구버전 호환)
                return fifthSegment;
            }
            return ResolveAutoOverlay(characterId, emote, GetCurrentMode(characterId));
        }

        /// <summary>현재 추적중 모드 조회 (없으면 VirtualOverlayDatabase의 defaultOverlayMode)</summary>
        string GetCurrentMode(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (overlayModes.TryGetValue(characterId, out var m)) return m;
            var entry = VirtualOverlayDatabase.Instance?.GetById(characterId);
            return entry?.defaultOverlayMode;
        }

        /// <summary>모드 설정 (Enter/Mode action에서 호출)</summary>
        void SetMode(string characterId, string mode)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(mode)) return;
            overlayModes[characterId] = mode;
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

        /// <summary>
        /// 오버레이 전환 (VirtualBGOverlay.SwitchAsync 연동 — 표정 변경 시)
        /// </summary>
        async UniTask SwitchOverlayAsync(string overlayName, CancellationToken ct)
        {
            var overlay = StageManager.Instance?.VirtualBG;
            if (overlay != null)
            {
                await overlay.SwitchAsync(overlayName, ct: ct);
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
            // [진단 로그] BGM과 동시 발생 시 찌직거림 추적용
            Debug.Log($"[CharacterLayer][GlitchSFX] t={Time.time:F2}");
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
        /// CSV: FX,,CharShake[:슬롯:강도:시간], FX,,CharJump[:슬롯:높이:시간], FX,,CharDim[:슬롯:알파:시간], FX,,CharGlitch[:슬롯:강도:시간]
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

            // 기본값은 FXDefaultsConfig (SO)에서 — CharLayer/CharSlot 간 불일치 제거
            var cfg = FXDefaultsConfig.Instance;
            float defShakeStr   = cfg != null ? cfg.charShakeStrength  : 18f;
            float defShakeDur   = cfg != null ? cfg.charShakeDuration  : 0.3f;
            float defJumpH      = cfg != null ? cfg.charJumpHeight     : 35f;
            float defJumpDur    = cfg != null ? cfg.charJumpDuration   : 0.3f;
            float defDimAlpha   = cfg != null ? cfg.charDimAlpha       : 0.4f;
            float defDimDur     = cfg != null ? cfg.charDimDuration    : 0.3f;
            float defGlitchStr  = cfg != null ? cfg.charGlitchStrength : 1.0f;
            float defGlitchDur  = cfg != null ? cfg.charGlitchDuration : 0.6f;

            switch (effect)
            {
                case "CharShake":
                {
                    float strength = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float s) ? s : defShakeStr;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : defShakeDur;
                    await slot.ShakeAsync(strength, duration, ct);
                    break;
                }
                case "CharJump":
                {
                    float height = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float h) ? h : defJumpH;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : defJumpDur;
                    await slot.JumpAsync(height, duration, ct);
                    break;
                }
                case "CharDim":
                {
                    float alpha = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float a) ? a : defDimAlpha;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : defDimDur;
                    await slot.DimAsync(alpha, duration, ct);
                    break;
                }
                case "CharGlitch":
                {
                    float strength = parts.Length > paramOffset && float.TryParse(parts[paramOffset], out float s) ? s : defGlitchStr;
                    float duration = parts.Length > paramOffset + 1 && float.TryParse(parts[paramOffset + 1], out float d) ? d : defGlitchDur;
                    await slot.GlitchAsync(strength, duration, ct);
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
